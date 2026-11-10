using System;
using System.Collections;
using System.Reflection;
using BepInEx;
using RoR2;
using UnityEngine;
using UnityEngine.UI;

[BepInPlugin("local.r2mod.visualsmoke", "Combat Visual Smoke", "1.0.0")]
[BepInDependency("local.r2mod.riftsurvivor")]
public partial class Smoke : BaseUnityPlugin
{
    const BindingFlags Flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    Assembly Mod => typeof(RiftSurvivor.Plugin).Assembly;
    Type T(string name) => Mod.GetType("RiftSurvivor." + name, true);
    object Call(string type, string method, params object[] args) => T(type).GetMethod(method, Flags).Invoke(null, args);
    void Start() { StartCoroutine(Check()); }
    void CheckOne(string name, Action action)
    {
        try { action(); Logger.LogInfo("SMOKE PASS " + name); }
        catch (Exception e) { Logger.LogError("SMOKE FAIL " + name + " " + e); }
    }
    void CheckChargeTargeting(CharacterBody attacker, HealthComponent victim)
    {
        attacker.transform.position = new Vector3(1000f, 1000f, 1000f);
        victim.transform.position = attacker.transform.position + Vector3.forward * 12f;
        Physics.SyncTransforms();
        // Fresh fixtures have not run the first RecalculateStats/Start yet.
        typeof(CharacterBody).GetProperty("visionDistance", Flags).SetValue(attacker, 60f);
        var hurt = victim.body.mainHurtBox;
        var aim = new Ray(attacker.corePosition, (hurt.transform.position - attacker.corePosition).normalized);
        bool Valid(Ray ray, float range = 30f) => (bool)Call("ShoulderCharge", "ValidTarget", attacker, ray, hurt, range, 0f);
        if (!Valid(aim)) throw new Exception("Visible forward enemy rejected");
        var selected = (HurtBox)Call("ShoulderCharge", "FindTarget", attacker, aim, 30f);
        if (!selected || selected.healthComponent != victim) throw new Exception("Forward enemy not locked: vision=" + attacker.visionDistance + " bullseyes=" + HurtBox.readOnlyBullseyesList.Count + " targetTeam=" + hurt.teamIndex);
        if (Valid(new Ray(aim.origin, -aim.direction))) throw new Exception("Enemy behind crosshair accepted");
        if (Valid(aim, 2f)) throw new Exception("Out-of-range enemy accepted");
        GameObject wall = null;
        try {
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.layer = LayerIndex.world.intVal;
            wall.transform.position = Vector3.Lerp(aim.origin, hurt.transform.position, .5f);
            wall.transform.localScale = Vector3.one * 3f;
            Physics.SyncTransforms();
            if (Valid(aim)) throw new Exception("Target locked through a wall");
        } finally { if (wall) DestroyImmediate(wall); }
        victim.Networkhealth = 0f;
        if (Valid(aim)) throw new Exception("Dead enemy accepted");
        if ((HurtBox)Call("ShoulderCharge", "FindTarget", attacker, aim, 30f)) throw new Exception("Empty search should leave charge unlocked");
        victim.Networkhealth = 100f;
    }

    IEnumerator Check()
    {
        yield return null;
        CheckOne("Addressable shaders", () => {
            Call("CombatVisuals", "Initialize");
            foreach (var property in new[] { "VertexShader", "TextureShader" }) {
                var shader = (Shader)T("CombatVisuals").GetProperty(property, Flags).GetValue(null);
                if (!shader.isSupported) throw new Exception("Unsupported shader " + shader.name);
            }
        });
        CheckOne("Additive skill HUD preserves vanilla icon", () => {
            var root = new GameObject("SmokeSkillIcon", typeof(RectTransform)); root.SetActive(false);
            var icon = root.AddComponent<RoR2.UI.SkillIcon>();
            var imageObject = new GameObject("OriginalIcon", typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(root.transform, false);
            icon.iconImage = imageObject.GetComponent<Image>();
            icon.iconImage.color = Color.gray;
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);
            icon.iconImage.sprite = sprite;
            var originalChildren = imageObject.transform.childCount;
            var label = (Text)T("CombatStatusHud").GetMethod("CreateBadge", Flags).Invoke(null, new object[] { icon });
            var rect = label.rectTransform;
            if (!label.font || label.raycastTarget || label.supportRichText) throw new Exception("Badge font/input setup failed");
            if (rect.parent != imageObject.transform || rect.anchorMin.y != 1f || rect.pivot.y != 0f || rect.anchoredPosition.y < 0f)
                throw new Exception("Badge overlaps vanilla icon");
            if (imageObject.transform.childCount != originalChildren + 1 || imageObject.GetComponentsInChildren<Image>(true).Length != 1)
                throw new Exception("Unexpected overlay panel");
            if (icon.iconImage.sprite != sprite || icon.iconImage.color != Color.gray || !icon.iconImage.enabled)
                throw new Exception("Vanilla icon modified");
            if (T("CombatStatusHud").GetMethod("CreateBadge", Flags).Invoke(null, new object[] { root.AddComponent<RoR2.UI.SkillIcon>() }) != null)
                throw new Exception("Unbound icon should not create a badge");
            Destroy(root); Destroy(sprite);
        });
        CheckOne("Visible sword counter", () => {
            var buff = (BuffDef)T("JieSwords").GetField("bladeCount", Flags).GetValue(null);
            if (!buff || buff.isHidden || !buff.iconSprite || !buff.canStack) throw new Exception("Sword count is hidden or lacks icon");
        });
        CheckOne("Azure sword mesh and trail", () => {
            var go = new GameObject("SmokeSword");
            go.AddComponent<RiftSurvivor.JieAzureSword>().OnStartClient();
            var render = go.GetComponentInChildren<MeshRenderer>();
            if (!render || !render.sharedMaterial.shader) throw new Exception("Sword renderer missing");
            Logger.LogInfo("SMOKE sword shader=" + render.sharedMaterial.shader.name + " color=" + render.sharedMaterial.HasProperty("_Color"));
            Destroy(go);
        });
        CheckOne("Gold seal", () => Destroy((GameObject)Call("JieStormEffects", "MakeSeal")));
        CheckOne("Seeker mesh and trail", () => {
            var go = new GameObject("SmokeSeeker");
            Call("XiaoheiRoundArt", "Attach", go.transform, 85f);
            if (!go.GetComponent<MeshRenderer>()) throw new Exception("Seeker renderer missing");
            Destroy(go);
        });
        CheckOne("Chinese HUD font", () => {
            var f = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 20);
            f.RequestCharactersInTexture("杰哥小黑泵感燃脂万剑归宗", 20, FontStyle.Normal);
            if (!f.HasCharacter('杰')) throw new Exception("Chinese glyph missing");
            Destroy(f);
        });
        foreach (var name in new[] { "XiaoheiPistolStreak", "XiaoheiReplayStreak", "XiaoheiAmberRound", "JieGoldSwordBolt", "JieFallingHeavenSword" })
        {
            CheckOne("Effect " + name, () => {
                var field = name == "XiaoheiPistolStreak" ? "PistolTracer" : name == "XiaoheiReplayStreak" ? "EchoTracer" : name == "XiaoheiAmberRound" ? "SeekerEffect" : name == "JieGoldSwordBolt" ? "bolt" : "heaven"; var prefab = (GameObject)T(name.StartsWith("Xiaohei") ? "XiaoheiCombat" : "JieStormEffects").GetField(field, Flags).GetValue(null); 
                if (!prefab) throw new Exception("Effect prefab missing");
                
                var go = Instantiate(prefab);
                var effect = go.GetComponent<EffectComponent>();
                effect.effectData = new EffectData { origin = new Vector3(0,0,20), start = new Vector3(0,0,0), scale = 8f, genericFloat = .3f };
                var component = go.GetComponent(T(name.StartsWith("Xiaohei") ? "XiaoheiShotVisual" : "JieStormVisual"));
                component.GetType().GetMethod("Start", Flags).Invoke(component, null);
                if (!go.GetComponentInChildren<Renderer>()) throw new Exception("Effect renderer missing");
                Destroy(go);
            });
        }
        // CharacterModel's native OnDisable reads the elite catalogs. Lobby
        // displays must be exercised after those globals have initialized.
        Logger.LogInfo("SMOKE WAITING FOR CONTENT");
        float deadline = Time.realtimeSinceStartup + 120f;
        while (!RoR2Content.Elites.Poison || !RoR2Content.Elites.Haunted || !RoR2Content.Elites.Lunar || !DLC1Content.Elites.Void || RoR2Content.Elites.Poison.eliteIndex == EliteIndex.None)
        {
            if (Time.realtimeSinceStartup > deadline) { Logger.LogError("SMOKE FAIL Content initialization timed out"); Application.Quit(); yield break; }
            yield return null;
        }
        foreach (var id in new[] { "RiftRunner", "Xiaohei", "JieGe", "WeiGe" })
        {
            GameObject instance = null;
            var definition = Array.Find(Resources.FindObjectsOfTypeAll<SurvivorDef>(), survivor => survivor.cachedName == id);
            CheckOne("Own display " + id, () => {
                if (!definition || !definition.displayPrefab) throw new Exception("Survivor display missing");
                var prefab = definition.displayPrefab;
                if (prefab.GetComponentInChildren<Animator>(true)) throw new Exception("Borrowed animator remains");
                if (prefab.GetComponentInChildren<ModelSkinController>(true)) throw new Exception("Borrowed skin controller remains");
                foreach (var child in prefab.GetComponentsInChildren<Transform>(true))
                    if (child.name.StartsWith("GunMesh", StringComparison.Ordinal)) throw new Exception("Borrowed gun remains");
                instance = Instantiate(prefab);
                DontDestroyOnLoad(instance); // Scene initialization may finish between the two lifecycle samples.
                var renderer = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                if (!renderer || !renderer.sharedMesh || !renderer.sharedMaterial || !renderer.rootBone || renderer.bones.Length < 15)
                    throw new Exception("Authored appearance not bound synchronously in Awake");
                if (renderer.sharedMesh.name.Contains("Commando")) throw new Exception("Borrowed mesh remains");
                Logger.LogInfo("SMOKE display " + id + " mesh=" + renderer.sharedMesh.name + " bones=" + renderer.bones.Length + " scale=" + renderer.rootBone.localScale);
            });
            yield return null;
            CheckOne("Own display after first frame " + id, () => {
                if (!instance) throw new Exception("Display instance missing");
                var renderer = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                if (!renderer || !renderer.sharedMesh || renderer.sharedMesh.name.Contains("Commando"))
                    throw new Exception("Appearance replaced after Awake");
            });
            if (instance) Destroy(instance);
            CheckOne("Hidden original body gear " + id, () => {
                if (!definition || !definition.bodyPrefab) throw new Exception("Body prefab missing");
                var body = definition.bodyPrefab;
                if (body.GetComponentInChildren<ModelSkinController>(true)) throw new Exception("Borrowed body skin remains");
                foreach (var renderer in body.GetComponentsInChildren<Renderer>(true))
                    if (renderer.name.StartsWith("GunMesh", StringComparison.Ordinal) && (renderer.enabled || !renderer.forceRenderingOff))
                        throw new Exception("Borrowed gun visible on prefab");
                var animator = body.GetComponentInChildren<Animator>(true);
                if (!animator || !animator.runtimeAnimatorController || !animator.avatar) throw new Exception("Gameplay animation assets missing");
            });
        }
        CheckOne("Charge targeting, direct skill kill reset and server charge ledger", () => {
            bool wasServer = UnityEngine.Networking.NetworkServer.active;
            GameObject attackerObject = null, victimObject = null;
            try {
                if (!wasServer) {
                    UnityEngine.Networking.NetworkServer.dontListen = true;
                    if (!UnityEngine.Networking.NetworkServer.Listen(0)) throw new Exception("Test server unavailable");
                }
                foreach (var id in new[] { "Xiaohei", "JieGe", "RiftRunner" }) {
                    var def = Array.Find(Resources.FindObjectsOfTypeAll<SurvivorDef>(), d => d.cachedName == id);
                    attackerObject = Instantiate(def.bodyPrefab);
                    victimObject = Instantiate(BodyCatalog.FindBodyPrefab("BeetleBody"));
                    var attacker = attackerObject.GetComponent<CharacterBody>();
                    var victim = victimObject.GetComponent<HealthComponent>();
                    attacker.teamComponent.teamIndex = TeamIndex.Player;
                    victim.body.teamComponent.teamIndex = TeamIndex.Monster;
                    attacker.healthComponent.Networkhealth = 100f;
                    victim.Networkhealth = 100f;
                    if (id == "RiftRunner") CheckChargeTargeting(attacker, victim);
                    var reset = attackerObject.GetComponent(T("DirectSkillKillReset"));
                    if (!reset) throw new Exception(id + " reset component missing");
                    var pending = T("DirectSkillKillReset").GetField("pending", Flags);
                    var slot = id == "RiftRunner" ? attacker.skillLocator.utility : attacker.skillLocator.secondary;
                    var otherSlot = id == "RiftRunner" ? attacker.skillLocator.secondary : attacker.skillLocator.utility;
                    otherSlot.stock = 0;
                    var ledger = attackerObject.AddComponent(T("CuiServerCharges"));
                    var spend = T("CuiServerCharges").GetMethod("TrySpendRemote", Flags);
                    slot.stock = slot.maxStock;
                    if (!(bool)spend.Invoke(ledger, new object[] {slot, Time.fixedTimeAsDouble})) throw new Exception("Initial spend rejected");
                    slot.stock = 0; slot.rechargeStopwatch = 1.25f;
                    var direct = new DamageInfo { attacker = attackerObject, damage = 550f, damageType = default };
                    Call("DirectSkillKillReset", "Track", direct, attacker);
                    // Same attacker/source, different DamageInfo: a nested item proc must not reset.
                    var proc = new DamageInfo { attacker = attackerObject, damage = 550f, damageType = default };
                    Call("DirectSkillKillReset", "OnDeath", new DamageReport(proc, victim, 550f, 100f));
                    if ((bool)pending.GetValue(reset)) throw new Exception("Indirect kill reset cooldown");
                    victim.body.teamComponent.teamIndex = TeamIndex.Player;
                    Call("DirectSkillKillReset", "OnDeath", new DamageReport(direct, victim, 550f, 100f));
                    if ((bool)pending.GetValue(reset)) throw new Exception("Friendly kill reset cooldown");
                    victim.body.teamComponent.teamIndex = TeamIndex.Monster;
                    Call("DirectSkillKillReset", "OnDeath", new DamageReport(direct, victim, 550f, 100f));
                    if (!(bool)pending.GetValue(reset)) throw new Exception("Direct enemy kill did not queue reset");
                    if (slot.stock != 0) throw new Exception("Reset ran before original cast finished");
                    T("DirectSkillKillReset").GetMethod("LateUpdate", Flags).Invoke(reset, null);
                    if (slot.stock != slot.maxStock || slot.rechargeStopwatch != 0f || (bool)pending.GetValue(reset))
                        throw new Exception("Stock/timer not reset");
                    if (otherSlot.stock != 0) throw new Exception("Reset affected the wrong skill slot");
                    if (!(bool)spend.Invoke(ledger, new object[] {slot, Time.fixedTimeAsDouble}))
                        throw new Exception("Server ledger rejected reset charge");
                    Call("DirectSkillKillReset", "Forget", direct);
                    pending.SetValue(reset, false);
                    Call("DirectSkillKillReset", "OnDeath", new DamageReport(direct, victim, 550f, 100f));
                    if ((bool)pending.GetValue(reset)) throw new Exception("Expired damage still resets");
                    DestroyImmediate(attackerObject); DestroyImmediate(victimObject);
                    attackerObject = victimObject = null;
                }
            } finally {
                if (attackerObject) DestroyImmediate(attackerObject);
                if (victimObject) DestroyImmediate(victimObject);
                if (!wasServer) { UnityEngine.Networking.NetworkServer.Shutdown(); UnityEngine.Networking.NetworkServer.dontListen = false; }
            }
        });
        CheckOne("Jie crosshair flight and balanced gravity", () => {
            var def = Array.Find(Resources.FindObjectsOfTypeAll<SurvivorDef>(), d => d.cachedName == "JieGe");
            var go = Instantiate(def.bodyPrefab);
            try {
                var body = go.GetComponent<CharacterBody>();
                var input = body.inputBank;
                var aim = new Vector3(0f, .6f, .8f);
                input.aimDirection = aim; input.rawMoveData = new Vector2(0f, 1f);
                Vector3 Direction() => (Vector3)Call("JieFlight", "FlightDirection", body);
                if (Vector3.Distance(Direction(), aim) > .001f) throw new Exception("Forward flight ignores pitch");
                input.rawMoveData = new Vector2(0f, -1f);
                if (Vector3.Distance(Direction(), -aim) > .001f) throw new Exception("Backward flight ignores pitch");
                input.rawMoveData = new Vector2(1f, 0f);
                if (Mathf.Abs(Direction().y) > .001f || Direction().magnitude < .99f) throw new Exception("Strafe is not horizontal");
                input.rawMoveData = Vector2.one;
                if (Direction().magnitude > 1.001f) throw new Exception("Diagonal speed exceeds flight speed");
                input.rawMoveData = Vector2.zero;
                if (Direction().sqrMagnitude > .0001f) throw new Exception("Released input does not hover");
                input.aimDirection = Vector3.up; input.rawMoveData = Vector2.right;
                if (Direction().magnitude < .99f) throw new Exception("Vertical aim loses strafe");
                var motor = body.characterMotor;
                int gravity = motor.gravityParameters.channeledAntiGravityGranterCount;
                int flying = motor.flightParameters.channeledFlightGranterCount;
                var flight = go.GetComponent<RiftSurvivor.JieFlight>();
                var toggle = T("JieFlight").GetMethod("SetAntiGravity", Flags);
                toggle.Invoke(flight, new object[] {true}); toggle.Invoke(flight, new object[] {true});
                if (motor.flightParameters.channeledFlightGranterCount != flying + 1) throw new Exception("Flight grant is unbalanced");
                toggle.Invoke(flight, new object[] {false});
                if (motor.gravityParameters.channeledAntiGravityGranterCount != gravity || motor.flightParameters.channeledFlightGranterCount != flying)
                    throw new Exception("Flight/gravity counters not restored");
                if (body.skillLocator.utility.skillDef.baseRechargeInterval != 2f) throw new Exception("Blink cooldown is not 2 seconds");
            } finally { DestroyImmediate(go); }
        });
        CheckOne("Jie instant blink capsule collision", () => {
            var def = Array.Find(Resources.FindObjectsOfTypeAll<SurvivorDef>(), d => d.cachedName == "JieGe");
            var go = Instantiate(def.bodyPrefab); GameObject wall = null;
            try {
                var motor = go.GetComponent<CharacterMotor>();
                var start = new Vector3(1000f, 1000f, 1000f);
                motor.Motor.SetPosition(start, true); Physics.SyncTransforms();
                Vector3 Destination(Vector3 direction, bool expected) {
                    object[] args = {motor, direction, 16f, Vector3.zero};
                    bool result = (bool)Call("JieBlink", "TryDestination", args);
                    if (result != expected) throw new Exception("Unexpected blink validity " + result + " direction=" + direction);
                    return (Vector3)args[3];
                }
                if (Vector3.Distance(Destination(Vector3.forward, true), start + Vector3.forward * 16f) > .01f)
                    throw new Exception("Unblocked blink distance differs");
                if (Destination(new Vector3(0f, 1f, 1f), true).y < start.y + 10f) throw new Exception("Upward blink lost pitch");
                Destination(Vector3.zero, false);
                wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.layer = LayerIndex.world.intVal;
                wall.transform.localScale = new Vector3(10f, 10f, 1f); wall.transform.position = start + Vector3.forward * 8f;
                Physics.SyncTransforms();
                float travel = Destination(Vector3.forward, true).z - start.z;
                if (travel < 5f || travel >= 7.5f) throw new Exception("Blink did not stop before wall " + travel);
                wall.transform.position = start; Physics.SyncTransforms();
                Destination(Vector3.forward, false);
                if (Vector3.Distance(go.transform.position, start) > .01f) throw new Exception("Collision probe moved the body");
            } finally { if (wall) DestroyImmediate(wall); DestroyImmediate(go); }
        });
        GameObject blinkEffect = null;
        CheckOne("Jie blink sword light and rings", () => {
            blinkEffect = (GameObject)Call("JieBlinkVisual", "Show", Vector3.zero, Vector3.forward * 16f);
            if (blinkEffect.GetComponentsInChildren<LineRenderer>().Length < 17 || blinkEffect.GetComponentsInChildren<MeshRenderer>().Length != 4)
                throw new Exception("Blink lights/sword echoes missing");
            foreach (var render in blinkEffect.GetComponentsInChildren<Renderer>())
                if (!render.sharedMaterial || !render.sharedMaterial.shader.isSupported) throw new Exception("Unsupported blink material");
        });
        yield return new WaitForSeconds(.7f);
        CheckOne("Jie blink effect cleanup", () => { if (blinkEffect) throw new Exception("Blink effect did not expire"); });
        CheckOne("Lobby materials stay separate and preserve emission", () => {
            foreach (var id in new[] { "RiftRunner", "Xiaohei", "JieGe", "WeiGe" }) {
                var def = Array.Find(Resources.FindObjectsOfTypeAll<SurvivorDef>(), d => d.cachedName == id);
                var display = Instantiate(def.displayPrefab);
                try {
                    var renderer = display.GetComponentInChildren<SkinnedMeshRenderer>();
                    var material = renderer.sharedMaterial;
                    if (!material.name.EndsWith("LobbyFill")) throw new Exception(id + " has no lobby fill");
                    var source = (Material)Call("SurvivorDisplay", "SourceMaterial", material);
                    if (source == material || (Material)Call("SurvivorDisplay", "DisplayMaterial", source, null) != material)
                        throw new Exception("Display cache not reused");
                    var body = def.bodyPrefab.GetComponent<CharacterBody>();
                    if ((Material)Call("SurvivorDisplay", "DisplayMaterial", source, body) != source)
                        throw new Exception("Gameplay material changed");
                    var before = ((Texture2D)source.GetTexture("_EmTex")).GetPixels32();
                    var after = ((Texture2D)material.GetTexture("_EmTex")).GetPixels32();
                    bool brighter = false;
                    for (int i = 0; i < after.Length; i++) {
                        if (after[i].r < before[i].r || after[i].g < before[i].g || after[i].b < before[i].b)
                            throw new Exception("Original emission dimmed");
                        brighter |= after[i].r > before[i].r || after[i].g > before[i].g || after[i].b > before[i].b;
                    }
                    if (!brighter) throw new Exception(id + " lobby material was not lifted");
                } finally { DestroyImmediate(display); }
            }
        });
        yield return CheckSpectacle();
        CheckCuiRework();
        CheckHandGlow();
        CheckSwordReserve();
        yield return CheckVfxPolish();
        yield return CheckEchoAndLanding();
        yield return CheckRequestedVfx();
        CheckReplaySentry();
        CheckDownedRescue();
        foreach (var argument in Environment.GetCommandLineArgs())
            if (argument.StartsWith("--lobby-preview-out=", StringComparison.Ordinal))
                yield return LobbyPreview.Capture(Mod, argument.Substring("--lobby-preview-out=".Length),
                    (name, action) => CheckOne(name, action));
        foreach (var argument in Environment.GetCommandLineArgs())
            if (argument.StartsWith("--art-preview-out=", StringComparison.Ordinal))
                CheckOne("Weapon art preview", () => ArtPreview.Capture(Mod, argument.Substring("--art-preview-out=".Length)));
        foreach (var argument in Environment.GetCommandLineArgs())
            if (argument.StartsWith("--character-preview-out=", StringComparison.Ordinal))
                CheckOne("Character lighting preview", () => ArtPreview.Capture(Mod, argument.Substring("--character-preview-out=".Length), true));
        foreach (var argument in Environment.GetCommandLineArgs())
            if (argument.StartsWith("--spectacle-preview-out=", StringComparison.Ordinal))
                CheckOne("Spectacle rendered preview", () => ArtPreview.Capture(Mod, argument.Substring("--spectacle-preview-out=".Length),false,true));
        foreach (var argument in Environment.GetCommandLineArgs())
            if (argument.StartsWith("--vfx-sequence-out=", StringComparison.Ordinal))
                CheckOne("Combat VFX multi-frame GPU preview", () => VfxSequencePreview.Capture(Mod, argument.Substring("--vfx-sequence-out=".Length)));
        foreach (var argument in Environment.GetCommandLineArgs())
            if (argument.StartsWith("--hand-preview-out=", StringComparison.Ordinal))
                CheckOne("Actual hand glow animation GPU preview", () => HandPresentationPreview.Capture(Mod, argument.Substring("--hand-preview-out=".Length)));
        foreach (var argument in Environment.GetCommandLineArgs())
            if (argument.StartsWith("--sword-preview-out=", StringComparison.Ordinal))
                CheckOne("Nine sword reserve and local avatar GPU preview", () => SwordPresentationPreview.Capture(Mod, argument.Substring("--sword-preview-out=".Length)));
        Logger.LogInfo("SMOKE COMPLETE");
        yield return null;
        Application.Quit();
    }
}



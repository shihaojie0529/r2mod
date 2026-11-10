using System.Collections.Generic;
using R2API;
using RoR2;
using RoR2.UI;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using static RiftSurvivor.SurvivorLanguage;

namespace RiftSurvivor
{
    // A lethal hit on a player leaves them downed while a teammate can still reach them.
    // The server owns every decision. Clients only read the two hidden buff counts,
    // and a bleed-out finishes through vanilla death so extra lives and game over still apply.
    [DisallowMultipleComponent]
    public sealed class DownedRescue : MonoBehaviour
    {
        internal const float Range = 8f, ReviveHealth = .4f, ReviveGrace = 2f, CrawlSpeed = .15f;
        private const float LethalHealth = 1.01f, PublishInterval = .1f, TextInterval = .1f, DeathWindow = 1f;
        private const int RingSegments = 64;

        private static readonly Color DownedColor = new Color(1f, .28f, .22f), RevivedColor = new Color(.35f, 1f, .55f);
        private static BuffDef secondsBuff, progressBuff;
        private static Material ringMaterial;

        private readonly Dictionary<HUD, Text> labels = new Dictionary<HUD, Text>();
        private CharacterBody body;
        private HealthComponent health;
        private LineRenderer ring;
        private RescueClock clock;
        private GameObject lastAttacker, lastInflictor;
        private int downs;
        private bool downed, interruptedAuthority;
        private float graceEnds, deathEnds, nextPublish, nextText;

        internal static void Register()
        {
            secondsBuff = HiddenCount("DownedRescueSeconds");
            progressBuff = HiddenCount("DownedRescueProgress");
            Add("RIFT_DOWNED", "{0} is down! Get close to revive them.", "{0} 倒地了！靠近 8 米内救援。", "{0} 倒地了！靠近 8 公尺內救援。");
            Add("RIFT_REVIVED", "{0} is back on their feet.", "{0} 被拉起来了。", "{0} 被拉起來了。");

            RoR2Application.onFixedUpdate += AttachToPlayers;
            On.RoR2.HealthComponent.TakeDamageProcess += (orig, self, info) => GuardHit(orig, self, info);
            On.RoR2.CharacterBody.RecalculateStats += (orig, owner) =>
            {
                orig(owner);
                if (IsDowned(owner)) owner.moveSpeed *= CrawlSpeed;
            };
            On.RoR2.GenericSkill.CanExecute += (orig, self) => !IsDowned(self.characterBody) && orig(self);
            On.RoR2.GenericSkill.ExecuteIfReady += (orig, self) => !IsDowned(self.characterBody) && orig(self);
            On.RoR2.EquipmentSlot.ExecuteIfReady += (orig, self) => !IsDowned(self.characterBody) && orig(self);
            On.RoR2.Interactor.PerformInteraction += (orig, self, target) =>
            {
                if (!IsDowned(self.GetComponent<CharacterBody>())) orig(self, target);
            };
        }

        private static BuffDef HiddenCount(string name)
        {
            var buff = ScriptableObject.CreateInstance<BuffDef>();
            buff.name = name;
            buff.canStack = buff.isHidden = buff.ignoreGrowthNectar = true;
            if (!ContentAddition.AddBuffDef(buff)) throw new System.InvalidOperationException(name);
            return buff;
        }

        internal static bool IsDowned(CharacterBody target) => target && secondsBuff && target.GetBuffCount(secondsBuff) > 0;

        // Alive and able to act. Use this instead of healthComponent.alive for anything
        // that a downed player should no longer be doing.
        internal static bool IsUp(CharacterBody target) =>
            target && target.healthComponent && target.healthComponent.alive && !IsDowned(target);

        // The Cui Ge team heal also pushes every downed teammate toward being revived.
        internal static void ApplySupply(CharacterBody caster, float amount)
        {
            if (!NetworkServer.active || !IsUp(caster)) return;
            var players = PlayerCharacterMasterController.instances;
            for (int i = 0; i < players.Count; i++)
            {
                var target = players[i].master ? players[i].master.GetBody() : null;
                var rescue = target ? target.GetComponent<DownedRescue>() : null;
                if (rescue && rescue.downed) rescue.clock.Supply(amount);
            }
        }

        // Polling also covers clients, where a body can start before its master is resolved.
        private static void AttachToPlayers()
        {
            if (!Run.instance) return;
            var players = PlayerCharacterMasterController.instances;
            for (int i = 0; i < players.Count; i++)
            {
                var target = players[i].master ? players[i].master.GetBody() : null;
                if (target && !target.GetComponent<DownedRescue>()) target.gameObject.AddComponent<DownedRescue>();
            }
        }

        private static void GuardHit(On.RoR2.HealthComponent.orig_TakeDamageProcess orig, HealthComponent self, DamageInfo info)
        {
            var rescue = NetworkServer.active && info != null && self.body ? self.body.GetComponent<DownedRescue>() : null;
            if (!rescue || Time.fixedTime < rescue.deathEnds) { orig(self, info); return; }
            if (rescue.downed || Time.fixedTime < rescue.graceEnds) { info.rejected = true; return; }

            bool guarded = self.alive && rescue.CanGoDown();
            if (guarded) info.damageType |= DamageType.NonLethal;
            orig(self, info);
            if (!guarded) return;
            if (!self.alive) Debug.LogWarning("[DownedRescue] NonLethal did not stop a lethal hit; the player died normally.");
            else if (self.combinedHealth <= LethalHealth) rescue.EnterDowned(info);
        }

        private void Awake()
        {
            body = GetComponent<CharacterBody>();
            health = body ? body.healthComponent : null;
        }

        private bool CanGoDown() => downs < RescueClock.MaxDowns && AnyoneElseUp();

        private bool AnyoneElseUp()
        {
            var players = PlayerCharacterMasterController.instances;
            for (int i = 0; i < players.Count; i++)
            {
                var master = players[i].master;
                if (!master) continue;
                var other = master.GetBody();
                if (other == body) continue;
                if (IsUp(other) || master.IsExtraLifePendingServer()) return true;
            }
            return false;
        }

        private int CountRescuers()
        {
            int count = 0;
            var players = PlayerCharacterMasterController.instances;
            for (int i = 0; i < players.Count; i++)
            {
                var other = players[i].master ? players[i].master.GetBody() : null;
                if (other != body && IsUp(other) &&
                    (other.corePosition - body.corePosition).sqrMagnitude <= Range * Range) count++;
            }
            return count;
        }

        private void EnterDowned(DamageInfo info)
        {
            downs++;
            downed = true;
            clock = RescueClock.Start(downs);
            lastAttacker = info.attacker;
            lastInflictor = info.inflictor;
            nextPublish = 0f;
            Publish();
            Announce("RIFT_DOWNED");
        }

        private void FixedUpdate()
        {
            if (!body || !health) return;
            if (NetworkServer.active && downed) ServerTick();
            if (body.hasEffectiveAuthority) AuthorityTick();
        }

        private void ServerTick()
        {
            if (!health.alive) { ClearDowned(); return; }
            // With nobody left standing there is no rescue to wait for.
            if (!AnyoneElseUp()) { BleedOut(); return; }
            var step = clock.Tick(Time.fixedDeltaTime, CountRescuers());
            if (step == RescueStep.Rescued) Revive();
            else if (step == RescueStep.BledOut) BleedOut();
            else Publish();
        }

        private void Publish()
        {
            if (Time.fixedTime < nextPublish) return;
            nextPublish = Time.fixedTime + PublishInterval;
            SetCount(secondsBuff, Mathf.Clamp(Mathf.CeilToInt(clock.Remaining), 1, 30));
            SetCount(progressBuff, Mathf.Clamp(Mathf.FloorToInt(clock.Progress * 100f), 0, 100));
        }

        private void SetCount(BuffDef buff, int count)
        {
            if (body.GetBuffCount(buff) != count) body.SetBuffCount(buff.buffIndex, count);
        }

        private void ClearDowned()
        {
            downed = false;
            SetCount(secondsBuff, 0);
            SetCount(progressBuff, 0);
        }

        private void Revive()
        {
            ClearDowned();
            graceEnds = Time.fixedTime + ReviveGrace;
            health.Networkhealth = Mathf.Max(health.health, health.fullHealth * ReviveHealth);
            health.ForceShieldRegen();
            SkillSpectacle.Emit(SpectacleKind.Supply, body.corePosition, Vector3.up, 2.5f);
            Announce("RIFT_REVIVED");
        }

        private void BleedOut()
        {
            ClearDowned();
            deathEnds = Time.fixedTime + DeathWindow;
            health.Suicide(lastAttacker, lastInflictor, DamageType.Generic);
        }

        private void Announce(string token) => Chat.SendBroadcastChat(new Chat.SimpleChatMessage
        {
            baseToken = token,
            paramTokens = new[] { body.GetUserName() }
        });

        // Skill states and jumps run on the owning client, so they are stopped there.
        private void AuthorityTick()
        {
            if (!IsDowned(body)) { interruptedAuthority = false; return; }
            if (!interruptedAuthority)
            {
                interruptedAuthority = true;
                foreach (var machine in GetComponents<EntityStateMachine>())
                    if (machine.state != null && !machine.IsInMainState()) machine.SetNextStateToMain();
            }
            body.isSprinting = false;
            if (body.characterMotor) body.characterMotor.jumpCount = body.maxJumpCount;
        }

        private void LateUpdate()
        {
            if (!NetworkClient.active || !body) return;
            bool show = health && health.alive && IsDowned(body);
            float progress = show ? body.GetBuffCount(progressBuff) * .01f : 0f;
            UpdateRing(show, progress);
            if (!show) { ClearLabels(); return; }

            bool refreshText = Time.unscaledTime >= nextText;
            if (refreshText) nextText = Time.unscaledTime + TextInterval;
            foreach (var hud in HUD.instancesList)
            {
                var view = hud && hud.cameraRigController ? hud.cameraRigController.sceneCam : null;
                if (!view || !hud.mainContainer) continue;
                bool fresh = !labels.TryGetValue(hud, out var label) || !label;
                if (fresh) labels[hud] = label = CreateLabel(hud.mainContainer.transform);
                Place(label, view);
                if (refreshText || fresh) Describe(label, hud, view, progress);
            }
        }

        private void Describe(Text label, HUD hud, Camera view, float progress)
        {
            string state = progress > 0f ? $"救援中 {progress * 100f:0}%" : $"倒地 {body.GetBuffCount(secondsBuff)}s";
            string detail = hud.targetBodyObject == gameObject ? "等待队友靠近"
                : $"{body.GetUserName()} · {Vector3.Distance(view.transform.position, body.corePosition):0} 米";
            label.text = state + "\n" + detail;
            label.color = Color.Lerp(DownedColor, RevivedColor, progress);
        }

        // Off-screen teammates stay pinned to the screen edge, so the label also points the way.
        private void Place(Text label, Camera view)
        {
            var point = view.WorldToViewportPoint(body.corePosition + Vector3.up * 2f);
            var fromCenter = new Vector2(point.x - .5f, point.y - .5f);
            if (point.z < 0f) fromCenter = -fromCenter;
            float reach = Mathf.Max(Mathf.Abs(fromCenter.x), Mathf.Abs(fromCenter.y));
            if (reach > .44f || point.z < 0f) fromCenter *= .44f / Mathf.Max(reach, .001f);
            var anchor = new Vector2(.5f, .5f) + fromCenter;
            var rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = Vector2.zero;
        }

        private static Text CreateLabel(Transform parent)
        {
            var textObject = new GameObject("DownedRescueLabel", typeof(RectTransform), typeof(Text), typeof(Outline));
            textObject.transform.SetParent(parent, false);
            var rect = (RectTransform)textObject.transform;
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(240f, 52f);
            var label = textObject.GetComponent<Text>();
            label.font = CombatStatusHud.LabelFont();
            label.fontSize = 18;
            label.supportRichText = false;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            textObject.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, .9f);
            return label;
        }

        private void ClearLabels()
        {
            if (labels.Count == 0) return;
            foreach (var label in labels.Values)
                if (label) Destroy(label.gameObject);
            labels.Clear();
        }

        private void UpdateRing(bool show, float progress)
        {
            if (!show) { if (ring) ring.enabled = false; return; }
            if (!ring) ring = CreateRing();
            ring.enabled = true;
            ring.transform.SetPositionAndRotation(body.footPosition + Vector3.up * .15f, Quaternion.identity);
            ring.startColor = ring.endColor = Color.Lerp(DownedColor, RevivedColor, progress);
        }

        private LineRenderer CreateRing()
        {
            if (!ringMaterial) ringMaterial = CombatVisuals.Stroke("DownedRescueRing");
            var ringObject = new GameObject("DownedRescueRing");
            ringObject.transform.SetParent(transform, false);
            var line = ringObject.AddComponent<LineRenderer>();
            line.sharedMaterial = ringMaterial;
            line.useWorldSpace = false;
            line.loop = true;
            line.widthMultiplier = .22f;
            line.positionCount = RingSegments;
            for (int i = 0; i < RingSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / RingSegments;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * Range, 0f, Mathf.Sin(angle) * Range));
            }
            return line;
        }

        private void OnDestroy() => ClearLabels();
    }
}

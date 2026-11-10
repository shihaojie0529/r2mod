using System;
using System.Collections.Generic;
using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace RiftSurvivor
{
    // Order is the network id; append only.
    internal enum NarutoEffect : uint
    {
        None, HitSpark, Impact, Rasengan, TailedBeastBomb, Crater, Slam, Roar, Chidori, Kirin, BoltImpact,
        SasukeSpark, MinatoSpark, SenkoSlash, SixPathsSpark, IndraBurst, GoldBurst, Shatter, Cut, ClawHit, RasenShuriken
    }

    // Borrows Zerneas' Naruto Collection effects at runtime when those mods are
    // installed. Nothing from them is shipped here: each client resolves the
    // prefab locally by name, and without the mods only our own visuals play.
    internal static class NarutoFx
    {
        private static readonly string[] Names =
        {
            null, "ShippudenHitSpark", "ShippudenImpact", "ShippudenRasenganBlast", "SixBijuudamaBlast", "FourCrater",
            "SixSlam", "SixRoarBurst", "SasukeShippudenChidoriBurst", "SasukeShippudenKirinStrike", "SasukeShippudenBoltImpact",
            "SasukeShippudenHitSpark", "MinatoHitSpark", "MinatoSenkoSlash", "NarutoSixPathsHitSpark", "SasukeRinneganArrowBurst",
            "NarutoSixPathsGiantBurstGold", "HakuShatter", "ZabuzaCut", "SixClawHit", "SageRasenShurikenBurst"
        };
        private static readonly Dictionary<string, GameObject> resolved = new Dictionary<string, GameObject>();
        private static readonly Dictionary<(GameObject, NarutoEffect), float> nextAllowed = new Dictionary<(GameObject, NarutoEffect), float>();
        private static GameObject relay;
        private static float windowStart;
        private static int windowCount;
        // At ten times attack speed, light hits land dozens of times per second.
        private const float Window = .25f;
        private const int LightBudget = 10;

        internal static void Register()
        {
            relay = CombatVisuals.EffectPrefab("NarutoFxRelay");
            relay.AddComponent<EffectComponent>().applyScale = false;
            var attributes = relay.AddComponent<VFXAttributes>();
            attributes.DoNotPool = true;
            attributes.vfxPriority = VFXAttributes.VFXPriority.Always;
            relay.AddComponent<NarutoFxRelay>();
            if (!ContentAddition.AddEffect(relay)) throw new InvalidOperationException("Cannot register NarutoFxRelay");
        }

        // A zero direction keeps the prefab upright, as ground effects expect.
        // A positive interval marks a light, repeatable hit that may be dropped.
        internal static void Emit(NarutoEffect kind, Vector3 point, Vector3 direction, float scale = 1f,
            float shake = 0f, GameObject source = null, float interval = 0f)
        {
            if (!NetworkServer.active || !relay || kind == NarutoEffect.None) return;
            if (interval > 0f)
            {
                float now = Time.time;
                if (source)
                {
                    var key = (source, kind);
                    if (nextAllowed.TryGetValue(key, out var next) && now < next) return;
                    if (nextAllowed.Count > 256) nextAllowed.Clear();
                    nextAllowed[key] = now + interval;
                }
                if (now - windowStart > Window) { windowStart = now; windowCount = 0; }
                if (++windowCount > LightBudget) return;
            }
            EffectManager.SpawnEffect(relay, new EffectData
            {
                origin = point, scale = scale, genericUInt = (uint)kind, genericFloat = shake,
                rotation = direction.sqrMagnitude > .0001f ? Util.QuaternionSafeLookRotation(direction) : Quaternion.identity
            }, true);
        }

        internal static GameObject Resolve(NarutoEffect kind)
        {
            int index = (int)kind;
            if (index <= 0 || index >= Names.Length) return null;
            string name = Names[index];
            if (resolved.TryGetValue(name, out var prefab)) return prefab;
            // Prefer the catalog entry: it is the copy the Naruto mods spawn,
            // with their game shader swaps already applied.
            for (int i = 0; i < EffectCatalog.effectCount && !prefab; i++)
            {
                var def = EffectCatalog.GetEffectDef((EffectIndex)i);
                if (def != null && def.prefab && string.Equals(def.prefabName, name, StringComparison.OrdinalIgnoreCase))
                    prefab = def.prefab;
            }
            if (!prefab)
                foreach (var bundle in AssetBundle.GetAllLoadedAssetBundles())
                {
                    if (!bundle || bundle.isStreamedSceneAssetBundle) continue;
                    try { prefab = bundle.LoadAsset<GameObject>(name); }
                    catch (Exception) { prefab = null; }
                    if (prefab) break;
                }
            resolved[name] = prefab;
            return prefab;
        }
    }

    internal sealed class NarutoFxRelay : MonoBehaviour
    {
        private void Start()
        {
            var data = GetComponent<EffectComponent>().effectData;
            Destroy(gameObject, .1f);
            if (data == null) return;
            var kind = (NarutoEffect)data.genericUInt;
            float scale = Mathf.Clamp(data.scale, .2f, 12f);
            var prefab = NarutoFx.Resolve(kind);
            if (prefab)
            {
                // Spawn outside the effect pool; its own EffectComponent still
                // needs data, which it reads on its first Start.
                var fx = Instantiate(prefab, data.origin, data.rotation);
                fx.transform.localScale = prefab.transform.localScale * scale;
                var component = fx.GetComponent<EffectComponent>();
                if (component) component.effectData = new EffectData { origin = data.origin, rotation = data.rotation, scale = scale };
                Destroy(fx, 6f);
            }
            if (data.genericFloat > 0f)
                ShakeEmitter.CreateSimpleShakeEmitter(data.origin, new Wave
                    { amplitude = data.genericFloat, frequency = 25f, cycleOffset = 0f },
                    Mathf.Lerp(.12f, .45f, Mathf.Clamp01(data.genericFloat / 3f)), 35f, true);
        }
    }
}

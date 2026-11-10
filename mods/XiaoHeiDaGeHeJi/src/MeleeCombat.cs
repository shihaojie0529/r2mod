using System.Collections.Generic;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace RiftSurvivor
{
    internal static class MeleeCombat
    {
        private static GameObject fistImpact;
        private static GameObject slamImpact;
        private static NetworkSoundEventDef contactSound;

        internal static void LoadEffects()
        {
            fistImpact = Addressables.LoadAssetAsync<GameObject>(
                "RoR2/Base/Loader/ImpactLoaderFistSmall.prefab").WaitForCompletion();
            slamImpact = Addressables.LoadAssetAsync<GameObject>(
                "RoR2/Base/Loader/LoaderGroundSlam.prefab").WaitForCompletion();
            contactSound = Addressables.LoadAssetAsync<NetworkSoundEventDef>(
                "RoR2/Base/Loader/nseLoaderM2Impact.asset").WaitForCompletion();
        }

        // A short, thick cast gives a fist reach on large hurtboxes and keeps vanilla
        // damage processing (including weak points, blocking, item procs and force).
        // A charge reuses its hit set between casts; a punch gets a fresh set.
        internal static bool Sweep(CharacterBody body, Vector3 origin, Vector3 direction,
            float reach, float radius, float coefficient, float force, DamageTypeCombo type,
            bool crit, HashSet<HealthComponent> touched, bool triggerCover = false, bool resetOnKill = false,
            NarutoEffect fx = NarutoEffect.None, float fxScale = 1f, float fxShake = 0f, float fxInterval = 0f)
        {
            Vector3 firstContact = default;
            var attack = new BulletAttack
            {
                owner = body.gameObject, weapon = body.gameObject, origin = origin,
                aimVector = direction, maxDistance = reach, radius = radius, bulletCount = 1,
                damage = body.damage * coefficient, damageType = type, isCrit = crit,
                procCoefficient = 1f, force = force, falloffModel = BulletAttack.FalloffModel.None,
                smartCollision = false, stopperMask = LayerIndex.world.mask,
                hitEffectPrefab = fistImpact
            };
            DamageInfo directHit = null;
            if (resetOnKill)
                attack.modifyOutgoingDamageCallback = (BulletAttack shot, ref BulletAttack.BulletHit hit, DamageInfo info) =>
                {
                    directHit = info;
                    DirectSkillKillReset.Track(info, body);
                };
            bool landed = false;
            List<HealthComponent> coverTargets = null;
            var nativeHit = attack.hitCallback ?? BulletAttack.defaultHitCallback;
            attack.hitCallback = (BulletAttack shot, ref BulletAttack.BulletHit hit) =>
            {
                var hurtBox = hit.hitHurtBox ? hit.hitHurtBox : hit.hurtBox;
                if (!hurtBox)
                {
                    try { return nativeHit(shot, ref hit); } // World geometry can stop the cast.
                    finally { DirectSkillKillReset.Forget(directHit); directHit = null; }
                }
                var health = hurtBox.healthComponent;
                var contact = hit.distance > 0f ? hit.point : origin;
                if (!health || !health.alive || health == body.healthComponent ||
                    hurtBox.teamIndex == TeamIndex.Neutral ||
                    !TeamMask.GetEnemyTeams(body.teamComponent.teamIndex).HasTeam(hurtBox.teamIndex) ||
                    Physics.Linecast(origin, contact, LayerIndex.world.mask, QueryTriggerInteraction.Ignore) ||
                    !touched.Add(health))
                    return true; // Keep looking for other targets along this sweep.

                float before = health.combinedHealth + health.barrier;
                if (!triggerCover) WeiBonds.DirectHit(body, health, contact, DinnerStrike.Cui);
                bool keepGoing;
                try { keepGoing = nativeHit(shot, ref hit); }
                finally { DirectSkillKillReset.Forget(directHit); directHit = null; }
                bool damaged = !health || !health.alive || health.combinedHealth + health.barrier < before;
                if (damaged && !landed) firstContact = contact;
                landed |= damaged;
                if (triggerCover && health)
                    (coverTargets ??= new List<HealthComponent>()).Add(health);
                return keepGoing;
            };
            attack.Fire();
            // A follow-up shot may proc other attacks. Finish the original sweep first.
            if (coverTargets != null)
                foreach (var target in coverTargets) XiaoheiCombat.OnCuiImpact(body, target);
            if (landed && contactSound) EffectManager.SimpleSoundEffect(contactSound.index, origin, true);
            if (landed) NarutoFx.Emit(fx, firstContact, direction, fxScale, fxShake, body.gameObject, fxInterval);
            return landed;
        }

        internal static void Slam(CharacterBody body, bool heavy, bool crit)
        {
            float radius = heavy ? 8f : 6f;
            SkillSpectacle.Emit(SpectacleKind.Slam, body.footPosition, Vector3.up, radius);
            // The overhead slam lands as a Tailed Beast Bomb on a scorched crater.
            if (heavy)
            {
                NarutoFx.Emit(NarutoEffect.TailedBeastBomb, body.footPosition, Vector3.zero, 2.2f, 3.5f);
                NarutoFx.Emit(NarutoEffect.Crater, body.footPosition, Vector3.zero, 2.4f);
            }
            else NarutoFx.Emit(NarutoEffect.Slam, body.footPosition, Vector3.zero, 2.4f, 2f);
            var result = new BlastAttack
            {
                attacker = body.gameObject, inflictor = body.gameObject, teamIndex = body.teamComponent.teamIndex,
                attackerFiltering = AttackerFiltering.NeverHitSelf, position = body.corePosition,
                radius = radius, baseDamage = body.damage * (heavy ? 11f : 6f),
                baseForce = heavy ? 1650f : 1000f, bonusForce = Vector3.up * (heavy ? 450f : 250f),
                procCoefficient = 1f, crit = crit, falloffModel = BlastAttack.FalloffModel.None,
                modifyOutgoingBlastInfoCallback = (ref BlastAttack.BlastAttackDamageInfo damage, BlastAttack.HitPoint hit) =>
                    WeiBonds.DirectHit(body, hit.hurtBox ? hit.hurtBox.healthComponent : damage.hitHealthComponent,
                        hit.hitPosition, DinnerStrike.Cui),
                damageType = DamageTypeCombo.GenericSecondary, losType = BlastAttack.LoSType.NearestHit
            }.Fire();
            // Snapshot direct hits before either bond can start another blast.
            var targets = new List<HealthComponent>(result.hitCount);
            var markedTargets = new List<(HealthComponent target, XiaoheiMarks marks)>();
            for (int i = 0; i < result.hitCount; i++)
            {
                var hurtBox = result.hitPoints[i].hurtBox;
                if (!hurtBox || !hurtBox.healthComponent) continue;
                var target = hurtBox.healthComponent;
                targets.Add(target);
                var marks = XiaoheiCombat.ActiveMarks(target);
                if (marks) markedTargets.Add((target, marks));
            }
            if (heavy) JieBonds.OnHeavySlam(body, targets);
            foreach (var hit in markedTargets)
                if (hit.marks && hit.target && hit.target.body) hit.marks.Cover(body, hit.target);
            if (slamImpact)
                EffectManager.SpawnEffect(slamImpact, new EffectData
                {
                    origin = body.footPosition, scale = radius, rotation = Quaternion.identity
                }, true);
        }
    }
}

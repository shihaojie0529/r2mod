using System;
using System.Collections.Generic;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace RiftSurvivor
{
    [Flags]
    internal enum JieBondTrigger { HeavySlam = 1, Recall = 2 }

    internal static class JieBonds
    {
        internal static void OnRecallHit(CharacterBody jie, HealthComponent victim, Vector3 contact)
        {
            if (!NetworkServer.active || !jie) return;
            var marks = XiaoheiCombat.ActiveMarks(victim);
            if (marks) marks.SwordBond(jie, JieBondTrigger.Recall, contact, Above(victim.body));
        }

        internal static void OnHeavySlam(CharacterBody cui, List<HealthComponent> directHits)
        {
            if (!NetworkServer.active || !cui || !cui.teamComponent) return;
            var landing = cui.footPosition;
            CharacterBody swordsman = null;
            HealthComponent host = null;
            float nearest = float.PositiveInfinity;
            var markedHits = new List<(XiaoheiMarks mark, CharacterBody owner, Vector3 contact, Vector3 frame)>();
            // A group slam sends one wave. Each pinned enemy actually struck can
            // qualify for its own mark, even with an unmarked enemy closer to Cui.
            foreach (var target in directHits)
            {
                if (!target || !target.body) continue;
                var owner = JieSwords.FindOwner(target, cui.teamComponent.teamIndex);
                if (!owner) continue;
                var contact = target.body.corePosition;
                var mark = XiaoheiCombat.ActiveMarks(target);
                if (mark) markedHits.Add((mark, owner, contact, Above(target.body)));
                float distance = (contact - landing).sqrMagnitude;
                if (distance > nearest || (distance == nearest && host && target.body.netId.Value >= host.body.netId.Value)) continue;
                host = target;
                swordsman = owner;
                nearest = distance;
            }
            if (!swordsman) return;
            CuiVitals.GainPump(cui);
            JieBondEffect.Emit(landing + Vector3.up * .15f, false);
            new BlastAttack
            {
                attacker = swordsman.gameObject, inflictor = swordsman.gameObject,
                teamIndex = swordsman.teamComponent.teamIndex, attackerFiltering = AttackerFiltering.NeverHitSelf,
                position = landing, radius = 8f, baseDamage = swordsman.damage * 3.6f,
                crit = swordsman.RollCrit(), procCoefficient = 1f, falloffModel = BlastAttack.FalloffModel.None,
                damageType = DamageTypeCombo.Generic, losType = BlastAttack.LoSType.NearestHit
            }.Fire();
            foreach (var hit in markedHits)
                if (hit.mark && hit.owner) hit.mark.SwordBond(hit.owner, JieBondTrigger.HeavySlam, hit.contact, hit.frame);
        }

        private static Vector3 Above(CharacterBody body) =>
            body.corePosition + Vector3.up * (body.radius + .8f);

        internal static void Pierce(CharacterBody shooter, Vector3 contact)
        {
            var origin = XiaoheiBallistics.Muzzle(shooter);
            var direction = contact - origin;
            if (direction.sqrMagnitude < .0001f) direction = shooter.inputBank.aimDirection;
            var end = PierceLine(shooter, origin, direction, XiaoheiBallistics.Range, 4.5f, null, XiaoheiCombat.Impact);
            XiaoheiBallistics.Trace(XiaoheiCombat.PistolTracer, origin, end);
        }

        internal static Vector3 PierceLine(CharacterBody shooter, Vector3 origin, Vector3 direction, float range,
            float coefficient, HealthComponent alreadyHit, GameObject impact)
        {
            direction.Normalize();
            var shot = new BulletAttack
            {
                owner = shooter.gameObject, weapon = shooter.gameObject,
                origin = origin, aimVector = direction, maxDistance = range,
                bulletCount = 1, radius = .22f, damage = shooter.damage * coefficient, isCrit = shooter.RollCrit(),
                procCoefficient = 1f, damageType = DamageTypeCombo.Generic, smartCollision = false,
                stopperMask = LayerIndex.world.mask, queryTriggerInteraction = QueryTriggerInteraction.Collide,
                falloffModel = BulletAttack.FalloffModel.None, hitEffectPrefab = impact
            };
            var touched = new HashSet<HealthComponent>();
            if (alreadyHit) touched.Add(alreadyHit);
            var end = origin + direction * shot.maxDistance;
            var native = shot.hitCallback ?? BulletAttack.defaultHitCallback;
            shot.hitCallback = (BulletAttack attack, ref BulletAttack.BulletHit hit) =>
            {
                var hurt = hit.hitHurtBox ? hit.hitHurtBox : hit.hurtBox;
                if (!hurt)
                {
                    bool keepGoing = native(attack, ref hit);
                    if (!keepGoing) end = hit.point;
                    return keepGoing;
                }
                if (XiaoheiBallistics.IsLiveEnemy(shooter, hurt) && touched.Add(hurt.healthComponent))
                    native(attack, ref hit);
                return true;
            };
            shot.Fire();
            return end;
        }

        internal static void FinishReplay(CharacterBody jie, Vector3 contact, Vector3 framePosition)
        {
            JieBondEffect.Emit(framePosition, true);
            JieStorm.HeavenSword(jie, contact, 8f, 9f);
        }
    }
}

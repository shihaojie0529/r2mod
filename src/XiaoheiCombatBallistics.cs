using RoR2;
using RoR2.Orbs;
using UnityEngine;

namespace RiftSurvivor
{
    internal static class XiaoheiBallistics
    {
        internal const float Range = 300f;

        internal static Vector3 Muzzle(CharacterBody body)
        {
            var muzzle = XiaoheiModel.Muzzle(body);
            return muzzle ? muzzle.position : body.aimOrigin;
        }

        internal static void Trace(GameObject tracer, Vector3 start, Vector3 end)
        {
            if (tracer) EffectManager.SpawnEffect(tracer, new EffectData { start = start, origin = end }, true);
        }

        internal static bool IsLiveEnemy(CharacterBody shooter, HurtBox hurt) =>
            hurt && hurt.isActiveAndEnabled && hurt.healthComponent && hurt.healthComponent.alive &&
            hurt.healthComponent.body && hurt.healthComponent != shooter.healthComponent &&
            hurt.teamIndex != TeamIndex.Neutral && hurt.teamIndex != TeamIndex.None &&
            TeamMask.GetEnemyTeams(shooter.teamComponent.teamIndex).HasTeam(hurt.teamIndex);

        private static bool CanLock(CharacterBody shooter, HurtBox hurt)
        {
            if (!IsLiveEnemy(shooter, hurt)) return false;
            var visibility = hurt.healthComponent.body.GetVisibilityLevel(shooter);
            return visibility == VisibilityLevel.Visible || visibility == VisibilityLevel.Revealed;
        }

        internal static Vector3 ReplayDirection(CharacterBody body, Ray aim, Vector3 origin)
        {
            // Converge on the owner's current crosshair, including after a blink;
            // the actual shot is still stopped by cover in front of the echo.
            var destination = Util.CharacterRaycast(body.gameObject, aim, out var hit, Range,
                LayerIndex.world.mask | LayerIndex.entityPrecise.mask, QueryTriggerInteraction.Collide)
                ? hit.point : aim.GetPoint(Range);
            var direction = destination - origin;
            return direction.sqrMagnitude < .0001f ? aim.direction : direction.normalized;
        }

        // Use native bullet processing for falloff, weak points, collision and
        // item procs. The tracer starts at the visible gun, the ray at the reticle.
        internal static (bool landed, HealthComponent victim, Vector3 point) Fire(CharacterBody body,
            Ray ray, Vector3 muzzle, float coefficient, DamageTypeCombo type, GameObject tracer, bool personalShot = false, bool resetSecondary = false,
            NarutoEffect fx = NarutoEffect.None, float fxScale = 1f, float fxShake = 0f, float fxInterval = 0f)
        {
            var attack = new BulletAttack
            {
                owner = body.gameObject, weapon = body.gameObject, origin = ray.origin,
                aimVector = ray.direction, maxDistance = Range, radius = .12f, bulletCount = 1,
                damage = body.damage * coefficient, damageType = type, isCrit = body.RollCrit(),
                procCoefficient = 1f, force = 75f, falloffModel = BulletAttack.FalloffModel.None,
                smartCollision = true, queryTriggerInteraction = QueryTriggerInteraction.Collide,
                hitEffectPrefab = XiaoheiCombat.Impact
            };
            DamageInfo directHit = null;
            if (resetSecondary)
                attack.modifyOutgoingDamageCallback = (BulletAttack shot, ref BulletAttack.BulletHit contact, DamageInfo info) =>
                {
                    directHit = info;
                    DirectSkillKillReset.Track(info, body);
                };
            HealthComponent victim = null;
            Vector3 hitPoint = default;
            bool hitEnemy = false;
            Vector3 end = ray.GetPoint(Range);
            var native = attack.hitCallback ?? BulletAttack.defaultHitCallback;
            attack.hitCallback = (BulletAttack shot, ref BulletAttack.BulletHit contact) =>
            {
                var hurt = contact.hitHurtBox ? contact.hitHurtBox : contact.hurtBox;
                var health = hurt ? hurt.healthComponent : null;
                bool enemy = IsLiveEnemy(body, hurt);
                // Count a live enemy contact even if armor or immunity absorbs
                // the shot. Let vanilla resolve its damage, props and world stops.
                bool keepGoing;
                try { keepGoing = native(shot, ref contact); }
                finally { DirectSkillKillReset.Forget(directHit); directHit = null; }
                if (enemy || !keepGoing) end = contact.point;
                if (enemy)
                {
                    hitEnemy = true;
                    victim = health;
                    hitPoint = contact.point;
                    if (personalShot) WeiBonds.TryCatch(body, health, contact.point);
                }
                return keepGoing;
            };
            attack.Fire();
            Trace(tracer, muzzle, end);
            if (hitEnemy) NarutoFx.Emit(fx, hitPoint, -ray.direction, fxScale, fxShake, body.gameObject, fxInterval);
            return (hitEnemy, victim, hitPoint);
        }

        internal static void HitTarget(CharacterBody shooter, HealthComponent health, HurtBox hurt,
            float damage, bool crit, DamageTypeCombo type, bool resetSecondary = false)
        {
            var info = new DamageInfo
            {
                attacker = shooter.gameObject, inflictor = shooter.gameObject, damage = damage, crit = crit,
                position = hurt ? hurt.transform.position : health.body.corePosition,
                inflictedHurtbox = hurt, procCoefficient = 1f, damageType = type
            };
            if (hurt) info.ModifyDamageInfo(hurt.damageModifier);
            var victim = health.gameObject;
            if (resetSecondary) DirectSkillKillReset.Track(info, shooter);
            try { health.TakeDamage(info); }
            finally { DirectSkillKillReset.Forget(info); }
            if (info.rejected) return;
            GlobalEventManager.instance.OnHitEnemy(info, victim);
            GlobalEventManager.instance.OnHitAll(info, victim);
        }

        internal static void Seek(CharacterBody body, XiaoheiCombat combat, Ray aim, Vector3 muzzle)
        {
            bool afterCup = combat.ConsumeCupLine();
            uint miss = combat.PendingMiss;
            var search = new BullseyeSearch
            {
                viewer = body, searchOrigin = aim.origin, searchDirection = aim.direction,
                teamMaskFilter = TeamMask.GetEnemyTeams(body.teamComponent.teamIndex),
                maxDistanceFilter = Range, maxAngleFilter = 15f, filterByLoS = true,
                sortMode = BullseyeSearch.SortMode.Angle
            };
            search.RefreshCandidates();
            foreach (var hurt in search.GetResults())
            {
                if (!CanLock(body, hurt)) continue;
                OrbManager.instance.AddOrb(new XiaoheiSeekerOrb
                {
                    origin = muzzle, target = hurt, shooter = body, combat = combat,
                    damage = body.damage * 5.5f, crit = body.RollCrit(), miss = miss, afterCup = afterCup
                });
                return;
            }
            // Some shootable enemies have no search bullseye. An unlocked round
            // still travels straight and can earn its mark on an actual impact.
            var result = Fire(body, aim, muzzle, 5.5f, DamageTypeCombo.GenericSecondary, XiaoheiCombat.SeekerEffect,
                personalShot: true, resetSecondary: true, fx: NarutoEffect.Kirin, fxScale: 1.2f, fxShake: 1.8f);
            combat.SeekerResult(result.victim, result.landed, miss, afterCup, result.point);
        }
    }

    internal sealed class XiaoheiSeekerOrb : Orb
    {
        internal CharacterBody shooter;
        internal XiaoheiCombat combat;
        internal float damage;
        internal bool crit, afterCup;
        internal uint miss;

        public override void Begin()
        {
            duration = Mathf.Clamp(distanceToTarget / 85f, .055f, .65f);
            if (!XiaoheiCombat.SeekerEffect || !target) return;
            var data = new EffectData { start = origin, origin = target.transform.position, genericFloat = duration };
            data.SetHurtBoxReference(target);
            EffectManager.SpawnEffect(XiaoheiCombat.SeekerEffect, data, true);
        }

        public override void OnArrival()
        {
            if (!shooter || !combat) return;
            var health = target ? target.healthComponent : null;
            bool hit = XiaoheiBallistics.IsLiveEnemy(shooter, target);
            Vector3 point = hit ? target.transform.position : origin;
            if (hit)
            {
                XiaoheiBallistics.HitTarget(shooter, health, target, damage, crit, DamageTypeCombo.GenericSecondary, resetSecondary: true);
                WeiBonds.TryCatch(shooter, health, point);
                SkillSpectacle.Emit(SpectacleKind.Seeker,point,Vector3.up,1.9f);
                NarutoFx.Emit(NarutoEffect.Kirin, point, Vector3.zero, 1.2f, 1.8f);
            }
            combat.SeekerResult(health, hit, miss, afterCup, point);
        }
    }

    // Detached from EntityStateMachine lifetime so the owner can shoot or
    // blink freely. A cooldown reset may start another independent volley.
    internal sealed class XiaoheiReplayVolley : MonoBehaviour
    {
        internal const float Duration = 1.5f;
        private CharacterBody body;
        private Vector3 origin;
        private Ray initialAim;
        private double began;
        private int fired, total;

        internal void Begin(CharacterBody shooter, Vector3 muzzle, Ray aim, int highlights)
        {
            body = shooter;
            origin = muzzle;
            initialAim = aim;
            total = 6 + 2 * highlights;
            CombatStatusHud.Replay(shooter);
            SkillSpectacle.Emit(SpectacleKind.Replay,muzzle,aim.direction,2.5f);
            began = Time.fixedTimeAsDouble;
            Fire(initialAim);
        }

        private void FixedUpdate()
        {
            if (!DownedRescue.IsUp(body)) { Destroy(this); return; }
            double elapsed = Time.fixedTimeAsDouble - began;
            while (fired < total && elapsed + .00001 >= fired * Duration / (total - 1))
                Fire(body.inputBank ? body.inputBank.GetAimRay() : initialAim);
            if (fired == total) Destroy(this);
        }

        private void Fire(Ray aim)
        {
            var direction = XiaoheiBallistics.ReplayDirection(body, aim, origin);
            XiaoheiBallistics.Fire(body, new Ray(origin, direction), origin, 2.5f,
                DamageTypeCombo.GenericSpecial, XiaoheiCombat.EchoTracer,
                fx: NarutoEffect.Chidori, fxScale: .9f, fxShake: .5f, fxInterval: .08f);
            fired++;
        }
    }

    // The baked pose inherits the renderer's import axes. Turn its whole
    // silhouette in world space about the original barrel tip to keep the
    // gun aligned with the stationary tracer origin. Blink echoes never turn.
    internal sealed class XiaoheiReplayPose : MonoBehaviour
    {
        private CharacterBody body;
        private Vector3 muzzle, startingOffset, startingHeading;
        private Quaternion startingRotation;

        internal void Bind(CharacterBody owner, Vector3 pivot, Vector3 barrelDirection)
        {
            body = owner;
            muzzle = pivot;
            startingOffset = transform.position - pivot;
            startingRotation = transform.rotation;
            startingHeading = Vector3.ProjectOnPlane(barrelDirection, Vector3.up).normalized;
            if (startingHeading.sqrMagnitude < .5f) startingHeading = owner.transform.forward;
        }

        private void LateUpdate()
        {
            if (!DownedRescue.IsUp(body)) { Destroy(gameObject); return; }
            if (!body.inputBank) return;
            var direction = XiaoheiBallistics.ReplayDirection(body, body.inputBank.GetAimRay(), muzzle);
            var heading = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (heading.sqrMagnitude < .0001f) return;
            var turn = Quaternion.AngleAxis(Vector3.SignedAngle(startingHeading, heading, Vector3.up), Vector3.up);
            transform.SetPositionAndRotation(muzzle + turn * startingOffset, turn * startingRotation);
        }
    }
}

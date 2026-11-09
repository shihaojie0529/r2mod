using System.Collections.Generic;
using EntityStates;
using RoR2;
using RoR2.Skills;
using UnityEngine;
using UnityEngine.Networking;

namespace RiftSurvivor
{
    public sealed class JieStormSkillDef : SkillDef
    {
        public override EntityState InstantiateNextState(GenericSkill slot)
        {
            var state = base.InstantiateNextState(slot);
            var caster = slot.characterBody;
            if (state is JieStorm storm && caster && caster.inputBank)
                storm.CaptureAim(caster, caster.inputBank.GetAimRay());
            return state;
        }
    }

    public sealed class JieStorm : BaseSkillState
    {
        internal const float Range = 60f;
        private Vector3 castDirection;
        private HurtBoxReference castSeal;
        private HurtBox localSeal;

        internal static void Register() => JieStormEffects.Register();

        internal void CaptureAim(CharacterBody caster, Ray aim)
        {
            castDirection = aim.direction;
            localSeal = FindSeal(caster, aim);
            castSeal = localSeal ? HurtBoxReference.FromHurtBox(localSeal) : default;
        }

        public override void OnEnter()
        {
            base.OnEnter();
            var slot = skillLocator ? skillLocator.special : null;
            if (!slot || slot.stateMachine != outer || slot.activationState.stateType != typeof(JieStorm) ||
                !healthComponent.alive || !GetComponent<JieFlight>()) return;
            var aim = GetAimRay();
            if (castDirection.sqrMagnitude < .5f) CaptureAim(characterBody, aim);
            aim.direction = castDirection;
            if (isAuthority && characterBody.isPlayerControlled)
                GetComponent<JieStormSight>().HidePreview(6f);
            if (!NetworkServer.active || !CuiServerCharges.TrySpend(characterBody, slot, true)) return;
            var seal = localSeal;
            if (!isAuthority)
            {
                seal = castSeal.rootObject ? castSeal.ResolveHurtBox() : null;
                if (!ValidRemoteSeal(characterBody, aim, seal)) seal = null;
            }
            gameObject.AddComponent<JieGoldVolley>().Begin(characterBody, seal);
        }

        private static bool ValidRemoteSeal(CharacterBody caster, Ray aim, HurtBox hurt)
        {
            if (!CanSee(caster, hurt) || !hurt.collider) return false;
            // Aim may clip the edge of a large boss. Check its hittable surface, allowing
            // a little lateral motion while the target reference travels to the server.
            float along = Vector3.Dot(hurt.transform.position - aim.origin, aim.direction);
            var point = hurt.collider.ClosestPoint(aim.GetPoint(Mathf.Clamp(along, 0f, Range)));
            var offset = point - aim.origin;
            along = Vector3.Dot(offset, aim.direction);
            return along >= 0f && offset.sqrMagnitude <= Range * Range &&
                (point - aim.GetPoint(along)).sqrMagnitude <= 2.5f * 2.5f &&
                !Physics.Linecast(aim.origin, point, LayerIndex.world.mask, QueryTriggerInteraction.Ignore);
        }

        public override void OnSerialize(NetworkWriter writer)
        {
            base.OnSerialize(writer);
            writer.Write(castDirection);
            castSeal.Write(writer);
        }
        public override void OnDeserialize(NetworkReader reader)
        {
            base.OnDeserialize(reader);
            var direction = reader.ReadVector3();
            castSeal.Read(reader);
            if (direction.sqrMagnitude > .8f && direction.sqrMagnitude < 1.2f) castDirection = direction.normalized;
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (isAuthority && fixedAge >= .08f) outer.SetNextStateToMain();
        }

        internal static bool CanSee(CharacterBody caster, HurtBox hurt)
        {
            if (!XiaoheiBallistics.IsLiveEnemy(caster, hurt)) return false;
            var level = hurt.healthComponent.body.GetVisibilityLevel(caster);
            return level == VisibilityLevel.Visible || level == VisibilityLevel.Revealed;
        }

        internal static HurtBox FindSeal(CharacterBody caster, Ray aim)
        {
            if (!Util.CharacterRaycast(caster.gameObject, aim, out var hit, Range,
                LayerIndex.world.mask | LayerIndex.entityPrecise.mask, QueryTriggerInteraction.Collide)) return null;
            var hurt = hit.collider ? hit.collider.GetComponent<HurtBox>() : null;
            return CanSee(caster, hurt) ? hurt : null;
        }

        internal static void HeavenSword(CharacterBody caster, Vector3 position, float radius, float coefficient)
        {
            if (!NetworkServer.active || !caster) return;
            JieStormEffects.Drop(position, radius);
            var impact = caster.gameObject.AddComponent<JieHeavenImpact>();
            impact.Begin(caster, position, radius, coefficient);
        }
    }

    internal sealed class JieHeavenImpact : MonoBehaviour
    {
        private CharacterBody caster;
        private Vector3 point;
        private float radius, damage, arrives;
        private bool crit;

        internal void Begin(CharacterBody owner, Vector3 position, float range, float coefficient)
        {
            caster = owner;
            point = position;
            radius = range;
            damage = owner.damage * coefficient;
            crit = owner.RollCrit();
            arrives = Time.fixedTime + JieStormEffects.DropTime;
        }

        private void FixedUpdate()
        {
            if (Time.fixedTime < arrives) return;
            enabled = false;
            if (NetworkServer.active && caster)
                new BlastAttack
                {
                    attacker = caster.gameObject, inflictor = caster.gameObject, teamIndex = caster.teamComponent.teamIndex,
                    position = point, radius = radius, baseDamage = damage, crit = crit, procCoefficient = 1f,
                    attackerFiltering = AttackerFiltering.NeverHitSelf, damageType = DamageTypeCombo.GenericSpecial,
                    losType = BlastAttack.LoSType.NearestHit, falloffModel = BlastAttack.FalloffModel.None
                }.Fire();
            Destroy(this);
        }
    }

    internal sealed class JieGoldVolley : MonoBehaviour
    {
        private sealed class Shot
        {
            internal HurtBox target;
            internal Vector3 from, endpoint;
            internal float fired, arrives;
            internal bool hasTarget, hitsWorld;
        }

        internal const float Duration = 6f;
        internal const int WaveSize = 6, Waves = 6;
        internal static float ReleaseAt(int wave) => .30f + wave;
        private readonly List<Shot> active = new List<Shot>(WaveSize);
        private readonly List<HurtBox> targets = new List<HurtBox>();
        private readonly HashSet<HealthComponent> distinct = new HashSet<HealthComponent>();
        private CharacterBody caster;
        private HurtBox seal;
        private float started, searchTime = float.NegativeInfinity;
        private int nextWave, nextTarget;
        private bool hasHit;
        private Vector3 lastHit;

        internal void Begin(CharacterBody owner, HurtBox locked)
        {
            caster = owner;
            seal = locked;
            started = Time.fixedTime;
            CombatStatusHud.Storm(owner);
            JieStormEffects.OpenFan(owner);
            if (seal) JieStormEffects.Lock(seal.healthComponent.body);
        }

        private HurtBox ChooseTarget()
        {
            // Once cast, the marked enemy is pursued even if Jie turns or flies away.
            if (XiaoheiBallistics.IsLiveEnemy(caster, seal)) return seal;
            seal = null;
            if (Time.fixedTime - searchTime >= .08f)
            {
                searchTime = Time.fixedTime;
                targets.Clear();
                distinct.Clear();
                var search = new BullseyeSearch
                {
                    viewer = caster, searchOrigin = caster.corePosition, searchDirection = Vector3.forward,
                    maxDistanceFilter = JieStorm.Range, maxAngleFilter = 180f, filterByLoS = true,
                    teamMaskFilter = TeamMask.GetEnemyTeams(caster.teamComponent.teamIndex),
                    sortMode = BullseyeSearch.SortMode.Distance
                };
                search.RefreshCandidates();
                foreach (var hurt in search.GetResults())
                    if (JieStorm.CanSee(caster, hurt) && distinct.Add(hurt.healthComponent)) targets.Add(hurt);
                if (targets.Count > 0) nextTarget %= targets.Count;
            }
            for (int i = 0; i < targets.Count; ++i)
            {
                var target = targets[nextTarget];
                nextTarget = (nextTarget + 1) % targets.Count;
                if (JieStorm.CanSee(caster, target)) return target;
            }
            return null;
        }

        private void Aim(Shot shot, Vector3 from, HurtBox target)
        {
            shot.from = from;
            shot.fired = Time.fixedTime;
            shot.hasTarget = target;
            shot.target = target;
            shot.hitsWorld = false;
            if (target) shot.endpoint = target.transform.position;
            else
            {
                var aim = caster.inputBank.GetAimRay();
                shot.hitsWorld = Physics.Raycast(aim, out var hit, JieStorm.Range,
                    LayerIndex.world.mask, QueryTriggerInteraction.Ignore);
                shot.endpoint = shot.hitsWorld ? hit.point : aim.GetPoint(JieStorm.Range);
            }
            float travel = Mathf.Clamp(Vector3.Distance(from, shot.endpoint) / 105f, .07f, .58f);
            // Final-wave replacements still land inside the six-second cast window.
            shot.arrives = Mathf.Min(shot.fired + travel, started + Duration);
            JieStormEffects.Launch(from, shot.endpoint, target, Mathf.Max(.02f, shot.arrives - shot.fired));
        }

        private void FixedUpdate()
        {
            if (!caster || !caster.healthComponent.alive) { Destroy(this); return; }
            float elapsed = Time.fixedTime - started;
            while (nextWave < Waves && elapsed >= ReleaseAt(nextWave))
            {
                for (int i = 0; i < WaveSize; ++i)
                {
                    int index = WaveSize * nextWave + i;
                    JieStormEffects.FanPose(caster, index, out var from, out _);
                    var shot = new Shot();
                    Aim(shot, from, ChooseTarget());
                    active.Add(shot);
                }
                nextWave++;
            }
            for (int i = 0; i < active.Count; ++i)
            {
                var shot = active[i];
                if (shot.hasTarget && !XiaoheiBallistics.IsLiveEnemy(caster, shot.target))
                {
                    float progress = Mathf.InverseLerp(shot.fired, shot.arrives, Time.fixedTime);
                    Aim(shot, JieStormEffects.FlightPoint(shot.from, shot.endpoint, progress), ChooseTarget());
                }
                if (shot.target) shot.endpoint = shot.target.transform.position;
                if (Time.fixedTime < shot.arrives) continue;
                if (shot.target || shot.hitsWorld)
                {
                    lastHit = shot.endpoint;
                    hasHit = true;
                }
                if (shot.target)
                {
                    WeiBonds.DirectHit(caster, shot.target.healthComponent, shot.endpoint, DinnerStrike.Sword);
                    XiaoheiBallistics.HitTarget(caster, shot.target.healthComponent, shot.target, caster.damage * 2f,
                        caster.RollCrit(), DamageTypeCombo.GenericSpecial);
                }
                active.RemoveAt(i--);
            }
            if (elapsed < Duration || active.Count != 0 || nextWave < Waves) return;
            enabled = false;
            if (XiaoheiBallistics.IsLiveEnemy(caster, seal)) JieStorm.HeavenSword(caster, seal.transform.position, 12f, 18f);
            else if (hasHit) JieStorm.HeavenSword(caster, lastHit, 12f, 18f);
            Destroy(this);
        }
    }

    internal sealed class JieStormSight : MonoBehaviour
    {
        private CharacterBody body, selected;
        private GameObject preview;
        private float poll, hideUntil;
        private void Awake() => body = GetComponent<CharacterBody>();
        internal void HidePreview(float duration) { hideUntil = Time.time + duration; selected = null; }
        private void LateUpdate()
        {
            if (!NetworkClient.active || !body.hasEffectiveAuthority || !body.isPlayerControlled) return;
            if (Time.time >= poll)
            {
                poll = Time.time + .07f;
                var slot = body.skillLocator ? body.skillLocator.special : null;
                var hurt = Time.time >= hideUntil && body.healthComponent && body.healthComponent.alive &&
                    slot && slot.skillDef && slot.activationState.stateType == typeof(JieStorm) && body.inputBank
                    ? JieStorm.FindSeal(body, body.inputBank.GetAimRay()) : null;
                selected = hurt ? hurt.healthComponent.body : null;
            }
            if (selected && !preview) preview = JieStormEffects.MakeSeal();
            if (!preview) return;
            preview.SetActive(selected && selected.healthComponent.alive);
            if (selected) preview.transform.position = JieStormEffects.SealPoint(selected);
        }
        private void OnDisable() { if (preview) Destroy(preview); preview = null; selected = null; }
    }
}

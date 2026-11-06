using EntityStates;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace RiftSurvivor
{
    public sealed class ShakerAim : BaseSkillState
    {
        private const float HoldTime = .30f;
        internal const float MaxRange = 45f;
        internal const float AimCos = .9781476f; // 12-degree half cone.
        private static GameObject lockPrefab;
        private Indicator indicator;
        private CharacterBody selected;
        private bool startedAim;

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (!startedAim && fixedAge >= HoldTime)
            {
                startedAim = true;
                CuiMotion.Play(characterBody, "aim", 1.5f);
                if (isAuthority && characterBody.isPlayerControlled)
                {
                    if (!lockPrefab) lockPrefab = LegacyResourcesAPI.Load<GameObject>("Prefabs/HuntressTrackingIndicator");
                    if (lockPrefab) indicator = new Indicator(gameObject, lockPrefab);
                }
            }
            if (!isAuthority) return;
            // An AI doppelganger can drink without trying to acquire a human ally.
            if (!characterBody.isPlayerControlled)
            {
                Commit(characterBody, Vector3.zero);
                return;
            }

            Ray aim = GetAimRay();
            if (startedAim)
            {
                StartAimMode(aim, .3f, false);
                selected = FindTarget(aim);
                if (indicator != null)
                {
                    indicator.targetTransform = selected ? selected.coreTransform : null;
                    indicator.active = selected;
                }
            }
            if (IsKeyDownAuthority()) return;
            CharacterBody target = startedAim ? selected : characterBody;
            if (target) Commit(target, aim.direction);
            else outer.SetNextStateToMain();
        }

        private CharacterBody FindTarget(Ray aim)
        {
            CharacterBody best = null;
            float bestDot = AimCos;
            var teammates = TeamComponent.GetTeamMembers(teamComponent.teamIndex);
            foreach (var member in teammates)
            {
                var candidate = member.body;
                if (!IsAlly(characterBody, candidate)) continue;
                Vector3 delta = candidate.corePosition - aim.origin;
                float distance = delta.magnitude;
                if (distance <= .001f || distance > MaxRange) continue;
                float dot = Vector3.Dot(aim.direction, delta / distance);
                if (dot <= bestDot || !HasLine(aim.origin, candidate.corePosition)) continue;
                bestDot = dot;
                best = candidate;
            }
            return best;
        }

        private void Commit(CharacterBody target, Vector3 direction)
        {
            outer.SetNextState(new ShakerCast
            {
                targetId = target.netId, selfDrink = target == characterBody, releaseDirection = direction.normalized
            });
        }

        internal static bool Equipped(GenericSkill slot) => slot && slot.skillDef &&
            slot.skillDef.activationState.stateType == typeof(ShakerAim);

        internal static bool IsAlly(CharacterBody caster, CharacterBody target) =>
            target && target != caster && target.isPlayerControlled && target.healthComponent &&
            target.healthComponent.alive && target.teamComponent && caster.teamComponent &&
            target.teamComponent.teamIndex == caster.teamComponent.teamIndex;

        internal static bool HasLine(Vector3 origin, Vector3 target) =>
            !Physics.Linecast(origin, target, LayerIndex.world.mask, QueryTriggerInteraction.Ignore);

        public override void OnExit()
        {
            if (indicator != null) indicator.active = false;
            if (startedAim) CuiMotion.Stop(characterBody, "aim");
            base.OnExit();
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.PrioritySkill;
    }

    public sealed class ShakerCast : BaseSkillState
    {
        internal NetworkInstanceId targetId;
        internal bool selfDrink;
        internal Vector3 releaseDirection;
        private float duration;

        public override void OnSerialize(NetworkWriter writer)
        {
            base.OnSerialize(writer);
            writer.Write(targetId);
            writer.Write(selfDrink);
            writer.Write(releaseDirection);
        }

        public override void OnDeserialize(NetworkReader reader)
        {
            base.OnDeserialize(reader);
            targetId = reader.ReadNetworkId();
            selfDrink = reader.ReadBoolean();
            releaseDirection = reader.ReadVector3();
        }

        public override void OnEnter()
        {
            base.OnEnter();
            duration = selfDrink ? .95f : .65f;
            var targetObject = NetworkServer.active
                ? NetworkServer.FindLocalObject(targetId)
                : ClientScene.FindLocalObject(targetId);
            CharacterBody target = targetObject ? targetObject.GetComponent<CharacterBody>() : null;
            if (!target) return;

            if (NetworkServer.active)
            {
                if (!characterBody || !healthComponent.alive || !characterBody.GetComponent<CuiVitals>() ||
                    !ShakerAim.Equipped(skillLocator.special) ||
                    skillLocator.special.stateMachine != outer || outer.customName != "Weapon")
                    return;
                if (selfDrink ? target != characterBody : !ValidThrow(target)) return;
                if (!CuiServerCharges.TrySpend(characterBody, skillLocator.special, prepaidByLocalSkill: false)) return;
                bool empowered = CuiVitals.TrySpendFull(characterBody);
                CuiShakerHealing.Serve(target, empowered);
                if (!selfDrink) XiaoheiCombat.OnCup(target);
                CuiVitals.ConfirmShaker(characterBody);
            }
            else if (isAuthority)
            {
                characterBody.GetComponent<CuiVitals>().AwaitShakerReceipt();
            }
            CuiMotion.Play(characterBody, selfDrink ? "drink" : "throw", duration);
            if (!selfDrink) CuiMotion.ThrowBottle(characterBody, target, .273f);
            Util.PlaySound(selfDrink ? "Play_item_use_fruit" : "Play_commando_M2_grenade_throw", gameObject);
        }

        private bool ValidThrow(CharacterBody target)
        {
            if (!ShakerAim.IsAlly(characterBody, target)) return false;
            // Check the actual release cone against server positions. Allow the
            // last networked aim update to trail the release by up to 60 degrees.
            Vector3 origin = inputBank.aimOrigin;
            Vector3 delta = target.corePosition - origin;
            float norm = releaseDirection.sqrMagnitude;
            return norm >= .98f && norm <= 1.02f && delta.sqrMagnitude > .000001f &&
                delta.sqrMagnitude <= ShakerAim.MaxRange * ShakerAim.MaxRange &&
                Vector3.Dot(releaseDirection.normalized, delta.normalized) >= ShakerAim.AimCos &&
                Vector3.Dot(releaseDirection.normalized, inputBank.aimDirection.normalized) >= .5f &&
                ShakerAim.HasLine(origin, target.corePosition);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (isAuthority && fixedAge >= duration) outer.SetNextStateToMain();
        }

        public override void OnExit()
        {
            CuiMotion.Stop(characterBody, selfDrink ? "drink" : "throw");
            base.OnExit();
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.Skill;
    }
}

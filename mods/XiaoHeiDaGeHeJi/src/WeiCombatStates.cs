using EntityStates;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace RiftSurvivor
{
    public abstract class WeiThrowState : BaseSkillState
    {
        internal Vector3 direction;
        protected WeiCombat Combat { get; private set; }
        protected Ray Aim { get; private set; }
        protected abstract SkillSlot Slot { get; }
        protected virtual System.Type Equipped => GetType();
        protected virtual float Duration => .17f;
        protected virtual bool UsesAimMode => true;

        public override void OnEnter()
        {
            base.OnEnter();
            var slot = skillLocator ? skillLocator.GetSkill(Slot) : null;
            if (!slot || slot.stateMachine != outer || slot.activationState.stateType != Equipped ||
                !inputBank || !healthComponent || !healthComponent.alive) return;
            Combat = GetComponent<WeiCombat>();
            if (!Combat) return;
            var ray = GetAimRay();
            if (direction.sqrMagnitude > .5f) ray.direction = direction;
            else direction = ray.direction;
            Aim = ray;
            if (UsesAimMode) StartAimMode(ray, .5f, false);
            Begin();
        }

        protected abstract void Begin();

        public override void OnSerialize(NetworkWriter writer) { base.OnSerialize(writer); writer.Write(direction); }
        public override void OnDeserialize(NetworkReader reader)
        {
            base.OnDeserialize(reader);
            var v = reader.ReadVector3();
            if (v.sqrMagnitude > .5f && v.sqrMagnitude < 1.5f) direction = v.normalized;
        }
        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (isAuthority && fixedAge >= Duration) outer.SetNextStateToMain();
        }
        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.Skill;
    }

    public sealed class WeiDelivery : WeiThrowState
    {
        private bool automatic;
        private HurtBoxReference target;
        protected override SkillSlot Slot => SkillSlot.Primary;
        protected override float Duration => 1f / Mathf.Max(.1f, attackSpeedStat);
        protected override bool UsesAimMode => !automatic;

        internal void SetAutomaticTarget(HurtBox selected)
        {
            automatic = true;
            target = HurtBoxReference.FromHurtBox(selected);
        }

        public override void OnSerialize(NetworkWriter writer)
        {
            base.OnSerialize(writer);
            writer.Write(automatic);
            if (automatic) target.Write(writer);
        }

        public override void OnDeserialize(NetworkReader reader)
        {
            base.OnDeserialize(reader);
            automatic = reader.ReadBoolean();
            if (automatic) target.Read(reader);
        }

        protected override void Begin()
        {
            if (NetworkServer.active)
            {
                HurtBox locked = null;
                if (automatic)
                {
                    var control = GetComponent<WeiAutoFire>();
                    if (!control || !control.BodyReady() || !(skillLocator.primary.skillDef is WeiDeliverySkillDef)) return;
                    locked = WeiAutoFire.ValidateOrAcquire(characterBody, target.ResolveHurtBox());
                    if (!locked) return;
                }
                if (!Combat.Deliver(Aim, Duration, locked)) return;
            }
            if (isAuthority && characterBody.isPlayerControlled)
                GetComponent<WeiAutoFire>().ShotStarted(Duration);
            Util.PlaySound("Play_commando_M2_grenade_throw", gameObject);
        }
    }

    public sealed class WeiCut : WeiThrowState
    {
        protected override SkillSlot Slot => SkillSlot.Secondary;
        protected override float Duration => float.PositiveInfinity;
        protected override bool UsesAimMode => false;
        protected override void Begin()
        {
            if (NetworkServer.active) Combat.Cut(Aim);
            else if (isAuthority) Combat.AwaitReceipt(false);
        }
        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (!isAuthority) return;
            if (!Combat || !DownedRescue.IsUp(characterBody)) { outer.SetNextStateToMain(); return; }
            if (characterBody.isPlayerControlled && IsKeyDownAuthority())
            {
                if (Combat.Carrying) StartAimMode(.25f);
                return;
            }
            outer.SetNextState(new WeiPaste { direction = GetAimRay().direction });
        }
        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.PrioritySkill;
    }

    public sealed class WeiPaste : WeiThrowState
    {
        protected override SkillSlot Slot => SkillSlot.Secondary;
        protected override System.Type Equipped => typeof(WeiCut);
        protected override void Begin()
        {
            if (NetworkServer.active) Combat.Paste(Aim);
        }
    }

    public sealed class WeiSelectAll : WeiThrowState
    {
        protected override SkillSlot Slot => SkillSlot.Special;
        protected override void Begin()
        {
            if (NetworkServer.active) Combat.SummonSwarm(Aim);
            else if (isAuthority) Combat.AwaitReceipt(true);
        }
    }
}

using EntityStates;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace RiftSurvivor
{
    public abstract class XiaoheiGunState : BaseSkillState
    {
        private Vector3 aimDirection;
        private float duration;
        protected abstract SkillSlot Slot { get; }
        protected abstract float Recovery { get; }
        protected abstract void Cast(XiaoheiCombat combat, Ray aim);

        public override void OnEnter()
        {
            base.OnEnter();
            duration = Mathf.Max(Time.fixedDeltaTime, Recovery / Mathf.Max(.1f, attackSpeedStat));
            var combat = GetComponent<XiaoheiCombat>();
            if (!combat || !healthComponent || !healthComponent.alive || !skillLocator || !inputBank) return;
            var slot = skillLocator.GetSkill(Slot);
            if (!slot || !slot.skillDef || slot.stateMachine != outer || slot.activationState.stateType != GetType()) return;
            if (NetworkServer.active)
            {
                if (Slot == SkillSlot.Primary)
                {
                    if (!isAuthority && !combat.AcceptRemotePrimary(duration)) return;
                }
                else if (!CuiServerCharges.TrySpend(characterBody, slot, prepaidByLocalSkill: true)) return;
            }
            var aim = GetAimRay();
            if (aimDirection.sqrMagnitude > .5f) aim.direction = aimDirection;
            else aimDirection = aim.direction;
            StartAimMode(aim, duration + .3f, false);
            Cast(combat, aim);
        }

        protected void FireCue(string sound, Ray aim)
        {
            if (!NetworkClient.active) return;
            Util.PlaySound(sound, gameObject);
            XiaoheiModel.Recoil(characterBody);
            if (XiaoheiCombat.MuzzleFlash)
                EffectManager.SimpleImpactEffect(XiaoheiCombat.MuzzleFlash,
                    XiaoheiBallistics.Muzzle(characterBody), aim.direction, false);
        }

        public override void OnSerialize(NetworkWriter writer)
        {
            base.OnSerialize(writer);
            writer.Write(aimDirection);
        }

        public override void OnDeserialize(NetworkReader reader)
        {
            base.OnDeserialize(reader);
            var aim = reader.ReadVector3();
            if (aim.sqrMagnitude > .5f && aim.sqrMagnitude < 1.5f) aimDirection = aim.normalized;
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (isAuthority && fixedAge >= duration) outer.SetNextStateToMain();
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.Skill;
    }

    public sealed class XiaoheiPrimary : XiaoheiGunState
    {
        protected override SkillSlot Slot => SkillSlot.Primary;
        protected override float Recovery => 1f / 3f;

        protected override void Cast(XiaoheiCombat combat, Ray aim)
        {
            FireCue("Play_commando_R", aim);
            if (!NetworkServer.active) return;
            var shot = XiaoheiBallistics.Fire(characterBody, aim, XiaoheiBallistics.Muzzle(characterBody),
                2.2f, DamageTypeCombo.GenericPrimary, XiaoheiCombat.PistolTracer, personalShot: true);
            combat.PrimaryResult(shot.landed);
        }
    }

    public sealed class XiaoheiSecondary : XiaoheiGunState
    {
        protected override SkillSlot Slot => SkillSlot.Secondary;
        protected override float Recovery => .24f;

        protected override void Cast(XiaoheiCombat combat, Ray aim)
        {
            FireCue("Play_commando_M2", aim);
            if (NetworkServer.active)
                XiaoheiBallistics.Seek(characterBody, combat, aim, XiaoheiBallistics.Muzzle(characterBody));
            if (isAuthority) XiaoheiBlink.BeginSlide(characterBody);
            if (NetworkClient.active && inputBank.moveVector.sqrMagnitude > .001f) XiaoheiSlideVisual.Begin(characterBody);
        }
    }

    public sealed class XiaoheiReplay : XiaoheiGunState
    {
        protected override SkillSlot Slot => SkillSlot.Special;
        protected override float Recovery => .12f;

        protected override void Cast(XiaoheiCombat combat, Ray aim)
        {
            if (NetworkServer.active)
                XiaoheiReplaySentry.Spawn(characterBody, aim, combat.SpendHighlights());
            if (NetworkClient.active) Util.PlaySound("Play_commando_M2", gameObject);
        }
    }
}

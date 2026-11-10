using System.Collections.Generic;
using EntityStates;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace RiftSurvivor
{
    // Keep the registered name for the special slot and its server receipts.
    public sealed class ShakerAim : BaseSkillState
    {
        internal const float NormalHeal = .30f, EmpoweredHeal = .50f;
        private bool accepted;
        internal static bool Equipped(GenericSkill slot) => slot && slot.skillDef &&
            slot.activationState.stateType == typeof(ShakerAim);

        public override void OnEnter()
        {
            base.OnEnter();
            if(!characterBody || !healthComponent || !healthComponent.alive || !GetComponent<CuiVitals>() ||
                !Equipped(skillLocator.special) || skillLocator.special.stateMachine!=outer || outer.customName!="Weapon")return;
            if(NetworkServer.active)
            {
                if(!CuiServerCharges.TrySpend(characterBody,skillLocator.special,false))return;
                bool empowered=CuiVitals.TrySpendFull(characterBody);
                HealTeam(characterBody,empowered);
                CuiVitals.ConfirmShaker(characterBody);
            }
            else if(isAuthority)GetComponent<CuiVitals>().AwaitShakerReceipt();
            accepted=true;
            CuiMotion.Play(characterBody,"drink",.65f);
            Util.PlaySound("Play_item_use_fruit",gameObject);
        }
        internal static int HealTeam(CharacterBody caster,bool empowered)
        {
            if(!NetworkServer.active || !caster || !caster.teamComponent || !caster.healthComponent || !caster.healthComponent.alive)return 0;
            var recipients=new List<CharacterBody>();
            foreach(var member in TeamComponent.GetTeamMembers(caster.teamComponent.teamIndex))
            {
                var body=member.body;
                if(DownedRescue.IsUp(body))recipients.Add(body);
            }
            if(!recipients.Contains(caster))recipients.Add(caster);
            int count=0;
            foreach(var body in recipients)
            {
                if(!DownedRescue.IsUp(body))continue;
                body.healthComponent.Heal(body.healthComponent.fullHealth*(empowered?EmpoweredHeal:NormalHeal),default,true);
                SkillSpectacle.Emit(SpectacleKind.Supply,body.corePosition,Vector3.up,empowered?3f:2f);
                if(body!=caster)XiaoheiCombat.OnCup(body);
                count++;
            }
            DownedRescue.ApplySupply(caster,empowered?EmpoweredHeal:NormalHeal);
            return count;
        }
        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if(isAuthority && (!accepted || fixedAge>=.65f))outer.SetNextStateToMain();
        }
        public override void OnExit(){CuiMotion.Stop(characterBody,"drink");base.OnExit();}
        public override InterruptPriority GetMinimumInterruptPriority()=>InterruptPriority.Skill;
    }
}

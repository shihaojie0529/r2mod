using EntityStates;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace RiftSurvivor
{
    public sealed class CuiChargeWindup : AimedMeleeState
    {
        internal const float MaxHold = 2f;
        internal float HeldSeconds => Mathf.Min(fixedAge,MaxHold);
        private bool accepted;
        private GameObject aura;
        public override void OnEnter()
        {
            base.OnEnter();
            accepted=AcceptSlot(skillLocator ? skillLocator.utility : null,true);
            if(!accepted){if(isAuthority)outer.SetNextStateToMain();return;}
            if(NetworkServer.active)GetComponent<CuiVitals>().BeginCharge();
            StartAimMode(MaxHold+.3f);
            CuiMotion.Play(characterBody,"aim",MaxHold);
            if(NetworkClient.active)
            {
                aura=new GameObject("CuiChargeWindupAura");aura.transform.SetParent(characterBody.coreTransform,false);
                var geometry=aura.AddComponent<RadiantGeometry>();
                geometry.Arc("ChargeGatheringRing",new Color(1f,.65f,.08f,.65f),.7f,.065f,20f,135f);
                geometry.Arc("ChargeGatheringOrbit",new Color(1f,.92f,.65f,.65f),.64f,.025f,200f,135f);
            }
        }
        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if(!accepted)return;
            if(aura)
            {
                float t=Mathf.Clamp01(fixedAge/MaxHold);
                aura.transform.localScale=Vector3.one*Mathf.Lerp(.65f,1.25f,t);
                aura.transform.rotation=Util.QuaternionSafeLookRotation(GetAimRay().direction)*Quaternion.Euler(0,0,fixedAge*100f);
            }
            if(!isAuthority)return;
            if(characterMotor)characterMotor.velocity*=.85f;
            if(!IsKeyDownAuthority() || fixedAge>=MaxHold || !characterBody.isPlayerControlled)
                outer.SetNextState(new ShoulderCharge{heldSeconds=Mathf.Min(fixedAge,MaxHold)});
        }
        public override void OnExit()
        {
            if(accepted && NetworkServer.active && characterBody)GetComponent<CuiVitals>().EndChargeWindup();
            if(aura)Object.Destroy(aura);
            CuiMotion.Stop(characterBody,"aim");
            base.OnExit();
        }
        public override InterruptPriority GetMinimumInterruptPriority()=>InterruptPriority.PrioritySkill;
    }
}

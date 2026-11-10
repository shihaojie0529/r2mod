using System;
using System.Collections.Generic;
using System.Reflection;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

public partial class Smoke
{
    void CheckCuiRework()
    {
        bool wasServer=NetworkServer.active;
        var fixtures=new List<GameObject>();
        CharacterBody Make(string id,TeamIndex team)
        {
            var def=Array.Find(Resources.FindObjectsOfTypeAll<SurvivorDef>(),d=>d.cachedName==id);
            var go=Instantiate(def.bodyPrefab);fixtures.Add(go);
            foreach(var machine in go.GetComponents<EntityStateMachine>())machine.enabled=false;
            var body=go.GetComponent<CharacterBody>();body.teamComponent.teamIndex=team;
            typeof(CharacterBody).GetProperty("maxHealth",Flags).SetValue(body,100f);
            typeof(CharacterBody).GetProperty("maxShield",Flags).SetValue(body,0f);
            typeof(CharacterBody).GetProperty("maxBarrier",Flags).SetValue(body,100f);
            NetworkServer.Spawn(go);body.healthComponent.Networkhealth=100f;
            return body;
        }
        try
        {
            if(!wasServer){NetworkServer.dontListen=true;if(!NetworkServer.Listen(0))throw new Exception("No Cui fixture server");}
            var cui=Make("RiftRunner",TeamIndex.Monster);
            var vitals=cui.GetComponent<RiftSurvivor.CuiVitals>();
            var health=cui.healthComponent;
            var pump=(BuffDef)T("CuiVitals").GetField("pump",Flags).GetValue(null);
            CheckOne("Cui native barrier gain, uppercut and two health cap",()=>{
                cui.RecalculateStats();
                if(Mathf.Abs(health.fullBarrier-200f)>.001f)throw new Exception("Native barrier cap incorrect "+health.fullBarrier);
                health.Networkbarrier=0f;
                Call("CuiVitals","PunchHit",cui,false);
                if(Mathf.Abs(health.barrier-8f)>.01f)throw new Exception("Jab barrier missing "+health.barrier);
                Call("CuiVitals","PunchHit",cui,true);
                if(Mathf.Abs(health.barrier-20f)>.01f)throw new Exception("Uppercut barrier missing");
                for(int i=0;i<50;i++)Call("CuiVitals","PunchHit",cui,false);
                if(Mathf.Abs(health.barrier-200f)>.01f)throw new Exception("Barrier exceeded cap");
                var other=Make("JieGe",TeamIndex.Monster);
                other.healthComponent.AddBarrier(250f);
                if(other.healthComponent.barrier>100.01f)throw new Exception("Other survivor barrier changed");
            });
            CheckOne("Cui native barrier holds in combat and slowly decays out of combat",()=>{
                var last=T("CuiVitals").GetField("lastCombatAt",Flags);
                typeof(CharacterBody).GetProperty("outOfCombat",Flags).SetValue(cui,true);
                typeof(CharacterBody).GetProperty("outOfDanger",Flags).SetValue(cui,true);
                last.SetValue(vitals,Time.fixedTime-10f);
                var decay=typeof(HealthComponent).GetMethod("GetBarrierDecayRate",Flags);
                if(Mathf.Abs((float)decay.Invoke(health,null)-2f)>.01f)throw new Exception("Slow decay missing");
                typeof(CharacterBody).GetProperty("outOfCombat",Flags).SetValue(cui,false);
                if((float)decay.Invoke(health,null)!=0f)throw new Exception("Barrier drains in combat");
                typeof(CharacterBody).GetProperty("outOfCombat",Flags).SetValue(cui,true);
                var damage=new DamageInfo{damage=10f};vitals.OnIncomingDamageServer(damage);
                if((float)decay.Invoke(health,null)!=0f)throw new Exception("Taking damage did not delay decay");
            });
            CheckOne("Cui pump reductions and charging guard multiply",()=>{
                for(int count=0;count<=6;count++)
                {
                    cui.SetBuffCount(pump.buffIndex,count);
                    var info=new DamageInfo{damage=100f};vitals.OnIncomingDamageServer(info);
                    if(Mathf.Abs(info.damage-(100f-count*5f))>.01f)throw new Exception("Pump reduction incorrect "+count);
                }
                Call("CuiVitals","SetCharging",cui,true);
                var charged=new DamageInfo{damage=100f};vitals.OnIncomingDamageServer(charged);
                if(Mathf.Abs(charged.damage-28f)>.01f)throw new Exception("Charge guard mismatch");
                Call("CuiVitals","SetCharging",cui,false);
                if(!(bool)Call("CuiVitals","TrySpendFull",cui))throw new Exception("Full pump not spent");
                var retained=new DamageInfo{damage=100f};vitals.OnIncomingDamageServer(retained);
                if(Mathf.Abs(retained.damage-70f)>.01f)throw new Exception("Retained guard lost");
            });
            CheckOne("Cui charge scaling and one-use server windup permit",()=>{
                float Coefficient(float seconds)=>(float)Call("ShoulderCharge","DamageCoefficient",seconds);
                if(Coefficient(0f)!=3.5f || Coefficient(1f)!=8.75f || Coefficient(2f)!=14f || Coefficient(100f)!=14f)throw new Exception("Charge coefficients incorrect");
                var begin=T("CuiVitals").GetMethod("BeginCharge",Flags);var end=T("CuiVitals").GetMethod("EndChargeWindup",Flags);
                var release=T("CuiVitals").GetMethod("ReleaseCharge",Flags);var start=T("CuiVitals").GetField("chargeBegan",Flags);
                var args=new object[]{2f,0f};
                if((bool)release.Invoke(vitals,args))throw new Exception("Dash accepted without windup");
                begin.Invoke(vitals,null);start.SetValue(vitals,Time.fixedTime-1f);end.Invoke(vitals,null);
                args=new object[]{2f,0f};if(!(bool)release.Invoke(vitals,args) || (float)args[1]>1.101f)throw new Exception("Remote charge duration not bounded by server time");
                if((bool)release.Invoke(vitals,new object[]{2f,0f}))throw new Exception("Windup permit replayed");
                begin.Invoke(vitals,null);end.Invoke(vitals,null);T("CuiVitals").GetField("chargeExited",Flags).SetValue(vitals,Time.fixedTime-.3f);
                if((bool)release.Invoke(vitals,new object[]{1f,0f}))throw new Exception("Canceled windup accepted");
                begin.Invoke(vitals,null);if((bool)release.Invoke(vitals,new object[]{float.NaN,0f}))throw new Exception("NaN charge accepted");
                if(cui.skillLocator.utility.activationState.stateType!=T("CuiChargeWindup") || !cui.skillLocator.utility.skillDef.mustKeyPress || cui.skillLocator.special.skillDef.stockToConsume!=0)throw new Exception("Skill registration mismatch");
            });
            CheckOne("Cui instant team heal includes self, distant ally and excludes enemy/dead",()=>{
                var ally=Make("WeiGe",TeamIndex.Monster);var dead=Make("WeiGe",TeamIndex.Monster);var enemy=Make("WeiGe",TeamIndex.Void);
                ally.transform.position=Vector3.one*5000f;
                health.Networkhealth=10f;ally.healthComponent.Networkhealth=10f;dead.healthComponent.Networkhealth=0f;enemy.healthComponent.Networkhealth=10f;
                int count=(int)Call("ShakerAim","HealTeam",cui,false);
                if(count<2 || Mathf.Abs(health.health-40f)>.01f || Mathf.Abs(ally.healthComponent.health-40f)>.01f)throw new Exception("Normal team heal incorrect "+health.health+" "+ally.healthComponent.health);
                if(dead.healthComponent.health!=0f || enemy.healthComponent.health!=10f)throw new Exception("Invalid recipient healed");
                Call("ShakerAim","HealTeam",cui,true);
                if(Mathf.Abs(health.health-90f)>.01f || Mathf.Abs(ally.healthComponent.health-90f)>.01f)throw new Exception("Empowered team heal incorrect");
            });
        }
        finally
        {
            foreach(var go in fixtures)if(go)DestroyImmediate(go);
            if(!wasServer){NetworkServer.Shutdown();NetworkServer.dontListen=false;}
        }
    }
}


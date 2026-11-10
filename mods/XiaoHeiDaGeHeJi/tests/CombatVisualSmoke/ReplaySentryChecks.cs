using System;
using System.Collections.Generic;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
public partial class Smoke
{
    void CheckReplaySentry()
    {
        bool wasServer=NetworkServer.active;var roots=new List<GameObject>();
        CharacterBody caster=null,enemy=null,second=null;GameObject sentry=null,wall=null;
        try
        {
            if(!wasServer){NetworkServer.dontListen=true;if(!NetworkServer.Listen(0))throw new Exception("No sentry test server");}
            // A menu has no GlobalEventManager; native BulletAttack requires the
            // same event dispatcher that a real run creates before any hit.
            if(!GlobalEventManager.instance)
            {
                var events=new GameObject("ReplayHitEventFixture");roots.Add(events);
                events.AddComponent<GlobalEventManager>();
            }
            CharacterBody Make(GameObject prefab,Vector3 position,TeamIndex team)
            {
                var go=Instantiate(prefab,position,Quaternion.identity);roots.Add(go);
                foreach(var behaviour in go.GetComponents<Behaviour>())behaviour.enabled=false;
                var body=go.GetComponent<CharacterBody>();body.teamComponent.teamIndex=team;
                body.healthComponent.Networkhealth=1000;
                NetworkServer.Spawn(go);return body;
            }
            var def=Array.Find(Resources.FindObjectsOfTypeAll<SurvivorDef>(),d=>d.cachedName=="Xiaohei");
            Vector3 anchor=new Vector3(1000,1000,1000);
            caster=Make(def.bodyPrefab,anchor,TeamIndex.Player);
            enemy=Make(BodyCatalog.FindBodyPrefab("BeetleBody"),anchor+Vector3.forward*20,TeamIndex.Monster);
            second=Make(BodyCatalog.FindBodyPrefab("BeetleBody"),anchor+Vector3.back*25,TeamIndex.Monster);
            var barrel=(Vector3)Call("XiaoheiModel","ReplayMuzzle",15f);
            Physics.SyncTransforms();
            CheckOne("Giant sentry auto-acquires enemies in all directions and retargets dead enemies",()=>{
                HurtBox Acquire(HurtBox old)=>(HurtBox)Call("XiaoheiReplaySentry","Acquire",caster,anchor,barrel,old);
                var first=Acquire(null);if(!first || first.healthComponent!=enemy.healthComponent)throw new Exception("Nearest target not selected");
                caster.inputBank.aimDirection=Vector3.left;
                if(Acquire(first)!=first)throw new Exception("Target follows crosshair");
                enemy.healthComponent.Networkhealth=0;
                var next=Acquire(first);if(!next || next.healthComponent!=second.healthComponent)throw new Exception("Dead target not replaced by rear target");
                enemy.healthComponent.Networkhealth=1000;
            });
            CheckOne("Giant sentry rejects allies, out-of-range enemies and terrain occlusion",()=>{
                var hurt=enemy.mainHurtBox;
                bool Valid()=>(bool)Call("XiaoheiReplaySentry","ValidTarget",caster,hurt,anchor,barrel);
                if(!Valid())throw new Exception("Visible enemy rejected");
                enemy.teamComponent.teamIndex=TeamIndex.Player;if(Valid())throw new Exception("Ally accepted");enemy.teamComponent.teamIndex=TeamIndex.Monster;
                enemy.transform.position=anchor+Vector3.forward*70;Physics.SyncTransforms();if(Valid())throw new Exception("Out of range accepted");
                enemy.transform.position=anchor+Vector3.forward*20;Physics.SyncTransforms();
                wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.layer=LayerIndex.world.intVal;
                wall.transform.position=anchor+Vector3.forward*10;wall.transform.localScale=new Vector3(30,40,1);Physics.SyncTransforms();
                if(Valid())throw new Exception("Fires through wall");DestroyImmediate(wall);wall=null;
            });
            CheckOne("Single sentry preserves ground anchor and giant barrel across network snapshots",()=>{
                sentry=(GameObject)Call("XiaoheiReplaySentry","Spawn",caster,new Ray(caster.corePosition,Vector3.forward),3);roots.Add(sentry);
                var component=sentry.GetComponent(T("XiaoheiReplaySentry"));
                Vector3 point=sentry.transform.position;caster.transform.position+=Vector3.right*30;
                if((int)T("XiaoheiReplaySentry").GetField("total",Flags).GetValue(component)!=36)throw new Exception("Wrong full-highlight count");
                var writer=new NetworkWriter();T("XiaoheiReplaySentry").GetMethod("OnSerialize",Flags).Invoke(component,new object[]{writer,true});
                var copy=new GameObject("ReplayNetworkCopy");roots.Add(copy);copy.AddComponent<NetworkIdentity>();
                var replica=copy.AddComponent(T("XiaoheiReplaySentry"));
                T("XiaoheiReplaySentry").GetMethod("OnDeserialize",Flags).Invoke(replica,new object[]{new NetworkReader(writer.ToArray()),true});
                var muzzle=(Vector3)T("XiaoheiReplaySentry").GetProperty("Muzzle",Flags).GetValue(replica);
                if(copy.transform.position!=point || Vector3.Distance(muzzle,point+barrel)>.001f || muzzle.y-point.y<5f)
                    throw new Exception("Ground anchor or giant muzzle mismatch");
                foreach(int stack in new[]{0,1,2,3})if((int)Call("XiaoheiReplaySentry","ShotCount",stack)!=18+stack*6)throw new Exception("Wrong volley count");
                T("XiaoheiReplaySentry").GetField("began",Flags).SetValue(component,Time.fixedTime-3.8f);
                typeof(CharacterBody).GetProperty("damage",Flags).SetValue(caster,10f);
                typeof(HealthComponent).GetProperty("fullHealth",Flags)?.GetSetMethod(true)?.Invoke(enemy.healthComponent,new object[]{10000f});
                enemy.healthComponent.Networkhealth=10000;second.healthComponent.Networkhealth=10000;
                float before=enemy.healthComponent.health;
                Physics.SyncTransforms();
                T("XiaoheiReplaySentry").GetMethod("FixedUpdate",Flags).Invoke(component,null);
                if((int)T("XiaoheiReplaySentry").GetField("scheduled",Flags).GetValue(component)!=36 ||
                    (int)T("XiaoheiReplaySentry").GetField("fired",Flags).GetValue(component)!=36)
                    throw new Exception("Not all 36 rounds fired");
                if(enemy.healthComponent.health>=before)throw new Exception("Giant barrel rounds did not damage selected target");
                Logger.LogInfo("REPLAY DAMAGE CHECK 36 actual shots, target health "+before+" -> "+enemy.healthComponent.health);
            });
        }
        finally
        {
            if(wall)DestroyImmediate(wall);
            foreach(var root in roots)if(root)DestroyImmediate(root);
            if(!wasServer){NetworkServer.Shutdown();NetworkServer.dontListen=false;}
        }
    }
}

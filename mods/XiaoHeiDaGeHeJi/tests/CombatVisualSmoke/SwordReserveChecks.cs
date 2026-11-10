using System;
using System.Collections.Generic;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
public partial class Smoke
{
    void CheckSwordReserve()
    {
        CheckOne("Nine sword reserve counts, unique docking sockets and cloak visibility",()=>{
            bool wasServer=NetworkServer.active; GameObject go=null;
            try {
                if(!wasServer){NetworkServer.dontListen=true;if(!NetworkServer.Listen(0))throw new Exception("No reserve fixture server");}
                var def=Array.Find(Resources.FindObjectsOfTypeAll<SurvivorDef>(),d=>d.cachedName=="JieGe");
                go=Instantiate(def.bodyPrefab);foreach(var state in go.GetComponents<EntityStateMachine>())state.enabled=false;
                var body=go.GetComponent<CharacterBody>();
                var visual=go.GetComponent(T("JieSwordRackVisual"));
                var model=go.GetComponentInChildren<CharacterModel>();
                var skin=Array.Find(go.GetComponentsInChildren<SkinnedMeshRenderer>(true),r=>r.sharedMesh && r.sharedMesh.name=="JieGeSkin");
                if(!visual || !model || !skin)throw new Exception("Incomplete reserve visibility fixture");
                bool Visible()=>(bool)T("JieSwordRackVisual").GetMethod("OwnerVisible",Flags).Invoke(visual,null);
                model.invisibilityCount=0;skin.enabled=true;skin.forceRenderingOff=false;
                if(!Visible())throw new Exception("Visible owner hides stored swords");
                model.invisibilityCount=1;if(Visible())throw new Exception("Stored swords reveal cloak");
                model.invisibilityCount=0;skin.forceRenderingOff=true;if(Visible())throw new Exception("Forced hidden model reveals swords");
                skin.forceRenderingOff=false;skin.enabled=false;if(Visible())throw new Exception("Disabled model reveals swords");
                skin.enabled=true;if(!Visible())throw new Exception("Stored swords do not recover after reveal");
                var deployed=(BuffDef)T("JieSwords").GetField("deployedCount",Flags).GetValue(null);
                for(int count=0;count<=12;count++) {
                    body.SetBuffCount(deployed.buffIndex,count);
                    int reserve=(int)Call("JieSwords","ReadyCount",body);
                    if(reserve!=9-Mathf.Min(9,count))throw new Exception("Wrong reserve for "+count);
                }
                body.SetBuffCount(deployed.buffIndex,0);
                var rack=go.GetComponent(T("JieSwords"));
                var occupied=new HashSet<int>();
                for(int i=0;i<9;i++) {
                    int slot=(int)T("JieSwords").GetMethod("ClaimReturnSocket",Flags).Invoke(rack,null);
                    if(slot<0||slot>=9||!occupied.Add(slot))throw new Exception("Duplicate docking slot");
                    object[] args={body,i,Vector3.zero,Quaternion.identity};
                    T("JieSwordRackVisual").GetMethod("SocketPose",Flags).Invoke(null,args);
                    var point=(Vector3)args[2];
                    if(Vector3.Dot(point-body.corePosition,body.characterDirection ? body.characterDirection.forward : body.transform.forward)>=0f)throw new Exception("Reserve sword is not behind body");
                }
            }finally{if(go)DestroyImmediate(go);if(!wasServer){NetworkServer.Shutdown();NetworkServer.dontListen=false;}}
        });
    }
}

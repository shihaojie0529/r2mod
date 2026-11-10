using System;
using System.Collections;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
public partial class Smoke
{
    IEnumerator CheckEchoAndLanding()
    {
        GameObject echo=null,actor=null;Mesh baked=null;
        bool wasServer=NetworkServer.active;
        CheckOne("Xiaohei echo uses a transparent authored pose with a smooth expiry",()=>{
            try {
                if(!wasServer){NetworkServer.dontListen=true;if(!NetworkServer.Listen(0))throw new Exception("No echo fixture server");}
                var def=Array.Find(Resources.FindObjectsOfTypeAll<SurvivorDef>(),d=>d.cachedName=="Xiaohei");
                actor=Instantiate(def.bodyPrefab);foreach(var state in actor.GetComponents<EntityStateMachine>())state.enabled=false;
                echo=(GameObject)Call("XiaoheiModel","CreateEcho",actor.GetComponent<CharacterBody>(),Vector3.right*3,1f);
                if(!echo)throw new Exception("No authored echo");
                baked=echo.GetComponent<MeshFilter>().sharedMesh;
                var renderer=echo.GetComponent<MeshRenderer>();var material=renderer.sharedMaterial;
                if(material.GetFloat("_SrcBlend")!=5f || material.GetFloat("_DstBlend")!=1f || baked.colors.Length!=baked.vertexCount)
                    throw new Exception("Opaque echo or missing palette");
                var fade=echo.GetComponent(T("XiaoheiEchoMesh"));var update=T("XiaoheiEchoMesh").GetMethod("Update",Flags);
                var block=new MaterialPropertyBlock();
                T("XiaoheiEchoMesh").GetField("began",Flags).SetValue(fade,Time.time-.5f);update.Invoke(fade,null);
                renderer.GetPropertyBlock(block);float middle=block.GetColor("_TintColor").a;
                T("XiaoheiEchoMesh").GetField("began",Flags).SetValue(fade,Time.time-.98f);update.Invoke(fade,null);
                renderer.GetPropertyBlock(block);
                if(middle<.95f || block.GetColor("_TintColor").a>.05f)throw new Exception("Echo does not fade smoothly");
            }finally{if(actor)DestroyImmediate(actor);if(!wasServer){NetworkServer.Shutdown();NetworkServer.dontListen=false;}}
        });
        CheckOne("Heaven sword shockwave starts on landing rather than cast",()=>{
            var prefab=(GameObject)T("JieStormEffects").GetField("heaven",Flags).GetValue(null);
            var go=Instantiate(prefab);
            try {
                go.GetComponent<EffectComponent>().effectData=new EffectData{origin=Vector3.zero,scale=12f};
                var visual=(Behaviour)go.GetComponent(T("JieStormVisual"));visual.enabled=false;
                T("JieStormVisual").GetMethod("Start",Flags).Invoke(visual,null);
                if((bool)T("JieStormVisual").GetField("impactShown",Flags).GetValue(visual))throw new Exception("Impact precedes landing");
                T("JieStormVisual").GetField("began",Flags).SetValue(visual,Time.time-.20f);
                T("JieStormVisual").GetMethod("Animate",Flags).Invoke(visual,null);
                if(!(bool)T("JieStormVisual").GetField("impactShown",Flags).GetValue(visual))throw new Exception("Landing impact missing");
            }finally{DestroyImmediate(go);}
        });
        yield return new WaitForSeconds(1.1f);
        CheckOne("Xiaohei echo frees its baked pose mesh on expiry",()=>{
            if(echo || baked)throw new Exception("Echo pose leaked");
        });
        if(echo)DestroyImmediate(echo);
    }
}

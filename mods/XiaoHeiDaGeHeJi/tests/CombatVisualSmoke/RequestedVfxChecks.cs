using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
public partial class Smoke
{
    IEnumerator CheckRequestedVfx()
    {
        var active=new List<GameObject>();GameObject actor=null;Mesh giantMesh=null;
        CheckOne("Blink endpoints survive network state serialization",()=>{
            var state=Activator.CreateInstance(T("XiaoheiBlinkTravel"));
            T("XiaoheiBlinkTravel").GetField("departure",Flags).SetValue(state,new Vector3(1,2,3));
            T("XiaoheiBlinkTravel").GetField("destination",Flags).SetValue(state,new Vector3(13,2,3));
            var writer=new NetworkWriter();T("XiaoheiBlinkTravel").GetMethod("OnSerialize",Flags).Invoke(state,new object[]{writer});
            var copy=Activator.CreateInstance(T("XiaoheiBlinkTravel"));
            T("XiaoheiBlinkTravel").GetMethod("OnDeserialize",Flags).Invoke(copy,new object[]{new NetworkReader(writer.ToArray())});
            if((Vector3)T("XiaoheiBlinkTravel").GetField("destination",Flags).GetValue(copy)!=new Vector3(13,2,3))throw new Exception("Lost destination");
        });
        CheckOne("Blink and recall show anchored paths and visible endpoint bursts",()=>{
            foreach(bool recall in new[]{false,true})
            {
                var from=new Vector3(3,2,1);var to=from+Vector3.right*12;
                var go=(GameObject)Call("SkillBlinkVisual","Show",from,to,false,recall);active.Add(go);
                var line=go.transform.Find("BlinkEnergyCore").GetComponent<LineRenderer>();
                var geo=go.GetComponent(T("RadiantGeometry"));T("RadiantGeometry").GetField("began",Flags).SetValue(geo,Time.time-.2f);
                T("RadiantGeometry").GetMethod("Update",Flags).Invoke(geo,null);
                if(Vector3.Distance(line.transform.TransformPoint(line.GetPosition(1)),to)>.01f)throw new Exception("Endpoint drift");
                if(go.GetComponentsInChildren<ParticleSystem>().Length!=2 || go.GetComponentsInChildren<LineRenderer>().Length<17)throw new Exception("Endpoint cues missing");
            }
            if(Call("SkillBlinkVisual","Show",Vector3.zero,Vector3.zero,false,false)!=null)throw new Exception("Zero travel effect");
        });
        bool wasServer=NetworkServer.active;
        CheckOne("Replay avatar is fifteen metres with luminous geometry and bounded particles",()=>{
            if(!wasServer){NetworkServer.dontListen=true;if(!NetworkServer.Listen(0))throw new Exception("No fixture server");}
            var def=Array.Find(Resources.FindObjectsOfTypeAll<SurvivorDef>(),d=>d.cachedName=="Xiaohei");
            actor=Instantiate(def.bodyPrefab);foreach(var state in actor.GetComponents<EntityStateMachine>())state.enabled=false;
            // This menu fixture has no CharacterMaster or input player. Keep only
            // the model live; native interaction/equipment scripts require a real run.
            foreach(var behaviour in actor.GetComponents<Behaviour>())behaviour.enabled=false;
            actor.GetComponent<HealthComponent>().Networkhealth=100f;
            var go=(GameObject)Call("XiaoheiReplayAvatar","Show",actor.GetComponent<CharacterBody>(),Vector3.right,Vector3.forward);active.Add(go);
            if(!go)throw new Exception("No giant");
            var mesh=go.GetComponentInChildren<MeshFilter>();giantMesh=mesh.sharedMesh;
            float height=mesh.GetComponent<MeshRenderer>().bounds.size.y;
            if(Mathf.Abs(height-15f)>.15f)throw new Exception("Giant height "+height);
            if(go.GetComponentsInChildren<LineRenderer>().Length<30)throw new Exception("Mandala missing");
            foreach(var p in go.GetComponentsInChildren<ParticleSystem>())if(p.main.maxParticles>32 || p.main.loop)throw new Exception("Unbounded emission");
            var visual=go.GetComponent(T("XiaoheiReplayAvatar"));
            T("XiaoheiReplayAvatar").GetField("began",Flags).SetValue(visual,Time.time-.35f);
            T("XiaoheiReplayAvatar").GetMethod("LateUpdate",Flags).Invoke(visual,null);
            CaptureRequestedVfx(go,actor);
        });
        CheckOne("Giant body remains at the cast point after owner movement",()=>{
            var go=active[active.Count-1];var point=go.transform.position;var rotation=go.transform.rotation;
            actor.transform.position+=new Vector3(20,3,12);
            actor.GetComponent<InputBankTest>().aimDirection=Vector3.back;
            T("XiaoheiReplayAvatar").GetMethod("LateUpdate",Flags).Invoke(go.GetComponent(T("XiaoheiReplayAvatar")),null);
            if(Vector3.Distance(point,go.transform.position)>.001f || Quaternion.Angle(rotation,go.transform.rotation)>.01f)
                throw new Exception("Giant follows caster or camera");
            if(go.GetComponentsInChildren<MeshFilter>().Length!=1)throw new Exception("Extra small replay body");
        });
        yield return new WaitForSeconds(4.2f);
        CheckOne("New movement and giant effects free objects and baked meshes",()=>{
            foreach(var go in active)if(go)throw new Exception("Leaked "+go.name);
            if(giantMesh)throw new Exception("Giant mesh leaked");
        });
        if(actor)DestroyImmediate(actor);
        if(!wasServer){NetworkServer.Shutdown();NetworkServer.dontListen=false;}
    }
    void CaptureRequestedVfx(GameObject giant,GameObject actor)
    {
        const int width=640,height=480,layer=30;
        var atlas=new Texture2D(width*2,height*2,TextureFormat.RGB24,false);
        var camObject=new GameObject("RequestedVfxPreview");var camera=camObject.AddComponent<Camera>();camera.enabled=false;
        camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.035f,.065f);
        camera.cullingMask=1<<layer;camera.nearClipPlane=.01f;camera.farClipPlane=150;
        var target=new RenderTexture(width*2,height*2,24);var resolved=new RenderTexture(width,height,0);
        camera.targetTexture=target;var previous=RenderTexture.active;
        try{
            for(int tile=0;tile<4;tile++)
            {
                GameObject root=null;
                try{
                    if(tile<2)root=(GameObject)Call("SkillBlinkVisual","Show",new Vector3(-5,1,0),new Vector3(5,1,0),tile==1,false);
                    else if(tile==2){
                        root=new GameObject("WeiSelectPreview");
                        foreach(string name in new[]{"SelectAll","SelectionLink"}){
                            var child=new GameObject(name);child.transform.SetParent(root.transform,false);
                            child.AddComponent<EffectComponent>().effectData=new EffectData{origin=name=="SelectAll"?Vector3.zero:new Vector3(2,2,0),start=Vector3.up,scale=name=="SelectAll"?4.5f:1f,rotation=Quaternion.identity};
                            var burst=child.AddComponent(T("SkillBurst"));T("SkillBurst").GetField("kind",Flags).SetValue(burst,Enum.Parse(T("SpectacleKind"),name));
                            T("SkillBurst").GetMethod("Start",Flags).Invoke(burst,null);((Behaviour)burst).enabled=false;
                        }
                    }else root=giant;
                    foreach(var p in root.GetComponentsInChildren<ParticleSystem>())p.Simulate(.12f,true,false,true);
                    foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=layer;
                    if(tile==3)foreach(var t in actor.GetComponentsInChildren<Transform>(true))t.gameObject.layer=layer;
                    Vector3 center=tile==3?giant.transform.position+Vector3.up*7.5f:Vector3.up;
                    camera.orthographicSize=tile==3?9f:4.8f;
                    camera.transform.position=center+new Vector3(1.5f,3f,-26);
                    camera.transform.LookAt(center);
                    camera.Render();Graphics.Blit(target,resolved);RenderTexture.active=resolved;
                    var shot=new Texture2D(width,height,TextureFormat.RGB24,false);shot.ReadPixels(new Rect(0,0,width,height),0,0);shot.Apply();
                    atlas.SetPixels(tile%2*width,(1-tile/2)*height,width,height,shot.GetPixels());DestroyImmediate(shot);
                    foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=0;
                }finally{if(root && tile!=3)DestroyImmediate(root);}
            }
            atlas.Apply();File.WriteAllBytes("D:/project/r2mod/mods/XiaoHeiDaGeHeJi/dist/requested-vfx-0.5.17.png",atlas.EncodeToPNG());
        }finally{RenderTexture.active=previous;camera.targetTexture=null;target.Release();resolved.Release();DestroyImmediate(target);DestroyImmediate(resolved);DestroyImmediate(atlas);DestroyImmediate(camObject);}
    }
}

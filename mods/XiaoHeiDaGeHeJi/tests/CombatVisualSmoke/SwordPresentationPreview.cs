using System;
using System.IO;
using System.Linq;
using System.Reflection;
using RoR2;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Networking;

internal static class SwordPresentationPreview
{
    const BindingFlags F=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    internal static void Capture(Assembly mod,string output)
    {
        Type T(string n)=>mod.GetType("RiftSurvivor."+n,true);
        object Call(string t,string m,params object[] args)=>T(t).GetMethod(m,F).Invoke(null,args);
        const int w=768,h=512,layer=30;
        var def=Array.Find(Resources.FindObjectsOfTypeAll<SurvivorDef>(),d=>d.cachedName=="JieGe");
        var atlas=new Texture2D(w*2,h*2,TextureFormat.RGB24,false);
        var target=new RenderTexture(w,h,24);var high=new RenderTexture(w*2,h*2,24);
        var camObject=new GameObject("SwordPresentationCamera");var cam=camObject.AddComponent<Camera>();
        cam.enabled=false;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.035f,.055f,.075f);
        cam.cullingMask=1<<layer;cam.nearClipPlane=.01f;cam.farClipPlane=120f;cam.aspect=w/(float)h;cam.targetTexture=high;
        var sceneLights=UnityEngine.Object.FindObjectsOfType<Light>();
        var lightMasks=sceneLights.Select(l=>l.cullingMask).ToArray();
        for(int i=0;i<sceneLights.Length;i++)sceneLights[i].cullingMask&=~(1<<layer);
        var lightObject=new GameObject("SwordPreviewLight");var light=lightObject.AddComponent<Light>();
        light.type=LightType.Directional;light.intensity=.65f;light.cullingMask=1<<layer;
        light.transform.rotation=Quaternion.Euler(35,-25,0);
        var oldTarget=RenderTexture.active;var oldMode=RenderSettings.ambientMode;var oldAmbient=RenderSettings.ambientLight;
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.10f,.12f,.15f);
        var jade=new Color(.12f,.94f,.79f);
        var rigReport=new System.Text.StringBuilder();
        bool wasServer=NetworkServer.active,oldDontListen=NetworkServer.dontListen;
        try
        {
            if(!wasServer){NetworkServer.dontListen=true;if(!NetworkServer.Listen(0))throw new InvalidOperationException("No preview fixture server");}
            for(int panel=0;panel<4;panel++)
            {
                var root=new GameObject("SwordPresentation"+panel);
                Mesh baked=null;
                try
                {
                    cam.orthographic=true;
                    if(panel<2)
                    {
                        var fixture=UnityEngine.Object.Instantiate(def.bodyPrefab,Vector3.zero,Quaternion.identity);fixture.transform.SetParent(root.transform,true);
                        foreach(var b in fixture.GetComponentsInChildren<Behaviour>(true))b.enabled=false;
                        foreach(var r in fixture.GetComponentsInChildren<Renderer>(true))r.enabled=false;
                        foreach(var c in fixture.GetComponentsInChildren<Collider>(true))c.enabled=false;
                        var body=fixture.GetComponent<CharacterBody>();
                        if(body.characterDirection)body.characterDirection.forward=Vector3.forward;
                        // The visible mesh and SocketPose share one gameplay body and rig.
                        var skin=Array.Find(fixture.GetComponentsInChildren<SkinnedMeshRenderer>(true),
                            r=>r.sharedMesh && r.sharedMesh.name=="JieGeSkin");
                        if(!skin || !skin.rootBone)throw new InvalidOperationException("Actual JieGeSkin rig is missing");
                        baked=new Mesh{name="JieGameplaySwordRackSnapshot"};
                        skin.BakeMesh(baked,false);baked.RecalculateBounds();
                        var snapshot=new GameObject("JieGameplaySkinSnapshot");snapshot.transform.SetParent(skin.transform,false);
                        snapshot.AddComponent<MeshFilter>().sharedMesh=baked;
                        var snapshotView=snapshot.AddComponent<MeshRenderer>();snapshotView.sharedMaterials=skin.sharedMaterials;
                        snapshotView.shadowCastingMode=ShadowCastingMode.Off;
                        snapshotView.lightProbeUsage=LightProbeUsage.Off;snapshotView.reflectionProbeUsage=ReflectionProbeUsage.Off;
                        string header="panel="+panel+" root="+fixture.transform.position+" core="+body.corePosition+
                            " bodyFoot="+body.footPosition+" rig="+skin.rootBone.position+" rigScale="+skin.rootBone.lossyScale+
                            " skinBounds="+snapshotView.bounds;
                        Debug.Log("VFX sword actual rig "+header);rigReport.AppendLine(header);
                        foreach(var name in new[]{"Hips","Chest","Head","LeftFoot","RightFoot"})
                        {
                            var bone=Array.Find(skin.bones,node=>node && node.name==name);
                            if(!bone)throw new InvalidOperationException("Missing actual Jie bone "+name);
                            string line=name+" world="+bone.position+" relativeToCore="+(bone.position-body.corePosition)+
                                " rigLocal="+skin.rootBone.InverseTransformPoint(bone.position);
                            Debug.Log("VFX sword actual rig "+line);rigReport.AppendLine(line);
                        }
                        int stored=panel==0?9:5;
                        for(int i=0;i<9;i++)
                        {
                            object[] args={body,i,Vector3.zero,Quaternion.identity};
                            T("JieSwordRackVisual").GetMethod("SocketPose",F).Invoke(null,args);
                            var home=(Vector3)args[2];var pose=(Quaternion)args[3];
                            if(panel==0)rigReport.AppendLine("socket="+i+" world="+home+" relativeToCore="+(home-body.corePosition));
                            var sword=new GameObject("ReserveSword"+i).transform;sword.SetParent(root.transform,false);
                            Call("JieSwordArt","Attach",sword,jade);sword.localScale=Vector3.one*1.02f;
                            if(i<stored)sword.SetPositionAndRotation(home,pose);
                            else
                            {
                                int index=i-stored;float sign=index%2==0?-1f:1f;
                                var from=home+new Vector3(sign*(2f+.22f*index),.7f+.33f*index,-.85f);
                                var now=Vector3.Lerp(from,home,.56f)+Vector3.up*.44f;
                                sword.SetPositionAndRotation(now,Quaternion.LookRotation((home-now).normalized));
                                var trails=(TrailRenderer[])Call("JieSwordArt","Trail",sword,jade,.22f,.23f);
                                var points=new Vector3[16];
                                for(int p=0;p<16;p++){float t=Mathf.Lerp(.12f,.56f,p/15f);points[p]=Vector3.Lerp(from,home,t)+Vector3.up*(.45f*Mathf.Sin(t*Mathf.PI));}
                                foreach(var trail in trails){trail.AddPositions(points);trail.emitting=false;}
                            }
                        }
                        Frame(root,cam,new Vector3(.04f,.08f,-1f));
                    }
                    else if(panel>=2)
                    {
                        var display=UnityEngine.Object.Instantiate(def.displayPrefab);display.transform.SetParent(root.transform,false);
                        foreach(var b in display.GetComponentsInChildren<MonoBehaviour>(true))b.enabled=false;
                        cam.orthographic=false;cam.fieldOfView=70;
                        cam.transform.SetPositionAndRotation(new Vector3(0,2.6f,-5),Quaternion.identity);
                        var avatarObject=new GameObject("LocalAvatar");avatarObject.transform.SetParent(root.transform,false);
                        var avatar=(MonoBehaviour)avatarObject.AddComponent(T("JieCelestialAvatar"));avatar.enabled=false;
                        T("JieCelestialAvatar").GetMethod("Start",F).Invoke(avatar,null);
                        T("JieCelestialAvatar").GetField("started",F).SetValue(avatar,Time.time-.55f);
                        T("JieCelestialAvatar").GetMethod("LateUpdate",F).Invoke(avatar,null);
                        object[] args={cam,Vector3.zero,Quaternion.identity};
                        T("JieCelestialAvatar").GetMethod("LocalViewPose",F).Invoke(null,args);
                        avatarObject.transform.SetPositionAndRotation((Vector3)args[1],(Quaternion)args[2]);
                        T("JieCelestialAvatar").GetMethod("SetLocalPresentation",F).Invoke(avatar,new object[]{true});
                        if(panel==3)
                        {
                            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.SetParent(root.transform,false);
                            wall.name="OpaqueWallAtTwoMetres";wall.transform.position=cam.transform.position+cam.transform.forward*2f;
                            wall.transform.localScale=new Vector3(12,12,.2f);
                        }
                        var marker=GameObject.CreatePrimitive(PrimitiveType.Sphere);marker.transform.SetParent(root.transform,false);
                        marker.transform.position=cam.transform.position+cam.transform.forward*2f;
                        marker.transform.localScale=Vector3.one*.015f;
                    }
                    else
                    {
                        var sword=new GameObject("SwordDetail").transform;sword.SetParent(root.transform,false);
                        Call("JieSwordArt","Attach",sword,jade);sword.localRotation=Quaternion.Euler(-90,0,-22);
                        Frame(root,cam,new Vector3(.12f,.06f,-1f));
                    }
                    foreach(var child in root.GetComponentsInChildren<Transform>(true))child.gameObject.layer=layer;
                    cam.Render();Graphics.Blit(high,target);RenderTexture.active=target;
                    var tile=new Texture2D(w,h,TextureFormat.RGB24,false);
                    try{tile.ReadPixels(new Rect(0,0,w,h),0,0);tile.Apply();
                        var pixels=tile.GetPixels32();var first=pixels[0];int visible=0;
                        foreach(var pixel in pixels)if(Mathf.Abs(pixel.r-first.r)+Mathf.Abs(pixel.g-first.g)+Mathf.Abs(pixel.b-first.b)>30)visible++;
                        if(visible<300)throw new InvalidOperationException("Blank presentation panel "+panel);
                        atlas.SetPixels((panel%2)*w,(1-panel/2)*h,w,h,tile.GetPixels());}
                    finally{UnityEngine.Object.DestroyImmediate(tile);}
                }finally{UnityEngine.Object.DestroyImmediate(root);if(baked)UnityEngine.Object.DestroyImmediate(baked);}
            }
            atlas.Apply();Directory.CreateDirectory(System.IO.Path.GetDirectoryName(output));File.WriteAllBytes(output,atlas.EncodeToPNG());
            File.WriteAllText(output+".txt","Top: nine stored swords; five stored with four illustrative returning paths. Bottom: local 70 degree view; same avatar behind an opaque wall two metres from camera.\nActual meshes, materials and SocketPose. Return path panel is a presentation fixture, not a network replay.\n"+rigReport);
        }
        finally
        {
            for(int i=0;i<sceneLights.Length;i++)if(sceneLights[i])sceneLights[i].cullingMask=lightMasks[i];
            RenderTexture.active=oldTarget;RenderSettings.ambientMode=oldMode;RenderSettings.ambientLight=oldAmbient;
            cam.targetTexture=null;target.Release();high.Release();
            if(!wasServer){NetworkServer.Shutdown();NetworkServer.dontListen=oldDontListen;}
            UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(high);UnityEngine.Object.DestroyImmediate(atlas);
            UnityEngine.Object.DestroyImmediate(camObject);UnityEngine.Object.DestroyImmediate(lightObject);
        }
    }
    static void Frame(GameObject root,Camera cam,Vector3 direction)
    {
        var views=root.GetComponentsInChildren<Renderer>().Where(r=>r.enabled&&!r.forceRenderingOff).ToArray();
        var bounds=views[0].bounds;foreach(var view in views)bounds.Encapsulate(view.bounds);
        Debug.Log("VFX sword frame "+root.name+" bounds="+bounds);
        if(bounds.size.magnitude>25f) {
            foreach(var view in views)Debug.Log("VFX view "+view.name+" "+view.bounds);
            throw new InvalidOperationException("Preview has unrelated remote fixture bounds");
        }
        cam.transform.position=bounds.center+direction.normalized*8f;cam.transform.LookAt(bounds.center);
        float extent=0;
        foreach(int x in new[]{-1,1})foreach(int y in new[]{-1,1})foreach(int z in new[]{-1,1})
        {
            var p=cam.transform.InverseTransformPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3(x,y,z)));
            extent=Mathf.Max(extent,Mathf.Abs(p.x)/cam.aspect,Mathf.Abs(p.y));
        }
        cam.orthographicSize=extent*1.1f;
    }
}


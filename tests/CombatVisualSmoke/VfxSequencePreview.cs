using System;
using System.IO;
using System.Reflection;
using RoR2;
using UnityEngine;
internal static class VfxSequencePreview
{
    const BindingFlags F=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    internal static void Capture(Assembly mod,string output)
    {
        Type T(string n)=>mod.GetType("RiftSurvivor."+n,true);
        const int width=320,height=240,layer=30;
        var names=Enum.GetNames(T("SpectacleKind"));
        var atlas=new Texture2D(width*4,height*names.Length,TextureFormat.RGB24,false);
        var camObject=new GameObject("VfxSequenceCamera");
        var camera=camObject.AddComponent<Camera>();camera.enabled=false;
        camera.orthographic=true;camera.orthographicSize=3.3f;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.045f,.06f);
        camera.cullingMask=1<<layer;camera.nearClipPlane=.01f;camera.farClipPlane=100f;
        camera.transform.position=new Vector3(0,0,-14);camera.transform.rotation=Quaternion.identity;
        var target=new RenderTexture(width*2,height*2,24);camera.targetTexture=target;
        var resolved=new RenderTexture(width,height,0);
        var previous=RenderTexture.active;var times=new[]{.025f,.10f,.22f,.48f};
        try
        {
            for(int row=0;row<names.Length;row++)for(int col=0;col<4;col++)
            {
                var go=new GameObject("Sequence"+names[row]);
                try {
                    go.AddComponent<EffectComponent>().effectData=new EffectData{
                        origin=Vector3.zero,start=new Vector3(2,1,0),rotation=Quaternion.identity,scale=2f};
                    var burst=go.AddComponent(T("SkillBurst"));
                    T("SkillBurst").GetField("kind",F).SetValue(burst,Enum.Parse(T("SpectacleKind"),names[row]));
                    T("SkillBurst").GetMethod("Start",F).Invoke(burst,null);((Behaviour)burst).enabled=false;
                    float t=times[col];T("SkillBurst").GetField("began",F).SetValue(burst,Time.time-t);
                    T("SkillBurst").GetMethod("Update",F).Invoke(burst,null);
                    foreach(var geo in go.GetComponentsInChildren(T("RadiantGeometry"))) {
                        float life=(float)T("RadiantGeometry").GetField("lifetime",F).GetValue(geo);if(life<=0f)continue;
                        T("RadiantGeometry").GetField("began",F).SetValue(geo,Time.time-t);
                        T("RadiantGeometry").GetMethod("Update",F).Invoke(geo,null);
                    }
                    foreach(var particle in go.GetComponentsInChildren<ParticleSystem>())particle.Simulate(t,true,false,true);
                    foreach(var child in go.GetComponentsInChildren<Transform>(true))child.gameObject.layer=layer;
                    camera.Render();Graphics.Blit(target,resolved);RenderTexture.active=resolved;
                    var tile=new Texture2D(width,height,TextureFormat.RGB24,false);
                    tile.ReadPixels(new Rect(0,0,width,height),0,0);tile.Apply();
                    atlas.SetPixels(col*width,(names.Length-1-row)*height,width,height,tile.GetPixels());
                    UnityEngine.Object.DestroyImmediate(tile);
                } finally {UnityEngine.Object.DestroyImmediate(go);}
            }
            atlas.Apply();Directory.CreateDirectory(System.IO.Path.GetDirectoryName(output));File.WriteAllBytes(output,atlas.EncodeToPNG());
            File.WriteAllText(output+".txt","Rows: "+string.Join(", ",names)+"\nColumns: 25, 100, 220, 480 ms\nActual Unity materials; fixed framing; no bloom/postprocessing.\n");
        }
        finally {
            RenderTexture.active=previous;camera.targetTexture=null;target.Release();resolved.Release();
            UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(resolved);
            UnityEngine.Object.DestroyImmediate(atlas);UnityEngine.Object.DestroyImmediate(camObject);
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using RoR2;
using UnityEngine;
public partial class Smoke
{
    IEnumerator CheckVfxPolish()
    {
        CheckOne("Transparent soft materials share cached feather masks",()=>{
            var a=(Material)Call("CombatVisuals","Stroke","TestStroke");
            var b=(Material)Call("CombatVisuals","Stroke","TestStroke2");
            var c=(Material)Call("CombatVisuals","SoftRadiance","TestMote",false);
            try {
                if(!a.shader.isSupported || a.GetFloat("_SrcBlend")!=5f || a.GetFloat("_DstBlend")!=1f || (a.HasProperty("_ZWrite") && a.GetFloat("_ZWrite")!=0f))
                    throw new Exception("Incorrect transparent shader state");
                var properties=new List<string>();
                for(int i=0;i<a.shader.GetPropertyCount();i++)properties.Add(a.shader.GetPropertyName(i));
                Logger.LogInfo("VFX shader properties "+string.Join(",",properties));
                if(a.mainTexture==Texture2D.whiteTexture || a.mainTexture!=b.mainTexture || a.mainTexture==c.mainTexture)
                    throw new Exception("Missing feather mask/cache");
            } finally {DestroyImmediate(a);DestroyImmediate(b);DestroyImmediate(c);}
        });
        CheckOne("Local avatar ignores walls without mutating world materials",()=>{
            var root=new GameObject("AvatarMaterialFixture");
            try
            {
                var avatar=(Behaviour)root.AddComponent(T("JieCelestialAvatar"));avatar.enabled=false;
                T("JieCelestialAvatar").GetMethod("Start",Flags).Invoke(avatar,null);
                var renderers=root.GetComponentsInChildren<Renderer>();
                var original=new Material[renderers.Length][];
                var occlusion=new bool[renderers.Length];
                var z=new Dictionary<Material,float>();
                for(int i=0;i<renderers.Length;i++) {
                    original[i]=renderers[i].sharedMaterials;
                    occlusion[i]=renderers[i].allowOcclusionWhenDynamic;
                    foreach(var material in original[i])if(material.HasProperty("_ZTest"))z[material]=material.GetFloat("_ZTest");
                }
                var method=T("JieCelestialAvatar").GetMethod("SetLocalPresentation",Flags);
                method.Invoke(avatar,new object[]{true});
                var variants=new Material[renderers.Length][];
                for(int i=0;i<renderers.Length;i++) {
                    variants[i]=renderers[i].sharedMaterials;
                    if(renderers[i].allowOcclusionWhenDynamic)throw new Exception("Local projection permits baked occlusion");
                    foreach(var material in variants[i])
                        if(!material.HasProperty("_ZTest") || material.GetFloat("_ZTest")!=8f || !material.shader.isSupported)
                            throw new Exception("Local projection can be occluded by terrain");
                }
                foreach(var pair in z)if(pair.Key.GetFloat("_ZTest")!=pair.Value)throw new Exception("Shared world shader changed");
                method.Invoke(avatar,new object[]{false});
                for(int i=0;i<renderers.Length;i++) {
                    if(renderers[i].allowOcclusionWhenDynamic!=occlusion[i])throw new Exception("Observer occlusion flag not restored");
                    for(int j=0;j<original[i].Length;j++)
                        if(renderers[i].sharedMaterials[j]!=original[i][j])throw new Exception("Observer materials not restored");
                }
                method.Invoke(avatar,new object[]{true});
                for(int i=0;i<renderers.Length;i++)
                    for(int j=0;j<variants[i].Length;j++)
                        if(renderers[i].sharedMaterials[j]!=variants[i][j])throw new Exception("Projection materials not cached");
            }finally{DestroyImmediate(root);}
        });
        var active=new List<GameObject>();
        CheckOne("Every combat burst has bounded particles and finite cleanup",()=>{
            foreach(object kind in Enum.GetValues(T("SpectacleKind")))
            {
                var go=new GameObject("PolishLifecycle"+kind);active.Add(go);
                go.AddComponent<EffectComponent>().effectData=new EffectData{
                    origin=Vector3.zero,start=new Vector3(7,3,1),rotation=Quaternion.identity,scale=2f};
                var component=go.AddComponent(T("SkillBurst"));
                T("SkillBurst").GetField("kind",Flags).SetValue(component,kind);
                T("SkillBurst").GetMethod("Start",Flags).Invoke(component,null);
                ((Behaviour)component).enabled=false;
                foreach(var particle in go.GetComponentsInChildren<ParticleSystem>())
                    if(particle.main.loop || particle.main.maxParticles>32)throw new Exception("Unbounded particles "+kind);
                if(kind.ToString()=="Lightning") {
                    var geo=go.GetComponent(T("RadiantGeometry"));
                    T("RadiantGeometry").GetField("began",Flags).SetValue(geo,Time.time-.1f);
                    T("RadiantGeometry").GetMethod("Update",Flags).Invoke(geo,null);
                    var line=go.transform.Find("ElectricArc").GetComponent<LineRenderer>();
                    if(Vector3.Distance(line.transform.TransformPoint(line.GetPosition(line.positionCount-1)),new Vector3(7,3,1))>.001f)
                        throw new Exception("Lightning endpoint drift");
                }
                if(kind.ToString()=="Punch") {
                    var geo=go.GetComponent(T("RadiantGeometry"));
                    T("RadiantGeometry").GetField("began",Flags).SetValue(geo,Time.time-.1f);
                    T("RadiantGeometry").GetMethod("Update",Flags).Invoke(geo,null);
                    var line=go.transform.Find("FistSweep").GetComponent<LineRenderer>();
                    if(line.widthCurve.Evaluate(0f)>.001f || line.widthCurve.Evaluate(1f)>.001f || line.widthCurve.Evaluate(.25f)<.5f)
                        throw new Exception("Arc taper was flattened during fade");
                }
            }
        });
        yield return new WaitForSeconds(1.05f);
        CheckOne("All burst renderers expire after the animation",()=>{
            foreach(var go in active)if(go)throw new Exception("Leaked "+go.name);
        });
        foreach(var go in active)if(go)DestroyImmediate(go);
        CheckOne("Local celestial avatar remains visible and clears crosshair",()=>{
            var go=new GameObject("AvatarCameraFixture");
            try {
                var camera=go.AddComponent<Camera>();camera.enabled=false;
                foreach(float fov in new[]{50f,70f,100f})foreach(float aspect in new[]{4f/3f,16f/9f,21f/9f})
                {
                    camera.fieldOfView=fov;camera.aspect=aspect;
                    foreach(float pitch in new[]{-65f,0f,65f})
                    {
                        camera.transform.rotation=Quaternion.Euler(pitch,35f,0f);
                        object[] args={camera,Vector3.zero,Quaternion.identity};
                        T("JieCelestialAvatar").GetMethod("LocalViewPose",Flags).Invoke(null,args);
                        var pos=(Vector3)args[1];var rot=(Quaternion)args[2];
                        // Root is the bottom of a twelve metre figure.
                        var center=camera.WorldToViewportPoint(pos+rot*new Vector3(0,5,0));
                        if(center.z<=camera.nearClipPlane || center.x<0f || center.x>.45f || center.y<0f || center.y>1f)
                            throw new Exception("Avatar out of local frame "+fov+"/"+aspect+"/"+pitch+" "+center);
                        // Conservative bounds include all orbit blades and depth.
                        foreach(float x in new[]{-8f,8f})foreach(float y in new[]{-3f,13f})foreach(float zpos in new[]{-3f,3f}) {
                            var edge=camera.WorldToViewportPoint(pos+rot*new Vector3(x,y,zpos));
                            if(edge.z<=camera.nearClipPlane || edge.x<0f || edge.x>.5f || edge.y<0f || edge.y>1f)
                                throw new Exception("Avatar bounds clipped or cover aim "+fov+"/"+aspect+"/"+pitch+" "+edge);
                        }
                    }
                }
            } finally {DestroyImmediate(go);}
        });
    }
}

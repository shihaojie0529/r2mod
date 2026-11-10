using System;
using System.Collections;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

public partial class Smoke
{
    IEnumerator CheckSpectacle()
    {
        CheckOne("Additive glow shader supports transparency",()=>{
            var material=(Material)Call("CombatVisuals","Radiance","SmokeRadiance");
            try {
                if(!material.shader.isSupported || !material.HasProperty("_SrcBlend") || !material.HasProperty("_DstBlend"))throw new Exception("Glow shader cannot blend");
                if(material.GetFloat("_SrcBlend")!=5f || material.GetFloat("_DstBlend")!=1f)throw new Exception("Incorrect additive blend");
            }finally{DestroyImmediate(material);}
        });
        CheckOne("Six distinct furniture meshes and damage profiles",()=>{
            for(int i=0;i<6;i++)
            {
                var go=new GameObject("FurnitureFixture");
                try
                {
                    object kind=Enum.ToObject(T("FurnitureKind"),i);
                    Call("ExtendedFurnitureArt","Attach",go.transform,kind);
                    var mesh=go.GetComponent<MeshFilter>().sharedMesh;
                    if(!mesh || mesh.vertexCount<24 || mesh.bounds.size.y<.3f)throw new Exception("Furniture geometry missing "+kind);
                    if(!go.GetComponent<MeshRenderer>().sharedMaterial.shader.isSupported)throw new Exception("Furniture shader "+kind);
                    float multiplier=(float)Call("FurnitureCatalog","Multiplier",kind);
                    if(multiplier<.8f || multiplier>1.5f)throw new Exception("Invalid damage profile "+kind);
                }finally{DestroyImmediate(go);}
            }
        });
        CheckOne("Giant gold fist and celestial sword immortal",()=>{
            var fist=new GameObject("FistFixture");var avatar=new GameObject("AvatarFixture");
            try
            {
                Call("GoldenFist","Attach",fist.transform);
                if(fist.GetComponent<MeshFilter>().sharedMesh.bounds.size.x<1f)throw new Exception("Fist too small");
                var comp=avatar.AddComponent(T("JieCelestialAvatar"));
                T("JieCelestialAvatar").GetMethod("Start",Flags).Invoke(comp,null);
                T("JieCelestialAvatar").GetMethod("LateUpdate",Flags).Invoke(comp,null);
                if(avatar.GetComponentsInChildren<MeshRenderer>().Length<49)throw new Exception("Immortal/orbit swords missing");
                var body=avatar.transform.Find("SwordImmortalDharmaBody").GetComponent<MeshFilter>().sharedMesh;
                if(body.bounds.size.y * 1.2f<11f)throw new Exception("Celestial figure too small");
                foreach(var view in avatar.GetComponentsInChildren<Renderer>())
                    if(!view.sharedMaterial.shader.isSupported)throw new Exception("Unsupported celestial shader");
            }finally{DestroyImmediate(fist);DestroyImmediate(avatar);}
        });
        CheckOne("Ordinary movement has no automatic VFX emitter",()=>{
            if(Mod.GetType("RiftSurvivor.MovementRadiance",false)!=null)
                throw new Exception("Movement-driven ribbons and pulses must be removed");
        });
        Component burst=null;
        CheckOne("Networked spectacle catalog and lightning",()=>{
            var effects=(System.Collections.IDictionary)T("SkillSpectacle").GetField("effects",Flags).GetValue(null);
            if(effects.Count!=12)throw new Exception("Effect catalog incomplete");
            foreach(System.Collections.DictionaryEntry pair in effects)
            {
                var instance=Instantiate((GameObject)pair.Value);
                try
                {
                    instance.GetComponent<EffectComponent>().effectData=new EffectData{origin=Vector3.zero,start=Vector3.one*5,rotation=Quaternion.identity,scale=2f};
                    var comp=instance.GetComponent(T("SkillBurst"));T("SkillBurst").GetMethod("Start",Flags).Invoke(comp,null);
                    if(instance.GetComponentsInChildren<LineRenderer>().Length<2)throw new Exception("Missing energy strokes "+pair.Key);
                }finally{DestroyImmediate(instance);}
            }
            burst=(Component)Call("RadiantGeometry","Flash",Vector3.zero,Quaternion.identity,Color.cyan,2f,.2f);
        });
        
        yield return new WaitForSecondsRealtime(.8f);
        CheckOne("Spectacle lifetime cleanup",()=>{if(burst)throw new Exception("Transient effect did not expire");});
        CheckOne("Furniture slow and bleed are server owned",()=>{
            bool wasServer=NetworkServer.active;GameObject a=null,v=null;
            try
            {
                if(!wasServer){NetworkServer.dontListen=true;if(!NetworkServer.Listen(0))throw new Exception("No fixture server");}
                var def=Array.Find(Resources.FindObjectsOfTypeAll<SurvivorDef>(),d=>d.cachedName=="WeiGe");
                a=Instantiate(def.bodyPrefab);v=Instantiate(BodyCatalog.FindBodyPrefab("BeetleBody"));
                NetworkServer.Spawn(a);NetworkServer.Spawn(v);
                var owner=a.GetComponent<CharacterBody>();var victim=v.GetComponent<HealthComponent>();
                owner.teamComponent.teamIndex=TeamIndex.Player;victim.body.teamComponent.teamIndex=TeamIndex.Monster;
                owner.baseDamage=10f;owner.RecalculateStats();victim.body.RecalculateStats();victim.Networkhealth=100f;
                Call("FurnitureCatalog","Status",owner,victim,Enum.Parse(T("FurnitureKind"),"Fridge"));
                if(!victim.body.HasBuff(RoR2Content.Buffs.Slow80))throw new Exception("Ice slow missing alive="+victim.alive+" health="+victim.health+" max="+victim.fullHealth+" body="+!!victim.body+" index="+RoR2Content.Buffs.Slow80.buffIndex);
                Call("FurnitureCatalog","Status",owner,victim,Enum.Parse(T("FurnitureKind"),"Table"));
                var dot=DotController.FindDotController(v);
                if(!dot || !dot.HasDotActive(DotController.DotIndex.Bleed))throw new Exception("Table bleed missing");
            }finally{if(v)DotController.RemoveAllDots(v);if(a)DestroyImmediate(a);if(v)DestroyImmediate(v);if(!wasServer){NetworkServer.Shutdown();NetworkServer.dontListen=false;}}
        });
    }
}



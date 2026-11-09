using RoR2;
using UnityEngine;

namespace RiftSurvivor
{
    // One world-space replay body: the muzzle, light and shots belong to this figure.
    internal sealed class XiaoheiReplayAvatar : MonoBehaviour
    {
        internal const float Height = 15f;
        private CharacterBody owner;
        private float began;
        private Transform wheel;
        private MeshRenderer figure;
        private LineRenderer[] lines;
        private Color[] colors;
        private Vector3 worldPosition;
        private Quaternion worldRotation;
        private readonly MaterialPropertyBlock tint = new MaterialPropertyBlock();

        internal static GameObject Show(CharacterBody body, Vector3 offset, Vector3 direction)
        {
            var echo=XiaoheiModel.CreateReplayFigure(Height,XiaoheiReplaySentry.Duration);
            if(!echo)return null;
            var root=new GameObject("XiaoheiHeavenAndEarthAvatar");
            var visual=root.AddComponent<XiaoheiReplayAvatar>();
            visual.Build(body,echo,offset,direction);
            return root;
        }
        private void Build(CharacterBody body,GameObject echo,Vector3 offset,Vector3 direction)
        {
            owner=body;began=Time.time;
            Vector3 heading=Vector3.ProjectOnPlane(direction,Vector3.up).normalized;
            if(heading.sqrMagnitude<.5f)heading=body.transform.forward;
            worldRotation=Quaternion.LookRotation(heading);
            worldPosition=body.footPosition+offset;
            transform.SetPositionAndRotation(worldPosition,worldRotation);
            figure=echo.GetComponent<MeshRenderer>();
            echo.transform.SetParent(transform,false);
            // Replace the small echo's fade; the whole apparition shares one envelope.
            ((Behaviour)echo.GetComponent<XiaoheiEchoMesh>()).enabled=false;
            // Canonical mesh is already luminous and scaled about its feet.
            wheel=new GameObject("CelestialReplayMandala").transform;wheel.SetParent(transform,false);
            wheel.localPosition=new Vector3(0,8.5f,-2.1f);
            var geo=wheel.gameObject.AddComponent<RadiantGeometry>();
            var violet=new Color(.8f,.3f,2f,.75f);var cyan=new Color(.3f,1.4f,2f,.8f);
            geo.Ring("HeavenOuterHalo",violet,6.1f,.22f,Quaternion.identity,Vector3.zero,96);
            geo.Ring("HeavenWhiteRim",new Color(.8f,1.7f,2f,.85f),5.85f,.055f,Quaternion.identity,Vector3.zero,96);
            geo.Ring("HeavenInnerHalo",cyan,4.7f,.12f,Quaternion.identity,Vector3.zero,80);
            for(int i=0;i<12;i++)
            {
                geo.Arc("OrbitRunes",cyan,5.3f,.16f,i*30f,16f);
                float a=i*Mathf.PI/6f;Vector3 radial=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);
                geo.Stroke("HaloCrownRay",violet,.095f,radial*6.35f,radial*(i%3==0?7.4f:6.8f));
            }
            var aura=gameObject.AddComponent<RadiantGeometry>();
            aura.Ring("EarthSeal",violet,4.1f,.18f,Quaternion.Euler(90,0,0),Vector3.up*.15f);
            aura.Ring("EarthCore",cyan,3.6f,.065f,Quaternion.Euler(90,0,0),Vector3.up*.18f);
            for(int i=0;i<6;i++)
            {
                float a=i*Mathf.PI/3f;Vector3 p=new Vector3(Mathf.Cos(a)*3.7f,.2f,Mathf.Sin(a)*2f);
                var ray=aura.Stroke("AscendingLight",new Color(.4f,.7f,2f,.32f),.22f,p,p+Vector3.up*(9f+i%3*2f));
                ray.widthCurve=AnimationCurve.Linear(0,1,1,0);
            }
            SkillVfxParticles.Burst(transform,cyan,32,3.5f,7f,1.4f,true);
            lines=GetComponentsInChildren<LineRenderer>();colors=new Color[lines.Length];
            for(int i=0;i<lines.Length;i++)colors[i]=lines[i].startColor;
            Destroy(gameObject,XiaoheiReplaySentry.Duration);
            LateUpdate();
        }
        private void LateUpdate()
        {
            if(!owner || !owner.healthComponent || !owner.healthComponent.alive){Destroy(gameObject);return;}
            // Networked sentry owns the fixed ground anchor and aiming rotation. No camera projection.
            float age=Time.time-began;
            float envelope=Mathf.SmoothStep(0,1,age/.16f)*Mathf.SmoothStep(0,1,(XiaoheiReplaySentry.Duration-age)/.25f);
            float pulse=.86f+.14f*Mathf.Sin(age*31f);
            tint.SetColor("_TintColor",new Color(1,1,1,envelope*pulse));figure.SetPropertyBlock(tint);
            for(int i=0;i<lines.Length;i++){var c=colors[i];c.a*=envelope*pulse;lines[i].startColor=lines[i].endColor=c;}
            wheel.localRotation=Quaternion.Euler(0,0,age*24f);
        }
    }
}

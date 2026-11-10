using System.Collections.Generic;
using RoR2;
using UnityEngine;

namespace RiftSurvivor
{
    // A skill event, never a velocity detector: walking, jumping and flight stay quiet.
    internal sealed class SkillBlinkVisual : MonoBehaviour
    {
        internal const float Lifetime = .58f;
        private float began;
        private readonly List<Transform> blades = new List<Transform>();
        internal static GameObject Show(Vector3 from, Vector3 to, bool sword, bool recall)
        {
            var delta = to - from;
            if (!(delta.sqrMagnitude > .0025f) || float.IsInfinity(delta.sqrMagnitude) || delta.sqrMagnitude > 6400f) return null;
            var root = new GameObject(sword ? "JieSwordBlink" : "XiaoheiRiftBlink");
            root.transform.SetPositionAndRotation(from, Quaternion.LookRotation(delta));
            root.AddComponent<SkillBlinkVisual>().Build(delta.magnitude, sword, recall);
            return root;
        }
        private void Build(float length, bool sword, bool recall)
        {
            began = Time.time;
            Color color = sword ? new Color(.12f, 1.7f, 1.2f, .8f) :
                recall ? new Color(1.3f, .6f, 1.8f, .85f) : new Color(.35f, .9f, 2f, .9f);
            var geo = gameObject.AddComponent<RadiantGeometry>();
            geo.Growth = 0f; // World endpoints must not move as the streak fades.
            geo.Stroke("BlinkSoftWake", new Color(color.r,color.g,color.b,.22f), .85f, Vector3.zero, Vector3.forward * length);
            geo.Stroke("BlinkEnergyCore", color, .22f, Vector3.zero, Vector3.forward * length);
            geo.Stroke("BlinkWhiteFilament", new Color(1.8f,1.9f,2f,.9f), .055f, Vector3.zero, Vector3.forward * length);
            for (int side = -1; side <= 1; side += 2)
            {
                var points = new Vector3[25];
                for (int i = 0; i < points.Length; i++)
                {
                    float t = i / 24f;
                    points[i] = new Vector3(side * Mathf.Sin(t*Mathf.PI)*.65f, Mathf.Sin(t*Mathf.PI*2f)*.18f, t*length);
                }
                geo.Stroke("SplitSlipstream", color, .09f, points);
            }
            foreach (float z in new[] { 0f, length })
            {
                var end = new GameObject("BlinkEndpoint"); end.transform.SetParent(transform,false); end.transform.localPosition=Vector3.forward*z;
                var ring = end.AddComponent<RadiantGeometry>(); ring.Growth=.55f;
                ring.Ring("RiftShockRing",color,1.1f,.16f,Quaternion.identity,Vector3.zero);
                ring.Ring("RiftWhiteRim",new Color(1.6f,1.8f,2f,.85f),.94f,.035f,Quaternion.identity,Vector3.zero);
                for(int i=0;i<4;i++)ring.Arc("BrokenRiftArc",color,1.45f,.085f,i*90f,48f);
                SkillVfxParticles.Burst(end.transform,color,24,.75f,3f,.48f,false);
                ring.Expire(Lifetime);
            }
            if(sword)for(int i=0;i<4;i++)
            {
                var blade=new GameObject("BlinkSwordEcho").transform;blade.SetParent(transform,false);
                blade.localPosition=Vector3.forward*length*(i+1f)/5f;
                JieSwordArt.Attach(blade,color);blade.localScale=Vector3.one*1.3f;blades.Add(blade);
            }
            geo.Expire(Lifetime);
        }
        private void Update()
        {
            float t=Mathf.Clamp01((Time.time-began)/Lifetime);
            foreach(var blade in blades)blade.localScale=Vector3.one*(1.3f*(1f-t*t));
        }
    }

    internal sealed class XiaoheiSlideVisual : MonoBehaviour
    {
        private CharacterBody body;
        private Vector3 previous;
        private float ends;
        internal static void Begin(CharacterBody owner)
        {
            var fx=owner.gameObject.AddComponent<XiaoheiSlideVisual>();
            fx.body=owner;fx.previous=owner.corePosition;fx.ends=Time.time+.22f;
        }
        private void LateUpdate()
        {
            if(!body || !body.healthComponent.alive || Time.time>=ends){Destroy(this);return;}
            Vector3 next=body.corePosition;
            // Ignore concurrent teleports; those have their own complete endpoint cue.
            float distance=(next-previous).sqrMagnitude;
            if(distance>.09f && distance<9f)
            {
                var geo=new GameObject("XiaoheiSkillSlideWake").AddComponent<RadiantGeometry>();
                geo.transform.position=previous;geo.Growth=0f;
                geo.Stroke("SlideCore",new Color(.35f,.85f,1.8f,.65f),.23f,Vector3.zero,next-previous);
                geo.Expire(.24f);previous=next;
            }
            else if(distance>=9f)previous=next;
        }
    }
}

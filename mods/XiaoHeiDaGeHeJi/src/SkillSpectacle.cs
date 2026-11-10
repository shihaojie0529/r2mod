using System;
using System.Collections.Generic;
using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;

namespace RiftSurvivor
{
    internal enum SpectacleKind { Punch, Uppercut, Impact, Slam, Buff, Furniture, Lightning, Seeker, Replay, Supply, SelectAll, SelectionLink }

    // Transient damage cues use EffectManager so all clients see the same event.
    internal static class SkillSpectacle
    {
        private static readonly Dictionary<SpectacleKind, GameObject> effects = new Dictionary<SpectacleKind, GameObject>();
        internal static readonly Color Gold = new Color(1f, .65f, .08f);
        internal static void Register()
        {
            foreach (SpectacleKind kind in Enum.GetValues(typeof(SpectacleKind)))
            {
                var prefab = CombatVisuals.EffectPrefab("Spectacle" + kind);
                prefab.AddComponent<EffectComponent>().applyScale = false;
                var attributes = prefab.AddComponent<VFXAttributes>();
                attributes.DoNotPool = true;
                attributes.vfxPriority = VFXAttributes.VFXPriority.Always;
                prefab.AddComponent<SkillBurst>().kind = kind;
                if (!ContentAddition.AddEffect(prefab)) throw new InvalidOperationException("Cannot register " + kind);
                effects.Add(kind, prefab);
            }
        }
        internal static void Emit(SpectacleKind kind, Vector3 point, Vector3 direction, float size = 1f, uint style = 0)
        {
            if (!NetworkServer.active || !effects.TryGetValue(kind, out var prefab)) return;
            EffectManager.SpawnEffect(prefab, new EffectData { origin = point,
                rotation = Util.QuaternionSafeLookRotation(direction), scale = size, genericUInt = style }, true);
        }
        internal static void Arc(Vector3 from, Vector3 to)
        {
            if (!NetworkServer.active) return;
            EffectManager.SpawnEffect(effects[SpectacleKind.Lightning], new EffectData {origin=from,start=to,scale=1f},true);
        }
        internal static void Selection(Vector3 caster, Vector3 cargo)
        {
            if (!NetworkServer.active) return;
            EffectManager.SpawnEffect(effects[SpectacleKind.SelectionLink],
                new EffectData { origin=cargo, start=caster, scale=1f }, true);
        }
        internal static Color Hue(CharacterBody body) => body.GetComponent<CuiVitals>() ? Gold :
            body.GetComponent<JieFlight>() ? new Color(.12f, 1f, .76f) :
            body.GetComponent<WeiCombat>() ? new Color(.30f, .65f, 1f) : new Color(.55f, .30f, 1f);
    }

    // Reusable additive geometry; rings and rays fade without per-frame allocations.
    internal sealed class RadiantGeometry : MonoBehaviour
    {
        private static Material material;
        private readonly List<LineRenderer> lines = new List<LineRenderer>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<float> widths = new List<float>();
        private float began, lifetime;
        private Vector3 velocity, initialScale;
        internal float Growth = .35f;
        internal LineRenderer Stroke(string name, Color color, float width, params Vector3[] points)
        {
            if (!material) material = CombatVisuals.SoftRadiance("RadiantSkillAdditive", true);
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.sharedMaterial = material; line.useWorldSpace = false;
            line.positionCount = points.Length; line.SetPositions(points);
            line.widthMultiplier = width;
            line.widthCurve = AnimationCurve.Linear(0f,1f,1f,1f);
            line.startColor = line.endColor = color;
            line.numCapVertices = 3; line.numCornerVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
            lines.Add(line); colors.Add(color); widths.Add(width);
            return line;
        }
        internal void Ring(string name, Color color, float radius, float width, Quaternion rotation, Vector3 offset, int count = 64)
        {
            var points = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count;
                points[i] = offset + rotation * new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
            }
            Stroke(name, color, width, points).loop = true;
        }
        internal void Arc(string name, Color color, float radius, float width, float from, float sweep)
        {
            const int count = 32;
            var points = new Vector3[count + 1];
            for (int i = 0; i <= count; i++)
            {
                float a = (from + sweep * i / count) * Mathf.Deg2Rad;
                points[i] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius;
            }
            var line = Stroke(name, color, width, points);
            line.widthCurve = new AnimationCurve(new Keyframe(0f,0f), new Keyframe(.25f,1f), new Keyframe(.65f,.75f), new Keyframe(1f,0f));
            line.widthMultiplier = width;
        }
        internal void Rays(Color color, int count, float radius, float length)
        {
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2f / count;
                var radial = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                Stroke("EnergyRay", color, .035f, radial * radius, radial * (radius + length));
            }
        }
        internal void Expire(float seconds, Vector3 motion = default)
        { began = Time.time; lifetime = seconds; velocity = motion; initialScale = transform.localScale; Destroy(gameObject, seconds); }
        private void Update()
        {
            if (lifetime <= 0f) return;
            float t = Mathf.Clamp01((Time.time - began) / lifetime);
            transform.position += velocity * Time.deltaTime;
            transform.localScale = initialScale * (1f + Mathf.SmoothStep(0f, 1f, t) * Growth);
            for (int i = 0; i < lines.Count; i++)
            {
                var color = colors[i]; color.a *= (1f - t) * (1f - t);
                lines[i].startColor = lines[i].endColor = color;
                lines[i].widthMultiplier = widths[i] * (1f - t * .7f);
            }
        }
        internal static RadiantGeometry Flash(Vector3 position, Quaternion rotation, Color color, float size, float seconds)
        {
            var go = new GameObject("SkillRadiance"); go.transform.SetPositionAndRotation(position, rotation);
            var fx = go.AddComponent<RadiantGeometry>();
            fx.Ring("OuterHalo", new Color(color.r,color.g,color.b,.7f), size, .055f, Quaternion.identity, Vector3.zero);
            fx.Ring("InnerHalo", new Color(1f,1f,1f,.45f), size * .90f, .018f, Quaternion.identity, Vector3.zero);
            SkillVfxParticles.Burst(fx.transform, color, 16, size * .5f, size, seconds * .8f, false);
            fx.Expire(seconds); return fx;
        }
    }

    internal sealed class SkillBurst : MonoBehaviour
    {
        [SerializeField] internal SpectacleKind kind;
        private Transform fist;
        private float began;
        private Vector3 baseScale;
        private MaterialPropertyBlock fade;
        private MeshRenderer fistRenderer;
        private LineRenderer[] fistLines;
        private void Start()
        {
            var data = GetComponent<EffectComponent>().effectData;
            transform.SetPositionAndRotation(data.origin, data.rotation);
            began = Time.time;
            if (kind == SpectacleKind.SelectionLink)
            {
                transform.rotation=Quaternion.identity;
                var link=gameObject.AddComponent<RadiantGeometry>();link.Growth=0f;
                var blue=new Color(.25f,1.1f,2f,.8f);
                link.Stroke("TelekineticSelectionLink",new Color(.25f,.8f,2f,.45f),.16f,Vector3.zero,data.start-data.origin);
                link.Stroke("SelectionWhiteCore",new Color(.8f,1.7f,2f,.7f),.035f,Vector3.zero,data.start-data.origin);
                foreach(int x in new[]{-1,1})foreach(int y in new[]{-1,1})
                    link.Stroke("SelectCorner",blue,.12f,new Vector3(x*.6f,y*1.3f,0),new Vector3(x*1.3f,y*1.3f,0),new Vector3(x*1.3f,y*.6f,0));
                link.Ring("SelectedCargoSeal",blue,1.5f,.09f,Quaternion.Euler(90,0,0),Vector3.zero);
                SkillVfxParticles.Burst(transform,blue,16,1f,2f,.55f,true);
                link.Expire(.7f);return;
            }
            if (kind == SpectacleKind.SelectAll)
            {
                // A horizontal command seal, followed by tall blue selection pillars.
                transform.rotation=Quaternion.identity;
                var seal=gameObject.AddComponent<RadiantGeometry>();seal.Growth=.3f;
                var blue=new Color(.22f,.85f,2f,.85f);
                float radius=Mathf.Clamp(data.scale,1f,6f);
                seal.Ring("AdminCommandSeal",blue,radius,.18f,Quaternion.Euler(90,0,0),Vector3.zero);
                seal.Ring("AdminWhiteSeal",new Color(.75f,1.6f,2f,.8f),radius*.88f,.045f,Quaternion.Euler(90,0,0),Vector3.zero);
                for(int i=0;i<8;i++)
                {
                    float a=i*Mathf.PI/4f;var p=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius*.8f;
                    var beam=seal.Stroke("AdminRisingPillar",new Color(.25f,.8f,2f,.5f),.22f,p,p+Vector3.up*(i%2==0?4f:2.7f));
                    beam.widthCurve=AnimationCurve.Linear(0,1,1,0);
                }
                SkillVfxParticles.Burst(transform,blue,32,radius*.7f,4f,.8f,true);
                seal.Expire(.9f);return;
            }
            if (kind == SpectacleKind.Lightning)
            {
                transform.rotation = Quaternion.identity;
                var arc = gameObject.AddComponent<RadiantGeometry>();
                var points = new Vector3[9];
                for(int i=0;i<points.Length;i++) points[i]=Vector3.Lerp(Vector3.zero,data.start-data.origin,i/8f) +
                    (i>0 && i<8 ? new Vector3(Mathf.Sin(i*7f)*.3f,Mathf.Cos(i*5f)*.3f,0) : Vector3.zero);
                arc.Stroke("ElectricArc",new Color(.8f,.5f,1f),.12f,points);
                arc.Stroke("ElectricCore",Color.white,.035f,points);
                arc.Growth = 0f; arc.Expire(.20f); return;
            }
            float size = Mathf.Clamp(data.scale, .4f, 20f);
            bool punch = kind == SpectacleKind.Punch || kind == SpectacleKind.Uppercut;
            bool upper = kind == SpectacleKind.Uppercut;
            Color color = kind == SpectacleKind.Furniture ? FurnitureCatalog.ColorFor((FurnitureKind)Mathf.Clamp((int)data.genericUInt,0,5)) :
                kind == SpectacleKind.Seeker ? new Color(1f,.45f,.12f) : kind == SpectacleKind.Replay ? new Color(.50f,.25f,1f) :
                kind == SpectacleKind.Supply ? new Color(.25f,1f,.55f) : SkillSpectacle.Gold;
            float life = punch ? .26f : (kind == SpectacleKind.Supply || kind == SpectacleKind.Buff) ? .85f : .55f;
            var geo = gameObject.AddComponent<RadiantGeometry>();
            geo.Growth = punch ? .08f : .18f;
            var soft = new Color(color.r, color.g, color.b, .65f);
            if (punch)
            {
                // A short directional crescent follows the fist; the contact cue
                // remains separate from the swing and never blankets the target.
                geo.Arc("FistSweep", soft, size * .48f, .14f, upper ? -100f : 20f, 150f);
                geo.Arc("FistSweepCore", new Color(1f,.94f,.70f,.7f), size * .47f, .025f, upper ? -95f : 25f, 125f);
                Util.PlaySound("Play_loader_m1_swing", gameObject);
                fist = new GameObject("GiantGoldenFistEcho").transform;
                fist.SetParent(transform, false);
                baseScale = Vector3.one * (upper ? .82f : .58f);
                fist.localScale = baseScale;
                GoldenFist.Attach(fist);
                fistLines = fist.GetComponentsInChildren<LineRenderer>();
                fistRenderer = fist.GetComponent<MeshRenderer>(); fade = new MaterialPropertyBlock();
                SkillVfxParticles.Burst(transform, color, upper ? 12 : 7, size * .22f, 2.4f, life, false);
            }
            else if (kind == SpectacleKind.Supply || kind == SpectacleKind.Buff)
            {
                geo.Ring("RecoveryWave",soft,size*.68f,.055f,Quaternion.identity,Vector3.zero);
                geo.Ring("RecoveryInner",new Color(.8f,1f,.9f,.5f),size*.60f,.018f,Quaternion.identity,Vector3.zero);
                SkillVfxParticles.Burst(transform,color,20,size*.55f,1.4f,.75f,true);
            }
            else if (kind == SpectacleKind.Replay)
            {
                for(int i=0;i<3;i++) geo.Arc("ReplayLens",soft,size*(.40f+i*.12f),.045f,i*120f,82f);
                geo.Arc("ReplayCore",new Color(.8f,.85f,1f,.7f),size*.36f,.025f,15f,260f);
                SkillVfxParticles.Burst(transform,color,14,size*.5f,.7f,.45f,true);
            }
            else if (kind == SpectacleKind.Seeker || kind == SpectacleKind.Impact)
            {
                geo.Ring("ImpactPulse",soft,size*.52f,.08f,Quaternion.identity,Vector3.zero);
                geo.Arc("ImpactCore",new Color(1f,.94f,.7f,.8f),size*.30f,.035f,0f,300f);
                SkillVfxParticles.Burst(transform,color,18,.12f,4.5f,.48f,false);
            }
            else
            {
                // The effect's +Z is the surface normal. Do not rotate the same
                // ground wave a second time or make it larger than its hit area.
                geo.Ring("GroundShockwave",soft,size*.82f,.095f,Quaternion.identity,Vector3.zero);
                geo.Ring("GroundWaveCore",new Color(1f,.91f,.63f,.5f),size*.75f,.025f,Quaternion.identity,Vector3.zero);
                for(int i=0;i<5;i++) geo.Arc("ImpactFragment",soft,size*(.40f+i*.07f),.04f,i*72f,28f);
                SkillVfxParticles.Burst(transform,color,24,size*.35f,3f,.50f,false);
            }
            geo.Expire(life);
        }

        private void Update()
        {
            if (!fist) return;
            float t = Mathf.Clamp01((Time.time - began) / .26f);
            fist.localPosition = new Vector3(0f, kind == SpectacleKind.Uppercut ? t * .8f : 0f, t * 1.3f);
            fist.localScale = baseScale * (1f + t * .12f);
            fade.SetColor("_TintColor",new Color(1f,1f,1f,(1f-t)*(1f-t)));
            fistRenderer.SetPropertyBlock(fade);
            foreach (var line in fistLines)
            {
                var color = line.startColor; color.a = .65f * (1f-t)*(1f-t);
                line.startColor = line.endColor = color;
            }
        }
    }

    internal static class GoldenFist
    {
        private static Mesh mesh;
        private static Material material;
        internal static void Attach(Transform parent)
        {
            if (!mesh)
            {
                var vertices = new List<Vector3>(); var indices = new List<int>(); var colors = new List<Color>();
                void Box(Vector3 center, Vector3 size)
                {
                    int first = vertices.Count;
                    for (int i = 0; i < 8; i++)
                    {
                        vertices.Add(center + Vector3.Scale(size, new Vector3((i&1)==0?-.5f:.5f,(i&2)==0?-.5f:.5f,(i&4)==0?-.5f:.5f)));
                        colors.Add(new Color(1f, .64f + .06f * (i % 3), .10f, .46f));
                    }
                    foreach (int i in new[]{0,2,3,0,3,1,4,5,7,4,7,6,0,4,6,0,6,2,1,3,7,1,7,5,2,6,7,2,7,3,0,1,5,0,5,4}) indices.Add(first+i);
                }
                void Round(Vector3 center, Vector3 radius)
                {
                    int first=vertices.Count;
                    const int rows=8, cols=12;
                    for(int y=0;y<=rows;y++)for(int x=0;x<=cols;x++)
                    {
                        float p=y*Mathf.PI/rows,a=x*Mathf.PI*2f/cols;
                        var normal=new Vector3(Mathf.Sin(p)*Mathf.Cos(a),Mathf.Cos(p),Mathf.Sin(p)*Mathf.Sin(a));
                        vertices.Add(center+Vector3.Scale(normal,radius));colors.Add(new Color(1f,.70f,.14f,.32f));
                    }
                    for(int y=0;y<rows;y++)for(int x=0;x<cols;x++)
                    {
                        int n=first+y*(cols+1)+x;
                        indices.AddRange(new[]{n,n+cols+1,n+1,n+1,n+cols+1,n+cols+2});
                    }
                }
                Box(new Vector3(0,-.04f,-.30f), new Vector3(.58f,.58f,.95f)); // wrist
                Round(Vector3.zero,new Vector3(.60f,.44f,.50f)); // curled palm
                for(int i=0;i<4;i++)
                {
                    Round(new Vector3(-.42f+i*.28f,.18f,.44f),new Vector3(.16f,.25f,.26f));
                    Round(new Vector3(-.42f+i*.28f,-.17f,.36f),new Vector3(.145f,.16f,.19f));
                }
                Round(new Vector3(.54f,-.22f,.09f),new Vector3(.22f,.24f,.34f)); // folded thumb
                mesh = new Mesh { name="CuiGoldenClenchedFist" };
                mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(indices,0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
                material = CombatVisuals.Radiance("CuiGoldenFistGhost");
            }
            parent.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var view = parent.gameObject.AddComponent<MeshRenderer>(); view.sharedMaterial = material;
            view.shadowCastingMode = ShadowCastingMode.Off;
            var outline = parent.gameObject.AddComponent<RadiantGeometry>();
            for(int i=0;i<4;i++)
                outline.Stroke("GoldenKnuckle",new Color(1f,.85f,.25f),.045f,
                    new Vector3(-.53f+i*.28f,-.02f,.66f), new Vector3(-.53f+i*.28f,.39f,.66f), new Vector3(-.31f+i*.28f,.39f,.66f));
        }
    }

}

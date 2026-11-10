using System.Collections.Generic;
using RoR2;
using UnityEngine;
using UnityEngine.Rendering;

namespace RiftSurvivor
{
    // A full-size twelve-metre sword immortal. The casting player's presentation
    // is composed beside the camera's aim corridor; observers see it behind Jie.
    [DefaultExecutionOrder(100)]
    internal sealed class JieCelestialAvatar : MonoBehaviour
    {
        private readonly Transform[] orbit = new Transform[48];
        private Transform mandala;
        private MeshRenderer figureRenderer;
        private LineRenderer[] lines;
        private Color[] lineColors;
        private readonly MaterialPropertyBlock fade = new MaterialPropertyBlock();
        private CharacterBody owner;
        private float started;
        private bool initialized;
        private static Mesh immortal;
        private static Material ghost;
        private Renderer[] presentationRenderers;
        private Material[][] worldMaterials, localMaterials;
        private bool[] worldDynamicOcclusion;
        private bool localPresentation, materialModeApplied;
        // Variants belong only to this presentation. The combat swords and remote
        // avatar keep their original shared materials and ordinary depth testing.
        private static readonly Dictionary<Material, Material> projectionMaterials = new Dictionary<Material, Material>();

        internal void Initialize(CharacterBody body, float began)
        { owner = body; started = began; initialized = true; }

        private void Start()
        {
            if (!initialized) started = Time.time;
            var gold = JieStormEffects.Gold;
            if (!immortal) BuildImmortal();
            var figure = new GameObject("SwordImmortalDharmaBody"); figure.transform.SetParent(transform,false);
            figure.transform.localScale = Vector3.one * 1.2f;
            figure.transform.localPosition = new Vector3(0f,0f,-1.5f);
            figure.AddComponent<MeshFilter>().sharedMesh = immortal;
            figureRenderer = figure.AddComponent<MeshRenderer>(); figureRenderer.sharedMaterial = ghost;
            figureRenderer.shadowCastingMode = ShadowCastingMode.Off; figureRenderer.receiveShadows = false;
            var geometry = figure.AddComponent<RadiantGeometry>();
            geometry.Ring("ImmortalCrown",gold,1.12f,.055f,Quaternion.identity,new Vector3(0,9.4f,-.1f));
            geometry.Ring("ImmortalCrownCore",new Color(1f,.94f,.7f,.7f),.98f,.024f,Quaternion.identity,new Vector3(0,9.4f,-.1f));
            foreach(int sign in new[]{-1,1})
            {
                geometry.Stroke("RobeContour",new Color(1f,.78f,.3f,.65f),.045f,
                    new Vector3(sign*.4f,8.5f,.15f),new Vector3(sign*1.42f,7.9f,.2f),
                    new Vector3(sign*1.15f,5.3f,.3f),new Vector3(sign*2.45f,.8f,.1f));
                geometry.Stroke("ImmortalSleeve",gold,.055f,new Vector3(sign*1.4f,7.8f,.2f),
                    new Vector3(sign*2.5f,6.5f,.25f),new Vector3(sign*3.15f,7.7f,.4f));
                geometry.Stroke("RobeInlay",new Color(.35f,1f,.88f,.55f),.029f,new Vector3(sign*.66f,8.05f,.6f),
                    new Vector3(0,6.9f,.83f),new Vector3(sign*.55f,3.1f,.99f),new Vector3(sign*1.6f,1.0f,.7f));
                geometry.Stroke("RobeGoldFold",new Color(1f,.84f,.47f,.38f),.023f,
                    new Vector3(sign*.5f,5.3f,.72f),new Vector3(sign*.94f,2.9f,.94f),new Vector3(sign*1.35f,.9f,.85f));
                geometry.Stroke("ImmortalEye",new Color(1f,.95f,.78f,.9f),.031f,
                    new Vector3(sign*.14f,9.44f,.61f),new Vector3(sign*.36f,9.49f,.57f));
            }
            geometry.Stroke("CrownBlade",gold,.04f,new Vector3(0,10.2f,0),new Vector3(0,10.75f,0));
            mandala = new GameObject("CelestialSwordMandala").transform; mandala.SetParent(transform,false);
            mandala.localPosition = new Vector3(0,5f,-2.2f);
            var rings = mandala.gameObject.AddComponent<RadiantGeometry>();
            rings.Ring("OuterCelestialCircle",new Color(1f,.73f,.19f,.55f),6.2f,.045f,Quaternion.identity,Vector3.zero,128);
            rings.Ring("InnerCelestialCircle",new Color(1f,.84f,.4f,.48f),5.85f,.020f,Quaternion.identity,Vector3.zero,128);
            rings.Ring("JadeCelestialCircle",new Color(.2f,1f,.8f,.32f),4.1f,.032f,Quaternion.identity,Vector3.zero,96);
            for(int i=0;i<24;i++)
            {
                float a=i*Mathf.PI*2f/24;
                var r=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);var s=new Vector3(-r.y,r.x,0);
                rings.Stroke("CelestialRune",new Color(1f,.8f,.3f,.72f),.034f,r*5.72f-s*.1f,r*6.02f,r*5.72f+s*.1f);
            }
            for(int i=0;i<orbit.Length;i++)
            {
                var sword = new GameObject("CelestialOrbitSword").transform;sword.SetParent(transform,false);
                sword.localScale=Vector3.one*(i<24?1.55f:1.15f);
                JieSwordArt.Attach(sword,i<24?gold:JieSwordArt.Jade);orbit[i]=sword;
            }
            lines=GetComponentsInChildren<LineRenderer>();lineColors=new Color[lines.Length];
            for(int i=0;i<lines.Length;i++)lineColors[i]=lines[i].startColor;
            CachePresentationMaterials();
            SetLocalPresentation(localPresentation);
        }

        private void CachePresentationMaterials()
        {
            presentationRenderers=GetComponentsInChildren<Renderer>(true);
            worldMaterials=new Material[presentationRenderers.Length][];
            worldDynamicOcclusion=new bool[presentationRenderers.Length];
            localMaterials=new Material[presentationRenderers.Length][];
            for(int i=0;i<presentationRenderers.Length;i++)
            {
                worldDynamicOcclusion[i]=presentationRenderers[i].allowOcclusionWhenDynamic;
                var originals=presentationRenderers[i].sharedMaterials;
                worldMaterials[i]=originals;
                var variants=new Material[originals.Length];
                for(int slot=0;slot<originals.Length;slot++)
                {
                    var source=originals[slot];
                    if(!source)continue;
                    if(!projectionMaterials.TryGetValue(source,out var variant) || !variant)
                    {
                        if(source.shader==ghost.shader)
                            variant=new Material(source){name=source.name+"LocalProjection"};
                        else
                        {
                            // Ritual blades are a low-alpha apparition in the local
                            // view; their real attacking counterparts remain steel.
                            variant=CombatVisuals.Radiance(source.name+"LocalProjection");
                            variant.SetColor("_TintColor",new Color(1f,.78f,.32f,.16f));
                        }
                        variant.SetFloat("_ZTest",(float)CompareFunction.Always);
                        if(variant.HasProperty("_ZWrite"))variant.SetFloat("_ZWrite",0f);
                        variant.renderQueue=(int)RenderQueue.Transparent+50;
                        projectionMaterials[source]=variant;
                    }
                    variants[slot]=variant;
                }
                localMaterials[i]=variants;
            }
            materialModeApplied=false;
        }

        // Material arrays and variants are built once; the camera check below
        // changes references only when switching between local and observer views.
        internal void SetLocalPresentation(bool enabled)
        {
            if(presentationRenderers==null){localPresentation=enabled;return;}
            if(materialModeApplied && localPresentation==enabled)return;
            localPresentation=enabled;
            var materials=enabled?localMaterials:worldMaterials;
            for(int i=0;i<presentationRenderers.Length;i++)
                if(presentationRenderers[i])
                {
                    presentationRenderers[i].sharedMaterials=materials[i];
                    // ZTest cannot help a renderer culled before drawing. Only
                    // the owner's projection bypasses baked dynamic occlusion.
                    presentationRenderers[i].allowOcclusionWhenDynamic=enabled?false:worldDynamicOcclusion[i];
                }
            materialModeApplied=true;
        }

        private void OnDisable() => SetLocalPresentation(false);

        // The complete figure and its 48 swords fit in the left half at every FOV
        // and aspect ratio. Neither its scale nor the actual volley origins change.
        internal static void LocalViewPose(Camera camera, out Vector3 position, out Quaternion rotation)
        {
            float tangent=Mathf.Tan(Mathf.Clamp(camera.fieldOfView,5f,160f)*Mathf.Deg2Rad*.5f);
            float aspect=Mathf.Max(.1f,camera.aspect);
            // Three metres of depth reserve account for the robe and ring plane
            // lying in front of the presentation origin, closer to the camera.
            float fitDistance=Mathf.Max(12f,15.5f/(2f*tangent*.8f),16.5f/(tangent*aspect*.86f));
            float distance=fitDistance+3f;
            float halfWidth=fitDistance*tangent*aspect;
            var view=camera.transform;
            position=view.position+view.forward*distance-view.right*(8.3f+halfWidth*.045f)-view.up*5f;
            rotation=view.rotation;
        }

        private void LateUpdate()
        {
            if(!mandala) return;
            Camera camera=null;
            if(owner)
                foreach(var local in LocalUserManager.readOnlyLocalUsersList)
                    if(local.cachedBody==owner && local.cameraRigController && local.cameraRigController.sceneCam)
                    {camera=local.cameraRigController.sceneCam;break;}
            SetLocalPresentation(camera != null);
            if(camera)
            {
                LocalViewPose(camera,out var position,out var rotation);
                transform.SetPositionAndRotation(position,rotation);
            }
            else {transform.localPosition=Vector3.zero;transform.localRotation=Quaternion.identity;}
            float age=Time.time-started;
            float envelope=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(age/.26f))*
                Mathf.SmoothStep(0f,1f,Mathf.Clamp01((JieGoldVolley.Duration-age)/.42f));
            // Keep the giant silhouette, bringing in its light before the first volley.
            transform.localScale=Vector3.one*Mathf.Lerp(.94f,1f,envelope);
            fade.SetColor("_TintColor",new Color(1f,1f,1f,envelope));figureRenderer.SetPropertyBlock(fade);
            for(int i=0;i<lines.Length;i++)
            {var color=lineColors[i];color.a*=envelope;lines[i].startColor=lines[i].endColor=color;}
            float time=Time.time;
            mandala.localRotation=Quaternion.Euler(0,0,time*5f);
            for(int i=0;i<orbit.Length;i++)
            {
                bool outer=i<24;
                float a=(i%24)*Mathf.PI*2f/24+time*(outer?.19f:-.26f);
                var radial=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);
                orbit[i].localPosition=new Vector3(0,5,-2.1f)+radial*(outer?6.65f:4.6f);
                orbit[i].localRotation=Quaternion.LookRotation(radial,Vector3.forward);
                orbit[i].localScale=Vector3.one*(outer?1.55f:1.15f)*Mathf.SmoothStep(0f,1f,envelope);
            }
        }

        private static void BuildImmortal()
        {
            var vertices=new List<Vector3>();var indices=new List<int>();var colors=new List<Color>();
            void Ellipsoid(Vector3 center,Vector3 radius,Quaternion rotation,float opacity=.07f)
            {
                int first=vertices.Count;const int rows=12,cols=20;
                for(int row=0;row<=rows;row++)for(int col=0;col<=cols;col++)
                {
                    float p=row*Mathf.PI/rows,a=col*Mathf.PI*2f/cols;
                    var n=new Vector3(Mathf.Sin(p)*Mathf.Cos(a),Mathf.Cos(p),Mathf.Sin(p)*Mathf.Sin(a));
                    vertices.Add(center+rotation*Vector3.Scale(n,radius));
                    colors.Add(new Color(1f,.69f,.22f,opacity*Mathf.Lerp(.55f,1f,Mathf.Abs(n.x))));
                }
                for(int row=0;row<rows;row++)for(int col=0;col<cols;col++)
                {int n=first+row*(cols+1)+col;indices.AddRange(new[]{n,n+cols+1,n+1,n+1,n+cols+1,n+cols+2});}
            }
            // Continuous pleated robe replaces the old stack of overlapping spheres.
            int robeStart=vertices.Count;const int sides=40;
            var heights=new[]{.55f,1.2f,3.1f,5.25f,6.35f,7.75f,8.2f};
            var widths=new[]{2.52f,2.35f,1.72f,1.04f,1.18f,1.47f,.66f};
            var depths=new[]{.94f,1f,.9f,.64f,.7f,.68f,.45f};
            for(int row=0;row<heights.Length;row++)for(int col=0;col<=sides;col++)
            {
                float a=col*Mathf.PI*2f/sides;
                float pleat=1f+.055f*Mathf.Cos(a*10f)*(1f-row/(float)heights.Length);
                vertices.Add(new Vector3(Mathf.Cos(a)*widths[row]*pleat,heights[row],Mathf.Sin(a)*depths[row]*pleat));
                colors.Add(new Color(1f,.66f,.17f,.045f+.025f*Mathf.Pow(Mathf.Abs(Mathf.Cos(a)),3f)));
            }
            for(int row=0;row<heights.Length-1;row++)for(int col=0;col<sides;col++)
            {int n=robeStart+row*(sides+1)+col;indices.AddRange(new[]{n,n+1,n+sides+1,n+1,n+sides+2,n+sides+1});}
            Ellipsoid(new Vector3(0,9.4f,0),new Vector3(.58f,.87f,.55f),Quaternion.identity,.105f);
            Ellipsoid(new Vector3(0,10.15f,-.1f),new Vector3(.33f,.45f,.3f),Quaternion.identity,.09f);
            Ellipsoid(new Vector3(0,8.6f,0),new Vector3(.32f,.38f,.3f),Quaternion.identity);
            foreach(int s in new[]{-1,1})
            {
                Ellipsoid(new Vector3(s*1.9f,7.2f,0),new Vector3(.55f,1.12f,.53f),Quaternion.Euler(0,0,s*45));
                Ellipsoid(new Vector3(s*2.75f,7.15f,.1f),new Vector3(.43f,1.0f,.46f),Quaternion.Euler(0,0,-s*24));
                Ellipsoid(new Vector3(s*3.13f,8.06f,.31f),new Vector3(.25f,.37f,.16f),Quaternion.identity,.11f);
                for(int finger=0;finger<4;finger++)
                    Ellipsoid(new Vector3(s*(2.97f+finger*.105f),8.47f,.34f),new Vector3(.044f,.26f-finger*.025f,.058f),Quaternion.Euler(0,0,s*(finger-1)*6),.085f);
            }
            immortal=new Mesh{name="JieSculptedCelestialDharma"};immortal.SetVertices(vertices);immortal.SetColors(colors);
            immortal.SetTriangles(indices,0);immortal.RecalculateNormals();immortal.RecalculateBounds();
            ghost=CombatVisuals.Radiance("JieCelestialTranslucentGold");
        }
    }
}

using System.Collections.Generic;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;

namespace RiftSurvivor
{
    internal enum FurnitureKind : byte { Chair, Sofa, Table, Fridge, Lamp, Wardrobe }
    internal static class FurnitureCatalog
    {
        internal const int Count = 6;
        internal static float Multiplier(FurnitureKind kind) => kind == FurnitureKind.Wardrobe ? 1.5f :
            kind == FurnitureKind.Table ? 1.1f : kind == FurnitureKind.Lamp ? .8f : kind == FurnitureKind.Fridge ? .9f : 1f;
        internal static float Radius(FurnitureKind kind) => kind == FurnitureKind.Sofa ? .48f :
            kind == FurnitureKind.Wardrobe ? .48f : kind == FurnitureKind.Fridge ? .42f : .30f;
        internal static float HalfHeight(FurnitureKind kind) => kind == FurnitureKind.Fridge ? .85f :
            kind == FurnitureKind.Wardrobe ? .95f : kind == FurnitureKind.Lamp ? .90f : kind == FurnitureKind.Table ? .5f : kind == FurnitureKind.Sofa ? .44f : .49f;
        internal static Color ColorFor(FurnitureKind kind) => kind == FurnitureKind.Fridge ? new Color(.25f,.85f,1f) :
            kind == FurnitureKind.Lamp ? new Color(.70f,.38f,1f) : kind == FurnitureKind.Table ? new Color(1f,.28f,.24f) :
            kind == FurnitureKind.Wardrobe ? new Color(1f,.65f,.15f) : new Color(.35f,1f,.75f);

        internal static void Status(CharacterBody owner, HealthComponent victim, FurnitureKind kind)
        {
            if (!NetworkServer.active || !owner || !victim || !victim.alive || !victim.body) return;
            var body=victim.body;
            switch(kind)
            {
                case FurnitureKind.Chair:
                case FurnitureKind.Wardrobe:
                    var hurt=body.GetComponent<SetStateOnHurt>();
                    if(hurt && hurt.canBeStunned) hurt.SetStun(kind==FurnitureKind.Wardrobe?1f:.45f);
                    break;
                case FurnitureKind.Fridge: body.AddTimedBuff(RoR2Content.Buffs.Slow80,2f); break;
                case FurnitureKind.Table:
                    var bleed = new InflictDotInfo {victimObject=victim.gameObject,attackerObject=owner.gameObject,
                        hitHurtBox=body.mainHurtBox,dotIndex=DotController.DotIndex.Bleed,duration=3f,damageMultiplier=.5f};
                    DotController.InflictDot(ref bleed);
                    break;
            }
        }
        // Secondary damage has a lower proc coefficient and cannot recurse through
        // the thrown-object path. One enemy's multiple hurtboxes count only once.
        internal static void Burst(CharacterBody owner, HealthComponent direct, Vector3 point, float damage, bool crit, FurnitureKind kind)
        {
            if (!NetworkServer.active || !owner) return;
            SkillSpectacle.Emit(SpectacleKind.Furniture,point,Vector3.up,kind==FurnitureKind.Sofa?4f:1.8f, (uint)kind);
            if(kind!=FurnitureKind.Sofa && kind!=FurnitureKind.Lamp) return;
            var unique=new HashSet<HealthComponent>();
            if(direct) unique.Add(direct);
            int count=0;
            foreach(var col in Physics.OverlapSphere(point,kind==FurnitureKind.Sofa?4f:7f,LayerIndex.entityPrecise.mask,QueryTriggerInteraction.Collide))
            {
                var hurt=col.GetComponent<HurtBox>();
                if(!XiaoheiBallistics.IsLiveEnemy(owner,hurt) || !unique.Add(hurt.healthComponent) ||
                    Physics.Linecast(point,hurt.transform.position,LayerIndex.world.mask,QueryTriggerInteraction.Ignore)) continue;
                var victim=hurt.healthComponent;
                var info=new DamageInfo {attacker=owner.gameObject,inflictor=owner.gameObject,damage=damage*.55f,
                    crit=crit,position=hurt.transform.position,procCoefficient=.35f,damageType=DamageTypeCombo.GenericPrimary,
                    force=kind==FurnitureKind.Sofa?(hurt.transform.position-point).normalized*800f+Vector3.up*200f:Vector3.zero};
                victim.TakeDamage(info);
                if(!info.rejected && GlobalEventManager.instance)
                {GlobalEventManager.instance.OnHitEnemy(info,victim.gameObject);GlobalEventManager.instance.OnHitAll(info,victim.gameObject);}
                if(kind==FurnitureKind.Lamp)
                {
                    SkillSpectacle.Arc(point,hurt.transform.position);
                    if(++count==2) break;
                }
            }
        }
    }
    internal static class ExtendedFurnitureArt
    {
        private static readonly Dictionary<FurnitureKind,Mesh> meshes=new Dictionary<FurnitureKind,Mesh>();
        private static readonly Dictionary<FurnitureKind,Material> materials=new Dictionary<FurnitureKind,Material>();
        internal static void Attach(Transform root,FurnitureKind kind)
        {
            if(kind==FurnitureKind.Chair || kind==FurnitureKind.Sofa){WeiFurnitureArt.Attach(root,kind==FurnitureKind.Sofa);return;}
            if(!meshes.TryGetValue(kind,out var mesh))
            {
                var v=new List<Vector3>();var tris=new List<int>();var colors=new List<Color>();
            void Box(Vector3 center, Vector3 size, Color color, float bevel = .025f, float tilt = 0f)
            {
                var half = size * .5f;
                float b = Mathf.Min(bevel, half.x * .38f, half.y * .38f, half.z * .38f);
                var rot = Quaternion.Euler(tilt, 0, 0);
                Vector3 Point(int axis, float value) { var p = Vector3.zero; p[axis] = value; return p; }
                void Face(params Vector3[] points)
                {
                    var normal = Vector3.Cross(points[1] - points[0], points[2] - points[0]).normalized;
                    var middle = Vector3.zero;
                    foreach (var p in points) middle += p / points.Length;
                    if (Vector3.Dot(normal, middle) < 0f) System.Array.Reverse(points);
                    int first = v.Count;
                    Color32 tint = color;
                    foreach (var p in points) { v.Add(center + rot * p); colors.Add(tint); }
                    for (int i = 1; i + 1 < points.Length; i++) { tris.Add(first); tris.Add(first + i); tris.Add(first + i + 1); }
                }
                // Six faces, twelve edge bevels and eight corner caps.
                for (int axis = 0; axis < 3; axis++)
                {
                    int u = (axis + 1) % 3, v = (axis + 2) % 3;
                    foreach (int sign in new[] { -1, 1 })
                    {
                        var c = Point(axis, half[axis] * sign);
                        var a = Point(u, half[u] - b); var d = Point(v, half[v] - b);
                        Face(c - a - d, c + a - d, c + a + d, c - a + d);
                    }
                }
                for (int axis = 0; axis < 3; axis++)
                {
                    int u = (axis + 1) % 3, v = (axis + 2) % 3;
                    foreach (int x in new[] { -1, 1 }) foreach (int y in new[] { -1, 1 })
                    {
                        var a = Point(u, half[u] * x) + Point(v, (half[v] - b) * y);
                        var c = Point(u, (half[u] - b) * x) + Point(v, half[v] * y);
                        var d = Point(axis, half[axis] - b);
                        Face(a - d, c - d, c + d, a + d);
                    }
                }
                foreach (int x in new[] { -1, 1 }) foreach (int y in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 })
                {
                    var c = Vector3.Scale(half - Vector3.one * b, new Vector3(x,y,z));
                    Face(c + Point(0, x*b), c + Point(1, y*b), c + Point(2, z*b));
                }
            }
                var oak=new Color(.52f,.31f,.15f);var dark=new Color(.18f,.13f,.11f);var white=new Color(.80f,.87f,.91f);
                if(kind==FurnitureKind.Table)
                {
                    Box(new Vector3(0,.40f,0),new Vector3(1.65f,.16f,1.05f),oak);
                    Box(new Vector3(0,.49f,0),new Vector3(1.52f,.025f,.92f),new Color(.72f,.52f,.28f));
                    foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})Box(new Vector3(x*.65f,-.06f,z*.36f),new Vector3(.12f,.88f,.12f),dark);
                    Box(new Vector3(0,.28f,0),new Vector3(1.4f,.15f,.82f),dark);
                }
                else if(kind==FurnitureKind.Fridge)
                {
                    Box(Vector3.zero,new Vector3(.85f,1.7f,.75f),white);
                    Box(new Vector3(0,.52f,.39f),new Vector3(.79f,.58f,.07f),new Color(.65f,.77f,.82f));
                    Box(new Vector3(0,-.31f,.39f),new Vector3(.79f,1.02f,.07f),white);
                    Box(new Vector3(.28f,.50f,.46f),new Vector3(.045f,.28f,.05f),dark);
                    Box(new Vector3(.28f,-.17f,.46f),new Vector3(.045f,.42f,.05f),dark);
                    Box(new Vector3(-.16f,.55f,.435f),new Vector3(.19f,.12f,.015f),new Color(.15f,.7f,1f));
                    for(int j=0;j<7;j++) Box(new Vector3(0,-.45f+j*.16f,-.39f),new Vector3(.65f,.045f,.025f),dark,.006f);
                    Box(new Vector3(0,.70f,-.40f),new Vector3(.30f,.08f,.025f),new Color(.2f,.8f,1f),.006f);
                }
                else if(kind==FurnitureKind.Lamp)
                {
                    Box(new Vector3(0,-.85f,0),new Vector3(.58f,.10f,.58f),dark);
                    Box(new Vector3(0,-.14f,0),new Vector3(.065f,1.40f,.065f),new Color(.8f,.66f,.30f));
                    Box(new Vector3(0,.64f,0),new Vector3(.64f,.48f,.64f),new Color(.97f,.82f,.48f));
                    Box(new Vector3(0,.90f,0),new Vector3(.12f,.06f,.12f),dark);
                }
                else
                {
                    Box(Vector3.zero,new Vector3(1.20f,1.90f,.65f),dark);
                    foreach(int x in new[]{-1,1})
                    {
                        Box(new Vector3(x*.29f,0,.35f),new Vector3(.55f,1.74f,.08f),oak);
                        Box(new Vector3(x*.08f,.05f,.415f),new Vector3(.04f,.24f,.05f),new Color(1f,.81f,.35f));
                        Box(new Vector3(x*.29f,.03f,.40f),new Vector3(.44f,1.44f,.02f),new Color(.62f,.40f,.21f));
                    }
                    Box(new Vector3(0,.91f,0),new Vector3(1.32f,.13f,.75f),oak);
                    foreach(int x in new[]{-1,1})Box(new Vector3(x*.29f,0,-.34f),new Vector3(.50f,1.70f,.045f),oak);
                    Box(new Vector3(0,-.85f,-.37f),new Vector3(1.10f,.07f,.04f),new Color(.8f,.62f,.28f));
                }
                mesh=new Mesh{name="Wei"+kind};mesh.SetVertices(v);mesh.SetColors(colors);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
                meshes.Add(kind,mesh);materials.Add(kind,CombatVisuals.PaletteSurface(mesh,"Wei"+kind+"Finish",.3f));
            }
            root.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var view=root.gameObject.AddComponent<MeshRenderer>();view.sharedMaterial=materials[kind];view.shadowCastingMode=ShadowCastingMode.On;
        }
    }
}

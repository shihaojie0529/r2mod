using System.Collections.Generic;
using RoR2;
using UnityEngine;
using UnityEngine.Rendering;

namespace RiftSurvivor
{
    // Both throwables are readable as real furnishings from the player's camera:
    // a high-backed dining chair and a low, broad two-seat sofa with separate cushions.
    internal static class WeiFurnitureArt
    {
        private static Mesh chair, sofa;
        private static Material chairFinish, sofaFinish;
        private static Material material;
        private static Material Paint => material ? material :
            (material = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Standard")) { name = "WeiFurniturePaint" });

        internal static void Attach(Transform root, bool couch)
        {
            if (couch && !sofa) sofa = Build(true, out sofaFinish);
            if (!couch && !chair) chair = Build(false, out chairFinish);
            var filter = root.gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = couch ? sofa : chair;
            var view = root.gameObject.AddComponent<MeshRenderer>();
            view.sharedMaterial = couch ? sofaFinish : chairFinish;
            view.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static Mesh Build(bool couch, out Material finish)
        {
            var verts = new List<Vector3>();
            var swatches = new List<Color32>();
            var colorIndices = new List<int>();
            var indices = new List<int>();
            var oak = new Color(.44f,.25f,.12f);
            var walnut = new Color(.23f,.14f,.10f);
            var linen = new Color(.87f,.74f,.50f);
            var piping = new Color(.41f,.69f,.63f);
            var upholstery = new Color(.10f,.35f,.35f);

            // Opaque palette faces share one mesh and one material per furnishing.
            void Block(Vector3 center, Vector3 size, Color color, float bevel = .025f, float tilt = 0f)
            {
                float x=size.x*.5f, y=size.y*.5f, z=size.z*.5f;
                float b=Mathf.Min(bevel,x*.40f,z*.40f);
                var corners = new[]{new Vector2(-x+b,-z),new Vector2(x-b,-z),new Vector2(x,-z+b),new Vector2(x,z-b),
                    new Vector2(x-b,z),new Vector2(-x+b,z),new Vector2(-x,z-b),new Vector2(-x,-z+b)};
                var rot = Quaternion.Euler(tilt,0,0);
                void Face(Vector3[] points, float tone)
                {
                    int start = verts.Count;
                    Color32 tint = new Color(color.r*tone,color.g*tone,color.b*tone,1f);
                    int swatch = swatches.IndexOf(tint);
                    if (swatch < 0) { swatch = swatches.Count; swatches.Add(tint); }
                    foreach (var p in points)
                    {
                        verts.Add(center+rot*p);
                        colorIndices.Add(swatch);
                    }
                    for(int i=1;i+1<points.Length;i++){indices.Add(start);indices.Add(start+i);indices.Add(start+i+1);}
                }
                var top=new Vector3[8];var bottom=new Vector3[8];
                for(int i=0;i<8;i++)
                {
                    top[i]=new Vector3(corners[i].x,y,corners[i].y);
                    bottom[i]=new Vector3(corners[i].x,-y,corners[i].y);
                }
                Face(bottom,.6f);
                for(int i=0;i<8;i++){int j=(i+1)%8;Face(new[]{top[i],top[j],bottom[j],bottom[i]},.78f+.16f*Mathf.Sin(i*.8f));}
                System.Array.Reverse(top);
                Face(top,1.08f);
            }
            if (couch)
            {
                foreach(int s in new[]{-1,1}) foreach(int d in new[]{-1,1})
                    Block(new Vector3(s*.73f,-.37f,d*.30f),new Vector3(.12f,.14f,.13f),walnut);
                Block(new Vector3(0,-.18f,0),new Vector3(1.80f,.26f,.87f),upholstery,.08f);
                Block(new Vector3(0,-.12f,.435f),new Vector3(1.70f,.028f,.026f),piping);
                foreach(int s in new[]{-1,1})
                {
                    Block(new Vector3(s*.385f,-.025f,.07f),new Vector3(.74f,.15f,.64f),piping,.09f);
                    Block(new Vector3(s*.385f,.19f,-.30f),new Vector3(.755f,.51f,.20f),upholstery,.07f,-9f);
                    Block(new Vector3(s*.83f,.052f,0),new Vector3(.17f,.29f,.81f),upholstery,.055f);
                }
                Block(new Vector3(-.47f,.18f,-.12f),new Vector3(.31f,.25f,.16f),linen,.055f,-15f);
            }
            else
            {
                foreach(int s in new[]{-1,1}) foreach(int d in new[]{-1,1})
                    Block(new Vector3(s*.21f,-.265f,d*.20f),new Vector3(.062f,.45f,.067f),walnut,.012f,d*4f);
                Block(new Vector3(0,-.05f,0),new Vector3(.57f,.08f,.56f),oak,.045f);
                Block(new Vector3(0,.006f,.018f),new Vector3(.54f,.06f,.50f),linen,.055f);
                foreach(int s in new[]{-1,1})
                    Block(new Vector3(s*.218f,.22f,-.235f),new Vector3(.065f,.52f,.064f),walnut,.012f,-7f);
                Block(new Vector3(0,.396f,-.267f),new Vector3(.57f,.15f,.061f),oak,.025f,-7f);
                Block(new Vector3(0,.215f,-.248f),new Vector3(.49f,.060f,.047f),oak,.016f,-7f);
            }
            var mesh=new Mesh{name=couch?"WeiTwoSeatSofa":"WeiDiningChair"};
            mesh.SetVertices(verts);
            mesh.SetUVs(0, colorIndices.ConvertAll(i => new Vector2((i + .5f) / swatches.Count, .5f)));
            mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            finish = new Material(Shader.Find("Unlit/Texture") ?? Shader.Find("Standard"))
            {
                name = mesh.name + "Finish",
                mainTexture = FriendSkin.Palette(mesh.name + "Palette", swatches.ToArray())
            };
            return mesh;
        }

        internal static TrailRenderer Trail(Transform root, bool mob)
        {
            var trail = root.gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = Paint;
            trail.time = mob ? .26f : .18f;
            trail.minVertexDistance = .10f;
            trail.startWidth = mob ? .38f : .21f;
            trail.endWidth = .015f;
            trail.startColor = mob ? new Color(.27f,.87f,1f,.92f) : new Color(.58f,.98f,.85f,.65f);
            trail.endColor = new Color(.1f,.46f,.9f,0f);
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.emitting = false;
            return trail;
        }

        internal static GameObject Frame(bool automatic = false)
        {
            var frame = new GameObject(automatic ? "WeiAutomaticLock" : "WeiSelectionFrame");
            frame.AddComponent<Billboard>();
            foreach (int x in new[]{-1,1}) foreach(int y in new[]{-1,1})
            {
                var line = new GameObject("Bracket").AddComponent<LineRenderer>();
                line.transform.SetParent(frame.transform,false);
                line.sharedMaterial = Paint;
                line.useWorldSpace = false;
                line.positionCount = 3;
                if (automatic)
                    line.SetPositions(new[]{new Vector3(x*.11f,y*.34f,0),new Vector3(x*.225f,y*.225f,0),new Vector3(x*.34f,y*.11f,0)});
                else
                    line.SetPositions(new[]{new Vector3(x*.27f,y*.5f,0),new Vector3(x*.5f,y*.5f,0),new Vector3(x*.5f,y*.27f,0)});
                line.startWidth = line.endWidth = .047f;
                line.startColor = line.endColor = automatic ? new Color(.55f,.88f,1f) : new Color(.18f,.83f,1f);
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
            }
            return frame;
        }
    }
}

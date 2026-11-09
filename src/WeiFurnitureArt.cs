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
            (material = CombatVisuals.Stroke("WeiFurniturePaint"));

        internal static void Attach(Transform root, bool couch)
        {
            if (couch && !sofa) sofa = Build(true, out sofaFinish);
            if (!couch && !chair) chair = Build(false, out chairFinish);
            var filter = root.gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = couch ? sofa : chair;
            var view = root.gameObject.AddComponent<MeshRenderer>();
            view.sharedMaterial = couch ? sofaFinish : chairFinish;
            view.shadowCastingMode = ShadowCastingMode.On;
            view.receiveShadows = true;
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
                    int first = verts.Count;
                    Color32 tint = color;
                    int swatch = swatches.IndexOf(tint);
                    if (swatch < 0) { swatch = swatches.Count; swatches.Add(tint); }
                    foreach (var p in points) { verts.Add(center + rot * p); colorIndices.Add(swatch); }
                    for (int i = 1; i + 1 < points.Length; i++) { indices.Add(first); indices.Add(first + i); indices.Add(first + i + 1); }
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
                foreach (int sign in new[] { -1, 1 })
                {
                    // Upholstery seams remain geometry at normal play distance.
                    Block(new Vector3(sign*.385f,.19f,-.408f), new Vector3(.012f,.38f,.009f), piping, .003f, -9f);
                    Block(new Vector3(sign*.385f,.39f,-.335f), new Vector3(.60f,.009f,.014f), piping, .003f, -9f);
                    Block(new Vector3(sign*.385f,.17f,-.420f), new Vector3(.035f,.035f,.009f), upholstery, .008f);
                    Block(new Vector3(sign*.385f,.053f,.377f), new Vector3(.66f,.009f,.012f), linen, .003f);
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
                foreach (int sign in new[] { -1, 1 })
                {
                    Block(new Vector3(sign*.21f,-.19f,0), new Vector3(.028f,.034f,.40f), oak, .007f);
                    Block(new Vector3(sign*.255f,.039f,.018f), new Vector3(.008f,.008f,.46f), piping, .002f);
                }
                Block(new Vector3(0,-.18f,.20f), new Vector3(.42f,.034f,.028f), oak, .007f);
                Block(new Vector3(0,.039f,.255f), new Vector3(.50f,.008f,.008f), piping, .002f);
            }
            var mesh=new Mesh{name=couch?"WeiTwoSeatSofa":"WeiDiningChair"};
            mesh.SetVertices(verts);
            mesh.SetUVs(0, colorIndices.ConvertAll(i => new Vector2((i + .5f) / swatches.Count, .5f)));
            mesh.SetTriangles(indices,0);CombatVisuals.SmoothNormals(mesh, 52f);mesh.RecalculateBounds();
            finish = CombatVisuals.Surface(mesh.name + "Finish",
                FriendSkin.Palette(mesh.name + "Palette", swatches.ToArray()), couch ? .18f : .26f);
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

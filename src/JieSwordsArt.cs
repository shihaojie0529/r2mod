using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RiftSurvivor
{
    // One shared, collider-free, double-edged jian with the tip along +Z.
    // Blue and golden swords use the same silhouette and distinct blade colors.
    internal static class JieSwordArt
    {
        private static Mesh sword;
        private static Material fittings;
        private static readonly Dictionary<Color, Material> blades = new Dictionary<Color, Material>();

        internal static void Attach(Transform parent, Color color)
        {
            if (!sword) sword = Build();
            if (!fittings)
                fittings = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Standard")) { name = "JieSwordFittings" };
            if (!blades.TryGetValue(color, out var material))
            {
                material = new Material(fittings) { name = "JieSwordBlade", color = color };
                blades.Add(color, material);
            }
            var filter = parent.gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = sword;
            var renderer = parent.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { material, fittings };
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static Mesh Build()
        {
            var positions = new List<Vector3>();
            var colors = new List<Color>();
            var blade = new List<int>();
            var furniture = new List<int>();

            void Face(List<int> triangles, Color tint, params Vector3[] vertices)
            {
                int first = positions.Count;
                foreach (var vertex in vertices) { positions.Add(vertex); colors.Add(tint); }
                for (int i = 1; i + 1 < vertices.Length; i++)
                {
                    triangles.Add(first);
                    triangles.Add(first + i);
                    triangles.Add(first + i + 1);
                }
            }

            Vector3[] Section(float z, float halfWidth, float ridge) => new[] {
                new Vector3(-halfWidth, 0, z), new Vector3(0, ridge, z),
                new Vector3(halfWidth, 0, z), new Vector3(0, -ridge, z) };
            var heel = Section(-.20f, .032f, .014f);
            var shoulder = Section(-.12f, .047f, .018f);
            var taper = Section(.36f, .028f, .012f);
            var tip = new Vector3(0, 0, .56f);
            var facets = new[] { Color.white, new Color(.53f,.75f,.81f), new Color(.73f,.86f,.90f), new Color(.91f,.99f,1f) };
            for (int i = 0; i < 4; ++i)
            {
                int j = (i + 1) % 4;
                Face(blade, facets[i], heel[i], shoulder[i], shoulder[j], heel[j]);
                Face(blade, facets[i], shoulder[i], taper[i], taper[j], shoulder[j]);
                Face(blade, facets[i], taper[i], tip, taper[j]);
            }

            var bronze = new Color(.84f,.66f,.32f);
            var brass = new Color(1f,.87f,.53f);
            var grip = new Color(.10f,.16f,.18f);
            void Sleeve(float z0, float z1, float radius0, float radius1, Color tint)
            {
                const int n = 8;
                Vector3 Ring(int i, float z, float r) => new Vector3(
                    Mathf.Cos(i * 2f*Mathf.PI/n)*r, Mathf.Sin(i * 2f*Mathf.PI/n)*r, z);
                for (int i = 0; i < n; i++)
                {
                    Face(furniture, tint, Ring(i,z0,radius0), Ring(i+1,z0,radius0), Ring(i+1,z1,radius1), Ring(i,z1,radius1));
                    Face(furniture, tint, Ring(i,z0,radius0), new Vector3(0,0,z0), Ring(i+1,z0,radius0));
                    Face(furniture, tint, Ring(i+1,z1,radius1), new Vector3(0,0,z1), Ring(i,z1,radius1));
                }
            }
            Sleeve(-.49f,-.245f,.020f,.023f,grip);
            Sleeve(-.54f,-.50f,.014f,.034f,brass);
            Sleeve(-.50f,-.475f,.034f,.020f,bronze);
            for (int ring = 0; ring < 4; ring++)
            {
                float z = -.452f + .050f*ring;
                Sleeve(z,z+.009f,.024f,.024f,bronze);
            }
            Sleeve(-.268f,-.190f,.030f,.033f,brass);
            // Swept shoulders on the guard read clearly from both flanks.
            for (int sign = -1; sign <= 1; sign += 2)
            {
                var profile = new[] { new Vector2(0,-.196f),new Vector2(.071f,-.188f),
                    new Vector2(.124f,-.202f),new Vector2(.099f,-.225f),new Vector2(.022f,-.25f),new Vector2(0,-.25f) };
                var front = new Vector3[profile.Length]; var back = new Vector3[profile.Length];
                for (int i=0; i<profile.Length; i++)
                {
                    var point = profile[sign < 0 ? profile.Length - 1 - i : i];
                    front[i] = new Vector3(sign*point.x,.017f,point.y);
                    back[i] = new Vector3(sign*point.x,-.017f,point.y);
                }
                Face(furniture, brass, front);
                for (int i=0; i<front.Length; i++)
                {
                    int j = (i+1)%front.Length;
                    Face(furniture, bronze, front[i],back[i],back[j],front[j]);
                }
                System.Array.Reverse(back);
                Face(furniture, bronze, back);
            }
            var mesh = new Mesh { name = "JieDoubleEdgedSword", subMeshCount = 2 };
            mesh.SetVertices(positions);
            mesh.SetColors(colors);
            mesh.SetTriangles(blade, 0);
            mesh.SetTriangles(furniture, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

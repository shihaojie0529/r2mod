using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RiftSurvivor
{
    internal static class XiaoheiRoundArt
    {
        internal static readonly Color Core = new Color32(174, 255, 242, 255);
        internal static readonly Color Amber = new Color32(255, 174, 66, 255);

        private enum Finish { Graphite, Titanium, Teal, Core, Amber }
        private const int Sides = 32;
        private static Mesh round;
        private static Material jacket, glow;

        internal static Material Glow => glow ? glow :
            (glow = CombatVisuals.Stroke("XiaoheiLightTrail"));

        // The bearing follows local +Z. Only the effect instance owns the trail history.
        internal static void Attach(Transform parent, float visualSpeed = 85f)
        {
            if (!round) round = BuildRound();
            if (!jacket) jacket = MakeJacket();
            parent.gameObject.AddComponent<MeshFilter>().sharedMesh = round;
            var renderer = parent.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = jacket;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var emitter = new GameObject("XiaoheiCoreExhaust").transform;
            emitter.SetParent(parent, false);
            emitter.localPosition = Vector3.back * .40f;
            var trail = emitter.gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = Glow;
            // Long shots cap their flight time and travel faster. Keep the wake
            // below four metres at that speed as well as on a close-range turn.
            trail.time = Mathf.Min(.052f, 3.8f / Mathf.Max(1f, visualSpeed));
            trail.minVertexDistance = .08f;
            trail.startWidth = .12f;
            trail.endWidth = .012f;
            trail.startColor = Core;
            trail.endColor = new Color(.12f, .74f, .76f, 0f);
            trail.numCapVertices = 4;
            trail.numCornerVertices = 4;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
        }

        private static Material MakeJacket()
        {
            // One opaque draw with a tiny palette. A bevel remains visible when the
            // camera looks straight down the bore, even in a dark stage.
            Color[] finishes = { new Color32(34, 51, 61, 255), new Color32(178, 200, 200, 255),
                new Color32(48, 172, 176, 255), Core, Amber };
            var pixels = new Color32[Sides * finishes.Length];
            var emission = new Color32[pixels.Length];
            for (int side = 0; side < Sides; side++)
            {
                float angle = (side + .5f) * (2f * Mathf.PI / Sides);
                float light = .73f + .27f * Mathf.Cos(angle - 2.1f);
                for (int i = 0; i < finishes.Length; i++)
                {
                    bool luminous = i == (int)Finish.Core || i == (int)Finish.Amber;
                    Color color = finishes[i] * (luminous ? Mathf.Lerp(.76f, 1f, light) : light);
                    color.a = 1f;
                    pixels[side * finishes.Length + i] = color;
                    emission[side * finishes.Length + i] = luminous ? (Color32)(finishes[i] * (i == (int)Finish.Core ? 1f : .48f)) : new Color32(0,0,0,255);
                }
            }
            var texture = new Texture2D(finishes.Length, Sides, TextureFormat.RGBA32, false)
                { name = "XiaoheiRoundPalette", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            var emissionTexture = new Texture2D(finishes.Length, Sides, TextureFormat.RGBA32, false)
                { name = "XiaoheiRoundEmission", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            emissionTexture.SetPixels32(emission); emissionTexture.Apply(false, false);
            return CombatVisuals.Surface("XiaoheiSeekerJacket", texture, .58f, emissionTexture, 1.2f);
        }

        private static Mesh BuildRound()
        {
            var points = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            var directions = new Vector3[Sides];
            for (int i = 0; i < Sides; i++)
            {
                float angle = i * (2f * Mathf.PI / Sides);
                directions[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            }

            float z = -.40f, radius = 0f;
            void Ring(float nextZ, float nextRadius, Finish finish, bool windows = false)
            {
                for (int i = 0; i < Sides; i++)
                {
                    int j = (i + 1) % Sides;
                    int start = points.Count;
                    points.Add(Vector3.forward * z + directions[i] * radius);
                    points.Add(Vector3.forward * z + directions[j] * radius);
                    points.Add(Vector3.forward * nextZ + directions[j] * nextRadius);
                    points.Add(Vector3.forward * nextZ + directions[i] * nextRadius);
                    // Four graphite channels expose the pistol's pale power cell.
                    var surface = windows && i % (Sides / 4) < 2 ? Finish.Core : finish;
                    var uv = new Vector2(((int)surface + .5f) / ((int)Finish.Amber + 1), (i + .5f) / Sides);
                    for (int k = 0; k < 4; k++) uvs.Add(uv);
                    if (radius > 0f)
                    {
                        triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                    }
                    if (nextRadius > 0f)
                    {
                        triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
                    }
                }
                radius = nextRadius;
                z = nextZ;
            }

            // From the firing player's view the base reads as an illuminated primer,
            // nested inside a dark rim. The projecting amber band sits at the nose.
            Ring(-.40f, .068f, Finish.Core);
            Ring(-.385f, .098f, Finish.Teal);
            Ring(-.39f, .128f, Finish.Graphite);
            Ring(-.366f, .145f, Finish.Titanium);
            Ring(-.323f, .145f, Finish.Graphite);
            Ring(-.305f, .120f, Finish.Graphite);
            Ring(-.264f, .120f, Finish.Teal);
            Ring(-.244f, .146f, Finish.Titanium);
            Ring(.032f, .151f, Finish.Graphite, windows: true);
            Ring(.064f, .146f, Finish.Titanium);
            Ring(.082f, .163f, Finish.Titanium);
            Ring(.092f, .177f, Finish.Amber);
            Ring(.126f, .177f, Finish.Amber);
            Ring(.139f, .163f, Finish.Titanium);
            Ring(.155f, .139f, Finish.Amber);
            Ring(.207f, .130f, Finish.Teal);
            Ring(.278f, .113f, Finish.Teal);
            Ring(.343f, .084f, Finish.Core);
            Ring(.382f, .055f, Finish.Core);
            Ring(.401f, .031f, Finish.Core);
            Ring(.408f, 0f, Finish.Core);

            var mesh = new Mesh { name = "XiaoheiSeekerRound" };
            mesh.SetVertices(points);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            CombatVisuals.SmoothNormals(mesh, 48f);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

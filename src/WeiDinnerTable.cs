using System.Collections.Generic;
using RoR2;
using UnityEngine;
using UnityEngine.Rendering;

namespace RiftSurvivor
{
    internal sealed class WeiDinnerTable : MonoBehaviour
    {
        private static Mesh tableMesh;
        private static Material paint;
        private Transform table;
        private LineRenderer door, shock;
        private float age;

        private void Start()
        {
            transform.position = GetComponent<EffectComponent>().effectData.origin;
            if (!tableMesh) tableMesh = BuildTable();
            if (!paint) paint = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Standard"));
            table = new GameObject("FourPlaceSettings").transform;
            table.SetParent(transform, false);
            table.gameObject.AddComponent<MeshFilter>().sharedMesh = tableMesh;
            var renderer = table.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = paint;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            door = JieStormEffects.Line(transform, .10f);
            door.startColor = door.endColor = new Color(.25f, .83f, 1f);
            door.loop = true;
            door.positionCount = 4;
            door.SetPositions(new[] {new Vector3(-2.6f, 7f, -1.7f), new Vector3(2.6f, 7f, -1.7f),
                new Vector3(2.6f, 7f, 1.7f), new Vector3(-2.6f, 7f, 1.7f)});
            shock = JieStormEffects.Line(transform, .14f);
            shock.loop = true;
            shock.positionCount = 60;
            for (int i = 0; i < shock.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / shock.positionCount;
                shock.SetPosition(i, new Vector3(Mathf.Cos(angle), .03f, Mathf.Sin(angle)));
            }
            Animate();
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (table) Animate();
            if (age > 1.05f) Destroy(gameObject);
        }

        private void Animate()
        {
            float fall = Mathf.Clamp01(age / WeiBanquet.FallTime);
            table.localPosition = Vector3.up * (7f * (1f - fall * fall));
            table.localRotation = Quaternion.Euler(0f, -10f + 10f * fall, (1f - fall) * 12f);
            door.enabled = fall < 1f;
            float after = Mathf.Max(0f, age - WeiBanquet.FallTime);
            shock.enabled = after > 0f && after < .33f;
            shock.transform.localScale = Vector3.one * Mathf.Lerp(.1f, 8f, Mathf.Clamp01(after / .33f));
            table.localScale = Vector3.one * (1f - Mathf.Clamp01((after - .42f) / .25f));
        }

        private static Mesh BuildTable()
        {
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            var colors = new List<Color32>();
            var wood = new Color32(145, 78, 53, 255);
            var darkWood = new Color32(88, 50, 41, 255);
            var cloth = new Color32(185, 66, 48, 255);
            var glaze = new Color32(113, 162, 177, 255);
            var rice = new Color32(251, 241, 203, 255);
            var bamboo = new Color32(228, 188, 128, 255);

            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 color)
            {
                int i = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                for (int n = 0; n < 4; ++n) colors.Add(color);
                indices.Add(i); indices.Add(i+1); indices.Add(i+2);
                indices.Add(i); indices.Add(i+2); indices.Add(i+3);
            }
            void Cap(Vector3 a, Vector3 peak, Vector3 b)
            {
                int i = vertices.Count;
                vertices.Add(a); vertices.Add(peak); vertices.Add(b);
                colors.Add(rice); colors.Add(rice); colors.Add(rice);
                indices.Add(i); indices.Add(i+1); indices.Add(i+2);
            }
            void Box(Vector3 center, Vector3 extent, Color32 top, Color32 edge)
            {
                var corners = new Vector3[8];
                for (int i = 0; i < 8; ++i)
                    corners[i] = center + new Vector3((i & 1) == 0 ? -extent.x : extent.x,
                        (i & 2) == 0 ? -extent.y : extent.y, (i & 4) == 0 ? -extent.z : extent.z);
                Quad(corners[2], corners[6], corners[7], corners[3], top);
                Quad(corners[0], corners[1], corners[5], corners[4], edge);
                Quad(corners[0], corners[2], corners[3], corners[1], edge);
                Quad(corners[4], corners[5], corners[7], corners[6], edge);
                Quad(corners[0], corners[4], corners[6], corners[2], edge);
                Quad(corners[1], corners[3], corners[7], corners[5], edge);
            }
            Box(new Vector3(0f, 1.18f, 0f), new Vector3(2.05f, .12f, 1.24f), wood, darkWood);
            Box(new Vector3(0f, 1.31f, 0f), new Vector3(.48f, .012f, 1.25f), cloth, cloth);
            for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Box(new Vector3(x*1.76f, .54f, z*.98f), new Vector3(.10f, .54f, .10f), wood, darkWood);
                var center = new Vector3(x*.99f, 1.30f, z*.70f);
                for (int i = 0; i < 16; ++i)
                {
                    float a = i * Mathf.PI / 8f, b = (i+1) * Mathf.PI / 8f;
                    var u = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    var v = new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b));
                    Quad(center + u*.13f, center + u*.32f + Vector3.up*.20f,
                        center + v*.32f + Vector3.up*.20f, center + v*.13f, glaze);
                    Quad(center + v*.32f + Vector3.up*.20f, center + u*.32f + Vector3.up*.20f,
                        center + u*.17f + Vector3.up*.27f, center + v*.17f + Vector3.up*.27f, rice);
                    Cap(center + u*.17f + Vector3.up*.27f, center + Vector3.up*.31f,
                        center + v*.17f + Vector3.up*.27f);
                }
                for (int stick = 0; stick < 2; ++stick)
                    Box(center + new Vector3(.42f + stick*.067f, .036f, 0f),
                        new Vector3(.020f, .022f, .36f), bamboo, bamboo);
            }
            var mesh = new Mesh {name = "WeiFamilyDinner"};
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

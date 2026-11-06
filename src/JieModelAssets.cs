using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace RiftSurvivor
{
    // Both photo-inspired sculpts use the same palette, rig and authored-normal format.
    internal sealed class FriendSkin
    {
        [Serializable] private sealed class Sculpt { public Joint[] joints = null; public Part[] parts = null; }
        [Serializable] private sealed class Joint { public string name = null; public float[] pivot = null; }
        [Serializable] private sealed class Part
        {
            public string bone = null;
            public int[] tint = null, triangles = null;
            public float[] vertices = null, normals = null;
        }

        internal readonly Mesh Mesh;
        internal readonly Material Material;
        internal readonly Vector3[] Pivots;
        internal readonly Color32[] Colors;

        private static Stream Resource(string name, string suffix) =>
            typeof(FriendSkin).Assembly.GetManifestResourceStream("RiftSurvivor." + name.ToLowerInvariant() + "-" + suffix)
            ?? throw new InvalidDataException("Missing " + name + " " + suffix);

        internal static Texture2D Portrait(string name)
        {
            using var input = Resource(name, "portrait.png");
            using var bytes = new MemoryStream();
            input.CopyTo(bytes);
            var image = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                { name = name + "Portrait", wrapMode = TextureWrapMode.Clamp };
            if (!ImageConversion.LoadImage(image, bytes.ToArray()))
                throw new InvalidDataException("Invalid portrait for " + name);
            return image;
        }

        internal static Texture2D Palette(string name, Color32[] colors)
        {
            var palette = new Texture2D(colors.Length, 1, TextureFormat.RGBA32, false)
                { name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            palette.SetPixels32(colors);
            palette.Apply(false, true);
            return palette;
        }

        private static Vector3 Vector(float[] xyz, int n) => new Vector3(xyz[n], xyz[n + 1], xyz[n + 2]);

        internal FriendSkin(string name, Shader shader, string[] boneNames)
        {
            using var stream = Resource(name, "model.json");
            using var reader = new StreamReader(stream);
            var sculpt = JsonUtility.FromJson<Sculpt>(reader.ReadToEnd());
            Pivots = new Vector3[boneNames.Length];
            foreach (var joint in sculpt.joints)
            {
                int bone = Array.IndexOf(boneNames, joint.name);
                if (bone < 0) throw new InvalidDataException("Unknown " + name + " joint " + joint.name);
                Pivots[bone] = Vector(joint.pivot, 0);
            }
            Colors = new Color32[sculpt.parts.Length];
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var texCoords = new List<Vector2>();
            var weights = new List<BoneWeight>();
            var triangles = new List<int>();
            for (int i = 0; i < sculpt.parts.Length; ++i)
            {
                var part = sculpt.parts[i];
                int bone = Array.IndexOf(boneNames, part.bone);
                if (bone < 0) throw new InvalidDataException("Unknown " + name + " joint " + part.bone);
                int start = verts.Count;
                Colors[i] = new Color32((byte)part.tint[0], (byte)part.tint[1], (byte)part.tint[2], 255);
                var uv = new Vector2((i + .5f) / Colors.Length, .5f);
                for (int v = 0; v < part.vertices.Length; v += 3)
                {
                    verts.Add(Vector(part.vertices, v));
                    normals.Add(Vector(part.normals, v));
                    texCoords.Add(uv);
                    weights.Add(new BoneWeight { boneIndex0 = bone, weight0 = 1f });
                }
                foreach (var index in part.triangles) triangles.Add(start + index);
            }
            Material = new Material(shader) { name = name + "Cloth", mainTexture = Palette(name + "Palette", Colors) };
            if (Material.HasProperty("_Cull")) Material.SetFloat("_Cull", (float)CullMode.Off);
            if (Material.HasProperty("_Smoothness")) Material.SetFloat("_Smoothness", .12f);
            if (Material.HasProperty("_NormalStrength")) Material.SetFloat("_NormalStrength", 0f);
            Mesh = new Mesh { name = name + "Skin",
                indexFormat = verts.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            Mesh.SetVertices(verts);
            Mesh.SetNormals(normals);
            Mesh.SetUVs(0, texCoords);
            Mesh.SetTriangles(triangles, 0);
            Mesh.bindposes = Array.ConvertAll(Pivots, p => Matrix4x4.Translate(-p));
            Mesh.boneWeights = weights.ToArray();
            Mesh.RecalculateBounds();
        }
    }
}

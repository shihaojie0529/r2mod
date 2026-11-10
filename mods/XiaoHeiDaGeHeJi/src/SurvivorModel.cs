using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RoR2;
using UnityEngine;
using UnityEngine.Rendering;

namespace RiftSurvivor
{
    // Bind the authored appearance in Awake before its first rendered frame.
    [DefaultExecutionOrder(30000)]
    public sealed class SurvivorModel : MonoBehaviour
    {
        [Serializable] private sealed class Geometry { public Part[] parts = null; }
        [Serializable] private sealed class Part
        {
            public string bone = null;
            public float[] pivot = null;
            public float[] color = null;
            public float[] colorEnd = null;
            public float[] shade = null;
            public float[] vertices = null;
            public int[] triangles = null;
            [NonSerialized] public float firstU;
            [NonSerialized] public float lastU;

            public Vector2 PaletteUV(int vertex) => new Vector2(
                firstU == lastU ? firstU : Mathf.Lerp(firstU, lastU, shade[vertex]), .5f);
        }

        private static Geometry geometry;
        private static Material paletteMaterial;
        private static Mesh costume;
        private static Vector3[] pivots;
        private static bool loggedFailure;
        private CharacterModel model;
        private CharacterBody body;
        private SkinnedMeshRenderer target;
        private CuiRig rig;

        internal static void Install(GameObject prefab)
        {
            OriginalModelGuard.Prepare(prefab);
            var characterModel = prefab.GetComponentInChildren<CharacterModel>(true);
            var fallback = prefab.GetComponentInChildren<Animator>(true);
            var owner = characterModel ? characterModel.gameObject : (fallback ? fallback.gameObject : prefab);
            owner.AddComponent<CuiMotion>();
            owner.AddComponent<SurvivorModel>();
        }

        private void Awake()
        {
            model = GetComponent<CharacterModel>();
            body = GetComponentInParent<CharacterBody>();
            var renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            target = Array.Find(renderers, r => string.Equals(r.name, "CommandoMesh", StringComparison.OrdinalIgnoreCase));
            if (!target && model) target = model.mainSkinnedMeshRenderer;
            if (!target) target = renderers.OrderByDescending(r => r.bones.Length).FirstOrDefault();
            LateUpdate();
        }

        private void LateUpdate()
        {
            if (!target) return;
            var infos = model ? model.baseRendererInfos : null;
            int slot = -1;
            for (int i = 0; infos != null && i < infos.Length; ++i)
                if (infos[i].renderer == target) { slot = i; break; }
            if (rig == null)
            {
                try
                {
                    var shader = FindSkinShader(target, slot >= 0 ? infos[slot].defaultMaterial : null);
                    if (!shader) return;
                    LoadGeometry(shader);
                    if (!costume) costume = BuildMesh();
                    body = model && model.body ? model.body : GetComponentInParent<CharacterBody>();
                    rig = new CuiRig(transform, target, body, pivots);
                    HideOriginalGear();
                    AttachSkin();
                    GetComponent<CuiMotion>().Bind(rig, model, body, target);
                }
                catch (Exception error)
                {
                    if (!loggedFailure) Debug.LogError("[RiftSurvivor] Could not attach Cui Ge's animated model: " + error);
                    loggedFailure = true;
                    enabled = false;
                    return;
                }
            }
            var material = SurvivorDisplay.DisplayMaterial(paletteMaterial, body);
            if (target.sharedMesh != costume || target.rootBone != rig.Root) AttachSkin();
            if (slot >= 0 && infos[slot].defaultMaterial != material)
            {
                infos[slot].defaultMaterial = material;
                model.materialsDirty = true;
            }
            else if (slot < 0 && target.sharedMaterial != material)
                target.sharedMaterial = SurvivorDisplay.DisplayMaterial(paletteMaterial, body);
            OriginalModelGuard.Reveal(target);
        }

        // The base prefabs can have a renderer before their addressable mesh or material arrives.
        internal static Shader FindSkinShader(Renderer renderer, Material slotMaterial)
        {
            if (slotMaterial && slotMaterial.shader) return slotMaterial.shader;
            var gameShader = CombatVisuals.TextureShader;
            if (gameShader) return gameShader;
            var material = renderer.sharedMaterial;
            return material && material.shader ? material.shader : CombatVisuals.TextureShader;
        }

        private void AttachSkin()
        {
            target.sharedMesh = costume;
            target.bones = rig.OrderedBones;
            target.rootBone = rig.Root;
            target.sharedMaterial = SurvivorDisplay.DisplayMaterial(paletteMaterial, body);
            target.localBounds = new Bounds(new Vector3(0, 1f, .15f), new Vector3(3f, 3.3f, 3f));
        }

        private void HideOriginalGear()
        {
            // Keep native attachment transforms, hurtboxes and future item displays functional.
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r == target || !(r is MeshRenderer || r is SkinnedMeshRenderer) || r.GetComponentInParent<ItemDisplay>()) continue;
                string label = r.name.ToLowerInvariant();
                if (label.Contains("gun") || label.Contains("pistol") || label.Contains("jetpack")) r.forceRenderingOff = true;
            }
        }

        private static void LoadGeometry(Shader shader)
        {
            if (geometry != null)
                return;
            Geometry data;
            using (var stream = typeof(SurvivorModel).Assembly.GetManifestResourceStream("RiftSurvivor.model.json"))
            using (var reader = new StreamReader(stream ?? throw new InvalidDataException("Embedded model missing.")))
                data = JsonUtility.FromJson<Geometry>(reader.ReadToEnd());

            if (data?.parts == null || data.parts.Length == 0)
                throw new InvalidDataException("Model has no geometry.");

            var palette = CreatePalette(data.parts);
            paletteMaterial = new Material(shader) { name = "RiftRunnerCloth" };
            paletteMaterial.SetTexture("_MainTex", palette);
            if (paletteMaterial.HasProperty("_Color"))
                paletteMaterial.SetColor("_Color", Color.white);
            if (paletteMaterial.HasProperty("_Cull"))
                paletteMaterial.SetFloat("_Cull", (float)CullMode.Off);
            if (paletteMaterial.HasProperty("_NormalStrength"))
                paletteMaterial.SetFloat("_NormalStrength", 0f);
            if (paletteMaterial.HasProperty("_Smoothness"))
                paletteMaterial.SetFloat("_Smoothness", 0.08f);
            if (paletteMaterial.HasProperty("_SpecularStrength")) paletteMaterial.SetFloat("_SpecularStrength", 0f);
            CombatVisuals.ApplyReadability(paletteMaterial);
            geometry = data;
        }

        private static Texture2D CreatePalette(Part[] parts)
        {
            // Solids sample a texel center; gradients interpolate only between
            // their own adjacent endpoint centers. The texture has no mipmaps.
            int width = parts.Length + parts.Count(part => part.colorEnd?.Length > 0);
            var pixels = new Color32[width];
            int index = 0;
            foreach (var part in parts)
            {
                bool gradient = part.colorEnd?.Length > 0;
                bool hasShade = part.shade?.Length > 0;
                if (part.color?.Length != 3 || gradient != hasShade ||
                    (gradient && (part.colorEnd.Length != 3 || part.vertices?.Length != 3 * part.shade.Length)))
                    throw new InvalidDataException("Invalid model palette or vertex shading on " + part.bone);

                part.firstU = (index + .5f) / width;
                part.lastU = part.firstU;
                pixels[index++] = new Color(part.color[0], part.color[1], part.color[2], 1f);
                if (gradient)
                {
                    part.lastU = (index + .5f) / width;
                    pixels[index++] = new Color(part.colorEnd[0], part.colorEnd[1], part.colorEnd[2], 1f);
                }
            }
            var palette = new Texture2D(width, 1, TextureFormat.RGBA32, false, false)
            {
                name = "RiftRunnerPalette", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            palette.SetPixels32(pixels);
            palette.Apply(false, false);
            return palette;
        }

        private static Mesh BuildMesh()
        {
            var positions = new List<Vector3>();
            var texCoords = new List<Vector2>();
            var weights = new List<BoneWeight>();
            var indices = new List<int>();
            var binds = new Matrix4x4[CuiRig.Names.Length];
            pivots = new Vector3[CuiRig.Names.Length];
            foreach (var part in geometry.parts)
            {
                int bone = Array.IndexOf(CuiRig.Names, part.bone);
                if (bone < 0) throw new InvalidDataException("Unknown animated model joint: " + part.bone);
                pivots[bone] = Vector(part.pivot, 0);
                binds[bone] = Matrix4x4.Translate(-pivots[bone]);
                bool shoulder = part.bone == "LeftUpperArm" || part.bone == "RightUpperArm";
                int start = positions.Count;
                for (int v=0; v<part.vertices.Length; v+=3)
                {
                    var vertex = Vector(part.vertices,v);
                    positions.Add(vertex);
                    var weight = new BoneWeight {boneIndex0 = bone, weight0 = 1f};
                    if (shoulder)
                    {
                        // Pin the inside of the deltoid to the torso while the outer arm swings.
                        float chest = .78f * (1f - Mathf.SmoothStep(0, 1, (Mathf.Abs(vertex.x) - .158f) / .126f));
                        // Unity expects influences sorted by weight, including on low skin-quality settings.
                        int chestJoint = 1;
                        weight.boneIndex0 = chest > .5f ? chestJoint : bone;
                        weight.boneIndex1 = chest > .5f ? bone : chestJoint;
                        weight.weight0 = Mathf.Max(chest, 1f - chest);
                        weight.weight1 = 1f - weight.weight0;
                    }
                    weights.Add(weight);
                    texCoords.Add(part.PaletteUV(v / 3));
                }
                foreach (int index in part.triangles) indices.Add(start + index);
            }
            var mesh = new Mesh {name = "CuiGeSkin", indexFormat = positions.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16};
            mesh.SetVertices(positions);
            mesh.SetUVs(0, texCoords);
            mesh.SetTriangles(indices, 0);
            mesh.bindposes = binds;
            mesh.boneWeights = weights.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            return mesh;
        }

        private static Vector3 Vector(float[] xyz, int offset) => new Vector3(xyz[offset], xyz[offset + 1], xyz[offset + 2]);
    }
}

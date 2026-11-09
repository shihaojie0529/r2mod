using System.Collections.Generic;
using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;

namespace RiftSurvivor
{
    // RoR2 1.5 redirects Shader.Find through LegacyShaderAPI, which throws for
    // built-in Unity shader names. These keys are present in the game's catalog.
    internal static class CombatVisuals
    {
        private static Shader vertexShader, textureShader, radianceShader;
        private static Material trailMaterial;
        private static readonly Dictionary<Texture2D, Texture2D> readablePalettes = new Dictionary<Texture2D, Texture2D>();
        internal static Shader VertexShader => vertexShader ? vertexShader :
            (vertexShader = Addressables.LoadAssetAsync<Shader>("RoR2/Base/Shaders/HGVertexOnly.shader").WaitForCompletion());
        internal static Shader TextureShader => textureShader ? textureShader :
            (textureShader = Addressables.LoadAssetAsync<Shader>("RoR2/Base/Shaders/HGStandard.shader").WaitForCompletion());

        // Keep dark clothes and steel readable in unlit parts of a stage. The
        // albedo lift preserves hues; a small emissive fill preserves the shape
        // under weak scene light without removing normal/specular lighting.
        internal static void ApplyReadability(Material material, Texture emission = null, float glow = 0f)
        {
            var source = material.mainTexture as Texture2D;
            if (!source || !source.isReadable) return;
            if (!readablePalettes.TryGetValue(source, out var palette))
            {
                var pixels = source.GetPixels32();
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color color = pixels[i];
                    float shadow = 1f - Mathf.SmoothStep(.35f, .80f, Mathf.Max(color.r, color.g, color.b));
                    color.r = Mathf.Lerp(color.r, Mathf.Min(.985f, .065f + .97f * Mathf.Pow(color.r, .85f)), shadow);
                    color.g = Mathf.Lerp(color.g, Mathf.Min(.985f, .065f + .97f * Mathf.Pow(color.g, .85f)), shadow);
                    color.b = Mathf.Lerp(color.b, Mathf.Min(.985f, .065f + .97f * Mathf.Pow(color.b, .85f)), shadow);
                    pixels[i] = color;
                }
                palette = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false)
                    { name = source.name + "Readable", filterMode = source.filterMode, wrapMode = source.wrapMode };
                palette.SetPixels32(pixels); palette.Apply(false, false);
                readablePalettes.Add(source, palette);
                readablePalettes.Add(palette, palette);
            }
            material.mainTexture = palette;
            if (!material.HasProperty("_EmTex")) return;
            var colors = palette.GetPixels32();
            var explicitLight = emission as Texture2D;
            var lights = explicitLight && explicitLight.isReadable ? explicitLight.GetPixels32() : null;
            float power = Mathf.Max(1f, glow * 1.25f);
            var fill = new Color32[colors.Length];
            for (int i = 0; i < fill.Length; i++)
            {
                Color pigment = colors[i];
                float shadow = 1f - Mathf.SmoothStep(.35f, .80f, Mathf.Max(pigment.r, pigment.g, pigment.b));
                Color color = pigment.linear;
                Color accent = lights != null && i < lights.Length ? ((Color)lights[i]).linear : Color.black;
                color.r = Mathf.Max(Mathf.Max(.008f, color.r * .16f) * shadow / power, accent.r);
                color.g = Mathf.Max(Mathf.Max(.008f, color.g * .16f) * shadow / power, accent.g);
                color.b = Mathf.Max(Mathf.Max(.008f, color.b * .16f) * shadow / power, accent.b);
                color.a = 1f;
                fill[i] = color.gamma;
            }
            var light = new Texture2D(palette.width, palette.height, TextureFormat.RGBA32, false)
                { name = material.name + "ReadabilityFill", filterMode = palette.filterMode, wrapMode = palette.wrapMode };
            light.SetPixels32(fill); light.Apply(false, false);
            material.SetTexture("_EmTex", light);
            if (material.HasProperty("_EmColor")) material.SetColor("_EmColor", Color.white);
            if (material.HasProperty("_EmPower")) material.SetFloat("_EmPower", power);
        }

        internal static Material Surface(string name, Texture palette, float smoothness, Texture emission = null, float glow = 0f)
        {
            var material = new Material(TextureShader) { name = name, mainTexture = palette };
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
            if (material.HasProperty("_NormalStrength")) material.SetFloat("_NormalStrength", 0f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_SpecularStrength")) material.SetFloat("_SpecularStrength", .3f);
            if (emission && material.HasProperty("_EmTex"))
            {
                material.SetTexture("_EmTex", emission);
                if (material.HasProperty("_EmColor")) material.SetColor("_EmColor", Color.white);
                if (material.HasProperty("_EmPower")) material.SetFloat("_EmPower", glow);
            }
            ApplyReadability(material, emission, glow);
            return material;
        }

        // Smooth coincident vertices without welding palette UV seams or hard edges.
        internal static void SmoothNormals(Mesh mesh, float angle = 48f)
        {
            mesh.RecalculateNormals();
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var result = new Vector3[normals.Length];
            var groups = new Dictionary<Vector3Int, List<int>>();
            for (int i = 0; i < vertices.Length; i++)
            {
                var point = vertices[i] * 100000f;
                var key = new Vector3Int(Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.y), Mathf.RoundToInt(point.z));
                if (!groups.TryGetValue(key, out var entries)) groups.Add(key, entries = new List<int>());
                entries.Add(i);
            }
            float threshold = Mathf.Cos(angle * Mathf.Deg2Rad);
            foreach (var group in groups.Values)
                foreach (int i in group)
                {
                    var sum = Vector3.zero;
                    foreach (int j in group)
                        if (Vector3.Dot(normals[i], normals[j]) >= threshold) sum += normals[j];
                    result[i] = sum.sqrMagnitude > .000001f ? sum.normalized : normals[i];
                }
            mesh.normals = result;
        }

        internal static Material Stroke(string name) => SoftRadiance(name, true);

        private static Texture2D ribbonMask, moteMask;
        internal static Material SoftRadiance(string name, bool ribbon)
        {
            var material = Radiance(name);
            var texture = ribbon ? ribbonMask : moteMask;
            if (!texture)
            {
                const int size = 64;
                texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                    { name = ribbon ? "SoftEnergyRibbon" : "SoftEnergyMote",
                      wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float across = Mathf.Abs((y + .5f) / size * 2f - 1f);
                        float along = Mathf.Abs((x + .5f) / size * 2f - 1f);
                        float d = ribbon ? across : Mathf.Sqrt(across * across + along * along);
                        float edge = 1f - Mathf.SmoothStep(.05f, 1f, d);
                        pixels[y * size + x] = new Color(1f, 1f, 1f, edge * edge);
                    }
                texture.SetPixels(pixels); texture.Apply(false, true);
                if (ribbon) ribbonMask = texture; else moteMask = texture;
            }
            material.SetTexture("_MainTex", texture);
            return material;
        }

        // The vertex-only shader used for opaque accents does not guarantee alpha
        // blending. Cloud Remap is the game's native additive particle shader.
        internal static Material Radiance(string name)
        {
            if (!radianceShader) radianceShader = Addressables.LoadAssetAsync<Shader>(
                "RoR2/Base/Shaders/HGCloudRemap.shader").WaitForCompletion();
            var material = new Material(radianceShader) {name=name, renderQueue=(int)RenderQueue.Transparent};
            foreach (var texture in new[] {"_MainTex","_Cloud1Tex","_Cloud2Tex"})
                if (material.HasProperty(texture)) material.SetTexture(texture,Texture2D.whiteTexture);
            if (material.HasProperty("_TintColor")) material.SetColor("_TintColor",Color.white);
            if (material.HasProperty("_AlphaBoost")) material.SetFloat("_AlphaBoost",1f);
            if (material.HasProperty("_AlphaBias")) material.SetFloat("_AlphaBias",0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend",(float)BlendMode.One);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite",0f);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull",(float)CullMode.Off);
            material.EnableKeyword("DISABLEREMAP"); material.EnableKeyword("VERTEXCOLOR");
            return material;
        }

        internal static Material PaletteSurface(Mesh mesh, string name, float smoothness)
        {
            var swatches = new List<Color32>();
            var uv = new Vector2[mesh.vertexCount];
            var colors = mesh.colors32;
            for (int i = 0; i < colors.Length; i++)
            {
                int index = swatches.IndexOf(colors[i]);
                if (index < 0) { index = swatches.Count; swatches.Add(colors[i]); }
                uv[i] = new Vector2(index, .5f);
            }
            for (int i = 0; i < uv.Length; i++) uv[i].x = (uv[i].x + .5f) / swatches.Count;
            mesh.uv = uv;
            return Surface(name, FriendSkin.Palette(name + "Palette", swatches.ToArray()), smoothness);
        }

        internal static void Initialize()
        {
            if (!VertexShader || !TextureShader) throw new System.InvalidOperationException("Combat shaders failed to load.");
        }

        internal static GameObject EffectPrefab(string name)
        {
            // The bool overload of CreateEmptyPrefab in R2API.Prefab 1.1.1
            // registers a NetworkIdentity even when false is passed.
            var source = new GameObject(name);
            source.SetActive(false);
            var prefab = PrefabAPI.InstantiateClone(source, name, false);
            Object.Destroy(source);
            prefab.SetActive(true);
            return prefab;
        }

        internal static void Trail(Transform parent, Color color, float width, float lifetime)
        {
            if (!trailMaterial) trailMaterial = Stroke("CombatSwordTrail");
            var trail = parent.gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = trailMaterial;
            trail.time = lifetime;
            trail.minVertexDistance = .08f;
            trail.widthMultiplier = width;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f,.72f), new Keyframe(.12f,1f), new Keyframe(1f,0f));
            trail.startColor = color;
            trail.endColor = new Color(color.r, color.g, color.b, 0f);
            trail.numCapVertices = 4;
            trail.numCornerVertices = 4;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
        }
    }
}

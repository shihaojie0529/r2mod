using System;
using R2API;
using RoR2;
using RoR2.ContentManagement;
using UnityEngine.AddressableAssets;
using UnityEngine;
using UnityEngine.Rendering;

namespace RiftSurvivor
{
    internal static class SurvivorDisplay
    {
        private static readonly System.Collections.Generic.Dictionary<Material, Material> displayMaterials =
            new System.Collections.Generic.Dictionary<Material, Material>();

        private static readonly System.Collections.Generic.Dictionary<Material, Material> sourceMaterials =
            new System.Collections.Generic.Dictionary<Material, Material>();
        internal static Material SourceMaterial(Material material) =>
            material && sourceMaterials.TryGetValue(material, out var source) ? source : material;

        internal static Material DisplayMaterial(Material source, CharacterBody body)
        {
            if (body || !source) return source;
            if (displayMaterials.TryGetValue(source, out var cached)) return cached;
            var palette = source.mainTexture as Texture2D;
            if (!palette || !palette.isReadable || !source.HasProperty("_EmTex")) return source;
            var material = new Material(source) { name = source.name + "LobbyFill" };
            float power = source.HasProperty("_EmPower") ? Mathf.Max(1f, source.GetFloat("_EmPower")) : 1f;
            var pigment = palette.GetPixels32();
            var oldTexture = source.GetTexture("_EmTex") as Texture2D;
            var previous = oldTexture && oldTexture.isReadable ? oldTexture.GetPixels32() : null;
            var pixels = new Color32[pigment.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                Color color = ((Color)pigment[i]).linear;
                Color accent = previous != null && i < previous.Length ? ((Color)previous[i]).linear : Color.black;
                color.r = Mathf.Max(accent.r, Mathf.Max(.012f, color.r * .34f) / power);
                color.g = Mathf.Max(accent.g, Mathf.Max(.012f, color.g * .34f) / power);
                color.b = Mathf.Max(accent.b, Mathf.Max(.012f, color.b * .34f) / power);
                color.a = 1f; pixels[i] = color.gamma;
            }
            var fill = new Texture2D(palette.width, palette.height, TextureFormat.RGBA32, false)
                { name = material.name + "NeutralFill", filterMode = palette.filterMode, wrapMode = palette.wrapMode };
            fill.SetPixels32(pixels); fill.Apply(false, false);
            material.SetTexture("_EmTex", fill);
            if (material.HasProperty("_EmColor")) material.SetColor("_EmColor", Color.white);
            if (material.HasProperty("_EmPower")) material.SetFloat("_EmPower", power);
            sourceMaterials.Add(material, source);
            displayMaterials.Add(source, material);
            displayMaterials.Add(material, material);
            return material;
        }

        // Display geometry and poses come from the same authored assets as the
        // survivor. No Commando display, skeleton, animator or skin is inherited.
        internal static GameObject Create(string name, Action<GameObject> install)
        {
            var source = new GameObject(name);
            source.SetActive(false);
            var display = PrefabAPI.InstantiateClone(source, name, false);
            UnityEngine.Object.DestroyImmediate(source);
            display.SetActive(true); // PrefabAPI keeps it under its inactive holder.
            var geometry = new GameObject(name + "Mesh");
            geometry.transform.SetParent(display.transform, false);
            var renderer = geometry.AddComponent<SkinnedMeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.updateWhenOffscreen = true;
            var model = display.AddComponent<CharacterModel>();
            model.mainSkinnedMeshRenderer = renderer;
            model.baseRendererInfos = new[] { new CharacterModel.RendererInfo {
                renderer = renderer, defaultShadowCastingMode = ShadowCastingMode.On
            } };
            install(display);
            return display;
        }
    }

    // The gameplay body retains its native hitboxes, animator events and item
    // anchors. Suppress the borrowed appearance before any instance can render.
    [DisallowMultipleComponent, DefaultExecutionOrder(31050)]
    public sealed class OriginalModelGuard : MonoBehaviour
    {
        [SerializeField] private Renderer[] borrowedGear = Array.Empty<Renderer>();
        private bool revealed;
        private static RuntimeAnimatorController gameplayAnimator;
        private static Avatar gameplayAvatar;

        private static T LoadAnimatorAsset<T>(AssetOrDirectReference<T> reference) where T : UnityEngine.Object
        {
            if (reference.directRef) return reference.directRef;
            return reference.address != null && reference.address.RuntimeKeyIsValid()
                ? Addressables.LoadAssetAsync<T>(reference.address.RuntimeKey).WaitForCompletion() : null;
        }

        internal static void Prepare(GameObject prefab)
        {
            foreach (var controller in prefab.GetComponentsInChildren<ModelSkinController>(true))
            {
                // RoR2 1.5's skin component also loads the gameplay animator.
                // Keep those assets explicitly when removing its skin overrides.
                var animator = controller.GetComponent<Animator>();
                if (animator)
                {
                    if (!animator.runtimeAnimatorController)
                    {
                        if (!gameplayAnimator) gameplayAnimator = LoadAnimatorAsset(controller.animatorControllerRef);
                        animator.runtimeAnimatorController = gameplayAnimator;
                    }
                    if (!animator.avatar)
                    {
                        if (!gameplayAvatar) gameplayAvatar = LoadAnimatorAsset(controller.avatarRef);
                        animator.avatar = gameplayAvatar;
                    }
                }
                UnityEngine.Object.DestroyImmediate(controller);
            }
            var model = prefab.GetComponentInChildren<CharacterModel>(true);
            var root = model ? model.gameObject : prefab;
            var guard = root.GetComponent<OriginalModelGuard>();
            if (!guard) guard = root.AddComponent<OriginalModelGuard>();
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            guard.borrowedGear = Array.FindAll(renderers, renderer => {
                if (renderer.GetComponentInParent<ItemDisplay>()) return false;
                var name = renderer.name.ToLowerInvariant();
                return name.Contains("gunmesh") || name.Contains("pistol") || name.Contains("jetpack");
            });
            guard.HideGear();
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                renderer.forceRenderingOff = true;
        }

        internal static void Reveal(SkinnedMeshRenderer renderer)
        {
            // Called only after the authored mesh, material and bones are bound.
            var guard = renderer.GetComponentInParent<OriginalModelGuard>();
            if (!guard || guard.revealed) return;
            guard.revealed = true;
            renderer.forceRenderingOff = false;
        }

        private void Awake() => HideGear();
        private void LateUpdate() => HideGear();
        private void HideGear()
        {
            foreach (var renderer in borrowedGear)
                if (renderer) { renderer.enabled = false; renderer.forceRenderingOff = true; }
        }
    }
}

using System;
using System.Collections;
using System.IO;
using System.Reflection;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;

internal static class LobbyPreview
{
    internal static IEnumerator Capture(Assembly mod, string output, Action<string, Action> check)
    {
        var previous = SceneManager.GetActiveScene();
        var definition = SceneCatalog.FindSceneDef("lobby");
        object sceneKey = definition && definition.sceneAddress != null ? definition.sceneAddress.AssetGUID : "Assets/RoR2/Base/Scenes/lobby/lobby.unity";
        var handle = Addressables.LoadSceneAsync(sceneKey, LoadSceneMode.Additive);
        float deadline = Time.realtimeSinceStartup + 90f;
        while (handle.IsValid() && !handle.IsDone && Time.realtimeSinceStartup < deadline) yield return null;
        if (!handle.IsValid() || handle.Status != UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
        {
            check("Actual lobby lighting preview", () => { throw new Exception("Lobby scene load failed: valid=" + handle.IsValid() + (handle.IsValid() ? " status=" + handle.Status + " error=" + handle.OperationException : "")); });
            yield break;
        }
        SceneManager.SetActiveScene(handle.Result.Scene);
        yield return null;
        check("Actual lobby lighting preview", () => Render(mod, output, handle.Result.Scene));
        if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        yield return Addressables.UnloadSceneAsync(handle);
    }

    private static void Render(Assembly mod, string output, Scene scene)
    {
        const int width = 512, height = 640;
        const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var sourceMaterial = mod.GetType("RiftSurvivor.SurvivorDisplay").GetMethod("SourceMaterial", flags);
        // Native PreGameController instantiates the mode's background separately
        // from the lobby scene. Include it so its red room lights are actually present.
        var mode = GameModeCatalog.GetGameModePrefabComponent(0);
        var backgroundPrefab = mode.lobbyBackgroundPrefab ? mode.lobbyBackgroundPrefab :
            Addressables.LoadAssetAsync<GameObject>(mode.lobbyBackgroundPrefabRef.RuntimeKey).WaitForCompletion();
        var background = UnityEngine.Object.Instantiate(backgroundPrefab);
        var slots = UnityEngine.Object.FindObjectsOfType<RoR2.SurvivorMannequins.SurvivorMannequinSlotController>();
        var slot = Array.Find(slots, s => s.gameObject.scene == scene);
        if (!slot) throw new Exception("Actual lobby mannequin slot not found");
        var cameraObject = new GameObject("LobbyReadabilityCamera");
        var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
        camera.clearFlags = CameraClearFlags.Skybox; camera.renderingPath = RenderingPath.DeferredShading;
        camera.fieldOfView = 35f; camera.nearClipPlane = .05f; camera.farClipPlane = 1000f;
        camera.allowHDR = true;
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        var atlas = new Texture2D(width * 4, height * 2, TextureFormat.RGB24, false);
        var report = new System.Text.StringBuilder();
        report.AppendLine("Actual lobby scene: " + scene.path);
        report.AppendLine("Native game-mode background: " + backgroundPrefab.name);
        report.AppendLine("Top: original material; bottom: lobby fill. Same pose/camera/scene lights. No UI/postprocessing.");
        int lights = 0;
        foreach (var light in UnityEngine.Object.FindObjectsOfType<Light>())
            if (light.gameObject.scene == scene) { lights++; report.AppendLine(light.name + " " + light.type + " intensity=" + light.intensity); }
        report.AppendLine("Lobby scene lights=" + lights);
        if (lights == 0) throw new Exception("Lobby background lighting is missing");
        GameObject display = null;
        var oldTarget = RenderTexture.active;
        try {
            camera.targetTexture = target;
            var ids = new[] { "RiftRunner", "Xiaohei", "JieGe", "WeiGe" };
            for (int i = 0; i < ids.Length; i++) {
                var def = Array.Find(Resources.FindObjectsOfTypeAll<SurvivorDef>(), d => d.cachedName == ids[i]);
                display = UnityEngine.Object.Instantiate(def.displayPrefab, slot.transform.position, slot.transform.rotation);
                var renderer = display.GetComponentInChildren<SkinnedMeshRenderer>();
                var model = display.GetComponent<CharacterModel>();
                var improved = renderer.sharedMaterial;
                var original = (Material)sourceMaterial.Invoke(null, new object[] {improved});
                // Bake the authored mesh to frame its visible silhouette, rather than broad gameplay bounds.
                var baked = new Mesh(); renderer.BakeMesh(baked);
                var center = renderer.transform.TransformPoint(baked.bounds.center);
                float size = Mathf.Max(.5f, baked.bounds.size.y * renderer.transform.lossyScale.y);
                UnityEngine.Object.DestroyImmediate(baked);
                var direction = slot.transform.forward;
                camera.transform.position = center + direction * (size * 2.0f) + Vector3.up * (size * .08f);
                camera.transform.LookAt(center);
                for (int row = 0; row < 2; row++) {
                    var material = row == 0 ? original : improved;
                    renderer.sharedMaterial = material;
                    if (model && model.baseRendererInfos.Length > 0) model.baseRendererInfos[0].defaultMaterial = material;
                    camera.Render(); RenderTexture.active = target;
                    var tile = new Texture2D(width, height, TextureFormat.RGB24, false);
                    tile.ReadPixels(new Rect(0, 0, width, height), 0, 0); tile.Apply();
                    atlas.SetPixels(i * width, (1 - row) * height, width, height, tile.GetPixels());
                    UnityEngine.Object.DestroyImmediate(tile);
                }
                UnityEngine.Object.DestroyImmediate(display); display = null;
            }
            atlas.Apply(); Directory.CreateDirectory(System.IO.Path.GetDirectoryName(output));
            File.WriteAllBytes(output, atlas.EncodeToPNG()); File.WriteAllText(output + ".txt", report.ToString());
        } finally {
            RenderTexture.active = oldTarget; camera.targetTexture = null;
            if (display) UnityEngine.Object.DestroyImmediate(display);
            UnityEngine.Object.DestroyImmediate(background);
            target.Release(); UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(atlas); UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }
}

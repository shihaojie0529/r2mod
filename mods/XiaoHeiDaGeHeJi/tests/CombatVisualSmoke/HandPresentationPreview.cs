using System;
using System.IO;
using System.Reflection;
using System.Text;
using RoR2;
using UnityEngine;
using UnityEngine.Rendering;

internal static class HandPresentationPreview
{
    private const BindingFlags Flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    // Columns: idle, left jab, right hook, right uppercut. Top row: both real
    // fists close up. Bottom row: upper-body view at a fixed, wider distance.
    // Both rows retain one common scale across all four poses for comparison.
    internal static void Capture(Assembly mod, string output)
    {
        Type T(string name) => mod.GetType("RiftSurvivor." + name, true);
        const int width = 640, height = 480, layer = 30;
        var names = new[] {"idle", "jab", "cross", "uppercut"};
        var times = new[] {.30f, .28125f, .28125f, .33333f};
        var clipSeconds = new[] {2.4f, .32f, .32f, .48f};
        GameObject display = null, cameraObject = null, keyObject = null, fillObject = null;
        Camera camera = null;
        RenderTexture supersampled = null, resolved = null;
        Texture2D atlas = null, tile = null;
        Mesh baked = null;
        var previousTarget = RenderTexture.active;
        var previousAmbient = RenderSettings.ambientLight;
        var previousAmbientMode = RenderSettings.ambientMode;
        var sceneLights = UnityEngine.Object.FindObjectsOfType<Light>();
        var sceneLightMasks = Array.ConvertAll(sceneLights, light => light.cullingMask);
        try
        {
            // Other scene lights must not add a second, uncontrolled exposure to
            // this private studio layer. Restore their exact masks on exit.
            foreach (var light in sceneLights) if (light) light.cullingMask &= ~(1 << layer);
            var def = Array.Find(Resources.FindObjectsOfTypeAll<SurvivorDef>(), d => d.cachedName == "RiftRunner");
            if (!def || !def.displayPrefab) throw new Exception("Cui display prefab missing");
            display = UnityEngine.Object.Instantiate(def.displayPrefab);
            // A private, layer-isolated fixture is sampled in one frame. It has
            // no CharacterBody, server, player input, stage state or damage.
            display.transform.position = new Vector3(0f, 1000f, 0f);
            if (display.GetComponentInChildren<CharacterBody>(true)) throw new Exception("Preview requires a bodyless display");
            var model = display.GetComponentInChildren<CharacterModel>(true);
            var skin = display.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var motion = display.GetComponentInChildren(T("CuiMotion"), true);
            var glow = display.GetComponentInChildren(T("CuiFistGlow"), true);
            if (!model || !skin || !skin.sharedMesh || !motion || !glow) throw new Exception("Cui display did not bind its rig and hand effects");
            var rig = skin.rootBone;
            if (!rig) throw new Exception("Cui authored rig missing");
            float scale = Mathf.Max(.01f, Mathf.Abs(rig.lossyScale.y));
            model.enabled = false;
            ((Behaviour)motion).enabled = false;
            ((Behaviour)glow).enabled = false;
            foreach (string type in new[] {"SurvivorModel", "CuiGear"})
            {
                var controller = display.GetComponentInChildren(T(type), true) as Behaviour;
                if (controller) controller.enabled = false;
            }
            foreach (var child in display.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer;
            foreach (var view in display.GetComponentsInChildren<Renderer>(true))
            { view.enabled = true; view.forceRenderingOff = false; }
            var sourceMaterial = (Material)T("SurvivorDisplay").GetMethod("SourceMaterial", Flags)
                .Invoke(null, new object[] {skin.sharedMaterial});
            if (!sourceMaterial) throw new Exception("Combat source material is missing");
            // Camera.Render does not advance Unity's skinning update between
            // synchronous samples. Render a CPU-baked pose instead of reusing
            // the skinning buffer from the first pose of this engine frame.
            var snapshotObject = new GameObject("CuiCurrentPoseSnapshot");
            snapshotObject.layer = layer;
            snapshotObject.transform.SetParent(skin.transform, false);
            var snapshotFilter = snapshotObject.AddComponent<MeshFilter>();
            var snapshotRenderer = snapshotObject.AddComponent<MeshRenderer>();
            snapshotRenderer.sharedMaterial = sourceMaterial;
            snapshotRenderer.shadowCastingMode = ShadowCastingMode.On;
            snapshotRenderer.receiveShadows = true;
            snapshotRenderer.lightProbeUsage = LightProbeUsage.Off;
            snapshotRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            skin.enabled = false;
            var nodes = display.GetComponentsInChildren<Transform>(true);
            Transform Node(string name) => Array.Find(nodes, node => node.name == name);
            var surfaces = new[] {Node("LeftHandGoldenSkin")?.GetComponent<MeshRenderer>(), Node("RightHandGoldenSkin")?.GetComponent<MeshRenderer>()};
            var trails = new[] {Node("LeftHandStrikeTrail")?.GetComponent<TrailRenderer>(), Node("RightHandStrikeTrail")?.GetComponent<TrailRenderer>()};
            if (Array.Exists(surfaces, view => !view) || Array.Exists(trails, trail => !trail))
                throw new Exception("Cui fitted hand surfaces or strike trails missing");

            var motionType = T("CuiMotion");
            var play = motionType.GetMethod("PlayCue", Flags);
            var actionField = motionType.GetField("action", Flags);
            var shownField = motionType.GetField("shown", Flags);
            var tick = motionType.GetMethod("Tick", Flags);
            var glowUpdate = T("CuiFistGlow").GetMethod("LateUpdate", Flags);
            if (play == null || actionField == null || shownField == null || tick == null || glowUpdate == null)
                throw new Exception("Cui authored pose sampler missing");
            void Pose(string name, float normalized)
            {
                play.Invoke(motion, new object[] {name, 1f});
                var action = actionField.GetValue(motion);
                action.GetType().GetField("start", Flags).SetValue(action, Time.unscaledTime - normalized);
                actionField.SetValue(motion, action);
                // A capture is an exact authored pose, without a transition
                // blended from whichever clip happened to be sampled before it.
                shownField.SetValue(motion, null);
                tick.Invoke(motion, new object[] {0f});
                glowUpdate.Invoke(glow, null);
            }
            void Sample(int column)
            {
                foreach (var trail in trails) {trail.emitting = false; trail.Clear();}
                if (column == 0)
                {
                    Pose(names[column], times[column]);
                    skin.BakeMesh(baked, false); baked.RecalculateBounds();
                    return;
                }
                // Reconstruct only the last lifetime of the real strike path.
                // No time advances during a synchronous Camera.Render capture,
                // so explicitly sample the authored hand at each trail point.
                float start = Mathf.Max(.151f, times[column] - trails[column == 1 ? 0 : 1].time / clipSeconds[column]);
                const int steps = 12;
                for (int i = 0; i <= steps; ++i)
                {
                    float t = Mathf.Lerp(start, times[column], i / (float)steps);
                    Pose(names[column], t);
                    foreach (var trail in trails)
                    {
                        if (!trail.emitting) continue;
                        var point = trail.transform.position;
                        trail.AddPosition(point);
                    }
                }
                skin.BakeMesh(baked, false); baked.RecalculateBounds();
            }

            cameraObject = new GameObject("CuiHandPresentationCamera");
            camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false; camera.orthographic = true; camera.allowHDR = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.035f, .045f, .060f);
            camera.cullingMask = 1 << layer;
            camera.renderingPath = RenderingPath.DeferredShading;
            camera.nearClipPlane = .01f; camera.farClipPlane = 100f;
            var direction = (rig.forward * 2f + rig.right * .72f + rig.up * .22f).normalized;
            var cameraRotation = Quaternion.LookRotation(-direction, rig.up);
            camera.transform.rotation = cameraRotation;
            camera.aspect = width / (float)height;
            keyObject = new GameObject("CuiHandPresentationKey");
            var key = keyObject.AddComponent<Light>(); key.type = LightType.Directional;
            key.intensity = .55f; key.color = new Color(1f,.92f,.82f); key.cullingMask = 1 << layer;
            key.transform.rotation = Quaternion.LookRotation(-rig.forward + rig.right * .55f - rig.up * .8f, rig.up);
            fillObject = new GameObject("CuiHandPresentationFill");
            var fill = fillObject.AddComponent<Light>(); fill.type = LightType.Directional;
            fill.intensity = .12f; fill.color = new Color(.55f,.73f,1f); fill.cullingMask = 1 << layer;
            fill.transform.rotation = Quaternion.LookRotation(rig.forward - rig.right - rig.up * .45f, rig.up);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.055f,.065f,.08f);
            supersampled = new RenderTexture(width * 2, height * 2, 24, RenderTextureFormat.ARGB32);
            supersampled.antiAliasing = 1; supersampled.filterMode = FilterMode.Bilinear;
            resolved = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            camera.targetTexture = supersampled;
            atlas = new Texture2D(width * 4, height * 2, TextureFormat.RGB24, false);
            tile = new Texture2D(width, height, TextureFormat.RGB24, false);
            baked = new Mesh {name = "CuiHandPreviewCurrentPose"};
            snapshotFilter.sharedMesh = baked;
            var weights = skin.sharedMesh.boneWeights;
            var sourceVertices = skin.sharedMesh.vertices;
            var bindposes = skin.sharedMesh.bindposes;
            string ValidateHandAlignment()
            {
                var vertices = baked.vertices;
                if (vertices.Length != sourceVertices.Length) throw new Exception("Baked skin lost vertices");
                var offsets = new float[2];
                for (int side = 0; side < 2; ++side)
                {
                    var joint = surfaces[side].transform.parent;
                    int bone = Array.FindIndex(skin.bones, transform => transform == joint);
                    if (bone < 0) throw new Exception("Hand glow joint is absent from the skin");
                    var expected = Vector3.zero; var actual = Vector3.zero; int count = 0;
                    for (int v = 0; v < vertices.Length; ++v)
                    {
                        if (weights[v].boneIndex0 != bone || weights[v].weight0 < .999f) continue;
                        expected += joint.TransformPoint(bindposes[bone].MultiplyPoint3x4(sourceVertices[v]));
                        actual += snapshotObject.transform.TransformPoint(vertices[v]);
                        count++;
                    }
                    if (count == 0) throw new Exception("Baked hand has no source vertices");
                    offsets[side] = Vector3.Distance(expected / count, actual / count);
                    if (offsets[side] > .01f * scale)
                        throw new Exception("Baked skin disagrees with the animated " + joint.name + " by " + offsets[side]);
                }
                return " baked-to-bone hand offsets L=" + offsets[0] + " R=" + offsets[1];
            }
            var closeBounds = new Bounds[4];
            var upperBounds = new Bounds[4];
            float closeExtent = .17f * scale, upperExtent = .55f * scale;
            float Extent(Bounds bounds)
            {
                float extent = 0f;
                var inverse = Quaternion.Inverse(cameraRotation);
                foreach (int x in new[] {-1,1}) foreach (int y in new[] {-1,1}) foreach (int z in new[] {-1,1})
                {
                    var corner = inverse * Vector3.Scale(bounds.extents, new Vector3(x,y,z));
                    extent = Mathf.Max(extent, Mathf.Abs(corner.y), Mathf.Abs(corner.x) / camera.aspect);
                }
                return extent;
            }
            for (int column = 0; column < names.Length; ++column)
            {
                Sample(column);
                var close = surfaces[0].bounds; close.Encapsulate(surfaces[1].bounds);
                // Detail framing prioritizes the two current fists; the short
                // history can naturally leave frame instead of shrinking them.
                close.Expand(.14f * scale); // Include the actual soft glow around the skin.
                closeBounds[column] = close;
                closeExtent = Mathf.Max(closeExtent, Extent(close) * 1.14f);
                ValidateHandAlignment();
                var vertices = baked.vertices;
                bool started = false;
                var upper = close;
                for (int v = 0; v < vertices.Length; ++v)
                {
                    // Authored bones 0..8 are hips, chest, head and both arms.
                    // Excluding leg vertices gives a true upper-body framing.
                    if (weights[v].boneIndex0 > 8) continue;
                    var point = skin.transform.TransformPoint(vertices[v]);
                    if (!started) {upper = new Bounds(point, Vector3.zero); started = true;} else upper.Encapsulate(point);
                }
                if (!started) throw new Exception("Upper-body pose bounds are empty");
                upper.Encapsulate(close); upperBounds[column] = upper;
                upperExtent = Mathf.Max(upperExtent, Extent(upper) * 1.20f);
            }
            // The normal-distance row has deliberately more breathing room than
            // the detail row, while preserving the exact same shader intensities.
            upperExtent = Mathf.Max(upperExtent, closeExtent * 1.45f);
            var report = new StringBuilder();
            report.AppendLine("Columns: idle, left jab, right hook (cross), right uppercut.");
            report.AppendLine("Top: both fists close up. Bottom: upper-body distance. Fixed scale within each row.");
            report.AppendLine("Real Cui skin CPU-baked at each authored pose; actual hand bones and glow component. Zero pump; no network body.");
            report.AppendLine("Combat source material: " + sourceMaterial.name + ". No character-select emissive fill.");
            report.AppendLine("Baked hand vertices are compared with the exact animated hand-bone skinning transform; offsets must stay below 1cm at unit scale.");
            report.AppendLine("Short strike trails sample the actual authored hand trajectory within the renderer lifetime.");
            report.AppendLine("2x supersampled Unity GPU capture. Key=.55, fill=.12; scene lights excluded. No bloom or postprocessing.");
            report.AppendLine("Orthographic half-heights: detail=" + closeExtent + ", upper-body=" + upperExtent + ".");
            for (int column = 0; column < names.Length; ++column)
            {
                Sample(column);
                report.AppendLine(names[column] + " normalized pose=" + times[column] + " left trail=" + trails[0].emitting + " right trail=" + trails[1].emitting + ValidateHandAlignment());
                for (int row = 0; row < 2; ++row)
                {
                    var center = row == 0 ? closeBounds[column].center : upperBounds[column].center;
                    camera.orthographicSize = row == 0 ? closeExtent : upperExtent;
                    camera.transform.SetPositionAndRotation(center + direction * (8f * scale), cameraRotation);
                    camera.Render(); Graphics.Blit(supersampled, resolved); RenderTexture.active = resolved;
                    tile.ReadPixels(new Rect(0,0,width,height),0,0); tile.Apply(false,false);
                    atlas.SetPixels(column * width, (1 - row) * height, width, height, tile.GetPixels());
                }
            }
            atlas.Apply(false, false);
            string folder = System.IO.Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            File.WriteAllBytes(output, atlas.EncodeToPNG());
            File.WriteAllText(output + ".txt", report.ToString());
        }
        finally
        {
            RenderTexture.active = previousTarget;
            RenderSettings.ambientLight = previousAmbient; RenderSettings.ambientMode = previousAmbientMode;
            for (int i = 0; i < sceneLights.Length; ++i) if (sceneLights[i]) sceneLights[i].cullingMask = sceneLightMasks[i];
            if (camera) camera.targetTexture = null;
            if (display) UnityEngine.Object.DestroyImmediate(display);
            if (baked) UnityEngine.Object.DestroyImmediate(baked);
            if (tile) UnityEngine.Object.DestroyImmediate(tile);
            if (atlas) UnityEngine.Object.DestroyImmediate(atlas);
            if (supersampled) {supersampled.Release(); UnityEngine.Object.DestroyImmediate(supersampled);}
            if (resolved) {resolved.Release(); UnityEngine.Object.DestroyImmediate(resolved);}
            if (cameraObject) UnityEngine.Object.DestroyImmediate(cameraObject);
            if (keyObject) UnityEngine.Object.DestroyImmediate(keyObject);
            if (fillObject) UnityEngine.Object.DestroyImmediate(fillObject);
        }
    }
}

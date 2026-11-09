using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

internal static class ArtPreview
{
    private const BindingFlags Flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    internal static void Capture(Assembly mod, string output, bool characters = false, bool spectacle = false)
    {
        Type T(string name) => mod.GetType("RiftSurvivor." + name, true);
        object Call(string type, string method, params object[] args) => T(type).GetMethod(method, Flags).Invoke(null, args);
        const int size = 512, layer = 30;
        int columns = characters ? 4 : 3, rows = characters ? 1 : 3;
        var atlas = new Texture2D(size * columns, size * rows, TextureFormat.RGB24, false);
        var cameraObject = new GameObject("WeaponArtCamera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.055f,.075f,.10f);
        camera.cullingMask = 1 << layer;
        camera.renderingPath = RenderingPath.DeferredShading;
        camera.nearClipPlane = .01f;
        camera.farClipPlane = 50f;
        var target = new RenderTexture(size,size,24,RenderTextureFormat.ARGB32);
        target.antiAliasing = 1;
        var supersampled = new RenderTexture(size*2,size*2,24,RenderTextureFormat.ARGB32);
        supersampled.filterMode = FilterMode.Bilinear;
        camera.targetTexture = supersampled;
        var keyObject = new GameObject("ArtKey");
        var key = keyObject.AddComponent<Light>();
        key.type = LightType.Directional; key.intensity = 1.25f; key.color = new Color(1f,.92f,.82f); key.cullingMask = 1 << layer;
        key.transform.rotation = Quaternion.Euler(42,-35,0);
        var fillObject = new GameObject("ArtFill");
        var fill = fillObject.AddComponent<Light>();
        fill.type = LightType.Directional; fill.intensity = .65f; fill.color = new Color(.55f,.76f,1f); fill.cullingMask = 1 << layer;
        fill.transform.rotation = Quaternion.Euler(20,140,0);
        var oldActive = RenderTexture.active;
        var oldAmbient = RenderSettings.ambientLight;
        var oldMode = RenderSettings.ambientMode;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.28f,.30f,.34f);
        var shader = T("CombatVisuals").GetProperty("TextureShader",Flags).GetValue(null);
        Call("CuiGear","LoadProps",shader);
        var materialReport = new System.Text.StringBuilder();
        for (int index = 0; index < (characters ? 4 : 9); index++)
        {
            var root = new GameObject("PreviewProp");
            if (spectacle)
            {
                if(index==0) {Call("GoldenFist","Attach",root.transform);root.transform.rotation=Quaternion.Euler(-15,155,-12);}
                else if(index==1)
                {
                    var avatar=root.AddComponent(T("JieCelestialAvatar"));
                    T("JieCelestialAvatar").GetMethod("Start",Flags).Invoke(avatar,null);
                    T("JieCelestialAvatar").GetField("started",Flags).SetValue(avatar,Time.time-.5f);
                    T("JieCelestialAvatar").GetMethod("LateUpdate",Flags).Invoke(avatar,null);
                }
                else if(index<8) {Call("ExtendedFurnitureArt","Attach",root.transform,Enum.ToObject(T("FurnitureKind"),index-2));root.transform.rotation=Quaternion.Euler(0,180,0); }
                else
                {
                    var effect=root.AddComponent<RoR2.EffectComponent>();effect.effectData=new RoR2.EffectData{origin=Vector3.zero,rotation=Quaternion.identity,scale=2f};
                    var burst=root.AddComponent(T("SkillBurst"));T("SkillBurst").GetField("kind",Flags).SetValue(burst,Enum.Parse(T("SpectacleKind"),"Seeker"));
                    T("SkillBurst").GetMethod("Start",Flags).Invoke(burst,null);
                }
            }
            else if (characters)
            {
                var ids = new[] { "RiftRunner", "Xiaohei", "JieGe", "WeiGe" };
                var survivor = Array.Find(Resources.FindObjectsOfTypeAll<RoR2.SurvivorDef>(), def => def.cachedName == ids[index % 4]);
                var display = UnityEngine.Object.Instantiate(survivor.displayPrefab);
                display.transform.SetParent(root.transform, false);
                key.intensity = 1.25f;
                fill.intensity = .65f;
                key.transform.rotation = Quaternion.Euler(42,145,0);
                fill.transform.rotation = Quaternion.Euler(20,-40,0);
                RenderSettings.ambientLight = new Color(.28f,.30f,.34f);
            }
            else
            {
            if (index == 0) Call("JieSwordArt","Attach",root.transform,new Color(.18f,.92f,.81f));
            else if (index == 1) Call("XiaoheiRoundArt","Attach",root.transform,85f);
            else if (index < 4) Call("WeiFurnitureArt","Attach",root.transform,index == 3);
            else if (index < 6)
            {
                var prop = T("CuiGear").GetField(index==4?"plateAsset":"shakerAsset",Flags).GetValue(null);
                root.AddComponent<MeshFilter>().sharedMesh = (Mesh)prop.GetType().GetField("mesh",Flags).GetValue(prop);
                root.AddComponent<MeshRenderer>().sharedMaterial = (Material)prop.GetType().GetField("material",Flags).GetValue(prop);
            }
            else if (index == 6) Call("JieSwordArt","Attach",root.transform,new Color(1f,.71f,.15f));
            else if (index == 7)
            {
                var original = (Mesh)T("XiaoheiModel").GetField("skin", Flags).GetValue(null);
                var material = (Material)T("XiaoheiModel").GetField("cloth", Flags).GetValue(null);
                var source = original.triangles;
                var weights = original.boneWeights;
                var indices = new System.Collections.Generic.List<int>();
                for (int t = 0; t < source.Length; t += 3)
                    if (weights[source[t]].boneIndex0 == 8 && weights[source[t+1]].boneIndex0 == 8 && weights[source[t+2]].boneIndex0 == 8)
                    { indices.Add(source[t]); indices.Add(source[t+1]); indices.Add(source[t+2]); }
                var mesh = new Mesh { name = "PistolDetailPreview", indexFormat = original.indexFormat };
                // Compact the selected triangles so unused body vertices cannot expand bounds.
                var vertices = original.vertices; var normals = original.normals; var uv = original.uv;
                var points = new Vector3[indices.Count]; var directions = new Vector3[indices.Count]; var texels = new Vector2[indices.Count];
                var compact = new int[indices.Count];
                for (int i=0; i<indices.Count; i++) { points[i]=vertices[indices[i]]; directions[i]=normals[indices[i]]; texels[i]=uv[indices[i]]; compact[i]=i; }
                mesh.vertices=points;mesh.normals=directions;mesh.uv=texels;mesh.triangles=compact;mesh.RecalculateBounds();
                root.AddComponent<MeshFilter>().sharedMesh=mesh;
                root.AddComponent<MeshRenderer>().sharedMaterial=material;
            }
            else
            {
                var seal = (GameObject)Call("JieStormEffects","MakeSeal");
                seal.transform.SetParent(root.transform,false);
                root.transform.rotation = Quaternion.LookRotation(new Vector3(1.1f,.7f,-1.7f));
            }
            }
            if (!characters && !spectacle && (index == 0 || index == 6)) root.transform.rotation = Quaternion.Euler(-45,10,-30);
            if (!characters && !spectacle && index == 4) root.transform.rotation = Quaternion.Euler(80,0,0);
            foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer;
            var renderers = Array.FindAll(root.GetComponentsInChildren<Renderer>(), r => (r is MeshRenderer || r is SkinnedMeshRenderer || r is LineRenderer) && r.enabled && !r.forceRenderingOff);
            if (characters)
                foreach (var renderer in renderers)
                {
                    var material = renderer.sharedMaterial;
                    materialReport.AppendLine(root.name + " " + index + " " + renderer.name + " " + material.shader.name + " " + material.mainTexture?.name);
                    foreach (var property in new[] { "_SpecularStrength", "_Smoothness", "_EmPower", "_RampChoice", "_DiffuseBias" })
                        if (material.HasProperty(property)) materialReport.AppendLine(property + "=" + material.GetFloat(property));
                }
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            var direction = (spectacle && (index==1 || index==8) ? new Vector3(.18f,.08f,2f) : characters ? new Vector3(.75f,.35f,2f) : new Vector3(1.1f,.7f,-1.7f)).normalized;
            var center = bounds.center;
            camera.transform.position = center + direction * 8f;
            camera.transform.LookAt(center);
            float extent = 0f;
            foreach (int x in new[]{-1,1}) foreach(int y in new[]{-1,1}) foreach(int z in new[]{-1,1})
            {
                var corner = center + Vector3.Scale(bounds.extents,new Vector3(x,y,z));
                var local = camera.transform.InverseTransformPoint(corner);
                extent = Mathf.Max(extent, Mathf.Abs(local.x), Mathf.Abs(local.y));
            }
            camera.orthographicSize = extent * 1.14f;
            camera.Render();
            Graphics.Blit(supersampled, target);
            var previous = RenderTexture.active; RenderTexture.active = target;
            var tile = new Texture2D(size,size,TextureFormat.RGB24,false);
            tile.ReadPixels(new Rect(0,0,size,size),0,0);tile.Apply();
            atlas.SetPixels((index%columns)*size,(rows-1-index/columns)*size,size,size,tile.GetPixels());
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(tile);
            UnityEngine.Object.DestroyImmediate(root);
        }
        atlas.Apply();
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        File.WriteAllBytes(output,atlas.EncodeToPNG());
        if (characters) File.WriteAllText(output + ".materials.txt", materialReport.ToString());
        RenderSettings.ambientLight = oldAmbient; RenderSettings.ambientMode = oldMode;
        camera.targetTexture=null; RenderTexture.active=oldActive; target.Release(); supersampled.Release();
        UnityEngine.Object.DestroyImmediate(supersampled);
        UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(atlas);
        UnityEngine.Object.DestroyImmediate(cameraObject);UnityEngine.Object.DestroyImmediate(keyObject);UnityEngine.Object.DestroyImmediate(fillObject);
    }
}

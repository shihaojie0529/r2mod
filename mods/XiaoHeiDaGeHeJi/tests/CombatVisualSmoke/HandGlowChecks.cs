using System;
using RoR2;
using UnityEngine;

public partial class Smoke
{
    // Run after the native elite/content catalogs are ready, just like the
    // existing display checks. This fixture needs neither a body nor a server.
    void CheckHandGlow()
    {
        GameObject display = null;
        Component glow = null;
        CharacterModel model = null;
        SkinnedMeshRenderer skin = null;
        var surfaces = new MeshRenderer[2];
        var halos = new MeshRenderer[2];
        var trails = new TrailRenderer[2];
        var views = new Renderer[6];
        var defaults = new Material[6];
        var glowType = T("CuiFistGlow");
        var show = glowType.GetMethod("Show", Flags);
        var update = glowType.GetMethod("LateUpdate", Flags);
        void Pose(string cue, float t)
        {
            if (!glow || show == null || update == null) throw new Exception("Hand glow fixture is unbound");
            show.Invoke(glow, new object[] {cue, t});
            update.Invoke(glow, null);
        }
        Color Light(Renderer view)
        {
            var block = new MaterialPropertyBlock();
            view.GetPropertyBlock(block);
            return block.GetColor(Shader.PropertyToID("_TintColor"));
        }
        void Trails(bool left, bool right)
        {
            if (trails[0].emitting != left || trails[1].emitting != right)
                throw new Exception("Unexpected strike emitters: left=" + trails[0].emitting + " right=" + trails[1].emitting);
        }
        void Registered()
        {
            for (int i = 0; i < views.Length; ++i)
            {
                var entries = Array.FindAll(model.baseRendererInfos, info => info.renderer == views[i]);
                if (entries.Length != 1 || entries[0].defaultMaterial != defaults[i] ||
                    !entries[0].ignoreOverlays || !entries[0].hideOnDeath)
                    throw new Exception("Missing, duplicate or stale native renderer registration: " + views[i].name);
            }
        }
        try
        {
            CheckOne("Cui fists bind to a bodyless authored display", () => {
                var def = Array.Find(Resources.FindObjectsOfTypeAll<SurvivorDef>(), d => d.cachedName == "RiftRunner");
                if (!def || !def.displayPrefab) throw new Exception("Cui display prefab missing");
                display = Instantiate(def.displayPrefab);
                if (display.GetComponentInChildren<CharacterBody>(true)) throw new Exception("Display unexpectedly contains a gameplay body");
                glow = display.GetComponentInChildren(glowType, true);
                model = display.GetComponentInChildren<CharacterModel>(true);
                skin = display.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (!glow || !model || !skin || !skin.sharedMesh) throw new Exception("Display did not synchronously bind both hand effects");
                if (glowType.GetField("body", Flags).GetValue(glow) != null) throw new Exception("Display glow acquired an unrelated body");
                if ((int)T("CuiVitals").GetMethod("PumpCount", Flags).Invoke(null, new object[] {null}) != 0)
                    throw new Exception("Bodyless pump count must be zero");
                var nodes = display.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < 2; ++i)
                {
                    string hand = i == 0 ? "LeftHand" : "RightHand";
                    Transform Node(string suffix) => Array.Find(nodes, n => n.name == hand + suffix);
                    var surface = Node("GoldenSkin"); var halo = Node("SoftGlow"); var trail = Node("StrikeTrail");
                    if (!surface || !halo || !trail) throw new Exception("Missing hand effect nodes: " + hand);
                    surfaces[i] = surface.GetComponent<MeshRenderer>();
                    halos[i] = halo.GetComponent<MeshRenderer>();
                    trails[i] = trail.GetComponent<TrailRenderer>();
                    if (!surfaces[i] || !halos[i] || !trails[i]) throw new Exception("Missing hand effect renderers: " + hand);
                    if (surface.parent != halo.parent || surface.parent != trail.parent || surface.parent.name != hand)
                        throw new Exception("Hand effects are not attached to the authored " + hand + " bone");
                    views[i * 3] = surfaces[i]; views[i * 3 + 1] = halos[i]; views[i * 3 + 2] = trails[i];
                }
                for (int i = 0; i < views.Length; ++i)
                {
                    defaults[i] = views[i].sharedMaterial;
                    if (!defaults[i] || !defaults[i].shader || !defaults[i].shader.isSupported)
                        throw new Exception("Unsupported fist material: " + views[i].name);
                }
            });
            if (!display || !glow || !model || !skin || Array.Exists(views, view => !view)) return;

            CheckOne("Cui gold skin preserves actual hand geometry and native visibility registration", () => {
                var source = skin.sharedMesh;
                var weights = source.boneWeights;
                var vertices = source.vertices;
                var binds = source.bindposes;
                var triangles = source.triangles;
                for (int i = 0; i < 2; ++i)
                {
                    int bone = Array.FindIndex(skin.bones, b => b == surfaces[i].transform.parent);
                    if (bone < 0) throw new Exception("Glow bone absent from the skinned renderer");
                    int count = 0, indexCount = 0;
                    var bounds = new Bounds();
                    bool Belongs(int v) => weights[v].boneIndex0 == bone && weights[v].weight0 >= .999f;
                    for (int v = 0; v < vertices.Length; ++v)
                    {
                        if (!Belongs(v)) continue;
                        var point = binds[bone].MultiplyPoint3x4(vertices[v]);
                        if (count++ == 0) bounds = new Bounds(point, Vector3.zero); else bounds.Encapsulate(point);
                    }
                    for (int t = 0; t < triangles.Length; t += 3)
                        if (Belongs(triangles[t]) && Belongs(triangles[t + 1]) && Belongs(triangles[t + 2])) indexCount += 3;
                    var mesh = surfaces[i].GetComponent<MeshFilter>().sharedMesh;
                    if (!mesh || count == 0 || mesh.vertexCount != count || mesh.GetIndexCount(0) != indexCount)
                        throw new Exception("Hand surface was replaced with unrelated or incomplete geometry");
                    if ((mesh.bounds.center - bounds.center).magnitude > .004f ||
                        (mesh.bounds.size - bounds.size).magnitude > .008f)
                        throw new Exception("Hand glow is enlarged beyond its fitted surface");
                    if (halos[i].transform.localScale.x > .35f) throw new Exception("Idle hand halo obscures the fist silhouette");
                }
                Registered();
            });

            CheckOne("Cui idle, run and jump keep fists lit without movement trails", () => {
                foreach (string cue in new[] {"idle", "run", "air", "land", "lobby"})
                {
                    Pose(cue, .3f); Trails(false, false);
                    for (int i = 0; i < 2; ++i)
                        if (surfaces[i].forceRenderingOff || halos[i].forceRenderingOff || Light(surfaces[i]).r <= .1f)
                            throw new Exception("Persistent fist glow missing in " + cue);
                }
            });

            CheckOne("Cui jab, hook and uppercut illuminate only their striking hand", () => {
                foreach (string cue in new[] {"jab", "cross", "uppercut"})
                {
                    bool left = cue == "jab";
                    Pose(cue, .02f); Trails(false, false);
                    Pose(cue, .3f); Trails(left, !left);
                    float active = Light(surfaces[left ? 0 : 1]).r, resting = Light(surfaces[left ? 1 : 0]).r;
                    if (active < resting * 2f) throw new Exception(cue + " does not visibly brighten the striking fist");
                    Pose(cue, .9f); Trails(false, false);
                }
            });

            CheckOne("Cui death and native invisibility hide all fist effects", () => {
                Pose("jab", .3f); Pose("death", 1f); Trails(false, false);
                foreach (var view in views) if (!view.forceRenderingOff) throw new Exception("Dead fist effect remains visible: " + view.name);
                foreach (var trail in trails) if (trail.positionCount != 0) throw new Exception("Death retained a strike trail");
                int hidden = model.invisibilityCount;
                try
                {
                    model.invisibilityCount = hidden + 1;
                    Pose("cross", .3f); Trails(false, false);
                    foreach (var view in views) if (!view.forceRenderingOff) throw new Exception("Hidden fist effect remains visible: " + view.name);
                }
                finally {model.invisibilityCount = hidden;}
                Pose("idle", .3f);
                foreach (var view in views) if (view.forceRenderingOff) throw new Exception("Fist visibility did not recover: " + view.name);
            });

            CheckOne("Cui fist registration recovers after cloaking and a renderer-list rebuild", () => {
                // Cloaking temporarily clears materials. A subsequent skin/list
                // rebuild must restore original defaults, not cache those nulls.
                model.baseRendererInfos = Array.FindAll(model.baseRendererInfos,
                    info => Array.IndexOf(views, info.renderer) < 0);
                foreach (var view in views) view.sharedMaterial = null;
                Pose("idle", .3f); Registered();
                Pose("idle", .3f); Registered();
                for (int i = 0; i < views.Length; ++i) views[i].sharedMaterial = defaults[i];
            });

            CheckOne("Cui fist component removal unregisters every owned renderer", () => {
                Pose("uppercut", .3f);
                DestroyImmediate(glow); glow = null;
                foreach (var trail in trails)
                    if (trail && (trail.emitting || trail.positionCount != 0)) throw new Exception("Removed glow kept an emitting trail");
                if (Array.Exists(model.baseRendererInfos, info => Array.IndexOf(views, info.renderer) >= 0))
                    throw new Exception("Destroyed fist component left native renderer registrations behind");
            });
        }
        finally {if (display) DestroyImmediate(display);}
    }
}

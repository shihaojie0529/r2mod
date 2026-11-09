using System;
using System.Collections.Generic;
using RoR2;
using UnityEngine;
using UnityEngine.Rendering;

namespace RiftSurvivor
{
    // The glow follows the authored hand vertices and bone poses, including lobby
    // animation. It never guesses a hand position from the body's forward vector.
    [DisallowMultipleComponent, DefaultExecutionOrder(31100)]
    internal sealed class CuiFistGlow : MonoBehaviour
    {
        private sealed class Hand
        {
            internal Transform joint;
            internal MeshRenderer surface, halo;
            internal TrailRenderer trail;
            internal Material trailMaterial;
            internal readonly MaterialPropertyBlock surfaceBlock = new MaterialPropertyBlock();
            internal readonly MaterialPropertyBlock haloBlock = new MaterialPropertyBlock();
            internal bool visible;
        }

        private static readonly int Tint = Shader.PropertyToID("_TintColor");
        private static readonly Mesh[] handMeshes = new Mesh[2];
        private static Mesh haloMesh;
        private static Material surfaceMaterial, haloMaterial;
        private readonly Hand[] hands = new Hand[2];
        private CharacterModel owner;
        private CharacterBody body;
        private SkinnedMeshRenderer bodyRenderer;
        private CharacterModel.RendererInfo[] registered;
        private string cue;
        private float cueTime, pump;

        internal void Bind(CuiRig rig, SkinnedMeshRenderer renderer, CharacterModel model, CharacterBody character)
        {
            if (Application.isBatchMode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null ||
                rig == null || !rig.Root || !renderer || !renderer.sharedMesh) return;
            if (owner && owner != model) RemoveRenderers();
            owner = model; body = character; bodyRenderer = renderer;
            if (!surfaceMaterial) surfaceMaterial = CombatVisuals.Radiance("CuiFistSurfaceLight");
            if (!haloMaterial) haloMaterial = CombatVisuals.SoftRadiance("CuiFistSoftBloom", false);
            if (!haloMesh)
            {
                haloMesh = new Mesh {name = "CuiFistBloomQuad"};
                haloMesh.vertices = new[] {new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),
                    new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)};
                haloMesh.uv = new[] {Vector2.zero,Vector2.right,Vector2.one,Vector2.up};
                haloMesh.colors = new[] {Color.white,Color.white,Color.white,Color.white};
                haloMesh.triangles = new[] {0,1,2,0,2,3};
                haloMesh.RecalculateBounds();
                haloMesh.UploadMeshData(true);
            }
            for (int i = 0; i < hands.Length; ++i)
            {
                string boneName = i == 0 ? "LeftHand" : "RightHand";
                if (!rig.Bones.TryGetValue(boneName, out var joint) || !joint) continue;
                if (!handMeshes[i]) handMeshes[i] = ExtractHand(renderer.sharedMesh, Array.IndexOf(CuiRig.Names, boneName));
                var hand = hands[i];
                if (hand == null)
                {
                    hand = hands[i] = new Hand();
                    hand.surface = View(boneName + "GoldenSkin", handMeshes[i], surfaceMaterial, joint);
                    hand.halo = View(boneName + "SoftGlow", haloMesh, haloMaterial, joint);
                    hand.halo.gameObject.AddComponent<CuiFistBillboard>();
                    hand.halo.transform.localPosition = handMeshes[i].bounds.center;
                    hand.halo.transform.localScale = Vector3.one * .27f;
                    var trailAnchor = new GameObject(boneName + "StrikeTrail").transform;
                    trailAnchor.gameObject.layer = renderer.gameObject.layer;
                    trailAnchor.SetParent(joint, false);
                    trailAnchor.localPosition = handMeshes[i].bounds.center;
                    CombatVisuals.Trail(trailAnchor, new Color(1f,.64f,.12f,.72f), .065f, .11f);
                    hand.trail = trailAnchor.GetComponent<TrailRenderer>();
                    hand.trailMaterial = hand.trail.sharedMaterial;
                    hand.trail.minVertexDistance = .012f;
                    hand.trail.widthCurve = new AnimationCurve(new Keyframe(0,1),new Keyframe(.5f,.65f),new Keyframe(1,0));
                    hand.trail.emitting = false;
                }
                hand.joint = joint;
                hand.surface.transform.SetParent(joint, false);
                hand.halo.transform.SetParent(joint, false);
                hand.trail.transform.SetParent(joint, false);
            }
            RegisterRenderers();
        }

        // Pose time comes from the same clip used for the mesh. Attack speed and
        // remote state playback therefore change the light and the punch together.
        internal void Show(string animation, float normalizedTime)
        { cue = animation; cueTime = normalizedTime; }

        private void LateUpdate()
        {
            if (!bodyRenderer) return;
            RegisterRenderers();
            bool visible = cue != "death" && !bodyRenderer.forceRenderingOff &&
                (owner ? owner.invisibilityCount <= 0 : bodyRenderer.enabled) &&
                (!body || !body.healthComponent || body.healthComponent.alive);
            float now = body ? Time.time : Time.unscaledTime;
            float dt = body ? Time.deltaTime : Time.unscaledDeltaTime;
            pump = Mathf.Lerp(pump, CuiVitals.PumpCount(body) / 6f, 1f - Mathf.Exp(-9f * dt));
            bool punch = cue == "jab" || cue == "cross" || cue == "uppercut";
            float swing = punch ? Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.08f,.30f,cueTime)) *
                (1f - Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.48f,.85f,cueTime))) : 0f;
            bool charged = cue == "slamHeavy" || cue == "ready";
            for (int i = 0; i < hands.Length; ++i)
            {
                var hand = hands[i];
                if (hand == null || !hand.joint) continue;
                bool striking = punch && (cue == "jab" ? i == 0 : i == 1);
                float accent = striking ? swing : charged ? .3f : 0f;
                float breathe = .96f + .04f * Mathf.Sin(now * 2.5f + i * .4f);
                float light = (.48f + pump * .72f + accent * 1.65f) * breathe;
                hand.surface.GetPropertyBlock(hand.surfaceBlock);
                hand.surfaceBlock.SetColor(Tint, new Color(light,light,light,1f));
                hand.surface.SetPropertyBlock(hand.surfaceBlock);
                hand.halo.GetPropertyBlock(hand.haloBlock);
                hand.haloBlock.SetColor(Tint, new Color(1f,.56f + accent * .18f,.085f + accent * .15f,
                    .075f + pump * .085f + accent * .15f));
                hand.halo.SetPropertyBlock(hand.haloBlock);
                hand.halo.transform.localScale = Vector3.one * (.27f + pump * .035f + accent * .045f);
                hand.surface.forceRenderingOff = hand.halo.forceRenderingOff = hand.trail.forceRenderingOff = !visible;
                if (!visible || !hand.visible) hand.trail.Clear();
                hand.trail.emitting = visible && striking && cueTime > .15f && cueTime < .61f;
                hand.trail.widthMultiplier = .065f * Mathf.Max(.01f, Mathf.Abs(hand.joint.lossyScale.y));
                hand.visible = visible;
            }
        }

        private MeshRenderer View(string name, Mesh mesh, Material material, Transform joint)
        {
            var obj = new GameObject(name);
            obj.layer = bodyRenderer.gameObject.layer;
            obj.transform.SetParent(joint, false);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            var view = obj.AddComponent<MeshRenderer>();
            view.sharedMaterial = material;
            view.shadowCastingMode = ShadowCastingMode.Off;
            view.receiveShadows = false;
            view.lightProbeUsage = LightProbeUsage.Off;
            view.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return view;
        }

        private static Mesh ExtractHand(Mesh source, int bone)
        {
            var sourceVertices = source.vertices;
            var sourceNormals = source.normals;
            var weights = source.boneWeights;
            var bind = source.bindposes[bone];
            var remap = new int[sourceVertices.Length];
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            for (int i = 0; i < sourceVertices.Length; ++i)
            {
                remap[i] = -1;
                if (weights[i].boneIndex0 != bone || weights[i].weight0 < .999f) continue;
                remap[i] = vertices.Count;
                var point = bind.MultiplyPoint3x4(sourceVertices[i]);
                var normal = bind.MultiplyVector(sourceNormals[i]).normalized;
                vertices.Add(point + normal * .0015f);
                float tip = Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.025f,-.10f,point.y));
                colors.Add(new Color(1f,.60f + tip * .18f,.12f + tip * .15f,.16f + tip * .44f));
            }
            var indices = source.triangles;
            for (int i = 0; i < indices.Length; i += 3)
            {
                int a = remap[indices[i]], b = remap[indices[i+1]], c = remap[indices[i+2]];
                if (a < 0 || b < 0 || c < 0) continue;
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
            }
            if (vertices.Count == 0 || triangles.Count == 0) throw new InvalidOperationException("Cui hand has no matching vertices.");
            var mesh = new Mesh {name = "CuiFistGlow_" + CuiRig.Names[bone]};
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.UploadMeshData(true);
            return mesh;
        }

        private void RegisterRenderers()
        {
            if (!owner || owner.baseRendererInfos == null || ReferenceEquals(owner.baseRendererInfos, registered)) return;
            var infos = new List<CharacterModel.RendererInfo>(owner.baseRendererInfos);
            foreach (var hand in hands)
            {
                if (hand == null) continue;
                void Include(Renderer renderer, Material material)
                {
                    if (infos.Exists(info => info.renderer == renderer)) return;
                    infos.Add(new CharacterModel.RendererInfo {renderer = renderer, defaultMaterial = material,
                        defaultShadowCastingMode = ShadowCastingMode.Off, ignoreOverlays = true, hideOnDeath = true});
                }
                Include(hand.surface, surfaceMaterial); Include(hand.halo, haloMaterial); Include(hand.trail, hand.trailMaterial);
            }
            registered = infos.Count == owner.baseRendererInfos.Length ? owner.baseRendererInfos : infos.ToArray();
            owner.baseRendererInfos = registered;
            owner.materialsDirty = true;
        }

        private void RemoveRenderers()
        {
            if (!owner || owner.baseRendererInfos == null) return;
            var infos = new List<CharacterModel.RendererInfo>(owner.baseRendererInfos);
            infos.RemoveAll(info => Array.Exists(hands, hand => hand != null &&
                (ReferenceEquals(info.renderer,hand.surface) || ReferenceEquals(info.renderer,hand.halo) || ReferenceEquals(info.renderer,hand.trail))));
            owner.baseRendererInfos = infos.ToArray(); owner.materialsDirty = true; registered = null;
        }

        private void OnDisable()
        {
            foreach (var hand in hands)
            {
                if (hand == null) continue;
                if (hand.surface) hand.surface.forceRenderingOff = true;
                if (hand.halo) hand.halo.forceRenderingOff = true;
                if (hand.trail) {hand.trail.emitting = false; hand.trail.Clear();}
                hand.visible = false;
            }
        }

        private void OnDestroy()
        {
            RemoveRenderers();
            foreach (var hand in hands)
            {
                if (hand == null) continue;
                if (hand.surface) Destroy(hand.surface.gameObject);
                if (hand.halo) Destroy(hand.halo.gameObject);
                if (hand.trail) Destroy(hand.trail.gameObject);
            }
        }
    }

    // Each camera sees a soft circular glow; split-screen cameras do not inherit
    // another player's orientation. The actual fist surface keeps its bone pose.
    internal sealed class CuiFistBillboard : MonoBehaviour
    {
        private void OnWillRenderObject()
        {
            var camera = Camera.current;
            if (camera) transform.rotation = camera.transform.rotation;
        }
    }
}

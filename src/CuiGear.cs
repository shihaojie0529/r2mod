using System;
using System.Collections.Generic;
using System.IO;
using RoR2;
using UnityEngine;
using UnityEngine.Rendering;

namespace RiftSurvivor
{
    // One fitted rig owns the held objects and one independent, reusable flight visual.
    // The latter follows a world-space arc, so hiding the hand prop never hides the toss.
    [DefaultExecutionOrder(31000)]
    internal sealed class CuiGear : MonoBehaviour
    {
        [Serializable] private sealed class Document { public Piece[] parts = null; }
        [Serializable] private sealed class Piece
        {
            public string kind = null;
            public float[] color = null, vertices = null;
            public int[] triangles = null;
        }

        private sealed class Prop
        {
            internal Mesh mesh;
            internal Material material;
        }

        private static readonly Vector3 RightGrip = new Vector3(.035f, -.032f, .015f);
        private static readonly Vector3 LeftGrip = new Vector3(-.035f, -.032f, .015f);
        private static Prop plateAsset, shakerAsset;
        private static bool loadFailed;
        private Transform root, rightHand, leftHand;
        private CharacterModel owner;
        private SkinnedMeshRenderer bodyRenderer;
        private MeshRenderer plate, heldCup, thrownCup;
        private CharacterModel.RendererInfo[] registered;
        private CharacterBody receiver;
        private bool pending, flying;
        private float releaseAt, flightStart, flightDuration, arcHeight;
        private Vector3 launchPoint;
        private Quaternion launchRotation;

        internal void Bind(CuiRig rig, SkinnedMeshRenderer renderer, CharacterModel model)
        {
            if (Application.isBatchMode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null ||
                rig == null || !rig.Root || !renderer || loadFailed) return;
            if (!rig.Bones.TryGetValue("RightHand", out var right) ||
                !rig.Bones.TryGetValue("LeftHand", out var left) || !right || !left) return;

            if (plateAsset == null)
            {
                var material = renderer.sharedMaterial;
                if (!material || !material.shader) return;
                try { LoadProps(material.shader); }
                catch (Exception e)
                {
                    loadFailed = true;
                    Debug.LogWarning("[CuiGe] Unable to load gym props: " + e.Message);
                    return;
                }
            }
            if (owner && owner != model) RemoveRenderers();
            root = rig.Root;
            rightHand = right;
            leftHand = left;
            owner = model;
            bodyRenderer = renderer;
            plate = Prepare(plate, plateAsset, "CuiIronPlate");
            heldCup = Prepare(heldCup, shakerAsset, "CuiHeldShaker");
            thrownCup = Prepare(thrownCup, shakerAsset, "CuiFlyingShaker");
            RegisterRenderers();
        }

        internal void Show(string cue, float normalizedTime, Quaternion plateOrientation, Quaternion shakerOrientation)
        {
            if (!root || !plate || !leftHand || !rightHand) return;
            bool slamming = cue == "slam" || cue == "slamHeavy";
            bool handlingCup = cue == "drink" || cue == "aim" || cue == "throw";
            SetVisible(plate, slamming);
            SetVisible(heldCup, handlingCup && (cue != "throw" || normalizedTime < .42f));
            if (slamming)
            {
                Vector3 center = (rightHand.TransformPoint(RightGrip) + leftHand.TransformPoint(LeftGrip)) * .5f;
                plate.transform.localPosition = root.InverseTransformPoint(center);
                plate.transform.localRotation = plateOrientation;
            }
            if (handlingCup)
            {
                heldCup.transform.localPosition = root.InverseTransformPoint(rightHand.TransformPoint(RightGrip));
                heldCup.transform.localRotation = shakerOrientation;
            }
        }

        internal void Throw(CharacterBody target, float delay)
        {
            if (!target || !thrownCup) return;
            receiver = target;
            releaseAt = Time.time + Mathf.Max(0f, delay);
            pending = true;
        }

        private void LateUpdate()
        {
            if (!root || !bodyRenderer || !thrownCup) return;
            RegisterRenderers(); // Skins can rebuild CharacterModel.baseRendererInfos.
            bool hidden = bodyRenderer.forceRenderingOff || (!owner && !bodyRenderer.enabled);
            plate.forceRenderingOff = heldCup.forceRenderingOff = thrownCup.forceRenderingOff = hidden;
            float now = Time.time;
            if (!receiver)
            {
                if (pending || flying) EndFlight();
                return;
            }
            if (pending && now >= releaseAt)
            {
                pending = false;
                flying = true;
                launchPoint = rightHand.TransformPoint(RightGrip);
                launchRotation = heldCup.transform.rotation;
                flightStart = now;
                float distance = Vector3.Distance(launchPoint, receiver.corePosition);
                flightDuration = Mathf.Clamp(.18f + distance * .008f, .2f, .4f);
                arcHeight = Mathf.Clamp(.18f + distance * .07f, .2f, 1.4f);
                SetVisible(thrownCup, true);
            }
            if (!flying) return;
            float t = Mathf.Clamp01((now - flightStart) / flightDuration);
            thrownCup.transform.position = Vector3.Lerp(launchPoint, receiver.corePosition, t)
                + Vector3.up * (4f * t * (1f - t) * arcHeight);
            thrownCup.transform.rotation = launchRotation * Quaternion.Euler(235f * t, 0f, 0f);
            if (t >= 1f) EndFlight();
        }

        private void EndFlight()
        {
            pending = flying = false;
            receiver = null;
            SetVisible(thrownCup, false);
        }

        private MeshRenderer Prepare(MeshRenderer instance, Prop asset, string label)
        {
            if (!instance)
            {
                var obj = new GameObject(label);
                obj.layer = bodyRenderer.gameObject.layer;
                obj.AddComponent<MeshFilter>().sharedMesh = asset.mesh;
                instance = obj.AddComponent<MeshRenderer>();
                instance.sharedMaterial = asset.material;
                instance.shadowCastingMode = bodyRenderer.shadowCastingMode;
                instance.receiveShadows = bodyRenderer.receiveShadows;
            }
            instance.transform.SetParent(root, false);
            instance.transform.localScale = Vector3.one;
            SetVisible(instance, false);
            return instance;
        }

        private static void SetVisible(Renderer prop, bool visible)
        {
            if (prop && prop.gameObject.activeSelf != visible) prop.gameObject.SetActive(visible);
        }

        private void RegisterRenderers()
        {
            // Lobby displays can have an animator/renderer without a CharacterModel.
            // Their ordinary materials still render; only overlay registration is skipped.
            if (!owner || !bodyRenderer) return;
            var current = owner.baseRendererInfos;
            if (current == null || ReferenceEquals(current, registered)) return;
            var updated = new List<CharacterModel.RendererInfo>(current);
            void Include(MeshRenderer renderer, Prop asset)
            {
                if (updated.Exists(info => info.renderer == renderer)) return;
                updated.Add(new CharacterModel.RendererInfo
                {
                    renderer = renderer, defaultMaterial = asset.material,
                    defaultShadowCastingMode = bodyRenderer.shadowCastingMode, ignoreOverlays = false
                });
            }
            Include(plate, plateAsset);
            Include(heldCup, shakerAsset);
            Include(thrownCup, shakerAsset);
            registered = updated.Count == current.Length ? current : updated.ToArray();
            owner.baseRendererInfos = registered;
            owner.materialsDirty = true;
        }

        private void RemoveRenderers()
        {
            if (!owner || owner.baseRendererInfos == null) return;
            var infos = new List<CharacterModel.RendererInfo>(owner.baseRendererInfos);
            infos.RemoveAll(info => ReferenceEquals(info.renderer, plate) || ReferenceEquals(info.renderer, heldCup)
                || ReferenceEquals(info.renderer, thrownCup));
            owner.baseRendererInfos = infos.ToArray();
            owner.materialsDirty = true;
            registered = null;
        }

        private void OnDestroy()
        {
            RemoveRenderers();
            // Props are rig children. Clean them up even if the behaviour alone is removed.
            if (plate) Destroy(plate.gameObject);
            if (heldCup) Destroy(heldCup.gameObject);
            if (thrownCup) Destroy(thrownCup.gameObject);
        }

        private static void LoadProps(Shader shader)
        {
            Document doc;
            using (var stream = typeof(CuiGear).Assembly.GetManifestResourceStream("RiftSurvivor.props.json"))
            using (var text = new StreamReader(stream ?? throw new InvalidDataException("Embedded prop mesh missing.")))
                doc = JsonUtility.FromJson<Document>(text.ReadToEnd());
            if (doc?.parts == null) throw new InvalidDataException("No prop parts found.");
            plateAsset = BuildProp("plate", doc.parts, shader);
            shakerAsset = BuildProp("shaker", doc.parts, shader);
        }

        private static Prop BuildProp(string kind, Piece[] all, Shader shader)
        {
            var pieces = Array.FindAll(all, p => p.kind == kind);
            if (pieces.Length == 0) throw new InvalidDataException("Missing prop: " + kind);
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var indices = new List<int>();
            var swatches = new Color32[pieces.Length];
            for (int i = 0; i < pieces.Length; i++)
            {
                var piece = pieces[i];
                if (piece.color?.Length != 3 || piece.vertices == null || piece.vertices.Length % 3 != 0 ||
                    piece.triangles == null || piece.triangles.Length % 3 != 0)
                    throw new InvalidDataException("Invalid geometry for " + kind);
                int first = vertices.Count;
                for (int j = 0; j < piece.vertices.Length; j += 3)
                {
                    vertices.Add(new Vector3(piece.vertices[j], piece.vertices[j+1], piece.vertices[j+2]));
                    uv.Add(new Vector2((i + .5f) / pieces.Length, .5f));
                }
                foreach (int index in piece.triangles)
                {
                    if (index < 0 || index >= vertices.Count-first) throw new InvalidDataException("Invalid prop triangle.");
                    indices.Add(first + index);
                }
                swatches[i] = new Color(piece.color[0], piece.color[1], piece.color[2], 1f);
            }
            var mesh = new Mesh { name = "CuiGear_" + kind };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);

            var texture = new Texture2D(swatches.Length, 1, TextureFormat.RGBA32, false, false)
            {
                name = mesh.name + "Palette", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(swatches);
            texture.Apply(false, true);
            var material = new Material(shader) { name = mesh.name + "Material" };
            material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
            if (material.HasProperty("_NormalStrength")) material.SetFloat("_NormalStrength", 0f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", kind == "plate" ? .35f : .18f);
            return new Prop { mesh = mesh, material = material };
        }
    }
}

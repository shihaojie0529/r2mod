using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace RiftSurvivor
{
    // A presentation of the synchronized reserve, never an independent sword count.
    [DisallowMultipleComponent, DefaultExecutionOrder(31100)]
    internal sealed class JieSwordRackVisual : MonoBehaviour
    {
        private CharacterBody owner;
        private CharacterModel model;
        private SkinnedMeshRenderer bodyRenderer;
        private SkinnedMeshRenderer[] skinCandidates;
        private Transform rack;
        private readonly Transform[] swords = new Transform[JieSwords.Capacity];
        private readonly float[] reveal = new float[JieSwords.Capacity];
        private void Awake()
        {
            owner = GetComponent<CharacterBody>();
            model = GetComponentInChildren<CharacterModel>(true);
            skinCandidates = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            ResolveBodyRenderer();
        }

        // The curved arrangement opens toward the shoulders and leaves the head clear.
        internal static void SocketPose(CharacterBody body, int index, out Vector3 position, out Quaternion rotation)
        {
            var forward = body.characterDirection ? body.characterDirection.forward : body.transform.forward;
            forward = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (forward.sqrMagnitude < .001f) forward = Vector3.forward;
            var basis = Quaternion.LookRotation(forward.normalized, Vector3.up);
            int spoke = index == 0 ? 0 : ((index + 1) / 2) * (index % 2 == 0 ? 1 : -1);
            float angle = spoke * 12f * Mathf.Deg2Rad;
            var radial = new Vector3(Mathf.Sin(angle), Mathf.Cos(angle), 0f);
            var local = new Vector3(0f, -.16f, -.66f) + radial * .74f;
            local.y += Mathf.Sin(Time.time * 1.9f + index * .7f) * .035f;
            position = body.corePosition + basis * local;
            rotation = basis * Quaternion.LookRotation(radial, Vector3.forward);
        }

        private void LateUpdate()
        {
            if (!NetworkClient.active || !owner || !owner.healthComponent || !owner.healthComponent.alive)
            { Clear(); return; }
            if (!rack)
            {
                rack = new GameObject("JieNineSwordReserve").transform;
                rack.SetParent(transform, false);
                for (int i = 0; i < swords.Length; i++)
                {
                    swords[i] = new GameObject("JadeReserveSword" + (i + 1)).transform;
                    swords[i].SetParent(rack, false);
                    JieSwordArt.Attach(swords[i], JieSwordArt.Jade);
                }
            }
            bool visibleBody = OwnerVisible();
            rack.gameObject.SetActive(visibleBody);
            if (!visibleBody) return;
            int ready = JieSwords.ReadyCount(owner);
            for (int i = 0; i < swords.Length; i++)
            {
                bool visible = i < ready;
                swords[i].gameObject.SetActive(visible);
                if (!visible) { reveal[i] = 0f; continue; }
                reveal[i] = Mathf.Min(1f, reveal[i] + Time.deltaTime * 6f);
                SocketPose(owner, i, out var point, out var rotation);
                swords[i].SetPositionAndRotation(point, rotation);
                swords[i].localScale = Vector3.one * (1.02f * Mathf.SmoothStep(.15f, 1f, reveal[i]));
            }
        }

        internal bool OwnerVisible()
        {
            if (!bodyRenderer || !bodyRenderer.sharedMesh || bodyRenderer.sharedMesh.name != "JieGeSkin")
                ResolveBodyRenderer();
            // Stored weapons are part of the owner's silhouette: cloak and
            // native model suppression must never leave a visible sword rack.
            return (!model || model.invisibilityCount <= 0) &&
                (!bodyRenderer || (bodyRenderer.enabled && !bodyRenderer.forceRenderingOff));
        }

        private void ResolveBodyRenderer()
        {
            // The borrowed gameplay CharacterModel does not always populate
            // mainSkinnedMeshRenderer. JieModel binds its actual authored skin
            // to the existing CommandoMesh renderer, so prefer that real mesh.
            if (skinCandidates != null)
                foreach (var candidate in skinCandidates)
                    if (candidate && candidate.sharedMesh && candidate.sharedMesh.name == "JieGeSkin")
                    { bodyRenderer = candidate; return; }
            if (model && model.mainSkinnedMeshRenderer)
            { bodyRenderer = model.mainSkinnedMeshRenderer; return; }
            if (skinCandidates != null)
                foreach (var candidate in skinCandidates)
                    if (candidate && candidate.name == "CommandoMesh")
                    { bodyRenderer = candidate; return; }
        }

        private void Clear()
        {
            if (rack) Destroy(rack.gameObject);
            rack = null;
            for (int i = 0; i < reveal.Length; i++) reveal[i] = 0f;
        }
        private void OnDisable() => Clear();
    }
}

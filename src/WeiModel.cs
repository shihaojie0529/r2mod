using System;
using EntityStates;
using RoR2;
using UnityEngine;

namespace RiftSurvivor
{
    [DisallowMultipleComponent, DefaultExecutionOrder(30100)]
    public sealed class WeiModel : MonoBehaviour
    {
        internal static Texture2D Portrait { get; } = FriendSkin.Portrait("WeiGe");
        private static FriendSkin skin;
        private static bool reportedFailure;
        private static readonly Material[] WatchMaterials = new Material[8];
        private static readonly int[] SegmentSwatches = { 0x67f1b9, 0x70f3bb, 0x7af5bd };
        private const int StepSwatch = 0xf6bb68;
        private CharacterModel model;
        private CharacterBody body;
        private SkinnedMeshRenderer target;
        private CuiRig rig;
        private Vector3 previousPosition;
        private float phase, stride, pitch, yaw, grip, release, select;
        private EntityState previousWeapon, previousCarry, previousSpecial;

        internal static void Install(GameObject prefab)
        {
            var model = prefab.GetComponentInChildren<CharacterModel>(true);
            var animator = prefab.GetComponentInChildren<Animator>(true);
            (model ? model.gameObject : animator ? animator.gameObject : prefab).AddComponent<WeiModel>();
        }

        private void Awake()
        {
            model = GetComponent<CharacterModel>();
            body = GetComponentInParent<CharacterBody>();
            var renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            target = Array.Find(renderers, r => r.name == "CommandoMesh");
            if (!target && model) target = model.mainSkinnedMeshRenderer;
            if (!target && renderers.Length > 0) target = renderers[0];
        }

        private void LateUpdate()
        {
            if (!target || !target.sharedMesh) return;
            var infos = model ? model.baseRendererInfos : null;
            int slot = -1;
            for (int i = 0; infos != null && i < infos.Length; ++i)
                if (infos[i].renderer == target) { slot = i; break; }
            if (rig == null)
            {
                try
                {
                    var original = slot >= 0 ? infos[slot].defaultMaterial : target.sharedMaterial;
                    if (!original) return;
                    skin ??= new FriendSkin("WeiGe", original.shader, CuiRig.Names);
                    rig = new CuiRig(transform, target, body, skin.Pivots);
                    rig.Root.name = "WeiGeRig";
                    previousPosition = transform.position;
                    foreach (var gun in GetComponentsInChildren<MeshRenderer>(true))
                        if (gun.name.StartsWith("GunMesh", StringComparison.Ordinal)) gun.forceRenderingOff = true;
                }
                catch (Exception ex)
                {
                    if (!reportedFailure) Debug.LogError("[RiftSurvivor] Could not attach Wei Ge's model: " + ex);
                    reportedFailure = true;
                    enabled = false;
                    return;
                }
            }
            float pulse = body ? WeiVitals.DoorPulse(body) : 0f;
            int stacks = body ? Mathf.Clamp(WeiVitals.Stacks(body), 0, 3) : 3;
            var material = WatchMaterial(stacks, pulse > 0f);
            if (target.sharedMesh != skin.Mesh || target.rootBone != rig.Root)
            {
                target.bones = rig.OrderedBones;
                target.rootBone = rig.Root;
                target.sharedMesh = skin.Mesh;
                target.sharedMaterial = material;
                target.localBounds = new Bounds(Vector3.up, new Vector3(2.6f, 2.8f, 2.5f));
            }
            if (slot >= 0 && infos[slot].defaultMaterial != material)
            {
                infos[slot].defaultMaterial = material;
                model.materialsDirty = true;
            }
            else if (slot < 0 && target.sharedMaterial != material)
                target.sharedMaterial = material;
            Pose(body ? Time.deltaTime : Time.unscaledDeltaTime, pulse);
        }

        private static Material WatchMaterial(int stacks, bool pulse)
        {
            int index = stacks + (pulse ? 4 : 0);
            if (WatchMaterials[index]) return WatchMaterials[index];
            var swatches = (Color32[])skin.Colors.Clone();
            var emissions = new Color32[swatches.Length];
            var dim = new Color32(43, 62, 54, 255);
            for (int i = 0; i < swatches.Length; i++)
            {
                var color = swatches[i];
                int rgb = color.r << 16 | color.g << 8 | color.b;
                int segment = Array.IndexOf(SegmentSwatches, rgb);
                emissions[i] = new Color32(0, 0, 0, 255);
                if (segment < 0 && rgb != StepSwatch) continue;
                bool lit = segment >= 0 ? segment < stacks : pulse;
                if (lit) emissions[i] = swatches[i];
                else swatches[i] = dim;
            }
            var material = new Material(skin.Material) { name = "WeiGeWatch" + index,
                mainTexture = FriendSkin.Palette("WeiGeDisplay" + index, swatches) };
            var glow = FriendSkin.Palette("WeiGeWatchGlow" + index, emissions);
            if (material.HasProperty("_EmTex"))
            {
                material.SetTexture("_EmTex", glow);
                if (material.HasProperty("_EmColor")) material.SetColor("_EmColor", Color.white);
                if (material.HasProperty("_EmPower")) material.SetFloat("_EmPower", 1.4f);
            }
            if (material.HasProperty("_EmissionMap"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetTexture("_EmissionMap", glow);
                if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.white);
            }
            return WatchMaterials[index] = material;
        }

        private void Pose(float dt, float pulse)
        {
            var bones = rig.OrderedBones;
            float time = body ? Time.time : Time.unscaledTime;
            float breath = Mathf.Sin(time * 1.9f);
            var delta = transform.position - previousPosition;
            previousPosition = transform.position;
            float speed = body && dt > .0001f && delta.sqrMagnitude < 1f
                ? Vector3.ProjectOnPlane(delta, Vector3.up).magnitude / dt : 0f;
            stride = Mathf.MoveTowards(stride, Mathf.Clamp01(speed / 6.3f), 7f * dt);
            phase += dt * Mathf.Lerp(5f, 10.5f, stride);
            float step = Mathf.Sin(phase) * stride;
            float targetPitch = 0f, targetYaw = 0f;
            if (body && body.inputBank)
            {
                var aim = rig.Root.InverseTransformDirection(body.inputBank.aimDirection);
                targetPitch = -Mathf.Asin(Mathf.Clamp(aim.y, -1f, 1f)) * Mathf.Rad2Deg;
                targetYaw = Mathf.Clamp(Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg, -65f, 65f);
            }
            float blend = 1f - Mathf.Exp(-14f * dt);
            pitch = Mathf.Lerp(pitch, targetPitch, blend);
            yaw = Mathf.Lerp(yaw, targetYaw, blend);
            if (body && body.skillLocator)
            {
                var weapon = body.skillLocator.primary.stateMachine;
                var carry = body.skillLocator.secondary.stateMachine;
                var special = body.skillLocator.special.stateMachine;
                if (weapon && previousWeapon != weapon.state)
                {
                    previousWeapon = weapon.state;
                    if (previousWeapon is WeiDelivery) release = 1f;
                }
                if (carry && previousCarry != carry.state)
                {
                    previousCarry = carry.state;
                    if (previousCarry is WeiPaste) release = 1f;
                }
                if (special && previousSpecial != special.state)
                {
                    previousSpecial = special.state;
                    if (previousSpecial is WeiSelectAll) select = 1f;
                }
            }
            bool carrying = body && previousCarry is WeiCut && WeiThrowActor.HeldBy(body);
            grip = Mathf.Lerp(grip, carrying ? 1f : 0f, blend);
            float buzz = Mathf.Sin(pulse * 5f * Mathf.PI * 2f) * pulse;
            bones[0].localPosition = skin.Pivots[0] + Vector3.up * (.014f * Mathf.Abs(step) + .002f * breath);
            bones[0].localRotation = Quaternion.Euler(2.3f * stride, step * 3.5f, -step * 1.9f);
            bones[1].localRotation = Quaternion.Euler(pitch * .17f + .45f * breath, yaw * .36f, 0f);
            bones[2].localRotation = Quaternion.Euler(pitch * .45f + pulse * 6f, yaw * .44f, 0f);
            // The raised thumb becomes a forward command gesture. Grab remains held until release.
            bones[6].localRotation = Quaternion.Euler(6f * step - grip * 26f - release * 21f,
                yaw * .22f - grip * 9f, -7f * select);
            bones[7].localRotation = Quaternion.Euler(release * 35f + grip * 11f, 0f, select * 10f);
            bones[8].localRotation = Quaternion.Euler(-13f * grip + 9f * release, 8f * grip, 0f);
            bones[3].localRotation = Quaternion.Euler(-13f * step - 19f * select, 0f, -16f * select);
            bones[4].localRotation = Quaternion.Euler(-12f * pulse, 0f, 0f);
            bones[5].localRotation = Quaternion.Euler(1.8f * buzz, -9f * select, 3f * buzz);
            release = Mathf.MoveTowards(release, 0f, 4f * dt);
            select = Mathf.MoveTowards(select, 0f, 2.8f * dt);
            bool airborne = body && body.characterMotor && !body.characterMotor.isGrounded;
            for (int side = 0; side < 2; ++side)
            {
                int leg = 9 + side * 3;
                float swing = side == 0 ? step : -step;
                float knee = Mathf.Max(0f, -swing) * 44f;
                if (airborne) { swing = side == 0 ? .48f : -.25f; knee = side == 0 ? 33f : 49f; }
                bones[leg].localRotation = Quaternion.Euler(-31f * swing, 0f, 0f);
                bones[leg + 1].localRotation = Quaternion.Euler(knee, 0f, 0f);
                bones[leg + 2].localRotation = Quaternion.Euler(-.43f * knee + swing * 11f, 0f, 0f);
            }
        }
    }
}

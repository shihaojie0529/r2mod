using System;
using EntityStates;
using RoR2;
using UnityEngine;

namespace RiftSurvivor
{
    [DisallowMultipleComponent, DefaultExecutionOrder(30100)]
    public sealed class JieModel : MonoBehaviour
    {
        internal static Texture2D Portrait { get; } = FriendSkin.Portrait("JieGe");
        private static readonly string[] Joints = MakeJointList();
        private static FriendSkin skin;
        private static bool reported;
        private CuiRig rig;
        private Transform sword;
        private Transform[] bones;
        private CharacterModel model;
        private CharacterBody body;
        private SkinnedMeshRenderer target;
        private Vector3 restPosition, previousPosition;
        private Quaternion restRotation;
        private float cycle, stride, pitch, yaw, bank;
        private EntityState previousWeapon, previousStorm;
        private float drawGesture, recallGesture, stormGesture;

        private static string[] MakeJointList()
        {
            var names = new string[CuiRig.Names.Length + 1];
            Array.Copy(CuiRig.Names, names, CuiRig.Names.Length);
            names[names.Length - 1] = "Ride";
            return names;
        }

        internal static void Install(GameObject prefab)
        {
            OriginalModelGuard.Prepare(prefab);
            var model = prefab.GetComponentInChildren<CharacterModel>(true);
            var animator = prefab.GetComponentInChildren<Animator>(true);
            (model ? model.gameObject : animator ? animator.gameObject : prefab).AddComponent<JieModel>();
        }

        private void Awake()
        {
            model = GetComponent<CharacterModel>();
            body = GetComponentInParent<CharacterBody>();
            var renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            target = Array.Find(renderers, r => r.name == "CommandoMesh");
            if (!target && model) target = model.mainSkinnedMeshRenderer;
            if (!target && renderers.Length > 0) target = renderers[0];
            LateUpdate();
        }

        private void LateUpdate()
        {
            if (!target) return;
            var infos = model ? model.baseRendererInfos : null;
            int slot = -1;
            for (int i = 0; infos != null && i < infos.Length; ++i)
                if (infos[i].renderer == target) { slot = i; break; }
            if (rig == null)
            {
                try
                {
                    var shader = SurvivorModel.FindSkinShader(target, slot >= 0 ? infos[slot].defaultMaterial : null);
                    if (!shader) return;
                    skin ??= new FriendSkin("JieGe", shader, Joints);
                    rig = new CuiRig(transform, target, body, skin.Pivots);
                    rig.Root.name = "JieGeRig";
                    restPosition = rig.Root.localPosition;
                    restRotation = rig.Root.localRotation;
                    previousPosition = transform.position;
                    sword = new GameObject("JieGeRidingSword").transform;
                    sword.SetParent(rig.Root, false);
                    bones = new Transform[Joints.Length];
                    rig.OrderedBones.CopyTo(bones, 0);
                    bones[bones.Length - 1] = sword;
                    foreach (var gun in GetComponentsInChildren<MeshRenderer>(true))
                        if (gun.name.StartsWith("GunMesh", StringComparison.Ordinal)) gun.forceRenderingOff = true;
                }
                catch (Exception ex)
                {
                    if (!reported) Debug.LogError("[RiftSurvivor] Could not attach Jie Ge's model: " + ex);
                    reported = true;
                    enabled = false;
                    return;
                }
            }
            var material = SurvivorDisplay.DisplayMaterial(skin.Material, body);
            if (target.sharedMesh != skin.Mesh || target.rootBone != rig.Root)
            {
                target.bones = bones;
                target.rootBone = rig.Root;
                target.sharedMesh = skin.Mesh;
                target.sharedMaterial = material;
                target.localBounds = new Bounds(Vector3.up, new Vector3(3.4f, 3.2f, 3.4f));
            }
            if (slot >= 0 && infos[slot].defaultMaterial != material)
            {
                infos[slot].defaultMaterial = material;
                model.materialsDirty = true;
            }
            else if (slot < 0 && target.sharedMaterial != material)
                target.sharedMaterial = material;
            OriginalModelGuard.Reveal(target);
            Pose(body ? Time.deltaTime : Time.unscaledDeltaTime);
        }

        private void Pose(float dt)
        {
            bool riding = !body || JieFlight.IsRiding(body);
            // Board is an independent joint on the same renderer; material/visibility effects
            // and lobby skin changes apply to it exactly as they do to the rider.
            sword.localScale = riding ? Vector3.one : Vector3.zero;
            float now = body ? Time.time : Time.unscaledTime;
            float blend = 1f - Mathf.Exp(-12f * dt);
            var travel = transform.position - previousPosition;
            previousPosition = transform.position;
            float speed = dt > .0001f && travel.sqrMagnitude < 1f
                ? Vector3.ProjectOnPlane(travel, Vector3.up).magnitude / dt : 0f;
            stride = Mathf.MoveTowards(stride, riding ? 0f : Mathf.Clamp01(speed / 6.5f), dt * 8f);
            cycle += dt * Mathf.Lerp(5f, 10.3f, stride);
            float step = Mathf.Sin(cycle) * stride;
            float breath = Mathf.Sin(now * 2f);
            float targetYaw = 0f, targetPitch = 0f, targetBank = 0f;
            if (body && body.inputBank)
            {
                var aim = rig.Root.InverseTransformDirection(body.inputBank.aimDirection);
                targetYaw = Mathf.Clamp(Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg, -55f, 55f);
                targetPitch = -Mathf.Asin(Mathf.Clamp(aim.y, -1f, 1f)) * Mathf.Rad2Deg;
                targetBank = -8f * Vector3.Dot(body.inputBank.moveVector, rig.Root.right);
            }
            pitch = Mathf.Lerp(pitch, targetPitch, blend);
            yaw = Mathf.Lerp(yaw, targetYaw, blend);
            bank = Mathf.Lerp(bank, riding ? targetBank : 0f, blend);
            float lift = riding ? (body ? .047f : .16f) + breath * .018f : 0f;
            rig.Root.localPosition = restPosition + restRotation * Vector3.up * (lift * rig.Root.localScale.y);
            rig.Root.localRotation = restRotation * Quaternion.Euler(0f, 0f, bank);
            bones[0].localPosition = skin.Pivots[0] + Vector3.up * (.014f * Mathf.Abs(step));
            bones[0].localRotation = Quaternion.Euler(2f * stride, step * 3f, -step * 1.8f);
            bones[1].localRotation = Quaternion.Euler(pitch * .16f + breath * .48f, yaw * .32f, 0f);
            bones[2].localRotation = Quaternion.Euler(pitch * .35f, yaw * .48f, 3f);
            if (body && body.skillLocator)
            {
                var weapon = body.skillLocator.primary.stateMachine;
                var storm = body.skillLocator.special.stateMachine;
                if (weapon && weapon.state != previousWeapon)
                {
                    previousWeapon = weapon.state;
                    if (previousWeapon is JieDrawSword) drawGesture = 1f;
                    if (previousWeapon is JieRecallSwords) recallGesture = 1f;
                }
                if (storm && storm.state != previousStorm)
                {
                    previousStorm = storm.state;
                    if (previousStorm is JieStorm) stormGesture = 1f;
                }
            }
            // A flick of the sword seal launches a blade; recall draws the wrist back.
            // The free hand opens for the golden formation while the casting hand remains usable.
            bones[3].localRotation = Quaternion.Euler(-16f * step - 20f * stormGesture, 0f,
                (riding ? -breath * 2.3f : 0f) - 34f * stormGesture);
            bones[6].localRotation = Quaternion.Euler(8f * step + breath * 1.3f - 18f * drawGesture,
                yaw * .19f + 12f * recallGesture, -1.5f - 8f * recallGesture);
            bones[7].localRotation = Quaternion.Euler(pitch * .17f + 24f * drawGesture - 25f * recallGesture, 0f, 0f);
            bones[8].localRotation = Quaternion.Euler(9f * drawGesture - 12f * recallGesture, -12f * recallGesture, 0f);
            drawGesture = Mathf.MoveTowards(drawGesture, 0f, dt * 5f);
            recallGesture = Mathf.MoveTowards(recallGesture, 0f, dt * 3.2f);
            stormGesture = Mathf.MoveTowards(stormGesture, 0f, dt * 1.65f);
            bool falling = body && !riding && body.characterMotor && !body.characterMotor.isGrounded;
            for (int side = 0; side < 2; ++side)
            {
                int leg = 9 + side * 3;
                float swing = side == 0 ? step : -step;
                float knee = Mathf.Max(0f, -swing) * 42f;
                if (falling) { swing = side == 0 ? .45f : -.24f; knee = side == 0 ? 32f : 44f; }
                bones[leg].localRotation = Quaternion.Euler(-32f * swing, 0f, 0f);
                bones[leg + 1].localRotation = Quaternion.Euler(knee, 0f, 0f);
                bones[leg + 2].localRotation = Quaternion.Euler(-.45f * knee + 10f * swing, 0f, 0f);
            }
        }
    }
}

using System;
using RoR2;
using UnityEngine;
using UnityEngine.Rendering;

namespace RiftSurvivor
{
    [DisallowMultipleComponent, DefaultExecutionOrder(30100)]
    public sealed class XiaoheiModel : MonoBehaviour
    {
        internal static Texture2D Portrait { get; } = FriendSkin.Portrait("Xiaohei");
        private static Mesh skin;
        private static Material cloth, echoCloth;
        private static Texture2D echoPalette;
        private static Vector3[] pivots;
        private static bool reported;
        private CharacterModel model;
        private CharacterBody body;
        private SkinnedMeshRenderer target;
        private CuiRig rig;
        private Transform muzzle;
        private float gait, stride, recoil, pitch, yaw, walkDirection = 1f;
        private Vector3 previousPosition;
        private bool sampledPosition;

        internal static void Install(GameObject prefab)
        {
            OriginalModelGuard.Prepare(prefab);
            var characterModel = prefab.GetComponentInChildren<CharacterModel>(true);
            var animator = prefab.GetComponentInChildren<Animator>(true);
            (characterModel ? characterModel.gameObject : animator ? animator.gameObject : prefab)
                .AddComponent<XiaoheiModel>();
        }

        private void Awake()
        {
            model = GetComponent<CharacterModel>();
            body = GetComponentInParent<CharacterBody>();
            var renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            target = Array.Find(renderers, r => r.name == "CommandoMesh");
            if (!target && model) target = model.mainSkinnedMeshRenderer;
            if (!target && renderers.Length != 0) target = renderers[0];
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
                    if (!skin) LoadSkin(shader);
                    rig = new CuiRig(transform, target, body, pivots);
                    rig.Root.name = "XiaoheiRig";
                    muzzle = new GameObject("XiaoheiMuzzle").transform;
                    muzzle.SetParent(rig.Bones["RightHand"], false);
                    muzzle.localPosition = new Vector3(.246f, 1.381f, .609f) - pivots[8];
                    foreach (var gun in GetComponentsInChildren<MeshRenderer>(true))
                        if (gun.name.StartsWith("GunMesh", StringComparison.Ordinal)) gun.forceRenderingOff = true;
                }
                catch (Exception ex)
                {
                    if (!reported) Debug.LogError("[RiftSurvivor] Could not fit Xiaohei's skin: " + ex);
                    reported = true;
                    enabled = false;
                    return;
                }
            }
            var material = SurvivorDisplay.DisplayMaterial(cloth, body);
            if (target.sharedMesh != skin || target.rootBone != rig.Root)
            {
                target.bones = rig.OrderedBones;
                target.rootBone = rig.Root;
                target.sharedMesh = skin;
                target.sharedMaterial = material;
                target.localBounds = new Bounds(Vector3.up, new Vector3(2.4f, 2.6f, 2.5f));
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

        private static void LoadSkin(Shader shader)
        {
            var model = new FriendSkin("Xiaohei", shader, CuiRig.Names);
            var spectral = Array.ConvertAll(model.Colors, swatch =>
            {
                float value = Mathf.Sqrt(((Color)swatch).grayscale);
                return (Color32)Color.Lerp(new Color(.02f, .32f, .40f), new Color(.77f, 1f, .91f), value);
            });
            echoPalette = FriendSkin.Palette("XiaoheiEchoPalette", spectral);
            cloth = model.Material;
            pivots = model.Pivots;
            skin = model.Mesh;
        }

        private void Pose(float dt)
        {
            var bones = rig.OrderedBones;
            float time = body ? Time.time : Time.unscaledTime;
            var position = body ? body.transform.position : transform.position;
            var delta = position - previousPosition;
            previousPosition = position;
            // Ignore teleports. They should not make a standing gunner sprint in place.
            float speed = body && sampledPosition && dt > .0001f && delta.sqrMagnitude < 1f
                ? Vector3.ProjectOnPlane(delta, Vector3.up).magnitude / dt : 0f;
            sampledPosition = true;
            bool airborne = body && body.characterMotor && !body.characterMotor.isGrounded;
            stride = Mathf.MoveTowards(stride, Mathf.Clamp01(speed / 6.5f), dt * 8f);
            if (speed > .05f)
                walkDirection = Vector3.Dot(delta, rig.Root.forward) < 0f ? -1f : 1f;
            gait += dt * Mathf.Lerp(5.5f, 11f, stride) * walkDirection;
            float step = Mathf.Sin(gait) * stride;
            float breath = Mathf.Sin(time * 2.2f);
            float targetPitch = 0f, targetYaw = 0f;
            if (body && body.inputBank)
            {
                var aim = rig.Root.InverseTransformDirection(body.inputBank.aimDirection);
                targetPitch = -Mathf.Asin(Mathf.Clamp(aim.y, -1f, 1f)) * Mathf.Rad2Deg;
                targetYaw = Mathf.Clamp(Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg, -65f, 65f);
            }
            float blend = 1f - Mathf.Exp(-16f * dt);
            pitch = Mathf.Lerp(pitch, targetPitch, blend);
            yaw = Mathf.Lerp(yaw, targetYaw, blend);
            recoil = Mathf.MoveTowards(recoil, 0f, dt * 7.5f);
            bones[0].localPosition = pivots[0] + Vector3.up * (.003f * breath + .017f * Mathf.Abs(step));
            bones[0].localRotation = Quaternion.Euler(airborne ? 7f : 2f * stride, step * 4f, -step * 2f);
            bones[1].localRotation = Quaternion.Euler(.16f * pitch + breath * .5f, .50f * yaw, 0f);
            bones[2].localRotation = Quaternion.Euler(.40f * pitch, .30f * yaw, -.8f);
            bones[3].localRotation = Quaternion.Euler(-18f * step - .9f * breath, 0f, 0f);
            bones[4].localRotation = Quaternion.Euler(-10f * stride - 9f * Mathf.Max(0f, step), 0f, 0f);
            bones[6].localRotation = Quaternion.Euler(.64f * pitch - 4f * recoil, .48f * yaw, 0f);
            bones[7].localRotation = Quaternion.Euler(.17f * pitch - 6f * recoil, 0f, 0f);
            bones[8].localRotation = Quaternion.Euler(-3f * recoil, 0f, 0f);
            for (int side = 0; side < 2; ++side)
            {
                int leg = 9 + side * 3;
                float swing = side == 0 ? step : -step;
                float knee = Mathf.Max(0f, -swing) * 48f;
                if (airborne) { swing = side == 0 ? .58f : -.32f; knee = side == 0 ? 34f : 57f; }
                bones[leg].localRotation = Quaternion.Euler(-33f * swing, 0f, 0f);
                bones[leg + 1].localRotation = Quaternion.Euler(knee, 0f, 0f);
                bones[leg + 2].localRotation = Quaternion.Euler(-.45f * knee + 12f * swing, 0f, 0f);
            }
        }

        private static XiaoheiModel Find(CharacterBody owner)
        {
            var root = owner && owner.modelLocator ? owner.modelLocator.modelTransform : null;
            return root ? root.GetComponentInChildren<XiaoheiModel>(true) : null;
        }

        internal static Transform Muzzle(CharacterBody owner)
        {
            var view = Find(owner);
            return view ? view.muzzle : null;
        }

        internal static void Recoil(CharacterBody owner)
        {
            var view = Find(owner);
            if (view) view.recoil = 1f;
        }

        // The authored pose makes the large body and its barrel identical on all peers.
        internal static Vector3 ReplayMuzzle(float height)
        {
            if (!skin) LoadSkin(CombatVisuals.TextureShader);
            return (new Vector3(.246f, 1.381f, .609f) - Vector3.up * skin.bounds.min.y) * (height / skin.bounds.size.y);
        }

        internal static GameObject CreateReplayFigure(float height, float lifetime)
        {
            if (!skin) LoadSkin(CombatVisuals.TextureShader);
            var mesh = Instantiate(skin); mesh.name = "XiaoheiGiantReplayPose";
            var uv = mesh.uv;
            var colors = new Color[mesh.vertexCount];
            for (int i = 0; i < colors.Length; i++)
            {
                float light = echoPalette.GetPixel(Mathf.Clamp((int)(uv[i].x * echoPalette.width), 0, echoPalette.width - 1), 0).grayscale;
                colors[i] = Color.Lerp(new Color(.28f,.38f,1.5f,.30f), new Color(.7f,1.8f,2.3f,.48f), light);
            }
            mesh.colors = colors;
            if (!echoCloth) echoCloth = CombatVisuals.Radiance("XiaoheiSpectralEcho");
            var figure = new GameObject("XiaoheiGiantReplayBody");
            float scale = height / mesh.bounds.size.y;
            figure.transform.localScale = Vector3.one * scale;
            figure.transform.localPosition = Vector3.down * mesh.bounds.min.y * scale;
            figure.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = figure.AddComponent<MeshRenderer>(); renderer.sharedMaterial = echoCloth;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            var cleanup = figure.AddComponent<XiaoheiEchoMesh>(); cleanup.mesh = mesh; cleanup.enabled = false;
            Destroy(figure, lifetime);
            return figure;
        }

        internal static GameObject CreateEcho(CharacterBody owner, Vector3 offset, float lifetime)
        {
            var view = Find(owner);
            if (!view || !view.target || view.rig == null || lifetime <= 0f) return null;
            var renderer = view.target;
            var mesh = new Mesh { name = "XiaoheiPoseSnapshot", indexFormat = skin.indexFormat };
            renderer.BakeMesh(mesh);
            if (!echoCloth) echoCloth = CombatVisuals.Radiance("XiaoheiSpectralEcho");
            // Preserve the authored silhouette and palette in an actually
            // transparent material; Standard's alpha alone remained opaque.
            var uv = mesh.uv;
            var colors = new Color[mesh.vertexCount];
            for (int i=0;i<colors.Length;i++)
            {
                var pigment=echoPalette.GetPixel(Mathf.Clamp((int)(uv[i].x*echoPalette.width),0,echoPalette.width-1),0);
                colors[i]=new Color(pigment.r,pigment.g,pigment.b,.22f);
            }
            mesh.colors=colors;
            var echo = new GameObject("XiaoheiEcho");
            echo.transform.SetPositionAndRotation(renderer.transform.position + offset, renderer.transform.rotation);
            echo.transform.localScale = renderer.transform.lossyScale;
            echo.AddComponent<MeshFilter>().sharedMesh = mesh;
            var display = echo.AddComponent<MeshRenderer>();
            display.sharedMaterial = echoCloth;
            display.shadowCastingMode = ShadowCastingMode.Off;
            display.receiveShadows = false;
            var fading = echo.AddComponent<XiaoheiEchoMesh>();
            fading.mesh = mesh; fading.Initialize(display, lifetime);
            Destroy(echo, lifetime);
            return echo;
        }
    }

    // Destroy the baked mesh on expiry, death or an early return to the blink anchor.
    public sealed class XiaoheiEchoMesh : MonoBehaviour
    {
        internal Mesh mesh;
        private MeshRenderer view;
        private float began, duration;
        private readonly MaterialPropertyBlock fade = new MaterialPropertyBlock();
        internal void Initialize(MeshRenderer renderer, float lifetime)
        { view=renderer; began=Time.time; duration=lifetime; }
        private void Update()
        {
            if(!view || duration<=0f)return;
            float age=Time.time-began;
            float alpha=Mathf.SmoothStep(0f,1f,age/.10f) *
                Mathf.SmoothStep(0f,1f,(duration-age)/Mathf.Min(.28f,duration*.3f));
            fade.SetColor("_TintColor",new Color(1f,1f,1f,alpha));
            view.SetPropertyBlock(fade);
        }
        private void OnDestroy() { if (mesh) Destroy(mesh); }
    }
}

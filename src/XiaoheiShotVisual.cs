using System;
using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.Rendering;

namespace RiftSurvivor
{
    internal enum XiaoheiShotKind { Pistol, Replay, Seeker }

    internal sealed class XiaoheiShotVisual : MonoBehaviour
    {
        [SerializeField] private XiaoheiShotKind kind;
        private Vector3 muzzle, endpoint, arc;
        private Transform target;
        private LineRenderer streak, core;
        private float began, duration, span;

        internal static GameObject Prefab(string name, XiaoheiShotKind kind)
        {
            var prefab = CombatVisuals.EffectPrefab(name);
            prefab.AddComponent<EffectComponent>().applyScale = false;
            var vfx = prefab.AddComponent<VFXAttributes>();
            vfx.DoNotPool = true;
            vfx.vfxPriority = VFXAttributes.VFXPriority.Always;
            prefab.AddComponent<XiaoheiShotVisual>().kind = kind;
            if (!ContentAddition.AddEffect(prefab)) throw new InvalidOperationException("Cannot register " + name);
            return prefab;
        }

        private void Start()
        {
            var data = GetComponent<EffectComponent>().effectData;
            muzzle = data.start;
            endpoint = data.origin;
            began = Time.time;
            var delta = endpoint - muzzle;
            float distance = delta.magnitude;
            if (kind == XiaoheiShotKind.Seeker)
            {
                // Only the locked orb has a travel time. A free shot already resolved its ray.
                bool homing = data.genericFloat > 0f;
                duration = homing ? data.genericFloat : Mathf.Clamp(distance / 380f, .035f, .095f);
                if (homing)
                {
                    var hurt = data.ResolveHurtBoxReference();
                    if (hurt) target = hurt.transform;
                    var right = Vector3.Cross(Vector3.up, delta.normalized);
                    if (right.sqrMagnitude < .01f) right = Vector3.right;
                    arc = (right.normalized * .55f + Vector3.up * .8f) * Mathf.Min(distance * .09f, 1.45f);
                }
                MoveRound(0f);
                XiaoheiRoundArt.Attach(transform, distance / duration);
            }
            else
            {
                bool replay = kind == XiaoheiShotKind.Replay;
                duration = Mathf.Clamp(distance / 360f, .12f, .20f);
                span = Mathf.Clamp(distance * .32f, .7f, replay ? 4.5f : 6.5f);
                streak = gameObject.AddComponent<LineRenderer>();
                streak.sharedMaterial = XiaoheiRoundArt.Glow;
                streak.useWorldSpace = true;
                streak.positionCount = 2;
                streak.startWidth = replay ? .065f : .075f;
                streak.endWidth = replay ? .16f : .19f;
                streak.startColor = replay ? new Color(.50f, .70f, .79f, .07f) : new Color(.16f, .84f, .92f, .22f);
                streak.endColor = replay ? new Color(.72f, .87f, .94f, .62f) : XiaoheiRoundArt.Core;
                streak.shadowCastingMode = ShadowCastingMode.Off;
                streak.receiveShadows = false;
                streak.numCapVertices = 4;
                var spine = new GameObject("ShotWhiteCore");
                spine.transform.SetParent(transform, false);
                core = spine.AddComponent<LineRenderer>();
                core.sharedMaterial = XiaoheiRoundArt.Glow;
                core.positionCount = 2;
                core.useWorldSpace = true;
                core.startWidth = replay ? .015f : .022f;
                core.endWidth = replay ? .038f : .046f;
                core.startColor = new Color(.75f,1f,.94f,.1f);
                core.endColor = new Color(.88f,1f,.97f,.95f);
                core.numCapVertices = 4;
                core.shadowCastingMode = ShadowCastingMode.Off;
                core.receiveShadows = false;
                DrawStreak(0f);
            }
        }

        private void LateUpdate()
        {
            float t = Mathf.Clamp01((Time.time - began) / duration);
            if (kind == XiaoheiShotKind.Seeker) MoveRound(t);
            else DrawStreak(t);
            if (t >= 1f) Destroy(gameObject);
        }

        private void MoveRound(float t)
        {
            if (target) endpoint = target.position;
            var direction = endpoint - muzzle + arc * (4f - 8f * t);
            transform.position = Vector3.Lerp(muzzle, endpoint, t) + arc * (4f * t * (1f - t));
            if (direction.sqrMagnitude > .000001f)
                transform.rotation = Util.QuaternionSafeLookRotation(direction);
        }

        private void DrawStreak(float t)
        {
            var delta = endpoint - muzzle;
            float distance = delta.magnitude;
            float tail = t * (distance + span) - span * .75f;
            var direction = delta.normalized;
            streak.SetPosition(0, muzzle + direction * Mathf.Clamp(tail, 0f, distance));
            streak.SetPosition(1, muzzle + direction * Mathf.Clamp(tail + span, 0f, distance));
            if (core)
            {
                core.SetPosition(0, streak.GetPosition(0));
                core.SetPosition(1, streak.GetPosition(1));
                float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.55f, 1f, t));
                var bright = core.endColor; bright.a = .95f * fade; core.endColor = bright;
                var halo = streak.endColor; halo.a = (kind == XiaoheiShotKind.Replay ? .55f : .85f) * fade; streak.endColor = halo;
            }
        }
    }
}

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
        private LineRenderer streak;
        private float began, duration, span;

        internal static GameObject Prefab(string name, XiaoheiShotKind kind)
        {
            var prefab = PrefabAPI.CreateEmptyPrefab(name, false);
            prefab.AddComponent<EffectComponent>().applyScale = false;
            var vfx = prefab.AddComponent<VFXAttributes>();
            vfx.DoNotPool = true;
            vfx.vfxPriority = kind == XiaoheiShotKind.Seeker ? VFXAttributes.VFXPriority.Always : VFXAttributes.VFXPriority.Medium;
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
                duration = Mathf.Clamp(distance / 480f, .045f, .11f);
                span = Mathf.Clamp(distance * .32f, .7f, replay ? 4.5f : 6.5f);
                streak = gameObject.AddComponent<LineRenderer>();
                streak.sharedMaterial = XiaoheiRoundArt.Glow;
                streak.useWorldSpace = true;
                streak.positionCount = 2;
                streak.startWidth = replay ? .014f : .021f;
                streak.endWidth = replay ? .041f : .068f;
                streak.startColor = replay ? new Color(.50f, .70f, .79f, .07f) : new Color(.16f, .84f, .92f, .22f);
                streak.endColor = replay ? new Color(.72f, .87f, .94f, .62f) : XiaoheiRoundArt.Core;
                streak.shadowCastingMode = ShadowCastingMode.Off;
                streak.receiveShadows = false;
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
        }
    }
}

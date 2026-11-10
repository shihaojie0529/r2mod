using UnityEngine;
using RoR2;

namespace RiftSurvivor
{
    internal static class JieBlink
    {
        private const float Skin = .025f;
        internal static bool TryDestination(CharacterMotor motor, Vector3 direction, float distance, out Vector3 destination)
        {
            destination = motor ? motor.transform.position : Vector3.zero;
            if (!motor || !motor.Motor || !motor.Motor.Capsule ||
                !(direction.sqrMagnitude > .0001f) || float.IsInfinity(direction.sqrMagnitude) ||
                !(distance > 0f) || float.IsInfinity(distance)) return false;
            direction.Normalize();
            var capsule = motor.Motor.Capsule;
            var scale = capsule.transform.lossyScale;
            float radius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float axis = Mathf.Max(0f, capsule.height * Mathf.Abs(scale.y) * .5f - radius);
            var center = capsule.transform.TransformPoint(capsule.center);
            var top = center + capsule.transform.up * axis;
            var bottom = center - capsule.transform.up * axis;
            float probe = Mathf.Max(.01f, radius - Skin);
            if (Physics.CheckCapsule(top, bottom, probe, LayerIndex.world.mask, QueryTriggerInteraction.Ignore)) return false;
            if (Physics.CapsuleCast(top, bottom, probe, direction, out var hit, distance + Skin,
                LayerIndex.world.mask, QueryTriggerInteraction.Ignore)) distance = Mathf.Min(distance, hit.distance - Skin);
            if (distance < .05f) return false;
            var delta = direction * distance;
            if (Physics.CheckCapsule(top + delta, bottom + delta, probe, LayerIndex.world.mask,
                QueryTriggerInteraction.Ignore)) return false;
            destination += delta;
            return true;
        }
    }
}

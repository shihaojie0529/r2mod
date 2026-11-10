using System.Collections.Generic;
using EntityStates;
using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace RiftSurvivor
{
    public sealed class WeiPortalCast : BaseSkillState
    {
        private Vector3 direction;
        public override void OnEnter()
        {
            base.OnEnter();
            if (direction.sqrMagnitude < .5f) direction = GetAimRay().direction;
            if (!NetworkServer.active || !healthComponent || !healthComponent.alive || !skillLocator) return;
            var slot = skillLocator.utility;
            if (!slot || slot.stateMachine != outer || slot.activationState.stateType != typeof(WeiPortalCast)) return;
            var portal = GetComponent<WeiPortal>();
            if (portal) portal.Open(characterBody, direction);
        }

        public override void OnSerialize(NetworkWriter writer) { base.OnSerialize(writer); writer.Write(direction); }
        public override void OnDeserialize(NetworkReader reader)
        {
            base.OnDeserialize(reader);
            var vector = reader.ReadVector3();
            if (vector.sqrMagnitude > .8f && vector.sqrMagnitude < 1.2f) direction = vector.normalized;
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (isAuthority && fixedAge >= .08f) outer.SetNextStateToMain();
        }
    }

    public sealed class WeiPortal : MonoBehaviour
    {
        private static GameObject pairPrefab;
        internal static readonly List<WeiDoorPair> Pairs = new List<WeiDoorPair>();
        private const float Skin = .035f;
        internal static void Register()
        {
            pairPrefab = PrefabAPI.CreateEmptyPrefab("WeiLinkedDoors", true);
            pairPrefab.AddComponent<WeiDoorPair>();
        }
        internal static void Install(GameObject body) => body.AddComponent<WeiPortal>();

        internal void Open(CharacterBody owner, Vector3 direction)
        {
            if (!owner || !owner.characterMotor || !owner.teamComponent) return;
            direction.Normalize();
            if (direction.sqrMagnitude < .5f) return;
            Capsule(owner, out var top, out var bottom, out float radius);
            float distance = 20f;
            if (Physics.CapsuleCast(top, bottom, radius, direction, out var hit, distance + Skin,
                    LayerIndex.world.mask, QueryTriggerInteraction.Ignore)) distance = Mathf.Min(distance, hit.distance - Skin);
            var displacement = direction * distance;
            if (distance <= .10f || Blocked(owner, displacement) ||
                !CuiServerCharges.TrySpend(owner, owner.skillLocator.utility, false)) return;
            var pair = Instantiate(pairPrefab, owner.corePosition, Quaternion.identity).GetComponent<WeiDoorPair>();
            pair.Initialize(owner, owner.corePosition, owner.corePosition + displacement, direction);
            pair.Move(owner, owner.transform.position + displacement, true);
            NetworkServer.Spawn(pair.gameObject);
            WeiVitals.OnDoorOpened(owner);
        }

        private static void Capsule(CharacterBody body, out Vector3 top, out Vector3 bottom, out float probe)
        {
            var motor = body.characterMotor;
            var collider = motor && motor.Motor ? motor.Motor.Capsule : null;
            if (!collider)
            {
                // Fly/rigidbody survivors can also be passengers.
                probe = Mathf.Max(.08f, body.radius - Skin);
                top = bottom = body.corePosition;
                return;
            }
            var scale = collider.transform.lossyScale;
            float radius = collider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float halfAxis = Mathf.Max(0f, .5f * collider.height * Mathf.Abs(scale.y) - radius);
            var center = collider.transform.TransformPoint(collider.center);
            var offset = collider.transform.up * halfAxis;
            top = center + offset;
            bottom = center - offset;
            probe = Mathf.Max(.05f, radius - Skin);
        }

        internal static bool Blocked(CharacterBody body, Vector3 displacement)
        {
            Capsule(body, out var top, out var bottom, out float radius);
            return Physics.CheckCapsule(top + displacement, bottom + displacement, radius,
                LayerIndex.world.mask, QueryTriggerInteraction.Ignore);
        }

        internal static bool TryAim(CharacterBody owner, Ray aim, out Vector3 entry)
        {
            entry = default;
            float nearest = 60f;
            if (Physics.Raycast(aim, out var wall, nearest, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
                nearest = wall.distance;
            if (!Closest(owner, aim, nearest, out var pair, out bool atFirst, out _)) return false;
            entry = atFirst ? pair.first : pair.second;
            return true;
        }

        // Called before the actor chooses its first contact (wall, hurtbox or doorway).
        // Only its caller commits blockedUntil after that comparison chooses this portal.
        internal static bool TryTransit(CharacterBody owner, Vector3 from, Vector3 to, ref float blockedUntil,
            out Vector3 arrival, out Vector3 exitHeading, out float toEntryDistance)
        {
            arrival = exitHeading = default;
            toEntryDistance = float.PositiveInfinity;
            var delta = to - from;
            float length = delta.magnitude;
            if (Time.fixedTime < blockedUntil || length < .0001f ||
                !Closest(owner, new Ray(from, delta / length), length, out var pair, out bool atFirst, out toEntryDistance))
                return false;
            exitHeading = atFirst ? pair.axis : -pair.axis;
            arrival = (atFirst ? pair.second : pair.first) + exitHeading * .03f;
            return true;
        }

        private static bool Closest(CharacterBody owner, Ray ray, float maximum, out WeiDoorPair best,
            out bool first, out float nearest)
        {
            best = null;
            first = false;
            nearest = maximum;
            if (!owner || !owner.teamComponent) return false;
            foreach (var pair in Pairs)
            {
                if (!pair || !pair.Allows(owner)) continue;
                if (!pair.Crosses(ray, nearest, out bool a, out float distance)) continue;
                best = pair;
                first = a;
                nearest = distance;
            }
            return best;
        }
    }
}

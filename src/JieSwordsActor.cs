using System;
using System.Collections.Generic;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace RiftSurvivor
{
    // A single server-owned identity lives through flight, embedding and recall.
    // Joined-in-progress clients receive its current pose and attachment socket.
    public sealed class JieAzureSword : NetworkBehaviour
    {
        private enum Phase : byte { Outbound, Attached, Planted, Returning, Retired }
        private const float Radius = .085f, OutSpeed = 47f, ReturnSpeed = 68f;
        private const float SendInterval = .05f;
        private Phase phase;
        private JieSwords rack;
        private CharacterBody owner;
        private HurtBox target, attachment, recallAttachment;
        internal HealthComponent AttachedHealth { get; private set; }
        private bool hadLock, crit, frozen;
        private float flightTime;
        private Vector3 socketPoint, socketForward, destination;
        private short socketIndex;
        private NetworkInstanceId socketBody;
        private HashSet<HealthComponent> returnedHits;
        private readonly Collider[] overlapBuffer = new Collider[12];
        private readonly RaycastHit[] castBuffer = new RaycastHit[24];

        private Vector3 fromSample, lastSample;
        private float sampleAt;

        internal void Launch(JieSwords source, CharacterBody caster, HurtBox lockTarget)
        {
            rack = source;
            owner = caster;
            target = lockTarget;
            hadLock = target;
            crit = owner.RollCrit();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            JieSwordArt.Attach(transform, new Color(.18f, .92f, .81f));
        }

        public override float GetNetworkSendInterval() => SendInterval;

        public override bool OnSerialize(NetworkWriter writer, bool initialState)
        {
            writer.Write((byte)phase);
            writer.Write(transform.position);
            writer.Write(transform.forward);
            if (phase == Phase.Attached)
            {
                writer.Write(socketBody);
                writer.Write(socketIndex);
                writer.Write(socketPoint);
                writer.Write(socketForward);
            }
            return true;
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            var previous = phase;
            phase = (Phase)reader.ReadByte();
            fromSample = transform.position;
            lastSample = reader.ReadVector3();
            transform.rotation = Util.QuaternionSafeLookRotation(reader.ReadVector3());
            sampleAt = Time.time;
            if (initialState || phase != previous || phase == Phase.Planted)
                fromSample = transform.position = lastSample;
            if (phase == Phase.Attached)
            {
                socketBody = reader.ReadNetworkId();
                socketIndex = reader.ReadInt16();
                socketPoint = reader.ReadVector3();
                socketForward = reader.ReadVector3();
                attachment = null;
            }
        }

        private void LateUpdate()
        {
            if (NetworkServer.active) return;
            if (phase == Phase.Attached)
            {
                if (!attachment)
                {
                    var host = ClientScene.FindLocalObject(socketBody);
                    var body = host ? host.GetComponent<CharacterBody>() : null;
                    var group = body ? body.hurtBoxGroup : null;
                    if (group && socketIndex >= 0 && socketIndex < group.hurtBoxes.Length) attachment = group.hurtBoxes[socketIndex];
                }
                if (attachment && attachment.healthComponent && attachment.healthComponent.alive) FollowAttachment();
            }
            else if (phase == Phase.Outbound || phase == Phase.Returning)
                transform.position = Vector3.Lerp(fromSample, lastSample, (Time.time - sampleAt) / SendInterval);
        }

        private void FixedUpdate()
        {
            if (!NetworkServer.active || phase == Phase.Retired) return;
            if (!owner || !owner.healthComponent || !owner.healthComponent.alive) { Retire(); return; }
            if (phase == Phase.Attached)
            {
                if (attachment && AttachedHealth && AttachedHealth.alive) FollowAttachment();
                else
                {
                    if (attachment && !frozen) FollowAttachment();
                    attachment = null;
                    AttachedHealth = null;
                    phase = Phase.Planted;
                    SetDirtyBit(1);
                }
                return;
            }
            if (phase == Phase.Planted) return;

            Vector3 origin = transform.position;
            bool returning = phase == Phase.Returning;
            Vector3 direction = returning ? (destination - origin).normalized : TurnToTarget(origin);
            float step = returning ? Mathf.Min(ReturnSpeed * Time.fixedDeltaTime, Vector3.Distance(origin, destination))
                : OutSpeed * Time.fixedDeltaTime;
            if (direction.sqrMagnitude > .5f) transform.rotation = Util.QuaternionSafeLookRotation(direction);
            else direction = transform.forward;
            Advance(origin, direction, step, returning);

            if (phase == Phase.Outbound)
            {
                flightTime += Time.fixedDeltaTime;
                // A sword fired into empty sky never occupies a planted slot.
                if (flightTime >= 4f) Retire();
            }
            if (phase == Phase.Returning && (transform.position - destination).sqrMagnitude < .00001f) Retire();
        }

        private Vector3 TurnToTarget(Vector3 origin)
        {
            var heading = transform.forward;
            if (hadLock && !XiaoheiBallistics.IsLiveEnemy(owner, target))
            {
                target = JieSwords.FindTarget(owner, new Ray(origin, heading), true);
                hadLock = target;
            }
            if (!target) return heading;
            var toTarget = target.transform.position - origin;
            if (toTarget.sqrMagnitude < .0001f) return heading;
            // Tighten the turn close to the target to avoid orbiting a small hurtbox.
            return toTarget.sqrMagnitude < 9f ? toTarget.normalized :
                Vector3.RotateTowards(heading, toTarget.normalized, 13f * Time.fixedDeltaTime, 0f).normalized;
        }

        private void Advance(Vector3 origin, Vector3 heading, float step, bool returning)
        {
            var ray = new Ray(origin, heading);
            bool inWall = Physics.CheckSphere(origin, .008f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore);
            if (inWall)
            {
                if (returning) Retire(); else Plant(null, origin, heading);
                return;
            }
            // A sphere already touching a wall may have no entry hit. The center
            // ray still catches the wall if a sword starts near it and recalls inward.
            bool hullHit = Physics.SphereCast(ray, Radius, out var hull, step, LayerIndex.world.mask, QueryTriggerInteraction.Ignore);
            bool rayHit = Physics.Raycast(ray, out var pointHit, step, LayerIndex.world.mask, QueryTriggerInteraction.Ignore);
            bool wall = hullHit || rayHit;
            float travel = Mathf.Min(hullHit ? hull.distance : step, rayHit ? pointHit.distance : step);
            if (wall) travel = Mathf.Max(0f, travel - .005f);
            int mask = LayerIndex.entityPrecise.mask;
            // A sweep alone misses colliders already touching its starting point.
            var overlaps = overlapBuffer;
            int count = Physics.OverlapSphereNonAlloc(origin, Radius, overlaps, mask, QueryTriggerInteraction.Collide);
            if (count == overlaps.Length) { overlaps = Physics.OverlapSphere(origin, Radius, mask, QueryTriggerInteraction.Collide); count = overlaps.Length; }
            for (int i = 0; i < count; i++)
            {
                var collider = overlaps[i];
                if (Contact(collider, collider.ClosestPoint(origin), origin, heading, returning)) return;
            }
            if (travel > .00001f)
            {
                var casts = castBuffer;
                count = Physics.SphereCastNonAlloc(ray, Radius, casts, travel, mask, QueryTriggerInteraction.Collide);
                if (count == casts.Length) { casts = Physics.SphereCastAll(ray, Radius, travel, mask, QueryTriggerInteraction.Collide); count = casts.Length; }
                Array.Sort(casts, 0, count, DistanceComparer.Instance);
                for (int i = 0; i < count; i++)
                {
                    var hit = casts[i];
                    var point = hit.distance > .00001f ? hit.point : hit.collider.ClosestPoint(origin);
                    if (Contact(hit.collider, point, origin, heading, returning)) return;
                }
            }
            transform.position = origin + heading * travel;
            if (wall) { if (returning) Retire(); else Plant(null, transform.position, heading); }
            else SetDirtyBit(1);
        }

        private bool Contact(Collider collider, Vector3 point, Vector3 origin, Vector3 heading, bool returning)
        {
            var hurt = collider ? collider.GetComponent<HurtBox>() : null;
            if (!XiaoheiBallistics.IsLiveEnemy(owner, hurt) ||
                Physics.Linecast(origin, point, LayerIndex.world.mask, QueryTriggerInteraction.Ignore)) return false;
            if (returning) { ReturnHit(hurt, point); return false; }
            Plant(hurt, point, heading);
            WeiBonds.DirectHit(owner, hurt.healthComponent, point, DinnerStrike.Sword);
            XiaoheiBallistics.HitTarget(owner, hurt.healthComponent, hurt, owner.damage * 1.6f, crit, DamageTypeCombo.GenericPrimary);
            HitFlash(point, heading);
            return true;
        }

        private void Plant(HurtBox hurt, Vector3 point, Vector3 flight)
        {
            int fan = rack.Plant(this);
            // Fan out the hilts when consecutive swords hit exactly the same socket.
            Vector3 right = Vector3.Cross(Mathf.Abs(flight.y) > .95f ? Vector3.forward : Vector3.up, flight).normalized;
            float angle = fan * (2f * Mathf.PI / JieSwords.Capacity);
            Vector3 side = right * Mathf.Cos(angle) + Vector3.Cross(flight, right) * Mathf.Sin(angle);
            Vector3 embedded = (flight + .55f * side).normalized;
            transform.SetPositionAndRotation(point, Util.QuaternionSafeLookRotation(embedded));
            phase = hurt ? Phase.Attached : Phase.Planted;
            attachment = hurt;
            if (hurt)
            {
                AttachedHealth = hurt.healthComponent;
                socketBody = AttachedHealth.body.netId;
                socketIndex = hurt.indexInGroup;
                socketPoint = hurt.transform.InverseTransformPoint(point);
                socketForward = hurt.transform.InverseTransformDirection(embedded);
            }
            SetDirtyBit(1);
        }

        private void FollowAttachment() => transform.SetPositionAndRotation(
            attachment.transform.TransformPoint(socketPoint),
            Util.QuaternionSafeLookRotation(attachment.transform.TransformDirection(socketForward)));

        internal void FreezeAtDeath(HealthComponent victim)
        {
            if (phase != Phase.Attached || AttachedHealth != victim || !attachment) return;
            FollowAttachment();
            frozen = true;
        }

        internal void BeginReturn(Vector3 endpoint)
        {
            if (phase == Phase.Attached && attachment && !frozen) FollowAttachment();
            recallAttachment = attachment;
            attachment = null;
            AttachedHealth = null;
            destination = endpoint;
            returnedHits = new HashSet<HealthComponent>();
            crit = owner.RollCrit();
            phase = Phase.Returning;
            var delta = destination - transform.position;
            if (delta.sqrMagnitude > .00001f) transform.rotation = Util.QuaternionSafeLookRotation(delta);
            SetDirtyBit(1);
        }

        internal void HitAttachment()
        {
            if (XiaoheiBallistics.IsLiveEnemy(owner, recallAttachment)) ReturnHit(recallAttachment, transform.position);
            recallAttachment = null;
        }

        private void ReturnHit(HurtBox hurt, Vector3 point)
        {
            var victim = hurt.healthComponent;
            if (!returnedHits.Add(victim)) return;
            WeiBonds.DirectHit(owner, victim, point, DinnerStrike.Sword);
            XiaoheiBallistics.HitTarget(owner, victim, hurt, owner.damage * 1.8f, crit, DamageTypeCombo.GenericSecondary);
            JieBonds.OnRecallHit(owner, victim, point);
            HitFlash(point, transform.forward);
        }

        private static void HitFlash(Vector3 point, Vector3 heading)
        {
            if (JieSwords.Impact) EffectManager.SimpleImpactEffect(JieSwords.Impact, point, heading, true);
        }

        internal void Retire()
        {
            if (phase == Phase.Retired) return;
            phase = Phase.Retired;
            if (rack) rack.Forget(this);
            NetworkServer.Destroy(gameObject);
        }

        private void OnDestroy() { if (NetworkServer.active && rack) rack.Forget(this); }

        private sealed class DistanceComparer : IComparer<RaycastHit>
        {
            internal static readonly DistanceComparer Instance = new DistanceComparer();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }
    }
}

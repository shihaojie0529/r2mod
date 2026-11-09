using System.Collections.Generic;
using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;

namespace RiftSurvivor
{
    // The linked frames have one reliable identity. Spawn data gives everyone the
    // same geometry; later messages deliver accepted warps to the owning client.
    public sealed class WeiDoorPair : NetworkBehaviour
    {
        private const float Lifetime = 3f;
        private static readonly Bounds Aperture = new Bounds(Vector3.zero, new Vector3(1.95f, 2.55f, .24f));
        private static readonly Bounds Release = new Bounds(Vector3.zero, new Vector3(2.17f, 2.78f, 1.02f));
        private static Material frameMaterial;
        internal Vector3 first, second, axis;
        private Quaternion inverse;
        private TeamIndex team;
        private NetworkInstanceId maker;
        private CharacterBody owner;
        private float ends;
        private bool bondUsed;
        private struct Warp { internal NetworkInstanceId id; internal Vector3 root; internal bool opening; }
        private sealed class Visitor { internal Vector3 previous; internal bool armed; }
        private readonly List<Warp> warps = new List<Warp>();
        private readonly Dictionary<CharacterBody, Visitor> visits = new Dictionary<CharacterBody, Visitor>();
        private int delivered;
        private int lastWarpFrame = -1;

        internal void Initialize(CharacterBody source, Vector3 a, Vector3 b, Vector3 direction)
        {
            owner = source;
            team = owner.teamComponent.teamIndex;
            maker = owner.netId;
            first = a;
            second = b;
            axis = direction;
            ends = Run.FixedTimeStamp.now.t + Lifetime;
            Orient();
        }

        private void Orient()
        {
            var up = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > .97f ? Vector3.forward : Vector3.up;
            inverse = Quaternion.Inverse(Quaternion.LookRotation(axis, up));
        }

        private void Track() { if (!WeiPortal.Pairs.Contains(this)) WeiPortal.Pairs.Add(this); }
        public override void OnStartServer() { base.OnStartServer(); Track(); }
        public override void OnStartClient()
        {
            base.OnStartClient();
            Track();
            Frame(first, new Color(.16f, .90f, .96f));
            Frame(second, new Color(1f, .68f, .22f));
            if (!NetworkServer.active && ClientScene.FindLocalObject(maker)) Deliver();
        }
        private void OnDestroy() => WeiPortal.Pairs.Remove(this);
        internal bool Allows(CharacterBody traveler) => traveler.teamComponent.teamIndex == team && Run.FixedTimeStamp.now.t < ends;

        public override float GetNetworkSendInterval() => 0f;
        public override bool OnSerialize(NetworkWriter writer, bool initialState)
        {
            if (initialState)
            {
                writer.Write(maker); writer.Write((int)team);
                writer.Write(first); writer.Write(second); writer.Write(axis); writer.Write(ends);
            }
            writer.Write((uint)warps.Count);
            foreach (var warp in warps) { writer.Write(warp.id); writer.Write(warp.root); writer.Write(warp.opening); }
            return true;
        }
        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            if (initialState)
            {
                maker = reader.ReadNetworkId();
                team = (TeamIndex)reader.ReadInt32();
                first = reader.ReadVector3(); second = reader.ReadVector3(); axis = reader.ReadVector3();
                ends = reader.ReadSingle();
                Orient();
            }
            uint entries = reader.ReadUInt32();
            warps.Clear();
            for (int i = 0; i < entries; ++i)
                warps.Add(new Warp { id = reader.ReadNetworkId(), root = reader.ReadVector3(), opening = reader.ReadBoolean() });
            // A final reliable update and the despawn can arrive in the same frame.
            if (!initialState && !NetworkServer.active) Deliver();
        }

        internal bool Crosses(Ray world, float max, out bool atFirst, out float distance)
        {
            bool a = Cross(first, world, max, out float da);
            bool b = Cross(second, world, max, out float db);
            atFirst = a && (!b || da <= db);
            distance = atFirst ? da : db;
            return a || b;
        }
        private bool Cross(Vector3 center, Ray world, float max, out float distance)
        {
            var ray = new Ray(inverse * (world.origin - center), inverse * world.direction);
            distance = 0f;
            return !Aperture.Contains(ray.origin) && Aperture.IntersectRay(ray, out distance) && distance <= max;
        }
        private bool Outside(Vector3 position) => !Release.Contains(inverse * (position - first)) &&
                                                   !Release.Contains(inverse * (position - second));

        private void FixedUpdate()
        {
            if (!NetworkServer.active) { Deliver(); return; }
            if (!owner || Run.FixedTimeStamp.now.t >= ends)
            {
                // Two physics ticks can precede the next network send. Leave the
                // last accepted warp alive for that send, without accepting new crossings.
                if (lastWarpFrame != Time.frameCount) NetworkServer.Destroy(gameObject);
                return;
            }
            foreach (var member in TeamComponent.GetTeamMembers(team))
            {
                var body = member.body;
                if (!body || !body.isPlayerControlled || !body.healthComponent || !body.healthComponent.alive) continue;
                var here = body.corePosition;
                if (!visits.TryGetValue(body, out var visit))
                {
                    visits.Add(body, new Visitor { previous = here, armed = Outside(here) });
                    continue;
                }
                var delta = here - visit.previous;
                var previous = visit.previous;
                visit.previous = here;
                if (!visit.armed) { visit.armed = Outside(here); continue; }
                float length = delta.magnitude;
                if (length < .0001f || length > 12f || !Crosses(new Ray(previous, delta / length), length,
                        out bool atFirst, out _)) continue;
                // Preserve the lateral and height offset through the aperture. A
                // taller teammate's boots must not end up below the floor.
                var entryCenter = atFirst ? first : second;
                var destination = (atFirst ? second : first) + Vector3.ProjectOnPlane(here - entryCenter, axis);
                var displacement = destination - here;
                if (WeiPortal.Blocked(body, displacement)) continue;
                Move(body, body.transform.position + displacement, false);
            }
        }

        internal void Move(CharacterBody body, Vector3 root, bool opening)
        {
            visits[body] = new Visitor { previous = body.corePosition + root - body.transform.position, armed = false };
            WeiVitals.ResetWalkSample(body);
            ShoulderCharge.BeforePortalWarp(body, root);
            if (body.hasEffectiveAuthority) TeleportHelper.TeleportGameObject(body.gameObject, root);
            else
            {
                lastWarpFrame = Time.frameCount;
                // Mirror immediately on the server for held monsters and collisions.
                // The reliable pair record instructs the remote native motor to commit.
                var motor = body.characterMotor;
                if (motor && motor.Motor) motor.Motor.SetPosition(root, true);
                else body.transform.position = root;
            }
            WeiCombat.UpdateHeldPose(body);
            warps.Add(new Warp { id = body.netId, root = root, opening = opening });
            if (!opening)
            {
                SetDirtyBit(1);
                if (!bondUsed && body.GetComponent<CuiVitals>())
                {
                    bondUsed = true;
                    WeiBonds.OnCuiThroughDoor(owner, body);
                }
                if (body.GetComponent<XiaoheiCombat>() && Random.value < .25f)
                    Chat.SendBroadcastChat(new Chat.SimpleChatMessage { baseToken = "WEIGE_SHOES" });
            }
        }
        private void Deliver()
        {
            while (delivered < warps.Count)
            {
                var warp = warps[delivered++];
                var target = ClientScene.FindLocalObject(warp.id);
                var body = target ? target.GetComponent<CharacterBody>() : null;
                if (!body || !body.hasEffectiveAuthority) continue;
                if (warp.opening && warp.id == maker)
                {
                    var slot = body.skillLocator.utility;
                    if (slot && slot.activationState.stateType == typeof(WeiPortalCast)) slot.DeductStock(1);
                }
                TeleportHelper.TeleportGameObject(body.gameObject, warp.root);
            }
        }

        private void Frame(Vector3 center, Color color)
        {
            var root = new GameObject("WeiDoorFrame").transform;
            root.SetParent(transform, false);
            root.SetPositionAndRotation(center, Quaternion.Inverse(inverse));
            var glow = root.gameObject.AddComponent<RadiantGeometry>();
            glow.Arc("PortalOuterRune", new Color(color.r,color.g,color.b,.6f), 1.65f, .05f, 20f, 140f);
            glow.Arc("PortalReturnRune", new Color(color.r,color.g,color.b,.6f), 1.65f, .05f, 200f, 140f);
            glow.Arc("PortalInnerRune", new Color(1f,1f,1f,.45f), 1.51f, .018f, 45f, 90f);
            root.gameObject.AddComponent<PortalFramePulse>();
            var frame = Stroke(root, color, .065f);
            frame.loop = true;
            frame.positionCount = 8;
            frame.SetPositions(new[] { new Vector3(-.99f,-1.26f,0), new Vector3(.99f,-1.26f,0),
                new Vector3(1.05f,-1.2f,0), new Vector3(1.05f,1.14f,0), new Vector3(.92f,1.27f,0),
                new Vector3(-.92f,1.27f,0), new Vector3(-1.05f,1.14f,0), new Vector3(-1.05f,-1.20f,0) });
            var handle = Stroke(root, color, .085f);
            handle.positionCount = 2;
            handle.SetPositions(new[] { new Vector3(.77f,-.17f,0), new Vector3(.77f,.16f,0) });
        }
        private static LineRenderer Stroke(Transform parent, Color color, float width)
        {
            var line = new GameObject("DoorLight").AddComponent<LineRenderer>();
            line.transform.SetParent(parent, false);
            if (!frameMaterial) frameMaterial = CombatVisuals.Stroke("WeiPortalSoftFrame");
            line.sharedMaterial = frameMaterial;
            line.useWorldSpace = false;
            line.startWidth = line.endWidth = width;
            line.startColor = line.endColor = color;
            line.numCornerVertices = 4; line.numCapVertices = 4;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }
    }
}

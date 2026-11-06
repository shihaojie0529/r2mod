using System;
using System.Collections.Generic;
using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace RiftSurvivor
{
    [DisallowMultipleComponent]
    public sealed class WeiVitals : MonoBehaviour
    {
        private const int MaxStacks = 3;
        private static BuffDef fatBurn, doorReceipt;
        private CharacterBody body;
        private Vector3 lastPosition, lastIntent;
        private bool wasGrounded;
        private float sampledAt, ignoreUntil, meters;
        private int lastDoor;
        private float pulseAt = float.NegativeInfinity;
        private readonly Dictionary<uint, float> installationReady = new Dictionary<uint, float>();

        internal static void Register()
        {
            fatBurn = CreateBuff("WeiFatBurn", false);
            doorReceipt = CreateBuff("WeiWatchDoorReceipt", true);
        }

        private static BuffDef CreateBuff(string name, bool hidden)
        {
            var buff = ScriptableObject.CreateInstance<BuffDef>();
            buff.name = name;
            buff.canStack = true;
            buff.isHidden = buff.ignoreGrowthNectar = hidden;
            buff.iconSprite = GymIcons.Get("wei-steps");
            buff.buffColor = new Color32(244, 176, 83, 255);
            if (!ContentAddition.AddBuffDef(buff)) throw new InvalidOperationException("Cannot register " + name);
            return buff;
        }

        private void Awake() => body = GetComponent<CharacterBody>();
        private void Start()
        {
            lastPosition = transform.position;
            sampledAt = Time.fixedTime;
            lastDoor = body.GetBuffCount(doorReceipt);
        }

        internal static int Stacks(CharacterBody owner) => owner && fatBurn ? owner.GetBuffCount(fatBurn) : 0;
        internal static float DoorPulse(CharacterBody owner)
        {
            var vitals = owner ? owner.GetComponent<WeiVitals>() : null;
            return vitals ? Mathf.Clamp01(1f - (Time.time - vitals.pulseAt) / .55f) : 0f;
        }

        internal static bool TrySpend(CharacterBody owner)
        {
            if (!NetworkServer.active || !owner || !owner.healthComponent.alive) return false;
            int count = Stacks(owner);
            if (count <= 0) return false;
            owner.SetBuffCount(fatBurn.buffIndex, count - 1);
            return true;
        }

        internal static void GrantStack(CharacterBody owner)
        {
            if (NetworkServer.active && owner && owner.healthComponent.alive)
                owner.SetBuffCount(fatBurn.buffIndex, Mathf.Min(MaxStacks, Stacks(owner) + 1));
        }

        internal bool ClaimInstallation(CharacterBody swordsman)
        {
            uint partner = swordsman.master ? swordsman.master.netId.Value : swordsman.netId.Value;
            if (installationReady.TryGetValue(partner, out float ready) && Time.fixedTime < ready) return false;
            installationReady[partner] = Time.fixedTime + 4f;
            return true;
        }

        internal static void OnDoorOpened(CharacterBody owner)
        {
            if (!NetworkServer.active || !owner) return;
            var vitals = owner.GetComponent<WeiVitals>();
            if (!vitals) return;
            vitals.AddDistance(1f);
            owner.SetBuffCount(doorReceipt.buffIndex, owner.GetBuffCount(doorReceipt) % 127 + 1);
        }

        internal static void ResetWalkSample(CharacterBody traveler)
        {
            var vitals = traveler ? traveler.GetComponent<WeiVitals>() : null;
            if (!vitals) return;
            vitals.wasGrounded = false;
            vitals.lastIntent = Vector3.zero;
            vitals.lastPosition = traveler.transform.position;
            vitals.sampledAt = vitals.ignoreUntil = Time.fixedTime;
        }

        private void AddDistance(float distance)
        {
            meters += distance;
            int earned = Mathf.FloorToInt(meters / 20f);
            if (earned <= 0) return;
            meters -= earned * 20f;
            body.SetBuffCount(fatBurn.buffIndex, Mathf.Min(MaxStacks, Stacks(body) + earned));
        }

        private void FixedUpdate()
        {
            if (!body || !body.healthComponent || !body.healthComponent.alive) return;
            int receipt = body.GetBuffCount(doorReceipt);
            if (receipt != lastDoor)
            {
                lastDoor = receipt;
                pulseAt = Time.time;
                if (NetworkClient.active && body.isPlayerControlled && body.hasEffectiveAuthority)
                    Chat.AddMessage(Language.GetString("WEIGE_DOOR_STEP"));
            }
            if (!NetworkServer.active) return;
            var motor = body.characterMotor;
            var input = body.inputBank;
            var position = transform.position;
            bool onGround = motor && (body.hasEffectiveAuthority ? motor.isGrounded : motor.netIsGrounded);
            var intent = input ? input.moveVector : Vector3.zero;
            // Account for the motor's last step on the frame the player releases W.
            var stepDirection = intent.sqrMagnitude > .001f ? intent : lastIntent;
            bool moving = onGround && wasGrounded && stepDirection.sqrMagnitude > .001f && Time.fixedTime > ignoreUntil;
            var offset = position - lastPosition;
            if (moving && offset.sqrMagnitude > .000001f)
            {
                float dt = Mathf.Clamp(Time.fixedTime - sampledAt, Time.fixedDeltaTime, .25f);
                float distance = offset.magnitude;
                if (distance <= Mathf.Max(1f, body.moveSpeed) * dt * 1.65f + .035f)
                {
                    // Only player-directed travel counts, including the uphill
                    // component of walking a slope. Do not credit lateral knockback.
                    var normal = motor.estimatedGroundNormal;
                    if (normal.sqrMagnitude < .5f) normal = Vector3.up;
                    var stride = Vector3.ProjectOnPlane(stepDirection, normal).normalized;
                    AddDistance(Mathf.Max(0f, Vector3.Dot(offset, stride)));
                }
            }
            if (!moving || offset.sqrMagnitude > .000001f) sampledAt = Time.fixedTime;
            lastPosition = position;
            lastIntent = intent;
            wasGrounded = onGround && Time.fixedTime > ignoreUntil;
        }
    }
}

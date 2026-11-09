using System;
using EntityStates;
using R2API;
using RoR2;
using RoR2.Skills;
using UnityEngine;
using UnityEngine.Networking;

namespace RiftSurvivor
{
    public sealed class XiaoheiBlinkSkillDef : SkillDef
    {
        // Native stock cost is zero so the same key can recall an empty slot.
        // Charge only after the gesture resolves to a collision-checked forward blink.
        private static bool Available(GenericSkill slot)
        {
            if (slot.stock > 0) return true;
            var blink = slot.GetComponent<XiaoheiBlink>();
            return blink && blink.HasOrigin;
        }

        public override bool IsReady(GenericSkill slot) => Available(slot) && base.IsReady(slot);
        public override bool CanExecute(GenericSkill slot) => Available(slot) && base.CanExecute(slot);
    }

    public sealed class XiaoheiBlinkInput : BaseSkillState
    {
        private const float RecallHold = .25f;
        private XiaoheiBlink blink;
        private bool offeredRecall;

        public override void OnEnter()
        {
            base.OnEnter();
            blink = GetComponent<XiaoheiBlink>();
            offeredRecall = blink && blink.HasOrigin && characterBody.isPlayerControlled;
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (!isAuthority) return;
            bool held = IsKeyDownAuthority();
            if (offeredRecall && held && fixedAge < RecallHold) return;

            bool recall = offeredRecall && fixedAge >= RecallHold;
            if (blink && blink.TryTravel(recall, out var travel)) outer.SetNextState(travel);
            else outer.SetNextStateToMain();
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.PrioritySkill;
    }

    // This short state carries the actual departure pose location to every observer.
    // The owning motor alone moves and the native network transform replicates it.
    public sealed class XiaoheiBlinkTravel : BaseState
    {
        internal Vector3 departure, destination;
        internal bool recall, first;
        internal float started;

        public override void OnEnter()
        {
            base.OnEnter();
            if (!isAuthority)
            {
                var blink = GetComponent<XiaoheiBlink>();
                if (blink) blink.ShowTravel(this);
            }
            Util.PlaySound("Play_huntress_shift_mini_blink", gameObject);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (isAuthority) outer.SetNextStateToMain();
        }

        public override void OnSerialize(NetworkWriter writer)
        {
            base.OnSerialize(writer);
            writer.Write(departure);
            writer.Write(destination);
            writer.Write(recall);
            writer.Write(first);
            writer.Write(started);
        }

        public override void OnDeserialize(NetworkReader reader)
        {
            base.OnDeserialize(reader);
            departure = reader.ReadVector3();
            destination = reader.ReadVector3();
            recall = reader.ReadBoolean();
            first = reader.ReadBoolean();
            started = reader.ReadSingle();
        }
    }

    public sealed class XiaoheiBlink : MonoBehaviour
    {
        private const float OriginLifetime = 3f;
        private const float BlinkDistance = 12f;
        private const float SlideDistance = 6f;
        private const float SlideSpeed = 36f;
        private const float Skin = .025f;
        private static BuffDef shortenReceipt, chargeReceipt;
        private CharacterBody body;
        private GameObject echo;
        private Vector3 origin, slideDirection, slideEnd;
        private float expires, slideRemaining;
        private int lastShorten, lastCharge;

        internal bool HasOrigin => Time.fixedTime < expires;
        internal float OriginRemaining => Mathf.Max(0f, expires - Time.fixedTime);

        internal static void Register()
        {
            shortenReceipt = Receipt("XiaoheiBlinkShortened");
            chargeReceipt = Receipt("XiaoheiBlinkRestocked");
        }

        private static BuffDef Receipt(string name)
        {
            var buff = ScriptableObject.CreateInstance<BuffDef>();
            buff.name = name;
            buff.canStack = buff.isHidden = buff.ignoreGrowthNectar = true;
            if (!ContentAddition.AddBuffDef(buff)) throw new InvalidOperationException("Cannot register " + name);
            return buff;
        }

        private void Awake() => body = GetComponent<CharacterBody>();

        internal static void ShortenRecharge(CharacterBody recipient) => Reward(recipient, false);
        internal static void GrantCharge(CharacterBody recipient) => Reward(recipient, true);

        // Combat runs on the server, while the client's GenericSkill owns its recharge
        // clock. Replicated counters retain multiple rewards between two network updates.
        private static void Reward(CharacterBody recipient, bool wholeCharge)
        {
            if (!NetworkServer.active || !recipient || !recipient.GetComponent<XiaoheiBlink>()) return;
            if (recipient.hasEffectiveAuthority) ApplyReward(recipient.skillLocator.utility, wholeCharge);
            else
            {
                var receipt = wholeCharge ? chargeReceipt : shortenReceipt;
                recipient.SetBuffCount(receipt.buffIndex, recipient.GetBuffCount(receipt) % 127 + 1);
            }
        }

        private static void ApplyReward(GenericSkill slot, bool wholeCharge)
        {
            if (!slot || !(slot.skillDef is XiaoheiBlinkSkillDef) || slot.stock >= slot.maxStock) return;
            if (!wholeCharge && slot.rechargeStopwatch + 1f < slot.finalRechargeInterval)
            {
                slot.rechargeStopwatch += 1f;
                return;
            }
            // A three-hit streak shortens the current charge only. A cup grants a
            // stock outright and preserves the partial recharge on any remaining deficit.
            float partial = slot.rechargeStopwatch;
            slot.AddOneStock();
            slot.rechargeStopwatch = wholeCharge && slot.stock < slot.maxStock ? partial : 0f;
        }

        private void Receive(BuffDef receipt, ref int previous, bool wholeCharge)
        {
            int latest = body.GetBuffCount(receipt);
            int count = previous == 0 ? latest : (latest - previous + 127) % 127;
            previous = latest;
            if (latest == 0 || !body.hasEffectiveAuthority) return;
            for (int i = 0; i < count; i++) ApplyReward(body.skillLocator.utility, wholeCharge);
        }

        internal static void BeginSlide(CharacterBody source)
        {
            if (!source || !source.hasEffectiveAuthority || !source.inputBank || !source.characterMotor) return;
            var blink = source.GetComponent<XiaoheiBlink>();
            if (!blink || source.inputBank.moveVector.sqrMagnitude < .001f) return;
            blink.slideDirection = blink.TravelDirection(source.inputBank.moveVector);
            blink.slideEnd = source.transform.position + blink.slideDirection * SlideDistance;
            blink.slideRemaining = SlideDistance;
        }

        private Vector3 TravelDirection(Vector3 move)
        {
            var motor = body.characterMotor;
            if (motor.isGrounded && Mathf.Abs(move.y) < .05f)
            {
                var slope = Vector3.ProjectOnPlane(move, motor.estimatedGroundNormal);
                if (slope.sqrMagnitude > .001f) move = slope;
            }
            return move.normalized;
        }

        private void FixedUpdate()
        {
            if (!body || !body.healthComponent || !body.healthComponent.alive)
            {
                ClearOrigin();
                slideRemaining = 0f;
                return;
            }
            if (!NetworkServer.active)
            {
                Receive(shortenReceipt, ref lastShorten, false);
                Receive(chargeReceipt, ref lastCharge, true);
            }
            if (slideRemaining <= 0f || !body.hasEffectiveAuthority) return;
            // Walking during the gesture also advances toward its six-metre endpoint.
            slideRemaining = Mathf.Min(slideRemaining, Vector3.Dot(slideEnd - transform.position, slideDirection));
            if (slideRemaining <= 0f) return;
            float step = Mathf.Min(slideRemaining, SlideSpeed * Time.fixedDeltaTime);
            // KCC consumes root motion against terrain on its normal movement tick.
            body.characterMotor.rootMotion += slideDirection * step;
            slideRemaining -= step;
        }

        internal bool TryTravel(bool recall, out XiaoheiBlinkTravel travel)
        {
            travel = null;
            var motor = body.characterMotor;
            var slot = body.skillLocator.utility;
            if (!body.hasEffectiveAuthority || !motor || !motor.Motor || !(slot.skillDef is XiaoheiBlinkSkillDef) ||
                !body.healthComponent.alive || (recall ? !HasOrigin : slot.stock < 1)) return false;

            Vector3 start = transform.position;
            CapsuleCollider capsule = motor.Motor.Capsule;
            if (!capsule) return false;
            Vector3 scale = capsule.transform.lossyScale;
            float radius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float axis = Mathf.Max(0f, capsule.height * Mathf.Abs(scale.y) * .5f - radius);
            Vector3 center = capsule.transform.TransformPoint(capsule.center);
            Vector3 top = center + capsule.transform.up * axis;
            Vector3 bottom = center - capsule.transform.up * axis;
            float probe = Mathf.Max(.01f, radius - Skin);
            Vector3 destination = origin;

            if (!recall)
            {
                Vector3 move = body.inputBank.moveVector;
                Vector3 direction = move.sqrMagnitude > .001f ? TravelDirection(move) : body.inputBank.aimDirection.normalized;
                if (direction.sqrMagnitude < .001f) return false;
                float distance = BlinkDistance;
                if (Physics.CapsuleCast(top, bottom, probe, direction, out var hit, BlinkDistance + Skin,
                    LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
                    distance = Mathf.Min(distance, hit.distance - Skin);
                if (distance < .05f) return false;
                destination = start + direction * distance;
            }
            // A moving door or platform can occupy an origin during its three seconds.
            Vector3 delta = destination - start;
            if (Physics.CheckCapsule(top + delta, bottom + delta, probe, LayerIndex.world.mask,
                QueryTriggerInteraction.Ignore)) return false;

            travel = new XiaoheiBlinkTravel
            {
                departure = start,
                destination = destination,
                recall = recall,
                first = !recall && !HasOrigin,
                started = Run.FixedTimeStamp.now.t
            };
            ShowTravel(travel);
            if (!recall) slot.DeductStock(1);
            slideRemaining = 0f;
            motor.rootMotion = motor.velocity = Vector3.zero;
            motor.Motor.SetPosition(destination, true);
            return true;
        }

        internal void ShowTravel(XiaoheiBlinkTravel travel)
        {
            if (NetworkClient.active)
            {
                var core = body.corePosition - transform.position;
                SkillBlinkVisual.Show(travel.departure + core, travel.destination + core, false, travel.recall);
            }
            if (travel.recall) ClearOrigin();
            if (!travel.first) return;
            ClearOrigin();
            float remaining = OriginLifetime - Mathf.Max(0f, Run.FixedTimeStamp.now.t - travel.started);
            if (!(remaining > 0f) || remaining > OriginLifetime) return;
            origin = travel.departure;
            expires = Time.fixedTime + remaining;
            if (NetworkClient.active)
                echo = XiaoheiModel.CreateEcho(body, origin - transform.position, remaining);
        }

        private void ClearOrigin()
        {
            expires = 0f;
            if (echo) Destroy(echo);
            echo = null;
        }

        private void OnDisable() => ClearOrigin();
    }
}

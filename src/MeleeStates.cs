using System.Collections.Generic;
using System.Runtime.CompilerServices;
using EntityStates;
using RoR2;
using RoR2.Skills;
using UnityEngine;
using UnityEngine.Networking;

namespace RiftSurvivor
{
    public abstract class AimedMeleeState : BaseSkillState
    {
        protected Vector3 Aim { get; private set; }

        public override void OnEnter()
        {
            base.OnEnter();
            if (Aim.sqrMagnitude < 0.5f && inputBank) Aim = GetAimRay().direction.normalized;
            if (Aim.sqrMagnitude < 0.5f) Aim = transform.forward;
        }

        public override void OnSerialize(NetworkWriter writer)
        {
            base.OnSerialize(writer);
            writer.Write(Aim);
        }

        public override void OnDeserialize(NetworkReader reader)
        {
            base.OnDeserialize(reader);
            var aim = reader.ReadVector3();
            // Discard zero, non-finite or malformed direction packets.
            if (aim.sqrMagnitude > 0.5f && aim.sqrMagnitude < 1.5f) Aim = aim.normalized;
        }

        // Remote owners send state transitions. Resolve gameplay only on the survivor
        // and state machine that actually have this skill equipped. The skill owner
        // already paid in SkillDef.OnExecute; validate remote charges on the host.
        protected bool AcceptSlot(GenericSkill slot, bool costsStock)
        {
            if (!characterBody || !healthComponent || !healthComponent.alive) return false;
            if (!NetworkServer.active) return true;
            if (!characterBody.GetComponent<CuiVitals>() || !slot || !slot.skillDef ||
                slot.stateMachine != outer || slot.activationState.stateType != GetType()) return false;
            return !costsStock || CuiServerCharges.TrySpend(characterBody, slot, prepaidByLocalSkill: true);
        }
    }

    public abstract class TimedMeleeStrike : AimedMeleeState
    {
        private float duration;
        private float impactTime;
        private bool resolved;
        private string motionCue;
        private float beganAt;
        private bool receivedStart;
        protected bool Accepted { get; set; }
        protected void BeginStrike(string cue, float seconds, float contactFraction)
        {
            duration = Mathf.Max(Time.fixedDeltaTime * 2f, seconds / Mathf.Max(attackSpeedStat, 0.1f));
            impactTime = duration * contactFraction;
            if (!receivedStart) beganAt = Run.FixedTimeStamp.now.t;
            if (!Accepted) return;
            StartAimMode(duration + 0.25f);
            motionCue = cue;
            CuiMotion.Play(characterBody, cue, duration);
        }

        protected abstract void ImpactServer();

        private void Resolve()
        {
            if (!Accepted || resolved || !NetworkServer.active || !characterBody ||
                !characterBody.healthComponent || !characterBody.healthComponent.alive) return;
            resolved = true;
            ImpactServer();
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (fixedAge >= impactTime) Resolve();
            if (isAuthority && fixedAge >= duration) outer.SetNextStateToMain();
        }

        public override void OnExit()
        {
            // A remote owner's transition back to idle can reach the host before a
            // local physics tick advances the wind-up, especially at high attack speed.
            // Consult the run clock for catch-up, without rewarding early interruptions.
            if (!outer.destroying && !isAuthority && receivedStart &&
                Run.FixedTimeStamp.now.t - beganAt + Time.fixedDeltaTime >= impactTime) Resolve();
            if (motionCue != null) CuiMotion.Stop(characterBody, motionCue);
            base.OnExit();
        }

        public override void OnSerialize(NetworkWriter writer)
        {
            base.OnSerialize(writer);
            writer.Write(beganAt);
        }

        public override void OnDeserialize(NetworkReader reader)
        {
            base.OnDeserialize(reader);
            float sent = reader.ReadSingle(), now = Run.FixedTimeStamp.now.t;
            receivedStart = !float.IsNaN(sent) && !float.IsInfinity(sent);
            if (receivedStart) beganAt = Mathf.Clamp(sent, now - .35f, now);
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.PrioritySkill;
    }

    public sealed class ComboPunch : TimedMeleeStrike, SteppedSkillDef.IStepSetter
    {
        private sealed class Rhythm { public float next, resetAt; public int expected; }
        private static readonly ConditionalWeakTable<CharacterBody, Rhythm> RemoteRhythms =
            new ConditionalWeakTable<CharacterBody, Rhythm>();
        private byte step;
        public void SetStep(int value) => step = (byte)Mathf.Clamp(value, 0, 2);

        public override void OnEnter()
        {
            base.OnEnter();
            Accepted = AcceptSlot(skillLocator ? skillLocator.primary : null, false);
            if (Accepted && NetworkServer.active && !isAuthority)
            {
                var rhythm = RemoteRhythms.GetValue(characterBody, _ => new Rhythm());
                float now = Time.fixedTime;
                // Permit modest packet bunching without granting a faster sustained
                // attack rate. A reset to jab is safe; a client cannot skip to uppercut.
                const float jitter = .09f;
                if (rhythm.next > now + jitter) Accepted = false;
                else
                {
                    if (step == 0 || now >= rhythm.resetAt) rhythm.expected = 0;
                    step = (byte)rhythm.expected;
                    rhythm.expected = (step + 1) % 3;
                    float interval = Mathf.Max(Time.fixedDeltaTime * 2f,
                        (step == 2 ? .48f : .32f) / Mathf.Max(.1f, attackSpeedStat));
                    rhythm.next = Mathf.Max(rhythm.next, now - jitter) + interval;
                    rhythm.resetAt = now + 1.25f + jitter;
                }
            }
            if (step == 2) BeginStrike("uppercut", 0.48f, 1f/3f);
            else BeginStrike(step == 0 ? "jab" : "cross", 0.32f, 0.28f);
        }

        protected override void ImpactServer()
        {
            bool third = step == 2;
            if (MeleeCombat.Sweep(characterBody, characterBody.corePosition, Aim,
                third ? 3.3f : 3f, third ? 1.25f : 1.05f, third ? 3f : 1.6f, third ? 560f : 120f,
                DamageTypeCombo.GenericPrimary, RollCrit(), new HashSet<HealthComponent>()))
                CuiVitals.PunchHit(characterBody, third);
        }

        public override void OnSerialize(NetworkWriter writer) { base.OnSerialize(writer); writer.Write(step); }
        public override void OnDeserialize(NetworkReader reader) { base.OnDeserialize(reader); SetStep(reader.ReadByte()); }
    }

    public sealed class PlateSlam : TimedMeleeStrike
    {
        private bool heavy;
        private bool receivedPose;
        public override void OnEnter()
        {
            base.OnEnter();
            // Client only previews the pose from the replicated buff. Only the host
            // spends stacks and chooses coefficients for the actual blast.
            Accepted = AcceptSlot(skillLocator ? skillLocator.secondary : null, true);
            if (NetworkServer.active) heavy = Accepted && CuiVitals.TrySpendFull(characterBody);
            else if (!receivedPose) heavy = CuiVitals.IsFull(characterBody);
            BeginStrike(heavy ? "slamHeavy" : "slam", 0.65f, 0.40f);
        }

        protected override void ImpactServer() => MeleeCombat.Slam(characterBody, heavy, RollCrit());

        public override void OnSerialize(NetworkWriter writer)
        {
            base.OnSerialize(writer);
            writer.Write(heavy);
        }

        public override void OnDeserialize(NetworkReader reader)
        {
            base.OnDeserialize(reader);
            heavy = reader.ReadBoolean();
            receivedPose = true;
        }
    }

    public sealed class ShoulderCharge : AimedMeleeState
    {
        private const float Duration = 0.48f;
        private readonly HashSet<HealthComponent> victims = new HashSet<HealthComponent>();
        private Vector3 previousCore;
        private float speed;
        private float travelledDistance;
        private Vector3 portalEntry;
        private float entryRadius, lastExitSampleAt = -1f;
        private bool crit;
        private bool stopped;
        private bool guarding;

        public override void OnEnter()
        {
            base.OnEnter();
            if (!AcceptSlot(skillLocator ? skillLocator.utility : null, true))
            {
                stopped = true;
                if (isAuthority) outer.SetNextStateToMain();
                return;
            }
            speed = Mathf.Max(4f, moveSpeedStat * 3.6f);
            previousCore = characterBody.corePosition;
            StartAimMode(Duration);
            CuiMotion.Play(characterBody, "charge", Duration);
            if (isAuthority && characterMotor)
            {
                if (Aim.y > 0.1f) characterMotor.Motor.ForceUnground();
                characterMotor.velocity = Vector3.zero;
            }
            if (NetworkServer.active)
            {
                crit = RollCrit();
                if (characterMotor && !WallAhead())
                {
                    CuiVitals.SetCharging(characterBody, true);
                    guarding = true;
                }
            }
        }

        private bool WallAhead() => Physics.SphereCast(characterBody.corePosition, characterBody.radius * .65f,
            Aim, out _, characterBody.radius * .35f + speed * Time.fixedDeltaTime,
            LayerIndex.world.mask, QueryTriggerInteraction.Ignore);

        internal static void BeforePortalWarp(CharacterBody traveler, Vector3 destinationRoot)
        {
            if (!NetworkServer.active) return;
            var machine = EntityStateMachine.FindByCustomName(traveler.gameObject, "Body");
            if (!machine || !(machine.state is ShoulderCharge charge) || !charge.guarding) return;
            // Resolve the last steps into the door, then start the next sweep at the exit.
            charge.SweepTravel();
            charge.previousCore = destinationRoot + (traveler.corePosition - traveler.transform.position);
            if (!traveler.hasEffectiveAuthority)
            {
                charge.portalEntry = traveler.corePosition;
                float separation = (charge.previousCore - charge.portalEntry).magnitude;
                charge.entryRadius = Mathf.Min(separation * .5f, traveler.radius + charge.speed * .10f);
                charge.lastExitSampleAt = Time.fixedTime;
            }
        }

        private bool SweepTravel()
        {
            var current = characterBody.corePosition;
            var travelled = current - previousCore;
            float distance = travelled.magnitude;
            float nextDistance = travelledDistance + distance;
            bool withinDash = nextDistance <= speed * Duration + 1f && Vector3.Dot(travelled, Aim) >= -.15f;
            if (lastExitSampleAt >= 0f && distance > .0001f)
            {
                // The guest can still send entrance positions before it receives the warp.
                // Reject them without moving the exit baseline or spending dash distance.
                float fromEntry = (current - portalEntry).magnitude;
                float allowed = speed * (Time.fixedTime - lastExitSampleAt + Time.fixedDeltaTime) + .15f;
                if (!withinDash || distance > allowed || (fromEntry < entryRadius && fromEntry < distance)) return false;
                lastExitSampleAt = Time.fixedTime;
            }
            // Network transform samples may bunch up, but together they cannot deal
            // damage beyond the distance of one dash or trace a teleport backwards.
            travelledDistance = nextDistance;
            if (withinDash && !Physics.Linecast(previousCore, current, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
            {
                var direction = travelled.sqrMagnitude > 0.0001f ? travelled.normalized : Aim;
                MeleeCombat.Sweep(characterBody, previousCore, direction, distance + 0.95f,
                    1.1f, 3.5f, 1000f, DamageTypeCombo.GenericUtility, crit, victims, triggerCover: true);
            }
            previousCore = current;
            return true;
        }

        private void EndGuard()
        {
            if (!guarding) return;
            guarding = false;
            CuiVitals.SetCharging(characterBody, false);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (stopped) return;
            bool validPosition = !NetworkServer.active || !guarding || SweepTravel();
            if (!characterMotor || fixedAge >= Duration || (validPosition && WallAhead()))
            {
                stopped = true;
                EndGuard();
                CuiMotion.Stop(characterBody, "charge");
                if (isAuthority) outer.SetNextStateToMain();
                return;
            }
            if (isAuthority)
            {
                characterMotor.moveDirection = Vector3.zero;
                characterMotor.velocity = Vector3.zero;
                characterMotor.rootMotion += Aim * (speed * Time.fixedDeltaTime);
            }
        }

        public override void OnExit()
        {
            if (NetworkServer.active && guarding && characterBody && healthComponent && healthComponent.alive)
                SweepTravel();
            EndGuard();
            CuiMotion.Stop(characterBody, "charge");
            base.OnExit();
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.PrioritySkill;
    }
}

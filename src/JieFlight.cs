using EntityStates;
using RoR2;
using UnityEngine;

namespace RiftSurvivor
{
    // The normal Commando main state handles walking, skill input and the first
    // jump. Airborne jump gestures belong to the riding component, even with a Feather.
    public sealed class JieFlightMain : GenericCharacterMain
    {
        public override void ProcessJump()
        {
            if (!characterMotor.isGrounded || JieFlight.IsRiding(characterBody)) return;
            if (inputBank.jump.justPressed) GetComponent<JieFlight>().groundPressAt = Time.fixedTime;
            base.ProcessJump();
        }
    }

    // The separate networked lane persists through a hit or a weapon skill. Its
    // replicated state is also the source of truth for the remote riding pose.
    public sealed class JieHover : BaseState
    {
        public override void OnEnter()
        {
            base.OnEnter();
            if (isAuthority) GetComponent<JieFlight>().SetAntiGravity(true);
        }

        public override void OnExit()
        {
            var flight = GetComponent<JieFlight>();
            if (flight) flight.SetAntiGravity(false);
            base.OnExit();
        }
    }

    [DefaultExecutionOrder(30)]
    public sealed class JieFlight : MonoBehaviour
    {
        private const float HoldTime = .20f;
        private CharacterBody body;
        private EntityStateMachine flightMachine, bodyMachine, slideMachine;
        private bool antiGravity, wasDown, canDismount;
        private float pressedAt;
        internal float groundPressAt = float.NegativeInfinity;
        internal float dashRecallUntil = float.NegativeInfinity;

        internal static void Install(GameObject prefab)
        {
            var main = EntityStateMachine.FindByCustomName(prefab, "Body");
            main.mainStateType = SurvivorSkills.Register<JieFlightMain>();
            SurvivorSkills.Register<JieHover>();
            SurvivorSkills.AddIdleMachine(prefab, "JieFlight", resetOnHurt: false);
            prefab.AddComponent<JieFlight>();
            prefab.AddComponent<JieStormSight>();
        }

        private void Awake()
        {
            body = GetComponent<CharacterBody>();
            flightMachine = EntityStateMachine.FindByCustomName(gameObject, "JieFlight");
            bodyMachine = EntityStateMachine.FindByCustomName(gameObject, "Body");
            slideMachine = EntityStateMachine.FindByCustomName(gameObject, "Slide");
        }

        internal static bool IsRiding(CharacterBody owner)
        {
            var flight = owner ? owner.GetComponent<JieFlight>() : null;
            return flight && ((flight.flightMachine && flight.flightMachine.state is JieHover) ||
                              (flight.slideMachine && flight.slideMachine.state is JieDash));
        }

        internal void Mount()
        {
            if (!body.hasEffectiveAuthority || !body.healthComponent.alive) return;
            if (!(flightMachine.state is JieHover)) flightMachine.SetNextState(new JieHover());
            canDismount = false;
            pressedAt = Time.fixedTime;
        }

        internal void SetAntiGravity(bool on)
        {
            if (antiGravity == on || !body || !body.characterMotor) return;
            var motor = body.characterMotor;
            var gravity = motor.gravityParameters;
            gravity.channeledAntiGravityGranterCount += on ? 1 : -1;
            motor.gravityParameters = gravity;
            antiGravity = on;
            if (!on) return;
            if (motor.isGrounded) motor.rootMotion += Vector3.up * .18f;
            motor.Motor.ForceUnground();
            var velocity = motor.velocity;
            velocity.y = 0f;
            motor.velocity = velocity;
        }

        private void FixedUpdate()
        {
            if (!body.hasEffectiveAuthority || !body.healthComponent || !body.healthComponent.alive || !body.inputBank) return;
            var motor = body.characterMotor;
            bool down = body.inputBank.jump.down;
            bool press = down && !wasDown, release = !down && wasDown;
            wasDown = down;
            if (slideMachine.state is JieDash) return;
            bool riding = flightMachine.state is JieHover;
            if (!riding)
            {
                if (press && !motor.isGrounded && groundPressAt != Time.fixedTime && bodyMachine.state is JieFlightMain)
                    Mount();
                return;
            }

            if (press) { pressedAt = Time.fixedTime; canDismount = true; }
            float held = Time.fixedTime - pressedAt;
            if (release && canDismount && held < HoldTime)
            {
                flightMachine.SetNextStateToMain();
                return;
            }
            // Keep the sword mounted and process its jump gestures; the optional X
            // controller supplies pitch-directed steering until the player switches it off.
            if (HiddenFlight.IsEnabled(body)) return;
            var movement = Vector3.ProjectOnPlane(body.inputBank.moveVector, Vector3.up);
            motor.moveDirection = movement;
            // Pitch never directs the board into the ground. The motor still
            // resolves walls and ceilings, and normal damage states may interrupt attacks.
            var velocity = movement * body.moveSpeed;
            velocity.y = down && held >= HoldTime ? Mathf.Max(4f, body.moveSpeed * .8f) : 0f;
            motor.velocity = velocity;
        }

        private void OnDisable() => SetAntiGravity(false);
    }

    public sealed class JieDash : BaseSkillState
    {
        internal const float Distance = 16f;
        private const float Duration = .24f;
        private Vector3 direction;
        private float remaining = Distance;

        public override void OnEnter()
        {
            base.OnEnter();
            var flight = GetComponent<JieFlight>();
            if (flight) flight.dashRecallUntil = Time.fixedTime + Duration + .35f;
            if (!isAuthority) return;
            var move = inputBank.moveVector;
            direction = move.sqrMagnitude > .001f ? Vector3.ProjectOnPlane(move, Vector3.up).normalized : GetAimRay().direction;
            if (direction.sqrMagnitude < .001f) direction = transform.forward;
            characterMotor.Motor.ForceUnground();
            characterMotor.velocity = Vector3.zero;
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (!isAuthority) return;
            float step = Mathf.Min(remaining, Distance * Time.fixedDeltaTime / Duration);
            characterMotor.velocity = Vector3.zero;
            characterMotor.rootMotion += direction * step;
            remaining -= step;
            if (remaining > .001f) return;
            GetComponent<JieFlight>().Mount();
            outer.SetNextStateToMain();
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.PrioritySkill;
    }
}

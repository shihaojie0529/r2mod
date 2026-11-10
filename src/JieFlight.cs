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
            var flying = motor.flightParameters;
            flying.channeledFlightGranterCount += on ? 1 : -1;
            motor.flightParameters = flying;
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
            var movement = FlightDirection(body);
            motor.moveDirection = movement;
            motor.velocity = movement * HiddenFlight.Speed(body);
        }

        internal static Vector3 FlightDirection(CharacterBody owner)
        {
            if (!owner || !owner.inputBank) return Vector3.zero;
            var right = owner.transform.right;
            foreach (var local in LocalUserManager.readOnlyLocalUsersList)
                if (local.cachedBody == owner && local.cameraRigController && local.cameraRigController.sceneCam)
                { right = local.cameraRigController.sceneCam.transform.right; break; }
            var move = owner.inputBank.rawMoveData;
            return HiddenFlight.FlightDirection(owner.inputBank.aimDirection, right, move.y, move.x);
        }

        private void OnDisable() => SetAntiGravity(false);
    }

    public sealed class JieDash : BaseSkillState
    {
        internal const float Distance = 16f;
        private Vector3 departure, destination;
        private bool blinked;

        public override void OnEnter()
        {
            base.OnEnter();
            var slot = skillLocator ? skillLocator.utility : null;
            var flight = GetComponent<JieFlight>();
            if (!flight || !healthComponent || !healthComponent.alive || !slot ||
                slot.stateMachine != outer || slot.activationState.stateType != typeof(JieDash)) return;
            if (UnityEngine.Networking.NetworkServer.active &&
                !CuiServerCharges.TrySpend(characterBody, slot, true)) return;
            flight.dashRecallUntil = Time.fixedTime + .4f;
            if (isAuthority && characterMotor && characterMotor.Motor)
            {
                departure = transform.position;
                var direction = JieFlight.FlightDirection(characterBody);
                if (direction.sqrMagnitude < .001f) direction = GetAimRay().direction.normalized;
                blinked = JieBlink.TryDestination(characterMotor, direction, Distance, out destination);
                if (blinked)
                {
                    characterMotor.rootMotion = characterMotor.velocity = Vector3.zero;
                    characterMotor.Motor.ForceUnground();
                    characterMotor.Motor.SetPosition(destination, true);
                }
                flight.Mount();
            }
            // The owner teleports once; native network transforms carry the new
            // position. The state packet carries only the visual endpoints to peers.
            if (UnityEngine.Networking.NetworkClient.active && blinked && ValidVisual(departure, destination))
            {
                var coreOffset = characterBody.corePosition - transform.position;
                JieBlinkVisual.Show(departure + coreOffset, destination + coreOffset);
                Util.PlaySound("Play_huntress_shift_mini_blink", gameObject);
            }
        }

        private static bool ValidVisual(Vector3 from, Vector3 to) =>
            !float.IsNaN(from.sqrMagnitude) && !float.IsInfinity(from.sqrMagnitude) &&
            (to - from).sqrMagnitude > .0001f && (to - from).sqrMagnitude <= (Distance + .1f) * (Distance + .1f);

        public override void OnSerialize(UnityEngine.Networking.NetworkWriter writer)
        {
            base.OnSerialize(writer);
            writer.Write(blinked); writer.Write(departure); writer.Write(destination);
        }

        public override void OnDeserialize(UnityEngine.Networking.NetworkReader reader)
        {
            base.OnDeserialize(reader);
            blinked = reader.ReadBoolean(); departure = reader.ReadVector3(); destination = reader.ReadVector3();
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (isAuthority) outer.SetNextStateToMain();
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.PrioritySkill;
    }
}

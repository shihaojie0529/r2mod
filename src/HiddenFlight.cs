using EntityStates;
using RoR2;
using RoR2.UI;
using UnityEngine;

namespace RiftSurvivor
{
    // Only the keyboard owner's authoritative body gets this optional movement controller.
    // Its normal network transform replicates the result; no extra skill or network lane is needed.
    [DisallowMultipleComponent, DefaultExecutionOrder(40)]
    public sealed class HiddenFlight : MonoBehaviour
    {
        private LocalUser owner;
        private CharacterBody body;
        private CharacterMotor motor;
        private RigidbodyMotor rigidMotor;
        private Rigidbody rigid;
        private CharacterController capsule;
        private IPhysMotor physicsMotor;
        private EntityStateMachine main, slide, utilityLane;
        private bool controlling, restoreRigidGravity;
        private static bool registered;

        internal static void Register()
        {
            if (registered) return;
            registered = true;
            RoR2Application.onUpdate += PollToggle;
        }

        internal static bool CanReadKeys(LocalUser user) =>
            user != null && Application.isFocused && Time.timeScale > 0f && !PauseManager.isPaused && !PauseManager.PauseScreenIsOpen() &&
            !ChatBox.IsActive && !ConsoleWindow.IsShown() && !user.isUIFocused &&
            (!user.eventSystem || !user.eventSystem.isCursorVisible) &&
            (!user.cameraRigController || (user.cameraRigController.isControlAllowed && !user.cameraRigController.isCutscene));

        private static void PollToggle()
        {
            if (!Run.instance || !Input.GetKeyDown(KeyCode.X)) return;
            foreach (var user in LocalUserManager.readOnlyLocalUsersList)
            {
                var body = user.cachedBody;
                if (user.inputPlayer == null || !user.inputPlayer.controllers.hasKeyboard || !body ||
                    !body.isPlayerControlled || !body.hasEffectiveAuthority || !body.healthComponent ||
                    !body.healthComponent.alive || !body.inputBank || !CanReadKeys(user)) continue;
                var flight = body.GetComponent<HiddenFlight>();
                if (flight && flight.enabled)
                {
                    flight.enabled = false;
                    Chat.AddMessage(Language.GetString("WEIGE_FLIGHT_OFF"));
                }
                else
                {
                    if (!flight) flight = body.gameObject.AddComponent<HiddenFlight>();
                    if (!flight.motor && flight.physicsMotor == null && !flight.rigid && !flight.capsule) return;
                    flight.owner = user;
                    flight.enabled = true;
                    Chat.AddMessage(Language.GetString("WEIGE_FLIGHT_ON"));
                }
                return; // A keyboard is a single player's device, including in local co-op.
            }
        }

        private void Awake()
        {
            body = GetComponent<CharacterBody>();
            motor = body.characterMotor;
            rigid = body.rigidbody;
            capsule = GetComponent<CharacterController>();
            rigidMotor = GetComponent<RigidbodyMotor>();
            physicsMotor = GetComponent<IPhysMotor>();
            main = EntityStateMachine.FindByCustomName(gameObject, "Body");
            slide = EntityStateMachine.FindByCustomName(gameObject, "Slide");
            enabled = false;
        }

        internal static bool IsEnabled(CharacterBody body)
        {
            var flight = body ? body.GetComponent<HiddenFlight>() : null;
            return flight && flight.enabled;
        }

        private bool MovementSkillOwnsMotor()
        {
            // Stun, rolls and multi-stage movement skills retain all their native root motion,
            // velocity and gravity. Weapon skills can run while the player steers normally.
            if (slide && slide.state != null && !slide.IsInMainState()) return true;
            bool bodyAction = main && main.state != null && !main.IsInMainState();
            if (bodyAction && (main.IsInInitialState() || main.state is StunState || main.state is FrozenState ||
                               main.state is GenericCharacterDeath || main.state is GenericCharacterSpawnState)) return true;
            // A stationary shot may occupy Body too. Yield to a velocity-driven lunge,
            // not to the mere presence of a firing state on that lane.
            if (bodyAction && motor && motor.velocity.sqrMagnitude > body.moveSpeed * body.moveSpeed * 1.32f) return true;
            if (motor && motor.disableAirControlUntilCollision) return true;
            if (motor && motor.rootMotion.sqrMagnitude > .000001f)
            {
                var primary = body.skillLocator ? body.skillLocator.primary : null;
                var weapon = primary ? primary.stateMachine : null;
                if (!body.rootMotionInMainState || bodyAction || (weapon && !weapon.IsInMainState())) return true;
            }
            if (rigidMotor && rigidMotor.rootMotion.sqrMagnitude > .000001f) return true;
            var utility = body.skillLocator ? body.skillLocator.utility : null;
            var lane = utility ? utility.stateMachine : null;
            // These weapon-lane utilities can keep an aiming cursor open while flying.
            // Movement utilities (including ones that start on Weapon) still take priority.
            var activation = utility ? utility.activationState.stateType : null;
            if (activation == typeof(EntityStates.Engi.EngiMissilePainter.Paint) ||
                activation == typeof(EntityStates.Engi.EngiWeapon.FireBubbleShield) ||
                activation == typeof(EntityStates.Mage.Weapon.PrepWall))
            {
                utilityLane = null;
                return false;
            }
            if (lane && lane.state != null && !lane.IsInMainState() &&
                activation == lane.state.GetType()) utilityLane = lane;
            if (!utilityLane) return false;
            if (!utilityLane.IsInMainState() || utilityLane.HasPendingState()) return true;
            utilityLane = null;
            return false;
        }

        private void FixedUpdate()
        {
            if (owner == null || owner.cachedBody != body || owner.inputPlayer == null ||
                !owner.inputPlayer.controllers.hasKeyboard || !body.hasEffectiveAuthority ||
                !body.isPlayerControlled || !body.healthComponent || !body.healthComponent.alive)
            {
                enabled = false;
                return;
            }
            bool canSteer = !MovementSkillOwnsMotor();
            // Clear only ordinary ground-walk motion. Leave the initial take-off displacement intact.
            if (canSteer && motor && body.rootMotionInMainState) motor.rootMotion = Vector3.zero;
            SetControl(canSteer);
            if (!canSteer) return;

            Vector3 direction = Vector3.zero;
            if (CanReadKeys(owner))
            {
                // Native action axes are sampled before camera projection, including rebound keys.
                var move = body.inputBank.rawMoveData;
                var cam = owner.cameraRigController ? owner.cameraRigController.sceneCam : null;
                direction = FlightDirection(body.inputBank.aimDirection, cam ? cam.transform.right : transform.right,
                    move.y, move.x);
            }
            var velocity = direction * body.moveSpeed;
            if (motor)
            {
                motor.moveDirection = direction;
                motor.velocity = velocity;
            }
            else
            {
                if (rigidMotor) rigidMotor.moveVector = direction;
                if (physicsMotor != null) physicsMotor.velocityAuthority = velocity;
                else if (capsule && capsule.enabled) capsule.Move(velocity * Time.fixedDeltaTime);
                else if (rigid && !rigid.isKinematic) rigid.velocity = velocity;
                else if (rigid && velocity.sqrMagnitude > .00001f)
                {
                    float distance = velocity.magnitude * Time.fixedDeltaTime;
                    if (rigid.SweepTest(direction, out var hit, distance, QueryTriggerInteraction.Ignore))
                        distance = Mathf.Max(0f, hit.distance - .015f);
                    rigid.MovePosition(rigid.position + direction * distance);
                }
            }
        }

        internal static Vector3 FlightDirection(Vector3 aim, Vector3 cameraRight, float forward, float side)
        {
            aim = aim.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, aim);
            // Straight up/down still has a meaningful camera yaw and lateral direction.
            if (right.sqrMagnitude < .0001f) right = Vector3.ProjectOnPlane(cameraRight, aim);
            return Vector3.ClampMagnitude(aim * forward + right.normalized * side, 1f);
        }

        private void SetControl(bool on)
        {
            if (controlling == on) return;
            controlling = on;
            if (motor)
            {
                int delta = on ? 1 : -1;
                var gravity = motor.gravityParameters;
                gravity.channeledAntiGravityGranterCount += delta;
                motor.gravityParameters = gravity;
                var flying = motor.flightParameters;
                flying.channeledFlightGranterCount += delta;
                motor.flightParameters = flying;
                if (on && motor.Motor && motor.isGrounded)
                {
                    motor.Motor.ForceUnground();
                    motor.rootMotion += Vector3.up * .15f;
                }
            }
            else if (rigid)
            {
                if (on)
                {
                    restoreRigidGravity = rigid.useGravity;
                    if (restoreRigidGravity) rigid.useGravity = false;
                }
                else if (restoreRigidGravity) rigid.useGravity = true;
            }
        }

        private void OnEnable()
        {
            if (body) body.onSkillActivatedAuthority += SkillStarted;
        }

        private void SkillStarted(GenericSkill skill)
        {
            if (!skill || !body.skillLocator) return;
            if (skill == body.skillLocator.utility) utilityLane = skill.stateMachine;
            else if (utilityLane == skill.stateMachine) utilityLane = null;
        }

        private void OnDisable()
        {
            if (body) body.onSkillActivatedAuthority -= SkillStarted;
            utilityLane = null;
            SetControl(false);
            owner = null;
        }
    }
}

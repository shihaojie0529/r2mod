using EntityStates;
using RoR2;
using RoR2.Skills;
using UnityEngine;
using UnityEngine.Networking;

namespace RiftSurvivor
{
    // Both manual and automatic requests execute the equipped GenericSkill.
    // The request belongs to one synchronous skill instantiation, even though
    // the resulting state may not enter or reach the server until a later tick.
    public sealed class WeiDeliverySkillDef : SkillDef
    {
        public override bool CanExecute(GenericSkill slot)
        {
            var fire = slot.GetComponent<WeiAutoFire>();
            return (!fire || fire.CanDeliver) && base.CanExecute(slot);
        }

        public override EntityState InstantiateNextState(GenericSkill slot)
        {
            var state = base.InstantiateNextState(slot);
            var fire = slot.GetComponent<WeiAutoFire>();
            if (state is WeiDelivery delivery && fire && fire.RequestedTarget)
                delivery.SetAutomaticTarget(fire.RequestedTarget);
            return state;
        }
    }

    [DisallowMultipleComponent, DefaultExecutionOrder(50)]
    public sealed class WeiAutoFire : MonoBehaviour
    {
        private const float Range = 60f;
        private CharacterBody body;
        private WeiCombat combat;
        private EntityStateMachine main;
        private LocalUser local;
        private HurtBox locked;
        private float nextSearch;
        private double nextPrimary;
        private GameObject marker;
        private bool showLock;
        internal HurtBox RequestedTarget { get; private set; }
        internal bool CanDeliver => Time.fixedTimeAsDouble + .0001 >= nextPrimary && !combat.Carrying;
        internal void ShotStarted(float recovery) => nextPrimary = Time.fixedTimeAsDouble + recovery;

        private void Awake()
        {
            body = GetComponent<CharacterBody>();
            combat = GetComponent<WeiCombat>();
            main = EntityStateMachine.FindByCustomName(gameObject, "Body");
        }

        internal bool BodyReady() => body && body.isPlayerControlled && !body.allSkillsDisabled && DownedRescue.IsUp(body) &&
            main && main.state is GenericCharacterMain && main.IsInMainState() && !main.HasPendingState();

        internal static bool Valid(CharacterBody caster, HurtBox target)
        {
            if (!JieStorm.CanSee(caster, target)) return false;
            var point = target.transform.position;
            return (point - caster.aimOrigin).sqrMagnitude <= Range * Range &&
                !Physics.Linecast(caster.aimOrigin, point, LayerIndex.world.mask, QueryTriggerInteraction.Ignore);
        }

        private static HurtBox FindNearest(CharacterBody caster)
        {
            // No view-axis filter: targets above, below and directly behind the
            // camera participate in exactly the same distance comparison.
            HurtBox best = null;
            float nearest = Range * Range;
            foreach (var candidate in CharacterBody.readOnlyInstancesList)
            {
                var hurt = candidate ? candidate.mainHurtBox : null;
                if (!hurt) continue;
                float distance = (hurt.transform.position - caster.aimOrigin).sqrMagnitude;
                if (distance > nearest || !Valid(caster, hurt)) continue;
                nearest = distance;
                best = hurt;
            }
            return best;
        }

        internal static HurtBox ValidateOrAcquire(CharacterBody caster, HurtBox target) =>
            Valid(caster, target) ? target : FindNearest(caster);

        private bool CanControl()
        {
            if (!NetworkClient.active || !Run.instance || !body.hasEffectiveAuthority || !BodyReady() || !body.inputBank) return false;
            if (local == null || local.cachedBody != body)
            {
                local = null;
                foreach (var user in LocalUserManager.readOnlyLocalUsersList)
                    if (user.cachedBody == body) { local = user; break; }
            }
            return local != null && HiddenFlight.CanReadKeys(local);
        }

        private void FixedUpdate()
        {
            showLock = false;
            if (!CanControl()) return;
            var slot = body.skillLocator ? body.skillLocator.primary : null;
            if (!slot || !(slot.skillDef is WeiDeliverySkillDef) || slot.activationState.stateType != typeof(WeiDelivery)) return;
            var input = body.inputBank;
            if (input.skill1.down || input.skill2.justPressed || input.skill3.justPressed || input.skill4.justPressed || combat.Carrying) return;
            var weapon = slot.stateMachine;
            if (!weapon) return;

            if (Time.fixedTime >= nextSearch)
            {
                nextSearch = Time.fixedTime + .10f;
                locked = ValidateOrAcquire(body, locked);
            }
            if (!locked) return;
            showLock = true;
            if (!weapon.IsInMainState() || weapon.HasPendingState() || !slot.CanExecute()) return;
            // Between two scans the target may have died or moved behind cover.
            if (!Valid(body, locked)) { locked = null; nextSearch = 0f; showLock = false; return; }
            RequestedTarget = locked;
            try { slot.ExecuteIfReady(); }
            finally { RequestedTarget = null; }
        }

        private void LateUpdate()
        {
            // FixedUpdate stops in a single-player pause. Recheck focus before
            // drawing the lock so menus and other local players keep their own UI.
            bool visible = showLock && local != null && local.cachedBody == body && HiddenFlight.CanReadKeys(local) &&
                locked && locked.healthComponent && locked.healthComponent.alive;
            if (!visible) { if (marker) marker.SetActive(false); return; }
            if (!marker) marker = WeiFurnitureArt.Frame(automatic: true);
            marker.SetActive(true);
            var enemy = locked.healthComponent.body;
            marker.transform.position = enemy.corePosition + Vector3.up * (Mathf.Max(.6f, enemy.radius) + .55f);
            marker.transform.localScale = Vector3.one * Mathf.Clamp(enemy.radius, .65f, 1.6f);
        }

        private void OnDisable()
        {
            if (marker) Destroy(marker);
            marker = null;
            local = null;
            locked = RequestedTarget = null;
            showLock = false;
        }
    }
}

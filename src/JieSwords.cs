using System;
using System.Collections.Generic;
using EntityStates;
using R2API;
using RoR2;
using RoR2.Skills;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;

namespace RiftSurvivor
{
    [DisallowMultipleComponent]
    public sealed class JieSwords : MonoBehaviour
    {
        internal const int Capacity = 9;
        private static readonly List<JieSwords> owners = new List<JieSwords>();
        private static readonly Sprite[] recallIcons = new Sprite[Capacity + 1];
        private static BuffDef bladeCount, deployedCount, recallReceipt;
        private static GameObject swordPrefab;
        internal static GameObject Impact { get; private set; }
        internal static GameObject PierceTracer { get; private set; }
        private readonly List<JieAzureSword> planted = new List<JieAzureSword>(Capacity);
        private readonly HashSet<JieAzureSword> deployed = new HashSet<JieAzureSword>();
        private CharacterBody body;
        private double nextPrimary;
        private bool awaitingReceipts;
        private int lastReceipt, nextFan, nextDock;

        internal static void Register()
        {
            bladeCount = Counter("JiePlantedSwords");
            bladeCount.isHidden = false;
            bladeCount.iconSprite = GymIcons.Get("jie-draw");
            bladeCount.buffColor = new Color(.18f, .92f, .81f);
            deployedCount = Counter("JieDeployedSwords");
            recallReceipt = Counter("JieAcceptedRecall");
            for (int i = 0; i < recallIcons.Length; i++) recallIcons[i] = GymIcons.Get("jie-recall-" + i);
            swordPrefab = PrefabAPI.CreateEmptyPrefab("JieAzureSword", true);
            swordPrefab.AddComponent<JieAzureSword>();
            GlobalEventManager.onCharacterDeathServer += FreezeSockets;
            Impact = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Merc/OmniImpactVFXSlashMerc.prefab").WaitForCompletion();
            PierceTracer = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Huntress/TracerHuntressSnipe.prefab").WaitForCompletion();
        }

        private static BuffDef Counter(string name)
        {
            var counter = ScriptableObject.CreateInstance<BuffDef>();
            counter.name = name;
            counter.isHidden = counter.canStack = counter.ignoreGrowthNectar = true;
            if (!ContentAddition.AddBuffDef(counter)) throw new InvalidOperationException("Cannot register " + name);
            return counter;
        }

        internal static void Install(GameObject prefab)
        {
            prefab.AddComponent<JieSwords>();
            prefab.AddComponent<JieSwordRackVisual>();
        }
        private void Awake() => body = GetComponent<CharacterBody>();
        private void OnEnable() => owners.Add(this);
        private void OnDisable() => owners.Remove(this);

        internal static int Count(GenericSkill slot) =>
            slot && slot.characterBody && bladeCount ? Mathf.Clamp(slot.characterBody.GetBuffCount(bladeCount), 0, Capacity) : 0;
        internal static Sprite RecallIcon(GenericSkill slot) => recallIcons[Count(slot)];
        // Recall empties the planted list before the actors actually arrive home.
        // The synchronized live count prevents a second rack appearing mid-flight.
        internal static int ReadyCount(CharacterBody owner) => owner && deployedCount
            ? Capacity - Mathf.Clamp(owner.GetBuffCount(deployedCount), 0, Capacity) : Capacity;

        internal void Launch(Ray aim, float interval)
        {
            if (!NetworkServer.active) return;
            if (!body.hasEffectiveAuthority)
            {
                double now = Time.fixedTimeAsDouble;
                const double jitter = .09;
                if (nextPrimary > now + jitter) return;
                nextPrimary = Math.Max(now - jitter, nextPrimary) + interval;
            }
            var target = FindTarget(body, aim, false);
            var sword = Instantiate(swordPrefab, aim.origin, Util.QuaternionSafeLookRotation(aim.direction));
            var actor = sword.GetComponent<JieAzureSword>();
            actor.Launch(this, body, target);
            deployed.Add(actor);
            PublishDeployed();
            NetworkServer.Spawn(sword);
        }

        // First lock uses the full forward hemisphere sorted by angular error.
        // After a kill, the airborne blade instead seeks the nearest visible enemy to itself.
        internal static HurtBox FindTarget(CharacterBody owner, Ray ray, bool retarget)
        {
            var search = new BullseyeSearch
            {
                viewer = owner, teamMaskFilter = TeamMask.GetEnemyTeams(owner.teamComponent.teamIndex),
                searchOrigin = ray.origin, searchDirection = ray.direction, maxDistanceFilter = 60f,
                maxAngleFilter = retarget ? 180f : 90f, filterByLoS = true,
                sortMode = retarget ? BullseyeSearch.SortMode.Distance : BullseyeSearch.SortMode.Angle
            };
            search.RefreshCandidates();
            foreach (var hurt in search.GetResults()) if (JieStorm.CanSee(owner, hurt)) return hurt;
            return null;
        }

        private static void FreezeSockets(DamageReport report)
        {
            if (!report.victim) return;
            foreach (var rack in owners)
                foreach (var sword in rack.planted)
                    if (sword) sword.FreezeAtDeath(report.victim);
        }

        internal int Plant(JieAzureSword sword)
        {
            if (planted.Count == Capacity)
            {
                var oldest = planted[0];
                planted.RemoveAt(0);
                oldest.Retire();
            }
            planted.Add(sword);
            PublishCount();
            int fan = nextFan;
            nextFan = (nextFan + 1) % Capacity;
            return fan;
        }

        internal void Forget(JieAzureSword sword)
        {
            if (planted.Remove(sword) && body && NetworkServer.active) PublishCount();
            if (deployed.Remove(sword) && body && NetworkServer.active) PublishDeployed();
        }

        internal int ClaimReturnSocket()
        {
            int result = nextDock;
            nextDock = (nextDock + 1) % Capacity;
            return result;
        }

        private void PublishCount() => body.SetBuffCount(bladeCount.buffIndex, planted.Count);
        private void PublishDeployed() => body.SetBuffCount(deployedCount.buffIndex, Mathf.Min(Capacity, deployed.Count));

        // Keep the attachment reference through the damage call. A lethal plate
        // slam can still identify the socket before its next physics tick drops the sword.
        internal static CharacterBody FindOwner(HealthComponent victim, TeamIndex team, List<CharacterBody> matches = null)
        {
            if (!NetworkServer.active || !victim) return null;
            CharacterBody best = null;
            foreach (var rack in owners)
            {
                var owner = rack.body;
                if (!owner || !owner.healthComponent || !owner.healthComponent.alive || owner.teamComponent.teamIndex != team ||
                    (matches == null && best && owner.netId.Value >= best.netId.Value)) continue;
                foreach (var sword in rack.planted)
                    if (sword && sword.AttachedHealth == victim)
                    {
                        matches?.Add(owner);
                        if (!best || owner.netId.Value < best.netId.Value) best = owner;
                        break;
                    }
            }
            return best;
        }

        internal void Recall(Vector3 destination)
        {
            if (!NetworkServer.active || planted.Count == 0 ||
                !CuiServerCharges.TrySpend(body, body.skillLocator.secondary, false)) return;
            var returning = planted.ToArray();
            planted.Clear();
            PublishCount();
            if (!body.hasEffectiveAuthority)
                body.SetBuffCount(recallReceipt.buffIndex, body.GetBuffCount(recallReceipt) % 127 + 1);

            // Arm every blade and snapshot its current attachment before the first
            // contact can kill a host, trigger a proc, or plant another sword.
            foreach (var sword in returning) if (sword) sword.BeginReturn(destination);
            foreach (var sword in returning) if (sword) sword.HitAttachment();
        }

        internal void AwaitConfirmation()
        {
            if (awaitingReceipts) return;
            lastReceipt = body.GetBuffCount(recallReceipt);
            awaitingReceipts = true;
        }

        private void FixedUpdate()
        {
            if (NetworkServer.active || !awaitingReceipts || !body.hasEffectiveAuthority) return;
            int receipt = body.GetBuffCount(recallReceipt);
            int count = receipt == 0 ? 0 : (receipt - lastReceipt + 127) % 127;
            lastReceipt = receipt;
            var slot = body.skillLocator.secondary;
            if (count > 0 && slot && slot.skillDef is JieRecallSkillDef)
                slot.DeductStock(Mathf.Min(count, slot.stock));
        }
    }

    public sealed class JieRecallSkillDef : SkillDef
    {
        public override bool IsReady(GenericSkill slot) => JieSwords.Count(slot) > 0 && base.IsReady(slot);
        public override bool CanExecute(GenericSkill slot) => JieSwords.Count(slot) > 0 && base.CanExecute(slot);
        public override Sprite GetCurrentIcon(GenericSkill slot) => JieSwords.RecallIcon(slot);

        public override EntityState InstantiateNextState(GenericSkill slot)
        {
            var state = base.InstantiateNextState(slot);
            if (state is JieRecallSwords recall && slot.characterBody)
                recall.castPosition = slot.characterBody.corePosition;
            return state;
        }
    }

    public sealed class JieDrawSword : BaseSkillState
    {
        private Vector3 direction;
        private float duration;
        public override void OnEnter()
        {
            base.OnEnter();
            duration = Mathf.Max(Time.fixedDeltaTime, 1f / (3f * Mathf.Max(.1f, attackSpeedStat)));
            var rack = GetComponent<JieSwords>();
            var slot = skillLocator ? skillLocator.primary : null;
            if (!rack || !inputBank || !healthComponent || !healthComponent.alive || !slot || slot.stateMachine != outer ||
                slot.activationState.stateType != typeof(JieDrawSword)) return;
            var aim = GetAimRay();
            if (direction.sqrMagnitude > .5f) aim.direction = direction;
            else direction = aim.direction;
            StartAimMode(aim, duration + .3f, false);
            if (NetworkServer.active) rack.Launch(aim, duration);
            if (NetworkClient.active) Util.PlaySound("Play_merc_m1_hard_swing", gameObject);
        }
        public override void OnSerialize(NetworkWriter writer) { base.OnSerialize(writer); writer.Write(direction); }
        public override void OnDeserialize(NetworkReader reader)
        {
            base.OnDeserialize(reader);
            var sent = reader.ReadVector3();
            if (sent.sqrMagnitude > .5f && sent.sqrMagnitude < 1.5f) direction = sent.normalized;
        }
        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (isAuthority && fixedAge >= duration) outer.SetNextStateToMain();
        }
        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.Skill;
    }

    public sealed class JieRecallSwords : BaseSkillState
    {
        internal Vector3? castPosition;

        public override void OnSerialize(NetworkWriter writer)
        {
            base.OnSerialize(writer);
            writer.Write(castPosition.HasValue);
            if (castPosition.HasValue) writer.Write(castPosition.Value);
        }

        public override void OnDeserialize(NetworkReader reader)
        {
            base.OnDeserialize(reader);
            castPosition = reader.ReadBoolean() ? reader.ReadVector3() : (Vector3?)null;
        }

        private Vector3 RecallPoint()
        {
            var current = characterBody.corePosition;
            if (!castPosition.HasValue) return current;
            var requested = castPosition.Value;
            if (isAuthority) return requested;

            // Allow for movement between the press and the server's transform sample.
            // Root-motion dashes can cover 16 m even when the motor reports zero velocity.
            float speed = Mathf.Max(characterBody.moveSpeed, characterMotor ? characterMotor.velocity.magnitude : 0f);
            float allowance = Mathf.Clamp(characterBody.radius + speed * .35f, 2f, 17f);
            var flight = GetComponent<JieFlight>();
            if (flight && flight.dashRecallUntil >= Time.fixedTime)
                allowance = Mathf.Max(allowance, JieDash.Distance + characterBody.radius);
            if ((requested - current).sqrMagnitude <= allowance * allowance &&
                !Physics.Linecast(current, requested, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
                return requested;
            return current;
        }

        public override void OnEnter()
        {
            base.OnEnter();
            var rack = GetComponent<JieSwords>();
            var slot = skillLocator ? skillLocator.secondary : null;
            if (!rack || !healthComponent || !healthComponent.alive || !slot || slot.stateMachine != outer ||
                slot.activationState.stateType != typeof(JieRecallSwords)) return;
            if (NetworkServer.active) rack.Recall(RecallPoint());
            else if (isAuthority) rack.AwaitConfirmation();
        }
        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (isAuthority && fixedAge >= .12f) outer.SetNextStateToMain();
        }
        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.Skill;
    }
}

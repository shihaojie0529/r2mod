using System;
using System.Collections.Generic;
using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace RiftSurvivor
{
    internal struct WeiAim
    {
        internal Ray ray;
        internal HurtBox target;
        internal bool door;
        internal Vector3 doorPoint;

        internal static WeiAim Resolve(CharacterBody caster, Ray ray, HealthComponent exclude)
        {
            var aim = new WeiAim { ray = ray };
            if (WeiPortal.TryAim(caster, ray, out aim.doorPoint))
            {
                aim.door = true;
                return aim;
            }
            var search = new BullseyeSearch
            {
                viewer = caster, searchOrigin = ray.origin, searchDirection = ray.direction,
                maxDistanceFilter = 60f, maxAngleFilter = 12f, filterByLoS = true,
                teamMaskFilter = TeamMask.GetEnemyTeams(caster.teamComponent.teamIndex),
                sortMode = BullseyeSearch.SortMode.Angle
            };
            search.RefreshCandidates();
            foreach (var hurt in search.GetResults())
            {
                if (!JieStorm.CanSee(caster, hurt) || hurt.healthComponent == exclude) continue;
                aim.target = hurt;
                break;
            }
            return aim;
        }

        internal Vector3 Direction(Vector3 origin)
        {
            var point = door ? doorPoint : target ? target.transform.position : ray.GetPoint(120f);
            return (point - origin).sqrMagnitude < .0001f ? ray.direction : (point - origin).normalized;
        }
    }

    public sealed class WeiCombat : MonoBehaviour
    {
        private struct Candidate
        {
            internal CharacterBody mob;
            internal float distance;
        }

        private const int SwarmSize = 100;
        // Spread over two seconds so a guest's client never receives 100 spawns in one tick.
        private const float SwarmPerSecond = 50f;
        private static BuffDef pasteReceipt, selectReceipt;
        private static uint nextBatch;
        private CharacterBody body;
        private WeiThrowActor held;
        private bool initialSlam;
        private int nextFurniture;
        private Ray grabAim;
        private float grabbedAt, previewAt;
        private double nextM1;
        private RemoteCadence remoteM1;
        private int lastPaste, lastSelect;
        private bool trackPaste, trackSelect;
        private GameObject preview;
        private HurtBox previewTarget;
        private readonly List<Candidate> selection = new List<Candidate>();
        private readonly List<HurtBox> swarmTargets = new List<HurtBox>();
        private int swarmLeft, swarmIndex, swarmCursor;
        private float swarmCredit, nextSwarmScan;
        private uint swarmBatch;
        private Ray swarmAim;

        // A held button can outlast a missed grab or a monster killed in hand.
        internal bool Carrying => NetworkServer.active ? held && held.CanRelease : WeiThrowActor.HeldBy(body);

        internal static void Register()
        {
            pasteReceipt = Counter("WeiAcceptedPaste");
            selectReceipt = Counter("WeiAcceptedSelection");
            WeiThrowActor.Register();
        }

        internal static void Install(GameObject prefab)
        {
            prefab.AddComponent<WeiCombat>();
            prefab.AddComponent<WeiAutoFire>();
        }

        private static BuffDef Counter(string name)
        {
            var buff = ScriptableObject.CreateInstance<BuffDef>();
            buff.name = name;
            buff.isHidden = buff.canStack = buff.ignoreGrowthNectar = true;
            if (!ContentAddition.AddBuffDef(buff)) throw new InvalidOperationException("Cannot register " + name);
            return buff;
        }

        private void Awake() => body = GetComponent<CharacterBody>();

        internal static void UpdateHeldPose(CharacterBody owner)
        {
            var combat = owner ? owner.GetComponent<WeiCombat>() : null;
            if (NetworkServer.active && combat && combat.held) combat.held.FollowHand();
        }

        internal static Vector3 Hand(CharacterBody owner, float radius) =>
            Hand(owner, radius, owner.inputBank.aimDirection);

        private static Vector3 Hand(CharacterBody owner, float radius, Vector3 facing)
        {
            var flat = Vector3.ProjectOnPlane(facing, Vector3.up).normalized;
            if (flat.sqrMagnitude < .5f) flat = owner.transform.forward;
            var offset = flat * (owner.radius + radius + .42f) + Vector3.Cross(Vector3.up, flat) * .34f
                + Vector3.up * (.52f + .25f * radius);
            return ClearOf(owner.corePosition, offset, radius);
        }

        // Stops short of terrain so a new object never starts inside a wall.
        private static Vector3 ClearOf(Vector3 center, Vector3 offset, float radius)
        {
            var ray = new Ray(center, offset.normalized);
            if (Physics.SphereCast(ray, radius, out var wall, offset.magnitude, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
                return ray.GetPoint(Mathf.Max(0f, wall.distance - .06f));
            return center + offset;
        }

        internal bool Deliver(Ray ray, float interval, HurtBox automaticTarget = null)
        {
            // The same cadence covers a manual shot immediately following auto
            // fire (including if RMB interrupted its recovery), on host or guest.
            double now = Time.fixedTimeAsDouble;
            if (Carrying) return false;
            if (body.hasEffectiveAuthority)
            {
                const double rounding = .0001;
                if (nextM1 > now + rounding) return false;
                nextM1 = Math.Max(nextM1, now - rounding) + interval;
            }
            else if (!remoteM1.TryAccept(now, interval)) return false;
            WeiAim aim;
            Vector3 origin;
            if (automaticTarget)
            {
                var bearing = automaticTarget.transform.position - body.aimOrigin;
                aim = new WeiAim { ray = new Ray(body.aimOrigin, bearing.normalized), target = automaticTarget };
                origin = Hand(body, .2f, bearing);
            }
            else
            {
                aim = WeiAim.Resolve(body, ray, null);
                origin = Hand(body, .2f);
            }
            float coefficient = WeiVitals.TrySpend(body) ? 4f : 3.2f;
            var prop = WeiThrowActor.MakeFurnitureKind(body, origin, (FurnitureKind)nextFurniture);
            nextFurniture = (nextFurniture + 1) % FurnitureCatalog.Count;
            prop.Launch(body, aim, coefficient, DamageTypeCombo.GenericPrimary, false, 0);
            return true;
        }

        internal static bool Grabbable(CharacterBody caster, CharacterBody mob) =>
            mob && mob != caster && mob.healthComponent && mob.healthComponent.alive &&
            mob.hasEffectiveAuthority && !mob.isPlayerControlled && !mob.isBoss && !mob.isChampion &&
            mob.hullClassification != HullClassification.BeetleQueen && !WeiThrowActor.Occupied(mob.healthComponent) &&
            JieStorm.CanSee(caster, mob.mainHurtBox);

        private static bool Line(Vector3 from, Vector3 to) =>
            !Physics.Linecast(from, to, LayerIndex.world.mask, QueryTriggerInteraction.Ignore);

        private void Collect(WeiAim aim)
        {
            selection.Clear();
            Vector3 center = body.corePosition;
            const float range = 25f;
            var boxed = aim.target ? aim.target.healthComponent.body : null;
            Candidate fallback = default;
            foreach (var mob in CharacterBody.readOnlyInstancesList)
            {
                if (!Grabbable(body, mob)) continue;
                var point = mob.corePosition;
                var delta = point - center;
                float distance = delta.sqrMagnitude;
                if (distance > range * range || !Line(body.aimOrigin, point)) continue;
                var value = new Candidate { mob = mob, distance = distance };
                if (mob == boxed) fallback = value;
                else selection.Add(value);
            }
            if (selection.Count == 0 && fallback.mob) selection.Add(fallback);
            selection.Sort((a,b) => a.distance.CompareTo(b.distance));
        }

        internal void Cut(Ray ray)
        {
            if (held || body.skillLocator.secondary.stock < 1) return;
            var aim = WeiAim.Resolve(body, ray, null);
            Collect(aim);
            if (selection.Count == 0) return;
            var mob = selection[0].mob;
            initialSlam = aim.target && aim.target.healthComponent.body == mob;
            grabAim = ray;
            grabbedAt = Time.fixedTime;
            held = WeiThrowActor.GrabMob(body, mob);
        }

        internal void Paste(Ray ray)
        {
            if (!held) return;
            var prop = held;
            held = null;
            if (!prop.CanRelease || !TryPay(false)) { prop.Cancel(); return; }
            var aim = WeiAim.Resolve(body, ray, prop.Passenger);
            bool sameAim = (grabAim.origin - ray.origin).sqrMagnitude < 4f && Vector3.Dot(grabAim.direction, ray.direction) > .9781f;
            bool slam = initialSlam && !aim.door && !aim.target && (Time.fixedTime - grabbedAt < .24f || sameAim);
            prop.Launch(body, aim, WeiVitals.TrySpend(body) ? 7.5f : 6f, DamageTypeCombo.GenericSecondary, slam, 0);
        }

        // Needs no target: the cast always goes off unless the last swarm is still leaving.
        internal void SummonSwarm(Ray ray)
        {
            if (swarmLeft > 0 || !TryPay(true)) return;
            SkillSpectacle.Emit(SpectacleKind.SelectAll, body.footPosition + Vector3.up * .12f, Vector3.up, 4.5f);
            swarmBatch = ++nextBatch;
            if (swarmBatch == 0) swarmBatch = ++nextBatch;
            swarmAim = ray;
            swarmLeft = SwarmSize;
            swarmIndex = 0;
            swarmCredit = 1f;
            nextSwarmScan = 0f;
        }

        private void AdvanceSwarm()
        {
            if (!DownedRescue.IsUp(body)) { swarmLeft = 0; return; }
            swarmCredit += Time.fixedDeltaTime * SwarmPerSecond;
            for (; swarmCredit >= 1f && swarmLeft > 0; swarmCredit -= 1f, swarmLeft--)
            {
                var kind = (FurnitureKind)(swarmIndex % FurnitureCatalog.Count);
                // A golden-angle spiral keeps consecutive pieces apart around Wei Ge.
                var offset = Quaternion.AngleAxis(swarmIndex * 137.508f, Vector3.up) * Vector3.forward * (2.5f + swarmIndex % 4 * 1.2f)
                    + Vector3.up * (1.5f + swarmIndex % 5 * .9f);
                swarmIndex++;
                var aim = new WeiAim { ray = swarmAim, target = NextSwarmTarget() };
                var prop = WeiThrowActor.MakeFurnitureKind(body, ClearOf(body.corePosition, offset, FurnitureCatalog.Radius(kind)), kind);
                prop.Launch(body, aim, 4f, DamageTypeCombo.GenericSpecial, false, swarmBatch, this);
            }
        }

        // Deals the visible enemies out in turn; furniture in flight also calls
        // this when its target dies, so the scan is cached across all 100 pieces.
        internal HurtBox NextSwarmTarget()
        {
            if (Time.fixedTime >= nextSwarmScan)
            {
                nextSwarmScan = Time.fixedTime + .15f;
                swarmTargets.Clear();
                foreach (var candidate in CharacterBody.readOnlyInstancesList)
                {
                    var hurt = candidate ? candidate.mainHurtBox : null;
                    if (hurt && WeiAutoFire.Valid(body, hurt)) swarmTargets.Add(hurt);
                }
            }
            for (int tries = swarmTargets.Count; tries > 0; tries--)
            {
                swarmCursor = (swarmCursor + 1) % swarmTargets.Count;
                var hurt = swarmTargets[swarmCursor];
                if (XiaoheiBallistics.IsLiveEnemy(body, hurt)) return hurt;
            }
            return null;
        }

        private bool TryPay(bool special)
        {
            var slot = special ? body.skillLocator.special : body.skillLocator.secondary;
            if (!CuiServerCharges.TrySpend(body, slot, false)) return false;
            if (!body.hasEffectiveAuthority)
            {
                var buff = special ? selectReceipt : pasteReceipt;
                body.SetBuffCount(buff.buffIndex, body.GetBuffCount(buff) % 127 + 1);
            }
            return true;
        }

        internal void AwaitReceipt(bool special)
        {
            if (special && !trackSelect) { lastSelect = body.GetBuffCount(selectReceipt); trackSelect = true; }
            if (!special && !trackPaste) { lastPaste = body.GetBuffCount(pasteReceipt); trackPaste = true; }
        }

        private void Receive(GenericSkill slot, BuffDef buff, Type expected, ref int previous)
        {
            int now = body.GetBuffCount(buff);
            int accepted = now == 0 ? 0 : (now - previous + 127) % 127;
            previous = now;
            if (accepted > 0 && slot && slot.activationState.stateType == expected)
                slot.DeductStock(Mathf.Min(slot.stock, accepted));
        }

        private void FixedUpdate()
        {
            if (NetworkServer.active && swarmLeft > 0) AdvanceSwarm();
            if (NetworkServer.active && held)
            {
                var state = body.skillLocator.secondary.stateMachine.state;
                if (!DownedRescue.IsUp(body) || !(state is WeiCut) && !(state is WeiPaste))
                { held.Cancel(); held = null; }
            }
            else if (!NetworkServer.active && body.hasEffectiveAuthority)
            {
                if (trackPaste) Receive(body.skillLocator.secondary, pasteReceipt, typeof(WeiCut), ref lastPaste);
                if (trackSelect) Receive(body.skillLocator.special, selectReceipt, typeof(WeiSelectAll), ref lastSelect);
            }
        }

        private void LateUpdate()
        {
            if (!NetworkClient.active || !body.hasEffectiveAuthority || !body.isPlayerControlled || !body.inputBank ||
                !DownedRescue.IsUp(body))
            { if (preview) preview.SetActive(false); return; }
            if (Time.time >= previewAt)
            {
                previewAt = Time.time + .06f;
                var exclude = WeiThrowActor.HeldBy(body);
                previewTarget = WeiAim.Resolve(body, body.inputBank.GetAimRay(), exclude).target;
            }
            if (previewTarget && !preview) preview = WeiFurnitureArt.Frame();
            if (!preview) return;
            bool visible = previewTarget && previewTarget.healthComponent &&
                previewTarget.healthComponent.alive && previewTarget.healthComponent.body;
            preview.SetActive(visible);
            if (!visible) return;
            var enemy = previewTarget.healthComponent.body;
            preview.transform.position = enemy.corePosition;
            preview.transform.localScale = Vector3.one * Mathf.Clamp(enemy.radius * 1.6f, .8f, 7f);
        }

        private void OnDisable()
        {
            if (preview) Destroy(preview);
            if (NetworkServer.active && held) held.Cancel();
        }
    }
}

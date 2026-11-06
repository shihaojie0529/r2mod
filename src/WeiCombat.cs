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
            internal WeiThrowActor furniture;
            internal float angle, distance;
        }

        private static BuffDef pasteReceipt, selectReceipt;
        private static uint nextBatch;
        private CharacterBody body;
        private WeiThrowActor held;
        private bool initialSlam, nextSofa;
        private Ray grabAim;
        private float grabbedAt, previewAt;
        private double nextM1;
        private int lastPaste, lastSelect;
        private bool trackPaste, trackSelect;
        private GameObject preview;
        private HurtBox previewTarget;
        private readonly List<Candidate> selection = new List<Candidate>();
        private readonly List<WeiThrowActor> reserved = new List<WeiThrowActor>(6);

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
            var ray = new Ray(owner.corePosition, offset.normalized);
            if (Physics.SphereCast(ray, radius, out var wall, offset.magnitude, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
                return ray.GetPoint(Mathf.Max(0f, wall.distance - .06f));
            return owner.corePosition + offset;
        }

        internal bool Deliver(Ray ray, float interval, HurtBox automaticTarget = null)
        {
            // The same cadence covers a manual shot immediately following auto
            // fire (including if RMB interrupted its recovery), on host or guest.
            double now = Time.fixedTimeAsDouble;
            double jitter = body.hasEffectiveAuthority ? .0001 : Math.Min(.09, interval * .20);
            if (Carrying || nextM1 > now + jitter) return false;
            nextM1 = Math.Max(nextM1, now - jitter) + interval;
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
            var prop = WeiThrowActor.MakeFurniture(body, origin, nextSofa);
            nextSofa = !nextSofa;
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

        private void Collect(WeiAim aim, bool group)
        {
            selection.Clear();
            Vector3 center = body.corePosition;
            float range = group ? 40f : 25f;
            var boxed = aim.target ? aim.target.healthComponent.body : null;
            Candidate fallback = default;
            foreach (var mob in CharacterBody.readOnlyInstancesList)
            {
                if (!Grabbable(body, mob)) continue;
                var point = mob.corePosition;
                var delta = point - center;
                float distance = delta.sqrMagnitude;
                if (distance > range * range || !Line(body.aimOrigin, point)) continue;
                float dot = Vector3.Dot(aim.ray.direction, (point - aim.ray.origin).normalized);
                if (group && dot < .7071068f) continue;
                var value = new Candidate { mob = mob, distance = distance, angle = 1f - dot };
                if (mob == boxed) fallback = value;
                else selection.Add(value);
            }
            if (group)
            {
                foreach (var prop in WeiThrowActor.Grounded)
                {
                    if (!prop || !prop.CanSelect(body)) continue;
                    var point = prop.transform.position;
                    float distance = (point - center).sqrMagnitude;
                    float dot = Vector3.Dot(aim.ray.direction, (point - aim.ray.origin).normalized);
                    if (distance <= range * range && dot >= .7071068f && Line(body.aimOrigin, point))
                        selection.Add(new Candidate { furniture = prop, angle = 1f - dot, distance = distance });
                }
            }
            if (selection.Count == 0 && fallback.mob) selection.Add(fallback);
            if (group) selection.Sort((a,b) => a.angle == b.angle ? a.distance.CompareTo(b.distance) : a.angle.CompareTo(b.angle));
            else selection.Sort((a,b) => a.distance.CompareTo(b.distance));
        }

        internal void Cut(Ray ray)
        {
            if (held || body.skillLocator.secondary.stock < 1) return;
            var aim = WeiAim.Resolve(body, ray, null);
            Collect(aim, false);
            if (selection.Count == 0) return;
            var mob = selection[0].mob;
            initialSlam = aim.target && aim.target.healthComponent.body == mob;
            grabAim = ray;
            grabbedAt = Time.fixedTime;
            held = WeiThrowActor.GrabMob(body, mob, true);
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

        internal void SelectAll(Ray ray)
        {
            var aim = WeiAim.Resolve(body, ray, held ? held.Passenger : null);
            Collect(aim, true);
            if (selection.Count == 0) return;
            // Reserve actual objects before spending. A failed cast leaves a
            // sofa's six-second clock alone and never moves or stuns a monster.
            try
            {
                foreach (var item in selection)
                {
                    var prop = item.furniture;
                    if (prop)
                    {
                        if (!prop.ReserveFurniture(body)) continue;
                    }
                    else prop = WeiThrowActor.GrabMob(body, item.mob, false);
                    if (prop) reserved.Add(prop);
                    if (reserved.Count == 6) break;
                }
                if (reserved.Count == 0 || !TryPay(true)) return;
                uint batch = ++nextBatch;
                if (batch == 0) batch = ++nextBatch;
                foreach (var prop in reserved)
                {
                    bool slam = prop.Passenger && aim.target && prop.Passenger == aim.target.healthComponent;
                    prop.Launch(body, aim, prop.Passenger ? 6f : 4f, DamageTypeCombo.GenericSpecial, slam, batch);
                }
            }
            finally
            {
                foreach (var prop in reserved) if (prop) prop.UndoReservation();
                reserved.Clear();
            }
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
            if (NetworkServer.active && held)
            {
                var state = body.skillLocator.secondary.stateMachine.state;
                if (!body.healthComponent.alive || !(state is WeiCut) && !(state is WeiPaste))
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
                !body.healthComponent || !body.healthComponent.alive)
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

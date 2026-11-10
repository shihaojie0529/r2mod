using System.Collections.Generic;
using R2API;
using RoR2;
using RoR2.CharacterAI;
using UnityEngine;
using UnityEngine.Networking;

namespace RiftSurvivor
{
    // One server-owned object represents the same passenger/furniture throughout
    // carrying, release, portal travel and its first (and only) damaging impact.
    public sealed class WeiThrowActor : NetworkBehaviour
    {
        private enum Mode : byte { Held, Flying, Falling, Grounded, Done }
        private enum Cargo : byte { Chair, Sofa, Enemy, Table, Fridge, Lamp, Wardrobe }
        private FurnitureKind Furniture => cargo == Cargo.Sofa ? FurnitureKind.Sofa : cargo == Cargo.Table ? FurnitureKind.Table : cargo == Cargo.Fridge ? FurnitureKind.Fridge : cargo == Cargo.Lamp ? FurnitureKind.Lamp : cargo == Cargo.Wardrobe ? FurnitureKind.Wardrobe : FurnitureKind.Chair;
        private static GameObject prefab;
        private static readonly List<WeiThrowActor> actors = new List<WeiThrowActor>();
        private static readonly Dictionary<HealthComponent, WeiThrowActor> claimed = new Dictionary<HealthComponent, WeiThrowActor>();
        private Mode mode;
        private Cargo cargo;
        private CharacterBody owner, passenger;
        private HealthComponent health;
        private NetworkInstanceId ownerId, mobId;
        private BaseAI[] pausedBrains;
        private Rigidbody passengerRigidbody;
        private bool wasKinematic;
        private Vector3 centerToRoot;
        private HurtBox target;
        private WeiCombat seeker;
        private Vector3 heading;
        private float damage, radius, ends, fallSpeed, blockedUntil;
        private bool crit, hand, spawned;
        private uint batch;
        private DamageTypeCombo damageType;
        private readonly RaycastHit[] castHits = new RaycastHit[18];
        private readonly Collider[] overlaps = new Collider[12];

        private bool built;
        private Vector3 lastSample, fromSample;
        private float sampleAt, flashUntil;
        private GameObject frame;
        private TrailRenderer trail;

        internal bool CanRelease => mode == Mode.Held && passenger && health && health.alive;
        internal HealthComponent Passenger => health;
        private TeamIndex ownerTeam;
        internal static bool Occupied(HealthComponent victim) => claimed.TryGetValue(victim, out var prop) && prop;

        internal static HealthComponent HeldBy(CharacterBody caster)
        {
            foreach (var item in actors)
            {
                if (!item || item.mode != Mode.Held || item.cargo != Cargo.Enemy || item.ownerId != caster.netId) continue;
                if (item.health) return item.health.alive ? item.health : null;
                var obj = ClientScene.FindLocalObject(item.mobId);
                var health = obj ? obj.GetComponent<HealthComponent>() : null;
                if (health && health.alive) return health;
            }
            return null;
        }

        internal static void Register()
        {
            prefab = PrefabAPI.CreateEmptyPrefab("WeiTelekinesisPayload", true);
            prefab.AddComponent<WeiThrowActor>();
        }

        private void OnEnable()
        {
            if (mode != Mode.Done) { actors.Add(this); return; }
            if (NetworkServer.active && spawned) NetworkServer.Destroy(gameObject);
            else Destroy(gameObject);
        }

        private static WeiThrowActor Make(CharacterBody caster, Vector3 point, Cargo kind)
        {
            var actor = Instantiate(prefab, point, Quaternion.identity).GetComponent<WeiThrowActor>();
            actor.owner = caster;
            actor.ownerId = caster.netId;
            actor.ownerTeam = caster.teamComponent.teamIndex;
            actor.cargo = kind;
            return actor;
        }

        internal static WeiThrowActor MakeFurniture(CharacterBody caster, Vector3 point, bool sofa)
        {
            return MakeFurnitureKind(caster, point, sofa ? FurnitureKind.Sofa : FurnitureKind.Chair);
        }

        internal static WeiThrowActor MakeFurnitureKind(CharacterBody caster, Vector3 point, FurnitureKind kind)
        {
            var cargo = kind == FurnitureKind.Chair ? Cargo.Chair : kind == FurnitureKind.Sofa ? Cargo.Sofa : kind == FurnitureKind.Table ? Cargo.Table : kind == FurnitureKind.Fridge ? Cargo.Fridge : kind == FurnitureKind.Lamp ? Cargo.Lamp : Cargo.Wardrobe;
            var actor = Make(caster, point, cargo);
            actor.radius = FurnitureCatalog.Radius(kind);
            actor.Spawn();
            return actor;
        }

        private void Spawn()
        {
            spawned = true;
            NetworkServer.Spawn(gameObject);
        }

        internal static WeiThrowActor GrabMob(CharacterBody caster, CharacterBody mob)
        {
            if (!WeiCombat.Grabbable(caster, mob)) return null;
            var actor = Make(caster, mob.corePosition, Cargo.Enemy);
            actor.passenger = mob;
            actor.health = mob.healthComponent;
            actor.mobId = mob.netId;
            actor.centerToRoot = mob.transform.position - mob.corePosition;
            actor.radius = Mathf.Clamp(mob.radius * .72f, .20f, .84f);
            actor.hand = true;
            claimed.Add(actor.health, actor);
            actor.SuspendPassenger();
            actor.FollowHand();
            actor.Spawn();
            return actor;
        }

        private void SuspendPassenger()
        {
            var brains = passenger.master ? passenger.master.GetComponents<BaseAI>() : null;
            if (brains != null)
            {
                var paused = new List<BaseAI>();
                foreach (var brain in brains) if (brain.enabled) { brain.enabled = false; paused.Add(brain); }
                pausedBrains = paused.ToArray();
            }
            var hurt = passenger.GetComponent<SetStateOnHurt>();
            if (hurt && hurt.canBeStunned) hurt.SetStun(.25f);
            if (!passenger.characterMotor && passenger.rigidbody)
            {
                passengerRigidbody = passenger.rigidbody;
                wasKinematic = passengerRigidbody.isKinematic;
                if (!wasKinematic)
                {
                    passengerRigidbody.velocity = Vector3.zero;
                    passengerRigidbody.angularVelocity = Vector3.zero;
                    passengerRigidbody.isKinematic = true;
                }
            }
        }

        private void RestorePassenger()
        {
            if (!ReferenceEquals(health, null) && claimed.TryGetValue(health, out var current) && current == this)
                claimed.Remove(health);
            if (passengerRigidbody) passengerRigidbody.isKinematic = wasKinematic;
            passengerRigidbody = null;
            if (pausedBrains != null)
            {
                foreach (var brain in pausedBrains) if (brain) brain.enabled = true;
                pausedBrains = null;
            }
            hand = false;
        }

        private void MoveTo(Vector3 point, bool teleported = false)
        {
            transform.position = point;
            if (!passenger || !health.alive) return;
            var root = point + centerToRoot;
            if (teleported) TeleportHelper.TeleportGameObject(passenger.gameObject, root);
            var motor = passenger.characterMotor;
            if (motor && motor.Motor)
            {
                motor.Motor.ForceUnground();
                motor.moveDirection = motor.velocity = Vector3.zero;
                motor.Motor.SetPosition(root, true);
            }
            else if (passengerRigidbody) passengerRigidbody.position = root;
            else passenger.transform.position = root;
        }

        internal void FollowHand()
        {
            if (mode != Mode.Held || !hand || !owner || !passenger) return;
            var point = WeiCombat.Hand(owner, radius);
            MoveTo(point, (point - transform.position).sqrMagnitude > 16f);
            SetDirtyBit(1);
        }

        internal void Launch(CharacterBody caster, WeiAim aim, float coefficient, DamageTypeCombo type, bool slam, uint selectionBatch, WeiCombat retargetFrom = null)
        {
            seeker = retargetFrom;
            owner = caster;
            ownerId = caster.netId;
            ownerTeam = caster.teamComponent.teamIndex;
            damage = caster.damage * coefficient * (cargo == Cargo.Enemy ? 1f : FurnitureCatalog.Multiplier(Furniture));
            damageType = type;
            crit = caster.RollCrit();
            target = slam || aim.door ? null : aim.target;
            batch = selectionBatch;
            if (slam)
            {
                float lift = Physics.SphereCast(transform.position, radius, Vector3.up, out var ceiling, 1.4f,
                    LayerIndex.world.mask, QueryTriggerInteraction.Ignore) ? Mathf.Max(0f, ceiling.distance - .04f) : 1.4f;
                MoveTo(transform.position + Vector3.up * lift);
            }
            if (aim.door && (hand || cargo != Cargo.Enemy && mode == Mode.Held) &&
                Vector3.Dot(aim.doorPoint - transform.position, aim.ray.direction) <= .05f)
                MoveTo(aim.ray.origin);
            hand = false;
            mode = Mode.Flying;
            heading = slam ? Vector3.down : aim.Direction(transform.position);
            transform.rotation = Util.QuaternionSafeLookRotation(heading);
            ends = Time.fixedTime + 6f;
            blockedUntil = 0f;
            if (batch != 0) flashUntil = Time.time + .36f;
            if (!spawned) Spawn();
            SetDirtyBit(1);
        }

        private void FixedUpdate()
        {
            if (!NetworkServer.active || mode == Mode.Done) return;
            if (mode == Mode.Grounded) { if (Time.fixedTime >= ends) Cancel(); return; }
            if (mode == Mode.Falling) { Fall(); return; }
            if (!DownedRescue.IsUp(owner) || cargo == Cargo.Enemy && (!health || !health.alive))
            { Cancel(); return; }
            if (mode == Mode.Held) { FollowHand(); return; }
            if (Time.fixedTime >= ends)
            {
                if (cargo == Cargo.Enemy) Cancel(); else StartFalling();
                return;
            }
            if (seeker && !XiaoheiBallistics.IsLiveEnemy(owner, target))
            {
                target = seeker.NextSwarmTarget();
                // Face a new target outright: at 38 m/s a piece could otherwise circle one beside it.
                if (target) heading = Util.QuaternionSafeLookRotation(target.transform.position - transform.position) * Vector3.forward;
            }
            if (target && XiaoheiBallistics.IsLiveEnemy(owner, target))
            {
                var delta = target.transform.position - transform.position;
                if (delta.sqrMagnitude > .001f)
                    heading = Vector3.RotateTowards(heading, delta.normalized, (seeker ? 14f : 4.3f) * Time.fixedDeltaTime, 0f).normalized;
            }
            float step = (cargo == Cargo.Enemy ? 32f : 38f) * Time.fixedDeltaTime;
            Travel(step);
        }

        private void Travel(float step)
        {
            var from = transform.position;
            var hit = Sweep(from, heading, step);
            if (WeiPortal.TryTransit(owner, from, from + heading * step, ref blockedUntil,
                out var arrival, out var exitHeading, out float doorDistance) && doorDistance <= hit.distance + .001f)
            {
                blockedUntil = Time.fixedTime + .30f;
                heading = exitHeading.normalized;
                target = null;
                MoveTo(arrival, true);
                from = arrival;
                step = Mathf.Max(0f, step - doorDistance);
                hit = Sweep(from, heading, step);
            }
            transform.rotation = Util.QuaternionSafeLookRotation(heading);
            if (hit.distance <= step)
            {
                MoveTo(from + heading * Mathf.Max(0, hit.distance - .02f));
                Land(hit);
            }
            else MoveTo(from + heading * step);
            SetDirtyBit(1);
        }

        private struct Contact
        {
            internal float distance;
            internal Vector3 point, normal;
            internal HurtBox hurt;
        }

        private Contact Sweep(Vector3 from, Vector3 dir, float step)
        {
            var result = new Contact { distance = float.PositiveInfinity };
            var ray = new Ray(from, dir);
            int world = LayerIndex.world.mask, precise = LayerIndex.entityPrecise.mask;
            if (Physics.CheckSphere(from, .008f, world, QueryTriggerInteraction.Ignore))
                return new Contact { distance = 0f, point = from, normal = -dir };
            if (Physics.SphereCast(ray, radius, out var wall, step, world, QueryTriggerInteraction.Ignore))
                result = new Contact { distance = wall.distance, point = wall.point, normal = wall.normal };
            if (Physics.Raycast(ray, out wall, step, world, QueryTriggerInteraction.Ignore) && wall.distance < result.distance)
                result = new Contact { distance = wall.distance, point = wall.point, normal = wall.normal };
            var starts = overlaps;
            int n = Physics.OverlapSphereNonAlloc(from, radius, starts, precise, QueryTriggerInteraction.Collide);
            if (n == starts.Length) { starts = Physics.OverlapSphere(from, radius, precise, QueryTriggerInteraction.Collide); n = starts.Length; }
            for (int i = 0; i < n; i++) Consider(starts[i], 0f, starts[i].ClosestPoint(from), from, ref result);
            if (result.distance <= .0001f) return result;
            var hits = castHits;
            float reach = Mathf.Min(step, result.distance);
            n = Physics.SphereCastNonAlloc(ray, radius, hits, reach, precise, QueryTriggerInteraction.Collide);
            if (n == hits.Length) { hits = Physics.SphereCastAll(ray, radius, reach, precise, QueryTriggerInteraction.Collide); n = hits.Length; }
            for (int i = 0; i < n; i++)
            {
                var hit = hits[i];
                Consider(hit.collider, hit.distance, hit.distance > .0001f ? hit.point : hit.collider.ClosestPoint(from), from, ref result);
            }
            return result;
        }

        private void Consider(Collider col, float distance, Vector3 point, Vector3 from, ref Contact best)
        {
            if (distance >= best.distance) return;
            var hurt = col ? col.GetComponent<HurtBox>() : null;
            if (!XiaoheiBallistics.IsLiveEnemy(owner, hurt) || hurt.healthComponent == health ||
                Physics.Linecast(from, point, LayerIndex.world.mask, QueryTriggerInteraction.Ignore)) return;
            best = new Contact { hurt = hurt, distance = distance, point = point };
        }

        private void Land(Contact impact)
        {
            // Claim before any damage/proc can reenter through another projectile.
            if (mode != Mode.Flying) return;
            mode = Mode.Done;
            var victim = health;
            var struck = impact.hurt;
            if (passenger) RestorePassenger();
            if (struck) Direct(struck.healthComponent, struck, impact.point);
            if (cargo != Cargo.Enemy) FurnitureCatalog.Burst(owner, struck ? struck.healthComponent : null, impact.point, damage, crit, Furniture);
            if (victim && victim.alive) Direct(victim, passenger.mainHurtBox, transform.position);
            if (cargo == Cargo.Enemy)
            {
                NarutoFx.Emit(NarutoEffect.Impact, impact.point, Vector3.zero, 1.6f, 1.4f, owner ? owner.gameObject : null, .1f);
                NetworkServer.Destroy(gameObject); return;
            }
            if (!struck && impact.normal.y > .48f) Rest(impact.point);
            else StartFalling();
        }

        private void Direct(HealthComponent victim, HurtBox hurt, Vector3 point)
        {
            if (!victim || !victim.alive) return;
            WeiBonds.OnThrowHit(owner, victim, point, heading, cargo != Cargo.Enemy);
            XiaoheiBallistics.HitTarget(owner, victim, hurt, damage, crit, damageType);
            if (cargo != Cargo.Enemy) FurnitureCatalog.Status(owner, victim, Furniture);
        }

        internal static bool TryIntercept(CharacterBody shooter, HealthComponent victim, Vector3 point)
        {
            if (!NetworkServer.active || !shooter || !victim || !claimed.TryGetValue(victim, out var prop) ||
                !prop || prop.mode != Mode.Flying || !prop.owner || !shooter.teamComponent ||
                prop.ownerTeam != shooter.teamComponent.teamIndex) return false;
            prop.mode = Mode.Done;
            prop.RestorePassenger();
            var unique = new HashSet<HealthComponent> { victim };
            var targets = new List<HurtBox>();
            foreach (var col in Physics.OverlapSphere(point, 6f, LayerIndex.entityPrecise.mask, QueryTriggerInteraction.Collide))
            {
                var hurt = col.GetComponent<HurtBox>();
                if (XiaoheiBallistics.IsLiveEnemy(prop.owner, hurt) && unique.Add(hurt.healthComponent)) targets.Add(hurt);
            }
            if (victim.alive) XiaoheiBallistics.HitTarget(prop.owner, victim, victim.body.mainHurtBox, prop.damage, prop.crit, prop.damageType);
            foreach (var hurt in targets)
                if (hurt && hurt.healthComponent.alive)
                    XiaoheiBallistics.HitTarget(prop.owner, hurt.healthComponent, hurt, prop.damage, prop.crit, prop.damageType);
            if (JieSwords.Impact) EffectManager.SpawnEffect(JieSwords.Impact, new EffectData { origin = point, scale = 4f }, true);
            NetworkServer.Destroy(prop.gameObject);
            return true;
        }

        private void StartFalling()
        {
            mode = Mode.Falling;
            fallSpeed = .5f;
            ends = Time.fixedTime + 8f;
            LevelFurniture();
            SetDirtyBit(1);
        }

        private void Fall()
        {
            if (Time.fixedTime >= ends) { Cancel(); return; }
            fallSpeed = Mathf.Min(35f, fallSpeed + 19.6f * Time.fixedDeltaTime);
            float dy = fallSpeed * Time.fixedDeltaTime;
            float halfHeight = FurnitureCatalog.HalfHeight(Furniture);
            var from = transform.position + Vector3.up * .20f;
            if (Physics.Raycast(from, Vector3.down, out var floor, dy + halfHeight + .20f,
                LayerIndex.world.mask, QueryTriggerInteraction.Ignore)) Rest(floor.point);
            else transform.position += Vector3.down * dy;
            SetDirtyBit(1);
        }

        private void LevelFurniture()
        {
            var flat = Vector3.ProjectOnPlane(heading, Vector3.up);
            transform.rotation = Util.QuaternionSafeLookRotation(flat.sqrMagnitude < .001f ? Vector3.forward : flat);
        }

        private void Rest(Vector3 floor)
        {
            LevelFurniture();
            transform.position = floor + Vector3.up * (FurnitureCatalog.HalfHeight(Furniture));
            mode = Mode.Grounded;
            ends = Time.fixedTime + 6f;
            SetDirtyBit(1);
        }

        internal void Cancel()
        {
            if (mode == Mode.Done) return;
            mode = Mode.Done;
            RestorePassenger();
            if (spawned) NetworkServer.Destroy(gameObject);
            else Destroy(gameObject);
        }

        public override float GetNetworkSendInterval() => .05f;
        public override bool OnSerialize(NetworkWriter writer, bool initialState)
        {
            writer.Write((byte)mode); writer.Write((byte)cargo); writer.Write(ownerId); writer.Write(mobId);
            writer.Write(transform.position); writer.Write(transform.rotation);
            writer.Write(batch);
            writer.Write(Mathf.Clamp(flashUntil - Time.time, 0f, .36f));
            return true;
        }
        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            var previous = mode;
            mode = (Mode)reader.ReadByte(); cargo = (Cargo)reader.ReadByte();
            ownerId = reader.ReadNetworkId(); mobId = reader.ReadNetworkId();
            fromSample = transform.position; lastSample = reader.ReadVector3(); transform.rotation = reader.ReadQuaternion();
            batch = reader.ReadUInt32();
            flashUntil = Time.time + reader.ReadSingle();
            if (initialState || previous != mode || mode == Mode.Grounded || (fromSample - lastSample).sqrMagnitude > 16f)
            {
                fromSample = transform.position = lastSample;
                if (trail) trail.Clear();
            }
            sampleAt = Time.time;
        }

        private void LateUpdate()
        {
            if (!NetworkClient.active) return;
            if (!NetworkServer.active) transform.position = Vector3.Lerp(fromSample, lastSample, (Time.time - sampleAt) / .05f);
            if (!built)
            {
                built = true;
                if (cargo != Cargo.Enemy) ExtendedFurnitureArt.Attach(transform, Furniture);
                trail = WeiFurnitureArt.Trail(transform, cargo == Cargo.Enemy);
                if (cargo != Cargo.Enemy)
                {
                    trail.startColor = FurnitureCatalog.ColorFor(Furniture); trail.startWidth = .42f; trail.time = .32f;
                    var halo = new GameObject("FurnitureElementHalo").transform; halo.SetParent(transform,false);
                    var geo = halo.gameObject.AddComponent<RadiantGeometry>();
                    geo.Ring("CargoRune", FurnitureCatalog.ColorFor(Furniture), FurnitureCatalog.Radius(Furniture) + .25f, .045f, Quaternion.identity, Vector3.zero, 32);
                }
            }
            trail.emitting = mode == Mode.Flying;
            if(batch != 0)
            {
                trail.startColor=new Color(.3f,1.2f,2f,.9f);
                trail.endColor=new Color(.35f,.5f,1.8f,0f);
                trail.startWidth=.8f;trail.time=.5f;
            }
            if (flashUntil > Time.time && !frame)
            {
                frame = WeiFurnitureArt.Frame();
                frame.transform.SetParent(transform, false);
                frame.transform.localScale = Vector3.one * 1.4f;
            }
            if (frame) frame.SetActive(Time.time < flashUntil);
        }
        private void OnDisable()
        {
            actors.Remove(this);
            // NetworkServer.active may already be false during scene teardown.
            // Client replicas carry only IDs and have no local passenger to restore.
            RestorePassenger();
            mode = Mode.Done;
        }
    }
}

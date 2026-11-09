using System;
using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace RiftSurvivor
{
    // Only this survivor's body receives the damage modifier or natural recovery.
    public sealed class CuiVitals : MonoBehaviour, IOnIncomingDamageServerReceiver
    {
        private const int MaxPump = 6;
        private static BuffDef pump, runnerHigh, chargeGuard, retainedGuard, castReceipt;
        private CharacterBody body;
        private CuiPumpMemory memory;
        private float regenTime, regenFraction;
        private int previousPump, lastReceipt;
        private bool trackingReceipts;
        private float lastCombatAt = float.NegativeInfinity;
        private float chargeBegan, chargeExited;
        private bool chargePermit, windingUp;
        internal const float ShieldPerPunch = .08f, ShieldPerUppercut = .12f;
        internal const float ShieldCap = 2f, ShieldDecay = .02f, CombatGrace = 5f;

        internal static void Register()
        {
            pump = Buff("CuiPump", "pump", new Color(.96f,.62f,.24f), true);
            runnerHigh = Buff("CuiPunchRecovery", "punch", new Color(.58f,.94f,.65f));
            chargeGuard = Buff("CuiChargeGuard", "charge", new Color(.39f,.75f,.93f));
            retainedGuard = Buff("CuiRetainedGuard", "slam", new Color(.94f,.74f,.39f));
            // Receipts travel with the vanilla reliable buff count. A remote player
            // pays stock only after the server verifies the target and accepts the cast.
            castReceipt = Buff("CuiShakerReceipt", "shaker", Color.white, true, true);
            ContentAddition.AddEntityState<ShakerAim>(out bool aim);
            if (!aim) throw new InvalidOperationException("Failed to register shaker states.");
            GlobalEventManager.onCharacterDeathServer += ClearPumpOnDeath;
            On.RoR2.CharacterBody.RecalculateStats += (orig, owner) =>
            {
                orig(owner);
                if (owner.GetComponent<CuiVitals>()) owner.maxBarrier = owner.maxHealth * ShieldCap;
            };
            On.RoR2.HealthComponent.GetBarrierDecayRate += (orig, health) =>
            {
                var vitals = health.body ? health.body.GetComponent<CuiVitals>() : null;
                return vitals ? vitals.BarrierDecayRate() : orig(health);
            };
        }

        private static BuffDef Buff(string name, string icon, Color color, bool stacks = false, bool hidden = false)
        {
            var buff = ScriptableObject.CreateInstance<BuffDef>();
            buff.name = name;
            buff.iconSprite = GymIcons.Get(icon);
            buff.buffColor = color;
            buff.canStack = stacks;
            buff.isHidden = hidden;
            buff.ignoreGrowthNectar = hidden;
            if (!ContentAddition.AddBuffDef(buff)) throw new InvalidOperationException("Cannot register " + name);
            return buff;
        }

        internal static void Install(GameObject prefab) => prefab.AddComponent<CuiVitals>();

        private void Awake() => body = GetComponent<CharacterBody>();

        private void Start()
        {
            if (!NetworkServer.active) return;
            var health = body.healthComponent;
            if (Array.IndexOf(health.onIncomingDamageReceivers ?? Array.Empty<IOnIncomingDamageServerReceiver>(), this) < 0)
                health.AddOnIncomingDamageServerReceiver(this);
            RestorePump();
        }

        private void RestorePump()
        {
            if (memory || !body.master) return;
            memory = body.master.GetComponent<CuiPumpMemory>();
            if (memory)
                body.SetBuffCount(pump.buffIndex, memory.count);
            else
            {
                memory = body.master.gameObject.AddComponent<CuiPumpMemory>();
                memory.count = body.GetBuffCount(pump);
            }
        }

        private static void ClearPumpOnDeath(DamageReport report)
        {
            if (!NetworkServer.active || !report.victimMaster) return;
            var saved = report.victimMaster.GetComponent<CuiPumpMemory>();
            if (saved) saved.count = 0;
        }

        internal static void PunchHit(CharacterBody source, bool third)
        {
            if (!GainPump(source)) return;
            var vitals = source.GetComponent<CuiVitals>();
            vitals.lastCombatAt = Time.fixedTime;
            source.healthComponent.AddBarrier(source.healthComponent.fullHealth * (third ? ShieldPerUppercut : ShieldPerPunch));
            if (third) source.AddTimedBuff(runnerHigh, 4f, 1);
        }

        internal static bool GainPump(CharacterBody source)
        {
            if (!NetworkServer.active || !source || !source.healthComponent.alive) return false;
            var vitals = source.GetComponent<CuiVitals>();
            if (!vitals) return false;
            vitals.RestorePump();
            int count = Mathf.Min(MaxPump, source.GetBuffCount(pump) + 1);
            source.SetBuffCount(pump.buffIndex, count);
            if (vitals.memory) vitals.memory.count = count;
            return true;
        }

        internal static int PumpCount(CharacterBody source) => source && pump ? source.GetBuffCount(pump) : 0;

        internal static bool IsFull(CharacterBody source) => source && source.GetBuffCount(pump) >= MaxPump;

        internal static bool TrySpendFull(CharacterBody source)
        {
            if (!NetworkServer.active || !IsFull(source)) return false;
            var vitals = source.GetComponent<CuiVitals>();
            if (!vitals) return false;
            vitals.RestorePump();
            source.SetBuffCount(pump.buffIndex, 0);
            if (vitals.memory) vitals.memory.count = 0;
            source.AddTimedBuff(retainedGuard, 2f, 1);
            return true;
        }

        internal static void SetCharging(CharacterBody source, bool charging)
        {
            if (NetworkServer.active && source)
                source.SetBuffCount(chargeGuard.buffIndex, charging ? 1 : 0);
        }

        internal float BarrierDecayRate() => body && body.healthComponent &&
            Time.fixedTime - lastCombatAt >= CombatGrace && body.outOfCombat && body.outOfDanger
                ? body.healthComponent.fullHealth * ShieldDecay : 0f;

        internal void BeginCharge()
        {
            chargePermit = windingUp = true;
            chargeBegan = Time.fixedTime;
        }
        internal void EndChargeWindup()
        {
            windingUp = false;
            chargeExited = Time.fixedTime;
        }
        internal bool ReleaseCharge(float requested, out float seconds)
        {
            seconds = 0f;
            if (!NetworkServer.active || !chargePermit || !body.healthComponent.alive ||
                (!windingUp && Time.fixedTime - chargeExited > .25f) ||
                Time.fixedTime - chargeBegan > CuiChargeWindup.MaxHold + 1f || float.IsNaN(requested) || float.IsInfinity(requested)) return false;
            chargePermit = windingUp = false;
            seconds = Mathf.Clamp(requested, 0f, Mathf.Min(CuiChargeWindup.MaxHold,Time.fixedTime-chargeBegan+.10f));
            return true;
        }

        public void OnIncomingDamageServer(DamageInfo damageInfo)
        {
            if (!damageInfo.rejected && damageInfo.damage > 0f) lastCombatAt = Time.fixedTime;
            float pumpReduction = body.HasBuff(retainedGuard) ? .30f : .05f * body.GetBuffCount(pump);
            damageInfo.damage *= (1f - pumpReduction) * (body.HasBuff(chargeGuard) ? .4f : 1f);
        }

        internal static void ConfirmShaker(CharacterBody source)
        {
            int serial = source.GetBuffCount(castReceipt);
            source.SetBuffCount(castReceipt.buffIndex, serial % 127 + 1);
        }

        internal void AwaitShakerReceipt()
        {
            if (trackingReceipts) return;
            // Ignore historical casts when acquiring authority over an existing
            // body. After our first attempt, only accepted casts advance the serial.
            lastReceipt = body.GetBuffCount(castReceipt);
            trackingReceipts = true;
        }

        private void FixedUpdate()
        {
            if (!body || !body.healthComponent || !body.healthComponent.alive) return;
            int count = body.GetBuffCount(pump);
            if (count == MaxPump && previousPump != MaxPump)
                CuiMotion.Play(body, "ready", .85f);
            previousPump = count;
            if (!NetworkServer.active)
            {
                int receipt = body.GetBuffCount(castReceipt);
                if (trackingReceipts && body.hasEffectiveAuthority && receipt != 0 && receipt != lastReceipt &&
                    ShakerAim.Equipped(body.skillLocator.special))
                {
                    int accepted = lastReceipt == 0 ? receipt : (receipt - lastReceipt + 127) % 127;
                    body.skillLocator.special.DeductStock(Mathf.Min(accepted, body.skillLocator.special.stock));
                }
                lastReceipt = receipt;
                return;
            }

            RestorePump();
            if (body.healthComponent.barrier > body.healthComponent.fullBarrier)
                body.healthComponent.Networkbarrier = body.healthComponent.fullBarrier;
            regenTime += Time.fixedDeltaTime;
            regenFraction += Time.fixedDeltaTime * (body.HasBuff(runnerHigh) ? .04f : .02f);
            if (regenTime >= .25f)
            {
                body.healthComponent.Heal(body.healthComponent.fullHealth * regenFraction, default, false);
                regenTime = regenFraction = 0f;
            }
        }
    }

    // The master survives stage transitions. An actual death resets its saved count.
    internal sealed class CuiPumpMemory : MonoBehaviour { internal int count; }

}

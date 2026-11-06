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
        internal static BuffDef ShakerRecovery { get; private set; }
        private CharacterBody body;
        private CuiPumpMemory memory;
        private float regenTime, regenFraction;
        private int previousPump, lastReceipt;
        private bool trackingReceipts;

        internal static void Register()
        {
            pump = Buff("CuiPump", "pump", new Color(.96f,.62f,.24f), true);
            runnerHigh = Buff("CuiPunchRecovery", "punch", new Color(.58f,.94f,.65f));
            chargeGuard = Buff("CuiChargeGuard", "charge", new Color(.39f,.75f,.93f));
            retainedGuard = Buff("CuiRetainedGuard", "slam", new Color(.94f,.74f,.39f));
            ShakerRecovery = Buff("CuiShakerRecovery", "shaker", new Color(.38f,.88f,.77f));
            // Receipts travel with the vanilla reliable buff count. A remote player
            // pays stock only after the server verifies the target and accepts the cast.
            castReceipt = Buff("CuiShakerReceipt", "shaker", Color.white, true, true);
            ContentAddition.AddEntityState<ShakerAim>(out bool aim);
            ContentAddition.AddEntityState<ShakerCast>(out bool cast);
            if (!aim || !cast) throw new InvalidOperationException("Failed to register shaker states.");
            GlobalEventManager.onCharacterDeathServer += ClearPumpOnDeath;
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
            if (GainPump(source) && third)
                source.AddTimedBuff(runnerHigh, 4f, 1);
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

        public void OnIncomingDamageServer(DamageInfo damageInfo)
        {
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

    internal sealed class CuiShakerHealing : MonoBehaviour
    {
        private CharacterBody recipient;
        private int ticks;
        private float nextTick, fraction, endsAt;

        internal static void Serve(CharacterBody target, bool empowered)
        {
            var effect = target.GetComponent<CuiShakerHealing>();
            if (!effect) effect = target.gameObject.AddComponent<CuiShakerHealing>();
            effect.recipient = target;
            effect.fraction = empowered ? .10f : .06f;
            effect.nextTick = Time.fixedTime;
            effect.endsAt = Time.fixedTime + 8f;
            effect.ticks = 8;
            target.AddTimedBuff(CuiVitals.ShakerRecovery, 8f, 1);
            // A new cup replaces the remaining course and pays its first sip on receipt.
            effect.HealIfDue();
        }

        private void FixedUpdate()
        {
            if (!NetworkServer.active || !recipient || !recipient.healthComponent.alive || Time.fixedTime >= endsAt)
            {
                enabled = false;
                return;
            }
            HealIfDue();
        }

        private void HealIfDue()
        {
            enabled = true;
            if (ticks <= 0 || Time.fixedTime < nextTick) return;
            ticks--;
            nextTick += 1f;
            recipient.healthComponent.Heal(recipient.healthComponent.fullHealth * fraction, default, true);
        }
    }
}

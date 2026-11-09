using System;
using System.Collections.Generic;
using RoR2;
using RoR2.Skills;
using UnityEngine;
using UnityEngine.Networking;

namespace RiftSurvivor
{
    // A remote player's skill slot belongs to the client. The server owns its own
    // spending limit, including when the game's proxy never runs native recharge.
    internal sealed class CuiServerCharges : MonoBehaviour
    {
        private sealed class Account
        {
            internal GenericSkill slot;
            internal SkillDef skill;
            internal int capacity, available, lastNative;
            internal double fraction, sampledAt;
        }

        private readonly List<Account> slots = new List<Account>(3);
        private CharacterBody body;

        internal static bool TrySpend(CharacterBody source, GenericSkill slot, bool prepaidByLocalSkill)
        {
            if (!NetworkServer.active || !source || !slot || !slot.skillDef) return false;
            if (source.hasEffectiveAuthority)
            {
                if (prepaidByLocalSkill) return true;
                if (slot.stock < 1) return false;
                slot.DeductStock(1);
                return true;
            }

            var ledger = source.GetComponent<CuiServerCharges>();
            if (!ledger)
            {
                ledger = source.gameObject.AddComponent<CuiServerCharges>();
                ledger.body = source;
            }
            return ledger.TrySpendRemote(slot, Time.fixedTimeAsDouble);
        }

        internal static void Reset(GenericSkill slot)
        {
            DirectSkillKillReset.Apply(slot);
            var ledger = slot ? slot.GetComponent<CuiServerCharges>() : null;
            if (!ledger) return;
            foreach (var account in ledger.slots)
            {
                if (account.slot != slot || account.skill != slot.skillDef) continue;
                account.capacity = account.available = Math.Max(0, slot.maxStock);
                account.lastNative = slot.stock;
                account.fraction = 0d;
                account.sampledAt = Time.fixedTimeAsDouble;
            }
        }

        private bool TrySpendRemote(GenericSkill slot, double now)
        {
            Account account = null;
            for (int i = slots.Count - 1; i >= 0; i--)
            {
                if (slots[i].slot != slot) continue;
                if (slots[i].skill == slot.skillDef) account = slots[i];
                else slots.RemoveAt(i);
                break;
            }
            if (account == null)
            {
                int capacity = Math.Max(0, slot.maxStock);
                account = new Account
                {
                    slot = slot, skill = slot.skillDef, capacity = capacity,
                    available = slot.skillDef.fullRestockOnAssign ? capacity : Mathf.Clamp(slot.stock, 0, capacity),
                    sampledAt = now, lastNative = slot.stock
                };
                slots.Add(account);
            }

            Advance(account, now);
            if (account.available < 1) return false;
            account.available--;
            // Mirror to the server's local slot so a later real restock (an ammo
            // pickup, for example) is observable, even on proxies without recharge.
            slot.stock = account.available;
            account.lastNative = slot.stock;
            return true;
        }

        private void FixedUpdate()
        {
            if (!NetworkServer.active || !body || body.hasEffectiveAuthority)
            {
                slots.Clear();
                return;
            }
            double now = Time.fixedTimeAsDouble;
            for (int i = slots.Count - 1; i >= 0; i--)
            {
                var account = slots[i];
                if (!account.slot || account.slot.skillDef != account.skill) slots.RemoveAt(i);
                else Advance(account, now);
            }
        }

        private static void Advance(Account account, double now)
        {
            GenericSkill slot = account.slot;
            int capacity = Math.Max(0, slot.maxStock);
            if (capacity > account.capacity)
                account.available += capacity - account.capacity;
            account.capacity = capacity;
            account.available = Math.Min(account.available, capacity);

            double elapsed = Math.Max(0d, now - account.sampledAt);
            account.sampledAt = now;
            float interval = slot.finalRechargeInterval;
            int batch = slot.rechargeStock;
            if (account.available < capacity && batch > 0)
            {
                if (interval <= 0f) account.available = capacity;
                else if (!float.IsNaN(interval) && !float.IsInfinity(interval))
                {
                    account.fraction += elapsed / interval;
                    double batchesNeeded = Math.Ceiling((capacity - account.available) / (double)batch);
                    double charged = Math.Min(batchesNeeded, Math.Floor(account.fraction + 1e-8));
                    account.available = (int)Math.Min(capacity, account.available + charged * batch);
                    account.fraction = Math.Max(0d, account.fraction - charged);
                }
            }

            int native = Mathf.Clamp(slot.stock, 0, capacity);
            if (native > account.lastNative && native > account.available)
            {
                // An observed native grant can bring a charge forward. Align the
                // partial timer so it cannot be paid again by the server clock.
                account.available = native;
                account.fraction = interval > 0f ? Mathf.Clamp01(slot.rechargeStopwatch / interval) : 0d;
            }
            if (account.available == capacity) account.fraction = 0d;
            if (native < account.available)
            {
                // Restart the native proxy's partial timer at the same point when
                // our clock beats it, preventing a delayed second native payout.
                slot.stock = account.available;
                slot.rechargeStopwatch = interval > 0f && !float.IsInfinity(interval)
                    ? (float)(account.fraction * interval) : 0f;
            }
            account.lastNative = slot.stock;
        }
    }
}

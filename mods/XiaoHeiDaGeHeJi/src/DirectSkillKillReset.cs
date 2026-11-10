using System;
using System.Collections.Generic;
using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace RiftSurvivor
{
    // Track the exact direct hit, rather than its damage source or a later proc.
    internal sealed class DirectSkillKillReset : MonoBehaviour
    {
        private static readonly Dictionary<DamageInfo, CharacterBody> hits = new Dictionary<DamageInfo, CharacterBody>();
        private static BuffDef receipt;
        [SerializeField] internal SkillSlot slot = SkillSlot.Secondary;
        private CharacterBody body;
        private bool pending;
        private int previous;

        internal static void Register()
        {
            receipt = ScriptableObject.CreateInstance<BuffDef>();
            receipt.name = "DirectSkillKillReset";
            receipt.canStack = receipt.isHidden = receipt.ignoreGrowthNectar = true;
            if (!ContentAddition.AddBuffDef(receipt)) throw new InvalidOperationException("Cannot register direct skill reset");
            GlobalEventManager.onCharacterDeathServer += OnDeath;
        }

        private void Awake() => body = GetComponent<CharacterBody>();
        internal static void Track(DamageInfo info, CharacterBody attacker) => hits[info] = attacker;
        internal static void Forget(DamageInfo info) { if (info != null) hits.Remove(info); }

        private static void OnDeath(DamageReport report)
        {
            if (!NetworkServer.active || report == null || report.damageInfo == null ||
                !hits.TryGetValue(report.damageInfo, out var attacker) || !attacker ||
                report.attackerBody != attacker || !report.victimBody || !attacker.teamComponent ||
                !TeamMask.GetEnemyTeams(attacker.teamComponent.teamIndex).HasTeam(report.victimTeamIndex)) return;
            var reset = attacker.GetComponent<DirectSkillKillReset>();
            if (reset) reset.pending = true;
        }

        // Run after skill execution and Jie's recall-spending receipts, so a kill
        // inside OnEnter cannot have its reward consumed by the original cast.
        private void LateUpdate()
        {
            if (!body || !body.healthComponent || !body.healthComponent.alive) { pending = false; return; }
            if (NetworkServer.active)
            {
                if (!pending) return;
                pending = false;
                CuiServerCharges.Reset(body.skillLocator.GetSkill(slot));
                if (!body.hasEffectiveAuthority)
                    body.SetBuffCount(receipt.buffIndex, body.GetBuffCount(receipt) % 127 + 1);
            }
            else if (body.hasEffectiveAuthority)
            {
                int current = body.GetBuffCount(receipt);
                if (current != previous && current != 0) Apply(body.skillLocator.GetSkill(slot));
                previous = current;
            }
        }

        internal static void Apply(GenericSkill slot)
        {
            if (!slot) return;
            slot.stock = slot.maxStock;
            slot.rechargeStopwatch = 0f;
        }
    }
}

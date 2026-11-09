using System;
using System.Collections.Generic;
using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace RiftSurvivor
{
    internal readonly struct DinnerCredit
    {
        internal readonly CharacterBody Wei;
        internal readonly TeamIndex Team;
        internal readonly float Damage;
        internal DinnerCredit(CharacterBody owner)
        {
            Wei = owner;
            Team = owner.teamComponent.teamIndex;
            Damage = owner.damage;
        }
    }

    internal sealed class WeiBanquet : MonoBehaviour
    {
        internal const float FallTime = .36f;
        private static readonly Dictionary<TeamIndex, float> nextByTeam = new Dictionary<TeamIndex, float>();
        private static GameObject effect;
        private DinnerCredit credit;
        private float landsAt, damage;
        private bool crit;

        internal static void Register()
        {
            effect = CombatVisuals.EffectPrefab("WeiDinnerForFour");
            effect.AddComponent<EffectComponent>().applyScale = false;
            var attributes = effect.AddComponent<VFXAttributes>();
            attributes.vfxPriority = VFXAttributes.VFXPriority.Always;
            attributes.DoNotPool = true;
            effect.AddComponent<WeiDinnerTable>();
            if (!ContentAddition.AddEffect(effect)) throw new InvalidOperationException("Cannot register dinner table.");
            Run.onRunStartGlobal += _ => nextByTeam.Clear();
        }

        internal static bool TryReserve(TeamIndex team)
        {
            if (!NetworkServer.active || (nextByTeam.TryGetValue(team, out float ready) && Time.fixedTime < ready)) return false;
            nextByTeam[team] = Time.fixedTime + 20f;
            return true;
        }

        internal static void Drop(DinnerCredit credit, Vector3 point)
        {
            // The landing belongs to this scene, not to the victim's lifetime.
            var landing = new GameObject("WeiBanquetLanding").AddComponent<WeiBanquet>();
            landing.transform.position = point;
            landing.credit = credit;
            landing.damage = (credit.Wei ? credit.Wei.damage : credit.Damage) * 10f;
            landing.crit = credit.Wei && credit.Wei.RollCrit();
            landing.landsAt = Time.fixedTime + FallTime;
            EffectManager.SpawnEffect(effect, new EffectData { origin = point }, true);
        }

        private void FixedUpdate()
        {
            if (Time.fixedTime < landsAt) return;
            enabled = false;
            var attacker = credit.Wei ? credit.Wei.gameObject : null;
            if (NetworkServer.active)
                new BlastAttack
                {
                    attacker = attacker, inflictor = attacker, teamIndex = credit.Team,
                    attackerFiltering = AttackerFiltering.NeverHitSelf, position = transform.position,
                    radius = 8f, baseDamage = damage, crit = crit, procCoefficient = 1f,
                    damageType = DamageTypeCombo.Generic, falloffModel = BlastAttack.FalloffModel.None,
                    losType = BlastAttack.LoSType.NearestHit
                }.Fire();
            Destroy(gameObject);
        }
    }
}

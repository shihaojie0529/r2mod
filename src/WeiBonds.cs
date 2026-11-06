using System;
using System.Collections;
using System.Collections.Generic;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace RiftSurvivor
{
    [Flags]
    internal enum DinnerStrike { Sword = 1, Cui = 2, Throw = 4, All = Sword | Cui | Throw }

    internal static class WeiBonds
    {
        // Called at direct contact, before normal damage can kill the marked target.
        // Neither the installation ray nor the banquet blast feeds this counter.
        internal static void DirectHit(CharacterBody attacker, HealthComponent victim, Vector3 point, DinnerStrike strike)
        {
            if (!NetworkServer.active || !attacker || !attacker.teamComponent) return;
            var marks = XiaoheiCombat.ActiveMarks(victim);
            if (marks) marks.DinnerHit(attacker, strike, point);
        }

        internal static void OnThrowHit(CharacterBody wei, HealthComponent victim, Vector3 point, Vector3 direction, bool furniture)
        {
            if (!NetworkServer.active || !wei || !victim || !victim.body) return;
            DirectHit(wei, victim, point, DinnerStrike.Throw);
            if (!furniture) return;
            var vitals = wei.GetComponent<WeiVitals>();
            if (!vitals) return;
            var partners = new List<CharacterBody>();
            JieSwords.FindOwner(victim, wei.teamComponent.teamIndex, partners);
            foreach (var jie in partners)
                if (vitals.ClaimInstallation(jie))
                    vitals.StartCoroutine(Installation(jie, victim, victim.body.corePosition, direction));
        }

        private static IEnumerator Installation(CharacterBody jie, HealthComponent victim, Vector3 point, Vector3 direction)
        {
            // Allow the furniture's direct hit to finish before its follow-up.
            yield return new WaitForFixedUpdate();
            if (!jie || !NetworkServer.active) yield break;
            if (victim && victim.alive)
                XiaoheiBallistics.HitTarget(jie, victim, victim.body.mainHurtBox, jie.damage * 3.2f,
                    jie.RollCrit(), DamageTypeCombo.Generic);
            if (direction.sqrMagnitude < .001f) direction = jie.transform.forward;
            var end = JieBonds.PierceLine(jie, point, direction, 10f, 3.2f, victim, JieSwords.Impact);
            XiaoheiBallistics.Trace(JieSwords.PierceTracer, point, end);
        }

        internal static void TryCatch(CharacterBody shooter, HealthComponent victim, Vector3 point)
        {
            if (!NetworkServer.active || !shooter || !victim) return;
            var gunner = shooter.GetComponent<XiaoheiCombat>();
            if (!gunner || !WeiThrowActor.TryIntercept(shooter, victim, point)) return;
            gunner.AddHighlight();
            XiaoheiCombat.Speak("WEIGE_PASS");
            XiaoheiCombat.Speak("WEIGE_CATCH");
        }

        internal static void OnCuiThroughDoor(CharacterBody wei, CharacterBody traveler)
        {
            if (!NetworkServer.active || !wei || !traveler || !traveler.GetComponent<CuiVitals>() ||
                !traveler.healthComponent.alive || wei.teamComponent.teamIndex != traveler.teamComponent.teamIndex) return;
            traveler.healthComponent.AddBarrier(traveler.healthComponent.fullHealth * .12f);
            WeiVitals.GrantStack(wei);
            XiaoheiCombat.Speak("WEIGE_COACH");
            XiaoheiCombat.Speak("WEIGE_SHARING");
        }
    }
}

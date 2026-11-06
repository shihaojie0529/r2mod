using System;
using System.Collections.Generic;
using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;

namespace RiftSurvivor
{
    [DisallowMultipleComponent]
    public sealed class XiaoheiCombat : MonoBehaviour
    {
        private static BuffDef highlight, marked;
        private static GameObject markNotice;
        internal static GameObject PistolTracer, EchoTracer, Impact, SeekerEffect, MuzzleFlash;

        private CharacterBody body;
        private readonly XiaoheiStreak streak = new XiaoheiStreak();
        private bool nextShotAfterCup;
        private double nextPrimary;

        internal static void Register()
        {
            highlight = RegisterBuff("XiaoheiHighlight", "xh-highlight", new Color(.32f, 1f, .88f), true);
            marked = RegisterBuff("XiaoheiMarked", "xh-seeker", new Color(1f, .53f, .23f), false);
            PistolTracer = XiaoheiShotVisual.Prefab("XiaoheiPistolStreak", XiaoheiShotKind.Pistol);
            EchoTracer = XiaoheiShotVisual.Prefab("XiaoheiReplayStreak", XiaoheiShotKind.Replay);
            Impact = Load("Common/VFX/OmniImpactVFX");
            SeekerEffect = XiaoheiShotVisual.Prefab("XiaoheiAmberRound", XiaoheiShotKind.Seeker);
            MuzzleFlash = Load("Common/VFX/Muzzleflash1");
            markNotice = PrefabAPI.CreateEmptyPrefab("XiaoheiMarkNotice", false);
            markNotice.AddComponent<EffectComponent>();
            var vfx = markNotice.AddComponent<VFXAttributes>();
            vfx.DoNotPool = true;
            vfx.vfxPriority = VFXAttributes.VFXPriority.Always;
            markNotice.AddComponent<XiaoheiMarkNotice>();
            if (!ContentAddition.AddEffect(markNotice)) throw new InvalidOperationException("Cannot register XiaoheiMarkNotice");
            CharacterBody.onBodyStartGlobal += ShowExistingMark;
            CharacterBody.onBuffFirstStackGained += OnBuffGained;
        }

        internal static void Install(GameObject prefab) => prefab.AddComponent<XiaoheiCombat>();

        private static BuffDef RegisterBuff(string name, string icon, Color color, bool positive)
        {
            var buff = ScriptableObject.CreateInstance<BuffDef>();
            buff.name = name;
            buff.iconSprite = GymIcons.Get(icon);
            buff.buffColor = color;
            buff.canStack = positive;
            buff.isDebuff = !positive;
            if (!ContentAddition.AddBuffDef(buff)) throw new InvalidOperationException("Cannot register " + name);
            return buff;
        }

        private static GameObject Load(string path) => Addressables.LoadAssetAsync<GameObject>(
            "RoR2/Base/" + path + ".prefab").WaitForCompletion();

        private static void ShowExistingMark(CharacterBody target)
        {
            if (target.HasBuff(marked)) ShowMark(target);
        }

        private static void OnBuffGained(CharacterBody target, BuffDef buff)
        {
            if (buff == marked) ShowMark(target);
        }

        internal static void ShowMark(CharacterBody target, Vector3? contact = null)
        {
            if (!NetworkClient.active || !target || !target.healthComponent || !target.healthComponent.alive) return;
            var visual = target.GetComponent<XiaoheiMarkVisual>();
            if (!visual) visual = target.gameObject.AddComponent<XiaoheiMarkVisual>();
            visual.Show(target, marked, contact);
        }

        private void Awake() => body = GetComponent<CharacterBody>();

        internal bool AcceptRemotePrimary(float interval)
        {
            // A remote owner can interrupt its own states. Preserve the sustained
            // cadence while allowing occasional state packets to arrive in a batch.
            double now = Time.fixedTimeAsDouble;
            const double jitter = .09;
            if (nextPrimary > now + jitter) return false;
            nextPrimary = Math.Max(nextPrimary, now - jitter) + interval;
            return true;
        }

        internal void PrimaryResult(bool landed)
        {
            if (ConsumeCupLine() && !landed) Speak("XIAOHEI_BOND_MISS");
            if (!landed) streak.Miss(Time.fixedTime);
            else if (streak.Hit()) RewardStreak();
        }

        internal bool ConsumeCupLine()
        {
            bool consumed = nextShotAfterCup;
            nextShotAfterCup = false;
            return consumed;
        }

        internal uint PendingMiss => streak.MissId;

        internal void SeekerResult(HealthComponent victim, bool hit, uint attemptedRepair, bool afterCup, Vector3 contact)
        {
            if (!hit)
            {
                if (afterCup) Speak("XIAOHEI_BOND_MISS");
                return;
            }
            if (!body.healthComponent.alive) return;
            if (streak.Repair(attemptedRepair, Time.fixedTime)) RewardStreak();
            if (!victim || !victim.alive || !victim.body) return;
            var marks = victim.GetComponent<XiaoheiMarks>();
            if (!marks) marks = victim.gameObject.AddComponent<XiaoheiMarks>();
            marks.Refresh(body, victim.body.HasBuff(marked));
            victim.body.AddTimedBuff(marked, 5f, 1);
            var data = new EffectData { origin = contact };
            data.SetNetworkedObjectReference(victim.gameObject);
            EffectManager.SpawnEffect(markNotice, data, true);
        }

        private void RewardStreak()
        {
            AddHighlight();
            XiaoheiBlink.ShortenRecharge(body);
        }

        internal void AddHighlight() =>
            body.SetBuffCount(highlight.buffIndex, Mathf.Min(3, body.GetBuffCount(highlight) + 1));

        internal int SpendHighlights()
        {
            int stacks = Mathf.Clamp(body.GetBuffCount(highlight), 0, 3);
            body.SetBuffCount(highlight.buffIndex, 0);
            return stacks;
        }

        internal static void OnCup(CharacterBody recipient)
        {
            if (!NetworkServer.active || !recipient || !recipient.healthComponent.alive) return;
            var shooter = recipient.GetComponent<XiaoheiCombat>();
            if (!shooter) return;
            shooter.AddHighlight();
            XiaoheiBlink.GrantCharge(recipient);
            shooter.nextShotAfterCup = true;
            Speak("XIAOHEI_BOND_CUP");
        }

        internal static void OnCuiImpact(CharacterBody cui, HealthComponent victim)
        {
            if (!NetworkServer.active || !cui) return;
            var marks = ActiveMarks(victim);
            if (marks) marks.Cover(cui, victim);
        }

        internal static XiaoheiMarks ActiveMarks(HealthComponent victim) =>
            victim && victim.body && victim.body.HasBuff(marked) ? victim.GetComponent<XiaoheiMarks>() : null;

        internal static void Speak(string token) =>
            Chat.SendBroadcastChat(new Chat.SimpleChatMessage { baseToken = token });
    }

    // One mark per shooter on this target. Refreshing that shooter's mark
    // re-arms their cover shot without disturbing other players' marks.
    internal sealed class XiaoheiMarks : MonoBehaviour
    {
        private sealed class Mark
        {
            internal CharacterBody shooter;
            internal TeamIndex markingTeam;
            internal float ends;
            internal bool used;
            internal JieBondTrigger swordBonds;
            internal DinnerStrike dinnerHits;
            internal DinnerCredit dinnerCredit;
        }
        private readonly List<Mark> marks = new List<Mark>();

        internal void Refresh(CharacterBody shooter, bool hasPreviousDebuff)
        {
            if (hasPreviousDebuff)
                marks.RemoveAll(m => m.shooter == shooter || m.ends <= Time.fixedTime);
            else marks.Clear(); // A cleanse also ends those owners' unfinished team combos.
            marks.Add(new Mark { shooter = shooter, markingTeam = shooter.teamComponent.teamIndex, ends = Time.fixedTime + 5f });
        }

        internal void SwordBond(CharacterBody jie, JieBondTrigger trigger, Vector3 contact, Vector3 framePosition)
        {
            for (int i = 0; i < marks.Count; i++)
            {
                var mark = marks[i];
                var shooter = mark.shooter;
                if (mark.ends <= Time.fixedTime || !shooter || !shooter.healthComponent.alive ||
                    shooter.teamComponent.teamIndex != jie.teamComponent.teamIndex || (mark.swordBonds & trigger) != 0)
                    continue;
                // Reserve this half and the completion before damage can re-enter.
                mark.swordBonds |= trigger;
                bool complete = mark.swordBonds == (JieBondTrigger.Recall | JieBondTrigger.HeavySlam);
                if (trigger == JieBondTrigger.Recall)
                {
                    shooter.GetComponent<XiaoheiCombat>().AddHighlight();
                    JieBonds.Pierce(shooter, contact);
                }
                if (complete) JieBonds.FinishReplay(jie, contact, framePosition);
            }
        }

        internal void Cover(CharacterBody cui, HealthComponent victim)
        {
            for (int i = 0; i < marks.Count; ++i)
            {
                var mark = marks[i];
                var shooter = mark.shooter;
                if (mark.used || mark.ends <= Time.fixedTime || !shooter || !shooter.healthComponent.alive ||
                    shooter.teamComponent.teamIndex != cui.teamComponent.teamIndex) continue;
                mark.used = true; // Claim before any item proc or secondary damage can re-enter.
                var muzzle = XiaoheiModel.Muzzle(shooter);
                XiaoheiBallistics.Trace(XiaoheiCombat.PistolTracer, muzzle ? muzzle.position : shooter.corePosition,
                    victim.body.corePosition);
                if (victim.alive)
                    XiaoheiBallistics.HitTarget(shooter, victim, victim.body.mainHurtBox,
                        shooter.damage * 6.5f, shooter.RollCrit(), DamageTypeCombo.Generic);
                cui.healthComponent.AddBarrier(cui.healthComponent.fullHealth * .12f);
                XiaoheiCombat.Speak("XIAOHEI_BOND_COVER");
            }
        }

        internal void DinnerHit(CharacterBody source, DinnerStrike strike, Vector3 point)
        {
            foreach (var mark in marks)
            {
                if (mark.ends <= Time.fixedTime || mark.markingTeam != source.teamComponent.teamIndex ||
                    (mark.dinnerHits & strike) != 0) continue;
                if (strike == DinnerStrike.Throw)
                    mark.dinnerCredit = new DinnerCredit(source);
                mark.dinnerHits |= strike;
                if (mark.dinnerHits == DinnerStrike.All && WeiBanquet.TryReserve(mark.dinnerCredit.Team))
                    WeiBanquet.Drop(mark.dinnerCredit, point);
            }
        }
    }

    // A transmitted effect tells observers exactly which body to draw the mark on.
    // The replicated debuff then controls its lifetime, including cleanses.
    internal sealed class XiaoheiMarkNotice : MonoBehaviour
    {
        private void Start()
        {
            var data = GetComponent<EffectComponent>().effectData;
            var victim = data?.ResolveNetworkedObjectReference();
            if (victim) XiaoheiCombat.ShowMark(victim.GetComponent<CharacterBody>(), data.origin);
            Destroy(gameObject);
        }
    }
}

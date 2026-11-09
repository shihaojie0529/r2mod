using System;
using System.Collections.Generic;
using R2API;
using RoR2;
using RoR2.UI;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace RiftSurvivor
{
    // Server clocks replicate through vanilla buff counts. The HUD reads those
    // counts, never infers an accepted cast from a local button press.
    public sealed class CombatStatusHud : MonoBehaviour
    {
        private static BuffDef stormClock, replayClock;
        private static Font font;
        private CharacterBody body;
        private float stormEnds, replayEnds, nextPublish, nextDraw;
        private readonly Dictionary<SkillIcon, Text> badges = new Dictionary<SkillIcon, Text>();
        private readonly HashSet<SkillIcon> visibleIcons = new HashSet<SkillIcon>();
        private readonly List<SkillIcon> staleIcons = new List<SkillIcon>();

        internal static void Register()
        {
            stormClock = Clock("JieStormRemaining");
            replayClock = Clock("XiaoheiReplayRemaining");
            CharacterBody.onBodyStartGlobal += owner =>
            {
                if (owner.GetComponent<JieSwords>() || owner.GetComponent<XiaoheiCombat>() ||
                    owner.GetComponent<CuiVitals>() || owner.GetComponent<WeiVitals>())
                    Ensure(owner);
            };
        }

        private static BuffDef Clock(string name)
        {
            var buff = ScriptableObject.CreateInstance<BuffDef>();
            buff.name = name;
            buff.canStack = buff.isHidden = buff.ignoreGrowthNectar = true;
            if (!ContentAddition.AddBuffDef(buff)) throw new InvalidOperationException(name);
            return buff;
        }

        private static CombatStatusHud Ensure(CharacterBody owner) =>
            owner.GetComponent<CombatStatusHud>() ?? owner.gameObject.AddComponent<CombatStatusHud>();
        internal static void Storm(CharacterBody owner) => Ensure(owner).stormEnds = Time.fixedTime + JieGoldVolley.Duration;
        internal static void Replay(CharacterBody owner) => Ensure(owner).replayEnds = Time.fixedTime + XiaoheiReplaySentry.Duration;
        private void Awake() => body = GetComponent<CharacterBody>();

        private void FixedUpdate()
        {
            if (!NetworkServer.active || Time.fixedTime < nextPublish || !body) return;
            nextPublish = Time.fixedTime + .1f;
            bool alive = body.healthComponent && body.healthComponent.alive;
            Publish(stormClock, alive ? stormEnds : 0f);
            Publish(replayClock, alive ? replayEnds : 0f);
        }

        private void Publish(BuffDef buff, float ends)
        {
            int count = Mathf.Clamp(Mathf.CeilToInt((ends - Time.fixedTime) * 10f), 0, 60);
            if (body.GetBuffCount(buff) != count) body.SetBuffCount(buff.buffIndex, count);
        }

        private void LateUpdate()
        {
            if (!NetworkClient.active || !body || Time.unscaledTime < nextDraw) return;
            nextDraw = Time.unscaledTime + .1f;
            visibleIcons.Clear();
            bool alive = body.healthComponent && body.healthComponent.alive;
            foreach (var hud in HUD.instancesList)
            {
                if (!hud || hud.targetBodyObject != gameObject || !alive) continue;
                foreach (var icon in hud.GetComponentsInChildren<SkillIcon>(true))
                {
                    // Match the actual bound skill, including spectating and HUD rebuilds.
                    if (!icon || !icon.targetSkill || icon.targetSkill.characterBody != body) continue;
                    string text = Status(icon.targetSkill);
                    if (string.IsNullOrEmpty(text)) continue;
                    if (!badges.TryGetValue(icon, out var badge) || !badge)
                        badges[icon] = badge = CreateBadge(icon);
                    if (!badge) continue;
                    badge.text = text;
                    visibleIcons.Add(icon);
                }
            }
            staleIcons.Clear();
            foreach (var pair in badges)
                if (!visibleIcons.Contains(pair.Key))
                {
                    if (pair.Value) Destroy(pair.Value.gameObject);
                    staleIcons.Add(pair.Key);
                }
            foreach (var icon in staleIcons) badges.Remove(icon);
        }

        private string Status(GenericSkill skill)
        {
            var skills = body.skillLocator;
            if (!skills) return null;
            if (body.GetComponent<JieSwords>())
            {
                if (skill == skills.secondary) return $"插剑 {JieSwords.Count(skill)}/9";
                if (skill == skills.utility && JieFlight.IsRiding(body)) return "御剑中";
                float remaining = body.GetBuffCount(stormClock) * .1f;
                if (skill == skills.special && remaining > 0f) return $"施放 {remaining:0.0}s";
            }
            else if (body.GetComponent<XiaoheiCombat>())
            {
                if (skill == skills.primary) return $"连中 {XiaoheiCombat.HitProgress(body)}/3";
                if (skill == skills.special)
                {
                    float remaining = body.GetBuffCount(replayClock) * .1f;
                    return $"高光 {XiaoheiCombat.Highlights(body)}/3" +
                        (remaining > 0f ? $"\n分身 {remaining:0.0}s" : "");
                }
                var blink = body.GetComponent<XiaoheiBlink>();
                if (skill == skills.utility && blink && blink.HasOrigin)
                    return $"回位 {blink.OriginRemaining:0.0}s";
            }
            else if (body.GetComponent<CuiVitals>())
            {
                if (skill == skills.primary) return $"护盾 {body.healthComponent.barrier:0}/{body.healthComponent.fullBarrier:0}";
                if (skill == skills.utility)
                {
                    var machine = EntityStateMachine.FindByCustomName(body.gameObject,"Body");
                    if (machine && machine.state is CuiChargeWindup windup) return $"蓄力 {windup.HeldSeconds:0.0}/2s";
                }
                if (skill == skills.secondary || skill == skills.special)
                    return $"泵感 {CuiVitals.PumpCount(body)}/6" + (CuiVitals.IsFull(body) ? "\n强化就绪" : "");
            }
            else if (body.GetComponent<WeiVitals>())
            {
                if (skill == skills.primary) return $"燃脂 {WeiVitals.Stacks(body)}/3";
            }
            return null;
        }

        private static Text CreateBadge(SkillIcon icon)
        {
            if (!icon.iconImage) return null;
            var textObject = new GameObject("RosterSkillStatus", typeof(RectTransform), typeof(Text), typeof(Outline));
            textObject.transform.SetParent(icon.iconImage.transform, false);
            var rect = (RectTransform)textObject.transform;
            // A compact caption immediately above the original icon. No full HUD
            // panel, and no changes to its cooldown, stock, tooltip or key binding.
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 4f);
            rect.sizeDelta = new Vector2(0f, 32f);
            if (!font) font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 14);
            var label = textObject.GetComponent<Text>();
            label.font = font;
            label.fontSize = 14;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 9;
            label.resizeTextMaxSize = 14;
            label.supportRichText = false;
            label.color = new Color(.75f, 1f, .92f);
            label.alignment = TextAnchor.LowerCenter;
            label.raycastTarget = false;
            textObject.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, .9f);
            return label;
        }

        private void OnDestroy()
        {
            foreach (var badge in badges.Values)
                if (badge) Destroy(badge.gameObject);
            badges.Clear();
        }
    }
}

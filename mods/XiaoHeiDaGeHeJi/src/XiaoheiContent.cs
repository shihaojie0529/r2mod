using EntityStates;
using R2API;
using RoR2;
using RoR2.Skills;
using UnityEngine;

namespace RiftSurvivor
{
    internal static class XiaoheiContent
    {
        internal static void Register()
        {
            XiaoheiLanguage.Register();
            XiaoheiBlink.Register();
            XiaoheiCombat.Register();

            var body = PrefabAPI.InstantiateClone(SurvivorContent.LoadPrefab("CommandoBody"), "XiaoheiBody", true);
            var character = body.GetComponent<CharacterBody>();
            character.baseNameToken = "XIAOHEI_NAME";
            character.subtitleNameToken = "XIAOHEI_SUBTITLE";
            character.bodyColor = new Color32(112, 207, 251, 255);
            character.portraitIcon = XiaoheiModel.Portrait;
            SurvivorContent.ConfigureRangedStats(character, 110f, 33f, 7.5f);
            body.AddComponent<XiaoheiBlink>();
            XiaoheiCombat.Install(body);
            body.AddComponent<DirectSkillKillReset>();
            ConfigureSkills(body);
            XiaoheiModel.Install(body);

            var master = PrefabAPI.InstantiateClone(SurvivorContent.LoadPrefab("CommandoMonsterMaster"), "XiaoheiMonsterMaster", true);
            var display = SurvivorDisplay.Create("XiaoheiDisplay", XiaoheiModel.Install);
            SurvivorContent.RegisterRangedSurvivor("Xiaohei", "XIAOHEI", 101f, character, master, display);
        }

        private static void ConfigureSkills(GameObject body)
        {
            // Weapon handles the pistol and Slide handles blink. Replay has its own
            // networked lane so calling the echo never interrupts either input.
            SurvivorSkills.AddIdleMachine(body, "Replay");

            var slots = body.GetComponent<SkillLocator>();
            slots.passiveSkill = new SkillLocator.PassiveSkill
            {
                enabled = true, icon = GymIcons.Get("xh-highlight"),
                skillNameToken = "XIAOHEI_HIGHLIGHT_NAME", skillDescriptionToken = "XIAOHEI_HIGHLIGHT_DESC"
            };
            Add<XiaoheiPrimary>(slots.primary, "PISTOL", "xh-pistol", "Weapon", 0f, InterruptPriority.Any);
            Add<XiaoheiSecondary>(slots.secondary, "SEEKER", "xh-seeker", "Weapon", 4f, InterruptPriority.Skill);
            Add<XiaoheiBlinkInput>(slots.utility, "BLINK", "xh-blink", "Slide", 2f, InterruptPriority.Skill,
                ScriptableObject.CreateInstance<XiaoheiBlinkSkillDef>());
            SurvivorSkills.Register<XiaoheiBlinkTravel>();
            Add<XiaoheiReplay>(slots.special, "REPLAY", "xh-replay", "Replay", 10f, InterruptPriority.Skill);
        }

        private static void Add<T>(GenericSkill slot, string token, string icon, string machine,
            float cooldown, InterruptPriority priority, SkillDef skill = null) where T : EntityState, new()
        {
            bool isBlink = skill is XiaoheiBlinkSkillDef;
            if (!skill) skill = ScriptableObject.CreateInstance<SkillDef>();
            skill.baseMaxStock = isBlink ? 2 : 1;
            skill.rechargeStock = 1;
            skill.requiredStock = isBlink ? 0 : 1;
            skill.stockToConsume = isBlink || cooldown == 0f ? 0 : 1;
            skill.baseRechargeInterval = cooldown;
            skill.fullRestockOnAssign = true;
            skill.beginSkillCooldownOnSkillEnd = false;
            // Holding to recall must leave the partially recharged stock untouched.
            skill.resetCooldownTimerOnUse = !isBlink;
            skill.mustKeyPress = cooldown > 0f;
            skill.cancelSprintingOnActivation = !isBlink;
            skill.isCombatSkill = !isBlink;
            SurvivorSkills.Assign(slot, skill, "Xiaohei" + token, "XIAOHEI_" + token,
                icon, machine, SurvivorSkills.Register<T>(), priority);
        }
    }
}

using EntityStates;
using R2API;
using RoR2;
using RoR2.Skills;
using UnityEngine;

namespace RiftSurvivor
{
    internal static class JieContent
    {
        internal static void Register()
        {
            JieLanguage.Register();
            JieSwords.Register();
            JieStorm.Register();
            JieBondEffect.Register();

            var body = PrefabAPI.InstantiateClone(SurvivorContent.LoadPrefab("CommandoBody"), "JieGeBody", true);
            var character = body.GetComponent<CharacterBody>();
            character.baseNameToken = "JIEGE_NAME";
            character.subtitleNameToken = "JIEGE_SUBTITLE";
            character.bodyColor = new Color32(137, 225, 194, 255);
            character.portraitIcon = JieModel.Portrait;
            SurvivorContent.ConfigureRangedStats(character, 100f, 30f, 7f);
            JieSwords.Install(body);
            JieFlight.Install(body);
            SurvivorSkills.AddIdleMachine(body, "Storm");
            ConfigureSkills(body.GetComponent<SkillLocator>());
            JieModel.Install(body);

            var master = PrefabAPI.InstantiateClone(SurvivorContent.LoadPrefab("CommandoMonsterMaster"), "JieGeMonsterMaster", true);
            var display = PrefabAPI.InstantiateClone(SurvivorContent.LoadPrefab("CommandoDisplay"), "JieGeDisplay", false);
            JieModel.Install(display);
            SurvivorContent.RegisterRangedSurvivor("JieGe", "JIEGE", 102f, character, master, display);
        }

        private static void ConfigureSkills(SkillLocator slots)
        {
            slots.passiveSkill = new SkillLocator.PassiveSkill
            {
                enabled = true, icon = GymIcons.Get("jie-flight"),
                skillNameToken = "JIEGE_FLIGHT_NAME", skillDescriptionToken = "JIEGE_FLIGHT_DESC"
            };
            Add<JieDrawSword>(slots.primary, "DRAW", "jie-draw", "Weapon", 0f, 0, InterruptPriority.Any);
            Add<JieRecallSwords>(slots.secondary, "RECALL", "jie-recall-0", "Weapon", 4f, 0, InterruptPriority.Skill,
                ScriptableObject.CreateInstance<JieRecallSkillDef>());
            Add<JieDash>(slots.utility, "DASH", "jie-dash", "Slide", 6f, 1, InterruptPriority.Skill);
            Add<JieStorm>(slots.special, "STORM", "jie-storm", "Storm", 24f, 1, InterruptPriority.Skill,
                ScriptableObject.CreateInstance<JieStormSkillDef>());
        }

        private static void Add<T>(GenericSkill slot, string token, string icon, string machine,
            float cooldown, int cost, InterruptPriority priority, SkillDef skill = null) where T : EntityState, new()
        {
            if (!skill) skill = ScriptableObject.CreateInstance<SkillDef>();
            skill.baseMaxStock = skill.requiredStock = skill.rechargeStock = 1;
            skill.stockToConsume = cost;
            skill.baseRechargeInterval = cooldown;
            skill.fullRestockOnAssign = true;
            skill.beginSkillCooldownOnSkillEnd = false;
            skill.resetCooldownTimerOnUse = cost > 0;
            skill.mustKeyPress = cooldown > 0f;
            skill.isCombatSkill = typeof(T) != typeof(JieDash);
            skill.cancelSprintingOnActivation = skill.isCombatSkill;
            SurvivorSkills.Assign(slot, skill, "JieGe" + token, "JIEGE_" + token, icon,
                machine, SurvivorSkills.Register<T>(), priority);
        }
    }
}

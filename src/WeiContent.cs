using EntityStates;
using R2API;
using RoR2;
using RoR2.Skills;
using UnityEngine;

namespace RiftSurvivor
{
    internal static class WeiContent
    {
        internal static void Register()
        {
            WeiLanguage.Register();
            WeiVitals.Register();
            WeiCombat.Register();
            WeiPortal.Register();
            WeiBanquet.Register();
            var body = PrefabAPI.InstantiateClone(SurvivorContent.LoadPrefab("CommandoBody"), "WeiGeBody", true);
            var character = body.GetComponent<CharacterBody>();
            character.baseNameToken = "WEIGE_NAME";
            character.subtitleNameToken = "WEIGE_SUBTITLE";
            character.bodyColor = new Color32(104, 196, 252, 255);
            character.portraitIcon = WeiModel.Portrait;
            SurvivorContent.ConfigureRangedStats(character, 120f, 36f, 7f);
            body.AddComponent<WeiVitals>();
            WeiCombat.Install(body);
            WeiPortal.Install(body);
            SurvivorSkills.AddIdleMachine(body, "Select");
            SurvivorSkills.AddIdleMachine(body, "Carry");
            var slots = body.GetComponent<SkillLocator>();
            slots.passiveSkill = new SkillLocator.PassiveSkill
            {
                enabled = true, icon = GymIcons.Get("wei-steps"),
                skillNameToken = "WEIGE_STEPS_NAME", skillDescriptionToken = "WEIGE_STEPS_DESC"
            };
            Add<WeiDelivery>(slots.primary, "DELIVERY", "wei-delivery", "Weapon", 0f,
                ScriptableObject.CreateInstance<WeiDeliverySkillDef>());
            Add<WeiCut>(slots.secondary, "CUT", "wei-cut", "Carry", 5f);
            SurvivorSkills.Register<WeiPaste>();
            Add<WeiPortalCast>(slots.utility, "PORTAL", "wei-portal", "Slide", 7f);
            Add<WeiSelectAll>(slots.special, "SELECT", "wei-select", "Select", 18f);
            WeiModel.Install(body);
            var master = PrefabAPI.InstantiateClone(SurvivorContent.LoadPrefab("CommandoMonsterMaster"), "WeiGeMonsterMaster", true);
            var display = PrefabAPI.InstantiateClone(SurvivorContent.LoadPrefab("CommandoDisplay"), "WeiGeDisplay", false);
            WeiModel.Install(display);
            SurvivorContent.RegisterRangedSurvivor("WeiGe", "WEIGE", 103f, character, master, display);
        }

        private static void Add<T>(GenericSkill slot, string token, string icon, string machine, float cooldown,
            SkillDef skill = null)
            where T : EntityState, new()
        {
            if (!skill) skill = ScriptableObject.CreateInstance<SkillDef>();
            skill.baseMaxStock = skill.requiredStock = skill.rechargeStock = 1;
            // Cut, doors and group throws commit stock after the server accepts a cast.
            skill.stockToConsume = 0;
            skill.baseRechargeInterval = cooldown;
            skill.fullRestockOnAssign = true;
            skill.beginSkillCooldownOnSkillEnd = false;
            skill.resetCooldownTimerOnUse = false;
            skill.mustKeyPress = cooldown > 0f;
            skill.isCombatSkill = token != "PORTAL";
            // Automatic deliveries leave the player's run and flight controls alone.
            skill.cancelSprintingOnActivation = skill.isCombatSkill && token != "DELIVERY";
            SurvivorSkills.Assign(slot, skill, "WeiGe" + token, "WEIGE_" + token, icon, machine,
                SurvivorSkills.Register<T>(), cooldown == 0f ? InterruptPriority.Any : InterruptPriority.Skill);
        }
    }
}

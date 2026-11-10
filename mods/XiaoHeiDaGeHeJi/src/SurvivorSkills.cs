using System;
using System.Reflection;
using EntityStates;
using R2API;
using RoR2;
using RoR2.CharacterAI;
using RoR2.Skills;
using UnityEngine;

namespace RiftSurvivor
{
    internal static class SurvivorSkills
    {
        private static readonly FieldInfo Family = typeof(GenericSkill).GetField("_skillFamily",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        internal static void Configure(GameObject body)
        {
            var slots = body.GetComponent<SkillLocator>();
            slots.passiveSkill = new SkillLocator.PassiveSkill
            {
                enabled = true, icon = GymIcons.Get("pump"), skillNameToken = "CUIGE_PUMP_NAME",
                skillDescriptionToken = "CUIGE_PUMP_DESC"
            };
            MeleeCombat.LoadEffects();
            var combo = ScriptableObject.CreateInstance<SteppedSkillDef>();
            combo.stepCount = 3;
            combo.stepGraceDuration = 1.25f;
            Define(slots.primary, combo, Register<ComboPunch>(), "PUNCH", "punch", "Weapon", 0f, 0, InterruptPriority.Any);
            Define(slots.secondary, ScriptableObject.CreateInstance<SkillDef>(), Register<PlateSlam>(),
                "SLAM", "slam", "Weapon", 5f, 1, InterruptPriority.Skill);
            Register<ShoulderCharge>();
            Define(slots.utility, ScriptableObject.CreateInstance<SkillDef>(), Register<CuiChargeWindup>(),
                "CHARGE", "charge", "Body", 6f, 1, InterruptPriority.PrioritySkill);
            Define(slots.special, ScriptableObject.CreateInstance<SkillDef>(), new SerializableEntityStateType(typeof(ShakerAim)),
                "SHAKER", "shaker", "Weapon", 8f, 0, InterruptPriority.Skill);
        }

        internal static SerializableEntityStateType Register<T>() where T : EntityState, new()
        {
            var state = ContentAddition.AddEntityState<T>(out bool added);
            return added ? state : throw new InvalidOperationException("Could not register " + typeof(T).Name);
        }

        internal static EntityStateMachine AddIdleMachine(GameObject body, string name, bool resetOnHurt = true)
        {
            var machine = body.AddComponent<EntityStateMachine>();
            machine.customName = name;
            machine.initialStateType = machine.mainStateType = new SerializableEntityStateType(typeof(Idle));
            Append(ref body.GetComponent<NetworkStateMachine>().stateMachines, machine);
            Append(ref body.GetComponent<CharacterDeathBehavior>().idleStateMachine, machine);
            if (resetOnHurt) Append(ref body.GetComponent<SetStateOnHurt>().idleStateMachine, machine);
            Append(ref body.GetComponent<CharacterBody>().vehicleIdleStateMachine, machine);
            return machine;
        }

        private static void Append(ref EntityStateMachine[] machines, EntityStateMachine machine)
        {
            Array.Resize(ref machines, (machines?.Length ?? 0) + 1);
            machines[machines.Length - 1] = machine;
        }

        private static void Define(GenericSkill slot, SkillDef skill, SerializableEntityStateType state,
            string token, string icon, string machine, float recharge, int stockCost, InterruptPriority priority)
        {
            skill.baseMaxStock = skill.requiredStock = skill.rechargeStock = 1;
            skill.stockToConsume = stockCost;
            skill.baseRechargeInterval = recharge;
            skill.fullRestockOnAssign = true;
            skill.beginSkillCooldownOnSkillEnd = false;
            skill.cancelSprintingOnActivation = true;
            skill.mustKeyPress = token == "SHAKER" || token == "CHARGE";
            skill.isCombatSkill = token != "SHAKER";
            Assign(slot, skill, "CuiGe" + token, "CUIGE_" + token, icon, machine, state, priority);
        }

        internal static void Assign(GenericSkill slot, SkillDef skill, string name, string token,
            string icon, string machine, SerializableEntityStateType state, InterruptPriority priority)
        {
            ((ScriptableObject)skill).name = skill.skillName = name;
            skill.skillNameToken = token + "_NAME";
            skill.skillDescriptionToken = token + "_DESC";
            skill.icon = GymIcons.Get(icon);
            skill.keywordTokens = Array.Empty<string>();
            skill.activationState = state;
            skill.activationStateMachineName = machine;
            skill.interruptPriority = priority;
            var family = ScriptableObject.CreateInstance<SkillFamily>();
            ((ScriptableObject)family).name = name + "Family";
            family.variants = new[] { new SkillFamily.Variant { skillDef = skill,
                viewableNode = new ViewablesCatalog.Node(skill.skillNameToken, false, null) } };
            if (Family == null || !ContentAddition.AddSkillDef(skill) || !ContentAddition.AddSkillFamily(family))
                throw new InvalidOperationException("Could not assign " + name);
            Family.SetValue(slot, family);
        }

        internal static void ConfigureAI(GameObject master)
        {
            foreach (var driver in master.GetComponents<AISkillDriver>())
            {
                driver.requiredSkill = null;
                driver.movementType = AISkillDriver.MovementType.ChaseMoveTarget;
                if (driver.skillSlot == SkillSlot.Primary || driver.skillSlot == SkillSlot.Secondary)
                {
                    driver.minDistance = 0f;
                    driver.maxDistance = driver.skillSlot == SkillSlot.Primary ? 4f : 7f;
                }
                if (driver.skillSlot == SkillSlot.Special) driver.maxUserHealthFraction = .75f;
            }
        }
    }
}

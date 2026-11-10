using System;
using R2API;
using RoR2;
using RoR2.CharacterAI;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace RiftSurvivor
{
    internal static class SurvivorContent
    {
        private const string BaseAssetPath = "RoR2/Base/Commando/";
        private const float GroundSpeedBoost = 2.7f;

        internal static void Register()
        {
            SurvivorLanguage.Register();
            CuiVitals.Register();
            var originalBody = LoadPrefab("CommandoBody");
            var originalMaster = LoadPrefab("CommandoMonsterMaster");

            var body = PrefabAPI.InstantiateClone(originalBody, "RiftRunnerBody", true);
            var character = body.GetComponent<CharacterBody>();
            character.baseNameToken = "RIFTRUNNER_NAME";
            character.subtitleNameToken = "RIFTRUNNER_SUBTITLE";
            character.bodyColor = new Color32(100, 222, 217, 255);
            character.portraitIcon = SurvivorArt.Portrait;
            character.baseMaxHealth = 140f;
            character.levelMaxHealth = 42f;
            character.baseMoveSpeed = 8f * GroundSpeedBoost;
            character.baseAttackSpeed = 10f;
            CuiVitals.Install(body);
            body.AddComponent<DirectSkillKillReset>().slot = SkillSlot.Utility;
            SurvivorSkills.Configure(body);
            SurvivorModel.Install(body);

            // The game uses this AI master for doppelgangers (e.g. Artifact of Vengeance).
            var master = PrefabAPI.InstantiateClone(originalMaster, "RiftRunnerMonsterMaster", true);
            master.GetComponent<CharacterMaster>().bodyPrefab = body;
            SurvivorSkills.ConfigureAI(master);

            var survivor = ScriptableObject.CreateInstance<SurvivorDef>();
            survivor.cachedName = "RiftRunner";
            survivor.bodyPrefab = body;
            survivor.displayPrefab = SurvivorDisplay.Create("RiftRunnerDisplay", SurvivorModel.Install);
            survivor.primaryColor = character.bodyColor;
            survivor.displayNameToken = character.baseNameToken;
            survivor.descriptionToken = "RIFTRUNNER_DESCRIPTION";
            survivor.outroFlavorToken = "RIFTRUNNER_OUTRO";
            survivor.mainEndingEscapeFailureFlavorToken = "RIFTRUNNER_OUTRO_FAILURE";
            survivor.desiredSortPosition = 100f;

            if (!ContentAddition.AddBody(body) ||
                !ContentAddition.AddMaster(master) ||
                !ContentAddition.AddSurvivorDef(survivor))
            {
                throw new InvalidOperationException("R2API could not register the RiftRunner survivor. See the ContentManagement log for details.");
            }
        }

        internal static void ConfigureRangedStats(CharacterBody character, float health, float healthGrowth, float moveSpeed)
        {
            character.autoCalculateLevelStats = false;
            character.baseMaxHealth = health;
            character.levelMaxHealth = healthGrowth;
            character.baseDamage = 12f;
            character.levelDamage = 2.4f;
            character.baseMoveSpeed = moveSpeed * GroundSpeedBoost;
            character.levelMoveSpeed = 0f;
            character.baseRegen = 1f;
            character.levelRegen = .2f;
            character.baseAttackSpeed = 10f;
            character.levelAttackSpeed = 0f;
            character.baseArmor = character.levelArmor = 0f;
            character.baseCrit = 1f;
            character.levelCrit = 0f;
        }

        internal static void RegisterRangedSurvivor(string id, string token, float position,
            CharacterBody character, GameObject master, GameObject display)
        {
            var body = character.gameObject;
            master.GetComponent<CharacterMaster>().bodyPrefab = body;
            foreach (var driver in master.GetComponents<AISkillDriver>()) driver.requiredSkill = null;
            var survivor = ScriptableObject.CreateInstance<SurvivorDef>();
            survivor.cachedName = id;
            survivor.bodyPrefab = body;
            survivor.displayPrefab = display;
            survivor.primaryColor = character.bodyColor;
            survivor.displayNameToken = character.baseNameToken;
            survivor.descriptionToken = token + "_DESCRIPTION";
            survivor.outroFlavorToken = token + "_OUTRO";
            survivor.mainEndingEscapeFailureFlavorToken = token + "_OUTRO_FAILURE";
            survivor.desiredSortPosition = position;
            if (!ContentAddition.AddBody(body) || !ContentAddition.AddMaster(master) || !ContentAddition.AddSurvivorDef(survivor))
                throw new InvalidOperationException("Could not register " + id);
        }

        internal static GameObject LoadPrefab(string name)
        {
            string key = BaseAssetPath + name + ".prefab";
            var prefab = Addressables.LoadAssetAsync<GameObject>(key).WaitForCompletion();
            if (!prefab)
                throw new InvalidOperationException("Missing base-game prefab: " + key);
            return prefab;
        }
    }
}

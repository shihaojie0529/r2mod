using System;
using BepInEx;
using R2API;
using R2API.ContentManagement;
using R2API.Utils;

namespace RiftSurvivor
{
    [BepInPlugin("local.r2mod.riftsurvivor", "小黑大哥合集", "0.5.2")]
    [BepInDependency(R2APIContentManager.PluginGUID)]
    [BepInDependency(PrefabAPI.PluginGUID)]
    [BepInDependency(LanguageAPI.PluginGUID)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.EveryoneNeedSameModVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        // Explicit roster: each member registers its own text, skills and SurvivorDef.
        private static readonly Action[] Roster =
        {
            SurvivorContent.Register, // 崔哥 (legacy catalog ID: RiftRunner)
            XiaoheiContent.Register,
            JieContent.Register,
            WeiContent.Register,
        };

        private void Awake()
        {
            foreach (var registerMember in Roster) registerMember();
            HiddenFlight.Register();
            Logger.LogInfo($"Registered {Roster.Length} friend survivor(s) in {Info.Metadata.Name}.");
        }
    }
}

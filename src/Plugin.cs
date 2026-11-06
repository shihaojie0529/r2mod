using System;
using BepInEx;
using R2API;
using R2API.ContentManagement;
using R2API.Utils;

namespace RiftSurvivor
{
    [BepInPlugin("local.r2mod.riftsurvivor", "小黑大哥合集", "0.5.3")]
    [BepInDependency(R2APIContentManager.PluginGUID)]
    [BepInDependency(PrefabAPI.PluginGUID)]
    [BepInDependency(LanguageAPI.PluginGUID)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.EveryoneNeedSameModVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        // Explicit roster: each member registers its own text, skills and SurvivorDef.
        private static readonly (string Name, Action Register)[] Roster =
        {
            ("崔哥", SurvivorContent.Register),
            ("小黑", XiaoheiContent.Register),
            ("杰哥", JieContent.Register),
            ("伟哥", WeiContent.Register),
        };

        private void Awake()
        {
            int registered = 0;
            foreach (var member in Roster)
            {
                try
                {
                    member.Register();
                    registered++;
                }
                catch (Exception error)
                {
                    Logger.LogError($"{member.Name} 注册失败 ({Info.Metadata.Version}): {error}");
                }
            }
            HiddenFlight.Register();
            Logger.LogInfo($"{Info.Metadata.Name} {Info.Metadata.Version}: 已注册 {registered}/{Roster.Length} 位角色。");
        }
    }
}

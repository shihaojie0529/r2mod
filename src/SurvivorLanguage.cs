using R2API;

namespace RiftSurvivor
{
    internal static class SurvivorLanguage
    {
        internal static void Register()
        {
            Add("RIFTRUNNER_NAME", "Cui Ge", "崔哥", "崔哥");
            Add("RIFTRUNNER_SUBTITLE", "The Gym Regular", "健身房的老朋友", "健身房的老朋友");
            Add("RIFTRUNNER_DESCRIPTION",
                "Build Pump and a barrier by landing punches. Hold utility to charge a devastating shoulder rush, and press special to heal your entire team. Full Pump empowers your slam or team heal.",
                "贴身连拳，积攒泵感与护盾。按住 Shift 蓄力，松开撞出重击；按 R 立即为全队回血。满泵感可以强化重砸或全队治疗。",
                "貼身連拳，累積泵感與護盾。按住 Shift 蓄力，放開撞出重擊；按 R 立即為全隊回血。滿泵感可以強化重砸或全隊治療。");
            Add("RIFTRUNNER_OUTRO", "...and so he left, saving one last shake for a friend.",
                "……于是他离开了，把最后一杯留给了朋友。", "……於是他離開了，把最後一杯留給了朋友。");
            Add("RIFTRUNNER_OUTRO_FAILURE", "...and so he vanished, one set short.",
                "……于是他消失了，还差最后一组。", "……於是他消失了，還差最後一組。");

            Add("CUIGE_PUMP_NAME", "One More Set / Heating Up", "再来一组 / 越练越热", "再來一組 / 越練越熱");
            Add("CUIGE_PUMP_DESC",
                "Landed punches grant a barrier equal to 8% max health (12% for the uppercut), capped at 200% max health. It only decays out of combat, by 2% max health per second. Each landed punch grants 1 Pump: 5% less damage per stack, up to 6. Spending all 6 retains 30% reduction for 2s. Recover 2% max health per second; landing the uppercut raises this to 4% for 4s.",
                "普攻命中获得最大生命值 8% 的护盾，上勾拳为 12%，护盾上限为最大生命值的 200%；脱战后每秒减少最大生命值的 2%。每次挥拳命中只叠加一次。\n每次命中叠 1 层泵感，每层减伤 5%，最多 6 层；消耗满层后，30% 减伤保留 2 秒。每秒回血 2%，上勾拳命中后提高到每秒 4%，持续 4 秒。",
                "普攻命中獲得最大生命值 8% 的護盾，上鉤拳為 12%，護盾上限為最大生命值的 200%；脫戰後每秒減少最大生命值的 2%。每次揮拳命中只疊加一次。\n每次命中疊 1 層泵感，每層減傷 5%，最多 6 層；消耗滿層後，30% 減傷保留 2 秒。每秒回血 2%，上鉤拳命中後提高到每秒 4%，持續 4 秒。");
            Add("CUIGE_PUNCH_NAME", "Combination Punch", "组合拳", "組合拳");
            Add("CUIGE_PUNCH_DESC",
                "Jab, hook and uppercut for 160%, 160% and 300% damage. Landed punches grant a barrier of 8%, 8% and 12% max health. The uppercut also boosts recovery.",
                "刺拳、短摆拳、上勾拳分别造成 160%、160%、300% 伤害。命中分别叠加最大生命值 8%、8%、12% 的护盾；上勾拳同时强化自身回复。",
                "刺拳、短擺拳、上鉤拳分別造成 160%、160%、300% 傷害。命中分別疊加最大生命值 8%、8%、12% 的護盾；上鉤拳同時強化自身回復。");
            Add("CUIGE_SLAM_NAME", "Plate Slam", "杠铃片重砸", "槓鈴片重砸");
            Add("CUIGE_SLAM_DESC",
                "Slam a weight plate for <style=cIsDamage>600% damage</style> within 6m. At 6 Pump, spend all stacks to use Overhead Slam for <style=cIsDamage>1100% damage</style> within 8m. Cooldown: 5s.",
                "抡起杠铃片，对 6 米内的敌人造成<style=cIsDamage>600% 伤害</style>。满 6 层泵感时，消耗全部层数，改为「过顶重砸」，对 8 米内的敌人造成<style=cIsDamage>1100% 伤害</style>。冷却 5 秒。",
                "掄起槓片，對 6 公尺內的敵人造成<style=cIsDamage>600% 傷害</style>。滿 6 層泵感時，消耗全部層數，改為「過頂重砸」，對 8 公尺內的敵人造成<style=cIsDamage>1100% 傷害</style>。冷卻 5 秒。");
            Add("CUIGE_CHARGE_NAME", "Coming Through", "借过一下", "借過一下");
            Add("CUIGE_CHARGE_DESC",
                "Hold to charge for up to 2s, then release to rush a visible enemy within 30m. Damage scales from 350% to 1400%. Without a target, rush toward the crosshair. Take 60% less damage while rushing. Cooldown 6s; direct rush kills reset all charges.",
                "按住 Shift 蓄力，松开冲撞；最多蓄力 2 秒，伤害由 350% 逐渐提高至 1400%。自动锁定准星附近 30 米内可见敌人，无目标时沿准星突进，空中可用。冲撞期间额外减伤 60%。冷却 6 秒；冲撞直接击杀刷新冷却与全部充能。",
                "按住 Shift 蓄力，放開衝撞；最多蓄力 2 秒，傷害由 350% 逐漸提高至 1400%。自動鎖定準星附近 30 公尺內可見敵人，無目標時沿準星突進，空中可用。衝撞期間額外減傷 60%。冷卻 6 秒；衝撞直接擊殺刷新冷卻與全部充能。");
            Add("CUIGE_SHAKER_NAME", "Between-Set Refuel", "组间补给", "組間補給");
            Add("CUIGE_SHAKER_DESC",
                "Press to instantly heal all living allies, including yourself, for 30% of their max health, regardless of range. At 6 Pump, spend all stacks to heal 50% instead. Xiaohei also receives his cup bonus. Cooldown 8s.",
                "按下立即为全队所有存活友军（包含自己）恢复各自最大生命值的 30%，不限距离。满 6 层泵感时消耗全部层数，治疗提高至 50%。小黑仍获得补给羁绊奖励。冷却 8 秒。",
                "按下立即為全隊所有存活友軍（包含自己）恢復各自最大生命值的 30%，不限距離。滿 6 層泵感時消耗全部層數，治療提高至 50%。小黑仍獲得補給羈絆獎勵。冷卻 8 秒。");
        }

        internal static void Add(string token, string en, string zhCN, string zhTW)
        {
            LanguageAPI.Add(token, en);
            LanguageAPI.Add(token, zhCN, "zh-CN");
            LanguageAPI.Add(token, zhTW, "zh-TW");
        }
    }
}

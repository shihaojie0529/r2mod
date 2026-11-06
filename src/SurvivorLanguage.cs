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
                "Stay in punching range to build Pump. At six stacks, Cui Ge taps his fists together twice in front of his chest: spend them on Overhead Slam, or pass a stronger shake to a teammate.",
                "贴身出拳，积攒泵感。叠到六层，崔哥会在胸前双拳互碰两次。这时可以使出「过顶重砸」，也可以把强化的摇摇杯传给队友。",
                "貼身出拳，累積泵感。疊到六層，崔哥會在胸前雙拳互碰兩次。這時可以使出「過頂重砸」，也可以把強化的搖搖杯傳給隊友。");
            Add("RIFTRUNNER_OUTRO", "...and so he left, saving one last shake for a friend.",
                "……于是他离开了，把最后一杯留给了朋友。", "……於是他離開了，把最後一杯留給了朋友。");
            Add("RIFTRUNNER_OUTRO_FAILURE", "...and so he vanished, one set short.",
                "……于是他消失了，还差最后一组。", "……於是他消失了，還差最後一組。");

            Add("CUIGE_PUMP_NAME", "One More Set / Heating Up", "再来一组 / 越练越热", "再來一組 / 越練越熱");
            Add("CUIGE_PUMP_DESC",
                "One More Set: Each punch that deals damage grants 1 Pump: take <style=cIsUtility>5% less damage per stack</style>, up to 6 stacks. Spending all 6 preserves the full 30% reduction for 2s.\nHeating Up: Recover <style=cIsHealing>2% of max health per second</style>. Landing the third punch doubles this to <style=cIsHealing>4% for 4s</style>.",
                "再来一组：每次出拳造成伤害获得 1 层泵感，每层<style=cIsUtility>减伤 5%</style>，最多 6 层。消耗满层后，30% 减伤再保留 2 秒。\n越练越热：每秒恢复<style=cIsHealing>最大生命值的 2%</style>。第三拳命中后，回复提高到<style=cIsHealing>每秒 4%，持续 4 秒</style>。",
                "再來一組：每次出拳造成傷害獲得 1 層泵感，每層<style=cIsUtility>減傷 5%</style>，最多 6 層。消耗滿層後，30% 減傷再保留 2 秒。\n越練越熱：每秒恢復<style=cIsHealing>最大生命值的 2%</style>。第三拳命中後，回復提高到<style=cIsHealing>每秒 4%，持續 4 秒</style>。");
            Add("CUIGE_PUNCH_NAME", "Combination Punch", "组合拳", "組合拳");
            Add("CUIGE_PUNCH_DESC",
                "Jab, throw a short right hook, then uppercut for <style=cIsDamage>160%, 160%, and 300% damage</style>. Land the uppercut to boost your recovery.",
                "依次打出刺拳、后手短摆拳和上勾拳，分别造成<style=cIsDamage>160%、160%、300% 伤害</style>。上勾拳命中会强化自身回复。",
                "依序打出刺拳、後手短擺拳和上鉤拳，分別造成<style=cIsDamage>160%、160%、300% 傷害</style>。上鉤拳命中會強化自身回復。");
            Add("CUIGE_SLAM_NAME", "Plate Slam", "杠铃片重砸", "槓鈴片重砸");
            Add("CUIGE_SLAM_DESC",
                "Slam a weight plate for <style=cIsDamage>600% damage</style> within 6m. At 6 Pump, spend all stacks to use Overhead Slam for <style=cIsDamage>1100% damage</style> within 8m. Cooldown: 5s.",
                "抡起杠铃片，对 6 米内的敌人造成<style=cIsDamage>600% 伤害</style>。满 6 层泵感时，消耗全部层数，改为「过顶重砸」，对 8 米内的敌人造成<style=cIsDamage>1100% 伤害</style>。冷却 5 秒。",
                "掄起槓片，對 6 公尺內的敵人造成<style=cIsDamage>600% 傷害</style>。滿 6 層泵感時，消耗全部層數，改為「過頂重砸」，對 8 公尺內的敵人造成<style=cIsDamage>1100% 傷害</style>。冷卻 5 秒。");
            Add("CUIGE_CHARGE_NAME", "Coming Through", "借过一下", "借過一下");
            Add("CUIGE_CHARGE_DESC",
                "Charge in your aim direction, even in midair. Deal <style=cIsDamage>350% damage</style> to enemies in your path and take <style=cIsUtility>60% less damage</style> during the charge. Cooldown: 6s.",
                "沿准星方向沉肩冲撞，可在空中使用。对沿途敌人造成<style=cIsDamage>350% 伤害</style>，冲撞期间<style=cIsUtility>减伤 60%</style>。冷却 6 秒。",
                "朝準星方向沉肩衝撞，可在空中使用。對沿途敵人造成<style=cIsDamage>350% 傷害</style>，衝撞期間<style=cIsUtility>減傷 60%</style>。冷卻 6 秒。");
            Add("CUIGE_SHAKER_NAME", "Between-Set Refuel", "组间补给", "組間補給");
            Add("CUIGE_SHAKER_DESC",
                "Tap to drink. Hold to aim at an allied player within 45m, then release to pass. Each of 8 healing ticks restores <style=cIsHealing>6% of the recipient's max health</style>. At 6 Pump, spend all stacks to heal <style=cIsHealing>10% per tick</style>. Releasing without a target costs nothing. Cooldown: 8s.",
                "轻点自己喝。长按瞄准 45 米内的队友，松开传杯。每秒治疗一次，共 8 次，每次恢复<style=cIsHealing>饮用者最大生命值的 6%</style>。满 6 层泵感时，消耗全部层数，每次改为<style=cIsHealing>10%</style>。长按后未锁定目标就松开，不消耗技能或泵感。冷却 8 秒。",
                "輕點自己喝。長按瞄準 45 公尺內的隊友，放開傳杯。每秒治療一次，共 8 次，每次恢復<style=cIsHealing>飲用者最大生命值的 6%</style>。滿 6 層泵感時，消耗全部層數，每次改為<style=cIsHealing>10%</style>。長按後未鎖定目標就放開，不消耗技能或泵感。冷卻 8 秒。");
        }

        internal static void Add(string token, string en, string zhCN, string zhTW)
        {
            LanguageAPI.Add(token, en);
            LanguageAPI.Add(token, zhCN, "zh-CN");
            LanguageAPI.Add(token, zhTW, "zh-TW");
        }
    }
}

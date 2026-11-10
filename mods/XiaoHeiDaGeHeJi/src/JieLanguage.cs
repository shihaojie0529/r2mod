using static RiftSurvivor.SurvivorLanguage;

namespace RiftSurvivor
{
    internal static class JieLanguage
    {
        internal static void Register()
        {
            Add("JIEGE_NAME", "Jie Ge", "杰哥", "傑哥");
            Add("JIEGE_SUBTITLE", "The Wandering Sword Rider", "御剑散人", "御劍散人");
            Add("JIEGE_DESCRIPTION",
                "Ride your sword past the enemy line. Plant jade blades in enemies or terrain, then turn their return paths through the crowd. A golden seal lets you direct all one hundred blades of Sword Tempest at one target.",
                "脚踏本命剑，从敌群侧翼绕过去。出鞘的青剑会留在敌人身上或地形中，站好位置再喊一声「剑来」，就能让回剑沿途穿过整排敌人。准星对出金印时开大，一百柄金剑就会追向同一目标。",
                "腳踏本命劍，從敵群側翼繞過去。出鞘的青劍會留在敵人身上或地形中，站好位置再喊一聲「劍來」，就能讓回劍沿途穿過整排敵人。準星對出金印時開大，一百柄金劍就會追向同一目標。");
            Add("JIEGE_OUTRO", "...and so he left, counting his blades back into their case.",
                "……于是他离开了，数着回到匣中的最后几柄剑。", "……於是他離開了，數著回到匣中的最後幾柄劍。");
            Add("JIEGE_OUTRO_FAILURE", "...and so he vanished, his sword still waiting below.",
                "……于是他消失了，本命剑仍悬在他落脚的地方。", "……於是他消失了，本命劍仍懸在他落腳的地方。");
            Add("JIEGE_FLIGHT_NAME", "Sword Rider", "御剑凌空", "御劍凌空");
            Add("JIEGE_FLIGHT_DESC",
                "Jump, then tap jump again to mount your sword. W/S fly forward/backward along your crosshair, A/D strafe; release movement to hover. Tap jump to dismount or remount midfall. Fly for as long as you like.",
                "起跳后再轻点跳跃即可踏剑。W/S 沿准星方向前后飞行，A/D 横移，松开移动键悬停。抬头上升，低头下降。轻点跳跃收剑下落，下落中再点即可重新踏剑。御剑没有时限或冷却。",
                "起跳後再輕點跳躍即可踏劍。W/S 沿準星方向前後飛行，A/D 橫移，鬆開移動鍵懸停。抬頭上升，低頭下降。輕點跳躍收劍下落，下落中再點即可重新踏劍。御劍沒有時限或冷卻。");
            Add("JIEGE_DRAW_NAME", "Unsheathe", "出鞘", "出鞘");
            Add("JIEGE_DRAW_DESC",
                "Hold to launch 3 jade blades per second for <style=cIsDamage>160% damage</style> each. A blade tracks the visible enemy closest to your aim within 60m ahead, or flies straight. Blades lodge in enemies and terrain. Keep up to 9.",
                "按住每秒射出 3 柄青剑，每剑造成<style=cIsDamage>160% 伤害</style>。优先追击前方 60 米内最靠近准星的可见敌人，无目标则直飞。命中敌人或地形会留剑，最多 9 柄，第十柄替换最早的一柄。",
                "按住每秒射出 3 柄青劍，每劍造成<style=cIsDamage>160% 傷害</style>。優先追擊前方 60 公尺內最靠近準星的可見敵人，無目標則直飛。命中敵人或地形會留劍，最多 9 柄，第十柄替換最早的一柄。");
            Add("JIEGE_RECALL_NAME", "Return to Me", "剑来", "劍來");
            Add("JIEGE_RECALL_DESC",
                "Recall every lodged blade to your position at cast time. Each strikes its host and pierces enemies along the way for <style=cIsDamage>180% damage per enemy</style>. Walls stop the blades. Cooldown 4s, spent only when blades return. Direct recall kills fully reset this skill; lodged blades are still required.",
                "所有留剑直线飞回你出招时的位置。每柄对附着目标和沿途敌人各造成<style=cIsDamage>180% 伤害</style>，九剑全中为 1620%。回程遇墙即停。冷却 4 秒，无剑时不消耗冷却。回剑直接击杀敌人时刷新冷却与全部充能，再次召回仍需留剑。",
                "所有留劍直線飛回你出招時的位置。每柄對附著目標和沿途敵人各造成<style=cIsDamage>180% 傷害</style>，九劍全中為 1620%。回程遇牆即停。冷卻 4 秒，無劍時不消耗冷卻。回劍直接擊殺敵人時刷新冷卻與全部充能，再次召回仍需留劍。");
            Add("JIEGE_DASH_NAME", "Catch the Wind", "乘风换位", "乘風換位");
            Add("JIEGE_DASH_DESC",
                "Instantly blink 16m in your flight direction, or along your crosshair while stationary. Walls stop the blink. Leave jade sword echoes and finish hovering on your sword. Cooldown 2s.",
                "沿飞行方向瞬间闪现 16 米，无移动输入时朝准星闪现。遇墙停在墙前，留下青白剑光与残剑，结束时踏剑悬停。冷却 2 秒。",
                "沿飛行方向瞬間閃現 16 公尺，無移動輸入時朝準星閃現。遇牆停在牆前，留下青白劍光與殘劍，結束時踏劍懸停。冷卻 2 秒。");
            Add("JIEGE_STORM_NAME", "Sword Tempest", "万剑归宗", "萬劍歸宗");
            Add("JIEGE_STORM_DESC",
                "Launch 100 golden swords in a steady 5s stream for <style=cIsDamage>200% damage per sword</style>. They auto-target visible enemies within 60m and switch when one dies. Aim at a golden seal to focus them all on it. Finish with a heavenly sword for <style=cIsDamage>1800% damage</style> within 12m. Cooldown 24s.",
                "一百柄金剑从身后连续飞出，5 秒内放完，每柄造成<style=cIsDamage>200% 伤害</style>，自动锁定周身 60 米内的可见敌人，目标阵亡时自动转火。对准敌人的金印开大即可集火。收尾天剑对 12 米内敌人造成<style=cIsDamage>1800% 伤害</style>。冷却 24 秒。",
                "一百柄金劍從身後連續飛出，5 秒內放完，每柄造成<style=cIsDamage>200% 傷害</style>，自動鎖定周身 60 公尺內的可見敵人，目標陣亡時自動轉火。對準敵人的金印開大即可集火。收尾天劍對 12 公尺內敵人造成<style=cIsDamage>1800% 傷害</style>。冷卻 24 秒。");
        }
    }
}

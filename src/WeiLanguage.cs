using static RiftSurvivor.SurvivorLanguage;

namespace RiftSurvivor
{
    internal static class WeiLanguage
    {
        internal static void Register()
        {
            Add("WEIGE_NAME", "Wei Ge", "伟哥", "偉哥");
            Add("WEIGE_SUBTITLE", "The Super Administrator", "超级管理员", "超級管理員");
            Add("WEIGE_DESCRIPTION",
                "Wei Ge runs a smart home that picks its own delivery targets. Fly where you like and it will send chairs and sofas after nearby enemies. To take over, aim and hold primary fire. Your two-way doors can carry teammates and deliveries alike.",
                "伟哥给别墅装了自动送货。你管飞，系统会自己找怪扔家具，身后的飞怪也能锁。想指定收件人，再对准它按住左键。开一扇任意门，队友和飞出去的货物都能从另一头出来。",
                "偉哥給別墅裝了自動送貨。你管飛，系統會自己找怪扔家具，身後的飛怪也能鎖。想指定收件人，再對準牠按住左鍵。開一扇任意門，隊友和飛出去的貨物都能從另一頭出來。");
            Add("WEIGE_OUTRO", "...and so he left, just in time to set the table.", "……于是他离开了，回家正好摆碗筷。", "……於是他離開了，回家正好擺碗筷。");
            Add("WEIGE_OUTRO_FAILURE", "...and so he vanished, with one door still ajar.", "……于是他消失了，还留着一扇虚掩的门。", "……於是他消失了，還留著一扇虛掩的門。");
            Add("WEIGE_STEPS_NAME", "Daily Step Goal", "今日步数达标", "今日步數達標");
            Add("WEIGE_STEPS_DESC",
                "Walk or sprint 20m on the ground to earn one Fat Burn, up to 3. Opening a door adds 1 step. Your next primary or secondary throw spends one stack to deal <style=cIsDamage>25% more damage</style>.",
                "每在地面行走或冲刺 20 米，手表就攒 1 层燃脂，最多 3 层。成功开门记 1 步。左键或右键成功投出时自动消耗 1 层，伤害<style=cIsDamage>提高 25%</style>。",
                "每在地面行走或衝刺 20 公尺，手錶就攢 1 層燃脂，最多 3 層。成功開門記 1 步。左鍵或右鍵成功投出時自動消耗 1 層，傷害<style=cIsDamage>提高 25%</style>。");
            Add("WEIGE_DELIVERY_NAME", "Furniture Delivery", "家具快送", "家具快送");
            Add("WEIGE_DELIVERY_DESC",
                "Automatically lock a visible enemy in any direction within 60m and throw furniture for <style=cIsDamage>320% damage</style> (400% with Fat Burn), even while flying. Hold primary for manual aim. Grounded furniture remains for 6s.",
                "自动锁定周身 60 米内的可见敌人，持续投出家具。每件直击<style=cIsDamage>320% 伤害</style>，消耗燃脂时为 400%。飞行中也会自动攻击，按住左键可改按准星投掷。落地家具保留 6 秒。",
                "自動鎖定周身 60 公尺內的可見敵人，持續投出家具。每件直擊<style=cIsDamage>320% 傷害</style>，消耗燃脂時為 400%。飛行中也會自動攻擊，按住左鍵可改按準星投擲。落地家具保留 6 秒。");
            Add("WEIGE_CUT_NAME", "Cut / Paste", "剪切 / 粘贴", "剪下 / 貼上");
            Add("WEIGE_CUT_DESC",
                "Tap to throw the nearest visible non-boss enemy within 25m. Hold to carry it, even through a door, then release to throw. The payload and its victim each take <style=cIsDamage>600% damage</style> (750% with Fat Burn). Cooldown 5s on a successful throw.",
                "点按可抓起 25 米内最近的可见普通怪，朝准星投出。长按可拎着它穿门，松开时再瞄准。两只怪相撞各受<style=cIsDamage>600% 伤害</style>，消耗燃脂时为 750%。成功投出后冷却 5 秒。",
                "點按可抓起 25 公尺內最近的可見普通怪，朝準星投出。長按可拎著牠穿門，鬆開時再瞄準。兩隻怪相撞各受<style=cIsDamage>600% 傷害</style>，消耗燃脂時為 750%。成功投出後冷卻 5 秒。");
            Add("WEIGE_PORTAL_NAME", "Door to Door", "全屋直达", "全屋直達");
            Add("WEIGE_PORTAL_DESC",
                "Travel up to 20m toward the crosshair, stopping before walls. Leave a two-way door for 3s for yourself, teammates and thrown objects. Cooldown 7s.",
                "沿准星移动最多 20 米，遇墙停在墙前。起点与出口在 3 秒内双向互通，队友、家具和抛出的小怪也能穿门。冷却 7 秒。",
                "沿準星移動最多 20 公尺，遇牆停在牆前。起點與出口在 3 秒內雙向互通，隊友、家具和拋出的小怪也能穿門。冷卻 7 秒。");
            Add("WEIGE_SELECT_NAME", "Select All", "全选", "全選");
            Add("WEIGE_SELECT_DESC",
                "Select up to 6 visible small enemies or grounded furniture near the reticle within 40m and throw them together. Each piece of furniture deals <style=cIsDamage>400% damage</style>. Thrown enemies deal 600% on impact. Keeps Fat Burn. Cooldown 18s on a successful throw.",
                "框起准星附近 40 米内的普通怪和落地家具，最多 6 个，一起投出。每件家具<style=cIsDamage>400% 伤害</style>，每只怪按 600% 碰撞结算。保留燃脂，成功投出后冷却 18 秒。",
                "框起準星附近 40 公尺內的普通怪和落地家具，最多 6 個，一起投出。每件家具<style=cIsDamage>400% 傷害</style>，每隻怪按 600% 碰撞結算。保留燃脂，成功投出後冷卻 18 秒。");
            Add("WEIGE_DOOR_STEP", "Today's steps +1", "今日步数 +1", "今日步數 +1");
            Add("WEIGE_FLIGHT_ON", "Flight enabled", "准星飞行已开启", "準星飛行已開啟");
            Add("WEIGE_FLIGHT_OFF", "Flight disabled", "准星飞行已关闭", "準星飛行已關閉");
            Add("WEIGE_SHOES", "Wei Ge: “Shoes off indoors.”", "伟哥：「进屋换鞋。」", "偉哥：「進屋換鞋。」");
            Add("WEIGE_COACH", "Cui Ge: “This counts toward your workout too?”", "崔哥：「这也算你练的？」", "崔哥：「這也算你練的？」");
            Add("WEIGE_SHARING", "Wei Ge: “Family sharing.”", "伟哥：「家庭共享。」", "偉哥：「家庭共享。」");
            Add("WEIGE_PASS", "Wei Ge: “I set that up perfectly. How could you miss?”", "伟哥：「给你喂这么舒服还打不中？」", "偉哥：「給你餵這麼舒服還打不中？」");
            Add("WEIGE_CATCH", "Xiaohei: “Look again. Who finished it?”", "小黑：「你看清楚，谁补的伤害？」", "小黑：「你看清楚，誰補的傷害？」");
        }
    }
}

using static RiftSurvivor.SurvivorLanguage;

namespace RiftSurvivor
{
    internal static class XiaoheiLanguage
    {
        internal static void Register()
        {
            Add("XIAOHEI_NAME", "Xiaohei", "小黑", "小黑");
            Add("XIAOHEI_SUBTITLE", "The Former Prodigy", "昔日天才", "昔日天才");
            Add("XIAOHEI_DESCRIPTION",
                "Xiaohei is a mobile pistol shooter. Chain hits to build Highlight, blink to a new angle, then call in your past self for a replay. Mark a target for Cui Ge and the two of you can finish it together.",
                "小黑是个靠手感吃饭的枪手。连续命中攒下高光，闪到侧翼，再叫巅峰时期的自己一起架枪。和崔哥组队时，给他正要冲撞或重砸的目标挂上标记，就能补上一枪。",
                "小黑是個靠手感吃飯的槍手。連續命中攢下高光，閃到側翼，再叫巔峰時期的自己一起架槍。和崔哥組隊時，給他正要衝撞或重砸的目標掛上標記，就能補上一槍。");
            Add("XIAOHEI_OUTRO", "...and so he left, with one more clip worth saving.",
                "……于是他离开了。这次的高光，也值得存下来。", "……於是他離開了。這次的高光，也值得存下來。");
            Add("XIAOHEI_OUTRO_FAILURE", "...and so his last replay flickered out.",
                "……于是他消失了，原地的残影也随之熄灭。", "……於是他消失了，原地的殘影也隨之熄滅。");
            Add("XIAOHEI_HIGHLIGHT_NAME", "Found My Rhythm", "手感回来了", "手感回來了");
            Add("XIAOHEI_HIGHLIGHT_DESC",
                "Every 3 consecutive primary hits grant <style=cIsUtility>1 Highlight</style> (up to 3) and advance the current blink recharge by 1s. You may switch targets. A miss resets the streak and preserves earned stacks.",
                "左键每连续命中 3 发，获得 <style=cIsUtility>1 层高光</style>，并使当前一层闪现充能提前 1 秒。最多 3 层，可以换目标。打空会中断连击，保留已有高光。",
                "左鍵每連續命中 3 發，獲得 <style=cIsUtility>1 層高光</style>，並使當前一層閃現充能提前 1 秒。最多 3 層，可以換目標。打空會中斷連擊，保留已有高光。");
            Add("XIAOHEI_PISTOL_NAME", "I've Got the Angle", "我架枪了", "我架槍了");
            Add("XIAOHEI_PISTOL_DESC",
                "Hold to fire 3 energy rounds per second for <style=cIsDamage>220% damage</style> each. Accurate in midair.",
                "按住光能手枪，每秒连射 3 发，每发造成<style=cIsDamage>220% 伤害</style>。空中也能稳定射击。",
                "按住光能手槍，每秒連射 3 發，每發造成<style=cIsDamage>220% 傷害</style>。空中也能穩定射擊。");
            Add("XIAOHEI_SEEKER_NAME", "Whiffed That One", "这波马了", "這波馬了");
            Add("XIAOHEI_SEEKER_DESC",
                "Fire a seeking round for <style=cIsDamage>550% damage</style> and mark its target with an amber ring for 5s. Hold a direction to slide 6m, even in midair. Hit within 1.5s of a primary miss to count that miss toward your streak. Cooldown 4s.",
                "追踪弹造成<style=cIsDamage>550% 伤害</style>，在目标头顶留下持续 5 秒的琥珀色光环。开枪时按住方向键可侧滑 6 米，空中可用。左键打空后 1.5 秒内补中，把那发空枪算回连击。冷却 4 秒。",
                "追蹤彈造成<style=cIsDamage>550% 傷害</style>，在目標頭頂留下持續 5 秒的琥珀色光環。開槍時按住方向鍵可側滑 6 公尺，空中可用。左鍵打空後 1.5 秒內補中，把那發空槍算回連擊。冷卻 4 秒。");
            Add("XIAOHEI_BLINK_NAME", "Lag Spike", "卡了卡了", "卡了卡了");
            Add("XIAOHEI_BLINK_DESC",
                "Tap to blink 12m in your movement direction. Your first blink leaves an afterimage for 3s. Hold while it remains to return for free, even at zero stock. Stores 2 charges, recharging one every 2s.",
                "点按朝移动方向闪现 12 米，可在空中连闪。首次闪现留下 3 秒残影。残影还在时长按可免费回位，充能耗尽也能回。最多 2 层，每 2 秒恢复 1 层。",
                "點按朝移動方向閃現 12 公尺，可在空中連閃。首次閃現留下 3 秒殘影。殘影還在時長按可免費回位，充能耗盡也能回。最多 2 層，每 2 秒恢復 1 層。");
            Add("XIAOHEI_REPLAY_NAME", "Roll the Old Clip", "放段以前的", "放段以前的");
            Add("XIAOHEI_REPLAY_DESC",
                "Your past self fires 6 rounds toward your crosshair over 1.5s for <style=cIsDamage>250% damage</style> each. Spend all Highlight for 2 extra rounds per stack, up to 12. You remain free to fire, slide and blink. Cooldown 10s.",
                "巅峰时期的你在 1.5 秒内朝准星打出 6 发，每发<style=cIsDamage>250% 伤害</style>。消耗全部高光，每层多打 2 发，满层共 12 发。分身开火时，本人仍可开枪、侧滑和闪现。冷却 10 秒。",
                "巔峰時期的你在 1.5 秒內朝準星打出 6 發，每發<style=cIsDamage>250% 傷害</style>。消耗全部高光，每層多打 2 發，滿層共 12 發。分身開火時，本人仍可開槍、側滑和閃現。冷卻 10 秒。");
            Add("XIAOHEI_BOND_COVER", "Xiaohei: “For you? I've always got the angle.”",
                "小黑：「别人我不一定架，你我肯定架。」", "小黑：「別人我不一定架，你我肯定架。」");
            Add("XIAOHEI_BOND_CUP", "Xiaohei: “See that? He saved one just for me.”",
                "小黑：「看见没？他单独给我的。」", "小黑：「看見沒？他單獨給我的。」");
            Add("XIAOHEI_BOND_MISS", "Xiaohei: “...Lag. Definitely lag.”",
                "小黑：「……卡了卡了。」", "小黑：「……卡了卡了。」");
        }
    }
}

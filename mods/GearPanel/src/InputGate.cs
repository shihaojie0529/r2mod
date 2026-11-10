using System;
using BepInEx.Logging;
using HarmonyLib;
using RoR2;
using RoR2.UI;

namespace GearPanel
{
    // While the panel is open the game shows its cursor and ignores character input,
    // otherwise typing in the search box would also move, shoot and fire equipment.
    internal static class InputGate
    {
        private static bool active;
        private static MPEventSystem heldEventSystem;

        public static void Install(string harmonyId, ManualLogSource log)
        {
            try
            {
                var target = AccessTools.Method(typeof(PlayerCharacterMasterController), "CanSendBodyInput");
                var postfix = new HarmonyMethod(typeof(InputGate), nameof(BlockBodyInput));
                new Harmony(harmonyId).Patch(target, postfix: postfix);
            }
            catch (Exception error)
            {
                log.LogWarning($"无法屏蔽角色输入，面板打开时按键仍会操作角色：{error.Message}");
            }
        }

        public static void Begin()
        {
            if (active) return;
            active = true;
            var user = LocalUserManager.GetFirstLocalUser();
            heldEventSystem = user != null ? user.eventSystem : null;
            if (heldEventSystem) heldEventSystem.cursorOpenerCount++;
        }

        public static void End()
        {
            if (!active) return;
            active = false;
            if (heldEventSystem) heldEventSystem.cursorOpenerCount--;
            heldEventSystem = null;
        }

        private static void BlockBodyInput(ref bool __result)
        {
            if (active) __result = false;
        }
    }
}

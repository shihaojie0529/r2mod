using BepInEx;
using BepInEx.Configuration;
using RoR2;
using UnityEngine;

namespace GearPanel
{
    [BepInPlugin(Guid, "道具装备管理面板", "0.1.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private const string Guid = "local.r2mod.gearpanel";

        private readonly PanelView view = new PanelView();
        private ConfigEntry<KeyCode> hotkey;

        private void Awake()
        {
            hotkey = Config.Bind("General", "Hotkey", KeyCode.F6, "打开或关闭道具装备管理面板的按键。");
            InputGate.Install(Guid, Logger);
        }

        private void Update()
        {
            if (view.IsOpen && !Run.instance) view.Close();
            else if (Input.GetKeyDown(hotkey.Value)) Toggle();
        }

        private void Toggle()
        {
            if (view.IsOpen) view.Close();
            else if (Run.instance) view.Open();
        }

        private void OnGUI() => view.Draw();

        private void OnDestroy() => view.Close();
    }
}

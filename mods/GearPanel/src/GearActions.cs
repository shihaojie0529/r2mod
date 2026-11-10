using RoR2;
using UnityEngine.Networking;

namespace GearPanel
{
    // Inventories only accept changes on the server; every edit goes through this gate.
    internal static class GearActions
    {
        public static bool CanEdit => NetworkServer.active && Run.instance;

        public static int CountOf(Inventory inventory, GearEntry entry) =>
            inventory.GetItemCountPermanent(entry.Item);

        public static void ChangeCount(Inventory inventory, GearEntry entry, int change) =>
            SetCount(inventory, entry, GearRules.Adjusted(CountOf(inventory, entry), change));

        public static void SetCount(Inventory inventory, GearEntry entry, int target)
        {
            if (!CanEdit || !inventory) return;
            var difference = target - CountOf(inventory, entry);
            if (difference > 0) inventory.GiveItemPermanent(entry.Item, difference);
            else if (difference < 0) inventory.RemoveItemPermanent(entry.Item, -difference);
        }

        public static void Equip(Inventory inventory, EquipmentIndex equipment)
        {
            if (!CanEdit || !inventory) return;
            var location = inventory.activeEquipmentLocation;
            inventory.SetEquipmentIndex(equipment, in location);
        }
    }
}

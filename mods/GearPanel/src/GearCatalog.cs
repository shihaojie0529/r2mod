using System;
using System.Collections.Generic;
using RoR2;
using UnityEngine;

namespace GearPanel
{
    internal sealed class GearEntry
    {
        private readonly Func<Sprite> loadIcon;
        private Sprite icon;
        private bool iconResolved;

        public GearEntry(string name, string internalName, string tierLabel, Color tierColor, int rank, Func<Sprite> loadIcon)
        {
            Name = name;
            InternalName = internalName;
            TierLabel = tierLabel;
            TierColor = tierColor;
            Rank = rank;
            this.loadIcon = loadIcon;
        }

        public string Name { get; }
        public string InternalName { get; }
        public string TierLabel { get; }
        public Color TierColor { get; }
        public int Rank { get; }
        public ItemIndex Item { get; set; } = ItemIndex.None;
        public EquipmentIndex Equipment { get; set; } = EquipmentIndex.None;

        // Icons may live behind Addressables, so each is resolved only once its row is on screen.
        public Sprite Icon
        {
            get
            {
                if (iconResolved) return icon;
                iconResolved = true;
                try { icon = loadIcon(); }
                catch (Exception) { icon = null; }
                return icon;
            }
        }
    }

    internal sealed class GearCatalog
    {
        private static readonly Color Muted = new Color(0.65f, 0.65f, 0.65f);
        private static readonly Color Void = new Color(0.8f, 0.45f, 0.95f);
        private static readonly Color Lunar = new Color(0.45f, 0.75f, 1f);
        private static readonly Color Boss = new Color(1f, 0.85f, 0.25f);

        public List<GearEntry> Items { get; } = new List<GearEntry>();
        public List<GearEntry> Equipment { get; } = new List<GearEntry>();

        // Rebuilt on every open so names follow the current game language.
        public static GearCatalog Build()
        {
            var catalog = new GearCatalog();
            foreach (var index in ItemCatalog.allItems)
            {
                var def = ItemCatalog.GetItemDef(index);
                if (!def || def.hidden || string.IsNullOrEmpty(def.nameToken)) continue;
                var (label, color, rank) = Describe(def.tier);
                catalog.Items.Add(new GearEntry(Localize(def.nameToken, def.name), def.name, label, color, rank, def.GetIcon) { Item = index });
            }
            foreach (var index in EquipmentCatalog.allEquipment)
            {
                var def = EquipmentCatalog.GetEquipmentDef(index);
                if (!def || string.IsNullOrEmpty(def.nameToken)) continue;
                var (label, color, rank) = Describe(def);
                catalog.Equipment.Add(new GearEntry(Localize(def.nameToken, def.name), def.name, label, color, rank, def.GetIcon) { Equipment = index });
            }
            Comparison<GearEntry> order = (a, b) =>
            {
                var byRank = a.Rank.CompareTo(b.Rank);
                return byRank != 0 ? byRank : string.CompareOrdinal(a.Name, b.Name);
            };
            catalog.Items.Sort(order);
            catalog.Equipment.Sort(order);
            return catalog;
        }

        private static string Localize(string token, string fallback)
        {
            var text = Language.GetString(token);
            return string.IsNullOrEmpty(text) || text == token ? fallback : text;
        }

        private static (string, Color, int) Describe(ItemTier tier)
        {
            switch (tier)
            {
                case ItemTier.Tier1: return ("普通", Color.white, 0);
                case ItemTier.Tier2: return ("罕见", new Color(0.45f, 0.9f, 0.35f), 1);
                case ItemTier.Tier3: return ("传说", new Color(1f, 0.35f, 0.3f), 2);
                case ItemTier.Boss: return ("首领", Boss, 3);
                case ItemTier.Lunar: return ("月球", Lunar, 4);
                case ItemTier.VoidTier1:
                case ItemTier.VoidTier2:
                case ItemTier.VoidTier3:
                case ItemTier.VoidBoss: return ("虚空", Void, 5);
                default: return ("其他", Muted, 6);
            }
        }

        private static (string, Color, int) Describe(EquipmentDef def)
        {
            if (def.isLunar) return ("月球", Lunar, 2);
            if (def.isBoss) return ("首领", Boss, 1);
            return def.canDrop ? ("装备", new Color(1f, 0.6f, 0.2f), 0) : ("其他", Muted, 3);
        }
    }
}

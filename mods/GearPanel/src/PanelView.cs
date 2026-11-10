using System.Collections.Generic;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace GearPanel
{
    // Uses fixed rects instead of GUILayout and draws only on-screen rows: the catalog has hundreds of entries.
    internal sealed class PanelView
    {
        private const float DesignHeight = 1080f;
        private const float PanelWidth = 1040f;
        private const float PanelHeight = 800f;
        private const float Padding = 16f;
        private const float RowHeight = 48f;
        private const float ControlHeight = 36f;

        private enum Tab { Items, Equipment }

        private readonly List<PlayerCharacterMasterController> players = new List<PlayerCharacterMasterController>();
        private readonly List<GearEntry> visible = new List<GearEntry>();

        private GearCatalog catalog = new GearCatalog();
        private PlayerCharacterMasterController target;
        private Tab tab = Tab.Items;
        private bool heldOnly;
        private string search = "";
        private string amountText = "1";
        private Vector2 scroll;

        private Font font;
        private GUIStyle label, centered, title, button, field, panelBackground, rowBackground;
        private Texture2D cursorTexture;

        public bool IsOpen { get; private set; }

        public void Open()
        {
            catalog = GearCatalog.Build();
            scroll = Vector2.zero;
            IsOpen = true;
            InputGate.Begin();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            InputGate.End();
        }

        public void Draw()
        {
            if (!IsOpen) return;
            EnsureStyles();
            if (Event.current.type == EventType.Layout) Refresh();

            var previousMatrix = GUI.matrix;
            var scale = Screen.height / DesignHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            var panel = new Rect((Screen.width / scale - PanelWidth) / 2f, (DesignHeight - PanelHeight) / 2f, PanelWidth, PanelHeight);
            GUI.Box(panel, GUIContent.none, panelBackground);
            var x = panel.x + Padding;
            var y = panel.y + Padding;
            var width = panel.width - Padding * 2f;

            GUI.Label(new Rect(x, y, width - 60f, 40f), "道具装备管理", title);
            if (GUI.Button(new Rect(x + width - ControlHeight, y, ControlHeight, ControlHeight), "×", button)) Close();
            y += 52f;

            if (!NetworkServer.active)
                GUI.Label(new Rect(x, y, width, 40f), "只有房主可以修改玩家的道具和装备。", label);
            else if (!target)
                GUI.Label(new Rect(x, y, width, 40f), "当前没有可操作的玩家。", label);
            else
                DrawEditor(x, y, width, panel.yMax - Padding - y);

            DrawCursor();
            GUI.matrix = previousMatrix;
        }

        private void Refresh()
        {
            players.Clear();
            foreach (var player in PlayerCharacterMasterController.instances)
                if (player && player.master && player.master.inventory) players.Add(player);
            if (!target || !players.Contains(target)) target = DefaultTarget();

            visible.Clear();
            if (!target) return;
            var inventory = target.master.inventory;
            foreach (var entry in tab == Tab.Items ? catalog.Items : catalog.Equipment)
            {
                if (!GearRules.Matches(search, entry.Name, entry.InternalName)) continue;
                if (heldOnly && !IsHeld(inventory, entry)) continue;
                visible.Add(entry);
            }
        }

        private PlayerCharacterMasterController DefaultTarget()
        {
            var localUser = LocalUserManager.GetFirstLocalUser();
            var local = localUser != null ? localUser.cachedMasterController : null;
            if (local && players.Contains(local)) return local;
            return players.Count > 0 ? players[0] : null;
        }

        private bool IsHeld(Inventory inventory, GearEntry entry) =>
            tab == Tab.Items ? GearActions.CountOf(inventory, entry) > 0 : inventory.HasEquipment(entry.Equipment);

        private void DrawEditor(float x, float y, float width, float height)
        {
            var bottom = y + height;
            DrawTargetPicker(x, y);
            GUI.Label(new Rect(x + width - 210f, y, 90f, ControlHeight), "数量", label);
            amountText = GUI.TextField(new Rect(x + width - 130f, y, 130f, ControlHeight), amountText, 4, field);
            y += ControlHeight + 10f;

            DrawFilters(x, y, width);
            y += ControlHeight + 12f;

            var inventory = target.master.inventory;
            DrawRows(new Rect(x, y, width, bottom - y), inventory);
        }

        private void DrawTargetPicker(float x, float y)
        {
            GUI.Label(new Rect(x, y, 90f, ControlHeight), "目标玩家", label);
            var cycleLeft = new Rect(x + 96f, y, ControlHeight, ControlHeight);
            var name = new Rect(cycleLeft.xMax + 6f, y, 330f, ControlHeight);
            var cycleRight = new Rect(name.xMax + 6f, y, ControlHeight, ControlHeight);
            if (GUI.Button(cycleLeft, "‹", button)) CycleTarget(-1);
            GUI.Label(name, target.GetDisplayName(), centered);
            if (GUI.Button(cycleRight, "›", button)) CycleTarget(1);
        }

        private void CycleTarget(int step)
        {
            if (players.Count == 0) return;
            var next = (players.IndexOf(target) + step + players.Count) % players.Count;
            target = players[next];
            scroll = Vector2.zero;
        }

        private void DrawFilters(float x, float y, float width)
        {
            if (Selectable(new Rect(x, y, 90f, ControlHeight), "道具", tab == Tab.Items)) SwitchTab(Tab.Items);
            if (Selectable(new Rect(x + 96f, y, 90f, ControlHeight), "装备", tab == Tab.Equipment)) SwitchTab(Tab.Equipment);
            if (Selectable(new Rect(x + 200f, y, 130f, ControlHeight), "只看已持有", heldOnly))
            {
                heldOnly = !heldOnly;
                scroll = Vector2.zero;
            }

            GUI.Label(new Rect(x + 346f, y, 60f, ControlHeight), "搜索", label);
            var searchBox = new Rect(x + 406f, y, width - 406f - 90f, ControlHeight);
            var edited = GUI.TextField(searchBox, search, 40, field);
            if (edited != search)
            {
                search = edited;
                scroll = Vector2.zero;
            }
            GUI.Label(new Rect(searchBox.xMax + 6f, y, 84f, ControlHeight), $"共 {visible.Count} 项", centered);
        }

        private void SwitchTab(Tab next)
        {
            tab = next;
            scroll = Vector2.zero;
        }

        private bool Selectable(Rect rect, string text, bool selected)
        {
            var previous = GUI.backgroundColor;
            if (selected) GUI.backgroundColor = new Color(0.35f, 0.65f, 1f);
            var pressed = GUI.Button(rect, text, button);
            GUI.backgroundColor = previous;
            return pressed;
        }

        private void DrawRows(Rect view, Inventory inventory)
        {
            if (visible.Count == 0)
            {
                GUI.Label(new Rect(view.x, view.y, view.width, 40f), "没有符合条件的条目。", centered);
                return;
            }

            var content = new Rect(0f, 0f, view.width - 22f, visible.Count * RowHeight);
            scroll.y = Mathf.Min(scroll.y, Mathf.Max(0f, content.height - view.height));
            scroll = GUI.BeginScrollView(view, scroll, content, false, true);
            var first = Mathf.Max(0, Mathf.FloorToInt(scroll.y / RowHeight));
            var last = Mathf.Min(visible.Count - 1, first + Mathf.CeilToInt(view.height / RowHeight));
            var amount = GearRules.ParseAmount(amountText);
            for (var index = first; index <= last; index++)
            {
                var row = new Rect(0f, index * RowHeight, content.width, RowHeight - 4f);
                GUI.Box(row, GUIContent.none, rowBackground);
                DrawRowHeading(row, visible[index]);
                if (tab == Tab.Items) DrawItemControls(row, inventory, visible[index], amount);
                else DrawEquipmentControls(row, inventory, visible[index]);
            }
            GUI.EndScrollView();
        }

        private void DrawRowHeading(Rect row, GearEntry entry)
        {
            DrawIcon(new Rect(row.x + 6f, row.y + 3f, 38f, 38f), entry.Icon);
            GUI.Label(new Rect(row.x + 52f, row.y, 330f, row.height), entry.Name, label);
            var previous = GUI.contentColor;
            GUI.contentColor = entry.TierColor;
            GUI.Label(new Rect(row.x + 386f, row.y, 60f, row.height), entry.TierLabel, label);
            GUI.contentColor = previous;
        }

        private static void DrawIcon(Rect rect, Sprite sprite)
        {
            if (!sprite || !sprite.texture) return;
            var texture = sprite.texture;
            var area = sprite.textureRect;
            var coordinates = new Rect(area.x / texture.width, area.y / texture.height, area.width / texture.width, area.height / texture.height);
            GUI.DrawTextureWithTexCoords(rect, texture, coordinates);
        }

        private void DrawItemControls(Rect row, Inventory inventory, GearEntry entry, int amount)
        {
            var y = row.y + (row.height - ControlHeight) / 2f;
            var count = GearActions.CountOf(inventory, entry);
            GUI.Label(new Rect(row.x + 452f, row.y, 76f, row.height), $"持有 {count}", centered);
            if (GUI.Button(new Rect(row.x + 532f, y, 100f, ControlHeight), $"+{amount}", button))
                GearActions.ChangeCount(inventory, entry, amount);
            if (GUI.Button(new Rect(row.x + 638f, y, 100f, ControlHeight), $"-{amount}", button))
                GearActions.ChangeCount(inventory, entry, -amount);
            if (GUI.Button(new Rect(row.x + 744f, y, 110f, ControlHeight), $"设为 {amount}", button))
                GearActions.SetCount(inventory, entry, amount);
            if (GUI.Button(new Rect(row.x + 860f, y, 124f, ControlHeight), "移除", button))
                GearActions.SetCount(inventory, entry, 0);
        }

        private void DrawEquipmentControls(Rect row, Inventory inventory, GearEntry entry)
        {
            var y = row.y + (row.height - ControlHeight) / 2f;
            var isCurrent = inventory.GetEquipmentIndex() == entry.Equipment;
            GUI.Label(new Rect(row.x + 452f, row.y, 180f, row.height), isCurrent ? "当前装备" : "", centered);
            var pressed = GUI.Button(new Rect(row.x + 860f, y, 124f, ControlHeight), isCurrent ? "卸下" : "装备", button);
            if (pressed) GearActions.Equip(inventory, isCurrent ? EquipmentIndex.None : entry.Equipment);
        }

        // The game's cursor is a canvas element that IMGUI paints over, so the panel draws its own.
        private void DrawCursor()
        {
            if (Event.current.type != EventType.Repaint) return;
            var position = Event.current.mousePosition;
            GUI.DrawTexture(new Rect(position.x, position.y, 24f, 24f), cursorTexture);
        }

        private void EnsureStyles()
        {
            if (label != null) return;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "PingFang SC", "Noto Sans CJK SC", "Arial" }, 18);
            label = new GUIStyle(GUI.skin.label) { font = font, fontSize = 18, alignment = TextAnchor.MiddleLeft, richText = false };
            label.normal.textColor = Color.white;
            centered = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
            title = new GUIStyle(label) { fontSize = 26, fontStyle = FontStyle.Bold };
            button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 18, richText = false };
            field = new GUIStyle(GUI.skin.textField) { font = font, fontSize = 18, alignment = TextAnchor.MiddleLeft, richText = false };
            panelBackground = new GUIStyle { normal = { background = Solid(new Color(0.07f, 0.08f, 0.11f, 0.97f)) } };
            rowBackground = new GUIStyle { normal = { background = Solid(new Color(1f, 1f, 1f, 0.05f)) } };
            cursorTexture = BuildCursor();
        }

        private static Texture2D Solid(Color color)
        {
            var texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private static Texture2D BuildCursor()
        {
            const int size = 24;
            var texture = new Texture2D(size, size) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point };
            for (var row = 0; row < size; row++)
            {
                for (var column = 0; column < size; column++)
                {
                    // IMGUI shows row 0 at the bottom of the texture; the arrow tip must be top-left.
                    var fromTop = size - 1 - row;
                    var inside = column <= fromTop && column + fromTop / 2 < size;
                    var edge = inside && (column == 0 || column == fromTop || column + fromTop / 2 == size - 1);
                    texture.SetPixel(column, row, !inside ? Color.clear : edge ? Color.black : Color.white);
                }
            }
            texture.Apply();
            return texture;
        }
    }
}

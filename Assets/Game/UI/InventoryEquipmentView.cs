using System;
using System.Collections.Generic;
using HeroesOfDragonland.Items;
using UnityEngine;
using UnityEngine.UIElements;

namespace HeroesOfDragonland.UI
{
    [DisallowMultipleComponent, RequireComponent(typeof(UIDocument))]
    public sealed class InventoryEquipmentView : MonoBehaviour
    {
        private VisualElement root, window, grid;
        private Button toggle, equip, unequip, consume;
        private Label details, status, tooltip;
        private readonly List<ItemButton> inventorySlots = new();
        private readonly List<ItemButton> equipmentSlots = new();
        public event Action ToggleRequested;
        public event Action CloseRequested;
        public event Action<int> InventorySelected;
        public event Action<EquipmentSlot> EquipmentSelected;
        public event Action EquipRequested;
        public event Action UnequipRequested;
        public bool IsOpen => window != null && window.style.display.value != DisplayStyle.None;
        public bool EquipEnabled => equip != null && equip.enabledSelf;
        public bool UnequipEnabled => unequip != null && unequip.enabledSelf;
        public bool ConsumeEnabled => consume != null && consume.enabledSelf;
        public string DetailsText => details?.text;
        public string StatusText => status?.text;
        public int DisplayedSlotCount => inventorySlots.Count;

        private void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear(); inventorySlots.Clear(); equipmentSlots.Clear();
            root.pickingMode = PickingMode.Ignore;
            root.style.flexGrow = 1;
            root.style.alignItems = Align.Center; root.style.justifyContent = Justify.Center;
            root.style.fontSize = 16; root.style.color = Color.white;
            toggle = new Button(() => ToggleRequested?.Invoke()) { text = "Inventory [I]" };
            toggle.style.position = Position.Absolute; toggle.style.right = 22; toggle.style.bottom = 22;
            toggle.style.width = 160; toggle.style.height = 34;
            root.Add(toggle);
            window = new VisualElement { name = "InventoryEquipmentWindow" };
            window.style.width = Length.Percent(94); window.style.maxWidth = 1000;
            window.style.height = Length.Percent(82); window.style.maxHeight = 610;
            window.style.backgroundColor = new Color(.035f, .045f, .065f, .98f);
            window.style.paddingLeft = window.style.paddingRight = 16;
            window.style.paddingTop = window.style.paddingBottom = 12;
            root.Add(window);
            var header = new VisualElement(); header.style.flexDirection = FlexDirection.Row;
            var title = new Label("INVENTORY & EQUIPMENT"); title.style.fontSize = 22; title.style.flexGrow = 1;
            header.Add(title); header.Add(new Button(() => CloseRequested?.Invoke()) { text = "Close [Esc]" });
            window.Add(header);
            var hint = new Label("Select a slot to inspect it. Hover an item for its tooltip."); hint.style.marginBottom = 12; window.Add(hint);
            var columns = new VisualElement(); columns.style.flexDirection = FlexDirection.Row; columns.style.flexGrow = 1; columns.style.minHeight = 0;
            window.Add(columns);
            var bag = Column(columns, "BAG", 38);
            var scroll = new ScrollView(ScrollViewMode.Vertical); scroll.style.flexGrow = 1; bag.Add(scroll);
            grid = new VisualElement(); grid.style.flexDirection = FlexDirection.Row; grid.style.flexWrap = Wrap.Wrap; scroll.Add(grid);
            var gear = Column(columns, "EQUIPMENT", 24);
            var gearScroll = new ScrollView(ScrollViewMode.Vertical); gear.Add(gearScroll);
            for (int i = 1; i <= 6; i++)
            {
                var slot = (EquipmentSlot)i;
                var button = new ItemButton(() => EquipmentSelected?.Invoke(slot), true);
                button.style.height = 58; button.style.marginBottom = 5;
                button.Set(null, 0, slot.ToString(), false, "Empty " + slot);
                AttachTooltip(button); gearScroll.Add(button); equipmentSlots.Add(button);
            }
            var info = Column(columns, "ITEM DETAILS", 38);
            var detailScroll = new ScrollView(ScrollViewMode.Vertical); detailScroll.style.flexGrow = 1; info.Add(detailScroll);
            details = new Label("Select an item."); details.style.whiteSpace = WhiteSpace.Normal; detailScroll.Add(details);
            equip = new Button(() => EquipRequested?.Invoke()) { text = "Equip" }; equip.style.height = 32; info.Add(equip);
            unequip = new Button(() => UnequipRequested?.Invoke()) { text = "Unequip" }; unequip.style.height = 32; info.Add(unequip);
            consume = new Button { text = "Use unavailable" }; consume.SetEnabled(false); info.Add(consume);
            status = new Label(""); status.style.whiteSpace = WhiteSpace.Normal; status.style.minHeight = 40; status.style.marginTop = 10; window.Add(status);
            tooltip = new Label(); tooltip.pickingMode = PickingMode.Ignore;
            tooltip.style.position = Position.Absolute; tooltip.style.width = 300; tooltip.style.maxHeight = Length.Percent(85);
            tooltip.style.whiteSpace = WhiteSpace.Normal; tooltip.style.fontSize = 14;
            tooltip.style.backgroundColor = new Color(.07f, .08f, .12f, 1);
            tooltip.style.paddingLeft = tooltip.style.paddingRight = 12; tooltip.style.paddingTop = tooltip.style.paddingBottom = 10;
            root.Add(tooltip);
            SetAvailable(false); SetOpen(false); SetDetails("Select an item.", false, false, false);
        }
        private static VisualElement Column(VisualElement parent, string heading, float width)
        {
            var column = new VisualElement(); column.style.width = Length.Percent(width); column.style.paddingRight = 10; column.style.minHeight = 0;
            var label = new Label(heading); label.style.unityFontStyleAndWeight = FontStyle.Bold; label.style.marginBottom = 8;
            column.Add(label); parent.Add(column); return column;
        }
        private void AttachTooltip(ItemButton button)
        {
            button.RegisterCallback<PointerEnterEvent>(evt => { if (button.HasItem) { tooltip.text = button.Description; tooltip.style.display = DisplayStyle.Flex; PositionTooltip(evt.position); } });
            button.RegisterCallback<PointerMoveEvent>(evt => PositionTooltip(evt.position));
            button.RegisterCallback<PointerLeaveEvent>(_ => HideTooltip());
        }
        private void PositionTooltip(Vector2 point)
        {
            point = root.WorldToLocal(point);
            tooltip.style.left = Mathf.Clamp(point.x + 18, 0, Mathf.Max(0, root.layout.width - 310));
            float height = float.IsNaN(tooltip.layout.height) ? 320 : tooltip.layout.height;
            tooltip.style.top = Mathf.Clamp(point.y + 16, 0, Mathf.Max(0, root.layout.height - height - 12));
        }
        public void HideTooltip() { if (tooltip != null) tooltip.style.display = DisplayStyle.None; }
        public void SetAvailable(bool value) { toggle.style.display = value ? DisplayStyle.Flex : DisplayStyle.None; if (!value) SetOpen(false); }
        public void SetOpen(bool value) { window.style.display = value ? DisplayStyle.Flex : DisplayStyle.None; toggle.text = value ? "Close inventory [I]" : "Inventory [I]"; HideTooltip(); }
        public void SetSlotCount(int count)
        {
            if (inventorySlots.Count == count) return;
            inventorySlots.Clear(); grid.Clear();
            for (int i = 0; i < count; i++)
            {
                int index = i;
                var button = new ItemButton(() => InventorySelected?.Invoke(index), false);
                button.style.width = Length.Percent(23); button.style.marginRight = Length.Percent(1); button.style.marginBottom = 5;
                button.style.height = 80; AttachTooltip(button); grid.Add(button); inventorySlots.Add(button);
            }
        }
        public void SetInventorySlot(int index, ItemDefinition item, int amount, bool selected, string text)
            => inventorySlots[index].Set(item, amount, "Slot " + (index + 1), selected, text);
        public void SetEquipmentSlot(EquipmentSlot slot, ItemDefinition item, bool selected, string text)
            => equipmentSlots[(int)slot - 1].Set(item, item != null ? 1 : 0, slot.ToString(), selected, text);
        public void SetDetails(string text, bool canEquip, bool canUnequip, bool consumable)
        {
            details.text = text; equip.SetEnabled(canEquip); unequip.SetEnabled(canUnequip);
            consume.style.display = consumable ? DisplayStyle.Flex : DisplayStyle.None;
        }
        public void SetStatus(string text) => status.text = text;

        private sealed class ItemButton : Button
        {
            private readonly Image image;
            private readonly PlaceholderIcon fallback;
            private readonly Label nameLabel, amountLabel;
            private readonly bool equipment;
            public string Description { get; private set; }
            public bool HasItem { get; private set; }
            public ItemButton(Action clicked, bool equipment) : base(clicked)
            {
                this.equipment = equipment;
                style.paddingLeft = style.paddingRight = 3;
                if (equipment) style.flexDirection = FlexDirection.Row;
                var icon = new VisualElement(); icon.style.width = 34; icon.style.height = 34; icon.style.flexShrink = 0;
                image = new Image { scaleMode = ScaleMode.ScaleToFit }; image.StretchToParentSize(); icon.Add(image);
                fallback = new PlaceholderIcon(); fallback.StretchToParentSize(); icon.Add(fallback); Add(icon);
                nameLabel = new Label(); nameLabel.style.fontSize = 11; nameLabel.style.whiteSpace = WhiteSpace.Normal; Add(nameLabel);
                amountLabel = new Label(); amountLabel.style.position = Position.Absolute; amountLabel.style.right = 3; amountLabel.style.top = 2; amountLabel.style.fontSize = 12; Add(amountLabel);
                IgnoreChildren(this);
            }
            private static void IgnoreChildren(VisualElement element) { foreach (var child in element.Children()) { child.pickingMode = PickingMode.Ignore; IgnoreChildren(child); } }
            public void Set(ItemDefinition item, int amount, string emptyName, bool selected, string description)
            {
                HasItem = item != null; Description = description;
                image.sprite = item != null ? item.Icon : null;
                image.style.display = image.sprite != null ? DisplayStyle.Flex : DisplayStyle.None;
                fallback.style.display = item != null && image.sprite == null ? DisplayStyle.Flex : DisplayStyle.None;
                fallback.Item = item; fallback.MarkDirtyRepaint();
                nameLabel.text = item == null ? emptyName : (equipment ? emptyName + "\n" : "") + item.DisplayName;
                amountLabel.text = item != null && !equipment ? amount.ToString() : "";
                Color rarity = item == null ? new Color(.2f, .23f, .3f) : RarityColor(item.Rarity);
                style.borderLeftColor = style.borderRightColor = style.borderTopColor = style.borderBottomColor = rarity;
                style.borderLeftWidth = style.borderRightWidth = style.borderTopWidth = style.borderBottomWidth = 2;
                style.backgroundColor = selected ? new Color(.24f, .32f, .42f) : new Color(.09f, .11f, .15f);
                nameLabel.style.color = item != null ? rarity : Color.gray;
            }
        }
        public static Color RarityColor(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Uncommon => new Color(.4f, .9f, .5f), ItemRarity.Rare => new Color(.45f, .7f, 1),
            ItemRarity.Epic => new Color(.8f, .5f, 1), ItemRarity.Legendary => new Color(1, .7f, .25f), _ => new Color(.85f, .87f, .9f)
        };
        private sealed class PlaceholderIcon : VisualElement
        {
            public ItemDefinition Item;
            public PlaceholderIcon() => generateVisualContent += Draw;
            private void Draw(MeshGenerationContext context)
            {
                if (Item == null) return;
                var p = context.painter2D; p.strokeColor = RarityColor(Item.Rarity); p.fillColor = new Color(.2f, .3f, .42f); p.lineWidth = 2;
                p.BeginPath();
                if (Item.Category == ItemCategory.Consumable)
                { p.MoveTo(new Vector2(13, 3)); p.LineTo(new Vector2(21, 3)); p.LineTo(new Vector2(21, 11)); p.LineTo(new Vector2(28, 19)); p.LineTo(new Vector2(25, 30)); p.LineTo(new Vector2(9, 30)); p.LineTo(new Vector2(6, 19)); p.LineTo(new Vector2(13, 11)); }
                else if (Item.Category == ItemCategory.Weapon)
                { p.MoveTo(new Vector2(28, 3)); p.LineTo(new Vector2(26, 13)); p.LineTo(new Vector2(15, 24)); p.LineTo(new Vector2(10, 19)); }
                else if (Item.Category == ItemCategory.Armor)
                { p.MoveTo(new Vector2(7, 6)); p.LineTo(new Vector2(13, 3)); p.LineTo(new Vector2(21, 3)); p.LineTo(new Vector2(28, 6)); p.LineTo(new Vector2(25, 21)); p.LineTo(new Vector2(17, 31)); p.LineTo(new Vector2(9, 21)); }
                else { p.MoveTo(new Vector2(17, 3)); p.LineTo(new Vector2(30, 17)); p.LineTo(new Vector2(17, 31)); p.LineTo(new Vector2(4, 17)); }
                p.ClosePath(); p.Fill(); p.Stroke();
                if (Item.Category == ItemCategory.Weapon)
                { p.BeginPath(); p.MoveTo(new Vector2(6, 17)); p.LineTo(new Vector2(20, 29)); p.MoveTo(new Vector2(12, 23)); p.LineTo(new Vector2(5, 31)); p.Stroke(); }
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace HeroesOfDragonland.UI.Minimap
{
    // Passive schematic UI. It never renders a camera view of hidden scene objects.
    [DefaultExecutionOrder(270), DisallowMultipleComponent, RequireComponent(typeof(UIDocument))]
    public sealed class TacticalMinimapView : MonoBehaviour
    {
        [SerializeField] private TacticalMapSource source;
        [SerializeField, Min(0)] private float topOffset = 270;
        [SerializeField] private bool showWorldObjectiveIcons;
        private VisualElement root, panel, map, background, symbols, worldLayer;
        private Label compass;
        private readonly List<Label> icons = new(), worldIcons = new();
        private TacticalMapDefinition drawnDefinition;
        private int revision = -1;
        public int VisibleMarkerCount { get; private set; }
        private void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement; root.Clear(); root.pickingMode = PickingMode.Ignore;
            worldLayer = new VisualElement(); Fill(worldLayer); root.Add(worldLayer);
            panel = new VisualElement { name = "TacticalMinimap" }; panel.pickingMode = PickingMode.Ignore;
            panel.style.position = Position.Absolute; panel.style.right = 18; panel.style.top = topOffset;
            panel.style.width = 226; panel.style.backgroundColor = new Color(.045f, .055f, .065f, .96f); panel.style.color = Color.white;
            panel.style.paddingLeft = panel.style.paddingRight = 8; panel.style.paddingTop = panel.style.paddingBottom = 6;
            panel.Add(new Label("TACTICAL MAP  |  N ^"));
            map = new VisualElement(); map.pickingMode = PickingMode.Ignore; map.style.height = 210; map.style.width = 210;
            map.style.position = Position.Relative; map.style.overflow = Overflow.Hidden; map.style.backgroundColor = new Color(.11f, .13f, .14f);
            background = new VisualElement(); Fill(background); map.Add(background);
            symbols = new VisualElement(); Fill(symbols); map.Add(symbols); panel.Add(map);
            compass = new Label(); compass.style.fontSize = 11; panel.Add(compass);
            var legend = new Label("@ You  o Ally  F Fort  K King\nC Camp  B Boss  G Objective\nx Cleared  ? Unknown  ! Reveal"); legend.style.fontSize = 10; panel.Add(legend);
            root.Add(panel); revision = -1; drawnDefinition = null; icons.Clear(); worldIcons.Clear();
            root.RegisterCallback<GeometryChangedEvent>(_ => Layout()); Layout();
        }
        private static void Fill(VisualElement element)
        { element.pickingMode = PickingMode.Ignore; element.style.position = Position.Absolute; element.style.left = element.style.top = element.style.right = element.style.bottom = 0; }
        private void Layout()
        {
            if (!TacticalMapRules.Finite(root.resolvedStyle.height) || root.resolvedStyle.height <= 0) return;
            float side = root.resolvedStyle.width < 1000 ? 160 : 210;
            map.style.width = map.style.height = side; panel.style.width = side + 16;
            panel.style.top = Mathf.Max(18, Mathf.Min(topOffset, root.resolvedStyle.height - side - 108));
        }
        private static Color TeamColor(MapTeam team) => team == MapTeam.A ? new Color(.4f, .75f, 1) : team == MapTeam.B ? new Color(1, .48f, .35f) : new Color(.9f, .8f, .56f);
        private static string Glyph(MapMarker marker)
        {
            string glyph = marker.Kind switch { MapMarkerKind.LocalPlayer => "@", MapMarkerKind.Ally => "o", MapMarkerKind.Fortress => "F",
                MapMarkerKind.King => "K", MapMarkerKind.Camp => "C", MapMarkerKind.BossCamp => "B", MapMarkerKind.Objective => "G", _ => "!" };
            if (marker.State == MapMarkerState.Cleared) glyph += "x";
            else if (marker.State == MapMarkerState.Unknown) glyph += "?";
            else if (marker.State == MapMarkerState.Unavailable) glyph += "-";
            if (marker.Team != MapTeam.None) glyph += marker.Team.ToString();
            return glyph;
        }
        private static Label Icon(List<Label> pool, VisualElement parent, int index)
        {
            while (pool.Count <= index)
            {
                var icon = new Label(); icon.pickingMode = PickingMode.Ignore; icon.style.position = Position.Absolute;
                icon.style.fontSize = 12; icon.style.unityFontStyleAndWeight = FontStyle.Bold;
                icon.style.backgroundColor = new Color(.025f, .03f, .035f, .9f); icon.style.paddingLeft = icon.style.paddingRight = 2;
                icon.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50)); parent.Add(icon); pool.Add(icon);
            }
            return pool[index];
        }
        private void LateUpdate()
        {
            bool visible = source != null && source.isActiveAndEnabled && source.Visible;
            panel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible) { VisibleMarkerCount = 0; foreach (var icon in worldIcons) icon.style.display = DisplayStyle.None; return; }
            if (revision != source.Revision)
            {
                revision = source.Revision; var definition = source.Definition;
                if (drawnDefinition != definition) { drawnDefinition = definition; DrawRegions(definition); }
                int count = 0; Label localIcon = null;
                foreach (var marker in source.Markers)
                {
                    if (count >= 80) break;
                    if (!TacticalMapRules.Project(marker.Position, definition.Center.x, definition.Center.y, definition.Size.x, definition.Size.y, out var u, out var v)) continue;
                    var icon = Icon(icons, symbols, count++); icon.text = Glyph(marker); icon.tooltip = marker.Label;
                    icon.style.color = marker.Kind == MapMarkerKind.LocalPlayer ? Color.white
                        : marker.Kind == MapMarkerKind.RevealedEnemy ? new Color(1, .57f, .24f) : TeamColor(marker.Team);
                    icon.style.left = Length.Percent((float)u * 100); icon.style.top = Length.Percent((float)v * 100); icon.style.display = DisplayStyle.Flex;
                    if (marker.Kind == MapMarkerKind.LocalPlayer) localIcon = icon;
                }
                localIcon?.BringToFront();
                for (int i = count; i < icons.Count; i++) icons[i].style.display = DisplayStyle.None;
                VisibleMarkerCount = count; compass.text = "N = +Z  |  Facing " + TacticalMapRules.Compass(source.Heading) + " " + Mathf.RoundToInt(source.Heading) + " deg";
            }
            DrawWorldIcons();
        }
        private void DrawRegions(TacticalMapDefinition definition)
        {
            background.Clear(); float side = Mathf.Max(definition.Size.x, definition.Size.y);
            foreach (var region in definition.Regions)
            {
                var rectangle = new VisualElement(); rectangle.pickingMode = PickingMode.Ignore; rectangle.style.position = Position.Absolute;
                rectangle.style.left = Length.Percent(50 + (region.center.x - definition.Center.x - region.size.x * .5f) / side * 100);
                rectangle.style.top = Length.Percent(50 - (region.center.y - definition.Center.y + region.size.y * .5f) / side * 100);
                rectangle.style.width = Length.Percent(region.size.x / side * 100); rectangle.style.height = Length.Percent(region.size.y / side * 100);
                rectangle.style.backgroundColor = region.color; background.Add(rectangle);
            }
        }
        private void DrawWorldIcons()
        {
            int count = 0; var camera = showWorldObjectiveIcons ? Camera.main : null;
            if (camera != null) foreach (var marker in source.Markers)
            {
                if (marker.Kind == MapMarkerKind.LocalPlayer || marker.Kind == MapMarkerKind.Ally || marker.Kind == MapMarkerKind.RevealedEnemy) continue;
                var point = new Vector3((float)marker.Position.X, (float)marker.Position.Y + 3, (float)marker.Position.Z);
                var screen = camera.WorldToViewportPoint(point);
                if (screen.z <= 0 || screen.x < 0 || screen.x > 1 || screen.y < 0 || screen.y > 1) continue;
                var icon = Icon(worldIcons, worldLayer, count++); icon.text = Glyph(marker); icon.style.color = TeamColor(marker.Team);
                icon.style.left = Length.Percent(screen.x * 100); icon.style.top = Length.Percent((1 - screen.y) * 100); icon.style.display = DisplayStyle.Flex;
            }
            for (int i = count; i < worldIcons.Count; i++) worldIcons[i].style.display = DisplayStyle.None;
        }
        private void OnDisable() { root?.Clear(); icons.Clear(); worldIcons.Clear(); revision = -1; VisibleMarkerCount = 0; }
    }
}

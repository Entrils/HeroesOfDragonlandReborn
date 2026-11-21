using System.Collections.Generic;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Player;
using HeroesOfDragonland.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace HeroesOfDragonland.UI.Minimap
{
    public sealed partial class TacticalMinimapView
    {
        private readonly List<Label> mapPings = new(), worldPings = new();
        private VisualElement pingSymbols, pingWorldLayer;
        private Label pingReticle;
        private float nextPingInput;

        private void CreatePingPresentation()
        {
            pingSymbols = new VisualElement();
            Fill(pingSymbols);
            map.Add(pingSymbols);
            pingWorldLayer = new VisualElement();
            Fill(pingWorldLayer);
            root.Insert(root.IndexOf(panel), pingWorldLayer);
            pingReticle = Icon(new List<Label>(), pingWorldLayer, 0);
            pingReticle.text = "·";
            pingReticle.style.backgroundColor = Color.clear;
            pingReticle.style.fontSize = 19;
            pingReticle.style.left = pingReticle.style.top = Length.Percent(50);
            pingReticle.style.color = Color.white;
            pingReticle.style.display = DisplayStyle.None;
        }

        private void DrawPings(bool visible)
        {
            int mapCount = 0, worldCount = 0;
            var viewer = visible ? source.PingViewer : null;
            bool showReticle = false;
            if (viewer != null && viewer.Object != null && viewer.Object.IsValid && !viewer.IsSuspended
                && NetworkMatch.AllowsGameplay(viewer.Runner))
            {
                var camera = Camera.main;
                var stats = viewer.GetComponent<NetworkCharacterStats>();
                showReticle = camera != null && camera.GetComponent<BattlefieldCamera>() is { IsCaptured: true }
                    && Application.isFocused && UnityEngine.Cursor.lockState == CursorLockMode.Locked
                    && stats != null && !stats.Dead
                    && viewer.Runner.GetComponentInParent<LocalGameplayInputGate>() is not { IsBlocked: true };
                if (showReticle) PollPingInput(viewer, camera);
                var definition = source.Definition;
                foreach (var ping in viewer.ReadTeamPings(source.PingRound))
                {
                    var color = ping.Kind == TeamPingKind.Danger || ping.Kind == TeamPingKind.Enemy ? new Color(1, .5f, .35f)
                        : ping.Kind == TeamPingKind.Retreat ? new Color(.55f, .85f, 1) : new Color(1, .83f, .38f);
                    var position = new MapPoint(ping.Position.x, ping.Position.y, ping.Position.z);
                    if (TacticalMapRules.Project(position, definition.Center.x, definition.Center.y, definition.Size.x, definition.Size.y, out var u, out var v))
                    {
                        var icon = Icon(mapPings, pingSymbols, mapCount++);
                        PositionPing(icon, Glyph(ping.Kind) + ping.Source, color, (float)u, (float)v);
                    }
                    if (camera == null) continue;
                    var screen = camera.WorldToViewportPoint(ping.Position + Vector3.up * .5f);
                    if (screen.z <= 0 || screen.x < 0 || screen.x > 1 || screen.y < 0 || screen.y > 1) continue;
                    var world = Icon(worldPings, pingWorldLayer, worldCount++);
                    PositionPing(world, ping.Kind + " · Player " + ping.Source, color, screen.x, 1 - screen.y);
                }
            }
            pingReticle.style.display = showReticle ? DisplayStyle.Flex : DisplayStyle.None;
            for (int i = mapCount; i < mapPings.Count; i++) mapPings[i].style.display = DisplayStyle.None;
            for (int i = worldCount; i < worldPings.Count; i++) worldPings[i].style.display = DisplayStyle.None;
        }

        private void PollPingInput(NetworkTestPlayer viewer, Camera camera)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || Time.unscaledTime < nextPingInput) return;
            TeamPingKind intent;
            if (keyboard.yKey.wasPressedThisFrame) intent = TeamPingKind.Danger;
            else if (keyboard.uKey.wasPressedThisFrame) intent = TeamPingKind.Retreat;
            else if (keyboard.tKey.wasPressedThisFrame) intent = TeamPingKind.Location;
            else return;
            nextPingInput = Time.unscaledTime + .2f;
            if (TeamPings.TryAim(viewer, camera, intent, out var point, out var target))
                viewer.RequestTeamPing(intent, point, target);
        }

        private static void PositionPing(Label icon, string label, Color color, float u, float v)
        {
            if (icon.text != label) icon.text = label;
            icon.style.color = color;
            icon.style.left = Length.Percent(u * 100);
            icon.style.top = Length.Percent(v * 100);
            icon.style.display = DisplayStyle.Flex;
        }

        private static string Glyph(TeamPingKind kind) => kind switch
        {
            TeamPingKind.Enemy => "E", TeamPingKind.Camp => "C", TeamPingKind.Boss => "B",
            TeamPingKind.Fortress => "F", TeamPingKind.King => "K", TeamPingKind.Loot => "L",
            TeamPingKind.Retreat => "R", TeamPingKind.Danger => "!", _ => "+"
        };
    }
}

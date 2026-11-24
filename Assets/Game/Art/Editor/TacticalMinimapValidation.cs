using System;
using System.Collections;
using System.Linq;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.UI.Minimap;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HeroesOfDragonland.Art.Editor
{
    public static class TacticalMinimapValidation
    {
        public static string Status { get; private set; } = "Not run";
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException("M62: " + message); }
        [MenuItem("Heroes of Dragonland/UI/Run Tactical Minimap Play Validation (M62)")]
        public static void Start()
        {
            Check(Application.isPlaying && !Application.isBatchMode, "Use non-headless Play Mode with an admitted living local player");
            Check(Status != "RUNNING", "Already running"); TacticalMinimapSetup.Validate(); Status = "RUNNING";
            var host = new GameObject("Temporary M62 check").AddComponent<ArtPipelineValidationHost>(); host.StartCoroutine(Run(host.gameObject));
        }
        private static IEnumerator Run(GameObject host)
        {
            try
            {
                var bootstrap = UnityEngine.Object.FindAnyObjectByType<MultiplayerBootstrap>(); Check(bootstrap != null, "Missing bootstrap");
                var source = bootstrap.GetComponent<TacticalMapSource>(); var view = bootstrap.GetComponentInChildren<TacticalMinimapView>(true);
                Check(source != null && view != null && source.isActiveAndEnabled, "Missing installed map source/view");
                source.Refresh(); yield return null;
                Check(source.Visible && source.Markers.Any(m => m.Kind == MapMarkerKind.LocalPlayer), "Start a match and use a living admitted local player");
                var local = source.Markers.Single(m => m.Kind == MapMarkerKind.LocalPlayer);
                Check(source.Markers.Count(m => m.Kind == MapMarkerKind.King) == 2, "Both published Kings must be available");
                Check(source.Markers.Count(m => m.Kind == MapMarkerKind.Fortress) == 2, "Both public fortresses required");
                Check(source.Markers.Any(m => m.Kind == MapMarkerKind.Camp) && source.Markers.Any(m => m.Kind == MapMarkerKind.BossCamp), "Public camps/boss camp missing");
                foreach (var subject in TacticalMapSubject.Active)
                {
                    if (subject == null || !subject.Valid || subject.Actor.Runner != bootstrap.Runner || subject.Player == null) continue;
                    if (TacticalMapSource.Team(subject.Player.Team) != local.Team)
                        Check(!source.Markers.Any(m => m.Id == "ally:" + subject.Actor.Id), "Enemy actor entered normal player markers");
                }
                Check(!source.Markers.Any(m => m.Kind == MapMarkerKind.RevealedEnemy), "Baseline M62 must not produce enemy reveals automatically");
                var panel = view.GetComponent<UIDocument>().rootVisualElement.Q("TacticalMinimap");
                Check(panel != null && panel.pickingMode == PickingMode.Ignore && view.VisibleMarkerCount > 0, "Passive map UI did not render markers");
                Check(view.GetComponentsInChildren<Camera>(true).Length == 0, "Minimap must not render scene actors through a camera");
                Status = "PASS loaded public markers/Kings/camps, normal enemy exclusion and passive schematic UI. Real host/client privacy, camp cycles, reveal authorization producer, reconnect and visual overlap still require manual acceptance.";
                Debug.Log("[M62] " + Status);
            }
            finally
            {
                if (Status == "RUNNING") { Status = "FAIL — inspect Console"; Debug.LogError("[M62] " + Status); }
                UnityEngine.Object.Destroy(host);
            }
        }
    }
}

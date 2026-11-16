using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.World;
using HeroesOfDragonland.UI.Minimap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace HeroesOfDragonland.Art.Editor
{
    public static class TacticalMinimapSetup
    {
        public const string DefinitionPath = "Assets/Game/Data/Minimap/BattlefieldTacticalMap.asset";
        private static bool Install(GameObject root, bool undo)
        {
            if (root.GetComponent<NetworkObject>() == null || root.GetComponent<TacticalMapSubject>() != null) return false;
            if (root.GetComponent<NetworkTestPlayer>() == null && root.GetComponent<NetworkCamp>() == null && root.GetComponent<NetworkFortressGate>() == null) return false;
            if (undo) Undo.AddComponent<TacticalMapSubject>(root); else root.AddComponent<TacticalMapSubject>(); return true;
        }
        [MenuItem("Heroes of Dragonland/UI/Build and Install Tactical Minimap (M62)")]
        public static void BuildAndInstallLoadedScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != "Assets/Scenes/DragonlandBattlefield.unity")
                throw new InvalidOperationException("Inspect/open the existing battlefield in Edit Mode through MCP first.");
            var roots = scene.GetRootGameObjects();
            T[] All<T>() where T : Component => roots.SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
            var bootstrap = All<MultiplayerBootstrap>().Single();
            var panels = All<UIDocument>(); var settings = panels.FirstOrDefault(d => d.panelSettings != null)?.panelSettings;
            if (settings == null) throw new InvalidOperationException("Install/reuse the existing UI Toolkit HUD first.");
            var map = roots.Single(r => r.name.StartsWith("Battlefield |", StringComparison.Ordinal));
            var ground = map.GetComponentsInChildren<MeshRenderer>(true).Single(r => r.name == "Ground");
            var spawnService = bootstrap.GetComponent<TeamSpawnService>();
            if (spawnService == null || spawnService.Areas.Count != 2) throw new InvalidOperationException("Expected the two existing fortress spawn areas.");
            if (!AssetDatabase.IsValidFolder("Assets/Game/Data/Minimap")) AssetDatabase.CreateFolder("Assets/Game/Data", "Minimap");
            var definition = AssetDatabase.LoadAssetAtPath<TacticalMapDefinition>(DefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<TacticalMapDefinition>();
                var landmarks = new List<TacticalMapDefinition.Landmark>();
                foreach (var area in spawnService.Areas)
                    landmarks.Add(new TacticalMapDefinition.Landmark { id = "fortress:" + area.Team, label = area.Team + " Fortress",
                        kind = MapMarkerKind.Fortress, team = TacticalMapSource.Team(area.Team), position = area.transform.position });
                var spawner = bootstrap.GetComponent<NetworkCampSpawner>();
                if (spawner == null) throw new InvalidOperationException("Missing current camp authoring.");
                var authored = new SerializedObject(spawner).FindProperty("entries");
                for (int i = 0; i < authored.arraySize; i++)
                {
                    var entry = authored.GetArrayElementAtIndex(i); var camp = entry.FindPropertyRelative("prefab").objectReferenceValue as NetworkCamp;
                    if (camp == null || camp.Definition == null) throw new InvalidOperationException("Invalid public camp anchor " + i);
                    bool boss = camp.Definition.Enemies.Any(m =>
                    {
                        var brain = m.Prefab != null ? m.Prefab.GetComponent<EnemyBrain>() : null;
                        return brain != null && brain.Definition != null && brain.Definition.Boss != null;
                    });
                    landmarks.Add(new TacticalMapDefinition.Landmark { id = "camp:" + i, label = boss ? "Boss camp" : "Neutral camp " + (i + 1),
                        kind = boss ? MapMarkerKind.BossCamp : MapMarkerKind.Camp,
                        position = spawner.transform.TransformPoint(entry.FindPropertyRelative("localPosition").vector3Value) });
                }
                var assault = bootstrap.GetComponent<FortressAssaultSpawner>();
                if (assault != null && assault.Definition != null)
                    for (int i = 0; i < assault.Definition.Gates.Count; i++)
                    {
                        var gate = assault.Definition.Gates[i];
                        if (gate.Prefab == null) continue;
                        var identity = gate.Prefab.GetComponent<FortressGate>();
                        if (identity == null) throw new InvalidOperationException("Missing authored FortressGate identity.");
                        landmarks.Add(new TacticalMapDefinition.Landmark { id = "gate:" + i, label = identity.Team + " Gate",
                            kind = MapMarkerKind.Objective, team = TacticalMapSource.Team(identity.Team), position = gate.Position });
                    }
                var data = new SerializedObject(definition);
                data.FindProperty("center").vector2Value = new Vector2(ground.bounds.center.x, ground.bounds.center.z);
                data.FindProperty("size").vector2Value = new Vector2(ground.bounds.size.x, ground.bounds.size.z);
                var list = data.FindProperty("landmarks"); list.arraySize = landmarks.Count;
                for (int i = 0; i < landmarks.Count; i++)
                {
                    var src = landmarks[i]; var item = list.GetArrayElementAtIndex(i);
                    item.FindPropertyRelative("id").stringValue = src.id; item.FindPropertyRelative("label").stringValue = src.label;
                    item.FindPropertyRelative("kind").intValue = (int)src.kind; item.FindPropertyRelative("team").intValue = (int)src.team;
                    item.FindPropertyRelative("position").vector3Value = src.position;
                }
                var geometry = map.GetComponentsInChildren<MeshRenderer>(true).Where(r => new[] { "Forest", "Rocky Highlands", "Ruined Battlefield", "Corrupted reach", "Trail junction" }.Contains(r.name)).ToArray();
                var regions = data.FindProperty("regions"); regions.arraySize = geometry.Length;
                for (int i = 0; i < geometry.Length; i++)
                {
                    var renderer = geometry[i]; var item = regions.GetArrayElementAtIndex(i);
                    item.FindPropertyRelative("label").stringValue = renderer.name;
                    item.FindPropertyRelative("center").vector2Value = new Vector2(renderer.bounds.center.x, renderer.bounds.center.z);
                    item.FindPropertyRelative("size").vector2Value = new Vector2(renderer.bounds.size.x, renderer.bounds.size.z);
                    item.FindPropertyRelative("color").colorValue = renderer.name == "Forest" ? new Color(.13f,.23f,.18f)
                        : renderer.name == "Corrupted reach" ? new Color(.23f,.16f,.25f) : renderer.name == "Trail junction" ? new Color(.36f,.32f,.23f)
                        : renderer.name == "Rocky Highlands" ? new Color(.24f,.27f,.3f) : new Color(.28f,.25f,.22f);
                }
                data.ApplyModifiedPropertiesWithoutUndo();
                if (!definition.Validate(out var error)) throw new InvalidOperationException(error);
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }
            if (!definition.Validate(out var reason)) throw new InvalidOperationException(reason);
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Game/Data" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid); var root = PrefabUtility.LoadPrefabContents(path);
                try { if (Install(root, false)) PrefabUtility.SaveAsPrefabAsset(root, path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            if (bootstrap.GetComponent<TemporaryEnemyReveals>() == null) Undo.AddComponent<TemporaryEnemyReveals>(bootstrap.gameObject);
            var source = bootstrap.GetComponent<TacticalMapSource>() ?? Undo.AddComponent<TacticalMapSource>(bootstrap.gameObject);
            Undo.RecordObject(source, "M62 map definition"); var sourceData = new SerializedObject(source);
            sourceData.FindProperty("definition").objectReferenceValue = definition; sourceData.ApplyModifiedPropertiesWithoutUndo();
            foreach (var actor in All<NetworkObject>()) Install(actor.gameObject, true);
            var view = All<TacticalMinimapView>().SingleOrDefault();
            if (view == null)
            {
                var obj = new GameObject("Tactical Minimap UI"); Undo.RegisterCreatedObjectUndo(obj, "M62 UI"); obj.transform.SetParent(bootstrap.transform, false);
                var document = Undo.AddComponent<UIDocument>(obj); document.panelSettings = settings;
                document.sortingOrder = panels.Select(d => d.sortingOrder).DefaultIfEmpty(0).Max() + 10; view = Undo.AddComponent<TacticalMinimapView>(obj);
            }
            Undo.RecordObject(view, "M62 source"); var viewData = new SerializedObject(view);
            viewData.FindProperty("source").objectReferenceValue = source; viewData.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets(); Fusion.Editor.NetworkProjectConfigUtilities.RebuildPrefabTable(); EditorSceneManager.MarkSceneDirty(scene);
            Validate(); Debug.Log("[M62] Public schematic minimap and passive hooks installed. No enemy reveal producer/network payload. Inspect layout/Console and save through MCP; Play/host-client checks required.");
        }
        [MenuItem("Heroes of Dragonland/UI/Validate Tactical Minimap (M62)")]
        public static void Validate()
        {
            var definition = AssetDatabase.LoadAssetAtPath<TacticalMapDefinition>(DefinitionPath);
            if (definition == null) throw new InvalidOperationException("Build M62 in the inspected battlefield first.");
            if (!definition.Validate(out var error)) throw new InvalidOperationException(error);
            if (definition.Landmarks.Count(l => l.kind == MapMarkerKind.Fortress) != 2
                || !definition.Landmarks.Any(l => l.kind == MapMarkerKind.BossCamp)) throw new InvalidOperationException("Expected two fortresses and at least one public boss camp.");
            Debug.Log("[M62] PASS public map definition. Actual minimap UI, authoritative state delivery and host/client privacy need Play checks.");
        }
    }
}

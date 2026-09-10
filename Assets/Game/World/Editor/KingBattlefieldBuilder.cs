using HeroesOfDragonland.Match;
using HeroesOfDragonland.Player;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Teams;
using HeroesOfDragonland.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HeroesOfDragonland.World.Editor
{
    public static class KingBattlefieldBuilder
    {
        public const string ScenePath = "Assets/Scenes/KingBattlefieldTest.unity";
        private const string DataPath = "Assets/Game/Data/KingBattlefieldTest";
        public static void Build() => FortressTestSceneBuilder.BuildBattlefield(ScenePath, DataPath, AddObjectives);

        private static void AddObjectives(Scene scene)
        {
            var profile = ScriptableObject.CreateInstance<CharacterStatsConfig>();
            var settings = new SerializedObject(profile);
            settings.FindProperty("maxHealth").floatValue = 1000;
            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(profile, DataPath + "/KingStats.asset");
            var gold = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(1f, 0.75f, 0.1f) };
            AssetDatabase.CreateAsset(gold, DataPath + "/Crown.mat");
            GameObject player = null;
            foreach (var candidate in Object.FindObjectsByType<PlayerController>())
                if (candidate.gameObject.scene == scene) player = candidate.gameObject;
            if (player == null) throw new System.InvalidOperationException("Battlefield player missing.");
            var a = CreateKing(TeamId.TeamA, new Vector3(-14, 0, 3), profile, gold, player);
            var b = CreateKing(TeamId.TeamB, new Vector3(14, 0, 3), profile, gold, player);
            var manager = new GameObject("Match").AddComponent<MatchManager>();
            var config = new SerializedObject(manager);
            config.FindProperty("teamAKing").objectReferenceValue = a;
            config.FindProperty("teamBKing").objectReferenceValue = b;
            config.ApplyModifiedPropertiesWithoutUndo();
            manager.gameObject.AddComponent<MatchStatusDisplay>();
        }

        private static KingObjective CreateKing(TeamId team, Vector3 position, CharacterStatsConfig profile, Material gold, GameObject player)
        {
            var root = new GameObject(team == TeamId.TeamA ? "Team A King" : "Team B King");
            root.transform.position = position;
            root.AddComponent<TeamIdentity>().TryConfigure(team, CombatantKind.NPC);
            var king = root.AddComponent<KingObjective>();
            var config = new SerializedObject(king); config.FindProperty("configuration").objectReferenceValue = profile;
            config.ApplyModifiedPropertiesWithoutUndo();
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule); body.name = "King Body";
            body.transform.SetParent(root.transform, false); body.transform.localPosition = Vector3.up * 1.25f;
            body.transform.localScale = new Vector3(1.3f, 1.25f, 1.3f);
            body.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(DataPath + (team == TeamId.TeamA ? "/TeamA.mat" : "/TeamB.mat"));
            var crown = GameObject.CreatePrimitive(PrimitiveType.Cube); crown.name = "Crown";
            crown.transform.SetParent(root.transform, false); crown.transform.localPosition = Vector3.up * 2.55f;
            crown.transform.localScale = new Vector3(1.2f, 0.25f, 1.2f); crown.GetComponent<Renderer>().sharedMaterial = gold;
            Object.DestroyImmediate(crown.GetComponent<Collider>());
            var label = new GameObject("King Label").AddComponent<TextMesh>(); label.transform.SetParent(root.transform, false);
            label.transform.localPosition = Vector3.up * 3.2f; label.text = root.name;
            label.anchor = TextAnchor.MiddleCenter; label.characterSize = 0.15f; label.fontSize = 40;
            var debug = root.AddComponent<KingObjectiveDebug>();
            var debugConfig = new SerializedObject(debug); debugConfig.FindProperty("attacker").objectReferenceValue = player;
            debugConfig.ApplyModifiedPropertiesWithoutUndo();
            return king;
        }
    }
}

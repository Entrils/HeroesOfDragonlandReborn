using System;
using System.IO;
using System.Linq;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.Abilities;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.EquipmentSystem;
using HeroesOfDragonland.InventorySystem;
using HeroesOfDragonland.Interaction;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Match;
using HeroesOfDragonland.Player;
using HeroesOfDragonland.Progression;
using HeroesOfDragonland.Skills;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Targeting;
using HeroesOfDragonland.Teams;
using HeroesOfDragonland.World.Camps;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace HeroesOfDragonland.World.Editor
{
    public static class TutorialSceneBuilder
    {
        private const string Data = "Assets/Game/Data/";
        private const string Folder = Data + "Tutorial/";

        [MenuItem("Dragonland/Setup/Training Grounds")]
        public static string Build()
        {
            if (Application.isPlaying || Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save open scenes and exit Play Mode before rebuilding Training Grounds.");
            if (!AssetDatabase.IsValidFolder(Folder.TrimEnd('/'))) AssetDatabase.CreateFolder(Data.TrimEnd('/'), "Tutorial");
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                Populate(scene);
                if (!EditorSceneManager.SaveScene(scene, TutorialProgress.ScenePath)) throw new IOException("Could not save Training Grounds.");
                if (!EditorBuildSettings.scenes.Any(item => item.path == TutorialProgress.ScenePath && item.enabled))
                    EditorBuildSettings.scenes = EditorBuildSettings.scenes.Where(item => item.path != TutorialProgress.ScenePath)
                        .Append(new EditorBuildSettingsScene(TutorialProgress.ScenePath, true)).ToArray();
                AssetDatabase.SaveAssets();
                return TutorialProgress.ScenePath;
            }
            finally
            {
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                if (!scene.isDirty) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void Populate(Scene scene)
        {
            var slate = Material("Stone", new Color(.31f, .40f, .43f));
            var turf = Material("Ground", new Color(.24f, .34f, .28f));
            var amber = Material("Objective", new Color(.97f, .6f, .12f));
            var blue = Material("Player", new Color(.14f, .39f, .75f));
            var red = Material("Enemy", new Color(.71f, .23f, .18f));
            var map = new GameObject("Training arena");
            Cube("Courtyard", new Vector3(0, -.25f, 13), new Vector3(24, .5f, 36), turf, map.transform);
            Cube("West wall", new Vector3(-12, 1, 13), new Vector3(.5f, 2, 36), slate, map.transform);
            Cube("East wall", new Vector3(12, 1, 13), new Vector3(.5f, 2, 36), slate, map.transform);
            Cube("Rear wall", new Vector3(0, 1, -5), new Vector3(24, 2, .5f), slate, map.transform);
            Cube("Far wall", new Vector3(0, 1, 31), new Vector3(24, 2, .5f), slate, map.transform);
            Cube("Fortress west wall", new Vector3(-7, 1.5f, 22), new Vector3(10, 3, 1), slate, map.transform);
            Cube("Fortress east wall", new Vector3(7, 1.5f, 22), new Vector3(10, 3, 1), slate, map.transform);
            var nav = map.AddComponent<NavMeshSurface>();
            nav.collectObjects = CollectObjects.Children;
            nav.BuildNavMesh();
            var baked = AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(Folder + "TrainingNavMesh.asset");
            if (baked == null) AssetDatabase.CreateAsset(nav.navMeshData, Folder + "TrainingNavMesh.asset");
            else { EditorUtility.CopySerialized(nav.navMeshData, baked); nav.navMeshData = baked; EditorUtility.SetDirty(baked); }

            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.45f;
            light.transform.rotation = Quaternion.Euler(48, -30, 0);
            var camera = new GameObject("Training camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.gameObject.AddComponent<AudioListener>();
            var follow = camera.gameObject.AddComponent<ThirdPersonCamera>();
            var avatar = new GameObject("Trainee");
            avatar.SetActive(false);
            var capsule = avatar.AddComponent<CharacterController>();
            capsule.center = Vector3.up; capsule.height = 2; capsule.radius = .36f;
            Primitive("Trainee body", PrimitiveType.Capsule, Vector3.up, new Vector3(.68f, .95f, .68f), blue, avatar.transform);
            Ref(avatar.AddComponent<CharacterStats>(), "configuration", Asset<CharacterStatsConfig>("DefaultCharacterStats.asset"));
            avatar.AddComponent<TeamIdentity>().TryConfigure(TeamId.TeamA, CombatantKind.Player);
            Ref(avatar.AddComponent<Inventory>(), "catalog", Asset<ItemCatalog>("Items/TestItemCatalog.asset"));
            avatar.AddComponent<Equipment>();
            avatar.AddComponent<CharacterAttributes>();
            Ref(avatar.AddComponent<SessionProgression>(), "definition", Asset<LevelProgressionDefinition>("Progression/DefaultLevelProgression.asset"));
            var skills = avatar.AddComponent<SessionSkills>();
            Edit(skills, data => data.FindProperty("definitions").GetArrayElementAtIndex(3).objectReferenceValue = Asset<SkillDefinition>("Abilities/FireProjectileSkill.asset"));
            avatar.AddComponent<AbilityCaster>();
            avatar.AddComponent<AbilityPresentation>();
            avatar.AddComponent<EnemyThreatTarget>();
            avatar.AddComponent<CombatDefense>();
            Ref(avatar.AddComponent<ParryDefense>(), "definition", Asset<ParryDefinition>("DefaultParry.asset"));
            avatar.AddComponent<TargetLock>();
            avatar.AddComponent<PlayerInteractor>();
            Ref(avatar.AddComponent<MeleeAttack>(), "definition", Asset<MeleeAttackDefinition>("LightAttack.asset"));
            Ref(avatar.AddComponent<PlayerDodge>(), "definition", Asset<DodgeDefinition>("DefaultDodge.asset"));
            var controller = avatar.AddComponent<PlayerController>();
            Ref(controller, "followCamera", follow);
            Ref(controller, "inputActions", Load<InputActionAsset>("Assets/InputSystem_Actions.inputactions"));
            Ref(follow, "target", avatar.transform);
            avatar.SetActive(true);

            var mark = Primitive("Move here", PrimitiveType.Cylinder, new Vector3(0, .025f, 3.1f), new Vector3(1.45f, .025f, 1.45f), amber);
            var dummy = Actor("Practice dummy", new Vector3(0, 0, 6.0f), amber, TeamId.Unassigned);
            var guard = Actor("Sparring guard", new Vector3(0, 0, 7.0f), red, TeamId.TeamB);
            guard.GetComponent<DamageReceiver>().enabled = false;
            var guardAttack = guard.gameObject.AddComponent<EnemyMeleeAttack>();
            var guardDefinition = CopyAsset<EnemyDefinition>("Enemies/Wolf.asset", "SparringGuard.asset");
            Edit(guardDefinition, data =>
            {
                data.FindProperty("attackWindup").floatValue = 1.12f;
                data.FindProperty("attackRange").floatValue = 2.8f;
                data.FindProperty("attackCooldown").floatValue = 2.5f;
                data.FindProperty("damage").floatValue = 12;
            });

            var lever = Cube("Lever", new Vector3(3, .65f, 8.5f), new Vector3(.6f, 1.3f, .6f), amber).AddComponent<TutorialLever>();
            var chest = Cube("Training chest", new Vector3(3, .43f, 9.8f), new Vector3(1.2f, .86f, .8f), amber).AddComponent<LootChest>();
            var sword = Asset<ItemDefinition>("Items/IronSword.asset");
            var loot = CopyAsset<LootTable>("TestChestLoot.asset", "TrainingChestLoot.asset");
            Edit(loot, data =>
            {
                data.FindProperty("minimumDrops").intValue = 1;
                data.FindProperty("maximumDrops").intValue = 1;
                var entries = data.FindProperty("entries"); entries.arraySize = 1;
                entries.GetArrayElementAtIndex(0).FindPropertyRelative("item").objectReferenceValue = sword;
            });
            Ref(chest, "lootTable", loot);

            var campDef = CopyAsset<CampDefinition>("Camps/TestNeutralCamp.asset", "TrainingCamp.asset");
            Edit(campDef, data =>
            {
                var enemies = data.FindProperty("enemies"); enemies.arraySize = 1;
                enemies.GetArrayElementAtIndex(0).FindPropertyRelative("count").intValue = 1;
            });
            if (!campDef.TryValidate(out var campError)) throw new InvalidOperationException(campError);
            var camp = new GameObject("Neutral camp").AddComponent<Camp>();
            camp.transform.position = new Vector3(0, 0, 15);
            Ref(camp, "definition", campDef);
            Primitive("Camp boundary", PrimitiveType.Cylinder, new Vector3(0, .03f, 0), new Vector3(3, .02f, 3), slate, camp.transform);
            var enemySpawn = Child(camp.transform, "Enemy spawn", Vector3.forward);
            Ref(camp, "chestSpawnPoint", Child(camp.transform, "Reward spawn", Vector3.right));
            Edit(camp, data =>
            {
                data.FindProperty("campId").stringValue = "training-neutral-camp";
                var points = data.FindProperty("enemySpawnPoints"); points.arraySize = 1;
                points.GetArrayElementAtIndex(0).objectReferenceValue = enemySpawn;
            });

            var gateObj = new GameObject("Enemy fortress gate");
            gateObj.transform.position = new Vector3(0, 0, 22);
            var rootBox = gateObj.AddComponent<BoxCollider>(); rootBox.center = new Vector3(0, 1.25f); rootBox.size = new Vector3(4, 2.5f, 1);
            var body = Primitive("Gate body", PrimitiveType.Cube, rootBox.center, rootBox.size, red, gateObj.transform);
            Ref(gateObj.AddComponent<CharacterStats>(), "configuration", Asset<CharacterStatsConfig>("DefaultCharacterStats.asset"));
            var gate = gateObj.AddComponent<FortressGate>();
            Ref(gate, "gateBody", body.transform);
            Edit(gate, data => data.FindProperty("team").intValue = (int)TeamId.TeamB);
            gateObj.GetComponent<DamageReceiver>().enabled = false;
            gateObj.AddComponent<Targetable>().enabled = false;

            var friendly = King("Your King", new Vector3(-9, 0, 1), blue, TeamId.TeamA);
            var enemy = King("Enemy King", new Vector3(0, 0, 25), red, TeamId.TeamB);
            var match = new GameObject("Offline match objective").AddComponent<MatchManager>();
            Edit(match, data => data.FindProperty("startAutomatically").boolValue = false);
            Ref(match, "teamAKing", friendly); Ref(match, "teamBKing", enemy);

            foreach (var inactive in new[] { dummy.gameObject, guard.gameObject, lever.gameObject, chest.gameObject,
                         camp.gameObject, enemy.gameObject }) inactive.SetActive(false);
            var director = new GameObject("Tutorial").AddComponent<TutorialFlow>();
            Ref(director, "player", controller); Ref(director, "playerCamera", camera);
            Ref(director, "movementMark", mark.transform); Ref(director, "trainingTarget", dummy);
            Ref(director, "practiceGuard", guardAttack); Ref(director, "guardDefinition", guardDefinition);
            Ref(director, "lever", lever); Ref(director, "chest", chest); Ref(director, "trainingWeapon", sword);
            Ref(director, "camp", camp); Ref(director, "gate", gate); Ref(director, "enemyKing", enemy); Ref(director, "match", match);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static CharacterStats Actor(string label, Vector3 position, Material color, TeamId team)
        {
            var actor = new GameObject(label);
            actor.transform.position = position;
            var collider = actor.AddComponent<CapsuleCollider>(); collider.center = Vector3.up; collider.height = 2; collider.radius = .45f;
            Primitive("Body", PrimitiveType.Capsule, Vector3.up, new Vector3(.9f, 1, .9f), color, actor.transform);
            var stats = actor.AddComponent<CharacterStats>(); Ref(stats, "configuration", Asset<CharacterStatsConfig>("DefaultCharacterStats.asset"));
            actor.AddComponent<DamageReceiver>();
            actor.AddComponent<TeamIdentity>().TryConfigure(team, CombatantKind.NPC);
            actor.AddComponent<Targetable>();
            return stats;
        }

        private static KingObjective King(string label, Vector3 point, Material material, TeamId team)
        {
            var root = Actor(label, point, material, team);
            Primitive("Crown", PrimitiveType.Cylinder, new Vector3(0, 2.03f, 0), new Vector3(.7f, .09f, .7f), Material("Objective", new Color(.97f, .6f, .12f)), root.transform);
            var king = root.gameObject.AddComponent<KingObjective>();
            Ref(king, "configuration", Asset<CharacterStatsConfig>("DefaultCharacterStats.asset"));
            return king;
        }

        private static Transform Child(Transform parent, string label, Vector3 position)
        {
            var child = new GameObject(label).transform; child.SetParent(parent, false); child.localPosition = position; return child;
        }
        private static GameObject Cube(string label, Vector3 position, Vector3 scale, Material material, Transform parent = null)
            => Primitive(label, PrimitiveType.Cube, position, scale, material, parent, true);
        private static GameObject Primitive(string label, PrimitiveType kind, Vector3 position, Vector3 scale, Material material, Transform parent = null, bool solid = false)
        {
            var obj = GameObject.CreatePrimitive(kind); obj.name = label; obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position; obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
            return obj;
        }
        private static Material Material(string label, Color color)
        {
            string path = Folder + label + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color }; AssetDatabase.CreateAsset(material, path);
            return material;
        }
        private static T CopyAsset<T>(string source, string name) where T : UnityEngine.Object
        {
            string path = Folder + name;
            if (AssetDatabase.LoadAssetAtPath<T>(path) == null && !AssetDatabase.CopyAsset(Data + source, path)) throw new IOException(path);
            return Load<T>(path);
        }
        private static T Asset<T>(string path) where T : UnityEngine.Object => Load<T>(Data + path);
        private static T Load<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing tutorial asset " + path);
        private static void Ref(UnityEngine.Object target, string property, UnityEngine.Object value) =>
            Edit(target, data => data.FindProperty(property).objectReferenceValue = value);
        private static void Edit(UnityEngine.Object target, Action<SerializedObject> update)
        {
            var data = new SerializedObject(target); update(data); data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

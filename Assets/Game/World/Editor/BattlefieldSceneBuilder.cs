using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Teams;
using HeroesOfDragonland.World.Camps;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HeroesOfDragonland.World.Editor
{
    // Authoring only: invoke through Unity MCP. Runtime gameplay remains in existing network systems.
    public static class BattlefieldSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/DragonlandBattlefield.unity";
        public const string Folder = "Assets/Game/Data/Battlefield";
        public const string Materials = "Assets/Art/Materials/Battlefield";
        private static readonly Vector3[] campPositions = {
            new Vector3(-25,0,18), new Vector3(-25,0,-18), new Vector3(25,0,-18), new Vector3(25,0,18),
            new Vector3(-12,0,25), new Vector3(-12,0,-25), new Vector3(12,0,-25), new Vector3(12,0,25), Vector3.zero
        };
        private static Material ground, forest, rockFloor, corruption, ruins, path, stone, foliage, trunk, darkRock, gold, blue, red;

        [MenuItem("Dragonland/Build First Battlefield")]
        public static void Build()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Use Edit Mode.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null || AssetDatabase.IsValidFolder(Folder))
                throw new InvalidOperationException("Battlefield already exists. Inspect it instead of replacing authored content.");
            AssetDatabase.CreateFolder("Assets/Game/Data", "Battlefield");
            AssetDatabase.CreateFolder("Assets/Art/Materials", "Battlefield");
            MakeMaterials();
            var player = CopyPlayer(); var match = CopyMatch();
            var camps = new[] { CreateCamp("Low", 0), CreateCamp("Medium", 1), CreateCamp("High", 2) };
            var spawnRules = ScriptableObject.CreateInstance<SpawnZoneRules>();
            Set(spawnRules, "protectedZone", true); Set(spawnRules, "enemyPlayerEntry", (int)EnemySpawnZoneEntry.BlockEntry);
            AssetDatabase.CreateAsset(spawnRules, Folder + "/SpawnRules.asset");
            if (!AssetDatabase.CopyAsset(MultiplayerFortressSceneBuilder.ScenePath, ScenePath)) throw new InvalidOperationException("Could not copy fortress scene.");
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive); SceneManager.SetActiveScene(scene);
            try
            {
                var bootstrap = scene.GetRootGameObjects().Select(root => root.GetComponent<MultiplayerBootstrap>()).First(value => value != null);
                // Remove copied test geometry and spawn points only in this NEW scene. Source assets stay intact.
                foreach (var root in scene.GetRootGameObjects())
                    if (root.GetComponent<NavMeshSurface>() != null || root.GetComponent<TeamSpawnArea>() != null) Object.DestroyImmediate(root);
                Set(bootstrap, "playerPrefab", player);
                Set(bootstrap.GetComponent<NetworkFortressSpawner>(), "matchPrefab", match);
                Set(bootstrap.GetComponent<MultiplayerTestPanel>(), "room", "dragonland-battlefield");
                bootstrap.gameObject.AddComponent<BattlefieldAcceptanceProbe>();
                var map = new GameObject("Battlefield | 88 x 88 | primitive blockout");
                Floor(map.transform, "Ground", Vector3.zero, new Vector2(88,88), ground, true);
                Biomes(map.transform); Routes(map.transform); Fortresses(map.transform); Cover(map.transform); Boundaries(map.transform);
                var a = SpawnArea(map.transform, TeamId.TeamA, spawnRules);
                var b = SpawnArea(map.transform, TeamId.TeamB, spawnRules);
                var spawnData = new SerializedObject(bootstrap.GetComponent<TeamSpawnService>());
                var areas = spawnData.FindProperty("areas"); areas.arraySize = 2;
                areas.GetArrayElementAtIndex(0).objectReferenceValue = a; areas.GetArrayElementAtIndex(1).objectReferenceValue = b; spawnData.ApplyModifiedPropertiesWithoutUndo();
                var campData = new SerializedObject(bootstrap.GetComponent<NetworkCampSpawner>());
                var entries = campData.FindProperty("entries"); entries.arraySize = campPositions.Length;
                for (int i = 0; i < campPositions.Length; i++)
                {
                    int tier = i < 4 ? 0 : i < 8 ? 1 : 2;
                    var entry = entries.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("prefab").objectReferenceValue = camps[tier];
                    entry.FindPropertyRelative("localPosition").vector3Value = campPositions[i];
                    entry.FindPropertyRelative("yaw").floatValue = i == 2 || i == 3 || i == 6 || i == 7 ? 180 : 0;
                    CampMarker(map.transform, i, tier);
                }
                campData.ApplyModifiedPropertiesWithoutUndo();
                var camera = scene.GetRootGameObjects().Select(root => root.GetComponent<Camera>()).First(value => value != null);
                camera.orthographic = true; camera.orthographicSize = 50; camera.farClipPlane = 250;
                camera.transform.SetPositionAndRotation(new Vector3(0,82,-56), Quaternion.Euler(55.65f,0,0));
                Set(camera.gameObject.AddComponent<BattlefieldCamera>(), "bootstrap", bootstrap);
                var surface = map.AddComponent<NavMeshSurface>(); surface.collectObjects = CollectObjects.Children;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.BuildNavMesh();
                if (surface.navMeshData == null) throw new InvalidOperationException("Battlefield navigation bake failed.");
                AssetDatabase.CreateAsset(surface.navMeshData, Folder + "/BattlefieldNavMesh.asset");
                Physics.SyncTransforms(); ValidateLoaded(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save battlefield.");
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.CloseScene(scene, true); if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); }
            Fusion.Editor.NetworkProjectConfigUtilities.RebuildPrefabTable();
            foreach (var guid in AssetDatabase.FindAssets("t:NetworkProjectConfigAsset")) AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            Debug.Log("[Battlefield] Saved 88m square 3v3 map, two fortresses, four biomes, nine live camps, navigation and UI.");
        }

        private static void MakeMaterials()
        {
            ground=Mat("Ground",new Color(.24f,.29f,.25f)); forest=Mat("Forest floor",new Color(.19f,.29f,.20f));
            rockFloor=Mat("Highlands floor",new Color(.35f,.36f,.34f)); corruption=Mat("Corrupted floor",new Color(.22f,.16f,.29f));
            ruins=Mat("Ruined battlefield floor",new Color(.38f,.34f,.27f)); path=Mat("Trail",new Color(.49f,.43f,.31f));
            stone=Mat("Placeholder stone",new Color(.40f,.44f,.45f)); foliage=Mat("Placeholder foliage",new Color(.15f,.36f,.23f));
            trunk=Mat("Placeholder wood",new Color(.25f,.18f,.12f)); darkRock=Mat("Placeholder corruption",new Color(.35f,.20f,.46f));
            gold=Mat("Contested objective",new Color(.92f,.68f,.18f)); blue=Mat("Team A",new Color(.15f,.48f,.80f)); red=Mat("Team B",new Color(.82f,.27f,.20f));
        }
        private static Material Mat(string name, Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name=name, color=color };
            material.SetFloat("_Smoothness", .05f); AssetDatabase.CreateAsset(material, Materials+"/"+name+".mat"); return material;
        }
        private static GameObject Group(Transform parent, string name)
        { var go=new GameObject(name); go.transform.SetParent(parent,false); return go; }
        private static GameObject Shape(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material, bool solid=true)
        {
            var go=GameObject.CreatePrimitive(type); go.name=name; go.transform.SetParent(parent,false);
            go.transform.localPosition=position; go.transform.localScale=scale; go.GetComponent<Renderer>().sharedMaterial=material;
            if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>());
            else { var modifier=go.AddComponent<NavMeshModifier>(); modifier.overrideArea=true; modifier.area=NavMesh.GetAreaFromName("Not Walkable"); }
            return go;
        }
        private static void Floor(Transform parent, string name, Vector3 center, Vector2 size, Material material, bool solid=false)
        {
            var go=Shape(parent,name,PrimitiveType.Cube,center+Vector3.up*(solid ? -.25f : .012f),new Vector3(size.x,solid ? .5f : .012f,size.y),material,solid);
            go.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            if (solid) Object.DestroyImmediate(go.GetComponent<NavMeshModifier>());
        }
        private static void Label(Transform parent, string text, Vector3 position, float size, Color color)
        {
            var label=Group(parent,text).AddComponent<TextMesh>(); label.transform.localPosition=position;
            label.transform.rotation=Quaternion.Euler(90,0,0); label.anchor=TextAnchor.MiddleCenter;
            label.alignment=TextAlignment.Center; label.fontSize=64; label.characterSize=size; label.color=color; label.text=text;
        }
        private static void Biomes(Transform parent)
        {
            var regions=Group(parent,"Biomes | rotationally matched terrain").transform;
            foreach (int sign in new[]{1,-1})
            {
                Floor(regions,"Forest",new Vector3(-23*sign,0,25*sign),new Vector2(38,34),forest);
                Floor(regions,"Rocky Highlands",new Vector3(23*sign,0,25*sign),new Vector2(38,34),rockFloor);
                Floor(regions,"Corrupted reach",new Vector3(0,.012f,30*sign),new Vector2(12,24),corruption);
                Label(regions,"FOREST",new Vector3(-28*sign,.065f,38*sign),.22f,Color.white);
                Label(regions,"ROCKY HIGHLANDS",new Vector3(27*sign,.065f,38*sign),.18f,Color.white);
                Label(regions,"CORRUPTED REACH",new Vector3(0,.07f,39*sign),.14f,new Color(.8f,.65f,.95f));
            }
            Floor(regions,"Ruined Battlefield",Vector3.up*.025f,new Vector2(47,20),ruins);
            Label(regions,"RUINED BATTLEFIELD",new Vector3(0,.08f,9),.19f,Color.white);
        }
        private static void Trail(Transform parent, params Vector2[] points)
        {
            for (int i=1;i<points.Length;i++)
            {
                Vector2 delta=points[i]-points[i-1], center=(points[i]+points[i-1])*.5f;
                var strip=Shape(parent,"Trail junction",PrimitiveType.Cube,new Vector3(center.x,.045f,center.y),new Vector3(3.2f,.012f,delta.magnitude+1.5f),path,false);
                strip.transform.localRotation=Quaternion.Euler(0,Mathf.Atan2(delta.x,delta.y)*Mathf.Rad2Deg,0);
                strip.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            }
        }
        private static void Routes(Transform parent)
        {
            var roads=Group(parent,"Routes | open network with cross connections").transform;
            Trail(roads,new Vector2(-36,0),new Vector2(-18,0),Vector2.zero,new Vector2(18,0),new Vector2(36,0));
            foreach (int sign in new[]{1,-1})
            {
                Trail(roads,new Vector2(-30,12*sign),new Vector2(-24,25*sign),new Vector2(-12,31*sign),new Vector2(0,34*sign),new Vector2(12,31*sign),new Vector2(24,25*sign),new Vector2(30,12*sign));
                Trail(roads,new Vector2(-24,0),new Vector2(-14,12*sign),new Vector2(0,16*sign),new Vector2(14,12*sign),new Vector2(24,0));
                Trail(roads,new Vector2(-25,18*sign),new Vector2(-14,12*sign),new Vector2(-12,25*sign));
                Trail(roads,new Vector2(25,18*sign),new Vector2(14,12*sign),new Vector2(12,25*sign));
                Trail(roads,new Vector2(-8,0),new Vector2(0,16*sign),new Vector2(0,34*sign));
                Trail(roads,new Vector2(8,0),new Vector2(0,16*sign));
            }
        }
        private static void Fortresses(Transform parent)
        {
            foreach (int sign in new[]{-1,1})
            {
                var fortress=Group(parent,sign<0?"Team A Fortress":"Team B Fortress").transform;
                fortress.localPosition=new Vector3(33*sign,0,0); fortress.localRotation=Quaternion.Euler(0,sign<0?0:180,0);
                Material team=sign<0?blue:red;
                Floor(fortress,"Courtyard",Vector3.zero,new Vector2(18,24),stone);
                Shape(fortress,"Rear wall",PrimitiveType.Cube,new Vector3(-9,1.75f,0),new Vector3(1,3.5f,24),stone);
                foreach (int side in new[]{-1,1})
                {
                    Shape(fortress,"Front wall beside main gate",PrimitiveType.Cube,new Vector3(9,1.75f,7.5f*side),new Vector3(1,3.5f,9),stone);
                    Shape(fortress,"Sally port rear wall",PrimitiveType.Cube,new Vector3(-4.5f,1.75f,12*side),new Vector3(9,3.5f,1),stone);
                    Shape(fortress,"Sally port front wall",PrimitiveType.Cube,new Vector3(7.5f,1.75f,12*side),new Vector3(3,3.5f,1),stone);
                    foreach (int x in new[]{-1,1})
                    {
                        Shape(fortress,"Corner tower",PrimitiveType.Cylinder,new Vector3(8.5f*x,2.5f,11.5f*side),new Vector3(3,2.5f,3),stone);
                        Shape(fortress,"Team tower cap",PrimitiveType.Cylinder,new Vector3(8.5f*x,5.15f,11.5f*side),new Vector3(3.2f,.25f,3.2f),team,false);
                    }
                }
                Shape(fortress,"Open gate lintel",PrimitiveType.Cube,new Vector3(9,4.5f,0),new Vector3(1.5f,1,6),team,false);
                Label(fortress,sign<0?"TEAM A FORTRESS":"TEAM B FORTRESS",new Vector3(-1,.09f,8),.16f,Color.white);
                Label(fortress,"SPAWN",new Vector3(-3,.1f,-8),.14f,team.color);
            }
        }
        private static void Cover(Transform parent)
        {
            var obstacles=Group(parent,"Cover | paired ambush pockets and sightline breaks").transform;
            foreach (int sign in new[]{1,-1})
            {
                var pair=Group(obstacles,sign>0?"Northwest half":"Southeast half").transform;
                pair.localRotation=Quaternion.Euler(0,sign>0?0:180,0);
                foreach (var p in new[]{new Vector2(-34,22),new Vector2(-30,29),new Vector2(-23,33),new Vector2(-18,20),new Vector2(-37,33),new Vector2(-18,35)})
                {
                    for (int j=0;j<3;j++)
                    {
                        Vector3 offset=new Vector3(p.x+(j-1)*2.4f,0,p.y+(j==1?1.4f:0));
                        Shape(pair,"Tree trunk | collision cover",PrimitiveType.Cylinder,offset+Vector3.up*1.8f,new Vector3(.85f,1.8f,.85f),trunk);
                        Shape(pair,"Tree canopy placeholder",PrimitiveType.Sphere,offset+Vector3.up*4,new Vector3(3.4f,3,3.4f),foliage,false);
                    }
                }
                foreach (var p in new[]{new Vector2(32,22),new Vector2(31,31),new Vector2(20,34),new Vector2(19,19),new Vector2(37,35),new Vector2(6,23)})
                {
                    var outcrop=Shape(pair,"Rocky outcrop | non-traversable high ground",PrimitiveType.Cube,new Vector3(p.x,2.2f,p.y),new Vector3(4,4.4f,3),stone);
                    outcrop.transform.localRotation=Quaternion.Euler(0,22,0);
                    Shape(pair,"Rock cap",PrimitiveType.Sphere,new Vector3(p.x,4,p.y),new Vector3(4,1.8f,3),stone,false);
                }
                // Short broken walls open onto more than one approach; no sealed farming room.
                foreach (var p in new[]{new Vector2(-17,6),new Vector2(-7,7),new Vector2(10,7),new Vector2(-18,-7)})
                {
                    Shape(pair,"Broken ruin wall",PrimitiveType.Cube,new Vector3(p.x,1.4f,p.y),new Vector3(5,2.8f,1),stone);
                    Shape(pair,"Ruin pillar",PrimitiveType.Cube,new Vector3(p.x-2,2,p.y),new Vector3(1.2f,4,1.2f),stone);
                }
                foreach (var p in new[]{new Vector2(-4,28),new Vector2(4,36),new Vector2(-4,18)})
                {
                    var shard=Shape(pair,"Corrupted stone placeholder",PrimitiveType.Cube,new Vector3(p.x,2,p.y),new Vector3(1.8f,4,1.8f),darkRock);
                    shard.transform.localRotation=Quaternion.Euler(0,35,0);
                }
                Shape(pair,"Central ring flank cover",PrimitiveType.Cube,new Vector3(-6,1.5f,-3),new Vector3(1.2f,3,3.5f),stone);
            }
        }
        private static void Boundaries(Transform parent)
        {
            var edge=Group(parent,"Map boundary").transform;
            for(int sign=-1;sign<=1;sign+=2)
            {
                Shape(edge,"Boundary",PrimitiveType.Cube,new Vector3(0,2,44*sign),new Vector3(89,4,1),stone);
                Shape(edge,"Boundary",PrimitiveType.Cube,new Vector3(44*sign,2,0),new Vector3(1,4,89),stone);
            }
        }
        private static TeamSpawnArea SpawnArea(Transform parent, TeamId team, SpawnZoneRules rules)
        {
            var root=Group(parent,team+" protected spawn"); root.transform.localPosition=team==TeamId.TeamA?new Vector3(-36,1,-5):new Vector3(36,1,5);
            root.transform.localRotation=Quaternion.Euler(0,team==TeamId.TeamA?0:180,0);
            var area=root.AddComponent<TeamSpawnArea>(); var data=new SerializedObject(area);
            data.FindProperty("team").intValue=(int)team; data.FindProperty("size").vector2Value=new Vector2(6,6); data.FindProperty("rules").objectReferenceValue=rules;
            var points=data.FindProperty("points"); points.arraySize=6;
            for(int i=0;i<6;i++)
            {
                var point=Group(root.transform,"Spawn "+(i+1)).transform; point.localPosition=new Vector3((i/3-.5f)*2,0,(i%3-1)*1.6f);
                point.localRotation=Quaternion.LookRotation(Vector3.right); points.GetArrayElementAtIndex(i).objectReferenceValue=point;
            }
            data.ApplyModifiedPropertiesWithoutUndo(); return area;
        }
        private static void CampMarker(Transform parent, int index, int tier)
        {
            var marker=Group(parent,"Camp "+(index+1)+" | "+new[]{"LOW","MEDIUM","HIGH"}[tier]); marker.transform.localPosition=campPositions[index];
            Material material=tier==2?gold:tier==1?darkRock:path;
            float radius=tier==2?5:3.5f;
            for(int i=0;i<20;i++)
            {
                float angle=i*Mathf.PI*2/20;
                Shape(marker.transform,"Camp perimeter marker",PrimitiveType.Cube,new Vector3(Mathf.Cos(angle)*radius,.08f,Mathf.Sin(angle)*radius),new Vector3(.45f,.16f,.45f),material,false);
            }
            Label(marker.transform,new[]{"LOW / 40 XP","MEDIUM / 90 XP","CONTESTED / 240 XP"}[tier],new Vector3(0,.10f,-radius-1),tier==2?.18f:.15f,Color.white);
        }
        private static NetworkObject CopyPlayer()
        {
            string target=Folder+"/BattlefieldPlayer.prefab";
            if (!AssetDatabase.CopyAsset(MultiplayerFortressSceneBuilder.Folder+"/NetworkFortressPlayer.prefab",target)) throw new InvalidOperationException("Cannot copy player.");
            var root=PrefabUtility.LoadPrefabContents(target);
            try { Set(root.GetComponent<NetworkTestPlayer>(),"arenaHalfExtent",43f); PrefabUtility.SaveAsPrefabAsset(root,target); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return AssetDatabase.LoadAssetAtPath<NetworkObject>(target);
        }
        private static NetworkMatch CopyMatch()
        {
            var lifecycle=Object.Instantiate(AssetDatabase.LoadAssetAtPath<MatchLifecycleConfig>(MultiplayerFortressSceneBuilder.Folder+"/MatchLifecycle.asset"));
            if(lifecycle==null) throw new InvalidOperationException("Missing match lifecycle.");
            Set(lifecycle,"minimumPlayers",6); AssetDatabase.CreateAsset(lifecycle,Folder+"/BattlefieldLifecycle.asset");
            string target=Folder+"/BattlefieldMatch.prefab";
            if(!AssetDatabase.CopyAsset(MultiplayerFortressSceneBuilder.Folder+"/NetworkMatch.prefab",target)) throw new InvalidOperationException("Cannot copy match.");
            var root=PrefabUtility.LoadPrefabContents(target);
            try
            {
                var match=root.GetComponent<NetworkMatch>(); Set(match,"lifecycle",lifecycle);
                Set(match,"teamAKingPosition",new Vector3(-36,0,0)); Set(match,"teamBKingPosition",new Vector3(36,0,0));
                Set(match,"teamAGuardPosition",new Vector3(-27,0,0)); Set(match,"teamBGuardPosition",new Vector3(27,0,0));
                PrefabUtility.SaveAsPrefabAsset(root,target);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return AssetDatabase.LoadAssetAtPath<NetworkMatch>(target);
        }
        private static NetworkCamp CreateCamp(string name, int tier)
        {
            var table=Object.Instantiate(AssetDatabase.LoadAssetAtPath<LootTable>("Assets/Game/Data/TestChestLoot.asset"));
            Set(table,"minimumDrops",tier==0?1:tier==1?3:5); Set(table,"maximumDrops",tier==0?2:tier==1?4:6);
            // Reuse valid catalog entries; central farming produces more actual loot, not only a decorative marker.
            var tableData=new SerializedObject(table); var weights=tableData.FindProperty("rarityWeights");
            float[][] rarity={new[]{1f,.5f,.1f,0f,0f},new[]{.5f,1f,1f,.3f,.05f},new[]{.1f,.5f,1.5f,2f,1f}};
            for(int i=0;i<5;i++) weights.GetArrayElementAtIndex(i).floatValue=rarity[tier][i]; tableData.ApplyModifiedPropertiesWithoutUndo();
            if(!table.TryValidate(null,out var reason)) throw new InvalidOperationException(reason);
            AssetDatabase.CreateAsset(table,Folder+"/"+name+"Loot.asset");
            string chestPath=Folder+"/"+name+"Chest.prefab";
            AssetDatabase.CopyAsset(MultiplayerCampSceneBuilder.Folder+"/NetworkCampChest.prefab",chestPath);
            var chest=PrefabUtility.LoadPrefabContents(chestPath);
            try { Set(chest.GetComponent<LootChest>(),"lootTable",table); PrefabUtility.SaveAsPrefabAsset(chest,chestPath); }
            finally { PrefabUtility.UnloadPrefabContents(chest); }
            string[] enemyNames=tier==0?new[]{"Wolf","Wolf"}:tier==1?new[]{"Bear","Wolf"}:new[]{"Knight","Knight","Bear"};
            var enemies=new List<NetworkEnemy>();
            foreach(string enemyName in enemyNames.Distinct())
            {
                string source=MultiplayerPveSceneBuilder.Folder+"/Network"+enemyName+".prefab";
                var original=AssetDatabase.LoadAssetAtPath<NetworkEnemy>(source);
                var definition=Object.Instantiate(original.GetComponent<EnemyBrain>().Definition); definition.name=name+enemyName;
                var stats=Object.Instantiate(definition.Stats);
                Set(stats,"maxHealth",tier==0?40f:tier==1?80f:120f); AssetDatabase.CreateAsset(stats,Folder+"/"+name+enemyName+"Stats.asset");
                Set(definition,"stats",stats); Set(definition,"detectionRadius",tier==0?4.5f:tier==1?5.5f:7f);
                Set(definition,"leashDistance",tier==0?7f:tier==1?8f:10f); Set(definition,"patrolRadius",1f); Set(definition,"damage",tier==0?6f:tier==1?10f:14f);
                AssetDatabase.CreateAsset(definition,Folder+"/"+name+enemyName+".asset");
                string enemyPath=Folder+"/"+name+enemyName+".prefab"; AssetDatabase.CopyAsset(source,enemyPath);
                var enemy=PrefabUtility.LoadPrefabContents(enemyPath);
                try { Set(enemy.GetComponent<EnemyBrain>(),"definition",definition); Set(enemy.GetComponent<CharacterStats>(),"configuration",stats); PrefabUtility.SaveAsPrefabAsset(enemy,enemyPath); }
                finally { PrefabUtility.UnloadPrefabContents(enemy); }
                enemies.Add(AssetDatabase.LoadAssetAtPath<NetworkEnemy>(enemyPath));
            }
            var campDefinition=ScriptableObject.CreateInstance<NetworkCampDefinition>(); var data=new SerializedObject(campDefinition);
            data.FindProperty("experienceReward").intValue=tier==0?40:tier==1?90:240; data.FindProperty("experienceRadius").floatValue=12;
            data.FindProperty("chestPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<NetworkLootChest>(chestPath);
            data.FindProperty("chestOffset").vector3Value=new Vector3(0,0,-1.5f);
            var members=data.FindProperty("enemies"); members.arraySize=enemyNames.Length;
            for(int i=0;i<enemyNames.Length;i++)
            {
                var entry=members.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("prefab").objectReferenceValue=enemies.First(enemy=>enemy.GetComponent<EnemyBrain>().Definition.name==name+enemyNames[i]);
                entry.FindPropertyRelative("offset").vector3Value=new Vector3((i-(enemyNames.Length-1)*.5f)*2,0,1.5f);
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            if(!campDefinition.TryValidate(out reason)) throw new InvalidOperationException(reason);
            AssetDatabase.CreateAsset(campDefinition,Folder+"/"+name+"Camp.asset");
            string campPath=Folder+"/"+name+"Camp.prefab"; AssetDatabase.CopyAsset(MultiplayerCampSceneBuilder.Folder+"/NetworkNeutralCamp.prefab",campPath);
            var camp=PrefabUtility.LoadPrefabContents(campPath);
            try
            {
                Set(camp.GetComponent<NetworkCamp>(),"definition",campDefinition);
                var label=camp.GetComponentInChildren<TextMesh>(); label.transform.localPosition=new Vector3(0,3,0);
                PrefabUtility.SaveAsPrefabAsset(camp,campPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(camp); }
            return AssetDatabase.LoadAssetAtPath<NetworkCamp>(campPath);
        }
        private static void Set(Object target, string property, object value)
        {
            var data=new SerializedObject(target); var field=data.FindProperty(property);
            if(field==null) throw new InvalidOperationException("Missing field "+property+" on "+target);
            if(value is Object obj) field.objectReferenceValue=obj;
            else if(value is Vector3 vector) field.vector3Value=vector;
            else if(value is float number) field.floatValue=number;
            else if(value is int integer) field.intValue=integer;
            else if(value is bool boolean) field.boolValue=boolean;
            else if(value is string text) field.stringValue=text;
            else throw new InvalidOperationException("Unsupported editor value.");
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void ValidateLoaded(Scene scene)
        {
            var bootstrap=scene.GetRootGameObjects().Select(root=>root.GetComponent<MultiplayerBootstrap>()).First(value=>value!=null);
            var areas=bootstrap.GetComponent<TeamSpawnService>().Areas;
            if(areas.Count!=2 || areas.Any(area=>area==null || !area.IsProtected || area.PointCount<6)) throw new InvalidOperationException("Two protected spawn areas required.");
            Physics.SyncTransforms();
            foreach(var area in areas)
                for(int i=0;i<area.PointCount;i++)
                {
                    if(!area.TryGetPoint(i,out var point,out _)) throw new InvalidOperationException("Invalid spawn point.");
                    if(Physics.CheckCapsule(point+Vector3.up*.5f,point-Vector3.up*.5f,.48f,~0,QueryTriggerInteraction.Ignore)) throw new InvalidOperationException("Blocked spawn: "+point);
                }
            if (scene.GetRootGameObjects().Any(root => root.transform.Find("Balance pass | rotation barriers") != null))
            {
                BattlefieldBalancePass.Validate();
                return;
            }
            foreach(var position in campPositions)
            {
                foreach(var offset in new[]{new Vector3(-2,0,1.5f),new Vector3(0,0,1.5f),new Vector3(2,0,1.5f),new Vector3(0,0,-1.5f)})
                    if(!NavMesh.SamplePosition(position+offset,out var hit,.8f,NavMesh.AllAreas) || Vector3.Distance(hit.position,position+offset)>.8f)
                        throw new InvalidOperationException("Camp anchor off NavMesh: "+(position+offset));
            }
            float direct=PathLength(new Vector3(-36,0,0),Vector3.zero);
            if(direct>50) throw new InvalidOperationException("Excessive travel to center: "+direct);
            foreach(int sign in new[]{1,-1})
            {
                Vector3[] route={new Vector3(-36,0,0),new Vector3(-30,0,15*sign),new Vector3(-12,0,31*sign),new Vector3(0,0,34*sign),new Vector3(12,0,31*sign),new Vector3(30,0,15*sign),new Vector3(36,0,0)};
                for(int i=1;i<route.Length;i++) PathLength(route[i-1],route[i]);
                PathLength(new Vector3(-24,0,0),new Vector3(0,0,16*sign)); PathLength(new Vector3(0,0,16*sign),new Vector3(24,0,0));
            }
            foreach(var position in campPositions)
            {
                float a=PathLength(new Vector3(-36,0,0),position), b=PathLength(new Vector3(36,0,0),-position);
                if(Mathf.Abs(a-b)>2) throw new InvalidOperationException("Unbalanced paired camp access: "+position+" "+a+" / "+b);
            }
            Debug.Log("[BattlefieldValidation] PASS clear spawns, camp anchors, center/flank connections, paired access; base-to-center="+direct.ToString("0.0")+"m / "+(direct/5).ToString("0.0")+"s at walking speed.");
        }
        public static float PathLength(Vector3 from, Vector3 to)
        {
            var route=new NavMeshPath();
            if(!NavMesh.SamplePosition(from,out var start,2,NavMesh.AllAreas) || !NavMesh.SamplePosition(to,out var end,2,NavMesh.AllAreas)
                || !NavMesh.CalculatePath(start.position,end.position,NavMesh.AllAreas,route) || route.status!=NavMeshPathStatus.PathComplete)
                throw new InvalidOperationException("Disconnected route: "+from+" -> "+to);
            float length=0; for(int i=1;i<route.corners.Length;i++) length+=Vector3.Distance(route.corners[i-1],route.corners[i]); return length;
        }
        public static void ValidateSaved()
        {
            var previous=SceneManager.GetActiveScene(); var scene=SceneManager.GetSceneByPath(ScenePath); bool opened=!scene.isLoaded;
            if(opened) scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
            try { SceneManager.SetActiveScene(scene); ValidateLoaded(scene); }
            finally { if(opened) EditorSceneManager.CloseScene(scene,true); if(previous.IsValid()&&previous.isLoaded) SceneManager.SetActiveScene(previous); }
        }
        public static void CaptureOverview()
        {
            var previous=SceneManager.GetActiveScene(); var scene=SceneManager.GetSceneByPath(ScenePath); bool opened=!scene.isLoaded;
            if(opened) scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var camera=scene.GetRootGameObjects().Select(root=>root.GetComponent<Camera>()).First(value=>value!=null);
                var oldPosition=camera.transform.position; var oldRotation=camera.transform.rotation; float oldSize=camera.orthographicSize;
                camera.transform.SetPositionAndRotation(new Vector3(0,100,0),Quaternion.Euler(90,0,0)); camera.orthographicSize=49;
                var target=new RenderTexture(1400,1400,24); var old=RenderTexture.active;
                var texture=new Texture2D(1400,1400,TextureFormat.RGB24,false);
                try
                {
                    camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
                    texture.ReadPixels(new Rect(0,0,1400,1400),0,0); texture.Apply();
                    System.IO.File.WriteAllBytes("Builds/battlefield-overview.png",texture.EncodeToPNG());
                }
                finally
                {
                    camera.targetTexture=null; RenderTexture.active=old; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(texture);
                    camera.transform.SetPositionAndRotation(oldPosition,oldRotation); camera.orthographicSize=oldSize;
                }
            }
            finally { if(opened) EditorSceneManager.CloseScene(scene,true); if(previous.IsValid()&&previous.isLoaded) SceneManager.SetActiveScene(previous); }
        }
    }
}

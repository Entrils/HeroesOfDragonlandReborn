using System;
using System.Linq;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.World.Camps;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace HeroesOfDragonland.World.Editor
{
    public static class BattlefieldBalancePass
    {
        private const string Folder = BattlefieldSceneBuilder.Folder;
        private static void Set(UnityEngine.Object target, string field, float value)
        {
            Undo.RecordObject(target, "Battlefield balance");
            var data = new SerializedObject(target);
            var property = data.FindProperty(field);
            if (property.propertyType == SerializedPropertyType.Integer) property.intValue = (int)value;
            else property.floatValue = value;
            data.ApplyModifiedProperties();
        }

        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != BattlefieldSceneBuilder.ScenePath)
                throw new InvalidOperationException("Open DragonlandBattlefield alone in Edit Mode.");
            var map = scene.GetRootGameObjects().First(x => x.GetComponent<NavMeshSurface>() != null);
            if (map.transform.Find("Balance pass | rotation barriers") != null)
                throw new InvalidOperationException("Balance pass already applied; inspect instead of duplicating.");
            string[] tiers = { "Low", "Medium", "High" };
            for (int i = 0; i < 3; i++)
            {
                var definition = AssetDatabase.LoadAssetAtPath<NetworkCampDefinition>(Folder + "/" + tiers[i] + "Camp.asset");
                Set(definition, "initialSpawnDelay", i == 2 ? 150 : 0);
                Set(definition, "respawnDelay", new[] {75,105,165}[i]);
                Set(definition, "experienceReward", new[] {25,50,90}[i]);
                Set(definition, "experienceRadius", 9);
                var loot = AssetDatabase.LoadAssetAtPath<LootTable>(Folder + "/" + tiers[i] + "Loot.asset");
                Set(loot, "minimumDrops", i == 2 ? 2 : 1);
                Set(loot, "maximumDrops", i == 0 ? 1 : i == 1 ? 2 : 3);
                if (i == 2)
                {
                    var data = new SerializedObject(loot);
                    float[] weights = { .3f, 1f, 1.5f, .5f, .05f };
                    for (int j = 0; j < 5; j++) data.FindProperty("rarityWeights").GetArrayElementAtIndex(j).floatValue = weights[j];
                    data.ApplyModifiedProperties();
                }
                if (!definition.TryValidate(out var reason)) throw new InvalidOperationException(reason);
            }
            var bootstrap = UnityEngine.Object.FindFirstObjectByType<MultiplayerBootstrap>();
            var spawner = bootstrap.GetComponent<NetworkCampSpawner>();
            var campData = new SerializedObject(spawner);
            Undo.RecordObject(spawner, "Move camp anchors");
            var entries = campData.FindProperty("entries");
            for (int i = 0; i < entries.arraySize; i++)
            {
                var position = entries.GetArrayElementAtIndex(i).FindPropertyRelative("localPosition");
                var old = position.vector3Value;
                var next = i < 4 ? new Vector3(Mathf.Sign(old.x)*33,0,Mathf.Sign(old.z)*18)
                    : i < 8 ? new Vector3(Mathf.Sign(old.x)*10,0,Mathf.Sign(old.z)*34) : Vector3.zero;
                position.vector3Value = next;
                var marker = map.GetComponentsInChildren<Transform>().First(t => t.name.StartsWith("Camp " + (i+1) + " |"));
                Undo.RecordObject(marker, "Move camp marker"); marker.position = next;
                var label = marker.GetComponentInChildren<TextMesh>();
                Undo.RecordObject(label, "Camp timing label");
                label.text = i < 4 ? "HOME / 25 XP / 75s" : i < 8 ? "FLANK / 50 XP / 105s" : "CENTER / 90 XP / 2:30 / 165s";
            }
            campData.ApplyModifiedProperties();
            var root = new GameObject("Balance pass | rotation barriers");
            Undo.RegisterCreatedObjectUndo(root, "Create rotation barriers"); root.transform.SetParent(map.transform, false);
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Battlefield/Placeholder stone.mat");
            foreach (int sign in new[]{-1,1})
            {
                Barrier(root.transform, new Vector3(15*sign,2,0), new Vector3(3,4,26), material);
                foreach (int side in new[]{-1,1})
                    Barrier(root.transform, new Vector3(21*sign,2,26*side), new Vector3(2,4,22), material);
            }
            string configPath = Folder + "/BattlefieldRespawn.asset";
            if (AssetDatabase.LoadAssetAtPath<SessionRespawnConfig>(configPath) == null)
                AssetDatabase.CopyAsset("Assets/Game/Data/MultiplayerFortress/SessionRespawn.asset", configPath);
            var config = AssetDatabase.LoadAssetAtPath<SessionRespawnConfig>(configPath);
            Set(config,"lateDelay",15); Set(config,"rampStartSeconds",600); Set(config,"rampEndSeconds",1200);
            var fortress = bootstrap.GetComponent<NetworkFortressSpawner>();
            Undo.RecordObject(fortress,"Battlefield respawn config");
            var fortressData = new SerializedObject(fortress);
            fortressData.FindProperty("respawnConfig").objectReferenceValue = config; fortressData.ApplyModifiedProperties();
            Physics.SyncTransforms();
            var surface = map.GetComponent<NavMeshSurface>();
            // Update the existing asset; preserve its GUID and scene reference.
            RebuildNavigation();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("[BattlefieldBalance] Saved recurring objectives, home/flank anchors, barriers and respawn ramp.");
        }

        public static void RebuildNavigation()
        {
            var surface=UnityEngine.Object.FindFirstObjectByType<NavMeshSurface>();
            var asset=surface.navMeshData;
            surface.BuildNavMesh();
            var baked=surface.navMeshData;
            surface.RemoveData();
            EditorUtility.CopySerialized(baked,asset);
            asset.name="BattlefieldNavMesh";
            surface.navMeshData=asset; surface.AddData();
            EditorUtility.SetDirty(asset);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene()); AssetDatabase.SaveAssets();
        }

        private static void Barrier(Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(wall,"Create rotation barrier");
            wall.name = "Rotation ridge | solid"; wall.transform.SetParent(parent,false);
            wall.transform.position = position; wall.transform.localScale = scale;
            wall.GetComponent<Renderer>().sharedMaterial = material;
            var modifier = wall.AddComponent<NavMeshModifier>();
            modifier.overrideArea = true; modifier.area = NavMesh.GetAreaFromName("Not Walkable");
        }

        public static void Validate()
        {
            Vector3[] positions = {new Vector3(-36,0,-5), Vector3.zero, new Vector3(-33,0,18),
                new Vector3(-10,0,34), new Vector3(10,0,34), new Vector3(24,0,0), new Vector3(-33,0,-18)};
            int[,] pairs = {{0,1},{0,2},{2,3},{3,4},{3,1},{1,5},{0,6}};
            for (int k=0;k<pairs.GetLength(0);k++)
            {
                int a=pairs[k,0], b=pairs[k,1];
                if (!NavMesh.SamplePosition(positions[a],out var x,1,NavMesh.AllAreas)
                    || !NavMesh.SamplePosition(positions[b],out var y,1,NavMesh.AllAreas))
                    throw new InvalidOperationException("Anchor off mesh " + a + " " + b);
                var path=new NavMeshPath(); NavMesh.CalculatePath(x.position,y.position,NavMesh.AllAreas,path);
                if(path.status!=NavMeshPathStatus.PathComplete) throw new InvalidOperationException("Blocked route");
                float distance=0; for(int j=1;j<path.corners.Length;j++) distance+=Vector3.Distance(path.corners[j-1],path.corners[j]);
                Debug.Log($"[BalanceRoute] {a}->{b}: {distance:F1}m / {distance/5:F1}s");
                // Rotational symmetry includes terrain and NavMesh, not only authored positions.
                var mirrored=new NavMeshPath();
                NavMesh.CalculatePath(new Vector3(-x.position.x,x.position.y,-x.position.z),
                    new Vector3(-y.position.x,y.position.y,-y.position.z),NavMesh.AllAreas,mirrored);
                float reverse=0;for(int j=1;j<mirrored.corners.Length;j++)reverse+=Vector3.Distance(mirrored.corners[j-1],mirrored.corners[j]);
                if(mirrored.status!=NavMeshPathStatus.PathComplete)
                    throw new InvalidOperationException("Blocked mirrored route " + a + " " + b);
                // NavMesh polygon A* can select a different detour around long ridges.
                // Verify the rotated counterpart of the actual route is traversable.
                for(int j=1;j<path.corners.Length;j++)
                {
                    var from=path.corners[j-1];var to=path.corners[j];
                    from=new Vector3(-from.x,from.y,-from.z);to=new Vector3(-to.x,to.y,-to.z);
                    if(!NavMesh.SamplePosition(from,out var mirroredFrom,.5f,NavMesh.AllAreas)
                        || !NavMesh.SamplePosition(to,out var mirroredTo,.5f,NavMesh.AllAreas))
                        throw new InvalidOperationException("Mirrored route corner blocked");
                    var segment=new NavMeshPath();NavMesh.CalculatePath(mirroredFrom.position,mirroredTo.position,NavMesh.AllAreas,segment);
                    if(segment.status!=NavMeshPathStatus.PathComplete)throw new InvalidOperationException("Mirrored route disconnected");
                }
                Debug.Log($"[BalanceRoute] paired automatic route: {reverse:F1}m; difference {Mathf.Abs(reverse-distance):F1}m");
                if ((a==1 && b==5 && distance<35) || (a==2 && b==3 && distance<25))
                    throw new InvalidOperationException("Rotation barrier did not lengthen route.");
            }
            var spawner=UnityEngine.Object.FindFirstObjectByType<NetworkCampSpawner>();
            var entries=new SerializedObject(spawner).FindProperty("entries");
            for(int i=0;i<entries.arraySize;i++)
            {
                var entry=entries.GetArrayElementAtIndex(i);
                var camp=(NetworkCamp)entry.FindPropertyRelative("prefab").objectReferenceValue;
                var position=spawner.transform.TransformPoint(entry.FindPropertyRelative("localPosition").vector3Value);
                var rotation=spawner.transform.rotation*Quaternion.Euler(0,entry.FindPropertyRelative("yaw").floatValue,0);
                foreach(var member in camp.Definition.Enemies)
                    if(!NavMesh.SamplePosition(position+rotation*member.Offset,out _,1,NavMesh.AllAreas))
                        throw new InvalidOperationException("Enemy off NavMesh at camp " + i);
            }
            var config=AssetDatabase.LoadAssetAtPath<SessionRespawnConfig>(Folder+"/BattlefieldRespawn.asset");
            if(config.DelayAt(0)!=5 || config.DelayAt(600)!=5 || config.DelayAt(900)!=10 || config.DelayAt(1200)!=15)
                throw new InvalidOperationException("Respawn ramp invalid");
            var match=AssetDatabase.LoadAssetAtPath<NetworkMatch>(Folder+"/BattlefieldMatch.prefab");
            var matchData=new SerializedObject(match);
            foreach(string field in new[]{"teamAKingPrefab","teamBKingPrefab"})
            {
                var king=(NetworkKingObjective)matchData.FindProperty(field).objectReferenceValue;
                Debug.Log("[BalanceSiege] "+field+" "+JsonUtility.ToJson(king.GetComponent<HeroesOfDragonland.Stats.CharacterStats>()));
            }
            Debug.Log("[BattlefieldBalanceValidation] PASS routes, paired distances, enemy anchors, respawn ramp");
        }
    }
}

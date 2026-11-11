using System;
using Fusion;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.World;
using HeroesOfDragonland.World.Camps;

public class CampDifficultyValidation
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static void Validate()
    {
        const string folder = "Assets/Game/Data/Battlefield/";
        var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>("Assets/Game/Data/Items/TestItemCatalog.asset");
        string[] names = { "Low", "Medium", "High", "Boss" };
        int previousXp = 0;
        for (int tier = 0; tier < 4; tier++)
        {
            var camp = AssetDatabase.LoadAssetAtPath<NetworkCamp>(folder + names[tier] + "Camp.prefab");
            var definition = camp.Definition;
            Check(definition.TryValidate(out var reason), reason);
            Check((int)definition.Difficulty == tier && definition.ExperienceReward > previousXp, "Tier/XP progression");
            previousXp = definition.ExperienceReward;
            var table = definition.ChestPrefab.GetComponent<LootChest>().Table;
            Check(table.TryValidate(catalog, out reason), reason);
            var seen = new bool[5];
            var random = new System.Random(1729);
            for (int roll = 0; roll < 2000; roll++)
            {
                Check(table.TryRoll(random, out var rewards, out reason), reason);
                Check(rewards.Length >= (tier < 2 ? 1 : tier == 2 ? 2 : 3) && rewards.Length <= tier + 1, "Drop count");
                foreach (var reward in rewards)
                {
                    Check(catalog.TryGet(reward.ItemId, out var item), "Catalog resolution");
                    int rarity = (int)item.Rarity;
                    Check(rarity == tier || rarity == tier + 1, "Rarity outside tier range");
                    seen[rarity] = true;
                }
            }
            Check(seen[tier] && seen[tier + 1], "Both rarities must be reachable");
            Debug.Log(names[tier] + ": valid definition/catalog and 2000 seeded loot rolls");
        }
        var spawner = UnityEngine.Object.FindFirstObjectByType<NetworkCampSpawner>();
        var entries = new SerializedObject(spawner).FindProperty("entries");
        Check(entries.arraySize == 11, "Eleven camp anchors required");
        int[] counts = new int[4];
        for (int i = 0; i < entries.arraySize; i++)
        {
            var entry = entries.GetArrayElementAtIndex(i);
            var camp = (NetworkCamp)entry.FindPropertyRelative("prefab").objectReferenceValue;
            var position = spawner.transform.TransformPoint(entry.FindPropertyRelative("localPosition").vector3Value);
            var rotation = spawner.transform.rotation * Quaternion.Euler(0, entry.FindPropertyRelative("yaw").floatValue, 0);
            counts[(int)camp.Definition.Difficulty]++;
            if (camp.Definition.Difficulty == CampDifficulty.Contested) Check(position == Vector3.zero, "Boss must be central");
            foreach (var member in camp.Definition.Enemies)
                Check(NavMesh.SamplePosition(position + rotation * member.Offset, out _, 1, NavMesh.AllAreas), "Enemy anchor off NavMesh " + i);
            Check(NavMesh.SamplePosition(position + rotation * camp.Definition.ChestOffset, out _, 1, NavMesh.AllAreas), "Chest anchor off NavMesh");
        }
        Check(counts[0] == 4 && counts[1] == 4 && counts[2] == 2 && counts[3] == 1, "Tier distribution");
        Debug.Log("PASS: tiers, XP, 8000 loot rolls, central objective and all enemy/chest anchors");
    }
}


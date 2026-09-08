using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Player;
using HeroesOfDragonland.Interaction;
using HeroesOfDragonland.InventorySystem;
using HeroesOfDragonland.World;
using HeroesOfDragonland.World.Camps;

internal class CommandScript : IRunCommand
{
    private void Check(bool value, string label)
    {
        if (!value) throw new Exception("FAIL: " + label);
        Debug.Log("Camp PASS: " + label);
    }
    private long Items(Inventory inventory)
    {
        long count = 0;
        for (int i = 0; i < inventory.SlotCount; i++) count += inventory.GetSlot(i).Amount;
        return count;
    }
    public void Execute(ExecutionResult result)
    {
        if (!Application.isPlaying) throw new Exception("Run in fresh Play Mode after Start; exit Play Mode afterwards.");
        var camp = GameObject.Find("Test Neutral Camp").GetComponent<Camp>();
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        player.enabled = false;
        foreach (var brain in UnityEngine.Object.FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None)) brain.SetAuthority(false);
        var inventory = player.GetComponent<Inventory>();
        var originalItems = Items(inventory);
        var originalRevision = inventory.Revision;
        int originalChests = UnityEngine.Object.FindObjectsByType<LootChest>(FindObjectsSortMode.None).Length;
        Check(camp.IsInitialized && camp.Enemies.Count == 3 && camp.DeadCount == 0, "three camp-owned enemies spawned");
        Check(camp.Enemies[0].Definition.name == "Bandit" && camp.Enemies[2].Definition.name == "Wolf"
            && camp.Enemies[2].GetComponent<CharacterStats>().MaxHealth == 60, "group types configured before stats initialization");
        Check(!camp.IsCleared && camp.SpawnedChest == null && camp.AwardedExperience == 0, "active camp has no rewards");
        int xpEvents = 0, clearEvents = 0;
        bool xpOrder = false, clearOrder = false, reentrantRejected = false;
        camp.ExperienceAwarded += reward =>
        {
            xpEvents++;
            xpOrder = camp.IsCleared && camp.AwardedExperience == 150 && reward.Amount == 150
                && reward.CampId == camp.Id && camp.SpawnedChest == null && inventory.Revision == originalRevision;
            reentrantRejected = !camp.TryComplete() && !camp.SetAuthority(false) && !camp.TryInitialize(out _);
        };
        camp.Cleared += value =>
        {
            clearEvents++;
            clearOrder = value == camp && xpEvents == 1 && camp.SpawnedChest != null && !camp.SpawnedChest.IsOpen;
        };
        var first = camp.Enemies[0];
        first.gameObject.SetActive(false);
        Check(!camp.TryComplete() && camp.DeadCount == 0, "disabled living enemy does not count as dead");
        first.gameObject.SetActive(true);
        for (int i = 0; i < camp.Enemies.Count; i++)
        {
            var receiver = camp.Enemies[i].GetComponent<DamageReceiver>();
            receiver.TakeDamage(10000, player.gameObject);
            receiver.TakeDamage(10000, player.gameObject);
            Check(camp.DeadCount == i + 1, "death counted once for member " + i);
            if (i < camp.Enemies.Count - 1)
                Check(!camp.IsCleared && xpEvents == 0 && camp.SpawnedChest == null, "partial kill does not clear camp");
        }
        Check(camp.IsCleared && xpEvents == 1 && clearEvents == 1 && xpOrder && clearOrder, "clear -> XP event -> closed chest order");
        Check(reentrantRejected && camp.HasAuthority, "reentrant completion and authority mutation rejected");
        var chest = camp.SpawnedChest;
        var point = new SerializedObject(camp).FindProperty("chestSpawnPoint").objectReferenceValue as Transform;
        Check(Vector3.Distance(chest.transform.position, point.position) < 0.001f
            && Quaternion.Angle(chest.transform.rotation, point.rotation) < 0.01f
            && chest.Table == camp.Definition.LootTable, "configured chest position, rotation and loot table");
        Check(UnityEngine.Object.FindObjectsByType<LootChest>(FindObjectsSortMode.None).Length == originalChests + 1,
            "exactly one new chest");
        Check(!chest.IsOpen && chest.PendingRewards.Count == 0 && inventory.Revision == originalRevision
            && Items(inventory) == originalItems, "camp never gives inventory items or opens chest");
        for (int i = 0; i < 5; i++) Check(!camp.TryComplete(), "repeat clear rejected " + i);
        camp.enabled = false; camp.enabled = true;
        Check(xpEvents == 1 && clearEvents == 1 && camp.SpawnedChest == chest, "disable/enable does not repeat rewards");
        Check(!chest.TryInteract(inventory, out _), "remote opening rejected");
        var cc = player.GetComponent<CharacterController>();
        cc.enabled = false;
        player.transform.position = chest.transform.position + Vector3.back * 1.6f;
        cc.enabled = true;
        player.GetComponent<CharacterStats>().ResetToFull();
        Physics.SyncTransforms();
        camp.SetAuthority(false);
        Check(!chest.HasAuthority && !chest.TryInteract(inventory, out _), "camp authority propagates to unopened chest");
        camp.SetAuthority(true);
        inventory.SetAuthority(false);
        Check(!chest.TryInteract(inventory, out _), "replica inventory cannot receive chest rewards");
        inventory.SetAuthority(true);
        Check(player.GetComponent<PlayerInteractor>().TryInteract(out string message), "player manually opens spawned chest: " + message);
        Check(chest.IsOpen && Items(inventory) > originalItems && inventory.Revision > originalRevision,
            "chest loot enters existing inventory");
        long lootedItems = Items(inventory);
        Check(!chest.TryInteract(inventory, out _) && Items(inventory) == lootedItems,
            "opened exhausted chest cannot grant twice");
        Check(xpEvents == 1 && clearEvents == 1, "opening chest does not award XP again");
        Debug.Log("CAMP FLOW VALIDATION PASSED");
    }
}

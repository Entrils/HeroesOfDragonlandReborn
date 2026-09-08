using System;
using UnityEngine;
using UnityEditor;
using HeroesOfDragonland.Progression;
using HeroesOfDragonland.World.Camps;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.Player;
using HeroesOfDragonland.Interaction;
using HeroesOfDragonland.InventorySystem;
using HeroesOfDragonland.EquipmentSystem;
using HeroesOfDragonland.Items;

internal class CommandScript : IRunCommand
{
    private void Check(bool value, string label)
    {
        if (!value) throw new Exception("FAIL: " + label);
        Debug.Log("Camp XP PASS: " + label);
    }
    public void Execute(ExecutionResult result)
    {
        if (!Application.isPlaying) throw new Exception("Run in fresh Play Mode after Start");
        var player = GameObject.Find("Player");
        var progression = player.GetComponent<SessionProgression>();
        var session = UnityEngine.Object.FindFirstObjectByType<ProgressionSession>();
        var camp = GameObject.Find("Test Neutral Camp").GetComponent<Camp>();
        var source = camp.GetComponent<CampExperienceSource>();
        var inv = player.GetComponent<Inventory>();
        player.GetComponent<PlayerController>().enabled = false;
        foreach (var enemy in UnityEngine.Object.FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None)) enemy.SetAuthority(false);
        Check(progression.IsInitialized && progression.SessionId == session.SessionId && progression.Level == 1
            && progression.CurrentXP == 0 && progression.ProgressionPoints == 0, "scene starts a fresh shared session");
        int levels = 0, xpGrants = 0;
        progression.LeveledUp += _ => levels++;
        progression.ExperienceGained += _ => xpGrants++;
        long inventoryRevision = inv.Revision;
        for (int i = 0; i < camp.Enemies.Count; i++)
        {
            camp.Enemies[i].GetComponent<DamageReceiver>().TakeDamage(10000, player);
            if (i < camp.Enemies.Count - 1) Check(progression.CurrentXP == 0 && xpGrants == 0, "partial camp kill gives no XP");
        }
        Check(camp.IsCleared && progression.Level == 2 && progression.CurrentXP == 50
            && progression.XPRequiredForNextLevel == 150 && progression.ProgressionPoints == 1 && levels == 1 && xpGrants == 1,
            "camp awards 150 XP: level 2, 50 XP remainder, one point");
        Check(camp.SpawnedChest != null && !camp.SpawnedChest.IsOpen && inv.Revision == inventoryRevision,
            "XP does not auto-open chest or grant loot");
        source.TryDeliverReward(); source.enabled = false; source.enabled = true;
        camp.TryComplete();
        Check(xpGrants == 1 && progression.CurrentXP == 50, "retries and re-enable cannot duplicate camp XP");
        var cc = player.GetComponent<CharacterController>();
        cc.enabled = false;
        player.transform.position = camp.SpawnedChest.transform.position + Vector3.back * 1.6f;
        cc.enabled = true; Physics.SyncTransforms();
        Check(player.GetComponent<PlayerInteractor>().TryInteract(out string message), "manual chest opening: " + message);
        Check(inv.Revision > inventoryRevision && xpGrants == 1, "loot delivered separately with no second XP grant");
        string oldSession = session.SessionId;
        Check(session.TryStartSession("validation-next-match", out _) && progression.Level == 1
            && progression.CurrentXP == 0 && progression.ProgressionPoints == 0, "match start resets player progression");
        source.TryDeliverReward(); source.enabled = false; source.enabled = true;
        Check(progression.Level == 1 && progression.CurrentXP == 0 && xpGrants == 1, "old cleared camp cannot pay into new match");
        Check(!progression.TryAddExperience(150, oldSession, "late-network-reward", out _), "delayed previous-session XP rejected");
        Check(progression.TryAddExperience(500, progression.SessionId, "future-source:test", out _)
            && progression.Level == 4 && progression.CurrentXP == 0 && progression.ProgressionPoints == 5,
            "generic future source supports multiple levels");
        Check(player.GetComponent<CharacterAttributes>().TryGetBaseValue(ItemStat.Level, out float level) && level == 4,
            "player attribute level follows progression");
        Check(session.TryStartSession("validation-next-match", out _) && progression.Level == 4,
            "repeated match-start notification preserves earned XP");
        Debug.Log("CAMP SESSION PROGRESSION VALIDATION PASSED");
    }
}

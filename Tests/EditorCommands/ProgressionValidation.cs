using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using HeroesOfDragonland.Progression;
using HeroesOfDragonland.EquipmentSystem;
using HeroesOfDragonland.Items;

internal class CommandScript : IRunCommand
{
    private void Check(bool value, string label)
    {
        if (!value) throw new Exception("FAIL: " + label);
        Debug.Log("Progression PASS: " + label);
    }
    public void Execute(ExecutionResult result)
    {
        if (!Application.isPlaying) throw new Exception("Run in Play Mode");
        var config = ScriptableObject.CreateInstance<LevelProgressionDefinition>();
        result.RegisterObjectCreation(config);
        var actor = new GameObject("Progression validation player");
        var other = new GameObject("Progression validation second player");
        result.RegisterObjectCreation(actor); result.RegisterObjectCreation(other);
        try
        {
            var settings = new SerializedObject(config);
            var levels = settings.FindProperty("levels");
            levels.arraySize = 4;
            int[] thresholds = { 100, 150, 250, 400 };
            for (int i = 0; i < 4; i++)
            {
                levels.GetArrayElementAtIndex(i).FindPropertyRelative("requiredXP").intValue = thresholds[i];
                levels.GetArrayElementAtIndex(i).FindPropertyRelative("progressionPoints").intValue = i + 1;
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
            Check(config.TryValidate(out _), "curve accepts positive thresholds and points");
            var p = actor.AddComponent<SessionProgression>();
            var q = other.AddComponent<SessionProgression>();
            foreach (var player in new[] { p, q })
            {
                var fields = new SerializedObject(player);
                fields.FindProperty("definition").objectReferenceValue = config;
                fields.ApplyModifiedPropertiesWithoutUndo();
                Check(player.TryBeginSession("test-session", out _), "initialize player session");
            }
            Check(p.Level == 1 && p.CurrentXP == 0 && p.XPRequiredForNextLevel == 100 && p.ProgressionPoints == 0, "level-one defaults");
            var events = new List<LevelUpEvent>();
            int gains = 0, changes = 0;
            p.LeveledUp += events.Add;
            p.ExperienceGained += _ => gains++;
            p.Changed += () => changes++;
            Check(!p.TryAddExperience(0, p.SessionId, "zero", out _)
                && !p.TryAddExperience(-1, p.SessionId, "negative", out _)
                && !p.TryAddExperience(10, "stale-session", "stale", out _)
                && !p.TryAddExperience(10, p.SessionId, "", out _) && gains == 0 && changes == 0,
                "invalid and stale rewards reject without events");
            Check(p.TryAddExperience(99, p.SessionId, "one", out _) && p.Level == 1 && p.CurrentXP == 99 && events.Count == 0,
                "below threshold preserves XP");
            Check(p.TryAddExperience(1, p.SessionId, "two", out _) && p.Level == 2 && p.CurrentXP == 0
                && p.XPRequiredForNextLevel == 150 && p.ProgressionPoints == 1 && events.Count == 1,
                "exact threshold levels once");
            Check(!p.TryAddExperience(1, p.SessionId, "two", out _) && p.CurrentXP == 0 && events.Count == 1,
                "same reward cannot pay twice");
            Check(q.Level == 1 && q.CurrentXP == 0 && q.TryAddExperience(10, q.SessionId, "two", out _) && q.CurrentXP == 10,
                "player state and reward deduplication are independent");
            Check(p.TryBeginSession("test-session", out _) && p.Level == 2 && p.ProgressionPoints == 1, "same session ID is idempotent");
            Check(p.TryBeginSession("large-grant", out _) && p.Level == 1 && p.ProgressionPoints == 0 && p.CurrentXP == 0,
                "new session resets level, XP and points");
            events.Clear(); gains = 0; changes = 0;
            bool committed = true, reentrantRejected = true;
            Action<LevelUpEvent> observe = e =>
            {
                committed &= p.Level == 4 && p.CurrentXP == 150 && p.ProgressionPoints == 6;
                reentrantRejected &= !p.TryAddExperience(100, p.SessionId, "reentrant", out _)
                    && !p.TryBeginSession("reentrant-reset", out _) && !p.SetAuthority(false);
            };
            p.LeveledUp += observe;
            Check(p.TryAddExperience(650, p.SessionId, "one", out _), "new session accepts reused reward ID");
            p.LeveledUp -= observe;
            Check(p.Level == 4 && p.CurrentXP == 150 && p.XPRequiredForNextLevel == 400 && p.ProgressionPoints == 6,
                "large XP crosses three levels with remainder");
            Check(events.Count == 3 && events[0].Level == 2 && events[1].Level == 3 && events[2].Level == 4
                && events[2].PreviousLevel == 3 && events[0].PointsAwarded == 1 && events[2].PointsAwarded == 3
                && events[1].TotalProgressionPoints == 3 && gains == 1 && changes == 1,
                "one ordered event per level and one change per grant");
            Check(committed && reentrantRejected && p.HasAuthority, "events observe committed state and reject reentrant writes");
            var attributes = actor.AddComponent<CharacterAttributes>();
            Check(attributes.TryGetBaseValue(ItemStat.Level, out float level) && level == p.Level
                && attributes.TryGetValue(ItemStat.Level, out level) && level == p.Level, "equipment reads actual session level");
            actor.SetActive(false);
            actor.SetActive(true);
            Check(p.Level == 4 && p.CurrentXP == 150 && p.ProgressionPoints == 6, "disable/enable does not reset progression");
            p.SetAuthority(false);
            Check(!p.TryAddExperience(1, p.SessionId, "replica", out _) && !p.TryBeginSession("replica-reset", out _),
                "replica cannot grant XP or reset session");
            p.SetAuthority(true);
            Check(p.TryAddExperience(long.MaxValue, p.SessionId, "huge", out _) && p.IsMaximumLevel && p.Level == 5
                && p.CurrentXP == 0 && p.XPRequiredForNextLevel == 0 && p.ProgressionPoints == 10,
                "extreme reward is bounded and cannot overflow");
            Check(!p.TryAddExperience(1, p.SessionId, "capped", out _) && p.ProgressionPoints == 10, "level cap gives no extra points");

            levels.GetArrayElementAtIndex(0).FindPropertyRelative("requiredXP").intValue = 200;
            settings.ApplyModifiedPropertiesWithoutUndo();
            Check(q.XPRequiredForNextLevel == 100, "curve is frozen for the current session");
            Check(q.TryBeginSession("changed-curve", out _) && q.XPRequiredForNextLevel == 200, "next session uses updated configuration");
            levels.GetArrayElementAtIndex(0).FindPropertyRelative("requiredXP").intValue = 0;
            settings.ApplyModifiedPropertiesWithoutUndo();
            Check(!config.TryValidate(out _) && !q.TryBeginSession("bad-curve", out _) && q.SessionId == "changed-curve",
                "invalid curve cannot reset current progression");
            levels.GetArrayElementAtIndex(0).FindPropertyRelative("requiredXP").intValue = 100;
            levels.GetArrayElementAtIndex(0).FindPropertyRelative("progressionPoints").intValue = -1;
            settings.ApplyModifiedPropertiesWithoutUndo();
            Check(!config.TryValidate(out _), "negative points rejected");
            levels.arraySize = 0; settings.ApplyModifiedPropertiesWithoutUndo();
            Check(!config.TryValidate(out _), "empty curve rejected");
            Debug.Log("SESSION PROGRESSION VALIDATION PASSED");
        }
        finally
        {
            result.DestroyObject(actor); result.DestroyObject(other); result.DestroyObject(config);
        }
    }
}

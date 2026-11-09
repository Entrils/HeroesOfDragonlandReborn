using System;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Progression;
using HeroesOfDragonland.Skills;
using HeroesOfDragonland.Stats;
using UnityEditor;
using UnityEngine;

internal class CommandScript : IRunCommand
{
    private static void Check(bool condition, string label)
    { if (!condition) throw new Exception("Skill validation failed: " + label); }
    public void Execute(ExecutionResult result)
    {
        Check(Application.isPlaying, "Run in Play Mode");
        var actor = new GameObject("Temporary session skill rules validation");
        actor.SetActive(false); actor.transform.position = new Vector3(5000, 1, 5000);
        var curve = ScriptableObject.CreateInstance<LevelProgressionDefinition>();
        var attack = ScriptableObject.CreateInstance<MeleeAttackDefinition>();
        result.RegisterObjectCreation(actor);
        try
        {
            var curveData = new SerializedObject(curve); var levels = curveData.FindProperty("levels"); levels.arraySize = 2;
            for (int i = 0; i < 2; i++)
            { levels.GetArrayElementAtIndex(i).FindPropertyRelative("requiredXP").intValue = 1; levels.GetArrayElementAtIndex(i).FindPropertyRelative("progressionPoints").intValue = 3; }
            curveData.ApplyModifiedPropertiesWithoutUndo();
            var skills = actor.AddComponent<SessionSkills>(); var progression = actor.GetComponent<SessionProgression>();
            var progressionData = new SerializedObject(progression); progressionData.FindProperty("definition").objectReferenceValue = curve; progressionData.ApplyModifiedPropertiesWithoutUndo();
            var skillsData = new SerializedObject(skills); var entries = skillsData.FindProperty("definitions"); entries.arraySize = 3;
            var files = new[] { "PowerStrike", "Guard", "MeleeTraining" };
            for (int i = 0; i < 3; i++)
            {
                var definition = AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/Game/Data/Skills/" + files[i] + ".asset");
                Check(definition != null && definition.IsValid, "valid definition " + files[i]); entries.GetArrayElementAtIndex(i).objectReferenceValue = definition;
            }
            skillsData.ApplyModifiedPropertiesWithoutUndo();
            var melee = actor.AddComponent<MeleeAttack>(); var meleeData = new SerializedObject(melee);
            meleeData.FindProperty("definition").objectReferenceValue = attack; meleeData.ApplyModifiedPropertiesWithoutUndo();
            actor.SetActive(true); var stats = actor.GetComponent<CharacterStats>();
            Check(progression.TryBeginSession("skill-rules-a", out _), "start session");
            Check(skills.SkillPoints == 1 && skills.LearnedMask == 0, "one initial point");
            Check(!skills.TryLearn(-1, skills.SessionId, out _) && !skills.TryLearn(3, skills.SessionId, out _), "invalid indices rejected");
            Check(!skills.TryLearn(1, skills.SessionId, out _) && skills.MissingRequirements(1).Contains("Level"), "level gate");
            Check(!skills.TryActivate(0, skills.SessionId, out _) && stats.CurrentMana == stats.MaxMana, "unlearned cast has no cost");
            Check(skills.TryLearn(0, skills.SessionId, out _) && skills.SkillPoints == 0, "learn costs point");
            Check(!skills.TryLearn(0, skills.SessionId, out _), "duplicate learn rejected");
            float stamina = stats.CurrentStamina, mana = stats.CurrentMana;
            Check(skills.TryActivate(0, skills.SessionId, out _) && stats.CurrentMana == mana - 20
                && stats.CurrentStamina == stamina && melee.IsBusy, "strike pays Mana only and starts melee");
            Check(!skills.TryActivate(0, skills.SessionId, out _) && stats.CurrentMana == mana - 20, "cooldown rejects without cost");
            melee.TickAttack(2); skills.Tick(5);
            Check(stats.TrySpendMana(stats.CurrentMana) && !skills.TryActivate(0, skills.SessionId, out _)
                && !melee.IsBusy && skills.CooldownRemaining(0) == 0, "insufficient Mana has no effects");
            stats.ResetToFull();
            Check(progression.TryAddExperience(2, progression.SessionId, "rules-levels", out _)
                && skills.SkillPoints == 2, "one point per crossed level");
            Check(!skills.TryLearn(2, skills.SessionId, out _) && skills.MissingRequirements(2).Contains("Vitality"), "stat gate");
            Check(progression.TryIncreaseAttribute(ItemStat.Vitality, progression.SessionId, out _)
                && progression.TryIncreaseAttribute(ItemStat.Vitality, progression.SessionId, out _), "allocate prerequisites");
            Check(skills.TryLearn(1, skills.SessionId, out _) && skills.TryLearn(2, skills.SessionId, out _)
                && skills.SkillPoints == 0, "separate skill and attribute points");
            Check(!skills.TryActivate(2, skills.SessionId, out _) && skills.ModifyMeleeDamage(25) == 30
                && skills.ModifyMeleeDamage(25) == 30, "passive applies exactly once and cannot cast");
            mana = stats.CurrentMana;
            Check(skills.TryActivate(1, skills.SessionId, out _) && stats.CurrentMana == mana - 25 && skills.IsInvulnerable
                && stats.ApplyDamage(10) == 0, "defense spends Mana and blocks damage");
            skills.Tick(1.6f);
            Check(!skills.IsInvulnerable && stats.ApplyDamage(10) == 10, "defense expires");
            float cooldown = skills.CooldownRemaining(1); skills.Tick(float.NaN); skills.Tick(-1);
            Check(skills.CooldownRemaining(1) == cooldown, "invalid time ignored");
            skills.Tick(10); stats.ResetToFull();
            Check(skills.TryActivate(1, skills.SessionId, out _), "defense reusable after cooldown");
            Check(progression.TryBeginSession("skill-rules-b", out _) && skills.SkillPoints == 1 && skills.LearnedMask == 0
                && skills.GuardRemaining == 0 && skills.CooldownRemaining(0) == 0 && skills.CooldownRemaining(1) == 0
                && skills.ModifyMeleeDamage(25) == 25, "new session removes all skills, passive effects and timers");
            Check(!skills.TryLearn(0, "skill-rules-a", out _) && !skills.TryActivate(0, "skill-rules-a", out _), "stale session rejected");
            Check(skills.TryLearn(0, skills.SessionId, out _) && progression.TryBeginSession(skills.SessionId, out _)
                && skills.IsLearned(0) && skills.SkillPoints == 0, "same-session reset idempotent");
            result.Log("PASS: skill points, all prerequisites, invalid requests, Mana, cooldowns, active/passive effects, defense expiration and session reset.");
        }
        finally { result.DestroyObject(actor); result.DestroyObject(curve); result.DestroyObject(attack); }
    }
}

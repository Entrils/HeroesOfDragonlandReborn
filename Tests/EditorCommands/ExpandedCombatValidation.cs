using System;
using UnityEngine;
using UnityEditor;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Multiplayer;

internal class CommandScript : IRunCommand
{
    private int checks;
    private void Check(bool value, string name) { if (!value) throw new Exception("FAIL " + name); checks++; }
    public void Execute(ExecutionResult result)
    {
        Check(Application.isPlaying, "Play Mode");
        GameObject attacker = null, victim = null;
        MeleeAttackDefinition definition = null;
        CharacterStatsConfig config = null;
        try
        {
            attacker = new GameObject("Expanded combat attacker"); attacker.transform.position = new Vector3(1000, 0, 1000);
            var attack = attacker.AddComponent<MeleeAttack>();
            definition = ScriptableObject.CreateInstance<MeleeAttackDefinition>();
            var serialized = new SerializedObject(attack); serialized.FindProperty("definition").objectReferenceValue = definition; serialized.ApplyModifiedPropertiesWithoutUndo();
            var a = attacker.GetComponent<CharacterStats>();
            victim = new GameObject("Expanded combat victim"); victim.transform.position = attacker.transform.position + Vector3.forward * 1.2f;
            var receiver = victim.AddComponent<DamageReceiver>();
            var collider = victim.AddComponent<CapsuleCollider>(); collider.center = Vector3.up; collider.height = 2;
            var v = victim.GetComponent<CharacterStats>();
            config = ScriptableObject.CreateInstance<CharacterStatsConfig>();
            serialized = new SerializedObject(config); serialized.FindProperty("maxHealth").floatValue = 1000; serialized.ApplyModifiedPropertiesWithoutUndo();
            v.ConfigureForSpawn(config);
            var defense = victim.GetComponent<CombatDefense>();
            Physics.SyncTransforms();
            Check(attack.TryAttack(), "light starts");
            Check(Mathf.Approximately(a.CurrentStamina, 100 - definition.StaminaCost), "light unchanged cost");
            attack.TickAttack(definition.ActiveStartTime + 0.01f);
            Check(Mathf.Approximately(v.CurrentHealth, 1000 - definition.Damage), "light unchanged damage");
            attack.TickAttack(0.01f);
            Check(Mathf.Approximately(v.CurrentHealth, 1000 - definition.Damage), "one hit per target");
            attack.TickAttack(10); a.ResetToFull(); v.ResetToFull(); defense.Tick(10);
            Check(attack.TryHeavyAttack(), "heavy starts");
            Check(Mathf.Approximately(a.CurrentStamina, 100 - definition.StaminaCost * definition.HeavyStaminaMultiplier), "heavy increased cost");
            attack.TickAttack(definition.ActiveStartTime + 0.01f);
            Check(v.CurrentHealth == 1000, "heavy slower windup");
            attack.TickAttack(definition.ActiveStartTime * definition.HeavyTimingMultiplier);
            Check(Mathf.Approximately(v.CurrentHealth, 1000 - definition.Damage * definition.HeavyDamageMultiplier), "heavy increased damage");
            Check(defense.IsStaggered && attack.StaggerPower > definition.StaggerPower, "heavy increased stagger");
            attack.TickAttack(10); a.ResetToFull(); v.ResetToFull(); defense.Tick(10);
            defense.SetBlockIntent(true, false);
            Check(defense.IsBlocking, "held block");
            float stamina = v.CurrentStamina;
            Check(Mathf.Approximately(receiver.TakeDamage(20, attacker), 5), "block reduces damage by 75 percent");
            Check(Mathf.Approximately(v.CurrentStamina, stamina - 20), "block hit stamina cost");
            defense.Tick(0.5f);
            Check(Mathf.Approximately(v.CurrentStamina, stamina - 25), "held block drain");
            receiver.TakeDamage(100, attacker);
            Check(v.CurrentStamina == 0 && !defense.IsBlocking && defense.IsStaggered && defense.GuardBreakSequence == 1, "guard break exhausts stamina and interrupts");
            defense.ApplyStagger(10000); defense.Tick(1.01f);
            Check(!defense.IsStaggered, "hits cannot extend stagger");
            defense.ApplyStagger(10000);
            Check(!defense.IsStaggered, "post stagger immunity");
            v.ResetToFull(); defense.SetBlockIntent(true, false);
            Check(!defense.IsBlocking, "guard break requires input release");
            defense.SetBlockIntent(false, false); defense.SetBlockIntent(true, false);
            Check(defense.IsBlocking, "block resumes after release");
            defense.SetBlockIntent(false, false); defense.Tick(10);
            defense.ApplyStagger(v.StaggerResistance - 1);
            Check(!defense.IsStaggered, "resistance absorbs low stagger");
            defense.ApplyStagger(1);
            Check(defense.IsStaggered, "resistance threshold staggers");
            defense.Tick(10);
            var interrupted = victim.AddComponent<MeleeAttack>();
            serialized = new SerializedObject(interrupted); serialized.FindProperty("definition").objectReferenceValue = definition; serialized.ApplyModifiedPropertiesWithoutUndo();
            Check(interrupted.TryAttack(), "victim attack begins");
            defense.ApplyStagger(v.StaggerResistance);
            Check(!interrupted.IsBusy && !interrupted.TryHeavyAttack(), "stagger cancels attack and rejects new attack");
            defense.Tick(10); v.ResetToFull(); defense.SetBlockIntent(true, false);
            victim.AddComponent(typeof(CharacterStats).Assembly.GetType("HeroesOfDragonland.Multiplayer.NetworkCharacterStats"));
            float hp = v.CurrentHealth, sp = v.CurrentStamina;
            Check(receiver.TakeDamage(40, attacker) == 0 && v.CurrentHealth == hp && v.CurrentStamina == sp, "non authoritative damage cannot mutate block or resources");
            defense.Tick(10); defense.ApplyStagger(10000);
            Check(!defense.IsStaggered && v.CurrentStamina == sp && !interrupted.TryAttack(), "non authoritative timers stagger and attacks rejected");
            result.Log("PASS expanded combat: " + checks + " checks");
        }
        finally
        {
            if (attacker != null) UnityEngine.Object.DestroyImmediate(attacker);
            if (victim != null) UnityEngine.Object.DestroyImmediate(victim);
            if (definition != null) UnityEngine.Object.DestroyImmediate(definition);
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
        }
    }
}

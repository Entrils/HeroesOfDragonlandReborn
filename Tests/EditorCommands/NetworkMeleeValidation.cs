using System;
using UnityEngine;
using UnityEditor;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Combat;

internal class CommandScript : IRunCommand
{
    private int checks;
    private void Check(bool value, string message)
    {
        if (!value) throw new Exception("FAIL: " + message);
        checks++;
    }
    public void Execute(ExecutionResult result)
    {
        if (!Application.isPlaying) throw new Exception("Run in Play Mode so Unity initializes component lifecycles");
        var attacker = new GameObject("M3 temporary melee test");
        var victim = new GameObject("M3 temporary victim");
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        result.RegisterObjectCreation(attacker); result.RegisterObjectCreation(victim); result.RegisterObjectCreation(wall);
        try
        {
            attacker.transform.position = new Vector3(100, 1, 100);
            victim.transform.position = attacker.transform.position + Vector3.forward * 1.6f;
            var stats = attacker.AddComponent<CharacterStats>();
            var melee = attacker.AddComponent<MeleeAttack>();
            var fields = new SerializedObject(melee);
            fields.FindProperty("definition").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MeleeAttackDefinition>("Assets/Game/Data/MultiplayerFoundation/NetworkMelee.asset");
            fields.ApplyModifiedPropertiesWithoutUndo();
            victim.AddComponent<DamageReceiver>();
            var health = victim.GetComponent<CharacterStats>();
            victim.AddComponent<CapsuleCollider>();
            var extra = new GameObject("Second hurt collider"); extra.transform.SetParent(victim.transform, false); extra.AddComponent<SphereCollider>();
            wall.transform.position = new Vector3(120, 1, 120);
            Physics.SyncTransforms();
            Check(melee.TryAttack(), "attack accepted");
            Check(stats.CurrentStamina == 75, "single stamina charge");
            for (int i=0; i<20; i++) Check(!melee.TryAttack(), "spam rejected");
            melee.TickAttack(0.1f);
            Check(health.CurrentHealth == 100, "windup no damage");
            melee.TickAttack(0.1f);
            Check(health.CurrentHealth == 75, "server geometry hits");
            for(int i=0; i<10; i++) melee.TickAttack(0.01f);
            Check(health.CurrentHealth == 75, "multiple colliders and active ticks do not duplicate damage");
            Check(stats.CurrentStamina == 75, "rejected spam does not spend");
            melee.TickAttack(0.3f);
            Check(!melee.TryAttack(), "recovery rejects attack");
            melee.TickAttack(0.4f);
            victim.transform.position += Vector3.forward * 10;
            Physics.SyncTransforms();
            Check(melee.TryAttack(), "next swing accepted"); melee.TickAttack(1);
            Check(health.CurrentHealth == 75, "out of range rejected");
            victim.transform.position = attacker.transform.position + Vector3.forward * 1.6f;
            wall.transform.position = attacker.transform.position + new Vector3(0, 1, 0.7f);
            wall.transform.localScale = new Vector3(4, 4, 0.1f);
            Physics.SyncTransforms();
            Check(melee.TryAttack(), "obstructed swing starts"); melee.TickAttack(1);
            Check(health.CurrentHealth == 75, "wall blocks hit");
            stats.TrySpendStamina(stats.CurrentStamina);
            Check(!melee.TryAttack(), "empty stamina rejects attack");

            result.Log("PASS: " + checks + " melee geometry, timing, spam and duplicate checks");
        }
        finally { result.DestroyObject(wall); result.DestroyObject(victim); result.DestroyObject(attacker); }
    }
}

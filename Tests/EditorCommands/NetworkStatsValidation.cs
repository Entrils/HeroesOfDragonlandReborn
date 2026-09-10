using System;
using UnityEngine;
using HeroesOfDragonland.Stats;

internal class CommandScript : IRunCommand
{
    private int checks;
    private void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL: " + message);
        checks++;
    }
    public void Execute(ExecutionResult result)
    {
        var offline = new GameObject("Offline stats validation");
        var replica = new GameObject("Network stats authority validation");
        result.RegisterObjectCreation(offline);
        result.RegisterObjectCreation(replica);
        try
        {
            var stats = offline.AddComponent<CharacterStats>();
            Check(stats.CanMutate && !stats.UsesNetworkClock, "offline behavior retained");
            Check(stats.ApplyDamage(25) == 25 && stats.CurrentHealth == 75, "existing damage path");
            Check(stats.Heal(10) == 10 && stats.CurrentHealth == 85, "existing healing path");
            Check(stats.TrySpendStamina(40), "authoritative stamina spending");
            Check(stats.TrySpendMana(50), "authoritative mana spending");
            stats.TickRegeneration(0.5f);
            Check(stats.CurrentStamina == 60 && stats.CurrentMana == 50, "regeneration delays");
            stats.TickRegeneration(1.5f);
            Check(stats.CurrentStamina == 80 && stats.CurrentMana == 50, "partial tick after stamina delay");
            stats.TickRegeneration(1);
            Check(stats.CurrentStamina == 100 && stats.CurrentMana == 55, "mana delayed regeneration");
            Check(!stats.TrySpendStamina(101) && !stats.TrySpendMana(56), "insufficient resources rejected");
            Check(!stats.TrySpendMana(float.NaN) && !stats.TrySpendStamina(-1), "invalid costs rejected");
            Check(stats.ApplyDamage(float.PositiveInfinity) == 0, "invalid damage rejected");
            int deaths = 0;
            stats.Died += () => deaths++;
            stats.ApplyDamage(1000); stats.ApplyDamage(1000);
            Check(stats.IsDead && deaths == 1, "single authoritative death");
            Check(stats.Heal(1000) == 0 && !stats.TrySpendStamina(0), "dead actor cannot heal or act");
            stats.ResetToFull();
            Check(!stats.IsDead && stats.CurrentHealth == 100, "explicit offline respawn retained");

            var adapter = typeof(CharacterStats).Assembly.GetType("HeroesOfDragonland.Multiplayer.NetworkCharacterStats", true);
            replica.AddComponent(adapter);
            var proxy = replica.GetComponent<CharacterStats>();
            Check(!proxy.CanMutate && proxy.UsesNetworkClock, "network actor fails closed before spawn");
            Check(proxy.ApplyDamage(10) == 0 && proxy.Heal(100) == 0, "client damage and healing blocked");
            proxy.ResetToFull(); proxy.ConfigureForSpawn(null);
            Check(proxy.CurrentHealth == 100, "client spawn configuration blocked");
            Check(!proxy.TrySpendStamina(1) && !proxy.TrySpendMana(1), "client resource writes blocked");
            result.Log("PASS: " + checks + " Stats integration/authority checks");
        }
        finally { result.DestroyObject(replica); result.DestroyObject(offline); }
    }
}

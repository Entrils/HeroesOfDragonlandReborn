using System;
using System.Linq;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Match;
using HeroesOfDragonland.Player;
using HeroesOfDragonland.Teams;
using HeroesOfDragonland.World;
using UnityEngine;
using UnityEngine.SceneManagement;

internal class CommandScript : IRunCommand
{
    private static void Check(bool value, string reason) { if (!value) throw new Exception("FAIL: " + reason); }
    public void Execute(ExecutionResult result)
    {
        var scene = SceneManager.GetActiveScene();
        Check(Application.isPlaying && scene.name == "KingBattlefieldTest", "Run in the King battlefield in Play Mode");
        var kings = UnityEngine.Object.FindObjectsByType<KingObjective>().Where(k => k.gameObject.scene == scene).ToArray();
        var guards = UnityEngine.Object.FindObjectsByType<FortressGuard>().Where(g => g.gameObject.scene == scene).ToArray();
        var player = UnityEngine.Object.FindObjectsByType<PlayerController>().Single(p => p.gameObject.scene == scene);
        var match = UnityEngine.Object.FindObjectsByType<MatchManager>().Single(m => m.gameObject.scene == scene);
        Check(kings.Length == 2 && kings.Count(k => k.Team == TeamId.TeamA) == 1 && kings.Count(k => k.Team == TeamId.TeamB) == 1,
            "exactly one King per team");
        Check(guards.Length == 2 && guards.Select(g => g.GetComponent<TeamIdentity>().Team).Distinct().Count() == 2, "both team guards present");
        Check(match.State == MatchState.Playing && kings.All(k => !k.IsDefeated && k.Stats.MaxHealth == 1000), "fresh playing match and King profiles");
        var identity = player.GetComponent<TeamIdentity>(); Check(identity != null && identity.HasTeam, "team-assigned player");
        var ally = kings.Single(k => k.Team == identity.Team);
        var enemy = kings.Single(k => k.Team != identity.Team);
        int events = 0; Action<TeamId> observer = winner => { events++; Check(winner == identity.Team, "reported winner"); };
        match.MatchEnded += observer;
        bool originalFriendlyFire = identity.AllowFriendlyFire;
        try
        {
            identity.TryConfigure(identity.Team, CombatantKind.Player, true);
            Check(ally.GetComponent<DamageReceiver>().TakeDamage(10000, player.gameObject) == 0, "King rejects explicit friendly fire");
            float remaining = enemy.Stats.CurrentHealth;
            Check(enemy.GetComponent<DamageReceiver>().TakeDamage(remaining, player.gameObject) == remaining, "actual battlefield enemy King killed");
            Check(match.State == MatchState.Finished && match.Winner == identity.Team && events == 1, "actual battlefield victory event");
            Check(ally.GetComponent<DamageReceiver>().TakeDamage(10000, enemy.gameObject) == 0 && !ally.IsDefeated, "further objective damage frozen");
            result.Log("PASS: battlefield winner " + match.Winner + ". Restart Play Mode and switch player team with TeamDebug to test the other team.");
        }
        finally { match.MatchEnded -= observer; identity.TryConfigure(identity.Team, CombatantKind.Player, originalFriendlyFire); }
    }
}

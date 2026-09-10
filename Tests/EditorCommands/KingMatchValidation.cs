using System;
using UnityEngine;
using UnityEditor;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Match;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Targeting;
using HeroesOfDragonland.Teams;
using HeroesOfDragonland.World;

internal class CommandScript : IRunCommand
{
    private static void Check(bool value, string label) { if (!value) throw new Exception("FAIL: " + label); }
    public void Execute(ExecutionResult result)
    {
        Check(Application.isPlaying, "Run in Play Mode");
        Run(TeamId.TeamA); Run(TeamId.TeamB);
        Check(!EditorUtility.scriptCompilationFailed, "Editor compilation");
        result.Log("PASS: both King victory paths, roster checks, high health, targetability, strict damage policy, waiting/finished gates, reentrant and one-shot events.");
    }
    private static void Set(UnityEngine.Object obj, string name, UnityEngine.Object value)
    { var so = new SerializedObject(obj); so.FindProperty(name).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    private static KingObjective King(Transform parent, TeamId team, CharacterStatsConfig config)
    {
        var actor = new GameObject(team + " test King"); actor.SetActive(false); actor.transform.SetParent(parent);
        actor.AddComponent<TeamIdentity>().TryConfigure(team, CombatantKind.NPC);
        var king = actor.AddComponent<KingObjective>(); Set(king, "configuration", config);
        actor.SetActive(true); return king;
    }
    private static GameObject Attacker(Transform parent, TeamId team)
    {
        var actor = new GameObject(team + " attacker"); actor.transform.SetParent(parent);
        actor.AddComponent<TeamIdentity>().TryConfigure(team, CombatantKind.Player, true); return actor;
    }
    private static void Run(TeamId defeated)
    {
        var root = new GameObject("King validation fixtures"); root.SetActive(false);
        var profile = ScriptableObject.CreateInstance<CharacterStatsConfig>();
        try
        {
            var cfg = new SerializedObject(profile); cfg.FindProperty("maxHealth").floatValue = 1000; cfg.ApplyModifiedPropertiesWithoutUndo();
            var a = King(root.transform, TeamId.TeamA, profile); var b = King(root.transform, TeamId.TeamB, profile);
            var sourceA = Attacker(root.transform, TeamId.TeamA); var sourceB = Attacker(root.transform, TeamId.TeamB);
            var go = new GameObject("Match under test"); go.transform.SetParent(root.transform);
            var match = go.AddComponent<MatchManager>();
            var config = new SerializedObject(match); config.FindProperty("startAutomatically").boolValue = false; config.ApplyModifiedPropertiesWithoutUndo();
            Set(match, "teamAKing", a); Set(match, "teamBKing", a);
            var victim = defeated == TeamId.TeamA ? a : b; var survivor = defeated == TeamId.TeamA ? b : a;
            var enemy = defeated == TeamId.TeamA ? sourceB : sourceA;
            var ally = defeated == TeamId.TeamA ? sourceA : sourceB;
            // Register BEFORE King Awake to test an earlier listener's reentrant hit.
            float reentrantDamage = -1;
            victim.GetComponent<CharacterStats>().Died += () => reentrantDamage = survivor.GetComponent<DamageReceiver>().TakeDamage(10000, ally);
            root.SetActive(true);
            Check(a.Stats.MaxHealth == 1000 && b.Stats.MaxHealth == 1000 && a.GetComponent<Targetable>().IsValid, "high health and targetable");
            Check(!match.TryStartMatch(out _) && match.State == MatchState.Waiting, "duplicate King roster rejected");
            Set(match, "teamBKing", b);
            b.GetComponent<TeamIdentity>().TryConfigure(TeamId.TeamA, CombatantKind.NPC);
            Check(!match.TryStartMatch(out _) && match.State == MatchState.Waiting, "wrong team roster rejected");
            b.GetComponent<TeamIdentity>().TryConfigure(TeamId.TeamB, CombatantKind.NPC);
            var receiver = victim.GetComponent<DamageReceiver>();
            Check(receiver.TakeDamage(50, enemy) == 0, "no objective damage before start");
            int ended = 0, finished = 0;
            float damageAfterFinish = -1;
            match.StateChanged += state => { if (state == MatchState.Finished) finished++; };
            match.MatchEnded += winner =>
            {
                ended++;
                Check(match.State == MatchState.Finished && winner == (defeated == TeamId.TeamA ? TeamId.TeamB : TeamId.TeamA), "winner committed before event");
                damageAfterFinish = survivor.GetComponent<DamageReceiver>().TakeDamage(10000, ally);
                Check(!match.TryStartMatch(out _), "reentrant restart rejected");
            };
            Check(match.TryStartMatch(out _) && !match.TryStartMatch(out _), "single start");
            Check(receiver.TakeDamage(100, ally) == 0, "King immune to friendly fire override");
            Check(receiver.TakeDamage(100, null) == 0 && receiver.TakeDamage(100, root) == 0, "environmental/unassigned sources rejected");
            match.enabled = false; Check(receiver.TakeDamage(100, enemy) == 0, "disabled manager gates objectives"); match.enabled = true;
            victim.enabled = false; Check(receiver.TakeDamage(100, enemy) == 0, "disabled King policy fails closed"); victim.enabled = true;
            var weapon = new GameObject("child weapon"); weapon.transform.SetParent(enemy.transform);
            Check(receiver.TakeDamage(100, weapon) == 100 && victim.Stats.CurrentHealth == 900 && match.State == MatchState.Playing, "enemy parent source accepted and nonlethal hit keeps Playing");
            bool healthObserverSawWinner = false;
            victim.Stats.HealthChanged += (hp, maximum) => { if (hp == 0) healthObserverSawWinner = match.State == MatchState.Finished; };
            Check(receiver.TakeDamage(10000, enemy) == 900, "lethal enemy damage");
            Check(match.State == MatchState.Finished && match.Winner == (defeated == TeamId.TeamA ? TeamId.TeamB : TeamId.TeamA)
                && ended == 1 && finished == 1 && healthObserverSawWinner, "immediate one-shot outcome");
            Check(reentrantDamage == 0 && damageAfterFinish == 0 && survivor.Stats.CurrentHealth == 1000, "second objective protected during and after death callbacks");
            Check(!victim.GetComponent<Targetable>().IsValid && receiver.TakeDamage(1, enemy) == 0, "dead King untargetable and cannot die twice");
            victim.Stats.ResetToFull();
            Check(receiver.TakeDamage(10000, enemy) == 0 && ended == 1, "resource reset cannot restart finished objectives");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(profile); }
    }
}

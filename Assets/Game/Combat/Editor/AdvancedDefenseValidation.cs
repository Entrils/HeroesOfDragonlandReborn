using System;
using System.Collections;
using System.Collections.Generic;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Teams;
using UnityEditor;
using UnityEngine;

namespace HeroesOfDragonland.Combat.Editor
{
    public static class AdvancedDefenseValidation
    {
        public static string Status { get; private set; } = "Not run";
        private static void Check(bool condition, string label)
        { if (!condition) throw new InvalidOperationException("Advanced defense: " + label); }
        private static void Near(float a, float b, string label) => Check(Mathf.Abs(a - b) < .005f, label + " (" + a + " / " + b + ")");
        public static void ValidateAssets()
        {
            var definition = AssetDatabase.LoadAssetAtPath<ParryDefinition>(AdvancedDefenseSetup.DefinitionPath);
            Check(definition != null && definition.IsValid, "valid shared parry timing");
            int players = 0, actors = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Game/Data" }))
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (var defense in root.GetComponentsInChildren<CombatDefense>(true))
                {
                    actors++; Check(defense.GetComponent<CharacterPoise>() != null, root.name + " poise component");
                    if (defense.GetComponent<NetworkPlayerCombat>() == null) continue;
                    players++; Check(defense.GetComponent<ParryDefense>()?.Definition?.IsValid == true, root.name + " parry reference");
                }
            }
            Check(players == 4 && actors > players, "four player prefabs and NPC defense components");
            Debug.Log("[AdvancedDefenseValidation] PASS valid parry data, all player references and " + actors + " poise combatants.");
        }
        public static void Start()
        {
            Check(Application.isPlaying, "Play Mode required"); ValidateAssets(); Status = "RUNNING";
            UnityEngine.Object.FindAnyObjectByType<MultiplayerBootstrap>().StartCoroutine(Run());
        }
        private static IEnumerator Run()
        {
            var temporary = new List<UnityEngine.Object>();
            try
            {
                var profile = ScriptableObject.CreateInstance<MeleeAttackDefinition>(); temporary.Add(profile);
                var timing = ScriptableObject.CreateInstance<ParryDefinition>(); temporary.Add(timing);
                var attacker = Actor("M44 attacker", new Vector3(0, 0, 100), profile, timing); temporary.Add(attacker);
                var defender = Actor("M44 defender", new Vector3(0, 0, 101.5f), profile, timing); temporary.Add(defender);
                defender.transform.rotation = Quaternion.LookRotation(Vector3.back);
                var a = attacker.GetComponent<MeleeAttack>(); var d = defender.GetComponent<MeleeAttack>();
                var ap = attacker.GetComponent<CombatDefense>(); var dp = defender.GetComponent<CombatDefense>();
                var hp = defender.GetComponent<CharacterStats>(); var asp = attacker.GetComponent<CharacterStats>();
                var poise = defender.GetComponent<CharacterPoise>(); var parry = defender.GetComponent<ParryDefense>();
                Action reset = () => { Reset(attacker); Reset(defender); };
                Action strike = () => { Physics.SyncTransforms(); Check(a.TryAttack(), "real swing accepted"); a.TickAttack(profile.ActiveStartTime + .001f); };
                // Success is a real overlap/hit-window callback, never a fabricated parry RPC.
                reset(); Check(parry.TryStart(Vector3.back), "parry accepted");
                Near(hp.CurrentStamina, hp.MaxStamina - timing.StaminaCost, "parry pays once");
                Check(!parry.TryStart(Vector3.back), "held/repeated parry cannot restart"); dp.Tick(timing.Startup + .001f);
                int successes = parry.SuccessSequence; strike();
                Check(parry.SuccessSequence == successes + 1 && !a.IsBusy && ap.IsStaggered && ap.LastInterrupt == DefenseInterrupt.Parried, "parry interrupts and staggers attacker");
                Near(hp.CurrentHealth, hp.MaxHealth, "parry prevents HP damage"); Near(poise.Current, poise.Maximum, "parry prevents poise damage");
                Check(!parry.IsActive && parry.IsBusy, "successful window consumed; recovery retained");
                Reset(attacker); strike(); Check(parry.SuccessSequence == successes + 1 && hp.CurrentHealth < hp.MaxHealth, "one success per attempt");
                reset(); Check(parry.TryStart(Vector3.back), "startup fixture"); strike();
                Check(parry.SuccessSequence == successes + 1 && hp.CurrentHealth < hp.MaxHealth, "startup is not a parry window");
                reset(); Check(parry.TryStart(Vector3.back), "late fixture"); dp.Tick(timing.Startup + timing.Window + .001f);
                Check(!parry.IsActive, "active window closes after endpoint; elapsed=" + parry.Elapsed.ToString("R") + " end=" + (timing.Startup + timing.Window).ToString("R"));
                strike(); Check(hp.CurrentHealth < hp.MaxHealth, "late hit is not parried");
                reset(); Check(parry.TryStart(Vector3.forward), "rear fixture"); dp.Tick(timing.Startup + .001f); strike();
                Check(hp.CurrentHealth < hp.MaxHealth && a.IsBusy, "rear hit bypasses frontal parry");
                reset(); Check(d.TryAttack() && !parry.TryStart(Vector3.back), "parry cannot cancel an attack");
                reset(); hp.TrySpendStamina(hp.CurrentStamina); Check(!parry.TryStart(Vector3.back), "insufficient stamina rejects parry");
                reset(); Check(parry.TryStart(Vector3.back), "cooldown fixture"); dp.Tick(timing.Duration + .001f);
                Check(!parry.IsBusy && !parry.TryStart(Vector3.back), "recovery end does not bypass cooldown"); dp.Tick(parry.CooldownRemaining + .001f);
                Check(parry.TryStart(Vector3.back), "new attempt after full cooldown");
                // Existing block chip/upkeep and event-driven exhaustion integrate with guard break.
                reset(); dp.SetBlockIntent(true, false); strike();
                Near(hp.CurrentHealth, hp.MaxHealth - profile.Damage * dp.BlockedDamageMultiplier, "block chip damage");
                Near(hp.CurrentStamina, hp.MaxStamina - profile.Damage * dp.BlockStaminaPerDamage, "block impact cost");
                Near(poise.Current, poise.Maximum, "blocked hit does not consume poise");
                int guard = dp.GuardBreakSequence; hp.TrySpendStamina(hp.CurrentStamina);
                Check(dp.GuardBreakSequence == guard + 1 && !dp.IsBlocking && dp.LastInterrupt == DefenseInterrupt.GuardBreak, "any blocking stamina exhaustion breaks guard once");
                Near(dp.StaggerRemaining, dp.GuardBreakDuration, "configurable guard-break duration");
                hp.ResetToFull(); dp.Tick(dp.GuardBreakDuration + dp.RecoveryImmunity + .01f); dp.SetBlockIntent(true, false);
                Check(!dp.IsBlocking, "guard must be released after break"); dp.SetBlockIntent(false, false); dp.SetBlockIntent(true, false); Check(dp.IsBlocking, "released guard can recover");
                reset(); dp.SetBlockIntent(true, false); guard = dp.GuardBreakSequence;
                dp.Tick(hp.CurrentStamina / dp.BlockStaminaPerSecond + .001f);
                Check(dp.GuardBreakSequence == guard + 1 && dp.IsStaggered, "upkeep exhaustion breaks guard");
                reset(); dp.ApplyPoiseDamage(20); Near(poise.Current, poise.Maximum - 20, "poise damage");
                dp.Tick(poise.RegenDelay - .01f); Near(poise.Current, poise.Maximum - 20, "poise waits for regen delay");
                dp.Tick(.51f); Near(poise.Current, poise.Maximum - 20 + poise.RegenPerSecond * .5f, "partial-tick regen after delay");
                dp.ApplyPoiseDamage(5); Near(poise.RegenDelayRemaining, poise.RegenDelay, "new damage resets regen delay");
                reset(); Check(d.TryAttack(), "poise interruption fixture"); int stuns = dp.StaggerSequence;
                dp.ApplyPoiseDamage(poise.Maximum * .5f); Check(!dp.IsStaggered && d.IsBusy, "subthreshold poise hit preserves attack");
                dp.ApplyPoiseDamage(poise.Maximum * .5f);
                Check(dp.IsStaggered && !d.IsBusy && !d.HasBufferedAttack && dp.StaggerSequence == stuns + 1, "threshold interrupts attack and buffered chain");
                Near(poise.Current, poise.Maximum, "poise resets on break to prevent immediate rebreak");
                float firstDuration = dp.StaggerRemaining; dp.Tick(.1f); dp.ApplyPoiseDamage(10000);
                Near(dp.StaggerRemaining, firstDuration - .1f, "hits cannot extend an active stagger");
                dp.Tick(dp.StaggerRemaining + .01f); Check(!dp.IsStaggered && dp.ImmunityRemaining > 0, "post-stagger control interval");
                dp.ApplyPoiseDamage(10000); Check(!dp.IsStaggered && dp.StaggerSequence == stuns + 1, "protected interval rejects poise damage");
                dp.SetBlockIntent(true, false); hp.TrySpendStamina(hp.CurrentStamina);
                Check(!dp.IsStaggered && !dp.IsBlocking, "guard exhaustion during protection drops block without another stun");
                reset(); stuns = dp.StaggerSequence; int freeFrames = 0;
                for (int i = 0; i < 100; i++) { dp.ApplyPoiseDamage(10000); dp.Tick(.05f); if (!dp.IsStaggered) freeFrames++; }
                Check(freeFrames > 40 && dp.StaggerSequence - stuns <= 4, "continuous hits cannot permanently stagger-lock");
                // A protected attacker still loses the parried swing, but its stun timer cannot be renewed.
                reset(); ap.ApplyPoiseDamage(10000); ap.Tick(ap.StaggerRemaining + .01f); a.TickAttack(a.RestartRemaining + .01f);
                int priorStuns = ap.StaggerSequence; Check(parry.TryStart(Vector3.back), "protected attacker fixture"); dp.Tick(timing.Startup + .001f); strike();
                Check(!a.IsBusy && !ap.IsStaggered && ap.StaggerSequence == priorStuns, "parry interrupts without bypassing recovery immunity");
                reset(); Check(parry.TryStart(Vector3.back), "forged source fixture"); dp.Tick(timing.Startup + .001f);
                successes = parry.SuccessSequence; defender.GetComponent<DamageReceiver>().TakeDamage(1, attacker);
                Check(parry.SuccessSequence == successes, "busy/source identity alone cannot fabricate a successful parry");
                reset(); attacker.AddComponent<TeamIdentity>().TryConfigure(TeamId.TeamA, CombatantKind.Player);
                defender.AddComponent<TeamIdentity>().TryConfigure(TeamId.TeamA, CombatantKind.Player);
                Check(parry.TryStart(Vector3.back), "ally fixture"); dp.Tick(timing.Startup + .001f); successes = parry.SuccessSequence; strike();
                Check(parry.SuccessSequence == successes && hp.CurrentHealth == hp.MaxHealth, "team policy precedes parry/cost/poise");
                yield return null;
                Status = "PASS real parries/startup/end/rear/cost/cooldown; block/guard-break/upkeep; poise delay/threshold; protected control intervals under continuous hits; safe interruption and team policy.";
                Debug.Log("[AdvancedDefenseValidation] " + Status);
            }
            finally
            {
                for (int i = temporary.Count - 1; i >= 0; i--) if (temporary[i] != null) UnityEngine.Object.Destroy(temporary[i]);
                if (Status == "RUNNING") Status = "FAILED; see Console";
            }
        }
        private static GameObject Actor(string name, Vector3 position, MeleeAttackDefinition profile, ParryDefinition parry)
        {
            var actor = new GameObject(name); actor.SetActive(false); actor.transform.position = position;
            actor.AddComponent<DamageReceiver>(); var melee = actor.AddComponent<MeleeAttack>(); var defense = actor.AddComponent<ParryDefense>();
            var d = new SerializedObject(melee); d.FindProperty("definition").objectReferenceValue = profile; d.ApplyModifiedPropertiesWithoutUndo();
            d = new SerializedObject(defense); d.FindProperty("definition").objectReferenceValue = parry; d.ApplyModifiedPropertiesWithoutUndo();
            var collider = actor.AddComponent<BoxCollider>(); collider.center = Vector3.up; collider.size = new Vector3(.6f, 1.8f, .6f);
            actor.SetActive(true); return actor;
        }
        private static void Reset(GameObject actor)
        {
            var defense = actor.GetComponent<CombatDefense>(); defense.enabled = false; defense.enabled = true;
            var melee = actor.GetComponent<MeleeAttack>(); melee.enabled = false; melee.enabled = true;
            actor.GetComponent<CharacterStats>().ResetToFull(); melee.TickAttack(melee.RestartRemaining + .001f);
        }
    }
}

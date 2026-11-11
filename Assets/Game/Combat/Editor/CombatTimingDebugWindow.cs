using System;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Stats;
using UnityEditor;
using UnityEngine;

namespace HeroesOfDragonland.Combat.Editor
{
    public sealed class CombatTimingDebugWindow : EditorWindow
    {
        private CombatDefense selected;
        private MeleeAttack attack;
        private ParryDefense parry;
        private bool trace;
        private Vector2 scroll;
        [MenuItem("Heroes of Dragonland/Combat/Timing Debugger")]
        public static void Open() => GetWindow<CombatTimingDebugWindow>("Combat timing");
        private void OnEnable() { Selection.selectionChanged += Selected; EditorApplication.update += Refresh; Selected(); }
        private void OnDisable() { Unsubscribe(); Selection.selectionChanged -= Selected; EditorApplication.update -= Refresh; }
        private void Refresh() { if (Application.isPlaying) Repaint(); }
        private void Selected()
        {
            Unsubscribe(); selected = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<CombatDefense>() : null;
            attack = selected != null ? selected.GetComponent<MeleeAttack>() : null;
            parry = selected != null ? selected.GetComponent<ParryDefense>() : null;
            if (trace) Subscribe(); Repaint();
        }
        private void Subscribe()
        {
            if (selected != null) selected.Interrupted += Interrupted;
            if (attack != null) { attack.AttackStarted += AttackStarted; attack.AttackFinished += AttackFinished; }
            if (parry != null) { parry.AttemptStarted += ParryStarted; parry.Successful += ParrySuccess; }
        }
        private void Unsubscribe()
        {
            if (selected != null) selected.Interrupted -= Interrupted;
            if (attack != null) { attack.AttackStarted -= AttackStarted; attack.AttackFinished -= AttackFinished; }
            if (parry != null) { parry.AttemptStarted -= ParryStarted; parry.Successful -= ParrySuccess; }
        }
        private void Log(string value)
        {
            if (selected == null) return;
            var net = selected.GetComponent<NetworkPlayerCombat>();
            Debug.Log("[CombatTiming] " + selected.name + " time=" + Time.time.ToString("F3")
                + (net != null && net.Object != null && net.Object.IsValid ? " tick=" + net.Runner.Tick.Raw : " offline") + " " + value, selected);
        }
        private void Interrupted(DefenseInterrupt reason) => Log("interrupt=" + reason + " stagger=" + selected.StaggerRemaining.ToString("F3") + " protection=" + selected.ImmunityRemaining.ToString("F3"));
        private void AttackStarted() => Log("attack start index=" + attack.MoveIndex + " heavy=" + attack.IsHeavy + " swing=" + attack.SwingDuration.ToString("F3"));
        private void AttackFinished() => Log("attack end elapsed=" + attack.AttackElapsed.ToString("F3"));
        private void ParryStarted() => Log("parry attempt=" + parry.AttemptSequence);
        private void ParrySuccess() => Log("parry success=" + parry.SuccessSequence + " elapsed=" + parry.Elapsed.ToString("F3"));
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Select a gameplay combatant or its visual child.", EditorStyles.wordWrappedLabel);
            if (selected == null) { EditorGUILayout.HelpBox("No CombatDefense on the selected actor.", MessageType.Info); return; }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.ObjectField("Combatant", selected, typeof(CombatDefense), true);
            var stats = selected.GetComponent<CharacterStats>(); var net = selected.GetComponent<NetworkPlayerCombat>();
            var enemy = selected.GetComponent<NetworkEnemy>(); var poise = selected.GetComponent<CharacterPoise>();
            if (net != null && (net.Object == null || !net.Object.IsValid)) net = null;
            if (enemy != null && (enemy.Object == null || !enemy.Object.IsValid)) enemy = null;
            var networkStats = selected.GetComponent<NetworkCharacterStats>();
            bool replica = Application.isPlaying && stats.UsesNetworkClock && networkStats != null
                && (networkStats.Object == null || !networkStats.Object.IsValid || !networkStats.HasStateAuthority || !networkStats.Runner.IsServer);
            EditorGUILayout.LabelField("Authority", replica ? "Replica snapshots (no local defense simulation)" : "Server / offline simulation");
            bool nextTrace = EditorGUILayout.Toggle("Trace selected events to Console", trace);
            if (nextTrace != trace) { Unsubscribe(); trace = nextTrace; if (trace) Subscribe(); }
            EditorGUILayout.Space(); EditorGUILayout.LabelField("Block / interruption settings", EditorStyles.boldLabel);
            Value("Blocked damage fraction", selected.BlockedDamageMultiplier);
            Value("Stamina per damage / per second", selected.BlockStaminaPerDamage, selected.BlockStaminaPerSecond);
            Value("Poise stagger / guard-break duration", selected.StaggerDuration, selected.GuardBreakDuration);
            Value("Post-stagger protection", selected.RecoveryImmunity);
            if (Application.isPlaying)
            {
                float current = replica ? net != null ? net.Poise : enemy != null ? enemy.Poise : 0 : poise != null ? poise.Current : 0;
                float maximum = replica ? net != null ? net.MaxPoise : enemy != null ? enemy.MaxPoise : 0 : poise != null ? poise.Maximum : 0;
                Bar("Poise", current, maximum);
                Value("Poise regen delay / rate", stats.PoiseRegenDelay, stats.PoiseRegenPerSecond);
                Value("Poise regen countdown", replica ? net != null ? net.PoiseRegenRemaining : enemy != null ? enemy.PoiseRegenRemaining : 0 : poise != null ? poise.RegenDelayRemaining : 0);
                Value("Stagger / protection remaining", replica ? net != null ? net.StaggerRemaining : enemy != null ? enemy.StaggerRemaining : 0 : selected.StaggerRemaining,
                    replica ? net != null ? net.StaggerImmunityRemaining : enemy != null ? enemy.StaggerImmunityRemaining : 0 : selected.ImmunityRemaining);
                if (replica && net == null && enemy == null) EditorGUILayout.HelpBox("This actor has no public defense snapshot. Inspect its authoritative instance.", MessageType.Info);
            }
            if (parry != null)
            {
                EditorGUILayout.Space(); EditorGUILayout.ObjectField("Parry settings (C)", parry.Definition, typeof(ParryDefinition), false);
                if (parry.Definition != null)
                {
                    Value("Parry active interval", parry.Definition.Startup, parry.Definition.Startup + parry.Definition.Window);
                    Value("Total recovery end / cooldown", parry.Definition.Duration, parry.Definition.Cooldown);
                    Value("Parry cost / frontal arc", parry.Definition.StaminaCost, parry.Definition.Arc);
                    Value("Attacker stagger", parry.Definition.AttackerStagger);
                    if (Application.isPlaying && !replica) { Bar("Parry timeline", parry.Elapsed, parry.Definition.Duration); Value("Cooldown remaining", parry.CooldownRemaining); }
                    if (replica && net != null) EditorGUILayout.LabelField("Accepted parry", "attempt=" + net.ParrySequence + " success=" + net.ParrySuccessSequence + " active=" + net.Parrying + " busy=" + net.ParryBusy);
                }
            }
            if (attack != null && attack.Definition != null)
            {
                EditorGUILayout.Space(); EditorGUILayout.LabelField("Attack timing", EditorStyles.boldLabel);
                if (replica) EditorGUILayout.HelpBox("Attack profile below is the local content preview. Accepted index/duration come from snapshots.", MessageType.Info);
                float scale = attack.SwingDuration / Mathf.Max(.001f, attack.Definition.AttackDuration);
                Value("Hit window", attack.Definition.ActiveStartTime * scale, attack.Definition.ActiveEndTime * scale);
                Value("Swing / recovery", attack.SwingDuration, attack.RecoveryDuration);
                Value("Poise damage", attack.PoiseDamage);
                if (Application.isPlaying && !replica)
                {
                    Bar("Attack timeline", attack.AttackElapsed, attack.SwingDuration + attack.RecoveryDuration);
                    EditorGUILayout.LabelField("Buffer / dodge cancel", attack.HasBufferedAttack + " / " + attack.CanDodgeCancel);
                    Value("Restart lockout", attack.RestartRemaining);
                }
                if (net != null && net.Object != null && net.Object.IsValid)
                    EditorGUILayout.LabelField("Accepted attack", "sequence=" + net.AttackSequence + " index=" + net.AttackMoveIndex + " tick=" + net.AttackStartTick + " duration=" + net.AttackSwingDuration.ToString("F3"));
            }
            EditorGUILayout.EndScrollView();
        }
        private static void Value(string name, float value) => EditorGUILayout.LabelField(name, value.ToString("F3"));
        private static void Value(string name, float a, float b) => EditorGUILayout.LabelField(name, a.ToString("F3") + " / " + b.ToString("F3"));
        private static void Bar(string name, float value, float max)
            => EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 20), max > 0 ? Mathf.Clamp01(value / max) : 0, name + " " + value.ToString("F3") + " / " + max.ToString("F3"));
    }
}

using System;
using System.Text;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.EquipmentSystem;
using HeroesOfDragonland.Progression;
using HeroesOfDragonland.Stats;
using UnityEngine;

namespace HeroesOfDragonland.Skills
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SessionProgression), typeof(CharacterAttributes), typeof(CharacterStats))]
    public sealed class SessionSkills : MonoBehaviour, IInvulnerabilityProvider
    {
        public const int SkillCount = 3;
        [SerializeField] private SkillDefinition[] definitions = new SkillDefinition[SkillCount];
        private readonly float[] cooldowns = new float[SkillCount];
        private SessionProgression progression;
        private CharacterStats stats;
        private CharacterAttributes attributes;
        private int highestLevel, learnedMask;
        private float guardRemaining;
        private bool processing;
        public string SessionId { get; private set; }
        public int SkillPoints { get; private set; }
        public int LearnedMask => learnedMask;
        public float GuardRemaining => guardRemaining;
        public bool HasAuthority => GetComponent<ISessionSkillAuthority>()?.CanMutateSkills ?? stats.CanMutate;
        public bool IsInvulnerable => isActiveAndEnabled && HasAuthority && !stats.IsDead && guardRemaining > 0;
        public event Action Changed;
        public SkillDefinition GetDefinition(int index) => index >= 0 && index < SkillCount && definitions != null
            && definitions.Length == SkillCount ? definitions[index] : null;
        public bool IsLearned(int index) => index >= 0 && index < SkillCount && (learnedMask & (1 << index)) != 0;
        public float CooldownRemaining(int index) => index >= 0 && index < SkillCount ? cooldowns[index] : 0;

        private void Awake()
        {
            progression = GetComponent<SessionProgression>(); stats = GetComponent<CharacterStats>();
            attributes = GetComponent<CharacterAttributes>();
        }
        private void OnEnable()
        {
            progression.SessionStarted += OnSession;
            progression.Changed += OnProgression;
            stats.Died += OnDeath;
            OnProgression();
        }
        private void OnDisable()
        {
            progression.SessionStarted -= OnSession; progression.Changed -= OnProgression; stats.Died -= OnDeath;
            guardRemaining = 0;
        }
        private void OnDeath() { guardRemaining = 0; Notify(); }
        private void OnSession(string id)
        {
            // SessionStarted can fire during network spawn before the match is playing.
            // Only the progression authority may establish the reset boundary.
            if (!progression.HasAuthority || SessionId == id) return;
            SessionId = id; highestLevel = 1; SkillPoints = 1; learnedMask = 0;
            guardRemaining = 0; Array.Clear(cooldowns, 0, cooldowns.Length); Notify();
        }
        private void OnProgression()
        {
            if (!progression.HasAuthority || !progression.IsInitialized) return;
            OnSession(progression.SessionId);
            if (progression.Level <= highestLevel) return;
            SkillPoints += progression.Level - highestLevel; highestLevel = progression.Level; Notify();
        }
        public string MissingRequirements(int index)
        {
            var definition = GetDefinition(index);
            if (definition == null || !definition.IsValid) return "Skill is not configured.";
            var missing = new StringBuilder();
            if (!progression.IsInitialized || progression.SessionId != SessionId) missing.AppendLine("No active skill session.");
            if (progression.Level < definition.RequiredLevel) missing.AppendLine("Level: " + progression.Level + " / " + definition.RequiredLevel);
            foreach (var requirement in definition.Requirements)
            {
                bool known = attributes.TryGetBaseValue(requirement.Stat, out float current);
                if (!known || current < requirement.MinimumValue)
                    missing.AppendLine(requirement.Stat + ": " + (known ? current.ToString("0.##") : "unknown") + " / " + requirement.MinimumValue);
            }
            return missing.ToString().TrimEnd();
        }
        public bool CanLearn(int index, out string reason)
        {
            reason = MissingRequirements(index);
            if (reason.Length > 0) return false;
            if (IsLearned(index)) reason = "Already learned.";
            else if (stats.IsDead) reason = "Cannot learn while dead.";
            else if (SkillPoints < GetDefinition(index).PointCost) reason = "Not enough skill points.";
            return reason.Length == 0;
        }
        public bool TryLearn(int index, string sessionId, out string reason)
        {
            reason = "Skill learning requires the current session's authority.";
            if (!isActiveAndEnabled || !HasAuthority || processing || sessionId != SessionId) return false;
            if (!CanLearn(index, out reason)) return false;
            processing = true;
            try
            {
                SkillPoints -= GetDefinition(index).PointCost; learnedMask |= 1 << index; Notify(); return true;
            }
            finally { processing = false; }
        }
        public bool CanActivate(int index, out string reason)
        {
            reason = MissingRequirements(index);
            if (reason.Length > 0) return false;
            var definition = GetDefinition(index);
            if (!IsLearned(index)) reason = "Learn this skill first.";
            else if (definition.Kind != SkillKind.Active) reason = "Passive skills are always applied while learned.";
            else if (stats.IsDead) reason = "Cannot use skills while dead.";
            else if (cooldowns[index] > 0) reason = "Skill is on cooldown.";
            else if (stats.CurrentMana < definition.ManaCost) reason = "Not enough Mana.";
            else if (GetComponent<MeleeAttack>()?.IsBusy == true || GetComponent<HeroesOfDragonland.Multiplayer.NetworkPlayerCombat>()?.AttackBusy == true
                || GetComponent<HeroesOfDragonland.Multiplayer.NetworkPlayerDodge>()?.Busy == true
                || GetComponent<HeroesOfDragonland.Player.PlayerDodge>()?.IsBusy == true) reason = "Finish your current action first.";
            else if (definition.Effect == SkillEffect.MeleeStrike && GetComponent<MeleeAttack>()?.Definition == null) reason = "Melee attack is not configured.";
            return reason.Length == 0;
        }
        public bool TryActivate(int index, string sessionId, out string reason)
        {
            reason = "Skill activation requires the current session's authority.";
            if (!isActiveAndEnabled || !HasAuthority || processing || sessionId != SessionId) return false;
            if (!CanActivate(index, out reason)) return false;
            processing = true;
            try
            {
                var definition = GetDefinition(index);
                // No client target, hit, damage amount, cost, or duration is accepted.
                if (definition.Effect == SkillEffect.MeleeStrike)
                {
                    var combat = GetComponent<HeroesOfDragonland.Multiplayer.NetworkPlayerCombat>();
                    bool started = combat != null ? combat.TryStartSkillStrike(definition.Magnitude, definition.ManaCost)
                        : GetComponent<MeleeAttack>().TrySkillAttack(definition.Magnitude, definition.ManaCost);
                    if (!started) { reason = "Melee action is unavailable."; return false; }
                }
                else if (!stats.TrySpendMana(definition.ManaCost)) { reason = "Mana payment failed."; return false; }
                cooldowns[index] = definition.Cooldown;
                if (definition.Effect == SkillEffect.Guard) guardRemaining = definition.Magnitude;
                Notify(); return true;
            }
            finally { processing = false; }
        }
        public float ModifyMeleeDamage(float damage)
        {
            if (!isActiveAndEnabled || !HasAuthority || stats.IsDead) return damage;
            for (int i = 0; i < SkillCount; i++)
            {
                var definition = GetDefinition(i);
                if (IsLearned(i) && definition != null && definition.Effect == SkillEffect.MeleeTraining
                    && MissingRequirements(i).Length == 0) damage += definition.Magnitude;
            }
            return damage;
        }
        private void Update() { if (GetComponent<ISessionSkillAuthority>() == null) Tick(Time.deltaTime); }
        public void Tick(float deltaTime)
        {
            if (!HasAuthority || deltaTime <= 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            bool changed = false;
            for (int i = 0; i < SkillCount; i++)
                if (cooldowns[i] > 0) { cooldowns[i] = Mathf.Max(0, cooldowns[i] - deltaTime); changed = true; }
            if (guardRemaining > 0) { guardRemaining = stats.IsDead ? 0 : Mathf.Max(0, guardRemaining - deltaTime); changed = true; }
            if (changed) Notify();
        }
        internal void ApplySnapshot(string sessionId, int points, int mask, float guard, float[] remaining)
        {
            if (GetComponent<ISessionSkillAuthority>() == null || HasAuthority || remaining == null || remaining.Length != SkillCount) return;
            SessionId = sessionId; SkillPoints = points; learnedMask = mask; guardRemaining = guard;
            Array.Copy(remaining, cooldowns, SkillCount); Notify();
        }
        private void Notify()
        {
            if (Changed == null) return;
            foreach (Action listener in Changed.GetInvocationList())
                try { listener(); } catch (Exception exception) { Debug.LogException(exception, this); }
        }
    }
}

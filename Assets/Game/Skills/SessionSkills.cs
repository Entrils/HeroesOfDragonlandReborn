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
        public const int SkillCount = 6;
        [SerializeField] private SkillDefinition[] definitions = new SkillDefinition[SkillCount];
        public const int Capacity = 24;
        [SerializeField] private SkillTreeDefinition tree;
        private readonly float[] cooldowns = new float[Capacity];
        private readonly byte[] ranks = new byte[Capacity];
        private bool validTree;
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
        public event Action RanksChanged;
        public int NodeCount => tree != null ? (validTree ? tree.Count : 0) : SkillCount;
        public SkillDefinition GetDefinition(int index)
        {
            if (index < 0 || index >= NodeCount) return null;
            return tree != null ? tree.GetNode(index) : (definitions != null && index < definitions.Length ? definitions[index] : null);
        }

        public int GetRank(int index) => index >= 0 && index < NodeCount ? ranks[index] : 0;
        public bool IsLearned(int index) => GetRank(index) > 0;
        public float CooldownRemaining(int index) => index >= 0 && index < NodeCount ? cooldowns[index] : 0;

        private void Awake()
        {
            validTree = tree != null && tree.IsValid();
            if (tree != null && !validTree) Debug.LogError("Invalid session skill tree.", this);
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
            GetComponent<HeroesOfDragonland.Abilities.AbilityCaster>()?.Clear();
            guardRemaining = 0;
            Array.Clear(cooldowns, 0, cooldowns.Length);
            Array.Clear(ranks, 0, ranks.Length);
            RanksChanged?.Invoke();
            Notify();
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
            var definition = GetDefinition(index);
            if (GetRank(index) >= definition.MaxRank) reason = "Maximum rank reached.";
            else if (stats.IsDead) reason = "Cannot learn while dead.";
            else if (tree != null)
            {
                foreach (var prerequisite in definition.Prerequisites)
                    if (GetRank(tree.IndexOf(prerequisite.Skill)) < prerequisite.Rank)
                    {
                        reason = $"Requires {prerequisite.Skill.DisplayName} rank {prerequisite.Rank}.";
                        break;
                    }
                if (reason.Length == 0 && !string.IsNullOrEmpty(definition.ExclusiveGroup))
                    for (int i = 0; i < NodeCount; i++)
                        if (i != index && IsLearned(i) && GetDefinition(i).ExclusiveGroup == definition.ExclusiveGroup)
                        {
                            reason = $"Cannot be combined with {GetDefinition(i).DisplayName}.";
                            break;
                        }
            }
            if (reason.Length == 0 && SkillPoints < definition.PointCost) reason = "Not enough skill points.";
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
                SkillPoints -= GetDefinition(index).PointCost;
                ranks[index]++;
                learnedMask |= 1 << index;
                RanksChanged?.Invoke();
                Notify();
                return true;
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
            else if (GetComponent<HeroesOfDragonland.Abilities.AbilityCaster>() is { CastRemaining: > 0 }) reason = "Already casting.";
            else if (GetComponent<CombatDefense>()?.ActionsBlocked == true) reason = "Finish your current action first.";
            else if (cooldowns[index] > 0) reason = "Skill is on cooldown.";
            else if (stats.CurrentMana < ManaCost(definition))
                reason = HeroesOfDragonland.Abilities.AbilityCaster.ManaRequired(stats.CurrentMana, ManaCost(definition));
            else if (GetComponent<MeleeAttack>()?.IsBusy == true || GetComponent<HeroesOfDragonland.Multiplayer.NetworkPlayerCombat>()?.AttackBusy == true
                || GetComponent<HeroesOfDragonland.Multiplayer.NetworkPlayerDodge>()?.Busy == true
                || GetComponent<HeroesOfDragonland.Player.PlayerDodge>()?.IsBusy == true) reason = "Finish your current action first.";
            else if (definition.Effect == SkillEffect.MeleeStrike && GetComponent<MeleeAttack>()?.Definition == null) reason = "Melee attack is not configured.";
            if (reason.Length == 0 && definition.Effect == SkillEffect.Ability)
            {
                var caster = GetComponent<HeroesOfDragonland.Abilities.AbilityCaster>();
                if (caster == null) reason = "Ability caster is missing.";
                else caster.CanStart(definition.Ability, definition.Ability.Target == HeroesOfDragonland.Abilities.AbilityTarget.SelfOrAlly
                    ? caster.SelectHealingTarget(definition.Ability) : null, out reason);
            }
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
                if (definition.Effect == SkillEffect.Ability)
                {
                    var caster = GetComponent<HeroesOfDragonland.Abilities.AbilityCaster>();
                    var target = definition.Ability.Target == HeroesOfDragonland.Abilities.AbilityTarget.SelfOrAlly
                        ? caster.SelectHealingTarget(definition.Ability) : null;
                    if (!caster.TryStart(definition.Ability, target, out reason)) return false;
                }
                else if (definition.Effect == SkillEffect.MeleeStrike)
                {
                    var combat = GetComponent<HeroesOfDragonland.Multiplayer.NetworkPlayerCombat>();
                    bool started = combat != null ? combat.TryStartSkillStrike(definition.Magnitude, definition.ManaCost)
                        : GetComponent<MeleeAttack>().TrySkillAttack(definition.Magnitude, definition.ManaCost);
                    if (!started) { reason = "Melee action is unavailable."; return false; }
                }
                else if (!stats.TrySpendMana(definition.ManaCost))
                { reason = HeroesOfDragonland.Abilities.AbilityCaster.ManaRequired(stats.CurrentMana, definition.ManaCost); return false; }
                cooldowns[index] = Cooldown(definition);
                if (definition.Effect == SkillEffect.Guard) guardRemaining = definition.Magnitude;
                Notify(); return true;
            }
            finally { processing = false; }
        }
        public float ModifyMeleeDamage(float damage)
        {
            if (!isActiveAndEnabled || !HasAuthority || stats.IsDead) return damage;
            return damage + GetBonus(SkillEffect.MeleeTraining);
        }

        public float GetBonus(SkillEffect effect)
        {
            if (!isActiveAndEnabled) return 0;
            float total = 0;
            for (int i = 0; i < NodeCount; i++)
                if (ranks[i] > 0 && GetDefinition(i) is { Kind: SkillKind.Passive } node && node.Effect == effect)
                    total += node.Magnitude * ranks[i];
            return total;
        }

        public float CostMultiplier(SkillEffect effect) => 1f - Mathf.Clamp(GetBonus(effect), 0f, .75f);
        public float ManaCost(SkillDefinition skill) => skill.ManaCost
            * (skill.Effect == SkillEffect.Ability ? CostMultiplier(SkillEffect.SpellEfficiency) : 1f);
        public float Cooldown(SkillDefinition skill) => skill.Cooldown
            * (skill.Effect == SkillEffect.Ability ? CostMultiplier(SkillEffect.SpellCooldown) : 1f);
        private void Update() { if (GetComponent<ISessionSkillAuthority>() == null) { Tick(Time.deltaTime); GetComponent<HeroesOfDragonland.Abilities.AbilityCaster>()?.Tick(Time.deltaTime); } }
        public void Tick(float deltaTime)
        {
            if (!HasAuthority || deltaTime <= 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            bool changed = false;
            for (int i = 0; i < NodeCount; i++)
                if (cooldowns[i] > 0) { cooldowns[i] = Mathf.Max(0, cooldowns[i] - deltaTime); changed = true; }
            if (guardRemaining > 0) { guardRemaining = stats.IsDead ? 0 : Mathf.Max(0, guardRemaining - deltaTime); changed = true; }
            if (changed) Notify();
        }
        internal void ApplySnapshot(string sessionId, int points, int mask, float guard, float[] remaining, byte[] newRanks)
        {
            if (GetComponent<ISessionSkillAuthority>() == null || HasAuthority || remaining.Length != Capacity || newRanks.Length != Capacity) return;
            SessionId = sessionId; SkillPoints = points; learnedMask = mask; guardRemaining = guard;
            bool ranksChanged = false;
            for (int i = 0; i < Capacity; i++)
            {
                ranksChanged |= ranks[i] != newRanks[i];
                ranks[i] = newRanks[i];
            }
            Array.Copy(remaining, cooldowns, Capacity);
            if (ranksChanged) RanksChanged?.Invoke();
            Notify();
        }
        private void Notify()
        {
            if (Changed == null) return;
            foreach (Action listener in Changed.GetInvocationList())
                try { listener(); } catch (Exception exception) { Debug.LogException(exception, this); }
        }
    }
}

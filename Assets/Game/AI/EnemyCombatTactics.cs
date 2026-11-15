using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Stats;
using UnityEngine;

namespace HeroesOfDragonland.AI
{
    // Decisions only. Perception, authority, attacks, leash and death remain in the shared brain.
    internal sealed class EnemyCombatTactics
    {
        private readonly EnemyBrain brain;
        private readonly EnemyNavigation navigation;
        private readonly EnemyMeleeAttack attack;
        private readonly CharacterStats stats;
        private float clock, repath;
        private bool recoveringMana;
        private bool retreatBlocked;
        public EnemyCombatTactics(EnemyBrain brain, EnemyNavigation navigation, EnemyMeleeAttack attack, CharacterStats stats)
        { this.brain = brain; this.navigation = navigation; this.attack = attack; this.stats = stats; }
        public void Tick(float dt) { clock += dt; repath -= dt; }
        public void Reset() { recoveringMana = false; brain.GetComponent<CombatDefense>()?.SetBlockIntent(false, false); }
        public bool TryAct(EnemyThreatTarget target, bool visible, float dt)
        {
            var d = brain.Definition;
            var defense = brain.GetComponent<CombatDefense>();
            Vector3 offset = brain.transform.position - target.NavigationPosition; offset.y = 0;
            float distance = offset.magnitude;
            Vector3 away = distance > .01f ? offset / distance : -brain.transform.forward;
            if (d.Archetype == EnemyArchetype.Knight)
            {
                bool guard = visible && distance < d.AttackRange * 1.5f && clock % 3.2f < 1.2f;
                defense?.SetBlockIntent(guard, attack.IsBusy);
                if (guard) { navigation.Stop(); navigation.Face(target.NavigationPosition, dt); return true; }
            }
            if (d.Archetype == EnemyArchetype.Mage)
            {
                if (stats.CurrentMana < d.SpellManaCost) recoveringMana = true;
                if (stats.CurrentMana >= Mathf.Min(stats.MaxMana, d.SpellManaCost * 2)) recoveringMana = false;
            }
            bool retreat = d.IsRanged && (distance < d.PreferredDistance * .7f || recoveringMana);
            bool circle = d.Archetype == EnemyArchetype.Wolf && visible && attack.CooldownRemaining > .15f && distance < d.AttackRange * 2;
            if (!retreat && !circle) return false;
            navigation.Face(target.NavigationPosition, dt);
            if (repath <= 0)
            {
                repath = .35f;
                Vector3 tangent = Vector3.Cross(Vector3.up, away);
                Vector3 destination = target.NavigationPosition + away * (d.IsRanged ? d.PreferredDistance : d.AttackRange * .9f) + tangent * (circle ? 1.2f : 2f);
                // Failed retreat is not a reason to drop aggro. Try the other side, then hold.
                retreatBlocked = Vector3.Distance(destination, brain.SpawnPosition) > d.LeashDistance ||
                    !navigation.MoveTo(destination, d.ChaseSpeed, .2f);
                if (retreatBlocked)
                {
                    destination = target.NavigationPosition + away * d.PreferredDistance - tangent * 2f;
                    retreatBlocked = Vector3.Distance(destination, brain.SpawnPosition) > d.LeashDistance || !navigation.MoveTo(destination, d.ChaseSpeed, .2f);
                    if (retreatBlocked) navigation.Stop();
                }
            }
            // A cornered archer can still shoot; an exhausted mage must regenerate.
            return recoveringMana || circle || (!retreatBlocked && distance < d.PreferredDistance * .7f);
        }
    }
}

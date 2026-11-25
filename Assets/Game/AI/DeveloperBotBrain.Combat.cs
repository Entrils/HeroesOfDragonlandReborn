#if UNITY_EDITOR || DEBUG
using Fusion;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.Abilities;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Skills;
using HeroesOfDragonland.Stats;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    public sealed partial class DeveloperBotBrain
    {
        private enum DefenseChoice { Block, Dodge, Parry }
        private NetworkId telegraphActor;
        private int telegraphSequence;
        private float noticedAt;
        private bool defended;
        private DefenseChoice defenseChoice;

        private bool TryDefend(NetworkObject preferred, ref Vector3 facing, ref Vector3 move, out bool block, out bool dodge)
        {
            block = dodge = false;
            NetworkObject threat = preferred;
            if (!Threat(threat, out int sequence, out float impact, out bool parryable))
            {
                threat = null;
                foreach (var enemy in enemies)
                    if (Threat(enemy.Object, out sequence, out impact, out parryable))
                    { threat = enemy.Object; break; }
                if (threat == null)
                    foreach (var enemy in visibleCreatures)
                        if (Threat(enemy.Object, out sequence, out impact, out parryable))
                        { threat = enemy.Object; break; }
            }
            if (threat == null) return false;
            if (telegraphActor != threat.Id || telegraphSequence != sequence)
            {
                telegraphActor = threat.Id;
                telegraphSequence = sequence;
                noticedAt = age;
                defended = false;
                defenseChoice = parryable && parry?.Definition != null && Chance(Tuning.parryChance) ? DefenseChoice.Parry
                    : Chance(Tuning.dodgeChance) ? DefenseChoice.Dodge : DefenseChoice.Block;
            }
            if (age - noticedAt < Tuning.reactionSeconds || impact > .48f) return false;

            facing = Planar(threat.transform.position - player.transform.position).normalized;
            if (facing.sqrMagnitude < .01f) facing = player.transform.forward;
            if (defended) return true;
            if (combat.IsBusy && (defenseChoice != DefenseChoice.Dodge || !combat.CanDodgeCancel)) return true;

            if (defenseChoice == DefenseChoice.Parry && impact >= .02f && parry?.Definition != null &&
                impact <= parry.Definition.Startup + parry.Definition.Window * .55f)
            {
                parryPress = unchecked(parryPress + 1);
                defended = true;
                return true;
            }
            if (defenseChoice == DefenseChoice.Dodge && age >= dodgeAt && roll?.Definition != null && !roll.Busy
                && stats.CurrentStamina >= roll.Definition.StaminaCost && impact <= roll.InvulnerabilityStartTime + .09f)
            {
                dodge = true;
                defended = true;
                dodgeAt = age + .8f;
                move = Goal is DeveloperBotGoal.Retreat or DeveloperBotGoal.NeutralBoss ? -facing
                    : Vector3.Cross(Vector3.up, facing) * (Chance(.5f) ? 1 : -1);
                return true;
            }

            bool waitingForTimedDefense = defenseChoice != DefenseChoice.Block && impact > .07f;
            block = !waitingForTimedDefense && Stamina > .16f && equipment?.EquippedWeapon?.Archetype != WeaponArchetype.Bow;
            return true;
        }

        private bool Threat(NetworkObject actor, out int sequence, out float impact, out bool parryable)
        {
            sequence = 0;
            impact = 0;
            parryable = false;
            if (!VisibleLiving(actor)) return false;
            float dt = player.Runner.DeltaTime;
            if (actor.TryGetComponent<NetworkPlayerCombat>(out var melee))
            {
                if (!melee.AttackBusy || melee.Definition is not { } profile) return false;
                Vector3 offset = Planar(player.transform.position - actor.transform.position);
                float reach = actor.GetComponent<HeroesOfDragonland.EquipmentSystem.Equipment>()?.EquippedWeapon?.Range
                    ?? profile.HitCenter.z + profile.HitHalfExtents.z;
                if (offset.sqrMagnitude > (reach + .85f) * (reach + .85f) || Vector3.Angle(melee.AttackFacing, offset) > 72)
                    return false;
                sequence = melee.AttackSequence;
                impact = melee.AttackSwingDuration * (profile.ActiveStartTime / profile.AttackDuration)
                    - (player.Runner.Tick.Raw - melee.AttackStartTick) * dt;
                parryable = true;
            }
            else if (actor.TryGetComponent<NetworkEnemy>(out var enemy))
            {
                if (!enemy.AttackBusy || enemy.Definition == null) return false;
                var set = enemy.PresentedAttackSet;
                if (set != null && set.Count > 0)
                {
                    var attack = set.Get(enemy.AttackIndex);
                    if (!attack.Contains(enemy.AttackOrigin, enemy.AttackForward, player.transform.position)) return false;
                }
                else if (Distance(enemy.transform.position) > enemy.Definition.AttackRange + .75f) return false;
                sequence = enemy.AttackSequence;
                impact = enemy.AttackWindup - (player.Runner.Tick.Raw - enemy.AttackStartTick) * dt;
                parryable = enemy.Definition.Boss == null && !enemy.Definition.IsRanged;
            }
            else return false;
            return impact >= -.10f && impact <= .9f;
        }

        private void ImproveLoadout()
        {
            if (AutomatedMatchSimulation.Active == null) MatchSimulationBotLoadout.Improve(player);
            EquipmentQuality = 0;
            if (equipment != null)
                for (var slot = EquipmentSlot.MainHand; slot <= EquipmentSlot.Legs; slot++)
                    if (equipment.GetItem(slot) is { } item)
                        EquipmentQuality += item.Rarity == ItemRarity.Common ? .07f : .16f;
            EquipmentQuality = Mathf.Clamp01(EquipmentQuality);
            if (skills == null || skills.SkillPoints <= 0) return;
            // Learn a usable ability or its prerequisites before spending spare points on more ranks.
            int selected = -1, best = -1;
            for (int i = 0; i < skills.NodeCount; i++)
            {
                var skill = skills.GetDefinition(i);
                if (skill == null || !skills.CanLearn(i, out _)) continue;
                int priority = skill.Effect == SkillEffect.Ability ? 5 : skills.GetRank(i) == 0 ? 3 : 1;
                if (priority > best) { selected = i; best = priority; }
            }
            if (selected >= 0) skills.TryLearn(selected, skills.SessionId, out _);
        }

        private void TryConsumable(bool fighting)
        {
            if (inventory == null) return;
            if (Health < .55f && inventory.TryUseBotConsumable(ConsumableEffect.Health)
                || fighting && Stamina < .29f && inventory.TryUseBotConsumable(ConsumableEffect.Stamina)
                || Mana < .32f && inventory.TryUseBotConsumable(ConsumableEffect.Mana)
                || fighting && Health < .67f && inventory.TryUseBotConsumable(ConsumableEffect.Defense)
                || fighting && inventory.TryUseBotConsumable(ConsumableEffect.Damage))
                ConsumablesUsed++;
        }

        private bool TryAbility(bool fighting, float distance, Vector3 facing)
        {
            if (skills == null || age < nextAbility || combat.IsBusy || combat.ActionsBlocked || roll != null && roll.Busy
                || !fighting && Health >= .7f && allies.Count == 0) return false;
            nextAbility = age + .8f;
            if (!Chance(Tuning.abilityChance)) return false;
            for (int i = 0; i < skills.NodeCount; i++)
            {
                var skill = skills.GetDefinition(i);
                if (skill == null || !skill.IsValid || skill.Kind != SkillKind.Active || !skills.IsLearned(i) || skills.CooldownRemaining(i) > 0)
                    continue;
                bool suitable = skill.Effect switch
                {
                    SkillEffect.MeleeStrike => fighting && distance <= AttackDistance(false),
                    SkillEffect.Guard => fighting && Health < .85f && distance < 4,
                    SkillEffect.Ability => skill.Ability.Effect switch
                    {
                        AbilityEffect.FireProjectile => fighting && distance < skill.Ability.Range * .9f
                            && Vector3.Dot(player.transform.forward, facing) > .96f,
                        AbilityEffect.Shield => fighting && distance < 7 && Health < .85f,
                        AbilityEffect.Heal => Health < .67f || allies.Exists(ally =>
                            Distance(ally.transform.position) < skill.Ability.Range && senses.CanSee(ally.transform, Tuning)
                            && ally.GetComponent<CharacterStats>() is { } vital && Fraction(vital.CurrentHealth, vital.MaxHealth) < .55f),
                        _ => false
                    },
                    _ => false
                };
                if (!suitable || !skills.TryActivate(i, skills.SessionId, out _)) continue;
                AbilitiesUsed++;
                return true;
            }
            return false;
        }
    }
}
#endif

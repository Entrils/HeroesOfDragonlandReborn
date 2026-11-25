#if UNITY_EDITOR || DEBUG
using Fusion;
using HeroesOfDragonland.Abilities;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.EquipmentSystem;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Progression;
using HeroesOfDragonland.Skills;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.World;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    public enum DeveloperBotGoal
    {
        Roam, EnemyPlayer, NeutralCamp, Chest, FortressGuard, King, FortressGate, NeutralBoss,
        Retreat, AssistAlly, ContestChest, DefendFortress, PushFortress, ScoutCamp, Search
    }

    public sealed partial class DeveloperBotBrain
    {
        private static readonly Unity.Profiling.ProfilerMarker TickMarker = new("Dragonland.BotTick");
        private static readonly DeveloperBotTuning Defaults = new();
        private readonly NetworkTestPlayer player;
        private readonly DeveloperBotNavigation navigation = new();
        private readonly DeveloperBotSenses senses;
        private readonly DeveloperBotService service;
        private readonly TeamSpawnService spawnService;
        private readonly CharacterStats stats;
        private readonly NetworkPlayerCombat combat;
        private readonly NetworkPlayerDodge roll;
        private readonly ParryDefense parry;
        private readonly Equipment equipment;
        private readonly NetworkInventory inventory;
        private readonly SessionProgression progression;
        private readonly SessionSkills skills;
        private readonly AbilityCaster caster;
        private readonly System.Random random;
        private readonly Vector3 home;
        private float age, think, dodgeAt, chestAt, inspectAt, nextAttackPress, nextLoadout, nextItem, nextAbility;
        private bool targetVisible;
        private uint lightPress, heavyPress, parryPress;
        private NetworkObject target;
        private Vector3 destination, previousPosition;
        private DeveloperBotTuning Tuning => service != null ? service.Tuning ?? Defaults : Defaults;

        public DeveloperBotGoal Goal { get; private set; }
        public int ChestOpens { get; private set; }
        public int ConsumablesUsed { get; private set; }
        public int AbilitiesUsed { get; private set; }
        public string LastChestResult { get; private set; }
        public float DistanceTravelled { get; private set; }
        public Vector3 Destination => destination;
        public bool WantsTravel { get; private set; }
        public bool HasNavigationPath => navigation.HasPath;

        public DeveloperBotBrain(NetworkTestPlayer owner)
        {
            player = owner;
            stats = owner.GetComponent<CharacterStats>();
            combat = owner.GetComponent<NetworkPlayerCombat>();
            roll = owner.GetComponent<NetworkPlayerDodge>();
            parry = owner.GetComponent<ParryDefense>();
            equipment = owner.GetComponent<Equipment>();
            inventory = owner.GetComponent<NetworkInventory>();
            progression = owner.GetComponent<SessionProgression>();
            skills = owner.GetComponent<SessionSkills>();
            caster = owner.GetComponent<AbilityCaster>();
            service = owner.Runner.GetComponentInParent<DeveloperBotService>();
            spawnService = owner.Runner.GetComponentInParent<TeamSpawnService>();
            senses = new DeveloperBotSenses(owner);
            home = previousPosition = destination = owner.transform.position;
            random = new System.Random(owner.ParticipantNumber * 7919 + owner.SpawnLane * 127);
            think = owner.SpawnLane * .06f;
        }

        public NetworkPlayerInput Tick(float deltaTime)
        {
            using var marker = TickMarker.Auto();
            WantsTravel = false;
            if (!NetworkParticipant.IsAdmitted(player.Object) || !NetworkMatch.AllowsGameplay(player.Runner)) return default;
            if (stats.IsDead)
            {
                target = null; seenTarget = default; goalUntil = 0; think = 0;
                previousPosition = player.transform.position;
                return default;
            }
            float travelled = Vector3.Distance(previousPosition, player.transform.position);
            if (travelled < 2) DistanceTravelled += travelled;
            previousPosition = player.transform.position;
            age += deltaTime;
            think -= deltaTime;
            if (think <= 0)
            {
                if (age >= nextLoadout)
                {
                    nextLoadout = age + 2;
                    ImproveLoadout();
                }
                SelectGoal();
                think = Mathf.Max(.1f, Tuning.decisionInterval);
                inspectAt = age;
            }
            if (age >= inspectAt)
            {
                inspectAt = age + .10f;
                targetVisible = Visible(target);
                if (targetVisible) Observe(target);
                else if (target != null) think = 0;
            }

            bool chestGoal = Goal is DeveloperBotGoal.Chest or DeveloperBotGoal.ContestChest;
            bool targetAttackable = targetVisible && !chestGoal && VisibleLiving(target);
            var gate = targetVisible && Goal == DeveloperBotGoal.FortressGate ? target.GetComponent<Collider>() : null;
            Vector3 approach = gate != null ? gate.ClosestPoint(player.transform.position) : destination;
            Vector3 offset = Planar(approach - player.transform.position);
            float distance = offset.magnitude;
            Vector3 facing = distance > .05f ? offset / distance : player.transform.forward;
            bool usingBow = equipment?.EquippedWeapon?.Archetype == WeaponArchetype.Bow;
            float stop = chestGoal ? 1.35f : gate != null ? .85f : !targetAttackable ? 1.25f : AttackDistance(usingBow);
            Vector3 move = distance > stop ? navigation.Steer(player, approach, deltaTime) : Vector3.zero;
            WantsTravel = distance > stop + .4f;
            if (move.sqrMagnitude > .01f && !targetAttackable) facing = move;

            bool defending = TryDefend(targetAttackable ? target : null, ref facing, ref move, out bool block, out bool dodge);
            if (age >= nextItem && !defending)
            {
                nextItem = age + .65f;
                TryConsumable(targetAttackable);
            }
            bool activated = !defending && TryAbility(targetAttackable, distance, facing);
            if (defending || activated || caster != null && caster.IsCasting)
            {
                WantsTravel = false;
                if (!dodge) move = Vector3.zero;
            }
            else if (targetAttackable && distance <= stop + .18f && age >= nextAttackPress && Stamina > .12f && !combat.ActionsBlocked)
            {
                nextAttackPress = age + Mathf.Max(.12f, Tuning.attackInterval);
                if (!usingBow && Stamina > .53f && Chance(Tuning.heavyChance + (gate != null ? .12f : 0)))
                    heavyPress = unchecked(heavyPress + 1);
                else lightPress = unchecked(lightPress + 1);
            }

            if (chestGoal && targetVisible && !defending && distance < 2.5f && age >= chestAt)
            {
                chestAt = age + 2;
                if (target.TryGetComponent<NetworkLootChest>(out var chest))
                {
                    bool opened = chest.TryOpenForBot(player.Object, out var reason);
                    LastChestResult = reason;
                    if (opened) { ChestOpens++; think = 0; nextLoadout = 0; }
                }
            }

            Vector3 bowAim = Vector3.zero;
            if (usingBow && targetAttackable)
            {
                var aim = target.GetComponentInChildren<HeroesOfDragonland.Targeting.Targetable>();
                Vector3 point = aim != null ? aim.AimPosition : target.transform.position + Vector3.up * .5f;
                bowAim = (point - BowShot.Origin(player.transform)).normalized;
            }
            return new NetworkPlayerInput
            {
                Move = new Vector2(move.x, move.z), Facing = new Vector2(facing.x, facing.z).normalized,
                LightAttackPress = lightPress, HeavyAttackPress = heavyPress, ParryPress = parryPress,
                Dodge = dodge, Block = block, BowAim = bowAim
            };
        }

        private float AttackDistance(bool usingBow)
        {
            var weapon = equipment?.EquippedWeapon;
            if (usingBow) return Mathf.Clamp(weapon.Range * .7f, 3f, Tuning.sightRange * .8f);
            float radius = 0;
            if (target != null && target.TryGetComponent<Collider>(out var collider))
                radius = Mathf.Min(collider.bounds.extents.x, collider.bounds.extents.z);
            var profile = combat.Definition;
            float range = weapon != null ? weapon.Range * .8f : profile != null ? profile.HitCenter.z + profile.HitHalfExtents.z - .35f : 1;
            return Mathf.Max(1.15f, radius + range);
        }

        internal bool CanObserve(Transform actor) => senses.CanSee(actor, Tuning);

        private bool Chance(float chance) => random.NextDouble() < Mathf.Clamp01(chance);
    }
}
#endif

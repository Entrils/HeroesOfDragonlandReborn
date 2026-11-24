#if UNITY_EDITOR || DEBUG
using Fusion;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Teams;
using HeroesOfDragonland.World;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    public enum DeveloperBotGoal { Roam, EnemyPlayer, NeutralCamp, Chest, FortressGuard, King, FortressGate, NeutralBoss }

    // Simple server decision loop. No transforms, health or inventory bypasses.
    public sealed class DeveloperBotBrain
    {
        private static readonly Unity.Profiling.ProfilerMarker TickMarker = new("Dragonland.BotTick");
        private readonly NetworkTestPlayer player;
        private readonly DeveloperBotNavigation navigation = new DeveloperBotNavigation();
        private readonly CharacterStats stats;
        private readonly NetworkPlayerCombat combat;
        private readonly NetworkPlayerDodge roll;
        private readonly EnemyThreatTarget threatTarget;
        private float age, think, dodgeAt, chestAt;
        private NetworkObject target;
        private Vector3 destination;
        public DeveloperBotGoal Goal { get; private set; }
        public int ChestOpens { get; private set; }
        public string LastChestResult { get; private set; }
        public float DistanceTravelled { get; private set; }
        public Vector3 Destination => destination;
        public bool WantsTravel { get; private set; }
        public bool HasNavigationPath => navigation.HasPath;
        private Vector3 previousPosition;
        private uint attackPress;
        private float nextAttackPress;
        public DeveloperBotBrain(NetworkTestPlayer owner)
        {
            player = owner; stats = owner.GetComponent<CharacterStats>(); combat = owner.GetComponent<NetworkPlayerCombat>();
            roll = owner.GetComponent<NetworkPlayerDodge>();
            threatTarget = owner.GetComponent<EnemyThreatTarget>();
            previousPosition = owner.transform.position;
            dodgeAt = 3 + owner.SpawnLane;
        }
        public NetworkPlayerInput Tick(float deltaTime)
        {
            using var marker = TickMarker.Auto();
            WantsTravel = false;
            if (!NetworkParticipant.IsAdmitted(player.Object) || !NetworkMatch.AllowsGameplay(player.Runner) || stats.IsDead) return default;
            DistanceTravelled += Vector3.Distance(previousPosition, player.transform.position); previousPosition = player.transform.position;
            age += deltaTime; think -= deltaTime;
            if (think <= 0 || !ValidTarget()) { SelectGoal(); think = .5f; }
            if (target != null && target.IsValid) destination = target.transform.position;
            var gateCollider = Goal == DeveloperBotGoal.FortressGate && target != null ? target.GetComponent<Collider>() : null;
            bool gate = gateCollider != null;
            var approach = gate ? gateCollider.ClosestPoint(player.transform.position) : destination;
            Vector3 facing = approach - player.transform.position; facing.y = 0;
            float distance = facing.magnitude;
            float stop = gate ? .9f : Goal == DeveloperBotGoal.Chest ? 1.3f : AttackDistance();
            WantsTravel = distance > stop + .5f;
            bool canAttack = Goal == DeveloperBotGoal.EnemyPlayer || Goal == DeveloperBotGoal.NeutralCamp
                || Goal == DeveloperBotGoal.FortressGuard || Goal == DeveloperBotGoal.King || Goal == DeveloperBotGoal.FortressGate
                || Goal == DeveloperBotGoal.NeutralBoss;
            bool bossThreat = BossWindupThreat(out var timeToImpact);
            bool preparingDefense = bossThreat && timeToImpact < .75f;
            bool readyToRoll = preparingDefense && roll != null && roll.Definition != null && !roll.Busy
                && !combat.IsBusy && !combat.ActionsBlocked
                && stats.CurrentStamina >= roll.Definition.StaminaCost;
            bool bossDodge = preparingDefense && readyToRoll
                && timeToImpact <= roll.Definition.InvulnerabilityStartTime + .08f;
            bool block = preparingDefense && !readyToRoll && timeToImpact < .45f;
            if (canAttack && !preparingDefense && distance <= stop + .25f && age >= nextAttackPress)
            { attackPress = unchecked(attackPress + 1); nextAttackPress = age + .22f; }
            Vector3 move = distance > stop && !preparingDefense ? navigation.Steer(player, approach, deltaTime) : Vector3.zero;
            bool dodge = canAttack && !gate && Goal != DeveloperBotGoal.NeutralBoss && distance < 6 && age >= dodgeAt;
            if (dodge) { dodgeAt = age + 5 + player.SpawnLane; move = Vector3.Cross(Vector3.up, facing.normalized); }
            if (bossDodge) move = -facing.normalized;
            if (preparingDefense) WantsTravel = false;
            if (Goal == DeveloperBotGoal.Chest && distance < 2.5f && age >= chestAt)
            {
                chestAt = age + 3;
                var chest = target != null ? target.GetComponent<NetworkLootChest>() : null;
                if (chest != null)
                {
                    bool opened = chest.TryOpenForBot(player.Object, out var reason);
                    LastChestResult = reason;
                    if (opened) { ChestOpens++; think = 0; }
                }
            }
            return new NetworkPlayerInput {
                Move = new Vector2(move.x, move.z), Facing = new Vector2(facing.x, facing.z).normalized,
                Attack = canAttack && !preparingDefense && distance <= stop + .25f && !dodge,
                LightAttackPress = attackPress,
                Dodge = dodge || bossDodge,
                Block = block
            };
        }

        private bool BossWindupThreat(out float timeToImpact)
        {
            timeToImpact = 0;
            if (Goal != DeveloperBotGoal.NeutralBoss || target == null || threatTarget == null
                || !target.TryGetComponent<NetworkEnemy>(out var boss) || !boss.AttackBusy) return false;
            var attackSet = boss.PresentedAttackSet;
            if (attackSet == null || attackSet.Count == 0) return false;
            var move = attackSet.Get(boss.AttackIndex);
            timeToImpact = move.Windup - (player.Runner.Tick.Raw - boss.AttackStartTick) * player.Runner.DeltaTime;
            return timeToImpact >= 0 && move.Contains(boss.AttackOrigin, boss.AttackForward, threatTarget.NavigationPosition);
        }
        private float AttackDistance()
        {
            float radius = 0;
            var collider = target != null ? target.GetComponent<Collider>() : null;
            if (collider != null) radius = Mathf.Min(collider.bounds.extents.x, collider.bounds.extents.z);
            return Mathf.Max(1.25f, radius + .85f);
        }
        private bool ValidTarget()
        {
            if (target == null || !target.IsValid || target.Runner != player.Runner) return Goal == DeveloperBotGoal.Roam;
            var health = target.GetComponent<CharacterStats>();
            if (health != null && health.IsDead) return false;
            var chest = target.GetComponent<NetworkLootChest>();
            return chest == null || !chest.IsOpen;
        }
        private float Distance(Vector3 point)
        {
            var delta = point - player.transform.position; delta.y = 0; return delta.magnitude;
        }
        private bool Eligible(NetworkObject actor)
        {
            if (actor == null || actor == player.Object || !actor.IsValid || actor.Runner != player.Runner) return false;
            var spawn = player.Runner.GetComponentInParent<TeamSpawnService>();
            if (actor.GetComponent<NetworkTestPlayer>() != null && spawn != null && !spawn.AllowsSpawn(player.Team, actor.transform.position)) return false;
            var health = actor.GetComponent<CharacterStats>();
            return health != null && !health.IsDead && !TeamIdentity.AreAllies(TeamIdentity.Find(player.gameObject), TeamIdentity.Find(actor.gameObject));
        }
        private void SelectGoal()
        {
            target = null;
            float playing = NetworkMatchPacing.MatchFor(player.Runner)?.PlayingSeconds ?? 0;
            if (playing < 300 && player.SpawnLane != 0 && TrySelectBoss()) return;
            // One bot per side keeps pressure on the objective once the opening farm is over.
            // Its teammates still clear guards and engage the enemy ahead of it.
            bool siegeLead = AutomatedMatchSimulation.Active != null && player.SpawnLane == 0 && playing > 120;
            float nearest = siegeLead ? 4 : 9;
            foreach (var enemy in Object.FindObjectsByType<NetworkTestPlayer>())
                if (Eligible(enemy.Object) && enemy.Team != player.Team && Distance(enemy.transform.position) < nearest)
                { nearest = Distance(enemy.transform.position); target = enemy.Object; }
            if (target != null) { Goal = DeveloperBotGoal.EnemyPlayer; return; }
            nearest = siegeLead ? 4 : 14;
            if (age >= chestAt) foreach (var chest in Object.FindObjectsByType<NetworkLootChest>())
                if (chest.Object != null && chest.Object.IsValid && chest.Runner == player.Runner && !chest.IsOpen && Distance(chest.transform.position) < nearest)
                { nearest = Distance(chest.transform.position); target = chest.Object; }
            if (target != null) { Goal = DeveloperBotGoal.Chest; return; }
            bool farm = !siegeLead && (age + player.SpawnLane * 13) % 100 < 45;
            nearest = float.PositiveInfinity;
            if (farm) foreach (var enemy in Object.FindObjectsByType<NetworkEnemy>())
                if (Eligible(enemy.Object) && enemy.GetComponent<NetworkFortressGuard>() == null && Distance(enemy.transform.position) < nearest)
                { nearest = Distance(enemy.transform.position); target = enemy.Object; }
            if (target != null) { Goal = DeveloperBotGoal.NeutralCamp; return; }
            // Siege needs to remove the real closed gate, rather than path indefinitely
            // toward an objective behind it. Damage still uses the ordinary melee policy.
            nearest = float.PositiveInfinity;
            foreach (var gate in Object.FindObjectsByType<NetworkFortressGate>())
                if (gate.Team != player.Team && !gate.IsOpen && Eligible(gate.Object) && Distance(gate.transform.position) < nearest)
                { nearest = Distance(gate.transform.position); target = gate.Object; }
            if (target != null) { Goal = DeveloperBotGoal.FortressGate; return; }
            if (siegeLead && TrySelectKing()) return;
            nearest = float.PositiveInfinity;
            foreach (var guard in Object.FindObjectsByType<NetworkFortressGuard>())
            {
                var enemy = guard.GetComponent<NetworkEnemy>();
                if (guard.IsEnemy(player) && Eligible(enemy.Object) && Distance(guard.transform.position) < nearest)
                { nearest = Distance(guard.transform.position); target = enemy.Object; }
            }
            if (target != null) { Goal = DeveloperBotGoal.FortressGuard; return; }
            if (TrySelectKing()) return;
            Goal = DeveloperBotGoal.Roam;
            float angle = age * .05f + player.SpawnLane + (player.Team == TeamId.TeamB ? Mathf.PI : 0);
            destination = new Vector3(Mathf.Sin(angle) * 25, 1, Mathf.Cos(angle) * 25);
        }

        private bool TrySelectBoss()
        {
            foreach (var enemy in Object.FindObjectsByType<NetworkEnemy>())
            {
                if (enemy.Definition.Boss == null || !Eligible(enemy.Object)) continue;
                target = enemy.Object;
                Goal = DeveloperBotGoal.NeutralBoss;
                return true;
            }
            return false;
        }

        private bool TrySelectKing()
        {
            foreach (var king in Object.FindObjectsByType<NetworkKingObjective>())
            {
                if (king.Team == player.Team || !Eligible(king.Object)) continue;
                target = king.Object;
                Goal = DeveloperBotGoal.King;
                return true;
            }
            return false;
        }
    }
}
#endif

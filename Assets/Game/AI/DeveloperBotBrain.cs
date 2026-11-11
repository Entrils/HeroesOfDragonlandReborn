#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Fusion;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Teams;
using HeroesOfDragonland.World;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    public enum DeveloperBotGoal { Roam, EnemyPlayer, NeutralCamp, Chest, FortressGuard, King }

    // Simple server decision loop. No transforms, health or inventory bypasses.
    public sealed class DeveloperBotBrain
    {
        private readonly NetworkTestPlayer player;
        private readonly DeveloperBotNavigation navigation = new DeveloperBotNavigation();
        private readonly CharacterStats stats;
        private readonly NetworkPlayerCombat combat;
        private float age, think, dodgeAt, chestAt;
        private NetworkObject target;
        private Vector3 destination;
        public DeveloperBotGoal Goal { get; private set; }
        public int ChestOpens { get; private set; }
        public string LastChestResult { get; private set; }
        public float DistanceTravelled { get; private set; }
        private Vector3 previousPosition;
        private uint attackPress;
        private float nextAttackPress;
        public DeveloperBotBrain(NetworkTestPlayer owner)
        {
            player = owner; stats = owner.GetComponent<CharacterStats>(); combat = owner.GetComponent<NetworkPlayerCombat>();
            previousPosition = owner.transform.position;
            dodgeAt = 3 + owner.ParticipantNumber % 5;
        }
        public NetworkPlayerInput Tick(float deltaTime)
        {
            if (!NetworkParticipant.IsAdmitted(player.Object) || !NetworkMatch.AllowsGameplay(player.Runner) || stats.IsDead) return default;
            DistanceTravelled += Vector3.Distance(previousPosition, player.transform.position); previousPosition = player.transform.position;
            age += deltaTime; think -= deltaTime;
            if (think <= 0 || !ValidTarget()) { SelectGoal(); think = .5f; }
            if (target != null && target.IsValid) destination = target.transform.position;
            Vector3 facing = destination - player.transform.position; facing.y = 0;
            float distance = facing.magnitude;
            float stop = Goal == DeveloperBotGoal.Chest ? 1.3f : AttackDistance();
            bool canAttack = Goal == DeveloperBotGoal.EnemyPlayer || Goal == DeveloperBotGoal.NeutralCamp
                || Goal == DeveloperBotGoal.FortressGuard || Goal == DeveloperBotGoal.King;
            if (canAttack && distance <= stop + .25f && age >= nextAttackPress)
            { attackPress = unchecked(attackPress + 1); nextAttackPress = age + .22f; }
            Vector3 move = distance > stop ? navigation.Steer(player, destination, deltaTime) : Vector3.zero;
            bool dodge = canAttack && distance < 6 && age >= dodgeAt;
            if (dodge) { dodgeAt = age + 5 + player.ParticipantNumber % 4; move = Vector3.Cross(Vector3.up, facing.normalized); }
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
                Attack = canAttack && distance <= stop + .25f && !dodge,
                LightAttackPress = attackPress,
                Dodge = dodge
            };
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
            float nearest = 9;
            foreach (var enemy in Object.FindObjectsByType<NetworkTestPlayer>(FindObjectsSortMode.None))
                if (Eligible(enemy.Object) && enemy.Team != player.Team && Distance(enemy.transform.position) < nearest)
                { nearest = Distance(enemy.transform.position); target = enemy.Object; }
            if (target != null) { Goal = DeveloperBotGoal.EnemyPlayer; return; }
            nearest = 14;
            if (age >= chestAt) foreach (var chest in Object.FindObjectsByType<NetworkLootChest>(FindObjectsSortMode.None))
                if (chest.Object != null && chest.Object.IsValid && chest.Runner == player.Runner && !chest.IsOpen && Distance(chest.transform.position) < nearest)
                { nearest = Distance(chest.transform.position); target = chest.Object; }
            if (target != null) { Goal = DeveloperBotGoal.Chest; return; }
            bool farm = (age + player.SpawnLane * 13) % 100 < 45;
            nearest = float.PositiveInfinity;
            if (farm) foreach (var enemy in Object.FindObjectsByType<NetworkEnemy>(FindObjectsSortMode.None))
                if (Eligible(enemy.Object) && enemy.GetComponent<NetworkFortressGuard>() == null && Distance(enemy.transform.position) < nearest)
                { nearest = Distance(enemy.transform.position); target = enemy.Object; }
            if (target != null) { Goal = DeveloperBotGoal.NeutralCamp; return; }
            nearest = float.PositiveInfinity;
            foreach (var guard in Object.FindObjectsByType<NetworkFortressGuard>(FindObjectsSortMode.None))
            {
                var enemy = guard.GetComponent<NetworkEnemy>();
                if (guard.IsEnemy(player) && Eligible(enemy.Object) && Distance(guard.transform.position) < nearest)
                { nearest = Distance(guard.transform.position); target = enemy.Object; }
            }
            if (target != null) { Goal = DeveloperBotGoal.FortressGuard; return; }
            foreach (var king in Object.FindObjectsByType<NetworkKingObjective>(FindObjectsSortMode.None))
                if (king.Team != player.Team && Eligible(king.Object)) { target = king.Object; Goal = DeveloperBotGoal.King; return; }
            Goal = DeveloperBotGoal.Roam;
            destination = new Vector3(Mathf.Sin(age * .05f + player.ParticipantNumber) * 25, 1, Mathf.Cos(age * .05f + player.ParticipantNumber) * 25);
        }
    }
}
#endif

#if UNITY_EDITOR || DEBUG
using System.Collections.Generic;
using Fusion;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Teams;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    public sealed partial class DeveloperBotBrain
    {
        private readonly List<NetworkTestPlayer> allies = new();
        private readonly List<NetworkTestPlayer> enemies = new();
        private readonly List<NetworkEnemy> visibleCreatures = new();
        private readonly Dictionary<NetworkId, float> campVisits = new();
        private NetworkId seenTarget;
        private Vector3 seenPosition;
        private float seenAt = float.NegativeInfinity;
        private Vector3 ownFortress;
        private bool hasOwnFortress;
        private float goalUntil;
        public int VisibleAllies => allies.Count;
        public int VisibleEnemies => enemies.Count;
        public int Level => progression?.Level ?? 1;
        public float EquipmentQuality { get; private set; }

        private float Health => Fraction(stats.CurrentHealth, stats.MaxHealth);
        private float Stamina => Fraction(stats.CurrentStamina, stats.MaxStamina);
        private float Mana => Fraction(stats.CurrentMana, stats.MaxMana);
        private bool SiegeReady => Level >= 3 || EquipmentQuality > .25f;

        private static float Fraction(float value, float maximum) => maximum > 0 ? Mathf.Clamp01(value / maximum) : 0;
        private float Distance(Vector3 point) => Planar(point - player.transform.position).magnitude;
        private static Vector3 Planar(Vector3 value) => new(value.x, 0, value.z);
        private bool SameWorld(NetworkObject actor) => actor != null && actor.IsValid && actor.Runner == player.Runner;

        private bool Visible(NetworkObject actor) => SameWorld(actor) && actor != player.Object && senses.CanSee(actor.transform, Tuning);
        private bool VisibleLiving(NetworkObject actor) => Visible(actor)
            && actor.GetComponent<CharacterStats>() is { IsDead: false };

        private void Observe(NetworkObject actor)
        {
            destination = actor.transform.position;
            seenTarget = actor.Id;
            seenPosition = destination;
            seenAt = age;
        }

        private void SelectGoal()
        {
            allies.Clear();
            enemies.Clear();
            foreach (var other in Object.FindObjectsByType<NetworkTestPlayer>())
            {
                if (!VisibleLiving(other.Object) || other.IsSuspended) continue;
                if (other.Team == player.Team) allies.Add(other);
                else if (other.Team is TeamId.TeamA or TeamId.TeamB &&
                    (spawnService == null || spawnService.AllowsSpawn(player.Team, other.transform.position))) enemies.Add(other);
            }

            hasOwnFortress = false;
            NetworkKingObjective enemyKing = null;
            foreach (var king in Object.FindObjectsByType<NetworkKingObjective>())
            {
                if (!SameWorld(king.Object)) continue;
                if (king.Team == player.Team) { ownFortress = king.transform.position; hasOwnFortress = true; }
                else enemyKing = king;
            }

            visibleCreatures.Clear();
            foreach (var enemy in Object.FindObjectsByType<NetworkEnemy>())
                if (VisibleLiving(enemy.Object) && enemy.Definition != null
                    && (!enemy.TryGetComponent<NetworkFortressGuard>(out var guard) || guard.IsEnemy(player)))
                    visibleCreatures.Add(enemy);

            Transform closestThreat = null;
            float nearbyDanger = 99;
            void SenseThreat(Transform actor)
            {
                float distance = Distance(actor.position);
                if (distance >= nearbyDanger) return;
                nearbyDanger = distance;
                closestThreat = actor;
            }
            foreach (var enemy in enemies) SenseThreat(enemy.transform);
            foreach (var creature in visibleCreatures)
                if (Distance(creature.transform.position) <= creature.Definition.AttackRange + 3)
                    SenseThreat(creature.transform);
            bool recovering = Goal == DeveloperBotGoal.Retreat && age < goalUntil && Health < Tuning.retreatHealth + .18f;
            if (recovering || Health < Tuning.retreatHealth && nearbyDanger < 9 || Health < .14f
                || nearbyDanger < 5 && Stamina < .13f && enemies.Count > allies.Count + 1)
            {
                if (!recovering)
                {
                    Vector3 cover = hasOwnFortress ? ownFortress : home;
                    if (Distance(cover) < 5 && closestThreat != null)
                        cover = player.transform.position + Planar(player.transform.position - closestThreat.position).normalized * 6;
                    Navigate(DeveloperBotGoal.Retreat, cover);
                    goalUntil = age + 4;
                }
                return;
            }

            NetworkObject choice = null;
            DeveloperBotGoal goal = DeveloperBotGoal.Roam;
            float best = float.NegativeInfinity;
            void Consider(NetworkObject actor, DeveloperBotGoal candidate, float score)
            {
                if (actor == target) score += 4; // Avoid trading equally good targets every decision.
                if (score <= best) return;
                choice = actor;
                goal = candidate;
                best = score;
            }

            foreach (var enemy in enemies)
            {
                bool defend = hasOwnFortress && Planar(enemy.transform.position - ownFortress).sqrMagnitude < 16 * 16;
                bool assist = allies.Exists(ally => Planar(ally.transform.position - enemy.transform.position).sqrMagnitude < 7 * 7);
                float score = 65 - Distance(enemy.transform.position) * 2
                    + (defend ? 20 : assist ? 13 : 0) + (SiegeReady ? 3 : 0) - (enemies.Count > allies.Count + 1 ? 7 : 0);
                Consider(enemy.Object, defend ? DeveloperBotGoal.DefendFortress : assist ? DeveloperBotGoal.AssistAlly : DeveloperBotGoal.EnemyPlayer, score);
            }

            if (age >= chestAt && Health > .23f)
                foreach (var chest in Object.FindObjectsByType<NetworkLootChest>())
                {
                    if (!Visible(chest.Object) || chest.IsOpen) continue;
                    bool contested = enemies.Exists(enemy => Planar(enemy.transform.position - chest.transform.position).sqrMagnitude < 8 * 8);
                    float score = 69 - Distance(chest.transform.position) * 2 - (contested ? 9 : 0) + (1 - EquipmentQuality) * 6;
                    Consider(chest.Object, contested ? DeveloperBotGoal.ContestChest : DeveloperBotGoal.Chest, score);
                }

            bool push = SiegeReady || age > 80 + player.SpawnLane * 12;
            foreach (var enemy in visibleCreatures)
            {
                float distance = Distance(enemy.transform.position);
                if (enemy.GetComponent<NetworkFortressGuard>() != null)
                {
                    Consider(enemy.Object, DeveloperBotGoal.FortressGuard,
                        (push ? 75 : 67) - 2 * distance + Mathf.Max(0, 8 - distance) * 1.4f);
                }
                else if (enemy.Definition.Boss != null)
                {
                    int support = 0;
                    foreach (var ally in allies)
                        if (Planar(ally.transform.position - enemy.transform.position).sqrMagnitude < 15 * 15) support++;
                    if (Health > .58f && Stamina > .32f &&
                        (support > 0 || Level >= Tuning.soloBossLevel && EquipmentQuality >= .25f))
                        Consider(enemy.Object, DeveloperBotGoal.NeutralBoss, 53 + 9 * support - 1.6f * distance);
                }
                else Consider(enemy.Object, DeveloperBotGoal.NeutralCamp, (push ? 48 : 61) - distance * 2);
            }

            foreach (var gate in Object.FindObjectsByType<NetworkFortressGate>())
                if (gate.Team != player.Team && VisibleLiving(gate.Object) && !gate.IsOpen)
                    Consider(gate.Object, DeveloperBotGoal.FortressGate, (push ? 73 : 59) - Distance(gate.transform.position) * 2);

            if (enemyKing != null && VisibleLiving(enemyKing.Object))
                Consider(enemyKing.Object, DeveloperBotGoal.King, (push ? 83 : 72) - Distance(enemyKing.transform.position) * 2);

            if (choice != null && best > 3)
            {
                target = choice;
                Goal = goal;
                Observe(choice);
                return;
            }

            if (seenTarget != default && age - seenAt <= Tuning.lastSeenSeconds && Distance(seenPosition) > 2)
            {
                Navigate(DeveloperBotGoal.Search, seenPosition);
                return;
            }

            if (!push && TryScoutCamp(out var camp))
            {
                Navigate(DeveloperBotGoal.ScoutCamp, camp);
                return;
            }
            if (enemyKing != null)
            {
                Navigate(DeveloperBotGoal.PushFortress, enemyKing.transform.position);
                return;
            }
            float angle = age * .05f + player.SpawnLane + (player.Team == TeamId.TeamB ? Mathf.PI : 0);
            Navigate(DeveloperBotGoal.Roam, new Vector3(Mathf.Sin(angle) * 25, home.y, Mathf.Cos(angle) * 25));
        }

        private bool TryScoutCamp(out Vector3 site)
        {
            site = default;
            float nearest = 36;
            foreach (var camp in Object.FindObjectsByType<NetworkCamp>())
            {
                if (!SameWorld(camp.Object) || camp.Definition == null) continue;
                float distance = Distance(camp.transform.position);
                if (distance >= nearest) continue;
                if (distance < 3 || (Visible(camp.Object) && camp.State != NetworkCampState.Active))
                { campVisits[camp.Object.Id] = age + 18; continue; }
                if (campVisits.TryGetValue(camp.Object.Id, out float retry) && retry > age) continue;
                nearest = distance;
                site = camp.transform.position;
            }
            return nearest < 36;
        }

        private void Navigate(DeveloperBotGoal goal, Vector3 point)
        {
            Goal = goal;
            target = null;
            destination = point;
        }
    }
}
#endif

using UnityEngine;
#if UNITY_EDITOR || DEBUG
using System;
using System.Linq;
using Fusion;
using HeroesOfDragonland.Abilities;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.InventorySystem;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Skills;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Teams;
using HeroesOfDragonland.UI;
using HeroesOfDragonland.World;
using HeroesOfDragonland.World.Camps;
#endif

namespace HeroesOfDragonland.AI
{
    // Only installed with -hodrBossCheck. Uses normal owning-player spell and chest requests.
    public sealed class BossNetworkProbe : MonoBehaviour
    {
#if UNITY_EDITOR || DEBUG
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!Environment.GetCommandLineArgs().Contains("-hodrBossCheck")) return;
            var bootstrap = FindAnyObjectByType<MultiplayerBootstrap>();
            if (bootstrap != null) bootstrap.gameObject.AddComponent<BossNetworkProbe>();
        }
        private MultiplayerBootstrap bootstrap;
        private NetworkCamp camp;
        private NetworkEnemy boss;
        private float began = -1, nextCast, nextClaim, nextReport;
        private bool initialized, phaseOne, transition, phaseTwo, hitA, hitB, seededKill, looted, denied, pvpPlaced, complete, uiSeen;
        private long baselineA, baselineB, bossKillAwards;
        private NetworkId chestId;
        private void Awake() => bootstrap = GetComponent<MultiplayerBootstrap>();
        private static void Check(bool condition, string label)
        { if (!condition) throw new InvalidOperationException("[BossNetwork] FAIL " + label); }
        private void LateUpdate()
        {
            var runner = bootstrap.Runner;
            if (complete || runner == null || !runner.IsRunning || !NetworkMatch.AllowsGameplay(runner)) return;
            var humans = FindObjectsByType<NetworkTestPlayer>().Where(p => p.Object != null && p.Object.IsValid && p.Runner == runner && !p.IsBot).ToArray();
            if (humans.Length != 2) return;
            if (began < 0) began = Time.realtimeSinceStartup;
            float age = Time.realtimeSinceStartup - began;
            camp ??= FindObjectsByType<NetworkCamp>().FirstOrDefault(c => c.Object != null && c.Object.IsValid && c.Runner == runner && c.Definition.Difficulty == CampDifficulty.Contested);
            if (camp == null) return;
            if (runner.IsServer && !initialized)
            {
                if (camp.State == NetworkCampState.Pending) camp.TryStartDevelopmentEncounter("-hodrBossCheck");
                boss = FindObjectsByType<NetworkEnemy>().FirstOrDefault(e => e.Object != null && e.Object.IsValid && e.Runner == runner && e.Definition.Boss != null);
                if (boss == null) return;
                initialized = true;
                foreach (var bot in humans[0].Runner.GetComponentInParent<MultiplayerBootstrap>().Avatars.Where(p => p.GetComponent<NetworkTestPlayer>().IsBot))
                { bot.GetComponent<NetworkTestPlayer>().enabled = false; bot.GetComponent<MeleeAttack>().enabled = false; }
                foreach (var human in humans)
                {
                    var skills = human.GetComponent<SessionSkills>(); Check(skills.TryLearn(3, skills.SessionId, out _), "fire learned");
                    human.GetComponent<CharacterStats>().ResetToFull();
                    bool a = human.Team == TeamId.TeamA;
                    human.GetComponent<HeroesOfDragonland.Progression.SessionProgression>().ExperienceGained += xp =>
                    {
                        if (xp == NetworkMatchPacing.BalanceFor(runner).BossKillExperience) bossKillAwards++;
                    };
                    human.GetComponent<NetworkTransform>().Teleport(new Vector3(0, 1, a ? 3 : -3), Quaternion.LookRotation(a ? Vector3.back : Vector3.forward));
                }
                // Fast-forward the health budget, not death/reward state. Real owner projectiles cross the threshold and kill.
                var hp = boss.GetComponent<CharacterStats>(); hp.ApplyDamage(hp.CurrentHealth - 920);
                boss.GetComponent<DamageReceiver>().DamageTaken += (amount, source) => {
                    var player = source != null ? source.GetComponent<NetworkTestPlayer>() : null;
                    if (player != null) { if (player.Team == TeamId.TeamA) hitA = true; else if (player.Team == TeamId.TeamB) hitB = true; }
                };
                camp.Cleared += c => Check(!c.TryComplete(), "reentrant clear rejected");
            }
            boss ??= FindObjectsByType<NetworkEnemy>().FirstOrDefault(e => e.Object != null && e.Object.IsValid && e.Runner == runner && e.Definition.Boss != null);
            if (boss == null) return;
            phaseOne |= boss.BossPhase == BossPhase.PhaseOne; transition |= boss.BossPhase == BossPhase.Transition; phaseTwo |= boss.BossPhase == BossPhase.PhaseTwo;
            var hud = FindAnyObjectByType<GameplayHudView>(); uiSeen |= hud != null && hud.HasBoss;
            if (runner.IsServer && !seededKill && phaseTwo && hitA && hitB && age > 10)
            { var hp = boss.GetComponent<CharacterStats>(); hp.ApplyDamage(Mathf.Max(0, hp.CurrentHealth - 20)); seededKill = true; }
            if (runner.IsServer && camp.State != NetworkCampState.Cleared)
                foreach (var human in humans) human.GetComponent<CharacterStats>().ResetToFull(); // Keep acceptance actors alive while observing full attack/phase replication.
            NetworkLootChest chest = null;
            if (camp.ChestId != default && runner.TryFindObject(camp.ChestId, out var chestObject)) chest = chestObject.GetComponent<NetworkLootChest>();
            if (runner.IsServer && chest != null && chestId == default)
            {
                chestId = chest.Object.Id;
                foreach (var human in humans)
                {
                    var inv = human.GetComponent<Inventory>();
                    if (human.Team == TeamId.TeamA) baselineA = inv.Revision; else baselineB = inv.Revision;
                    bool a = human.Team == TeamId.TeamA;
                    human.GetComponent<NetworkTransform>().Teleport(chest.transform.position + new Vector3(a ? -1.4f : 1.4f, 1, 0));
                }
            }
            if (chest != null && chest.IsOpen)
            {
                looted = true;
                if (runner.IsServer && !pvpPlaced)
                {
                    Check(camp.CompletionCount == 1 && camp.DeadCount == 1 && !camp.TryComplete() && camp.ChestId == chestId, "single death and reward state");
                    Check(FindObjectsByType<NetworkLootChest>().Count(c => c.Object != null && c.Object.IsValid && c.Runner == runner && c.CampId == camp.Object.Id) == 1, "one linked chest");
                    long changes = 0;
                    int recipients = 0;
                    foreach (var human in humans)
                    {
                        var inventory = human.GetComponent<Inventory>(); changes += inventory.Revision - (human.Team == TeamId.TeamA ? baselineA : baselineB);
                        Check(!chest.TryOpenFor(human.Object.InputAuthority, out _), "duplicate/competing claim rejected");
                        var progression = human.GetComponent<NetworkSessionProgression>();
                        if (human.GetComponent<HeroesOfDragonland.Progression.SessionProgression>()
                            .HasExperienceReward("camp:" + camp.Object.Id + ":" + camp.CompletionCount))
                        {
                            recipients++;
                            Check(!progression.TryGrantExperience(camp.AwardedExperience, "camp:" + camp.Object.Id + ":" + camp.CompletionCount), "duplicate XP rejected for rewarded actor");
                        }
                    }
                    Check(recipients == camp.RewardedPlayers && recipients <= 1, "camp XP restricted to the winning contribution team");
                    Check(bossKillAwards == 1, "boss kill XP awarded once separately from camp XP");
                    Check(changes == 1 && chest.GetComponent<LootChest>().Revision == 1, "one atomic inventory transfer");
                    pvpPlaced = true;
                }
            }
            if (!runner.IsServer && !denied)
            {
                var hp = boss.GetComponent<CharacterStats>(); float before = hp.CurrentHealth;
                boss.GetComponent<EnemyBrain>().Tick(100);
                Check(hp.ApplyDamage(10000) == 0 && hp.CurrentHealth == before && !boss.CanSimulateEnemy && boss.SimulationTicks == 0, "replica boss mutation denied");
                Check(!camp.TryComplete() && !camp.TryInitialize(), "replica reward mutation denied"); denied = true;
            }
            if (!runner.IsServer && chest != null)
                Check(!chest.GetComponent<LootChest>().TryInteract(humans[0].GetComponent<Inventory>(), out _), "replica loot mutation denied");
            if (runner.TryGetPlayerObject(runner.LocalPlayer, out var local))
            {
                bool a = local.GetComponent<NetworkTestPlayer>().Team == TeamId.TeamA;
                bootstrap.TestInput = Vector2.zero; bootstrap.TestAttack = bootstrap.TestHeavyAttack = bootstrap.TestDodge = bootstrap.TestParry = bootstrap.TestBlock = false;
                bootstrap.TestFacing = new Vector2(0, a ? -1 : 1);
                var skills = local.GetComponent<SessionSkills>();
                if (Time.realtimeSinceStartup >= nextCast && chest == null && skills.IsLearned(3) && skills.CooldownRemaining(3) <= 0)
                { local.GetComponent<NetworkSessionSkills>().RequestActivate(3); nextCast = Time.realtimeSinceStartup + 1; }
                if (chest != null && !chest.IsOpen && Time.realtimeSinceStartup >= nextClaim)
                { chest.RequestOpen(); nextClaim = Time.realtimeSinceStartup + 1; }
            }
            // The skills fixture exercises real PvP casts. Here validate the boss does not change team hostility.
            bool pvp = looted && TeamIdentity.CanDamage(humans[0].gameObject, humans[1].gameObject)
                && TeamIdentity.CanDamage(humans[1].gameObject, humans[0].gameObject);
            if (phaseOne && transition && phaseTwo && looted && pvp && uiSeen && (runner.IsServer ? hitA && hitB : denied))
            { complete = true; Debug.Log("[BossNetwork] PASS both-team owner spell damage, replicated phases/UI, preserved team hostility, one server death/chest, atomic contested loot, duplicate reward denial and replica guards; server=" + runner.IsServer); }
            if (age > 80) { complete = true; Check(false, "timeout"); }
            if (Time.realtimeSinceStartup >= nextReport)
            {
                nextReport = Time.realtimeSinceStartup + 4;
                Debug.Log("[BossNetwork] server=" + runner.IsServer + " age=" + age.ToString("F1") + " phase=" + boss.BossPhase + " hp=" + boss.GetComponent<NetworkCharacterStats>().Health + " hits=" + hitA + "/" + hitB + " camp=" + camp.State + " loot=" + looted + " pvp=" + pvp + " ui=" + uiSeen);
            }
        }
        private void OnDisable()
        {
            if (bootstrap == null) return;
            bootstrap.TestInput = bootstrap.TestFacing = null;
            bootstrap.TestAttack = bootstrap.TestHeavyAttack = bootstrap.TestDodge = bootstrap.TestParry = bootstrap.TestBlock = null;
        }
#endif
    }
}

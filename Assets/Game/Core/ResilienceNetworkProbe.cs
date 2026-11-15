using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using Fusion;
using HeroesOfDragonland.InventorySystem;
using HeroesOfDragonland.EquipmentSystem;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Progression;
#endif
namespace HeroesOfDragonland.Multiplayer
{
    // Explicit local acceptance controls. Inert without -hodrResilienceControl and absent from release logic.
    public sealed class ResilienceNetworkProbe : MonoBehaviour
    {
        [SerializeField] private Fusion.NetworkObject chestPrefab;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private string control, lastCommand;
        private NetworkId recordedId;
        private PlayerRef recordedOwner;
        private Teams.TeamId recordedTeam;
        private string equipment, inventory;
        private int level, deaths;
        private NetworkLootChest chest;
        private MultiplayerBootstrap bootstrap;
        private void Awake()
        {
            bootstrap = GetComponent<MultiplayerBootstrap>();
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "-hodrResilienceControl") control = args[i + 1];
            if (!string.IsNullOrEmpty(control)) Application.targetFrameRate = 60;
        }
        private void Check(bool ok, string label)
        {
            if (!ok) throw new InvalidOperationException("[ResilienceCheck] FAIL " + label);
            Debug.Log("[ResilienceCheck] PASS " + label);
        }
        private NetworkObject Remote()
        {
            var r = bootstrap.Runner;
            foreach (var p in r.ActivePlayers)
                if (p != r.LocalPlayer && r.TryGetPlayerObject(p, out var actor) && actor != null && actor.IsValid) return actor;
            throw new InvalidOperationException("No remote avatar");
        }
        private string InventoryKey(NetworkObject actor)
        {
            var inv = actor.GetComponent<Inventory>();
            return string.Join(";", Enumerable.Range(0, inv.SlotCount).Select(i => inv.GetSlot(i).ItemId + ":" + inv.GetSlot(i).Amount));
        }
        private string EquipmentKey(NetworkObject actor)
            => string.Join(";", Enumerable.Range(1, 6).Select(i => actor.GetComponent<Equipment>().GetItemId((EquipmentSlot)i)));
        private void Record(NetworkObject actor)
        {
            recordedId = actor.Id; recordedOwner = actor.InputAuthority;
            recordedTeam = actor.GetComponent<NetworkTestPlayer>().Team;
            equipment = EquipmentKey(actor); inventory = InventoryKey(actor);
            level = actor.GetComponent<SessionProgression>().Level;
            deaths = actor.GetComponent<NetworkPlayerRespawn>().DeathSequence;
        }
        private void Update()
        {
            if (string.IsNullOrEmpty(control) || !File.Exists(control)) return;
            string command;
            try { command = File.ReadAllText(control).Trim(); } catch (IOException) { return; }
            if (string.IsNullOrEmpty(command) || command == lastCommand) return;
            lastCommand = command;
            string action = command.Split(' ').Last();
            try
            {
                var r = bootstrap.Runner;
                var m = GetComponent<NetworkFortressSpawner>().Match;
                switch (action)
                {
                    case "policies":
                        var teams = new NetworkTeamRoster();
                        Check(teams.TryAssign(10, out var first) && teams.TryAssign(20, out _), "initial roster allocation");
                        Check(teams.Transfer(10, 30) && teams.TryGetTeam(30, out var moved) && moved == first && teams.Count == 2, "roster transfer preserves team and capacity");
                        Check(teams.Transfer(30, 30) && !teams.Transfer(10, 40) && teams.Count == 2, "same-ID reclaim and duplicate transfer safety");
                        var identities = new ReconnectReservations(); var credential = new byte[32];
                        identities.Track(PlayerRef.FromIndex(0), credential);
                        Check(identities.IsDuplicate(credential) && !identities.IsDuplicate(credential, PlayerRef.FromIndex(0)), "pending identity rejects duplicate transport but permits own spawn retry");
                        Check(NetworkFailureMessages.Classify(ShutdownReason.GameIsFull) == NetworkConnectionState.SessionFull
                            && NetworkFailureMessages.Classify(ShutdownReason.GameNotFound) == NetworkConnectionState.SessionNotFound
                            && NetworkFailureMessages.Classify(ShutdownReason.GameClosed) == NetworkConnectionState.SessionClosed
                            && NetworkFailureMessages.Classify(ShutdownReason.ConnectionRefused) == NetworkConnectionState.JoinRejected, "explicit failure categories");
                        break;
                    case "ready":
                        if (r.IsServer) m.SetShortMatch(true);
                        m.RPC_SetReady(true); break;
                    case "start": Check(m.TryStartLobby(), "host starts ready match"); break;
                    case "duplicate":
                        var before = bootstrap.Runner;
                        _ = bootstrap.StartSession(GameMode.Client, bootstrap.RoomName);
                        Check(bootstrap.Runner == before && !string.IsNullOrEmpty(bootstrap.RequestNotice), "duplicate join rejected without replacing runner"); break;
                    case "seed":
                        var seeded = Remote();
                        Check(seeded.GetComponent<Inventory>().TryAdd("b4d4a0612c1acd74d98f66ef3969959f"), "seed equipment item");
                        Check(seeded.GetComponent<Equipment>().TryEquip(0, EquipmentSlot.MainHand, out _), "equip before disconnect");
                        Check(seeded.GetComponent<NetworkSessionProgression>().TryGrantExperience(300, "m51-seed"), "seed progression");
                        Record(seeded); break;
                    case "death-drop":
                        var dying = Remote();
                        dying.GetComponent<CharacterStats>().ApplyDamage(999999);
                        Check(dying.GetComponent<NetworkPlayerRespawn>().WaitingForRespawn, "death schedules respawn before departure");
                        Record(dying); r.Disconnect(dying.InputAuthority); break;
                    case "drop":
                        var dropping = Remote(); Record(dropping); r.Disconnect(dropping.InputAuthority); break;
                    case "chest-drop":
                        var looter = Remote();
                        chest = r.Spawn(chestPrefab, looter.transform.position + Vector3.up * .2f, Quaternion.identity).GetComponent<NetworkLootChest>();
                        Physics.SyncTransforms();
                        Check(chest.TryOpenFor(looter.InputAuthority, out var reason), "atomic chest claim before disconnect: " + reason);
                        Check(!chest.TryOpenFor(looter.InputAuthority, out _), "duplicate chest claim denied");
                        Record(looter); r.Disconnect(looter.InputAuthority); break;
                    case "reserved":
                        bootstrap.OnPlayerLeft(r, recordedOwner); // Duplicate callbacks must not release the reserved team slot.
                        Check(bootstrap.ReservedPlayerCount == 1, "one disconnected reservation");
                        Check(r.TryFindObject(recordedId, out var reserved) && reserved.GetComponent<NetworkTestPlayer>().Suspended, "reserved avatar is suspended");
                        Check(!NetworkParticipant.IsAdmitted(reserved), "disconnected avatar cannot mutate gameplay");
                        Check(!reserved.GetComponent<NetworkSessionProgression>().CanMutateProgression && !reserved.GetComponent<NetworkCharacterStats>().CanMutateStats, "disconnected stats and progression reject mutations");
                        Check(!reserved.GetComponent<NetworkInventory>().TryAddItem("b4d4a0612c1acd74d98f66ef3969959f"), "disconnected inventory rejects rewards");
                        if (chest != null) Check(!chest.TryOpenFor(recordedOwner, out _), "departed chest sender rejected");
                        break;
                    case "resumed":
                        var resumed = Remote();
                        Check(resumed.Id == recordedId && resumed.InputAuthority != recordedOwner, "same avatar assigned to new connection");
                        Check(resumed.GetComponent<NetworkTestPlayer>().Team == recordedTeam, "original team preserved");
                        Check(EquipmentKey(resumed) == equipment && InventoryKey(resumed) == inventory, "inventory and equipment preserved exactly");
                        Check(resumed.GetComponent<SessionProgression>().Level == level, "progression preserved");
                        Check(resumed.GetComponent<NetworkPlayerRespawn>().DeathSequence == deaths, "death sequence preserved");
                        Check(NetworkParticipant.IsAdmitted(resumed) && !resumed.GetComponent<NetworkTestPlayer>().Suspended && bootstrap.ReservedPlayerCount == 0, "one admitted owner and no reservation leak");
                        if (chest != null) Check(chest.IsOpen && !chest.TryOpenFor(resumed.InputAuthority, out _), "reconnect does not duplicate chest loot");
                        break;
                    case "local-record":
                        Check(r.TryGetPlayerObject(r.LocalPlayer, out var local), "local avatar present"); Record(local); break;
                    case "local-restored":
                        Check(r.TryGetPlayerObject(r.LocalPlayer, out var restored), "reconnected local avatar present");
                        Check(InventoryKey(restored) == inventory && EquipmentKey(restored) == equipment, "new input owner receives private inventory snapshot"); break;
                    case "finish-drop":
                        var leaving = Remote(); Record(leaving);
                        var king = FindObjectsByType<NetworkKingObjective>().First(k => k.Runner == r);
                        king.Stats.ApplyDamage(999999);
                        Check(m.State == MatchPhase.Finished, "match result committed");
                        r.Disconnect(leaving.InputAuthority); break;
                    case "lobby-clean":
                        Check(m.State == MatchPhase.Lobby && m.FinishCount == 1 && !m.CanPlay, "single match result returns to lobby");
                        Check(!r.TryFindObject(recordedId, out _) && bootstrap.ReservedPlayerCount == 0, "old avatar and reconnect reservation removed at round boundary"); break;
                    case "leave": _ = bootstrap.Disconnect(); break;
                    case "roster":
                        int count = r.ActivePlayers.Count();
                        Check(m.TeamACount + m.TeamBCount == count && count <= 6, "avatar roster matches active connections: " + count); break;
                    default: throw new InvalidOperationException("Unknown command " + action);
                }
                Debug.Log("[ResilienceCommand] DONE " + command);
            }
            catch (Exception e) { Debug.LogException(e); }
        }
#endif
    }
}


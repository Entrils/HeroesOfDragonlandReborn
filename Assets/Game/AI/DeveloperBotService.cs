#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using Fusion;
using HeroesOfDragonland.Teams;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    [DisallowMultipleComponent, RequireComponent(typeof(MultiplayerBootstrap))]
    public sealed class DeveloperBotService : MonoBehaviour
    {
        private sealed class Slot
        {
            internal int Key;
            internal TeamId Team;
            internal NetworkObject Avatar;
        }
        [SerializeField, Range(0, 5)] private int requestedBots;
        private readonly List<Slot> slots = new List<Slot>();
        private MultiplayerBootstrap bootstrap;
        private float retryAt;
        public int RequestedBots => requestedBots;
        public void StartLocalMatch()
        {
            if (!bootstrap.Running && !bootstrap.Busy) _ = bootstrap.StartSession(GameMode.Single, "developer-bots");
        }
        public string Status { get; private set; } = "Host a match to spawn bots.";
        public IEnumerable<NetworkObject> Avatars
        {
            get { foreach (var slot in slots) if (slot.Avatar != null && slot.Avatar.IsValid) yield return slot.Avatar; }
        }
        public int SpawnedCount { get { int count = 0; foreach (var actor in Avatars) count++; return count; } }
        private void Awake()
        {
            bootstrap = GetComponent<MultiplayerBootstrap>();
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-hodrBots" && int.TryParse(args[i + 1], out int count)) requestedBots = Mathf.Clamp(count, 0, 5);
        }
        private void OnEnable() => GetComponent<MultiplayerBootstrap>().SessionStarted += OnSessionStarted;
        private void OnDisable()
        {
            GetComponent<MultiplayerBootstrap>().SessionStarted -= OnSessionStarted;
            ReleaseSession();
        }
        private void OnSessionStarted(NetworkRunner runner) { if (runner.IsServer) Reconcile(); }
        public bool SetBotCount(int count)
        {
            if (count < 0 || count > 5 || (bootstrap.Running && !bootstrap.Runner.IsServer)) return false;
            requestedBots = count;
            if (bootstrap.Running) Reconcile();
            return true;
        }
        private void Update()
        {
            if (!bootstrap.Running || !bootstrap.Runner.IsServer || Time.unscaledTime < retryAt) return;
            retryAt = Time.unscaledTime + .5f;
            Reconcile();
        }
        private void Reconcile()
        {
            requestedBots = Mathf.Clamp(requestedBots, 0, 5);
            while (slots.Count > requestedBots)
            {
                var slot = slots[slots.Count - 1]; Despawn(slot);
                bootstrap.ReleaseBot(slot.Key); slots.RemoveAt(slots.Count - 1);
            }
            while (slots.Count < requestedBots)
            {
                int key = -100000 - slots.Count;
                if (!bootstrap.ReserveBot(key, out var team)) break;
                slots.Add(new Slot { Key = key, Team = team });
            }
            SpawnPending();
            Status = SpawnedCount + "/" + requestedBots + " bots / six total participants maximum";
        }
        internal void SpawnPending()
        {
            var runner = bootstrap.Runner;
            if (runner == null || !runner.IsRunning || !runner.IsServer) return;
            foreach (var slot in slots)
            {
                if (slot.Avatar != null && slot.Avatar.IsValid) continue;
                var spawn = GetComponent<TeamSpawnService>();
                if (spawn == null || !spawn.TryChooseSpawn(runner, slot.Team, null, out var position, out var rotation)) continue;
                int lane = 0;
                for (; lane < 3; lane++)
                {
                    bool used = false;
                    foreach (var actor in bootstrap.Avatars)
                    {
                        var player = actor.GetComponent<NetworkTestPlayer>();
                        if (player.Team == slot.Team && player.SpawnLane == lane) { used = true; break; }
                    }
                    if (!used) break;
                }
                try
                {
                    slot.Avatar = runner.Spawn(bootstrap.PlayerPrefab, position, rotation, PlayerRef.None, (r, obj) => {
                        slot.Avatar = obj; // Register before Spawned initializes progression.
                        obj.GetComponent<NetworkTestPlayer>().Initialize(slot.Team, bootstrap.NextParticipantNumber(), lane, true);
                    });
                    if (slot.Avatar == null) throw new System.InvalidOperationException("Bot spawn returned null.");
                }
                catch (System.Exception exception)
                {
                    Despawn(slot);
                    Debug.LogException(exception, this);
                }
            }
        }
        internal bool Contains(NetworkObject actor)
        {
            if (!isActiveAndEnabled || actor == null || actor.Runner != bootstrap.Runner || actor.InputAuthority != PlayerRef.None) return false;
            var player = actor.GetComponent<NetworkTestPlayer>();
            return player != null && player.IsBot && slots.Exists(slot => slot.Avatar == actor);
        }
        private void Despawn(Slot slot)
        {
            var actor = slot.Avatar; slot.Avatar = null;
            if (actor == null || !actor.IsValid) return;
            foreach (var collider in actor.GetComponentsInChildren<Collider>()) collider.enabled = false;
            if (actor.Runner.IsRunning && actor.Runner.IsServer) actor.Runner.Despawn(actor);
        }
        internal void ClearAvatars() { foreach (var slot in slots) Despawn(slot); }
        internal void ReleaseSession()
        {
            ClearAvatars();
            if (bootstrap != null) foreach (var slot in slots) bootstrap.ReleaseBot(slot.Key);
            slots.Clear();
        }
        private void OnGUI()
        {
            if (Application.isBatchMode || !HeroesOfDragonland.UI.DeveloperUiLayout.Visible) return;
            var matrix = HeroesOfDragonland.UI.DeveloperUiLayout.Begin();
            GUILayout.BeginArea(new Rect(HeroesOfDragonland.UI.DeveloperUiLayout.Width - 376, 240, 360, 180), GUI.skin.box);
            GUILayout.Label("Developer bots (server controlled)");
            bool previous = GUI.enabled;
            GUI.enabled = !bootstrap.Busy && (!bootstrap.Running || bootstrap.Runner.IsServer);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("−", GUILayout.Width(35))) SetBotCount(Mathf.Max(0, requestedBots - 1));
            GUILayout.Label(requestedBots + " bots", GUILayout.Width(80));
            if (GUILayout.Button("+", GUILayout.Width(35))) SetBotCount(Mathf.Min(5, requestedBots + 1));
            if (GUILayout.Button("Fill 3v3")) SetBotCount(5);
            GUILayout.EndHorizontal();
            if (!bootstrap.Running && GUILayout.Button("Start local match (no server connection)"))
                StartLocalMatch();
            GUI.enabled = previous;
            GUILayout.Label(Status);
            GUILayout.Label("Bot AI and controls: Editor / Development Build only");
            GUILayout.EndArea();
            GUI.matrix = matrix;
        }
    }
}
#else
namespace HeroesOfDragonland.Multiplayer
{
    // Keep the serialized scene component valid in release builds; no bot controls or AI are compiled.
    public sealed class DeveloperBotService : UnityEngine.MonoBehaviour { }
}
#endif

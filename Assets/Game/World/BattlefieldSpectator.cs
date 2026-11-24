using System.Collections.Generic;
using Fusion;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Presentation;
using HeroesOfDragonland.Teams;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace HeroesOfDragonland.World
{
    // Submit the chosen ally and a snapshot of authored public scenery to an isolated camera.
    // Live world actors, dynamic effects, actor shadows, and positional audio stay out of this view.
    [DefaultExecutionOrder(210), DisallowMultipleComponent]
    public sealed class BattlefieldSpectator : MonoBehaviour
    {
        private const string ViewLayer = "SpectatorView";
        private readonly List<NetworkTestPlayer> allies = new();
        private Camera gameplay, spectator;
        private Transform scenery;
        private Transform audioAnchor;
        private readonly List<(Mesh Mesh, Material Material, int Submesh, Matrix4x4 Matrix, Bounds Bounds)> backdrop = new();
        private AudioListener gameplayAudio, spectatorAudio;
        private CharacterVisual model;
        private CharacterAppearanceVisual appearance;
        private NetworkTestPlayer viewed;
        private int lastParticipant, layer;
        private bool savedCamera, savedAudio;

        public bool Active { get; private set; }
        public NetworkTestPlayer Viewed => viewed;
        internal Camera ViewCamera => spectator;
        internal bool HasModel => model != null;
        internal void Step(int direction) => Select(Cycle(allies.IndexOf(viewed), direction, allies.Count));

        public static bool Eligible(TeamId localTeam, TeamId candidateTeam, bool self, bool dead, bool suspended)
            => (localTeam is TeamId.TeamA or TeamId.TeamB) && candidateTeam == localTeam && !self && !dead && !suspended;

        internal void Configure(Camera gameplayCamera, Transform publicScenery)
        {
            gameplay = gameplayCamera;
            scenery = publicScenery;
            if (scenery == null) return;
            foreach (var mesh in scenery.GetComponentsInChildren<MeshFilter>())
            {
                if (mesh.sharedMesh == null || mesh.GetComponentInParent<NetworkObject>() != null
                    || !mesh.TryGetComponent<MeshRenderer>(out var renderer) || !renderer.enabled) continue;
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length && i < mesh.sharedMesh.subMeshCount; i++)
                    if (materials[i] != null) backdrop.Add((mesh.sharedMesh, materials[i], i, mesh.transform.localToWorldMatrix, renderer.bounds));
            }
        }

        internal void UpdateView(MultiplayerBootstrap bootstrap, NetworkTestPlayer local, bool active)
        {
            if (!active || local == null)
            {
                StopViewing();
                return;
            }

            if (!Active) BeginViewing(bootstrap);
            allies.Clear();
            foreach (var candidate in FindObjectsByType<NetworkTestPlayer>())
            {
                if (candidate.Object == null || !candidate.Object.IsValid || candidate.Runner != local.Runner
                    || !candidate.TryGetComponent<NetworkCharacterStats>(out var stats) || stats.MaxHealth <= 0
                    || !Eligible(local.Team, candidate.Team, candidate == local, stats.Dead, candidate.IsSuspended)) continue;
                allies.Add(candidate);
            }
            allies.Sort((a, b) => a.ParticipantNumber.CompareTo(b.ParticipantNumber));

            int index = allies.IndexOf(viewed);
            if (index < 0 && allies.Count > 0)
            {
                index = allies.FindIndex(ally => ally.ParticipantNumber > lastParticipant);
                if (index < 0) index = 0;
            }

            if (Application.isFocused && Keyboard.current is { } keys)
            {
                if (keys.pageDownKey.wasPressedThisFrame) index = Cycle(index, 1, allies.Count);
                else if (keys.pageUpKey.wasPressedThisFrame) index = Cycle(index, -1, allies.Count);
            }
            Select(index);
        }

        public static int Cycle(int index, int direction, int count)
            => count <= 0 ? -1 : index < 0 ? (direction < 0 ? count - 1 : 0) : (index + direction + count) % count;

        private void Select(int index)
        {
            viewed = index >= 0 && index < allies.Count ? allies[index] : null;
            if (viewed != null) lastParticipant = viewed.ParticipantNumber;
            if (model != null) model.gameObject.SetActive(viewed != null);
        }

        private void BeginViewing(MultiplayerBootstrap bootstrap)
        {
            layer = LayerMask.NameToLayer(ViewLayer);
            if (spectator == null) CreateView(bootstrap);
            savedCamera = gameplay.enabled;
            gameplayAudio = gameplay.GetComponent<AudioListener>();
            savedAudio = gameplayAudio != null && gameplayAudio.enabled;
            gameplay.enabled = false;
            if (savedAudio) gameplayAudio.enabled = false;
            spectatorAudio.enabled = savedAudio;
            spectator.enabled = true;
            Active = true;
        }

        private void CreateView(MultiplayerBootstrap bootstrap)
        {
            var stage = new GameObject("Allied spectator camera");
            stage.transform.position = new Vector3(0f, -400f, 0f);
            spectator = stage.AddComponent<Camera>();
            spectator.enabled = false;
            spectator.clearFlags = CameraClearFlags.SolidColor;
            spectator.backgroundColor = new Color(.038f, .057f, .075f);
            spectator.cullingMask = layer >= 0 ? 1 << layer : 0;
            spectator.nearClipPlane = .05f;
            spectator.farClipPlane = 32f;
            audioAnchor = new GameObject("Isolated spectator listener").transform;
            audioAnchor.position = new Vector3(0f, -400f, 0f);
            spectatorAudio = audioAnchor.gameObject.AddComponent<AudioListener>();
            spectatorAudio.enabled = false;

            if (layer < 0 || bootstrap.PlayerPrefab == null) return;
            var source = bootstrap.PlayerPrefab.GetComponent<NetworkCharacterVisual>()?.Visual;
            if (source == null) return;
            // This is the authored visual prefab branch. Reject a branch containing live actors.
            if (source.GetComponentInChildren<NetworkObject>(true) != null
                || source.GetComponentInChildren<Collider>(true) != null
                || source.GetComponentInChildren<AudioSource>(true) != null) return;

            model = Instantiate(source, stage.transform, false);
            model.transform.SetLocalPositionAndRotation(new Vector3(0f, -.85f, 3.35f), Quaternion.Euler(0f, 155f, 0f));
            foreach (var child in model.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            appearance = model.GetComponent<CharacterAppearanceVisual>();
            model.gameObject.SetActive(false);

            var key = new GameObject("Spectator key light").AddComponent<Light>();
            key.transform.SetParent(stage.transform, false);
            key.transform.localPosition = new Vector3(1f, 1.8f, 1.4f);
            key.cullingMask = 1 << layer;
            key.type = LightType.Point;
            key.range = 6f;
            key.intensity = 5f;
        }

        private void LateUpdate()
        {
            if (!Active || model == null) return;
            if (viewed == null || viewed.Object == null || !viewed.Object.IsValid || viewed.IsSuspended
                || viewed.GetComponent<NetworkCharacterStats>().Dead)
            {
                model.gameObject.SetActive(false);
                return;
            }
            var rotation = Quaternion.Euler(20f, viewed.transform.eulerAngles.y, 0f);
            Vector3 pivot = viewed.transform.position + Vector3.up * .95f;
            Vector3 offset = rotation * Vector3.back;
            float distance = 4.2f;
            foreach (var hit in Physics.SphereCastAll(pivot, .2f, offset, distance, ~0, QueryTriggerInteraction.Ignore))
                if (scenery != null && hit.transform.IsChildOf(scenery) && hit.transform.GetComponentInParent<NetworkObject>() == null)
                    distance = Mathf.Min(distance, Mathf.Max(.35f, hit.distance - .08f));
            spectator.transform.SetPositionAndRotation(pivot + offset * distance, rotation);
            var visual = viewed.GetComponent<NetworkCharacterVisual>()?.Visual;
            if (visual == null) { model.gameObject.SetActive(false); return; }
            model.transform.SetPositionAndRotation(visual.transform.position, visual.transform.rotation);
            foreach (var draw in backdrop)
            {
                if (draw.Bounds.SqrDistance(pivot) > 32f * 32f) continue;
                var render = new RenderParams(draw.Material)
                {
                    camera = spectator, layer = layer, worldBounds = draw.Bounds,
                    shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false
                };
                if (!Application.isBatchMode) Graphics.RenderMesh(render, draw.Mesh, draw.Submesh, draw.Matrix);
            }
            appearance?.Apply(viewed.Appearance);
            model.SetTint(viewed.Team == TeamId.TeamA ? new Color(.15f, .45f, 1f) : new Color(1f, .25f, .2f));
            // Only the chosen ally's animation is sampled. No world target or position enters the camera.
            model.Apply(visual.State, Time.deltaTime);
        }

        private void OnGUI()
        {
            if (!Active || Application.isBatchMode) return;
            float width = Mathf.Min(440f, Screen.width - 24f);
            GUILayout.BeginArea(new Rect((Screen.width - width) * .5f, 64f, width, 82f), GUI.skin.box);
            GUILayout.Label(viewed != null ? "Spectating ally P" + viewed.ParticipantNumber : "No living ally to spectate");
            if (allies.Count > 1)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Previous [Page Up]")) Step(-1);
                if (GUILayout.Button("Next [Page Down]")) Step(1);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndArea();
        }

        internal void StopViewing()
        {
            if (!Active) return;
            Active = false;
            viewed = null;
            lastParticipant = 0;
            allies.Clear();
            if (spectator != null) spectator.enabled = false;
            if (spectatorAudio != null) spectatorAudio.enabled = false;
            if (model != null) model.gameObject.SetActive(false);
            if (gameplayAudio != null) gameplayAudio.enabled = savedAudio;
            if (gameplay != null) gameplay.enabled = savedCamera;
        }

        private void OnDisable() => StopViewing();
        private void OnDestroy()
        {
            if (spectator != null) Destroy(spectator.gameObject);
            if (audioAnchor != null) Destroy(audioAnchor.gameObject);
        }
    }
}

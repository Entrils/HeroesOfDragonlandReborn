using HeroesOfDragonland.Teams;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    [RequireComponent(typeof(NetworkTestPlayer))]
    public sealed class NetworkPlayerPresentation : MonoBehaviour
    {
        [SerializeField] private Renderer body;
        [SerializeField] private TextMesh label;
        private NetworkTestPlayer player;
        private MaterialPropertyBlock properties;
        private TeamId shownTeam;
        private bool initialized;

        private void Awake()
        {
            player = GetComponent<NetworkTestPlayer>();
            properties = new MaterialPropertyBlock();
        }

        private void LateUpdate()
        {
            if (Application.isBatchMode || player.Object == null || !player.Object.IsValid) return;
            if (!initialized || shownTeam != player.Team)
            {
                initialized = true;
                shownTeam = player.Team;
                Color color = shownTeam == TeamId.TeamA ? new Color(0.15f, 0.45f, 1f) : new Color(1f, 0.25f, 0.2f);
                properties.SetColor("_BaseColor", color);
                properties.SetColor("_Color", color);
                if (body != null) body.SetPropertyBlock(properties);
                if (label != null) label.text = "P" + player.ParticipantNumber + " / " + shownTeam
                    + (player.HasInputAuthority ? " (you)" : "");
            }
            if (label != null && Camera.main != null) label.transform.rotation = Camera.main.transform.rotation;
            var state = GetComponent<NetworkCharacterStats>();
            if (state != null && label != null)
                label.text = "P" + player.ParticipantNumber + " / " + shownTeam
                    + (player.HasInputAuthority ? " (you)" : "")
                    + (state.Dead ? " / DEAD" : " / HP " + Mathf.CeilToInt(state.Health));
        }
    }
}

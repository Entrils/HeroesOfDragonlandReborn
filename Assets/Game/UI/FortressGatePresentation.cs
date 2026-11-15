using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.World;
using UnityEngine;

namespace HeroesOfDragonland.UI
{
    [DisallowMultipleComponent, RequireComponent(typeof(FortressGate))]
    public sealed class FortressGatePresentation : MonoBehaviour
    {
        [SerializeField] private TextMesh label;
        private FortressGate gate;
        private CharacterStats stats;
        private void Awake() { gate = GetComponent<FortressGate>(); stats = GetComponent<CharacterStats>(); }
        private void LateUpdate()
        {
            if (label == null) return;
            label.text = gate.Team + " GATE\n" + (gate.IsOpen ? "BREACHED" : Mathf.CeilToInt(stats.CurrentHealth) + " / " + Mathf.CeilToInt(stats.MaxHealth));
            label.color = gate.IsOpen ? new Color(1, .75f, .2f) : Color.white;
            if (Camera.main != null) label.transform.rotation = Camera.main.transform.rotation;
        }
    }
}

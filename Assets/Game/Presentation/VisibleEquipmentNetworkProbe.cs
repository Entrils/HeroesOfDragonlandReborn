using UnityEngine;

namespace HeroesOfDragonland.Presentation
{
    // Opt-in observation only: never equips items, changes health or bypasses an RPC.
    [DefaultExecutionOrder(300), DisallowMultipleComponent]
    public sealed class VisibleEquipmentNetworkProbe : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool run;
        private long revision = -1;
        private float settleUntil;
        private string signature;
        public string LastResult { get; private set; } = "Not enabled";
        private void Awake() => run = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-hodrVisibleEquipmentCheck") >= 0;
        public void EnableForValidation() { run = true; signature = null; revision = -1; }
        private void LateUpdate()
        {
            if (!run) return;
            var player = GetComponent<HeroesOfDragonland.Multiplayer.NetworkTestPlayer>();
            if (player == null || player.Object == null || !player.Object.IsValid) return;
            var equipment = GetComponent<HeroesOfDragonland.EquipmentSystem.Equipment>();
            var network = GetComponent<HeroesOfDragonland.Multiplayer.NetworkEquipment>();
            var view = GetComponent<CharacterEquipmentVisual>();
            var stats = GetComponent<HeroesOfDragonland.Multiplayer.NetworkCharacterStats>();
            if (equipment == null || view == null || network == null) return;
            if (revision != equipment.Revision)
            {
                revision = equipment.Revision;
                settleUntil = Time.unscaledTime + .15f;
                return;
            }
            if (Time.unscaledTime < settleUntil) return;
            string state = revision + ":" + player.IsSuspended + ":" + (stats != null && stats.Dead);
            if (state == signature) return;
            signature = state;
            string error = null;
            for (int i = 1; i <= 6 && error == null; i++)
            {
                var slot = (HeroesOfDragonland.Items.EquipmentSlot)i;
                if (player.IsSuspended)
                {
                    for (int p = 0; p < view.VisualCount(slot); p++)
                        if (view.GetVisual(slot, p) != null && view.GetVisual(slot, p).activeInHierarchy) error = "suspended gear visible";
                    continue;
                }
                string published = network.EquippedIds[i - 1].ToString();
                if (network.SnapshotRevision > 0 && published != (equipment.GetItemId(slot) ?? "")) { error = "public snapshot differs from presentation model"; break; }
                var item = equipment.GetItem(slot);
                var recipe = view.Catalog != null ? view.Catalog.Resolve(item) : null;
                bool weapon = i <= 2;
                int expected = item == null ? 0 : weapon ? 1 : recipe != null ? recipe.Parts.Count : -1;
                if (view.VisualCount(slot) != expected) { error = slot + " visual count"; break; }
                for (int p = 0; p < expected; p++)
                {
                    var part = view.GetVisual(slot, p);
                    if (part == null || !part.activeInHierarchy) { error = slot + " inactive visual"; break; }
                    foreach (var collider in part.GetComponentsInChildren<Collider>(true)) if (collider.enabled) error = "visual collider enabled";
                }
            }
            LastResult = (error == null ? "PASS" : "FAIL " + error) + " revision=" + revision + " suspended=" + player.IsSuspended
                + " dead=" + (stats != null && stats.Dead) + " replica=" + (player.HasInputAuthority ? "owner" : "remote");
            if (error == null) Debug.Log("[VisibleEquipment] " + LastResult, this);
            else Debug.LogError("[VisibleEquipment] " + LastResult, this);
        }
#endif
    }
}

using HeroesOfDragonland.Items;
using HeroesOfDragonland.Multiplayer;
using UnityEngine;

namespace HeroesOfDragonland.Presentation
{
    [DisallowMultipleComponent, RequireComponent(typeof(EquipmentVisualHooks))]
    public sealed class CharacterEquipmentVisual : MonoBehaviour
    {
        [SerializeField] private CharacterVisual visual;
        private EquipmentVisualHooks equipment;
        private bool stowed;
        private void Awake() => equipment = GetComponent<EquipmentVisualHooks>();
        private void OnEnable()
        {
            equipment.VisualChanged += Changed;
            Changed(EquipmentSlot.MainHand, equipment.GetItem(EquipmentSlot.MainHand));
        }
        private void OnDisable() { equipment.VisualChanged -= Changed; }
        private void Changed(EquipmentSlot slot, ItemDefinition item)
        {
            if (slot != EquipmentSlot.MainHand || visual == null) return;
            visual.ClearAttachment(CharacterSocket.RightHand); visual.ClearAttachment(CharacterSocket.Back);
            visual.Attach(item != null && item.Weapon != null ? item.Weapon.WeaponPrefab : null, stowed ? CharacterSocket.Back : CharacterSocket.RightHand);
        }
        // Presentation hook for a future accepted equip/stow action; no inventory or damage mutation.
        public void SetWeaponStowed(bool value)
        {
            if (stowed == value || visual == null) return;
            stowed = value;
            visual.MoveAttachment(value ? CharacterSocket.RightHand : CharacterSocket.Back, value ? CharacterSocket.Back : CharacterSocket.RightHand);
        }
    }
}

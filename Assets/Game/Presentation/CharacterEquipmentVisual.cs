using System.Collections.Generic;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Cosmetics;
using HeroesOfDragonland.Multiplayer;
using UnityEngine;

namespace HeroesOfDragonland.Presentation
{
    [DefaultExecutionOrder(200), DisallowMultipleComponent, RequireComponent(typeof(EquipmentVisualHooks))]
    public sealed class CharacterEquipmentVisual : MonoBehaviour
    {
        [SerializeField] private CharacterVisual visual;
        [SerializeField] private EquipmentVisualCatalog visualCatalog;
        [SerializeField] private Renderer hairRenderer;
        private EquipmentVisualHooks equipment;
        private CharacterAppearanceVisual appearance;
        private NetworkTestPlayer player;
        private bool stowed;
        private CosmeticVisualLoadout cosmetics;
        private MaterialPropertyBlock skinProperties;
        private bool dirty, wasSuspended;
        private readonly Dictionary<EquipmentSlot, SlotView> views = new();
        private sealed class SlotView
        {
            internal string ItemId;
            internal EquipmentVisualDefinition Definition;
            internal GameObject WeaponPrefab;
            internal CharacterSocket Socket;
            internal bool Weapon;
            internal CosmeticDefinition Skin;
            internal readonly List<GameObject> Parts = new();
        }
        public EquipmentVisualCatalog Catalog => visualCatalog;
        public int VisualCount(EquipmentSlot slot) => views.TryGetValue(slot, out var view) ? view.Parts.Count : 0;
        public GameObject GetVisual(EquipmentSlot slot, int part = 0) => views.TryGetValue(slot, out var view)
            && part >= 0 && part < view.Parts.Count ? view.Parts[part] : null;
        private bool Suspended => player != null && player.Object != null && player.Object.IsValid && player.IsSuspended;
        private void Awake()
        {
            equipment = GetComponent<EquipmentVisualHooks>();
            player = GetComponent<NetworkTestPlayer>();
        }
        private void OnEnable()
        {
            equipment ??= GetComponent<EquipmentVisualHooks>();
            if (visual == null) visual = GetComponentInChildren<CharacterVisual>(true);
            appearance = visual != null ? visual.GetComponent<CharacterAppearanceVisual>() : null;
            stowed = false;
            equipment.VisualChanged += Changed;
            Refresh();
        }
        private void OnDisable()
        {
            if (equipment != null) equipment.VisualChanged -= Changed;
            for (int i = 1; i <= 6; i++) Clear((EquipmentSlot)i);
            dirty = false;
        }
        private void Changed(EquipmentSlot slot, ItemDefinition item)
        {
            // Snapshot events may arrive while the reconnect reservation hides this actor.
            if (Suspended) { dirty = true; return; }
            Reconcile(slot, item);
        }
        public void Refresh()
        {
            if (!isActiveAndEnabled || equipment == null) return;
            if (Suspended) { dirty = true; return; }
            dirty = false;
            for (int i = 1; i <= 6; i++) Reconcile((EquipmentSlot)i, equipment.GetItem((EquipmentSlot)i));
        }
        private void LateUpdate()
        {
            bool suspended = Suspended;
            if (suspended != wasSuspended)
            {
                foreach (var view in views.Values)
                    foreach (var part in view.Parts) if (part != null) part.SetActive(!suspended);
                wasSuspended = suspended;
                if (!suspended) dirty = true;
            }
            if (suspended) return;
            if (player != null && player.Object != null && player.Object.IsValid
                && cosmetics.Packed != player.CosmeticState)
            {
                cosmetics = player.Cosmetics;
                foreach (var view in views.Values) ApplySkin(view);
            }
            if (dirty) Refresh();
        }
        private void Clear(EquipmentSlot slot)
        {
            if (slot == EquipmentSlot.Head) SetHelmet(false);
            if (!views.Remove(slot, out var previous)) return;
            if (previous.Weapon && visual != null && visual.GetAttachment(previous.Socket) == previous.Parts[0])
                visual.ClearAttachment(previous.Socket);
            else foreach (var part in previous.Parts) EquipmentVisualSafety.Release(part);
        }
        private void Reconcile(EquipmentSlot slot, ItemDefinition item)
        {
            if (slot < EquipmentSlot.MainHand || slot > EquipmentSlot.Legs) return;
            if (item == null || visual == null || item.EquipmentSlot != slot) { Clear(slot); return; }
            var definition = visualCatalog != null ? visualCatalog.Resolve(item) : null;
            bool weapon = slot == EquipmentSlot.MainHand || slot == EquipmentSlot.OffHand;
            var prefab = weapon ? definition != null && definition.Parts.Count > 0 ? definition.Parts[0]?.Prefab : null : null;
            if (weapon && prefab == null) prefab = item.Weapon != null ? item.Weapon.WeaponPrefab : null;
            if (views.TryGetValue(slot, out var current) && current.ItemId == item.Id && current.Definition == definition && current.WeaponPrefab == prefab)
            {
                bool intact = true;
                foreach (var part in current.Parts) if (part == null) intact = false;
                if (intact) return; // Repeated snapshots/refreshes never spawn duplicates.
            }
            Clear(slot); // Hide stale gear immediately, even if the next recipe is invalid.
            if (definition != null && (definition.Slot != slot || !definition.TryValidate(out _)))
            { Debug.LogWarning("Invalid equipment visual recipe for " + item.name, this); return; }
            var next = new SlotView { ItemId = item.Id, Definition = definition, WeaponPrefab = prefab, Weapon = weapon };
            if (weapon)
            {
                next.Socket = slot == EquipmentSlot.MainHand ? stowed ? CharacterSocket.Back : CharacterSocket.RightHand : CharacterSocket.LeftHand;
                if (prefab == null || !EquipmentVisualSafety.CanSpawn(prefab, out _)) return;
                var instance = visual.Attach(prefab, next.Socket);
                if (instance == null) return;
                if (definition != null)
                    (stowed && slot == EquipmentSlot.MainHand ? definition.StowedPose : definition.Parts[0].Pose).Apply(instance.transform);
                next.Parts.Add(instance);
            }
            else
            {
                if (definition == null) { Debug.LogWarning("No armor visual recipe for " + item.name + "; configure the visual catalog.", this); return; }
                // Resolve every target before spawning to avoid a half-equipped visual.
                var parents = new Transform[definition.Parts.Count];
                for (int i = 0; i < parents.Length; i++)
                    if ((parents[i] = definition.Parts[i].Resolve(visual)) == null)
                    { Debug.LogWarning("Missing Humanoid armor mount for " + item.name, this); return; }
                for (int i = 0; i < parents.Length; i++)
                {
                    var part = definition.Parts[i];
                    var instance = EquipmentVisualSafety.Spawn(part.Prefab, parents[i]);
                    if (instance == null)
                    {
                        foreach (var created in next.Parts) EquipmentVisualSafety.Release(created);
                        return;
                    }
                    part.Pose.Apply(instance.transform);
                    next.Parts.Add(instance);
                }
            }
            views[slot] = next;
            if (cosmetics.Packed != 0) ApplySkin(next);
            if (slot == EquipmentSlot.Head) SetHelmet(true);
        }

        private void ApplySkin(SlotView view)
        {
            var skin = cosmetics.Get(view.Weapon ? CosmeticCategory.WeaponSkin : CosmeticCategory.ArmorSkin);
            if (view.Skin == skin) return;
            view.Skin = skin;
            skinProperties ??= new MaterialPropertyBlock();
            foreach (var part in view.Parts)
                CosmeticPresentation.Tint(part, skin?.Color ?? Color.white, skinProperties);
        }

        private void SetHelmet(bool equipped)
        {
            if (appearance != null) appearance.SetHelmet(equipped);
            else if (hairRenderer != null) hairRenderer.forceRenderingOff = equipped;
        }
        // Local presentation hook only. No replicated stow action or inventory mutation is introduced.
        public void SetWeaponStowed(bool value)
        {
            if (stowed == value || visual == null) return;
            if (views.TryGetValue(EquipmentSlot.MainHand, out var view))
            {
                var target = value ? CharacterSocket.Back : CharacterSocket.RightHand;
                if (visual.GetSocket(target) == null) return;
                visual.MoveAttachment(view.Socket, target);
                view.Socket = target;
                var part = view.Parts[0];
                if (part != null)
                {
                    if (view.Definition != null) (value ? view.Definition.StowedPose : view.Definition.Parts[0].Pose).Apply(part.transform);
                    else { part.transform.localPosition = Vector3.zero; part.transform.localRotation = Quaternion.identity; part.transform.localScale = Vector3.one; }
                }
            }
            stowed = value;
        }
    }
}

using Fusion;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.EquipmentSystem;
using HeroesOfDragonland.InventorySystem;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Player;
using HeroesOfDragonland.Presentation.Vfx;
using HeroesOfDragonland.Teams;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HeroesOfDragonland.Presentation
{
    [DefaultExecutionOrder(230), DisallowMultipleComponent]
    public sealed class BowPresentation : MonoBehaviour
    {
        private NetworkPlayerCombat combat;
        private PlayerController local;
        private CharacterVisual visual;
        private Equipment equipment;
        private Inventory inventory;
        private GameObject arrow;
        private GameObject arrowPrefab;
        private Transform posedBow;
        private Quaternion restingGrip;
        public bool ProjectileVisible => arrow != null && arrow.activeSelf;
        public float PoseWeight { get; private set; }
        public int ImpactsPresented { get; private set; }

        private void Awake()
        {
            combat = GetComponent<NetworkPlayerCombat>();
            local = GetComponent<PlayerController>();
            equipment = GetComponent<Equipment>();
            inventory = GetComponent<Inventory>();
            visual = GetComponentInChildren<CharacterVisual>(true);
        }

        private void LateUpdate()
        {
            var bow = local != null ? local.LocalBow : null;
            if (combat != null)
            {
                if (combat.Object == null || !combat.Object.IsValid || !combat.Runner.IsRunning)
                { HideArrow(); return; }
                BowProfile profile = null;
                if (inventory != null && inventory.TryResolveItem(combat.BowItemId.ToString(), out var item))
                    profile = item.Weapon?.Bow;
                ShowProjectile(combat.ArrowInFlight, profile, combat.ArrowPosition, combat.ArrowDirection);
                SetPose(combat.BowDrawing, combat.BowDrawProgress, combat.BowReleaseProgress);
            }
            else if (bow != null)
            {
                ShowProjectile(bow.InFlight, bow.Profile, bow.Position, bow.Direction);
                SetPose(bow.Drawing, bow.DrawProgress, bow.ReleaseRemaining > 0 ? bow.ReleaseRemaining / bow.ReleaseDuration : 0);
            }
        }

        private void ShowProjectile(bool flying, BowProfile profile, Vector3 position, Vector3 direction)
        {
            var prefab = flying && profile != null ? profile.ProjectileVisual : null;
            if (prefab == null || direction.sqrMagnitude < .01f) { HideArrow(); return; }
            if (arrow == null || prefab != arrowPrefab)
            {
                if (arrow != null) { arrow.SetActive(false); Destroy(arrow); }
                arrow = Instantiate(prefab, transform);
                arrowPrefab = prefab;
            }
            arrow.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
            arrow.SetActive(true);
        }

        private void HideArrow() { if (arrow != null) arrow.SetActive(false); }

        private void SetPose(bool drawing, float draw, float release)
        {
            PoseWeight = drawing ? Mathf.SmoothStep(.1f, 1, draw) : release;
            if (PoseWeight <= 0 || visual == null || visual.Animator == null || !visual.Animator.isHuman
                || equipment == null || equipment.EquippedWeapon == null || equipment.EquippedWeapon.Archetype != WeaponArchetype.Bow)
            { ResetGrip(); return; }
            // Right hand presents the bow. Left hand draws toward the cheek, then follows through.
            var root = visual.transform;
            var aim = root.forward;
            Point(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, aim + root.right * .2f, PoseWeight);
            Point(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, aim, PoseWeight);
            Point(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm,
                Vector3.Lerp(aim + root.right * .35f, -root.right - aim * .2f, drawing ? draw : 1), PoseWeight);
            Point(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
                Vector3.Lerp(aim + root.right, root.right + root.up * .8f, drawing ? draw : release), PoseWeight);
            var bow = visual.GetAttachment(CharacterSocket.RightHand);
            if (bow == null) { ResetGrip(); return; }
            if (posedBow != bow.transform)
            {
                ResetGrip();
                posedBow = bow.transform;
                restingGrip = posedBow.localRotation;
            }
            posedBow.rotation = Quaternion.LookRotation(aim, root.up);
            SetString(posedBow, drawing ? draw : 0);
        }

        private void ResetGrip()
        {
            if (posedBow != null)
            {
                if (posedBow.parent == visual.GetSocket(CharacterSocket.RightHand)) posedBow.localRotation = restingGrip;
                SetString(posedBow, 0);
            }
            posedBow = null;
        }

        private static void SetString(Transform bow, float draw)
        {
            var center = new Vector3(0, 0, -.01f - draw * .34f);
            FitString(bow.Find("StringUpper"), new Vector3(0, .72f, -.01f), center);
            FitString(bow.Find("StringLower"), new Vector3(0, -.72f, -.01f), center);
        }

        private static void FitString(Transform part, Vector3 tip, Vector3 center)
        {
            if (part == null) return;
            part.SetLocalPositionAndRotation((tip + center) * .5f, Quaternion.FromToRotation(Vector3.up, center - tip));
            part.localScale = new Vector3(.005f, Vector3.Distance(center, tip) * .5f, .005f);
        }

        private void Point(HumanBodyBones joint, HumanBodyBones next, Vector3 direction, float weight)
        {
            var bone = visual.Animator.GetBoneTransform(joint);
            var child = visual.Animator.GetBoneTransform(next);
            if (bone != null && child != null)
                bone.rotation = Quaternion.Slerp(bone.rotation,
                    Quaternion.FromToRotation(child.position - bone.position, direction) * bone.rotation, weight);
        }

        private void OnGUI()
        {
            if (Application.isBatchMode || !Application.isFocused || Cursor.lockState != CursorLockMode.Locked
                || Mouse.current?.rightButton.isPressed != true
                || equipment == null || equipment.EquippedWeapon == null || equipment.EquippedWeapon.Archetype != WeaponArchetype.Bow
                || (combat != null && (combat.Object == null || !combat.Object.IsValid || !combat.HasInputAuthority))) return;
            var center = new Vector2(Screen.width * .5f, Screen.height * .5f);
            GUI.DrawTexture(new Rect(center.x - 6, center.y - 1, 12, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(center.x - 1, center.y - 6, 2, 12), Texture2D.whiteTexture);
        }

        internal void PresentImpact(Vector3 point, NetworkRunner runner, TeamId team, bool confirmed)
        {
            ImpactsPresented++;
            if (confirmed) return;
            var pool = runner != null ? VfxAudience.Pool(runner) : FindAnyObjectByType<VfxPool>();
            pool?.Play(VfxKind.MeleeHit, point, VfxAudience.Relation(runner, team, true));
        }

        private void OnDisable() { HideArrow(); ResetGrip(); PoseWeight = 0; }
    }
}

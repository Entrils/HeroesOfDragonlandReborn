using System;
using HeroesOfDragonland.Abilities;
using HeroesOfDragonland.EquipmentSystem;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Targeting;
using HeroesOfDragonland.Teams;
using UnityEngine;
using HeroesOfDragonland.Skills;
using UnityEngine.SceneManagement;

namespace HeroesOfDragonland.Combat
{
    // Shared offline and server simulation. Its caller supplies intent and owns authority.
    internal sealed class BowShot
    {
        private const float Radius = .045f;
        private readonly GameObject owner;
        private readonly Equipment gear;
        private readonly CharacterStats stats;
        private WeaponDefinition weapon;
        private ItemDefinition item;
        private Vector3 aim;
        private Targetable assistedTarget;
        private float remaining;
        private float distance;
        private float speed;
        private float damage;
        private string releasedFrom;
        private bool inFlight;
        private RaycastHit[] hits = new RaycastHit[16];
        private Collider[] overlaps = new Collider[8];
        public bool Drawing => weapon != null && remaining > 0;
        public bool InFlight => inFlight;
        public bool ResolvingHit { get; private set; }
        public Vector3 Position { get; private set; }
        public Vector3 Direction { get; private set; }
        public Vector3 Facing => Vector3.ProjectOnPlane(aim, Vector3.up).normalized;
        public float DrawDuration { get; private set; }
        public float DrawProgress => Drawing ? Mathf.Clamp01(1 - remaining / DrawDuration) : 0;
        public float ReleaseRemaining { get; private set; }
        public float ReleaseDuration { get; private set; }
        public BowProfile Profile { get; private set; }
        public event Action<Vector3> Impact;

        public BowShot(GameObject owner)
        {
            this.owner = owner;
            gear = owner.GetComponent<Equipment>();
            stats = owner.GetComponent<CharacterStats>();
        }

        public static Vector3 Origin(Transform root)
        {
            // Network capsules are centered on the root. Offline controllers start at the feet.
            Vector3 center = root.TryGetComponent<CapsuleCollider>(out var capsule) ? capsule.center
                : root.TryGetComponent<CharacterController>(out var controller) ? controller.center : Vector3.up;
            return root.TransformPoint(center) + Vector3.up * .35f;
        }

        public bool TryStart(Vector3 direction, Targetable target)
        {
            var equipped = gear != null ? gear.EquippedWeapon : null;
            if (Drawing || inFlight || ReleaseRemaining > 0 || stats == null || !stats.CanMutate || stats.IsDead
                || equipped == null || equipped.Archetype != WeaponArchetype.Bow || !equipped.TryValidate(out _)
                || !gear.CanUseEquippedWeapon(out _)
                || owner.GetComponent<AbilityCaster>() is { IsCasting: true }
                || owner.GetComponent<ParryDefense>() is { IsBusy: true }
                || !ValidAim(direction) || !stats.TrySpendStamina(equipped.StaminaCost)) return false;
            weapon = equipped;
            item = gear.GetItem(EquipmentSlot.MainHand);
            Profile = weapon.Bow;
            float attackSpeed = weapon.AttackSpeed * (1f + stats.SkillBonus(SkillEffect.AttackSpeed));
            DrawDuration = Mathf.Max(.05f, Profile.DrawSeconds / attackSpeed);
            ReleaseDuration = Mathf.Max(.05f, Profile.ReleaseSeconds / attackSpeed);
            remaining = DrawDuration;
            UpdateAim(direction, target);
            return true;
        }

        public void UpdateAim(Vector3 direction, Targetable target)
        {
            if (!Drawing || !ValidAim(direction)) return;
            aim = direction.normalized;
            assistedTarget = target;
        }

        public void Tick(float dt, bool interruptsDraw)
        {
            if (stats == null || !stats.CanMutate || stats.IsDead) { Cancel(); return; }
            ReleaseRemaining = Mathf.Max(0, ReleaseRemaining - dt);
            if (Drawing)
            {
                if (interruptsDraw || owner.GetComponent<ParryDefense>() is { IsBusy: true }
                    || owner.GetComponent<AbilityCaster>() is { IsCasting: true }
                    || gear.GetItem(EquipmentSlot.MainHand) != item || !gear.CanUseEquippedWeapon(out _))
                { CancelDraw(); return; }
                remaining -= dt;
                if (remaining > 0) return;
                Release();
                return;
            }
            if (!inFlight) return;
            float step = Mathf.Min(distance, speed * dt);
            if (FirstHit(Position, Direction, step, Radius, out var hit))
            {
                inFlight = false;
                var target = hit.collider.GetComponentInParent<DamageReceiver>();
                if (target != null)
                {
                    ResolvingHit = true;
                    try { target.TakeDamage(damage, owner, releasedFrom); }
                    finally { ResolvingHit = false; }
                }
                Impact?.Invoke(hit.point);
                return;
            }
            Position += Direction * step;
            distance -= step;
            if (distance <= 0) inFlight = false;
        }

        private void Release()
        {
            var arrow = Profile.BasicArrow;
            var supply = owner.GetComponent<IArrowSupply>();
            if ((supply != null && !supply.TryTakeArrow(arrow, out arrow)) || arrow == null || !arrow.IsValid)
            { CancelDraw(); return; }
            Position = Origin(owner.transform);
            Direction = AssistedDirection(Position);
            ReleaseRemaining = ReleaseDuration;
            if (MuzzleBlocked())
            {
                CancelDraw();
                Impact?.Invoke(Position);
                return;
            }
            releasedFrom = item?.DisplayName ?? "Bow shot";
            distance = weapon.Range;
            speed = Profile.ProjectileSpeed * arrow.SpeedMultiplier;
            damage = gear.GetModifiedStat(ItemStat.AttackDamage, weapon.BaseDamage) * arrow.DamageMultiplier;
            inFlight = float.IsFinite(speed) && float.IsFinite(damage);
            CancelDraw();
        }

        private Vector3 AssistedDirection(Vector3 origin)
        {
            if (assistedTarget == null || !assistedTarget.IsValid || Profile.AssistanceDegrees <= 0) return aim;
            var receiver = assistedTarget.GetComponentInParent<DamageReceiver>();
            if (receiver == null || receiver.gameObject == owner || !receiver.IsAlive
                || !TeamIdentity.CanDamage(owner, receiver.gameObject)) return aim;
            var point = assistedTarget.AimPosition;
            if (receiver.TryGetComponent<CapsuleCollider>(out var capsule) && capsule.enabled)
                point = Vector3.Lerp(capsule.ClosestPoint(point), capsule.bounds.center, .15f);
            Vector3 offset = point - origin;
            if (offset.sqrMagnitude < .01f
                || offset.sqrMagnitude > weapon.Range * weapon.Range
                || Vector3.Angle(aim, offset) > Profile.AssistanceDegrees) return aim;
            var corrected = offset.normalized;
            return FirstHit(origin, corrected, offset.magnitude, 0, out var hit)
                && hit.collider.GetComponentInParent<DamageReceiver>() == receiver ? corrected : aim;
        }

        private bool FirstHit(Vector3 origin, Vector3 direction, float range, float radius, out RaycastHit nearest)
        {
            nearest = default;
            float best = float.PositiveInfinity;
            var scene = owner.scene.GetPhysicsScene();
            int count;
            while (true)
            {
                count = radius > 0
                    ? scene.SphereCast(origin, radius, direction, hits, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    : scene.Raycast(origin, direction, hits, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                if (count < hits.Length) break;
                Array.Resize(ref hits, hits.Length * 2);
            }
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.distance >= best || hit.collider.transform.IsChildOf(owner.transform)) continue;
                nearest = hit;
                best = hit.distance;
            }
            return nearest.collider != null;
        }

        private bool MuzzleBlocked()
        {
            var scene = owner.scene.GetPhysicsScene();
            int count;
            while ((count = scene.OverlapSphere(Position, Radius, overlaps, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore)) == overlaps.Length)
                Array.Resize(ref overlaps, overlaps.Length * 2);
            for (int i = 0; i < count; i++)
                if (!overlaps[i].transform.IsChildOf(owner.transform)) return true;
            return false;
        }

        public void CancelDraw() { weapon = null; item = null; remaining = 0; assistedTarget = null; }
        public void Cancel() { CancelDraw(); inFlight = false; ReleaseRemaining = 0; }
        private static bool ValidAim(Vector3 direction) => float.IsFinite(direction.x) && float.IsFinite(direction.y)
            && float.IsFinite(direction.z) && direction.sqrMagnitude >= .01f && direction.sqrMagnitude <= 4
            && (direction.x * direction.x + direction.z * direction.z) > .001f;
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.EquipmentSystem;
using HeroesOfDragonland.InventorySystem;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Presentation;
using HeroesOfDragonland.Stats;
using UnityEditor;
using UnityEngine;

namespace HeroesOfDragonland.Art.Editor
{
    public static class WeaponMovesetValidation
    {
        public static string Status { get; private set; } = "Not run";
        private static void Check(bool value, string label)
        { if (!value) throw new InvalidOperationException("Weapon movesets: " + label); }
        private static void Cancel(MeleeAttack melee) { melee.enabled = false; melee.enabled = true; melee.TickAttack(melee.RestartRemaining + .01f); }
        public static void ValidateAssets()
        {
            foreach (WeaponArchetype kind in new[] { WeaponArchetype.Sword, WeaponArchetype.Greatsword, WeaponArchetype.Spear, WeaponArchetype.Staff })
            {
                var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Game/Data/Weapons/" + kind + ".asset");
                Check(weapon != null && weapon.TryValidate(out _) && weapon.Moveset != null, kind + " weapon linkage");
                Check(weapon.Moveset.Archetype == kind && weapon.Moveset.LightAttackCount >= 2, kind + " sequence");
                for (int i = 0; i < weapon.Moveset.LightAttackCount; i++)
                    Check(weapon.Moveset.GetAnimation(false, i)?.humanMotion == true, kind + " light clip " + i);
                Check(weapon.Moveset.GetAnimation(true, 0)?.humanMotion == true, kind + " heavy clip");
            }
            CharacterAnimationValidation.ValidateAssets();
            Debug.Log("[WeaponMovesetValidation] PASS four asset mappings, sequences, Humanoid clips and existing animation architecture.");
        }
        public static void Start()
        {
            ValidateAssets();
            StartFeedback();
        }

        public static void StartFeedback()
        {
            Check(Application.isPlaying, "Play Mode required");
            Status = "RUNNING";
            UnityEngine.Object.FindAnyObjectByType<MultiplayerBootstrap>().StartCoroutine(Run());
        }
        private static IEnumerator Run()
        {
            var temporary = new List<UnityEngine.Object>();
            var actor = new GameObject("Moveset acceptance fixture"); actor.SetActive(false); temporary.Add(actor);
            actor.transform.position = new Vector3(0, 1, 80);
            var inventory = actor.AddComponent<Inventory>();
            var equipment = actor.AddComponent<Equipment>();
            var melee = actor.AddComponent<MeleeAttack>();
            var stats = actor.GetComponent<CharacterStats>();
            var catalog = ScriptableObject.CreateInstance<ItemCatalog>(); temporary.Add(catalog);
            var items = new List<ItemDefinition>();
            try
            {
                foreach (WeaponArchetype kind in new[] { WeaponArchetype.Sword, WeaponArchetype.Greatsword, WeaponArchetype.Spear, WeaponArchetype.Staff, WeaponArchetype.Bow })
                {
                    var name = kind == WeaponArchetype.Bow ? "HuntingBow" : kind.ToString();
                    var source = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Game/Data/Weapons/" + name + ".asset");
                    var weapon = UnityEngine.Object.Instantiate(source); temporary.Add(weapon);
                    var w = new SerializedObject(weapon); w.FindProperty("statRequirements").arraySize = 0; w.ApplyModifiedPropertiesWithoutUndo();
                    var item = ScriptableObject.CreateInstance<ItemDefinition>(); temporary.Add(item);
                    var d = new SerializedObject(item);
                    d.FindProperty("id").stringValue = Guid.NewGuid().ToString("N");
                    d.FindProperty("displayName").stringValue = kind.ToString();
                    d.FindProperty("category").enumValueIndex = (int)ItemCategory.Weapon;
                    d.FindProperty("equipmentSlot").enumValueIndex = (int)EquipmentSlot.MainHand;
                    d.FindProperty("weapon").objectReferenceValue = weapon; d.ApplyModifiedPropertiesWithoutUndo(); items.Add(item);
                }
                var c = new SerializedObject(catalog); var list = c.FindProperty("items"); list.arraySize = items.Count;
                for (int i = 0; i < items.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
                c.ApplyModifiedPropertiesWithoutUndo();
                var inv = new SerializedObject(inventory); inv.FindProperty("catalog").objectReferenceValue = catalog; inv.ApplyModifiedPropertiesWithoutUndo();
                actor.SetActive(true);
                foreach (var item in items) Check(inventory.TryAdd(item), "fixture inventory");
                var visualObject = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(HumanoidAssetBuilder.VisualPath));
                temporary.Add(visualObject); var visual = visualObject.GetComponent<CharacterVisual>(); visualObject.transform.position = actor.transform.position;
                var target = new GameObject("Moveset hit target"); temporary.Add(target);
                target.transform.position = actor.transform.position + new Vector3(0, 1, .8f);
                target.AddComponent<BoxCollider>(); var receiver = target.AddComponent<DamageReceiver>();
                var targetStats = target.GetComponent<CharacterStats>();
                var feedback = CombatFeedbackProfiles.Current;
                Check(feedback != null && feedback.TrailMaterial != null, "combat feedback asset and trail material");
                DamageImpact lastImpact = default;
                DamageImpactCue lastCue = default;
                int contacts = 0;
                receiver.ContactResolved += (cue, source, impact) =>
                {
                    if (source != actor) return;
                    contacts++;
                    lastImpact = impact;
                    lastCue = cue;
                };
                for (int kind = 0; kind < items.Count - 1; kind++)
                {
                    Check(equipment.TryEquip(kind, EquipmentSlot.MainHand, out _), "equip " + items[kind].DisplayName);
                    var weapon = equipment.EquippedWeapon; var moveset = weapon.Moveset;
                    // M43's ordered buffered transitions are covered by AttackChainValidation.
                    for (int i = 0; i < 1; i++)
                    {
                        stats.ResetToFull(); targetStats.ResetToFull();
                        int index = i % moveset.LightAttackCount; var move = moveset.GetMove(false, index);
                        float before = stats.CurrentStamina;
                        Check(melee.TryAttack(), "light accepted");
                        Check(melee.MoveIndex == index && melee.Definition == move.Profile, "ordered/wrapping sequence");
                        Check(Mathf.Approximately(before - stats.CurrentStamina, move.StaminaCost), "per-move stamina");
                        Check(Mathf.Approximately(melee.SwingDuration, move.Profile.AttackDuration / move.AttackSpeed)
                            && Mathf.Approximately(melee.MovementMultiplier, move.MovementMultiplier)
                            && Mathf.Approximately(melee.StaggerPower, move.StaggerPower), "committed timing, movement, stagger");
                        Check(!melee.TryHeavyAttack(), "busy cannot replace committed attack");
                        contacts = 0;
                        Physics.SyncTransforms();
                        melee.TickAttack(move.Profile.ActiveStartTime / move.AttackSpeed * .5f);
                        Check(targetStats.CurrentHealth == targetStats.MaxHealth, "no hit before server window");
                        melee.TickAttack((move.Profile.ActiveEndTime - move.Profile.ActiveStartTime * .5f) / move.AttackSpeed + .001f);
                        Check(targetStats.CurrentHealth < targetStats.MaxHealth, "server hit during/crossing window");
                        var expectedWeapon = weapon.Archetype switch
                        {
                            WeaponArchetype.Greatsword => ImpactWeapon.Greatsword,
                            WeaponArchetype.Spear => ImpactWeapon.Spear,
                            WeaponArchetype.Staff => ImpactWeapon.Staff,
                            _ => ImpactWeapon.Sword
                        };
                        Check(contacts == 1 && lastCue == DamageImpactCue.MeleeHit && lastImpact.Weapon == expectedWeapon
                            && Vector3.Dot(lastImpact.Direction, actor.transform.forward) > .99f, "classified and directional contact");
                        float hitHealth = targetStats.CurrentHealth; melee.TickAttack(.001f);
                        Check(targetStats.CurrentHealth == hitHealth, "one hit per target per move");
                        visual.SetWeaponPresentation(weapon, false, index);
                        var snapshot = new CharacterVisualState { Attacking = true, ActionSequence = kind + 1, ActionDuration = melee.SwingDuration };
                        if (weapon.Archetype != WeaponArchetype.Staff)
                        {
                            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Weapons/" + weapon.Archetype + ".prefab");
                            Check(visual.Attach(model, CharacterSocket.RightHand) != null, "visible weapon fixture");
                        }
                        visual.Apply(snapshot, .1f);
                        var trail = visual.GetComponentInChildren<TrailRenderer>();
                        Check(trail == null || !trail.emitting, "no trail during anticipation");
                        if (weapon.Archetype != WeaponArchetype.Staff)
                        {
                            snapshot.ActionAge = (move.Profile.ActiveStartTime + move.Profile.ActiveEndTime) * .5f / move.AttackSpeed;
                            visual.Apply(snapshot, .1f);
                            trail = visual.GetComponentInChildren<TrailRenderer>();
                            Check(trail != null && trail.emitting, "trail emits in accepted active window");
                            snapshot.ActionAge = melee.SwingDuration;
                            visual.Apply(snapshot, .1f);
                            Check(!trail.emitting, "trail stops in follow-through");
                        }
                        yield return null;
                        var controller = visual.Animator.runtimeAnimatorController as AnimatorOverrideController;
                        var baseClip = visual.AnimationLibrary.Get(CharacterMotion.LightAttack);
                        Check(controller != null && controller[baseClip] == moveset.GetAnimation(false, index), "step clip override");
                        float remainder = (move.Profile.AttackDuration + move.Profile.RecoveryDuration - move.Profile.ActiveEndTime) / move.AttackSpeed;
                        melee.TickAttack(remainder);
                    }
                    melee.TickAttack(moveset.ComboResetDelay + .01f); stats.ResetToFull();
                    Check(melee.TryAttack() && melee.MoveIndex == 0, "idle timeout resets sequence");
                    Cancel(melee);
                    stats.TrySpendStamina(stats.CurrentStamina);
                    Check(!melee.TryAttack(), "insufficient stamina rejects attack");
                    stats.ResetToFull();
                    Check(melee.TryAttack() && melee.MoveIndex == 0, "rejected request does not advance sequence"); Cancel(melee);
                    stats.ResetToFull(); float stamina = stats.CurrentStamina;
                    var heavy = moveset.GetMove(true, 0);
                    Check(melee.TryHeavyAttack() && melee.IsHeavy && melee.Definition == heavy.Profile, "heavy selects independent profile");
                    Check(Mathf.Approximately(stamina - stats.CurrentStamina, heavy.StaminaCost), "heavy pays once without legacy multiplier");
                    visual.SetWeaponPresentation(weapon, true, 0);
                    visual.Apply(new CharacterVisualState { Attacking = true, HeavyAttack = true, ActionSequence = 50 + kind, ActionDuration = melee.SwingDuration }, .1f);
                    yield return null;
                    Check(((AnimatorOverrideController)visual.Animator.runtimeAnimatorController)[visual.AnimationLibrary.Get(CharacterMotion.HeavyAttack)] == moveset.GetAnimation(true, 0), "heavy clip override");
                    Cancel(melee);
                    // One target just outside reach must remain untouched, including on a long server tick.
                    targetStats.ResetToFull(); target.transform.position = actor.transform.position + new Vector3(0, 1, moveset.GetMove(false, 0).Range + 2);
                    Physics.SyncTransforms(); stats.ResetToFull(); Check(melee.TryAttack(), "range fixture attack");
                    melee.TickAttack(melee.SwingDuration);
                    Check(targetStats.CurrentHealth == targetStats.MaxHealth, "range enforced by server geometry"); Cancel(melee);
                    target.transform.position = actor.transform.position + new Vector3(0, 1, .8f);
                }
                Check(feedback.Resolve(DamageImpactCue.MeleeHit, ImpactWeapon.Greatsword).hitStop
                    > feedback.Resolve(DamageImpactCue.MeleeHit, ImpactWeapon.Sword).hitStop, "greatsword feedback carries more weight");
                Check(equipment.TryEquip(items.Count - 1, EquipmentSlot.MainHand, out _), "equip bow");
                stats.ResetToFull(); targetStats.ResetToFull();
                target.transform.position = BowNetworkProbe.OfflineCheckOrigin(actor) + actor.transform.forward * 6;
                Physics.SyncTransforms();
                contacts = 0;
                BowNetworkProbe.ValidateOfflineContact(actor);
                Check(contacts == 1 && lastCue == DamageImpactCue.MeleeHit && lastImpact.Weapon == ImpactWeapon.Bow
                    && targetStats.CurrentHealth < targetStats.MaxHealth, "bow publishes one confirmed swept contact");
                Check(equipment.TryUnequip(EquipmentSlot.MainHand, out _), "unequip");
                visual.SetWeaponPresentation(null, false, 0);
                Check(((AnimatorOverrideController)visual.Animator.runtimeAnimatorController)[visual.AnimationLibrary.Get(CharacterMotion.LightAttack)] == visual.AnimationLibrary.Get(CharacterMotion.LightAttack), "unequip restores default clip");
                Status = "PASS sword/greatsword/spear/staff/bow contact, standalone light/reset, rejected costs, heavy profiles, hit windows/dedup/range and actual clip overrides; chained transitions covered by M43.";
                Debug.Log("[WeaponMovesetValidation] " + Status);
            }
            finally
            {
                foreach (var obj in temporary.AsEnumerable().Reverse()) if (obj != null) UnityEngine.Object.Destroy(obj);
                if (Status == "RUNNING") Status = "FAILED; see Console";
            }
        }
    }
}

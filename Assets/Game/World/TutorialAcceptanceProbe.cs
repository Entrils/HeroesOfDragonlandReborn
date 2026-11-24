#if UNITY_EDITOR || DEBUG
using System;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Interaction;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Player;
using HeroesOfDragonland.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HeroesOfDragonland.World
{
    // Development-only offline traversal. Uses real moves, impacts, interactions and rewards.
    [DefaultExecutionOrder(200)]
    public sealed class TutorialAcceptanceProbe : MonoBehaviour
    {
        private TutorialFlow flow;
        private PlayerController player;
        private PlayerMovement movement;
        private MeleeAttack melee;
        private CombatDefense defense;
        private ParryDefense parry;
        private PlayerDodge dodge;
        private TargetLock targeting;
        private PlayerInteractor interactor;
        private TutorialFlow.Lesson last = (TutorialFlow.Lesson)(-1);
        private float entered;
        private bool began, success;
        private string screenshotPath;

        internal void Bind(TutorialFlow tutorial) => flow = tutorial;
        private void Start()
        {
            Application.targetFrameRate = 60;
            player = flow.Stats.GetComponent<PlayerController>();
            movement = player.GetComponent<PlayerMovement>();
            melee = player.GetComponent<MeleeAttack>();
            defense = player.GetComponent<CombatDefense>();
            dodge = player.GetComponent<PlayerDodge>();
            parry = player.GetComponent<ParryDefense>();
            targeting = player.GetComponent<TargetLock>();
            interactor = player.GetComponent<PlayerInteractor>();
            var args = Environment.GetCommandLineArgs();
            int capture = Array.IndexOf(args, "-hodrTutorialScreenshot");
            if (capture >= 0 && capture + 1 < args.Length && !Application.isBatchMode) screenshotPath = args[capture + 1];
            if (Array.IndexOf(args, "-hodrTutorialSkip") >= 0)
            {
                success = true;
                flow.SetPanel(TutorialFlow.Panel.Pause);
                if (Time.timeScale != 0) throw new InvalidOperationException("[TutorialCheck] FAIL pause did not stop the arena.");
                SceneManager.sceneLoaded += ReturnAfterSkip;
                flow.Skip();
            }
        }

        private void Update()
        {
            if (success || player == null) return;
            if (last != flow.Step)
            {
                last = flow.Step;
                entered = Time.unscaledTime;
                began = false;
                Debug.Log("[TutorialCheck] " + last);
            }
            if (Time.unscaledTime - entered > 22)
            {
                enabled = false;
                Debug.LogError("[TutorialCheck] FAIL timed out at " + last);
                Application.Quit(1);
                return;
            }
            player.enabled = false;
            var marker = flow.Marker;
            switch (last)
            {
                case TutorialFlow.Lesson.Move: movement.Tick(Vector3.forward, false, Time.deltaTime); break;
                case TutorialFlow.Lesson.Look:
                    flow.ViewCamera.GetComponent<ThirdPersonCamera>().ApplyLook(new Vector2(45, 0), true, Time.deltaTime);
                    break;
                case TutorialFlow.Lesson.Lock:
                    Approach(marker.position, 1.5f);
                    flow.ViewCamera.transform.LookAt(marker.position + Vector3.up);
                    targeting.Toggle();
                    break;
                case TutorialFlow.Lesson.Light: Swing(marker.position, false); break;
                case TutorialFlow.Lesson.Heavy: Swing(marker.position, true); break;
                case TutorialFlow.Lesson.Dodge:
                    movement.Tick(Vector3.zero, false, Time.deltaTime);
                    dodge.TryDodge(Vector3.back);
                    break;
                case TutorialFlow.Lesson.Block:
                    Approach(marker.position, 1.6f);
                    defense.SetBlockIntent(true, false);
                    break;
                case TutorialFlow.Lesson.Parry:
                    defense.SetBlockIntent(false, false);
                    Approach(marker.position, 1.6f);
                    if (screenshotPath != null && flow.DefenseProgress > .5f)
                    {
                        ScreenCapture.CaptureScreenshot(screenshotPath);
                        screenshotPath = null;
                    }
                    if (flow.DefenseCue.StartsWith("PARRY NOW", StringComparison.Ordinal)) parry.TryStart(Vector3.forward);
                    break;
                case TutorialFlow.Lesson.Stamina:
                    if (!began)
                    {
                        began = true;
                        flow.Stats.TrySpendStamina(24);
                    }
                    break;
                case TutorialFlow.Lesson.Mana:
                    if (flow.OpenPanel != TutorialFlow.Panel.Skills) flow.SetPanel(TutorialFlow.Panel.Skills);
                    flow.LearnPracticeSkill();
                    float mana = flow.Stats.CurrentMana;
                    flow.CastPracticeSkill();
                    if (flow.Step != last && flow.Stats.CurrentMana >= mana) throw new InvalidOperationException("[TutorialCheck] FAIL cast used no mana");
                    break;
                case TutorialFlow.Lesson.Interact:
                case TutorialFlow.Lesson.Chest:
                    Approach(marker.position, 1.2f);
                    interactor.TryInteract(out _);
                    break;
                case TutorialFlow.Lesson.Inventory: flow.SetPanel(TutorialFlow.Panel.Bag); break;
                case TutorialFlow.Lesson.Equip:
                    for (int i = 0; i < flow.Bag.SlotCount && flow.Step == last; i++) flow.EquipTrainingWeapon(i);
                    break;
                case TutorialFlow.Lesson.Camp:
                    if (!began)
                    {
                        began = true;
                        var equipment = player.GetComponent<CharacterEquipmentVisual>();
                        var sword = equipment != null ? equipment.GetVisual(EquipmentSlot.MainHand) : null;
                        if (sword == null || sword.name != "PV_IronSword(Clone)")
                            throw new InvalidOperationException("[TutorialCheck] FAIL equipped training sword model is missing.");
                        var rig = player.GetComponentInChildren<CharacterVisual>();
                        if (sword.transform.parent != rig.GetSocket(CharacterSocket.RightHand))
                            throw new InvalidOperationException("[TutorialCheck] FAIL training sword is not held in the right hand.");
                        var chest = GameObject.Find("Training chest").GetComponent<LootChest>();
                        if (chest.Revision != 1)
                            throw new InvalidOperationException("[TutorialCheck] FAIL training chest awarded more than once.");
                        Debug.Log("[TutorialCheck] PASS single training chest claim and IronSword in hand.");
                    }
                    foreach (var enemy in flow.PracticeCamp.Enemies)
                        if (!enemy.GetComponent<HeroesOfDragonland.Stats.CharacterStats>().IsDead)
                        { Swing(enemy.transform.position, false); break; }
                    break;
                case TutorialFlow.Lesson.Level:
                    flow.SetPanel(TutorialFlow.Panel.Skills);
                    flow.AllocatePoint();
                    if (flow.Step != last && flow.Progression.GetAllocatedPoints(ItemStat.Vitality) != 1)
                        throw new InvalidOperationException("[TutorialCheck] FAIL level-up point not applied");
                    break;
                case TutorialFlow.Lesson.Gate:
                case TutorialFlow.Lesson.King:
                    Swing(marker.position, false);
                    break;
                case TutorialFlow.Lesson.Finished:
                    success = true;
                    SceneManager.sceneLoaded += ReturnAfterVictory;
                    TutorialProgress.ReturnToMenu();
                    break;
            }
        }

        private static void ReturnAfterSkip(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= ReturnAfterSkip;
            CheckMenu(scene);
            Debug.Log("[TutorialCheck] PASS pause, skip and return to main menu.");
            Application.Quit();
        }

        private static void ReturnAfterVictory(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= ReturnAfterVictory;
            CheckMenu(scene);
            Debug.Log("[TutorialCheck] PASS all 18 lessons, offline King victory and return to main menu.");
            Application.Quit();
        }

        private static void CheckMenu(Scene scene)
        {
            if (scene.path != TutorialProgress.BattlefieldPath || Time.timeScale != 1)
                throw new InvalidOperationException("[TutorialCheck] FAIL main menu did not resume correctly.");
        }

        private void Swing(Vector3 position, bool heavy)
        {
            if (melee.IsBusy) return;
            Approach(position, 1.35f);
            if (heavy) melee.TryHeavyAttack(Vector3.forward);
            else melee.TryAttack(Vector3.forward);
        }

        private void Approach(Vector3 point, float distance)
        {
            var capsule = player.GetComponent<CharacterController>();
            capsule.enabled = false;
            player.transform.SetPositionAndRotation(new Vector3(point.x, .015f, point.z - distance), Quaternion.identity);
            capsule.enabled = true;
            Physics.SyncTransforms();
        }
    }
}
#endif

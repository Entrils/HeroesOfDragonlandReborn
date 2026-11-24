using HeroesOfDragonland.AI;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.EquipmentSystem;
using HeroesOfDragonland.InventorySystem;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Match;
using HeroesOfDragonland.Player;
using HeroesOfDragonland.Progression;
using HeroesOfDragonland.Skills;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Teams;
using HeroesOfDragonland.World.Camps;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HeroesOfDragonland.World
{
    [RequireComponent(typeof(TutorialView))]
    public sealed class TutorialFlow : MonoBehaviour
    {
        internal enum Lesson { Move, Look, Lock, Light, Heavy, Dodge, Block, Parry, Stamina,
            Mana, Interact, Chest, Inventory, Equip, Camp, Level, Gate, King, Finished }
        internal enum Panel { None, Pause, Bag, Skills }

        [SerializeField] private PlayerController player;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Transform movementMark;
        [SerializeField] private CharacterStats trainingTarget;
        [SerializeField] private EnemyMeleeAttack practiceGuard;
        [SerializeField] private EnemyDefinition guardDefinition;
        [SerializeField] private TutorialLever lever;
        [SerializeField] private LootChest chest;
        [SerializeField] private ItemDefinition trainingWeapon;
        [SerializeField] private Camp camp;
        [SerializeField] private FortressGate gate;
        [SerializeField] private KingObjective enemyKing;
        [SerializeField] private MatchManager match;
        internal CharacterStats Stats { get; private set; }
        internal SessionProgression Progression { get; private set; }
        internal Inventory Bag { get; private set; }
        internal Equipment Gear { get; private set; }
        internal Lesson Step { get; private set; }
        internal Panel OpenPanel { get; private set; }
        internal Camera ViewCamera => playerCamera;
        internal string Feedback { get; private set; } = "";
        internal ItemDefinition TrainingWeapon => trainingWeapon;
        internal SkillDefinition PracticeSkill => skills.GetDefinition(3);
        internal bool PracticeSkillLearned => skills.IsLearned(3);

        private SessionSkills skills;
        private TargetLock targeting;
        private MeleeAttack melee;
        private PlayerDodge dodge;
        private CombatDefense defense;
        private ParryDefense parry;
        private EnemyThreatTarget threat;
        private Quaternion lookStart;
        private bool defended, landed, sawStaminaSpend;
        private float guardStarted, nextStrike, feedbackUntil, targetHealth;
        private float pauseScale = 1;
        private int resumeInputFrame;
        private bool canSave = true;

        private void Start()
        {
            Stats = player.GetComponent<CharacterStats>();
            Bag = player.GetComponent<Inventory>();
            Gear = player.GetComponent<Equipment>();
            Progression = player.GetComponent<SessionProgression>();
            skills = player.GetComponent<SessionSkills>();
            targeting = player.GetComponent<TargetLock>();
            melee = player.GetComponent<MeleeAttack>();
            dodge = player.GetComponent<PlayerDodge>();
            defense = player.GetComponent<CombatDefense>();
            parry = player.GetComponent<ParryDefense>();
            threat = player.GetComponent<EnemyThreatTarget>();
            targeting.SetViewCamera(playerCamera);
            practiceGuard.Configure(guardDefinition);
            threat.Receiver.ImpactResolved += ObserveImpact;
            trainingTarget.HealthChanged += TargetHealthChanged;
            Stats.StaminaChanged += StaminaChanged;
            dodge.DodgeStarted += DodgeStarted;
            parry.Successful += Parried;
            practiceGuard.AttackStarted += GuardAttacking;
            match.MatchEnded += MatchEnded;
            camp.ExperienceAwarded += CampExperience;
            if (!Progression.TryBeginSession("offline-training", out var reason)) Debug.LogError(reason, this);
#if UNITY_EDITOR || DEBUG
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-hodrTutorialCheck") >= 0)
            {
                canSave = false;
                gameObject.AddComponent<TutorialAcceptanceProbe>().Bind(this);
            }
#endif
        }

        private void OnDestroy()
        {
            if (OpenPanel == Panel.Pause) Time.timeScale = pauseScale;
            if (Stats == null) return;
            threat.Receiver.ImpactResolved -= ObserveImpact;
            trainingTarget.HealthChanged -= TargetHealthChanged;
            Stats.StaminaChanged -= StaminaChanged;
            dodge.DodgeStarted -= DodgeStarted;
            parry.Successful -= Parried;
            practiceGuard.AttackStarted -= GuardAttacking;
            match.MatchEnded -= MatchEnded;
            camp.ExperienceAwarded -= CampExperience;
        }

        private void Update()
        {
            if (Stats == null || Step == Lesson.Finished) return;
            if (Time.unscaledTime > feedbackUntil) Feedback = "";
            ReadPanelInput();
            if (OpenPanel != Panel.None) return;
            if (resumeInputFrame != 0 && Time.frameCount >= resumeInputFrame)
            {
                resumeInputFrame = 0;
                player.enabled = true;
            }
            if (Stats.CurrentHealth <= 28) Stats.Heal(Stats.MaxHealth);
            switch (Step)
            {
                case Lesson.Move:
                    if (PlanarDistance(player.transform.position, movementMark.position) < .8f) Advance();
                    break;
                case Lesson.Look:
                    if (Quaternion.Angle(lookStart, playerCamera.transform.rotation) > 23) Advance();
                    break;
                case Lesson.Lock:
                    if (trainingTarget.IsDead) trainingTarget.ResetToFull();
                    if (targeting.CurrentTarget == trainingTarget.GetComponentInChildren<HeroesOfDragonland.Targeting.Targetable>()) Advance();
                    break;
                case Lesson.Light:
                case Lesson.Heavy:
                    if (landed) Advance();
                    else if (trainingTarget.IsDead) trainingTarget.ResetToFull();
                    break;
                case Lesson.Block:
                case Lesson.Parry:
                    if (defended) { Advance(); break; }
                    practiceGuard.Tick(Time.deltaTime);
                    if (Time.time >= nextStrike && practiceGuard.TryStart(threat)) nextStrike = Time.time + 2.8f;
                    break;
                case Lesson.Stamina:
                    if (sawStaminaSpend && Stats.CurrentStamina >= Stats.MaxStamina * .9f) Advance();
                    break;
                case Lesson.Interact:
                    if (lever.PullComplete) Advance();
                    break;
                case Lesson.Chest:
                    if (chest.IsOpen && Bag.GetAmount(trainingWeapon.Id) > 0) Advance();
                    break;
                case Lesson.Camp:
                    if (camp.IsCleared && Progression.Level > 1) Advance();
                    break;
                case Lesson.Gate:
                    if (gate.IsOpen) Advance();
                    break;
            }
        }

        internal void SetPanel(Panel panel)
        {
            if (Step == Lesson.Finished || OpenPanel == panel) return;
            if (OpenPanel == Panel.Pause) Time.timeScale = pauseScale;
            if (panel == Panel.Pause) { pauseScale = Time.timeScale; Time.timeScale = 0; }
            OpenPanel = panel;
            if (panel == Panel.None) resumeInputFrame = Time.frameCount + 1;
            else { resumeInputFrame = 0; player.enabled = false; }
            if (panel != Panel.None) defense.SetBlockIntent(false, false);
            if (panel == Panel.Bag && Step == Lesson.Inventory) Advance();
        }

        private void ReadPanelInput()
        {
            var keyboard = Keyboard.current;
            if (!Application.isFocused || keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame) SetPanel(OpenPanel == Panel.None ? Panel.Pause : Panel.None);
            else if (keyboard.iKey.wasPressedThisFrame && (Step == Lesson.Inventory || Step == Lesson.Equip) && OpenPanel != Panel.Pause)
                SetPanel(OpenPanel == Panel.Bag ? Panel.None : Panel.Bag);
            else if (keyboard.kKey.wasPressedThisFrame && (Step == Lesson.Mana || Step == Lesson.Level) && OpenPanel != Panel.Pause)
                SetPanel(OpenPanel == Panel.Skills ? Panel.None : Panel.Skills);
        }

        internal void EquipTrainingWeapon(int slot)
        {
            if (Step != Lesson.Equip || OpenPanel != Panel.Bag || Bag.GetSlot(slot).ItemId != trainingWeapon.Id) return;
            if (Gear.TryEquip(slot, EquipmentSlot.MainHand, out var reason))
            {
                SetPanel(Panel.None);
                Advance();
            }
            else Show(reason);
        }

        internal void LearnPracticeSkill()
        {
            if (OpenPanel != Panel.Skills || Step != Lesson.Mana) return;
            if (!skills.TryLearn(3, skills.SessionId, out var reason)) Show(reason);
        }

        internal void CastPracticeSkill()
        {
            if (OpenPanel != Panel.Skills || Step != Lesson.Mana) return;
            if (!skills.TryActivate(3, skills.SessionId, out var reason)) { Show(reason); return; }
            SetPanel(Panel.None);
            Advance("Mana spent · It regenerates over time.");
        }

        internal void AllocatePoint()
        {
            if (OpenPanel != Panel.Skills || Step != Lesson.Level) return;
            if (!Progression.TryIncreaseAttribute(ItemStat.Vitality, Progression.SessionId, out var reason)) { Show(reason); return; }
            SetPanel(Panel.None);
            Advance();
        }

        internal void Skip()
        {
            if (canSave) TutorialProgress.Skip();
            TutorialProgress.ReturnToMenu();
        }

        private void Advance(string message = "Objective complete")
        {
            Show(message);
            Step++;
            switch (Step)
            {
                case Lesson.Look:
                    movementMark.gameObject.SetActive(false);
                    lookStart = playerCamera.transform.rotation;
                    break;
                case Lesson.Lock:
                    trainingTarget.gameObject.SetActive(true);
                    break;
                case Lesson.Light:
                case Lesson.Heavy:
                    trainingTarget.ResetToFull();
                    targetHealth = trainingTarget.CurrentHealth;
                    landed = false;
                    break;
                case Lesson.Dodge:
                    trainingTarget.gameObject.SetActive(false);
                    break;
                case Lesson.Block:
                    practiceGuard.gameObject.SetActive(true);
                    break;
                case Lesson.Parry:
                    practiceGuard.Cancel();
                    defended = false;
                    nextStrike = Time.time + 1.2f;
                    break;
                case Lesson.Stamina:
                    practiceGuard.gameObject.SetActive(false);
                    sawStaminaSpend = Stats.CurrentStamina < Stats.MaxStamina * .9f;
                    break;
                case Lesson.Interact: lever.gameObject.SetActive(true); break;
                case Lesson.Chest: lever.gameObject.SetActive(false); chest.gameObject.SetActive(true); break;
                case Lesson.Camp: camp.gameObject.SetActive(true); break;
                case Lesson.Gate:
                    gate.GetComponent<DamageReceiver>().enabled = true;
                    gate.GetComponent<HeroesOfDragonland.Targeting.Targetable>().enabled = true;
                    break;
                case Lesson.King:
                    enemyKing.gameObject.SetActive(true);
                    if (!match.TryStartMatch(out var reason)) Debug.LogError(reason, this);
                    break;
            }
        }

        private void Show(string message) { Feedback = message; feedbackUntil = Time.unscaledTime + 2.6f; }
        private void ObserveImpact(DamageImpactCue cue, GameObject source)
        {
            if (Step == Lesson.Block && cue == DamageImpactCue.Blocked && source == practiceGuard.gameObject) defended = true;
        }
        private void TargetHealthChanged(float health, float maximum)
        {
            if ((Step == Lesson.Light || Step == Lesson.Heavy) && health < targetHealth && melee.IsHeavy == (Step == Lesson.Heavy))
                landed = true;
            targetHealth = health;
        }
        private void StaminaChanged(float current, float maximum) { if (Step == Lesson.Stamina && current < maximum * .9f) sawStaminaSpend = true; }
        private void DodgeStarted() { if (Step == Lesson.Dodge) Advance(); }
        private void Parried() { if (Step == Lesson.Parry) defended = true; }
        private void GuardAttacking() => guardStarted = Time.time;
        private void CampExperience(CampExperienceReward reward) =>
            Progression.TryAddExperience(reward.Amount, Progression.SessionId, "training-camp", out _);
        private void MatchEnded(TeamId winner)
        {
            if (Step != Lesson.King || winner != TeamId.TeamA) return;
            Step = Lesson.Finished;
            player.enabled = false;
            if (canSave) TutorialProgress.Complete();
        }

        internal string DefenseCue
        {
            get
            {
                if (Step != Lesson.Block && Step != Lesson.Parry) return "";
                if (!practiceGuard.IsBusy) return PlanarDistance(player.transform.position, practiceGuard.transform.position) > guardDefinition.AttackRange
                    ? "Step closer and face the guard." : "Get ready.";
                float remaining = practiceGuard.Windup - (Time.time - guardStarted);
                if (Step == Lesson.Parry && remaining < .22f) return "PARRY NOW  [C]";
                return "Incoming strike";
            }
        }
        internal float DefenseProgress => practiceGuard.IsBusy
            ? Mathf.Clamp01((Time.time - guardStarted) / practiceGuard.Windup) : 0;

        internal Transform Marker => Step switch
        {
            Lesson.Move => movementMark,
            Lesson.Lock or Lesson.Light or Lesson.Heavy => trainingTarget.transform,
            Lesson.Block or Lesson.Parry => practiceGuard.transform,
            Lesson.Interact => lever.transform,
            Lesson.Chest => chest.transform,
            Lesson.Camp => camp.transform,
            Lesson.Gate => gate.transform,
            Lesson.King => enemyKing.transform,
            _ => null
        };
        internal Camp PracticeCamp => camp;

        private static float PlanarDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }
}

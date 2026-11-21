using HeroesOfDragonland.UI;
using UnityEngine;

namespace HeroesOfDragonland.World
{
    public sealed class TutorialView : MonoBehaviour
    {
        private static readonly (string Title, string Prompt)[] Prompts =
        {
            ("Move to the marker", "WASD · Follow the gold marker."),
            ("Look around", "Move the mouse to rotate the camera."),
            ("Lock onto the dummy", "Aim at the dummy and press Middle Mouse."),
            ("Land a light attack", "Move into reach and click Left Mouse."),
            ("Land a heavy attack", "Press R. Heavy attacks hit harder and use more stamina."),
            ("Dodge", "Press Shift. Hold a movement key to choose a direction."),
            ("Block a strike", "Face the guard and hold Right Mouse."),
            ("Parry a strike", "Face the guard and press C just before the strike lands."),
            ("Recover stamina", "Dodge if the bar is full, then pause to recover stamina."),
            ("Spend mana", "Press K, learn Fire Projectile, then cast it."),
            ("Interact with the lever", "Approach the lever and press F."),
            ("Open a chest", "Approach the unlocked chest and press F to claim its loot."),
            ("Inspect your inventory", "Press I to open the bag and inspect the sword."),
            ("Equip your sword", "Choose Equip sword in the bag."),
            ("Clear the neutral camp", "Defeat the camp guard. Camps award XP and leave loot."),
            ("Spend a level-up point", "Press K and spend one point on Vitality."),
            ("Breach the fortress", "Attack the enemy gate to open a path to its King."),
            ("Defeat the enemy King", "Get through the breach and defeat the King to win.")
        };

        private TutorialFlow flow;
        private GUIStyle title, text, centered;
        private void Awake() => flow = GetComponent<TutorialFlow>();

        private void OnGUI()
        {
            if (Application.isBatchMode || flow.Stats == null) return;
            title ??= new GUIStyle(GUI.skin.label) { fontSize = 21, fontStyle = FontStyle.Bold, wordWrap = true };
            text ??= new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            centered ??= new GUIStyle(text) { alignment = TextAnchor.MiddleCenter };
            float width = Mathf.Min(510, Screen.width - 28);
            if (flow.Step == TutorialFlow.Lesson.Finished)
            {
                GUILayout.BeginArea(new Rect((Screen.width - width) * .5f, Screen.height * .3f, width, 155), GUI.skin.box);
                GUILayout.Label("Training complete", title);
                GUILayout.Label("The enemy King fell. Your team wins.", text);
                if (GUILayout.Button("Return to main menu", GUILayout.Height(36))) TutorialProgress.ReturnToMenu();
                GUILayout.EndArea();
                return;
            }

            GUILayout.BeginArea(new Rect(14, 14, width, 134), GUI.skin.box);
            var lesson = Prompts[(int)flow.Step];
            GUILayout.Label(lesson.Title, title);
            GUILayout.Label(lesson.Prompt, text);
            GUILayout.Label("Esc · Pause or skip training");
            GUILayout.EndArea();

            DrawResources();
            if (flow.OpenPanel == TutorialFlow.Panel.None)
            {
                DrawMarker();
                var cue = flow.DefenseCue;
                if (cue.Length > 0)
                {
                    var box = new Rect((Screen.width - width) * .5f, Screen.width < 830 ? 273 : 155, width, 35);
                    GUI.Box(box, "");
                    var tint = GUI.color;
                    GUI.color = new Color(1, .65f, .18f);
                    GUI.DrawTexture(new Rect(box.x + 4, box.yMax - 6, (box.width - 8) * flow.DefenseProgress, 3), Texture2D.whiteTexture);
                    GUI.color = tint;
                    GUI.Label(box, cue, centered);
                }
            }
            else DrawPanel(width);

            if (flow.Feedback.Length > 0)
                GUI.Box(new Rect((Screen.width - width) * .5f, Screen.height - 78, width, 36), flow.Feedback, centered);
        }

        private void DrawResources()
        {
            float width = Mathf.Min(280, Screen.width - 28);
            float y = Screen.width < 830 ? 154 : 14;
            var stats = flow.Stats;
            GUILayout.BeginArea(new Rect(Screen.width - width - 14, y, width, 114), GUI.skin.box);
            DrawBar("Health", stats.CurrentHealth, stats.MaxHealth, new Color(.75f, .26f, .27f));
            DrawBar("Stamina", stats.CurrentStamina, stats.MaxStamina, new Color(.24f, .68f, .41f));
            DrawBar("Mana", stats.CurrentMana, stats.MaxMana, new Color(.28f, .54f, .92f));
            GUILayout.Label("Level " + flow.Progression.Level + "  ·  XP " + flow.Progression.CurrentXP + " / " + flow.Progression.XPRequiredForNextLevel);
            GUILayout.EndArea();
        }

        private void DrawBar(string name, float value, float max, Color fill)
        {
            Rect bar = GUILayoutUtility.GetRect(20, 21, GUILayout.ExpandWidth(true));
            var tint = GUI.color;
            GUI.color = new Color(.14f, .17f, .19f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = fill;
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(value / Mathf.Max(1, max)), bar.height), Texture2D.whiteTexture);
            GUI.color = tint;
            GUI.Label(bar, name + "  " + Mathf.CeilToInt(value) + " / " + Mathf.CeilToInt(max), centered);
        }

        private void DrawMarker()
        {
            var marker = flow.Marker;
            if (marker == null || flow.ViewCamera == null) return;
            var screen = flow.ViewCamera.WorldToScreenPoint(marker.position + Vector3.up * .55f);
            float center = Screen.width * .5f;
            if (screen.z <= 0) screen.x = screen.x > center ? 20 : Screen.width - 20;
            float x = Mathf.Clamp(screen.x, 20, Screen.width - 20);
            float y = Mathf.Clamp(Screen.height - screen.y, 156, Screen.height - 104);
            var tint = GUI.color;
            GUI.color = new Color(1, .79f, .32f);
            GUI.Label(new Rect(x - 20, y - 20, 40, 40), "◆", centered);
            GUI.color = tint;
        }

        private void DrawPanel(float width)
        {
            GUILayout.BeginArea(new Rect((Screen.width - width) * .5f, Screen.height * .32f, width, 225), GUI.skin.box);
            switch (flow.OpenPanel)
            {
                case TutorialFlow.Panel.Pause:
                    GUILayout.Label("Training paused", title);
                    if (GUILayout.Button("Resume")) flow.SetPanel(TutorialFlow.Panel.None);
                    if (GUILayout.Button("Skip to main menu")) flow.Skip();
                    break;
                case TutorialFlow.Panel.Bag:
                    GUILayout.Label("Inventory", title);
                    for (int i = 0; i < flow.Bag.SlotCount; i++)
                    {
                        var slot = flow.Bag.GetSlot(i);
                        if (!flow.Bag.TryResolveItem(slot.ItemId, out var item)) continue;
                        GUILayout.Label(item.DisplayName + " × " + slot.Amount, text);
                        if (item == flow.TrainingWeapon && flow.Step == TutorialFlow.Lesson.Equip
                            && GUILayout.Button("Equip sword · Main hand")) flow.EquipTrainingWeapon(i);
                    }
                    GUILayout.Label("Main hand · " + (flow.Gear.EquippedWeapon != null ? flow.TrainingWeapon.DisplayName : "Empty"));
                    if (GUILayout.Button("Close [I]")) flow.SetPanel(TutorialFlow.Panel.None);
                    break;
                case TutorialFlow.Panel.Skills:
                    GUILayout.Label("Skills & progression", title);
                    if (flow.Step == TutorialFlow.Lesson.Mana)
                    {
                        string name = flow.PracticeSkill.DisplayName;
                        GUILayout.Label(name + " · " + flow.PracticeSkill.Ability.ManaCost + " mana", text);
                        if (!flow.PracticeSkillLearned)
                        {
                            if (GUILayout.Button("Learn " + name)) flow.LearnPracticeSkill();
                        }
                        else if (GUILayout.Button("Cast " + name)) flow.CastPracticeSkill();
                    }
                    else
                    {
                        GUILayout.Label("Level " + flow.Progression.Level + " · " + flow.Progression.ProgressionPoints + " attribute points", text);
                        if (GUILayout.Button("+1 Vitality")) flow.AllocatePoint();
                    }
                    if (GUILayout.Button("Close [K]")) flow.SetPanel(TutorialFlow.Panel.None);
                    break;
            }
            GUILayout.EndArea();
        }
    }
}

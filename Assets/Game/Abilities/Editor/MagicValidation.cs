using System;
using System.Collections.Generic;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.EquipmentSystem;
using HeroesOfDragonland.Progression;
using HeroesOfDragonland.Skills;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Teams;
using UnityEditor;
using UnityEngine;

namespace HeroesOfDragonland.Abilities.Editor
{
    public static class MagicValidation
    {
        private static int checks;
        private static void Check(bool condition, string label)
        { if (!condition) throw new InvalidOperationException("[MagicValidation] FAIL " + label); checks++; }
        private static void Near(float actual, float expected, string label) => Check(Mathf.Abs(actual - expected) < .01f, label + " actual=" + actual + " expected=" + expected);
        public static void ValidateAssets()
        {
            int players = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Game/Data" }))
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                var skills = root.GetComponent<SessionSkills>(); if (skills == null) continue; players++;
                Check(root.GetComponent<AbilityCaster>() != null && root.GetComponent<NetworkAbilities>() != null
                    && root.GetComponent<AbilityPresentation>() != null, root.name + " components");
                for (int i = 0; i < SessionSkills.SkillCount; i++) Check(skills.GetDefinition(i)?.IsValid == true, root.name + " valid slot " + i);
            }
            Check(players >= 2, "battlefield and fortress spawn prefabs");
        }
        [MenuItem("Heroes of Dragonland/Magic/Validate In Play Mode")]
        public static void Run()
        {
            Check(Application.isPlaying, "Play Mode required"); checks = 0; ValidateAssets();
            var temporary = new List<GameObject>();
            try
            {
                var actor = Actor("M45 caster", new Vector3(1000, 100, 1000), TeamId.TeamA); temporary.Add(actor);
                var ally = Actor("M45 ally", actor.transform.position + Vector3.forward * 3, TeamId.TeamA); temporary.Add(ally);
                var enemy = Actor("M45 enemy", actor.transform.position + Vector3.forward * 6, TeamId.TeamB); temporary.Add(enemy);
                var skills = actor.GetComponent<SessionSkills>(); var caster = actor.GetComponent<AbilityCaster>(); var stats = actor.GetComponent<CharacterStats>();
                var progression = actor.GetComponent<SessionProgression>(); var hp = enemy.GetComponent<CharacterStats>();
                Check(progression.TryBeginSession("m45-test", out _), "start skill session");
                Check(!skills.TryActivate(3, skills.SessionId, out _), "unlearned magic rejected");
                Check(skills.TryLearn(3, skills.SessionId, out _), "fire learned with session point");
                Check(progression.TryAddExperience(100000, progression.SessionId, "m45-xp", out _), "normal XP grants skill points");
                Check(skills.TryLearn(4, skills.SessionId, out _) && skills.TryLearn(5, skills.SessionId, out _), "healing and shield learned");
                var fire = skills.GetDefinition(3).Ability; var heal = skills.GetDefinition(4).Ability; var shield = skills.GetDefinition(5).Ability;
                int starts = 0, releases = 0, interrupts = 0, impacts = 0;
                caster.Presentation += (d, cue, p, dir) => { if (cue == AbilityCue.CastStarted) starts++; if (cue == AbilityCue.Released) releases++; if (cue == AbilityCue.Interrupted) interrupts++; if (cue == AbilityCue.Impact) impacts++; };
                Physics.SyncTransforms(); float mana = stats.CurrentMana;
                Check(!skills.TryActivate(3, "stale-session", out _), "stale session rejected");
                Check(skills.TryActivate(3, skills.SessionId, out _), "fire accepted");
                Near(stats.CurrentMana, mana - fire.ManaCost, "mana charged exactly once");
                Check(!skills.TryActivate(3, skills.SessionId, out _) && !skills.TryActivate(5, skills.SessionId, out _), "duplicate and concurrent casts rejected");
                Near(hp.CurrentHealth, hp.MaxHealth, "no effect before cast completes");
                caster.Tick(fire.CastTime / 2); Check(caster.IsCasting && releases == 0, "cast time honored");
                caster.Tick(fire.CastTime); Check(!caster.IsCasting && releases == 1, "release hook");
                caster.Tick(1); Near(hp.CurrentHealth, hp.MaxHealth - fire.Magnitude, "swept projectile damages enemy");
                Near(ally.GetComponent<CharacterStats>().CurrentHealth, ally.GetComponent<CharacterStats>().MaxHealth, "projectile ignores ally");
                caster.Tick(2); Near(hp.CurrentHealth, hp.MaxHealth - fire.Magnitude, "projectile hits only once");
                Check(!skills.TryActivate(3, skills.SessionId, out _), "cooldown remains active");
                Action reset = () => { caster.Interrupt(); caster.Tick(100); skills.Tick(100); stats.ResetToFull(); hp.ResetToFull(); Physics.SyncTransforms(); };
                reset(); Check(skills.TryActivate(3, skills.SessionId, out _), "movement interruption fixture");
                actor.transform.position += Vector3.right; caster.Tick(fire.CastTime + .1f);
                Check(!caster.IsCasting && interrupts == 1, "movement interrupts without release");
                Near(stats.CurrentMana, stats.MaxMana - fire.ManaCost, "interruption consumes mana");
                Check(skills.CooldownRemaining(3) > 0, "interruption consumes cooldown"); actor.transform.position -= Vector3.right;
                reset(); Check(skills.TryActivate(3, skills.SessionId, out _), "damage interruption fixture"); stats.ApplyDamage(1);
                Check(!caster.IsCasting && interrupts == 2, "damage immediately interrupts");
                reset(); Check(skills.TryActivate(5, skills.SessionId, out _), "shield accepted"); caster.Tick(shield.CastTime);
                Near(caster.ShieldCapacity, shield.Magnitude, "shield capacity"); Near(stats.ApplyDamage(10), 0, "shield absorbs damage");
                Near(caster.ShieldCapacity, shield.Magnitude - 10, "shield capacity depleted");
                Near(stats.ApplyDamage(50), 50 - (shield.Magnitude - 10), "overflow damages health"); Check(caster.ShieldRemaining == 0, "depleted shield ends");
                reset(); Check(skills.TryActivate(5, skills.SessionId, out _), "shield expiry fixture"); caster.Tick(shield.CastTime); caster.Tick(shield.Duration + .1f);
                Near(stats.ApplyDamage(5), 5, "expired shield no protection");
                reset(); var allyStats = ally.GetComponent<CharacterStats>(); allyStats.ApplyDamage(50);
                Near(stats.ApplyDamage(40), 40, "self injury fixture");
                Check(skills.TryActivate(4, skills.SessionId, out _), "ally healing accepted"); caster.Tick(heal.CastTime);
                Near(allyStats.CurrentHealth, allyStats.MaxHealth - 50 + heal.Magnitude, "short range ally healed");
                Near(stats.CurrentHealth, stats.MaxHealth - 40, "ally targeting does not heal caster");
                reset(); allyStats.ResetToFull(); stats.ApplyDamage(50); Check(skills.TryActivate(4, skills.SessionId, out _), "self healing fallback"); caster.Tick(heal.CastTime);
                Near(stats.CurrentHealth, stats.MaxHealth - 50 + heal.Magnitude, "self heal");
                Check(!caster.CanStart(heal, hp, out _), "cannot heal enemy");
                ally.transform.position += Vector3.forward * 10; Check(!caster.CanStart(heal, allyStats, out _), "out of range rejected"); ally.transform.position -= Vector3.forward * 10;
                reset(); allyStats.ApplyDamage(50); Check(skills.TryActivate(4, skills.SessionId, out _), "target loss fixture"); ally.transform.position += Vector3.forward * 10; caster.Tick(heal.CastTime);
                Near(allyStats.CurrentHealth, allyStats.MaxHealth - 50, "target leaving range cancels effect"); ally.transform.position -= Vector3.forward * 10;
                reset(); var wall = new GameObject("M45 wall"); temporary.Add(wall); wall.transform.position = actor.transform.position + Vector3.forward * 2 + Vector3.up;
                var box = wall.AddComponent<BoxCollider>(); box.size = new Vector3(3, 3, .4f); Physics.SyncTransforms();
                Check(!caster.CanStart(heal, allyStats, out _), "healing blocked by geometry");
                Check(skills.TryActivate(3, skills.SessionId, out _), "obstruction fixture"); caster.Tick(fire.CastTime); caster.Tick(1);
                Near(hp.CurrentHealth, hp.MaxHealth, "wall stops projectile"); wall.SetActive(false);
                reset(); stats.TrySpendMana(stats.CurrentMana); Check(!skills.TryActivate(3, skills.SessionId, out _), "insufficient mana rejected");
                reset(); Check(skills.TryActivate(3, skills.SessionId, out _), "death interruption fixture"); stats.ApplyDamage(10000); caster.Tick(1);
                Check(!caster.IsCasting && caster.ShieldRemaining == 0, "death clears magic state"); Check(!skills.TryActivate(5, skills.SessionId, out _), "dead casting rejected");
                stats.ResetToFull(); skills.Tick(100); Check(skills.TryActivate(5, skills.SessionId, out _), "session reset fixture"); caster.Tick(shield.CastTime);
                Check(progression.TryBeginSession("m45-next", out _), "next session");
                Check(skills.LearnedMask == 0 && skills.SkillPoints == 1 && caster.ShieldRemaining == 0 && !caster.IsCasting && skills.CooldownRemaining(5) == 0, "new session resets unlocks cooldown and effects");
                Check(starts > 5 && releases > 3 && interrupts > 2 && impacts > 0, "presentation hooks exercised");
                Debug.Log("[MagicValidation] PASS " + checks + " asset and gameplay assertions.");
            }
            finally { foreach (var go in temporary) if (go != null) UnityEngine.Object.DestroyImmediate(go); }
        }
        private static GameObject Actor(string name, Vector3 position, TeamId team)
        {
            var go = new GameObject(name); go.transform.position = position;
            go.AddComponent<CharacterStats>(); var progression = go.AddComponent<SessionProgression>();
            var data = new SerializedObject(progression); data.FindProperty("definition").objectReferenceValue = AssetDatabase.LoadAssetAtPath<LevelProgressionDefinition>("Assets/Game/Data/Progression/DefaultLevelProgression.asset"); data.ApplyModifiedPropertiesWithoutUndo();
            go.AddComponent<CharacterAttributes>(); var skills = go.AddComponent<SessionSkills>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Data/Battlefield/BattlefieldPlayer.prefab").GetComponent<SessionSkills>();
            data = new SerializedObject(skills); var definitions = data.FindProperty("definitions"); definitions.arraySize = SessionSkills.SkillCount;
            for (int i = 0; i < SessionSkills.SkillCount; i++) definitions.GetArrayElementAtIndex(i).objectReferenceValue = prefab.GetDefinition(i);
            data.ApplyModifiedPropertiesWithoutUndo(); go.AddComponent<AbilityCaster>();
            go.AddComponent<TeamIdentity>().TryConfigure(team, CombatantKind.Player);
            go.AddComponent<DamageReceiver>(); var capsule = go.AddComponent<CapsuleCollider>(); capsule.center = Vector3.up; capsule.height = 2; capsule.radius = .4f;
            return go;
        }
    }
}

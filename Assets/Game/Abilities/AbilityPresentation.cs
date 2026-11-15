using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HeroesOfDragonland.Abilities
{
    // Optional asset/animation hooks. This component never applies gameplay effects.
    [DisallowMultipleComponent, RequireComponent(typeof(AbilityCaster))]
    public sealed class AbilityPresentation : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        private AbilityCaster caster;
        private GameObject castingVfx;
        private readonly List<GameObject> visuals = new();
        private readonly List<GameObject> fireVisuals = new();
        private GameObject shieldVisual;
        private Material lineMaterial;
        private void Awake() { caster = GetComponent<AbilityCaster>(); if (animator == null) animator = GetComponentInChildren<Animator>(); }
        private void OnEnable() => caster.Presentation += OnCue;
        private void OnDisable()
        {
            caster.Presentation -= OnCue; StopCasting();
            foreach (var visual in visuals) if (visual != null) Destroy(visual);
            visuals.Clear();
            StopAllCoroutines();
            foreach (var fire in fireVisuals) if (fire != null) Destroy(fire);
            fireVisuals.Clear();
            if (shieldVisual != null) Destroy(shieldVisual);
            if (lineMaterial != null) Destroy(lineMaterial);
        }
        private void Update()
        {
            if (Application.isBatchMode) return;
            if (caster.ShieldRemaining > 0 && shieldVisual == null)
            {
                shieldVisual = Line("Magic shield", Color.cyan, .045f, 49);
                shieldVisual.transform.SetParent(transform, false);
                var line = shieldVisual.GetComponent<LineRenderer>(); line.useWorldSpace = false;
                for (int i = 0; i < 49; i++) { float angle = i * Mathf.PI * 2 / 48; line.SetPosition(i, new Vector3(Mathf.Cos(angle) * .8f, 1, Mathf.Sin(angle) * .8f)); }
            }
            if (caster.ShieldRemaining <= 0 && shieldVisual != null) Destroy(shieldVisual);
        }
        private GameObject Line(string title, Color color, float width, int count)
        {
            if (lineMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
                if (shader != null) lineMaterial = new Material(shader);
            }
            var go = new GameObject(title); var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = lineMaterial; line.startColor = line.endColor = color;
            line.startWidth = line.endWidth = width; line.positionCount = count; return go;
        }
        private IEnumerator FireVisual(AbilityDefinition definition, Vector3 position, Vector3 direction)
        {
            var go = Line("Fire projectile", new Color(1, .25f, .02f), definition.ProjectileRadius * 2, 2);
            fireVisuals.Add(go); float traveled = 0;
            while (go != null && traveled < definition.Range)
            {
                var line = go.GetComponent<LineRenderer>(); Vector3 head = position + direction * traveled;
                line.SetPosition(0, head); line.SetPosition(1, head - direction * .45f);
                traveled += definition.ProjectileSpeed * Time.deltaTime; yield return null;
            }
            if (go != null) Destroy(go); fireVisuals.Remove(go);
        }
        private void StopCasting() { if (castingVfx != null) Destroy(castingVfx); castingVfx = null; }
        private GameObject Spawn(GameObject prefab, Vector3 position, Vector3 direction, float life)
        {
            if (prefab == null || Application.isBatchMode) return null;
            var instance = Instantiate(prefab, position, Quaternion.LookRotation(direction.sqrMagnitude > .001f ? direction : Vector3.forward));
            visuals.RemoveAll(v => v == null); visuals.Add(instance); Destroy(instance, life); return instance;
        }
        private void OnCue(AbilityDefinition definition, AbilityCue cue, Vector3 position, Vector3 direction)
        {
            if (cue == AbilityCue.CastStarted)
            {
                StopCasting();
                if (animator != null && !string.IsNullOrEmpty(definition.AnimationTrigger))
                    foreach (var parameter in animator.parameters)
                        if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == definition.AnimationTrigger)
                        { animator.SetTrigger(parameter.nameHash); break; }
                castingVfx = Spawn(definition.CastVfx, position + Vector3.up, direction, definition.CastTime + 1);
                if (castingVfx != null) castingVfx.transform.SetParent(transform, true);
            }
            else if (cue == AbilityCue.Released)
            {
                StopCasting(); Spawn(definition.ReleaseVfx, position, direction, 2);
                if (definition.Effect == AbilityEffect.FireProjectile && !Application.isBatchMode) StartCoroutine(FireVisual(definition, position, direction));
            }
            else if (cue == AbilityCue.Interrupted) StopCasting();
            else if (cue == AbilityCue.Impact)
            {
                if (definition.Effect == AbilityEffect.FireProjectile)
                { foreach (var fire in fireVisuals) if (fire != null) Destroy(fire); fireVisuals.Clear(); }
                Spawn(definition.ImpactVfx, position, direction, 2);
            }
        }
    }
}

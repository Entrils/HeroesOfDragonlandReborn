using HeroesOfDragonland.AI;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    [DisallowMultipleComponent, RequireComponent(typeof(NetworkEnemy))]
    public sealed class BossTelegraphPresentation : MonoBehaviour
    {
        private NetworkEnemy enemy;
        private NetworkCharacterStats stats;
        private LineRenderer outline;
        private Material material;
        private Renderer body;
        private MaterialPropertyBlock tint;
        private const int Segments = 64;
        private void Awake()
        {
            body = transform.Find("Body")?.GetComponent<Renderer>(); tint = new MaterialPropertyBlock();
            enemy = GetComponent<NetworkEnemy>(); stats = GetComponent<NetworkCharacterStats>();
            var visual = new GameObject("Boss attack warning");
            visual.transform.SetParent(transform, false);
            outline = visual.AddComponent<LineRenderer>();
            material = new Material(Shader.Find("Sprites/Default"));
            outline.sharedMaterial = material;
            outline.useWorldSpace = true; outline.loop = true;
            outline.enabled = false;
        }
        private void LateUpdate()
        {
            outline.enabled = false;
            var set = enemy.PresentedAttackSet;
            if (enemy.Object == null || !enemy.Object.IsValid || set == null || set.Count == 0 || stats.Dead
                || !NetworkMatch.AllowsGameplay(enemy.Runner)) return;
            if (body != null && enemy.Definition.Boss != null)
            {
                body.GetPropertyBlock(tint);
                tint.SetColor("_BaseColor", enemy.BossPhase == BossPhase.PhaseTwo ? new Color(.6f, .12f, .08f) : enemy.BossPhase == BossPhase.Transition ? new Color(1, .55f, .08f) : new Color(.3f, .36f, .4f));
                body.SetPropertyBlock(tint);
            }
            if (enemy.BossPhase == BossPhase.Transition && enemy.Definition.Boss != null)
            {
                outline.enabled = true; outline.positionCount = Segments + 1;
                outline.startWidth = outline.endWidth = .16f;
                outline.startColor = outline.endColor = new Color(1, .7f, .1f);
                float progress = 1 - enemy.BossTransitionRemaining / enemy.Definition.Boss.TransitionDuration;
                for (int i = 0; i <= Segments; i++) outline.SetPosition(i, transform.position + Vector3.up * .12f
                    + Quaternion.AngleAxis(i * 360f / Segments, Vector3.up) * Vector3.forward * Mathf.Lerp(1.5f, 5, progress));
                return;
            }
            if (enemy.AttackSequence == 0) return;
            double time = enemy.HasStateAuthority ? enemy.Runner.LocalRenderTime : enemy.Runner.RemoteRenderTime;
            float age = (float)(time - enemy.AttackStartTick * (double)enemy.Runner.DeltaTime);
            var attack = set.Get(enemy.AttackIndex);
            if (age < 0 || age >= attack.Windup || (enemy.AttackEndTick != 0 && time >= enemy.AttackEndTick * (double)enemy.Runner.DeltaTime)) return;
            outline.enabled = true;
            outline.startWidth = outline.endWidth = Mathf.Lerp(.09f, .24f, age / attack.Windup);
            outline.startColor = outline.endColor = Color.Lerp(new Color(1, .65f, .05f), Color.red, age / attack.Windup);
            bool circle = attack.Angle >= 359;
            outline.positionCount = Segments + 1 + (circle ? 0 : 1);
            Vector3 center = enemy.AttackOrigin + Vector3.up * .12f;
            for (int i = 0; i <= Segments; i++)
            {
                float angle = -attack.Angle * .5f + attack.Angle * i / Segments;
                outline.SetPosition(i, center + Quaternion.AngleAxis(angle, Vector3.up) * enemy.AttackForward * attack.Radius);
            }
            if (!circle) outline.SetPosition(Segments + 1, center);
        }
        private void OnDisable() { if (outline != null) outline.enabled = false; }
        private void OnDestroy() { if (material != null) Destroy(material); }
    }
}

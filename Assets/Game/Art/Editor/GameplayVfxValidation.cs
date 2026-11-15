using System;
using System.Collections;
using HeroesOfDragonland.Presentation.Vfx;
using UnityEditor;
using UnityEngine;

namespace HeroesOfDragonland.Art.Editor
{
    public static class GameplayVfxValidation
    {
        public static string Status { get; private set; } = "Not run";
        private static void Check(bool value, string reason) { if (!value) throw new InvalidOperationException("M59: " + reason); }
        [MenuItem("Heroes of Dragonland/Art Pipeline/Run Gameplay VFX Play Validation (M59)")]
        public static void Start()
        {
            Check(Application.isPlaying, "Use Play Mode through MCP");
            Check(Status != "RUNNING", "Validation already running");
            GameplayVfxSetup.Validate();
            Status = "RUNNING";
            var host = new GameObject("Temporary M59 VFX check").AddComponent<ArtPipelineValidationHost>();
            host.StartCoroutine(Run(host.gameObject));
        }
        private static IEnumerator Run(GameObject host)
        {
            GameObject root = null, follow = null;
            try
            {
                root = new GameObject("Temporary isolated VFX pool"); root.SetActive(false);
                var pool = root.AddComponent<VfxPool>();
                var data = new SerializedObject(pool);
                data.FindProperty("catalog").objectReferenceValue = AssetDatabase.LoadAssetAtPath<VfxCatalog>(GameplayVfxSetup.CatalogPath);
                data.FindProperty("capacity").intValue = 16; data.ApplyModifiedPropertiesWithoutUndo();
                data.FindProperty("renderInBatchMode").boolValue = true; data.ApplyModifiedPropertiesWithoutUndo();
                root.SetActive(true);
                var point = new Vector3(0, 1, 100);
                var first = pool.Play(VfxKind.MeleeHit, point);
                Check(first.IsAlive && pool.ActiveCount == 1, "Native effect lease starts");
                first.Stop();
                var replacement = pool.Play(VfxKind.MeleeHit, point);
                first.Stop();
                Check(!first.IsAlive && replacement.IsAlive && pool.ActiveCount == 1, "Stale handle cannot stop reused Unity instance");
                replacement.Stop();
                var shortEffect = pool.Play(VfxKind.BlockedHit, point, lifetime: .05f);
                yield return new WaitForSeconds(.2f);
                Check(!shortEffect.IsAlive && pool.ActiveCount == 0, "Lifetime returns instance");
                follow = new GameObject("Temporary VFX follow target"); follow.transform.position = point;
                var shield = pool.Play(VfxKind.MagicShield, point, VfxRelation.Friendly, follow: follow.transform, lifetime: 2);
                follow.SetActive(false);
                yield return null;
                Check(!shield.IsAlive && pool.ActiveCount == 0, "Hidden/destroyed follow target cannot leave a persistent shield");
                follow.SetActive(true);
                VfxLease oldest = default;
                foreach (var spec in new[] { (VfxKind.LevelUp, 6), (VfxKind.ItemPickup, 4), (VfxKind.ChestOpening, 4), (VfxKind.CampCleared, 2) })
                    for (int i = 0; i < spec.Item2; i++)
                    {
                        var lease = pool.Play(spec.Item1, point);
                        Check(lease.IsAlive, "Fill cosmetic budget");
                        if (!oldest.IsAlive) oldest = lease;
                    }
                Check(pool.ActiveCount == 16, "Global native instance cap");
                var parry = pool.Play(VfxKind.Parry, point, VfxRelation.Hostile);
                Check(parry.IsAlive && !oldest.IsAlive && pool.ActiveCount == 16, "Important feedback replaces cosmetic clutter");
                int dropped = pool.Dropped;
                Check(!pool.Play(VfxKind.Victory, point).IsAlive && pool.Dropped == dropped + 1, "Excess cosmetics are dropped");
                root.SetActive(false);
                Check(!parry.IsAlive && pool.ActiveCount == 0, "Pool disable invalidates all outstanding handles");
                root.SetActive(true);
                foreach (VfxKind kind in Enum.GetValues(typeof(VfxKind)))
                {
                    var lease = pool.Play(kind, point, VfxRelation.Neutral, lifetime: .05f);
                    Check(lease.IsAlive, "Reusable prefab for " + kind);
                    lease.Stop();
                }
                foreach (var particles in root.GetComponentsInChildren<ParticleSystem>(true))
                    Check(particles.particleCount == 0, "Returned particle buffers are cleared");
                Status = "PASS native pool lifetime/reuse/stale handles/follow cancellation/priority cap/disable and all 17 prefabs. Actual combat outcomes, network delivery and visual overlap remain separate.";
                Debug.Log("[M59 Validation] " + Status);
            }
            finally
            {
                if (root != null) { root.SetActive(false); UnityEngine.Object.Destroy(root); }
                if (follow != null) UnityEngine.Object.Destroy(follow);
                if (Status == "RUNNING") Status = "FAILED; see Console";
                UnityEngine.Object.Destroy(host);
            }
        }
    }
}

using UnityEngine;

namespace HeroesOfDragonland.Presentation
{
    // Only inert rigid visuals are accepted. Reject scripts before Instantiate can run Awake.
    public static class EquipmentVisualSafety
    {
        public static bool CanSpawn(GameObject prefab, out string reason)
        {
            reason = null;
            if (prefab == null || !prefab.activeSelf) reason = "Equipment visual prefab must have an active root.";
            else if (prefab.GetComponentsInChildren<MeshRenderer>(true).Length == 0) reason = "Equipment visual needs a rigid mesh renderer.";
            else foreach (var component in prefab.GetComponentsInChildren<Component>(true))
            {
                if (component is Transform || component is MeshFilter || component is MeshRenderer || component is LODGroup
                    || component is Collider || component is Rigidbody) continue;
                reason = "Equipment visual contains a missing, animated, skinned or non-presentation component: " + prefab.name;
                break;
            }
            return reason == null;
        }
        public static GameObject Spawn(GameObject prefab, Transform parent)
        {
            if (parent == null || !CanSpawn(prefab, out _)) return null;
            var stage = new GameObject("EquipmentVisual staging");
            stage.SetActive(false);
            try
            {
                var instance = Object.Instantiate(prefab, stage.transform, false);
                foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                foreach (var body in instance.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.detectCollisions = false; }
                instance.transform.SetParent(parent, false);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
                return instance;
            }
            finally { Release(stage); }
        }
        public static void Release(GameObject instance)
        {
            if (instance == null) return;
            instance.SetActive(false); // No same-frame visible duplicate while destruction is deferred.
            if (Application.isPlaying) Object.Destroy(instance); else Object.DestroyImmediate(instance);
        }
    }
}

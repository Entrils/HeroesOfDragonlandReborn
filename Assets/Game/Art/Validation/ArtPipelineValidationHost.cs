#if UNITY_EDITOR
using UnityEngine;

namespace HeroesOfDragonland.Art.Editor
{
    // Editor-only coroutine host; never saved to any scene or prefab.
    public sealed class ArtPipelineValidationHost : MonoBehaviour { }
}
#endif

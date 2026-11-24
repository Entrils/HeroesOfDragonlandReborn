using HeroesOfDragonland.Presentation;
using UnityEngine;

namespace HeroesOfDragonland.Cosmetics
{
    internal static class CosmeticPresentation
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        internal static void Tint(GameObject part, Color tint, MaterialPropertyBlock properties)
        {
            if (part == null) return;
            foreach (var renderer in part.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var material = materials[i];
                    if (material == null || !material.HasProperty(BaseColor)) continue;
                    renderer.GetPropertyBlock(properties, i);
                    properties.SetColor(BaseColor, material.GetColor(BaseColor) * tint);
                    renderer.SetPropertyBlock(properties, i);
                }
            }
        }

        internal static void Pose(CharacterVisual visual, VictoryGesture gesture)
        {
            if (gesture == VictoryGesture.None || visual == null || visual.Animator == null || !visual.Animator.isHuman) return;
            var upperArm = visual.Animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            var lowerArm = visual.Animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            var hand = visual.Animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (upperArm == null || lowerArm == null || hand == null) return;

            var root = visual.transform;
            if (gesture == VictoryGesture.Salute)
            {
                Point(upperArm, lowerArm, root.right + root.forward * .35f);
                Point(lowerArm, hand, -root.right + root.up + root.forward * .25f);
            }
            else if (gesture == VictoryGesture.RaiseFist)
            {
                Point(upperArm, lowerArm, root.up + root.right * .38f);
                Point(lowerArm, hand, root.up + root.forward * .25f);
            }
        }

        private static void Point(Transform from, Transform to, Vector3 direction)
        {
            Vector3 current = to.position - from.position;
            if (current.sqrMagnitude > .000001f)
                from.rotation = Quaternion.FromToRotation(current, direction) * from.rotation;
        }
    }
}

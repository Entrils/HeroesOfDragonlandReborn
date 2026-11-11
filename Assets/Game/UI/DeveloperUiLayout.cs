using UnityEngine;

namespace HeroesOfDragonland.UI
{
    // Share UI Toolkit's 1280x720 fit scaling with the remaining developer IMGUI panels.
    public static class DeveloperUiLayout
    {
        public static bool Visible
        {
            get
            {
                var camera = Camera.main != null ? Camera.main.GetComponent<HeroesOfDragonland.World.BattlefieldCamera>() : null;
                return camera == null || !camera.IsCaptured;
            }
        }
        public static float Scale => Mathf.Max(.01f, Mathf.Min(Screen.width / 1280f, Screen.height / 720f));
        public static float Width => Screen.width / Scale;
        public static float Height => Screen.height / Scale;
        public static Matrix4x4 Begin()
        {
            var previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1));
            return previous;
        }
    }
}

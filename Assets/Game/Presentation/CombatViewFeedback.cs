using HeroesOfDragonland.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace HeroesOfDragonland.Presentation
{
    [DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    internal sealed class CombatViewFeedback : MonoBehaviour
    {
        private Camera view;
        private ThirdPersonCamera follow;
        private Transform observedTarget;
        private bool HasView => view.isActiveAndEnabled && follow != null && follow.isActiveAndEnabled
            && observedTarget != null && follow.Target == observedTarget;
        private Gamepad pad;
        private float shake, rumble;
        private double until, nextPulse, indicatorUntil;
        private Vector3 incoming;
        private bool shifted;

        internal static void Pulse(ThirdPersonCamera camera, Vector3 direction, float shake, float rumble, bool received)
        {
            var feedback = camera.GetComponent<CombatViewFeedback>();
            if (feedback == null) feedback = camera.gameObject.AddComponent<CombatViewFeedback>();
            if (!feedback.isActiveAndEnabled || !Application.isFocused) return;
            var now = Time.unscaledTimeAsDouble;
            if (feedback.observedTarget != camera.Target)
            {
                feedback.until = feedback.nextPulse = feedback.indicatorUntil = 0;
                feedback.follow = camera;
                feedback.observedTarget = camera.Target;
            }
            if (now >= feedback.nextPulse)
            {
                feedback.until = now + .12;
                feedback.nextPulse = now + .105;
                feedback.shake = Mathf.Clamp(shake, 0, .025f);
                feedback.rumble = Mathf.Clamp01(rumble);
            }
            if (received && direction.sqrMagnitude > .01f)
            {
                feedback.incoming = -direction;
                feedback.indicatorUntil = now + .22;
            }
        }

        private void Awake() => view = GetComponent<Camera>();
        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;
        }

        private void BeginCamera(ScriptableRenderContext context, Camera camera) { if (camera == view) Begin(); }
        private void EndCamera(ScriptableRenderContext context, Camera camera) { if (camera == view) End(); }
        private void OnPreCull() { if (GraphicsSettings.currentRenderPipeline == null) Begin(); }
        private void OnPostRender() { if (GraphicsSettings.currentRenderPipeline == null) End(); }

        private void Begin()
        {
            float envelope = Mathf.Clamp01((float)(until - Time.unscaledTimeAsDouble) / .12f);
            if (!HasView || !follow.CombatImpulses || envelope <= 0 || shifted) return;
            float phase = (1 - envelope) * 16;
            var offset = new Vector3(Mathf.Sin(phase), Mathf.Sin(phase * .71f) * .55f, 0) * (shake * envelope);
            view.worldToCameraMatrix = Matrix4x4.Translate(offset) * view.worldToCameraMatrix;
            shifted = true;
        }

        private void End()
        {
            if (!shifted) return;
            view.ResetWorldToCameraMatrix();
            shifted = false;
        }

        private void Update()
        {
            float envelope = Mathf.Clamp01((float)(until - Time.unscaledTimeAsDouble) / .12f);
            if (!Application.isFocused || !HasView || envelope <= 0) { StopRumble(); return; }
            if (pad != Gamepad.current) { StopRumble(); pad = Gamepad.current; }
            if (pad != null && pad.added) pad.SetMotorSpeeds(rumble * envelope, rumble * envelope * .55f);
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || !HasView || !Application.isFocused) return;
            float alpha = Mathf.Clamp01((float)(indicatorUntil - Time.unscaledTimeAsDouble) / .22f);
            if (alpha <= 0) return;
            Vector3 relative = view.transform.InverseTransformDirection(incoming);
            var direction = new Vector2(relative.x, -relative.z).normalized;
            if (direction == Vector2.zero) return;
            var center = new Vector2(Screen.width * .5f, Screen.height * .5f);
            var point = center + direction * Mathf.Min(Screen.width, Screen.height) * .16f;
            var oldColor = GUI.color;
            var oldMatrix = GUI.matrix;
            GUI.color = new Color(1, .47f, .24f, alpha * .8f);
            GUIUtility.RotateAroundPivot(Vector2.SignedAngle(Vector2.up, direction), point);
            GUI.DrawTexture(new Rect(point.x - 12, point.y - 2, 24, 4), Texture2D.whiteTexture);
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }

        private void StopRumble()
        {
            if (pad != null && pad.added) pad.SetMotorSpeeds(0, 0);
            pad = null;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused) return;
            until = nextPulse = indicatorUntil = 0;
            StopRumble();
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            End();
            StopRumble();
            until = nextPulse = indicatorUntil = 0;
        }
    }
}

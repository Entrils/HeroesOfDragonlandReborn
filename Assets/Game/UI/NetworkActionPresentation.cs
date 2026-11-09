using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    // Presentation reads accepted server timelines. It never runs melee or grants i-frames.
    [DisallowMultipleComponent, RequireComponent(typeof(NetworkPlayerCombat), typeof(NetworkPlayerDodge))]
    public sealed class NetworkActionPresentation : MonoBehaviour
    {
        [SerializeField] private Transform body;
        [SerializeField] private Transform swingPivot;
        private NetworkPlayerCombat combat;
        private NetworkPlayerDodge dodge;
        private NetworkCharacterStats stats;
        public bool SwingVisible { get; private set; }
        public bool RollVisible { get; private set; }
        private void Awake()
        {
            combat = GetComponent<NetworkPlayerCombat>();
            dodge = GetComponent<NetworkPlayerDodge>();
            stats = GetComponent<NetworkCharacterStats>();
        }
        private void LateUpdate()
        {
            if (combat.Object == null || !combat.Object.IsValid) return;
            if (!NetworkMatch.AllowsGameplay(combat.Runner))
            {
                SwingVisible = false; RollVisible = false;
                if (swingPivot != null) swingPivot.gameObject.SetActive(false);
                if (body != null) body.localRotation = Quaternion.identity;
                return;
            }
            double time = combat.HasStateAuthority ? combat.Runner.LocalRenderTime : combat.Runner.RemoteRenderTime;
            float attackAge = (float)(time - combat.AttackStartTick * (double)combat.Runner.DeltaTime);
            float duration = combat.AttackSwingDuration;
            bool swinging = !stats.Dead && combat.AttackBusy && combat.AttackSequence > 0
                && duration > 0 && attackAge >= 0f && attackAge < duration;
            SwingVisible = swinging && swingPivot != null;
            if (swingPivot != null)
            {
                swingPivot.gameObject.SetActive(swinging);
                if (swinging)
                    swingPivot.rotation = Quaternion.LookRotation(combat.AttackFacing)
                        * Quaternion.Euler(0, Mathf.Lerp(-65, 65, attackAge / duration), 0);
            }
            float dodgeAge = (float)(time - dodge.StartTick * (double)combat.Runner.DeltaTime);
            bool rolling = !stats.Dead && dodge.Busy && dodge.DodgeSequence > 0 && dodge.Definition != null
                && dodgeAge >= 0 && dodgeAge < dodge.Definition.Duration;
            RollVisible = rolling && body != null;
            if (body != null)
            {
                Vector3 localDirection = transform.InverseTransformDirection(dodge.Direction);
                body.localRotation = rolling
                    ? Quaternion.AngleAxis(360f * dodgeAge / dodge.Definition.Duration, Vector3.Cross(Vector3.up, localDirection))
                    : Quaternion.identity;
            }
        }
        private void OnDisable()
        {
            SwingVisible = false;
            RollVisible = false;
            if (body != null) body.localRotation = Quaternion.identity;
            if (swingPivot != null) swingPivot.gameObject.SetActive(false);
        }
    }
}

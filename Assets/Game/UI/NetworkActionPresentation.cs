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
            double time = combat.HasStateAuthority ? combat.Runner.LocalRenderTime : combat.Runner.RemoteRenderTime;
            float attackAge = (float)(time - combat.AttackStartTick * (double)combat.Runner.DeltaTime);
            bool swinging = !stats.Dead && combat.AttackSequence > 0 && combat.Definition != null
                && attackAge >= 0f && attackAge < combat.Definition.AttackDuration;
            SwingVisible = swinging && swingPivot != null;
            if (swingPivot != null)
            {
                swingPivot.gameObject.SetActive(swinging);
                if (swinging)
                    swingPivot.rotation = Quaternion.LookRotation(combat.AttackFacing)
                        * Quaternion.Euler(0, Mathf.Lerp(-65, 65, attackAge / combat.Definition.AttackDuration), 0);
            }
            float dodgeAge = (float)(time - dodge.StartTick * (double)combat.Runner.DeltaTime);
            bool rolling = !stats.Dead && dodge.DodgeSequence > 0 && dodge.Definition != null
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

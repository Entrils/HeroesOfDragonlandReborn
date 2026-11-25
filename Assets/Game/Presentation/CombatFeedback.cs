using Fusion;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.Audio;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Player;
using HeroesOfDragonland.Presentation.Vfx;
using HeroesOfDragonland.Teams;
using UnityEngine;

namespace HeroesOfDragonland.Presentation
{
    public static class CombatFeedback
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install()
        {
            DamageReceiver.LocalImpactResolved -= LocalImpact;
            DamageReceiver.LocalImpactResolved += LocalImpact;
        }

        private static void LocalImpact(DamageReceiver target, DamageImpactCue cue, GameObject source, DamageImpact impact)
        {
            var camera = Camera.main;
            var view = camera != null ? camera.GetComponent<ThirdPersonCamera>() : null;
            Present(null, target.gameObject, source, cue, impact, VfxAudience.Team(source), false, view);
        }

        internal static void Present(NetworkRunner runner, GameObject target, GameObject source, DamageImpactCue cue,
            DamageImpact impact, TeamId team, bool friendlyFire, ThirdPersonCamera localView = null)
        {
            var audio = runner != null ? AudioAudience.Service(runner) : Object.FindAnyObjectByType<GameAudioService>();
            if (cue == DamageImpactCue.OtherDamage)
            {
                audio?.Play(target.GetComponent<HeroesOfDragonland.Multiplayer.NetworkEnemy>() != null
                    ? AudioCue.EnemyDamage : AudioCue.Hit, impact.Point, target);
                return;
            }
            var profile = CombatFeedbackProfiles.Current?.Resolve(cue, impact.Weapon);
            bool ranged = impact.Weapon is ImpactWeapon.Bow or ImpactWeapon.Magic;
            var effect = profile?.effect ?? VfxKind.MeleeHit;
            var pool = runner != null ? VfxAudience.Pool(runner) : Object.FindAnyObjectByType<VfxPool>();
            pool?.Play(effect, impact.Point, VfxAudience.Relation(runner, team, true, friendlyFire),
                    direction: impact.Direction, scale: profile?.effectScale ?? 1);
            if (profile == null || Application.isBatchMode) return;

            audio?.PlayImpact(profile.sound, impact.Point, target, profile.pitch);
            GameObject local = null;
            if (runner != null && runner.LocalPlayer != PlayerRef.None && runner.TryGetPlayerObject(runner.LocalPlayer, out var player) && player != null)
                local = player.gameObject;
            else if (localView != null && localView.Target != null)
                local = localView.Target.GetComponentInParent<PlayerController>()?.gameObject;

            float weight = impact.Heavy ? 1.18f : 1;
            var reacting = cue == DamageImpactCue.Parried && source != null ? source : target;
            var direction = cue == DamageImpactCue.Parried ? -impact.Direction : impact.Direction;
            float pause = local != null && (local == source || local == target) ? profile.hitStop * weight : 0;
            if (reacting.GetComponentInChildren<CharacterVisual>() is { } humanoid)
                humanoid.PresentContact(direction, profile.recoil * weight, pause);
            else if (reacting.GetComponent<EnemyModelAnimation>() is { } enemy)
                enemy.PresentContact(direction, profile.recoil * weight, pause);
            if (local == null || (local != source && local != target)) return;

            if (localView == null && Camera.main != null) localView = Camera.main.GetComponent<ThirdPersonCamera>();
            if (localView == null || !localView.isActiveAndEnabled || localView.Target == null
                || (localView.Target != local.transform && !localView.Target.IsChildOf(local.transform))) return;
            CombatViewFeedback.Pulse(localView, reacting == target ? direction : -impact.Direction,
                profile.shake * weight, profile.rumble * weight, local == target || (cue == DamageImpactCue.Parried && local == source));
            if (local == source && source != reacting && !ranged)
                source.GetComponentInChildren<CharacterVisual>()?.PresentContact(Vector3.zero, 0, profile.hitStop * weight);
        }
    }
}

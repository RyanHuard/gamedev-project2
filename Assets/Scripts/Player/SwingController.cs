using UnityEngine;
using HalloweenDerby.Core;
using HalloweenDerby.Pitching;
using HalloweenDerby.Ball;
using HalloweenDerby.Audio;

namespace HalloweenDerby.Player
{
    /// Ties player input to pitch timing: resolves contact quality on swing, and resolves a
    /// plain Miss if the player never swings at all - no separate "strike" concept, a pitch
    /// left alone is scored identically to a whiff.
    public class SwingController : MonoBehaviour
    {
        [SerializeField] private PlayerInputHandler input;
        [SerializeField] private PitchController pitchController;
        [SerializeField] private ZoneReticleController reticle;
        [SerializeField] private ZoneContactMarkers contactMarkers;
        [SerializeField] private Animator batterAnimator;
        [SerializeField] private float maxChargeDuration = 1.2f;

        private bool swungThisPitch;

        public float CurrentChargeFactor => Mathf.Clamp01(input.ChargeHeldDuration / maxChargeDuration);

        private void OnEnable()
        {
            input.SwingPressed += HandleSwing;
            pitchController.PitchStarted += HandlePitchStarted;
        }

        private void OnDisable()
        {
            input.SwingPressed -= HandleSwing;
            pitchController.PitchStarted -= HandlePitchStarted;
        }

        private void HandlePitchStarted(PitchTypeData _)
        {
            swungThisPitch = false;
            contactMarkers?.Hide();
        }

        private void Update()
        {
            if (swungThisPitch || !pitchController.IsBallInFlight || pitchController.CurrentPitch == null)
                return;

            if (pitchController.TimeUntilIdealContact < -pitchController.CurrentPitch.WeakWindow)
                ResolveMiss(wasSwing: false);
        }

        private void HandleSwing()
        {
            if (swungThisPitch || !pitchController.IsBallInFlight || pitchController.CurrentPitch == null)
                return;

            swungThisPitch = true;
            if (batterAnimator != null) batterAnimator.SetTrigger("Swing");
            AudioManager.Instance?.PlayOneShot(SfxId.SwingWhoosh);

            float chargeFactor = CurrentChargeFactor;
            Vector2 reticleNorm = reticle != null ? reticle.NormalizedPosition : Vector2.zero;
            Vector2 pitchTargetNorm = pitchController.CurrentPitchTargetNormalized;
            var result = BallContactResolver.Resolve(pitchController.CurrentPitch, pitchController.TimeUntilIdealContact, chargeFactor, reticleNorm, pitchTargetNorm);
            contactMarkers?.ShowResult(reticleNorm, pitchTargetNorm);

            if (result.Quality == ContactQuality.Miss)
            {
                AudioManager.Instance?.PlayOneShot(SfxId.SwingMiss);
                GameEvents.RaiseHitResolved(HitOutcome.Miss(wasSwing: true));
            }
            else
            {
                SfxId crackSfx = result.Quality == ContactQuality.Perfect ? SfxId.BatCrackPerfect
                    : result.Quality == ContactQuality.Good ? SfxId.BatCrackGood
                    : SfxId.BatCrackWeak;
                AudioManager.Instance?.PlayOneShot(crackSfx);
                pitchController.ActiveBall?.ResolveContact(result.Quality, result.LaunchDirection, result.LaunchSpeed, chargeFactor);
            }

            pitchController.NotifyBallResolved();
        }

        private void ResolveMiss(bool wasSwing)
        {
            swungThisPitch = true;
            AudioManager.Instance?.PlayOneShot(SfxId.SwingMiss);
            GameEvents.RaiseHitResolved(HitOutcome.Miss(wasSwing));
            pitchController.NotifyBallResolved();
        }
    }
}

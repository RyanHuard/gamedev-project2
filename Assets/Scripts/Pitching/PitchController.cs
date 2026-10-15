using System;
using System.Collections;
using UnityEngine;
using HalloweenDerby.Core;
using HalloweenDerby.Ball;
using HalloweenDerby.Player;

namespace HalloweenDerby.Pitching
{
    /// Drives the pitcher's Idle -> Windup -> Released -> FollowThrough -> Idle cycle.
    /// Reused verbatim by the Headless Horseman boss with a faster/meaner PitchTypeData pool.
    public class PitchController : MonoBehaviour
    {
        public enum State { Idle, Windup, Released, FollowThrough }

        [SerializeField] private Animator animator;
        [SerializeField] private BallFlightController ballPrefab;
        [SerializeField] private Transform releasePoint;
        [SerializeField] private Transform platePoint;
        [SerializeField] private StrikeZone strikeZone;
        [SerializeField] private float followThroughDuration = 0.4f;

        public State CurrentState { get; private set; } = State.Idle;
        public PitchTypeData CurrentPitch { get; private set; }
        public BallFlightController ActiveBall { get; private set; }

        /// Seconds until the ball reaches the ideal-contact instant. Positive while approaching,
        /// counts down through zero, goes negative once it has passed the plate.
        public float TimeUntilIdealContact { get; private set; }
        public bool IsBallInFlight => CurrentState == State.Released;
        /// Where this pitch is actually headed, in strike-zone-normalized coordinates - what the
        /// player's reticle needs to match for full spatial credit.
        public Vector2 CurrentPitchTargetNormalized { get; private set; }

        public event Action<PitchTypeData> PitchStarted;
        public event Action PitchCycleCompleted;

        private float releaseTimestamp;
        private bool ballResolvedEarly;
        private Vector3 pitchTargetWorld;
        private float pendingLocationSpread;

        /// locationSpread: 0 = always dead center, 1 = can reach the zone edges, >1 can miss
        /// the zone entirely (a pitch out of the strike zone). Rounds dial this up over time.
        public void BeginPitch(PitchTypeData pitchType, float locationSpread = 0.35f)
        {
            if (CurrentState != State.Idle)
            {
                Debug.LogWarning($"{name}: BeginPitch called while not Idle (state={CurrentState}). Ignoring.");
                return;
            }

            CurrentPitch = pitchType;
            ballResolvedEarly = false;
            pendingLocationSpread = locationSpread;
            PitchStarted?.Invoke(pitchType);
            StartCoroutine(PitchRoutine(pitchType));
        }

        private IEnumerator PitchRoutine(PitchTypeData pitchType)
        {
            CurrentState = State.Windup;
            if (animator != null && !string.IsNullOrEmpty(pitchType.WindupAnimTrigger))
                animator.SetTrigger(pitchType.WindupAnimTrigger);
            GameEvents.RaisePitchWindupStarted(transform);
            HalloweenDerby.Audio.AudioManager.Instance?.PlayOneShot(HalloweenDerby.Audio.SfxId.PitcherWindup);

            yield return new WaitForSeconds(pitchType.WindupDuration);

            Release(pitchType);

            // Extends past FlightDuration by WeakWindow so IsBallInFlight (keyed to
            // CurrentState == Released) stays true through the same late-swing grace period
            // SwingController itself checks (TimeUntilIdealContact < -WeakWindow) and
            // BallFlightController's own TickPitchFlight honors. This used to cut off at
            // exactly FlightDuration - before the grace window even began - so SwingController's
            // "!pitchController.IsBallInFlight" guard silently blocked any late swing attempt
            // regardless of how BallFlightController's own state was extended.
            float elapsed = 0f;
            float lateSwingGraceWindow = pitchType.FlightDuration + pitchType.WeakWindow;
            while (elapsed < lateSwingGraceWindow && !ballResolvedEarly)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            CurrentState = State.FollowThrough;
            yield return new WaitForSeconds(followThroughDuration);

            CurrentState = State.Idle;
            CurrentPitch = null;
            ActiveBall = null;
            PitchCycleCompleted?.Invoke();
        }

        private void Release(PitchTypeData pitchType)
        {
            CurrentState = State.Released;
            releaseTimestamp = Time.time;

            if (animator != null && !string.IsNullOrEmpty(pitchType.ReleaseAnimTrigger))
                animator.SetTrigger(pitchType.ReleaseAnimTrigger);

            pitchTargetWorld = strikeZone != null ? strikeZone.GetRandomTargetPoint(pendingLocationSpread) : platePoint.position;
            CurrentPitchTargetNormalized = strikeZone != null ? strikeZone.WorldToNormalized(pitchTargetWorld) : Vector2.zero;

            if (ballPrefab != null && releasePoint != null)
            {
                ActiveBall = Instantiate(ballPrefab, releasePoint.position, releasePoint.rotation);
                ActiveBall.LaunchPitch(pitchType, releasePoint.position, pitchTargetWorld);
            }
            else
            {
                Debug.LogError($"{name}: cannot release pitch - missing reference(s): " +
                    $"ballPrefab={(ballPrefab != null)}, releasePoint={(releasePoint != null)}.");
            }

            GameEvents.RaisePitchReleased();
        }

        private void Update()
        {
            if (CurrentState != State.Released || CurrentPitch == null)
            {
                TimeUntilIdealContact = float.PositiveInfinity;
                return;
            }

            float elapsed = Time.time - releaseTimestamp;
            TimeUntilIdealContact = CurrentPitch.FlightDuration - elapsed;
        }

        /// Call when contact or a miss ends the ball's flight early so the pitcher doesn't
        /// sit through the rest of the nominal flight duration before resetting.
        public void NotifyBallResolved()
        {
            ballResolvedEarly = true;
        }
    }
}

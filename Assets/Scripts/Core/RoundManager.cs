using System.Collections;
using UnityEngine;
using HalloweenDerby.Pitching;
using HalloweenDerby.Audio;

namespace HalloweenDerby.Core
{
    public class RoundManager : MonoBehaviour
    {
        [SerializeField] private RoundConfig[] rounds;
        [SerializeField] private PitchController pitchController;
        [SerializeField] private float delayBeforeFirstPitch = 2f;
        [SerializeField] private float delayBeforeRetry = 1.5f;

        public RoundConfig CurrentRound { get; private set; }
        public int CurrentRoundIndex { get; private set; } = -1;
        public int OutsUsed { get; private set; }
        public int HomeRunsThisRound { get; private set; }
        public int TargetsHitThisRound { get; private set; }
        public float ScoreThisRound { get; private set; }

        private Coroutine pitchLoop;
        private bool roundActive;
        private bool ballCameraActive;

        private void OnEnable()
        {
            GameEvents.HitResolved += HandleHitResolved;
            GameEvents.BallCameraRequested += HandleBallCameraRequested;
            GameEvents.BallCameraReleased += HandleBallCameraReleased;
            GameEvents.RestartRequested += HandleRestartRequested;
        }

        private void OnDisable()
        {
            GameEvents.HitResolved -= HandleHitResolved;
            GameEvents.BallCameraRequested -= HandleBallCameraRequested;
            GameEvents.BallCameraReleased -= HandleBallCameraReleased;
            GameEvents.RestartRequested -= HandleRestartRequested;
        }

        private void HandleRestartRequested() => StartRound(0);

        private void HandleBallCameraRequested(Transform _) => ballCameraActive = true;
        private void HandleBallCameraReleased() => ballCameraActive = false;

        private void Start()
        {
            if (rounds != null && rounds.Length > 0)
                StartCoroutine(BeginRoundAfterDelay(0, delayBeforeFirstPitch));
        }

        private IEnumerator BeginRoundAfterDelay(int index, float delay)
        {
            yield return new WaitForSeconds(delay);
            StartRound(index);
        }

        public void StartRound(int index)
        {
            if (index < 0 || index >= rounds.Length)
            {
                Debug.Log("RoundManager: no more rounds - derby complete.");
                return;
            }

            CurrentRoundIndex = index;
            CurrentRound = rounds[index];
            OutsUsed = 0;
            HomeRunsThisRound = 0;
            TargetsHitThisRound = 0;
            ScoreThisRound = 0f;
            roundActive = true;

            AudioManager.Instance?.PlayOneShot(SfxId.RoundStart);
            // No music, per feedback - SFX only.
            GameEvents.RaiseRoundStarted(CurrentRound.RoundId);
            GameEvents.RaiseStageChanged(CurrentRoundIndex + 1, rounds.Length);
            GameEvents.RaiseTutorialTextRequested(CurrentRound.TutorialText, 6f);
            GameEvents.RaiseOutsChanged(OutsUsed, CurrentRound.OutLimit);
            GameEvents.RaiseScoreChanged(ScoreThisRound, CurrentRound.PointsToAdvance);

            if (pitchLoop != null) StopCoroutine(pitchLoop);
            pitchLoop = StartCoroutine(PitchLoopRoutine(CurrentRound));
        }

        public void RetryCurrentRound() => StartRound(CurrentRoundIndex);

        public void AdvanceToNextRound() => StartRound(CurrentRoundIndex + 1);

        private IEnumerator PitchLoopRoutine(RoundConfig round)
        {
            while (OutsUsed < round.OutLimit && !(round.EndEarlyOnGoalMet && IsGoalMet(round)))
            {
                var pitchData = round.PickRandomPitch();
                if (pitchData == null)
                {
                    Debug.LogWarning($"RoundManager: {round.RoundId} has no pitches configured.");
                    yield break;
                }

                bool cycleDone = false;
                void OnDone() => cycleDone = true;
                pitchController.PitchCycleCompleted += OnDone;
                pitchController.BeginPitch(pitchData, round.PitchLocationSpread);
                yield return new WaitUntil(() => cycleDone);
                pitchController.PitchCycleCompleted -= OnDone;

                if (OutsUsed >= round.OutLimit || (round.EndEarlyOnGoalMet && IsGoalMet(round)))
                    break;

                // Don't start the next windup until the ball-follow camera has actually
                // returned - on a well-hit ball, PitchController resets on its own fixed
                // follow-through timer regardless of how long the ball is still in the air, so
                // without this the next pitch could start windup while the camera was still
                // mid-flight with the ball.
                yield return new WaitUntil(() => !ballCameraActive);
                yield return new WaitForSeconds(round.PitchInterval);
            }

            EndRound(round);
        }

        private bool IsGoalMet(RoundConfig round)
        {
            return round.SuccessCriteria switch
            {
                RoundSuccessCriteria.HomeRunsOnly => HomeRunsThisRound >= round.HomeRunGoal,
                RoundSuccessCriteria.TargetsOnly => TargetsHitThisRound >= round.TargetGoal,
                RoundSuccessCriteria.Either => HomeRunsThisRound >= round.HomeRunGoal || TargetsHitThisRound >= round.TargetGoal,
                RoundSuccessCriteria.ScoreThreshold => ScoreThisRound >= round.PointsToAdvance,
                _ => false
            };
        }

        private void EndRound(RoundConfig round)
        {
            roundActive = false;
            bool success = IsGoalMet(round);

            GameEvents.RaiseRoundEnded(round.RoundId, success);
            if (success)
            {
                AudioManager.Instance?.PlayOneShot(SfxId.RoundClear);
                bool isFinalStage = CurrentRoundIndex >= rounds.Length - 1;
                if (isFinalStage)
                {
                    GameEvents.RaiseGameWon();
                }
                else
                {
                    // Clearing a stage now pauses on a "Stage Clear" menu instead of
                    // auto-continuing after a fixed delay - RoundManager just waits here until
                    // the HUD reports the player clicked Continue.
                    GameEvents.RaiseStageCleared(CurrentRoundIndex + 1, rounds.Length);
                    StartCoroutine(AdvanceAfterContinue());
                }
            }
            else
            {
                AudioManager.Instance?.PlayOneShot(SfxId.RoundFail);
                StartCoroutine(BeginRoundAfterDelay(CurrentRoundIndex, delayBeforeRetry));
            }
        }

        private IEnumerator AdvanceAfterContinue()
        {
            bool continued = false;
            void OnContinue() => continued = true;
            GameEvents.ContinueRequested += OnContinue;
            yield return new WaitUntil(() => continued);
            GameEvents.ContinueRequested -= OnContinue;
            StartRound(CurrentRoundIndex + 1);
        }

        private void HandleHitResolved(HitOutcome outcome)
        {
            if (!roundActive) return;

            bool counted = false;
            if (outcome.IsHomeRun)
            {
                HomeRunsThisRound++;
                counted = true;
            }
            if (outcome.HitTarget)
            {
                TargetsHitThisRound++;
                counted = true;
            }

            // 1 point per foot; fouls score nothing; home runs are worth double their distance.
            // Target hits (future rounds) still get a flat bonus on top.
            float points = outcome.IsFoul ? 0f : outcome.DistanceFeet * (outcome.IsHomeRun ? 2f : 1f);
            if (outcome.HitTarget) points += 75f;
            ScoreThisRound += points;
            GameEvents.RaiseScoreChanged(ScoreThisRound, CurrentRound.PointsToAdvance);

            if (!counted)
            {
                OutsUsed++;
                GameEvents.RaiseOutsChanged(OutsUsed, CurrentRound.OutLimit);
            }
        }
    }
}

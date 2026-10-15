using System;
using UnityEngine;

namespace HalloweenDerby.Core
{
    /// Central signal bus so systems (camera, audio, HUD, round manager) don't need
    /// direct references to each other - each raises/subscribes to the events it cares about.
    public static class GameEvents
    {
        public static event Action<Transform> PitchWindupStarted;
        public static event Action PitchReleased;
        public static event Action<Vector3, Vector3> BallContactMade; // contact point, ball velocity
        public static event Action<float> BallDistanceUpdated; // live distance-so-far in feet, while airborne
        public static event Action<HitOutcome> HitResolved;
        public static event Action<Vector3> BallLanded; // landing point
        public static event Action<Transform> BallCameraRequested; // ball transform to follow
        public static event Action BallCameraReleased;
        public static event Action<string, float> TutorialTextRequested; // message, duration (0 = until cleared)
        public static event Action<int, int> OutsChanged; // outsUsed, outLimit
        public static event Action<float, float> ScoreChanged; // scoreThisRound, pointsToAdvance
        public static event Action<string> RoundStarted;
        public static event Action<string, bool> RoundEnded; // round id, success
        public static event Action<int, int> StageChanged; // 1-based stage number, total stages - HUD label only
        public static event Action<int, int> StageCleared; // 1-based stage number just cleared, total stages
        public static event Action GameWon;
        public static event Action ContinueRequested;
        public static event Action RestartRequested;

        public static void RaisePitchWindupStarted(Transform pitcherTransform) => PitchWindupStarted?.Invoke(pitcherTransform);
        public static void RaisePitchReleased() => PitchReleased?.Invoke();
        public static void RaiseBallContactMade(Vector3 point, Vector3 velocity) => BallContactMade?.Invoke(point, velocity);
        public static void RaiseBallDistanceUpdated(float feetSoFar) => BallDistanceUpdated?.Invoke(feetSoFar);
        public static void RaiseHitResolved(HitOutcome outcome) => HitResolved?.Invoke(outcome);
        public static void RaiseBallLanded(Vector3 point) => BallLanded?.Invoke(point);
        public static void RaiseBallCameraRequested(Transform ballTransform) => BallCameraRequested?.Invoke(ballTransform);
        public static void RaiseBallCameraReleased() => BallCameraReleased?.Invoke();
        public static void RaiseTutorialTextRequested(string message, float duration = 0f) => TutorialTextRequested?.Invoke(message, duration);
        public static void RaiseOutsChanged(int outsUsed, int outLimit) => OutsChanged?.Invoke(outsUsed, outLimit);
        public static void RaiseScoreChanged(float scoreThisRound, float pointsToAdvance) => ScoreChanged?.Invoke(scoreThisRound, pointsToAdvance);
        public static void RaiseRoundStarted(string roundId) => RoundStarted?.Invoke(roundId);
        public static void RaiseRoundEnded(string roundId, bool success) => RoundEnded?.Invoke(roundId, success);
        public static void RaiseStageChanged(int stageNumber, int totalStages) => StageChanged?.Invoke(stageNumber, totalStages);
        public static void RaiseStageCleared(int stageNumber, int totalStages) => StageCleared?.Invoke(stageNumber, totalStages);
        public static void RaiseGameWon() => GameWon?.Invoke();
        public static void RaiseContinueRequested() => ContinueRequested?.Invoke();
        public static void RaiseRestartRequested() => RestartRequested?.Invoke();
    }
}

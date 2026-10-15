using UnityEngine;
using HalloweenDerby.Pitching;

namespace HalloweenDerby.Core
{
    public enum RoundSuccessCriteria { HomeRunsOnly, TargetsOnly, Either, ScoreThreshold }

    [CreateAssetMenu(fileName = "Round_", menuName = "Halloween Derby/Round Config")]
    public class RoundConfig : ScriptableObject
    {
        [Header("Identity")]
        public string RoundId = "round_1";
        public string DisplayName = "Warm-Up";
        [TextArea] public string TutorialText = "Press SPACE or click right as the pumpkin reaches the plate.";

        [Header("Pitching")]
        public PitchTypeData[] PitchPool;
        public float PitchInterval = 1.5f;
        public bool IsBossRound = false;
        [Tooltip("0 = pitches always target dead-center; 1 = can reach the zone edges; >1 can miss the zone entirely (a ball).")]
        public float PitchLocationSpread = 0.35f;

        [Header("Pass Conditions")]
        public int OutLimit = 5;
        public RoundSuccessCriteria SuccessCriteria = RoundSuccessCriteria.ScoreThreshold;
        [Tooltip("Home runs needed to clear the round (if criteria includes home runs).")]
        public int HomeRunGoal = 3;
        [Tooltip("Targets needed to clear the round (if criteria includes targets).")]
        public int TargetGoal = 3;
        [Tooltip("Points needed to clear the round (if criteria is ScoreThreshold) - 1 point per foot, 2x on a home run, 0 on a foul.")]
        public float PointsToAdvance = 400f;
        [Tooltip("Stop the pitch loop as soon as the goal above is met, instead of always using every out. Points-based rounds generally want this off, since more outs just means more chances to add points.")]
        public bool EndEarlyOnGoalMet = false;

        public PitchTypeData PickRandomPitch()
        {
            if (PitchPool == null || PitchPool.Length == 0) return null;
            return PitchPool[Random.Range(0, PitchPool.Length)];
        }
    }
}

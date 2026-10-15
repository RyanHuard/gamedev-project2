using UnityEngine;

namespace HalloweenDerby.Pitching
{
    [CreateAssetMenu(fileName = "PitchType_", menuName = "Halloween Derby/Pitch Type")]
    public class PitchTypeData : ScriptableObject
    {
        public string DisplayName = "Fastball";

        [Header("Timing")]
        [Tooltip("How long the pitcher telegraphs this pitch before release.")]
        public float WindupDuration = 0.8f;
        [Tooltip("Seconds for the ball to travel from release to the plate.")]
        public float FlightDuration = 0.55f;

        [Header("Animation Tell")]
        [Tooltip("Animator trigger fired at the start of the windup so the pitcher's tell is visually distinct per pitch type.")]
        public string WindupAnimTrigger = "Windup_Fastball";
        public string ReleaseAnimTrigger = "Pitch_Fastball";

        [Header("Ball Path")]
        [Tooltip("Lateral curve applied to the ball's path over its flight (meters at the plate). Positive = breaks toward first base.")]
        public float LateralBreak = 0f;
        [Tooltip("Vertical drop applied on top of gravity, to sell a changeup/curveball tumbling late.")]
        public float ExtraDrop = 0f;

        [Header("Contact Window")]
        [Tooltip("Base +/- seconds around the ideal contact instant that counts as Perfect contact.")]
        public float PerfectWindow = 0.04f;
        [Tooltip("Base +/- seconds that counts as Good contact.")]
        public float GoodWindow = 0.09f;
        [Tooltip("Base +/- seconds that counts as Weak contact (anything wider is a swing-and-miss).")]
        public float WeakWindow = 0.15f;
    }
}

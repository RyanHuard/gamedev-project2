namespace HalloweenDerby.Core
{
    public enum ContactQuality
    {
        Miss,
        Weak,
        Good,
        Perfect
    }

    public struct HitOutcome
    {
        public ContactQuality Quality;
        public float DistanceFeet;
        public bool IsHomeRun;
        public bool HitTarget;
        public bool IsFoul;
        /// Whether the player actually swung - a pitch left alone (reticle never clicked in
        /// time) and a genuine swing-and-miss are both just ContactQuality.Miss; this only
        /// distinguishes them for flavor (SFX/HUD wording), not scoring.
        public bool WasSwing;

        public static HitOutcome Miss(bool wasSwing) => new HitOutcome
        {
            Quality = ContactQuality.Miss,
            DistanceFeet = 0f,
            IsHomeRun = false,
            HitTarget = false,
            IsFoul = false,
            WasSwing = wasSwing
        };
    }
}

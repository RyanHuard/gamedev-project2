namespace HalloweenDerby.Core
{
    /// Shared constants so gameplay logic (BallFlightController's home-run distance) and level
    /// geometry (SceneBuilder's outfield wall placement) can't drift out of sync - they used to
    /// each hardcode their own "300f" independently.
    public static class DerbyConstants
    {
        public const float FeetPerUnit = 3.28084f; // 1 Unity unit = 1 meter
        public const float HomeRunDistanceFeet = 300f;
        public static float HomeRunDistanceUnits => HomeRunDistanceFeet / FeetPerUnit;

        /// Foul lines run at +/-45 degrees from the pitcher-plate line (matches SceneBuilder's
        /// CreateFoulLine angles) - a ball landing outside that cone is foul.
        public const float FoulLineAngleDegrees = 45f;
    }
}

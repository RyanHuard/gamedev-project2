using UnityEngine;
using HalloweenDerby.Core;
using HalloweenDerby.Pitching;

namespace HalloweenDerby.Ball
{
    /// Pure math: "zone hitting" contact resolution (MLB The Show / Super Mega Baseball style).
    /// Direction and launch angle are driven by the reticle's position RELATIVE TO where the
    /// ball actually crosses the zone, not the reticle's raw position:
    ///   - Reticle centered on the ball: solid line-drive contact.
    ///   - Reticle below the ball: gets underneath it - fly ball / potential home run.
    ///   - Reticle above the ball: hits down on it - ground ball.
    ///   - Reticle left of the ball: directs it to right field.
    ///   - Reticle right of the ball: directs it to left field.
    ///   - Farther from the ball (either axis): weaker contact, or a swing-and-miss.
    /// Swing timing is a secondary modifier on top of that: early swings pull the ball, late
    /// swings push it to the opposite field. No MonoBehaviour state so it's trivially
    /// unit-testable.
    public static class BallContactResolver
    {
        public struct ContactResult
        {
            public ContactQuality Quality;
            public Vector3 LaunchDirection; // normalized
            public float LaunchSpeed;
        }

        private const float ChargeWindowShrink = 0.45f; // charge=1 shrinks windows by 45%
        private const float ChargeSpeedBonus = 0.6f; // charge=1 adds 60% launch speed
        // A dead-center "Perfect" swing with no charge used to launch at ~19 degrees and travel
        // only ~238ft at the old 34 m/s - short of the 300ft fence even on a clean hit, with no
        // way to know that without already understanding you need to aim low/charge for power.
        // 41 m/s gets a clean, no-charge Perfect over the fence on its own.
        private const float BaseLaunchSpeed = 41f; // m/s for a Perfect, uncharged swing

        // Tight enough that good timing alone can't carry a swing where the reticle wasn't
        // actually on the ball - 0.8 (most of the zone's radius) let near-total misses of the
        // ball still register as "weak contact" as long as timing was good.
        private const float SpatialPerfectDist = 0.12f;
        private const float SpatialGoodDist = 0.28f;
        private const float SpatialWeakDist = 0.5f;

        private const float MaxPositionYawDegrees = 55f; // reticle-vs-ball horizontal offset - primary direction driver
        private const float MaxTimingYawDegrees = 25f; // timing offset - secondary pull/opposite-field nudge
        // A dead-center hit used to launch at only 19 degrees (a real line drive, ~238ft even at
        // Perfect/no-charge) - realistic, but means a player had to already know to deliberately
        // aim low in the zone just to get a home run-capable arc. Real MLB home runs average
        // ~25-30 degrees; centering this range on ~26 degrees means solid center contact alone
        // is HR-capable, and aiming low/high still meaningfully shifts toward fly ball/grounder.
        private const float MinLaunchAngleDeg = 14f; // reticle above the ball -> line drive/grounder
        private const float MaxLaunchAngleDeg = 38f; // reticle below the ball -> towering fly ball

        /// timeUntilIdealContact: seconds remaining until the ball reaches the plate at the
        /// moment the player swings (positive = early, negative = late).
        /// reticleNormalized/pitchTargetNormalized: zone-local coordinates, (0,0) = dead center,
        /// +/-1 = zone edge, beyond that = outside the zone. +X = right (from the batter's own
        /// point of view, facing the pitcher), +Y = up.
        public static ContactResult Resolve(PitchTypeData pitch, float timeUntilIdealContact, float chargeFactor, Vector2 reticleNormalized, Vector2 pitchTargetNormalized)
        {
            chargeFactor = Mathf.Clamp01(chargeFactor);
            float shrink = 1f - ChargeWindowShrink * chargeFactor;

            ContactQuality timingQuality = ClassifyTiming(pitch, timeUntilIdealContact, shrink, out float timingSeverity, out float pullSign);

            // Where the reticle actually was relative to the ball at contact - this, not the
            // reticle's raw position, is what determines direction/angle/quality.
            Vector2 relative = reticleNormalized - pitchTargetNormalized;
            float spatialDist = relative.magnitude;
            ContactQuality spatialQuality = ClassifySpatial(spatialDist);

            ContactQuality quality = (ContactQuality)Mathf.Min((int)timingQuality, (int)spatialQuality);

            if (quality == ContactQuality.Miss)
                return new ContactResult { Quality = quality, LaunchDirection = Vector3.forward, LaunchSpeed = 0f };

            // Reticle left of the ball (relative.x < 0) -> right field (+X); reticle right of
            // the ball (relative.x > 0) -> left field (-X). Hence the negation.
            float positionYawDeg = -Mathf.Clamp(relative.x, -1.5f, 1.5f) * MaxPositionYawDegrees;
            // Early swing (pullSign > 0, ball hasn't reached the plate yet) pulls to the
            // batter's pull side (-X / left field for a RHB); late swing pushes the other way.
            float timingYawDeg = -pullSign * timingSeverity * MaxTimingYawDegrees;
            float totalYawDeg = Mathf.Clamp(positionYawDeg + timingYawDeg, -75f, 75f);

            // Reticle below the ball (relative.y < 0) -> underneath it -> fly ball. Reticle
            // above the ball (relative.y > 0) -> hits down on it -> grounder.
            float verticalT = (Mathf.Clamp(relative.y, -1.5f, 1.5f) / 1.5f + 1f) * 0.5f;
            float launchAngleDeg = Mathf.Lerp(MaxLaunchAngleDeg, MinLaunchAngleDeg, verticalT);

            Quaternion yaw = Quaternion.Euler(0f, totalYawDeg, 0f);
            Vector3 flatForward = yaw * Vector3.forward;
            Vector3 direction = Quaternion.AngleAxis(-launchAngleDeg, yaw * Vector3.right) * flatForward;
            direction.Normalize();

            float qualitySpeedMul = quality switch
            {
                ContactQuality.Perfect => 1f,
                ContactQuality.Good => 0.92f,
                ContactQuality.Weak => 0.68f,
                _ => 0f
            };
            float speed = BaseLaunchSpeed * qualitySpeedMul * (1f + ChargeSpeedBonus * chargeFactor);

            return new ContactResult { Quality = quality, LaunchDirection = direction, LaunchSpeed = speed };
        }

        private static ContactQuality ClassifyTiming(PitchTypeData pitch, float timeUntilIdealContact, float windowShrink, out float severity, out float pullSign)
        {
            float perfectWindow = pitch.PerfectWindow * windowShrink;
            float goodWindow = pitch.GoodWindow * windowShrink;
            float weakWindow = pitch.WeakWindow * windowShrink;

            float absOffset = Mathf.Abs(timeUntilIdealContact);
            pullSign = Mathf.Sign(timeUntilIdealContact);
            severity = Mathf.Pow(Mathf.Clamp01(absOffset / Mathf.Max(weakWindow, 0.0001f)), 1f / 3f);

            if (absOffset <= perfectWindow) return ContactQuality.Perfect;
            if (absOffset <= goodWindow) return ContactQuality.Good;
            if (absOffset <= weakWindow) return ContactQuality.Weak;
            return ContactQuality.Miss;
        }

        private static ContactQuality ClassifySpatial(float distance)
        {
            if (distance <= SpatialPerfectDist) return ContactQuality.Perfect;
            if (distance <= SpatialGoodDist) return ContactQuality.Good;
            if (distance <= SpatialWeakDist) return ContactQuality.Weak;
            return ContactQuality.Miss;
        }
    }
}

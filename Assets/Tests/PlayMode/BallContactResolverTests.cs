using NUnit.Framework;
using UnityEngine;
using HalloweenDerby.Ball;
using HalloweenDerby.Core;
using HalloweenDerby.Pitching;

namespace HalloweenDerby.Tests
{
    /// Regression coverage for the exact bug reported: direction was observed to always go to
    /// one side regardless of reticle position, caused by (a) direction being driven by the
    /// reticle's raw position instead of its position relative to the ball, and (b) a flipped
    /// sign on the timing-based pull/opposite-field contribution.
    public class BallContactResolverTests
    {
        private static PitchTypeData MakePitch()
        {
            var p = ScriptableObject.CreateInstance<PitchTypeData>();
            p.PerfectWindow = 0.05f;
            p.GoodWindow = 0.1f;
            p.WeakWindow = 0.2f;
            return p;
        }

        [Test]
        public void ReticleLeftOfBall_GoesToRightField()
        {
            var pitch = MakePitch();
            // Perfect timing (offset 0) isolates the position contribution.
            var result = BallContactResolver.Resolve(pitch, 0f, 0f, reticleNormalized: new Vector2(-0.3f, 0f), pitchTargetNormalized: Vector2.zero);
            Assert.AreNotEqual(ContactQuality.Miss, result.Quality);
            Assert.Greater(result.LaunchDirection.x, 0f, "Reticle left of the ball should send it toward +X (right field).");
        }

        [Test]
        public void ReticleRightOfBall_GoesToLeftField()
        {
            var pitch = MakePitch();
            var result = BallContactResolver.Resolve(pitch, 0f, 0f, reticleNormalized: new Vector2(0.3f, 0f), pitchTargetNormalized: Vector2.zero);
            Assert.AreNotEqual(ContactQuality.Miss, result.Quality);
            Assert.Less(result.LaunchDirection.x, 0f, "Reticle right of the ball should send it toward -X (left field).");
        }

        [Test]
        public void ReticleCenteredOnBall_GoesStraight()
        {
            var pitch = MakePitch();
            var result = BallContactResolver.Resolve(pitch, 0f, 0f, reticleNormalized: Vector2.zero, pitchTargetNormalized: Vector2.zero);
            Assert.AreEqual(ContactQuality.Perfect, result.Quality);
            Assert.AreEqual(0f, result.LaunchDirection.x, 0.001f, "Centered contact with perfect timing should go dead straight.");
        }

        [Test]
        public void ReticleBelowBall_ProducesHigherLaunchAngle()
        {
            var pitch = MakePitch();
            var below = BallContactResolver.Resolve(pitch, 0f, 0f, new Vector2(0f, -0.4f), Vector2.zero);
            var above = BallContactResolver.Resolve(pitch, 0f, 0f, new Vector2(0f, 0.4f), Vector2.zero);
            float belowAngle = Mathf.Asin(below.LaunchDirection.y) * Mathf.Rad2Deg;
            float aboveAngle = Mathf.Asin(above.LaunchDirection.y) * Mathf.Rad2Deg;
            Assert.Greater(belowAngle, aboveAngle, "Reticle below the ball (fly ball) should have a higher launch angle than reticle above the ball (grounder).");
        }

        [Test]
        public void EarlySwing_PullsOppositeDirectionFromLateSwing()
        {
            var pitch = MakePitch();
            // Reticle centered on the ball isolates the timing-only contribution.
            var early = BallContactResolver.Resolve(pitch, pitch.WeakWindow * 0.9f, 0f, Vector2.zero, Vector2.zero);
            var late = BallContactResolver.Resolve(pitch, -pitch.WeakWindow * 0.9f, 0f, Vector2.zero, Vector2.zero);
            Assert.Less(early.LaunchDirection.x * late.LaunchDirection.x, 0f, "Early and late swings (same spot) should pull to opposite sides.");
        }

        [Test]
        public void PerfectCenteredSwing_ClearsTheHomeRunFence()
        {
            // A clean, no-charge, dead-center "Perfect" swing must clear the fence on its own -
            // Round 1 only teaches timing, so a player who nails it shouldn't also need to
            // already understand aiming low/charging for power just to see a home run.
            var pitch = MakePitch();
            var result = BallContactResolver.Resolve(pitch, 0f, chargeFactor: 0f, Vector2.zero, Vector2.zero);
            Assert.AreEqual(ContactQuality.Perfect, result.Quality);

            float g = Mathf.Abs(Physics.gravity.y);
            float elevationRad = Mathf.Asin(Mathf.Clamp(result.LaunchDirection.y, -1f, 1f));
            float rangeUnits = (result.LaunchSpeed * result.LaunchSpeed * Mathf.Sin(2f * elevationRad)) / g;

            Assert.GreaterOrEqual(rangeUnits, DerbyConstants.HomeRunDistanceUnits,
                $"Perfect centered swing only traveled {rangeUnits:0.0} units - fence is at {DerbyConstants.HomeRunDistanceUnits:0.0}.");
        }

        [Test]
        public void GoodCenteredSwing_AlsoClearsTheHomeRunFence()
        {
            // The scenario players actually hit most often: a realistic "Good" (not
            // frame-perfect) centered swing, no charge. If only true Perfect can clear the
            // fence, the fence will feel unreachable in practice.
            var pitch = MakePitch();
            // Spatial distance 0.2 sits between SpatialPerfectDist (0.12) and SpatialGoodDist
            // (0.28), so this reads as Good, not Perfect. Timing stays dead-on (0 offset).
            var result = BallContactResolver.Resolve(pitch, 0f, chargeFactor: 0f, new Vector2(0.2f, 0f), Vector2.zero);
            Assert.AreEqual(ContactQuality.Good, result.Quality);

            float g = Mathf.Abs(Physics.gravity.y);
            float elevationRad = Mathf.Asin(Mathf.Clamp(result.LaunchDirection.y, -1f, 1f));
            float rangeUnits = (result.LaunchSpeed * result.LaunchSpeed * Mathf.Sin(2f * elevationRad)) / g;

            Assert.GreaterOrEqual(rangeUnits, DerbyConstants.HomeRunDistanceUnits,
                $"Good centered swing only traveled {rangeUnits:0.0} units - fence is at {DerbyConstants.HomeRunDistanceUnits:0.0}.");
        }

        [Test]
        public void ReticleFarFromBall_IsAMiss()
        {
            var pitch = MakePitch();
            var result = BallContactResolver.Resolve(pitch, 0f, 0f, new Vector2(2f, 2f), Vector2.zero);
            Assert.AreEqual(ContactQuality.Miss, result.Quality);
        }

        [Test]
        public void GoodTimingButReticleNotOnBall_IsStillAMiss()
        {
            var pitch = MakePitch();
            // Perfect timing (offset 0) but the reticle is clearly off the ball (0.65 of a
            // zone-half-width/height away) - timing alone must not carry the swing.
            var result = BallContactResolver.Resolve(pitch, 0f, 0f, new Vector2(0.65f, 0.65f), Vector2.zero);
            Assert.AreEqual(ContactQuality.Miss, result.Quality);
        }
    }
}

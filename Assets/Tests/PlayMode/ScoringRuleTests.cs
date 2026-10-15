using NUnit.Framework;
using UnityEngine;
using HalloweenDerby.Core;

namespace HalloweenDerby.Tests
{
    /// Round 1 scoring rule: 1 point per foot, 0 for a foul, 2x for a home run.
    public class ScoringRuleTests
    {
        private static float ComputePoints(HitOutcome outcome)
        {
            float points = outcome.IsFoul ? 0f : outcome.DistanceFeet * (outcome.IsHomeRun ? 2f : 1f);
            if (outcome.HitTarget) points += 75f;
            return points;
        }

        [Test]
        public void FairBall_ScoresOnePointPerFoot()
        {
            var outcome = new HitOutcome { DistanceFeet = 150f, IsFoul = false, IsHomeRun = false };
            Assert.AreEqual(150f, ComputePoints(outcome));
        }

        [Test]
        public void FoulBall_ScoresZero()
        {
            var outcome = new HitOutcome { DistanceFeet = 220f, IsFoul = true, IsHomeRun = false };
            Assert.AreEqual(0f, ComputePoints(outcome));
        }

        [Test]
        public void HomeRun_ScoresDoubleDistance()
        {
            var outcome = new HitOutcome { DistanceFeet = 310f, IsFoul = false, IsHomeRun = true };
            Assert.AreEqual(620f, ComputePoints(outcome));
        }
    }
}

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using HalloweenDerby.Core;
using HalloweenDerby.Pitching;

namespace HalloweenDerby.Tests
{
    /// Regression test for the Stage 5 rule change: leaving a pitch alone entirely (never
    /// swinging) should not cost an out - only a genuine swing that isn't a home run should.
    /// Drives a real pitch all the way through its auto-miss window without ever calling
    /// SwingController.HandleSwing, and confirms RoundManager.OutsUsed never increments.
    public class TakePitchNoOutTest
    {
        [UnityTest]
        public IEnumerator LeavingAPitchAlone_DoesNotCostAnOut()
        {
            yield return EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/Derby.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            GameEvents.RaiseGameStartRequested();

            var rm = Object.FindAnyObjectByType<RoundManager>();
            var pitchController = Object.FindAnyObjectByType<PitchController>();
            Assert.IsNotNull(rm, "No RoundManager found");
            Assert.IsNotNull(pitchController, "No PitchController found");

            float elapsed = 0f;
            while (pitchController.CurrentState != PitchController.State.Released && elapsed < 8f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            Assert.AreEqual(PitchController.State.Released, pitchController.CurrentState, "Pitch never released within timeout");
            Assert.AreEqual(0, rm.OutsUsed, "Outs should still be 0 before the pitch even resolves");

            var pitch = pitchController.CurrentPitch;
            Assert.IsNotNull(pitch, "No CurrentPitch set after release");

            // Wait well past the auto-miss cutoff (FlightDuration + WeakWindow) without ever
            // touching Swing - this is a genuine "take."
            yield return new WaitForSeconds(pitch.FlightDuration + pitch.WeakWindow + 0.3f);

            Assert.AreEqual(0, rm.OutsUsed,
                "Leaving a pitch alone should not cost an out, but OutsUsed incremented anyway.");
        }
    }
}

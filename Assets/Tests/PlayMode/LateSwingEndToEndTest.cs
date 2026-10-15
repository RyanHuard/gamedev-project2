using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using HalloweenDerby.Pitching;
using HalloweenDerby.Player;

namespace HalloweenDerby.Tests
{
    /// The previous LateSwingTest manipulated BallFlightController's internal state directly
    /// and missed a second, separate gate: SwingController.HandleSwing() refuses to swing unless
    /// pitchController.IsBallInFlight is true, and IsBallInFlight is keyed to PitchController's
    /// own CurrentState, which was flipping out of Released at exactly FlightDuration - before
    /// the late-swing grace window (FlightDuration + WeakWindow) even started. This test drives
    /// the real PitchController coroutine and the real SwingController.HandleSwing to catch
    /// exactly that class of bug.
    public class LateSwingEndToEndTest
    {
        [UnityTest]
        public IEnumerator SwingingJustAfterThePlate_IsNotBlockedByTheGuard()
        {
            yield return EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/Derby.unity",
                new LoadSceneParameters(LoadSceneMode.Single));

            var pitchController = Object.FindAnyObjectByType<PitchController>();
            var swingController = Object.FindAnyObjectByType<SwingController>();
            Assert.IsNotNull(pitchController, "No PitchController found");
            Assert.IsNotNull(swingController, "No SwingController found");

            float elapsed = 0f;
            const float timeout = 8f;
            while (pitchController.CurrentState != PitchController.State.Released && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            Assert.AreEqual(PitchController.State.Released, pitchController.CurrentState, "Pitch never released within timeout");

            var pitch = pitchController.CurrentPitch;
            Assert.IsNotNull(pitch, "No CurrentPitch set after release");

            // Wait until just past the ball's nominal flight duration but still comfortably
            // inside the WeakWindow grace period - a legal "late swing" per the game's own rules.
            float targetWait = pitch.FlightDuration + pitch.WeakWindow * 0.5f;
            yield return new WaitForSeconds(targetWait);

            Assert.IsTrue(pitchController.IsBallInFlight,
                $"IsBallInFlight was false {targetWait:0.00}s after release, inside the " +
                $"FlightDuration({pitch.FlightDuration:0.00}) + WeakWindow({pitch.WeakWindow:0.00}) " +
                "grace window - a late swing attempt here would be silently blocked.");

            var swungField = typeof(SwingController).GetField("swungThisPitch", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsFalse((bool)swungField.GetValue(swingController),
                "The round's own auto-miss logic already resolved this pitch before our late swing attempt - test window needs adjusting, not a product bug.");

            var handleSwing = typeof(SwingController).GetMethod("HandleSwing", BindingFlags.NonPublic | BindingFlags.Instance);
            handleSwing.Invoke(swingController, null);

            Assert.IsTrue((bool)swungField.GetValue(swingController),
                "HandleSwing() returned without setting swungThisPitch - the " +
                "'!pitchController.IsBallInFlight' guard (or another guard) silently blocked a " +
                "legal late swing attempt inside the grace window.");
        }
    }
}

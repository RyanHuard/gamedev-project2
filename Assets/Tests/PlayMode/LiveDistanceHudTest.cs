using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using HalloweenDerby.Core;
using HalloweenDerby.Pitching;
using HalloweenDerby.UI;

namespace HalloweenDerby.Tests
{
    /// End-to-end check that a real contact actually updates the HUD's live distance readout
    /// mid-flight (not just that the event exists) - directly resolves contact on the active
    /// ball once released, bypassing simulated mouse/keyboard input.
    public class LiveDistanceHudTest
    {
        [UnityTest]
        public IEnumerator HitResolvingContact_UpdatesHudDistanceDuringFlight()
        {
            yield return EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/Derby.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            GameEvents.RaiseGameStartRequested();

            var pitchController = Object.FindAnyObjectByType<PitchController>();
            Assert.IsNotNull(pitchController, "No PitchController found");

            float elapsed = 0f;
            const float timeout = 8f;
            while (pitchController.CurrentState != PitchController.State.Released && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            Assert.AreEqual(PitchController.State.Released, pitchController.CurrentState, "Pitch never released within timeout");
            Assert.IsNotNull(pitchController.ActiveBall, "No ball in flight to resolve contact on");

            pitchController.ActiveBall.ResolveContact(Core.ContactQuality.Perfect, new Vector3(0f, 0.4f, 1f), 30f, 0f);

            // Let a few physics steps pass so FixedUpdate raises BallDistanceUpdated.
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return null;

            var hud = Object.FindAnyObjectByType<HUDController>();
            Assert.IsNotNull(hud, "No HUDController found");
            var distanceTextField = typeof(HUDController).GetField("distanceText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var distanceText = (UnityEngine.UI.Text)distanceTextField.GetValue(hud);

            Assert.IsNotNull(distanceText, "HUDController.distanceText is not wired");
            Assert.IsTrue(distanceText.text.Contains("ft"), $"Expected a live '... ft' readout, got '{distanceText.text}'");
            Assert.AreNotEqual("", distanceText.text.Trim(), "Distance text is empty after contact was resolved");
        }
    }
}

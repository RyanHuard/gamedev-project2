using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using HalloweenDerby.Pitching;

namespace HalloweenDerby.Tests
{
    /// Automated equivalent of "press play and watch for the ball" - catches exactly the kind
    /// of silent-reference-breakage bug that's easy to miss when eyeballing the Game view.
    public class PumpkinBallSmokeTest
    {
        [UnityTest]
        public IEnumerator PumpkinBall_SpawnsAndFlies_OnFirstPitch()
        {
            yield return EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/Derby.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            var pitchController = Object.FindAnyObjectByType<PitchController>();
            Assert.IsNotNull(pitchController, "No PitchController found in Derby.unity");

            float elapsed = 0f;
            const float timeout = 8f;
            while (pitchController.CurrentState != PitchController.State.Released && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.AreEqual(PitchController.State.Released, pitchController.CurrentState,
                $"Pitch never reached Released state within {timeout}s (stuck in {pitchController.CurrentState}).");
            Assert.IsNotNull(pitchController.ActiveBall,
                "PitchController.ActiveBall is null after release - the ball failed to spawn.");

            Vector3 posAtRelease = pitchController.ActiveBall.transform.position;
            yield return new WaitForSeconds(0.3f);

            Assert.IsNotNull(pitchController.ActiveBall, "Ball was destroyed unexpectedly during flight.");
            Vector3 posAfter = pitchController.ActiveBall.transform.position;

            Assert.AreNotEqual(posAtRelease, posAfter,
                "Ball position did not change after release - flight physics is not progressing.");

            float traveled = Vector3.Distance(posAtRelease, posAfter);
            Assert.Greater(traveled, 0.05f, $"Ball barely moved ({traveled:0.000} units) in 0.3s - flight looks stalled.");
        }
    }
}

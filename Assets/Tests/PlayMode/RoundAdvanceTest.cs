using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using HalloweenDerby.Core;

namespace HalloweenDerby.Tests
{
    /// Regression test for the exact bug reported: clearing a round (reaching the out limit
    /// with enough points) played the "round clear" sound and then just stopped forever -
    /// EndRound's success branch never actually called AdvanceToNextRound.
    public class RoundAdvanceTest
    {
        private static T GetPrivate<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            return (T)field.GetValue(target);
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(target, value);
        }

        [UnityTest]
        public IEnumerator ClearingARound_ActuallyAdvancesToTheNext()
        {
            yield return EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/Derby.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            GameEvents.RaiseGameStartRequested();

            var rm = Object.FindAnyObjectByType<RoundManager>();
            Assert.IsNotNull(rm, "No RoundManager found");

            // Let RoundManager.Start()'s real delayBeforeFirstPitch elapse so round 0 begins
            // through the normal flow (CurrentRoundIndex becomes a real, not reflected, 0).
            float elapsed = 0f;
            while (rm.CurrentRoundIndex < 0 && elapsed < 5f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            Assert.AreEqual(0, rm.CurrentRoundIndex, "Round 0 never started within timeout");

            // Stop the real pitch loop so it doesn't keep throwing pitches at the same time a
            // forced round-advance is happening underneath it.
            var pitchLoop = GetPrivate<Coroutine>(rm, "pitchLoop");
            if (pitchLoop != null) rm.StopCoroutine(pitchLoop);

            // Force a guaranteed "success" without needing to actually play out 5 real swings -
            // directly drive the private EndRound(RoundConfig) that contains the bug.
            // ScoreThisRound is an auto-property - its backing field has the compiler-generated
            // <Name>k__BackingField form, not the property name itself.
            SetPrivate(rm, "<ScoreThisRound>k__BackingField", 999999f);
            var endRound = typeof(RoundManager).GetMethod("EndRound", BindingFlags.NonPublic | BindingFlags.Instance);
            endRound.Invoke(rm, new object[] { rm.CurrentRound });

            // Clearing a round now pauses on the Stage Clear menu instead of auto-continuing -
            // simulate the player clicking Continue, same as HUDController.HandleContinueClicked.
            GameEvents.RaiseContinueRequested();

            float waited = 0f;
            while (rm.CurrentRoundIndex == 0 && waited < 5f)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            Assert.AreEqual(1, rm.CurrentRoundIndex,
                "Clearing round 0 and clicking Continue should have advanced to round 1 - it stayed on round 0 instead.");
        }
    }
}

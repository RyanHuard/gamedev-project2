using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using HalloweenDerby.Core;
using HalloweenDerby.UI;

namespace HalloweenDerby.Tests
{
    /// Covers the two behaviors added on top of RoundAdvanceTest: the HUD's stage label reads
    /// "Stage N" (not an underscored round id like "round_6_halloween_derby"), and clearing the
    /// final round shows the "You Win" panel instead of trying to advance past the last round.
    public class StageProgressionTest
    {
        private static void SetPrivate(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(target, value);
        }

        private static T GetPrivate<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            return (T)field.GetValue(target);
        }

        [UnityTest]
        public IEnumerator StageLabel_ReadsStageNumber_NotRoundId()
        {
            yield return EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/Derby.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            GameEvents.RaiseGameStartRequested();

            var rm = Object.FindAnyObjectByType<RoundManager>();
            var hud = Object.FindAnyObjectByType<HUDController>();
            Assert.IsNotNull(rm, "No RoundManager found");
            Assert.IsNotNull(hud, "No HUDController found");

            float elapsed = 0f;
            while (rm.CurrentRoundIndex < 0 && elapsed < 5f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            Assert.AreEqual(0, rm.CurrentRoundIndex, "Round 0 never started within timeout");

            var stageText = GetPrivate<Text>(hud, "stageText");
            Assert.IsNotNull(stageText, "HUDController has no stageText wired");
            Assert.AreEqual("Stage 1", stageText.text,
                $"Expected the stage label to read 'Stage 1', got '{stageText.text}' instead.");
        }

        [UnityTest]
        public IEnumerator ClearingTheFinalRound_ShowsGameWonPanel_NotAnotherStage()
        {
            yield return EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/Derby.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            GameEvents.RaiseGameStartRequested();

            var rm = Object.FindAnyObjectByType<RoundManager>();
            var hud = Object.FindAnyObjectByType<HUDController>();
            Assert.IsNotNull(rm, "No RoundManager found");
            Assert.IsNotNull(hud, "No HUDController found");

            float elapsed = 0f;
            while (rm.CurrentRoundIndex < 0 && elapsed < 5f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            Assert.AreEqual(0, rm.CurrentRoundIndex, "Round 0 never started within timeout");

            var pitchLoop = GetPrivate<Coroutine>(rm, "pitchLoop");
            if (pitchLoop != null) rm.StopCoroutine(pitchLoop);

            // Force CurrentRoundIndex straight to the last round (5 of 6) via StartRound, which
            // resets per-round state the same way a real playthrough reaching it would.
            rm.StartRound(5);
            yield return null;
            Assert.AreEqual(5, rm.CurrentRoundIndex, "Failed to jump to the final round for this test");

            SetPrivate(rm, "<ScoreThisRound>k__BackingField", 999999f);
            var endRound = typeof(RoundManager).GetMethod("EndRound", BindingFlags.NonPublic | BindingFlags.Instance);
            endRound.Invoke(rm, new object[] { rm.CurrentRound });
            yield return null;

            var gameWonPanel = GetPrivate<GameObject>(hud, "gameWonPanel");
            var stageClearPanel = GetPrivate<GameObject>(hud, "stageClearPanel");
            Assert.IsNotNull(gameWonPanel, "HUDController has no gameWonPanel wired");
            Assert.IsTrue(gameWonPanel.activeSelf, "Clearing the final round should show the You Win panel.");
            Assert.IsFalse(stageClearPanel.activeSelf, "The final round should show You Win, not a Stage Clear prompt for a stage that doesn't exist.");
            Assert.AreEqual(5, rm.CurrentRoundIndex, "Clearing the final round should not advance past it.");
        }
    }
}

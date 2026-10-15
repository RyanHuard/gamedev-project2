using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using HalloweenDerby.Core;

namespace HalloweenDerby.UI
{
    /// Plain uGUI (not TextMeshPro) so the HUD works without importing TMP's essential
    /// resources package - swap to TMP_Text later if/when that import step is done.
    public class HUDController : MonoBehaviour
    {
        [SerializeField] private Text distanceText;
        [SerializeField] private Text outsText;
        [SerializeField] private Text stageText;
        [SerializeField] private Text scoreText;
        [SerializeField] private GameObject tutorialPanel;
        [SerializeField] private Text tutorialText;

        [SerializeField] private GameObject stageClearPanel;
        [SerializeField] private Text stageClearText;
        [SerializeField] private Button continueButton;
        [SerializeField] private GameObject gameWonPanel;
        [SerializeField] private Text gameWonText;
        [SerializeField] private Button playAgainButton;

        private Coroutine tutorialHideRoutine;

        private void OnEnable()
        {
            GameEvents.HitResolved += HandleHitResolved;
            GameEvents.BallDistanceUpdated += HandleBallDistanceUpdated;
            GameEvents.OutsChanged += HandleOutsChanged;
            GameEvents.ScoreChanged += HandleScoreChanged;
            GameEvents.TutorialTextRequested += HandleTutorialTextRequested;
            GameEvents.StageChanged += HandleStageChanged;
            GameEvents.StageCleared += HandleStageCleared;
            GameEvents.GameWon += HandleGameWon;

            if (continueButton != null) continueButton.onClick.AddListener(HandleContinueClicked);
            if (playAgainButton != null) playAgainButton.onClick.AddListener(HandlePlayAgainClicked);
        }

        private void OnDisable()
        {
            GameEvents.HitResolved -= HandleHitResolved;
            GameEvents.BallDistanceUpdated -= HandleBallDistanceUpdated;
            GameEvents.OutsChanged -= HandleOutsChanged;
            GameEvents.ScoreChanged -= HandleScoreChanged;
            GameEvents.TutorialTextRequested -= HandleTutorialTextRequested;
            GameEvents.StageChanged -= HandleStageChanged;
            GameEvents.StageCleared -= HandleStageCleared;
            GameEvents.GameWon -= HandleGameWon;

            if (continueButton != null) continueButton.onClick.RemoveListener(HandleContinueClicked);
            if (playAgainButton != null) playAgainButton.onClick.RemoveListener(HandlePlayAgainClicked);
        }

        private void HandleBallDistanceUpdated(float feetSoFar)
        {
            if (distanceText != null) distanceText.text = $"{feetSoFar:0} ft";
        }

        private void Start()
        {
            if (tutorialPanel != null) tutorialPanel.SetActive(false);
            if (stageClearPanel != null) stageClearPanel.SetActive(false);
            if (gameWonPanel != null) gameWonPanel.SetActive(false);
        }

        private void HandleHitResolved(HitOutcome outcome)
        {
            if (distanceText == null) return;
            if (outcome.Quality == ContactQuality.Miss)
            {
                distanceText.text = "MISS";
                return;
            }
            if (outcome.IsFoul)
            {
                distanceText.text = $"FOUL ({outcome.DistanceFeet:0} ft)";
                return;
            }

            string suffix = outcome.IsHomeRun ? " - HOME RUN!" : outcome.HitTarget ? " - TARGET HIT!" : "";
            distanceText.text = $"{outcome.DistanceFeet:0} ft{suffix}";
        }

        private void HandleOutsChanged(int outsUsed, int outLimit)
        {
            if (outsText != null) outsText.text = $"Outs: {outsUsed} / {outLimit}";
        }

        private void HandleScoreChanged(float score, float pointsToAdvance)
        {
            if (scoreText != null) scoreText.text = $"Score: {score:0} / {pointsToAdvance:0}";
        }

        private void HandleStageChanged(int stageNumber, int totalStages)
        {
            if (stageText != null) stageText.text = $"Stage {stageNumber}";
        }

        private void HandleStageCleared(int stageNumber, int totalStages)
        {
            if (stageClearPanel == null || stageClearText == null) return;
            stageClearText.text = $"Stage {stageNumber} Clear!\n\nOnward to Stage {stageNumber + 1}...";
            stageClearPanel.SetActive(true);
        }

        private void HandleContinueClicked()
        {
            if (stageClearPanel != null) stageClearPanel.SetActive(false);
            GameEvents.RaiseContinueRequested();
        }

        private void HandleGameWon()
        {
            if (gameWonPanel == null || gameWonText == null) return;
            gameWonText.text = "YOU WIN!\n\nThanks for playing Halloween Derby.";
            gameWonPanel.SetActive(true);
        }

        private void HandlePlayAgainClicked()
        {
            if (gameWonPanel != null) gameWonPanel.SetActive(false);
            GameEvents.RaiseRestartRequested();
        }

        private void HandleTutorialTextRequested(string message, float duration)
        {
            if (tutorialPanel == null || tutorialText == null) return;

            tutorialText.text = message;
            tutorialPanel.SetActive(true);

            if (tutorialHideRoutine != null) StopCoroutine(tutorialHideRoutine);
            if (duration > 0f) tutorialHideRoutine = StartCoroutine(HideTutorialAfter(duration));
        }

        private IEnumerator HideTutorialAfter(float duration)
        {
            yield return new WaitForSeconds(duration);
            if (tutorialPanel != null) tutorialPanel.SetActive(false);
        }
    }
}

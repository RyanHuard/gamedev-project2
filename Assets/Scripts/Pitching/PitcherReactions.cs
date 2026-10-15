using UnityEngine;
using HalloweenDerby.Core;

namespace HalloweenDerby.Pitching
{
    /// Shared by the skeleton pitcher and the Headless Horseman boss: celebrates strikeouts,
    /// slumps on home runs. Pure Animator-trigger driven so it works with whatever clips were
    /// actually sourced for each character.
    public class PitcherReactions : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private string celebrateTrigger = "Celebrate";
        [SerializeField] private string slumpTrigger = "Slump";

        private void OnEnable() => GameEvents.HitResolved += HandleHitResolved;
        private void OnDisable() => GameEvents.HitResolved -= HandleHitResolved;

        private void HandleHitResolved(HitOutcome outcome)
        {
            if (animator == null) return;

            if (outcome.IsHomeRun)
                animator.SetTrigger(slumpTrigger);
            else if (outcome.Quality == ContactQuality.Miss)
                animator.SetTrigger(celebrateTrigger);
        }
    }
}

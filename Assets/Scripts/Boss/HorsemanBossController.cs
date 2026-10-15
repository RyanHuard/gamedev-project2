using UnityEngine;
using HalloweenDerby.Core;
using HalloweenDerby.Pitching;

namespace HalloweenDerby.Boss
{
    /// The Headless Horseman is just a PitchController (on the same GameObject) with a faster,
    /// fully-mixed pitch pool and a couple of boss-only flourishes on round start/clear.
    [RequireComponent(typeof(PitchController))]
    public class HorsemanBossController : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private string chargeInTrigger = "BossIntro";
        [SerializeField] private string defeatedTrigger = "BossDefeated";

        private void OnEnable()
        {
            GameEvents.RoundStarted += HandleRoundStarted;
            GameEvents.RoundEnded += HandleRoundEnded;
        }

        private void OnDisable()
        {
            GameEvents.RoundStarted -= HandleRoundStarted;
            GameEvents.RoundEnded -= HandleRoundEnded;
        }

        private void HandleRoundStarted(string roundId)
        {
            if (roundId == "round_6_halloween_derby" && animator != null)
                animator.SetTrigger(chargeInTrigger);
        }

        private void HandleRoundEnded(string roundId, bool success)
        {
            if (roundId == "round_6_halloween_derby" && success && animator != null)
                animator.SetTrigger(defeatedTrigger);
        }
    }
}

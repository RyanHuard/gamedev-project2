using UnityEngine;
using HalloweenDerby.Audio;

namespace HalloweenDerby.Targets
{
    /// Round 3 teaches the power swing: windows ignore weak/good charge-less contact
    /// and only break on a sufficiently charged hit, enforced by requiresChargedHit.
    public class HouseWindowTarget : TargetBase
    {
        [SerializeField] private GameObject brokenVisual;
        [SerializeField] private GameObject intactVisual;
        [SerializeField] private float minimumChargeToBreak = 0.5f;

        public bool TryHit(float chargeFactorUsed)
        {
            if (IsBroken) return false;
            if (requiresChargedHit && chargeFactorUsed < minimumChargeToBreak)
            {
                AudioManager.Instance?.PlayOneShot(SfxId.TargetThud);
                return false;
            }

            OnHitByBall();
            return true;
        }

        protected override void React()
        {
            AudioManager.Instance?.PlayOneShot(SfxId.GlassBreak);
            if (intactVisual != null) intactVisual.SetActive(false);
            if (brokenVisual != null) brokenVisual.SetActive(true);
        }
    }
}

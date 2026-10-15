using UnityEngine;

namespace HalloweenDerby.Targets
{
    /// Base for anything a hit ball can trigger a round-specific reaction on
    /// (tombstones, haunted-house windows, bats). Subclasses add their own
    /// visual/audio reaction; RoundManager listens to GameEvents.HitResolved
    /// for scoring rather than polling targets directly.
    public abstract class TargetBase : MonoBehaviour
    {
        [SerializeField] protected bool requiresChargedHit;
        [SerializeField] protected string hitSfxId = "TargetBreak";

        public bool IsBroken { get; protected set; }

        public virtual void OnHitByBall()
        {
            if (IsBroken) return;
            IsBroken = true;
            React();
        }

        protected abstract void React();
    }
}

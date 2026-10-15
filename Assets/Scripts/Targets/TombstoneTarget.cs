using UnityEngine;
using HalloweenDerby.Audio;

namespace HalloweenDerby.Targets
{
    public class TombstoneTarget : TargetBase
    {
        [SerializeField] private GameObject brokenVisual;
        [SerializeField] private GameObject intactVisual;

        protected override void React()
        {
            AudioManager.Instance?.PlayOneShot(SfxId.TombstoneBreak);
            if (intactVisual != null) intactVisual.SetActive(false);
            if (brokenVisual != null) brokenVisual.SetActive(true);
        }
    }
}

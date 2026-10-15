using System.Collections;
using UnityEngine;

namespace HalloweenDerby.Player
{
    /// After each swing, briefly shows where the reticle actually was (red X) versus where the
    /// ball actually crossed the zone (orange dot), so the player can read exactly how close -
    /// or far off - their swing was. Hidden again once the next pitch begins.
    public class ZoneContactMarkers : MonoBehaviour
    {
        [SerializeField] private StrikeZone zone;
        [SerializeField] private Transform swingMarker;
        [SerializeField] private Transform ballMarker;
        [SerializeField] private float displayDuration = 1.3f;

        private Coroutine hideRoutine;

        private void Awake()
        {
            if (swingMarker != null) swingMarker.gameObject.SetActive(false);
            if (ballMarker != null) ballMarker.gameObject.SetActive(false);
        }

        public void ShowResult(Vector2 reticleNormalized, Vector2 pitchTargetNormalized)
        {
            if (zone == null) return;

            if (swingMarker != null)
            {
                swingMarker.position = zone.NormalizedToWorld(reticleNormalized);
                swingMarker.gameObject.SetActive(true);
            }
            if (ballMarker != null)
            {
                ballMarker.position = zone.NormalizedToWorld(pitchTargetNormalized);
                ballMarker.gameObject.SetActive(true);
            }

            if (hideRoutine != null) StopCoroutine(hideRoutine);
            hideRoutine = StartCoroutine(HideAfterDelay());
        }

        public void Hide()
        {
            if (hideRoutine != null) StopCoroutine(hideRoutine);
            hideRoutine = null;
            if (swingMarker != null) swingMarker.gameObject.SetActive(false);
            if (ballMarker != null) ballMarker.gameObject.SetActive(false);
        }

        private IEnumerator HideAfterDelay()
        {
            yield return new WaitForSeconds(displayDuration);
            Hide();
        }
    }
}

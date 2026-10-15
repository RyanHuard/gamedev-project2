using System.Collections;
using UnityEngine;

namespace HalloweenDerby.Pitching
{
    /// Fallback "tell" for when the pitcher has no real Animator Controller yet: squashes and
    /// reddens the primitive capsule through the windup, then snaps forward on release. Safe to
    /// leave attached alongside a real Animator later - it only touches scale/color, and an
    /// Animator-driven character can just not have this component at all once clips exist.
    public class PrimitiveWindupTelegraph : MonoBehaviour
    {
        [SerializeField] private PitchController pitchController;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Renderer bodyRenderer;

        private Color baseColor;
        private Vector3 baseScale;
        private Material instanceMaterial;

        private void Awake()
        {
            instanceMaterial = bodyRenderer.material; // instance copy, safe to mutate at runtime
            baseColor = instanceMaterial.color;
            baseScale = visualRoot.localScale;
        }

        private void OnEnable()
        {
            pitchController.PitchStarted += HandlePitchStarted;
        }

        private void OnDisable()
        {
            pitchController.PitchStarted -= HandlePitchStarted;
        }

        private void HandlePitchStarted(PitchTypeData data)
        {
            StopAllCoroutines();
            StartCoroutine(TelegraphRoutine(data.WindupDuration));
        }

        private IEnumerator TelegraphRoutine(float windupDuration)
        {
            float t = 0f;
            Vector3 leanBack = baseScale + new Vector3(0f, -0.2f, 0.2f);
            while (t < windupDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / windupDuration);
                instanceMaterial.color = Color.Lerp(baseColor, Color.red, p);
                visualRoot.localScale = Vector3.Lerp(baseScale, leanBack, p);
                yield return null;
            }

            // release snap
            instanceMaterial.color = Color.white;
            visualRoot.localScale = baseScale + new Vector3(0f, 0.15f, -0.25f);

            yield return new WaitForSeconds(0.15f);

            instanceMaterial.color = baseColor;
            visualRoot.localScale = baseScale;
        }
    }
}

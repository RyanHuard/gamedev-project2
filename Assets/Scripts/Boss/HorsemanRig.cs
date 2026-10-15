using UnityEngine;

namespace HalloweenDerby.Boss
{
    /// Code-driven gallop/idle bob for the primitive horse body, since there's no CC0 horse
    /// model to source a real gallop clip from. Same procedural-animation approach as
    /// PrimitiveWindupTelegraph on the skeleton pitcher - satisfies "every character must be
    /// visibly animated" without a baked clip.
    public class HorsemanRig : MonoBehaviour
    {
        [SerializeField] private Transform bodyRoot;
        [SerializeField] private Transform neck;
        [SerializeField] private Transform[] legs;
        [SerializeField] private float bobAmplitude = 0.08f;
        [SerializeField] private float bobFrequency = 2.2f;
        [SerializeField] private float legSwingDegrees = 18f;

        private Vector3 bodyBasePos;
        private Vector3 neckBasePos;
        private Vector3[] legBasePos;

        private void Awake()
        {
            if (bodyRoot != null) bodyBasePos = bodyRoot.localPosition;
            if (neck != null) neckBasePos = neck.localPosition;
            if (legs != null)
            {
                legBasePos = new Vector3[legs.Length];
                for (int i = 0; i < legs.Length; i++)
                    if (legs[i] != null) legBasePos[i] = legs[i].localPosition;
            }
        }

        private void Update()
        {
            float t = Time.time * bobFrequency;
            float bob = Mathf.Sin(t) * bobAmplitude;

            if (bodyRoot != null) bodyRoot.localPosition = bodyBasePos + Vector3.up * bob;
            if (neck != null) neck.localPosition = neckBasePos + Vector3.up * bob;

            if (legs != null)
            {
                for (int i = 0; i < legs.Length; i++)
                {
                    if (legs[i] == null) continue;
                    float phase = t + (i % 2 == 0 ? 0f : Mathf.PI);
                    float swing = Mathf.Sin(phase) * legSwingDegrees;
                    legs[i].localRotation = Quaternion.Euler(swing, 0f, 0f);
                    legs[i].localPosition = legBasePos[i] + Vector3.up * Mathf.Abs(Mathf.Sin(phase)) * 0.04f;
                }
            }
        }
    }
}

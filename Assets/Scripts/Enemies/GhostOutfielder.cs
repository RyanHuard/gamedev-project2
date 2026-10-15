using UnityEngine;
using HalloweenDerby.Core;
using HalloweenDerby.Ball;
using HalloweenDerby.Audio;

namespace HalloweenDerby.Enemies
{
    /// Round 5 ("Bat Swarm") reuses aim by forcing the player to hit over/around ghosts
    /// that predict the ball's landing spot and try to catch it. FSM: Drift -> Intercept -> Drift.
    /// Floats with a procedural bob/drift since it has no imported animation clip.
    public class GhostOutfielder : MonoBehaviour
    {
        private enum State { Drift, Intercept }

        [SerializeField] private float driftRadius = 5f;
        [SerializeField] private float driftSpeed = 0.6f;
        [SerializeField] private float interceptSpeed = 6.5f;
        [SerializeField] private float bobAmplitude = 0.3f;
        [SerializeField] private float bobFrequency = 1f;
        [SerializeField] private float giveUpAfterSeconds = 4f;

        private State state = State.Drift;
        private Vector3 homePosition;
        private Vector3 predictedLandingPoint;
        private float interceptStartedAt;
        private float driftPhaseOffset;

        private void Start()
        {
            homePosition = transform.position;
            driftPhaseOffset = Random.Range(0f, Mathf.PI * 2f);
        }

        private void OnEnable() => GameEvents.BallContactMade += HandleBallContact;
        private void OnDisable() => GameEvents.BallContactMade -= HandleBallContact;

        private void HandleBallContact(Vector3 contactPoint, Vector3 velocity)
        {
            predictedLandingPoint = PredictLanding(contactPoint, velocity, homePosition.y);
            state = State.Intercept;
            interceptStartedAt = Time.time;
        }

        private static Vector3 PredictLanding(Vector3 start, Vector3 velocity, float groundY)
        {
            float g = Mathf.Abs(Physics.gravity.y);
            float vy = velocity.y;
            float heightAboveGround = start.y - groundY;
            float discriminant = vy * vy + 2f * g * heightAboveGround;
            float t = discriminant > 0f ? (vy + Mathf.Sqrt(discriminant)) / g : 0f;
            return start + new Vector3(velocity.x, 0f, velocity.z) * t;
        }

        private void Update()
        {
            float bob = Mathf.Sin(Time.time * bobFrequency + driftPhaseOffset) * bobAmplitude;

            if (state == State.Drift)
            {
                float t = Time.time * driftSpeed + driftPhaseOffset;
                Vector3 offset = new Vector3(Mathf.Sin(t), 0f, Mathf.Cos(t * 0.7f)) * driftRadius;
                Vector3 target = homePosition + offset;
                target.y = homePosition.y + bob;
                transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime);
            }
            else
            {
                Vector3 flatTarget = new Vector3(predictedLandingPoint.x, transform.position.y, predictedLandingPoint.z);
                transform.position = Vector3.MoveTowards(transform.position, flatTarget, interceptSpeed * Time.deltaTime);
                transform.position += Vector3.up * (bob * Time.deltaTime);

                if (Time.time - interceptStartedAt > giveUpAfterSeconds)
                    state = State.Drift;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (state != State.Intercept) return;

            var ball = other.GetComponentInParent<BallFlightController>();
            if (ball == null) return;

            ball.Catch();
            AudioManager.Instance?.PlayOneShot(SfxId.GhostOooo);
            state = State.Drift;
        }
    }
}

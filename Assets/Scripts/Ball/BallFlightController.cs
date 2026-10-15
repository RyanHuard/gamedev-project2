using UnityEngine;
using HalloweenDerby.Core;
using HalloweenDerby.Pitching;
using HalloweenDerby.Targets;
using HalloweenDerby.Audio;

namespace HalloweenDerby.Ball
{
    [RequireComponent(typeof(Rigidbody))]
    public class BallFlightController : MonoBehaviour
    {
        private enum FlightState { Pitching, Hit, Idle }

        [SerializeField] private Rigidbody rb;
        [Tooltip("1 Unity unit = 1 meter; this converts to feet for the HUD distance readout.")]
        [SerializeField] private float feetPerUnit = DerbyConstants.FeetPerUnit;
        [SerializeField] private float groundY = 0f;
        [SerializeField] private float homeRunDistanceFeet = DerbyConstants.HomeRunDistanceFeet;
        [SerializeField] private float despawnDelay = 3f;
        [Tooltip("Candy-burst VFX spawned where the jack-o'-lantern lands.")]
        [SerializeField] private GameObject landingBurstPrefab;

        private FlightState state = FlightState.Idle;
        private PitchTypeData currentPitch;
        private Vector3 pitchStart;
        private Vector3 platePosition;
        private float pitchElapsed;
        private ContactQuality pendingQuality;
        private float pendingChargeFactor;
        private bool pendingIsFoul;
        private bool outcomeReported;

        private void Awake()
        {
            if (rb == null) rb = GetComponent<Rigidbody>();
        }

        public void LaunchPitch(PitchTypeData pitch, Vector3 releasePos, Vector3 platePos)
        {
            currentPitch = pitch;
            pitchStart = releasePos;
            platePosition = platePos;
            pitchElapsed = 0f;
            outcomeReported = false;
            state = FlightState.Pitching;
            rb.isKinematic = true;
            transform.position = releasePos;
        }

        public void ResolveContact(ContactQuality quality, Vector3 direction, float speed, float chargeFactor = 0f)
        {
            if (state != FlightState.Pitching) return;

            pendingQuality = quality;
            pendingChargeFactor = chargeFactor;
            state = FlightState.Hit;
            rb.isKinematic = false;
            rb.linearVelocity = direction.normalized * speed;

            // Gravity only acts on Y, so the horizontal direction (and therefore fair/foul)
            // never changes after contact - safe to decide it once here.
            Vector3 flatDir = new Vector3(direction.x, 0f, direction.z);
            pendingIsFoul = flatDir.sqrMagnitude > 0.0001f
                && Vector3.Angle(Vector3.forward, flatDir) > DerbyConstants.FoulLineAngleDegrees;

            GameEvents.RaiseBallContactMade(transform.position, rb.linearVelocity);
            GameEvents.RaiseBallCameraRequested(transform);
        }

        private void FixedUpdate()
        {
            switch (state)
            {
                case FlightState.Pitching:
                    TickPitchFlight();
                    break;
                case FlightState.Hit:
                    GameEvents.RaiseBallDistanceUpdated(HorizontalDistanceFeet(transform.position));
                    if (transform.position.y <= groundY)
                        ResolveLanding();
                    break;
            }
        }

        private void TickPitchFlight()
        {
            pitchElapsed += Time.fixedDeltaTime;
            float t = pitchElapsed / Mathf.Max(currentPitch.FlightDuration, 0.0001f);
            float tEase = Mathf.Clamp01(t); // break/drop shouldn't keep exaggerating past the plate

            // LerpUnclamped (not Lerp) so the ball keeps traveling naturally through the late-
            // swing grace window below instead of freezing at the plate once t > 1.
            Vector3 straight = Vector3.LerpUnclamped(pitchStart, platePosition, t);
            Vector3 travelDir = (platePosition - pitchStart).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, travelDir);
            float curveEase = tEase * tEase;
            Vector3 offset = right * (currentPitch.LateralBreak * curveEase) + Vector3.down * (currentPitch.ExtraDrop * curveEase);
            transform.position = straight + offset;

            // Stay in the "swingable" (Pitching) state through the same late-swing grace window
            // SwingController uses for its own auto-miss cutoff (WeakWindow past the plate) -
            // previously this flipped to Idle at the exact instant t>=1, which was BEFORE that
            // window even started, so any legitimately-late-but-still-in-window swing hit a ball
            // that had already left the Pitching state and silently failed to register at all.
            if (pitchElapsed >= currentPitch.FlightDuration + currentPitch.WeakWindow)
            {
                // Passed the plate with no contact - SwingController reports the taken strike;
                // just let the ball sail on and get cleaned up.
                state = FlightState.Idle;
                rb.isKinematic = false;
                rb.linearVelocity = travelDir * 20f;
                Destroy(gameObject, despawnDelay);
            }
        }

        private void ResolveLanding()
        {
            if (outcomeReported) return;
            outcomeReported = true;
            FreezeBall();
            transform.position = new Vector3(transform.position.x, groundY, transform.position.z);

            float distanceFeet = HorizontalDistanceFeet(transform.position);
            var outcome = new HitOutcome
            {
                Quality = pendingQuality,
                DistanceFeet = distanceFeet,
                IsHomeRun = !pendingIsFoul && distanceFeet >= homeRunDistanceFeet,
                HitTarget = false,
                IsFoul = pendingIsFoul,
                WasSwing = true
            };

            GameEvents.RaiseHitResolved(outcome);
            GameEvents.RaiseBallLanded(transform.position);
            GameEvents.RaiseBallCameraReleased();
            AudioManager.Instance?.PlayOneShot(SfxId.PumpkinBurst);
            if (landingBurstPrefab != null) Instantiate(landingBurstPrefab, transform.position, Quaternion.identity);
            Destroy(gameObject, despawnDelay);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (state != FlightState.Hit || outcomeReported) return;

            var target = other.GetComponentInParent<TargetBase>();
            if (target == null) return;

            if (target is HouseWindowTarget window)
            {
                if (!window.TryHit(pendingChargeFactor)) return; // unbroken - ball just thuds off, keep flying
            }
            else
            {
                target.OnHitByBall();
            }

            outcomeReported = true;
            FreezeBall();

            var outcome = new HitOutcome
            {
                Quality = pendingQuality,
                DistanceFeet = HorizontalDistanceFeet(transform.position),
                IsHomeRun = false,
                HitTarget = true,
                IsFoul = false,
                WasSwing = true
            };

            GameEvents.RaiseHitResolved(outcome);
            GameEvents.RaiseBallCameraReleased();
            Destroy(gameObject, despawnDelay);
        }

        /// Called by a GhostOutfielder that intercepted the ball mid-flight. Forces a non-home-run,
        /// non-target outcome (an out) regardless of the distance the ball would otherwise have reached.
        public void Catch()
        {
            if (state != FlightState.Hit || outcomeReported) return;
            outcomeReported = true;
            FreezeBall();

            var outcome = new HitOutcome
            {
                Quality = pendingQuality,
                DistanceFeet = HorizontalDistanceFeet(transform.position),
                IsHomeRun = false,
                HitTarget = false,
                IsFoul = pendingIsFoul,
                WasSwing = true
            };

            GameEvents.RaiseHitResolved(outcome);
            GameEvents.RaiseBallCameraReleased();
            Destroy(gameObject, despawnDelay);
        }

        /// Stops the Rigidbody the instant an outcome is decided. Without this, gravity kept
        /// acting on the ball for the full despawnDelay before Destroy() actually removed it -
        /// since the ball's collider is a trigger (no physical collision response from the
        /// ground), it would keep sinking straight through the field for those seconds.
        private void FreezeBall()
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
        }

        private float HorizontalDistanceFeet(Vector3 position)
        {
            Vector3 flatPlate = new Vector3(platePosition.x, 0f, platePosition.z);
            Vector3 flatPos = new Vector3(position.x, 0f, position.z);
            return Vector3.Distance(flatPlate, flatPos) * feetPerUnit;
        }
    }
}

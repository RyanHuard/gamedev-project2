using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HalloweenDerby.Player
{
    /// Reads the "Derby" action map directly from the PlayerControls.inputactions asset at
    /// runtime (no generated C# wrapper - keeps this reliable in a CLI-only build pipeline).
    public class PlayerInputHandler : MonoBehaviour
    {
        [SerializeField] private InputActionAsset actionsAsset;

        private InputAction swingAction;
        private InputAction chargeAction;
        private InputAction pointAction;
        private InputAction pauseAction;

        public event Action SwingPressed;
        public event Action PausePressed;

        public bool ChargeHeld { get; private set; }
        public float ChargeHeldDuration { get; private set; }
        /// Raw mouse screen position (pixels) - consumed by ZoneReticleController to project
        /// onto the strike-zone plane.
        public Vector2 PointScreenPosition { get; private set; }

        private void Awake()
        {
            var map = actionsAsset.FindActionMap("Derby", throwIfNotFound: true);
            swingAction = map.FindAction("Swing", throwIfNotFound: true);
            chargeAction = map.FindAction("Charge", throwIfNotFound: true);
            pointAction = map.FindAction("Point", throwIfNotFound: true);
            pauseAction = map.FindAction("Pause", throwIfNotFound: true);
        }

        private void OnEnable()
        {
            swingAction.Enable();
            chargeAction.Enable();
            pointAction.Enable();
            pauseAction.Enable();
            swingAction.performed += OnSwingPerformed;
            pauseAction.performed += OnPausePerformed;
        }

        private void OnDisable()
        {
            swingAction.performed -= OnSwingPerformed;
            pauseAction.performed -= OnPausePerformed;
            swingAction.Disable();
            chargeAction.Disable();
            pointAction.Disable();
            pauseAction.Disable();
        }

        private void Update()
        {
            PointScreenPosition = pointAction.ReadValue<Vector2>();

            bool chargeHeldNow = chargeAction.IsPressed();
            if (chargeHeldNow && !ChargeHeld)
                ChargeHeldDuration = 0f;
            else if (chargeHeldNow)
                ChargeHeldDuration += Time.deltaTime;
            else
                ChargeHeldDuration = 0f;
            ChargeHeld = chargeHeldNow;
        }

        private void OnSwingPerformed(InputAction.CallbackContext ctx) => SwingPressed?.Invoke();
        private void OnPausePerformed(InputAction.CallbackContext ctx) => PausePressed?.Invoke();
    }
}

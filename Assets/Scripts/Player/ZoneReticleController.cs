using UnityEngine;

namespace HalloweenDerby.Player
{
    /// Projects the mouse cursor onto the strike-zone plane every frame, drives a visible
    /// reticle marker, and exposes the current aim point in zone-normalized coordinates for
    /// SwingController to use at the moment of contact.
    public class ZoneReticleController : MonoBehaviour
    {
        [SerializeField] private PlayerInputHandler input;
        [SerializeField] private StrikeZone zone;
        [SerializeField] private Transform reticleVisual;
        [Tooltip("How far outside the zone (in zone-half-widths) the reticle is allowed to travel, so players can aim at/lay off pitches outside the zone.")]
        [SerializeField] private float maxReach = 1.6f;

        public Vector2 NormalizedPosition { get; private set; }
        public Vector3 WorldPosition { get; private set; }

        private void Update()
        {
            Camera cam = Camera.main;
            if (cam == null || zone == null || input == null) return;

            Plane plane = new Plane(Vector3.forward, zone.WorldCenter);
            Ray ray = cam.ScreenPointToRay(input.PointScreenPosition);
            if (!plane.Raycast(ray, out float distance)) return;

            Vector3 hit = ray.GetPoint(distance);
            Vector2 norm = zone.WorldToNormalized(hit);
            norm.x = Mathf.Clamp(norm.x, -maxReach, maxReach);
            norm.y = Mathf.Clamp(norm.y, -maxReach, maxReach);

            NormalizedPosition = norm;
            WorldPosition = zone.NormalizedToWorld(norm);
            if (reticleVisual != null) reticleVisual.position = WorldPosition;
        }
    }
}

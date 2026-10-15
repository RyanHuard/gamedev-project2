using UnityEngine;

namespace HalloweenDerby.Player
{
    /// Defines the hittable zone over the plate (MLB The Show / Super Mega Baseball style "zone
    /// hitting") and draws its wireframe border. Transform position is the zone's ground anchor
    /// (same X/Z as the plate, Y=0); vertical extent is defined by bottom/top in world units.
    public class StrikeZone : MonoBehaviour
    {
        [SerializeField] private float width = 0.75f;
        [SerializeField] private float bottom = 0.55f;
        [SerializeField] private float top = 1.5f;
        [SerializeField] private float lineWidth = 0.025f;
        [SerializeField] private Color zoneColor = new Color(1f, 1f, 1f, 0.85f);

        public Vector3 WorldCenter => new Vector3(transform.position.x, (bottom + top) * 0.5f, transform.position.z);
        public float HalfWidth => width * 0.5f;
        public float HalfHeight => (top - bottom) * 0.5f;
        public float PlaneZ => transform.position.z;

        private void Awake() => BuildVisual();

        /// Converts a world position (X/Y only - Z is assumed to be on the zone's plane) into
        /// zone-local coordinates where (0,0) is dead center and +/-1 is the zone edge. Values
        /// beyond +/-1 mean "outside the zone" (a ball, not a strike) - not clamped, so callers
        /// can tell how far outside.
        public Vector2 WorldToNormalized(Vector3 worldPos)
        {
            Vector3 c = WorldCenter;
            return new Vector2((worldPos.x - c.x) / HalfWidth, (worldPos.y - c.y) / HalfHeight);
        }

        public Vector3 NormalizedToWorld(Vector2 normalized)
        {
            Vector3 c = WorldCenter;
            return new Vector3(c.x + normalized.x * HalfWidth, c.y + normalized.y * HalfHeight, PlaneZ);
        }

        /// Picks a pitch target within (or, with a wider spread, slightly outside) the zone.
        /// spread=0 is dead center; spread=1 can reach the zone edges; values above 1 can miss
        /// the zone entirely (a "ball" pitch) - rounds dial this up as they get harder.
        public Vector3 GetRandomTargetPoint(float spread)
        {
            Vector2 n = new Vector2(Random.Range(-spread, spread), Random.Range(-spread, spread));
            return NormalizedToWorld(n);
        }

        private void BuildVisual()
        {
            var lr = gameObject.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.positionCount = 4;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.numCapVertices = 2;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = zoneColor };
            lr.material = mat;
            lr.startColor = zoneColor;
            lr.endColor = zoneColor;

            Vector3 c = WorldCenter;
            lr.SetPosition(0, c + new Vector3(-HalfWidth, -HalfHeight, 0f));
            lr.SetPosition(1, c + new Vector3(HalfWidth, -HalfHeight, 0f));
            lr.SetPosition(2, c + new Vector3(HalfWidth, HalfHeight, 0f));
            lr.SetPosition(3, c + new Vector3(-HalfWidth, HalfHeight, 0f));
        }
    }
}

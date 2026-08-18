using UnityEngine;

namespace EvoCreatures.Environment
{
    // Attach to a GameObject with a BoxCollider set to "Is Trigger". The
    // collider's own bounds ARE the water body: top face = surface, bottom
    // face = pool floor. Make sure FloorY is deep enough that limb length
    // can't reach it - that's what makes walking mechanically impossible in
    // deep water, rather than just inconvenient. No friction hacks needed.
    [RequireComponent(typeof(BoxCollider))]
    public class WaterVolume : MonoBehaviour
    {
        private BoxCollider _collider;

        private void Awake()
        {
            _collider = GetComponent<BoxCollider>();
            _collider.isTrigger = true;
        }

        public float SurfaceY => _collider.bounds.max.y;
        public float FloorY => _collider.bounds.min.y;
        public float Depth => SurfaceY - FloorY;

        public bool ContainsXZ(Vector3 worldPos)
        {
            var b = _collider.bounds;
            return worldPos.x >= b.min.x && worldPos.x <= b.max.x &&
                   worldPos.z >= b.min.z && worldPos.z <= b.max.z;
        }

        // 0 = fully above water, 1 = fully submerged, linear across a band of
        // `partRadius` around the surface line (rough stand-in for a small
        // body part crossing the waterline rather than a point).
        public float GetSubmersionFraction(float worldY, float partRadius)
        {
            if (worldY > SurfaceY + partRadius) return 0f;
            if (worldY < SurfaceY - partRadius) return 1f;
            return Mathf.Clamp01((SurfaceY + partRadius - worldY) / (2f * partRadius));
        }
    }
}

using OpenTK.Mathematics;
using Engine.Scene;

namespace Engine.Physics
{
    /// <summary>
    /// Contains information about a raycast hit.
    /// Similar to Unity's RaycastHit structure.
    /// </summary>
    public struct RaycastHit
    {
        /// <summary>The entity that was hit</summary>
        public Entity? Entity { get; set; }

        /// <summary>The collider that was hit</summary>
        public Collider? Collider { get; set; }

        /// <summary>The point in world space where the ray hit the collider</summary>
        public Vector3 Point { get; set; }

        /// <summary>The normal of the surface hit</summary>
        public Vector3 Normal { get; set; }

        /// <summary>The distance from the ray origin to the hit point</summary>
        public float Distance { get; set; }

        public RaycastHit(Entity? entity, Collider? collider, Vector3 point, Vector3 normal, float distance)
        {
            Entity = entity;
            Collider = collider;
            Point = point;
            Normal = normal;
            Distance = distance;
        }
    }
}

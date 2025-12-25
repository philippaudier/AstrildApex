using System;
using OpenTK.Mathematics;
using Engine.Components;
using Engine.Serialization;

namespace Engine.Physics
{
    /// <summary>
    /// Base class for all collider components.
    /// Colliders are shapes used for collision detection and raycasting.
    /// </summary>
    public abstract class Collider : Component
    {
        /// <summary>Collision layer (0-31)</summary>
        [Engine.Serialization.SerializableAttribute("layer")]
        public int Layer = CollisionLayers.Default;

        /// <summary>If true, collider is a trigger (no collision response, just detection)</summary>
        [Engine.Serialization.SerializableAttribute("isTrigger")]
        public bool IsTrigger = false;

        /// <summary>Local-space center offset</summary>
        [Engine.Serialization.SerializableAttribute("center")]
        public Vector3 Center = Vector3.Zero;

        /// <summary>
        /// Get the world-space center of this collider
        /// </summary>
        public Vector3 WorldCenter
        {
            get
            {
                if (Entity == null) return Center;
                Entity.GetWorldTRS(out var pos, out var rot, out _);
                return pos + Vector3.Transform(Center, rot);
            }
        }

        /// <summary>
        /// Get the world-space rotation of this collider
        /// </summary>
        public Quaternion WorldRotation
        {
            get
            {
                if (Entity == null) return Quaternion.Identity;
                Entity.GetWorldTRS(out _, out var rot, out _);
                return rot;
            }
        }

        /// <summary>
        /// Get the world-space scale of this collider
        /// </summary>
        public Vector3 WorldScale
        {
            get
            {
                if (Entity == null) return Vector3.One;
                Entity.GetWorldTRS(out _, out _, out var scale);
                return scale;
            }
        }

        /// <summary>
        /// Auto-register with PhysicsManager when attached
        /// </summary>
        public override void OnAttached()
        {
            base.OnAttached();
            PhysicsManager.Instance.RegisterCollider(this);
        }

        /// <summary>
        /// Auto-unregister from PhysicsManager when detached
        /// </summary>
        public override void OnDetached()
        {
            base.OnDetached();
            PhysicsManager.Instance.UnregisterCollider(this);
        }

        /// <summary>
        /// Test if a ray intersects this collider
        /// </summary>
        /// <param name="origin">Ray origin in world space</param>
        /// <param name="direction">Ray direction (normalized)</param>
        /// <param name="hit">Hit information if collision occurs</param>
        /// <param name="maxDistance">Maximum ray distance</param>
        /// <returns>True if ray hits the collider</returns>
        public abstract bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hit, float maxDistance);

        /// <summary>
        /// Test if a point is inside this collider
        /// </summary>
        /// <param name="point">Point in world space</param>
        /// <returns>True if point is inside</returns>
        public abstract bool ContainsPoint(Vector3 point);

        /// <summary>
        /// Get the closest point on this collider to a given point
        /// </summary>
        /// <param name="point">Point in world space</param>
        /// <returns>Closest point on collider surface</returns>
        public abstract Vector3 ClosestPoint(Vector3 point);
    }
}

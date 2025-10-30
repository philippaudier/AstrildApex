using OpenTK.Mathematics;

namespace Engine.Physics
{
    public struct Bounds // AABB
    {
        public Vector3 Center;
        public Vector3 Extents;

        public Vector3 Min => Center - Extents;
        public Vector3 Max => Center + Extents;

        public static Bounds FromMinMax(Vector3 min, Vector3 max)
        {
            return new Bounds
            {
                Center = (min + max) * 0.5f,
                Extents = (max - min) * 0.5f
            };
        }
    }

    public struct OBB // Oriented Bounding Box
    {
        public Vector3 Center;
        public Vector3 HalfSize; // along local box axes
        public Matrix3 Orientation; // columns are world-space axes (orthonormal)
    }

    public struct Ray
    {
        public Vector3 Origin;
        public Vector3 Direction; // normalized
    }

    public struct RaycastHit
    {
        public Components.Component? Component; // optional component hit (e.g., collider)
        public Components.Component? OtherComponent; // reserved
        public Components.Component? ColliderComponent; // alias for Component
        public Components.Component? RigidbodyComponent; // reserved for future
        public Scene.Entity? Entity;
        public Vector3 Point;
        public Vector3 Normal;
        public float Distance;
    }

    public enum QueryTriggerInteraction { UseGlobal, Include, Ignore }

    public struct Collision // info passed to callbacks
    {
        public Components.Collider ThisCollider;
        public Components.Collider OtherCollider;
        public Scene.Entity ThisEntity => ThisCollider.Entity!;
        public Scene.Entity OtherEntity => OtherCollider.Entity!;
        public Vector3 Normal; // best estimate; may be zero in simple overlap
        public float Penetration; // best estimate; may be zero in simple overlap
    }
}

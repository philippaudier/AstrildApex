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

    // RaycastHit moved to separate file: Physics/RaycastHit.cs

    public enum QueryTriggerInteraction { UseGlobal, Include, Ignore }

    // Collision struct - kept for backward compatibility (empty stub)
    public struct Collision
    {
        public Scene.Entity? ThisEntity;
        public Scene.Entity? OtherEntity;
        public Vector3 Normal;
        public float Penetration;
    }
}

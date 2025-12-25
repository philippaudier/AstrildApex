using System;
using OpenTK.Mathematics;
using Engine.Serialization;

namespace Engine.Physics
{
    /// <summary>
    /// Sphere-shaped collider for collision detection
    /// </summary>
    public sealed class SphereCollider : Collider
    {
        [Engine.Serialization.SerializableAttribute("radius")]
        private float _radius = 0.5f;

        /// <summary>Sphere radius (local space, before scale)</summary>
        public float Radius
        {
            get => _radius;
            set => _radius = MathF.Max(0.001f, value);
        }

        /// <summary>Get the world-space radius (accounting for scale)</summary>
        public float WorldRadius
        {
            get
            {
                var scale = WorldScale;
                float maxScale = MathF.Max(MathF.Max(MathF.Abs(scale.X), MathF.Abs(scale.Y)), MathF.Abs(scale.Z));
                return Radius * maxScale;
            }
        }

        public override bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hit, float maxDistance)
        {
            hit = default;

            Vector3 center = WorldCenter;
            float radius = WorldRadius;

            // Ray-sphere intersection
            Vector3 oc = origin - center;
            float a = Vector3.Dot(direction, direction);
            float b = 2.0f * Vector3.Dot(oc, direction);
            float c = Vector3.Dot(oc, oc) - radius * radius;
            float discriminant = b * b - 4 * a * c;

            if (discriminant < 0)
                return false; // No intersection

            float t = (-b - MathF.Sqrt(discriminant)) / (2.0f * a);

            if (t < 0 || t > maxDistance)
                return false; // Behind ray origin or too far

            Vector3 hitPoint = origin + direction * t;
            Vector3 normal = Vector3.Normalize(hitPoint - center);

            hit = new RaycastHit(Entity, this, hitPoint, normal, t);
            return true;
        }

        public override bool ContainsPoint(Vector3 point)
        {
            Vector3 center = WorldCenter;
            float radius = WorldRadius;
            return (point - center).LengthSquared <= radius * radius;
        }

        public override Vector3 ClosestPoint(Vector3 point)
        {
            Vector3 center = WorldCenter;
            float radius = WorldRadius;
            Vector3 direction = point - center;
            float distance = direction.Length;

            if (distance <= radius)
                return point; // Point is inside

            return center + direction / distance * radius;
        }
    }
}

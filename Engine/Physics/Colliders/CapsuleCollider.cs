using System;
using OpenTK.Mathematics;
using Engine.Serialization;

namespace Engine.Physics
{
    /// <summary>
    /// Capsule-shaped collider (cylinder with hemispherical ends)
    /// </summary>
    public sealed class CapsuleCollider : Collider
    {
        [Engine.Serialization.SerializableAttribute("height")]
        private float _height = 2.0f;

        [Engine.Serialization.SerializableAttribute("radius")]
        private float _radius = 0.5f;

        [Engine.Serialization.SerializableAttribute("direction")]
        private int _direction = 1; // 0=X, 1=Y, 2=Z

        /// <summary>Total height of capsule including caps (local space)</summary>
        public float Height
        {
            get => _height;
            set => _height = MathF.Max(Radius * 2.0f + 0.001f, value);
        }

        /// <summary>Radius of capsule (local space)</summary>
        public float Radius
        {
            get => _radius;
            set
            {
                _radius = MathF.Max(0.001f, value);
                if (_height < _radius * 2.0f)
                    _height = _radius * 2.0f + 0.001f;
            }
        }

        /// <summary>Capsule axis direction: 0=X-axis, 1=Y-axis, 2=Z-axis</summary>
        public int Direction
        {
            get => _direction;
            set => _direction = MathHelper.Clamp(value, 0, 2);
        }

        /// <summary>Get the world-space height (accounting for scale)</summary>
        public float WorldHeight
        {
            get
            {
                var scale = WorldScale;
                float axisScale = Direction switch
                {
                    0 => MathF.Abs(scale.X),
                    1 => MathF.Abs(scale.Y),
                    2 => MathF.Abs(scale.Z),
                    _ => 1.0f
                };
                return Height * axisScale;
            }
        }

        /// <summary>Get the world-space radius (accounting for scale)</summary>
        public float WorldRadius
        {
            get
            {
                var scale = WorldScale;
                float radialScale = Direction switch
                {
                    0 => MathF.Max(MathF.Abs(scale.Y), MathF.Abs(scale.Z)),
                    1 => MathF.Max(MathF.Abs(scale.X), MathF.Abs(scale.Z)),
                    2 => MathF.Max(MathF.Abs(scale.X), MathF.Abs(scale.Y)),
                    _ => 1.0f
                };
                return Radius * radialScale;
            }
        }

        /// <summary>Get the capsule axis in world space</summary>
        private Vector3 GetWorldAxis()
        {
            Vector3 localAxis = Direction switch
            {
                0 => Vector3.UnitX,
                1 => Vector3.UnitY,
                2 => Vector3.UnitZ,
                _ => Vector3.UnitY
            };
            return Vector3.Transform(localAxis, WorldRotation);
        }

        /// <summary>Get the two sphere centers (top and bottom) in world space</summary>
        private void GetSphereCenters(out Vector3 top, out Vector3 bottom)
        {
            Vector3 center = WorldCenter;
            Vector3 axis = GetWorldAxis();
            float halfHeight = (WorldHeight - WorldRadius * 2.0f) * 0.5f;

            top = center + axis * halfHeight;
            bottom = center - axis * halfHeight;
        }

        public override bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hit, float maxDistance)
        {
            hit = default;

            GetSphereCenters(out Vector3 top, out Vector3 bottom);
            float radius = WorldRadius;

            // Ray-capsule intersection (treat as line segment with radius)
            Vector3 capsuleDir = top - bottom;
            float capsuleLength = capsuleDir.Length;

            if (capsuleLength < 1e-6f)
            {
                // Degenerate capsule (sphere)
                return RaySphereIntersection(origin, direction, WorldCenter, radius, out hit, maxDistance);
            }

            capsuleDir /= capsuleLength;

            // Closest point on capsule axis to ray
            Vector3 originToBottom = origin - bottom;
            float rayDotAxis = Vector3.Dot(direction, capsuleDir);
            float originDotAxis = Vector3.Dot(originToBottom, capsuleDir);

            float a = 1.0f - rayDotAxis * rayDotAxis;
            float b = Vector3.Dot(originToBottom, direction) - originDotAxis * rayDotAxis;

            float closestT;
            if (MathF.Abs(a) < 1e-6f)
            {
                // Ray parallel to capsule axis
                closestT = 0.0f;
            }
            else
            {
                closestT = -b / a;
            }

            Vector3 rayPoint = origin + direction * closestT;
            float axisT = Vector3.Dot(rayPoint - bottom, capsuleDir);
            axisT = MathHelper.Clamp(axisT, 0, capsuleLength);

            Vector3 axisPoint = bottom + capsuleDir * axisT;

            // Now do sphere intersection with the appropriate cap/cylinder section
            return RaySphereIntersection(origin, direction, axisPoint, radius, out hit, maxDistance);
        }

        private bool RaySphereIntersection(Vector3 origin, Vector3 direction, Vector3 center, float radius, out RaycastHit hit, float maxDistance)
        {
            hit = default;

            Vector3 oc = origin - center;
            float a = Vector3.Dot(direction, direction);
            float b = 2.0f * Vector3.Dot(oc, direction);
            float c = Vector3.Dot(oc, oc) - radius * radius;
            float discriminant = b * b - 4 * a * c;

            if (discriminant < 0)
                return false;

            float t = (-b - MathF.Sqrt(discriminant)) / (2.0f * a);

            if (t < 0 || t > maxDistance)
                return false;

            Vector3 hitPoint = origin + direction * t;
            Vector3 normal = Vector3.Normalize(hitPoint - center);

            hit = new RaycastHit(Entity, this, hitPoint, normal, t);
            return true;
        }

        public override bool ContainsPoint(Vector3 point)
        {
            GetSphereCenters(out Vector3 top, out Vector3 bottom);
            float radius = WorldRadius;

            // Project point onto capsule axis
            Vector3 capsuleDir = top - bottom;
            float capsuleLength = capsuleDir.Length;

            if (capsuleLength < 1e-6f)
            {
                // Degenerate capsule (sphere)
                return (point - WorldCenter).LengthSquared <= radius * radius;
            }

            capsuleDir /= capsuleLength;

            float t = Vector3.Dot(point - bottom, capsuleDir);
            t = MathHelper.Clamp(t, 0, capsuleLength);

            Vector3 closestPoint = bottom + capsuleDir * t;
            return (point - closestPoint).LengthSquared <= radius * radius;
        }

        public override Vector3 ClosestPoint(Vector3 point)
        {
            GetSphereCenters(out Vector3 top, out Vector3 bottom);
            float radius = WorldRadius;

            // Project point onto capsule axis
            Vector3 capsuleDir = top - bottom;
            float capsuleLength = capsuleDir.Length;

            if (capsuleLength < 1e-6f)
            {
                // Degenerate capsule (sphere)
                Vector3 center = WorldCenter;
                Vector3 direction = point - center;
                float distance = direction.Length;
                return distance <= radius ? point : center + direction / distance * radius;
            }

            capsuleDir /= capsuleLength;

            float t = Vector3.Dot(point - bottom, capsuleDir);
            t = MathHelper.Clamp(t, 0, capsuleLength);

            Vector3 axisPoint = bottom + capsuleDir * t;
            Vector3 toPoint = point - axisPoint;
            float distance2 = toPoint.Length;

            if (distance2 <= radius)
                return point; // Inside

            return axisPoint + toPoint / distance2 * radius;
        }
    }
}

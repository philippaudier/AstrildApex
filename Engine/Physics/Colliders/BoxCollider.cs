using System;
using OpenTK.Mathematics;
using Engine.Serialization;

namespace Engine.Physics
{
    /// <summary>
    /// Box-shaped collider for collision detection
    /// </summary>
    public sealed class BoxCollider : Collider
    {
        [Engine.Serialization.SerializableAttribute("size")]
        private Vector3 _size = Vector3.One;

        /// <summary>Box size (local space, before scale)</summary>
        public Vector3 Size
        {
            get => _size;
            set => _size = new Vector3(
                MathF.Max(0.001f, value.X),
                MathF.Max(0.001f, value.Y),
                MathF.Max(0.001f, value.Z)
            );
        }

        /// <summary>Get the world-space size (accounting for scale)</summary>
        public Vector3 WorldSize
        {
            get
            {
                var scale = WorldScale;
                return new Vector3(
                    Size.X * MathF.Abs(scale.X),
                    Size.Y * MathF.Abs(scale.Y),
                    Size.Z * MathF.Abs(scale.Z)
                );
            }
        }

        public override bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hit, float maxDistance)
        {
            hit = default;

            // Transform ray to local space (AABB test)
            Vector3 center = WorldCenter;
            Quaternion rotation = WorldRotation;
            Vector3 halfSize = WorldSize * 0.5f;

            // Inverse transform
            Quaternion invRot = Quaternion.Invert(rotation);
            Vector3 localOrigin = Vector3.Transform(origin - center, invRot);
            Vector3 localDir = Vector3.Transform(direction, invRot);

            // AABB ray intersection (slab method)
            Vector3 invDir = new Vector3(
                MathF.Abs(localDir.X) > 1e-6f ? 1.0f / localDir.X : float.MaxValue,
                MathF.Abs(localDir.Y) > 1e-6f ? 1.0f / localDir.Y : float.MaxValue,
                MathF.Abs(localDir.Z) > 1e-6f ? 1.0f / localDir.Z : float.MaxValue
            );

            Vector3 t1 = (-halfSize - localOrigin) * invDir;
            Vector3 t2 = (halfSize - localOrigin) * invDir;

            Vector3 tmin = Vector3.ComponentMin(t1, t2);
            Vector3 tmax = Vector3.ComponentMax(t1, t2);

            float tNear = MathF.Max(MathF.Max(tmin.X, tmin.Y), tmin.Z);
            float tFar = MathF.Min(MathF.Min(tmax.X, tmax.Y), tmax.Z);

            if (tNear > tFar || tFar < 0 || tNear > maxDistance)
                return false;

            float t = tNear >= 0 ? tNear : tFar;
            Vector3 localHitPoint = localOrigin + localDir * t;

            // Calculate normal (which face was hit)
            Vector3 localNormal = Vector3.Zero;
            float epsilon = 0.0001f;

            if (MathF.Abs(localHitPoint.X - halfSize.X) < epsilon) localNormal = Vector3.UnitX;
            else if (MathF.Abs(localHitPoint.X + halfSize.X) < epsilon) localNormal = -Vector3.UnitX;
            else if (MathF.Abs(localHitPoint.Y - halfSize.Y) < epsilon) localNormal = Vector3.UnitY;
            else if (MathF.Abs(localHitPoint.Y + halfSize.Y) < epsilon) localNormal = -Vector3.UnitY;
            else if (MathF.Abs(localHitPoint.Z - halfSize.Z) < epsilon) localNormal = Vector3.UnitZ;
            else if (MathF.Abs(localHitPoint.Z + halfSize.Z) < epsilon) localNormal = -Vector3.UnitZ;

            // Transform back to world space
            Vector3 worldHitPoint = center + Vector3.Transform(localHitPoint, rotation);
            Vector3 worldNormal = Vector3.Normalize(Vector3.Transform(localNormal, rotation));

            hit = new RaycastHit(Entity, this, worldHitPoint, worldNormal, t);
            return true;
        }

        public override bool ContainsPoint(Vector3 point)
        {
            // Transform point to local space
            Vector3 center = WorldCenter;
            Quaternion rotation = WorldRotation;
            Vector3 halfSize = WorldSize * 0.5f;

            Quaternion invRot = Quaternion.Invert(rotation);
            Vector3 localPoint = Vector3.Transform(point - center, invRot);

            // AABB contains test
            return MathF.Abs(localPoint.X) <= halfSize.X &&
                   MathF.Abs(localPoint.Y) <= halfSize.Y &&
                   MathF.Abs(localPoint.Z) <= halfSize.Z;
        }

        public override Vector3 ClosestPoint(Vector3 point)
        {
            // Transform to local space
            Vector3 center = WorldCenter;
            Quaternion rotation = WorldRotation;
            Vector3 halfSize = WorldSize * 0.5f;

            Quaternion invRot = Quaternion.Invert(rotation);
            Vector3 localPoint = Vector3.Transform(point - center, invRot);

            // Clamp to box bounds
            Vector3 clamped = new Vector3(
                MathHelper.Clamp(localPoint.X, -halfSize.X, halfSize.X),
                MathHelper.Clamp(localPoint.Y, -halfSize.Y, halfSize.Y),
                MathHelper.Clamp(localPoint.Z, -halfSize.Z, halfSize.Z)
            );

            // Transform back to world space
            return center + Vector3.Transform(clamped, rotation);
        }
    }
}

using System;
using OpenTK.Mathematics;

namespace Engine.Rendering
{
    /// <summary>
    /// Frustum culling utility for efficient visibility testing.
    /// Extracts frustum planes from view-projection matrix and tests points/spheres/AABBs.
    /// </summary>
    public sealed class FrustumCuller
    {
        // Frustum is defined by 6 planes: Near, Far, Left, Right, Top, Bottom
        private Vector4[] _planes = new Vector4[6];

        private const int PlaneNear = 0;
        private const int PlaneFar = 1;
        private const int PlaneLeft = 2;
        private const int PlaneRight = 3;
        private const int PlaneTop = 4;
        private const int PlaneBottom = 5;

        /// <summary>
        /// Extract frustum planes from a view-projection matrix.
        /// Call this once per frame with the current camera's VP matrix.
        /// </summary>
        public void ExtractPlanes(Matrix4 viewProjection)
        {
            // Gribb-Hartmann method for extracting frustum planes from VP matrix
            // Each plane is stored as (A, B, C, D) where Ax + By + Cz + D = 0

            // Left plane
            _planes[PlaneLeft] = new Vector4(
                viewProjection.M14 + viewProjection.M11,
                viewProjection.M24 + viewProjection.M21,
                viewProjection.M34 + viewProjection.M31,
                viewProjection.M44 + viewProjection.M41
            );

            // Right plane
            _planes[PlaneRight] = new Vector4(
                viewProjection.M14 - viewProjection.M11,
                viewProjection.M24 - viewProjection.M21,
                viewProjection.M34 - viewProjection.M31,
                viewProjection.M44 - viewProjection.M41
            );

            // Bottom plane
            _planes[PlaneBottom] = new Vector4(
                viewProjection.M14 + viewProjection.M12,
                viewProjection.M24 + viewProjection.M22,
                viewProjection.M34 + viewProjection.M32,
                viewProjection.M44 + viewProjection.M42
            );

            // Top plane
            _planes[PlaneTop] = new Vector4(
                viewProjection.M14 - viewProjection.M12,
                viewProjection.M24 - viewProjection.M22,
                viewProjection.M34 - viewProjection.M32,
                viewProjection.M44 - viewProjection.M42
            );

            // Near plane
            _planes[PlaneNear] = new Vector4(
                viewProjection.M14 + viewProjection.M13,
                viewProjection.M24 + viewProjection.M23,
                viewProjection.M34 + viewProjection.M33,
                viewProjection.M44 + viewProjection.M43
            );

            // Far plane
            _planes[PlaneFar] = new Vector4(
                viewProjection.M14 - viewProjection.M13,
                viewProjection.M24 - viewProjection.M23,
                viewProjection.M34 - viewProjection.M33,
                viewProjection.M44 - viewProjection.M43
            );

            // Normalize all planes
            for (int i = 0; i < 6; i++)
            {
                float length = new Vector3(_planes[i].X, _planes[i].Y, _planes[i].Z).Length;
                if (length > 0.0001f)
                {
                    _planes[i] /= length;
                }
            }
        }

        /// <summary>
        /// Test if a sphere is inside or intersecting the frustum.
        /// Fast and conservative test - perfect for vegetation instances.
        /// </summary>
        /// <param name="center">Center of the sphere (world space)</param>
        /// <param name="radius">Radius of the sphere</param>
        /// <returns>True if visible (inside or intersecting frustum)</returns>
        public bool TestSphere(Vector3 center, float radius)
        {
            // Test sphere against all 6 planes
            for (int i = 0; i < 6; i++)
            {
                // Calculate signed distance from sphere center to plane
                float distance = _planes[i].X * center.X +
                                _planes[i].Y * center.Y +
                                _planes[i].Z * center.Z +
                                _planes[i].W;

                // If sphere is completely outside any plane, it's not visible
                if (distance < -radius)
                {
                    return false;
                }
            }

            return true; // Sphere is inside or intersecting frustum
        }

        /// <summary>
        /// Test if a point is inside the frustum.
        /// </summary>
        public bool TestPoint(Vector3 point)
        {
            for (int i = 0; i < 6; i++)
            {
                float distance = _planes[i].X * point.X +
                                _planes[i].Y * point.Y +
                                _planes[i].Z * point.Z +
                                _planes[i].W;

                if (distance < 0)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Test if an axis-aligned bounding box (AABB) is visible in the frustum.
        /// </summary>
        public bool TestAABB(Vector3 min, Vector3 max)
        {
            // For each plane, find the "positive vertex" (the corner furthest in the direction of the plane normal)
            for (int i = 0; i < 6; i++)
            {
                Vector3 positiveVertex = new Vector3(
                    _planes[i].X >= 0 ? max.X : min.X,
                    _planes[i].Y >= 0 ? max.Y : min.Y,
                    _planes[i].Z >= 0 ? max.Z : min.Z
                );

                float distance = _planes[i].X * positiveVertex.X +
                                _planes[i].Y * positiveVertex.Y +
                                _planes[i].Z * positiveVertex.Z +
                                _planes[i].W;

                if (distance < 0)
                {
                    return false; // AABB is completely outside this plane
                }
            }

            return true; // AABB is inside or intersecting frustum
        }
    }
}

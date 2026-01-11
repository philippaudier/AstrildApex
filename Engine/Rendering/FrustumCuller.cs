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
        /// IMPORTANT: Pass View * Projection (OpenTK row-major convention).
        /// </summary>
        public void ExtractPlanes(Matrix4 viewProjection)
        {
            // Gribb-Hartmann method for extracting frustum planes from VP matrix
            // Each plane is stored as (A, B, C, D) where Ax + By + Cz + D = 0
            //
            // For OpenTK row-major matrices with View * Projection order,
            // we extract from COLUMNS of the combined VP matrix.

            // Left plane = column4 + column1
            _planes[PlaneLeft] = viewProjection.Column3 + viewProjection.Column0;

            // Right plane = column4 - column1
            _planes[PlaneRight] = viewProjection.Column3 - viewProjection.Column0;

            // Bottom plane = column4 + column2
            _planes[PlaneBottom] = viewProjection.Column3 + viewProjection.Column1;

            // Top plane = column4 - column2
            _planes[PlaneTop] = viewProjection.Column3 - viewProjection.Column1;

            // Near plane = column4 + column3
            _planes[PlaneNear] = viewProjection.Column3 + viewProjection.Column2;

            // Far plane = column4 - column3
            _planes[PlaneFar] = viewProjection.Column3 - viewProjection.Column2;

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
            // Plane normals point INWARD. A point is inside if distance >= 0 for all planes.
            // For a sphere, we allow distance >= -radius (sphere intersecting the plane).
            for (int i = 0; i < 6; i++)
            {
                // Calculate signed distance from sphere center to plane
                // Positive = inside frustum, Negative = outside frustum
                float distance = _planes[i].X * center.X +
                                _planes[i].Y * center.Y +
                                _planes[i].Z * center.Z +
                                _planes[i].W;

                // If sphere is completely outside any plane (entirely on negative side), it's not visible
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

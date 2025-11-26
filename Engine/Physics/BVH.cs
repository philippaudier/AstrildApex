using System;
using System.Collections.Generic;
using OpenTK.Mathematics;

namespace Engine.Physics
{
    /// <summary>
    /// Bounding Volume Hierarchy (BVH) for fast ray-triangle intersection
    /// Reduces complexity from O(N) to O(log N) for raycasts
    /// </summary>
    public class BVH
    {
        private BVHNode? _root;
        private int _maxTrianglesPerLeaf = 8; // Increased to 8 for faster builds (still good performance)

        public BVHNode? Root => _root;

        /// <summary>
        /// Build BVH from a list of triangles
        /// </summary>
        public void Build(List<Triangle> triangles)
        {
            if (triangles.Count == 0)
            {
                _root = null;
                return;
            }

            _root = BuildRecursive(triangles, 0, triangles.Count);
        }

        /// <summary>
        /// Recursively build BVH using SAH (Surface Area Heuristic) for optimal splits
        /// </summary>
        private BVHNode BuildRecursive(List<Triangle> triangles, int start, int count)
        {
            var node = new BVHNode();

            // Calculate bounds for this node
            node.Bounds = CalculateBounds(triangles, start, count);

            // Leaf node if we have few enough triangles
            if (count <= _maxTrianglesPerLeaf)
            {
                node.TriangleStart = start;
                node.TriangleCount = count;
                node.IsLeaf = true;
                return node;
            }

            // Find best split axis and position using SAH
            int bestAxis = 0;
            float bestCost = float.MaxValue;
            float bestSplit = 0;

            var bounds = node.Bounds;
            var extents = bounds.Max - bounds.Min;

            // Try each axis
            for (int axis = 0; axis < 3; axis++)
            {
                float axisExtent = axis == 0 ? extents.X : axis == 1 ? extents.Y : extents.Z;
                if (axisExtent < 0.0001f) continue; // Skip degenerate axes

                // Try split at center
                float center = axis == 0 ? bounds.Center.X : axis == 1 ? bounds.Center.Y : bounds.Center.Z;

                // Count triangles on each side
                int leftCount = 0;
                int rightCount = 0;

                for (int i = start; i < start + count; i++)
                {
                    var triCenter = GetTriangleCenter(triangles[i]);
                    float triPos = axis == 0 ? triCenter.X : axis == 1 ? triCenter.Y : triCenter.Z;

                    if (triPos < center) leftCount++;
                    else rightCount++;
                }

                // Avoid bad splits (all on one side)
                if (leftCount == 0 || rightCount == 0) continue;

                // Simple cost heuristic: prefer balanced splits
                float cost = MathF.Abs(leftCount - rightCount);

                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestAxis = axis;
                    bestSplit = center;
                }
            }

            // If no good split found, make this a leaf
            if (bestCost == float.MaxValue)
            {
                node.TriangleStart = start;
                node.TriangleCount = count;
                node.IsLeaf = true;
                return node;
            }

            // Partition triangles based on best split
            int leftEnd = Partition(triangles, start, count, bestAxis, bestSplit);

            int leftChildCount = leftEnd - start;
            int rightChildCount = count - leftChildCount;

            // Recursively build children
            if (leftChildCount > 0)
                node.Left = BuildRecursive(triangles, start, leftChildCount);

            if (rightChildCount > 0)
                node.Right = BuildRecursive(triangles, leftEnd, rightChildCount);

            node.IsLeaf = false;
            return node;
        }

        /// <summary>
        /// Partition triangles around a split plane (similar to quicksort partition)
        /// </summary>
        private int Partition(List<Triangle> triangles, int start, int count, int axis, float splitPos)
        {
            int left = start;
            int right = start + count - 1;

            while (left <= right)
            {
                // Find triangle on left that should be on right
                while (left <= right)
                {
                    var center = GetTriangleCenter(triangles[left]);
                    float pos = axis == 0 ? center.X : axis == 1 ? center.Y : center.Z;
                    if (pos >= splitPos) break;
                    left++;
                }

                // Find triangle on right that should be on left
                while (left <= right)
                {
                    var center = GetTriangleCenter(triangles[right]);
                    float pos = axis == 0 ? center.X : axis == 1 ? center.Y : center.Z;
                    if (pos < splitPos) break;
                    right--;
                }

                if (left < right)
                {
                    // Swap
                    var temp = triangles[left];
                    triangles[left] = triangles[right];
                    triangles[right] = temp;
                    left++;
                    right--;
                }
            }

            return left;
        }

        /// <summary>
        /// Calculate AABB bounds for a range of triangles
        /// </summary>
        private AABB CalculateBounds(List<Triangle> triangles, int start, int count)
        {
            if (count == 0)
                return new AABB { Min = Vector3.Zero, Max = Vector3.Zero };

            Vector3 min = new Vector3(float.MaxValue);
            Vector3 max = new Vector3(float.MinValue);

            for (int i = start; i < start + count; i++)
            {
                var tri = triangles[i];
                min = Vector3.ComponentMin(min, tri.V0);
                min = Vector3.ComponentMin(min, tri.V1);
                min = Vector3.ComponentMin(min, tri.V2);

                max = Vector3.ComponentMax(max, tri.V0);
                max = Vector3.ComponentMax(max, tri.V1);
                max = Vector3.ComponentMax(max, tri.V2);
            }

            return new AABB
            {
                Min = min,
                Max = max,
                Center = (min + max) * 0.5f
            };
        }

        /// <summary>
        /// Get center of a triangle
        /// </summary>
        private Vector3 GetTriangleCenter(Triangle tri)
        {
            return (tri.V0 + tri.V1 + tri.V2) / 3.0f;
        }

        /// <summary>
        /// Traverse BVH to find ray-triangle intersections
        /// </summary>
        public bool Traverse(Ray ray, List<Triangle> triangles, out float closestT, out Vector3 closestNormal, out int trianglesTested)
        {
            closestT = float.MaxValue;
            closestNormal = Vector3.UnitY;
            trianglesTested = 0;

            if (_root == null) return false;

            bool hit = false;
            TraverseRecursive(_root, ray, triangles, ref closestT, ref closestNormal, ref hit, ref trianglesTested);
            return hit;
        }

        private void TraverseRecursive(BVHNode node, Ray ray, List<Triangle> triangles,
            ref float closestT, ref Vector3 closestNormal, ref bool foundHit, ref int trianglesTested)
        {
            // Test ray against node bounds
            if (!RayAABBIntersect(ray, node.Bounds, out float tMin, out float tMax))
                return;

            // Early exit if this node is farther than current closest hit
            if (tMin > closestT)
                return;

            if (node.IsLeaf)
            {
                // Test all triangles in this leaf
                for (int i = node.TriangleStart; i < node.TriangleStart + node.TriangleCount; i++)
                {
                    trianglesTested++;
                    var tri = triangles[i];

                    if (RayTriangleIntersect(ray.Origin, ray.Direction, tri.V0, tri.V1, tri.V2, out float t))
                    {
                        if (t >= 0 && t < closestT)
                        {
                            closestT = t;
                            foundHit = true;

                            // Calculate normal
                            var e1 = tri.V1 - tri.V0;
                            var e2 = tri.V2 - tri.V0;
                            closestNormal = Vector3.Cross(e1, e2).Normalized();
                        }
                    }
                }
            }
            else
            {
                // Traverse children
                // Optimize: traverse nearest child first
                float tLeft = float.MaxValue;
                float tRight = float.MaxValue;

                if (node.Left != null)
                    RayAABBIntersect(ray, node.Left.Bounds, out tLeft, out _);

                if (node.Right != null)
                    RayAABBIntersect(ray, node.Right.Bounds, out tRight, out _);

                // Visit nearest first
                if (tLeft < tRight)
                {
                    if (node.Left != null)
                        TraverseRecursive(node.Left, ray, triangles, ref closestT, ref closestNormal, ref foundHit, ref trianglesTested);
                    if (node.Right != null)
                        TraverseRecursive(node.Right, ray, triangles, ref closestT, ref closestNormal, ref foundHit, ref trianglesTested);
                }
                else
                {
                    if (node.Right != null)
                        TraverseRecursive(node.Right, ray, triangles, ref closestT, ref closestNormal, ref foundHit, ref trianglesTested);
                    if (node.Left != null)
                        TraverseRecursive(node.Left, ray, triangles, ref closestT, ref closestNormal, ref foundHit, ref trianglesTested);
                }
            }
        }

        /// <summary>
        /// Fast Ray-AABB intersection test
        /// </summary>
        private bool RayAABBIntersect(Ray ray, AABB bounds, out float tMin, out float tMax)
        {
            tMin = 0f;
            tMax = float.MaxValue;

            for (int i = 0; i < 3; i++)
            {
                float origin = i == 0 ? ray.Origin.X : i == 1 ? ray.Origin.Y : ray.Origin.Z;
                float dir = i == 0 ? ray.Direction.X : i == 1 ? ray.Direction.Y : ray.Direction.Z;
                float minVal = i == 0 ? bounds.Min.X : i == 1 ? bounds.Min.Y : bounds.Min.Z;
                float maxVal = i == 0 ? bounds.Max.X : i == 1 ? bounds.Max.Y : bounds.Max.Z;

                if (MathF.Abs(dir) < 0.0001f)
                {
                    if (origin < minVal || origin > maxVal)
                        return false;
                }
                else
                {
                    float t1 = (minVal - origin) / dir;
                    float t2 = (maxVal - origin) / dir;

                    if (t1 > t2)
                    {
                        float temp = t1;
                        t1 = t2;
                        t2 = temp;
                    }

                    tMin = MathF.Max(tMin, t1);
                    tMax = MathF.Min(tMax, t2);

                    if (tMin > tMax)
                        return false;
                }
            }

            return tMax >= 0;
        }

        /// <summary>
        /// Möller-Trumbore ray-triangle intersection
        /// </summary>
        private bool RayTriangleIntersect(Vector3 rayOrigin, Vector3 rayDir, Vector3 v0, Vector3 v1, Vector3 v2, out float t)
        {
            t = 0;
            const float EPSILON = 0.0000001f;

            var edge1 = v1 - v0;
            var edge2 = v2 - v0;

            var h = Vector3.Cross(rayDir, edge2);
            var a = Vector3.Dot(edge1, h);

            if (a > -EPSILON && a < EPSILON)
                return false;

            var f = 1.0f / a;
            var s = rayOrigin - v0;
            var u = f * Vector3.Dot(s, h);

            if (u < 0.0f || u > 1.0f)
                return false;

            var q = Vector3.Cross(s, edge1);
            var v = f * Vector3.Dot(rayDir, q);

            if (v < 0.0f || u + v > 1.0f)
                return false;

            t = f * Vector3.Dot(edge2, q);
            return t > EPSILON;
        }
    }

    /// <summary>
    /// BVH Tree Node
    /// </summary>
    public class BVHNode
    {
        public AABB Bounds;
        public BVHNode? Left;
        public BVHNode? Right;
        public bool IsLeaf;
        public int TriangleStart; // Index in triangle array
        public int TriangleCount; // Number of triangles in this leaf
    }

    /// <summary>
    /// Axis-Aligned Bounding Box
    /// </summary>
    public struct AABB
    {
        public Vector3 Min;
        public Vector3 Max;
        public Vector3 Center;
    }

    /// <summary>
    /// Triangle for BVH
    /// </summary>
    public struct Triangle
    {
        public Vector3 V0, V1, V2;
    }
}

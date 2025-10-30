using System;
using System.Collections.Generic;
using Engine.Components;
using OpenTK.Mathematics;

namespace Engine.Physics
{
    /// <summary>
    /// Unity-like Physics static API wrapping the engine collision system.
    /// </summary>
    public static class Physics
    {
        public const int DefaultRaycastLayers = ~0;

        // --- Raycast (bool) ---
        public static bool Raycast(Vector3 origin, Vector3 direction, float maxDistance = float.MaxValue,
            int layerMask = DefaultRaycastLayers, QueryTriggerInteraction query = QueryTriggerInteraction.UseGlobal)
        {
            if (direction.LengthSquared < 1e-6f) return false; // Invalid direction
            var ray = new Ray { Origin = origin, Direction = Vector3.Normalize(direction) };
            return CollisionSystem.Raycast(ray, maxDistance, out _, layerMask, query);
        }

        public static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hitInfo, float maxDistance = float.MaxValue,
            int layerMask = DefaultRaycastLayers, QueryTriggerInteraction query = QueryTriggerInteraction.UseGlobal)
        {
            hitInfo = default;
            if (direction.LengthSquared < 1e-6f) return false; // Invalid direction
            var ray = new Ray { Origin = origin, Direction = Vector3.Normalize(direction) };
            return CollisionSystem.Raycast(ray, maxDistance, out hitInfo, layerMask, query);
        }

        public static bool Raycast(Ray ray, out RaycastHit hitInfo, float maxDistance = float.MaxValue,
            int layerMask = DefaultRaycastLayers, QueryTriggerInteraction query = QueryTriggerInteraction.UseGlobal)
        {
            hitInfo = default;
            if (ray.Direction.LengthSquared < 1e-6f) return false; // Invalid direction
            var r = ray; r.Direction = Vector3.Normalize(r.Direction);
            return CollisionSystem.Raycast(r, maxDistance, out hitInfo, layerMask, query);
        }

        // --- RaycastAll ---
        public static RaycastHit[] RaycastAll(Vector3 origin, Vector3 direction, float maxDistance = float.MaxValue,
            int layerMask = DefaultRaycastLayers, QueryTriggerInteraction query = QueryTriggerInteraction.UseGlobal)
        {
            var ray = new Ray { Origin = origin, Direction = direction.LengthSquared > 0 ? Vector3.Normalize(direction) : direction };
            var list = CollisionSystem.RaycastAll(ray, maxDistance, layerMask, query);
            return list.ToArray();
        }

        public static RaycastHit[] RaycastAll(Ray ray, float maxDistance = float.MaxValue,
            int layerMask = DefaultRaycastLayers, QueryTriggerInteraction query = QueryTriggerInteraction.UseGlobal)
        {
            var r = ray; r.Direction = r.Direction.LengthSquared > 0 ? Vector3.Normalize(r.Direction) : r.Direction;
            var list = CollisionSystem.RaycastAll(r, maxDistance, layerMask, query);
            return list.ToArray();
        }

        // --- RaycastNonAlloc ---
        public static int RaycastNonAlloc(Vector3 origin, Vector3 direction, RaycastHit[] results, float maxDistance = float.MaxValue,
            int layerMask = DefaultRaycastLayers, QueryTriggerInteraction query = QueryTriggerInteraction.UseGlobal)
        {
            var ray = new Ray { Origin = origin, Direction = direction.LengthSquared > 0 ? Vector3.Normalize(direction) : direction };
            return RaycastNonAlloc(ray, results, maxDistance, layerMask, query);
        }

        public static int RaycastNonAlloc(Ray ray, RaycastHit[] results, float maxDistance = float.MaxValue,
            int layerMask = DefaultRaycastLayers, QueryTriggerInteraction query = QueryTriggerInteraction.UseGlobal)
        {
            if (results == null || results.Length == 0) return 0;
            var r = ray; r.Direction = r.Direction.LengthSquared > 0 ? Vector3.Normalize(r.Direction) : r.Direction;
            var list = CollisionSystem.RaycastAll(r, maxDistance, layerMask, query);
            int n = Math.Min(results.Length, list.Count);
            for (int i = 0; i < n; i++) results[i] = list[i];
            return n;
        }

        // --- Overlap AABB (basic) ---
        public static bool OverlapBox(Vector3 center, Vector3 halfExtents, out List<Collider> results, int layerMask = DefaultRaycastLayers, QueryTriggerInteraction query = QueryTriggerInteraction.UseGlobal)
        {
            return CollisionSystem.OverlapAABB(center - halfExtents, center + halfExtents, out results, layerMask, query);
        }

        public static bool OverlapSphere(Vector3 center, float radius, out List<Collider> results, int layerMask = DefaultRaycastLayers, QueryTriggerInteraction query = QueryTriggerInteraction.UseGlobal)
        {
            // AABB approximation for now
            var r = new Vector3(radius);
            return CollisionSystem.OverlapAABB(center - r, center + r, out results, layerMask, query);
        }

        // --- Simple sphere cast (AABB sweep approximation) ---
        public static bool SphereCast(Ray ray, float radius, out RaycastHit hitInfo, float maxDistance = float.MaxValue, int layerMask = DefaultRaycastLayers, QueryTriggerInteraction query = QueryTriggerInteraction.UseGlobal)
        {
            // Approximate by inflating AABBs by radius and using ray test
            return CollisionSystem.SphereCast(ray, radius, maxDistance, out hitInfo, layerMask, query);
        }
    }
}

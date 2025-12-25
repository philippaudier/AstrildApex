using System;
using System.Collections.Generic;
using System.Linq;
using OpenTK.Mathematics;
using Engine.Core;
using Serilog;

namespace Engine.Physics
{
    /// <summary>
    /// Manages all physics colliders and provides query methods (raycasts, overlaps, etc.)
    /// Singleton that runs in the FixedUpdate phase.
    ///
    /// INSPIRED BY: Unity Physics, Unreal PhysX Manager
    ///
    /// USAGE:
    /// PhysicsManager.Instance.Raycast(origin, direction, out hit, distance, layerMask)
    /// PhysicsManager.Instance.SphereCast(origin, radius, direction, out hit, distance)
    /// PhysicsManager.Instance.OverlapSphere(center, radius, layerMask)
    /// </summary>
    public sealed class PhysicsManager : IUpdateSystem
    {
        private static PhysicsManager? _instance;
        public static PhysicsManager Instance => _instance ??= new PhysicsManager();

        // ===== CONFIGURATION =====
        public const float DefaultMaxRaycastDistance = 1000f;
        public const int AllLayersMask = ~0;

        // ===== COLLIDER REGISTRY =====
        private readonly List<Collider> _allColliders = new();
        private readonly object _colliderLock = new();

        // ===== IUpdateSystem IMPLEMENTATION =====
        public string Name => "PhysicsManager";
        public bool Enabled { get; set; } = true;
        public UpdatePhase Phase => UpdatePhase.FixedUpdate;
        public int Priority => 0;

        private PhysicsManager()
        {
            // Auto-register with update pipeline
            EngineUpdatePipeline.Instance.RegisterSystem(this);
            Log.Information("[PhysicsManager] Initialized and registered with EngineUpdatePipeline");
        }

        public void Update(float deltaTime)
        {
            // Physics manager doesn't need per-frame updates currently
            // Colliders are queried on-demand via Raycast/SphereCast/etc.
            // Future: Add continuous collision detection, trigger events, etc.
        }

        // ===== COLLIDER MANAGEMENT =====

        /// <summary>
        /// Register a collider (called automatically by Collider.OnAttached)
        /// </summary>
        public void RegisterCollider(Collider collider)
        {
            if (collider == null) return;

            lock (_colliderLock)
            {
                if (!_allColliders.Contains(collider))
                {
                    _allColliders.Add(collider);
                }
            }
        }

        /// <summary>
        /// Unregister a collider (called automatically by Collider.OnDetached)
        /// </summary>
        public void UnregisterCollider(Collider collider)
        {
            if (collider == null) return;

            lock (_colliderLock)
            {
                _allColliders.Remove(collider);
            }
        }

        /// <summary>
        /// Get all active colliders (for debugging)
        /// </summary>
        public IReadOnlyList<Collider> GetAllColliders()
        {
            lock (_colliderLock)
            {
                return _allColliders.ToList().AsReadOnly();
            }
        }

        // ===== RAYCASTING =====

        /// <summary>
        /// Cast a ray and return the first hit
        /// </summary>
        /// <param name="origin">Ray origin in world space</param>
        /// <param name="direction">Ray direction (will be normalized)</param>
        /// <param name="hit">Hit information if something was hit</param>
        /// <param name="maxDistance">Maximum ray distance</param>
        /// <param name="layerMask">Layer mask to filter colliders</param>
        /// <returns>True if ray hit something</returns>
        public bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hit, float maxDistance = DefaultMaxRaycastDistance, int layerMask = AllLayersMask)
        {
            hit = default;
            direction = direction.Normalized();

            RaycastHit? closestHit = null;
            float closestDistance = float.MaxValue;

            lock (_colliderLock)
            {
                foreach (var collider in _allColliders)
                {
                    // Skip disabled entities/components
                    if (collider.Entity == null || !collider.Entity.Active || !collider.Enabled)
                        continue;

                    // Layer filtering
                    if (!IsLayerInMask(collider.Layer, layerMask))
                        continue;

                    // Perform raycast
                    if (collider.Raycast(origin, direction, out RaycastHit currentHit, maxDistance))
                    {
                        if (currentHit.Distance < closestDistance)
                        {
                            closestDistance = currentHit.Distance;
                            closestHit = currentHit;
                        }
                    }
                }
            }

            if (closestHit.HasValue)
            {
                hit = closestHit.Value;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Cast a ray and return ALL hits (sorted by distance)
        /// </summary>
        public RaycastHit[] RaycastAll(Vector3 origin, Vector3 direction, float maxDistance = DefaultMaxRaycastDistance, int layerMask = AllLayersMask)
        {
            direction = direction.Normalized();
            var hits = new List<RaycastHit>();

            lock (_colliderLock)
            {
                foreach (var collider in _allColliders)
                {
                    if (collider.Entity == null || !collider.Entity.Active || !collider.Enabled)
                        continue;

                    if (!IsLayerInMask(collider.Layer, layerMask))
                        continue;

                    if (collider.Raycast(origin, direction, out RaycastHit hit, maxDistance))
                    {
                        hits.Add(hit);
                    }
                }
            }

            return hits.OrderBy(h => h.Distance).ToArray();
        }

        // ===== SPHERE CASTING =====

        /// <summary>
        /// Cast a sphere along a direction (like a thick raycast)
        /// </summary>
        /// <param name="origin">Sphere center at start</param>
        /// <param name="radius">Sphere radius</param>
        /// <param name="direction">Cast direction (will be normalized)</param>
        /// <param name="hit">Hit information</param>
        /// <param name="maxDistance">Maximum cast distance</param>
        /// <param name="layerMask">Layer mask filter</param>
        /// <returns>True if sphere hit something</returns>
        public bool SphereCast(Vector3 origin, float radius, Vector3 direction, out RaycastHit hit, float maxDistance = DefaultMaxRaycastDistance, int layerMask = AllLayersMask)
        {
            hit = default;
            direction = direction.Normalized();

            // Sample points along the sweep
            int samples = Math.Max(2, (int)(maxDistance / radius) + 1);
            float step = maxDistance / samples;

            for (int i = 0; i <= samples; i++)
            {
                Vector3 samplePos = origin + direction * (i * step);
                var overlapping = OverlapSphere(samplePos, radius, layerMask);

                if (overlapping.Length > 0)
                {
                    // Found collision - calculate hit info
                    var collider = overlapping[0];
                    Vector3 closestPoint = collider.ClosestPoint(samplePos);
                    Vector3 normal = Vector3.Normalize(samplePos - closestPoint);
                    float distance = i * step;

                    hit = new RaycastHit(collider.Entity, collider, closestPoint, normal, distance);
                    return true;
                }
            }

            return false;
        }

        // ===== OVERLAP QUERIES =====

        /// <summary>
        /// Find all colliders overlapping a sphere
        /// </summary>
        /// <param name="center">Sphere center in world space</param>
        /// <param name="radius">Sphere radius</param>
        /// <param name="layerMask">Layer mask filter</param>
        /// <returns>Array of overlapping colliders</returns>
        public Collider[] OverlapSphere(Vector3 center, float radius, int layerMask = AllLayersMask)
        {
            var results = new List<Collider>();

            lock (_colliderLock)
            {
                foreach (var collider in _allColliders)
                {
                    if (collider.Entity == null || !collider.Entity.Active || !collider.Enabled)
                        continue;

                    if (!IsLayerInMask(collider.Layer, layerMask))
                        continue;

                    // Simple distance check to collider center
                    Vector3 closestPoint = collider.ClosestPoint(center);
                    if ((closestPoint - center).LengthSquared <= radius * radius)
                    {
                        results.Add(collider);
                    }
                }
            }

            return results.ToArray();
        }

        /// <summary>
        /// Check if a point is inside any collider
        /// </summary>
        public bool CheckPoint(Vector3 point, int layerMask = AllLayersMask)
        {
            lock (_colliderLock)
            {
                foreach (var collider in _allColliders)
                {
                    if (collider.Entity == null || !collider.Entity.Active || !collider.Enabled)
                        continue;

                    if (!IsLayerInMask(collider.Layer, layerMask))
                        continue;

                    if (collider.ContainsPoint(point))
                        return true;
                }
            }

            return false;
        }

        // ===== UTILITIES =====

        /// <summary>
        /// Check if a layer is included in a layer mask
        /// </summary>
        private static bool IsLayerInMask(int layer, int mask)
        {
            if (layer < 0 || layer >= 32) return false;
            return (mask & (1 << layer)) != 0;
        }

        /// <summary>
        /// Reset physics manager (for Play Mode transitions)
        /// </summary>
        public void Reset()
        {
            lock (_colliderLock)
            {
                _allColliders.Clear();
            }
            Log.Information("[PhysicsManager] Reset complete");
        }

        /// <summary>
        /// Get debug info
        /// </summary>
        public string GetDebugInfo()
        {
            lock (_colliderLock)
            {
                return $"PhysicsManager: {_allColliders.Count} colliders registered";
            }
        }
    }
}

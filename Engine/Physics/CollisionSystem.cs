using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Engine.Components;
using OpenTK.Mathematics;

namespace Engine.Physics
{
    public static class CollisionSystem
    {
        private static readonly HashSet<Collider> _colliders = new();
        private static readonly HashSet<(Collider, Collider)> _currentPairs = new();
        private static readonly HashSet<(Collider, Collider)> _previousPairs = new();
        private static readonly HashSet<Collider> _dirty = new();
        
        // Spatial hash for efficient broadphase (replaces O(N²))
        private static readonly SpatialHash _spatialHash = new SpatialHash(cellSize: 5f);
        
        // Sleep/wake system for static colliders
        private static readonly HashSet<Collider> _sleepingColliders = new();
        private static readonly HashSet<Collider> _staticColliders = new();

        // Global setting
        public static QueryTriggerInteraction QueriesHitTriggers = QueryTriggerInteraction.Include;

        public static void Register(Collider c)
        {
            _colliders.Add(c);
            _spatialHash.Insert(c);
            _dirty.Add(c);
            
            // Mark as static if entity is static
            if (c.Entity != null && !c.Entity.Transform.HasDynamicMovement)
            {
                _staticColliders.Add(c);
            }
        }

        public static void Unregister(Collider c)
        {
            _colliders.Remove(c);
            _spatialHash.Remove(c);
            _dirty.Remove(c);
            _staticColliders.Remove(c);
            _sleepingColliders.Remove(c);
        }

        public static void MarkDirty(Collider c)
        {
            _dirty.Add(c);
            
            // Wake up if sleeping
            if (_sleepingColliders.Contains(c))
            {
                _sleepingColliders.Remove(c);
            }
        }

        public static void Step(float dt)
        {
            // Update bounds for dirty colliders and update spatial hash
            foreach (var c in _dirty)
            {
                c.UpdateWorldBounds();
                _spatialHash.Update(c);
            }
            _dirty.Clear();

            // Swap pair sets
            _previousPairs.Clear();
            foreach (var p in _currentPairs) _previousPairs.Add(p);
            _currentPairs.Clear();

            // Use spatial hash for broadphase (O(N) instead of O(N²))
            var potentialPairs = new HashSet<(Collider, Collider)>();
            _spatialHash.QueryPairs(potentialPairs);

            // Narrow-phase: test actual AABB overlap and generate collision events
            foreach (var (a, b) in potentialPairs)
            {
                if (!a.Enabled || a.Entity == null) continue;
                if (!b.Enabled || b.Entity == null) continue;
                
                // Skip if both are sleeping static colliders
                if (_sleepingColliders.Contains(a) && _sleepingColliders.Contains(b))
                    continue;

                if (!AABBOverlap(a.WorldAABB, b.WorldAABB)) continue;

                _currentPairs.Add((a, b));

                bool wasOverlapping = _previousPairs.Contains((a, b));
                bool isTriggerPair = a.IsTrigger || b.IsTrigger;

                var col = new Collision { ThisCollider = a, OtherCollider = b, Normal = Vector3.Zero, Penetration = 0f };
                
                // Compute penetration depth for better collision info
                if (!isTriggerPair && CollisionDetection.TestAABBAABB(a.WorldAABB, b.WorldAABB, out var normal, out var penetration))
                {
                    col.Normal = normal;
                    col.Penetration = penetration;
                }
                
                if (wasOverlapping)
                {
                    // Stay
                    if (isTriggerPair) { a.Entity.OnEachComponent(c => c.OnTriggerStay(col)); b.Entity.OnEachComponent(c => c.OnTriggerStay(Flip(col))); }
                    else { a.Entity.OnEachComponent(c => c.OnCollisionStay(col)); b.Entity.OnEachComponent(c => c.OnCollisionStay(Flip(col))); }
                }
                else
                {
                    // Enter - wake up sleeping colliders
                    if (_sleepingColliders.Contains(a)) _sleepingColliders.Remove(a);
                    if (_sleepingColliders.Contains(b)) _sleepingColliders.Remove(b);
                    
                    if (isTriggerPair) { a.Entity.OnEachComponent(c => c.OnTriggerEnter(col)); b.Entity.OnEachComponent(c => c.OnTriggerEnter(Flip(col))); }
                    else { a.Entity.OnEachComponent(c => c.OnCollisionEnter(col)); b.Entity.OnEachComponent(c => c.OnCollisionEnter(Flip(col))); }
                }
            }

            // Exits
            foreach (var p in _previousPairs)
            {
                if (_currentPairs.Contains(p)) continue;
                var (a, b) = p;
                if (a == null || b == null) continue;
                bool isTriggerPair = a.IsTrigger || b.IsTrigger;
                var col = new Collision { ThisCollider = a, OtherCollider = b, Normal = Vector3.Zero, Penetration = 0f };
                if (isTriggerPair) 
                { 
                    a.Entity?.OnEachComponent(c => c.OnTriggerExit(col)); 
                    b.Entity?.OnEachComponent(c => c.OnTriggerExit(Flip(col))); 
                }
                else 
                { 
                    a.Entity?.OnEachComponent(c => c.OnCollisionExit(col)); 
                    b.Entity?.OnEachComponent(c => c.OnCollisionExit(Flip(col))); 
                }
            }
            
            // Put static colliders to sleep if inactive for a while
            // (Simplified: just mark all static colliders that haven't moved as sleeping)
            foreach (var c in _staticColliders)
            {
                if (!_dirty.Contains(c) && c.Enabled)
                {
                    _sleepingColliders.Add(c);
                }
            }
        }

        // --- Queries ---
        public static bool Raycast(Ray ray, float maxDistance, out RaycastHit hit, int layerMask = ~0, QueryTriggerInteraction qti = QueryTriggerInteraction.UseGlobal)
        {
            hit = default;
            bool includeTriggers = qti == QueryTriggerInteraction.Include || (qti == QueryTriggerInteraction.UseGlobal && QueriesHitTriggers == QueryTriggerInteraction.Include);
            float best = float.MaxValue;
            Collider? bestCol = null;
            Vector3 bestPoint = default, bestNormal = default;

            // Use spatial hash to query only relevant colliders
            var rayEnd = ray.Origin + ray.Direction * maxDistance;
            var queryColliders = new HashSet<Collider>();
            _spatialHash.QueryAABB(
                Vector3.ComponentMin(ray.Origin, rayEnd) - new Vector3(0.1f),
                Vector3.ComponentMax(ray.Origin, rayEnd) + new Vector3(0.1f),
                queryColliders
            );

            foreach (var c in queryColliders)
            {
                if (!c.Enabled || c.Entity == null) continue;
                if (((1 << c.Layer) & layerMask) == 0) continue;
                if (!includeTriggers && c.IsTrigger) continue;

                // Ray vs AABB test (approximation for now)
                if (RayAABB(ray.Origin, ray.Direction, c.WorldAABB.Min, c.WorldAABB.Max, out float tmin, out var normal))
                {
                    if (tmin >= 0 && tmin <= maxDistance)
                    {
                        // Try narrow-phase if collider supports it
                        if (c.Raycast(ray, out var narrowHit))
                        {
                            if (narrowHit.Distance < best)
                            {
                                best = narrowHit.Distance; bestCol = c; bestNormal = narrowHit.Normal; bestPoint = narrowHit.Point;
                            }
                        }
                        else
                        {
                            // Do NOT accept AABB-only fallback for primitive colliders that have dedicated Raycast implementations,
                            // because their Raycast may miss (sampling) and an AABB hit would produce a false flat surface.
                            // Only accept AABB fallback for complex/mesh colliders that lack precise narrow-phase support.
                            bool hasPreciseRaycast = (c is Engine.Components.SphereCollider) || (c is Engine.Components.CapsuleCollider) || (c is Engine.Components.BoxCollider);
                            if (!hasPreciseRaycast && tmin < best)
                            {
                                best = tmin; bestCol = c; bestNormal = normal; bestPoint = ray.Origin + ray.Direction * tmin;
                            }
                        }
                    }
                }
            }

            if (bestCol != null)
            {
                hit = new RaycastHit { ColliderComponent = bestCol, Component = bestCol, Entity = bestCol.Entity, Distance = best, Point = bestPoint, Normal = bestNormal };
                return true;
            }
            return false;
        }

        public static List<RaycastHit> RaycastAll(Ray ray, float maxDistance, int layerMask = ~0, QueryTriggerInteraction qti = QueryTriggerInteraction.UseGlobal)
        {
            bool includeTriggers = qti == QueryTriggerInteraction.Include || (qti == QueryTriggerInteraction.UseGlobal && QueriesHitTriggers == QueryTriggerInteraction.Include);
            var results = new List<RaycastHit>(8);

            foreach (var c in _colliders)
            {
                if (!c.Enabled || c.Entity == null) continue;
                if (((1 << c.Layer) & layerMask) == 0) continue;
                if (!includeTriggers && c.IsTrigger) continue;

                if (RayAABB(ray.Origin, ray.Direction, c.WorldAABB.Min, c.WorldAABB.Max, out float tmin, out var normal))
                {
                    if (tmin >= 0 && tmin <= maxDistance)
                    {
                        if (c.Raycast(ray, out var narrowHit)) results.Add(narrowHit);
                        else
                        {
                            // Only add AABB fallback for colliders that don't have a precise Raycast
                            bool hasPreciseRaycast = (c is Engine.Components.SphereCollider) || (c is Engine.Components.CapsuleCollider) || (c is Engine.Components.BoxCollider);
                            if (!hasPreciseRaycast)
                            {
                                results.Add(new RaycastHit { ColliderComponent = c, Component = c, Entity = c.Entity, Distance = tmin, Point = ray.Origin + ray.Direction * tmin, Normal = normal });
                            }
                        }
                    }
                }
            }

            results.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            return results;
        }

        // --- Overlaps ---
        public static bool OverlapAABB(in Vector3 bmin, in Vector3 bmax, out List<Engine.Components.Collider> results, int layerMask = ~0, QueryTriggerInteraction qti = QueryTriggerInteraction.UseGlobal)
        {
            bool includeTriggers = qti == QueryTriggerInteraction.Include || (qti == QueryTriggerInteraction.UseGlobal && QueriesHitTriggers == QueryTriggerInteraction.Include);
            results = new List<Engine.Components.Collider>(8);
            
            // Use spatial hash to query relevant colliders
            var queryColliders = new HashSet<Collider>();
            _spatialHash.QueryAABB(bmin, bmax, queryColliders);
            
            foreach (var c in queryColliders)
            {
                if (!c.Enabled || c.Entity == null) continue;
                if (((1 << c.Layer) & layerMask) == 0) continue;
                if (!includeTriggers && c.IsTrigger) continue;
                if (AABBOverlap(c.WorldAABB, new Bounds { Center = (bmin + bmax) * 0.5f, Extents = (bmax - bmin) * 0.5f }))
                {
                    results.Add(c);
                }
            }
            return results.Count > 0;
        }

        /// <summary>
        /// Check if a capsule overlaps any colliders.
        /// </summary>
        public static bool OverlapCapsule(Vector3 point1, Vector3 point2, float radius, out List<Engine.Components.Collider> results, int layerMask = ~0, QueryTriggerInteraction qti = QueryTriggerInteraction.UseGlobal)
        {
            bool includeTriggers = qti == QueryTriggerInteraction.Include || (qti == QueryTriggerInteraction.UseGlobal && QueriesHitTriggers == QueryTriggerInteraction.Include);
            results = new List<Engine.Components.Collider>(8);

            // Calculate capsule AABB for spatial query
            var capsuleMin = Vector3.ComponentMin(point1, point2) - new Vector3(radius);
            var capsuleMax = Vector3.ComponentMax(point1, point2) + new Vector3(radius);

            // Use spatial hash
            var queryColliders = new HashSet<Collider>();
            _spatialHash.QueryAABB(capsuleMin, capsuleMax, queryColliders);

            foreach (var c in queryColliders)
            {
                if (!c.Enabled || c.Entity == null) continue;
                if (((1 << c.Layer) & layerMask) == 0) continue;
                if (!includeTriggers && c.IsTrigger) continue;

                bool overlaps = false;

                // Use appropriate collision test based on collider type
                if (c is Engine.Components.SphereCollider sphereCol && sphereCol.Entity != null)
                {
                    sphereCol.Entity.GetWorldTRS(out var wpos, out var wrot, out var wscl);
                    var sphereCenter = wpos + Vector3.Transform(sphereCol.Center * wscl, wrot);
                    float sphereRadius = sphereCol.Radius * MathF.Max(MathF.Max(MathF.Abs(wscl.X), MathF.Abs(wscl.Y)), MathF.Abs(wscl.Z));
                    overlaps = CollisionDetection.TestCapsuleSphere(point1, point2, radius, sphereCenter, sphereRadius, out _, out _, out _);
                }
                else if (c is Engine.Components.CapsuleCollider capsuleCol && capsuleCol.Entity != null)
                {
                    capsuleCol.Entity.GetWorldTRS(out var wpos, out var wrot, out var wscl);
                    var center = wpos + Vector3.Transform(capsuleCol.Center * wscl, wrot);
                    float capRadius = capsuleCol.Radius * MathF.Max(MathF.Max(MathF.Abs(wscl.X), MathF.Abs(wscl.Y)), MathF.Abs(wscl.Z));
                    float axisScale = capsuleCol.Direction switch { 0 => MathF.Abs(wscl.X), 1 => MathF.Abs(wscl.Y), 2 => MathF.Abs(wscl.Z), _ => MathF.Abs(wscl.Y) };
                    float height = MathF.Max(capsuleCol.Height * axisScale, 2f * capRadius);
                    float halfH = (height * 0.5f) - capRadius;
                    Vector3 axis = capsuleCol.Direction switch
                    {
                        0 => Vector3.Transform(Vector3.UnitX, wrot),
                        1 => Vector3.Transform(Vector3.UnitY, wrot),
                        2 => Vector3.Transform(Vector3.UnitZ, wrot),
                        _ => Vector3.Transform(Vector3.UnitY, wrot)
                    };
                    var p1 = center - axis * halfH;
                    var p2 = center + axis * halfH;
                    overlaps = CollisionDetection.TestCapsuleCapsule(point1, point2, radius, p1, p2, capRadius, out _, out _, out _);
                }
                else
                {
                    // Fallback to AABB for other types
                    overlaps = CollisionDetection.TestCapsuleAABB(point1, point2, radius, c.WorldAABB, out _, out _, out _);
                }

                if (overlaps)
                {
                    results.Add(c);
                }
            }

            return results.Count > 0;
        }

        // --- Sphere cast (AABB-inflated ray) ---
        public static bool SphereCast(Ray ray, float radius, float maxDistance, out RaycastHit hit, int layerMask = ~0, QueryTriggerInteraction qti = QueryTriggerInteraction.UseGlobal)
        {
            // Inflate each collider AABB by radius and test ray vs inflated AABB
            hit = default;
            bool includeTriggers = qti == QueryTriggerInteraction.Include || (qti == QueryTriggerInteraction.UseGlobal && QueriesHitTriggers == QueryTriggerInteraction.Include);
            float best = float.MaxValue; Engine.Components.Collider? bestCol = null; Vector3 bestPoint = default, bestNormal = default;
            foreach (var c in _colliders)
            {
                if (!c.Enabled || c.Entity == null) continue;
                if (((1 << c.Layer) & layerMask) == 0) continue;
                if (!includeTriggers && c.IsTrigger) continue;

                var aabb = c.WorldAABB;
                var min = aabb.Min - new Vector3(radius);
                var max = aabb.Max + new Vector3(radius);
                if (RayAABB(ray.Origin, ray.Direction, min, max, out float tmin, out var normal))
                {
                    if (tmin >= 0 && tmin <= maxDistance && tmin < best)
                    {
                        best = tmin; bestCol = c; bestNormal = normal; bestPoint = ray.Origin + ray.Direction * tmin;
                    }
                }
            }
            if (bestCol != null)
            {
                hit = new RaycastHit { ColliderComponent = bestCol, Component = bestCol, Entity = bestCol.Entity, Distance = best, Point = bestPoint, Normal = bestNormal };
                return true;
            }
            return false;
        }

        // --- Capsule cast (sweep a capsule along a direction) ---
        /// <summary>
        /// Casts a capsule along a ray direction (swept collision detection).
        /// The capsule is defined by two sphere centers (point1 and point2) and a radius.
        /// Uses proper collision detection based on collider type.
        /// </summary>
        public static bool CapsuleCast(Vector3 point1, Vector3 point2, float radius, Vector3 direction, float maxDistance, out RaycastHit hit, int layerMask = ~0, QueryTriggerInteraction qti = QueryTriggerInteraction.UseGlobal)
        {
            hit = default;
            bool includeTriggers = qti == QueryTriggerInteraction.Include || (qti == QueryTriggerInteraction.UseGlobal && QueriesHitTriggers == QueryTriggerInteraction.Include);
            float best = float.MaxValue;
            Engine.Components.Collider? bestCol = null;
            Vector3 bestPoint = default, bestNormal = default;

            var dir = direction.Normalized();

            // Calculate swept AABB for spatial query
            var capsuleMin = Vector3.ComponentMin(point1, point2) - new Vector3(radius);
            var capsuleMax = Vector3.ComponentMax(point1, point2) + new Vector3(radius);
            var endMin = capsuleMin + dir * maxDistance;
            var endMax = capsuleMax + dir * maxDistance;
            var sweptMin = Vector3.ComponentMin(capsuleMin, endMin);
            var sweptMax = Vector3.ComponentMax(capsuleMax, endMax);

            // Use spatial hash for broadphase
            var queryColliders = new HashSet<Collider>();
            _spatialHash.QueryAABB(sweptMin, sweptMax, queryColliders);

            foreach (var c in queryColliders)
            {
                if (!c.Enabled || c.Entity == null) continue;
                if (((1 << c.Layer) & layerMask) == 0) continue;
                if (!includeTriggers && c.IsTrigger) continue;

                // Sweep test: check for collision at multiple points along the path
                // Adaptive sampling: choose number of steps based on distance and capsule radius
                int steps = Math.Min(128, Math.Max(8, (int)(maxDistance / Math.Max(radius * 0.2f, 0.02f))));
                for (int i = 0; i <= steps; i++)
                {
                    float t = (float)i / (float)steps * maxDistance;
                    var testP1 = point1 + dir * t;
                    var testP2 = point2 + dir * t;
                    
                    bool hasHit = false;
                    Vector3 hitNormal = Vector3.UnitY;
                    Vector3 hitPoint = Vector3.Zero;
                    
                    if (c is Engine.Components.SphereCollider sphereCol && sphereCol.Entity != null)
                    {
                        sphereCol.Entity.GetWorldTRS(out var wpos, out var wrot, out var wscl);
                        var sphereCenter = wpos + Vector3.Transform(sphereCol.Center * wscl, wrot);
                        float sphereRadius = sphereCol.Radius * MathF.Max(MathF.Max(MathF.Abs(wscl.X), MathF.Abs(wscl.Y)), MathF.Abs(wscl.Z));
                        hasHit = CollisionDetection.TestCapsuleSphere(testP1, testP2, radius, sphereCenter, sphereRadius, out hitPoint, out hitNormal, out _);
                    }
                    else if (c is Engine.Components.CapsuleCollider capsuleCol && capsuleCol.Entity != null)
                    {
                        capsuleCol.Entity.GetWorldTRS(out var wpos, out var wrot, out var wscl);
                        var center = wpos + Vector3.Transform(capsuleCol.Center * wscl, wrot);
                        float capRadius = capsuleCol.Radius * MathF.Max(MathF.Max(MathF.Abs(wscl.X), MathF.Abs(wscl.Y)), MathF.Abs(wscl.Z));
                        float axisScale = capsuleCol.Direction switch { 0 => MathF.Abs(wscl.X), 1 => MathF.Abs(wscl.Y), 2 => MathF.Abs(wscl.Z), _ => MathF.Abs(wscl.Y) };
                        float height = MathF.Max(capsuleCol.Height * axisScale, 2f * capRadius);
                        float halfH = (height * 0.5f) - capRadius;
                        Vector3 axis = capsuleCol.Direction switch
                        {
                            0 => Vector3.Transform(Vector3.UnitX, wrot),
                            1 => Vector3.Transform(Vector3.UnitY, wrot),
                            2 => Vector3.Transform(Vector3.UnitZ, wrot),
                            _ => Vector3.Transform(Vector3.UnitY, wrot)
                        };
                        var p1 = center - axis * halfH;
                        var p2 = center + axis * halfH;
                        hasHit = CollisionDetection.TestCapsuleCapsule(testP1, testP2, radius, p1, p2, capRadius, out hitPoint, out hitNormal, out _);
                    }
                    else
                    {
                        // Fallback to AABB
                        hasHit = CollisionDetection.TestCapsuleAABB(testP1, testP2, radius, c.WorldAABB, out hitPoint, out hitNormal, out _);
                    }
                    
                    if (hasHit && t < best)
                    {
                        best = t;
                        bestCol = c;
                        bestNormal = hitNormal;
                        bestPoint = hitPoint;
                        break; // Found collision, no need to test further along this path
                    }
                }
            }

            if (bestCol != null)
            {
                hit = new RaycastHit { ColliderComponent = bestCol, Component = bestCol, Entity = bestCol.Entity, Distance = best, Point = bestPoint, Normal = bestNormal };
                return true;
            }
            return false;
        }

        /// <summary>
        /// Casts a capsule and returns all hits along the path.
        /// </summary>
        public static List<RaycastHit> CapsuleCastAll(Vector3 point1, Vector3 point2, float radius, Vector3 direction, float maxDistance, int layerMask = ~0, QueryTriggerInteraction qti = QueryTriggerInteraction.UseGlobal)
        {
            bool includeTriggers = qti == QueryTriggerInteraction.Include || (qti == QueryTriggerInteraction.UseGlobal && QueriesHitTriggers == QueryTriggerInteraction.Include);
            var results = new List<RaycastHit>(8);

            var capsuleCenter = (point1 + point2) * 0.5f;
            var capsuleHalfHeight = (point2 - point1).Length * 0.5f;
            var ray = new Ray { Origin = capsuleCenter, Direction = direction.Normalized() };

            foreach (var c in _colliders)
            {
                if (!c.Enabled || c.Entity == null) continue;
                if (((1 << c.Layer) & layerMask) == 0) continue;
                if (!includeTriggers && c.IsTrigger) continue;

                // Inflate AABB by capsule radius + half height
                var aabb = c.WorldAABB;
                float inflation = radius + capsuleHalfHeight;
                var min = aabb.Min - new Vector3(inflation);
                var max = aabb.Max + new Vector3(inflation);

                if (RayAABB(ray.Origin, ray.Direction, min, max, out float tmin, out var normal))
                {
                    if (tmin >= 0 && tmin <= maxDistance)
                    {
                        // Attempt a more precise capsule-vs-collider test similar to CapsuleCast
                        bool hasHit = false;
                        float hitT = tmin;
                        Vector3 hitNormal = normal;

                        // Sweep test: check for collision at multiple points along the path
                                int steps = Math.Min(128, Math.Max(6, (int)(maxDistance / Math.Max(radius * 0.5f, 0.02f))));
                                for (int i = 0; i <= steps; i++)
                                {
                                    float tt = (float)i / (float)steps * maxDistance;
                                    var testP1 = (point1) + ray.Direction.Normalized() * tt; // origin was capsule center in this method
                                    var testP2 = (point2) + ray.Direction.Normalized() * tt;

                            Vector3 localHitPoint = Vector3.Zero;
                            Vector3 localHitNormal = Vector3.UnitY;

                            if (c is Engine.Components.SphereCollider sphereCol && sphereCol.Entity != null)
                            {
                                sphereCol.Entity.GetWorldTRS(out var wpos, out var wrot, out var wscl);
                                var sphereCenter = wpos + Vector3.Transform(sphereCol.Center * wscl, wrot);
                                float sphereRadius = sphereCol.Radius * MathF.Max(MathF.Max(MathF.Abs(wscl.X), MathF.Abs(wscl.Y)), MathF.Abs(wscl.Z));
                                hasHit = CollisionDetection.TestCapsuleSphere(testP1, testP2, radius, sphereCenter, sphereRadius, out localHitPoint, out localHitNormal, out _);
                            }
                            else if (c is Engine.Components.CapsuleCollider capsuleCol && capsuleCol.Entity != null)
                            {
                                capsuleCol.Entity.GetWorldTRS(out var wpos, out var wrot, out var wscl);
                                var center = wpos + Vector3.Transform(capsuleCol.Center * wscl, wrot);
                                float capRadius = capsuleCol.Radius * MathF.Max(MathF.Max(MathF.Abs(wscl.X), MathF.Abs(wscl.Y)), MathF.Abs(wscl.Z));
                                float axisScale = capsuleCol.Direction switch { 0 => MathF.Abs(wscl.X), 1 => MathF.Abs(wscl.Y), 2 => MathF.Abs(wscl.Z), _ => MathF.Abs(wscl.Y) };
                                float height = MathF.Max(capsuleCol.Height * axisScale, 2f * capRadius);
                                float halfH = (height * 0.5f) - capRadius;
                                Vector3 axis = capsuleCol.Direction switch
                                {
                                    0 => Vector3.Transform(Vector3.UnitX, wrot),
                                    1 => Vector3.Transform(Vector3.UnitY, wrot),
                                    2 => Vector3.Transform(Vector3.UnitZ, wrot),
                                    _ => Vector3.Transform(Vector3.UnitY, wrot)
                                };
                                var p1 = center - axis * halfH;
                                var p2 = center + axis * halfH;
                                hasHit = CollisionDetection.TestCapsuleCapsule(testP1, testP2, radius, p1, p2, capRadius, out localHitPoint, out localHitNormal, out _);
                            }
                            else
                            {
                                hasHit = CollisionDetection.TestCapsuleAABB(testP1, testP2, radius, c.WorldAABB, out localHitPoint, out localHitNormal, out _);
                            }

                            if (hasHit)
                            {
                                hitT = tt;
                                hitNormal = localHitNormal;
                                break;
                            }
                        }

                        // If we found a shape-aware hit, add it; otherwise only add AABB fallback for non-primitive colliders
                        bool hasPreciseSweep = (c is Engine.Components.SphereCollider) || (c is Engine.Components.CapsuleCollider) || (c is Engine.Components.BoxCollider);
                        if (hasHit || !hasPreciseSweep)
                        {
                            results.Add(new RaycastHit
                            {
                                ColliderComponent = c,
                                Component = c,
                                Entity = c.Entity,
                                Distance = hitT,
                                Point = ray.Origin + ray.Direction * hitT,
                                Normal = hitNormal
                            });
                        }
                    }
                }
            }

            results.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            return results;
        }

        // --- Helpers ---
        public static Bounds ComputeAABB(OBB obb)
        {
            // AABB from OBB: center stays, extents = sum of abs(orientation columns) * halfSize
            var absOri = new Matrix3(System.MathF.Abs(obb.Orientation.M11), System.MathF.Abs(obb.Orientation.M12), System.MathF.Abs(obb.Orientation.M13),
                                     System.MathF.Abs(obb.Orientation.M21), System.MathF.Abs(obb.Orientation.M22), System.MathF.Abs(obb.Orientation.M23),
                                     System.MathF.Abs(obb.Orientation.M31), System.MathF.Abs(obb.Orientation.M32), System.MathF.Abs(obb.Orientation.M33));
            var ex = new Vector3(
                absOri.M11 * obb.HalfSize.X + absOri.M12 * obb.HalfSize.Y + absOri.M13 * obb.HalfSize.Z,
                absOri.M21 * obb.HalfSize.X + absOri.M22 * obb.HalfSize.Y + absOri.M23 * obb.HalfSize.Z,
                absOri.M31 * obb.HalfSize.X + absOri.M32 * obb.HalfSize.Y + absOri.M33 * obb.HalfSize.Z
            );
            return new Bounds { Center = obb.Center, Extents = ex };
        }

        private static bool AABBOverlap(Bounds a, Bounds b)
        {
            var amin = a.Min; var amax = a.Max; var bmin = b.Min; var bmax = b.Max;
            return (amin.X <= bmax.X && amax.X >= bmin.X) &&
                   (amin.Y <= bmax.Y && amax.Y >= bmin.Y) &&
                   (amin.Z <= bmax.Z && amax.Z >= bmin.Z);
        }

        private static (Collider, Collider) OrderPair(Collider a, Collider b)
        {
            return RuntimeHelpers.GetHashCode(a) < RuntimeHelpers.GetHashCode(b) ? (a, b) : (b, a);
        }

        private static Collision Flip(Collision c)
        {
            return new Collision { ThisCollider = c.OtherCollider, OtherCollider = c.ThisCollider, Normal = -c.Normal, Penetration = c.Penetration };
        }

        // Ray vs AABB from slab method
        private static bool RayAABB(in Vector3 ro, in Vector3 rd, in Vector3 bmin, in Vector3 bmax, out float tmin, out Vector3 normal)
        {
            tmin = 0f; float tmax = float.MaxValue; normal = Vector3.Zero;
            for (int i = 0; i < 3; i++)
            {
                float o = i == 0 ? ro.X : i == 1 ? ro.Y : ro.Z;
                float d = i == 0 ? rd.X : i == 1 ? rd.Y : rd.Z;
                float min = i == 0 ? bmin.X : i == 1 ? bmin.Y : bmin.Z;
                float max = i == 0 ? bmax.X : i == 1 ? bmax.Y : bmax.Z;
                if (System.MathF.Abs(d) < 1e-8f)
                {
                    if (o < min || o > max) return false;
                }
                else
                {
                    float inv = 1f / d;
                    float t1 = (min - o) * inv;
                    float t2 = (max - o) * inv;
                    float enter = System.MathF.Min(t1, t2);
                    float exit = System.MathF.Max(t1, t2);
                    if (enter > tmin)
                    {
                        tmin = enter;
                        normal = Vector3.Zero;
                        if (i == 0) normal = new Vector3(t1 > t2 ? 1 : -1, 0, 0);
                        if (i == 1) normal = new Vector3(0, t1 > t2 ? 1 : -1, 0);
                        if (i == 2) normal = new Vector3(0, 0, t1 > t2 ? 1 : -1);
                    }
                    tmax = System.MathF.Min(tmax, exit);
                    if (tmin > tmax) return false;
                }
            }
            return true;
        }
    }

    static class EntityExtensions
    {
        public static void OnEachComponent(this Engine.Scene.Entity? e, System.Action<Engine.Components.Component> action)
        {
            if (e == null) return;
            var components = e.GetAllComponents();
            if (components == null) return;
            foreach (var c in components)
            {
                if (c != null) action(c);
            }
        }
    }
}

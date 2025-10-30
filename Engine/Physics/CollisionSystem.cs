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

        // Global setting
        public static QueryTriggerInteraction QueriesHitTriggers = QueryTriggerInteraction.Include;

        public static void Register(Collider c)
        {
            _colliders.Add(c);
            _dirty.Add(c);
        }

        public static void Unregister(Collider c)
        {
            _colliders.Remove(c);
            _dirty.Remove(c);
        }

        public static void MarkDirty(Collider c)
        {
            _dirty.Add(c);
        }

        public static void Step(float dt)
        {
            // Update bounds for dirty colliders
            foreach (var c in _dirty)
                c.UpdateWorldBounds();
            _dirty.Clear();

            // Swap pair sets
            _previousPairs.Clear();
            foreach (var p in _currentPairs) _previousPairs.Add(p);
            _currentPairs.Clear();

            // Very simple N^2 broadphase for now (optimize later)
            var list = new List<Collider>(_colliders);
            for (int i = 0; i < list.Count; i++)
            {
                var a = list[i];
                if (!a.Enabled || a.Entity == null) continue;
                for (int j = i + 1; j < list.Count; j++)
                {
                    var b = list[j];
                    if (!b.Enabled || b.Entity == null) continue;

                    if (!AABBOverlap(a.WorldAABB, b.WorldAABB)) continue;

                    var pair = OrderPair(a, b);
                    _currentPairs.Add(pair);

                    bool wasOverlapping = _previousPairs.Contains(pair);
                    bool isTriggerPair = a.IsTrigger || b.IsTrigger;

                    var col = new Collision { ThisCollider = a, OtherCollider = b, Normal = Vector3.Zero, Penetration = 0f };
                    if (wasOverlapping)
                    {
                        // Stay
                        if (isTriggerPair) { a.Entity.OnEachComponent(c => c.OnTriggerStay(col)); b.Entity.OnEachComponent(c => c.OnTriggerStay(Flip(col))); }
                        else { a.Entity.OnEachComponent(c => c.OnCollisionStay(col)); b.Entity.OnEachComponent(c => c.OnCollisionStay(Flip(col))); }
                    }
                    else
                    {
                        // Enter
                        if (isTriggerPair) { a.Entity.OnEachComponent(c => c.OnTriggerEnter(col)); b.Entity.OnEachComponent(c => c.OnTriggerEnter(Flip(col))); }
                        else { a.Entity.OnEachComponent(c => c.OnCollisionEnter(col)); b.Entity.OnEachComponent(c => c.OnCollisionEnter(Flip(col))); }
                    }
                }
            }

            // Exits
            foreach (var p in _previousPairs)
            {
                if (_currentPairs.Contains(p)) continue;
                var (a, b) = p;
                bool isTriggerPair = a.IsTrigger || b.IsTrigger;
                var col = new Collision { ThisCollider = a, OtherCollider = b, Normal = Vector3.Zero, Penetration = 0f };
                if (isTriggerPair) { a.Entity.OnEachComponent(c => c.OnTriggerExit(col)); b.Entity.OnEachComponent(c => c.OnTriggerExit(Flip(col))); }
                else { a.Entity.OnEachComponent(c => c.OnCollisionExit(col)); b.Entity.OnEachComponent(c => c.OnCollisionExit(Flip(col))); }
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

            foreach (var c in _colliders)
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
                        else if (tmin < best)
                        {
                            best = tmin; bestCol = c; bestNormal = normal; bestPoint = ray.Origin + ray.Direction * tmin;
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
                        else results.Add(new RaycastHit { ColliderComponent = c, Component = c, Entity = c.Entity, Distance = tmin, Point = ray.Origin + ray.Direction * tmin, Normal = normal });
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
            foreach (var c in _colliders)
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
        /// Casts a capsule along a ray direction. The capsule is defined by two sphere centers (point1 and point2) and a radius.
        /// </summary>
        public static bool CapsuleCast(Vector3 point1, Vector3 point2, float radius, Vector3 direction, float maxDistance, out RaycastHit hit, int layerMask = ~0, QueryTriggerInteraction qti = QueryTriggerInteraction.UseGlobal)
        {
            // Simplified: Cast from capsule center with inflated AABB
            hit = default;
            bool includeTriggers = qti == QueryTriggerInteraction.Include || (qti == QueryTriggerInteraction.UseGlobal && QueriesHitTriggers == QueryTriggerInteraction.Include);
            float best = float.MaxValue;
            Engine.Components.Collider? bestCol = null;
            Vector3 bestPoint = default, bestNormal = default;

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
                    if (tmin >= 0 && tmin <= maxDistance && tmin < best)
                    {
                        best = tmin;
                        bestCol = c;
                        bestNormal = normal;
                        bestPoint = ray.Origin + ray.Direction * tmin;
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
                        results.Add(new RaycastHit
                        {
                            ColliderComponent = c,
                            Component = c,
                            Entity = c.Entity,
                            Distance = tmin,
                            Point = ray.Origin + ray.Direction * tmin,
                            Normal = normal
                        });
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

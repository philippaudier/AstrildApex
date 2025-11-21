using OpenTK.Mathematics;
using Engine.Components;

namespace Engine.Physics
{
    /// <summary>
    /// Represents a contact point between two colliders.
    /// </summary>
    public struct ContactPoint
    {
        public Vector3 Point;          // World space contact point
        public Vector3 Normal;         // Contact normal (from A to B)
        public float Penetration;      // Penetration depth
        public Collider ColliderA;     // First collider
        public Collider ColliderB;     // Second collider
    }

    /// <summary>
    /// Contact manifold - collection of contact points between two colliders.
    /// Used for robust collision resolution.
    /// </summary>
    public class ContactManifold
    {
        public Collider ColliderA = null!;
        public Collider ColliderB = null!;
        public readonly ContactPoint[] Points = new ContactPoint[4];
        public int ContactCount = 0;

        public void AddContact(Vector3 point, Vector3 normal, float penetration)
        {
            if (ContactCount < 4)
            {
                Points[ContactCount] = new ContactPoint
                {
                    Point = point,
                    Normal = normal,
                    Penetration = penetration,
                    ColliderA = ColliderA,
                    ColliderB = ColliderB
                };
                ContactCount++;
            }
        }

        public void Clear()
        {
            ContactCount = 0;
        }

        /// <summary>
        /// Get the average contact point (center of mass of all contacts).
        /// </summary>
        public Vector3 GetAveragePoint()
        {
            if (ContactCount == 0) return Vector3.Zero;
            
            Vector3 sum = Vector3.Zero;
            for (int i = 0; i < ContactCount; i++)
                sum += Points[i].Point;
            
            return sum / ContactCount;
        }

        /// <summary>
        /// Get the average penetration depth.
        /// </summary>
        public float GetAveragePenetration()
        {
            if (ContactCount == 0) return 0f;
            
            float sum = 0f;
            for (int i = 0; i < ContactCount; i++)
                sum += Points[i].Penetration;
            
            return sum / ContactCount;
        }

        /// <summary>
        /// Get the deepest contact point.
        /// </summary>
        public ContactPoint GetDeepestContact()
        {
            if (ContactCount == 0) return default;
            
            int deepestIndex = 0;
            float maxPenetration = Points[0].Penetration;
            
            for (int i = 1; i < ContactCount; i++)
            {
                if (Points[i].Penetration > maxPenetration)
                {
                    maxPenetration = Points[i].Penetration;
                    deepestIndex = i;
                }
            }
            
            return Points[deepestIndex];
        }
    }

    /// <summary>
    /// Collision detection utilities for computing contact manifolds.
    /// </summary>
    public static class CollisionDetection
    {
        /// <summary>
        /// Test if two AABBs overlap and compute penetration.
        /// </summary>
        public static bool TestAABBAABB(Bounds a, Bounds b, out Vector3 normal, out float penetration)
        {
            normal = Vector3.Zero;
            penetration = 0f;

            var aMin = a.Min;
            var aMax = a.Max;
            var bMin = b.Min;
            var bMax = b.Max;

            // Check for overlap
            if (aMin.X > bMax.X || aMax.X < bMin.X ||
                aMin.Y > bMax.Y || aMax.Y < bMin.Y ||
                aMin.Z > bMax.Z || aMax.Z < bMin.Z)
                return false;

            // Compute penetration on each axis
            float penetrationX = MathF.Min(aMax.X - bMin.X, bMax.X - aMin.X);
            float penetrationY = MathF.Min(aMax.Y - bMin.Y, bMax.Y - aMin.Y);
            float penetrationZ = MathF.Min(aMax.Z - bMin.Z, bMax.Z - aMin.Z);

            // Find axis of least penetration (MTV)
            if (penetrationX < penetrationY && penetrationX < penetrationZ)
            {
                penetration = penetrationX;
                normal = new Vector3(aMax.X - bMin.X < bMax.X - aMin.X ? 1 : -1, 0, 0);
            }
            else if (penetrationY < penetrationZ)
            {
                penetration = penetrationY;
                normal = new Vector3(0, aMax.Y - bMin.Y < bMax.Y - aMin.Y ? 1 : -1, 0);
            }
            else
            {
                penetration = penetrationZ;
                normal = new Vector3(0, 0, aMax.Z - bMin.Z < bMax.Z - aMin.Z ? 1 : -1);
            }

            return true;
        }

        /// <summary>
        /// Test capsule vs AABB and compute contact.
        /// </summary>
        public static bool TestCapsuleAABB(Vector3 p1, Vector3 p2, float radius, Bounds aabb, 
            out Vector3 contactPoint, out Vector3 normal, out float penetration)
        {
            contactPoint = Vector3.Zero;
            normal = Vector3.UnitY;
            penetration = 0f;

            // Clamp the capsule segment to the AABB
            var closestPoint = ClosestPointOnSegmentToAABB(p1, p2, aabb);
            
            // Find the closest point on AABB to the capsule point
            var aabbClosest = ClosestPointOnAABB(closestPoint, aabb);
            
            // Check if within radius
            var diff = closestPoint - aabbClosest;
            float distSq = diff.LengthSquared;
            
            if (distSq > radius * radius)
                return false;

            float dist = MathF.Sqrt(distSq);
            penetration = radius - dist;
            
            if (dist > 0.0001f)
            {
                normal = diff / dist;
            }
            else
            {
                // Capsule center exactly on AABB surface - use AABB normal
                normal = GetAABBNormal(closestPoint, aabb);
            }
            
            contactPoint = aabbClosest;
            return true;
        }

        /// <summary>
        /// Find the closest point on a line segment to an AABB.
        /// </summary>
        private static Vector3 ClosestPointOnSegmentToAABB(Vector3 p1, Vector3 p2, Bounds aabb)
        {
            var center = aabb.Center;
            var segment = p2 - p1;
            var segmentLength = segment.Length;
            
            if (segmentLength < 0.0001f)
                return p1;
            
            var dir = segment / segmentLength;
            
            // Find t parameter along segment closest to AABB center
            var toCenter = center - p1;
            float t = Vector3.Dot(toCenter, dir);
            t = MathHelper.Clamp(t, 0f, segmentLength);
            
            return p1 + dir * t;
        }

        /// <summary>
        /// Find the closest point on an AABB to a given point.
        /// </summary>
        private static Vector3 ClosestPointOnAABB(Vector3 point, Bounds aabb)
        {
            var min = aabb.Min;
            var max = aabb.Max;
            
            return new Vector3(
                MathHelper.Clamp(point.X, min.X, max.X),
                MathHelper.Clamp(point.Y, min.Y, max.Y),
                MathHelper.Clamp(point.Z, min.Z, max.Z)
            );
        }

        /// <summary>
        /// Get the normal of the AABB surface closest to a point.
        /// </summary>
        private static Vector3 GetAABBNormal(Vector3 point, Bounds aabb)
        {
            var min = aabb.Min;
            var max = aabb.Max;
            
            // Find which face is closest
            float minDist = float.MaxValue;
            Vector3 normal = Vector3.UnitY;
            
            var dists = new[]
            {
                (MathF.Abs(point.X - min.X), new Vector3(-1, 0, 0)),
                (MathF.Abs(point.X - max.X), new Vector3(1, 0, 0)),
                (MathF.Abs(point.Y - min.Y), new Vector3(0, -1, 0)),
                (MathF.Abs(point.Y - max.Y), new Vector3(0, 1, 0)),
                (MathF.Abs(point.Z - min.Z), new Vector3(0, 0, -1)),
                (MathF.Abs(point.Z - max.Z), new Vector3(0, 0, 1))
            };
            
            foreach (var (dist, norm) in dists)
            {
                if (dist < minDist)
                {
                    minDist = dist;
                    normal = norm;
                }
            }
            
            return normal;
        }

        /// <summary>
        /// Test if a sphere overlaps an AABB.
        /// </summary>
        public static bool TestSphereAABB(Vector3 center, float radius, Bounds aabb, 
            out Vector3 contactPoint, out Vector3 normal, out float penetration)
        {
            contactPoint = ClosestPointOnAABB(center, aabb);
            var diff = center - contactPoint;
            float distSq = diff.LengthSquared;
            
            
            if (distSq > radius * radius)
            {
                normal = Vector3.UnitY;
                penetration = 0f;
                return false;
            }
            
            float dist = MathF.Sqrt(distSq);
            penetration = radius - dist;
            
            if (dist > 0.0001f)
            {
                normal = diff / dist;
            }
            else
            {
                normal = GetAABBNormal(center, aabb);
            }
            
            return true;
        }

        /// <summary>
        /// Test capsule vs sphere collision.
        /// </summary>
        public static bool TestCapsuleSphere(Vector3 capsuleP1, Vector3 capsuleP2, float capsuleRadius, 
            Vector3 sphereCenter, float sphereRadius,
            out Vector3 contactPoint, out Vector3 normal, out float penetration)
        {
            contactPoint = Vector3.Zero;
            normal = Vector3.UnitY;
            penetration = 0f;

            // Find closest point on capsule segment to sphere center
            var closestOnCapsule = ClosestPointOnSegment(capsuleP1, capsuleP2, sphereCenter);
            
            // Check distance
            var diff = sphereCenter - closestOnCapsule;
            float distSq = diff.LengthSquared;
            float totalRadius = capsuleRadius + sphereRadius;
            
            if (distSq > totalRadius * totalRadius)
                return false;
            
            float dist = MathF.Sqrt(distSq);
            penetration = totalRadius - dist;
            
            if (dist > 0.0001f)
            {
                normal = diff / dist;
            }
            else
            {
                // Centers coincide - use arbitrary normal
                normal = Vector3.UnitY;
            }
            
            contactPoint = closestOnCapsule + normal * capsuleRadius;
            return true;
        }

        /// <summary>
        /// Test capsule vs capsule collision.
        /// </summary>
        public static bool TestCapsuleCapsule(Vector3 p1A, Vector3 p2A, float radiusA,
            Vector3 p1B, Vector3 p2B, float radiusB,
            out Vector3 contactPoint, out Vector3 normal, out float penetration)
        {
            contactPoint = Vector3.Zero;
            normal = Vector3.UnitY;
            penetration = 0f;

            // Find closest points between the two line segments
            ClosestPointsBetweenSegments(p1A, p2A, p1B, p2B, out var closestA, out var closestB);
            
            var diff = closestB - closestA;
            float distSq = diff.LengthSquared;
            float totalRadius = radiusA + radiusB;
            
            if (distSq > totalRadius * totalRadius)
                return false;
            
            float dist = MathF.Sqrt(distSq);
            penetration = totalRadius - dist;
            
            if (dist > 0.0001f)
            {
                normal = diff / dist;
            }
            else
            {
                // Segments overlap - use arbitrary normal
                normal = Vector3.UnitY;
            }
            
            contactPoint = closestA + normal * radiusA;
            return true;
        }

        /// <summary>
        /// Find closest point on a line segment to a point.
        /// </summary>
        private static Vector3 ClosestPointOnSegment(Vector3 p1, Vector3 p2, Vector3 point)
        {
            var segment = p2 - p1;
            var lengthSq = segment.LengthSquared;
            
            if (lengthSq < 0.0001f)
                return p1;
            
            float t = Vector3.Dot(point - p1, segment) / lengthSq;
            t = MathHelper.Clamp(t, 0f, 1f);
            
            return p1 + segment * t;
        }

        /// <summary>
        /// Find closest points between two line segments.
        /// </summary>
        private static void ClosestPointsBetweenSegments(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2,
            out Vector3 closestOnSeg1, out Vector3 closestOnSeg2)
        {
            var d1 = q1 - p1;
            var d2 = q2 - p2;
            var r = p1 - p2;
            
            float a = Vector3.Dot(d1, d1);
            float e = Vector3.Dot(d2, d2);
            float f = Vector3.Dot(d2, r);
            
            const float epsilon = 0.0001f;
            float s, t;
            
            if (a <= epsilon && e <= epsilon)
            {
                // Both segments are points
                closestOnSeg1 = p1;
                closestOnSeg2 = p2;
                return;
            }
            
            if (a <= epsilon)
            {
                // First segment is a point
                s = 0f;
                t = MathHelper.Clamp(f / e, 0f, 1f);
            }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= epsilon)
                {
                    // Second segment is a point
                    t = 0f;
                    s = MathHelper.Clamp(-c / a, 0f, 1f);
                }
                else
                {
                    // General case
                    float b = Vector3.Dot(d1, d2);
                    float denom = a * e - b * b;
                    
                    if (denom != 0f)
                    {
                        s = MathHelper.Clamp((b * f - c * e) / denom, 0f, 1f);
                    }
                    else
                    {
                        s = 0f;
                    }
                    
                    t = (b * s + f) / e;
                    
                    if (t < 0f)
                    {
                        t = 0f;
                        s = MathHelper.Clamp(-c / a, 0f, 1f);
                    }
                    else if (t > 1f)
                    {
                        t = 1f;
                        s = MathHelper.Clamp((b - c) / a, 0f, 1f);
                    }
                }
            }
            
            closestOnSeg1 = p1 + d1 * s;
            closestOnSeg2 = p2 + d2 * t;
        }
    }
}


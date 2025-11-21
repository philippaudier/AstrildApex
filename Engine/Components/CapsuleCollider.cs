using Engine.Serialization;
using Engine.Scene;
using Engine.Physics;
using OpenTK.Mathematics;

namespace Engine.Components
{
    public sealed class CapsuleCollider : Collider
    {
        // Height is the full height including hemispheres (Unity-like)
    [Engine.Serialization.Serializable("height")] public float Height = 2.0f;
    [Engine.Serialization.Serializable("radius")] public float Radius = 0.5f;
        // Direction 0=X,1=Y,2=Z (match Unity convention); default Y-up
    [Engine.Serialization.Serializable("direction")] public int Direction = 1;

        public override void Update(float deltaTime)
        {
            UpdateWorldBounds();
        }

        public override OBB GetWorldOBB()
        {
            var e = Entity;
            if (e == null)
            {
                return new OBB { Center = Vector3.Zero, HalfSize = Vector3.Zero, Orientation = Matrix3.Identity };
            }

            e.GetWorldTRS(out var wpos, out var wrot, out var wscl);

            var worldCenter = wpos + Vector3.Transform(Center * wscl, wrot);
            float r = Radius * System.MathF.Max(System.MathF.Max(System.MathF.Abs(wscl.X), System.MathF.Abs(wscl.Y)), System.MathF.Abs(wscl.Z));

            // Project height to world scale along direction
            float axisScale = Direction switch { 0 => System.MathF.Abs(wscl.X), 1 => System.MathF.Abs(wscl.Y), 2 => System.MathF.Abs(wscl.Z), _ => System.MathF.Abs(wscl.Y) };
            float h = System.MathF.Max(Height * axisScale, 2f * r); // ensure >= diameter
            // OBB half extents approximate capsule by cylinder bbox
            var half = Direction switch
            {
                0 => new Vector3(h * 0.5f, r, r),
                1 => new Vector3(r, h * 0.5f, r),
                2 => new Vector3(r, r, h * 0.5f),
                _ => new Vector3(r, h * 0.5f, r)
            };

            var ori = Matrix3.CreateFromQuaternion(wrot);
            return new OBB { Center = worldCenter, HalfSize = half, Orientation = ori };
        }

        public override bool Raycast(Engine.Physics.Ray ray, out Engine.Physics.RaycastHit hit)
        {
            hit = default;
            if (Entity == null) return false;
            
            Entity.GetWorldTRS(out var wpos, out var wrot, out var wscl);
            var center = wpos + Vector3.Transform(Center * wscl, wrot);
            float capRadius = Radius * System.MathF.Max(System.MathF.Max(System.MathF.Abs(wscl.X), System.MathF.Abs(wscl.Y)), System.MathF.Abs(wscl.Z));
            
            float axisScale = Direction switch { 0 => System.MathF.Abs(wscl.X), 1 => System.MathF.Abs(wscl.Y), 2 => System.MathF.Abs(wscl.Z), _ => System.MathF.Abs(wscl.Y) };
            float height = System.MathF.Max(Height * axisScale, 2f * capRadius);
            float halfH = (height * 0.5f) - capRadius;
            
            Vector3 axis = Direction switch
            {
                0 => Vector3.Transform(Vector3.UnitX, wrot),
                1 => Vector3.Transform(Vector3.UnitY, wrot),
                2 => Vector3.Transform(Vector3.UnitZ, wrot),
                _ => Vector3.Transform(Vector3.UnitY, wrot)
            };
            
            var p1 = center - axis * halfH;
            var p2 = center + axis * halfH;
            
            // Ray-capsule intersection: find closest point on segment to ray
            // Then check if distance to that point is within radius
            var segDir = p2 - p1;
            var segLength = segDir.Length;
            
            if (segLength < 0.0001f)
            {
                // Degenerate to sphere
                var oc = ray.Origin - p1;
                float a = Vector3.Dot(ray.Direction, ray.Direction);
                float b = 2.0f * Vector3.Dot(oc, ray.Direction);
                float c = Vector3.Dot(oc, oc) - capRadius * capRadius;
                float discriminant = b * b - 4 * a * c;
                
                if (discriminant < 0) return false;
                
                float t = (-b - System.MathF.Sqrt(discriminant)) / (2.0f * a);
                if (t < 0) return false;
                
                var hitPoint = ray.Origin + ray.Direction * t;
                var hitNormal = (hitPoint - p1).Normalized();
                
                hit = new Engine.Physics.RaycastHit
                {
                    ColliderComponent = this,
                    Component = this,
                    Entity = Entity,
                    Distance = t,
                    Point = hitPoint,
                    Normal = hitNormal
                };
                return true;
            }
            
            segDir = segDir.Normalized();
            
            // Find closest approach between ray and capsule segment
            float bestT = float.MaxValue;
            Vector3 bestPoint = Vector3.Zero;
            Vector3 bestNormal = Vector3.UnitY;
            
            // Test caps (two sphere ends)
            for (int i = 0; i < 2; i++)
            {
                var sphereCenter = i == 0 ? p1 : p2;
                var oc = ray.Origin - sphereCenter;
                float a = Vector3.Dot(ray.Direction, ray.Direction);
                float b = 2.0f * Vector3.Dot(oc, ray.Direction);
                float c = Vector3.Dot(oc, oc) - capRadius * capRadius;
                float discriminant = b * b - 4 * a * c;
                
                if (discriminant >= 0)
                {
                    float t = (-b - System.MathF.Sqrt(discriminant)) / (2.0f * a);
                    if (t >= 0 && t < bestT)
                    {
                        bestT = t;
                        bestPoint = ray.Origin + ray.Direction * t;
                        bestNormal = (bestPoint - sphereCenter).Normalized();
                    }
                }
            }
            
            // Test cylinder body (approximate with multiple samples)
            const int samples = 24;
            for (int i = 0; i <= samples; i++)
            {
                float t = (float)i / samples;
                var pointOnSeg = p1 + segDir * (halfH * 2 * t);
                
                var oc = ray.Origin - pointOnSeg;
                float a = Vector3.Dot(ray.Direction, ray.Direction);
                float b = 2.0f * Vector3.Dot(oc, ray.Direction);
                float c = Vector3.Dot(oc, oc) - capRadius * capRadius;
                float discriminant = b * b - 4 * a * c;
                
                if (discriminant >= 0)
                {
                    float rayT = (-b - System.MathF.Sqrt(discriminant)) / (2.0f * a);
                    if (rayT >= 0 && rayT < bestT)
                    {
                        bestT = rayT;
                        bestPoint = ray.Origin + ray.Direction * rayT;
                        bestNormal = (bestPoint - pointOnSeg).Normalized();
                    }
                }
            }
            
            if (bestT < float.MaxValue)
            {
                hit = new Engine.Physics.RaycastHit
                {
                    ColliderComponent = this,
                    Component = this,
                    Entity = Entity,
                    Distance = bestT,
                    Point = bestPoint,
                    Normal = bestNormal
                };
                return true;
            }
            
            return false;
        }
    }
}

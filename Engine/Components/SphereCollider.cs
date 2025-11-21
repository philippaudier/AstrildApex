using Engine.Serialization;
using Engine.Scene;
using Engine.Physics;
using OpenTK.Mathematics;

namespace Engine.Components
{
    public sealed class SphereCollider : Collider
    {
        [Engine.Serialization.Serializable("radius")] public float Radius = 0.5f;

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
            var half = new Vector3(r, r, r);
            return new OBB { Center = worldCenter, HalfSize = half, Orientation = Matrix3.Identity };
        }

        public override bool Raycast(Engine.Physics.Ray ray, out Engine.Physics.RaycastHit hit)
        {
            hit = default;
            if (Entity == null) return false;
            
            Entity.GetWorldTRS(out var wpos, out var wrot, out var wscl);
            var sphereCenter = wpos + Vector3.Transform(Center * wscl, wrot);
            float sphereRadius = Radius * System.MathF.Max(System.MathF.Max(System.MathF.Abs(wscl.X), System.MathF.Abs(wscl.Y)), System.MathF.Abs(wscl.Z));
            
            // Ray-sphere intersection
            var oc = ray.Origin - sphereCenter;
            float a = Vector3.Dot(ray.Direction, ray.Direction);
            float b = 2.0f * Vector3.Dot(oc, ray.Direction);
            float c = Vector3.Dot(oc, oc) - sphereRadius * sphereRadius;
            float discriminant = b * b - 4 * a * c;
            
            if (discriminant < 0)
                return false;
            
            float t = (-b - System.MathF.Sqrt(discriminant)) / (2.0f * a);
            
            if (t < 0)
                return false;
            
            var hitPoint = ray.Origin + ray.Direction * t;
            var hitNormal = (hitPoint - sphereCenter).Normalized();
            
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
    }
}

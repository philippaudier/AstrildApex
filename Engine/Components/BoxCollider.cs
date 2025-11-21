using Engine.Serialization;
using Engine.Scene;
using Engine.Physics;
using OpenTK.Mathematics;

namespace Engine.Components
{
    public sealed class BoxCollider : Collider
    {
    [Engine.Serialization.Serializable("size")] public Vector3 Size = Vector3.One;

        public override void OnAttached()
        {
            base.OnAttached();
            // Ensure bounds are up to date when attached
            UpdateWorldBounds();
        }

        public override void Update(float deltaTime)
        {
            // For kinematic transforms, ensure bounds track entity
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

            // Center in world
            var worldCenter = wpos + Vector3.Transform(Center * wscl, wrot);

            // Half sizes scaled by absolute world scale
            var absScale = new Vector3(System.MathF.Abs(wscl.X), System.MathF.Abs(wscl.Y), System.MathF.Abs(wscl.Z));
            var half = (Size * 0.5f) * absScale;

            // Orientation matrix from rotation
            var ori = Matrix3.CreateFromQuaternion(wrot);

            return new OBB { Center = worldCenter, HalfSize = half, Orientation = ori };
        }

        public override bool Raycast(Engine.Physics.Ray ray, out Engine.Physics.RaycastHit hit)
        {
            hit = default;
            var obb = GetWorldOBB();
            
            // Transform ray to OBB local space
            var invOri = obb.Orientation.Inverted();
            var localOrigin = invOri * (ray.Origin - obb.Center);
            var localDir = invOri * ray.Direction;
            
            // Ray-AABB test in local space
            var min = -obb.HalfSize;
            var max = obb.HalfSize;
            
            float tmin = float.MinValue;
            float tmax = float.MaxValue;
            Vector3 hitNormalLocal = Vector3.Zero;
            
            for (int i = 0; i < 3; i++)
            {
                float origin = i == 0 ? localOrigin.X : (i == 1 ? localOrigin.Y : localOrigin.Z);
                float dir = i == 0 ? localDir.X : (i == 1 ? localDir.Y : localDir.Z);
                float bmin = i == 0 ? min.X : (i == 1 ? min.Y : min.Z);
                float bmax = i == 0 ? max.X : (i == 1 ? max.Y : max.Z);
                
                if (System.MathF.Abs(dir) < 0.0001f)
                {
                    if (origin < bmin || origin > bmax)
                        return false;
                }
                else
                {
                    float t1 = (bmin - origin) / dir;
                    float t2 = (bmax - origin) / dir;
                    
                    if (t1 > t2) { float tmp = t1; t1 = t2; t2 = tmp; }
                    
                    if (t1 > tmin)
                    {
                        tmin = t1;
                        hitNormalLocal = Vector3.Zero;
                        if (i == 0) hitNormalLocal.X = dir > 0 ? -1 : 1;
                        else if (i == 1) hitNormalLocal.Y = dir > 0 ? -1 : 1;
                        else hitNormalLocal.Z = dir > 0 ? -1 : 1;
                    }
                    if (t2 < tmax) tmax = t2;
                    
                    if (tmin > tmax || tmax < 0)
                        return false;
                }
            }
            
            if (tmin < 0)
                return false;
            
            // Transform back to world space
            var hitPoint = ray.Origin + ray.Direction * tmin;
            var hitNormal = obb.Orientation * hitNormalLocal;
            
            hit = new Engine.Physics.RaycastHit
            {
                ColliderComponent = this,
                Component = this,
                Entity = Entity,
                Distance = tmin,
                Point = hitPoint,
                Normal = hitNormal.Normalized()
            };
            
            return true;
        }
    }
}

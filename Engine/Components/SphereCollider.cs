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
    }
}

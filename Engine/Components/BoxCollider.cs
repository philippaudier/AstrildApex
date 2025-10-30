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
    }
}

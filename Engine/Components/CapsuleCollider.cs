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
    }
}

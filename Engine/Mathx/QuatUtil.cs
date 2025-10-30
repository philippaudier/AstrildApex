using System;
using OpenTK.Mathematics;

namespace Engine.Mathx
{
    public static class QuatUtil
    {
        // Build quaternion from Euler XYZ (radians)
        public static Quaternion FromEulerXYZ(Vector3 eulerRad)
        {
            var qx = Quaternion.FromAxisAngle(Vector3.UnitX, eulerRad.X);
            var qy = Quaternion.FromAxisAngle(Vector3.UnitY, eulerRad.Y);
            var qz = Quaternion.FromAxisAngle(Vector3.UnitZ, eulerRad.Z);
            // Order: q = qz * qy * qx  (ZYX intrinsic / XYZ extrinsic)
            return qz * qy * qx;
        }

        // Extract Euler (XYZ order, radians) from quaternion
        public static Vector3 ToEulerXYZ(Quaternion q)
        {
            // normalized
            if (q.LengthSquared != 1f) q = Quaternion.Normalize(q);

            float sx = 2f * (q.W * q.X - q.Y * q.Z);
            sx = Math.Clamp(sx, -1f, 1f);
            float x = MathF.Asin(sx);

            float y = MathF.Atan2(2f * (q.W * q.Y + q.Z * q.X), 1f - 2f * (q.X * q.X + q.Y * q.Y));
            float z = MathF.Atan2(2f * (q.W * q.Z + q.X * q.Y), 1f - 2f * (q.Y * q.Y + q.Z * q.Z));
            return new Vector3(x, y, z);
        }
    }
}

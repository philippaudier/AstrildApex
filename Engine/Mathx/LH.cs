using OpenTK.Mathematics;

namespace Engine.Mathx;

/// <summary>Left-handed helpers: +Y up, +Z forward, +X right.</summary>
public static class LH
{
    public static Matrix4 LookAtLH(Vector3 eye, Vector3 target, Vector3 up)
    {
        var f = (target - eye).Normalized();
        var s = Vector3.Normalize(Vector3.Cross(up, f));   // LH
        var u = Vector3.Cross(f, s);

        var m = new Matrix4(
            new Vector4(s.X, u.X, f.X, 0),
            new Vector4(s.Y, u.Y, f.Y, 0),
            new Vector4(s.Z, u.Z, f.Z, 0),
            new Vector4(-Vector3.Dot(s, eye), -Vector3.Dot(u, eye), -Vector3.Dot(f, eye), 1)
        );
        return m;
    }

    /// <summary>
    /// Right-handed LookAt for OpenGL. +Y up, -Z forward (into screen), +X right.
    /// Use this directly instead of LookAtLH + ZFlip to preserve winding order.
    /// </summary>
    public static Matrix4 LookAtRH(Vector3 eye, Vector3 target, Vector3 up)
    {
        var f = (target - eye).Normalized();
        var s = Vector3.Normalize(Vector3.Cross(f, up));   // RH: reversed cross order
        var u = Vector3.Cross(s, f);

        var m = new Matrix4(
            new Vector4(s.X, u.X, -f.X, 0),  // Note: -f for RH (camera looks down -Z)
            new Vector4(s.Y, u.Y, -f.Y, 0),
            new Vector4(s.Z, u.Z, -f.Z, 0),
            new Vector4(-Vector3.Dot(s, eye), -Vector3.Dot(u, eye), Vector3.Dot(f, eye), 1)
        );
        return m;
    }

    public static Matrix4 PerspectiveLH(float fovyRad, float aspect, float zNear, float zFar)
    {
        // GL expects RH clip; we keep math LH and flip where needed in shaders later.
        float f = 1f / MathF.Tan(fovyRad / 2f);
        var m = Matrix4.Zero;
        m.M11 = f / aspect;
        m.M22 = f;
        m.M33 =  zFar / (zFar - zNear);
        m.M34 = 1f;
        m.M43 = (-zNear * zFar) / (zFar - zNear);
        return m;
    }
}

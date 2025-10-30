using System;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace Editor.Rendering
{
    /// <summary>
    /// Infinite shader-based grid renderer (like Unity)
    /// Uses a full-screen quad with procedural grid in fragment shader
    /// </summary>
    public sealed class GridRenderer : IDisposable
    {
    private int _vao = 0, _vbo = 0, _prog = 0;
    // _locViewProj refers to the inverse view-proj (u_InvViewProj).
    // _locViewProjForward will be the forward view-proj (u_ViewProj) used for depth computation.
    private int _locViewProj = -1, _locViewProjForward = -1, _locCamPos = -1, _locNear = -1, _locFar = -1;

        public GridRenderer() { Init(); }

        // For quick runtime diagnostics we log creation
        ~GridRenderer()
        {
            // Finalizer left empty intentionally; avoid relying on it for deterministic cleanup
        }

        private void Init()
        {
            // Create a full-screen quad (2 triangles covering NDC space)
            float[] quadVertices = {
                // positions (NDC)
                -1.0f, -1.0f, 0.0f,
                 1.0f, -1.0f, 0.0f,
                 1.0f,  1.0f, 0.0f,
                -1.0f, -1.0f, 0.0f,
                 1.0f,  1.0f, 0.0f,
                -1.0f,  1.0f, 0.0f
            };

            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();
            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, quadVertices.Length * sizeof(float), quadVertices, BufferUsageHint.StaticDraw);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);

            // Shader: procedural infinite grid
            const string VS = @"#version 330 core
layout(location=0) in vec3 aPos;
out vec3 vNearPoint;
out vec3 vFarPoint;
uniform mat4 u_InvViewProj;

vec3 UnprojectPoint(float x, float y, float z) {
    vec4 unprojectedPoint = u_InvViewProj * vec4(x, y, z, 1.0);
    return unprojectedPoint.xyz / unprojectedPoint.w;
}

void main() {
    vNearPoint = UnprojectPoint(aPos.x, aPos.y, 0.0); // Near plane
    vFarPoint = UnprojectPoint(aPos.x, aPos.y, 1.0);  // Far plane
    gl_Position = vec4(aPos, 1.0);
}";

            const string FS = @"#version 330 core
            out vec4 outColor;
            in vec3 vNearPoint;
            in vec3 vFarPoint;
            uniform vec3 u_CamPos;
            uniform mat4 u_ViewProj;

vec4 grid(vec3 fragPos3D, float scale) {
    vec2 coord = fragPos3D.xz * scale;
    vec2 derivative = fwidth(coord);
    vec2 grid = abs(fract(coord - 0.5) - 0.5) / derivative;
    float line = min(grid.x, grid.y);
    float minimumz = min(derivative.y, 1.0);
    float minimumx = min(derivative.x, 1.0);
    vec4 color = vec4(0.2, 0.2, 0.2, 1.0 - min(line, 1.0));

    // Z axis (blue)
    if(fragPos3D.x > -0.1 * minimumx && fragPos3D.x < 0.1 * minimumx)
        color = vec4(0.2, 0.2, 1.0, 1.0);
    // X axis (red)
    if(fragPos3D.z > -0.1 * minimumz && fragPos3D.z < 0.1 * minimumz)
        color = vec4(1.0, 0.2, 0.2, 1.0);

    return color;
}

float computeDepth(vec3 pos) {
    vec4 clip_space_pos = u_ViewProj * vec4(pos, 1.0);
    return (clip_space_pos.z / clip_space_pos.w) * 0.5 + 0.5;
}

void main() {
    float t = -vNearPoint.y / (vFarPoint.y - vNearPoint.y);

    // Discard if not hitting the ground plane (y=0) or if behind camera
    if(t < 0.0 || t > 1.0) {
        discard;
    }

    vec3 fragPos3D = vNearPoint + t * (vFarPoint - vNearPoint);

    // Compute proper depth
    gl_FragDepth = computeDepth(fragPos3D);

    float linearDepth = length(fragPos3D - u_CamPos);
    // Increase fading distance so grid remains visible farther away
    float fading = max(0.0, (2000.0 - linearDepth) / 2000.0);

    // Multiple scales for detail at different zoom levels
    vec4 grid1 = grid(fragPos3D, 1.0) * float(t > 0.0 && t <= 1.0); // 1 unit
    vec4 grid10 = grid(fragPos3D, 0.1) * float(t > 0.0 && t <= 1.0); // 10 units

    // Combine grids based on distance
    vec4 finalColor = mix(grid10, grid1, min(linearDepth / 50.0, 1.0));
    finalColor.a *= fading;

    // Avoid hard discard on small alpha - keep a tiny threshold so very distant lines are still visible
    if(finalColor.a < 0.0001)
        discard;

    outColor = finalColor;
}";

            int v = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(v, VS);
            GL.CompileShader(v);
            GL.GetShader(v, ShaderParameter.CompileStatus, out int okv);
            if (okv == 0) throw new Exception("Grid VS: " + GL.GetShaderInfoLog(v));

            int f = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(f, FS);
            GL.CompileShader(f);
            GL.GetShader(f, ShaderParameter.CompileStatus, out int okf);
            if (okf == 0) throw new Exception("Grid FS: " + GL.GetShaderInfoLog(f));

            _prog = GL.CreateProgram();
            GL.AttachShader(_prog, v);
            GL.AttachShader(_prog, f);
            GL.LinkProgram(_prog);
            GL.GetProgram(_prog, GetProgramParameterName.LinkStatus, out int okp);
            if (okp == 0) throw new Exception("Grid Link: " + GL.GetProgramInfoLog(_prog));

            GL.DetachShader(_prog, v);
            GL.DetachShader(_prog, f);
            GL.DeleteShader(v);
            GL.DeleteShader(f);

            _locViewProj = GL.GetUniformLocation(_prog, "u_InvViewProj");
            _locViewProjForward = GL.GetUniformLocation(_prog, "u_ViewProj");
            _locCamPos = GL.GetUniformLocation(_prog, "u_CamPos");
            _locNear = GL.GetUniformLocation(_prog, "u_Near");
            _locFar = GL.GetUniformLocation(_prog, "u_Far");
                // _locDebugAlwaysVisible = GL.GetUniformLocation(_prog, "u_DebugAlwaysVisible");
        }

        public void Render(Matrix4 view, Matrix4 proj, Vector3 camPos, Vector3 target, float fovY, int vw, int vh)
        {
            // Avoid expensive per-frame console logging; use engine debug logger when verbose enabled
            try {
                if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log("[GridRenderer] Render called");
            } catch { }
            // Calculate inverse view-projection matrix
            Matrix4 viewProj = view * proj;
            Matrix4 invViewProj = viewProj.Inverted();

            // Enable blending for grid transparency
            bool blendWas = GL.IsEnabled(EnableCap.Blend);
            bool depthWas = GL.IsEnabled(EnableCap.DepthTest);

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthMask(false); // Don't write to depth buffer

            GL.UseProgram(_prog);
            // If debug uniform is present we force the grid to be visible by disabling depth test
                // bool debugForce = (_locDebugAlwaysVisible != -1);
                // if (debugForce)
                // {
                //     // Disable depth testing so magenta always overwrites the viewport for debugging
                //     GL.Disable(EnableCap.DepthTest);
                //     GL.DepthMask(true);
                // }
            // Upload inverse view-proj for unprojecting full-screen quad in the vertex shader
            GL.UniformMatrix4(_locViewProj, false, ref invViewProj);
            // Upload forward view-proj for depth computation in the fragment shader
            if (_locViewProjForward != -1)
                GL.UniformMatrix4(_locViewProjForward, false, ref viewProj);

            // Debug: force grid visible for testing
                // if (_locDebugAlwaysVisible != -1)
                //     GL.Uniform1(_locDebugAlwaysVisible, 1);
            GL.Uniform3(_locCamPos, camPos.X, camPos.Y, camPos.Z);
            GL.Uniform1(_locNear, 0.1f); // These should match your camera near/far
            GL.Uniform1(_locFar, 5000.0f);

            GL.BindVertexArray(_vao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);

            // Restore state
                // Restore state
                GL.DepthMask(true);
                if (!blendWas) GL.Disable(EnableCap.Blend);
                if (!depthWas) GL.Disable(EnableCap.DepthTest);
        }

        public void Dispose()
        {
            if (_vao != 0) GL.DeleteVertexArray(_vao);
            if (_vbo != 0) GL.DeleteBuffer(_vbo);
            if (_prog != 0) GL.DeleteProgram(_prog);
        }
    }
}

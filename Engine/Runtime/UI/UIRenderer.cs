using System;
using System.Numerics;
using OpenTK.Graphics.OpenGL4;

namespace Engine.UI
{
    /// <summary>
    /// Minimal UIRenderer: collects geometry from canvases and draws colored quads.
    /// For MVP we use dynamic GL buffers and a simple shader.
    /// </summary>
    public class UIRenderer : IDisposable
    {
        private int _shader = 0;
        private int _vao = 0, _vbo = 0, _ebo = 0;
        private bool _glInitialized = false;

        public UIRenderer()
        {
            // Defer GL resource creation to first Render call where a GL context
            // is guaranteed to be current. This avoids InvalidOperation when
            // constructing the renderer on a non-GL thread.
            _glInitialized = false;
        }

        private void CreateShader()
        {
            string vs = @"#version 330 core
layout(location=0) in vec3 aPos;
layout(location=1) in vec2 aUV;
layout(location=2) in uint aColor;
layout(location=3) in uint aUseTex;
out vec2 vUV;
flat out uint vUseTex;
out vec4 vColor;
uniform vec2 uViewport;
void main(){
    vec2 pos = aPos.xy;
    // Convert from canvas pixels (origin top-left) to normalized device coords
    vec2 ndc = ((pos / uViewport) * 2.0 - 1.0) * vec2(1.0, -1.0);
    gl_Position = vec4(ndc, 0.0, 1.0);
    vUV = aUV;
    vUseTex = aUseTex;
    // decode ARGB uint -> vec4
    uint c = aColor;
    float a = float((c >> 24) & 0xFF) / 255.0;
    float r = float((c >> 16) & 0xFF) / 255.0;
    float g = float((c >> 8) & 0xFF) / 255.0;
    float b = float((c >> 0) & 0xFF) / 255.0;
    vColor = vec4(r,g,b,a);
}
";

            string fs = @"#version 330 core
in vec2 vUV;
flat in uint vUseTex;
in vec4 vColor;
out vec4 FragColor;
uniform sampler2D uFontAtlas;
void main(){
    vec4 base = vColor;
    if (vUseTex == 1u) {
        vec4 s = texture(uFontAtlas, vUV);
        // assume atlas stores glyph mask in alpha; combine mask with vertex color
        base.rgb = base.rgb * s.rgb;
        base.a = base.a * s.a;
    }
    FragColor = base;
}
";

            int vsId = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vsId, vs);
            GL.CompileShader(vsId);

            int fsId = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fsId, fs);
            GL.CompileShader(fsId);

            _shader = GL.CreateProgram();
            GL.AttachShader(_shader, vsId);
            GL.AttachShader(_shader, fsId);
            GL.LinkProgram(_shader);

            GL.DeleteShader(vsId);
            GL.DeleteShader(fsId);
        }

        public void RenderAllCanvases()
        {
            // Ensure GL is available and lazy-init resources
            if (!_glInitialized)
            {
                try
                {
                    CreateShader();
                    _vao = GL.GenVertexArray();
                    _vbo = GL.GenBuffer();
                    _ebo = GL.GenBuffer();
                    _glInitialized = true;
                }
                catch (Exception ex)
                {
                    // GL not ready on this thread/context; skip rendering this frame.
                    try { Console.WriteLine($"[UIRenderer] GL not initialized: {ex.Message}"); } catch { }
                    // Clear any GL error state so other renderers are not confused
                    try { while (GL.GetError() != ErrorCode.NoError) { } } catch { }
                    return;
                }
            }
            var canvases = EventSystem.Instance.Canvases;
            if (canvases.Count == 0) return;

            // For MVP handle one canvas at a time and draw its elements
            foreach (var c in canvases)
            {
                var mb = new UIMeshBuilder();
                // Optionally add a full-canvas background quad
                if (c.AutoBackground)
                {
                    // Top-left origin in canvas coordinates; add full-size quad
                    mb.AddQuad(0, 0, c.Size.X, c.Size.Y, c.BackgroundColor, new Vector2(0,0), new Vector2(1,1));
                }
                // Collect geometry from roots
                for (int r = 0; r < c.Roots.Count; r++)
                {
                    var root = c.Roots[r];
                    PopulateRecursive(root, mb, c.Size);
                }

                if (mb.Vertices.Count == 0) continue;

                // Upload vertex/index data
                GL.BindVertexArray(_vao);

                int vertexSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(UIVertex));
                GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
                GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(mb.Vertices.Count * vertexSize), mb.Vertices.ToArray(), BufferUsageHint.DynamicDraw);

                GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
                GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(mb.Indices.Count * sizeof(uint)), mb.Indices.ToArray(), BufferUsageHint.DynamicDraw);

                GL.EnableVertexAttribArray(0);
                GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, vertexSize, 0);
                GL.EnableVertexAttribArray(1);
                GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, vertexSize, System.Runtime.InteropServices.Marshal.OffsetOf(typeof(UIVertex), "UV").ToInt32());
                GL.EnableVertexAttribArray(2);
                GL.VertexAttribIPointer(2, 1, VertexAttribIntegerType.UnsignedInt, vertexSize, System.Runtime.InteropServices.Marshal.OffsetOf(typeof(UIVertex), "Color").ToInt32());
                // UseTexture attribute
                GL.EnableVertexAttribArray(3);
                GL.VertexAttribIPointer(3, 1, VertexAttribIntegerType.UnsignedInt, vertexSize, System.Runtime.InteropServices.Marshal.OffsetOf(typeof(UIVertex), "UseTexture").ToInt32());

                GL.UseProgram(_shader);
                int vpLoc = GL.GetUniformLocation(_shader, "uViewport");
                GL.Uniform2(vpLoc, c.Size.X, c.Size.Y);
                int fontLoc = GL.GetUniformLocation(_shader, "uFontAtlas");
                if (fontLoc >= 0) GL.Uniform1(fontLoc, 0);

                // Bind default font atlas if available (deferred upload)
                var atlas = FontAtlas.CreateDefault(14);
                if (atlas != null)
                {
                    int tex = 0;
                    try { tex = atlas.EnsureTextureId(); } catch { tex = 0; }
                    if (tex != 0)
                    {
                        GL.ActiveTexture(OpenTK.Graphics.OpenGL4.TextureUnit.Texture0);
                        GL.BindTexture(OpenTK.Graphics.OpenGL4.TextureTarget.Texture2D, tex);
                    }
                }

                GL.DrawElements(PrimitiveType.Triangles, mb.Indices.Count, DrawElementsType.UnsignedInt, 0);

                // Unbind atlas
                GL.BindTexture(OpenTK.Graphics.OpenGL4.TextureTarget.Texture2D, 0);

                GL.BindVertexArray(0);
                GL.UseProgram(0);
            }
        }

        private void PopulateRecursive(UIElement el, UIMeshBuilder mb, Vector2 canvasSize)
        {
            if (!el.Visible) return;
            el.OnPopulateMesh(mb, canvasSize);
            foreach (var ch in el.Children) PopulateRecursive(ch, mb, canvasSize);
        }

        public void Dispose()
        {
            if (_shader != 0) GL.DeleteProgram(_shader);
            if (_vbo != 0) GL.DeleteBuffer(_vbo);
            if (_ebo != 0) GL.DeleteBuffer(_ebo);
            if (_vao != 0) GL.DeleteVertexArray(_vao);
        }
    }
}

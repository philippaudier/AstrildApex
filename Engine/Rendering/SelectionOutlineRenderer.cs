using System;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace Engine.Rendering
{
    /// <summary>
    /// Renders selection outline using marginallyclever approach:
    /// 1. Render selected object as white mask in stencil FBO
    /// 2. Fullscreen quad searches neighbors for outline
    /// This completely avoids read/write feedback issues
    /// </summary>
    public class SelectionOutlineRenderer : IDisposable
    {
        // Stencil pass shader
        private int _stencilShader;

        // Outline expansion shader
        private int _expandShader;

        // Fullscreen quad
        private int _quadVao;
        private int _quadVbo;

        // Stencil FBO (R8 texture)
        private int _stencilFbo;
        private int _stencilTex;
        private int _stencilDepth;  // Depth buffer for stencil rendering

        private int _width;
        private int _height;
        private bool _initialized = false;

        public struct OutlineSettings
        {
            public bool Enabled;
            public float Thickness;
            public Vector4 Color;
            public bool EnablePulse;
            public float PulseSpeed;
            public float PulseMinAlpha;
            public float PulseMaxAlpha;

            public static OutlineSettings Default => new OutlineSettings
            {
                Enabled = true,
                Thickness = 3.0f,
                Color = new Vector4(1.0f, 0.5f, 0.0f, 1.0f),
                EnablePulse = true,
                PulseSpeed = 2.0f,
                PulseMinAlpha = 0.3f,
                PulseMaxAlpha = 1.0f
            };
        }

        public void Initialize()
        {
            if (_initialized) return;

            try
            {
                // Load stencil shader (renders object as white)
                _stencilShader = LoadShader(
                    "Engine/Rendering/Shaders/Effects/SelectionOutlineStencil.vert",
                    "Engine/Rendering/Shaders/Effects/SelectionOutlineStencil.frag",
                    "Stencil");

                // Load outline expansion shader
                _expandShader = LoadShader(
                    "Engine/Rendering/Shaders/Effects/SelectionOutlineExpand.vert",
                    "Engine/Rendering/Shaders/Effects/SelectionOutlineExpand.frag",
                    "Expand");

                // Create fullscreen quad
                CreateFullscreenQuad();

                _initialized = true;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[SelectionOutlineRenderer] Init failed: {ex.Message}");
                System.Console.WriteLine($"[SelectionOutlineRenderer] Stack trace: {ex.StackTrace}");
            }
        }

        private int LoadShader(string vertPath, string fragPath, string name)
        {
            if (!System.IO.File.Exists(vertPath))
            {
                throw new Exception($"Vertex shader file not found: {vertPath}");
            }

            if (!System.IO.File.Exists(fragPath))
            {
                throw new Exception($"Fragment shader file not found: {fragPath}");
            }

            string vertSource = System.IO.File.ReadAllText(vertPath);
            string fragSource = System.IO.File.ReadAllText(fragPath);

            int vertShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vertShader, vertSource);
            GL.CompileShader(vertShader);
            CheckShaderCompileStatus(vertShader, $"{name}.vert");

            int fragShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fragShader, fragSource);
            GL.CompileShader(fragShader);
            CheckShaderCompileStatus(fragShader, $"{name}.frag");

            int program = GL.CreateProgram();
            GL.AttachShader(program, vertShader);
            GL.AttachShader(program, fragShader);
            GL.LinkProgram(program);
            CheckProgramLinkStatus(program, name);

            GL.DeleteShader(vertShader);
            GL.DeleteShader(fragShader);

            return program;
        }

        private void CreateFullscreenQuad()
        {
            float[] quadVertices = {
                // positions   // texCoords
                -1.0f,  1.0f,  0.0f, 1.0f,
                -1.0f, -1.0f,  0.0f, 0.0f,
                 1.0f, -1.0f,  1.0f, 0.0f,

                -1.0f,  1.0f,  0.0f, 1.0f,
                 1.0f, -1.0f,  1.0f, 0.0f,
                 1.0f,  1.0f,  1.0f, 1.0f
            };

            _quadVao = GL.GenVertexArray();
            _quadVbo = GL.GenBuffer();

            GL.BindVertexArray(_quadVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _quadVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, quadVertices.Length * sizeof(float), quadVertices, BufferUsageHint.StaticDraw);

            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);

            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));

            GL.BindVertexArray(0);
        }

        public void Resize(int width, int height)
        {
            if (width <= 0 || height <= 0) return;
            if (_width == width && _height == height) return;

            _width = width;
            _height = height;

            // Clean up old resources
            if (_stencilFbo != 0) GL.DeleteFramebuffer(_stencilFbo);
            if (_stencilTex != 0) GL.DeleteTexture(_stencilTex);
            if (_stencilDepth != 0) GL.DeleteRenderbuffer(_stencilDepth);

            // Create R8 stencil texture
            _stencilTex = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _stencilTex);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.R8, width, height, 0, PixelFormat.Red, PixelType.UnsignedByte, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            // Create depth renderbuffer for stencil FBO
            _stencilDepth = GL.GenRenderbuffer();
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _stencilDepth);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.DepthComponent24, width, height);

            // Create stencil FBO
            _stencilFbo = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _stencilFbo);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _stencilTex, 0);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, _stencilDepth);

            var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != FramebufferErrorCode.FramebufferComplete)
            {
                throw new Exception($"Stencil FBO incomplete: {status}");
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }

        /// <summary>
        /// Render outline using marginallyclever two-pass approach
        /// </summary>
        /// <param name="sceneTexture">Scene color texture</param>
        /// <param name="renderObjectCallbacks">List of callbacks to render selected object geometries</param>
        /// <param name="view">View matrix</param>
        /// <param name="projection">Projection matrix</param>
        /// <param name="destFbo">Destination framebuffer to write final result</param>
        /// <param name="screenWidth">Screen width</param>
        /// <param name="screenHeight">Screen height</param>
        /// <param name="settings">Outline settings</param>
        /// <param name="time">Current time</param>
        public void RenderOutline(
            int sceneTexture,
            System.Collections.Generic.List<Action> renderObjectCallbacks,
            Matrix4 view,
            Matrix4 projection,
            int destFbo,
            int screenWidth,
            int screenHeight,
            OutlineSettings settings,
            float time)
        {
            if (!_initialized || !settings.Enabled || renderObjectCallbacks == null || renderObjectCallbacks.Count == 0)
            {
                return;
            }

            if (_width != screenWidth || _height != screenHeight)
            {
                Resize(screenWidth, screenHeight);
            }

            try
            {
                // PASS 1: Render selected objects as white in stencil FBO
                RenderStencilMask(renderObjectCallbacks, view, projection);

                // PASS 2: Fullscreen quad with neighbor search to create outline
                RenderOutlineExpansion(sceneTexture, destFbo, settings, time);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[SelectionOutlineRenderer] Render failed: {ex.Message}");
                System.Console.WriteLine($"[SelectionOutlineRenderer] Stack trace: {ex.StackTrace}");
            }
        }

        private void RenderStencilMask(System.Collections.Generic.List<Action> renderObjectCallbacks, Matrix4 view, Matrix4 projection)
        {
            // Bind stencil FBO
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _stencilFbo);
            GL.Viewport(0, 0, _width, _height);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);

            // Clear to BLACK (objects will be rendered as white)
            GL.ClearColor(0, 0, 0, 0);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            // Use stencil shader
            GL.UseProgram(_stencilShader);

            // Set view/projection (model matrix set by callbacks)
            int viewLoc = GL.GetUniformLocation(_stencilShader, "u_View");
            int projLoc = GL.GetUniformLocation(_stencilShader, "u_Projection");
            GL.UniformMatrix4(viewLoc, false, ref view);
            GL.UniformMatrix4(projLoc, false, ref projection);

            // Disable depth test and depth write to ensure rendering works
            GL.Disable(EnableCap.DepthTest);
            GL.DepthMask(false);

            // CRITICAL: Render all objects TWICE to capture full silhouettes
            // Once with front faces, once with back faces
            // This ensures complete outline regardless of viewing angle

            GL.Enable(EnableCap.CullFace);

            // First pass: render front faces of all objects
            GL.CullFace(TriangleFace.Back);
            foreach (var callback in renderObjectCallbacks)
            {
                callback();
                // Rebind shader in case callback changed it
                if (GL.GetInteger(GetPName.CurrentProgram) != _stencilShader)
                {
                    GL.UseProgram(_stencilShader);
                    GL.UniformMatrix4(viewLoc, false, ref view);
                    GL.UniformMatrix4(projLoc, false, ref projection);
                }
            }

            // Second pass: render back faces of all objects
            GL.CullFace(TriangleFace.Front);
            foreach (var callback in renderObjectCallbacks)
            {
                callback();
                // Rebind shader in case callback changed it
                if (GL.GetInteger(GetPName.CurrentProgram) != _stencilShader)
                {
                    GL.UseProgram(_stencilShader);
                    GL.UniformMatrix4(viewLoc, false, ref view);
                    GL.UniformMatrix4(projLoc, false, ref projection);
                }
            }

            GL.Disable(EnableCap.CullFace);
            GL.UseProgram(0);

            // Restore depth mask
            GL.DepthMask(true);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        private void RenderOutlineExpansion(int sceneTexture, int destFbo, OutlineSettings settings, float time)
        {
            // Copy scene texture to dest FBO to ensure we have the scene as base
            int srcFbo = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, srcFbo);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, sceneTexture, 0);

            // Blit from source to destination
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, srcFbo);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, destFbo);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
            GL.BlitFramebuffer(0, 0, _width, _height, 0, 0, _width, _height, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);

            GL.DeleteFramebuffer(srcFbo);

            // Unbind read/draw framebuffers to reset state
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);

            // Bind destination FBO for drawing outline
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, destFbo);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
            GL.Viewport(0, 0, _width, _height);

            // Reset all states that could block rendering
            GL.Disable(EnableCap.DepthTest);
            GL.DepthMask(false);
            GL.Disable(EnableCap.CullFace);
            GL.Disable(EnableCap.ScissorTest);
            GL.Disable(EnableCap.StencilTest);

            // Enable blending so outline is composited over the scene
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            GL.UseProgram(_expandShader);

            // Bind stencil texture (scene is already copied to FBO via blit)
            GL.Uniform1(GL.GetUniformLocation(_expandShader, "u_StencilTexture"), 0);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _stencilTex);

            // Set uniforms
            GL.Uniform4(GL.GetUniformLocation(_expandShader, "u_OutlineColor"), settings.Color);
            GL.Uniform1(GL.GetUniformLocation(_expandShader, "u_OutlineSize"), settings.Thickness);
            GL.Uniform2(GL.GetUniformLocation(_expandShader, "u_CanvasSize"), (float)_width, (float)_height);
            GL.Uniform1(GL.GetUniformLocation(_expandShader, "u_Time"), time);
            GL.Uniform1(GL.GetUniformLocation(_expandShader, "u_EnablePulse"), settings.EnablePulse ? 1 : 0);
            GL.Uniform1(GL.GetUniformLocation(_expandShader, "u_PulseSpeed"), settings.PulseSpeed);
            GL.Uniform1(GL.GetUniformLocation(_expandShader, "u_PulseMinAlpha"), settings.PulseMinAlpha);
            GL.Uniform1(GL.GetUniformLocation(_expandShader, "u_PulseMaxAlpha"), settings.PulseMaxAlpha);

            // Draw fullscreen quad
            GL.BindVertexArray(_quadVao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
            GL.BindVertexArray(0);
            GL.UseProgram(0);

            // Restore GL states to default
            GL.Disable(EnableCap.Blend);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthMask(true);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(TriangleFace.Back);
            GL.FrontFace(FrontFaceDirection.Ccw);

            // Unbind all textures
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, 0);

            // Reset viewport and framebuffer
            GL.Viewport(0, 0, _width, _height);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
        }

        private void CheckShaderCompileStatus(int shader, string name)
        {
            GL.GetShader(shader, ShaderParameter.CompileStatus, out int status);
            if (status == 0)
            {
                string log = GL.GetShaderInfoLog(shader);
                throw new Exception($"Shader compilation failed ({name}):\n{log}");
            }
        }

        private void CheckProgramLinkStatus(int program, string name)
        {
            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int status);
            if (status == 0)
            {
                string log = GL.GetProgramInfoLog(program);
                throw new Exception($"Shader program linking failed ({name}):\n{log}");
            }
        }

        public void Dispose()
        {
            if (_stencilShader != 0) GL.DeleteProgram(_stencilShader);
            if (_expandShader != 0) GL.DeleteProgram(_expandShader);
            if (_quadVao != 0) GL.DeleteVertexArray(_quadVao);
            if (_quadVbo != 0) GL.DeleteBuffer(_quadVbo);
            if (_stencilFbo != 0) GL.DeleteFramebuffer(_stencilFbo);
            if (_stencilTex != 0) GL.DeleteTexture(_stencilTex);
            if (_stencilDepth != 0) GL.DeleteRenderbuffer(_stencilDepth);

            _initialized = false;
        }
    }
}

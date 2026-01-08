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
                System.Console.WriteLine("[SelectionOutlineRenderer] Starting initialization...");

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
                System.Console.WriteLine("[SelectionOutlineRenderer] Initialized (marginallyclever approach)");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[SelectionOutlineRenderer] Init failed: {ex.Message}");
                System.Console.WriteLine($"[SelectionOutlineRenderer] Stack trace: {ex.StackTrace}");
            }
        }

        private int LoadShader(string vertPath, string fragPath, string name)
        {
            System.Console.WriteLine($"[SelectionOutlineRenderer] Loading shader '{name}': vert={vertPath}, frag={fragPath}");

            if (!System.IO.File.Exists(vertPath))
            {
                System.Console.WriteLine($"[SelectionOutlineRenderer] ERROR: Vertex shader not found: {vertPath}");
                System.Console.WriteLine($"[SelectionOutlineRenderer] Current directory: {System.IO.Directory.GetCurrentDirectory()}");
                throw new Exception($"Vertex shader file not found: {vertPath}");
            }

            if (!System.IO.File.Exists(fragPath))
            {
                System.Console.WriteLine($"[SelectionOutlineRenderer] ERROR: Fragment shader not found: {fragPath}");
                System.Console.WriteLine($"[SelectionOutlineRenderer] Current directory: {System.IO.Directory.GetCurrentDirectory()}");
                throw new Exception($"Fragment shader file not found: {fragPath}");
            }

            System.Console.WriteLine($"[SelectionOutlineRenderer] Shader files found, compiling...");

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

            System.Console.WriteLine($"[SelectionOutlineRenderer] Shader '{name}' compiled and linked successfully (program={program})");

            return program;
        }

        private void CreateFullscreenQuad()
        {
            System.Console.WriteLine("[SelectionOutlineRenderer] Creating fullscreen quad...");

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

            System.Console.WriteLine($"[SelectionOutlineRenderer] Generated VAO={_quadVao}, VBO={_quadVbo}");

            GL.BindVertexArray(_quadVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _quadVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, quadVertices.Length * sizeof(float), quadVertices, BufferUsageHint.StaticDraw);

            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);

            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));

            GL.BindVertexArray(0);

            System.Console.WriteLine($"[SelectionOutlineRenderer] Fullscreen quad created successfully");
        }

        public void Resize(int width, int height)
        {
            System.Console.WriteLine($"[SelectionOutlineRenderer] Resize called: {width}x{height} (current: {_width}x{_height})");

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

            try { Engine.Utils.DebugLogger.Log($"[SelectionOutlineRenderer] Resized stencil FBO to {width}x{height}"); } catch { }
        }

        /// <summary>
        /// Render outline using marginallyclever two-pass approach
        /// </summary>
        /// <param name="sceneTexture">Scene color texture</param>
        /// <param name="renderObjectCallback">Callback to render selected object geometry</param>
        /// <param name="view">View matrix</param>
        /// <param name="projection">Projection matrix</param>
        /// <param name="destFbo">Destination framebuffer to write final result</param>
        /// <param name="screenWidth">Screen width</param>
        /// <param name="screenHeight">Screen height</param>
        /// <param name="settings">Outline settings</param>
        /// <param name="time">Current time</param>
        public void RenderOutline(
            int sceneTexture,
            Action renderObjectCallback,
            Matrix4 view,
            Matrix4 projection,
            int destFbo,
            int screenWidth,
            int screenHeight,
            OutlineSettings settings,
            float time)
        {
            System.Console.WriteLine($"[SelectionOutlineRenderer] RenderOutline called: _initialized={_initialized}, settings.Enabled={settings.Enabled}, callback={(renderObjectCallback != null ? "OK" : "NULL")}");

            if (!_initialized || !settings.Enabled || renderObjectCallback == null)
            {
                System.Console.WriteLine($"[SelectionOutlineRenderer] Exiting early: _initialized={_initialized}, settings.Enabled={settings.Enabled}, callback={(renderObjectCallback != null ? "OK" : "NULL")}");
                return;
            }

            System.Console.WriteLine($"[SelectionOutlineRenderer] Checking resize: _width={_width}, _height={_height}, screenWidth={screenWidth}, screenHeight={screenHeight}");

            if (_width != screenWidth || _height != screenHeight)
            {
                Resize(screenWidth, screenHeight);
            }
            else
            {
                System.Console.WriteLine($"[SelectionOutlineRenderer] Skipping resize (dimensions match)");
            }

            try
            {
                System.Console.WriteLine($"[SelectionOutlineRenderer] Starting render passes...");

                // PASS 1: Render selected object as white in stencil FBO
                RenderStencilMask(renderObjectCallback, view, projection);

                // PASS 2: Fullscreen quad with neighbor search to create outline
                RenderOutlineExpansion(sceneTexture, destFbo, settings, time);

                System.Console.WriteLine($"[SelectionOutlineRenderer] Render passes completed");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[SelectionOutlineRenderer] Render failed: {ex.Message}");
                System.Console.WriteLine($"[SelectionOutlineRenderer] Stack trace: {ex.StackTrace}");
            }
        }

        private void RenderStencilMask(Action renderObjectCallback, Matrix4 view, Matrix4 projection)
        {
            System.Console.WriteLine($"[SelectionOutlineRenderer] PASS 1: Rendering stencil mask (FBO={_stencilFbo}, shader={_stencilShader})");

            // Bind stencil FBO
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _stencilFbo);
            GL.Viewport(0, 0, _width, _height);

            // CRITICAL: Ensure we're drawing to ColorAttachment0
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);

            // Verify FBO status
            var fboStatus = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            System.Console.WriteLine($"[SelectionOutlineRenderer] Stencil FBO status after bind: {fboStatus}");

            // Clear to BLACK (object will be rendered as white)
            GL.ClearColor(0, 0, 0, 0);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            // DEBUG TEST: Skip rendering and just clear to white to test if outline works
            bool TEST_SKIP_RENDER_CLEAR_WHITE = false;
            if (TEST_SKIP_RENDER_CLEAR_WHITE)
            {
                System.Console.WriteLine($"[SelectionOutlineRenderer] TEST: Clearing stencil to WHITE instead of rendering");
                GL.ClearColor(1, 1, 1, 1);
                GL.Clear(ClearBufferMask.ColorBufferBit);

                // Early return - skip rendering entirely
                GL.UseProgram(0);
                GL.DepthMask(true);
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
                System.Console.WriteLine($"[SelectionOutlineRenderer] TEST: Stencil cleared to white, PASS 1 complete");
                return;
            }

            // Verify clear worked by reading a pixel
            byte[] afterClearPixel = new byte[1];
            GL.ReadPixels(_width / 2, _height / 2, 1, 1, PixelFormat.Red, PixelType.UnsignedByte, afterClearPixel);
            System.Console.WriteLine($"[SelectionOutlineRenderer] Center pixel AFTER clear: {afterClearPixel[0]} (should be 0)");

            // Use stencil shader
            GL.UseProgram(_stencilShader);

            // Set view/projection (model matrix set by callback)
            int viewLoc = GL.GetUniformLocation(_stencilShader, "u_View");
            int projLoc = GL.GetUniformLocation(_stencilShader, "u_Projection");
            System.Console.WriteLine($"[SelectionOutlineRenderer] u_View loc={viewLoc}, u_Projection loc={projLoc}");

            // DEBUG: Print matrices to verify they're not zero/invalid
            System.Console.WriteLine($"[SelectionOutlineRenderer] View M11={view.M11:F3}, M14={view.M14:F3}, M41={view.M41:F3}, M44={view.M44:F3}");
            System.Console.WriteLine($"[SelectionOutlineRenderer] Proj M11={projection.M11:F3}, M33={projection.M33:F3}, M34={projection.M34:F3}, M44={projection.M44:F3}");

            GL.UniformMatrix4(viewLoc, false, ref view);
            GL.UniformMatrix4(projLoc, false, ref projection);

            // DISABLE depth test and depth write to ensure rendering works
            GL.Disable(EnableCap.DepthTest);
            GL.DepthMask(false);

            // CRITICAL: Render object TWICE to capture full silhouette
            // Once with front faces, once with back faces
            // This ensures complete outline regardless of viewing angle

            System.Console.WriteLine($"[SelectionOutlineRenderer] Rendering front faces...");
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(TriangleFace.Back);  // Render front faces

            // Verify shader is still bound before callback
            int programBefore = GL.GetInteger(GetPName.CurrentProgram);
            System.Console.WriteLine($"[SelectionOutlineRenderer] Program BEFORE callback: {programBefore} (should be {_stencilShader})");

            renderObjectCallback();

            // Check if callback changed the shader
            int programAfter = GL.GetInteger(GetPName.CurrentProgram);
            System.Console.WriteLine($"[SelectionOutlineRenderer] Program AFTER callback: {programAfter} (should still be {_stencilShader})");

            // Rebind shader and matrices if callback changed them
            if (programAfter != _stencilShader)
            {
                System.Console.WriteLine($"[SelectionOutlineRenderer] WARNING: Callback changed shader! Rebinding...");
                GL.UseProgram(_stencilShader);
                GL.UniformMatrix4(viewLoc, false, ref view);
                GL.UniformMatrix4(projLoc, false, ref projection);
            }

            System.Console.WriteLine($"[SelectionOutlineRenderer] Rendering back faces...");
            GL.CullFace(TriangleFace.Front);  // Render back faces
            renderObjectCallback();

            // Check again after second callback
            programAfter = GL.GetInteger(GetPName.CurrentProgram);
            if (programAfter != _stencilShader)
            {
                System.Console.WriteLine($"[SelectionOutlineRenderer] WARNING: Second callback changed shader!");
                GL.UseProgram(_stencilShader);
                GL.UniformMatrix4(viewLoc, false, ref view);
                GL.UniformMatrix4(projLoc, false, ref projection);
            }

            GL.Disable(EnableCap.CullFace);
            System.Console.WriteLine($"[SelectionOutlineRenderer] Both passes complete");

            GL.UseProgram(0);

            // Restore depth mask
            GL.DepthMask(true);

            // DEBUG: Read multiple pixels from stencil FBO to verify object was rendered
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _stencilFbo);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);

            // Count how many white pixels we have in stencil buffer
            int whitePixelCount = 0;
            int sampleCount = 0;

            // Sample a 5x5 grid across the screen
            for (int y = 0; y < 5; y++)
            {
                for (int x = 0; x < 5; x++)
                {
                    int px = (x + 1) * _width / 6;  // Divide screen into 6 parts, sample at 1/6, 2/6, 3/6, 4/6, 5/6
                    int py = (y + 1) * _height / 6;

                    byte[] pixel = new byte[1];
                    GL.ReadPixels(px, py, 1, 1, PixelFormat.Red, PixelType.UnsignedByte, pixel);

                    sampleCount++;
                    if (pixel[0] > 128) whitePixelCount++;  // Consider > 128 as white
                }
            }

            float coverage = (float)whitePixelCount / sampleCount * 100f;
            System.Console.WriteLine($"[SelectionOutlineRenderer] Stencil coverage: {whitePixelCount}/{sampleCount} samples ({coverage:F1}%) are white");

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            System.Console.WriteLine($"[SelectionOutlineRenderer] PASS 1 complete");
        }

        private void RenderOutlineExpansion(int sceneTexture, int destFbo, OutlineSettings settings, float time)
        {
            System.Console.WriteLine($"[SelectionOutlineRenderer] PASS 2: Outline expansion (destFbo={destFbo}, shader={_expandShader})");

            // FIRST: Copy scene texture to dest FBO to ensure we have the scene as base
            // This is safer than relying on the shader to read and copy the scene
            System.Console.WriteLine($"[SelectionOutlineRenderer] Copying scene texture {sceneTexture} to destFbo {destFbo}");

            // Create a temporary FBO for the source texture if we need to blit from it
            int srcFbo = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, srcFbo);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, sceneTexture, 0);

            var srcStatus = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            System.Console.WriteLine($"[SelectionOutlineRenderer] Temp source FBO status: {srcStatus}");

            // DEBUG: Read a pixel from source texture to verify it contains the scene
            byte[] srcPixel = new byte[4];
            GL.ReadPixels(_width / 2, _height / 2, 1, 1, PixelFormat.Rgba, PixelType.UnsignedByte, srcPixel);
            System.Console.WriteLine($"[SelectionOutlineRenderer] Source texture pixel (before blit): R={srcPixel[0]}, G={srcPixel[1]}, B={srcPixel[2]}");

            // Blit from source to destination
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, srcFbo);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, destFbo);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
            GL.BlitFramebuffer(0, 0, _width, _height, 0, 0, _width, _height, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);

            var blitError = GL.GetError();
            if (blitError != ErrorCode.NoError)
                System.Console.WriteLine($"[SelectionOutlineRenderer] Blit error: {blitError}");

            // Clean up temp FBO
            GL.DeleteFramebuffer(srcFbo);

            // CRITICAL: Unbind read/draw framebuffers to reset state
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);

            System.Console.WriteLine($"[SelectionOutlineRenderer] Scene copied to destFbo");

            // Now bind destination FBO for drawing outline
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, destFbo);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);  // Ensure draw buffer is set

            // Check FBO status
            var fboStatus = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            System.Console.WriteLine($"[SelectionOutlineRenderer] Dest FBO status: {fboStatus}");

            GL.Viewport(0, 0, _width, _height);

            // Reset all states that could block rendering
            GL.Disable(EnableCap.DepthTest);
            GL.DepthMask(false);
            GL.Disable(EnableCap.CullFace);
            GL.Disable(EnableCap.ScissorTest);
            GL.Disable(EnableCap.StencilTest);

            // ENABLE blending so outline is composited over the scene
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            System.Console.WriteLine($"[SelectionOutlineRenderer] GL states reset for fullscreen quad");

            // DEBUG: Verify scene is in FBO after blit
            byte[] preDrawPixel = new byte[4];
            GL.ReadPixels(_width / 2, _height / 2, 1, 1, PixelFormat.Rgba, PixelType.UnsignedByte, preDrawPixel);
            System.Console.WriteLine($"[SelectionOutlineRenderer] FBO pixel AFTER blit (before quad): R={preDrawPixel[0]}, G={preDrawPixel[1]}, B={preDrawPixel[2]}");

            GL.UseProgram(_expandShader);

            // Bind only stencil texture (scene is already copied to FBO via blit)
            GL.Uniform1(GL.GetUniformLocation(_expandShader, "u_StencilTexture"), 0);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _stencilTex);
            System.Console.WriteLine($"[SelectionOutlineRenderer] Bound stencil texture {_stencilTex} to unit 0");

            // DO NOT bind sceneTexture - it would cause read/write feedback since we're writing to the same FBO
            // The scene is already in the destination FBO from the blit above

            // Set uniforms
            GL.Uniform4(GL.GetUniformLocation(_expandShader, "u_OutlineColor"), settings.Color);
            GL.Uniform1(GL.GetUniformLocation(_expandShader, "u_OutlineSize"), settings.Thickness);
            GL.Uniform2(GL.GetUniformLocation(_expandShader, "u_CanvasSize"), (float)_width, (float)_height);
            System.Console.WriteLine($"[SelectionOutlineRenderer] Uniforms: thickness={settings.Thickness}, canvasSize={_width}x{_height}");
            System.Console.WriteLine($"[SelectionOutlineRenderer] Outline color: R={settings.Color.X}, G={settings.Color.Y}, B={settings.Color.Z}, A={settings.Color.W}");
            GL.Uniform1(GL.GetUniformLocation(_expandShader, "u_Time"), time);
            GL.Uniform1(GL.GetUniformLocation(_expandShader, "u_EnablePulse"), settings.EnablePulse ? 1 : 0);
            GL.Uniform1(GL.GetUniformLocation(_expandShader, "u_PulseSpeed"), settings.PulseSpeed);
            GL.Uniform1(GL.GetUniformLocation(_expandShader, "u_PulseMinAlpha"), settings.PulseMinAlpha);
            GL.Uniform1(GL.GetUniformLocation(_expandShader, "u_PulseMaxAlpha"), settings.PulseMaxAlpha);
            System.Console.WriteLine($"[SelectionOutlineRenderer] Pulse settings: enabled={settings.EnablePulse}, speed={settings.PulseSpeed}, minAlpha={settings.PulseMinAlpha}, maxAlpha={settings.PulseMaxAlpha}");

            // Draw fullscreen quad
            System.Console.WriteLine($"[SelectionOutlineRenderer] Drawing fullscreen quad (VAO={_quadVao}, VBO={_quadVbo})...");

            // Verify VAO is valid
            if (_quadVao == 0 || _quadVbo == 0)
            {
                System.Console.WriteLine($"[SelectionOutlineRenderer] ERROR: Invalid VAO or VBO! VAO={_quadVao}, VBO={_quadVbo}");
            }

            GL.BindVertexArray(_quadVao);

            // Check what's currently bound
            int boundVao = GL.GetInteger(GetPName.VertexArrayBinding);
            System.Console.WriteLine($"[SelectionOutlineRenderer] Bound VAO: {boundVao}");

            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);

            // Check GL error after draw
            var err = GL.GetError();
            if (err != ErrorCode.NoError)
                System.Console.WriteLine($"[SelectionOutlineRenderer] GL ERROR after DrawArrays: {err}");

            GL.BindVertexArray(0);
            GL.UseProgram(0);

            // DEBUG: Read a few pixels around the edge to see if outline was drawn
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, destFbo);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);

            // Read center pixel
            byte[] centerPixel = new byte[4];
            GL.ReadPixels(_width / 2, _height / 2, 1, 1, PixelFormat.Rgba, PixelType.UnsignedByte, centerPixel);
            System.Console.WriteLine($"[SelectionOutlineRenderer] Dest FBO center pixel: R={centerPixel[0]}, G={centerPixel[1]}, B={centerPixel[2]}, A={centerPixel[3]}");

            // Read a pixel near the edge where outline might be
            byte[] edgePixel = new byte[4];
            GL.ReadPixels(_width / 2 + 10, _height / 2 + 10, 1, 1, PixelFormat.Rgba, PixelType.UnsignedByte, edgePixel);
            System.Console.WriteLine($"[SelectionOutlineRenderer] Dest FBO edge pixel (+10,+10): R={edgePixel[0]}, G={edgePixel[1]}, B={edgePixel[2]}, A={edgePixel[3]}");

            // CRITICAL: Restore ALL GL states to default
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

            // Reset viewport to full size (in case it was modified)
            GL.Viewport(0, 0, _width, _height);

            // Restore framebuffer to default
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);

            System.Console.WriteLine($"[SelectionOutlineRenderer] PASS 2 complete, GL states restored");
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

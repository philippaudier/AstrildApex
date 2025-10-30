using System;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace Engine.Rendering.PostProcess
{
    /// <summary>
    /// Temporal Anti-Aliasing (TAA) renderer.
    /// Provides high-quality anti-aliasing by accumulating samples across frames.
    /// Uses temporal reprojection with neighborhood clamping to reduce ghosting.
    /// </summary>
    public class TAARenderer : IDisposable
    {
        // TAA settings
        public struct TAASettings
        {
            public bool Enabled;
            public float FeedbackMin;    // Minimum history weight (0.0-1.0, default 0.8)
            public float FeedbackMax;    // Maximum history weight (0.0-1.0, default 0.95)
            public bool UseYCoCg;        // Use YCoCg color space (better quality)
            public int JitterPattern;    // 0=Halton, 1=R2, 2=None
            public float JitterScale;    // Multiplier applied to jitter amplitude (1.0 = full)

            public static TAASettings Default => new TAASettings
            {
                Enabled = true,
                FeedbackMin = 0.85f,
                FeedbackMax = 0.98f,
                UseYCoCg = true, // Enable YCoCg by default for better temporal stability
                JitterPattern = 0, // Halton sequence (best quality)
                // Scale applied to jitter amplitude (1.0 = full pixel jitter). Lowering
                // reduces visible sub-pixel flicker at the cost of AA sample coverage.
                JitterScale = 1.0f
            };
        }

    // (Settings struct now includes JitterScale)

        // Framebuffers and textures
        private int _historyFBO = 0;
        private int _historyTexture = 0;

        // Resolution
        private int _width = 0;
        private int _height = 0;

        // TAA shader
        private ShaderProgram? _taaShader;

        // Blit shader (ultra-fast copy)
        private ShaderProgram? _blitShader;

        // Jitter state
        private int _frameIndex = 0;
        private Vector2 _currentJitter = Vector2.Zero;
        private Vector2 _previousJitter = Vector2.Zero;

        // Previous frame matrices for reprojection
        private Matrix4 _prevViewProj = Matrix4.Identity;

        // Fullscreen quad for post-processing (reused, no allocations)
        private int _quadVAO = 0;
        private int _quadVBO = 0;

        private bool _isFirstFrame = true;
        private bool _loggedOnce = false;

        // Performance: Cache uniform locations to avoid lookups every frame
        private int _uCurrentFrame = -1;
        private int _uDepth = -1;
        private int _uHistoryFrame = -1;
        private int _uInvViewProj = -1;
        private int _uPrevViewProj = -1;
        private int _uFeedbackMin = -1;
        private int _uFeedbackMax = -1;
        private int _uUseYCoCg = -1;
        private int _uJitter = -1;
        private int _uScreenSize = -1;
    private int _uVelocity = -1;

        private int _blitTexLoc = -1;

        public TAASettings Settings { get; set; } = TAASettings.Default;

        public Vector2 CurrentJitter => _currentJitter;

        public TAARenderer(int width, int height)
        {
            _width = width;
            _height = height;

            // Load TAA shader
            try
            {
                _taaShader = ShaderProgram.FromFiles(
                    "Engine/Rendering/Shaders/PostProcess/TAA.vert",
                    "Engine/Rendering/Shaders/PostProcess/TAA.frag"
                );
                // PERFORMANCE: Disabled log
                // Console.WriteLine($"[TAA] Shader loaded successfully (Handle: {_taaShader.Handle})");

                // Cache uniform locations (PERFORMANCE: avoid string lookups every frame)
                _uCurrentFrame = GL.GetUniformLocation(_taaShader.Handle, "u_CurrentFrame");
                _uDepth = GL.GetUniformLocation(_taaShader.Handle, "u_Depth");
                _uHistoryFrame = GL.GetUniformLocation(_taaShader.Handle, "u_HistoryFrame");
                _uInvViewProj = GL.GetUniformLocation(_taaShader.Handle, "u_InvViewProj");
                _uPrevViewProj = GL.GetUniformLocation(_taaShader.Handle, "u_PrevViewProj");
                _uFeedbackMin = GL.GetUniformLocation(_taaShader.Handle, "u_FeedbackMin");
                _uFeedbackMax = GL.GetUniformLocation(_taaShader.Handle, "u_FeedbackMax");
                _uUseYCoCg = GL.GetUniformLocation(_taaShader.Handle, "u_UseYCoCg");
                _uJitter = GL.GetUniformLocation(_taaShader.Handle, "u_Jitter");
                _uScreenSize = GL.GetUniformLocation(_taaShader.Handle, "u_ScreenSize");
                _uVelocity = GL.GetUniformLocation(_taaShader.Handle, "u_Velocity");

                // PERFORMANCE: Disabled logs
                // Console.WriteLine($"[TAA] Uniform locations - CurrentFrame:{_uCurrentFrame} Depth:{_uDepth} History:{_uHistoryFrame}");
                // Console.WriteLine($"[TAA] Uniform locations - InvViewProj:{_uInvViewProj} PrevViewProj:{_uPrevViewProj}");
                // Console.WriteLine($"[TAA] Uniform locations - Feedback:{_uFeedbackMin},{_uFeedbackMax} YCoCg:{_uUseYCoCg} Jitter:{_uJitter} ScreenSize:{_uScreenSize}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TAA] Failed to load shader: {ex.Message}");
                Console.WriteLine($"[TAA] Stack trace: {ex.StackTrace}");
            }

            // Load ultra-fast blit shader for copying result
            try
            {
                _blitShader = ShaderProgram.FromFiles(
                    "Engine/Rendering/Shaders/PostProcess/Blit.vert",
                    "Engine/Rendering/Shaders/PostProcess/Blit.frag"
                );
                _blitTexLoc = GL.GetUniformLocation(_blitShader.Handle, "u_Texture");
                // PERFORMANCE: Disabled log
                // Console.WriteLine($"[TAA] Blit shader loaded successfully (Handle: {_blitShader.Handle}, u_Texture: {_blitTexLoc})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TAA] Failed to load blit shader: {ex.Message}");
                Console.WriteLine($"[TAA] Stack trace: {ex.StackTrace}");
            }

            CreateResources();
            CreateFullscreenQuad();
        }

        private void CreateResources()
        {
            // Create history framebuffer
            _historyFBO = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _historyFBO);

            // History texture (RGBA16F for HDR)
            _historyTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _historyTexture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f,
                         _width, _height, 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                                   TextureTarget.Texture2D, _historyTexture, 0);

            // Verify framebuffer is complete
            var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != FramebufferErrorCode.FramebufferComplete)
            {
                Console.WriteLine($"[TAA] History framebuffer incomplete: {status}");
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            // PERFORMANCE: Disabled log (causes FPS drops when resizing)
            // Console.WriteLine($"[TAA] Resources created: {_width}x{_height}");
        }

        private void CreateFullscreenQuad()
        {
            // Fullscreen quad vertices (position + UV)
            float[] quadVertices = {
                // Positions        // UVs
                -1.0f,  1.0f, 0.0f,  0.0f, 1.0f,
                -1.0f, -1.0f, 0.0f,  0.0f, 0.0f,
                 1.0f, -1.0f, 0.0f,  1.0f, 0.0f,

                -1.0f,  1.0f, 0.0f,  0.0f, 1.0f,
                 1.0f, -1.0f, 0.0f,  1.0f, 0.0f,
                 1.0f,  1.0f, 0.0f,  1.0f, 1.0f
            };

            _quadVAO = GL.GenVertexArray();
            _quadVBO = GL.GenBuffer();

            GL.BindVertexArray(_quadVAO);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _quadVBO);
            GL.BufferData(BufferTarget.ArrayBuffer, quadVertices.Length * sizeof(float),
                         quadVertices, BufferUsageHint.StaticDraw);

            // Position attribute
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 5 * sizeof(float), 0);

            // UV attribute
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 5 * sizeof(float), 3 * sizeof(float));

            GL.BindVertexArray(0);
        }

        /// <summary>
        /// Calculate camera jitter for current frame using Halton sequence.
        /// Halton sequence provides good temporal distribution with low discrepancy.
        /// </summary>
        public Vector2 CalculateJitter()
        {
            if (!Settings.Enabled || Settings.JitterPattern == 2)
            {
                _currentJitter = Vector2.Zero;
                return Vector2.Zero;
            }

            _frameIndex++;

            if (Settings.JitterPattern == 0) // Halton sequence
            {
                // Halton(2, 3) sequence - most common for TAA
                float jitterX = HaltonSequence(_frameIndex, 2) - 0.5f;
                float jitterY = HaltonSequence(_frameIndex, 3) - 0.5f;

                // Scale to pixel size
                _currentJitter = new Vector2(
                    jitterX / _width,
                    jitterY / _height
                ) * Settings.JitterScale;
            }
            else // R2 sequence (alternative low-discrepancy pattern)
            {
                const float g = 1.32471795724474602596f; // Plastic constant
                const float a1 = 1.0f / g;
                const float a2 = 1.0f / (g * g);

                float jitterX = (_frameIndex * a1) % 1.0f - 0.5f;
                float jitterY = (_frameIndex * a2) % 1.0f - 0.5f;

                _currentJitter = new Vector2(
                    jitterX / _width,
                    jitterY / _height
                ) * Settings.JitterScale;
            }

            return _currentJitter;
        }

        /// <summary>
        /// Halton low-discrepancy sequence for jittering.
        /// </summary>
        private float HaltonSequence(int index, int baseNum)
        {
            float result = 0.0f;
            float f = 1.0f;
            int i = index;

            while (i > 0)
            {
                f /= baseNum;
                result += f * (i % baseNum);
                i /= baseNum;
            }

            return result;
        }

        /// <summary>
        /// Apply TAA to the input frame - OPTIMIZED for performance.
        /// </summary>
        /// <param name="inputTexture">Current frame (jittered)</param>
        /// <param name="depthTexture">Depth buffer for reprojection</param>
        /// <param name="invViewProj">Inverse view-projection matrix (current)</param>
        /// <param name="currentViewProj">Current view-projection matrix</param>
    public int RenderTAA(int inputTexture, int depthTexture, Matrix4 invViewProj, Matrix4 currentViewProj, int velocityTexture = 0)
        {
            if (_taaShader == null || !Settings.Enabled)
            {
                // TAA disabled - return input directly
                Console.WriteLine($"[TAARenderer] TAA disabled - shader null: {_taaShader == null}, enabled: {Settings.Enabled}");
                return inputTexture;
            }

            if (!_loggedOnce)
            {
                Console.WriteLine($"[TAARenderer] TAA ACTIVE - input: {inputTexture}, depth: {depthTexture}, shader: {_taaShader.Handle}");
                Console.WriteLine($"[TAARenderer] History texture: {_historyTexture}, FBO: {_historyFBO}");
                Console.WriteLine($"[TAARenderer] Settings - FeedbackMin: {Settings.FeedbackMin}, FeedbackMax: {Settings.FeedbackMax}, YCoCg: {Settings.UseYCoCg}");
                Console.WriteLine($"[TAARenderer] Jitter: ({_currentJitter.X:F6}, {_currentJitter.Y:F6})");
                _loggedOnce = true;
            }

            // PERFORMANCE: Use cached program handle instead of _taaShader.Use()
            GL.UseProgram(_taaShader.Handle);

            // Bind textures (minimize state changes)
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, inputTexture);
            GL.Uniform1(_uCurrentFrame, 0);

            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, depthTexture);
            GL.Uniform1(_uDepth, 1);

            GL.ActiveTexture(TextureUnit.Texture2);
            GL.BindTexture(TextureTarget.Texture2D, _historyTexture);
            GL.Uniform1(_uHistoryFrame, 2);

            // Optional velocity texture (camera/object motion vectors). Bind to unit 4 if provided.
            if (_uVelocity >= 0)
            {
                if (velocityTexture != 0)
                {
                    GL.ActiveTexture(TextureUnit.Texture4);
                    GL.BindTexture(TextureTarget.Texture2D, velocityTexture);
                    GL.Uniform1(_uVelocity, 4);
                }
                else
                {
                    // Unbind or set to unit 4 with zero to indicate none
                    GL.ActiveTexture(TextureUnit.Texture4);
                    GL.BindTexture(TextureTarget.Texture2D, 0);
                    GL.Uniform1(_uVelocity, 4);
                }
            }

            // Set uniforms using cached locations (FAST - no string lookups)
            GL.UniformMatrix4(_uInvViewProj, false, ref invViewProj);
            GL.UniformMatrix4(_uPrevViewProj, false, ref _prevViewProj);
            GL.Uniform1(_uFeedbackMin, Settings.FeedbackMin);
            GL.Uniform1(_uFeedbackMax, _isFirstFrame ? 0.0f : Settings.FeedbackMax);
            GL.Uniform1(_uUseYCoCg, Settings.UseYCoCg ? 1 : 0);
            GL.Uniform2(_uJitter, _currentJitter);
            GL.Uniform2(_uScreenSize, new Vector2(_width, _height));

            // Render to history buffer (this becomes next frame's history)
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _historyFBO);
            GL.Viewport(0, 0, _width, _height);

            // PERFORMANCE: Only clear on first frame
            if (_isFirstFrame)
                GL.Clear(ClearBufferMask.ColorBufferBit);

            // Draw fullscreen quad (single draw call)
            GL.BindVertexArray(_quadVAO);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);

            // PERFORMANCE: Unbind in batch
            GL.BindVertexArray(0);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            // Store current matrices for next frame
            _prevViewProj = currentViewProj;
            _previousJitter = _currentJitter;
            _isFirstFrame = false;

            // Return history texture (TAA result)
            return _historyTexture;
        }

        /// <summary>
        /// PERFORMANCE-OPTIMIZED: Copy TAA result to output texture using fast blit.
        /// Returns output FBO that can be used directly.
        /// </summary>
        public void BlitToTarget(int targetFBO, int width, int height)
        {
            if (_blitShader == null || _historyTexture == 0)
            {
                Console.WriteLine($"[TAARenderer] BlitToTarget skipped - blitShader null: {_blitShader == null}, historyTex: {_historyTexture}");
                return;
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, targetFBO);
            GL.Viewport(0, 0, width, height);

            // Ultra-fast blit
            GL.UseProgram(_blitShader.Handle);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _historyTexture);
            GL.Uniform1(_blitTexLoc, 0);

            // Single draw call
            GL.BindVertexArray(_quadVAO);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);

            // Cleanup
            GL.BindVertexArray(0);
            GL.UseProgram(0);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        /// <summary>
        /// Resize TAA buffers.
        /// </summary>
        public void Resize(int width, int height)
        {
            if (width == _width && height == _height)
                return;

            _width = width;
            _height = height;

            // Delete old resources
            if (_historyTexture != 0)
                GL.DeleteTexture(_historyTexture);
            if (_historyFBO != 0)
                GL.DeleteFramebuffer(_historyFBO);

            // Recreate
            CreateResources();

            // Reset state
            _isFirstFrame = true;
            _frameIndex = 0;

            Console.WriteLine($"[TAA] Resized to {width}x{height}");
        }

        /// <summary>
        /// Reset TAA history (call when camera cuts or scene changes dramatically).
        /// </summary>
        public void ResetHistory()
        {
            _isFirstFrame = true;
            _frameIndex = 0;
            Console.WriteLine("[TAA] History reset");
        }

        public void Dispose()
        {
            if (_historyTexture != 0)
                GL.DeleteTexture(_historyTexture);
            if (_historyFBO != 0)
                GL.DeleteFramebuffer(_historyFBO);
            if (_quadVAO != 0)
                GL.DeleteVertexArray(_quadVAO);
            if (_quadVBO != 0)
                GL.DeleteBuffer(_quadVBO);

            _taaShader?.Dispose();
            _blitShader?.Dispose();
        }
    }
}

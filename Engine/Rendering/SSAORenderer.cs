using System;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace Engine.Rendering
{
    /// <summary>
    /// SSAO (Screen Space Ambient Occlusion) renderer with multi-pass implementation.
    /// Supports geometry pass, occlusion calculation, and bilateral blur.
    /// </summary>
    public sealed class SSAORenderer : IDisposable
    {
        // One-shot guard for DebugLogCenterPixel to avoid per-frame spam
        private static bool _debugLogCenterPrinted = false;
        // SSAO parameters - simplified and cleaned up
        public struct SSAOSettings
        {
            public bool Enabled;
            public float Radius;        // World-space sampling radius (default: 0.5f)
            public float Bias;          // Self-occlusion bias (default: 0.025f)
            public float Intensity;     // Power curve for intensity (default: 1.0f)
            public int SampleCount;     // Number of samples (4, 8, 16, 32, 64, 128)
            public int BlurSize;        // Blur kernel radius in pixels (default: 2)
            public float Strength;      // Final SSAO strength multiplier (default: 1.0f)

            public static SSAOSettings Default => new SSAOSettings
            {
                Enabled = true,
                Radius = 0.5f,       // View-space radius (good default for most scenes)
                Bias = 0.025f,       // Prevents self-shadowing artifacts
                Intensity = 1.0f,    // Linear intensity
                SampleCount = 64,    // High quality
                BlurSize = 4,        // QUALITY: 9x9 bilateral blur (was 2)
                Strength = 1.0f      // Full strength
            };
        }

        // Framebuffers and textures
        private uint _geometryFBO;
        private uint _ssaoFBO;
        private uint _blurFBO;

        private uint _positionTex;      // View space positions
        private uint _normalTex;        // View space normals
        private uint _depthTex;         // Linear depth
        private uint _ssaoTex;          // Raw SSAO values
        private uint _blurredSSAOTex;   // Blurred SSAO

        // SSAO kernel and noise texture
        private uint _noiseTexture;     // 64x64 random rotation vectors (high quality, minimal tiling)
        private Vector3[] _ssaoKernel;  // Pre-generated hemisphere samples
        private bool _ssaoKernelUploaded = false; // Quick Win #3: Track if kernel has been uploaded

        // Shaders
        private ShaderProgram _geometryShader = null!;
        private ShaderProgram _ssaoShader = null!;
    private ShaderProgram? _ssaoPOCShader = null;
        private ShaderProgram _blurShader = null!;

        // Screen quad for full-screen passes
        private uint _quadVAO;
        private uint _quadVBO;

        // Render dimensions
        private int _width;
        private int _height;

        // PERFORMANCE: SSAO half-resolution (2x-4x faster, minimal quality loss)
        private int _ssaoWidth;
        private int _ssaoHeight;

        public SSAOSettings Settings { get; set; } = SSAOSettings.Default;

        public SSAORenderer(int width, int height)
        {
            _width = width;
            _height = height;

            // PERFORMANCE: Calculate half-resolution for SSAO (round down to even numbers)
            _ssaoWidth = Math.Max(1, (_width / 2) & ~1);
            _ssaoHeight = Math.Max(1, (_height / 2) & ~1);

            _ssaoKernel = new Vector3[128]; // Increased to support up to 128 samples
            Initialize();
        }

        private void Initialize()
        {
            GenerateSSAOKernel();
            CreateNoiseTexture();
            CreateFramebuffers();
            CreateShaders();
            CreateScreenQuad();
        }

        private void CreateFramebuffers()
        {
            // Geometry pass FBO (positions, normals, depth)
            _geometryFBO = (uint)GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _geometryFBO);

            // Position texture (RGB16F - optimisé pour performance, réduit 50% bande passante vs RGB32F)
            _positionTex = (uint)GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _positionTex);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgb16f, _width, _height, 0, PixelFormat.Rgb, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _positionTex, 0);

            // Normal texture (RGB16F)
            _normalTex = (uint)GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _normalTex);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgb16f, _width, _height, 0, PixelFormat.Rgb, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1, TextureTarget.Texture2D, _normalTex, 0);

            // Depth texture
            _depthTex = (uint)GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _depthTex);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent32f, _width, _height, 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, _depthTex, 0);

            // Set draw buffers
            DrawBuffersEnum[] drawBuffers = { DrawBuffersEnum.ColorAttachment0, DrawBuffersEnum.ColorAttachment1 };
            GL.DrawBuffers(2, drawBuffers);

            if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
                throw new Exception("Geometry framebuffer not complete!");

            // SSAO FBO (PERFORMANCE: Half-resolution for 2-4× speedup)
            _ssaoFBO = (uint)GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _ssaoFBO);

            _ssaoTex = (uint)GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _ssaoTex);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.R16f, _ssaoWidth, _ssaoHeight, 0, PixelFormat.Red, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _ssaoTex, 0);

            if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
                throw new Exception("SSAO framebuffer not complete!");

            // Blur FBO (PERFORMANCE: Also half-resolution)
            _blurFBO = (uint)GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _blurFBO);

            _blurredSSAOTex = (uint)GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _blurredSSAOTex);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.R16f, _ssaoWidth, _ssaoHeight, 0, PixelFormat.Red, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _blurredSSAOTex, 0);

            if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
                throw new Exception("Blur framebuffer not complete!");

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        private void CreateShaders()
        {
            _geometryShader = ShaderProgram.FromFiles(
                "Engine/Rendering/Shaders/SSAO/SSAOGeometry.vert",
                "Engine/Rendering/Shaders/SSAO/SSAOGeometry.frag");

            _ssaoShader = ShaderProgram.FromFiles(
                "Engine/Rendering/Shaders/SSAO/SSAOCalc.vert",
                "Engine/Rendering/Shaders/SSAO/SSAOCalc.frag");

            // Minimal POC SSAO shader for fast testing
            try
            {
                _ssaoPOCShader = ShaderProgram.FromFiles(
                    "Engine/Rendering/Shaders/SSAO/SSAOCalc.vert",
                    "Engine/Rendering/Shaders/SSAO/SSAO_POC.frag");
            }
            catch (Exception)
            {
                _ssaoPOCShader = null;
            }

            _blurShader = ShaderProgram.FromFiles(
                "Engine/Rendering/Shaders/SSAO/SSAOBlur.vert",
                "Engine/Rendering/Shaders/SSAO/SSAOBlur.frag");

        }

        /// <summary>
        /// Generate hemisphere-oriented SSAO sample kernel following LearnOpenGL approach
        /// </summary>
        private void GenerateSSAOKernel()
        {
            var random = new Random();
            _ssaoKernel = new Vector3[128]; // Increased to 128 samples

            for (int i = 0; i < 128; i++)
            {
                // Generate random sample in hemisphere (z > 0)
                Vector3 sample = new Vector3(
                    (float)(random.NextDouble() * 2.0 - 1.0), // x: [-1, 1]
                    (float)(random.NextDouble() * 2.0 - 1.0), // y: [-1, 1]
                    (float)random.NextDouble()                 // z: [0, 1] (hemisphere)
                );

                sample = Vector3.Normalize(sample);
                sample *= (float)random.NextDouble(); // Random distance

                // Scale samples so more are closer to the origin (lerp technique)
                float scale = (float)i / 128.0f;
                scale = Lerp(0.1f, 1.0f, scale * scale);
                sample *= scale;

                _ssaoKernel[i] = sample;
            }

        }

        /// <summary>
        /// Create 64x64 high-quality noise texture for random kernel rotations
        /// QUALITY IMPROVEMENT: 64x64 instead of 4x4 to reduce visible tiling artifacts
        /// Modern approach used in AAA games for smooth SSAO without banding
        /// </summary>
        private void CreateNoiseTexture()
        {
            const int noiseSize = 64; // QUALITY: 64x64 for minimal tiling (was 4x4)
            var random = new Random(12345); // Fixed seed for consistency
            Vector3[] ssaoNoise = new Vector3[noiseSize * noiseSize];

            for (int i = 0; i < ssaoNoise.Length; i++)
            {
                // Generate random vector in XY plane (tangent space rotation)
                // Z component MUST be 0 (rotation around surface normal)
                ssaoNoise[i] = new Vector3(
                    (float)(random.NextDouble() * 2.0 - 1.0),  // x: [-1, 1]
                    (float)(random.NextDouble() * 2.0 - 1.0),  // y: [-1, 1]
                    0.0f                                         // z: 0 (no rotation around XY)
                );
                // No normalization needed - will be normalized in shader
            }

            _noiseTexture = (uint)GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _noiseTexture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgb16f, noiseSize, noiseSize, 0, PixelFormat.Rgb, PixelType.Float, ssaoNoise);
            // Use Linear filtering for smoother noise transitions
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
        }

        private static float Lerp(float a, float b, float f) => a + f * (b - a);

        private void CreateScreenQuad()
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

            _quadVAO = (uint)GL.GenVertexArray();
            _quadVBO = (uint)GL.GenBuffer();

            GL.BindVertexArray(_quadVAO);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _quadVBO);
            GL.BufferData(BufferTarget.ArrayBuffer, quadVertices.Length * sizeof(float), quadVertices, BufferUsageHint.StaticDraw);

            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));

            GL.BindVertexArray(0);
        }

        public void Resize(int width, int height)
        {
            if (_width == width && _height == height) return;

            _width = width;
            _height = height;

            // PERFORMANCE: Recalculate half-resolution dimensions
            _ssaoWidth = Math.Max(1, (_width / 2) & ~1);
            _ssaoHeight = Math.Max(1, (_height / 2) & ~1);

            // Recreate textures with new size
            GL.DeleteTexture(_positionTex);
            GL.DeleteTexture(_normalTex);
            GL.DeleteTexture(_depthTex);
            GL.DeleteTexture(_ssaoTex);
            GL.DeleteTexture(_blurredSSAOTex);

            CreateFramebuffers();
        }

        /// <summary>
        /// Render geometry pass (positions and normals to G-buffer)
        /// </summary>
        public void BeginGeometryPass()
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _geometryFBO);
            GL.Viewport(0, 0, _width, _height);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            _geometryShader.Use();
        }

        public void EndGeometryPass()
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        /// <summary>
        /// Calculate SSAO values using screen-space sampling
        /// </summary>
        public void RenderSSAO(Matrix4 viewMatrix, Matrix4 projMatrix, float nearPlane, float farPlane)
        {
            if (!Settings.Enabled) return;

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _ssaoFBO);
            // PERFORMANCE: Render SSAO at half-resolution for 2-4× speedup
            GL.Viewport(0, 0, _ssaoWidth, _ssaoHeight);
            GL.Clear(ClearBufferMask.ColorBufferBit);

            // If user uses a very small sample count, switch to the POC shader for quick testing
            var usePOC = Settings.SampleCount <= 8 && _ssaoPOCShader != null;
            var shaderToUse = usePOC ? _ssaoPOCShader! : _ssaoShader;
            shaderToUse.Use();

            // Bind G-buffer textures
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _positionTex);
            _ssaoShader.SetInt("u_PositionTex", 0);

            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, _normalTex);
            _ssaoShader.SetInt("u_NormalTex", 1);

            GL.ActiveTexture(TextureUnit.Texture2);
            GL.BindTexture(TextureTarget.Texture2D, _noiseTexture);
            _ssaoShader.SetInt("u_NoiseTex", 2);

            // Set SSAO parameters
            shaderToUse.SetFloat("u_SSAORadius", Settings.Radius);
            shaderToUse.SetFloat("u_SSAOBias", Settings.Bias);
            shaderToUse.SetFloat("u_SSAOIntensity", Settings.Intensity);
            shaderToUse.SetInt("u_SSAOSamples", Settings.SampleCount);
            // PERFORMANCE: Pass half-resolution size to match the actual SSAO render target
            shaderToUse.SetVec2("u_ScreenSize", new Vector2(_ssaoWidth, _ssaoHeight));

            // Set projection matrix (view-space to clip-space)
            shaderToUse.SetMat4("u_ProjMatrix", projMatrix);

            // Quick Win #3: Upload SSAO kernel samples only once (they never change)
            if (!_ssaoKernelUploaded && !usePOC)
            {
                for (int i = 0; i < _ssaoKernel.Length; i++)
                {
                    _ssaoShader.SetVec3($"u_Samples[{i}]", _ssaoKernel[i]);
                }
                _ssaoKernelUploaded = true;
            }

            RenderQuad();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        /// <summary>
        /// Apply bilateral blur to SSAO - preserves edges while removing noise
        /// QUALITY IMPROVEMENT: Edge-aware blur using depth buffer
        /// </summary>
        public void BlurSSAO()
        {
            if (!Settings.Enabled) return;

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _blurFBO);
            // PERFORMANCE: Blur at half-resolution too
            GL.Viewport(0, 0, _ssaoWidth, _ssaoHeight);
            GL.Clear(ClearBufferMask.ColorBufferBit);

            _blurShader.Use();

            // Bind SSAO texture
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _ssaoTex);
            _blurShader.SetInt("u_SSAOTex", 0);

            // Bind depth texture for edge detection (bilateral blur)
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, _depthTex);
            _blurShader.SetInt("u_DepthTex", 1);

            // PERFORMANCE: Use SSAO resolution (half-res) for texel size
            _blurShader.SetVec2("u_TexelSize", new Vector2(1.0f / _ssaoWidth, 1.0f / _ssaoHeight));
            _blurShader.SetInt("u_BlurSize", Settings.BlurSize);

            RenderQuad();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        private void RenderQuad()
        {
            GL.BindVertexArray(_quadVAO);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
            GL.BindVertexArray(0);
        }

        /// <summary>
        /// Get the final SSAO texture for use in lighting
        /// </summary>
        public uint GetSSAOTexture()
        {
            return Settings.Enabled ? _blurredSSAOTex : 0; // Use blurred SSAO texture
        }

        // Expose G-buffer textures for debugging / visualization
        public uint GetPositionTexture() => _positionTex;
        public uint GetNormalTexture() => _normalTex;
        public uint GetDepthTexture() => _depthTex;
        public uint GetBlurredSSAOTex() => _blurredSSAOTex;

        /// <summary>
        /// Debug helper: read a single pixel from the geometry FBO position attachment (world-space position)
        /// Coordinates are in framebuffer space (0..width-1, 0..height-1)
        /// </summary>
        public Vector3 ReadPositionPixel(int x, int y)
        {
            // Clamp
            x = Math.Clamp(x, 0, _width - 1);
            y = Math.Clamp(y, 0, _height - 1);

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _geometryFBO);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            float[] pixel = new float[3];
            GL.ReadPixels(x, y, 1, 1, PixelFormat.Rgb, PixelType.Float, pixel);
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0);
            return new Vector3(pixel[0], pixel[1], pixel[2]);
        }

        /// <summary>
        /// Debug helper: read a single pixel from the geometry FBO normal attachment (world-space normal)
        /// </summary>
        public Vector3 ReadNormalPixel(int x, int y)
        {
            x = Math.Clamp(x, 0, _width - 1);
            y = Math.Clamp(y, 0, _height - 1);

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _geometryFBO);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment1);
            float[] pixel = new float[3];
            GL.ReadPixels(x, y, 1, 1, PixelFormat.Rgb, PixelType.Float, pixel);
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0);
            return new Vector3(pixel[0], pixel[1], pixel[2]);
        }

        /// <summary>
        /// Get geometry shader for external use
        /// </summary>
        public ShaderProgram GetGeometryShader()
        {
            return _geometryShader;
        }

        /// <summary>
        /// Debug: sample center pixel and log geometry + SSAO values when SSAO_DEBUG=1
        /// </summary>
        public void DebugLogCenterPixel()
        {
            var dbg = Environment.GetEnvironmentVariable("SSAO_DEBUG");
            // Default behavior: only print once to avoid per-frame spam. To force
            // continuous printing set SSAO_DEBUG to "1:FORCE".
            if (string.IsNullOrEmpty(dbg) || (!dbg.StartsWith("1") && !dbg.StartsWith("1:FORCE"))) return;
            if (!dbg.Contains(":FORCE"))
            {
                // If we've already printed once, skip further prints
                if (_debugLogCenterPrinted) return;
            }

            int cx = _width / 2;
            int cy = _height / 2;
            var pos = ReadPositionPixel(cx, cy);
            var n = ReadNormalPixel(cx, cy);

            // Read final SSAO (blurred) safely by reading the blur FBO's attachment at cx,cy
            try
            {
                float[] ssaoVal = new float[1];
                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _blurFBO);
                GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
                GL.ReadPixels(cx, cy, 1, 1, PixelFormat.Red, PixelType.Float, ssaoVal);
                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0);
                Console.WriteLine($"[SSAODebug] center pos={pos}, normal={n}, ssao={ssaoVal[0]}");
                // Mark as printed to prevent further spam
                _debugLogCenterPrinted = true;
            }
            catch (Exception ex)
            {
                try { Console.WriteLine("[SSAODebug] Failed to read blurred SSAO pixel: " + ex.Message); } catch { }
            }
        }

        public void Dispose()
        {
            GL.DeleteFramebuffer(_geometryFBO);
            GL.DeleteFramebuffer(_ssaoFBO);
            GL.DeleteFramebuffer(_blurFBO);

            GL.DeleteTexture(_positionTex);
            GL.DeleteTexture(_normalTex);
            GL.DeleteTexture(_depthTex);
            GL.DeleteTexture(_ssaoTex);
            GL.DeleteTexture(_blurredSSAOTex);
            GL.DeleteTexture(_noiseTexture);

            GL.DeleteVertexArray(_quadVAO);
            GL.DeleteBuffer(_quadVBO);

            _geometryShader?.Dispose();
            _ssaoShader?.Dispose();
            _blurShader?.Dispose();
        }
    }
}
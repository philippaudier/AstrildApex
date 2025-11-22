using System;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace Engine.Rendering
{
    // Simple PMREM generator: creates irradiance (diffuse) and prefiltered specular env maps
    public sealed class PMREMGenerator : IDisposable
    {
        private readonly int _cubeVao;
        private readonly int _cubeVbo;
        private ShaderProgram? _irradianceShader;
        private ShaderProgram? _prefilterShader;

        public PMREMGenerator()
        {
            // Create cube VAO/VBO
            float[] skyboxVerts = new float[] {
                -1.0f,  1.0f, -1.0f,
                -1.0f, -1.0f, -1.0f,
                 1.0f, -1.0f, -1.0f,
                 1.0f, -1.0f, -1.0f,
                 1.0f,  1.0f, -1.0f,
                -1.0f,  1.0f, -1.0f,

                -1.0f, -1.0f,  1.0f,
                -1.0f, -1.0f, -1.0f,
                -1.0f,  1.0f, -1.0f,
                -1.0f,  1.0f, -1.0f,
                -1.0f,  1.0f,  1.0f,
                -1.0f, -1.0f,  1.0f,

                 1.0f, -1.0f, -1.0f,
                 1.0f, -1.0f,  1.0f,
                 1.0f,  1.0f,  1.0f,
                 1.0f,  1.0f,  1.0f,
                 1.0f,  1.0f, -1.0f,
                 1.0f, -1.0f, -1.0f,

                -1.0f, -1.0f,  1.0f,
                -1.0f,  1.0f,  1.0f,
                 1.0f,  1.0f,  1.0f,
                 1.0f,  1.0f,  1.0f,
                 1.0f, -1.0f,  1.0f,
                -1.0f, -1.0f,  1.0f,

                -1.0f,  1.0f, -1.0f,
                 1.0f,  1.0f, -1.0f,
                 1.0f,  1.0f,  1.0f,
                 1.0f,  1.0f,  1.0f,
                -1.0f,  1.0f,  1.0f,
                -1.0f,  1.0f, -1.0f,

                -1.0f, -1.0f, -1.0f,
                -1.0f, -1.0f,  1.0f,
                 1.0f, -1.0f, -1.0f,
                 1.0f, -1.0f, -1.0f,
                -1.0f, -1.0f,  1.0f,
                 1.0f, -1.0f,  1.0f
            };

            _cubeVao = GL.GenVertexArray();
            _cubeVbo = GL.GenBuffer();
            GL.BindVertexArray(_cubeVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _cubeVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, skyboxVerts.Length * sizeof(float), skyboxVerts, BufferUsageHint.StaticDraw);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
            GL.BindVertexArray(0);

            try
            {
                _irradianceShader = ShaderProgram.FromFiles("Engine/Rendering/Shaders/pmrem_conv.vert", "Engine/Rendering/Shaders/pmrem_conv.frag");
            }
            catch (Exception ex)
            {
                try { Console.WriteLine($"[PMREMGenerator] Failed to compile irradiance shader: {ex.Message}"); } catch { }
                _irradianceShader = null;
            }

            try
            {
                _prefilterShader = ShaderProgram.FromFiles("Engine/Rendering/Shaders/pmrem_conv.vert", "Engine/Rendering/Shaders/prefilter_specular.frag");
            }
            catch (Exception ex)
            {
                try { Console.WriteLine($"[PMREMGenerator] Failed to compile prefilter shader: {ex.Message}"); } catch { }
                _prefilterShader = null;
            }
            try
            {
                if (_irradianceShader != null || _prefilterShader != null) {
                    try { Console.WriteLine("[PMREMGenerator] PMREM generator initialized successfully (shaders compiled)"); } catch { }
                }
            } catch { }
        }

        public uint GenerateIrradiance(uint sourceCubemap, int sampleSize = 64)
        {
            if (_irradianceShader == null || sourceCubemap == 0) return 0;
            try { Console.WriteLine($"[PMREMGenerator] Generating irradiance {sampleSize}x{sampleSize} from source={sourceCubemap}"); } catch { }

            int handle = GL.GenTexture();
            GL.BindTexture(TextureTarget.TextureCubeMap, handle);
            for (int f = 0; f < 6; f++)
            {
                GL.TexImage2D(TextureTarget.TextureCubeMapPositiveX + f, 0, PixelInternalFormat.Rgb16f, sampleSize, sampleSize, 0, PixelFormat.Rgb, PixelType.HalfFloat, IntPtr.Zero);
            }
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);

            int fbo = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
            int vpSize = sampleSize;

            // Precompute view/proj matrices for cube faces
            var captureProjection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(90f), 1f, 0.1f, 10f);
            var captureViews = new Matrix4[] {
                Matrix4.LookAt(Vector3.Zero, new Vector3(1,0,0), new Vector3(0,-1,0)),
                Matrix4.LookAt(Vector3.Zero, new Vector3(-1,0,0), new Vector3(0,-1,0)),
                Matrix4.LookAt(Vector3.Zero, new Vector3(0,1,0), new Vector3(0,0,1)),
                Matrix4.LookAt(Vector3.Zero, new Vector3(0,-1,0), new Vector3(0,0,-1)),
                Matrix4.LookAt(Vector3.Zero, new Vector3(0,0,1), new Vector3(0,-1,0)),
                Matrix4.LookAt(Vector3.Zero, new Vector3(0,0,-1), new Vector3(0,-1,0)),
            };

            GL.Viewport(0, 0, vpSize, vpSize);
            _irradianceShader.Use();
            _irradianceShader.SetInt("u_EnvMap", 0);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.TextureCubeMap, (int)sourceCubemap);

            // CRITICAL: Ensure source cubemap can sample mipmaps for blurred irradiance
            // Force mipmap filtering so textureLod() in shader can access blurred mip levels
            try
            {
                GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
                GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            }
            catch { }

            // Calculate max LOD of source cubemap for shader
            GL.BindTexture(TextureTarget.TextureCubeMap, (int)sourceCubemap);
            GL.GetTexLevelParameter(TextureTarget.TextureCubeMapPositiveX, 0, GetTextureParameter.TextureWidth, out int sourceWidth);
            float sourceMaxLod = (float)Math.Floor(Math.Log(sourceWidth, 2));

            // DEBUG: Check if mipmaps exist by querying mip 1
            GL.GetTexLevelParameter(TextureTarget.TextureCubeMapPositiveX, 1, GetTextureParameter.TextureWidth, out int mip1Width);
            try { Console.WriteLine($"[PMREMGenerator] Source cubemap: size={sourceWidth}x{sourceWidth}, maxLod={sourceMaxLod}, mip1={mip1Width}x{mip1Width}"); } catch { }

            // Render to each face
            GL.BindVertexArray(_cubeVao);
            for (int face = 0; face < 6; face++)
            {
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.TextureCubeMapPositiveX + face, handle, 0);
                var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
                if (status != FramebufferErrorCode.FramebufferComplete) continue;

                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                _irradianceShader.Use();
                _irradianceShader.SetMat4("uProjection", captureProjection);
                _irradianceShader.SetMat4("uView", captureViews[face]);
                // Pass max LOD to shader so it can sample the most blurred mip level
                _irradianceShader.SetFloat("u_PrefilterMaxLod", sourceMaxLod);
                // Use 1024 samples for ultra-smooth irradiance (eliminates white spots from sun/bright lights)
                // Higher sample count = better convolution quality for diffuse lighting
                _irradianceShader.SetInt("u_SampleCount", 1024);
                GL.DrawArrays(PrimitiveType.Triangles, 0, 36);
            }
            GL.BindVertexArray(0);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.DeleteFramebuffer(fbo);

            // Keep reasonable min filter
            GL.BindTexture(TextureTarget.TextureCubeMap, handle);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.BindTexture(TextureTarget.TextureCubeMap, 0);

            return (uint)handle;
        }

        public uint GeneratePrefilteredEnv(uint sourceCubemap, int baseSize = 512)
        {
            if (_prefilterShader == null || sourceCubemap == 0) return 0;
            try { Console.WriteLine($"[PMREMGenerator] Generating prefiltered env baseSize={baseSize} from source={sourceCubemap}"); } catch { }

            int handle = GL.GenTexture();
            GL.BindTexture(TextureTarget.TextureCubeMap, handle);

            int mipCount = (int)Math.Floor(Math.Log(baseSize, 2)) + 1;
            for (int mip = 0; mip < mipCount; mip++)
            {
                int mipW = Math.Max(1, baseSize >> mip);
                int mipH = mipW;
                for (int face = 0; face < 6; face++)
                {
                    GL.TexImage2D(TextureTarget.TextureCubeMapPositiveX + face, mip, PixelInternalFormat.Rgb16f,
                                  mipW, mipH, 0, PixelFormat.Rgb, PixelType.HalfFloat, IntPtr.Zero);
                }
            }

            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);

            // Ensure seamless cubemap filtering is enabled while working with cubemaps
            try { GL.Enable(EnableCap.TextureCubeMapSeamless); } catch { }

            int fbo = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);

            var captureProjection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(90f), 1f, 0.1f, 10f);
            var captureViews = new Matrix4[] {
                Matrix4.LookAt(Vector3.Zero, new Vector3(1,0,0), new Vector3(0,-1,0)),
                Matrix4.LookAt(Vector3.Zero, new Vector3(-1,0,0), new Vector3(0,-1,0)),
                Matrix4.LookAt(Vector3.Zero, new Vector3(0,1,0), new Vector3(0,0,1)),
                Matrix4.LookAt(Vector3.Zero, new Vector3(0,-1,0), new Vector3(0,0,-1)),
                Matrix4.LookAt(Vector3.Zero, new Vector3(0,0,1), new Vector3(0,-1,0)),
                Matrix4.LookAt(Vector3.Zero, new Vector3(0,0,-1), new Vector3(0,-1,0)),
            };

            _prefilterShader.Use();
            _prefilterShader.SetInt("u_EnvMap", 0);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.TextureCubeMap, (int)sourceCubemap);

            GL.BindVertexArray(_cubeVao);
            for (int mip = 0; mip < mipCount; mip++)
            {
                int mipW = Math.Max(1, baseSize >> mip);
                GL.Viewport(0, 0, mipW, mipW);
                float roughness = (float)mip / Math.Max(1, mipCount - 1);
                _prefilterShader.SetFloat("u_Roughness", roughness);
                // Increase sample count for higher mip levels (higher roughness) to reduce blockiness.
                // Linear ramp with a cap is used to avoid runaway sample counts on many-mip environments.
                int sampleCount = Math.Min(2048, 32 + mip * 128);
                if (sampleCount < 32) sampleCount = 32;
                _prefilterShader.SetInt("u_SampleCount", sampleCount);

                // Emit debug info so we can verify the sample budget chosen per-mip at runtime
                try { Console.WriteLine($"[PMREMGenerator] mip={mip} mipW={mipW} roughness={roughness:F3} samples={sampleCount}"); } catch { }

                for (int face = 0; face < 6; face++)
                {
                    GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.TextureCubeMapPositiveX + face, handle, mip);
                    var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
                    if (status != FramebufferErrorCode.FramebufferComplete) continue;

                    GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                    _prefilterShader.SetMat4("uProjection", captureProjection);
                    _prefilterShader.SetMat4("uView", captureViews[face]);
                    _prefilterShader.SetInt("u_SampleCount", sampleCount);

                    GL.DrawArrays(PrimitiveType.Triangles, 0, 36);
                }
            }
            GL.BindVertexArray(0);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.DeleteFramebuffer(fbo);

            // Inform SkyboxRenderer about prefilter mip count so shaders can use correct LOD range
            try
            {
                Engine.Rendering.SkyboxRenderer.PrefilterMaxLod = Math.Max(0.0f, (float)(mipCount - 1));
            }
            catch { }

            // Make sure the cubemap texture reports the correct max mip level to the GL sampler
            try
            {
                GL.BindTexture(TextureTarget.TextureCubeMap, handle);
                GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMaxLevel, mipCount - 1);
                GL.BindTexture(TextureTarget.TextureCubeMap, 0);
            }
            catch { }

            try {
                try { Console.WriteLine($"[PMREMGenerator] Generated prefiltered cubemap handle={handle}, mipCount={mipCount}, PrefilterMaxLod={Engine.Rendering.SkyboxRenderer.PrefilterMaxLod}"); } catch { }
            } catch { }

            return (uint)handle;
        }

        public void Dispose()
        {
            try { if (_irradianceShader != null) _irradianceShader.Dispose(); } catch { }
            try { if (_prefilterShader != null) _prefilterShader.Dispose(); } catch { }
            try { if (_cubeVao != 0) GL.DeleteVertexArray(_cubeVao); } catch { }
            try { if (_cubeVbo != 0) GL.DeleteBuffer(_cubeVbo); } catch { }
        }
    }
}

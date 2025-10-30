using System;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace Engine.Rendering.Shadows
{
    /// <summary>
    /// Modern, clean shadow mapping implementation for directional lights.
    /// Supports single shadow map with three quality modes:
    /// - PCF Grid (basic, fast, robust)
    /// - PCF Poisson Disk (better quality, same cost)
    /// - PCSS (soft shadows with contact hardening)
    /// </summary>
    public class ShadowManager : IDisposable
    {
        // Shadow map texture and framebuffer
        private int _shadowFBO = 0;
        private int _shadowTexture = 0;
        private int _shadowMapSize = 2048;

        // Light-space transformation matrix
        private Matrix4 _lightSpaceMatrix = Matrix4.Identity;

        // Shadow quality mode
        public enum ShadowQuality
        {
            PCF_Grid = 0,       // Basic PCF with grid sampling
            PCF_PoissonDisk = 1, // Better PCF with Poisson disk sampling
            PCSS = 2            // Percentage Closer Soft Shadows
        }

        public int ShadowTexture => _shadowTexture;
        public int ShadowMapSize => _shadowMapSize;
        public Matrix4 LightSpaceMatrix => _lightSpaceMatrix;

        public ShadowManager(int shadowMapSize = 2048)
        {
            _shadowMapSize = Math.Clamp(shadowMapSize, 512, 8192);
            CreateShadowResources();
        }

        private void CreateShadowResources()
        {
            // Create framebuffer for shadow rendering
            _shadowFBO = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _shadowFBO);

            // Create depth texture for shadow map
            _shadowTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _shadowTexture);

            // Use DepthComponent32F for better precision
            GL.TexImage2D(
                TextureTarget.Texture2D,
                0,
                PixelInternalFormat.DepthComponent32f,
                _shadowMapSize,
                _shadowMapSize,
                0,
                PixelFormat.DepthComponent,
                PixelType.Float,
                IntPtr.Zero
            );

            // Enable hardware shadow comparison (sampler2DShadow in GLSL)
            GL.TexParameter(TextureTarget.Texture2D, (TextureParameterName)All.TextureCompareMode, (int)All.CompareRefToTexture);
            GL.TexParameter(TextureTarget.Texture2D, (TextureParameterName)All.TextureCompareFunc, (int)All.Lequal);

            // Linear filtering for smoother shadows
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

            // Clamp to border with white color (areas outside shadow map = fully lit)
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
            float[] borderColor = { 1.0f, 1.0f, 1.0f, 1.0f };
            GL.TexParameter(TextureTarget.Texture2D, (TextureParameterName)All.TextureBorderColor, borderColor);

            // Attach depth texture to framebuffer
            GL.FramebufferTexture2D(
                FramebufferTarget.Framebuffer,
                FramebufferAttachment.DepthAttachment,
                TextureTarget.Texture2D,
                _shadowTexture,
                0
            );

            // We don't need color output for shadow pass
            GL.DrawBuffer(DrawBufferMode.None);
            GL.ReadBuffer(ReadBufferMode.None);

            // Verify framebuffer is complete
            var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != FramebufferErrorCode.FramebufferComplete)
            {
                throw new Exception($"Shadow framebuffer incomplete: {status}");
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        /// <summary>
        /// Begin rendering to the shadow map.
        /// Call this before rendering your scene from the light's perspective.
        /// </summary>
        public void BeginShadowPass()
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _shadowFBO);
            GL.Viewport(0, 0, _shadowMapSize, _shadowMapSize);
            GL.Clear(ClearBufferMask.DepthBufferBit);

            // Enable depth testing
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Less);

            // Enable front-face culling to reduce peter-panning
            // This renders only back faces to the shadow map
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(TriangleFace.Front);
        }

        /// <summary>
        /// End shadow rendering and restore previous state.
        /// </summary>
        public void EndShadowPass()
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            // Restore back-face culling
            GL.CullFace(TriangleFace.Back);
        }

        /// <summary>
        /// Calculate light-space matrix for directional light.
        /// This creates an orthographic projection that encompasses the scene.
        /// </summary>
        /// <param name="lightDirection">Direction the light is pointing (normalized)</param>
        /// <param name="sceneCenter">Center of the scene to shadow</param>
        /// <param name="sceneRadius">Radius of the scene to shadow</param>
        public void CalculateLightMatrix(Vector3 lightDirection, Vector3 sceneCenter, float sceneRadius)
        {
            // Normalize light direction
            lightDirection = Vector3.Normalize(lightDirection);

            // Position light far enough to encompass the scene
            Vector3 lightPos = sceneCenter - lightDirection * sceneRadius * 2.0f;

            // Create view matrix looking from light toward scene center
            Matrix4 lightView = Matrix4.LookAt(lightPos, sceneCenter, Vector3.UnitY);

            // Create orthographic projection that encompasses the scene
            // Use symmetric frustum centered on scene
            float orthoSize = sceneRadius * 1.5f; // 1.5x for margin
            Matrix4 lightProjection = Matrix4.CreateOrthographic(
                orthoSize * 2.0f,  // width
                orthoSize * 2.0f,  // height
                0.1f,              // near plane
                sceneRadius * 4.0f // far plane
            );

            // Combine view and projection
            _lightSpaceMatrix = lightView * lightProjection;
        }

        /// <summary>
        /// Bind shadow map texture to specified texture unit for sampling in shaders.
        /// </summary>
        public void BindShadowTexture(TextureUnit textureUnit)
        {
            GL.ActiveTexture(textureUnit);
            GL.BindTexture(TextureTarget.Texture2D, _shadowTexture);
        }

        public void Resize(int newSize)
        {
            if (newSize == _shadowMapSize) return;

            _shadowMapSize = Math.Clamp(newSize, 512, 8192);

            // Recreate resources with new size
            Dispose();
            CreateShadowResources();
        }

        public void Dispose()
        {
            if (_shadowTexture != 0)
            {
                GL.DeleteTexture(_shadowTexture);
                _shadowTexture = 0;
            }

            if (_shadowFBO != 0)
            {
                GL.DeleteFramebuffer(_shadowFBO);
                _shadowFBO = 0;
            }
        }
    }
}

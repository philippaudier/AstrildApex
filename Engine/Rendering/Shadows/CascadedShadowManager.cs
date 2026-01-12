using System;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace Engine.Rendering.Shadows
{
    /// <summary>
    /// Cascaded Shadow Maps (CSM) implementation for high-quality directional light shadows.
    /// Uses multiple shadow maps at different resolutions to provide detailed shadows
    /// near the camera and broader coverage for distant objects.
    ///
    /// Based on GPU Gems 3, Chapter 10: "Parallel-Split Shadow Maps on Programmable GPUs"
    /// </summary>
    public class CascadedShadowManager : IDisposable
    {
        // ============================================================================
        // CONFIGURATION
        // ============================================================================

        /// <summary>Number of shadow map cascades (typically 3-4)</summary>
        public const int CASCADE_COUNT = 4;

        /// <summary>Resolution of each cascade shadow map</summary>
        private int _shadowMapSize = 2048;

        /// <summary>Lambda for cascade split calculation (0 = linear, 1 = logarithmic)</summary>
        private float _cascadeSplitLambda = 0.75f;

        // ============================================================================
        // GPU RESOURCES
        // ============================================================================

        /// <summary>Framebuffers for each cascade</summary>
        private int[] _cascadeFBOs = new int[CASCADE_COUNT];

        /// <summary>Depth texture array containing all cascades</summary>
        private int _shadowTextureArray = 0;

        /// <summary>Light-space matrices for each cascade</summary>
        private Matrix4[] _lightSpaceMatrices = new Matrix4[CASCADE_COUNT];

        /// <summary>Cascade split distances (in view space, negative Z)</summary>
        private float[] _cascadeSplits = new float[CASCADE_COUNT + 1];

        /// <summary>Cascade far planes for shader (positive values for easy comparison)</summary>
        private float[] _cascadePlaneDistances = new float[CASCADE_COUNT];

        // ============================================================================
        // PUBLIC PROPERTIES
        // ============================================================================

        public int ShadowTextureArray => _shadowTextureArray;
        public int ShadowMapSize => _shadowMapSize;
        public Matrix4[] LightSpaceMatrices => _lightSpaceMatrices;
        public float[] CascadePlaneDistances => _cascadePlaneDistances;
        public int CascadeCount => CASCADE_COUNT;

        /// <summary>
        /// Lambda for cascade distribution (0 = linear, 1 = logarithmic).
        /// Higher values give more resolution to near cascades.
        /// </summary>
        public float CascadeSplitLambda
        {
            get => _cascadeSplitLambda;
            set => _cascadeSplitLambda = Math.Clamp(value, 0f, 1f);
        }

        // ============================================================================
        // CONSTRUCTOR
        // ============================================================================

        public CascadedShadowManager(int shadowMapSize = 2048)
        {
            _shadowMapSize = Math.Clamp(shadowMapSize, 512, 4096);
            CreateShadowResources();

            Console.WriteLine($"[CSM] Initialized with {CASCADE_COUNT} cascades at {_shadowMapSize}x{_shadowMapSize}");
        }

        // ============================================================================
        // RESOURCE MANAGEMENT
        // ============================================================================

        private void CreateShadowResources()
        {
            // Create texture array for all cascades
            _shadowTextureArray = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2DArray, _shadowTextureArray);

            // Allocate storage for all cascade layers
            GL.TexImage3D(
                TextureTarget.Texture2DArray,
                0,
                PixelInternalFormat.DepthComponent32f,
                _shadowMapSize,
                _shadowMapSize,
                CASCADE_COUNT,
                0,
                PixelFormat.DepthComponent,
                PixelType.Float,
                IntPtr.Zero
            );

            // Enable hardware shadow comparison (sampler2DArrayShadow in GLSL)
            GL.TexParameter(TextureTarget.Texture2DArray, (TextureParameterName)All.TextureCompareMode, (int)All.CompareRefToTexture);
            GL.TexParameter(TextureTarget.Texture2DArray, (TextureParameterName)All.TextureCompareFunc, (int)All.Lequal);

            // Linear filtering for smoother shadow edges
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

            // Clamp to border with white (areas outside = fully lit)
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
            float[] borderColor = { 1.0f, 1.0f, 1.0f, 1.0f };
            GL.TexParameter(TextureTarget.Texture2DArray, (TextureParameterName)All.TextureBorderColor, borderColor);

            // Create framebuffer for each cascade
            for (int i = 0; i < CASCADE_COUNT; i++)
            {
                _cascadeFBOs[i] = GL.GenFramebuffer();
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, _cascadeFBOs[i]);

                // Attach specific layer of texture array
                GL.FramebufferTextureLayer(
                    FramebufferTarget.Framebuffer,
                    FramebufferAttachment.DepthAttachment,
                    _shadowTextureArray,
                    0,  // mip level
                    i   // layer (cascade index)
                );

                // No color output needed
                GL.DrawBuffer(DrawBufferMode.None);
                GL.ReadBuffer(ReadBufferMode.None);

                // Verify framebuffer
                var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
                if (status != FramebufferErrorCode.FramebufferComplete)
                {
                    throw new Exception($"[CSM] Cascade {i} framebuffer incomplete: {status}");
                }
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.BindTexture(TextureTarget.Texture2DArray, 0);
        }

        // ============================================================================
        // CASCADE SPLIT CALCULATION
        // ============================================================================

        /// <summary>
        /// Calculate cascade split distances using practical split scheme.
        /// Combines logarithmic and linear splits for optimal distribution.
        /// </summary>
        /// <param name="nearPlane">Camera near plane</param>
        /// <param name="farPlane">Maximum shadow distance</param>
        public void CalculateCascadeSplits(float nearPlane, float farPlane)
        {
            float range = farPlane - nearPlane;
            float ratio = farPlane / nearPlane;

            _cascadeSplits[0] = nearPlane;

            for (int i = 1; i <= CASCADE_COUNT; i++)
            {
                float p = (float)i / CASCADE_COUNT;

                // Logarithmic split (better distribution near camera)
                float log = nearPlane * MathF.Pow(ratio, p);

                // Linear split (uniform distribution)
                float uniform = nearPlane + range * p;

                // Blend between logarithmic and linear based on lambda
                float split = _cascadeSplitLambda * log + (1.0f - _cascadeSplitLambda) * uniform;

                _cascadeSplits[i] = split;
                _cascadePlaneDistances[i - 1] = split;
            }

            // Debug output
            // Console.WriteLine($"[CSM] Splits: {string.Join(", ", _cascadePlaneDistances.Select(d => d.ToString("F1")))}");
        }

        // ============================================================================
        // LIGHT MATRIX CALCULATION
        // ============================================================================

        /// <summary>
        /// Calculate light-space matrices for all cascades.
        /// Based on OGLDev tutorial - calculates frustum corners directly from splits.
        /// </summary>
        /// <param name="lightDirection">Normalized light direction (pointing toward light)</param>
        /// <param name="cameraView">Camera view matrix</param>
        /// <param name="cameraProj">Camera projection matrix</param>
        public void CalculateLightMatrices(Vector3 lightDirection, Matrix4 cameraView, Matrix4 cameraProj)
        {
            // Normalize light direction
            Vector3 lightDir = Vector3.Normalize(lightDirection);

            // Stable up vector (avoid gimbal lock)
            Vector3 up = MathF.Abs(Vector3.Dot(lightDir, Vector3.UnitY)) > 0.99f
                ? Vector3.UnitZ
                : Vector3.UnitY;

            // Get camera vectors from inverse view matrix
            Matrix4 invCameraView = cameraView.Inverted();
            Vector3 cameraPos = invCameraView.Row3.Xyz;
            Vector3 cameraRight = invCameraView.Row0.Xyz;
            Vector3 cameraUp = invCameraView.Row1.Xyz;
            Vector3 cameraForward = -invCameraView.Row2.Xyz; // Camera looks down -Z

            // Extract FOV from projection matrix: proj[1,1] = 1 / tan(fov/2)
            float tanHalfVFov = 1.0f / cameraProj.M22;
            float tanHalfHFov = 1.0f / cameraProj.M11;

            for (int cascade = 0; cascade < CASCADE_COUNT; cascade++)
            {
                float nearSplit = _cascadeSplits[cascade];
                float farSplit = _cascadeSplits[cascade + 1];

                // Calculate frustum corners directly from cascade splits (OGLDev approach)
                // This avoids issues with camera far plane vs shadow distance mismatch
                float xn = nearSplit * tanHalfHFov;
                float xf = farSplit * tanHalfHFov;
                float yn = nearSplit * tanHalfVFov;
                float yf = farSplit * tanHalfVFov;

                // Build 8 frustum corners in world space
                Vector3 nearCenter = cameraPos + cameraForward * nearSplit;
                Vector3 farCenter = cameraPos + cameraForward * farSplit;

                Vector3[] frustumCornersWorld = new Vector3[8]
                {
                    // Near plane corners
                    nearCenter - cameraRight * xn - cameraUp * yn,
                    nearCenter + cameraRight * xn - cameraUp * yn,
                    nearCenter + cameraRight * xn + cameraUp * yn,
                    nearCenter - cameraRight * xn + cameraUp * yn,
                    // Far plane corners
                    farCenter - cameraRight * xf - cameraUp * yf,
                    farCenter + cameraRight * xf - cameraUp * yf,
                    farCenter + cameraRight * xf + cameraUp * yf,
                    farCenter - cameraRight * xf + cameraUp * yf,
                };

                // Calculate frustum center (average of all corners)
                Vector3 frustumCenter = Vector3.Zero;
                for (int i = 0; i < 8; i++)
                {
                    frustumCenter += frustumCornersWorld[i];
                }
                frustumCenter /= 8f;

                // Create light view matrix looking at frustum center
                Vector3 lightPos = frustumCenter - lightDir * 500f;
                Matrix4 lightView = Matrix4.LookAt(lightPos, frustumCenter, up);

                // Transform frustum corners to light view space and find bounding box
                float minX = float.MaxValue, maxX = float.MinValue;
                float minY = float.MaxValue, maxY = float.MinValue;
                float minZ = float.MaxValue, maxZ = float.MinValue;

                for (int i = 0; i < 8; i++)
                {
                    Vector4 cornerLS = Vector4.TransformRow(new Vector4(frustumCornersWorld[i], 1.0f), lightView);
                    minX = MathF.Min(minX, cornerLS.X);
                    maxX = MathF.Max(maxX, cornerLS.X);
                    minY = MathF.Min(minY, cornerLS.Y);
                    maxY = MathF.Max(maxY, cornerLS.Y);
                    minZ = MathF.Min(minZ, cornerLS.Z);
                    maxZ = MathF.Max(maxZ, cornerLS.Z);
                }

                // Extend Z range to capture shadow casters outside the view frustum
                // Objects behind the camera can still cast shadows into the view
                float zExtension = 500f;
                minZ -= zExtension;
                maxZ += zExtension;

                // Stabilize shadow map by snapping to texel grid (prevents shimmering)
                float worldUnitsPerTexelX = (maxX - minX) / _shadowMapSize;
                float worldUnitsPerTexelY = (maxY - minY) / _shadowMapSize;

                minX = MathF.Floor(minX / worldUnitsPerTexelX) * worldUnitsPerTexelX;
                maxX = MathF.Ceiling(maxX / worldUnitsPerTexelX) * worldUnitsPerTexelX;
                minY = MathF.Floor(minY / worldUnitsPerTexelY) * worldUnitsPerTexelY;
                maxY = MathF.Ceiling(maxY / worldUnitsPerTexelY) * worldUnitsPerTexelY;

                // Create orthographic projection from bounding box
                // In light view space: -Z is forward, so we negate Z for the projection
                Matrix4 lightProjection = Matrix4.CreateOrthographicOffCenter(
                    minX, maxX,
                    minY, maxY,
                    -maxZ, -minZ
                );

                // Combine view and projection matrices
                _lightSpaceMatrices[cascade] = lightView * lightProjection;
            }
        }

        // ============================================================================
        // RENDERING
        // ============================================================================

        /// <summary>
        /// Begin rendering to a specific cascade's shadow map.
        /// </summary>
        /// <param name="cascadeIndex">Which cascade to render (0 to CASCADE_COUNT-1)</param>
        public void BeginCascadePass(int cascadeIndex)
        {
            if (cascadeIndex < 0 || cascadeIndex >= CASCADE_COUNT)
            {
                throw new ArgumentOutOfRangeException(nameof(cascadeIndex));
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _cascadeFBOs[cascadeIndex]);
            GL.Viewport(0, 0, _shadowMapSize, _shadowMapSize);
            GL.Clear(ClearBufferMask.DepthBufferBit);

            // Enable depth testing
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Less);

            // Polygon offset to reduce shadow acne
            // Higher values push depth further from camera, reducing self-shadowing
            GL.Enable(EnableCap.PolygonOffsetFill);
            GL.PolygonOffset(1.5f, 4.0f);

            // Disable culling for shadow pass to handle all geometry types
            GL.Disable(EnableCap.CullFace);
        }

        /// <summary>
        /// End shadow rendering for current cascade.
        /// </summary>
        public void EndCascadePass()
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.Disable(EnableCap.PolygonOffsetFill);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(TriangleFace.Back);
        }

        /// <summary>
        /// Get the light-space matrix for a specific cascade.
        /// Use this when rendering objects into the shadow map.
        /// </summary>
        public Matrix4 GetLightSpaceMatrix(int cascadeIndex)
        {
            return _lightSpaceMatrices[cascadeIndex];
        }

        /// <summary>
        /// Bind the shadow texture array for sampling in shaders.
        /// </summary>
        public void BindShadowTexture(TextureUnit textureUnit)
        {
            GL.ActiveTexture(textureUnit);
            GL.BindTexture(TextureTarget.Texture2DArray, _shadowTextureArray);
        }

        // ============================================================================
        // RESIZE
        // ============================================================================

        public void Resize(int newSize)
        {
            if (newSize == _shadowMapSize) return;

            _shadowMapSize = Math.Clamp(newSize, 512, 4096);
            Dispose();
            CreateShadowResources();

            Console.WriteLine($"[CSM] Resized to {_shadowMapSize}x{_shadowMapSize}");
        }

        // ============================================================================
        // CLEANUP
        // ============================================================================

        public void Dispose()
        {
            if (_shadowTextureArray != 0)
            {
                GL.DeleteTexture(_shadowTextureArray);
                _shadowTextureArray = 0;
            }

            for (int i = 0; i < CASCADE_COUNT; i++)
            {
                if (_cascadeFBOs[i] != 0)
                {
                    GL.DeleteFramebuffer(_cascadeFBOs[i]);
                    _cascadeFBOs[i] = 0;
                }
            }
        }
    }
}

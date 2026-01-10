using System;
using System.IO;
using System.Linq;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Engine.Components;
using Engine.Scene;

namespace Engine.Rendering
{
    /// <summary>
    /// Renders layered procedural clouds with wind animation and atmospheric scattering.
    /// Integrates with WeatherComponent for dynamic cloud control.
    /// </summary>
    public class CloudRenderer : IDisposable
    {
        private ShaderProgram? _cloudShader;
        private int _vao;
        private int _vbo;
        private int _vertexCount;
        private Texture? _ditheringTexture;
        private Vector3 _windOffset = Vector3.Zero;
        private bool _initialized = false;

        // Dome parameters
        private const int DOME_SEGMENTS = 32;  // Horizontal segments
        private const int DOME_RINGS = 16;     // Vertical rings
        private const float DOME_RADIUS = 1000.0f; // Large radius for skybox

        /// <summary>
        /// Initialize cloud renderer resources
        /// </summary>
        public void Initialize()
        {
            if (_initialized)
                return;

            try
            {
                // Load cloud shader
                string vertPath = Path.Combine("Engine", "Rendering", "Shaders", "Effects", "clouds.vert");
                string fragPath = Path.Combine("Engine", "Rendering", "Shaders", "Effects", "clouds.frag");
                _cloudShader = ShaderProgram.FromFiles(vertPath, fragPath);
                if (_cloudShader == null)
                {
                    Console.WriteLine("[CloudRenderer] Failed to load cloud shader");
                    return;
                }

                // Create cloud dome mesh
                CreateCloudDome();

                // Load or generate dithering texture
                LoadDitheringTexture();

                _initialized = true;
                Console.WriteLine("[CloudRenderer] Initialized successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CloudRenderer] Initialization failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Render clouds for the current scene
        /// </summary>
        public void Render(Scene.Scene scene, Vector3 cameraPosition, Matrix4 viewMatrix, Matrix4 projectionMatrix)
        {
            if (!_initialized || _cloudShader == null)
                return;

            // Get weather component from scene
            var weather = GetWeatherComponent(scene);
            if (weather == null || !weather.CloudEnabled)
                return;

            // Early exit if coverage is zero (clear sky)
            if (weather.CloudCoverage < 0.01f)
                return;

            // Setup OpenGL state
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.DepthMask(false); // Don't write to depth buffer
            GL.DepthFunc(DepthFunction.Lequal); // Render at far plane
            GL.Disable(EnableCap.CullFace); // Render both sides

            // Use cloud shader (Global UBO with uViewProj and uCameraPos is already bound by ViewportRenderer)
            _cloudShader.Use();

            // Create model matrix: translate dome to camera position (like skybox)
            // This ensures the dome always surrounds the camera
            Matrix4 modelMatrix = Matrix4.CreateTranslation(cameraPosition);
            Matrix4 mvp = modelMatrix * viewMatrix * projectionMatrix;
            _cloudShader.SetMat4("uMVP", mvp);

            // Update wind offset (accumulated over time)
            UpdateWindOffset(weather);

            // Set cloud parameters (uniforms NOT in Global UBO)
            _cloudShader.SetFloat("uCloudCoverage", weather.CloudCoverage);
            _cloudShader.SetFloat("uCloudDensity", weather.CloudDensity);
            _cloudShader.SetInt("uCloudType", (int)weather.CloudType);
            _cloudShader.SetVec3("uCloudWindOffset", _windOffset);
            _cloudShader.SetFloat("uCloudScattering", weather.CloudScattering);
            _cloudShader.SetFloat("uCloudAmbient", weather.CloudAmbient);
            _cloudShader.SetFloat("uCloudDetailSpeed", weather.CloudDetailSpeed);

            // Set fine-tune parameters
            _cloudShader.SetFloat("uCloudNoiseScale", weather.CloudNoiseScale);
            _cloudShader.SetFloat("uCloudMorphSpeed", weather.CloudMorphSpeed);
            _cloudShader.SetFloat("uCloudEdgeSoftness", weather.CloudEdgeSoftness);
            _cloudShader.SetFloat("uCloudBillowiness", weather.CloudBillowiness);
            _cloudShader.SetFloat("uCloudDetailStrength", weather.CloudDetailStrength);

            // === SET DUAL-LAYER SCROLLING NOISE PARAMETERS ===
            // Layer 1: Primary noise layer
            _cloudShader.SetFloat("uNoiseLayer1Speed", weather.NoiseLayer1Speed);
            var layer1Dir = weather.GetNoiseLayer1Direction();
            _cloudShader.SetVec2("uNoiseLayer1Direction", layer1Dir);
            _cloudShader.SetFloat("uNoiseLayer1Scale", weather.NoiseLayer1Scale);

            // Layer 2: Secondary noise layer
            _cloudShader.SetFloat("uNoiseLayer2Speed", weather.NoiseLayer2Speed);
            var layer2Dir = weather.GetNoiseLayer2Direction();
            _cloudShader.SetVec2("uNoiseLayer2Direction", layer2Dir);
            _cloudShader.SetFloat("uNoiseLayer2Scale", weather.NoiseLayer2Scale);

            // === SET FBM PARAMETERS ===
            _cloudShader.SetInt("uFBMOctaves", weather.FBMOctaves);
            _cloudShader.SetFloat("uFBMLacunarity", weather.FBMLacunarity);
            _cloudShader.SetFloat("uFBMGain", weather.FBMGain);
            _cloudShader.SetFloat("uFBMStrength", weather.FBMStrength);
            _cloudShader.SetFloat("uWorleyWeight", weather.WorleyWeight);
            _cloudShader.SetFloat("uErosion", weather.Erosion);
            _cloudShader.SetFloat("uSharpness", weather.Sharpness);

            // Set sun direction and color from environment settings
            SetSunParameters(scene);

            // Bind dithering texture
            if (_ditheringTexture != null)
            {
                GL.ActiveTexture(TextureUnit.Texture10);
                _ditheringTexture.Bind();
                _cloudShader.SetInt("uDitheringTex", 10);
            }

            // Render cloud dome
            GL.BindVertexArray(_vao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, _vertexCount);
            GL.BindVertexArray(0);

            // Restore OpenGL state
            GL.DepthMask(true);
            GL.Disable(EnableCap.Blend);
            GL.Enable(EnableCap.CullFace);
        }

        /// <summary>
        /// Update wind offset for cloud animation
        /// </summary>
        private void UpdateWindOffset(WeatherComponent weather)
        {
            // Get wind direction (normalized)
            var windDir = weather.GetWindDirection();

            // Calculate wind velocity
            float windSpeed = weather.WindSpeed * weather.CloudSpeed;

            // Accumulate offset over time using engine deltaTime
            float deltaTime = Engine.Core.Time.DeltaTime;
            _windOffset.X += windDir.X * windSpeed * deltaTime;
            _windOffset.Z += windDir.Y * windSpeed * deltaTime;
        }

        /// <summary>
        /// Set sun direction, color, and intensity from environment settings
        /// </summary>
        private void SetSunParameters(Scene.Scene scene)
        {
            // Find environment settings in scene
            var envEntity = scene.Entities.FirstOrDefault(e => e.HasComponent<EnvironmentSettings>());
            if (envEntity == null)
            {
                // Fallback: default sun direction (overhead)
                _cloudShader?.SetVec3("uCloudSunDir", new Vector3(0, 1, 0));
                _cloudShader?.SetVec3("uCloudSunColor", Vector3.One);
                _cloudShader?.SetFloat("uCloudSunIntensity", 1.0f);
                return;
            }

            var env = envEntity.GetComponent<EnvironmentSettings>();
            if (env == null || env.MainDirectionalLight == null)
            {
                // Fallback
                _cloudShader?.SetVec3("uCloudSunDir", new Vector3(0, 1, 0));
                _cloudShader?.SetVec3("uCloudSunColor", Vector3.One);
                _cloudShader?.SetFloat("uCloudSunIntensity", 1.0f);
                return;
            }

            // Get light component
            var light = env.MainDirectionalLight.GetComponent<LightComponent>();
            if (light != null)
            {
                // Calculate sun direction from light transform rotation
                var transform = env.MainDirectionalLight.Transform;
                if (transform != null)
                {
                    // Light forward direction (Z-axis in OpenGL)
                    Vector3 forward = Vector3.Transform(new Vector3(0, 0, 1), transform.Rotation);
                    _cloudShader?.SetVec3("uCloudSunDir", -forward); // Negate for direction TO sun

                    // Sun color
                    _cloudShader?.SetVec3("uCloudSunColor", light.Color);

                    // Sun intensity (brightness) - critical for day/night variation
                    // This controls how bright the clouds are based on sun/moon light
                    _cloudShader?.SetFloat("uCloudSunIntensity", light.Intensity);
                    return;
                }
            }

            // Fallback if something went wrong
            _cloudShader?.SetVec3("uCloudSunDir", new Vector3(0, 1, 0));
            _cloudShader?.SetVec3("uCloudSunColor", Vector3.One);
            _cloudShader?.SetFloat("uCloudSunIntensity", 1.0f);
        }

        /// <summary>
        /// Get weather component from scene
        /// </summary>
        private WeatherComponent? GetWeatherComponent(Scene.Scene scene)
        {
            foreach (var entity in scene.Entities)
            {
                if (!entity.Active)
                    continue;

                var weather = entity.GetComponent<WeatherComponent>();
                if (weather != null)
                    return weather;
            }
            return null;
        }

        /// <summary>
        /// Create hemisphere/dome mesh for cloud rendering
        /// </summary>
        private void CreateCloudDome()
        {
            var vertices = new System.Collections.Generic.List<float>();

            // Generate dome vertices (hemisphere from 0 to 90 degrees elevation)
            for (int ring = 0; ring <= DOME_RINGS; ring++)
            {
                float v = (float)ring / DOME_RINGS;
                float elevation = v * MathHelper.PiOver2; // 0 to 90 degrees

                for (int segment = 0; segment <= DOME_SEGMENTS; segment++)
                {
                    float u = (float)segment / DOME_SEGMENTS;
                    float azimuth = u * MathHelper.TwoPi; // 0 to 360 degrees

                    // Spherical to Cartesian coordinates
                    float x = DOME_RADIUS * MathF.Cos(elevation) * MathF.Cos(azimuth);
                    float y = DOME_RADIUS * MathF.Sin(elevation);
                    float z = DOME_RADIUS * MathF.Cos(elevation) * MathF.Sin(azimuth);

                    vertices.Add(x);
                    vertices.Add(y);
                    vertices.Add(z);
                }
            }

            // Generate indices for triangles
            var indices = new System.Collections.Generic.List<uint>();
            for (int ring = 0; ring < DOME_RINGS; ring++)
            {
                for (int segment = 0; segment < DOME_SEGMENTS; segment++)
                {
                    uint current = (uint)(ring * (DOME_SEGMENTS + 1) + segment);
                    uint next = current + (uint)(DOME_SEGMENTS + 1);

                    // First triangle
                    indices.Add(current);
                    indices.Add(next);
                    indices.Add(current + 1);

                    // Second triangle
                    indices.Add(current + 1);
                    indices.Add(next);
                    indices.Add(next + 1);
                }
            }

            // Create expanded vertex array (indexed to non-indexed)
            var expandedVertices = new System.Collections.Generic.List<float>();
            foreach (var index in indices)
            {
                int vertexIndex = (int)index * 3;
                expandedVertices.Add(vertices[vertexIndex]);
                expandedVertices.Add(vertices[vertexIndex + 1]);
                expandedVertices.Add(vertices[vertexIndex + 2]);
            }

            _vertexCount = expandedVertices.Count / 3;

            // Create VAO and VBO
            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();

            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, expandedVertices.Count * sizeof(float),
                         expandedVertices.ToArray(), BufferUsageHint.StaticDraw);

            // Position attribute (location 0)
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            GL.BindVertexArray(0);

            Console.WriteLine($"[CloudRenderer] Created dome with {_vertexCount} vertices");
        }

        /// <summary>
        /// Load or generate dithering texture for gradient smoothing
        /// </summary>
        private void LoadDitheringTexture()
        {
            try
            {
                // Try to find existing dithering texture in assets
                // Fallback: generate blue noise procedurally

                // For now, create a simple noise texture
                _ditheringTexture = GenerateBlueNoiseTexture(64, 64);
                Console.WriteLine("[CloudRenderer] Generated dithering texture");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CloudRenderer] Failed to load dithering texture: {ex.Message}");
            }
        }

        /// <summary>
        /// Generate simple blue noise texture (fallback)
        /// </summary>
        private Texture GenerateBlueNoiseTexture(int width, int height)
        {
            var random = new Random();
            byte[] pixels = new byte[width * height];

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = (byte)random.Next(256);
            }

            int textureId = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, textureId);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.R8,
                         width, height, 0, PixelFormat.Red, PixelType.UnsignedByte, pixels);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

            GL.BindTexture(TextureTarget.Texture2D, 0);

            return new Texture(textureId, width, height);
        }

        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            if (_vao != 0)
            {
                GL.DeleteVertexArray(_vao);
                _vao = 0;
            }

            if (_vbo != 0)
            {
                GL.DeleteBuffer(_vbo);
                _vbo = 0;
            }

            _ditheringTexture?.Dispose();
            _initialized = false;
        }
    }

    /// <summary>
    /// Simple texture wrapper for dithering
    /// </summary>
    internal class Texture : IDisposable
    {
        public int Handle { get; }
        public int Width { get; }
        public int Height { get; }

        public Texture(int handle, int width, int height)
        {
            Handle = handle;
            Width = width;
            Height = height;
        }

        public void Bind()
        {
            GL.BindTexture(TextureTarget.Texture2D, Handle);
        }

        public void Dispose()
        {
            if (Handle != 0)
            {
                GL.DeleteTexture(Handle);
            }
        }
    }
}

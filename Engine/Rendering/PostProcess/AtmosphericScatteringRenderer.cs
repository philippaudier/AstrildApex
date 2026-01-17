using System;
using System.IO;
using System.Linq;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace Engine.Rendering.PostProcess
{
    /// <summary>
    /// Atmospheric Scattering Renderer - Triple A Quality
    /// Based on GPU Gems 2, Chapter 16: Accurate Atmospheric Scattering
    ///
    /// Features:
    /// - Rayleigh scattering (blue sky, wavelength-dependent)
    /// - Mie scattering (sun glow, haze, aerosols)
    /// - Single scattering with ray marching
    /// - Optical depth calculation (out-scattering)
    /// - Phase functions (Rayleigh + Henyey-Greenstein)
    /// - HDR exposure tone mapping
    /// - Aerial perspective for distant objects
    /// </summary>
    public class AtmosphericScatteringRenderer : Engine.Components.IPostProcessRenderer
    {
        private ShaderProgram? _shader;

        // Uniform locations - Textures & Camera
        private int _uSourceTexture = -1;
        private int _uDepthTexture = -1;
        private int _uInvProjection = -1;
        private int _uInvView = -1;
        private int _uCameraPos = -1;
        private int _uScreenSize = -1;

        // Sun / Light
        private int _uSunDirection = -1;
        private int _uSunColor = -1;
        private int _uSunIntensity = -1;

        // Atmosphere parameters
        private int _uPlanetRadius = -1;
        private int _uAtmosphereRadius = -1;
        private int _uRayleighScaleHeight = -1;
        private int _uMieScaleHeight = -1;

        // Scattering coefficients
        private int _uRayleighCoeff = -1;
        private int _uMieCoeff = -1;
        private int _uMieG = -1;

        // Rendering
        private int _uNumSamples = -1;
        private int _uNumLightSamples = -1;
        private int _uExposure = -1;
        private int _uIntensity = -1;

        // Underwater detection
        private int _uIsUnderwater = -1;
        private int _uWaterLevel = -1;

        public void Initialize()
        {
            try
            {
                var baseDir = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "Engine", "Rendering", "Shaders", "PostProcess"));
                var vertPath = Path.Combine(baseDir, "atmospheric_scattering.vert");
                var fragPath = Path.Combine(baseDir, "atmospheric_scattering.frag");

                if (File.Exists(vertPath) && File.Exists(fragPath))
                {
                    var vert = File.ReadAllText(vertPath);
                    var frag = File.ReadAllText(fragPath);
                    _shader = ShaderProgram.FromSource(vert, frag);
                    Console.WriteLine("[AtmosphericScatteringRenderer] Shader loaded successfully");
                }
                else
                {
                    Console.WriteLine($"[AtmosphericScatteringRenderer] Shader files not found: {vertPath}, {fragPath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AtmosphericScatteringRenderer] Shader load failed: {ex.Message}");
                _shader = null;
            }

            if (_shader != null)
            {
                _shader.Use();
                CacheUniformLocations();
            }
        }

        private void CacheUniformLocations()
        {
            if (_shader == null) return;

            // Textures & Camera
            _uSourceTexture = GL.GetUniformLocation(_shader.Handle, "u_SourceTexture");
            _uDepthTexture = GL.GetUniformLocation(_shader.Handle, "u_DepthTexture");
            _uInvProjection = GL.GetUniformLocation(_shader.Handle, "u_InvProjection");
            _uInvView = GL.GetUniformLocation(_shader.Handle, "u_InvView");
            _uCameraPos = GL.GetUniformLocation(_shader.Handle, "u_CameraPos");
            _uScreenSize = GL.GetUniformLocation(_shader.Handle, "u_ScreenSize");

            // Sun / Light
            _uSunDirection = GL.GetUniformLocation(_shader.Handle, "u_SunDirection");
            _uSunColor = GL.GetUniformLocation(_shader.Handle, "u_SunColor");
            _uSunIntensity = GL.GetUniformLocation(_shader.Handle, "u_SunIntensity");

            // Atmosphere parameters
            _uPlanetRadius = GL.GetUniformLocation(_shader.Handle, "u_PlanetRadius");
            _uAtmosphereRadius = GL.GetUniformLocation(_shader.Handle, "u_AtmosphereRadius");
            _uRayleighScaleHeight = GL.GetUniformLocation(_shader.Handle, "u_RayleighScaleHeight");
            _uMieScaleHeight = GL.GetUniformLocation(_shader.Handle, "u_MieScaleHeight");

            // Scattering coefficients
            _uRayleighCoeff = GL.GetUniformLocation(_shader.Handle, "u_RayleighCoeff");
            _uMieCoeff = GL.GetUniformLocation(_shader.Handle, "u_MieCoeff");
            _uMieG = GL.GetUniformLocation(_shader.Handle, "u_MieG");

            // Rendering
            _uNumSamples = GL.GetUniformLocation(_shader.Handle, "u_NumSamples");
            _uNumLightSamples = GL.GetUniformLocation(_shader.Handle, "u_NumLightSamples");
            _uExposure = GL.GetUniformLocation(_shader.Handle, "u_Exposure");
            _uIntensity = GL.GetUniformLocation(_shader.Handle, "u_Intensity");

            // Underwater detection
            _uIsUnderwater = GL.GetUniformLocation(_shader.Handle, "u_IsUnderwater");
            _uWaterLevel = GL.GetUniformLocation(_shader.Handle, "u_WaterLevel");
        }

        public void Render(Engine.Components.PostProcessEffect effect, Engine.Components.PostProcessContext context)
        {
            if (_shader == null)
            {
                Initialize();
            }

            if (_shader == null || !(effect is AtmosphericScatteringEffect atmo))
            {
                Engine.Rendering.PostProcessHelper.PassThrough(context);
                return;
            }

            _shader.Use();

            // Bind textures
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, context.SourceTexture);
            if (_uSourceTexture >= 0) GL.Uniform1(_uSourceTexture, 0);

            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, context.DepthTexture);
            if (_uDepthTexture >= 0) GL.Uniform1(_uDepthTexture, 1);

            // Camera matrices
            if (_uInvProjection >= 0 && context.ProjectionMatrix.HasValue)
            {
                var invProj = context.ProjectionMatrix.Value.Inverted();
                GL.UniformMatrix4(_uInvProjection, false, ref invProj);
            }

            if (_uInvView >= 0 && context.ViewMatrix.HasValue)
            {
                var invView = context.ViewMatrix.Value.Inverted();
                GL.UniformMatrix4(_uInvView, false, ref invView);
            }

            // Camera position
            Vector3 cameraPos = Vector3.Zero;
            if (context.ViewMatrix.HasValue)
            {
                var invView = context.ViewMatrix.Value.Inverted();
                cameraPos = invView.ExtractTranslation();
            }
            if (_uCameraPos >= 0) GL.Uniform3(_uCameraPos, cameraPos);

            // Screen size
            if (_uScreenSize >= 0) GL.Uniform2(_uScreenSize, (float)context.Width, (float)context.Height);

            // Sun direction - get from directional light in scene
            Vector3 sunDirection = new Vector3(0.3f, 0.8f, 0.3f); // Default (pointing TO sun)
            Vector3 sunColor = new Vector3(1.0f, 0.95f, 0.9f);
            float sunIntensity = 22.0f;

            if (context.Scene != null)
            {
                foreach (var entity in context.Scene.Entities)
                {
                    var light = entity.GetComponent<Engine.Components.LightComponent>();
                    if (light != null && light.Type == Engine.Components.LightType.Directional)
                    {
                        // Light direction points FROM sun, we need direction TO sun
                        sunDirection = -Vector3.Normalize(light.Direction);
                        sunColor = new Vector3(light.Color.X, light.Color.Y, light.Color.Z);
                        sunIntensity = atmo.SunIntensity;
                        break;
                    }
                }
            }

            if (_uSunDirection >= 0) GL.Uniform3(_uSunDirection, sunDirection);
            if (_uSunColor >= 0) GL.Uniform3(_uSunColor, sunColor);
            if (_uSunIntensity >= 0) GL.Uniform1(_uSunIntensity, sunIntensity);

            // Atmosphere parameters
            if (_uPlanetRadius >= 0) GL.Uniform1(_uPlanetRadius, atmo.PlanetRadius);
            if (_uAtmosphereRadius >= 0) GL.Uniform1(_uAtmosphereRadius, atmo.AtmosphereRadius);
            if (_uRayleighScaleHeight >= 0) GL.Uniform1(_uRayleighScaleHeight, atmo.RayleighScaleHeight);
            if (_uMieScaleHeight >= 0) GL.Uniform1(_uMieScaleHeight, atmo.MieScaleHeight);

            // Scattering coefficients
            if (_uRayleighCoeff >= 0) GL.Uniform3(_uRayleighCoeff, atmo.RayleighCoefficients);
            if (_uMieCoeff >= 0) GL.Uniform1(_uMieCoeff, atmo.MieCoefficient);
            if (_uMieG >= 0) GL.Uniform1(_uMieG, atmo.MieG);

            // Rendering parameters
            if (_uNumSamples >= 0) GL.Uniform1(_uNumSamples, atmo.NumSamples);
            if (_uNumLightSamples >= 0) GL.Uniform1(_uNumLightSamples, atmo.NumLightSamples);
            if (_uExposure >= 0) GL.Uniform1(_uExposure, atmo.Exposure);
            if (_uIntensity >= 0) GL.Uniform1(_uIntensity, atmo.Intensity);

            // Underwater detection - skip sky replacement when camera is below water
            float waterLevel = 0.0f;
            bool isUnderwater = false;
            if (context.Scene != null)
            {
                // Try to find WaterPlaneComponent to get water level
                foreach (var entity in context.Scene.Entities)
                {
                    var water = entity.GetComponent<Engine.Components.WaterPlaneComponent>();
                    if (water != null)
                    {
                        waterLevel = water.GetWaterLevel();
                        break;
                    }
                }
                isUnderwater = cameraPos.Y < waterLevel;
            }
            if (_uIsUnderwater >= 0) GL.Uniform1(_uIsUnderwater, isUnderwater ? 1 : 0);
            if (_uWaterLevel >= 0) GL.Uniform1(_uWaterLevel, waterLevel);

            // Draw fullscreen triangle
            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        }

        public void Dispose()
        {
            _shader?.Dispose();
            _shader = null;
        }
    }
}

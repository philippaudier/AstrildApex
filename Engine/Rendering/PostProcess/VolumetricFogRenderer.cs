using System;
using System.IO;
using System.Linq;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace Engine.Rendering.PostProcess
{
    public class VolumetricFogRenderer : Engine.Components.IPostProcessRenderer
    {
        private ShaderProgram? _shader;

        // Uniform locations
        private int _uSourceTexture = -1;
        private int _uDepthTexture = -1;
        private int _uInvProjection = -1;
        private int _uInvView = -1;
        private int _uCameraPos = -1; // Note: Global UBO also provides uCameraPos

        // Fog parameters
        private int _uFogColor = -1;
        private int _uDensity = -1;
        private int _uDepthStart = -1;
        private int _uDepthEnd = -1;
        private int _uUseExponential = -1;

        // Height-based fog
        private int _uUseHeightFog = -1;
        private int _uHeightFalloff = -1;
        private int _uBaseHeight = -1;
        private int _uMaxHeight = -1;

        // Scattering & atmosphere
        private int _uScatteringIntensity = -1;
        private int _uExtinctionFactor = -1;
        private int _uSunScatteringColor = -1;
        private int _uUseSunScattering = -1;
        private int _uSunDirection = -1;

        // Noise
        private int _uUseNoise = -1;
        private int _uNoiseScale = -1;
        private int _uNoiseSpeed = -1;
        private int _uNoiseStrength = -1;
        private int _uTime = -1;

        // Screen resolution for depth analysis
        private int _uScreenSize = -1;

        public void Initialize()
        {
            try
            {
                var baseDir = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "Engine", "Rendering", "Shaders", "PostProcess"));
                var vertPath = Path.Combine(baseDir, "volumetric_fog.vert");
                var fragPath = Path.Combine(baseDir, "volumetric_fog.frag");

                if (File.Exists(vertPath) && File.Exists(fragPath))
                {
                    var vert = File.ReadAllText(vertPath);
                    var frag = File.ReadAllText(fragPath);
                    _shader = ShaderProgram.FromSource(vert, frag);
                }
            }
            catch (Exception ex)
            {
                try { Engine.Utils.DebugLogger.Log($"[VolumetricFog] Shader load failed: {ex.Message}"); } catch { }
                _shader = null;
            }

            if (_shader != null)
            {
                _shader.Use();

                // Get all uniform locations
                _uSourceTexture = GL.GetUniformLocation(_shader.Handle, "u_SourceTexture");
                _uDepthTexture = GL.GetUniformLocation(_shader.Handle, "u_DepthTexture");
                _uInvProjection = GL.GetUniformLocation(_shader.Handle, "u_InvProjection");
                _uInvView = GL.GetUniformLocation(_shader.Handle, "u_InvView");
                _uCameraPos = GL.GetUniformLocation(_shader.Handle, "u_CameraPos");

                _uFogColor = GL.GetUniformLocation(_shader.Handle, "u_FogColor");
                _uDensity = GL.GetUniformLocation(_shader.Handle, "u_Density");
                _uDepthStart = GL.GetUniformLocation(_shader.Handle, "u_DepthStart");
                _uDepthEnd = GL.GetUniformLocation(_shader.Handle, "u_DepthEnd");
                _uUseExponential = GL.GetUniformLocation(_shader.Handle, "u_UseExponential");

                _uUseHeightFog = GL.GetUniformLocation(_shader.Handle, "u_UseHeightFog");
                _uHeightFalloff = GL.GetUniformLocation(_shader.Handle, "u_HeightFalloff");
                _uBaseHeight = GL.GetUniformLocation(_shader.Handle, "u_BaseHeight");
                _uMaxHeight = GL.GetUniformLocation(_shader.Handle, "u_MaxHeight");

                _uScatteringIntensity = GL.GetUniformLocation(_shader.Handle, "u_ScatteringIntensity");
                _uExtinctionFactor = GL.GetUniformLocation(_shader.Handle, "u_ExtinctionFactor");
                _uSunScatteringColor = GL.GetUniformLocation(_shader.Handle, "u_SunScatteringColor");
                _uUseSunScattering = GL.GetUniformLocation(_shader.Handle, "u_UseSunScattering");
                _uSunDirection = GL.GetUniformLocation(_shader.Handle, "u_SunDirection");

                _uUseNoise = GL.GetUniformLocation(_shader.Handle, "u_UseNoise");
                _uNoiseScale = GL.GetUniformLocation(_shader.Handle, "u_NoiseScale");
                _uNoiseSpeed = GL.GetUniformLocation(_shader.Handle, "u_NoiseSpeed");
                _uNoiseStrength = GL.GetUniformLocation(_shader.Handle, "u_NoiseStrength");
                _uTime = GL.GetUniformLocation(_shader.Handle, "u_Time");
                
                _uScreenSize = GL.GetUniformLocation(_shader.Handle, "u_ScreenSize");
            }
        }

        public void Render(Engine.Components.PostProcessEffect effect, Engine.Components.PostProcessContext context)
        {
            if (_shader == null)
            {
                Initialize();
            }

            if (_shader == null || !(effect is VolumetricFogEffect fog))
            {
                try { Engine.Utils.DebugLogger.Log("[VolumetricFog] Shader not initialized or effect type mismatch"); } catch { }
                return;
            }

            // Debug: Check if depth texture is valid
            if (context.DepthTexture == 0)
            {
                try { Engine.Utils.DebugLogger.Log("[VolumetricFog] WARNING: Depth texture is 0!"); } catch { }
            }

            _shader.Use();

            // Bind textures
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, context.SourceTexture);
            if (_uSourceTexture >= 0) GL.Uniform1(_uSourceTexture, 0);

            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, context.DepthTexture);
            if (_uDepthTexture >= 0) GL.Uniform1(_uDepthTexture, 1);

            // Camera matrices and position
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

            // Note: Camera position is provided by the Global UBO (uCameraPos), not per-effect uniform

            // Get effective fog parameters considering Global/Local/Blend mode
            Engine.Components.WeatherComponent? weatherComponent = null;
            if (context.Scene != null)
            {
                var weatherEntities = context.Scene.Cache != null
                    ? context.Scene.Cache.GetEntitiesWithComponent<Engine.Components.WeatherComponent>()
                    : context.Scene.Entities.ToList();
                weatherComponent = weatherEntities.FirstOrDefault()?.GetComponent<Engine.Components.WeatherComponent>();
            }
            var (fogColor, density, depthStart, depthEnd) = fog.GetEffectiveFogParameters(weatherComponent);

            // Fog parameters
            if (_uFogColor >= 0) GL.Uniform3(_uFogColor, fogColor);
            if (_uDensity >= 0) GL.Uniform1(_uDensity, density);
            if (_uDepthStart >= 0) GL.Uniform1(_uDepthStart, depthStart);
            if (_uDepthEnd >= 0) GL.Uniform1(_uDepthEnd, depthEnd);
            if (_uUseExponential >= 0) GL.Uniform1(_uUseExponential, fog.UseExponential ? 1 : 0);

            // Height-based fog
            if (_uUseHeightFog >= 0) GL.Uniform1(_uUseHeightFog, fog.UseHeightFog ? 1 : 0);
            if (_uHeightFalloff >= 0) GL.Uniform1(_uHeightFalloff, fog.HeightFalloff);
            if (_uBaseHeight >= 0) GL.Uniform1(_uBaseHeight, fog.BaseHeight);
            if (_uMaxHeight >= 0) GL.Uniform1(_uMaxHeight, fog.MaxHeight);

            // Scattering & atmosphere
            if (_uScatteringIntensity >= 0) GL.Uniform1(_uScatteringIntensity, fog.ScatteringIntensity);
            if (_uExtinctionFactor >= 0) GL.Uniform1(_uExtinctionFactor, fog.ExtinctionFactor);
            if (_uSunScatteringColor >= 0) GL.Uniform3(_uSunScatteringColor, fog.SunScatteringColor);
            if (_uUseSunScattering >= 0) GL.Uniform1(_uUseSunScattering, fog.UseSunScattering ? 1 : 0);

            // Sun direction (get from directional light if sun scattering is enabled)
            if (_uSunDirection >= 0 && fog.UseSunScattering)
            {
                // Default sun direction (light pointing down from above)
                // TODO: Get actual sun direction from EnvironmentSettings.MainDirectionalLight
                GL.Uniform3(_uSunDirection, new Vector3(0.3f, -0.7f, 0.3f));
            }

            // Noise
            if (_uUseNoise >= 0) GL.Uniform1(_uUseNoise, fog.UseNoise ? 1 : 0);
            if (_uNoiseScale >= 0) GL.Uniform1(_uNoiseScale, fog.NoiseScale);
            if (_uNoiseSpeed >= 0) GL.Uniform1(_uNoiseSpeed, fog.NoiseSpeed);
            if (_uNoiseStrength >= 0) GL.Uniform1(_uNoiseStrength, fog.NoiseStrength);
            // Use accumulated delta time for animation
            // TODO: Add proper time tracking to PostProcessContext
            if (_uTime >= 0)
            {
                // For now, use a simple accumulated time based on system time
                float time = (float)DateTime.Now.TimeOfDay.TotalSeconds;
                GL.Uniform1(_uTime, time);
            }

            // Screen size for depth buffer analysis (valley detection)
            if (_uScreenSize >= 0)
            {
                // Get screen size from context or default to reasonable size
                float width = context.Width > 0 ? context.Width : 1920;
                float height = context.Height > 0 ? context.Height : 1080;
                GL.Uniform2(_uScreenSize, width, height);
            }

            // Fullscreen triangle (project convention)
            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        }

        public void Dispose()
        {
            _shader?.Dispose();
            _shader = null;
        }
    }
}

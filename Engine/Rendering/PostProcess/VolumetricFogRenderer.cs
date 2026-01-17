using System;
using System.IO;
using System.Linq;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace Engine.Rendering.PostProcess
{
    /// <summary>
    /// True Volumetric Fog Renderer with Ray Marching
    /// Features:
    /// - Ray marching through 3D fog volume
    /// - Beer-Lambert light extinction
    /// - Henyey-Greenstein phase function for god rays (Mie scattering)
    /// - Height-based density falloff
    /// - 3D noise for density variation
    /// </summary>
    public class VolumetricFogRenderer : Engine.Components.IPostProcessRenderer
    {
        private ShaderProgram? _shader;

        // Uniform locations - Textures & Camera
        private int _uSourceTexture = -1;
        private int _uDepthTexture = -1;
        private int _uInvProjection = -1;
        private int _uInvView = -1;
        private int _uCameraPos = -1;
        private int _uTime = -1;
        private int _uScreenSize = -1;

        // Fog parameters
        private int _uFogColor = -1;
        private int _uDensity = -1;
        private int _uDepthStart = -1;
        private int _uDepthEnd = -1;

        // Ray marching
        private int _uRayMarchSteps = -1;
        private int _uMaxRayDistance = -1;

        // Height-based fog
        private int _uUseHeightFog = -1;
        private int _uHeightFalloff = -1;
        private int _uBaseHeight = -1;
        private int _uMaxHeight = -1;

        // Scattering & God rays
        private int _uUseSunScattering = -1;
        private int _uSunDirection = -1;
        private int _uSunScatteringColor = -1;
        private int _uScatteringIntensity = -1;
        private int _uMieG = -1;
        private int _uExtinctionFactor = -1;
        private int _uAmbientIntensity = -1;

        // God rays (radial blur)
        private int _uSunScreenPos = -1;
        private int _uGodRaysIntensity = -1;
        private int _uGodRaysDensity = -1;
        private int _uGodRaysDecay = -1;

        // Noise
        private int _uUseNoise = -1;
        private int _uNoiseScale = -1;
        private int _uNoiseSpeed = -1;
        private int _uNoiseStrength = -1;
        private int _uNoiseOctaves = -1;

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
                    Console.WriteLine("[VolumetricFogRenderer] Ray marching shader loaded successfully");
                }
                else
                {
                    Console.WriteLine($"[VolumetricFogRenderer] Shader files not found: {vertPath}, {fragPath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VolumetricFogRenderer] Shader load failed: {ex.Message}");
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
            _uTime = GL.GetUniformLocation(_shader.Handle, "u_Time");
            _uScreenSize = GL.GetUniformLocation(_shader.Handle, "u_ScreenSize");

            // Fog parameters
            _uFogColor = GL.GetUniformLocation(_shader.Handle, "u_FogColor");
            _uDensity = GL.GetUniformLocation(_shader.Handle, "u_Density");
            _uDepthStart = GL.GetUniformLocation(_shader.Handle, "u_DepthStart");
            _uDepthEnd = GL.GetUniformLocation(_shader.Handle, "u_DepthEnd");

            // Ray marching
            _uRayMarchSteps = GL.GetUniformLocation(_shader.Handle, "u_RayMarchSteps");
            _uMaxRayDistance = GL.GetUniformLocation(_shader.Handle, "u_MaxRayDistance");

            // Height fog
            _uUseHeightFog = GL.GetUniformLocation(_shader.Handle, "u_UseHeightFog");
            _uHeightFalloff = GL.GetUniformLocation(_shader.Handle, "u_HeightFalloff");
            _uBaseHeight = GL.GetUniformLocation(_shader.Handle, "u_BaseHeight");
            _uMaxHeight = GL.GetUniformLocation(_shader.Handle, "u_MaxHeight");

            // Scattering & God rays
            _uUseSunScattering = GL.GetUniformLocation(_shader.Handle, "u_UseSunScattering");
            _uSunDirection = GL.GetUniformLocation(_shader.Handle, "u_SunDirection");
            _uSunScatteringColor = GL.GetUniformLocation(_shader.Handle, "u_SunScatteringColor");
            _uScatteringIntensity = GL.GetUniformLocation(_shader.Handle, "u_ScatteringIntensity");
            _uMieG = GL.GetUniformLocation(_shader.Handle, "u_MieG");
            _uExtinctionFactor = GL.GetUniformLocation(_shader.Handle, "u_ExtinctionFactor");
            _uAmbientIntensity = GL.GetUniformLocation(_shader.Handle, "u_AmbientIntensity");

            // God rays (radial blur)
            _uSunScreenPos = GL.GetUniformLocation(_shader.Handle, "u_SunScreenPos");
            _uGodRaysIntensity = GL.GetUniformLocation(_shader.Handle, "u_GodRaysIntensity");
            _uGodRaysDensity = GL.GetUniformLocation(_shader.Handle, "u_GodRaysDensity");
            _uGodRaysDecay = GL.GetUniformLocation(_shader.Handle, "u_GodRaysDecay");

            // Noise
            _uUseNoise = GL.GetUniformLocation(_shader.Handle, "u_UseNoise");
            _uNoiseScale = GL.GetUniformLocation(_shader.Handle, "u_NoiseScale");
            _uNoiseSpeed = GL.GetUniformLocation(_shader.Handle, "u_NoiseSpeed");
            _uNoiseStrength = GL.GetUniformLocation(_shader.Handle, "u_NoiseStrength");
            _uNoiseOctaves = GL.GetUniformLocation(_shader.Handle, "u_NoiseOctaves");
        }

        public void Render(Engine.Components.PostProcessEffect effect, Engine.Components.PostProcessContext context)
        {
            if (_shader == null)
            {
                Initialize();
            }

            if (_shader == null || !(effect is VolumetricFogEffect fog))
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

            // Time and screen size
            if (_uTime >= 0) GL.Uniform1(_uTime, (float)DateTime.Now.TimeOfDay.TotalSeconds);
            if (_uScreenSize >= 0) GL.Uniform2(_uScreenSize, (float)context.Width, (float)context.Height);

            // Get effective fog parameters (considering Global/Local/Blend mode)
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

            // Ray marching parameters
            if (_uRayMarchSteps >= 0) GL.Uniform1(_uRayMarchSteps, fog.RayMarchSteps);
            if (_uMaxRayDistance >= 0) GL.Uniform1(_uMaxRayDistance, fog.MaxRayDistance);

            // Height-based fog
            if (_uUseHeightFog >= 0) GL.Uniform1(_uUseHeightFog, fog.UseHeightFog ? 1 : 0);
            if (_uHeightFalloff >= 0) GL.Uniform1(_uHeightFalloff, fog.HeightFalloff);
            if (_uBaseHeight >= 0) GL.Uniform1(_uBaseHeight, fog.BaseHeight);
            if (_uMaxHeight >= 0) GL.Uniform1(_uMaxHeight, fog.MaxHeight);

            // Scattering & God rays
            if (_uUseSunScattering >= 0) GL.Uniform1(_uUseSunScattering, fog.UseSunScattering ? 1 : 0);
            if (_uSunScatteringColor >= 0) GL.Uniform3(_uSunScatteringColor, fog.SunScatteringColor);
            if (_uScatteringIntensity >= 0) GL.Uniform1(_uScatteringIntensity, fog.ScatteringIntensity);
            if (_uMieG >= 0) GL.Uniform1(_uMieG, fog.MieG);
            if (_uExtinctionFactor >= 0) GL.Uniform1(_uExtinctionFactor, fog.ExtinctionFactor);

            // Sun direction - get from directional light in scene
            Vector3 sunDirection = new Vector3(0.3f, 0.8f, 0.3f); // Default (pointing TO sun)
            if (context.Scene != null)
            {
                foreach (var entity in context.Scene.Entities)
                {
                    var light = entity.GetComponent<Engine.Components.LightComponent>();
                    if (light != null && light.Type == Engine.Components.LightType.Directional)
                    {
                        // Light direction points FROM sun, we need direction TO sun
                        sunDirection = -Vector3.Normalize(light.Direction);
                        break;
                    }
                }
            }
            if (_uSunDirection >= 0) GL.Uniform3(_uSunDirection, sunDirection);

            // Ambient intensity
            if (_uAmbientIntensity >= 0) GL.Uniform1(_uAmbientIntensity, fog.AmbientIntensity);

            // God rays - calculate sun screen position
            Vector2 sunScreenPos = new Vector2(0.5f, 0.5f);
            if (context.ViewMatrix.HasValue && context.ProjectionMatrix.HasValue)
            {
                Vector3 sunWorldPos = cameraPos + sunDirection * 10000.0f;
                Vector4 sunClip = new Vector4(sunWorldPos, 1.0f) * context.ViewMatrix.Value * context.ProjectionMatrix.Value;

                if (sunClip.W > 0.001f)
                {
                    Vector3 sunNDC = new Vector3(sunClip.X, sunClip.Y, sunClip.Z) / sunClip.W;
                    sunScreenPos = new Vector2(sunNDC.X * 0.5f + 0.5f, sunNDC.Y * 0.5f + 0.5f);
                }
            }
            if (_uSunScreenPos >= 0) GL.Uniform2(_uSunScreenPos, sunScreenPos);
            if (_uGodRaysIntensity >= 0) GL.Uniform1(_uGodRaysIntensity, fog.GodRaysIntensity);
            if (_uGodRaysDensity >= 0) GL.Uniform1(_uGodRaysDensity, fog.GodRaysDensity);
            if (_uGodRaysDecay >= 0) GL.Uniform1(_uGodRaysDecay, fog.GodRaysDecay);

            // Noise
            if (_uUseNoise >= 0) GL.Uniform1(_uUseNoise, fog.UseNoise ? 1 : 0);
            if (_uNoiseScale >= 0) GL.Uniform1(_uNoiseScale, fog.NoiseScale);
            if (_uNoiseSpeed >= 0) GL.Uniform1(_uNoiseSpeed, fog.NoiseSpeed);
            if (_uNoiseStrength >= 0) GL.Uniform1(_uNoiseStrength, fog.NoiseStrength);
            if (_uNoiseOctaves >= 0) GL.Uniform1(_uNoiseOctaves, fog.NoiseOctaves);

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

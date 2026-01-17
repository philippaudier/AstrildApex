using System;
using System.IO;
using System.Linq;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Engine.Components;

namespace Engine.Rendering.PostProcess
{
    /// <summary>
    /// Underwater Volume Renderer - Subnautica-style volumetric underwater effects.
    /// Applies fog, god rays, light absorption, particles, and caustics when camera is underwater.
    /// </summary>
    public class UnderwaterRenderer : Engine.Components.IPostProcessRenderer
    {
        private ShaderProgram? _shader;

        // Uniform locations
        private int _uSourceTexture = -1;
        private int _uDepthTexture = -1;
        private int _uInvProjection = -1;
        private int _uInvView = -1;
        private int _uCameraPos = -1;
        private int _uTime = -1;
        private int _uScreenSize = -1;

        // Master
        private int _uWaterLevel = -1;

        // Volumetric Fog
        private int _uFogEnabled = -1;
        private int _uFogColor = -1;
        private int _uFogDensity = -1;
        private int _uVisibility = -1;
        private int _uFogSteps = -1;
        private int _uFogScattering = -1;
        private int _uFogAmbient = -1;
        private int _uFogHeightFalloff = -1;
        private int _uFogNoiseScale = -1;
        private int _uFogNoiseStrength = -1;

        // Absorption
        private int _uAbsorptionEnabled = -1;
        private int _uAbsorptionR = -1;
        private int _uAbsorptionG = -1;
        private int _uAbsorptionB = -1;

        // God rays
        private int _uGodRaysEnabled = -1;
        private int _uGodRaysIntensity = -1;
        private int _uGodRaysColor = -1;
        private int _uGodRaysDensity = -1;
        private int _uGodRaysDecay = -1;
        private int _uGodRaysSamples = -1;
        private int _uSunDirection = -1;
        private int _uSunScreenPos = -1;  // Sun position in screen space for radial blur

        // Particles (volumetric with depth and lighting)
        private int _uParticlesEnabled = -1;
        private int _uParticleDensity = -1;
        private int _uParticleColor = -1;
        private int _uParticleBrightness = -1;
        private int _uParticleSpeed = -1;
        private int _uParticleSizeMin = -1;
        private int _uParticleSizeMax = -1;
        private int _uParticleDepthLayers = -1;
        private int _uParticleLighting = -1;
        private int _uParticleScattering = -1;
        private int _uParticleTurbulence = -1;
        private int _uParticleGodRayGlow = -1;
        private int _uParticleFocusDistance = -1;
        private int _uParticleFocusRange = -1;
        private int _uParticleNearFade = -1;
        private int _uParticleFarFade = -1;
        private int _uParticleTexture = -1;
        private int _uParticleTextureEnabled = -1;

        // Caustics (GPU Gems with chromatic aberration)
        private int _uCausticsEnabled = -1;
        private int _uCausticsIntensity = -1;
        private int _uCausticsScale = -1;
        private int _uCausticsSpeed = -1;
        private int _uCausticsOctaves = -1;
        private int _uCausticsBrightness = -1;
        private int _uCausticsSharpness = -1;
        private int _uCausticsDistortion = -1;
        private int _uCausticsDepthFalloff = -1;
        private int _uCausticsChromatic = -1;

        // Tint & Ambient
        private int _uTintColor = -1;
        private int _uAmbientIntensity = -1;
        private int _uAmbientColor = -1;

        // Wave parameters (from WaterPlaneComponent for accurate caustics)
        private int _uWaveIterations = -1;
        private int _uWaveAmplitude = -1;
        private int _uWaveFrequency = -1;
        private int _uWaveSpeed = -1;
        private int _uWaveSteepness = -1;
        private int _uWaveDrag = -1;
        private int _uWaveDirection = -1;

        // Screen Distortion
        private int _uDistortionEnabled = -1;
        private int _uDistortionIntensity = -1;
        private int _uDistortionScale = -1;
        private int _uDistortionSpeed = -1;
        private int _uDistortionChromatic = -1;
        private int _uDistortionUseWaves = -1;
        private int _uDistortionWaveInfluence = -1;
        private int _uDistortionNoiseInfluence = -1;
        private int _uDistortionDepthFade = -1;

        // Snell's Window (Total Internal Reflection)
        private int _uSnellWindowEnabled = -1;
        private int _uSnellCriticalAngle = -1;
        private int _uSnellEdgeSoftness = -1;
        private int _uSnellReflectionTint = -1;
        private int _uSnellReflectionStrength = -1;
        private int _uSnellFresnelPower = -1;
        private int _uSnellWaveDistortion = -1;
        private int _uSnellUsePlanarReflection = -1;
        private int _uPlanarReflectionTex = -1;
        private int _uReflectionViewProj = -1;

        // Water Transition Effects
        private int _uTransitionEnabled = -1;
        private int _uTransitionProgress = -1;
        private int _uTransitionDirection = -1; // 1 = entering water, 0 = exiting water
        private int _uEnterBubbleIntensity = -1;
        private int _uEnterBubbleSize = -1;
        private int _uEnterBubbleCount = -1;
        private int _uEnterDistortion = -1;
        private int _uExitDropletIntensity = -1;
        private int _uExitDropletSize = -1;
        private int _uExitDropletCount = -1;
        private int _uExitDripSpeed = -1;
        private int _uExitTransitionOnly = -1; // When true, only apply exit droplets, skip underwater effects

        // Transition state tracking
        private bool _wasUnderwater = false;
        private float _transitionStartTime = 0.0f;
        private bool _transitionActive = false;
        private bool _transitionIsEntering = false;

        public void Initialize()
        {
            try
            {
                var baseDir = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "Engine", "Rendering", "Shaders", "PostProcess"));
                var vertPath = Path.Combine(baseDir, "underwater_volume.vert");
                var fragPath = Path.Combine(baseDir, "underwater_volume.frag");

                if (File.Exists(vertPath) && File.Exists(fragPath))
                {
                    var vert = File.ReadAllText(vertPath);
                    var frag = File.ReadAllText(fragPath);
                    _shader = ShaderProgram.FromSource(vert, frag);
                    Console.WriteLine("[UnderwaterRenderer] Shader loaded successfully");
                }
                else
                {
                    Console.WriteLine($"[UnderwaterRenderer] Shader files not found: {vertPath}, {fragPath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UnderwaterRenderer] Shader load failed: {ex.Message}");
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

            _uSourceTexture = GL.GetUniformLocation(_shader.Handle, "u_SourceTexture");
            _uDepthTexture = GL.GetUniformLocation(_shader.Handle, "u_DepthTexture");
            _uInvProjection = GL.GetUniformLocation(_shader.Handle, "u_InvProjection");
            _uInvView = GL.GetUniformLocation(_shader.Handle, "u_InvView");
            _uCameraPos = GL.GetUniformLocation(_shader.Handle, "u_CameraPos");
            _uTime = GL.GetUniformLocation(_shader.Handle, "u_Time");
            _uScreenSize = GL.GetUniformLocation(_shader.Handle, "u_ScreenSize");

            _uWaterLevel = GL.GetUniformLocation(_shader.Handle, "u_WaterLevel");

            _uFogEnabled = GL.GetUniformLocation(_shader.Handle, "u_FogEnabled");
            _uFogColor = GL.GetUniformLocation(_shader.Handle, "u_FogColor");
            _uFogDensity = GL.GetUniformLocation(_shader.Handle, "u_FogDensity");
            _uVisibility = GL.GetUniformLocation(_shader.Handle, "u_Visibility");
            _uFogSteps = GL.GetUniformLocation(_shader.Handle, "u_FogSteps");
            _uFogScattering = GL.GetUniformLocation(_shader.Handle, "u_FogScattering");
            _uFogAmbient = GL.GetUniformLocation(_shader.Handle, "u_FogAmbient");
            _uFogHeightFalloff = GL.GetUniformLocation(_shader.Handle, "u_FogHeightFalloff");
            _uFogNoiseScale = GL.GetUniformLocation(_shader.Handle, "u_FogNoiseScale");
            _uFogNoiseStrength = GL.GetUniformLocation(_shader.Handle, "u_FogNoiseStrength");

            _uAbsorptionEnabled = GL.GetUniformLocation(_shader.Handle, "u_AbsorptionEnabled");
            _uAbsorptionR = GL.GetUniformLocation(_shader.Handle, "u_AbsorptionR");
            _uAbsorptionG = GL.GetUniformLocation(_shader.Handle, "u_AbsorptionG");
            _uAbsorptionB = GL.GetUniformLocation(_shader.Handle, "u_AbsorptionB");

            _uGodRaysEnabled = GL.GetUniformLocation(_shader.Handle, "u_GodRaysEnabled");
            _uGodRaysIntensity = GL.GetUniformLocation(_shader.Handle, "u_GodRaysIntensity");
            _uGodRaysColor = GL.GetUniformLocation(_shader.Handle, "u_GodRaysColor");
            _uGodRaysDensity = GL.GetUniformLocation(_shader.Handle, "u_GodRaysDensity");
            _uGodRaysDecay = GL.GetUniformLocation(_shader.Handle, "u_GodRaysDecay");
            _uGodRaysSamples = GL.GetUniformLocation(_shader.Handle, "u_GodRaysSamples");
            _uSunDirection = GL.GetUniformLocation(_shader.Handle, "u_SunDirection");
            _uSunScreenPos = GL.GetUniformLocation(_shader.Handle, "u_SunScreenPos");

            _uParticlesEnabled = GL.GetUniformLocation(_shader.Handle, "u_ParticlesEnabled");
            _uParticleDensity = GL.GetUniformLocation(_shader.Handle, "u_ParticleDensity");
            _uParticleColor = GL.GetUniformLocation(_shader.Handle, "u_ParticleColor");
            _uParticleBrightness = GL.GetUniformLocation(_shader.Handle, "u_ParticleBrightness");
            _uParticleSpeed = GL.GetUniformLocation(_shader.Handle, "u_ParticleSpeed");
            _uParticleSizeMin = GL.GetUniformLocation(_shader.Handle, "u_ParticleSizeMin");
            _uParticleSizeMax = GL.GetUniformLocation(_shader.Handle, "u_ParticleSizeMax");
            _uParticleDepthLayers = GL.GetUniformLocation(_shader.Handle, "u_ParticleDepthLayers");
            _uParticleLighting = GL.GetUniformLocation(_shader.Handle, "u_ParticleLighting");
            _uParticleScattering = GL.GetUniformLocation(_shader.Handle, "u_ParticleScattering");
            _uParticleTurbulence = GL.GetUniformLocation(_shader.Handle, "u_ParticleTurbulence");
            _uParticleGodRayGlow = GL.GetUniformLocation(_shader.Handle, "u_ParticleGodRayGlow");
            _uParticleFocusDistance = GL.GetUniformLocation(_shader.Handle, "u_ParticleFocusDistance");
            _uParticleFocusRange = GL.GetUniformLocation(_shader.Handle, "u_ParticleFocusRange");
            _uParticleNearFade = GL.GetUniformLocation(_shader.Handle, "u_ParticleNearFade");
            _uParticleFarFade = GL.GetUniformLocation(_shader.Handle, "u_ParticleFarFade");
            _uParticleTexture = GL.GetUniformLocation(_shader.Handle, "u_ParticleTexture");
            _uParticleTextureEnabled = GL.GetUniformLocation(_shader.Handle, "u_ParticleTextureEnabled");

            _uCausticsEnabled = GL.GetUniformLocation(_shader.Handle, "u_CausticsEnabled");
            _uCausticsIntensity = GL.GetUniformLocation(_shader.Handle, "u_CausticsIntensity");
            _uCausticsScale = GL.GetUniformLocation(_shader.Handle, "u_CausticsScale");
            _uCausticsSpeed = GL.GetUniformLocation(_shader.Handle, "u_CausticsSpeed");
            _uCausticsOctaves = GL.GetUniformLocation(_shader.Handle, "u_CausticsOctaves");
            _uCausticsBrightness = GL.GetUniformLocation(_shader.Handle, "u_CausticsBrightness");
            _uCausticsSharpness = GL.GetUniformLocation(_shader.Handle, "u_CausticsSharpness");
            _uCausticsDistortion = GL.GetUniformLocation(_shader.Handle, "u_CausticsDistortion");
            _uCausticsDepthFalloff = GL.GetUniformLocation(_shader.Handle, "u_CausticsDepthFalloff");
            _uCausticsChromatic = GL.GetUniformLocation(_shader.Handle, "u_CausticsChromatic");

            _uTintColor = GL.GetUniformLocation(_shader.Handle, "u_TintColor");
            _uAmbientIntensity = GL.GetUniformLocation(_shader.Handle, "u_AmbientIntensity");
            _uAmbientColor = GL.GetUniformLocation(_shader.Handle, "u_AmbientColor");

            // Wave parameters
            _uWaveIterations = GL.GetUniformLocation(_shader.Handle, "u_WaveIterations");
            _uWaveAmplitude = GL.GetUniformLocation(_shader.Handle, "u_WaveAmplitude");
            _uWaveFrequency = GL.GetUniformLocation(_shader.Handle, "u_WaveFrequency");
            _uWaveSpeed = GL.GetUniformLocation(_shader.Handle, "u_WaveSpeed");
            _uWaveSteepness = GL.GetUniformLocation(_shader.Handle, "u_WaveSteepness");
            _uWaveDrag = GL.GetUniformLocation(_shader.Handle, "u_WaveDrag");
            _uWaveDirection = GL.GetUniformLocation(_shader.Handle, "u_WaveDirection");

            // Distortion
            _uDistortionEnabled = GL.GetUniformLocation(_shader.Handle, "u_DistortionEnabled");
            _uDistortionIntensity = GL.GetUniformLocation(_shader.Handle, "u_DistortionIntensity");
            _uDistortionScale = GL.GetUniformLocation(_shader.Handle, "u_DistortionScale");
            _uDistortionSpeed = GL.GetUniformLocation(_shader.Handle, "u_DistortionSpeed");
            _uDistortionChromatic = GL.GetUniformLocation(_shader.Handle, "u_DistortionChromatic");
            _uDistortionUseWaves = GL.GetUniformLocation(_shader.Handle, "u_DistortionUseWaves");
            _uDistortionWaveInfluence = GL.GetUniformLocation(_shader.Handle, "u_DistortionWaveInfluence");
            _uDistortionNoiseInfluence = GL.GetUniformLocation(_shader.Handle, "u_DistortionNoiseInfluence");
            _uDistortionDepthFade = GL.GetUniformLocation(_shader.Handle, "u_DistortionDepthFade");

            // Snell's Window
            _uSnellWindowEnabled = GL.GetUniformLocation(_shader.Handle, "u_SnellWindowEnabled");
            _uSnellCriticalAngle = GL.GetUniformLocation(_shader.Handle, "u_SnellCriticalAngle");
            _uSnellEdgeSoftness = GL.GetUniformLocation(_shader.Handle, "u_SnellEdgeSoftness");
            _uSnellReflectionTint = GL.GetUniformLocation(_shader.Handle, "u_SnellReflectionTint");
            _uSnellReflectionStrength = GL.GetUniformLocation(_shader.Handle, "u_SnellReflectionStrength");
            _uSnellFresnelPower = GL.GetUniformLocation(_shader.Handle, "u_SnellFresnelPower");
            _uSnellWaveDistortion = GL.GetUniformLocation(_shader.Handle, "u_SnellWaveDistortion");
            _uSnellUsePlanarReflection = GL.GetUniformLocation(_shader.Handle, "u_SnellUsePlanarReflection");
            _uPlanarReflectionTex = GL.GetUniformLocation(_shader.Handle, "u_PlanarReflectionTex");
            _uReflectionViewProj = GL.GetUniformLocation(_shader.Handle, "u_ReflectionViewProj");

            // Water Transition
            _uTransitionEnabled = GL.GetUniformLocation(_shader.Handle, "u_TransitionEnabled");
            _uTransitionProgress = GL.GetUniformLocation(_shader.Handle, "u_TransitionProgress");
            _uTransitionDirection = GL.GetUniformLocation(_shader.Handle, "u_TransitionDirection");
            _uEnterBubbleIntensity = GL.GetUniformLocation(_shader.Handle, "u_EnterBubbleIntensity");
            _uEnterBubbleSize = GL.GetUniformLocation(_shader.Handle, "u_EnterBubbleSize");
            _uEnterBubbleCount = GL.GetUniformLocation(_shader.Handle, "u_EnterBubbleCount");
            _uEnterDistortion = GL.GetUniformLocation(_shader.Handle, "u_EnterDistortion");
            _uExitDropletIntensity = GL.GetUniformLocation(_shader.Handle, "u_ExitDropletIntensity");
            _uExitDropletSize = GL.GetUniformLocation(_shader.Handle, "u_ExitDropletSize");
            _uExitDropletCount = GL.GetUniformLocation(_shader.Handle, "u_ExitDropletCount");
            _uExitDripSpeed = GL.GetUniformLocation(_shader.Handle, "u_ExitDripSpeed");
            _uExitTransitionOnly = GL.GetUniformLocation(_shader.Handle, "u_ExitTransitionOnly");
        }

        public void Render(Engine.Components.PostProcessEffect effect, Engine.Components.PostProcessContext context)
        {
            if (_shader == null)
            {
                Initialize();
            }

            if (_shader == null || !(effect is UnderwaterEffect underwater))
            {
                Engine.Rendering.PostProcessHelper.PassThrough(context);
                return;
            }

            // Get camera position from context or global UBO
            Vector3 cameraPos = Vector3.Zero;
            if (context.ViewMatrix.HasValue)
            {
                var invView = context.ViewMatrix.Value.Inverted();
                cameraPos = invView.ExtractTranslation();
            }

            // Determine water level and get wave parameters from WaterPlaneComponent
            float waterLevel = underwater.WaterLevel; // Default to manual
            WaterPlaneComponent? waterPlane = null;

            // Try to find WaterPlaneComponent in scene (for both water level and wave params)
            if (context.Scene != null)
            {
                // Use scene cache if available for performance
                var entities = context.Scene.Cache != null
                    ? context.Scene.Cache.GetEntitiesWithComponent<WaterPlaneComponent>()
                    : context.Scene.Entities.ToList();

                foreach (var entity in entities)
                {
                    waterPlane = entity.GetComponent<WaterPlaneComponent>();
                    if (waterPlane != null) break;
                }

                // Use auto water level if enabled
                if (underwater.Source == UnderwaterEffect.WaterLevelSource.Auto && waterPlane != null)
                {
                    waterLevel = waterPlane.GetWaterLevel();
                    underwater.DetectedWaterLevel = waterLevel; // Cache for UI display
                }
            }

            // Current underwater state
            bool isUnderwater = cameraPos.Y < waterLevel;
            float currentTime = (float)DateTime.Now.TimeOfDay.TotalSeconds;

            // Detect water surface crossing (transition)
            if (underwater.TransitionEnabled)
            {
                if (isUnderwater != _wasUnderwater)
                {
                    // Transition detected!
                    _transitionActive = true;
                    _transitionStartTime = currentTime;
                    _transitionIsEntering = isUnderwater; // true = entering water, false = exiting
                    underwater.IsEnteringWater = _transitionIsEntering;
                }

                // Update transition progress
                if (_transitionActive)
                {
                    float elapsed = currentTime - _transitionStartTime;
                    float progress = 1.0f - (elapsed / underwater.TransitionDuration);
                    progress = Math.Clamp(progress, 0.0f, 1.0f);
                    underwater.TransitionProgress = progress;

                    if (progress <= 0.0f)
                    {
                        _transitionActive = false;
                        underwater.TransitionProgress = 0.0f;
                    }
                }
            }
            else
            {
                underwater.TransitionProgress = 0.0f;
                _transitionActive = false;
            }

            _wasUnderwater = isUnderwater;

            // Skip rendering if camera is above water AND no active exit transition
            if (!isUnderwater && (!_transitionActive || _transitionIsEntering))
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

            if (_uCameraPos >= 0) GL.Uniform3(_uCameraPos, cameraPos);

            // Time and screen
            if (_uTime >= 0) GL.Uniform1(_uTime, (float)DateTime.Now.TimeOfDay.TotalSeconds);
            if (_uScreenSize >= 0) GL.Uniform2(_uScreenSize, (float)context.Width, (float)context.Height);

            // Master - use detected or manual water level
            if (_uWaterLevel >= 0) GL.Uniform1(_uWaterLevel, waterLevel);

            // Volumetric Fog
            if (_uFogEnabled >= 0) GL.Uniform1(_uFogEnabled, underwater.FogEnabled ? 1 : 0);
            if (_uFogColor >= 0) GL.Uniform3(_uFogColor, underwater.FogColor);
            if (_uFogDensity >= 0) GL.Uniform1(_uFogDensity, underwater.FogDensity);
            if (_uVisibility >= 0) GL.Uniform1(_uVisibility, underwater.Visibility);
            if (_uFogSteps >= 0) GL.Uniform1(_uFogSteps, underwater.FogSteps);
            if (_uFogScattering >= 0) GL.Uniform1(_uFogScattering, underwater.FogScattering);
            if (_uFogAmbient >= 0) GL.Uniform1(_uFogAmbient, underwater.FogAmbient);
            if (_uFogHeightFalloff >= 0) GL.Uniform1(_uFogHeightFalloff, underwater.FogHeightFalloff);
            if (_uFogNoiseScale >= 0) GL.Uniform1(_uFogNoiseScale, underwater.FogNoiseScale);
            if (_uFogNoiseStrength >= 0) GL.Uniform1(_uFogNoiseStrength, underwater.FogNoiseStrength);

            // Absorption
            if (_uAbsorptionEnabled >= 0) GL.Uniform1(_uAbsorptionEnabled, underwater.AbsorptionEnabled ? 1 : 0);
            if (_uAbsorptionR >= 0) GL.Uniform1(_uAbsorptionR, underwater.AbsorptionR);
            if (_uAbsorptionG >= 0) GL.Uniform1(_uAbsorptionG, underwater.AbsorptionG);
            if (_uAbsorptionB >= 0) GL.Uniform1(_uAbsorptionB, underwater.AbsorptionB);

            // God rays
            if (_uGodRaysEnabled >= 0) GL.Uniform1(_uGodRaysEnabled, underwater.GodRaysEnabled ? 1 : 0);
            if (_uGodRaysIntensity >= 0) GL.Uniform1(_uGodRaysIntensity, underwater.GodRaysIntensity);
            if (_uGodRaysColor >= 0) GL.Uniform3(_uGodRaysColor, underwater.GodRaysColor);
            if (_uGodRaysDensity >= 0) GL.Uniform1(_uGodRaysDensity, underwater.GodRaysDensity);
            if (_uGodRaysDecay >= 0) GL.Uniform1(_uGodRaysDecay, underwater.GodRaysDecay);
            if (_uGodRaysSamples >= 0) GL.Uniform1(_uGodRaysSamples, underwater.GodRaysSamples);

            // Sun direction - get from directional light in scene
            Vector3 sunDirection = new Vector3(0.3f, -0.8f, 0.3f); // Default
            if (context.Scene != null)
            {
                foreach (var entity in context.Scene.Entities)
                {
                    var light = entity.GetComponent<LightComponent>();
                    if (light != null && light.Type == LightType.Directional)
                    {
                        sunDirection = light.Direction;
                        break;
                    }
                }
            }
            if (_uSunDirection >= 0) GL.Uniform3(_uSunDirection, sunDirection);

            // Calculate sun's screen-space position for GPU Gems radial blur god rays
            // The sun is a directional light, so we project a point very far in the sun direction
            if (_uSunScreenPos >= 0 && context.ViewMatrix.HasValue && context.ProjectionMatrix.HasValue)
            {
                // Sun direction points FROM the sun, so negate to get direction TO the sun
                Vector3 toSun = Vector3.Normalize(-sunDirection);

                // Project a point very far in the sun direction to screen space
                // For underwater rays, we want the sun position on the water surface above us
                Vector3 sunWorldPos = cameraPos + toSun * 1000.0f;

                // Transform to clip space
                Vector4 sunClip = new Vector4(sunWorldPos, 1.0f);
                sunClip = sunClip * context.ViewMatrix.Value;
                sunClip = sunClip * context.ProjectionMatrix.Value;

                // Perspective divide and convert to screen UV [0,1]
                Vector2 sunScreenPos;
                if (sunClip.W > 0.001f)
                {
                    sunScreenPos = new Vector2(
                        (sunClip.X / sunClip.W) * 0.5f + 0.5f,
                        (sunClip.Y / sunClip.W) * 0.5f + 0.5f
                    );
                }
                else
                {
                    // Sun is behind camera - place off screen
                    sunScreenPos = new Vector2(-1.0f, -1.0f);
                }

                GL.Uniform2(_uSunScreenPos, sunScreenPos);
            }

            // Particles (volumetric with depth and lighting)
            if (_uParticlesEnabled >= 0) GL.Uniform1(_uParticlesEnabled, underwater.ParticlesEnabled ? 1 : 0);
            if (_uParticleDensity >= 0) GL.Uniform1(_uParticleDensity, underwater.ParticleDensity);
            if (_uParticleColor >= 0) GL.Uniform3(_uParticleColor, underwater.ParticleColor);
            if (_uParticleBrightness >= 0) GL.Uniform1(_uParticleBrightness, underwater.ParticleBrightness);
            if (_uParticleSpeed >= 0) GL.Uniform1(_uParticleSpeed, underwater.ParticleSpeed);
            if (_uParticleSizeMin >= 0) GL.Uniform1(_uParticleSizeMin, underwater.ParticleSizeMin);
            if (_uParticleSizeMax >= 0) GL.Uniform1(_uParticleSizeMax, underwater.ParticleSizeMax);
            if (_uParticleDepthLayers >= 0) GL.Uniform1(_uParticleDepthLayers, underwater.ParticleDepthLayers);
            if (_uParticleLighting >= 0) GL.Uniform1(_uParticleLighting, underwater.ParticleLighting);
            if (_uParticleScattering >= 0) GL.Uniform1(_uParticleScattering, underwater.ParticleScattering);
            if (_uParticleTurbulence >= 0) GL.Uniform1(_uParticleTurbulence, underwater.ParticleTurbulence);
            if (_uParticleGodRayGlow >= 0) GL.Uniform1(_uParticleGodRayGlow, underwater.ParticleGodRayGlow);
            if (_uParticleFocusDistance >= 0) GL.Uniform1(_uParticleFocusDistance, underwater.ParticleFocusDistance);
            if (_uParticleFocusRange >= 0) GL.Uniform1(_uParticleFocusRange, underwater.ParticleFocusRange);
            if (_uParticleNearFade >= 0) GL.Uniform1(_uParticleNearFade, underwater.ParticleNearFade);
            if (_uParticleFarFade >= 0) GL.Uniform1(_uParticleFarFade, underwater.ParticleFarFade);

            // Particle texture (optional)
            bool hasParticleTexture = false;
            if (underwater.ParticleTextureGuid.HasValue && underwater.ParticleTextureGuid.Value != Guid.Empty)
            {
                int particleTexHandle = TextureCache.GetOrLoad(underwater.ParticleTextureGuid.Value, guid =>
                {
                    if (Engine.Assets.AssetDatabase.TryGet(guid, out var record))
                        return record.Path;
                    return null;
                });

                if (particleTexHandle != 0 && particleTexHandle != TextureCache.White1x1)
                {
                    GL.ActiveTexture(TextureUnit.Texture2);
                    GL.BindTexture(TextureTarget.Texture2D, particleTexHandle);
                    if (_uParticleTexture >= 0) GL.Uniform1(_uParticleTexture, 2);
                    hasParticleTexture = true;
                }
            }
            if (_uParticleTextureEnabled >= 0) GL.Uniform1(_uParticleTextureEnabled, hasParticleTexture ? 1 : 0);

            // Caustics (GPU Gems with chromatic aberration)
            if (_uCausticsEnabled >= 0) GL.Uniform1(_uCausticsEnabled, underwater.CausticsEnabled ? 1 : 0);
            if (_uCausticsIntensity >= 0) GL.Uniform1(_uCausticsIntensity, underwater.CausticsIntensity);
            if (_uCausticsScale >= 0) GL.Uniform1(_uCausticsScale, underwater.CausticsScale);
            if (_uCausticsSpeed >= 0) GL.Uniform1(_uCausticsSpeed, underwater.CausticsSpeed);
            if (_uCausticsOctaves >= 0) GL.Uniform1(_uCausticsOctaves, underwater.CausticsOctaves);
            if (_uCausticsBrightness >= 0) GL.Uniform1(_uCausticsBrightness, underwater.CausticsBrightness);
            if (_uCausticsSharpness >= 0) GL.Uniform1(_uCausticsSharpness, underwater.CausticsSharpness);
            if (_uCausticsDistortion >= 0) GL.Uniform1(_uCausticsDistortion, underwater.CausticsDistortion);
            if (_uCausticsDepthFalloff >= 0) GL.Uniform1(_uCausticsDepthFalloff, underwater.CausticsDepthFalloff);
            if (_uCausticsChromatic >= 0) GL.Uniform1(_uCausticsChromatic, underwater.CausticsChromatic);

            // Tint & Ambient
            if (_uTintColor >= 0) GL.Uniform3(_uTintColor, underwater.TintColor);
            if (_uAmbientIntensity >= 0) GL.Uniform1(_uAmbientIntensity, underwater.AmbientIntensity);
            if (_uAmbientColor >= 0) GL.Uniform3(_uAmbientColor, underwater.AmbientColor);

            // Wave parameters from WaterPlaneComponent (for accurate Gerstner caustics)
            if (waterPlane != null)
            {
                if (_uWaveIterations >= 0) GL.Uniform1(_uWaveIterations, waterPlane.WaveIterations);
                if (_uWaveAmplitude >= 0) GL.Uniform1(_uWaveAmplitude, waterPlane.WaveAmplitude);
                if (_uWaveFrequency >= 0) GL.Uniform1(_uWaveFrequency, waterPlane.WaveFrequency);
                if (_uWaveSpeed >= 0) GL.Uniform1(_uWaveSpeed, waterPlane.WaveSpeed);
                if (_uWaveSteepness >= 0) GL.Uniform1(_uWaveSteepness, waterPlane.WaveSteepness);
                if (_uWaveDrag >= 0) GL.Uniform1(_uWaveDrag, waterPlane.WaveDrag);
                if (_uWaveDirection >= 0) GL.Uniform2(_uWaveDirection, waterPlane.WaveDirectionX, waterPlane.WaveDirectionZ);
            }
            else
            {
                // Default wave parameters if no WaterPlaneComponent found
                if (_uWaveIterations >= 0) GL.Uniform1(_uWaveIterations, 8);
                if (_uWaveAmplitude >= 0) GL.Uniform1(_uWaveAmplitude, 1.0f);
                if (_uWaveFrequency >= 0) GL.Uniform1(_uWaveFrequency, 1.0f);
                if (_uWaveSpeed >= 0) GL.Uniform1(_uWaveSpeed, 2.0f);
                if (_uWaveSteepness >= 0) GL.Uniform1(_uWaveSteepness, 0.5f);
                if (_uWaveDrag >= 0) GL.Uniform1(_uWaveDrag, 0.38f);
                if (_uWaveDirection >= 0) GL.Uniform2(_uWaveDirection, 1.0f, 0.3f);
            }

            // Screen Distortion
            if (_uDistortionEnabled >= 0) GL.Uniform1(_uDistortionEnabled, underwater.DistortionEnabled ? 1 : 0);
            if (_uDistortionIntensity >= 0) GL.Uniform1(_uDistortionIntensity, underwater.DistortionIntensity);
            if (_uDistortionScale >= 0) GL.Uniform1(_uDistortionScale, underwater.DistortionScale);
            if (_uDistortionSpeed >= 0) GL.Uniform1(_uDistortionSpeed, underwater.DistortionSpeed);
            if (_uDistortionChromatic >= 0) GL.Uniform1(_uDistortionChromatic, underwater.DistortionChromatic);
            if (_uDistortionUseWaves >= 0) GL.Uniform1(_uDistortionUseWaves, underwater.DistortionUseWaves ? 1 : 0);
            if (_uDistortionWaveInfluence >= 0) GL.Uniform1(_uDistortionWaveInfluence, underwater.DistortionWaveInfluence);
            if (_uDistortionNoiseInfluence >= 0) GL.Uniform1(_uDistortionNoiseInfluence, underwater.DistortionNoiseInfluence);
            if (_uDistortionDepthFade >= 0) GL.Uniform1(_uDistortionDepthFade, underwater.DistortionDepthFade);

            // Snell's Window (Total Internal Reflection)
            if (_uSnellWindowEnabled >= 0) GL.Uniform1(_uSnellWindowEnabled, underwater.SnellWindowEnabled ? 1 : 0);
            if (_uSnellCriticalAngle >= 0) GL.Uniform1(_uSnellCriticalAngle, underwater.SnellCriticalAngle);
            if (_uSnellEdgeSoftness >= 0) GL.Uniform1(_uSnellEdgeSoftness, underwater.SnellEdgeSoftness);
            if (_uSnellReflectionTint >= 0) GL.Uniform3(_uSnellReflectionTint, underwater.SnellReflectionTint);
            if (_uSnellReflectionStrength >= 0) GL.Uniform1(_uSnellReflectionStrength, underwater.SnellReflectionStrength);
            if (_uSnellFresnelPower >= 0) GL.Uniform1(_uSnellFresnelPower, underwater.SnellFresnelPower);
            if (_uSnellWaveDistortion >= 0) GL.Uniform1(_uSnellWaveDistortion, underwater.SnellWaveDistortion);

            // Planar reflection for Snell's window
            bool hasPlanarReflection = ReflectionBuffer.ReflectionTexture != 0;
            if (_uSnellUsePlanarReflection >= 0) GL.Uniform1(_uSnellUsePlanarReflection, hasPlanarReflection ? 1 : 0);

            if (hasPlanarReflection)
            {
                GL.ActiveTexture(TextureUnit.Texture3);
                GL.BindTexture(TextureTarget.Texture2D, ReflectionBuffer.ReflectionTexture);
                if (_uPlanarReflectionTex >= 0) GL.Uniform1(_uPlanarReflectionTex, 3);

                if (_uReflectionViewProj >= 0)
                {
                    var reflViewProj = ReflectionBuffer.ReflectionViewProj;
                    GL.UniformMatrix4(_uReflectionViewProj, false, ref reflViewProj);
                }
            }

            // Water Transition
            if (_uTransitionEnabled >= 0) GL.Uniform1(_uTransitionEnabled, underwater.TransitionEnabled && _transitionActive ? 1 : 0);
            if (_uTransitionProgress >= 0) GL.Uniform1(_uTransitionProgress, underwater.TransitionProgress);
            if (_uTransitionDirection >= 0) GL.Uniform1(_uTransitionDirection, _transitionIsEntering ? 1 : 0);
            if (_uEnterBubbleIntensity >= 0) GL.Uniform1(_uEnterBubbleIntensity, underwater.EnterBubbleIntensity);
            if (_uEnterBubbleSize >= 0) GL.Uniform1(_uEnterBubbleSize, underwater.EnterBubbleSize);
            if (_uEnterBubbleCount >= 0) GL.Uniform1(_uEnterBubbleCount, underwater.EnterBubbleCount);
            if (_uEnterDistortion >= 0) GL.Uniform1(_uEnterDistortion, underwater.EnterDistortion);
            if (_uExitDropletIntensity >= 0) GL.Uniform1(_uExitDropletIntensity, underwater.ExitDropletIntensity);
            if (_uExitDropletSize >= 0) GL.Uniform1(_uExitDropletSize, underwater.ExitDropletSize);
            if (_uExitDropletCount >= 0) GL.Uniform1(_uExitDropletCount, underwater.ExitDropletCount);
            if (_uExitDripSpeed >= 0) GL.Uniform1(_uExitDripSpeed, underwater.ExitDripSpeed);

            // Exit transition only mode: when above water but in exit transition
            bool exitTransitionOnly = !isUnderwater && _transitionActive && !_transitionIsEntering;
            if (_uExitTransitionOnly >= 0) GL.Uniform1(_uExitTransitionOnly, exitTransitionOnly ? 1 : 0);

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

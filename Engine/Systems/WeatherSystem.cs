using System;
using System.Linq;

namespace Engine.Systems
{
    /// <summary>
    /// ECS System that updates weather parameters, handles transitions,
    /// and manages automatic weather changes.
    /// Updates WeatherManager with current weather state for rendering.
    /// </summary>
    public sealed class WeatherSystem
    {
        private float _autoTransitionTimer = 0.0f;
        private const float AUTO_TRANSITION_INTERVAL = 120.0f; // 2 minutes between weather changes

        /// <summary>
        /// Update weather system - called every frame
        /// </summary>
        public void Update(Scene.Scene scene, float deltaTime)
        {
            if (scene == null || scene.Entities == null)
                return;

            // Find active weather component in scene
            Components.WeatherComponent? weatherComponent = null;
            foreach (var entity in scene.Entities)
            {
                if (!entity.Active) continue;
                
                var weather = entity.GetComponent<Components.WeatherComponent>();
                if (weather != null)
                {
                    weatherComponent = weather;
                    break;
                }
            }

            // If no weather component exists, use default weather
            if (weatherComponent == null)
            {
                WeatherManager.SetDefaultWeather();
                return;
            }

            // Handle weather transitions
            if (weatherComponent.TargetState != null)
            {
                UpdateWeatherTransition(weatherComponent, deltaTime);
            }

            // Handle automatic weather transitions
            if (weatherComponent.EnableAutoTransitions && weatherComponent.TargetState == null)
            {
                _autoTransitionTimer += deltaTime;
                if (_autoTransitionTimer >= AUTO_TRANSITION_INTERVAL)
                {
                    _autoTransitionTimer = 0.0f;
                    TransitionToRandomWeather(weatherComponent);
                }
            }

            // Update wetness based on rain
            UpdateWetness(weatherComponent, deltaTime);

            // Update snow coverage based on snow intensity
            UpdateSnowCoverage(weatherComponent, deltaTime);

            // Control particle systems based on weather
            UpdateParticleSystems(scene, weatherComponent);

            // Update global weather manager with current state
            WeatherManager.UpdateFromComponent(weatherComponent);
        }

        /// <summary>
        /// Smoothly transition weather parameters towards target state
        /// </summary>
        private void UpdateWeatherTransition(Components.WeatherComponent weather, float deltaTime)
        {
            if (weather.TargetState == null)
                return;

            var target = weather.TargetState;
            target.ElapsedTime += deltaTime;

            float t = Math.Min(target.ElapsedTime / target.TransitionDuration, 1.0f);
            
            // Smooth interpolation (ease in-out)
            float smoothT = t * t * (3.0f - 2.0f * t);

            // Interpolate all weather parameters
            weather.WindStrength = Lerp(weather.WindStrength, target.WindStrength, smoothT);
            weather.WindDirectionX = Lerp(weather.WindDirectionX, target.WindDirectionX, smoothT);
            weather.WindDirectionZ = Lerp(weather.WindDirectionZ, target.WindDirectionZ, smoothT);
            weather.WindSpeed = Lerp(weather.WindSpeed, target.WindSpeed, smoothT);
            weather.WindGustiness = Lerp(weather.WindGustiness, target.WindGustiness, smoothT);
            
            weather.RainIntensity = Lerp(weather.RainIntensity, target.RainIntensity, smoothT);
            weather.SnowIntensity = Lerp(weather.SnowIntensity, target.SnowIntensity, smoothT);
            // NOTE: SnowAccumulation is NOT interpolated - it's managed by UpdateSnowCoverage() based on melt/accumulation rates
            weather.Wetness = Lerp(weather.Wetness, target.Wetness, smoothT);
            
            weather.FogEnabled = target.FogEnabled; // Boolean - snap immediately
            weather.FogDensity = Lerp(weather.FogDensity, target.FogDensity, smoothT);
            weather.FogStart = Lerp(weather.FogStart, target.FogStart, smoothT);
            weather.FogEnd = Lerp(weather.FogEnd, target.FogEnd, smoothT);
            weather.FogColor = LerpVec3(weather.FogColor, target.FogColor, smoothT);

            // Transition complete
            if (t >= 1.0f)
            {
                weather.TargetState = null;
            }
        }

        /// <summary>
        /// Update surface wetness based on rain intensity
        /// </summary>
        private void UpdateWetness(Components.WeatherComponent weather, float deltaTime)
        {
            if (weather.RainIntensity > 0.01f)
            {
                // Rain is falling - increase wetness
                float targetWetness = weather.RainIntensity;
                weather.Wetness = Lerp(weather.Wetness, targetWetness, weather.RainWetnessSpeed * deltaTime);
            }
            else
            {
                // No rain - surfaces dry over time
                weather.Wetness = Lerp(weather.Wetness, 0.0f, weather.RainDryingSpeed * deltaTime);
            }

            // Clamp wetness
            weather.Wetness = Math.Clamp(weather.Wetness, 0.0f, 1.0f);
        }

        /// <summary>
        /// Update snow accumulation based on snow intensity.
        /// DECOUPLED SYSTEM:
        /// - SnowIntensity = rate of snowfall (0-1, how fast snow falls)
        /// - SnowAccumulation = total snow on ground (can exceed 1.0 for thick layers)
        /// When snowing: Accumulation increases continuously (no upper limit)
        /// When not snowing: Accumulation decreases (melting)
        /// </summary>
        private void UpdateSnowCoverage(Components.WeatherComponent weather, float deltaTime)
        {
            float oldAccumulation = weather.SnowAccumulation;

            if (weather.SnowIntensity > 0.01f)
            {
                // Snow is falling - increase accumulation continuously based on intensity
                // Accumulation rate = SnowIntensity * SnowAccumulationSpeed
                float accumulationRate = weather.SnowIntensity * weather.SnowAccumulationSpeed;
                weather.SnowAccumulation += accumulationRate * deltaTime;

                // NO CLAMP ON MAXIMUM - accumulation can exceed 1.0 for thick snow layers
                // Only clamp to minimum (can't go negative)
                weather.SnowAccumulation = Math.Max(0.0f, weather.SnowAccumulation);
            }
            else
            {
                // No snow - snow melts over time (gradual decrease)
                weather.SnowAccumulation = Lerp(weather.SnowAccumulation, 0.0f, weather.SnowMeltSpeed * deltaTime);

                // Only clamp to minimum (let it melt naturally to 0)
                weather.SnowAccumulation = Math.Max(0.0f, weather.SnowAccumulation);
            }

            // DEBUG LOG: Print every 60 frames (~1 second)
            // if (_debugFrameCounter++ % 60 == 0)
            // {
            //     System.Console.WriteLine($"[WeatherSystem] SnowIntensity={weather.SnowIntensity:F3}, SnowAccumulation: {oldAccumulation:F3} -> {weather.SnowAccumulation:F3}");
            // }
        }

        /// <summary>
        /// Update particle systems based on weather conditions
        /// </summary>
        private void UpdateParticleSystems(Scene.Scene scene, Components.WeatherComponent weather)
        {
            // Rain particle system control
            if (weather.RainParticleSystem != null)
            {
                var particleSystem = weather.RainParticleSystem.GetComponent<Components.ParticleSystem>();
                if (particleSystem != null)
                {
                    bool shouldPlay = weather.RainIntensity > 0.01f;
                    
                    if (shouldPlay && !particleSystem.IsPlaying)
                    {
                        particleSystem.Play();
                    }
                    else if (!shouldPlay && particleSystem.IsPlaying)
                    {
                        particleSystem.Stop();
                    }

                    // Optionally adjust emission rate based on rain intensity
                    if (particleSystem.IsPlaying)
                    {
                        float baseEmissionRate = 100.0f; // Base particles per second
                        particleSystem.EmissionRate = baseEmissionRate * weather.RainIntensity;
                    }
                }
            }

            // Snow particle system control
            if (weather.SnowParticleSystem != null)
            {
                var particleSystem = weather.SnowParticleSystem.GetComponent<Components.ParticleSystem>();
                if (particleSystem != null)
                {
                    bool shouldPlay = weather.SnowIntensity > 0.01f;
                    
                    if (shouldPlay && !particleSystem.IsPlaying)
                    {
                        particleSystem.Play();
                    }
                    else if (!shouldPlay && particleSystem.IsPlaying)
                    {
                        particleSystem.Stop();
                    }

                    // Optionally adjust emission rate based on snow intensity
                    if (particleSystem.IsPlaying)
                    {
                        float baseEmissionRate = 80.0f; // Base particles per second (slower than rain)
                        particleSystem.EmissionRate = baseEmissionRate * weather.SnowIntensity;
                    }
                }
            }
        }

        /// <summary>
        /// Transition to a random weather preset
        /// </summary>
        private void TransitionToRandomWeather(Components.WeatherComponent weather)
        {
            var presets = Components.WeatherPreset.GetAllPresets();
            var random = new Random();
            var randomPreset = presets[random.Next(presets.Length)];
            
            // Use longer transition for automatic changes
            weather.TransitionToPreset(randomPreset, customDuration: 20.0f);
        }

        /// <summary>
        /// Linear interpolation
        /// </summary>
        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * Math.Clamp(t, 0.0f, 1.0f);
        }

        /// <summary>
        /// Vector3 linear interpolation
        /// </summary>
        private static System.Numerics.Vector3 LerpVec3(System.Numerics.Vector3 a, System.Numerics.Vector3 b, float t)
        {
            t = Math.Clamp(t, 0.0f, 1.0f);
            return new System.Numerics.Vector3(
                a.X + (b.X - a.X) * t,
                a.Y + (b.Y - a.Y) * t,
                a.Z + (b.Z - a.Z) * t
            );
        }
    }

    /// <summary>
    /// Global singleton manager for accessing current weather state from anywhere.
    /// Used by rendering systems to apply weather effects.
    /// </summary>
    public static class WeatherManager
    {
        private static readonly object _lock = new object();
        
        // Current weather state (thread-safe)
        private static WeatherState _currentState = new WeatherState();

        /// <summary>
        /// Get current weather state (thread-safe)
        /// </summary>
        public static WeatherState GetCurrentWeather()
        {
            lock (_lock)
            {
                return _currentState.Clone();
            }
        }

        /// <summary>
        /// Update weather state from component (called by WeatherSystem or editor to push live values)
        /// </summary>
        public static void UpdateFromComponent(Components.WeatherComponent component)
        {
            lock (_lock)
            {
                _currentState.WindStrength = component.WindStrength;
                _currentState.WindDirectionX = component.WindDirectionX;
                _currentState.WindDirectionZ = component.WindDirectionZ;
                _currentState.WindSpeed = component.WindSpeed;
                _currentState.WindGustiness = component.WindGustiness;
                
                _currentState.BranchAmplitude = component.BranchAmplitude;
                _currentState.BranchSpeed = component.BranchSpeed;
                _currentState.BranchTurbulence = component.BranchTurbulence;
                _currentState.TrunkStiffness = component.TrunkStiffness;
                _currentState.TrunkBendAmount = component.TrunkBendAmount;
                _currentState.LeafFlutter = component.LeafFlutter;
                _currentState.LeafFlutterSpeed = component.LeafFlutterSpeed;
                
                _currentState.RainIntensity = component.RainIntensity;
                _currentState.SnowIntensity = component.SnowIntensity;
                _currentState.SnowAccumulation = component.SnowAccumulation;
                _currentState.Wetness = component.Wetness;
                _currentState.PuddleDepth = component.PuddleDepth;

                _currentState.SnowSlopeMin = component.SnowSlopeMin;
                _currentState.SnowSlopeMax = component.SnowSlopeMax;
                _currentState.SnowSparkle = component.SnowSparkle;
                _currentState.SnowDisplacement = component.SnowDisplacement;
                _currentState.SnowMapMaterial = component.SnowMapMaterial;
                
                _currentState.FogEnabled = component.FogEnabled;
                _currentState.FogDensity = component.FogDensity;
                _currentState.FogStart = component.FogStart;
                _currentState.FogEnd = component.FogEnd;
                _currentState.FogColor = component.FogColor;

                // Cloud parameters
                _currentState.CloudEnabled = component.CloudEnabled;
                _currentState.CloudCoverage = component.CloudCoverage;
                _currentState.CloudDensity = component.CloudDensity;
                _currentState.CloudType = component.CloudType;
                _currentState.CloudScattering = component.CloudScattering;
                _currentState.CloudAmbient = component.CloudAmbient;
                _currentState.CloudSpeed = component.CloudSpeed;
                _currentState.CloudTurbulence = component.CloudTurbulence;
                _currentState.CloudDetailSpeed = component.CloudDetailSpeed;
                _currentState.CloudNoiseScale = component.CloudNoiseScale;
                _currentState.CloudMorphSpeed = component.CloudMorphSpeed;
                _currentState.CloudEdgeSoftness = component.CloudEdgeSoftness;
                _currentState.CloudBillowiness = component.CloudBillowiness;
                _currentState.CloudDetailStrength = component.CloudDetailStrength;
            }
        }

        /// <summary>
        /// Set default weather (clear, calm)
        /// </summary>
        internal static void SetDefaultWeather()
        {
            lock (_lock)
            {
                var defaultPreset = Components.WeatherPreset.Clear;
                _currentState.WindStrength = defaultPreset.WindStrength;
                _currentState.WindDirectionX = defaultPreset.WindDirectionX;
                _currentState.WindDirectionZ = defaultPreset.WindDirectionZ;
                _currentState.WindSpeed = defaultPreset.WindSpeed;
                _currentState.WindGustiness = defaultPreset.WindGustiness;
                
                _currentState.RainIntensity = defaultPreset.RainIntensity;
                _currentState.SnowIntensity = defaultPreset.SnowIntensity;
                _currentState.SnowAccumulation = defaultPreset.SnowAccumulation;
                _currentState.Wetness = defaultPreset.Wetness;
                _currentState.PuddleDepth = 0.0f;

                _currentState.SnowSlopeMin = 0.0f;
                _currentState.SnowSlopeMax = 45.0f;
                _currentState.SnowSparkle = 0.5f;
                _currentState.SnowDisplacement = 0.02f;
                
                _currentState.FogEnabled = defaultPreset.FogEnabled;
                _currentState.FogDensity = defaultPreset.FogDensity;
                _currentState.FogStart = defaultPreset.FogStart;
                _currentState.FogEnd = defaultPreset.FogEnd;
                _currentState.FogColor = defaultPreset.FogColor;

                _currentState.CloudEnabled = defaultPreset.CloudEnabled;
                _currentState.CloudCoverage = defaultPreset.CloudCoverage;
                _currentState.CloudDensity = defaultPreset.CloudDensity;
                _currentState.CloudType = defaultPreset.CloudType;
                _currentState.CloudScattering = defaultPreset.CloudScattering;
            }
        }

        /// <summary>
        /// Reset weather manager (useful for scene transitions)
        /// </summary>
        public static void Reset()
        {
            SetDefaultWeather();
        }
    }

    /// <summary>
    /// Immutable weather state snapshot
    /// </summary>
    public sealed class WeatherState
    {
        public float WindStrength { get; set; }
        public float WindDirectionX { get; set; }
        public float WindDirectionZ { get; set; }
        public float WindSpeed { get; set; }
        public float WindGustiness { get; set; }
        
        // Advanced wind parameters (matching WeatherComponent defaults)
        public float BranchAmplitude { get; set; } = 2.5f;
        public float BranchSpeed { get; set; } = 4.0f;
        public float BranchTurbulence { get; set; } = 0.8f;
        public float TrunkStiffness { get; set; } = 0.85f;
        public float TrunkBendAmount { get; set; } = 0.3f;
        public float LeafFlutter { get; set; } = 0.6f;
        public float LeafFlutterSpeed { get; set; } = 8.0f;
        
        public float RainIntensity { get; set; }
        public float SnowIntensity { get; set; }
        public float SnowAccumulation { get; set; }
        public float Wetness { get; set; }
        public float PuddleDepth { get; set; }

        // Advanced snow parameters
        public float SnowSlopeMin { get; set; } = 0.0f;
        public float SnowSlopeMax { get; set; } = 45.0f;
        public float SnowSparkle { get; set; } = 0.5f;
        public float SnowDisplacement { get; set; } = 0.02f;

        // Snow material reference
        public Guid? SnowMapMaterial { get; set; } = null;
        
        public bool FogEnabled { get; set; }
        public float FogDensity { get; set; }
        public float FogStart { get; set; }
        public float FogEnd { get; set; }
        public System.Numerics.Vector3 FogColor { get; set; }

        // Cloud parameters
        public bool CloudEnabled { get; set; }
        public float CloudCoverage { get; set; }
        public float CloudDensity { get; set; }
        public Components.CloudType CloudType { get; set; }
        public float CloudScattering { get; set; }
        public float CloudAmbient { get; set; }
        public float CloudSpeed { get; set; }
        public float CloudTurbulence { get; set; }
        public float CloudDetailSpeed { get; set; }

        // Cloud fine-tune parameters
        public float CloudNoiseScale { get; set; } = 1.0f;
        public float CloudMorphSpeed { get; set; } = 0.5f;
        public float CloudEdgeSoftness { get; set; } = 0.5f;
        public float CloudBillowiness { get; set; } = 0.6f;
        public float CloudDetailStrength { get; set; } = 0.5f;

        /// <summary>
        /// Get normalized wind direction
        /// </summary>
        public System.Numerics.Vector2 GetWindDirection()
        {
            var dir = new System.Numerics.Vector2(WindDirectionX, WindDirectionZ);
            float len = dir.Length();
            return len > 0.001f ? dir / len : new System.Numerics.Vector2(1, 0);
        }

        /// <summary>
        /// Clone this weather state
        /// </summary>
        public WeatherState Clone()
        {
            return new WeatherState
            {
                WindStrength = this.WindStrength,
                WindDirectionX = this.WindDirectionX,
                WindDirectionZ = this.WindDirectionZ,
                WindSpeed = this.WindSpeed,
                WindGustiness = this.WindGustiness,
                
                BranchAmplitude = this.BranchAmplitude,
                BranchSpeed = this.BranchSpeed,
                BranchTurbulence = this.BranchTurbulence,
                TrunkStiffness = this.TrunkStiffness,
                TrunkBendAmount = this.TrunkBendAmount,
                LeafFlutter = this.LeafFlutter,
                LeafFlutterSpeed = this.LeafFlutterSpeed,
                
                RainIntensity = this.RainIntensity,
                SnowIntensity = this.SnowIntensity,
                SnowAccumulation = this.SnowAccumulation,
                Wetness = this.Wetness,
                PuddleDepth = this.PuddleDepth,

                SnowSlopeMin = this.SnowSlopeMin,
                SnowSlopeMax = this.SnowSlopeMax,
                SnowSparkle = this.SnowSparkle,
                SnowDisplacement = this.SnowDisplacement,
                SnowMapMaterial = this.SnowMapMaterial,
                
                FogEnabled = this.FogEnabled,
                FogDensity = this.FogDensity,
                FogStart = this.FogStart,
                FogEnd = this.FogEnd,
                FogColor = this.FogColor,

                CloudEnabled = this.CloudEnabled,
                CloudCoverage = this.CloudCoverage,
                CloudDensity = this.CloudDensity,
                CloudType = this.CloudType,
                CloudScattering = this.CloudScattering,
                CloudAmbient = this.CloudAmbient,
                CloudSpeed = this.CloudSpeed,
                CloudTurbulence = this.CloudTurbulence,
                CloudDetailSpeed = this.CloudDetailSpeed,
                CloudNoiseScale = this.CloudNoiseScale,
                CloudMorphSpeed = this.CloudMorphSpeed,
                CloudEdgeSoftness = this.CloudEdgeSoftness,
                CloudBillowiness = this.CloudBillowiness,
                CloudDetailStrength = this.CloudDetailStrength
            };
        }
    }
}

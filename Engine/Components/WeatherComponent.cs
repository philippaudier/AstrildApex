using System;
using System.Numerics;

namespace Engine.Components
{
    /// <summary>
    /// Component for weather system control.
    /// One WeatherComponent per scene controls global weather parameters.
    /// </summary>
    public sealed class WeatherComponent : Component
    {
        // === WIND PARAMETERS ===
        
        [Serialization.SerializableAttribute("windStrength")]
        public float WindStrength { get; set; } = 0.3f; // 0.0 to 1.0 - overall wind intensity

        [Serialization.SerializableAttribute("windDirectionX")]
        public float WindDirectionX { get; set; } = 1.0f; // Wind direction X component (normalized)

        [Serialization.SerializableAttribute("windDirectionZ")]
        public float WindDirectionZ { get; set; } = 0.0f; // Wind direction Z component (normalized)

        [Serialization.SerializableAttribute("windSpeed")]
        public float WindSpeed { get; set; } = 1.0f; // Animation speed multiplier

        [Serialization.SerializableAttribute("windGustiness")]
        public float WindGustiness { get; set; } = 0.5f; // 0.0 = smooth, 1.0 = gusty turbulence

        // === ADVANCED WIND PARAMETERS (Vegetation) ===
        
        [Serialization.SerializableAttribute("branchAmplitude")]
        public float BranchAmplitude { get; set; } = 2.5f; // Branch sway amplitude multiplier (increased for more visible movement)
        
        [Serialization.SerializableAttribute("branchSpeed")]
        public float BranchSpeed { get; set; } = 4.0f; // Branch oscillation speed (increased for more activity)
        
        [Serialization.SerializableAttribute("branchTurbulence")]
        public float BranchTurbulence { get; set; } = 0.8f; // Branch detail/noise intensity (increased for more chaos)
        
        [Serialization.SerializableAttribute("trunkStiffness")]
        public float TrunkStiffness { get; set; } = 0.85f; // Trunk rigidity (0=flexible, 1=rigid) - stiffer trunk, more flexible branches
        
        [Serialization.SerializableAttribute("trunkBendAmount")]
        public float TrunkBendAmount { get; set; } = 0.3f; // How much trunk bends at top (reduced to keep trunk stable)
        
        [Serialization.SerializableAttribute("leafFlutter")]
        public float LeafFlutter { get; set; } = 0.6f; // Leaf flutter intensity (doubled for visible individual leaf movement)
        
        [Serialization.SerializableAttribute("leafFlutterSpeed")]
        public float LeafFlutterSpeed { get; set; } = 8.0f; // Leaf flutter speed (increased for more rapid flutter)

        // === RAIN PARAMETERS ===
        
        [Serialization.SerializableAttribute("rainIntensity")]
        public float RainIntensity { get; set; } = 0.0f; // 0.0 = no rain, 1.0 = heavy rain

        [Serialization.SerializableAttribute("rainWetnessSpeed")]
        public float RainWetnessSpeed { get; set; } = 0.2f; // How fast surfaces get wet

        [Serialization.SerializableAttribute("rainDryingSpeed")]
        public float RainDryingSpeed { get; set; } = 0.1f; // How fast surfaces dry

        // === SNOW PARAMETERS ===

        [Serialization.SerializableAttribute("snowIntensity")]
        public float SnowIntensity { get; set; } = 0.0f; // 0.0 = no snowfall, 1.0 = heavy snowfall (rate of falling)

        [Serialization.SerializableAttribute("snowAccumulation")]
        public float SnowAccumulation { get; set; } = 0.0f; // Accumulated snow on ground (can exceed 1.0 for thick layers)

        // Legacy compatibility - old scenes may use "snowCoverage"
        [Serialization.SerializableAttribute("snowCoverage")]
        private float SnowCoverage_Legacy
        {
            get => SnowAccumulation;
            set => SnowAccumulation = value; // Redirect to SnowAccumulation
        }

        [Serialization.SerializableAttribute("snowAccumulationSpeed")]
        public float SnowAccumulationSpeed { get; set; } = 0.1f; // How fast snow accumulates when snowing

        [Serialization.SerializableAttribute("snowMeltSpeed")]
        public float SnowMeltSpeed { get; set; } = 0.05f; // How fast snow melts when not snowing

        [Serialization.SerializableAttribute("snowSlopeMin")]
        public float SnowSlopeMin { get; set; } = 0.0f; // Minimum slope angle (degrees) where snow can accumulate

        [Serialization.SerializableAttribute("snowSlopeMax")]
        public float SnowSlopeMax { get; set; } = 45.0f; // Maximum slope angle (degrees) where snow can accumulate

        [Serialization.SerializableAttribute("snowSparkle")]
        public float SnowSparkle { get; set; } = 0.5f; // Snow sparkle/glitter effect intensity (0.0 = off, 1.0 = maximum)

        [Serialization.SerializableAttribute("snowDisplacement")]
        public float SnowDisplacement { get; set; } = 0.5f; // Snow height displacement in world units (meters) - 0.5m = 50cm thick snow

        // === SURFACE EFFECTS ===
        
        [Serialization.SerializableAttribute("wetness")]
        public float Wetness { get; set; } = 0.0f; // Surface wetness 0.0 to 1.0 (affects smoothness/reflections)

        [Serialization.SerializableAttribute("puddleDepth")]
        public float PuddleDepth { get; set; } = 0.0f; // Depth of water puddles (future feature)

        // === WEATHER TRANSITIONS ===
        
        [Serialization.SerializableAttribute("transitionSpeed")]
        public float TransitionSpeed { get; set; } = 1.0f; // Speed of weather transitions (0.1 = slow, 2.0 = fast)

        [Serialization.SerializableAttribute("enableAutoTransitions")]
        public bool EnableAutoTransitions { get; set; } = false; // Automatic weather changes over time

        // === FOG/ATMOSPHERE ===
        
        [Serialization.SerializableAttribute("fogEnabled")]
        public bool FogEnabled { get; set; } = false; // Enable/disable fog
        
        [Serialization.SerializableAttribute("fogDensity")]
        public float FogDensity { get; set; } = 0.1f; // Atmospheric fog density (exponential fog)

        [Serialization.SerializableAttribute("fogColor")]
        public Vector3 FogColor { get; set; } = new Vector3(0.7f, 0.7f, 0.8f); // Fog color
        
        [Serialization.SerializableAttribute("fogStart")]
        public float FogStart { get; set; } = 10.0f; // Linear fog start distance
        
        [Serialization.SerializableAttribute("fogEnd")]
        public float FogEnd { get; set; } = 300.0f; // Linear fog end distance
        
        // === MATERIAL REFERENCES ===
        
        [Serialization.SerializableAttribute("wetnessMapMaterial")]
        public Guid? WetnessMapMaterial { get; set; } = null; // Material for wetness/puddle effects
        
        [Serialization.SerializableAttribute("snowMapMaterial")]
        public Guid? SnowMapMaterial { get; set; } = null; // Material for snow accumulation texture
        
        // === PARTICLE SYSTEM REFERENCES ===
        
        [Serialization.SerializableAttribute("rainParticleSystem")]
        public Scene.Entity? RainParticleSystem { get; set; } = null; // Entity with ParticleSystem for rain
        
        [Serialization.SerializableAttribute("snowParticleSystem")]
        public Scene.Entity? SnowParticleSystem { get; set; } = null; // Entity with ParticleSystem for snow

        // === RUNTIME STATE ===
        
        /// <summary>
        /// Target values for smooth transitions
        /// </summary>
        [NonSerialized]
        public WeatherTargetState? TargetState = null;

        /// <summary>
        /// Get normalized wind direction vector
        /// </summary>
        public Vector2 GetWindDirection()
        {
            var dir = new Vector2(WindDirectionX, WindDirectionZ);
            float len = dir.Length();
            return len > 0.001f ? dir / len : new Vector2(1, 0);
        }

        /// <summary>
        /// Apply a weather preset instantly
        /// </summary>
        public void ApplyPreset(WeatherPreset preset)
        {
            WindStrength = preset.WindStrength;
            WindDirectionX = preset.WindDirectionX;
            WindDirectionZ = preset.WindDirectionZ;
            WindSpeed = preset.WindSpeed;
            WindGustiness = preset.WindGustiness;
            
            RainIntensity = preset.RainIntensity;
            SnowIntensity = preset.SnowIntensity;
            SnowAccumulation = preset.SnowAccumulation;
            Wetness = preset.Wetness;
            
            FogEnabled = preset.FogEnabled;
            FogDensity = preset.FogDensity;
            FogColor = preset.FogColor;
            FogStart = preset.FogStart;
            FogEnd = preset.FogEnd;
        }

        /// <summary>
        /// Start transitioning to a weather preset
        /// </summary>
        public void TransitionToPreset(WeatherPreset preset, float? customDuration = null)
        {
            TargetState = new WeatherTargetState
            {
                WindStrength = preset.WindStrength,
                WindDirectionX = preset.WindDirectionX,
                WindDirectionZ = preset.WindDirectionZ,
                WindSpeed = preset.WindSpeed,
                WindGustiness = preset.WindGustiness,
                
                RainIntensity = preset.RainIntensity,
                SnowIntensity = preset.SnowIntensity,
                SnowAccumulation = preset.SnowAccumulation,
                Wetness = preset.Wetness,
                
                FogEnabled = preset.FogEnabled,
                FogDensity = preset.FogDensity,
                FogColor = preset.FogColor,
                FogStart = preset.FogStart,
                FogEnd = preset.FogEnd,
                
                TransitionDuration = customDuration ?? (10.0f / TransitionSpeed),
                ElapsedTime = 0.0f
            };
        }
    }

    /// <summary>
    /// Target state for weather transitions
    /// </summary>
    public sealed class WeatherTargetState
    {
        public float WindStrength;
        public float WindDirectionX;
        public float WindDirectionZ;
        public float WindSpeed;
        public float WindGustiness;
        
        public float RainIntensity;
        public float SnowIntensity;
        public float SnowAccumulation;
        public float Wetness;
        
        public bool FogEnabled;
        public float FogDensity;
        public Vector3 FogColor;
        public float FogStart;
        public float FogEnd;
        
        public float TransitionDuration;
        public float ElapsedTime;
    }

    /// <summary>
    /// Weather preset definition
    /// </summary>
    public sealed class WeatherPreset
    {
        public string Name { get; set; } = "Custom";
        
        public float WindStrength { get; set; }
        public float WindDirectionX { get; set; }
        public float WindDirectionZ { get; set; }
        public float WindSpeed { get; set; }
        public float WindGustiness { get; set; }
        
        public float RainIntensity { get; set; }
        public float SnowIntensity { get; set; }
        public float SnowAccumulation { get; set; }
        public float Wetness { get; set; }
        
        public bool FogEnabled { get; set; }
        public float FogDensity { get; set; }
        public float FogStart { get; set; }
        public float FogEnd { get; set; }
        public Vector3 FogColor { get; set; }

        // === BUILT-IN PRESETS ===
        
        public static WeatherPreset Clear => new WeatherPreset
        {
            Name = "Clear",
            WindStrength = 0.1f,
            WindDirectionX = 1.0f,
            WindDirectionZ = 0.0f,
            WindSpeed = 0.5f,
            WindGustiness = 0.2f,
            RainIntensity = 0.0f,
            SnowIntensity = 0.0f,
            SnowAccumulation = 0.0f,
            Wetness = 0.0f,
            FogEnabled = false,
            FogDensity = 0.0f,
            FogStart = 10.0f,
            FogEnd = 300.0f,
            FogColor = new Vector3(0.7f, 0.8f, 0.9f)
        };

        public static WeatherPreset Windy => new WeatherPreset
        {
            Name = "Windy",
            WindStrength = 0.6f,
            WindDirectionX = 1.0f,
            WindDirectionZ = 0.3f,
            WindSpeed = 1.5f,
            WindGustiness = 0.7f,
            RainIntensity = 0.0f,
            SnowIntensity = 0.0f,
            SnowAccumulation = 0.0f,
            Wetness = 0.0f,
            FogEnabled = false,
            FogDensity = 0.0f,
            FogStart = 10.0f,
            FogEnd = 300.0f,
            FogColor = new Vector3(0.7f, 0.8f, 0.9f)
        };

        public static WeatherPreset LightRain => new WeatherPreset
        {
            Name = "Light Rain",
            WindStrength = 0.3f,
            WindDirectionX = 1.0f,
            WindDirectionZ = 0.0f,
            WindSpeed = 1.0f,
            WindGustiness = 0.4f,
            RainIntensity = 0.3f,
            SnowIntensity = 0.0f,
            SnowAccumulation = 0.0f,
            Wetness = 0.5f,
            FogEnabled = true,
            FogDensity = 0.02f,
            FogStart = 20.0f,
            FogEnd = 200.0f,
            FogColor = new Vector3(0.6f, 0.65f, 0.7f)
        };

        public static WeatherPreset HeavyRain => new WeatherPreset
        {
            Name = "Heavy Rain",
            WindStrength = 0.5f,
            WindDirectionX = 1.0f,
            WindDirectionZ = 0.2f,
            WindSpeed = 1.3f,
            WindGustiness = 0.6f,
            RainIntensity = 0.8f,
            SnowIntensity = 0.0f,
            SnowAccumulation = 0.0f,
            Wetness = 1.0f,
            FogEnabled = true,
            FogDensity = 0.05f,
            FogStart = 10.0f,
            FogEnd = 150.0f,
            FogColor = new Vector3(0.5f, 0.55f, 0.6f)
        };

        public static WeatherPreset Storm => new WeatherPreset
        {
            Name = "Storm",
            WindStrength = 0.9f,
            WindDirectionX = 1.0f,
            WindDirectionZ = 0.5f,
            WindSpeed = 2.0f,
            WindGustiness = 0.9f,
            RainIntensity = 1.0f,
            SnowIntensity = 0.0f,
            SnowAccumulation = 0.0f,
            Wetness = 1.0f,
            FogEnabled = true,
            FogDensity = 0.08f,
            FogStart = 5.0f,
            FogEnd = 100.0f,
            FogColor = new Vector3(0.4f, 0.45f, 0.5f)
        };

        public static WeatherPreset LightSnow => new WeatherPreset
        {
            Name = "Light Snow",
            WindStrength = 0.2f,
            WindDirectionX = 1.0f,
            WindDirectionZ = 0.1f,
            WindSpeed = 0.8f,
            WindGustiness = 0.3f,
            RainIntensity = 0.0f,
            SnowIntensity = 0.3f,
            SnowAccumulation = 0.4f,
            Wetness = 0.0f,
            FogEnabled = true,
            FogDensity = 0.03f,
            FogStart = 15.0f,
            FogEnd = 180.0f,
            FogColor = new Vector3(0.8f, 0.85f, 0.9f)
        };

        public static WeatherPreset HeavySnow => new WeatherPreset
        {
            Name = "Heavy Snow",
            WindStrength = 0.4f,
            WindDirectionX = 1.0f,
            WindDirectionZ = 0.2f,
            WindSpeed = 1.0f,
            WindGustiness = 0.5f,
            RainIntensity = 0.0f,
            SnowIntensity = 0.8f,
            SnowAccumulation = 0.9f,
            Wetness = 0.0f,
            FogEnabled = true,
            FogDensity = 0.06f,
            FogStart = 10.0f,
            FogEnd = 120.0f,
            FogColor = new Vector3(0.85f, 0.9f, 0.95f)
        };

        public static WeatherPreset Blizzard => new WeatherPreset
        {
            Name = "Blizzard",
            WindStrength = 0.8f,
            WindDirectionX = 1.0f,
            WindDirectionZ = 0.4f,
            WindSpeed = 1.8f,
            WindGustiness = 0.8f,
            RainIntensity = 0.0f,
            SnowIntensity = 1.0f,
            SnowAccumulation = 1.0f,
            Wetness = 0.0f,
            FogEnabled = true,
            FogDensity = 0.1f,
            FogStart = 5.0f,
            FogEnd = 80.0f,
            FogColor = new Vector3(0.9f, 0.92f, 0.95f)
        };

        public static WeatherPreset Foggy => new WeatherPreset
        {
            Name = "Foggy",
            WindStrength = 0.05f,
            WindDirectionX = 1.0f,
            WindDirectionZ = 0.0f,
            WindSpeed = 0.3f,
            WindGustiness = 0.1f,
            RainIntensity = 0.0f,
            SnowIntensity = 0.0f,
            SnowAccumulation = 0.0f,
            Wetness = 0.3f,
            FogEnabled = true,
            FogDensity = 0.15f,
            FogStart = 5.0f,
            FogEnd = 100.0f,
            FogColor = new Vector3(0.7f, 0.75f, 0.8f)
        };

        /// <summary>
        /// Get all built-in presets
        /// </summary>
        public static WeatherPreset[] GetAllPresets() => new[]
        {
            Clear, Windy, LightRain, HeavyRain, Storm, 
            LightSnow, HeavySnow, Blizzard, Foggy
        };
    }
}

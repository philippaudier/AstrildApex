using System;
using OpenTK.Mathematics;
using Engine.Rendering;

namespace Engine.Components
{
    /// <summary>
    /// Time management component - drives time of day, seasons, and temporal systems.
    /// This is the master controller for all time-based environmental effects.
    /// </summary>
    public class TimeComponent : Component
    {
        // === TIME OF DAY ===

        [Serialization.SerializableAttribute("timeOfDay")]
        public float TimeOfDay { get; set; } = 12.0f; // 0-24 hours (12.0 = noon)

        [Serialization.SerializableAttribute("timeScale")]
        public float TimeScale { get; set; } = 1.0f; // Speed multiplier (1.0 = real-time, 60 = 1h/min)

        [Serialization.SerializableAttribute("autoAdvance")]
        public bool AutoAdvance { get; set; } = false; // Automatically advance time

        [Serialization.SerializableAttribute("dayLengthMinutes")]
        public float DayLengthMinutes { get; set; } = 24.0f; // Real-world minutes for full 24h cycle

        // === DATE & SEASON ===

        [Serialization.SerializableAttribute("dayOfYear")]
        public int DayOfYear { get; set; } = 180; // 0-365 (180 = summer solstice)

        [Serialization.SerializableAttribute("year")]
        public int Year { get; set; } = 2025;

        [Serialization.SerializableAttribute("autoAdvanceDate")]
        public bool AutoAdvanceDate { get; set; } = false; // Advance day when 24h passes

        // === LOCATION (for celestial calculations) ===

        [Serialization.SerializableAttribute("latitude")]
        public float Latitude { get; set; } = 45.0f; // Degrees (-90 to +90, 0 = equator)

        [Serialization.SerializableAttribute("longitude")]
        public float Longitude { get; set; } = 0.0f; // Degrees (-180 to +180, 0 = prime meridian)

        // === LINKED SYSTEMS ===

        [Serialization.SerializableAttribute("environmentEntity")]
        public Scene.Entity? EnvironmentEntity { get; set; } = null; // Entity with EnvironmentSettings

        [Serialization.SerializableAttribute("weatherEntity")]
        public Scene.Entity? WeatherEntity { get; set; } = null; // Entity with WeatherComponent

        [Serialization.SerializableAttribute("globalEffectsEntity")]
        public Scene.Entity? GlobalEffectsEntity { get; set; } = null; // Entity with GlobalEffects

        // === RUNTIME STATE ===

        [NonSerialized]
        private float _accumulatedTime = 0.0f; // For day advancement

        /// <summary>
        /// Update time and linked systems. Called by engine update loop.
        /// </summary>
        public new void Update(float deltaTime)
        {
            if (!AutoAdvance) return;

            // Calculate hours per real second
            float hoursPerRealSecond = 24.0f / (DayLengthMinutes * 60.0f);
            float timeAdvance = deltaTime * hoursPerRealSecond * TimeScale;

            // Advance time
            TimeOfDay += timeAdvance;
            _accumulatedTime += timeAdvance;

            // Wrap time of day
            if (TimeOfDay >= 24.0f)
            {
                TimeOfDay -= 24.0f;

                // Advance day if enabled
                if (AutoAdvanceDate)
                {
                    DayOfYear++;
                    if (DayOfYear >= 365)
                    {
                        DayOfYear = 0;
                        Year++;
                    }
                }
            }

            // Update dependent systems
            UpdateEnvironmentSettings();
            UpdateWeatherComponent();
            UpdateGlobalEffects();
        }

        /// <summary>
        /// Set time of day directly (for manual control)
        /// </summary>
        public void SetTimeOfDay(float hours)
        {
            TimeOfDay = Math.Clamp(hours, 0.0f, 24.0f);
            UpdateEnvironmentSettings();
            UpdateWeatherComponent();
            UpdateGlobalEffects();
        }

        /// <summary>
        /// Get normalized time of day (0.0 = midnight, 0.5 = noon, 1.0 = midnight)
        /// </summary>
        public float GetNormalizedTimeOfDay()
        {
            return TimeOfDay / 24.0f;
        }

        /// <summary>
        /// Get current season based on day of year
        /// </summary>
        public Season GetSeason()
        {
            // Northern hemisphere seasons (adjust for southern hemisphere if needed)
            if (DayOfYear >= 355 || DayOfYear < 80) return Season.Winter;      // Dec 21 - Mar 20
            if (DayOfYear >= 80 && DayOfYear < 172) return Season.Spring;      // Mar 21 - Jun 20
            if (DayOfYear >= 172 && DayOfYear < 266) return Season.Summer;     // Jun 21 - Sep 22
            return Season.Autumn;                                               // Sep 23 - Dec 20
        }

        /// <summary>
        /// Get season blend factor (0-1 for smooth transitions)
        /// </summary>
        public float GetSeasonBlend()
        {
            Season current = GetSeason();
            int dayInSeason = DayOfYear % 91; // Approximate days per season
            return dayInSeason / 91.0f;
        }

        /// <summary>
        /// Get sun/moon blend factor (0 = night/moon, 1 = day/sun)
        /// </summary>
        public float GetDayNightBlend()
        {
            // Sunrise: 6am, Sunset: 6pm (simplified, can be adjusted by season later)
            const float sunrise = 6.0f;
            const float sunset = 18.0f;

            if (TimeOfDay >= sunrise && TimeOfDay <= sunset)
            {
                // Day time - fade in at sunrise, fade out at sunset
                float dayProgress = (TimeOfDay - sunrise) / (sunset - sunrise);

                // Use smoothstep for smooth transitions
                if (dayProgress < 0.1f) // Morning fade-in (6am-7:12am)
                    return SmoothStep(0.0f, 1.0f, dayProgress / 0.1f);
                if (dayProgress > 0.9f) // Evening fade-out (5:48pm-6pm)
                    return SmoothStep(1.0f, 0.0f, (dayProgress - 0.9f) / 0.1f);

                return 1.0f; // Full day
            }

            return 0.0f; // Night
        }

        /// <summary>
        /// Get sunrise/sunset blend factor for golden hour effects (0 = no golden hour, 1 = peak golden hour)
        /// </summary>
        public float GetGoldenHourBlend()
        {
            // Golden hour: 5-7am (sunrise), 5-7pm (sunset)
            const float morningStart = 5.0f;
            const float morningEnd = 7.0f;
            const float eveningStart = 17.0f;
            const float eveningEnd = 19.0f;

            // Morning golden hour
            if (TimeOfDay >= morningStart && TimeOfDay < morningEnd)
            {
                float t = (TimeOfDay - morningStart) / (morningEnd - morningStart);
                return (float)Math.Sin(t * Math.PI); // Peak at sunrise (6am)
            }

            // Evening golden hour
            if (TimeOfDay >= eveningStart && TimeOfDay < eveningEnd)
            {
                float t = (TimeOfDay - eveningStart) / (eveningEnd - eveningStart);
                return (float)Math.Sin(t * Math.PI); // Peak at sunset (6pm)
            }

            return 0.0f;
        }

        /// <summary>
        /// Check if it's currently sunrise time
        /// </summary>
        public bool IsSunrise()
        {
            return TimeOfDay >= 5.0f && TimeOfDay < 7.0f;
        }

        /// <summary>
        /// Check if it's currently sunset time
        /// </summary>
        public bool IsSunset()
        {
            return TimeOfDay >= 17.0f && TimeOfDay < 19.0f;
        }

        // === PRIVATE HELPERS ===

        private float SmoothStep(float edge0, float edge1, float x)
        {
            x = Math.Clamp((x - edge0) / (edge1 - edge0), 0.0f, 1.0f);
            return x * x * (3.0f - 2.0f * x);
        }

        /// <summary>
        /// Update EnvironmentSettings component with current time
        /// </summary>
        private void UpdateEnvironmentSettings()
        {
            // Auto-detect EnvironmentSettings entity if not set
            if (EnvironmentEntity == null && Entity?.Scene != null)
            {
                try
                {
                    foreach (var e in Entity.Scene.Entities)
                    {
                        if (e.HasComponent<EnvironmentSettings>())
                        {
                            EnvironmentEntity = e;
                            break;
                        }
                    }
                }
                catch { }
            }

            if (EnvironmentEntity == null) return;

            var envSettings = EnvironmentEntity.GetComponent<EnvironmentSettings>();
            if (envSettings != null)
            {
                envSettings.TimeOfDay = TimeOfDay;
                envSettings.UpdateCelestialBodies(TimeOfDay);
            }
        }

        /// <summary>
        /// Update WeatherComponent with current time (for future weather-time integration)
        /// </summary>
        private void UpdateWeatherComponent()
        {
            if (WeatherEntity == null) return;

            var weather = WeatherEntity.GetComponent<WeatherComponent>();
            if (weather != null)
            {
                // Future: Could drive automatic weather transitions based on time
                // e.g., morning fog, afternoon clear, evening mist
            }
        }

        /// <summary>
        /// Update GlobalEffects with current time (for color grading, tonemapping, etc.)
        /// </summary>
        private void UpdateGlobalEffects()
        {
            // Auto-detect GlobalEffects entity if not set
            if (GlobalEffectsEntity == null && Entity?.Scene != null)
            {
                try
                {
                    foreach (var e in Entity.Scene.Entities)
                    {
                        if (e.HasComponent<GlobalEffects>())
                        {
                            GlobalEffectsEntity = e;
                            break;
                        }
                    }
                }
                catch { }
            }

            if (GlobalEffectsEntity == null) return;

            var globalEffects = GlobalEffectsEntity.GetComponent<GlobalEffects>();
            if (globalEffects != null)
            {
                // Color grading will be updated based on time of day
                var colorGrading = globalEffects.GetEffect<ColorGradingEffect>();
                if (colorGrading != null && colorGrading.Enabled)
                {
                    colorGrading.UpdateForTimeOfDay(TimeOfDay);
                }

                // Tonemapping exposure will be updated based on time of day
                var tonemapping = globalEffects.GetEffect<ToneMappingEffect>();
                if (tonemapping != null && tonemapping.Enabled)
                {
                    tonemapping.UpdateForTimeOfDay(TimeOfDay);
                }
            }
        }
    }

    /// <summary>
    /// Season enumeration
    /// </summary>
    public enum Season
    {
        Spring,
        Summer,
        Autumn,
        Winter
    }
}

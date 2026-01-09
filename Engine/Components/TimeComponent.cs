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

        // === PROCEDURAL SKY OVERRIDES (per-phase configurable via Inspector) ===

        [Serialization.SerializableAttribute("daySkyTint")]
        public System.Numerics.Vector3 DaySkyTint { get; set; } = new System.Numerics.Vector3(0.53f, 0.66f, 0.97f); // Bright sky blue

        [Serialization.SerializableAttribute("nightSkyTint")]
        public System.Numerics.Vector3 NightSkyTint { get; set; } = new System.Numerics.Vector3(0.12f, 0.15f, 0.25f); // Deep blue night sky

        [Serialization.SerializableAttribute("dawnSkyTint")]
        public System.Numerics.Vector3 DawnSkyTint { get; set; } = new System.Numerics.Vector3(1.0f, 0.7f, 0.5f); // Orange-pink sunrise

        [Serialization.SerializableAttribute("duskSkyTint")]
        public System.Numerics.Vector3 DuskSkyTint { get; set; } = new System.Numerics.Vector3(1.0f, 0.6f, 0.4f); // Deep orange sunset

        [Serialization.SerializableAttribute("dayGroundColor")]
        public System.Numerics.Vector3 DayGroundColor { get; set; } = new System.Numerics.Vector3(0.40f, 0.35f, 0.30f); // Brown earth

        [Serialization.SerializableAttribute("nightGroundColor")]
        public System.Numerics.Vector3 NightGroundColor { get; set; } = new System.Numerics.Vector3(0.08f, 0.09f, 0.12f); // Dark blue-grey ground

        [Serialization.SerializableAttribute("dayAtmosphereThickness")]
        public float DayAtmosphereThickness { get; set; } = 1.0f;

        [Serialization.SerializableAttribute("nightAtmosphereThickness")]
        public float NightAtmosphereThickness { get; set; } = 0.6f;

        [Serialization.SerializableAttribute("dawnDuskAtmosphereThickness")]
        public float DawnDuskAtmosphereThickness { get; set; } = 1.5f;

        // Sun/Moon visual parameters (time-driven overrides)
        [Serialization.SerializableAttribute("daySunSize")]
        public float DaySunSize { get; set; } = 0.04f;

        [Serialization.SerializableAttribute("nightMoonSize")]
        public float NightMoonSize { get; set; } = 0.02f;

        [Serialization.SerializableAttribute("dawnDuskSunSize")]
        public float DawnDuskSunSize { get; set; } = 0.06f;

        [Serialization.SerializableAttribute("daySunConvergence")]
        public float DaySunConvergence { get; set; } = 5.0f;

        [Serialization.SerializableAttribute("nightMoonConvergence")]
        public float NightMoonConvergence { get; set; } = 3.0f;

        [Serialization.SerializableAttribute("dawnDuskSunConvergence")]
        public float DawnDuskSunConvergence { get; set; } = 8.0f;

        /// <summary>
        /// Auto-detect and update linked entities. Call this from Inspector in Edit mode.
        /// In Play mode, Update() handles this automatically.
        /// </summary>
        public void UpdateLinkedEntitiesInEditMode(Scene.Scene? scene = null)
        {
            // Try to get scene from parameter, then from Entity, then give up
            var targetScene = scene ?? Entity?.Scene;

            if (targetScene == null)
            {
                Console.WriteLine("[TimeComponent] UpdateLinkedEntitiesInEditMode: Scene is null! (Entity.Scene not set)");
                return;
            }

            Console.WriteLine($"[TimeComponent] Searching for EnvironmentSettings in {targetScene.Entities.Count} entities...");

            // Auto-detect EnvironmentSettings
            if (EnvironmentEntity == null)
            {
                try
                {
                    foreach (var e in targetScene.Entities)
                    {
                        if (e.HasComponent<EnvironmentSettings>())
                        {
                            EnvironmentEntity = e;
                            Console.WriteLine($"[TimeComponent] FOUND EnvironmentSettings on entity '{e.Name}'!");
                            break;
                        }
                    }
                    if (EnvironmentEntity == null)
                    {
                        Console.WriteLine("[TimeComponent] No EnvironmentSettings found in scene!");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TimeComponent] Exception during EnvironmentSettings search: {ex.Message}");
                }
            }

            // Auto-detect GlobalEffects
            if (GlobalEffectsEntity == null)
            {
                try
                {
                    foreach (var e in targetScene.Entities)
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

            // Apply TimeOfDay to EnvironmentSettings
            if (EnvironmentEntity != null)
            {
                try
                {
                    var envSettings = EnvironmentEntity.GetComponent<EnvironmentSettings>();
                    if (envSettings != null)
                    {
                        envSettings.TimeOfDay = TimeOfDay;
                        envSettings.DayOfYear = DayOfYear;
                        envSettings.Latitude = Latitude;
                        envSettings.UpdateCelestialBodies(TimeOfDay, DayOfYear, Latitude);
                        // Apply smooth procedural sky overrides from TimeComponent based on current blends
                        try
                        {
                            float dayNight = GetDayNightBlend();
                            float golden = GetGoldenHourBlend();
                            bool isMorning = TimeOfDay < 12.0f;

                            // Base lerp between night and day
                            var baseSky = LerpVec3(NightSkyTint, DaySkyTint, dayNight);
                            var baseGround = LerpVec3(NightGroundColor, DayGroundColor, dayNight);
                            float baseAt = MathHelper.Lerp(NightAtmosphereThickness, DayAtmosphereThickness, dayNight);

                            // Golden hour target (dawn or dusk)
                            var goldenTargetSky = isMorning ? DawnSkyTint : DuskSkyTint;
                            float goldenAtTarget = DawnDuskAtmosphereThickness;

                            // Final values blend towards golden hour target when golden > 0
                            var finalSky = LerpVec3(baseSky, goldenTargetSky, golden);
                            var finalGround = LerpVec3(baseGround, goldenTargetSky, golden); // slight tint towards golden sky for ground
                            float finalAt = MathHelper.Lerp(baseAt, goldenAtTarget, golden);

                            // Sun size and convergence: blend between night (moon) and day (sun), then apply golden
                            float baseSunSize = MathHelper.Lerp(NightMoonSize, DaySunSize, dayNight);
                            float baseConvergence = MathHelper.Lerp(NightMoonConvergence, DaySunConvergence, dayNight);
                            float finalSunSize = MathHelper.Lerp(baseSunSize, DawnDuskSunSize, golden);
                            float finalConvergence = MathHelper.Lerp(baseConvergence, DawnDuskSunConvergence, golden);

                            var overrides = new Engine.Components.ProceduralSkyboxParameters();
                            overrides.SkyTint = new OpenTK.Mathematics.Vector3(finalSky.X, finalSky.Y, finalSky.Z);
                            overrides.GroundColor = new OpenTK.Mathematics.Vector3(finalGround.X, finalGround.Y, finalGround.Z);
                            overrides.AtmosphereThickness = finalAt;
                            overrides.Exposure = 0.0f;
                            overrides.SunSize = finalSunSize;
                            overrides.SunSizeConvergence = finalConvergence;

                            envSettings.ProceduralOverrides = overrides;
                        }
                        catch { }
                        Console.WriteLine($"[TimeComponent] Applied TimeOfDay={TimeOfDay:F2}, DayOfYear={DayOfYear}, Latitude={Latitude:F1} to EnvironmentSettings!");
                    }
                    else
                    {
                        Console.WriteLine("[TimeComponent] EnvironmentEntity found but has no EnvironmentSettings component!");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TimeComponent] Exception applying TimeOfDay: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("[TimeComponent] EnvironmentEntity is null, cannot apply TimeOfDay!");
            }
        }

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

        // Optional hook invoked by the serializer after references are resolved.
        // Ensures sensible defaults for color fields in case old/invalid serialized data
        // left them as (0,0,0).
        private void OnAfterDeserialize()
        {
            try
            {
                if (DaySkyTint.X == 0f && DaySkyTint.Y == 0f && DaySkyTint.Z == 0f)
                    DaySkyTint = new System.Numerics.Vector3(0.53f, 0.66f, 0.97f);
                if (NightSkyTint.X == 0f && NightSkyTint.Y == 0f && NightSkyTint.Z == 0f)
                    NightSkyTint = new System.Numerics.Vector3(0.12f, 0.15f, 0.25f);
                if (DawnSkyTint.X == 0f && DawnSkyTint.Y == 0f && DawnSkyTint.Z == 0f)
                    DawnSkyTint = new System.Numerics.Vector3(1.0f, 0.7f, 0.5f);
                if (DuskSkyTint.X == 0f && DuskSkyTint.Y == 0f && DuskSkyTint.Z == 0f)
                    DuskSkyTint = new System.Numerics.Vector3(1.0f, 0.6f, 0.4f);
                if (DayGroundColor.X == 0f && DayGroundColor.Y == 0f && DayGroundColor.Z == 0f)
                    DayGroundColor = new System.Numerics.Vector3(0.40f, 0.35f, 0.30f);
                if (NightGroundColor.X == 0f && NightGroundColor.Y == 0f && NightGroundColor.Z == 0f)
                    NightGroundColor = new System.Numerics.Vector3(0.08f, 0.09f, 0.12f);
            }
            catch { }
        }

        // Helper to lerp between two System.Numerics.Vector3 values
        private System.Numerics.Vector3 LerpVec3(System.Numerics.Vector3 a, System.Numerics.Vector3 b, float t)
        {
            t = Math.Clamp(t, 0.0f, 1.0f);
            return new System.Numerics.Vector3(
                a.X + (b.X - a.X) * t,
                a.Y + (b.Y - a.Y) * t,
                a.Z + (b.Z - a.Z) * t
            );
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
                envSettings.DayOfYear = DayOfYear;
                envSettings.Latitude = Latitude;
                envSettings.UpdateCelestialBodies(TimeOfDay, DayOfYear, Latitude);
                // Runtime: apply smooth procedural overrides so skybox renderer can read them each frame
                try
                {
                    float dayNight = GetDayNightBlend();
                    float golden = GetGoldenHourBlend();
                    bool isMorning = TimeOfDay < 12.0f;

                    var baseSky = LerpVec3(NightSkyTint, DaySkyTint, dayNight);
                    var baseGround = LerpVec3(NightGroundColor, DayGroundColor, dayNight);
                    float baseAt = MathHelper.Lerp(NightAtmosphereThickness, DayAtmosphereThickness, dayNight);

                    var goldenTargetSky = isMorning ? DawnSkyTint : DuskSkyTint;
                    var finalSky = LerpVec3(baseSky, goldenTargetSky, golden);
                    var finalGround = LerpVec3(baseGround, goldenTargetSky, golden);
                    float finalAt = MathHelper.Lerp(baseAt, DawnDuskAtmosphereThickness, golden);

                    float baseSunSize = MathHelper.Lerp(NightMoonSize, DaySunSize, dayNight);
                    float baseConvergence = MathHelper.Lerp(NightMoonConvergence, DaySunConvergence, dayNight);
                    float finalSunSize = MathHelper.Lerp(baseSunSize, DawnDuskSunSize, golden);
                    float finalConvergence = MathHelper.Lerp(baseConvergence, DawnDuskSunConvergence, golden);

                    var overrides = new Engine.Components.ProceduralSkyboxParameters();
                    overrides.SkyTint = new OpenTK.Mathematics.Vector3(finalSky.X, finalSky.Y, finalSky.Z);
                    overrides.GroundColor = new OpenTK.Mathematics.Vector3(finalGround.X, finalGround.Y, finalGround.Z);
                    overrides.AtmosphereThickness = finalAt;
                    overrides.Exposure = 0.0f;
                    overrides.SunSize = finalSunSize;
                    overrides.SunSizeConvergence = finalConvergence;
                    envSettings.ProceduralOverrides = overrides;
                }
                catch { }
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

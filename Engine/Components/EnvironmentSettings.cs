using System;
using OpenTK.Mathematics;

namespace Engine.Components
{
    /// <summary>
    /// Environment Settings component for managing global lighting, skybox, and celestial bodies.
    /// Handles sun/moon positioning and blending using a single directional light for optimal performance.
    /// </summary>
    public class EnvironmentSettings : Component
    {
        // === SKYBOX SETTINGS ===

        [Engine.Serialization.Serializable("skyboxMaterialPath")]
        public string SkyboxMaterialPath { get; set; } = ""; // Path to the skybox material asset

        [Engine.Serialization.Serializable("autoUpdateSkybox")]
        public bool AutoUpdateSkybox { get; set; } = true; // Automatically update skybox based on time of day

        // Legacy skybox settings (for backward compatibility)
        [Engine.Serialization.Serializable("skyboxTint")]
        public Vector3 SkyboxTint { get; set; } = Vector3.One;

        [Engine.Serialization.Serializable("skyboxExposure")]
        public float SkyboxExposure { get; set; } = 1.0f;

        // === CELESTIAL BODIES (SUN/MOON SYSTEM) ===

        /// <summary>
        /// Main directional light - represents both sun (day) and moon (night) for optimal performance.
        /// Light color and intensity blend smoothly based on time of day.
        /// </summary>
        [Engine.Serialization.Serializable("mainDirectionalLight")]
        public Engine.Scene.Entity? MainDirectionalLight { get; set; } = null;

        // Sun parameters
        [Engine.Serialization.Serializable("sunColor")]
        public Vector3 SunColor { get; set; } = new Vector3(1.0f, 0.95f, 0.88f); // Warm daylight

        [Engine.Serialization.Serializable("sunIntensity")]
        public float SunIntensity { get; set; } = 1.5f;

        [Engine.Serialization.Serializable("sunTemperature")]
        public float SunTemperature { get; set; } = 5500f; // Kelvin (5500K = daylight)

        [Engine.Serialization.Serializable("sunSize")]
        public float SunSize { get; set; } = 1.0f; // Visual size multiplier (for future volumetric effects)

        [Engine.Serialization.Serializable("sunCastShadows")]
        public bool SunCastShadows { get; set; } = true;

        // Moon parameters
        [Engine.Serialization.Serializable("moonColor")]
        public Vector3 MoonColor { get; set; } = new Vector3(0.6f, 0.7f, 0.85f); // Cool moonlight

        [Engine.Serialization.Serializable("moonIntensity")]
        public float MoonIntensity { get; set; } = 0.25f;

        [Engine.Serialization.Serializable("moonTemperature")]
        public float MoonTemperature { get; set; } = 4000f; // Kelvin (cooler than sun)

        [Engine.Serialization.Serializable("moonSize")]
        public float MoonSize { get; set; } = 1.0f; // Visual size multiplier

        [Engine.Serialization.Serializable("moonCastShadows")]
        public bool MoonCastShadows { get; set; } = false; // Moon usually doesn't cast strong shadows

        // Sunrise/Sunset colors (golden hour)
        [Engine.Serialization.Serializable("sunriseColor")]
        public Vector3 SunriseColor { get; set; } = new Vector3(1.0f, 0.7f, 0.4f); // Orange-pink

        [Engine.Serialization.Serializable("sunsetColor")]
        public Vector3 SunsetColor { get; set; } = new Vector3(1.0f, 0.5f, 0.3f); // Deep orange

        [Engine.Serialization.Serializable("goldenHourIntensity")]
        public float GoldenHourIntensity { get; set; } = 0.6f; // How much golden hour affects color (0-1)

        // Transition parameters
        [Engine.Serialization.Serializable("transitionDuration")]
        public float TransitionDuration { get; set; } = 1.0f; // Hours for sun/moon transition (sunrise/sunset)

        [Engine.Serialization.Serializable("transitionCurve")]
        public float TransitionCurve { get; set; } = 2.0f; // Smoothness curve (1 = linear, 2 = smooth, 3 = very smooth)

        // Orbital parameters (simplified celestial mechanics)
        [Engine.Serialization.Serializable("sunOrbitTilt")]
        public float SunOrbitTilt { get; set; } = 23.5f; // Earth's axial tilt in degrees

        [Engine.Serialization.Serializable("sunriseDirection")]
        public Vector3 SunriseDirection { get; set; } = new Vector3(1, 0, 0); // East (positive X)

        // === LEGACY SUPPORT (for backward compatibility with old scenes) ===

        [Engine.Serialization.Serializable("sunLight")]
        private Engine.Scene.Entity? SunLight_Legacy
        {
            get => MainDirectionalLight;
            set => MainDirectionalLight = value; // Redirect to new system
        }

        [Engine.Serialization.Serializable("moonLight")]
        private Engine.Scene.Entity? MoonLight_Legacy
        {
            get => null; // Moon is now merged with sun
            set { } // Ignore old moon light assignments
        }

        public uint? SunLightEntityId => MainDirectionalLight?.Id;
        public uint? MoonLightEntityId => null; // No longer used

        // === AMBIENT LIGHTING ===

        [Engine.Serialization.Serializable("ambientMode")]
        public AmbientMode AmbientMode { get; set; } = AmbientMode.Skybox;

        [Engine.Serialization.Serializable("ambientColor")]
        public Vector3 AmbientColor { get; set; } = new Vector3(0.2f, 0.2f, 0.2f);

        [Engine.Serialization.Serializable("ambientIntensity")]
        public float AmbientIntensity { get; set; } = 1.0f;

        // === FOG SETTINGS (DEPRECATED - use WeatherComponent instead) ===
        // Kept for backward compatibility but should not be used in new scenes

        [Engine.Serialization.Serializable("fogEnabled")]
        public bool FogEnabled { get; set; } = false;

        [Engine.Serialization.Serializable("fogColor")]
        public Vector3 FogColor { get; set; } = Vector3.One;

        [Engine.Serialization.Serializable("fogDensity")]
        public float FogDensity { get; set; } = 0.1f;

        [Engine.Serialization.Serializable("fogStart")]
        public float FogStart { get; set; } = 0.0f;

        [Engine.Serialization.Serializable("fogEnd")]
        public float FogEnd { get; set; } = 300.0f;

        // === TIME OF DAY (managed by TimeComponent, stored here for runtime) ===

        [Engine.Serialization.Serializable("timeOfDay")]
        public float TimeOfDay { get; set; } = 12.0f; // 0-24 hours (12 = noon)

        [Engine.Serialization.Serializable("dayOfYear")]
        public int DayOfYear { get; set; } = 180; // 0-365, used for seasonal trajectory

        [Engine.Serialization.Serializable("latitude")]
        public float Latitude { get; set; } = 45.0f; // Degrees (-90 to +90), affects day length

        // === PUBLIC API ===

        /// <summary>
        /// Update celestial bodies (sun/moon) based on current time of day and season.
        /// Called by TimeComponent or manually when time changes.
        /// </summary>
        public void UpdateCelestialBodies(float timeOfDay, int dayOfYear = -1, float latitude = -999.0f)
        {
            TimeOfDay = timeOfDay;
            if (dayOfYear >= 0)
            {
                DayOfYear = dayOfYear;
            }
            if (latitude > -999.0f)
            {
                Latitude = latitude;
            }

            if (MainDirectionalLight == null) return;

            var light = MainDirectionalLight.GetComponent<LightComponent>();
            if (light == null) return;

            // Calculate solar declination (seasonal tilt) - approximation of Earth's axial tilt effect
            // Day 0 = Jan 1, Day 172 = Jun 21 (summer solstice), Day 355 = Dec 21 (winter solstice)
            // Declination varies from -23.5° (winter) to +23.5° (summer)
            float dayAngle = (2.0f * MathHelper.Pi * (DayOfYear - 81)) / 365.0f; // Offset so day 81 (spring equinox) = 0°
            float solarDeclination = 23.5f * (float)Math.Sin(dayAngle); // Degrees

            // Calculate celestial body position (sun during day, moon during night)
            // Both follow an arc from East → Zenith → West (never going below terrain)
            
            float sunrise = GetSunriseHour();
            float sunset = GetSunsetHour();
            bool isDay = timeOfDay >= sunrise && timeOfDay <= sunset;
            
            // Calculate maximum elevation angle based on solar declination
            // At solar noon, the sun's elevation = 90° - latitude + declination
            // We clamp this to ensure the sun never goes below horizon
            float maxElevation = 90.0f - Math.Abs(Latitude) + solarDeclination;
            maxElevation = MathHelper.Clamp(maxElevation, 10.0f, 90.0f); // At least 10° above horizon
            
            Vector3 sunPosition;
            
            if (isDay)
            {
                // DAYTIME: Sun arc from East (sunrise) → Max Elevation (noon) → West (sunset)
                float dayDuration = sunset - sunrise;
                
                // Safety check: if day duration is too short, use a default arc
                if (dayDuration < 0.5f)
                {
                    dayDuration = 12.0f; // Fallback to 12-hour day
                    sunrise = 6.0f;
                    sunset = 18.0f;
                }
                
                float dayProgress = MathHelper.Clamp((timeOfDay - sunrise) / dayDuration, 0.0f, 1.0f); // 0 = sunrise, 0.5 = noon, 1 = sunset
                
                // Arc: 0° (east/horizon) → maxElevation (noon) → 0° (west/horizon)
                // Use sine curve for smooth arc, scaled by maxElevation
                float arcAngle = dayProgress * 180.0f; // 0° to 180°
                float arcAngleRad = MathHelper.DegreesToRadians(arcAngle);
                
                float elevationFactor = (float)Math.Sin(arcAngleRad); // 0 at horizon, 1 at peak
                float maxElevationRad = MathHelper.DegreesToRadians(maxElevation);
                
                sunPosition = new Vector3(
                    -(float)Math.Cos(arcAngleRad),  // X: -1 at east (sunrise), +1 at west (sunset)
                    elevationFactor * (float)Math.Sin(maxElevationRad), // Y: scaled by season, always >= 0
                    0  // Keep Z simple for now
                );
            }
            else
            {
                // NIGHTTIME: Moon arc from East (after sunset) → Zenith (midnight) → West (before sunrise)
                // Moon always stays above horizon with moderate elevation
                float nightDuration = (24.0f - sunset) + sunrise; // Total night hours
                
                // Safety check
                if (nightDuration < 0.5f || nightDuration > 20.0f)
                {
                    nightDuration = 12.0f; // Fallback
                    sunrise = 6.0f;
                    sunset = 18.0f;
                }
                
                float nightProgress;
                
                if (timeOfDay >= sunset)
                {
                    // Evening: sunset → midnight
                    nightProgress = (timeOfDay - sunset) / nightDuration;
                }
                else
                {
                    // Morning: midnight → sunrise
                    nightProgress = ((24.0f - sunset) + timeOfDay) / nightDuration;
                }
                
                nightProgress = MathHelper.Clamp(nightProgress, 0.0f, 1.0f);
                
                // Moon arc with moderate elevation (opposite season to sun)
                float moonMaxElevation = 60.0f - solarDeclination; // Moon higher in winter, lower in summer (opposite to sun)
                moonMaxElevation = MathHelper.Clamp(moonMaxElevation, 15.0f, 75.0f);
                
                float arcAngle = nightProgress * 180.0f; // 0° to 180°
                float arcAngleRad = MathHelper.DegreesToRadians(arcAngle);
                
                float elevationFactor = (float)Math.Sin(arcAngleRad); // Always positive (0 to 1 to 0)
                float moonMaxElevationRad = MathHelper.DegreesToRadians(moonMaxElevation);
                
                sunPosition = new Vector3(
                    -(float)Math.Cos(arcAngleRad),  // X: -1 at east, +1 at west
                    elevationFactor * (float)Math.Sin(moonMaxElevationRad), // Y: always >= 0
                    0  // Keep Z simple for now
                );
            }

            // Light direction points FROM sun position TOWARD scene center
            Vector3 lightDirection = -sunPosition;

            // Calculate Euler angles from sun position
            // For a light pointing downward from the sun's position:
            // - pitch: vertical angle (from sunPosition.Y)
            // - yaw: horizontal rotation from East to West (from sunPosition.X)
            
            // Pitch: angle from horizontal plane (positive = pointing down)
            float pitch = (float)Math.Asin(MathHelper.Clamp(sunPosition.Y, -1.0f, 1.0f));
            
            // Yaw: rotation in horizontal plane (East = -90°, South = 0°, West = +90°)
            // Since X goes from -1 (East) to +1 (West), we map this to yaw
            float yaw = (float)Math.Asin(MathHelper.Clamp(sunPosition.X, -1.0f, 1.0f));
            
            // Create rotation from Euler angles (Y-up, Z-forward system)
            // Apply rotations in order: Y (yaw) then X (pitch)
            Quaternion rotation = Quaternion.FromEulerAngles(pitch, yaw, 0);

            if (MainDirectionalLight.Transform != null)
            {
                MainDirectionalLight.Transform.Rotation = rotation;
            }

            // Blend sun/moon color and intensity based on time with improved transition
            float dayNightBlend = GetDayNightBlend(timeOfDay);
            Vector3 baseColor = Vector3.Lerp(MoonColor, SunColor, dayNightBlend);
            float baseIntensity = MathHelper.Lerp(MoonIntensity, SunIntensity, dayNightBlend);

            // Apply golden hour coloring (sunrise/sunset)
            float goldenHourBlend = GetGoldenHourBlend(timeOfDay);
            if (goldenHourBlend > 0.0f)
            {
                Vector3 goldenColor = timeOfDay < 12.0f ? SunriseColor : SunsetColor;
                baseColor = Vector3.Lerp(baseColor, goldenColor, goldenHourBlend * GoldenHourIntensity);
            }

            // Apply to light
            light.Color = baseColor;
            light.Intensity = baseIntensity;
            
            // Apply shadow casting (blend between sun and moon settings)
            light.CastShadows = dayNightBlend > 0.5f ? SunCastShadows : MoonCastShadows;

            // Update procedural skybox if enabled
            if (AutoUpdateSkybox)
            {
                var skyboxParams = CalculateProceduralSkyboxParameters(timeOfDay, dayNightBlend);
                // Store the parameters for the rendering system to apply
                SkyboxTint = skyboxParams.SkyTint;
                SkyboxExposure = skyboxParams.Exposure;
                // TODO: Other skybox parameters can be accessed via GetProceduralSkyboxParameters()
            }
        }

        /// <summary>
        /// Calculate sunrise hour based on latitude and solar declination (seasonal variation)
        /// </summary>
        public float GetSunriseHour(float latitude = -1.0f)
        {
            if (latitude < -90.0f) latitude = this.Latitude; // Use instance latitude if not specified
            float declination = GetSolarDeclination();
            float sunriseHour = CalculateSunriseHour(latitude, declination);
            return MathHelper.Clamp(sunriseHour, 4.0f, 8.0f); // Clamp to realistic range
        }

        /// <summary>
        /// Calculate sunset hour based on latitude and solar declination (seasonal variation)
        /// </summary>
        public float GetSunsetHour(float latitude = -1.0f)
        {
            if (latitude < -90.0f) latitude = this.Latitude; // Use instance latitude if not specified
            float declination = GetSolarDeclination();
            float sunsetHour = CalculateSunsetHour(latitude, declination);
            return MathHelper.Clamp(sunsetHour, 16.0f, 20.0f); // Clamp to realistic range
        }

        /// <summary>
        /// Get day/night blend factor (0 = night/moon, 1 = day/sun).
        /// Uses smooth transitions at sunrise and sunset with seasonal variation.
        /// </summary>
        public float GetDayNightBlend(float timeOfDay)
        {
            // Calculate sunrise and sunset times based on season and latitude
            float sunrise = GetSunriseHour();
            float sunset = GetSunsetHour();
            float transitionDuration = TransitionDuration;

            if (timeOfDay >= sunrise + transitionDuration && timeOfDay <= sunset - transitionDuration)
            {
                return 1.0f; // Full day
            }
            else if (timeOfDay >= sunrise && timeOfDay < sunrise + transitionDuration)
            {
                // Sunrise transition with custom curve
                float t = (timeOfDay - sunrise) / transitionDuration;
                return ApplyTransitionCurve(t);
            }
            else if (timeOfDay > sunset - transitionDuration && timeOfDay <= sunset)
            {
                // Sunset transition with custom curve
                float t = (timeOfDay - (sunset - transitionDuration)) / transitionDuration;
                return ApplyTransitionCurve(1.0f - t);
            }

            return 0.0f; // Night
        }

        /// <summary>
        /// Get golden hour blend factor (0 = no golden hour, 1 = peak golden hour).
        /// Golden hour occurs around sunrise and sunset (±1 hour window).
        /// </summary>
        public float GetGoldenHourBlend(float timeOfDay)
        {
            float sunrise = GetSunriseHour();
            float sunset = GetSunsetHour();
            
            float morningStart = sunrise - 1.0f;
            float morningEnd = sunrise + 1.0f;
            float eveningStart = sunset - 1.0f;
            float eveningEnd = sunset + 1.0f;

            // Morning golden hour
            if (timeOfDay >= morningStart && timeOfDay < morningEnd)
            {
                float t = (timeOfDay - morningStart) / (morningEnd - morningStart);
                return (float)Math.Sin(t * Math.PI); // Peak at 6am
            }

            // Evening golden hour
            if (timeOfDay >= eveningStart && timeOfDay < eveningEnd)
            {
                float t = (timeOfDay - eveningStart) / (eveningEnd - eveningStart);
                return (float)Math.Sin(t * Math.PI); // Peak at 6pm
            }

            return 0.0f;
        }

        /// <summary>
        /// Legacy method - get active light ID (now always returns main directional light)
        /// </summary>
        public uint? GetActiveLightId()
        {
            return MainDirectionalLight?.Id;
        }

        /// <summary>
        /// Legacy method - kept for backward compatibility
        /// </summary>
        public float GetSunMoonBlend()
        {
            return GetDayNightBlend(TimeOfDay);
        }

        /// <summary>
        /// Get current season based on day of year
        /// </summary>
        public string GetSeasonName()
        {
            if (DayOfYear >= 355 || DayOfYear < 80) return "Winter";      // Dec 21 - Mar 20
            if (DayOfYear >= 80 && DayOfYear < 172) return "Spring";      // Mar 21 - Jun 20
            if (DayOfYear >= 172 && DayOfYear < 266) return "Summer";     // Jun 21 - Sep 22
            return "Autumn";                                               // Sep 23 - Dec 20
        }

        /// <summary>
        /// Get approximate solar declination for current day of year (in degrees)
        /// </summary>
        public float GetSolarDeclination()
        {
            float dayAngle = (2.0f * MathHelper.Pi * (DayOfYear - 81)) / 365.0f;
            return 23.5f * (float)Math.Sin(dayAngle);
        }

        /// <summary>
        /// Update procedural skybox material based on time of day and season
        /// </summary>
        /// <summary>
        /// Calculate procedural skybox parameters based on time of day and season
        /// Returns a struct with all skybox shader parameters
        /// </summary>
        public ProceduralSkyboxParameters CalculateProceduralSkyboxParameters(float timeOfDay, float dayNightBlend)
        {
            // Calculate blending factors
            float goldenHourBlend = GetGoldenHourBlend(timeOfDay);
            bool isMorning = timeOfDay < 12.0f;

            // Sky Tint: Blend between day (blue) and night (dark blue)
            Vector3 daySkyColor = new Vector3(0.53f, 0.66f, 0.97f); // Sky blue
            Vector3 nightSkyColor = new Vector3(0.05f, 0.05f, 0.1f); // Dark blue
            Vector3 skyTint = Vector3.Lerp(nightSkyColor, daySkyColor, dayNightBlend);

            // Apply golden hour tint
            if (goldenHourBlend > 0.0f)
            {
                Vector3 goldenTint = isMorning ? new Vector3(1.0f, 0.7f, 0.5f) : new Vector3(1.0f, 0.6f, 0.4f);
                skyTint = Vector3.Lerp(skyTint, goldenTint, goldenHourBlend * 0.7f);
            }

            // Ground: Darker, brown-ish
            Vector3 dayGroundColor = new Vector3(0.4f, 0.35f, 0.3f);
            Vector3 nightGroundColor = new Vector3(0.02f, 0.02f, 0.03f);
            Vector3 groundColor = Vector3.Lerp(nightGroundColor, dayGroundColor, dayNightBlend);

            // Exposure: Brighter during day, dimmer at night
            float exposure = MathHelper.Lerp(0.8f, 1.3f, dayNightBlend);
            if (goldenHourBlend > 0.0f)
            {
                exposure = MathHelper.Lerp(exposure, 1.5f, goldenHourBlend);
            }

            // Atmosphere Thickness: Thicker at golden hour
            float atmosphereThickness = 1.0f + (goldenHourBlend * 0.5f);

            // Sun/Moon parameters
            Vector3 sunTint = new Vector3(1.0f, 0.95f, 0.88f); // Warm sun
            Vector3 moonTint = new Vector3(0.8f, 0.85f, 0.95f); // Cool moon
            Vector3 celestialTint = Vector3.Lerp(moonTint, sunTint, dayNightBlend);

            // Apply golden hour to sun tint
            if (goldenHourBlend > 0.0f && dayNightBlend > 0.5f)
            {
                Vector3 goldenSunTint = isMorning ? new Vector3(1.0f, 0.8f, 0.6f) : new Vector3(1.0f, 0.6f, 0.4f);
                celestialTint = Vector3.Lerp(celestialTint, goldenSunTint, goldenHourBlend);
            }

            // Sun size: Larger at golden hour
            float sunSize = 0.04f + (goldenHourBlend * 0.01f);
            float sunSizeConvergence = 5.0f + (goldenHourBlend * 5.0f); // More glow at golden hour

            return new ProceduralSkyboxParameters
            {
                SkyTint = skyTint,
                GroundColor = groundColor,
                Exposure = exposure,
                AtmosphereThickness = atmosphereThickness,
                SunTint = celestialTint,
                SunSize = sunSize,
                SunSizeConvergence = sunSizeConvergence
            };
        }

        /// <summary>
        /// Get current procedural skybox parameters (convenience method)
        /// </summary>
        public ProceduralSkyboxParameters GetProceduralSkyboxParameters()
        {
            float dayNightBlend = GetDayNightBlend(TimeOfDay);
            return CalculateProceduralSkyboxParameters(TimeOfDay, dayNightBlend);
        }

        // === PRIVATE HELPERS ===

        /// <summary>
        /// Apply custom transition curve for smooth blending
        /// </summary>
        private float ApplyTransitionCurve(float t)
        {
            t = MathHelper.Clamp(t, 0.0f, 1.0f);
            
            // Apply curve based on TransitionCurve parameter
            if (TransitionCurve <= 1.0f)
            {
                // Linear
                return t;
            }
            else if (TransitionCurve <= 2.0f)
            {
                // Smoothstep (default)
                return t * t * (3.0f - 2.0f * t);
            }
            else
            {
                // Smootherstep (very smooth)
                return t * t * t * (t * (t * 6.0f - 15.0f) + 10.0f);
            }
        }

        /// <summary>
        /// Calculate sunrise hour using approximate solar mechanics
        /// Based on hour angle at sunrise
        /// </summary>
        private float CalculateSunriseHour(float latitude, float declination)
        {
            float latRad = MathHelper.DegreesToRadians(latitude);
            float decRad = MathHelper.DegreesToRadians(declination);

            // Hour angle at sunrise/sunset: cos(H) = -tan(lat) * tan(dec)
            float cosH = -(float)(Math.Tan(latRad) * Math.Tan(decRad));

            // Check for polar day/night
            if (cosH > 1.0f) return 12.0f;  // Polar night - sun never rises, use noon
            if (cosH < -1.0f) return 0.0f;  // Polar day - sun never sets, use midnight

            float hourAngle = MathHelper.RadiansToDegrees((float)Math.Acos(cosH));
            float sunriseHour = 12.0f - (hourAngle / 15.0f); // 15° per hour

            return sunriseHour;
        }

        /// <summary>
        /// Calculate sunset hour using approximate solar mechanics
        /// </summary>
        private float CalculateSunsetHour(float latitude, float declination)
        {
            float latRad = MathHelper.DegreesToRadians(latitude);
            float decRad = MathHelper.DegreesToRadians(declination);

            float cosH = -(float)(Math.Tan(latRad) * Math.Tan(decRad));

            if (cosH > 1.0f) return 12.0f;  // Polar night
            if (cosH < -1.0f) return 24.0f; // Polar day

            float hourAngle = MathHelper.RadiansToDegrees((float)Math.Acos(cosH));
            float sunsetHour = 12.0f + (hourAngle / 15.0f);

            return sunsetHour;
        }

        private Quaternion QuaternionFromDirection(Vector3 direction)
        {
            // Create a quaternion that orients +Z (forward) toward the given direction
            // In a left-handed system, directional lights point in +Z direction

            if (direction.LengthSquared < 0.01f)
                return Quaternion.Identity;

            Vector3 forward = Vector3.Normalize(direction);
            Vector3 up = Vector3.UnitY;

            // Handle case where direction is parallel to up vector
            if (Math.Abs(Vector3.Dot(forward, up)) > 0.99f)
            {
                up = Vector3.UnitX;
            }

            Vector3 right = Vector3.Normalize(Vector3.Cross(up, forward));
            up = Vector3.Cross(forward, right);

            // Create rotation matrix (Matrix3 for quaternion conversion)
            Matrix3 rotationMatrix = new Matrix3(
                right.X, right.Y, right.Z,
                up.X, up.Y, up.Z,
                forward.X, forward.Y, forward.Z
            );

            return Quaternion.FromMatrix(rotationMatrix);
        }

        private float SmoothStep(float edge0, float edge1, float x)
        {
            x = MathHelper.Clamp((x - edge0) / (edge1 - edge0), 0.0f, 1.0f);
            return x * x * (3.0f - 2.0f * x);
        }
    }

    /// <summary>
    /// Parameters for procedural skybox rendering
    /// </summary>
    public struct ProceduralSkyboxParameters
    {
        public Vector3 SkyTint;
        public Vector3 GroundColor;
        public float Exposure;
        public float AtmosphereThickness;
        public Vector3 SunTint;
        public float SunSize;
        public float SunSizeConvergence;
    }

    public enum AmbientMode
    {
        Skybox,     // Ambient light derived from skybox
        Trilight,   // Unity-like trilight (sky, equator, ground colors)
        Color       // Flat ambient color
    }
}
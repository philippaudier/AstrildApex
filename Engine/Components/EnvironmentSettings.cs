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
        public string SkyboxMaterialPath { get; set; } = "";

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

        // Moon parameters
        [Engine.Serialization.Serializable("moonColor")]
        public Vector3 MoonColor { get; set; } = new Vector3(0.6f, 0.7f, 0.85f); // Cool moonlight

        [Engine.Serialization.Serializable("moonIntensity")]
        public float MoonIntensity { get; set; } = 0.25f;

        // Sunrise/Sunset colors (golden hour)
        [Engine.Serialization.Serializable("sunriseColor")]
        public Vector3 SunriseColor { get; set; } = new Vector3(1.0f, 0.7f, 0.4f); // Orange-pink

        [Engine.Serialization.Serializable("sunsetColor")]
        public Vector3 SunsetColor { get; set; } = new Vector3(1.0f, 0.5f, 0.3f); // Deep orange

        [Engine.Serialization.Serializable("goldenHourIntensity")]
        public float GoldenHourIntensity { get; set; } = 0.6f; // How much golden hour affects color (0-1)

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

        // === PUBLIC API ===

        /// <summary>
        /// Update celestial bodies (sun/moon) based on current time of day.
        /// Called by TimeComponent or manually when time changes.
        /// </summary>
        public void UpdateCelestialBodies(float timeOfDay)
        {
            TimeOfDay = timeOfDay;

            if (MainDirectionalLight == null) return;

            var light = MainDirectionalLight.GetComponent<LightComponent>();
            if (light == null) return;

            // Calculate sun position (0-24h → rotation angle)
            float sunAngle = (timeOfDay / 24.0f) * 360.0f - 90.0f; // -90 so noon = overhead
            Vector3 sunDirection = CalculateSunDirection(sunAngle);

            // Update light direction (rotate entity to point light in calculated direction)
            var rotation = QuaternionFromDirection(sunDirection);
            if (MainDirectionalLight.Transform != null)
            {
                MainDirectionalLight.Transform.Rotation = rotation;
            }

            // Blend sun/moon color and intensity based on time
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
        }

        /// <summary>
        /// Get day/night blend factor (0 = night/moon, 1 = day/sun).
        /// Uses smooth transitions at sunrise and sunset.
        /// </summary>
        public float GetDayNightBlend(float timeOfDay)
        {
            const float sunrise = 6.0f;  // 6:00 AM
            const float sunset = 18.0f;  // 6:00 PM
            const float transitionDuration = 1.0f; // 1 hour transition

            if (timeOfDay >= sunrise + transitionDuration && timeOfDay <= sunset - transitionDuration)
            {
                return 1.0f; // Full day
            }
            else if (timeOfDay >= sunrise && timeOfDay < sunrise + transitionDuration)
            {
                // Sunrise transition
                float t = (timeOfDay - sunrise) / transitionDuration;
                return SmoothStep(0.0f, 1.0f, t);
            }
            else if (timeOfDay > sunset - transitionDuration && timeOfDay <= sunset)
            {
                // Sunset transition
                float t = (timeOfDay - (sunset - transitionDuration)) / transitionDuration;
                return SmoothStep(1.0f, 0.0f, t);
            }

            return 0.0f; // Night
        }

        /// <summary>
        /// Get golden hour blend factor (0 = no golden hour, 1 = peak golden hour).
        /// Golden hour occurs during sunrise (5-7am) and sunset (5-7pm).
        /// </summary>
        public float GetGoldenHourBlend(float timeOfDay)
        {
            const float morningStart = 5.0f;
            const float morningEnd = 7.0f;
            const float eveningStart = 17.0f;
            const float eveningEnd = 19.0f;

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

        // === PRIVATE HELPERS ===

        private Vector3 CalculateSunDirection(float angleInDegrees)
        {
            // Convert angle to radians
            float angleRad = MathHelper.DegreesToRadians(angleInDegrees);

            // Rotate around the "east" axis (simplified orbit)
            // In a left-handed coordinate system: +X = east, +Y = up, +Z = north
            Vector3 direction = new Vector3(
                0,
                (float)Math.Sin(angleRad),
                (float)Math.Cos(angleRad)
            );

            // Apply axial tilt (simulates seasons)
            float tiltRad = MathHelper.DegreesToRadians(SunOrbitTilt);
            Matrix3 tiltMatrix = Matrix3.CreateRotationX(tiltRad);
            direction = Vector3.TransformNormal(direction, tiltMatrix);

            // Rotate to align with sunrise direction
            if (SunriseDirection.LengthSquared > 0.01f)
            {
                Vector3 east = Vector3.Normalize(SunriseDirection);
                Vector3 up = Vector3.UnitY;
                Vector3 north = Vector3.Cross(up, east);

                // Create rotation matrix from east/north basis
                Matrix3 basisMatrix = new Matrix3(
                    east.X, up.X, north.X,
                    east.Y, up.Y, north.Y,
                    east.Z, up.Z, north.Z
                );

                direction = Vector3.TransformNormal(direction, basisMatrix);
            }

            return Vector3.Normalize(direction);
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

            // Create rotation matrix
            Matrix3 rotationMatrix = new Matrix3(
                right.X, right.Y, right.Z,
                up.X, up.Y, up.Z,
                forward.X, forward.Y, forward.Z
            );

            return Quaternion.FromMatrix(new Matrix4(rotationMatrix));
        }

        private float SmoothStep(float edge0, float edge1, float x)
        {
            x = MathHelper.Clamp((x - edge0) / (edge1 - edge0), 0.0f, 1.0f);
            return x * x * (3.0f - 2.0f * x);
        }
    }

    public enum AmbientMode
    {
        Skybox,     // Ambient light derived from skybox
        Trilight,   // Unity-like trilight (sky, equator, ground colors)
        Color       // Flat ambient color
    }
}
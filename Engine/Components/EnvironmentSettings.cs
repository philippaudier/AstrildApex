using System;
using OpenTK.Mathematics;

namespace Engine.Components
{
    /// <summary>
    /// Unity-like Environment Settings component for managing global lighting and skybox
    /// </summary>
    public class EnvironmentSettings : Component
    {
        // Skybox settings
    [Engine.Serialization.Serializable("skyboxMaterialPath")]
    public string SkyboxMaterialPath { get; set; } = "";

    [Engine.Serialization.Serializable("skyboxTint")]
    public Vector3 SkyboxTint { get; set; } = Vector3.One;

    [Engine.Serialization.Serializable("skyboxExposure")]
    public float SkyboxExposure { get; set; } = 1.0f;
        
    // Environment lighting (store stable Entity references for serialization)
    [Engine.Serialization.Serializable("sunLight")]
    public Engine.Scene.Entity? SunLight { get; set; } = null;

    [Engine.Serialization.Serializable("moonLight")]
    public Engine.Scene.Entity? MoonLight { get; set; } = null;

    // Backwards-compatible id accessors (read-only, derived from Entity refs)
    public uint? SunLightEntityId => SunLight?.Id;
    public uint? MoonLightEntityId => MoonLight?.Id;
        
    // Ambient lighting (like Unity)
    [Engine.Serialization.Serializable("ambientMode")]
    public AmbientMode AmbientMode { get; set; } = AmbientMode.Skybox;

    [Engine.Serialization.Serializable("ambientColor")]
    public Vector3 AmbientColor { get; set; } = new Vector3(0.2f, 0.2f, 0.2f);

    [Engine.Serialization.Serializable("ambientIntensity")]
    public float AmbientIntensity { get; set; } = 1.0f;
        
    // Fog settings
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
        
    // Time of day (0-24, for sun/moon transition)
    [Engine.Serialization.Serializable("timeOfDay")]
    public float TimeOfDay { get; set; } = 12.0f; // Noon by default
        
        /// <summary>
        /// Get the currently active light based on time of day
        /// </summary>
        public uint? GetActiveLightId()
        {
            // Simple day/night cycle: 6-18 = day (sun), 18-6 = night (moon)
            bool isDay = TimeOfDay >= 6.0f && TimeOfDay < 18.0f;
            return isDay ? SunLightEntityId : MoonLightEntityId;
        }
        
        /// <summary>
        /// Get sun/moon blend factor (0 = full moon, 1 = full sun)
        /// </summary>
        public float GetSunMoonBlend()
        {
            if (TimeOfDay >= 6.0f && TimeOfDay <= 12.0f)
            {
                // Morning: fade in sun
                return (TimeOfDay - 6.0f) / 6.0f;
            }
            else if (TimeOfDay > 12.0f && TimeOfDay <= 18.0f)
            {
                // Afternoon: fade out sun
                return 1.0f - ((TimeOfDay - 12.0f) / 6.0f);
            }
            else
            {
                // Night: moon only
                return 0.0f;
            }
        }
    }
    
    public enum AmbientMode
    {
        Skybox,     // Ambient light derived from skybox
        Trilight,   // Unity-like trilight (sky, equator, ground colors)
        Color       // Flat ambient color
    }
}
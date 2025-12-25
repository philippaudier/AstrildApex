using System;

namespace Engine.Assets
{
    /// <summary>
    /// Water-specific material properties for WaterForward shader
    /// </summary>
    public sealed class WaterProperties
    {
        // Wave animation parameters
        public float WaveSpeed { get; set; } = 1.0f;           // Wave animation speed
        public float WaveHeight { get; set; } = 0.1f;          // Wave displacement height
        public float WaveFrequency { get; set; } = 2.0f;       // Wave frequency (how many waves)

        // Reflection parameters
        public float Reflectivity { get; set; } = 0.8f;        // How reflective (0-1)
        public float FresnelPower { get; set; } = 3.0f;        // Fresnel falloff power (1-10)
        public float DistortionStrength { get; set; } = 0.02f; // Normal-based distortion

        // Appearance
        public float Transparency { get; set; } = 0.3f;        // Water transparency (0-1)
        public float SpecularPower { get; set; } = 128.0f;     // Specular highlight power
        public float[] SpecularColor { get; set; } = new float[] { 1f, 1f, 1f }; // Specular color

        // Water presets
        public static WaterProperties CreateClearWater() => new WaterProperties
        {
            WaveSpeed = 0.5f,
            WaveHeight = 0.05f,
            WaveFrequency = 2.0f,
            Reflectivity = 0.8f,
            FresnelPower = 3.0f,
            DistortionStrength = 0.01f,
            Transparency = 0.2f,
            SpecularPower = 128.0f,
            SpecularColor = new float[] { 1f, 1f, 1f }
        };

        public static WaterProperties CreateOcean() => new WaterProperties
        {
            WaveSpeed = 1.5f,
            WaveHeight = 0.3f,
            WaveFrequency = 1.5f,
            Reflectivity = 0.9f,
            FresnelPower = 4.0f,
            DistortionStrength = 0.03f,
            Transparency = 0.4f,
            SpecularPower = 256.0f,
            SpecularColor = new float[] { 1f, 1f, 1f }
        };

        public static WaterProperties CreateLake() => new WaterProperties
        {
            WaveSpeed = 0.3f,
            WaveHeight = 0.02f,
            WaveFrequency = 3.0f,
            Reflectivity = 0.9f,
            FresnelPower = 2.5f,
            DistortionStrength = 0.005f,
            Transparency = 0.1f,
            SpecularPower = 64.0f,
            SpecularColor = new float[] { 1f, 1f, 1f }
        };

        public static WaterProperties CreateStylizedWater() => new WaterProperties
        {
            WaveSpeed = 2.0f,
            WaveHeight = 0.2f,
            WaveFrequency = 4.0f,
            Reflectivity = 0.7f,
            FresnelPower = 5.0f,
            DistortionStrength = 0.05f,
            Transparency = 0.5f,
            SpecularPower = 32.0f,
            SpecularColor = new float[] { 0.8f, 0.9f, 1f }
        };
    }
}

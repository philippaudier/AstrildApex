using System;

namespace Engine.Assets
{
    /// <summary>
    /// Water-specific material properties extending MaterialAsset
    /// </summary>
    public sealed class WaterMaterialProperties
    {
        // Wave parameters
        public float WaveAmplitude { get; set; } = 0.1f;
        public float WaveFrequency { get; set; } = 1.0f;
        public float WaveSpeed { get; set; } = 1.0f;
        public float[] WaveDirection { get; set; } = new float[] { 1f, 0f };

        // Water appearance
        public float[] WaterColor { get; set; } = new float[] { 0.1f, 0.3f, 0.5f, 0.8f }; // RGBA
        public float Opacity { get; set; } = 0.8f;

        // Albedo texture
        public Guid? AlbedoTexture { get; set; }
        public float[] AlbedoColor { get; set; } = new float[] { 1.0f, 1.0f, 1.0f, 1.0f }; // RGBA tint
        public float[] AlbedoTiling { get; set; } = new float[] { 1.0f, 1.0f };
        public float[] AlbedoScrollSpeed { get; set; } = new float[] { 0.0f, 0.0f }; // Scroll animation speed

        // Normal map
        public Guid? NormalTexture { get; set; }
        public float NormalStrength { get; set; } = 1.0f;
        public float[] NormalTiling { get; set; } = new float[] { 1.0f, 1.0f };
        public float[] NormalScrollSpeed1 { get; set; } = new float[] { 0.05f, 0.03f }; // First layer scroll speed
        public float[] NormalScrollSpeed2 { get; set; } = new float[] { -0.04f, -0.06f }; // Second layer scroll speed

        // PBR properties
        public float Metallic { get; set; } = 0.0f;
        public float Smoothness { get; set; } = 0.9f; // Water is typically very smooth

        // Noise texture 1 (refraction/distortion)
        public Guid? NoiseTexture1 { get; set; }
        public float[] Noise1Speed { get; set; } = new float[] { 0.03f, 0.03f };
        public float[] Noise1Direction { get; set; } = new float[] { 1f, 0f };
        public float[] Noise1Tiling { get; set; } = new float[] { 1f, 1f };
        public float Noise1Strength { get; set; } = 0.05f;

        // Noise texture 2 (refraction/distortion)
        public Guid? NoiseTexture2 { get; set; }
        public float[] Noise2Speed { get; set; } = new float[] { 0.02f, -0.02f };
        public float[] Noise2Direction { get; set; } = new float[] { 0f, 1f };
        public float[] Noise2Tiling { get; set; } = new float[] { 1.5f, 1.5f };
        public float Noise2Strength { get; set; } = 0.03f;

        // Refraction
        public float RefractionStrength { get; set; } = 0.5f;

        // Fresnel (reflectivity at angles)
        public float FresnelPower { get; set; } = 2.0f;
        public float[] FresnelColor { get; set; } = new float[] { 0.8f, 0.9f, 1.0f, 1.0f }; // RGBA

        // Planar Reflection (auto-generated, no manual texture needed)
        public bool EnableReflection { get; set; } = false;
        public float ReflectionStrength { get; set; } = 1.0f;
        public float ReflectionBlur { get; set; } = 0.0f;

        // Tessellation
        public float TessellationLevel { get; set; } = 32.0f; // 1-64, controls mesh subdivision
    // Use procedural noise (snoise) or sample noise textures for cheaper displacement
    public bool UseProceduralNoise { get; set; } = true;

    // Tessellation LOD distances (meters) and levels
    public float TessNearDistance { get; set; } = 20f;
    public float TessMidDistance { get; set; } = 80f;
    public float TessFarDistance { get; set; } = 200f;
    public float TessNearLevel { get; set; } = 32f;
    public float TessMidLevel { get; set; } = 16f;
    public float TessFarLevel { get; set; } = 4f;

    // Reflection update interval (frames). 1 = every frame, 2 = every other frame, etc.
    public int ReflectionUpdateInterval { get; set; } = 2;

        // Wave presets
        public static WaterMaterialProperties CreateSteady() => new WaterMaterialProperties
        {
            WaveAmplitude = 0.02f,
            WaveFrequency = 0.5f,
            WaveSpeed = 0.5f
        };

        public static WaterMaterialProperties CreateCalm() => new WaterMaterialProperties
        {
            WaveAmplitude = 0.05f,
            WaveFrequency = 0.8f,
            WaveSpeed = 0.8f
        };

        public static WaterMaterialProperties CreateModerate() => new WaterMaterialProperties
        {
            WaveAmplitude = 0.15f,
            WaveFrequency = 1.2f,
            WaveSpeed = 1.5f
        };

        public static WaterMaterialProperties CreateRough() => new WaterMaterialProperties
        {
            WaveAmplitude = 0.3f,
            WaveFrequency = 1.5f,
            WaveSpeed = 2.5f
        };

        public static WaterMaterialProperties CreateStormy() => new WaterMaterialProperties
        {
            WaveAmplitude = 0.6f,
            WaveFrequency = 2.0f,
            WaveSpeed = 4.0f
        };

        public static WaterMaterialProperties CreateTsunami() => new WaterMaterialProperties
        {
            WaveAmplitude = 1.5f,
            WaveFrequency = 0.3f,
            WaveSpeed = 8.0f
        };
    }
}

using System;

namespace Engine.Assets
{
    /// <summary>
    /// Glass-specific material properties extending MaterialAsset
    /// </summary>
    public sealed class GlassMaterialProperties
    {
        // Refraction properties
        public float RefractiveIndex { get; set; } = 1.5f;        // 1.0 = air, 1.33 = water, 1.5 = glass, 1.9 = diamond
        public float DistortionStrength { get; set; } = 1.0f;     // 0.0 = no distortion, 1.0 = full physical refraction
        public float ChromaticAberration { get; set; } = 0.0f;    // 0.0 = none, 1.0 = full RGB color separation

        // Appearance
        public float Roughness { get; set; } = 0.0f;              // 0.0 = smooth glass, 1.0 = frosted glass
        public float Thickness { get; set; } = 0.1f;              // Glass thickness (affects absorption/tint)
        public float[] Tint { get; set; } = new float[] { 1f, 1f, 1f }; // RGB color tint (absorption color)
        public float Opacity { get; set; } = 0.1f;                // 0.0 = fully transparent, 1.0 = opaque

        // Fresnel (reflection based on viewing angle)
        public float FresnelPower { get; set; } = 5.0f;           // Controls Fresnel falloff (1-10)
        public float ReflectionStrength { get; set; } = 1.0f;     // Base reflection intensity

        // Glass presets for common use cases
        public static GlassMaterialProperties CreateWindow() => new GlassMaterialProperties
        {
            RefractiveIndex = 1.52f,        // Standard window glass
            DistortionStrength = 0.05f,     // Minimal distortion (flat glass)
            ChromaticAberration = 0.0f,     // No aberration
            Roughness = 0.0f,               // Perfectly smooth
            Thickness = 0.05f,              // Thin glass
            Tint = new float[] { 1f, 1f, 1f }, // Clear
            Opacity = 0.05f,                // Very transparent
            FresnelPower = 5.0f,
            ReflectionStrength = 0.8f
        };

        public static GlassMaterialProperties CreateFrostedGlass() => new GlassMaterialProperties
        {
            RefractiveIndex = 1.5f,
            DistortionStrength = 0.2f,      // Some distortion
            ChromaticAberration = 0.0f,
            Roughness = 0.7f,               // Frosted surface
            Thickness = 0.08f,
            Tint = new float[] { 1f, 1f, 1f },
            Opacity = 0.3f,                 // More opaque
            FresnelPower = 3.0f,
            ReflectionStrength = 0.5f
        };

        public static GlassMaterialProperties CreateSphere() => new GlassMaterialProperties
        {
            RefractiveIndex = 1.5f,
            DistortionStrength = 1.0f,      // Full physical distortion (inverts image)
            ChromaticAberration = 0.2f,     // Slight color separation
            Roughness = 0.0f,               // Smooth
            Thickness = 0.3f,               // Thicker (more tint)
            Tint = new float[] { 1f, 1f, 1f },
            Opacity = 0.1f,
            FresnelPower = 5.0f,
            ReflectionStrength = 1.0f
        };

        public static GlassMaterialProperties CreateDiamond() => new GlassMaterialProperties
        {
            RefractiveIndex = 2.42f,        // Diamond has very high refractive index
            DistortionStrength = 1.0f,
            ChromaticAberration = 0.5f,     // Strong color dispersion
            Roughness = 0.0f,
            Thickness = 0.2f,
            Tint = new float[] { 1f, 1f, 1f },
            Opacity = 0.05f,
            FresnelPower = 5.0f,
            ReflectionStrength = 1.5f
        };

        public static GlassMaterialProperties CreateStainedGlass() => new GlassMaterialProperties
        {
            RefractiveIndex = 1.52f,
            DistortionStrength = 0.1f,
            ChromaticAberration = 0.0f,
            Roughness = 0.1f,
            Thickness = 0.1f,
            Tint = new float[] { 0.8f, 0.2f, 0.2f }, // Red tint
            Opacity = 0.2f,
            FresnelPower = 4.0f,
            ReflectionStrength = 0.6f
        };

        public static GlassMaterialProperties CreateBottle() => new GlassMaterialProperties
        {
            RefractiveIndex = 1.5f,
            DistortionStrength = 0.6f,      // Medium distortion due to curvature
            ChromaticAberration = 0.1f,
            Roughness = 0.05f,
            Thickness = 0.15f,
            Tint = new float[] { 0.7f, 0.9f, 0.8f }, // Green tint
            Opacity = 0.15f,
            FresnelPower = 5.0f,
            ReflectionStrength = 0.9f
        };
    }
}

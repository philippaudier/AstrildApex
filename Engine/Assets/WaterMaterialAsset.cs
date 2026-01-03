using System;

namespace Engine.Assets
{
    /// <summary>
    /// Water-specific material properties for WaterForward shader
    /// </summary>
    public sealed class WaterProperties
    {
        // === PHASE 1: Base Color ===
        public float[] WaterColor { get; set; } = new float[] { 0.0f, 0.4f, 0.6f, 1.0f };      // Shallow water color (RGBA)
        public float[] DeepWaterColor { get; set; } = new float[] { 0.0f, 0.1f, 0.3f, 1.0f };  // Deep water color (RGBA)
        public float Transparency { get; set; } = 0.3f;                                        // Overall transparency (0-1)

        // === PHASE 1 & 6: Wave Animation Parameters ===
        public float WaveSpeed { get; set; } = 1.0f;           // Wave animation speed
        public float WaveAmplitude { get; set; } = 0.1f;       // Wave displacement height (vertex displacement)
        public float WaveHeight { get; set; } = 0.1f;          // Legacy: Wave displacement height (use WaveAmplitude)
        public float WaveFrequency { get; set; } = 2.0f;       // Wave frequency (how many waves)
        public float[] WaveDirection { get; set; } = new float[] { 1.0f, 0.0f }; // Wave direction (normalized XZ)

        // === PHASE 2: Two-Layer Normal Mapping ===
        public float NormalStrength { get; set; } = 1.0f;      // Strength of first normal map
        public float NormalStrength2 { get; set; } = 0.5f;     // Strength of second normal map
        public float NormalBlend { get; set; } = 0.5f;         // Blend between two normal maps (0-1)
        public float NormalMapScale { get; set; } = 1.0f;      // Legacy: Tiling scale for normal maps (use NormalLayer1Scale/NormalLayer2Scale instead)
        public float NormalLayer1Scale { get; set; } = 1.0f;   // Tiling scale for first normal map layer
        public float NormalLayer2Scale { get; set; } = 1.3f;   // Tiling scale for second normal map layer
        public float NormalLayer1Speed { get; set; } = 0.05f;  // Speed of first normal layer animation
        public float NormalLayer2Speed { get; set; } = -0.03f; // Speed of second normal layer (negative = opposite direction)
        public float[] NormalLayer1Direction { get; set; } = new float[] { 1.0f, 0.0f }; // Direction of first normal layer
        public float[] NormalLayer2Direction { get; set; } = new float[] { 0.0f, 1.0f }; // Direction of second normal layer

        // === PHASE 2: Depth & Refraction ===
        public float DepthFadeDistance { get; set; } = 2.0f;   // Distance over which water fades to opaque
        public float RefractionStrength { get; set; } = 0.05f; // Strength of refraction distortion
        public bool UseRefraction { get; set; } = true;        // Enable/disable refraction

        // === PHASE 3: PBR & Lighting ===
        public float Roughness { get; set; } = 0.1f;           // Surface roughness (0 = mirror, 1 = diffuse)
        public float Metallic { get; set; } = 0.0f;            // Metallic property (usually 0 for water)
        public float Fresnel { get; set; } = 1.0f;             // Fresnel strength (reflection at grazing angles)
        public float SpecularStrength { get; set; } = 1.0f;    // Strength of direct specular highlights

        // === PHASE 3: Legacy Reflection Parameters (compatibility) ===
        public float Reflectivity { get; set; } = 0.8f;        // Legacy: How reflective (0-1) - use Fresnel instead
        public float FresnelPower { get; set; } = 3.0f;        // Legacy: Fresnel falloff power (1-10)
        public float DistortionStrength { get; set; } = 0.02f; // Legacy: Normal-based distortion - use RefractionStrength
        public float SpecularPower { get; set; } = 128.0f;     // Legacy: Specular highlight power - use Roughness
        public float[] SpecularColor { get; set; } = new float[] { 1f, 1f, 1f }; // Legacy: Specular color

        // === PHASE 4: Color Absorption (depth-based tinting) ===
        public float[] AbsorptionColor { get; set; } = new float[] { 0.4f, 0.8f, 1.0f }; // Color absorbed with depth (RGB)
        public float AbsorptionStrength { get; set; } = 0.1f;                            // Strength of color absorption

        // === PHASE 5: Foam & Edge Effects ===
        public float FoamAmount { get; set; } = 0.5f;          // Amount of foam at edges (0-1)
        public float FoamCutoff { get; set; } = 0.5f;          // Depth threshold for foam appearance (meters)
        public float[] FoamColor { get; set; } = new float[] { 1.0f, 1.0f, 1.0f, 1.0f }; // Foam color (RGBA)
        public float FoamTextureScale { get; set; } = 4.0f;    // Tiling scale for foam texture
        public float FoamAlphaClipThreshold { get; set; } = 0.1f; // Alpha clipping threshold for foam texture (0-1)
        public float EdgeFadeDistance { get; set; } = 1.0f;    // Distance for edge blending

        // === PHASE 6: AAA Features ===
        // Caustics
        public bool UseCaustics { get; set; } = true;          // Enable/disable caustics
        public float CausticsStrength { get; set; } = 1.0f;    // Intensity of caustics
        public float CausticsScale { get; set; } = 2.0f;       // Tiling scale for caustics pattern
        public float CausticsSpeed { get; set; } = 0.5f;       // Animation speed for caustics
        public float[] CausticsColor { get; set; } = new float[] { 1.0f, 0.95f, 0.8f }; // Caustics tint (RGB)
        public float CausticsSplit { get; set; } = 0.01f;      // Chromatic aberration strength (RGB split)
        public float CausticsDistortion { get; set; } = 1.0f;  // How much water normals affect caustics (0 = none, 1 = full physics)

        // Planar Reflections
        public bool UsePlanarReflections { get; set; } = true; // Enable/disable planar reflections (ENABLED BY DEFAULT)
        public float ReflectionBlur { get; set; } = 0.0f;       // Blur amount for reflections (0-1)
        public int ReflectionResolution { get; set; } = 1024;   // Resolution of reflection texture (256, 512, 1024, 2048)
        public bool FlipReflectionX { get; set; } = false;      // Flip reflection horizontally
        public bool FlipReflectionY { get; set; } = false;      // Flip reflection vertically
        public float ReflectionClipPlaneOffset { get; set; } = 0.05f; // Offset above water level for clipping plane (prevents terrain bleeding through)

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

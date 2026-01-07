using System;

namespace Engine.Assets
{
    /// <summary>
    /// Vegetation-specific material properties for VegetationForward shader
    /// </summary>
    public sealed class VegetationProperties
    {
        // === GLOBAL/LOCAL/BLEND WIND SYSTEM ===
        // 0 = Global (wind from WeatherComponent)
        // 1 = Local (material-specific wind parameters)
        // 2 = Blend (mix between Local and Global)
        public int WindMode { get; set; } = 0;                  // Wind control mode (Global/Local/Blend)
        public float WindBlendFactor { get; set; } = 1.0f;      // Blend factor (0 = local, 1 = global) - used when WindMode = 2

        // === LOCAL WIND PARAMETERS ===
        public float WindStrength { get; set; } = 0.5f;         // 0-1 overall wind intensity
        public float[] WindDirection { get; set; } = new float[] { 1.0f, 0.0f }; // Wind direction XZ (normalized)
        public float WindSpeed { get; set; } = 1.0f;            // Animation speed multiplier
        public float WindGustiness { get; set; } = 0.5f;        // 0 = smooth, 1 = gusty turbulence

        // === ADVANCED WIND PARAMETERS (Vegetation specific) ===
        public float BranchAmplitude { get; set; } = 2.5f;      // Branch sway amplitude multiplier
        public float BranchSpeed { get; set; } = 4.0f;          // Branch oscillation speed
        public float BranchTurbulence { get; set; } = 0.8f;     // Branch detail/noise intensity
        public float TrunkStiffness { get; set; } = 0.85f;      // Trunk rigidity (0=flexible, 1=rigid)
        public float TrunkBendAmount { get; set; } = 0.3f;      // How much trunk bends at top
        public float LeafFlutter { get; set; } = 0.6f;          // Leaf flutter intensity
        public float LeafFlutterSpeed { get; set; } = 8.0f;     // Leaf flutter speed
    }
}

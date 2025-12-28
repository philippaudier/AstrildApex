using System;

namespace Engine.Rendering.Terrain
{
    /// <summary>
    /// Preset configurations for common terrain types.
    /// These provide starting points for natural-looking procedural terrains.
    /// </summary>
    public static class TerrainPresets
    {
        public class PresetConfig
        {
            public string Name { get; set; } = "";
            public string Description { get; set; } = "";
            public NoiseType NoiseType { get; set; }
            public float NoiseScale { get; set; }
            public int Octaves { get; set; }
            public float Persistence { get; set; }
            public float Lacunarity { get; set; }
            public float HeightMultiplier { get; set; }
            public float HeightPower { get; set; }
            public bool IslandMode { get; set; }
            public float IslandFalloff { get; set; }
            public bool EnableTerracing { get; set; }
            public int TerraceCount { get; set; }

            // Domain warping parameters (to be added)
            public bool UseDomainWarping { get; set; }
            public float DomainWarpStrength { get; set; }
        }

        /// <summary>
        /// Gentle rolling hills - perfect for farmlands or peaceful landscapes
        /// </summary>
        public static readonly PresetConfig RollingHills = new PresetConfig
        {
            Name = "Rolling Hills",
            Description = "Gentle rolling terrain perfect for farmlands",
            NoiseType = NoiseType.Fractal,
            NoiseScale = 80f,
            Octaves = 4,
            Persistence = 0.5f,
            Lacunarity = 2.0f,
            HeightMultiplier = 0.6f,
            HeightPower = 1.2f,
            IslandMode = false,
            UseDomainWarping = true,
            DomainWarpStrength = 0.3f
        };

        /// <summary>
        /// Dramatic mountain ranges with sharp peaks and ridges
        /// </summary>
        public static readonly PresetConfig Mountains = new PresetConfig
        {
            Name = "Mountains",
            Description = "Dramatic mountain ranges with sharp peaks",
            NoiseType = NoiseType.Ridged,
            NoiseScale = 120f,
            Octaves = 6,
            Persistence = 0.6f,
            Lacunarity = 2.2f,
            HeightMultiplier = 1.2f,
            HeightPower = 0.8f,
            IslandMode = false,
            UseDomainWarping = true,
            DomainWarpStrength = 0.5f
        };

        /// <summary>
        /// Smooth desert dunes with soft curves
        /// </summary>
        public static readonly PresetConfig DesertDunes = new PresetConfig
        {
            Name = "Desert Dunes",
            Description = "Smooth desert dunes with soft curves",
            NoiseType = NoiseType.Billow,
            NoiseScale = 60f,
            Octaves = 3,
            Persistence = 0.4f,
            Lacunarity = 1.8f,
            HeightMultiplier = 0.4f,
            HeightPower = 1.5f,
            IslandMode = false,
            UseDomainWarping = true,
            DomainWarpStrength = 0.6f
        };

        /// <summary>
        /// Island with beaches and central high ground
        /// </summary>
        public static readonly PresetConfig Island = new PresetConfig
        {
            Name = "Island",
            Description = "Island with beaches and central highlands",
            NoiseType = NoiseType.Fractal,
            NoiseScale = 70f,
            Octaves = 5,
            Persistence = 0.5f,
            Lacunarity = 2.0f,
            HeightMultiplier = 1.0f,
            HeightPower = 1.0f,
            IslandMode = true,
            IslandFalloff = 3f,
            UseDomainWarping = true,
            DomainWarpStrength = 0.4f
        };

        /// <summary>
        /// Flat plateaus with sharp cliffs - mesa-like terrain
        /// </summary>
        public static readonly PresetConfig Plateaus = new PresetConfig
        {
            Name = "Plateaus",
            Description = "Flat plateaus with sharp cliffs (mesa-like)",
            NoiseType = NoiseType.Fractal,
            NoiseScale = 90f,
            Octaves = 4,
            Persistence = 0.5f,
            Lacunarity = 2.1f,
            HeightMultiplier = 1.0f,
            HeightPower = 0.6f,
            IslandMode = false,
            EnableTerracing = true,
            TerraceCount = 8,
            UseDomainWarping = false
        };

        /// <summary>
        /// Volcanic landscape with sharp features
        /// </summary>
        public static readonly PresetConfig Volcanic = new PresetConfig
        {
            Name = "Volcanic",
            Description = "Volcanic landscape with sharp features",
            NoiseType = NoiseType.Ridged,
            NoiseScale = 100f,
            Octaves = 7,
            Persistence = 0.7f,
            Lacunarity = 2.5f,
            HeightMultiplier = 1.5f,
            HeightPower = 0.7f,
            IslandMode = false,
            UseDomainWarping = true,
            DomainWarpStrength = 0.7f
        };

        /// <summary>
        /// Gentle plains with minimal elevation changes
        /// </summary>
        public static readonly PresetConfig Plains = new PresetConfig
        {
            Name = "Plains",
            Description = "Gentle plains with minimal elevation",
            NoiseType = NoiseType.Fractal,
            NoiseScale = 150f,
            Octaves = 3,
            Persistence = 0.3f,
            Lacunarity = 1.8f,
            HeightMultiplier = 0.3f,
            HeightPower = 1.3f,
            IslandMode = false,
            UseDomainWarping = true,
            DomainWarpStrength = 0.2f
        };

        /// <summary>
        /// Canyon with deep valleys and steep walls
        /// </summary>
        public static readonly PresetConfig Canyon = new PresetConfig
        {
            Name = "Canyon",
            Description = "Deep canyons with steep walls",
            NoiseType = NoiseType.Ridged,
            NoiseScale = 80f,
            Octaves = 5,
            Persistence = 0.6f,
            Lacunarity = 2.3f,
            HeightMultiplier = 1.0f,
            HeightPower = 2.0f, // Strong power curve creates deep valleys
            IslandMode = false,
            UseDomainWarping = true,
            DomainWarpStrength = 0.5f
        };

        /// <summary>
        /// Get all available presets
        /// </summary>
        public static PresetConfig[] GetAllPresets()
        {
            return new[]
            {
                RollingHills,
                Mountains,
                DesertDunes,
                Island,
                Plateaus,
                Volcanic,
                Plains,
                Canyon
            };
        }

        /// <summary>
        /// Apply a preset configuration to terrain component
        /// </summary>
        public static void ApplyPreset(Engine.Components.Terrain terrain, PresetConfig preset)
        {
            terrain.NoiseType = preset.NoiseType;
            terrain.NoiseScale = preset.NoiseScale;
            terrain.Octaves = preset.Octaves;
            terrain.Persistence = preset.Persistence;
            terrain.Lacunarity = preset.Lacunarity;
            terrain.HeightMultiplier = preset.HeightMultiplier;
            terrain.HeightPower = preset.HeightPower;
            terrain.IslandMode = preset.IslandMode;
            terrain.IslandFalloff = preset.IslandFalloff;
            terrain.EnableTerracing = preset.EnableTerracing;
            terrain.TerraceCount = preset.TerraceCount;
            terrain.UseDomainWarping = preset.UseDomainWarping;
            terrain.DomainWarpStrength = preset.DomainWarpStrength;
        }
    }
}

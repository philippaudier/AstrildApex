using System;

namespace Engine.Rendering.Terrain
{
    /// <summary>
    /// Noise type for procedural terrain generation
    /// </summary>
    public enum NoiseType
    {
        Fractal,    // Standard multi-octave noise (natural hills)
        Ridged,     // Inverted noise for mountain ridges
        Billow      // Abs noise for cloud-like formations
    }

    /// <summary>
    /// Parameters for procedural heightmap generation
    /// </summary>
    public class ProceduralHeightmapParams
    {
        public int Seed { get; set; } = 0;
        public float NoiseScale { get; set; } = 50f;
        public int Octaves { get; set; } = 4;
        public float Persistence { get; set; } = 0.5f;
        public float Lacunarity { get; set; } = 2.0f;
        public float OffsetX { get; set; } = 0f;
        public float OffsetY { get; set; } = 0f;
        public NoiseType NoiseType { get; set; } = NoiseType.Fractal;

        // Island mode - creates circular falloff for island generation
        public bool IslandMode { get; set; } = false;
        public float IslandFalloff { get; set; } = 3f;

        // Terracing - creates step-like layers
        public bool EnableTerracing { get; set; } = false;
        public int TerraceCount { get; set; } = 5;

        // Height remapping
        public float HeightMultiplier { get; set; } = 1f;
        public float HeightPower { get; set; } = 1f;  // Power curve (>1 = valleys, <1 = plateaus)

        // Domain warping - distorts noise space for more organic results
        public bool UseDomainWarping { get; set; } = false;
        public float DomainWarpStrength { get; set; } = 0.5f;  // 0 = no warp, 1 = strong warp

        // Erosion simulation
        public bool ApplyErosion { get; set; } = false;
        public int HydraulicIterations { get; set; } = 50000;
        public float HydraulicStrength { get; set; } = 0.3f;
        public int ThermalIterations { get; set; } = 5;
        public float ThermalTalusAngle { get; set; } = 0.05f;
        public float ThermalStrength { get; set; } = 0.5f;
    }

    /// <summary>
    /// High-level API for generating procedural heightmaps
    /// </summary>
    public static class ProceduralHeightmapGenerator
    {
        /// <summary>
        /// Generate a procedural heightmap with the given parameters.
        /// Returns normalized float[,] with values [0,1].
        /// </summary>
        public static float[,] Generate(int width, int height, ProceduralHeightmapParams parameters)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentException("Width and height must be positive");

            // Initialize noise with seed
            NoiseGenerator.InitializePermutation(parameters.Seed);

            float[,] heightmap = new float[width, height];
            float minValue = float.MaxValue;
            float maxValue = float.MinValue;

            // Generate raw noise
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Calculate normalized coordinates [0,1]
                    float nx = (float)x / width;
                    float ny = (float)y / height;

                    // Apply scale and offset
                    float sampleX = (nx - 0.5f) * parameters.NoiseScale + parameters.OffsetX;
                    float sampleY = (ny - 0.5f) * parameters.NoiseScale + parameters.OffsetY;

                    // Apply domain warping if enabled
                    // This distorts the sampling coordinates using additional noise layers
                    // Result: breaks up repetitive patterns and creates more organic shapes
                    if (parameters.UseDomainWarping)
                    {
                        // Sample two orthogonal noise fields to warp the domain
                        float warpScale = parameters.NoiseScale * 0.5f; // Warp at different frequency
                        float warpX = NoiseGenerator.SimplexNoise2D(sampleX * 0.3f, sampleY * 0.3f + 100f);
                        float warpY = NoiseGenerator.SimplexNoise2D(sampleX * 0.3f + 200f, sampleY * 0.3f);

                        // Apply warp with user-controlled strength
                        float warpAmount = parameters.DomainWarpStrength * warpScale;
                        sampleX += warpX * warpAmount;
                        sampleY += warpY * warpAmount;
                    }

                    // Sample noise based on type
                    float noiseValue = parameters.NoiseType switch
                    {
                        NoiseType.Fractal => NoiseGenerator.FractalNoise2D(
                            sampleX, sampleY,
                            parameters.Octaves,
                            parameters.Persistence,
                            parameters.Lacunarity),

                        NoiseType.Ridged => NoiseGenerator.RidgedNoise2D(
                            sampleX, sampleY,
                            parameters.Octaves,
                            parameters.Persistence,
                            parameters.Lacunarity),

                        NoiseType.Billow => NoiseGenerator.BillowNoise2D(
                            sampleX, sampleY,
                            parameters.Octaves,
                            parameters.Persistence,
                            parameters.Lacunarity),

                        _ => 0f
                    };

                    // Apply island mode falloff
                    if (parameters.IslandMode)
                    {
                        float dx = nx - 0.5f;
                        float dy = ny - 0.5f;
                        float distance = (float)Math.Sqrt(dx * dx + dy * dy) * 2f; // [0,~1.41]

                        // Smooth falloff curve
                        float falloff = (float)Math.Pow(distance, parameters.IslandFalloff);
                        noiseValue = Math.Max(0f, noiseValue - falloff);
                    }

                    heightmap[x, y] = noiseValue;

                    minValue = Math.Min(minValue, noiseValue);
                    maxValue = Math.Max(maxValue, noiseValue);
                }
            }

            // Normalize to [0, 1]
            if (maxValue > minValue)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        heightmap[x, y] = (heightmap[x, y] - minValue) / (maxValue - minValue);
                    }
                }
            }

            // Apply erosion BEFORE terracing and height adjustments
            // This ensures erosion operates on normalized [0,1] heightmap
            if (parameters.ApplyErosion)
            {
                Console.WriteLine($"[ProceduralHeightmap] Applying erosion (hydraulic={parameters.HydraulicIterations}, thermal={parameters.ThermalIterations})...");
                TerrainErosion.ApplyCombinedErosion(
                    heightmap,
                    parameters.HydraulicIterations, parameters.HydraulicStrength,
                    parameters.ThermalIterations, parameters.ThermalTalusAngle, parameters.ThermalStrength);

                // Re-normalize after erosion (values may have shifted)
                float erosionMin = float.MaxValue;
                float erosionMax = float.MinValue;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        erosionMin = Math.Min(erosionMin, heightmap[x, y]);
                        erosionMax = Math.Max(erosionMax, heightmap[x, y]);
                    }
                }

                if (erosionMax > erosionMin)
                {
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            heightmap[x, y] = (heightmap[x, y] - erosionMin) / (erosionMax - erosionMin);
                        }
                    }
                }
            }

            // Apply terracing
            if (parameters.EnableTerracing && parameters.TerraceCount > 1)
            {
                ApplyTerracing(heightmap, parameters.TerraceCount);
            }

            // Apply height curve
            if (Math.Abs(parameters.HeightPower - 1f) > 0.001f)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        heightmap[x, y] = (float)Math.Pow(heightmap[x, y], parameters.HeightPower);
                    }
                }
            }

            // Apply height multiplier
            if (Math.Abs(parameters.HeightMultiplier - 1f) > 0.001f)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        heightmap[x, y] = Math.Clamp(heightmap[x, y] * parameters.HeightMultiplier, 0f, 1f);
                    }
                }
            }

            return heightmap;
        }

        /// <summary>
        /// Apply step-like terracing to heightmap
        /// </summary>
        private static void ApplyTerracing(float[,] heightmap, int terraceCount)
        {
            int width = heightmap.GetLength(0);
            int height = heightmap.GetLength(1);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float h = heightmap[x, y];

                    // Quantize to terraces
                    float terrace = (float)Math.Floor(h * terraceCount) / terraceCount;

                    // Blend for smooth transitions (70% terrace, 30% original)
                    heightmap[x, y] = terrace * 0.7f + h * 0.3f;
                }
            }
        }

        /// <summary>
        /// Export heightmap to 16-bit PNG file
        /// </summary>
        public static void ExportToPng(float[,] heightmap, string filePath)
        {
            int width = heightmap.GetLength(0);
            int height = heightmap.GetLength(1);

            // Convert to 16-bit grayscale
            byte[] pixelData = new byte[width * height * 2]; // 2 bytes per pixel for 16-bit

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    ushort value = (ushort)(heightmap[x, y] * 65535f);
                    int idx = (y * width + x) * 2;

                    // Big-endian
                    pixelData[idx] = (byte)(value >> 8);
                    pixelData[idx + 1] = (byte)(value & 0xFF);
                }
            }

            // Use SixLabors.ImageSharp to write PNG
            using var image = SixLabors.ImageSharp.Image.LoadPixelData<SixLabors.ImageSharp.PixelFormats.L16>(
                pixelData, width, height);

            var encoder = new SixLabors.ImageSharp.Formats.Png.PngEncoder
            {
                BitDepth = SixLabors.ImageSharp.Formats.Png.PngBitDepth.Bit16,
                ColorType = SixLabors.ImageSharp.Formats.Png.PngColorType.Grayscale
            };

            using var stream = System.IO.File.Create(filePath);
            image.Save(stream, encoder);

            Console.WriteLine($"[ProceduralHeightmap] Exported heightmap to {filePath} ({width}x{height}, 16-bit)");
        }
    }
}

using System;

#pragma warning disable CS0618 // Using obsolete properties for preset initialization

namespace Engine.Assets
{
    /// <summary>
    /// Utility class for creating common terrain layer presets
    /// </summary>
    public static class TerrainLayerPresets
    {
        /// <summary>
        /// Create a grass layer (flat areas, low slopes)
        /// </summary>
        public static TerrainLayer CreateGrassLayer()
        {
            return new TerrainLayer
            {
                Name = "Grass",
                SlopeMinDeg = 0f,
                SlopeMaxDeg = 25f,
                SlopeBlendDistance = 5f,
                HeightMin = -1000f,
                HeightMax = 1000f,
                HeightBlendDistance = 10f,
                Strength = 1f,
                Priority = 0,
                BlendMode = TerrainLayerBlendMode.Slope,
                Tiling = new float[] { 10f, 10f },
                Offset = new float[] { 0f, 0f },
                Metallic = 0f,
                Smoothness = 0.3f
            };
        }

        /// <summary>
        /// Create a stone/rock layer (steep slopes)
        /// </summary>
        public static TerrainLayer CreateStoneLayer()
        {
            return new TerrainLayer
            {
                Name = "Stone",
                SlopeMinDeg = 30f,
                SlopeMaxDeg = 90f,
                SlopeBlendDistance = 8f,
                HeightMin = -1000f,
                HeightMax = 1000f,
                HeightBlendDistance = 15f,
                Strength = 1f,
                Priority = 1,
                BlendMode = TerrainLayerBlendMode.Slope,
                Tiling = new float[] { 5f, 5f },
                Offset = new float[] { 0f, 0f },
                Metallic = 0.1f,
                Smoothness = 0.7f
            };
        }

        /// <summary>
        /// Create a sand layer (low areas, beaches)
        /// </summary>
        public static TerrainLayer CreateSandLayer()
        {
            return new TerrainLayer
            {
                Name = "Sand",
                SlopeMinDeg = 0f,
                SlopeMaxDeg = 15f,
                SlopeBlendDistance = 3f,
                HeightMin = -5f,
                HeightMax = 2f,
                HeightBlendDistance = 2f,
                Strength = 1f,
                Priority = 2,
                BlendMode = TerrainLayerBlendMode.HeightAndSlope,
                Tiling = new float[] { 15f, 15f },
                Offset = new float[] { 0f, 0f },
                Metallic = 0f,
                Smoothness = 0.1f
            };
        }

        /// <summary>
        /// Create a snow layer (high altitude)
        /// </summary>
        public static TerrainLayer CreateSnowLayer()
        {
            return new TerrainLayer
            {
                Name = "Snow",
                SlopeMinDeg = 0f,
                SlopeMaxDeg = 40f,
                SlopeBlendDistance = 5f,
                HeightMin = 50f,
                HeightMax = 1000f,
                HeightBlendDistance = 20f,
                Strength = 1f,
                Priority = 3,
                BlendMode = TerrainLayerBlendMode.HeightAndSlope,
                Tiling = new float[] { 8f, 8f },
                Offset = new float[] { 0f, 0f },
                Metallic = 0f,
                Smoothness = 0.9f
            };
        }

        /// <summary>
        /// Create a dirt/soil layer (medium slopes, medium heights)
        /// </summary>
        public static TerrainLayer CreateDirtLayer()
        {
            return new TerrainLayer
            {
                Name = "Dirt",
                SlopeMinDeg = 15f,
                SlopeMaxDeg = 45f,
                SlopeBlendDistance = 7f,
                HeightMin = 0f,
                HeightMax = 50f,
                HeightBlendDistance = 10f,
                Strength = 1f,
                Priority = 1,
                BlendMode = TerrainLayerBlendMode.HeightAndSlope,
                Tiling = new float[] { 12f, 12f },
                Offset = new float[] { 0f, 0f },
                Metallic = 0f,
                Smoothness = 0.2f
            };
        }

        /// <summary>
        /// Create a mud layer (low areas with gentle slopes)
        /// </summary>
        public static TerrainLayer CreateMudLayer()
        {
            return new TerrainLayer
            {
                Name = "Mud",
                SlopeMinDeg = 0f,
                SlopeMaxDeg = 10f,
                SlopeBlendDistance = 3f,
                HeightMin = -10f,
                HeightMax = 5f,
                HeightBlendDistance = 5f,
                Strength = 1f,
                Priority = 2,
                BlendMode = TerrainLayerBlendMode.HeightAndSlope,
                Tiling = new float[] { 6f, 6f },
                Offset = new float[] { 0f, 0f },
                Metallic = 0f,
                Smoothness = 0.1f
            };
        }

        /// <summary>
        /// Create a base terrain layer that covers everything (fallback)
        /// </summary>
        public static TerrainLayer CreateBaseLayer()
        {
            return new TerrainLayer
            {
                Name = "Base Terrain",
                SlopeMinDeg = 0f,
                SlopeMaxDeg = 90f,
                SlopeBlendDistance = 0f,
                HeightMin = -10000f,
                HeightMax = 10000f,
                HeightBlendDistance = 0f,
                Strength = 0.5f,
                Priority = -1,
                BlendMode = TerrainLayerBlendMode.HeightOrSlope,
                Tiling = new float[] { 20f, 20f },
                Offset = new float[] { 0f, 0f },
                Metallic = 0f,
                Smoothness = 0.5f
            };
        }
    }
}

#pragma warning restore CS0618
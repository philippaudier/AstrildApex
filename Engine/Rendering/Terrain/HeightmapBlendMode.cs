namespace Engine.Rendering.Terrain
{
    /// <summary>
    /// Blend modes for combining procedural and texture heightmaps
    /// </summary>
    public enum HeightmapBlendMode
    {
        /// <summary>Replace - use only procedural (no blending)</summary>
        Replace = 0,

        /// <summary>Add - adds procedural to texture</summary>
        Add = 1,

        /// <summary>Multiply - multiplies texture by procedural</summary>
        Multiply = 2,

        /// <summary>Overlay - photoshop-style overlay blend</summary>
        Overlay = 3,

        /// <summary>Screen - brightens, opposite of multiply</summary>
        Screen = 4,

        /// <summary>Min - takes minimum of both</summary>
        Min = 5,

        /// <summary>Max - takes maximum of both</summary>
        Max = 6,

        /// <summary>Average - averages both heightmaps</summary>
        Average = 7
    }

    /// <summary>
    /// Utility methods for blending heightmaps
    /// </summary>
    public static class HeightmapBlending
    {
        /// <summary>
        /// Blend two heightmaps using specified mode and strength
        /// </summary>
        public static float[,] Blend(float[,] baseMap, float[,] blendMap, HeightmapBlendMode mode, float strength)
        {
            int width = baseMap.GetLength(0);
            int height = baseMap.GetLength(1);

            // Resize blend map if dimensions don't match
            if (blendMap.GetLength(0) != width || blendMap.GetLength(1) != height)
            {
                blendMap = ResizeHeightmap(blendMap, width, height);
            }

            float[,] result = new float[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float baseValue = baseMap[x, y];
                    float blendValue = blendMap[x, y];

                    float blended = mode switch
                    {
                        HeightmapBlendMode.Replace => blendValue,
                        HeightmapBlendMode.Add => baseValue + blendValue,
                        HeightmapBlendMode.Multiply => baseValue * blendValue,
                        HeightmapBlendMode.Overlay => Overlay(baseValue, blendValue),
                        HeightmapBlendMode.Screen => Screen(baseValue, blendValue),
                        HeightmapBlendMode.Min => Math.Min(baseValue, blendValue),
                        HeightmapBlendMode.Max => Math.Max(baseValue, blendValue),
                        HeightmapBlendMode.Average => (baseValue + blendValue) * 0.5f,
                        _ => baseValue
                    };

                    // Lerp based on strength
                    result[x, y] = baseValue + (blended - baseValue) * strength;
                }
            }

            return result;
        }

        private static float Overlay(float a, float b)
        {
            return a < 0.5f
                ? 2f * a * b
                : 1f - 2f * (1f - a) * (1f - b);
        }

        private static float Screen(float a, float b)
        {
            return 1f - (1f - a) * (1f - b);
        }

        /// <summary>
        /// Resize heightmap using bilinear interpolation
        /// </summary>
        private static float[,] ResizeHeightmap(float[,] source, int newWidth, int newHeight)
        {
            int srcWidth = source.GetLength(0);
            int srcHeight = source.GetLength(1);

            float[,] result = new float[newWidth, newHeight];

            for (int y = 0; y < newHeight; y++)
            {
                for (int x = 0; x < newWidth; x++)
                {
                    float u = (float)x / (newWidth - 1);
                    float v = (float)y / (newHeight - 1);

                    float sx = u * (srcWidth - 1);
                    float sy = v * (srcHeight - 1);

                    int x0 = (int)Math.Floor(sx);
                    int y0 = (int)Math.Floor(sy);
                    int x1 = Math.Min(x0 + 1, srcWidth - 1);
                    int y1 = Math.Min(y0 + 1, srcHeight - 1);

                    float fx = sx - x0;
                    float fy = sy - y0;

                    float h00 = source[x0, y0];
                    float h10 = source[x1, y0];
                    float h01 = source[x0, y1];
                    float h11 = source[x1, y1];

                    float h0 = h00 * (1f - fx) + h10 * fx;
                    float h1 = h01 * (1f - fx) + h11 * fx;

                    result[x, y] = h0 * (1f - fy) + h1 * fy;
                }
            }

            return result;
        }
    }
}

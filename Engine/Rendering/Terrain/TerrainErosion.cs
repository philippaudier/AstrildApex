using System;

namespace Engine.Rendering.Terrain
{
    /// <summary>
    /// Terrain erosion algorithms for realistic weathering effects
    /// </summary>
    public static class TerrainErosion
    {
        /// <summary>
        /// Apply hydraulic erosion (water flow simulation)
        /// Simulates rain eroding terrain, creating valleys and sediment deposits
        /// </summary>
        public static void ApplyHydraulicErosion(float[,] heightmap, int iterations, float erosionStrength, float sedimentCapacity, float evaporationRate)
        {
            int width = heightmap.GetLength(0);
            int height = heightmap.GetLength(1);

            Random rand = new Random();

            for (int iter = 0; iter < iterations; iter++)
            {
                // Simulate a water droplet
                int x = rand.Next(width);
                int y = rand.Next(height);

                float waterAmount = 1.0f;
                float sediment = 0.0f;
                float velocity = 1.0f;

                // Follow water droplet path
                for (int step = 0; step < 64; step++)
                {
                    if (x < 1 || x >= width - 1 || y < 1 || y >= height - 1)
                        break;

                    // Find direction of steepest descent
                    float currentHeight = heightmap[x, y];
                    float minHeight = currentHeight;
                    int minX = x;
                    int minY = y;

                    // Check 8 neighbors
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;

                            int nx = x + dx;
                            int ny = y + dy;

                            if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                            {
                                float neighborHeight = heightmap[nx, ny];
                                if (neighborHeight < minHeight)
                                {
                                    minHeight = neighborHeight;
                                    minX = nx;
                                    minY = ny;
                                }
                            }
                        }
                    }

                    // If no lower neighbor, deposit sediment and stop
                    if (minX == x && minY == y)
                    {
                        heightmap[x, y] += sediment * erosionStrength;
                        break;
                    }

                    // Calculate height difference
                    float heightDiff = currentHeight - minHeight;

                    // Capacity based on velocity and height difference
                    float capacity = Math.Max(0.01f, heightDiff) * velocity * waterAmount * sedimentCapacity;

                    // Erode or deposit
                    if (sediment > capacity)
                    {
                        // Deposit
                        float deposit = (sediment - capacity) * erosionStrength;
                        heightmap[x, y] += deposit;
                        sediment -= deposit;
                    }
                    else
                    {
                        // Erode
                        float erode = Math.Min((capacity - sediment) * erosionStrength, heightDiff);
                        heightmap[x, y] -= erode;
                        sediment += erode;
                    }

                    // Move to lower neighbor
                    x = minX;
                    y = minY;

                    // Update velocity and water
                    velocity = (float)Math.Sqrt(velocity * velocity + heightDiff);
                    waterAmount *= (1f - evaporationRate);

                    if (waterAmount < 0.01f)
                        break;
                }
            }
        }

        /// <summary>
        /// Apply thermal erosion (talus angle simulation)
        /// Simulates material sliding down slopes, creating realistic cliff faces
        /// </summary>
        public static void ApplyThermalErosion(float[,] heightmap, int iterations, float talusAngle, float erosionStrength)
        {
            int width = heightmap.GetLength(0);
            int height = heightmap.GetLength(1);

            float[,] diff = new float[width, height];

            for (int iter = 0; iter < iterations; iter++)
            {
                // Reset diff buffer
                Array.Clear(diff, 0, diff.Length);

                // Calculate material transfer
                for (int y = 1; y < height - 1; y++)
                {
                    for (int x = 1; x < width - 1; x++)
                    {
                        float currentHeight = heightmap[x, y];
                        float totalDiff = 0f;
                        int neighbors = 0;

                        // Check 4 cardinal neighbors
                        int[][] directions = new int[][] {
                            new int[] { 0, -1 }, // North
                            new int[] { 0,  1 }, // South
                            new int[] { -1, 0 }, // West
                            new int[] {  1, 0 }  // East
                        };

                        foreach (var dir in directions)
                        {
                            int nx = x + dir[0];
                            int ny = y + dir[1];

                            if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                            {
                                float neighborHeight = heightmap[nx, ny];
                                float heightDiff = currentHeight - neighborHeight;

                                // If height difference exceeds talus angle
                                if (heightDiff > talusAngle)
                                {
                                    float excess = (heightDiff - talusAngle) * 0.5f * erosionStrength;
                                    totalDiff += excess;
                                    diff[nx, ny] += excess;
                                    neighbors++;
                                }
                            }
                        }

                        if (neighbors > 0)
                        {
                            diff[x, y] -= totalDiff;
                        }
                    }
                }

                // Apply changes
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        heightmap[x, y] += diff[x, y];
                    }
                }
            }
        }

        /// <summary>
        /// Combined erosion (hydraulic + thermal) for best results
        /// </summary>
        public static void ApplyCombinedErosion(
            float[,] heightmap,
            int hydraulicIterations, float hydraulicStrength,
            int thermalIterations, float thermalTalusAngle, float thermalStrength)
        {
            // First apply thermal erosion to create natural slopes
            if (thermalIterations > 0)
            {
                ApplyThermalErosion(heightmap, thermalIterations, thermalTalusAngle, thermalStrength);
            }

            // Then apply hydraulic erosion to create valleys
            if (hydraulicIterations > 0)
            {
                ApplyHydraulicErosion(heightmap, hydraulicIterations, hydraulicStrength, 4.0f, 0.02f);
            }
        }
    }
}

using System;

namespace Engine.Mathx.Noise
{
    /// <summary>
    /// Simple Voronoi/Worley noise implementation
    /// </summary>
    public class VoronoiNoise : INoise
    {
        private readonly int _seed;

        public VoronoiNoise(int seed = 0)
        {
            _seed = seed;
        }

        public float Sample(float x, float z)
        {
            // Get the integer coordinates of the cell containing point (x,z)
            int xi = FastFloor(x);
            int zi = FastFloor(z);

            float minDist = float.MaxValue;

            // Check 9 neighboring cells (3x3 grid)
            for (int offsetZ = -1; offsetZ <= 1; offsetZ++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    int cellX = xi + offsetX;
                    int cellZ = zi + offsetZ;

                    // Generate a pseudo-random point within this cell
                    var (px, pz) = GetCellPoint(cellX, cellZ);

                    // Calculate distance from input point to this cell's point
                    float dx = x - px;
                    float dz = z - pz;
                    float dist = dx * dx + dz * dz; // squared distance for efficiency

                    if (dist < minDist)
                        minDist = dist;
                }
            }

            // Return the square root and normalize to roughly [-1, 1]
            // Voronoi typically produces values in [0, sqrt(2)] for adjacent cells
            float result = MathF.Sqrt(minDist);
            return Math.Clamp((result - 0.5f) * 2f, -1f, 1f);
        }

        public float SampleFractal(float x, float z, int octaves, float lacunarity, float gain)
        {
            float value = 0f;
            float amplitude = 1f;
            float frequency = 1f;

            for (int i = 0; i < octaves; i++)
            {
                value += Sample(x * frequency, z * frequency) * amplitude;
                frequency *= lacunarity;
                amplitude *= gain;
            }

            return value;
        }

        private (float x, float z) GetCellPoint(int cellX, int cellZ)
        {
            // Generate pseudo-random point within cell [cellX, cellX+1] x [cellZ, cellZ+1]
            uint hash = Hash2D(cellX, cellZ);

            float u = (hash & 0xFFFF) / 65535.0f;
            hash >>= 16;
            float v = (hash & 0xFFFF) / 65535.0f;

            return (cellX + u, cellZ + v);
        }

        private uint Hash2D(int x, int z)
        {
            // Simple 2D hash function
            uint h = (uint)(_seed ^ x ^ (z << 16));
            h = (h ^ (h >> 16)) * 0x21f0aaad;
            h = (h ^ (h >> 15)) * 0x735a2d97;
            h = h ^ (h >> 15);
            return h;
        }

        private static int FastFloor(float x)
        {
            int xi = (int)x;
            return x < xi ? xi - 1 : xi;
        }
    }
}
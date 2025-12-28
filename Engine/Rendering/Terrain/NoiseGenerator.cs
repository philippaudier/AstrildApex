using System;

namespace Engine.Rendering.Terrain
{
    /// <summary>
    /// Noise generation algorithms for procedural terrain.
    /// Implements Simplex Noise for natural-looking results.
    /// </summary>
    public static class NoiseGenerator
    {
        // Simplex noise gradient vectors
        private static readonly int[][] grad3 = new int[][]
        {
            new int[] {1,1,0}, new int[] {-1,1,0}, new int[] {1,-1,0}, new int[] {-1,-1,0},
            new int[] {1,0,1}, new int[] {-1,0,1}, new int[] {1,0,-1}, new int[] {-1,0,-1},
            new int[] {0,1,1}, new int[] {0,-1,1}, new int[] {0,1,-1}, new int[] {0,-1,-1}
        };

        // Permutation table
        private static int[] perm = new int[512];
        private static int[] permMod12 = new int[512];

        static NoiseGenerator()
        {
            InitializePermutation(0);
        }

        /// <summary>
        /// Initialize permutation table with a seed for reproducible noise.
        /// </summary>
        public static void InitializePermutation(int seed)
        {
            var random = new Random(seed);
            int[] p = new int[256];

            // Fill with sequential values
            for (int i = 0; i < 256; i++)
                p[i] = i;

            // Fisher-Yates shuffle
            for (int i = 255; i > 0; i--)
            {
                int j = random.Next(i + 1);
                int temp = p[i];
                p[i] = p[j];
                p[j] = temp;
            }

            // Duplicate for wrapping
            for (int i = 0; i < 512; i++)
            {
                perm[i] = p[i & 255];
                permMod12[i] = perm[i] % 12;
            }
        }

        // Simplex constants (cannot be const due to Math.Sqrt)
        private static readonly float F2 = 0.5f * (float)(Math.Sqrt(3.0) - 1.0);
        private static readonly float G2 = (float)((3.0 - Math.Sqrt(3.0)) / 6.0);

        /// <summary>
        /// 2D Simplex Noise - returns value in range [-1, 1]
        /// </summary>
        public static float SimplexNoise2D(float x, float y)
        {

            // Skew the input space to determine which simplex cell we're in
            float s = (x + y) * F2;
            int i = FastFloor(x + s);
            int j = FastFloor(y + s);

            float t = (i + j) * G2;
            float X0 = i - t;
            float Y0 = j - t;
            float x0 = x - X0;
            float y0 = y - Y0;

            // Determine which simplex we are in
            int i1, j1;
            if (x0 > y0) { i1 = 1; j1 = 0; }
            else { i1 = 0; j1 = 1; }

            // Offsets for second corner
            float x1 = x0 - i1 + G2;
            float y1 = y0 - j1 + G2;
            // Offsets for third corner
            float x2 = x0 - 1.0f + 2.0f * G2;
            float y2 = y0 - 1.0f + 2.0f * G2;

            // Hash coordinates of the three corners
            int ii = i & 255;
            int jj = j & 255;
            int gi0 = permMod12[ii + perm[jj]];
            int gi1 = permMod12[ii + i1 + perm[jj + j1]];
            int gi2 = permMod12[ii + 1 + perm[jj + 1]];

            // Calculate contributions from three corners
            float n0, n1, n2;

            float t0 = 0.5f - x0 * x0 - y0 * y0;
            if (t0 < 0) n0 = 0.0f;
            else
            {
                t0 *= t0;
                n0 = t0 * t0 * Dot(grad3[gi0], x0, y0);
            }

            float t1 = 0.5f - x1 * x1 - y1 * y1;
            if (t1 < 0) n1 = 0.0f;
            else
            {
                t1 *= t1;
                n1 = t1 * t1 * Dot(grad3[gi1], x1, y1);
            }

            float t2 = 0.5f - x2 * x2 - y2 * y2;
            if (t2 < 0) n2 = 0.0f;
            else
            {
                t2 *= t2;
                n2 = t2 * t2 * Dot(grad3[gi2], x2, y2);
            }

            // Sum contributions and scale to [-1, 1]
            return 70.0f * (n0 + n1 + n2);
        }

        /// <summary>
        /// Fractional Brownian Motion (fBm) - multi-octave noise for natural terrain.
        /// Returns value in range [0, 1].
        /// </summary>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        /// <param name="octaves">Number of noise layers (more = more detail)</param>
        /// <param name="persistence">Amplitude falloff per octave (typically 0.5)</param>
        /// <param name="lacunarity">Frequency multiplier per octave (typically 2.0)</param>
        public static float FractalNoise2D(float x, float y, int octaves, float persistence, float lacunarity)
        {
            float total = 0f;
            float amplitude = 1f;
            float frequency = 1f;
            float maxValue = 0f;  // Used for normalizing result to 0-1

            for (int i = 0; i < octaves; i++)
            {
                total += SimplexNoise2D(x * frequency, y * frequency) * amplitude;

                maxValue += amplitude;

                amplitude *= persistence;
                frequency *= lacunarity;
            }

            // Normalize to [0, 1]
            return (total / maxValue) * 0.5f + 0.5f;
        }

        /// <summary>
        /// Ridged noise - inverted abs(noise) for mountain ridges.
        /// Returns value in range [0, 1].
        /// </summary>
        public static float RidgedNoise2D(float x, float y, int octaves, float persistence, float lacunarity)
        {
            float total = 0f;
            float amplitude = 1f;
            float frequency = 1f;
            float maxValue = 0f;

            for (int i = 0; i < octaves; i++)
            {
                float signal = SimplexNoise2D(x * frequency, y * frequency);
                signal = 1f - Math.Abs(signal);  // Create ridges
                signal *= signal;  // Sharpen

                total += signal * amplitude;
                maxValue += amplitude;

                amplitude *= persistence;
                frequency *= lacunarity;
            }

            return total / maxValue;
        }

        /// <summary>
        /// Billow noise - abs(noise) for cloud-like formations.
        /// Returns value in range [0, 1].
        /// </summary>
        public static float BillowNoise2D(float x, float y, int octaves, float persistence, float lacunarity)
        {
            float total = 0f;
            float amplitude = 1f;
            float frequency = 1f;
            float maxValue = 0f;

            for (int i = 0; i < octaves; i++)
            {
                float signal = Math.Abs(SimplexNoise2D(x * frequency, y * frequency));

                total += signal * amplitude;
                maxValue += amplitude;

                amplitude *= persistence;
                frequency *= lacunarity;
            }

            return total / maxValue;
        }

        private static int FastFloor(float x)
        {
            int xi = (int)x;
            return x < xi ? xi - 1 : xi;
        }

        private static float Dot(int[] g, float x, float y)
        {
            return g[0] * x + g[1] * y;
        }
    }
}

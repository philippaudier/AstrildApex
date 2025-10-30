using System;

namespace Engine.Mathx.Noise
{
    /// <summary>
    /// Simple 2D Simplex noise implementation
    /// </summary>
    public class SimplexNoise : INoise
    {
        private readonly int[] _perm = new int[512];
        private static readonly float F2 = 0.5f * (MathF.Sqrt(3.0f) - 1.0f);
        private static readonly float G2 = (3.0f - MathF.Sqrt(3.0f)) / 6.0f;

        public SimplexNoise(int seed = 0)
        {
            // Build permutation table from seed
            var rnd = new Random(seed);
            int[] p = new int[256];
            for (int i = 0; i < 256; i++) p[i] = i;

            // Fisher-Yates shuffle
            for (int i = 255; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                (p[i], p[j]) = (p[j], p[i]);
            }

            // Duplicate for faster indexing
            for (int i = 0; i < 512; i++) _perm[i] = p[i & 255];
        }

        public float Sample(float x, float z)
        {
            float n0, n1, n2; // Noise contributions from the three corners

            // Skew the input space to determine which simplex cell we're in
            float s = (x + z) * F2;
            int i = FastFloor(x + s);
            int j = FastFloor(z + s);

            float t = (i + j) * G2;
            float X0 = i - t; // Unskew the cell origin back to (x,z) space
            float Y0 = j - t;
            float x0 = x - X0; // The x,z distances from the cell origin
            float y0 = z - Y0;

            // Determine which simplex we are in
            int i1, j1; // Offsets for second (middle) corner of simplex
            if (x0 > y0) { i1 = 1; j1 = 0; } // lower triangle, XY order: (0,0)->(1,0)->(1,1)
            else { i1 = 0; j1 = 1; } // upper triangle, YX order: (0,0)->(0,1)->(1,1)

            // A step of (1,0) in (i,j) means a step of (1-c,-c) in (x,y), and
            // a step of (0,1) in (i,j) means a step of (-c,1-c) in (x,y), where c = (3-sqrt(3))/6
            float x1 = x0 - i1 + G2; // Offsets for middle corner
            float y1 = y0 - j1 + G2;
            float x2 = x0 - 1.0f + 2.0f * G2; // Offsets for last corner
            float y2 = y0 - 1.0f + 2.0f * G2;

            // Work out the hashed gradient indices of the three simplex corners
            int ii = i & 255;
            int jj = j & 255;
            int gi0 = _perm[ii + _perm[jj]] % 12;
            int gi1 = _perm[ii + i1 + _perm[jj + j1]] % 12;
            int gi2 = _perm[ii + 1 + _perm[jj + 1]] % 12;

            // Calculate the contribution from the three corners
            float t0 = 0.5f - x0 * x0 - y0 * y0;
            if (t0 < 0) n0 = 0.0f;
            else
            {
                t0 *= t0;
                n0 = t0 * t0 * Dot(Grad3[gi0], x0, y0);
            }

            float t1 = 0.5f - x1 * x1 - y1 * y1;
            if (t1 < 0) n1 = 0.0f;
            else
            {
                t1 *= t1;
                n1 = t1 * t1 * Dot(Grad3[gi1], x1, y1);
            }

            float t2 = 0.5f - x2 * x2 - y2 * y2;
            if (t2 < 0) n2 = 0.0f;
            else
            {
                t2 *= t2;
                n2 = t2 * t2 * Dot(Grad3[gi2], x2, y2);
            }

            // Add contributions from each corner to get the final noise value.
            // The result is scaled to return values in the interval [-1,1].
            return 70.0f * (n0 + n1 + n2);
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

        private static int FastFloor(float x)
        {
            int xi = (int)x;
            return x < xi ? xi - 1 : xi;
        }

        private static float Dot(int[] g, float x, float y)
        {
            return g[0] * x + g[1] * y;
        }

        private static readonly int[][] Grad3 = new int[][]
        {
            new int[]{1,1,0}, new int[]{-1,1,0}, new int[]{1,-1,0}, new int[]{-1,-1,0},
            new int[]{1,0,1}, new int[]{-1,0,1}, new int[]{1,0,-1}, new int[]{-1,0,-1},
            new int[]{0,1,1}, new int[]{0,-1,1}, new int[]{0,1,-1}, new int[]{0,-1,-1}
        };
    }
}
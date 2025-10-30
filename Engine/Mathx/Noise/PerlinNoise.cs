using System;

namespace Engine.Mathx.Noise
{
    /// <summary>
    /// Simple Perlin noise implementation (placeholder)
    /// TODO: replace with optimized, tested implementation
    /// </summary>
    public class PerlinNoise : INoise
    {
        private readonly int[] _perm = new int[512];

        public PerlinNoise(int seed = 0)
        {
            // Build a permutation table from seed for determinism
            var rnd = new Random(seed);
            int[] p = new int[256];
            for (int i = 0; i < 256; i++) p[i] = i;
            // Fisher-Yates shuffle
            for (int i = 255; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                int tmp = p[i]; p[i] = p[j]; p[j] = tmp;
            }
            for (int i = 0; i < 512; i++) _perm[i] = p[i & 255];
        }

        private static float Fade(float t)
        {
            // 6t^5 - 15t^4 + 10t^3
            return t * t * t * (t * (t * 6 - 15) + 10);
        }

        private static float Lerp(float a, float b, float t) => a + t * (b - a);

        private static float Grad(int hash, float x, float y)
        {
            // Convert low 4 bits of hash code into 12 gradient directions
            int h = hash & 7; // 8 directions
            float u = h < 4 ? x : y;
            float v = h < 4 ? y : x;
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }

        public float Sample(float x, float z)
        {
            // Classic Perlin noise in 2D
            int xi = FastFloor(x) & 255;
            int yi = FastFloor(z) & 255;
            float xf = x - (float)Math.Floor(x);
            float yf = z - (float)Math.Floor(z);

            float u = Fade(xf);
            float v = Fade(yf);

            int aa = _perm[_perm[xi] + yi];
            int ab = _perm[_perm[xi] + yi + 1];
            int ba = _perm[_perm[xi + 1] + yi];
            int bb = _perm[_perm[xi + 1] + yi + 1];

            float x1 = Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1, yf), u);
            float x2 = Lerp(Grad(ab, xf, yf - 1), Grad(bb, xf - 1, yf - 1), u);
            float value = Lerp(x1, x2, v);
            // Perlin produces approximately in range [-1,1]
            return value;
        }

        private static int FastFloor(float x) => (int)(x >= 0 ? x : x - 1);

        public float SampleFractal(float x, float z, int octaves, float lacunarity, float gain)
        {
            float amplitude = 1.0f;
            float frequency = 1.0f;
            float sum = 0f;
            float max = 0f;
            for (int i = 0; i < Math.Max(1, octaves); i++)
            {
                sum += Sample(x * frequency, z * frequency) * amplitude;
                max += amplitude;
                amplitude *= gain;
                frequency *= lacunarity;
            }
            return sum / Math.Max(1e-6f, max);
        }
    }
}

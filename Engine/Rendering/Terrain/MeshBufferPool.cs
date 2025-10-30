using System;
using System.Collections.Concurrent;

namespace Engine.Rendering.Terrain
{
    /// <summary>
    /// Optimized pool for reusing mesh buffers to reduce GC pressure during terrain streaming.
    /// Uses size buckets for better memory efficiency and reduced fragmentation.
    /// </summary>
    public static class MeshBufferPool
    {
        // Size-bucketed pools for better memory management
        private static readonly ConcurrentBag<float[]> _floatPoolSmall = new(); // < 10KB
        private static readonly ConcurrentBag<float[]> _floatPoolMedium = new(); // 10-100KB
        private static readonly ConcurrentBag<float[]> _floatPoolLarge = new(); // > 100KB

        private static readonly ConcurrentBag<int[]> _intPoolSmall = new();
        private static readonly ConcurrentBag<int[]> _intPoolMedium = new();
        private static readonly ConcurrentBag<int[]> _intPoolLarge = new();

        private const int SmallThreshold = 2500;  // ~10KB for floats
        private const int LargeThreshold = 25000; // ~100KB for floats

        public static float[] RentFloat(int minSize)
        {
            // Choose appropriate pool based on size
            var pool = minSize <= SmallThreshold ? _floatPoolSmall :
                      minSize <= LargeThreshold ? _floatPoolMedium : _floatPoolLarge;

            // Try to find a suitable buffer (allow some size variance to reduce fragmentation)
            var temp = new System.Collections.Generic.List<float[]>();
            while (pool.TryTake(out var buf))
            {
                // Accept buffers within 25% size variance to reduce fragmentation
                if (buf.Length >= minSize && buf.Length <= minSize * 1.25f)
                {
                    // Return any temporaries back to pool
                    foreach (var t in temp) ReturnFloat(t);
                    return buf;
                }
                temp.Add(buf);
            }
            // Restore temporaries
            foreach (var t in temp) ReturnFloat(t);
            return new float[minSize];
        }

        public static void ReturnFloat(float[] buf)
        {
            if (buf == null) return;

            // Return to appropriate size bucket
            var pool = buf.Length <= SmallThreshold ? _floatPoolSmall :
                      buf.Length <= LargeThreshold ? _floatPoolMedium : _floatPoolLarge;
            pool.Add(buf);
        }

        // Convenience overload used when returning arrays from MeshData (may be exact sized)
        public static void ReturnFloat(float[]? buf, bool allowNull = true)
        {
            if (buf == null) return;
            ReturnFloat(buf);
        }

        public static int[] RentInt(int minSize)
        {
            // Choose appropriate pool based on size
            var pool = minSize <= SmallThreshold ? _intPoolSmall :
                      minSize <= LargeThreshold ? _intPoolMedium : _intPoolLarge;

            var temp = new System.Collections.Generic.List<int[]>();
            while (pool.TryTake(out var buf))
            {
                // Accept buffers within 25% size variance
                if (buf.Length >= minSize && buf.Length <= minSize * 1.25f)
                {
                    foreach (var t in temp) ReturnInt(t);
                    return buf;
                }
                temp.Add(buf);
            }
            foreach (var t in temp) ReturnInt(t);
            return new int[minSize];
        }

        public static void ReturnInt(int[] buf)
        {
            if (buf == null) return;

            // Return to appropriate size bucket
            var pool = buf.Length <= SmallThreshold ? _intPoolSmall :
                      buf.Length <= LargeThreshold ? _intPoolMedium : _intPoolLarge;
            pool.Add(buf);
        }

        public static void ReturnInt(int[]? buf, bool allowNull = true)
        {
            if (buf == null) return;
            ReturnInt(buf);
        }
    }
}

using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Engine.Rendering.Terrain.Tile
{
    /// <summary>
    /// CPU generator for terrain tiles with caching support.
    /// Generates vertices (pos, normal, uv) and indices for infinite streaming.
    /// </summary>
    public static class TileCpuGenerator
    {
        // Base tile resolution for LOD0 (vertices per side)
        public const int DefaultBaseResolution = 129;

        /// <summary>
        /// Generate tile with on-disk caching.
        /// </summary>
        public static (float[] vertices, uint[] indices) GenerateCachedTile(Engine.Components.Terrain terrain, int tileX, int tileY, int lod)
        {
            try
            {
                string cacheDir = Path.Combine("Cache", "Terrain", "tiles");
                Directory.CreateDirectory(cacheDir);

                string key = BuildTileCacheKey(terrain, tileX, tileY, lod);
                string cachePath = Path.Combine(cacheDir, $"tile_{key}.cache");

                // Try load from cache
                if (File.Exists(cachePath))
                {
                    try
                    {
                        using var fs = File.OpenRead(cachePath);
                        using var gz = new GZipStream(fs, CompressionMode.Decompress);
                        using var br = new BinaryReader(gz);

                        int magic = br.ReadInt32();
                        if (magic == 0x54544C45) // 'TTLE'
                        {
                            int version = br.ReadInt32();
                            if (version == 1)
                            {
                                int vCount = br.ReadInt32();
                                float[] verts = new float[vCount];
                                for (int i = 0; i < vCount; i++) verts[i] = br.ReadSingle();

                                int iCount = br.ReadInt32();
                                uint[] inds = new uint[iCount];
                                for (int i = 0; i < iCount; i++) inds[i] = br.ReadUInt32();

                                return (verts, inds);
                            }
                        }
                    }
                    catch { /* Cache corrupted, regenerate */ }
                }

                // Generate new tile
                var result = GenerateTileCpu(terrain, tileX, tileY, lod);

                // Save to cache
                try
                {
                    using var fsw = File.Create(cachePath + ".tmp");
                    using var gzw = new GZipStream(fsw, CompressionLevel.Optimal);
                    using var bw = new BinaryWriter(gzw);
                    bw.Write(0x54544C45); // 'TTLE'
                    bw.Write(1); // version
                    bw.Write(result.vertices.Length);
                    foreach (var f in result.vertices) bw.Write(f);
                    bw.Write(result.indices.Length);
                    foreach (var u in result.indices) bw.Write(u);

                    if (File.Exists(cachePath)) File.Delete(cachePath);
                    File.Move(cachePath + ".tmp", cachePath);
                }
                catch { /* Non-fatal: cache write failed */ }

                return result;
            }
            catch (Exception ex)
            {
                Engine.Utils.DebugLogger.Log($"[TileCpuGenerator] Failed to generate tile: {ex.Message}");
                return (Array.Empty<float>(), Array.Empty<uint>());
            }
        }

        /// <summary>
        /// Build cache key from terrain parameters.
        /// </summary>
        private static string BuildTileCacheKey(Engine.Components.Terrain terrain, int tileX, int tileY, int lod)
        {
            // Hash terrain parameters that affect geometry
            string terrainKey = $"w{terrain.TerrainWidth}_l{terrain.TerrainLength}_h{terrain.TerrainHeight}_" +
                               $"proc{terrain.UseProceduralGeneration}_seed{terrain.ProceduralSeed}_" +
                               $"scale{terrain.NoiseScale}_oct{terrain.Octaves}_" +
                               $"hm{terrain.HeightmapTextureGuid}_{terrain.BlendWithTexture}";

            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(terrainKey));
            var hex = BitConverter.ToString(bytes).Replace("-", "").Substring(0, 16);

            return $"{tileX}_{tileY}_lod{lod}_{hex}";
        }

        /// <summary>
        /// Generate tile CPU buffers (vertices + indices).
        /// </summary>
        private static (float[] vertices, uint[] indices) GenerateTileCpu(Engine.Components.Terrain terrain, int tileX, int tileY, int lod)
        {
            // Calculate resolution for this LOD
            int baseRes = DefaultBaseResolution;
            int res = Math.Max(2, baseRes >> lod);  // LOD0=129, LOD1=65, LOD2=33, LOD3=17

            int vertexCount = res * res;
            float[] vertices = new float[vertexCount * 8];  // pos(3) + normal(3) + uv(2)
            uint[] indices = new uint[(res - 1) * (res - 1) * 6];

            // Calculate world-space bounds for this tile
            float tileWorldSize = terrain.StreamingTileSize;
            if (tileWorldSize <= 0) tileWorldSize = 100f;

            float startX = tileX * tileWorldSize;
            float startZ = tileY * tileWorldSize;
            float stepX = tileWorldSize / (res - 1);
            float stepZ = tileWorldSize / (res - 1);

            // Generate vertices
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    int idx = (z * res + x) * 8;

                    float u = x / (float)(res - 1);
                    float v = z / (float)(res - 1);

                    float worldX = startX + x * stepX;
                    float worldZ = startZ + z * stepZ;

                    // Sample height using infinite terrain generation
                    float height = SampleHeightInfinite(terrain, worldX, worldZ);

                    // Position
                    vertices[idx + 0] = worldX;
                    vertices[idx + 1] = height;
                    vertices[idx + 2] = worldZ;

                    // Normal (computed later)
                    vertices[idx + 3] = 0f;
                    vertices[idx + 4] = 1f;
                    vertices[idx + 5] = 0f;

                    // UVs (0-1 range across tile)
                    vertices[idx + 6] = u;
                    vertices[idx + 7] = v;
                }
            }

            // Generate indices (CCW winding)
            int ii = 0;
            for (int z = 0; z < res - 1; z++)
            {
                for (int x = 0; x < res - 1; x++)
                {
                    uint topLeft = (uint)(z * res + x);
                    uint topRight = (uint)(z * res + x + 1);
                    uint bottomLeft = (uint)((z + 1) * res + x);
                    uint bottomRight = (uint)((z + 1) * res + x + 1);

                    // Triangle 1
                    indices[ii++] = topLeft;
                    indices[ii++] = bottomLeft;
                    indices[ii++] = topRight;

                    // Triangle 2
                    indices[ii++] = topRight;
                    indices[ii++] = bottomLeft;
                    indices[ii++] = bottomRight;
                }
            }

            // Compute normals from neighboring heights
            ComputeNormals(vertices, res, terrain, startX, startZ, stepX, stepZ);

            return (vertices, indices);
        }

        /// <summary>
        /// Compute smooth normals using finite differences.
        /// </summary>
        private static void ComputeNormals(float[] vertices, int res, Engine.Components.Terrain terrain, float startX, float startZ, float stepX, float stepZ)
        {
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    int idx = (z * res + x) * 8;
                    float worldX = startX + x * stepX;
                    float worldZ = startZ + z * stepZ;

                    // Sample neighboring heights
                    float hL = SampleSafe(terrain, worldX - stepX, worldZ);
                    float hR = SampleSafe(terrain, worldX + stepX, worldZ);
                    float hD = SampleSafe(terrain, worldX, worldZ - stepZ);
                    float hU = SampleSafe(terrain, worldX, worldZ + stepZ);

                    // Finite differences
                    float dHeightDx = (hR - hL) / (2.0f * stepX);
                    float dHeightDz = (hU - hD) / (2.0f * stepZ);

                    // Normal = (-dh/dx, 1, -dh/dz) normalized
                    float nx = -dHeightDx;
                    float ny = 1.0f;
                    float nz = -dHeightDz;
                    float invLen = 1.0f / (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);

                    vertices[idx + 3] = nx * invLen;
                    vertices[idx + 4] = ny * invLen;
                    vertices[idx + 5] = nz * invLen;
                }
            }
        }

        /// <summary>
        /// Safe height sampling with fallback.
        /// </summary>
        private static float SampleSafe(Engine.Components.Terrain terrain, float worldX, float worldZ)
        {
            try { return SampleHeightInfinite(terrain, worldX, worldZ); }
            catch { return 0f; }
        }

        /// <summary>
        /// Sample height at world position for infinite terrain.
        /// Handles procedural generation, heightmap tiling, and blending.
        /// </summary>
        public static float SampleHeightInfinite(Engine.Components.Terrain terrain, float worldX, float worldZ)
        {
            if (terrain == null)
            {
                return 0f;
            }

            // === INFINITE STREAMING MODE: PROCEDURAL ONLY ===
            // Heightmap blending is disabled for infinite terrain to ensure seamless tiles
            if (terrain.UseProceduralGeneration)
            {
                return GenerateProceduralHeight(terrain, worldX, worldZ);
            }

            // Fallback: flat terrain if no procedural generation
            return 0f;
        }

        /// <summary>
        /// Generate procedural height using noise (seamless, infinite).
        /// Uses Perlin-style noise for infinite terrain.
        /// </summary>
        private static float GenerateProceduralHeight(Engine.Components.Terrain terrain, float worldX, float worldZ)
        {
            // Apply offsets to world coordinates
            worldX += terrain.NoiseOffsetX;
            worldZ += terrain.NoiseOffsetY;

            try
            {
                // Scale coordinates by noise scale
                float x = worldX / terrain.NoiseScale;
                float z = worldZ / terrain.NoiseScale;

                // Multi-octave noise (Fractal Brownian Motion)
                float amplitude = 1f;
                float frequency = 1f;
                float noiseValue = 0f;
                float maxValue = 0f;

                for (int i = 0; i < terrain.Octaves; i++)
                {
                    float sampleX = x * frequency;
                    float sampleZ = z * frequency;

                    float sample = PerlinNoise2D(sampleX, sampleZ, terrain.ProceduralSeed);

                    noiseValue += sample * amplitude;
                    maxValue += amplitude;

                    amplitude *= terrain.Persistence;
                    frequency *= terrain.Lacunarity;
                }

                // Normalize to [0, 1]
                noiseValue = (noiseValue / maxValue + 1f) * 0.5f;

                // Apply height power curve
                noiseValue = (float)Math.Pow(noiseValue, terrain.HeightPower);

                // Apply height multiplier and terrain height
                return noiseValue * terrain.HeightMultiplier * terrain.TerrainHeight;
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// 2D Perlin noise implementation for seamless infinite terrain.
        /// </summary>
        private static float PerlinNoise2D(float x, float z, int seed)
        {
            // Grid cell coordinates
            int x0 = (int)Math.Floor(x);
            int z0 = (int)Math.Floor(z);
            int x1 = x0 + 1;
            int z1 = z0 + 1;

            // Fractional positions within cell
            float fx = x - x0;
            float fz = z - z0;

            // Smoothstep interpolation (smoother than linear)
            float u = Fade(fx);
            float v = Fade(fz);

            // Gradient vectors at corners (with seed integration)
            float g00 = GradientDot2D(Hash2D(x0, z0, seed), fx, fz);
            float g10 = GradientDot2D(Hash2D(x1, z0, seed), fx - 1f, fz);
            float g01 = GradientDot2D(Hash2D(x0, z1, seed), fx, fz - 1f);
            float g11 = GradientDot2D(Hash2D(x1, z1, seed), fx - 1f, fz - 1f);

            // Bilinear interpolation
            float x0Interp = Lerp(g00, g10, u);
            float x1Interp = Lerp(g01, g11, u);
            return Lerp(x0Interp, x1Interp, v);
        }

        /// <summary>
        /// Hash function to generate deterministic pseudo-random values from 2D coordinates + seed.
        /// </summary>
        private static int Hash2D(int x, int z, int seed)
        {
            // Integrate seed first
            int hash = seed;
            hash = hash * 374761393 + x; // Large prime multiplication
            hash = (hash ^ 61) ^ (hash >> 16);
            hash = hash + (hash << 3);
            hash = hash ^ z;
            hash = hash * 668265263; // Another large prime
            hash = hash + (hash << 10);
            hash = hash ^ (hash >> 6);
            hash = hash + (hash << 15);
            hash = hash ^ (hash >> 11);
            return hash;
        }

        /// <summary>
        /// Compute gradient dot product for Perlin noise.
        /// </summary>
        private static float GradientDot2D(int hash, float x, float z)
        {
            // Use hash to select one of 8 gradient directions
            switch (hash & 7)
            {
                case 0: return x + z;
                case 1: return -x + z;
                case 2: return x - z;
                case 3: return -x - z;
                case 4: return x;
                case 5: return -x;
                case 6: return z;
                case 7: return -z;
                default: return 0;
            }
        }

        /// <summary>
        /// Smooth fade curve (6t^5 - 15t^4 + 10t^3) for better interpolation.
        /// </summary>
        private static float Fade(float t)
        {
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        /// <summary>
        /// Sample heightmap with tiling for infinite terrain.
        /// </summary>
        private static float SampleHeightmapInfinite(Engine.Components.Terrain terrain, float worldX, float worldZ)
        {
            // Tile the heightmap texture across infinite terrain
            float tileSize = terrain.StreamingTileSize;
            if (tileSize <= 0) tileSize = 100f;

            // Wrap to [0, tileSize]
            float localX = ((worldX % tileSize) + tileSize) % tileSize;
            float localZ = ((worldZ % tileSize) + tileSize) % tileSize;

            // Convert to UV [0, 1]
            float u = localX / tileSize;
            float v = localZ / tileSize;

            // Sample terrain's heightmap (assumes _heightData is loaded)
            try
            {
                float terrainLocalX = u * terrain.TerrainWidth - terrain.TerrainWidth * 0.5f;
                float terrainLocalZ = v * terrain.TerrainLength - terrain.TerrainLength * 0.5f;
                return terrain.SampleHeight(terrainLocalX, terrainLocalZ);
            }
            catch
            {
                return 0f;
            }
        }
    }
}

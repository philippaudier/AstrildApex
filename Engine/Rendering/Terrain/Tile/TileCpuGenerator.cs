using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Linq;

namespace Engine.Rendering.Terrain.Tile
{
    /// <summary>
    /// CPU generator for terrain tiles with caching support.
    /// Generates vertices (pos, normal, uv) and indices for infinite streaming.
    /// </summary>
    public static class TileCpuGenerator
    {
        // Fallback base tile resolution for LOD0 (vertices per side)
        // NOTE: Actual resolution is taken from Terrain.MeshResolution if set
        public const int DefaultBaseResolution = 129;

        /// <summary>
        /// Generate tile with on-disk caching.
        /// Skirts are used to hide LOD seams, so neighbor LODs don't affect geometry.
        /// </summary>
        public static (float[] vertices, uint[] indices) GenerateCachedTile(Engine.Components.Terrain terrain, int tileX, int tileY, int lod, System.Collections.Generic.Dictionary<(int x, int y), int>? neighborLods = null)
        {
            try
            {
                string cacheDir = Path.Combine("Cache", "Terrain", "tiles");
                Directory.CreateDirectory(cacheDir);

                // Cache key based on tile position, LOD, and terrain params (not neighbors - skirts hide seams)
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

                // Generate new tile (pass neighbor info for stitching-aware generation)
                var result = GenerateTileCpu(terrain, tileX, tileY, lod, neighborLods);

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
            // Hash terrain parameters that affect geometry (including ClosedMesh settings and MeshResolution)
            string terrainKey = $"w{terrain.TerrainWidth}_l{terrain.TerrainLength}_h{terrain.TerrainHeight}_" +
                               $"proc{terrain.UseProceduralGeneration}_seed{terrain.ProceduralSeed}_" +
                               $"scale{terrain.NoiseScale}_oct{terrain.Octaves}_" +
                               $"hm{terrain.HeightmapTextureGuid}_{terrain.BlendWithTexture}_" +
                               $"closed{terrain.ClosedMesh}_skirt{terrain.SkirtDepth}_" +
                               $"res{terrain.MeshResolution}";

            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(terrainKey));
            var hex = BitConverter.ToString(bytes).Replace("-", "").Substring(0, 16);

            return $"{tileX}_{tileY}_lod{lod}_{hex}";
        }

        /// <summary>
        /// Generate tile CPU buffers (vertices + indices).
        /// Uses vertical skirts to hide seams between different LOD levels.
        /// </summary>
        private static (float[] vertices, uint[] indices) GenerateTileCpu(Engine.Components.Terrain terrain, int tileX, int tileY, int lod, System.Collections.Generic.Dictionary<(int x, int y), int>? neighborLods = null)
        {
            // Calculate resolution for this LOD using terrain's MeshResolution setting
            // DefaultBaseResolution is kept as fallback for backwards compatibility
            int baseRes = terrain.MeshResolution > 0 ? terrain.MeshResolution : DefaultBaseResolution;
            int res = Math.Max(2, baseRes >> lod);  // e.g. if baseRes=1024: LOD0=1024, LOD1=512, LOD2=256, LOD3=128

            // Calculate world-space bounds for this tile
            float tileWorldSize = terrain.StreamingTileSize;
            if (tileWorldSize <= 0) tileWorldSize = 100f;

            float startX = tileX * tileWorldSize;
            float startZ = tileY * tileWorldSize;
            float stepX = tileWorldSize / (res - 1);
            float stepZ = tileWorldSize / (res - 1);

            // SKIRT GEOMETRY: Add vertical skirts around edges to hide LOD seams
            // Skirt depth should be enough to cover max height difference between LODs
            float skirtDepth = terrain.SkirtDepth > 0 ? terrain.SkirtDepth : 10f;
            
            // Calculate buffer sizes: main grid + 4 skirt strips
            // Main grid: res × res vertices
            // Each skirt strip: res vertices (bottom row duplicated and dropped)
            int mainVertexCount = res * res;
            int skirtVertexCount = res * 4;  // 4 edges × res vertices each
            int totalVertexCount = mainVertexCount + skirtVertexCount;
            
            // Main grid indices: (res-1)² × 6
            // Each skirt strip: (res-1) × 6 indices
            int mainIndexCount = (res - 1) * (res - 1) * 6;
            int skirtIndexCount = (res - 1) * 4 * 6;  // 4 edges
            int totalIndexCount = mainIndexCount + skirtIndexCount;

            // Add closed mesh if enabled
            int closedMeshVertexCount = 0;
            int closedMeshIndexCount = 0;
            if (terrain.ClosedMesh)
            {
                closedMeshVertexCount = (4 * res * 2) + (res * res);
                closedMeshIndexCount = (4 * (res - 1) * 6) + ((res - 1) * (res - 1) * 6);
            }
            totalVertexCount += closedMeshVertexCount;
            totalIndexCount += closedMeshIndexCount;

            // Allocate buffers
            float[] vertices = Engine.Rendering.Terrain.MeshBufferPool.RentFloat(totalVertexCount * 8);
            uint[] indices = new uint[totalIndexCount];

            // Height cache for normal computation
            float[,] heightCache = new float[res, res];

            // === PASS 1: Generate main grid vertices ===
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    int idx = (z * res + x) * 8;

                    float u = x / (float)(res - 1);
                    float v = z / (float)(res - 1);

                    // World positions (no snapping - skirts will hide seams)
                    float worldX = startX + x * stepX;
                    float worldZ = startZ + z * stepZ;

                    // Snap edges exactly to tile boundaries (avoid FP drift)
                    if (x == 0) worldX = startX;
                    if (x == res - 1) worldX = startX + tileWorldSize;
                    if (z == 0) worldZ = startZ;
                    if (z == res - 1) worldZ = startZ + tileWorldSize;

                    float height = SampleHeightInfinite(terrain, worldX, worldZ);
                    heightCache[x, z] = height;

                    vertices[idx + 0] = worldX;
                    vertices[idx + 1] = height;
                    vertices[idx + 2] = worldZ;
                    vertices[idx + 3] = 0f;  // Normal (computed later)
                    vertices[idx + 4] = 1f;
                    vertices[idx + 5] = 0f;
                    vertices[idx + 6] = u;
                    vertices[idx + 7] = v;
                }
            }

            // === PASS 2: Generate main grid indices ===
            int ii = 0;
            for (int z = 0; z < res - 1; z++)
            {
                for (int x = 0; x < res - 1; x++)
                {
                    uint topLeft = (uint)(z * res + x);
                    uint topRight = (uint)(z * res + x + 1);
                    uint bottomLeft = (uint)((z + 1) * res + x);
                    uint bottomRight = (uint)((z + 1) * res + x + 1);

                    indices[ii++] = topLeft;
                    indices[ii++] = bottomLeft;
                    indices[ii++] = topRight;

                    indices[ii++] = topRight;
                    indices[ii++] = bottomLeft;
                    indices[ii++] = bottomRight;
                }
            }

            // === PASS 3: Generate skirt vertices and indices ===
            uint skirtBaseVertex = (uint)mainVertexCount;
            
            // TOP edge skirt (z = 0)
            for (int x = 0; x < res; x++)
            {
                int srcIdx = x;  // Top row vertex
                int dstIdx = (int)(skirtBaseVertex + x) * 8;
                
                float worldX = vertices[srcIdx * 8 + 0];
                float worldZ = vertices[srcIdx * 8 + 2];
                float height = vertices[srcIdx * 8 + 1] - skirtDepth;  // Drop down
                
                vertices[dstIdx + 0] = worldX;
                vertices[dstIdx + 1] = height;
                vertices[dstIdx + 2] = worldZ;
                vertices[dstIdx + 3] = 0f;
                vertices[dstIdx + 4] = 0f;
                vertices[dstIdx + 5] = -1f;  // Normal facing outward
                vertices[dstIdx + 6] = vertices[srcIdx * 8 + 6];
                vertices[dstIdx + 7] = vertices[srcIdx * 8 + 7];
            }
            
            // Top skirt indices (connect top edge to dropped vertices)
            for (int x = 0; x < res - 1; x++)
            {
                uint topEdge0 = (uint)x;
                uint topEdge1 = (uint)(x + 1);
                uint skirt0 = skirtBaseVertex + (uint)x;
                uint skirt1 = skirtBaseVertex + (uint)(x + 1);
                
                indices[ii++] = skirt0;
                indices[ii++] = topEdge0;
                indices[ii++] = skirt1;
                
                indices[ii++] = skirt1;
                indices[ii++] = topEdge0;
                indices[ii++] = topEdge1;
            }
            skirtBaseVertex += (uint)res;

            // BOTTOM edge skirt (z = res-1)
            for (int x = 0; x < res; x++)
            {
                int srcIdx = (res - 1) * res + x;  // Bottom row vertex
                int dstIdx = (int)(skirtBaseVertex + x) * 8;
                
                float worldX = vertices[srcIdx * 8 + 0];
                float worldZ = vertices[srcIdx * 8 + 2];
                float height = vertices[srcIdx * 8 + 1] - skirtDepth;
                
                vertices[dstIdx + 0] = worldX;
                vertices[dstIdx + 1] = height;
                vertices[dstIdx + 2] = worldZ;
                vertices[dstIdx + 3] = 0f;
                vertices[dstIdx + 4] = 0f;
                vertices[dstIdx + 5] = 1f;  // Normal facing outward
                vertices[dstIdx + 6] = vertices[srcIdx * 8 + 6];
                vertices[dstIdx + 7] = vertices[srcIdx * 8 + 7];
            }
            
            // Bottom skirt indices
            for (int x = 0; x < res - 1; x++)
            {
                uint bottomEdge0 = (uint)((res - 1) * res + x);
                uint bottomEdge1 = (uint)((res - 1) * res + x + 1);
                uint skirt0 = skirtBaseVertex + (uint)x;
                uint skirt1 = skirtBaseVertex + (uint)(x + 1);
                
                indices[ii++] = bottomEdge0;
                indices[ii++] = skirt0;
                indices[ii++] = bottomEdge1;
                
                indices[ii++] = bottomEdge1;
                indices[ii++] = skirt0;
                indices[ii++] = skirt1;
            }
            skirtBaseVertex += (uint)res;

            // LEFT edge skirt (x = 0)
            for (int z = 0; z < res; z++)
            {
                int srcIdx = z * res;  // Left column vertex
                int dstIdx = (int)(skirtBaseVertex + z) * 8;
                
                float worldX = vertices[srcIdx * 8 + 0];
                float worldZ = vertices[srcIdx * 8 + 2];
                float height = vertices[srcIdx * 8 + 1] - skirtDepth;
                
                vertices[dstIdx + 0] = worldX;
                vertices[dstIdx + 1] = height;
                vertices[dstIdx + 2] = worldZ;
                vertices[dstIdx + 3] = -1f;  // Normal facing outward
                vertices[dstIdx + 4] = 0f;
                vertices[dstIdx + 5] = 0f;
                vertices[dstIdx + 6] = vertices[srcIdx * 8 + 6];
                vertices[dstIdx + 7] = vertices[srcIdx * 8 + 7];
            }
            
            // Left skirt indices
            for (int z = 0; z < res - 1; z++)
            {
                uint leftEdge0 = (uint)(z * res);
                uint leftEdge1 = (uint)((z + 1) * res);
                uint skirt0 = skirtBaseVertex + (uint)z;
                uint skirt1 = skirtBaseVertex + (uint)(z + 1);
                
                indices[ii++] = leftEdge0;
                indices[ii++] = skirt0;
                indices[ii++] = leftEdge1;
                
                indices[ii++] = leftEdge1;
                indices[ii++] = skirt0;
                indices[ii++] = skirt1;
            }
            skirtBaseVertex += (uint)res;

            // RIGHT edge skirt (x = res-1)
            for (int z = 0; z < res; z++)
            {
                int srcIdx = z * res + (res - 1);  // Right column vertex
                int dstIdx = (int)(skirtBaseVertex + z) * 8;
                
                float worldX = vertices[srcIdx * 8 + 0];
                float worldZ = vertices[srcIdx * 8 + 2];
                float height = vertices[srcIdx * 8 + 1] - skirtDepth;
                
                vertices[dstIdx + 0] = worldX;
                vertices[dstIdx + 1] = height;
                vertices[dstIdx + 2] = worldZ;
                vertices[dstIdx + 3] = 1f;  // Normal facing outward
                vertices[dstIdx + 4] = 0f;
                vertices[dstIdx + 5] = 0f;
                vertices[dstIdx + 6] = vertices[srcIdx * 8 + 6];
                vertices[dstIdx + 7] = vertices[srcIdx * 8 + 7];
            }
            
            // Right skirt indices
            for (int z = 0; z < res - 1; z++)
            {
                uint rightEdge0 = (uint)(z * res + (res - 1));
                uint rightEdge1 = (uint)((z + 1) * res + (res - 1));
                uint skirt0 = skirtBaseVertex + (uint)z;
                uint skirt1 = skirtBaseVertex + (uint)(z + 1);
                
                indices[ii++] = skirt0;
                indices[ii++] = rightEdge0;
                indices[ii++] = skirt1;
                
                indices[ii++] = skirt1;
                indices[ii++] = rightEdge0;
                indices[ii++] = rightEdge1;
            }
            skirtBaseVertex += (uint)res;

            // Compute normals for main grid
            ComputeNormals(vertices, res, heightCache, stepX, stepZ);

            // Generate closed mesh if enabled
            if (terrain.ClosedMesh)
            {
                GenerateClosedMeshForTile(vertices, indices, res, terrain, startX, startZ, stepX, stepZ,
                    skirtBaseVertex, ii);
            }

            return (vertices, indices);
        }

        /// <summary>
        /// Compute smooth normals using finite differences from cached heights.
        /// OPTIMIZED: Uses pre-computed height cache instead of re-sampling (80% reduction in noise calls).
        /// SMOOTH: Uses adaptive sampling radius to reduce faceting visible with SSAO.
        /// </summary>
        private static void ComputeNormals(float[] vertices, int res, float[,] heightCache, float stepX, float stepZ)
        {
            // Adaptive smoothing radius based on resolution
            // Higher resolution = larger radius for better smoothing
            // This prevents faceting visible with SSAO while preserving detail
            // res=128 -> radius=1, res=256 -> radius=2, res=512 -> radius=4, res=1024 -> radius=8
            int smoothRadius = Math.Max(1, Math.Min(8, res / 128));

            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    int idx = (z * res + x) * 8;

                    // Get neighboring heights from cache with adaptive radius
                    // Larger radius = smoother normals, less faceting
                    int xL = Math.Max(x - smoothRadius, 0);
                    int xR = Math.Min(x + smoothRadius, res - 1);
                    int zD = Math.Max(z - smoothRadius, 0);
                    int zU = Math.Min(z + smoothRadius, res - 1);

                    float hL = heightCache[xL, z];
                    float hR = heightCache[xR, z];
                    float hD = heightCache[x, zD];
                    float hU = heightCache[x, zU];

                    // Finite differences (account for edge cases where neighbor is same vertex)
                    float dxStep = (xR - xL) * stepX;
                    float dzStep = (zU - zD) * stepZ;
                    float dHeightDx = dxStep > 0 ? (hR - hL) / dxStep : 0f;
                    float dHeightDz = dzStep > 0 ? (hU - hD) / dzStep : 0f;

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
        /// Generate closed mesh geometry (side walls + bottom face) for a tile.
        /// </summary>
        private static void GenerateClosedMeshForTile(float[] vertices, uint[] indices, int res,
            Engine.Components.Terrain terrain, float startX, float startZ, float stepX, float stepZ,
            uint baseVertexIndex, int baseIndexCount)
        {
            int vIdx = (int)baseVertexIndex * 8;
            int iIdx = baseIndexCount;
            uint currentVertex = baseVertexIndex;

            // Find the minimum height for the bottom of the skirt
            float minHeight = float.MaxValue;
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    float worldX = startX + x * stepX;
                    float worldZ = startZ + z * stepZ;
                    float h = SampleHeightInfinite(terrain, worldX, worldZ);
                    if (h < minHeight) minHeight = h;
                }
            }
            float skirtBottom = minHeight - terrain.SkirtDepth;

            // === SIDE WALL 1: Front edge (Z=0, X varies) ===
            for (int x = 0; x < res; x++)
            {
                float posX = startX + x * stepX;
                float posZ = startZ;
                float topHeight = SampleHeightInfinite(terrain, posX, posZ);
                float u = x / (float)(res - 1);

                // Top vertex
                vertices[vIdx++] = posX;
                vertices[vIdx++] = topHeight;
                vertices[vIdx++] = posZ;
                vertices[vIdx++] = 0f; vertices[vIdx++] = 0f; vertices[vIdx++] = -1f; // Normal facing forward
                vertices[vIdx++] = u; vertices[vIdx++] = 1f;

                // Bottom vertex
                vertices[vIdx++] = posX;
                vertices[vIdx++] = skirtBottom;
                vertices[vIdx++] = posZ;
                vertices[vIdx++] = 0f; vertices[vIdx++] = 0f; vertices[vIdx++] = -1f;
                vertices[vIdx++] = u; vertices[vIdx++] = 0f;
            }

            // Indices for front wall (CCW winding viewed from -Z)
            for (int x = 0; x < res - 1; x++)
            {
                uint topLeft = currentVertex + (uint)(x * 2);
                uint bottomLeft = topLeft + 1;
                uint topRight = topLeft + 2;
                uint bottomRight = topRight + 1;

                indices[iIdx++] = topLeft; indices[iIdx++] = topRight; indices[iIdx++] = bottomLeft;
                indices[iIdx++] = topRight; indices[iIdx++] = bottomRight; indices[iIdx++] = bottomLeft;
            }
            currentVertex += (uint)(res * 2);

            // === SIDE WALL 2: Back edge (Z=max, X varies) ===
            for (int x = 0; x < res; x++)
            {
                float posX = startX + x * stepX;
                float posZ = startZ + (res - 1) * stepZ;
                float topHeight = SampleHeightInfinite(terrain, posX, posZ);
                float u = x / (float)(res - 1);

                // Top vertex
                vertices[vIdx++] = posX;
                vertices[vIdx++] = topHeight;
                vertices[vIdx++] = posZ;
                vertices[vIdx++] = 0f; vertices[vIdx++] = 0f; vertices[vIdx++] = 1f; // Normal facing back
                vertices[vIdx++] = u; vertices[vIdx++] = 1f;

                // Bottom vertex
                vertices[vIdx++] = posX;
                vertices[vIdx++] = skirtBottom;
                vertices[vIdx++] = posZ;
                vertices[vIdx++] = 0f; vertices[vIdx++] = 0f; vertices[vIdx++] = 1f;
                vertices[vIdx++] = u; vertices[vIdx++] = 0f;
            }

            // Indices for back wall (CCW winding viewed from +Z)
            for (int x = 0; x < res - 1; x++)
            {
                uint topLeft = currentVertex + (uint)(x * 2);
                uint bottomLeft = topLeft + 1;
                uint topRight = topLeft + 2;
                uint bottomRight = topRight + 1;

                indices[iIdx++] = topRight; indices[iIdx++] = topLeft; indices[iIdx++] = bottomRight;
                indices[iIdx++] = topLeft; indices[iIdx++] = bottomLeft; indices[iIdx++] = bottomRight;
            }
            currentVertex += (uint)(res * 2);

            // === SIDE WALL 3: Left edge (X=0, Z varies) ===
            for (int z = 0; z < res; z++)
            {
                float posX = startX;
                float posZ = startZ + z * stepZ;
                float topHeight = SampleHeightInfinite(terrain, posX, posZ);
                float v = z / (float)(res - 1);

                // Top vertex
                vertices[vIdx++] = posX;
                vertices[vIdx++] = topHeight;
                vertices[vIdx++] = posZ;
                vertices[vIdx++] = -1f; vertices[vIdx++] = 0f; vertices[vIdx++] = 0f; // Normal facing left
                vertices[vIdx++] = v; vertices[vIdx++] = 1f;

                // Bottom vertex
                vertices[vIdx++] = posX;
                vertices[vIdx++] = skirtBottom;
                vertices[vIdx++] = posZ;
                vertices[vIdx++] = -1f; vertices[vIdx++] = 0f; vertices[vIdx++] = 0f;
                vertices[vIdx++] = v; vertices[vIdx++] = 0f;
            }

            // Indices for left wall (CCW winding viewed from -X)
            for (int z = 0; z < res - 1; z++)
            {
                uint topLeft = currentVertex + (uint)(z * 2);
                uint bottomLeft = topLeft + 1;
                uint topRight = topLeft + 2;
                uint bottomRight = topRight + 1;

                indices[iIdx++] = topRight; indices[iIdx++] = topLeft; indices[iIdx++] = bottomRight;
                indices[iIdx++] = topLeft; indices[iIdx++] = bottomLeft; indices[iIdx++] = bottomRight;
            }
            currentVertex += (uint)(res * 2);

            // === SIDE WALL 4: Right edge (X=max, Z varies) ===
            for (int z = 0; z < res; z++)
            {
                float posX = startX + (res - 1) * stepX;
                float posZ = startZ + z * stepZ;
                float topHeight = SampleHeightInfinite(terrain, posX, posZ);
                float v = z / (float)(res - 1);

                // Top vertex
                vertices[vIdx++] = posX;
                vertices[vIdx++] = topHeight;
                vertices[vIdx++] = posZ;
                vertices[vIdx++] = 1f; vertices[vIdx++] = 0f; vertices[vIdx++] = 0f; // Normal facing right
                vertices[vIdx++] = v; vertices[vIdx++] = 1f;

                // Bottom vertex
                vertices[vIdx++] = posX;
                vertices[vIdx++] = skirtBottom;
                vertices[vIdx++] = posZ;
                vertices[vIdx++] = 1f; vertices[vIdx++] = 0f; vertices[vIdx++] = 0f;
                vertices[vIdx++] = v; vertices[vIdx++] = 0f;
            }

            // Indices for right wall (CCW winding viewed from +X)
            for (int z = 0; z < res - 1; z++)
            {
                uint topLeft = currentVertex + (uint)(z * 2);
                uint bottomLeft = topLeft + 1;
                uint topRight = topLeft + 2;
                uint bottomRight = topRight + 1;

                indices[iIdx++] = topRight; indices[iIdx++] = bottomRight; indices[iIdx++] = topLeft;
                indices[iIdx++] = topLeft; indices[iIdx++] = bottomRight; indices[iIdx++] = bottomLeft;
            }
            currentVertex += (uint)(res * 2);

            // === BOTTOM FACE ===
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    float posX = startX + x * stepX;
                    float posZ = startZ + z * stepZ;
                    float u = x / (float)(res - 1);
                    float v = z / (float)(res - 1);

                    vertices[vIdx++] = posX;
                    vertices[vIdx++] = skirtBottom;
                    vertices[vIdx++] = posZ;
                    vertices[vIdx++] = 0f; vertices[vIdx++] = -1f; vertices[vIdx++] = 0f; // Normal facing down
                    vertices[vIdx++] = u; vertices[vIdx++] = v;
                }
            }

            // Indices for bottom face (reverse winding to face downward)
            for (int z = 0; z < res - 1; z++)
            {
                for (int x = 0; x < res - 1; x++)
                {
                    uint topLeft = currentVertex + (uint)(z * res + x);
                    uint topRight = topLeft + 1;
                    uint bottomLeft = currentVertex + (uint)((z + 1) * res + x);
                    uint bottomRight = bottomLeft + 1;

                    indices[iIdx++] = topLeft; indices[iIdx++] = topRight; indices[iIdx++] = bottomLeft;
                    indices[iIdx++] = topRight; indices[iIdx++] = bottomRight; indices[iIdx++] = bottomLeft;
                }
            }
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

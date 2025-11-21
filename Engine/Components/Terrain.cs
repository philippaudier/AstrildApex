using System;
using System.Security.Cryptography;
using System.Text;
using System.IO.Compression;
using OpenTK.Graphics.OpenGL4;

namespace Engine.Components
{
    /// <summary>
    /// Unity-style terrain component with heightmap-based mesh generation.
    /// Clean implementation without tessellation, layers, or splatmaps.
    /// </summary>
    public class Terrain : Component
    {
        // Serialized properties
        [Engine.Serialization.SerializableAttribute("terrainWidth")]
        public float TerrainWidth { get; set; } = 100f; // Réduit à 100 pour mieux visualiser

        [Engine.Serialization.SerializableAttribute("terrainLength")]
        public float TerrainLength { get; set; } = 100f; // Réduit à 100 pour mieux visualiser

        [Engine.Serialization.SerializableAttribute("terrainHeight")]
        public float TerrainHeight { get; set; } = 20f; // Réduit à 20 pour mieux visualiser

        [Engine.Serialization.SerializableAttribute("meshResolution")]
        public int MeshResolution { get; set; } = 128; // vertices per side (power of 2 + 1 recommended: 257, 513, 1025)

        [Engine.Serialization.SerializableAttribute("heightmapTextureGuid")]
        public Guid? HeightmapTextureGuid { get; set; } = null;

        [Engine.Serialization.SerializableAttribute("terrainMaterialGuid")]
        public Guid? TerrainMaterialGuid { get; set; } = null;

        // Water properties
        [Engine.Serialization.SerializableAttribute("enableWater")]
        public bool EnableWater { get; set; } = false;

        [Engine.Serialization.SerializableAttribute("waterMaterialGuid")]
        public Guid? WaterMaterialGuid { get; set; } = null;

        [Engine.Serialization.SerializableAttribute("waterHeight")]
        public float WaterHeight { get; set; } = 0f;

        // Runtime fields
        private float[,]? _heightData; // [x,z] heightmap data normalized [0,1]
        private int _vao = 0, _vbo = 0, _ebo = 0;
        private int _indexCount = 0;
        private bool _meshGenerated = false;

        // Public accessors for rendering
        public int VAO => _vao;
        public int IndexCount => _indexCount;
        public bool HasMesh() => _meshGenerated && _vao != 0 && _indexCount > 0;

        // Water plane rendering
        private int _waterVao = 0, _waterVbo = 0, _waterEbo = 0;

        public Terrain()
        {
        }

        /// <summary>
        /// Called when component is attached to an entity - regenerate mesh if heightmap is set
        /// </summary>
        public override void OnAttached()
        {
            base.OnAttached();

            Console.WriteLine($"[Terrain] OnAttached() called! HeightmapTextureGuid={HeightmapTextureGuid}, _meshGenerated={_meshGenerated}");

            // If we have a heightmap texture but no mesh, regenerate it
            // This happens when loading a saved scene
            if (HeightmapTextureGuid.HasValue && !_meshGenerated)
            {
                try
                {
                    Console.WriteLine($"[Terrain] OnAttached(): Regenerating terrain from saved heightmap {HeightmapTextureGuid.Value}");
                    GenerateTerrain();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Terrain] Failed to regenerate terrain on OnAttached: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"[Terrain] OnAttached(): Skipping regeneration - no heightmap or mesh already generated");
            }
        }

        /// <summary>
        /// Generate terrain mesh from heightmap. Call this after setting HeightmapTextureGuid.
        /// </summary>
        public void GenerateTerrain()
        {
            try
            {
                // Clear old terrain data first
                ClearTerrain();

                // Check GUID
                if (!HeightmapTextureGuid.HasValue)
                {
                    Console.WriteLine("[Terrain] ERROR: No heightmap texture GUID assigned");
                    return;
                }

                Console.WriteLine($"[Terrain] Starting terrain generation with HeightmapTextureGuid={HeightmapTextureGuid.Value}");

                // Debug: log mesh parameters
                Console.WriteLine($"[Terrain] Parameters: Width={TerrainWidth}, Length={TerrainLength}, Height={TerrainHeight}, Resolution={MeshResolution}");

                // Generate mesh (will check cache first, then load heightmap if needed)
                GenerateMesh();
                _meshGenerated = true;

                // CRITICAL: Load heightmap for collision detection AFTER mesh generation
                // Even if mesh was loaded from cache, we still need heightmap for HeightfieldCollider
                // This is done AFTER mesh generation to avoid loading it twice
                if (_heightData == null)
                {
                    Console.WriteLine("[Terrain] Loading heightmap for collision detection...");
                    _heightData = LoadHeightmap();
                    if (_heightData != null)
                    {
                        int hmWidth = _heightData.GetLength(0);
                        int hmHeight = _heightData.GetLength(1);
                        Console.WriteLine($"[Terrain] Loaded heightmap for collisions: {hmWidth}x{hmHeight}");
                    }
                }

                Console.WriteLine($"[Terrain] Terrain generated successfully: {MeshResolution}x{MeshResolution} vertices, {_indexCount} indices");

                // Generate water plane if enabled
                GenerateWaterPlane();

                // Notify HeightfieldCollider to update its bounds
                if (Entity != null)
                {
                    var heightfieldCollider = Entity.GetComponent<HeightfieldCollider>();
                    if (heightfieldCollider != null)
                    {
                        // First sync resolution/spacing so the collider uses the same mesh resolution
                        try
                        {
                            heightfieldCollider.SyncResolutionWithTerrain(this);
                        }
                        catch { }

                        // Then update bounds (broadphase)
                        heightfieldCollider.UpdateWorldBounds();
                        Console.WriteLine("[Terrain] Updated HeightfieldCollider bounds and resolution after terrain regeneration");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Terrain] Failed to generate terrain: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
        
        // Helper method to get min/max values from heightmap
        private (float min, float max) GetHeightmapMinMax()
        {
            if (_heightData == null) return (0f, 0f);
            
            float min = float.MaxValue;
            float max = float.MinValue;
            int width = _heightData.GetLength(0);
            int height = _heightData.GetLength(1);
            
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float h = _heightData[x, y];
                    min = Math.Min(min, h);
                    max = Math.Max(max, h);
                }
            }
            
            return (min, max);
        }

        /// <summary>
        /// Load heightmap from texture asset. Returns normalized float[,] with values [0,1].
        /// Uses disk cache to avoid expensive PNG decoding on subsequent loads.
        /// </summary>
        private float[,]? LoadHeightmap()
        {
            if (!HeightmapTextureGuid.HasValue)
            {
                Console.WriteLine("[Terrain] No heightmap texture assigned");
                return null;
            }

            // Try to load from cache first
            if (TryLoadHeightmapFromCache(out var cachedHeightmap))
            {
                Console.WriteLine($"[Terrain] ⚡ Loaded heightmap from cache ({cachedHeightmap.GetLength(0)}x{cachedHeightmap.GetLength(1)})");
                return cachedHeightmap;
            }

            // Cache miss - load from PNG and save to cache
            try
            {
                Console.WriteLine("[Terrain] Cache miss - loading heightmap from PNG...");
                var heightmap = Engine.Rendering.HeightmapLoader.LoadHeightmapFromTexture(HeightmapTextureGuid.Value);
                if (heightmap != null)
                {
                    SaveHeightmapToCache(heightmap);
                }
                return heightmap;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Terrain] Failed to load heightmap: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Generate terrain mesh with bilinear heightmap sampling for smooth results.
        /// Uses disk cache to avoid regenerating identical terrain meshes.
        /// </summary>
        private void GenerateMesh()
        {
            // PERFORMANCE: Try to load from cache FIRST before loading heightmap
            // This avoids the expensive heightmap PNG decode (8+ seconds for 1025x1025 16-bit)
            if (TryLoadMeshFromCache(out var cachedVertices, out var cachedIndices))
            {
                Console.WriteLine($"[Terrain] ⚡ Loaded mesh from cache ({cachedVertices.Length / 8} vertices, {cachedIndices.Length} indices)");
                UploadMeshToGPU(cachedVertices, cachedIndices);
                _indexCount = cachedIndices.Length;
                return;
            }

            // Cache miss - need to load heightmap and generate mesh from scratch
            Console.WriteLine("[Terrain] Cache miss - loading heightmap to generate mesh...");
            _heightData = LoadHeightmap();
            if (_heightData == null)
            {
                Console.WriteLine("[Terrain] Failed to load heightmap - cannot generate terrain");
                return;
            }

            int hmWidth = _heightData.GetLength(0);
            int hmHeight = _heightData.GetLength(1);
            Console.WriteLine($"[Terrain] Loaded heightmap: {hmWidth}x{hmHeight}");

            // Sample some heightmap values to verify it loaded correctly
            Console.WriteLine($"[Terrain] Heightmap samples: center={_heightData[hmWidth/2, hmHeight/2]}, corner={_heightData[0,0]}, " +
                              $"min={GetHeightmapMinMax().min}, max={GetHeightmapMinMax().max}");

            // Now generate the mesh from heightmap
            int res = Math.Max(2, MeshResolution);
            int vertexCount = res * res;

            // Vertex format: Position(3) + Normal(3) + TexCoord(2) = 8 floats
            float[] vertices = new float[vertexCount * 8];
            uint[] indices = new uint[(res - 1) * (res - 1) * 6];

            float stepX = TerrainWidth / (res - 1);
            float stepZ = TerrainLength / (res - 1);
            float startX = -TerrainWidth * 0.5f;
            float startZ = -TerrainLength * 0.5f;

            // Generate vertices with bilinear heightmap sampling
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    int idx = (z * res + x) * 8;

                    // UV coordinates [0,1]
                    float u = x / (float)(res - 1);
                    float v = z / (float)(res - 1);

                    // World position
                    float posX = startX + x * stepX;
                    float posZ = startZ + z * stepZ;

                    // Sample height with bilinear filtering
                    float height = SampleHeightBilinear(u, v);
                    float posY = height * TerrainHeight;

                    // Position
                    vertices[idx + 0] = posX;
                    vertices[idx + 1] = posY;
                    vertices[idx + 2] = posZ;

                    // Normal (placeholder, will be recalculated)
                    vertices[idx + 3] = 0f;
                    vertices[idx + 4] = 1f;
                    vertices[idx + 5] = 0f;

                    // TexCoord
                    vertices[idx + 6] = u;
                    vertices[idx + 7] = v;
                }
            }

            // Calculate smooth normals from mesh geometry
            CalculateNormals(vertices, res);

            // Generate triangle indices
            int iIdx = 0;
            for (int z = 0; z < res - 1; z++)
            {
                for (int x = 0; x < res - 1; x++)
                {
                    uint topLeft = (uint)(z * res + x);
                    uint topRight = (uint)(z * res + x + 1);
                    uint bottomLeft = (uint)((z + 1) * res + x);
                    uint bottomRight = (uint)((z + 1) * res + x + 1);

                    // Triangle 1 (CCW winding to face upward with backface culling)
                    indices[iIdx++] = topLeft;
                    indices[iIdx++] = topRight;
                    indices[iIdx++] = bottomLeft;

                    // Triangle 2 (CCW winding to face upward with backface culling)
                    indices[iIdx++] = bottomLeft;
                    indices[iIdx++] = topRight;
                    indices[iIdx++] = bottomRight;
                }
            }

            // Debug: log vertex/index counts before upload
            Console.WriteLine($"[Terrain] GenerateMesh: vertexCount={vertexCount}, indexCount={indices.Length}");

            // Save to cache for next time
            SaveMeshToCache(vertices, indices);

            // Upload to GPU
            UploadMeshToGPU(vertices, indices);
            _indexCount = indices.Length;
        }

        /// <summary>
        /// Get cache file path for terrain mesh
        /// </summary>
        private string GetCachePath()
        {
            // Create cache directory if needed
            string cacheDir = System.IO.Path.Combine("Cache", "Terrain");
            System.IO.Directory.CreateDirectory(cacheDir);

            // Build a deterministic key from heightmap guid + parameters
            // Include heightmap file modification time when available so the cache invalidates when the source changes
            string key = $"{HeightmapTextureGuid}_{MeshResolution}_{TerrainWidth}_{TerrainLength}_{TerrainHeight}";
            try
            {
                if (HeightmapTextureGuid.HasValue && Engine.Assets.AssetDatabase.TryGet(HeightmapTextureGuid.Value, out var rec))
                {
                    try
                    {
                        var ticks = System.IO.File.GetLastWriteTimeUtc(rec.Path).Ticks;
                        key += $"_{ticks}";
                    }
                    catch { /* ignore filesystem issues, key without timestamp still valid */ }
                }
            }
            catch { /* defensive: AssetDatabase may not be available at very early startup */ }

            // Use SHA256 to create a stable filename (GetHashCode is NOT stable across processes)
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(key));
            // Use first 16 hex chars (~64 bits) for a compact filename
            var hex = BitConverter.ToString(bytes).Replace("-", "").Substring(0, 16);

            return System.IO.Path.Combine(cacheDir, $"terrain_{hex}.cache");
        }

        /// <summary>
        /// Get cache file path for heightmap data (separate from mesh cache)
        /// </summary>
        private string GetHeightmapCachePath()
        {
            // Create cache directory if needed
            string cacheDir = System.IO.Path.Combine("Cache", "Terrain");
            System.IO.Directory.CreateDirectory(cacheDir);

            // Build a deterministic key from heightmap guid + file timestamp
            // We don't include terrain parameters here since heightmap is independent of terrain size
            string key = $"heightmap_{HeightmapTextureGuid}";
            try
            {
                if (HeightmapTextureGuid.HasValue && Engine.Assets.AssetDatabase.TryGet(HeightmapTextureGuid.Value, out var rec))
                {
                    try
                    {
                        var ticks = System.IO.File.GetLastWriteTimeUtc(rec.Path).Ticks;
                        key += $"_{ticks}";
                    }
                    catch { /* ignore filesystem issues, key without timestamp still valid */ }
                }
            }
            catch { /* defensive: AssetDatabase may not be available at very early startup */ }

            // Use SHA256 to create a stable filename
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(key));
            var hex = BitConverter.ToString(bytes).Replace("-", "").Substring(0, 16);

            return System.IO.Path.Combine(cacheDir, $"heightmap_{hex}.cache");
        }

        /// <summary>
        /// Try to load mesh from disk cache
        /// </summary>
        private bool TryLoadMeshFromCache(out float[] vertices, out uint[] indices)
        {
            vertices = Array.Empty<float>();
            indices = Array.Empty<uint>();

            try
            {
                string cachePath = GetCachePath();
                // Helpful debug output to show where we expect the cache to be
                Console.WriteLine($"[Terrain] Checking cache: {cachePath}");
                if (!System.IO.File.Exists(cachePath))
                    return false;

                using var fs = System.IO.File.OpenRead(cachePath);
                // Cache files are compressed with GZip - read through a GZipStream
                using var gz = new GZipStream(fs, CompressionMode.Decompress);
                using var br = new System.IO.BinaryReader(gz);

                // Read header
                int magic = br.ReadInt32();
                if (magic != 0x5452524E) // "TRRN"
                    return false;

                int version = br.ReadInt32();
                if (version != 1)
                    return false;

                // Read data
                int vertexCount = br.ReadInt32();
                vertices = new float[vertexCount];
                for (int i = 0; i < vertexCount; i++)
                    vertices[i] = br.ReadSingle();

                int indexCount = br.ReadInt32();
                indices = new uint[indexCount];
                for (int i = 0; i < indexCount; i++)
                    indices[i] = br.ReadUInt32();

                Console.WriteLine($"[Terrain] ⚡ Loaded mesh from compressed cache ({vertices.Length / 8} vertices, {indices.Length} indices)");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Terrain] Failed to load cache: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Save mesh to disk cache
        /// </summary>
        private void SaveMeshToCache(float[] vertices, uint[] indices)
        {
            try
            {
                string cachePath = GetCachePath();
                string tmpPath = cachePath + ".tmp";

                // Ensure directory exists
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(cachePath) ?? "Cache");

                // Write compressed data to a temporary file first
                using (var fs = System.IO.File.Create(tmpPath))
                using (var gz = new GZipStream(fs, CompressionLevel.Optimal))
                using (var bw = new System.IO.BinaryWriter(gz))
                {
                    // Write header
                    bw.Write(0x5452524E); // Magic "TRRN"
                    bw.Write(1); // Version

                    // Write data
                    bw.Write(vertices.Length);
                    foreach (var v in vertices)
                        bw.Write(v);

                    bw.Write(indices.Length);
                    foreach (var i in indices)
                        bw.Write(i);
                }

                // Move temp file into final location atomically (delete existing first if needed)
                try
                {
                    if (System.IO.File.Exists(cachePath))
                        System.IO.File.Delete(cachePath);
                    System.IO.File.Move(tmpPath, cachePath);
                }
                catch (Exception)
                {
                    // If move fails, attempt to copy then delete
                    try
                    {
                        System.IO.File.Copy(tmpPath, cachePath, true);
                        System.IO.File.Delete(tmpPath);
                    }
                    catch { /* ignore */ }
                }

                // Log final cache file info
                try
                {
                    var fi = new System.IO.FileInfo(cachePath);
                    Console.WriteLine($"[Terrain] 💾 Saved mesh to compressed cache: {cachePath} (size={fi.Length} bytes)");
                }
                catch
                {
                    Console.WriteLine($"[Terrain] 💾 Saved mesh to compressed cache: {cachePath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Terrain] Failed to save cache: {ex.Message}");
            }
        }

        /// <summary>
        /// Try to load heightmap from disk cache
        /// </summary>
        private bool TryLoadHeightmapFromCache(out float[,] heightmap)
        {
            heightmap = new float[0, 0];

            try
            {
                string cachePath = GetHeightmapCachePath();
                if (!System.IO.File.Exists(cachePath))
                    return false;

                using var fs = System.IO.File.OpenRead(cachePath);
                using var gz = new GZipStream(fs, CompressionMode.Decompress);
                using var br = new System.IO.BinaryReader(gz);

                // Read header
                int magic = br.ReadInt32();
                if (magic != 0x484D4150) // "HMAP"
                    return false;

                int version = br.ReadInt32();
                if (version != 1)
                    return false;

                // Read dimensions
                int width = br.ReadInt32();
                int height = br.ReadInt32();

                // Read data
                heightmap = new float[width, height];
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        heightmap[x, y] = br.ReadSingle();
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Terrain] Failed to load heightmap cache: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Save heightmap to disk cache
        /// </summary>
        private void SaveHeightmapToCache(float[,] heightmap)
        {
            try
            {
                string cachePath = GetHeightmapCachePath();
                string tmpPath = cachePath + ".tmp";

                // Ensure directory exists
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(cachePath) ?? "Cache");

                int width = heightmap.GetLength(0);
                int height = heightmap.GetLength(1);

                // Write compressed data to a temporary file first
                using (var fs = System.IO.File.Create(tmpPath))
                using (var gz = new GZipStream(fs, CompressionLevel.Optimal))
                using (var bw = new System.IO.BinaryWriter(gz))
                {
                    // Write header
                    bw.Write(0x484D4150); // Magic "HMAP"
                    bw.Write(1); // Version

                    // Write dimensions
                    bw.Write(width);
                    bw.Write(height);

                    // Write data
                    for (int x = 0; x < width; x++)
                    {
                        for (int y = 0; y < height; y++)
                        {
                            bw.Write(heightmap[x, y]);
                        }
                    }
                }

                // Move temp file into final location atomically
                try
                {
                    if (System.IO.File.Exists(cachePath))
                        System.IO.File.Delete(cachePath);
                    System.IO.File.Move(tmpPath, cachePath);
                }
                catch (Exception)
                {
                    // If move fails, attempt to copy then delete
                    try
                    {
                        System.IO.File.Copy(tmpPath, cachePath, true);
                        System.IO.File.Delete(tmpPath);
                    }
                    catch { /* ignore */ }
                }

                // Log final cache file info
                try
                {
                    var fi = new System.IO.FileInfo(cachePath);
                    Console.WriteLine($"[Terrain] 💾 Saved heightmap to compressed cache: {cachePath} (size={fi.Length} bytes)");
                }
                catch
                {
                    Console.WriteLine($"[Terrain] 💾 Saved heightmap to compressed cache: {cachePath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Terrain] Failed to save heightmap cache: {ex.Message}");
            }
        }

        /// <summary>
        /// Sample heightmap with bilinear interpolation for smooth terrain.
        /// </summary>
        private float SampleHeightBilinear(float u, float v)
        {
            if (_heightData == null) return 0f;

            int w = _heightData.GetLength(0);
            int h = _heightData.GetLength(1);

            // Convert UV to heightmap pixel coordinates
            float x = u * (w - 1);
            float y = v * (h - 1);

            // Get integer coordinates
            int x0 = Math.Clamp((int)Math.Floor(x), 0, w - 1);
            int y0 = Math.Clamp((int)Math.Floor(y), 0, h - 1);
            int x1 = Math.Clamp(x0 + 1, 0, w - 1);
            int y1 = Math.Clamp(y0 + 1, 0, h - 1);

            // Interpolation factors
            float fx = x - x0;
            float fy = y - y0;

            // Bilinear interpolation
            float h00 = _heightData[x0, y0];
            float h10 = _heightData[x1, y0];
            float h01 = _heightData[x0, y1];
            float h11 = _heightData[x1, y1];

            float h0 = h00 * (1f - fx) + h10 * fx;
            float h1 = h01 * (1f - fx) + h11 * fx;

            return h0 * (1f - fy) + h1 * fy;
        }

        /// <summary>
        /// Calculate smooth per-vertex normals directly from heightmap using gradient.
        /// This gives much smoother results than normals from mesh geometry.
        /// </summary>
        private void CalculateNormals(float[] vertices, int resolution)
        {
            if (_heightData == null) return;

            int hmWidth = _heightData.GetLength(0);
            int hmHeight = _heightData.GetLength(1);

            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int idx = (z * resolution + x) * 8;

                    // Get UV coordinates for this vertex
                    float u = x / (float)(resolution - 1);
                    float v = z / (float)(resolution - 1);

                    // Sample heightmap at this UV and neighboring UVs
                    float texelSizeU = 1.0f / (hmWidth - 1);
                    float texelSizeV = 1.0f / (hmHeight - 1);

                    float hL = SampleHeightBilinear(Math.Max(0f, u - texelSizeU), v);
                    float hR = SampleHeightBilinear(Math.Min(1f, u + texelSizeU), v);
                    float hD = SampleHeightBilinear(u, Math.Max(0f, v - texelSizeV));
                    float hU = SampleHeightBilinear(u, Math.Min(1f, v + texelSizeV));

                    // Calculate gradient in world space
                    // The horizontal distance between samples in world units
                    float worldStepX = TerrainWidth / (resolution - 1);
                    float worldStepZ = TerrainLength / (resolution - 1);

                    // Calculate the tangent and bitangent vectors
                    float dx = (hR - hL) * TerrainHeight / (2.0f * worldStepX);
                    float dz = (hU - hD) * TerrainHeight / (2.0f * worldStepZ);

                    // Normal is perpendicular to the surface
                    // FIX: Inverted normal direction - was pointing down, now points up
                    var normal = new System.Numerics.Vector3(dx, 1.0f, dz);
                    normal = System.Numerics.Vector3.Normalize(normal);

                    vertices[idx + 3] = normal.X;
                    vertices[idx + 4] = normal.Y;
                    vertices[idx + 5] = normal.Z;
                }
            }
        }

        private void AccumulateNormal(float[] vertices, int index, System.Numerics.Vector3 normal)
        {
            vertices[index + 3] += normal.X;
            vertices[index + 4] += normal.Y;
            vertices[index + 5] += normal.Z;
        }

        /// <summary>
        /// Upload mesh data to GPU buffers.
        /// </summary>
        private void UploadMeshToGPU(float[] vertices, uint[] indices)
        {
            // Clean up old buffers
            if (_vao != 0) GL.DeleteVertexArray(_vao);
            if (_vbo != 0) GL.DeleteBuffer(_vbo);
            if (_ebo != 0) GL.DeleteBuffer(_ebo);

            // Generate new buffers
            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();
            _ebo = GL.GenBuffer();

            GL.BindVertexArray(_vao);

            // Upload vertex data
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

            // Upload index data
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

            // Position attribute (location 0)
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            // Normal attribute (location 1)
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);

            // TexCoord attribute (location 2)
            GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, 8 * sizeof(float), 6 * sizeof(float));
            GL.EnableVertexAttribArray(2);

            GL.BindVertexArray(0);

            Console.WriteLine($"[Terrain] Uploaded mesh to GPU: VAO={_vao}, VBO={_vbo}, EBO={_ebo}");

            // If debugging is enabled via env var, also dump first few indices/vertices
            try
            {
                var dbg = Environment.GetEnvironmentVariable("TERRAIN_DEBUG_VIS");
                if (!string.IsNullOrEmpty(dbg) && dbg == "1")
                {
                    int dumpVertices = Math.Min(5, vertices.Length / 8);
                    for (int i = 0; i < dumpVertices; i++)
                    {
                        int baseIdx = i * 8;
                        Console.WriteLine($"[Terrain][DBG] V{i}: pos=({vertices[baseIdx+0]}, {vertices[baseIdx+1]}, {vertices[baseIdx+2]}), n=({vertices[baseIdx+3]}, {vertices[baseIdx+4]}, {vertices[baseIdx+5]}), uv=({vertices[baseIdx+6]}, {vertices[baseIdx+7]})");
                    }

                    int dumpIdx = Math.Min(12, indices.Length);
                    var sb = new System.Text.StringBuilder();
                    sb.Append("[Terrain][DBG] Indices: ");
                    for (int i = 0; i < dumpIdx; i++) sb.Append(indices[i] + ",");
                    Console.WriteLine(sb.ToString());
                }
            }
            catch { }
        }

        /// <summary>
        /// Render the terrain mesh.
        /// </summary>
        public void Render(System.Numerics.Vector3 viewPos)
        {
            // Don't render if mesh hasn't been generated
            if (!_meshGenerated || _vao == 0 || _indexCount == 0)
            {
                return;
            }
            
            // Verify VAO is still valid (might be invalidated after PlayMode changes)
            if (!GL.IsVertexArray(_vao))
            {
                try
                {
                    GenerateTerrain();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Terrain] Failed to regenerate terrain: {ex.Message}");
                    return;
                }
            }

            // Optional debug visualization: disable culling or force wireframe if env var set
            bool debugVis = false;
            try {
                debugVis = Environment.GetEnvironmentVariable("TERRAIN_DEBUG_VIS") == "1";
            } catch { }

            if (debugVis)
            {
                GL.Disable(EnableCap.CullFace);
                // Use the TriangleFace overload to avoid obsolete API warnings
                GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Line);
            }

            // Always disable face culling to ensure terrain is visible from all angles
            GL.Disable(EnableCap.CullFace);

            GL.BindVertexArray(_vao);
            GL.DrawElements(PrimitiveType.Triangles, _indexCount, DrawElementsType.UnsignedInt, 0);
            GL.BindVertexArray(0);

            if (debugVis)
            {
                GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
                GL.Enable(EnableCap.CullFace);
            }
        }

        /// <summary>
        /// Update or regenerate water plane (call when water settings change).
        /// </summary>
        public void UpdateWaterPlane()
        {
            GenerateWaterPlane();
        }

        /// <summary>
        /// Generate water plane mesh (simple quad at water height).
        /// </summary>
        private void GenerateWaterPlane()
        {
            // Clear existing water plane
            if (_waterVao != 0) { GL.DeleteVertexArray(_waterVao); _waterVao = 0; }
            if (_waterVbo != 0) { GL.DeleteBuffer(_waterVbo); _waterVbo = 0; }
            if (_waterEbo != 0) { GL.DeleteBuffer(_waterEbo); _waterEbo = 0; }

            if (!EnableWater) return;

            // Create a simple quad covering the terrain area at water height
            float halfWidth = TerrainWidth * 0.5f;
            float halfLength = TerrainLength * 0.5f;
            float y = WaterHeight;

            // Vertices: position(3) + normal(3) + uv(2) = 8 floats per vertex
            float[] vertices = new float[]
            {
                // Position                      // Normal        // UV
                -halfWidth, y, -halfLength,      0f, 1f, 0f,      0f, 0f,  // Bottom-left
                 halfWidth, y, -halfLength,      0f, 1f, 0f,      1f, 0f,  // Bottom-right
                 halfWidth, y,  halfLength,      0f, 1f, 0f,      1f, 1f,  // Top-right
                -halfWidth, y,  halfLength,      0f, 1f, 0f,      0f, 1f,  // Top-left
            };

            uint[] indices = new uint[]
            {
                0, 1, 2,  // First triangle
                2, 3, 0   // Second triangle
            };

            // Upload to GPU
            _waterVao = GL.GenVertexArray();
            _waterVbo = GL.GenBuffer();
            _waterEbo = GL.GenBuffer();

            GL.BindVertexArray(_waterVao);

            GL.BindBuffer(BufferTarget.ArrayBuffer, _waterVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _waterEbo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

            // Position attribute (location 0)
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            // Normal attribute (location 1)
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);

            // UV attribute (location 2)
            GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, 8 * sizeof(float), 6 * sizeof(float));
            GL.EnableVertexAttribArray(2);

            GL.BindVertexArray(0);

            Console.WriteLine($"[Terrain] Water plane generated at height {WaterHeight}");
        }

        /// <summary>
        /// Render the water plane if enabled.
        /// </summary>
        public void RenderWater()
        {
            if (!EnableWater || _waterVao == 0) return;

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.Disable(EnableCap.CullFace);

            GL.BindVertexArray(_waterVao);
            GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0);
            GL.BindVertexArray(0);

            GL.Disable(EnableCap.Blend);
        }

        /// <summary>
        /// Clear terrain and release GPU resources.
        /// </summary>
        public void ClearTerrain()
        {
            if (_vao != 0) { GL.DeleteVertexArray(_vao); _vao = 0; }
            if (_vbo != 0) { GL.DeleteBuffer(_vbo); _vbo = 0; }
            if (_ebo != 0) { GL.DeleteBuffer(_ebo); _ebo = 0; }
            if (_waterVao != 0) { GL.DeleteVertexArray(_waterVao); _waterVao = 0; }
            if (_waterVbo != 0) { GL.DeleteBuffer(_waterVbo); _waterVbo = 0; }
            if (_waterEbo != 0) { GL.DeleteBuffer(_waterEbo); _waterEbo = 0; }
            _heightData = null;
            _meshGenerated = false;
            _indexCount = 0;
            Console.WriteLine("[Terrain] Terrain cleared");
        }

        /// <summary>
        /// Sample terrain height at world position (returns world-space height).
        /// </summary>
        public float SampleHeight(float worldX, float worldZ)
        {
            if (_heightData == null) return 0f;

            // Get terrain entity world position
            float terrainWorldY = 0f;
            float localX = worldX;
            float localZ = worldZ;
            
            if (Entity != null)
            {
                Entity.GetWorldTRS(out var wpos, out var wrot, out var wscl);
                terrainWorldY = wpos.Y;

                // Convert world position to terrain local space (apply inverse rotation)
                OpenTK.Mathematics.Vector3 worldPos = new OpenTK.Mathematics.Vector3(worldX, 0, worldZ);
                OpenTK.Mathematics.Vector3 offset = worldPos - new OpenTK.Mathematics.Vector3(wpos.X, 0, wpos.Z);
                
                // Apply inverse rotation using conjugate quaternion
                OpenTK.Mathematics.Quaternion invRot = OpenTK.Mathematics.Quaternion.Conjugate(wrot);
                OpenTK.Mathematics.Vector3 localPos = invRot * offset;
                
                localX = localPos.X;
                localZ = localPos.Z;
            }

            // Convert local position to UV
            float u = (localX + TerrainWidth * 0.5f) / TerrainWidth;
            float v = (localZ + TerrainLength * 0.5f) / TerrainLength;

            // Clamp to valid range
            u = Math.Clamp(u, 0f, 1f);
            v = Math.Clamp(v, 0f, 1f);

            // Return world-space height (local height + terrain Y position)
            return terrainWorldY + SampleHeightBilinear(u, v) * TerrainHeight;
        }
    }
}

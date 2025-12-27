using System;
using System.Linq;
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

        // Terrain layers (moved from MaterialAsset for better workflow)
        [Engine.Serialization.SerializableAttribute("terrainLayers")]
        public Engine.Assets.TerrainLayer[]? TerrainLayers { get; set; } = null;

        // Water features removed: water plane is no longer managed by Terrain component

        // Vegetation properties
        [Engine.Serialization.SerializableAttribute("vegetationLayers")]
        public Engine.Assets.VegetationLayer[]? VegetationLayers { get; set; } = null;

        // === WEATHER SYSTEM MOVED TO WeatherComponent ===
        // Weather parameters are now controlled by the global WeatherComponent
        // See Engine.Components.WeatherComponent and Engine.Systems.WeatherSystem

        // Runtime fields
        private float[,]? _heightData; // [x,z] heightmap data normalized [0,1]
        private int _vao = 0, _vbo = 0, _ebo = 0;
        private int _indexCount = 0;
        private bool _meshGenerated = false;

        // Public accessors for rendering
        public int VAO => _vao;
        public int IndexCount => _indexCount;
        public bool HasMesh() => _meshGenerated && _vao != 0 && _indexCount > 0;

        // (Removed) Water plane rendering resources

        // Vegetation rendering
        private System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<OpenTK.Mathematics.Matrix4>>? _vegetationInstances = null;
        public System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<OpenTK.Mathematics.Matrix4>>? VegetationInstances => _vegetationInstances;

        // Event fired when vegetation is regenerated
        public event Action? VegetationRegenerated;

        /// <summary>
        /// Notify listeners that vegetation data has changed (e.g., inspector edits).
        /// This triggers batch updates in the renderer without regenerating geometry.
        /// </summary>
        public void NotifyVegetationChanged()
        {
            VegetationRegenerated?.Invoke();
        }

        public Terrain()
        {
        }

        /// <summary>
        /// Called when component is attached to an entity - regenerate mesh if heightmap is set
        /// CRITICAL: When cloned for PlayMode, we need to detect if our OpenGL resources are invalid
        /// </summary>
        public override void OnAttached()
        {
            base.OnAttached();

            Console.WriteLine($"[Terrain] OnAttached() called! HeightmapTextureGuid={HeightmapTextureGuid}, _meshGenerated={_meshGenerated}, _vao={_vao}");

            // CRITICAL FIX: After cloning, _meshGenerated might be true but _vao is shared/invalid
            // Check if VAO is actually valid, not just if _meshGenerated is true
            bool needsRegeneration = false;

            if (HeightmapTextureGuid.HasValue)
            {
                if (!_meshGenerated)
                {
                    // No mesh at all, need to generate
                    needsRegeneration = true;
                    Console.WriteLine($"[Terrain] OnAttached(): No mesh generated yet");
                }
                else if (_vao != 0 && !GL.IsVertexArray(_vao))
                {
                    // Mesh was supposed to be generated but VAO is invalid (cloned/shared handle)
                    needsRegeneration = true;
                    Console.WriteLine($"[Terrain] OnAttached(): VAO {_vao} is invalid (cloned scene?), regenerating");
                    _meshGenerated = false; // Reset flag
                }
                else if (_vao == 0)
                {
                    // No VAO at all
                    needsRegeneration = true;
                    Console.WriteLine($"[Terrain] OnAttached(): VAO is 0, regenerating");
                    _meshGenerated = false; // Reset flag
                }
            }

            if (needsRegeneration)
            {
                try
                {
                    Console.WriteLine($"[Terrain] OnAttached(): Regenerating terrain from heightmap {HeightmapTextureGuid!.Value}");
                    GenerateTerrain();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Terrain] Failed to regenerate terrain on OnAttached: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"[Terrain] OnAttached(): Mesh valid, skipping regeneration (VAO={_vao})");
            }
        }

        /// <summary>
        /// FIX C5: Called when entering Play Mode to regenerate vegetation
        /// This ensures vegetation entities are created in the Play scene
        /// </summary>
        public override void Start()
        {
            base.Start();

            // Check if we're in Play Mode by checking if Entity has a valid scene
            if (Entity?.Scene == null)
                return;

            // DEBUG: Log terrain state to diagnose invisibility issue
            // Mesh regeneration disabled - causes freeze and doesn't fix invisibility
            Console.WriteLine($"[Terrain] Start() in PlayMode: VAO={_vao}, HasMesh={HasMesh()}, Material={TerrainMaterialGuid}");

            // Regenerate vegetation if layers are defined but no vegetation instances exist yet
            if (VegetationLayers != null && VegetationLayers.Length > 0)
            {
                // Check if vegetation already exists (to avoid double-generation)
                bool vegetationExists = false;
                try
                {
                    // Look for vegetation parent entities
                    foreach (var layer in VegetationLayers)
                    {
                        string layerParentName = $"{Entity.Name}_Vegetation_{layer.Name}";
                        var layerParent = Entity.Scene.Entities.FirstOrDefault(e =>
                            e.Name == layerParentName && e.Parent == Entity);
                        if (layerParent != null)
                        {
                            vegetationExists = true;
                            break;
                        }
                    }
                }
                catch { }

                // Only regenerate if vegetation doesn't exist yet
                if (!vegetationExists)
                {
                    try
                    {
                        Console.WriteLine($"[Terrain] Start(): Regenerating vegetation for Play Mode");
                        GenerateVegetation(Entity.Scene!);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Terrain] Failed to generate vegetation in Start(): {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine($"[Terrain] Start(): Vegetation already exists, skipping generation");
                }
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

                // Water plane generation removed
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

                    // Triangle 1 (CCW winding when viewed from above Y+)
                    // Order: topLeft → bottomLeft → topRight
                    indices[iIdx++] = topLeft;
                    indices[iIdx++] = bottomLeft;
                    indices[iIdx++] = topRight;

                    // Triangle 2 (CCW winding when viewed from above Y+)
                    // Order: topRight → bottomLeft → bottomRight
                    indices[iIdx++] = topRight;
                    indices[iIdx++] = bottomLeft;
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
            // V3: Fixed normal calculation to use heightmap resolution instead of mesh resolution
            string key = $"v3_{HeightmapTextureGuid}_{MeshResolution}_{TerrainWidth}_{TerrainLength}_{TerrainHeight}";
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

                    // CRITICAL FIX: Calculate gradient in world space
                    // The distance between samples is based on HEIGHTMAP resolution, not mesh resolution!
                    // hL and hR are sampled at u±texelSizeU, so distance = 2*texelSizeU in UV space
                    // In world space: distance = 2 * texelSizeU * TerrainWidth = 2 * TerrainWidth / (hmWidth - 1)
                    float worldStepX = TerrainWidth / (hmWidth - 1);   // FIXED: Use heightmap resolution
                    float worldStepZ = TerrainLength / (hmHeight - 1); // FIXED: Use heightmap resolution

                    // Calculate tangent vectors (derivatives of position wrt u and v)
                    // Tangent along X axis: (1, dHeight/dX, 0)
                    // Tangent along Z axis: (0, dHeight/dZ, 1)
                    float dHeightDx = (hR - hL) * TerrainHeight / (2.0f * worldStepX);
                    float dHeightDz = (hU - hD) * TerrainHeight / (2.0f * worldStepZ);

                    var tangentX = new System.Numerics.Vector3(1.0f, dHeightDx, 0.0f);
                    var tangentZ = new System.Numerics.Vector3(0.0f, dHeightDz, 1.0f);

                    // Normal is cross product: tangentZ × tangentX 
                    // This creates upward-pointing normals for proper lighting (Y-up right-handed system)
                    // For terrain with X-right, Y-up, Z-forward: Cross(tangentZ, tangentX) points upward
                    var normal = System.Numerics.Vector3.Cross(tangentZ, tangentX);
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
            // Don't render if component is disabled
            if (!Enabled)
                return;

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

            // Optional debug visualization: wireframe mode if env var set
            bool debugVis = false;
            try {
                debugVis = Environment.GetEnvironmentVariable("TERRAIN_DEBUG_VIS") == "1";
            } catch { }

            if (debugVis)
            {
                GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Line);
            }

            // Culling is ENABLED - terrain triangles are wound CCW (counter-clockwise) to face upward
            // This allows backface culling to work properly, improving performance
            GL.BindVertexArray(_vao);
            GL.DrawElements(PrimitiveType.Triangles, _indexCount, DrawElementsType.UnsignedInt, 0);
            GL.BindVertexArray(0);

            if (debugVis)
            {
                GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
            }
        }

        // Water plane support removed from Terrain component.

        /// <summary>
        /// Clear terrain and release GPU resources.
        /// </summary>
        public void ClearTerrain()
        {
            if (_vao != 0) { GL.DeleteVertexArray(_vao); _vao = 0; }
            if (_vbo != 0) { GL.DeleteBuffer(_vbo); _vbo = 0; }
            if (_ebo != 0) { GL.DeleteBuffer(_ebo); _ebo = 0; }
            // Water GPU resources removed
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

        // === VEGETATION GENERATION ===

        /// <summary>
        /// Generate vegetation instances based on vegetation layers.
        /// Uses Poisson disk sampling for natural distribution.
        /// Creates actual entities with MeshRendererComponent for each vegetation instance.
        /// </summary>
        public void GenerateVegetation(Engine.Scene.Scene scene)
        {
            if (scene == null || VegetationLayers == null || VegetationLayers.Length == 0 || _heightData == null)
            {
                return;
            }

            try
            {
                try { Console.WriteLine($"[Terrain] GenerateVegetation START: Terrain={Entity?.Name ?? "(unnamed)"}, layers={VegetationLayers.Length}"); } catch { }
                // Initialize vegetation instances dictionary (kept for compatibility)
                _vegetationInstances = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<OpenTK.Mathematics.Matrix4>>();

                // Remove old vegetation entities
                RemoveOldVegetationEntities(scene);

                // Generate instances for each layer
                for (int layerIndex = 0; layerIndex < VegetationLayers.Length; layerIndex++)
                {
                    var layer = VegetationLayers[layerIndex];
                    // Skip if layer is disabled or has neither prefab nor model assigned
                    if (!layer.Enabled || (!layer.PrefabGuid.HasValue && !layer.ModelGuid.HasValue)) continue;

                    // Find or create layer parent entity
                    string layerParentName = $"{Entity?.Name ?? "Terrain"}_Vegetation_{layer.Name}";
                    var layerParent = Entity?.Children.FirstOrDefault(e => e.Name == layerParentName);

                    if (layerParent != null)
                    {
                        // Reuse existing layer - clear its children
                        var entitiesToRemove = new System.Collections.Generic.List<Engine.Scene.Entity>();
                        var childrenToRemove = layerParent.Children.ToList();
                        foreach (var child in childrenToRemove)
                        {
                            CollectDescendantsForRemoval(child, entitiesToRemove);
                        }

                        // Remove collected entities safely
                        foreach (var entity in entitiesToRemove)
                        {
                            entity.Active = false;
                            entity.SetParent(null, keepWorld: false);
                            scene.Entities.Remove(entity);
                            scene.RecycleEntityId(entity.Id);
                        }
                    }
                    else
                    {
                        // Create new layer parent
                        layerParent = new Engine.Scene.Entity
                        {
                            Id = scene.GetNextEntityId(),
                            Name = layerParentName,
                            Guid = Guid.NewGuid(),
                            Active = true
                        };
                        layerParent.SetParent(Entity, keepWorld: false);
                        scene.Entities.Add(layerParent);
                    }

                    var instances = GenerateLayerInstances(layer);
                    _vegetationInstances[layerIndex] = instances;

                    try { Console.WriteLine($"[Terrain] Layer {layerIndex} ('{layer.Name}') generated {instances?.Count ?? 0} instance(s)"); } catch { }

                    // Create entities for each instance
                    CreateVegetationEntities(scene, layer, instances, layerParent);
                }
                
                // Notify listeners that vegetation has been regenerated
                try { VegetationRegenerated?.Invoke(); } catch { }
                try { Console.WriteLine($"[Terrain] VegetationRegenerated invoked for Terrain={Entity?.Name ?? "(unnamed)"}"); } catch { }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Terrain] ERROR during vegetation generation: {ex.Message}");
                Console.WriteLine($"[Terrain] Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Remove old vegetation entities from previous generation.
        /// Keeps the layer parent entities, only removes the tree instances (children).
        /// </summary>
        private void RemoveOldVegetationEntities(Engine.Scene.Scene scene)
        {
            if (scene == null || Entity == null) return;

            string searchPattern = $"{Entity.Name}_Vegetation";

            // Find vegetation layer parents
            var vegetationLayers = Entity.Children.Where(e => e.Name.StartsWith(searchPattern)).ToList();
            if (vegetationLayers.Count == 0) {
                try { Console.WriteLine($"[Terrain] RemoveOldVegetationEntities: no vegetation layer parents found for '{Entity.Name}'"); } catch { }
                return;
            }

            // Deactivate and queue for removal (thread-safe approach)
            var entitiesToRemove = new System.Collections.Generic.List<Engine.Scene.Entity>();

            foreach (var layerParent in vegetationLayers)
            {
                var children = layerParent.Children.ToList();
                foreach (var child in children)
                {
                    CollectDescendantsForRemoval(child, entitiesToRemove);
                }
            }

            // Now safely remove all collected entities
            foreach (var entity in entitiesToRemove)
            {
                entity.Active = false;
                entity.SetParent(null, keepWorld: false);
                scene.Entities.Remove(entity);
                scene.RecycleEntityId(entity.Id);
            }
            try { Console.WriteLine($"[Terrain] RemoveOldVegetationEntities: removed {entitiesToRemove.Count} entities from '{Entity.Name}'"); } catch { }
        }

        /// <summary>
        /// Recursively collect entity and its descendants into a list for batch removal.
        /// </summary>
        private void CollectDescendantsForRemoval(Engine.Scene.Entity entity, System.Collections.Generic.List<Engine.Scene.Entity> list)
        {
            if (entity == null || entity == Entity) return;
            if (entity.HasComponent<Terrain>()) return;

            // Collect children first (depth-first)
            var children = entity.Children.ToList();
            foreach (var child in children)
            {
                CollectDescendantsForRemoval(child, list);
            }

            // Add this entity to removal list
            list.Add(entity);
        }


        /// <summary>
        /// Create actual entities for vegetation instances.
        /// </summary>
        private void CreateVegetationEntities(Engine.Scene.Scene scene, Engine.Assets.VegetationLayer layer, System.Collections.Generic.List<OpenTK.Mathematics.Matrix4>? instances, Engine.Scene.Entity parent)
        {
            if (scene == null) return;
            // Guard: instances may be null in some call paths; treat as no instances to create
            if (instances == null || instances.Count == 0) return;
            
            // Check if using prefab or model
            bool usingPrefab = layer.PrefabGuid.HasValue;
            bool usingModel = layer.ModelGuid.HasValue;
            
            if (!usingPrefab && !usingModel) return;

            // If using prefab, load it
            Engine.Assets.PrefabAsset? prefabAsset = null;
            if (usingPrefab)
            {
                try
                {
                    prefabAsset = Engine.Assets.AssetDatabase.LoadPrefab(layer.PrefabGuid!.Value);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Terrain] Failed to load prefab {layer.PrefabGuid}: {ex.Message}");
                    return;
                }
            }

            // If using model, load the mesh asset to get material info
            Engine.Assets.MeshAsset? meshAsset = null;
            if (usingModel)
            {
                meshAsset = Engine.Assets.AssetDatabase.LoadMeshAsset(layer.ModelGuid!.Value);
                if (meshAsset == null) return;
            }

            // If using prefab, instantiate prefab for each instance
            if (usingPrefab && prefabAsset != null && prefabAsset.RootEntity != null)
            {
                for (int i = 0; i < instances.Count; i++)
                {
                    var matrix = instances[i];

                    // Extract position, rotation, scale from matrix
                    var position = new OpenTK.Mathematics.Vector3(matrix.M41, matrix.M42, matrix.M43);

                    // Extract scale from matrix columns
                    var scaleX = new OpenTK.Mathematics.Vector3(matrix.M11, matrix.M12, matrix.M13).Length;
                    var scaleY = new OpenTK.Mathematics.Vector3(matrix.M21, matrix.M22, matrix.M23).Length;
                    var scaleZ = new OpenTK.Mathematics.Vector3(matrix.M31, matrix.M32, matrix.M33).Length;
                    var scale = new OpenTK.Mathematics.Vector3(scaleX, scaleY, scaleZ);

                    // Extract rotation (normalize the matrix columns to remove scale)
                    var rotMatrix = new OpenTK.Mathematics.Matrix3(
                        matrix.M11 / scaleX, matrix.M12 / scaleX, matrix.M13 / scaleX,
                        matrix.M21 / scaleY, matrix.M22 / scaleY, matrix.M23 / scaleY,
                        matrix.M31 / scaleZ, matrix.M32 / scaleZ, matrix.M33 / scaleZ
                    );
                    var rotation = OpenTK.Mathematics.Quaternion.FromMatrix(rotMatrix);

                    // Instantiate prefab
                    try
                    {
                        var prefabInstance = InstantiatePrefabData(prefabAsset.RootEntity, scene, parent);
                        if (prefabInstance != null)
                        {
                            prefabInstance.Name = $"{layer.Name}_{i}";
                            prefabInstance.Transform.Position = position;
                            prefabInstance.Transform.Rotation = rotation;

                            // CRITICAL FIX: MULTIPLY random scale with prefab's base scale instead of overwriting!
                            // This preserves the artist-authored scale from the prefab (e.g., if prefab is 2x size)
                            // while still allowing random variation via MinScale/MaxScale
                            // Example: prefab=(2,2,2), random=0.9 -> final=(1.8,1.8,1.8)
                            var prefabBaseScale = prefabInstance.Transform.Scale;
                            prefabInstance.Transform.Scale = new OpenTK.Mathematics.Vector3(
                                prefabBaseScale.X * scale.X,
                                prefabBaseScale.Y * scale.Y,
                                prefabBaseScale.Z * scale.Z
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Terrain] Failed to instantiate prefab instance {i}: {ex.Message}");
                    }
                }
                return; // Done with prefab instantiation
            }

            // Otherwise, use legacy model-based system
            if (!usingModel || meshAsset == null) return;

            // Determine which submeshes to render
            var submeshesToRender = new System.Collections.Generic.List<int>();
            if (layer.SubmeshIndex == -1)
            {
                // Render all submeshes
                int submeshCount = meshAsset.MaterialGuids?.Count ?? 0;
                for (int s = 0; s < submeshCount; s++)
                {
                    submeshesToRender.Add(s);
                }
                if (submeshesToRender.Count == 0)
                {
                    submeshesToRender.Add(0); // Fallback
                }
            }
            else
            {
                // Render specific submesh
                submeshesToRender.Add(layer.SubmeshIndex);
            }

            // Create entities - no arbitrary limit, let the user control density
            // Performance is managed by the density slider itself
            int createdInstances = 0;
            int totalEntitiesCreated = 0;

            for (int i = 0; i < instances.Count; i++)
            {
                var matrix = instances[i];

                // Extract position, rotation, scale from matrix
                var position = new OpenTK.Mathematics.Vector3(matrix.M41, matrix.M42, matrix.M43);

                // Extract scale from matrix columns
                var scaleX = new OpenTK.Mathematics.Vector3(matrix.M11, matrix.M12, matrix.M13).Length;
                var scaleY = new OpenTK.Mathematics.Vector3(matrix.M21, matrix.M22, matrix.M23).Length;
                var scaleZ = new OpenTK.Mathematics.Vector3(matrix.M31, matrix.M32, matrix.M33).Length;
                var scale = new OpenTK.Mathematics.Vector3(scaleX, scaleY, scaleZ);

                // Extract rotation (normalize the matrix columns to remove scale)
                var rotMatrix = new OpenTK.Mathematics.Matrix3(
                    matrix.M11 / scaleX, matrix.M12 / scaleX, matrix.M13 / scaleX,
                    matrix.M21 / scaleY, matrix.M22 / scaleY, matrix.M23 / scaleY,
                    matrix.M31 / scaleZ, matrix.M32 / scaleZ, matrix.M33 / scaleZ
                );
                var rotation = OpenTK.Mathematics.Quaternion.FromMatrix(rotMatrix);

                // OPTIMIZED: Create entities directly without intermediate parent
                // This saves 1 entity per tree instance
                foreach (var submeshIndex in submeshesToRender)
                {
                    // Get material GUID for this submesh
                    Guid materialGuid = Guid.Empty;
                    if (meshAsset.MaterialGuids != null && submeshIndex < meshAsset.MaterialGuids.Count)
                    {
                        materialGuid = meshAsset.MaterialGuids[submeshIndex] ?? Engine.Assets.AssetDatabase.EnsureDefaultWhiteMaterial();
                    }
                    if (materialGuid == Guid.Empty)
                    {
                        materialGuid = Engine.Assets.AssetDatabase.EnsureDefaultWhiteMaterial();
                    }

                    var vegEntity = new Engine.Scene.Entity
                    {
                        Id = scene.GetNextEntityId(),
                        Name = submeshesToRender.Count > 1 ? $"{layer.Name}_{i}_SM{submeshIndex}" : $"{layer.Name}_{i}",
                        Guid = Guid.NewGuid(),
                        Active = true
                    };
                    vegEntity.SetParent(parent, keepWorld: false);
                    vegEntity.Transform.Position = position;
                    vegEntity.Transform.Rotation = rotation;
                    vegEntity.Transform.Scale = scale;
                    scene.Entities.Add(vegEntity);

                    // Add MeshRenderer component
                    var meshRenderer = vegEntity.AddComponent<MeshRendererComponent>();
                    meshRenderer.CustomMeshGuid = layer.ModelGuid!.Value;
                    meshRenderer.SubmeshIndex = submeshIndex;
                    meshRenderer.MaterialGuid = materialGuid;

                    // Apply culling from material
                    meshRenderer.Culling = GetCullingModeFromMaterial(materialGuid);

                    totalEntitiesCreated++;
                }

                createdInstances++;
            }
            try { Console.WriteLine($"[Terrain] CreateVegetationEntities: layer='{layer.Name}', createdInstances={createdInstances}, totalEntitiesCreated={totalEntitiesCreated}"); } catch { }
        }

        /// <summary>
        /// Generate instances for a single vegetation layer.
        /// </summary>
        private System.Collections.Generic.List<OpenTK.Mathematics.Matrix4> GenerateLayerInstances(Engine.Assets.VegetationLayer layer)
        {
            var instances = new System.Collections.Generic.List<OpenTK.Mathematics.Matrix4>();
            var random = new Random(layer.Seed);

            // Calculate number of attempts based on density
            // Density is "instances per 100x100 area"
            float terrainArea = TerrainWidth * TerrainLength;
            float referenceArea = 100f * 100f; // 100x100 reference area
            int targetInstances = (int)((terrainArea / referenceArea) * layer.Density);

            // Scale limits based on density - no hard caps!
            // Estimate rejection rate at ~50%, so multiply by 3x for safety
            int numAttempts = targetInstances * 3;
            int maxInstances = Math.Max(targetInstances, 10000); // Allow up to target or 10k minimum

            // Get terrain transform
            var terrainPos = Entity?.Transform?.Position ?? OpenTK.Mathematics.Vector3.Zero;
            var terrainRot = Entity?.Transform?.Rotation ?? OpenTK.Mathematics.Quaternion.Identity;

            // Poisson disk sampling with rejection
            for (int i = 0; i < numAttempts; i++)
            {
                // Random position on terrain (in local space)
                float localX = ((float)random.NextDouble() - 0.5f) * TerrainWidth;
                float localZ = ((float)random.NextDouble() - 0.5f) * TerrainLength;

                // Convert to UV coordinates for heightmap sampling
                float u = (localX + TerrainWidth * 0.5f) / TerrainWidth;
                float v = (localZ + TerrainLength * 0.5f) / TerrainLength;

                // Sample height and normal
                float normalizedHeight = SampleHeightBilinear(u, v);
                var normal = SampleNormalBilinear(u, v);

                // Calculate slope angle (angle between normal and up vector)
                float slopeAngle = (float)(Math.Acos(Math.Clamp(normal.Y, -1f, 1f)) * (180.0 / Math.PI));

                // Check if we've reached the maximum number of instances
                if (instances.Count >= maxInstances) break;

                // Apply filters
                if (!layer.PassesHeightFilter(normalizedHeight)) continue;
                if (!layer.PassesSlopeFilter(slopeAngle)) continue;

                // Random scale
                float scale = layer.MinScale + (float)random.NextDouble() * (layer.MaxScale - layer.MinScale);

                // Random rotation
                float rotationY = layer.RandomRotation ? (float)(random.NextDouble() * Math.PI * 2.0) : 0f;

                // Calculate world position
                float worldY = normalizedHeight * TerrainHeight;
                var localPosition = new OpenTK.Mathematics.Vector3(localX, worldY, localZ);

                // Transform to world space
                var worldPosition = terrainPos + OpenTK.Mathematics.Vector3.Transform(localPosition, terrainRot);

                // Build transform matrix
                var rotation = OpenTK.Mathematics.Quaternion.FromAxisAngle(OpenTK.Mathematics.Vector3.UnitY, rotationY);

                // Align to normal if requested
                if (layer.AlignToNormal)
                {
                    var up = OpenTK.Mathematics.Vector3.UnitY;
                    var normalOTK = new OpenTK.Mathematics.Vector3(normal.X, normal.Y, normal.Z);
                    var alignmentRotation = CalculateAlignmentRotation(up, normalOTK);

                    // Apply alignment strength (0-100%)
                    float strength = Math.Clamp(layer.AlignmentStrength / 100f, 0f, 1f);
                    if (strength < 1f)
                    {
                        // Lerp between identity (no alignment) and full alignment
                        alignmentRotation = OpenTK.Mathematics.Quaternion.Slerp(
                            OpenTK.Mathematics.Quaternion.Identity,
                            alignmentRotation,
                            strength);
                    }

                    rotation = alignmentRotation * rotation;
                }

                // Build transform matrix using OpenTK native functions
                // This ensures correct layout for both row-major and column-major conventions
                var scaleMatrix = OpenTK.Mathematics.Matrix4.CreateScale(scale);
                var rotationMatrix = OpenTK.Mathematics.Matrix4.CreateFromQuaternion(rotation);
                var translationMatrix = OpenTK.Mathematics.Matrix4.CreateTranslation(worldPosition);

                // Combine: Scale * Rotation * Translation (standard TRS order)
                var matrix = scaleMatrix * rotationMatrix * translationMatrix;

                instances.Add(matrix);
            }

            try { Console.WriteLine($"[Terrain] GenerateLayerInstances: layer={layer?.Name ?? "(unnamed)"} produced {instances.Count} instance(s) (targetAttempts={numAttempts})"); } catch { }
            return instances;
        }

        /// <summary>
        /// Calculate rotation to align one vector to another.
        /// </summary>
        private OpenTK.Mathematics.Quaternion CalculateAlignmentRotation(OpenTK.Mathematics.Vector3 from, OpenTK.Mathematics.Vector3 to)
        {
            from = OpenTK.Mathematics.Vector3.Normalize(from);
            to = OpenTK.Mathematics.Vector3.Normalize(to);

            float dot = OpenTK.Mathematics.Vector3.Dot(from, to);

            // Vectors are parallel
            if (dot >= 0.999999f)
            {
                return OpenTK.Mathematics.Quaternion.Identity;
            }

            // Vectors are opposite
            if (dot <= -0.999999f)
            {
                var axis = OpenTK.Mathematics.Vector3.Cross(OpenTK.Mathematics.Vector3.UnitX, from);
                if (axis.LengthSquared < 0.000001f)
                {
                    axis = OpenTK.Mathematics.Vector3.Cross(OpenTK.Mathematics.Vector3.UnitZ, from);
                }
                axis = OpenTK.Mathematics.Vector3.Normalize(axis);
                return OpenTK.Mathematics.Quaternion.FromAxisAngle(axis, (float)Math.PI);
            }

            // General case
            var cross = OpenTK.Mathematics.Vector3.Cross(from, to);
            float s = (float)Math.Sqrt((1 + dot) * 2);
            float invS = 1f / s;

            return new OpenTK.Mathematics.Quaternion(
                cross.X * invS,
                cross.Y * invS,
                cross.Z * invS,
                s * 0.5f
            );
        }

        /// <summary>
        /// Sample terrain normal at UV coordinates (bilinear interpolation).
        /// </summary>
        private OpenTK.Mathematics.Vector3 SampleNormalBilinear(float u, float v)
        {
            if (_heightData == null) return OpenTK.Mathematics.Vector3.UnitY;

            int width = _heightData.GetLength(0);
            int height = _heightData.GetLength(1);

            // Convert UV to heightmap coordinates
            float fx = u * (width - 1);
            float fz = v * (height - 1);

            int x0 = (int)fx;
            int z0 = (int)fz;
            int x1 = Math.Min(x0 + 1, width - 1);
            int z1 = Math.Min(z0 + 1, height - 1);

            // Calculate normal using finite differences
            float hL = (x0 > 0) ? _heightData[x0 - 1, z0] : _heightData[x0, z0];
            float hR = (x1 < width - 1) ? _heightData[x1 + 1, z0] : _heightData[x1, z0];
            float hD = (z0 > 0) ? _heightData[x0, z0 - 1] : _heightData[x0, z0];
            float hU = (z1 < height - 1) ? _heightData[x0, z1 + 1] : _heightData[x0, z1];

            float dx = hR - hL;
            float dz = hU - hD;

            var normal = new OpenTK.Mathematics.Vector3(-dx * TerrainHeight, 2f, -dz * TerrainHeight);
            return OpenTK.Mathematics.Vector3.Normalize(normal);
        }

        /// <summary>
        /// Clear vegetation by regenerating with empty layers.
        /// This is safe and doesn't cause viewport corruption.
        /// </summary>
        public void ClearVegetation(Engine.Scene.Scene scene)
        {
            if (scene == null || Entity == null) return;

            // Clear the vegetation instances data
            _vegetationInstances?.Clear();
            _vegetationInstances = null;

            // Remove old vegetation entities using the same safe method as regeneration
            RemoveOldVegetationEntities(scene);

            // Notify that vegetation has been cleared
            VegetationRegenerated?.Invoke();
        }

        /// <summary>
        /// Recursively deactivate entity and all its descendants.
        /// Safe to call from any thread - doesn't modify scene.Entities list.
        /// </summary>
        private void DeactivateDescendants(Engine.Scene.Entity entity)
        {
            if (entity == null) return;

            // Deactivate this entity
            entity.Active = false;

            // Deactivate all children
            var children = entity.Children.ToList();
            foreach (var child in children)
            {
                DeactivateDescendants(child);
            }
        }

        /// <summary>
        /// Instantiate a prefab data structure into the scene
        /// </summary>
        private Engine.Scene.Entity? InstantiatePrefabData(Engine.Assets.PrefabEntityData prefabData, Engine.Scene.Scene scene, Engine.Scene.Entity? parent = null)
        {
            if (prefabData == null || scene == null) return null;

            try
            {
                // Create entity
                var entity = new Engine.Scene.Entity
                {
                    Id = scene.GetNextEntityId(),
                    Guid = System.Guid.NewGuid(),
                    Active = true
                };
                
                entity.Name = prefabData.Name;

                // Set local transform
                entity.Transform.Position = new OpenTK.Mathematics.Vector3(
                    prefabData.LocalPosition[0],
                    prefabData.LocalPosition[1],
                    prefabData.LocalPosition[2]
                );

                entity.Transform.Rotation = OpenTK.Mathematics.Quaternion.FromEulerAngles(
                    OpenTK.Mathematics.MathHelper.DegreesToRadians(prefabData.LocalRotation[0]),
                    OpenTK.Mathematics.MathHelper.DegreesToRadians(prefabData.LocalRotation[1]),
                    OpenTK.Mathematics.MathHelper.DegreesToRadians(prefabData.LocalRotation[2])
                );

                entity.Transform.Scale = new OpenTK.Mathematics.Vector3(
                    prefabData.LocalScale[0],
                    prefabData.LocalScale[1],
                    prefabData.LocalScale[2]
                );

                // Deserialize and attach components
                foreach (var (componentType, componentData) in prefabData.Components)
                {
                    try
                    {
                        // Try to find the type in Engine assembly first
                        var type = System.Type.GetType($"Engine.Components.{componentType}, Engine");
                        
                        // If not found, try Audio components
                        if (type == null)
                            type = System.Type.GetType($"Engine.Audio.Components.{componentType}, Engine");
                        
                        if (type == null)
                        {
                            Console.WriteLine($"[Terrain] Unknown component type: {componentType}");
                            continue;
                        }
                        
                        // Create component instance using reflection
                        var addComponentMethod = typeof(Engine.Scene.Entity).GetMethod("AddComponent", new System.Type[] { })?.MakeGenericMethod(type);
                        if (addComponentMethod == null)
                        {
                            Console.WriteLine($"[Terrain] Failed to find AddComponent method for {componentType}");
                            continue;
                        }
                        
                        var component = addComponentMethod.Invoke(entity, null) as Engine.Components.Component;
                        if (component == null)
                        {
                            Console.WriteLine($"[Terrain] Failed to create component instance for {componentType}");
                            continue;
                        }
                        
                        // Deserialize component data using ComponentSerializer
                        var dataDict = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, System.Text.Json.JsonElement>>(componentData.GetRawText());
                        if (dataDict != null)
                        {
                            Engine.Serialization.ComponentSerializer.Deserialize(component, dataDict);
                            
                            // Resolve references (critical for MeshRenderer)
                            try
                            {
                                Engine.Serialization.ComponentSerializer.ResolveReferences(component, dataDict, scene);
                            }
                            catch (System.Exception ex)
                            {
                                Console.WriteLine($"[Terrain] Failed to resolve references for {componentType}: {ex.Message}");
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Console.WriteLine($"[Terrain] Failed to deserialize component {componentType}: {ex.Message}");
                    }
                }

                // Add to scene BEFORE setting parent (so children can reference it)
                scene.Entities.Add(entity);
                
                // Set parent after adding to scene
                if (parent != null)
                {
                    entity.SetParent(parent, keepWorld: false);
                }

                // Recursively instantiate children
                foreach (var childData in prefabData.Children)
                {
                    InstantiatePrefabData(childData, scene, entity);
                }

                return entity;
            }
            catch (System.Exception ex)
            {
                Console.WriteLine($"[Terrain] Failed to instantiate prefab entity: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the appropriate culling mode for a submesh from its material.
        /// The culling mode is determined by the material, not by the vegetation layer.
        /// </summary>
        private Engine.Components.CullingMode GetCullingModeFromMaterial(System.Guid? materialGuid)
        {
            if (materialGuid.HasValue)
            {
                var material = Engine.Assets.AssetDatabase.LoadMaterial(materialGuid.Value);
                if (material != null)
                {
                    // Convert material's int CullingMode to Component CullingMode enum
                    return (Engine.Components.CullingMode)material.CullingMode;
                }
            }

            // Fallback to Back culling (most common default)
            return Engine.Components.CullingMode.Back;
        }
    }
}

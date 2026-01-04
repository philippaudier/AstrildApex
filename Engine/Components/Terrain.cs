using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.IO.Compression;
using OpenTK.Graphics.OpenGL4;

namespace Engine.Components
{
    /// <summary>
    /// Terrain rendering mode: single mesh or infinite streaming.
    /// </summary>
    public enum TerrainMode
    {
        /// <summary>
        /// Single terrain mesh (classic mode) - uses TerrainWidth/Length to define bounds.
        /// </summary>
        SingleTerrain = 0,

        /// <summary>
        /// Infinite streaming terrain - procedurally generates tiles around camera.
        /// TerrainWidth/Length/Height become templates for tile generation.
        /// </summary>
        InfiniteStreaming = 1
    }

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

        // Procedural generation properties
        [Engine.Serialization.SerializableAttribute("useProceduralGeneration")]
        public bool UseProceduralGeneration { get; set; } = false;

        [Engine.Serialization.SerializableAttribute("proceduralSeed")]
        public int ProceduralSeed { get; set; } = 0;

        [Engine.Serialization.SerializableAttribute("noiseScale")]
        public float NoiseScale { get; set; } = 50f;

        [Engine.Serialization.SerializableAttribute("octaves")]
        public int Octaves { get; set; } = 4;

        [Engine.Serialization.SerializableAttribute("persistence")]
        public float Persistence { get; set; } = 0.5f;

        [Engine.Serialization.SerializableAttribute("lacunarity")]
        public float Lacunarity { get; set; } = 2.0f;

        [Engine.Serialization.SerializableAttribute("noiseOffsetX")]
        public float NoiseOffsetX { get; set; } = 0f;

        [Engine.Serialization.SerializableAttribute("noiseOffsetY")]
        public float NoiseOffsetY { get; set; } = 0f;

        [Engine.Serialization.SerializableAttribute("noiseType")]
        public Engine.Rendering.Terrain.NoiseType NoiseType { get; set; } = Engine.Rendering.Terrain.NoiseType.Fractal;

        [Engine.Serialization.SerializableAttribute("islandMode")]
        public bool IslandMode { get; set; } = false;

        [Engine.Serialization.SerializableAttribute("islandFalloff")]
        public float IslandFalloff { get; set; } = 3f;

        [Engine.Serialization.SerializableAttribute("enableTerracing")]
        public bool EnableTerracing { get; set; } = false;

        [Engine.Serialization.SerializableAttribute("terraceCount")]
        public int TerraceCount { get; set; } = 5;

        [Engine.Serialization.SerializableAttribute("heightMultiplier")]
        public float HeightMultiplier { get; set; } = 1f;

        [Engine.Serialization.SerializableAttribute("heightPower")]
        public float HeightPower { get; set; } = 1f;

        [Engine.Serialization.SerializableAttribute("useDomainWarping")]
        public bool UseDomainWarping { get; set; } = false;

        [Engine.Serialization.SerializableAttribute("domainWarpStrength")]
        public float DomainWarpStrength { get; set; } = 0.5f;

        // Blending with texture heightmap
        [Engine.Serialization.SerializableAttribute("blendWithTexture")]
        public bool BlendWithTexture { get; set; } = false;

        [Engine.Serialization.SerializableAttribute("blendMode")]
        public Engine.Rendering.Terrain.HeightmapBlendMode BlendMode { get; set; } = Engine.Rendering.Terrain.HeightmapBlendMode.Add;

        [Engine.Serialization.SerializableAttribute("blendStrength")]
        public float BlendStrength { get; set; } = 0.5f;

        // Erosion simulation
        [Engine.Serialization.SerializableAttribute("applyErosion")]
        public bool ApplyErosion { get; set; } = false;

        [Engine.Serialization.SerializableAttribute("hydraulicIterations")]
        public int HydraulicIterations { get; set; } = 50000;

        [Engine.Serialization.SerializableAttribute("hydraulicStrength")]
        public float HydraulicStrength { get; set; } = 0.3f;

        [Engine.Serialization.SerializableAttribute("thermalIterations")]
        public int ThermalIterations { get; set; } = 5;

        [Engine.Serialization.SerializableAttribute("thermalTalusAngle")]
        public float ThermalTalusAngle { get; set; } = 0.05f;

        [Engine.Serialization.SerializableAttribute("thermalStrength")]
        public float ThermalStrength { get; set; } = 0.5f;

        // Terrain layers (moved from MaterialAsset for better workflow)
        [Engine.Serialization.SerializableAttribute("terrainLayers")]
        public Engine.Assets.TerrainLayer[]? TerrainLayers { get; set; } = null;

        // Water features removed: water plane is no longer managed by Terrain component

        // Vegetation properties
        [Engine.Serialization.SerializableAttribute("vegetationLayers")]
        public Engine.Assets.VegetationLayer[]? VegetationLayers { get; set; } = null;

        // === TERRAIN STREAMING SETTINGS ===
        // Infinite terrain streaming support (opt-in)

        private TerrainMode _mode = TerrainMode.SingleTerrain;

        [Engine.Serialization.SerializableAttribute("terrainMode")]
        public TerrainMode Mode
        {
            get => _mode;
            set
            {
                if (_mode != value)
                {
                    var oldMode = _mode;
                    _mode = value;
                    try
                    {
                        ModeChanged?.Invoke(oldMode, _mode);
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// Event raised when terrain mode changes (e.g., from SingleTerrain to InfiniteStreaming).
        /// Parameters: (oldMode, newMode)
        /// </summary>
        public event System.Action<TerrainMode, TerrainMode>? ModeChanged;

        [Engine.Serialization.SerializableAttribute("streamingTileSize")]
        public float StreamingTileSize { get; set; } = 100f;  // World size of each terrain tile (meters)

        [Engine.Serialization.SerializableAttribute("streamingRadius")]
        public int StreamingRadius { get; set; } = 2;  // Radius in tiles around camera (2 = 5x5 grid)

        [Engine.Serialization.SerializableAttribute("streamingMaxLOD")]
        public int StreamingMaxLOD { get; set; } = 3;  // Maximum LOD level (0 = highest detail)

        // === WEATHER SYSTEM MOVED TO WeatherComponent ===
        // Weather parameters are now controlled by the global WeatherComponent
        // See Engine.Components.WeatherComponent and Engine.Systems.WeatherSystem

        // Runtime fields
        private float[,]? _heightData; // [x,z] heightmap data normalized [0,1]
        private int _vao = 0, _vbo = 0, _ebo = 0;
        private int _indexCount = 0;
        private bool _meshGenerated = false;
        private bool _checkedGenerationOnLoad = false; // Prevents duplicate generation checks

        // Public accessors for rendering
        public int VAO => _vao;
        public int IndexCount => _indexCount;
        public bool HasMesh() => _meshGenerated && _vao != 0 && _indexCount > 0;

        // (Removed) Water plane rendering resources

        // Vegetation rendering
        private System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<OpenTK.Mathematics.Matrix4>>? _vegetationInstances = null;
        public System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<OpenTK.Mathematics.Matrix4>>? VegetationInstances => _vegetationInstances;

        /// <summary>
        /// Flag to track if vegetation was intentionally cleared (vs never generated).
        /// This is serialized so we don't auto-regenerate cleared vegetation on scene load.
        /// </summary>
        [Engine.Serialization.SerializableAttribute("vegetationCleared")]
        public bool VegetationCleared { get; set; } = false;

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

            Console.WriteLine($"[Terrain] OnAttached() called! UseProceduralGeneration={UseProceduralGeneration}, HeightmapTextureGuid={HeightmapTextureGuid}, _meshGenerated={_meshGenerated}, _vao={_vao}");

            // CRITICAL FIX: After cloning, _meshGenerated might be true but _vao is shared/invalid
            // Check if VAO is actually valid, not just if _meshGenerated is true
            bool needsRegeneration = false;

            // Check if we have a valid heightmap source (texture or procedural)
            bool hasHeightmapSource = UseProceduralGeneration || HeightmapTextureGuid.HasValue;

            if (hasHeightmapSource)
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
                    string source = UseProceduralGeneration ? "procedural" : $"heightmap {HeightmapTextureGuid}";
                    Console.WriteLine($"[Terrain] OnAttached(): Regenerating terrain from {source}");
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
        /// Called when component starts - used for both PlayMode and after scene load.
        /// This ensures terrain is generated even if OnAttached() was called before deserialization.
        /// </summary>
        public override void OnEnable()
        {
            base.OnEnable();

            // Check if we have a valid source but no mesh - works in both Edit and Play mode
            // This ensures terrain is generated when opening a scene OR entering play mode
            if (!_checkedGenerationOnLoad)
            {
                _checkedGenerationOnLoad = true;

                bool hasHeightmapSource = UseProceduralGeneration || HeightmapTextureGuid.HasValue;
                if (hasHeightmapSource && !_meshGenerated)
                {
                    try
                    {
                        Console.WriteLine($"[Terrain] OnEnable(): Detected missing mesh, generating terrain (UseProceduralGeneration={UseProceduralGeneration})");
                        GenerateTerrain();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Terrain] Failed to generate terrain in OnEnable(): {ex.Message}");
                    }
                }
            }
        }

        public override void Start()
        {
            base.Start();

            // Check if we're in Play Mode by checking if Entity has a valid scene
            if (Entity?.Scene == null)
                return;

            // One-time check after scene load: ensure terrain is generated if needed
            // This is a backup in case OnEnable didn't run (shouldn't happen normally)
            if (!_checkedGenerationOnLoad)
            {
                _checkedGenerationOnLoad = true;

                // Check if we have a valid source but no mesh
                bool hasHeightmapSource = UseProceduralGeneration || HeightmapTextureGuid.HasValue;
                if (hasHeightmapSource && !_meshGenerated)
                {
                    try
                    {
                        Console.WriteLine($"[Terrain] Start(): Detected missing mesh after scene load, generating terrain (UseProceduralGeneration={UseProceduralGeneration})");
                        GenerateTerrain();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Terrain] Failed to generate terrain in Start(): {ex.Message}");
                    }
                }
            }

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
        /// Generate terrain mesh from heightmap (texture or procedural).
        /// Call this after setting HeightmapTextureGuid or enabling UseProceduralGeneration.
        /// </summary>
        public void GenerateTerrain()
        {
            try
            {
                // Clear old terrain data first
                ClearTerrain();

                // Check if we have a valid source (texture or procedural)
                if (!UseProceduralGeneration && !HeightmapTextureGuid.HasValue)
                {
                    Console.WriteLine("[Terrain] ERROR: No heightmap source - enable procedural generation or assign a heightmap texture");
                    return;
                }

                string source = UseProceduralGeneration ? "Procedural" : $"Texture={HeightmapTextureGuid}";
                Console.WriteLine($"[Terrain] Starting terrain generation with source={source}");

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
        /// Load or generate heightmap. Returns normalized float[,] with values [0,1].
        /// Uses disk cache to avoid expensive PNG decoding or regeneration on subsequent loads.
        /// </summary>
        private float[,]? LoadHeightmap()
        {
            // Blend mode: procedural + texture
            if (UseProceduralGeneration && BlendWithTexture && HeightmapTextureGuid.HasValue)
            {
                return GenerateBlendedHeightmap();
            }

            // Procedural generation mode only
            if (UseProceduralGeneration)
            {
                return GenerateProceduralHeightmap();
            }

            // Texture-based mode (legacy)
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
        /// Generate blended heightmap from procedural + texture sources
        /// </summary>
        private float[,]? GenerateBlendedHeightmap()
        {
            try
            {
                Console.WriteLine("[Terrain] Generating blended heightmap (procedural + texture)...");

                // Generate procedural heightmap
                var proceduralMap = GenerateProceduralHeightmap();
                if (proceduralMap == null)
                {
                    Console.WriteLine("[Terrain] Failed to generate procedural heightmap for blending");
                    return null;
                }

                // Load texture heightmap
                var textureMap = Engine.Rendering.HeightmapLoader.LoadHeightmapFromTexture(HeightmapTextureGuid!.Value);
                if (textureMap == null)
                {
                    Console.WriteLine("[Terrain] Failed to load texture heightmap for blending, using procedural only");
                    return proceduralMap;
                }

                // Blend the two heightmaps
                var blended = Engine.Rendering.Terrain.HeightmapBlending.Blend(
                    textureMap, proceduralMap, BlendMode, BlendStrength);

                Console.WriteLine($"[Terrain] Blended heightmap generated: {blended.GetLength(0)}x{blended.GetLength(1)} (mode={BlendMode}, strength={BlendStrength})");
                return blended;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Terrain] Failed to generate blended heightmap: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Generate procedural heightmap using noise algorithms.
        /// Uses cache to avoid regenerating identical terrains.
        /// </summary>
        private float[,]? GenerateProceduralHeightmap()
        {
            // Try to load from cache first
            if (TryLoadHeightmapFromCache(out var cachedHeightmap))
            {
                Console.WriteLine($"[Terrain] ⚡ Loaded procedural heightmap from cache ({cachedHeightmap.GetLength(0)}x{cachedHeightmap.GetLength(1)})");
                return cachedHeightmap;
            }

            try
            {
                Console.WriteLine("[Terrain] Generating procedural heightmap...");

                // Use mesh resolution as heightmap resolution for consistency
                int resolution = MeshResolution;

                var parameters = new Engine.Rendering.Terrain.ProceduralHeightmapParams
                {
                    Seed = ProceduralSeed,
                    NoiseScale = NoiseScale,
                    Octaves = Octaves,
                    Persistence = Persistence,
                    Lacunarity = Lacunarity,
                    OffsetX = NoiseOffsetX,
                    OffsetY = NoiseOffsetY,
                    NoiseType = NoiseType,
                    IslandMode = IslandMode,
                    IslandFalloff = IslandFalloff,
                    EnableTerracing = EnableTerracing,
                    TerraceCount = TerraceCount,
                    HeightMultiplier = HeightMultiplier,
                    HeightPower = HeightPower,
                    UseDomainWarping = UseDomainWarping,
                    DomainWarpStrength = DomainWarpStrength,
                    ApplyErosion = ApplyErosion,
                    HydraulicIterations = HydraulicIterations,
                    HydraulicStrength = HydraulicStrength,
                    ThermalIterations = ThermalIterations,
                    ThermalTalusAngle = ThermalTalusAngle,
                    ThermalStrength = ThermalStrength
                };

                var heightmap = Engine.Rendering.Terrain.ProceduralHeightmapGenerator.Generate(
                    resolution, resolution, parameters);

                Console.WriteLine($"[Terrain] Generated procedural heightmap: {resolution}x{resolution}");

                // Save to cache for next time
                SaveHeightmapToCache(heightmap);

                return heightmap;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Terrain] Failed to generate procedural heightmap: {ex.Message}");
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

            string key;

            if (UseProceduralGeneration)
            {
                // Build key from all procedural parameters
                key = $"heightmap_procedural_{ProceduralSeed}_{NoiseScale}_{Octaves}_{Persistence}_{Lacunarity}_" +
                      $"{NoiseOffsetX}_{NoiseOffsetY}_{NoiseType}_{IslandMode}_{IslandFalloff}_" +
                      $"{EnableTerracing}_{TerraceCount}_{HeightMultiplier}_{HeightPower}_{UseDomainWarping}_{DomainWarpStrength}_" +
                      $"{BlendWithTexture}_{BlendMode}_{BlendStrength}_{HeightmapTextureGuid}_" +
                      $"{ApplyErosion}_{HydraulicIterations}_{HydraulicStrength}_{ThermalIterations}_{ThermalTalusAngle}_{ThermalStrength}_" +
                      $"{MeshResolution}";
            }
            else
            {
                // Build a deterministic key from heightmap guid + file timestamp
                // We don't include terrain parameters here since heightmap is independent of terrain size
                key = $"heightmap_{HeightmapTextureGuid}";
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
            }

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
                    // Post-process instances: apply prefab base scale and mesh minY pivot correction
                    try
                    {
                        // Clone list to allow modifications
                        if (instances == null) instances = new System.Collections.Generic.List<OpenTK.Mathematics.Matrix4>();

                        // 1) Apply prefab root scale if layer references a prefab
                        if (layer.PrefabGuid.HasValue)
                        {
                            try
                            {
                                var prefab = Engine.Assets.AssetDatabase.LoadPrefab(layer.PrefabGuid.Value);
                                if (prefab?.RootEntity != null)
                                {
                                    var ls = prefab.RootEntity.LocalScale;
                                    if (ls != null && ls.Length == 3 && !(Math.Abs(ls[0] - 1f) < 1e-6 && Math.Abs(ls[1] - 1f) < 1e-6 && Math.Abs(ls[2] - 1f) < 1e-6))
                                    {
                                        // Apply prefab base scale to the rotation/scale columns only.
                                        // Multiplying the full 4x4 matrix by a scale matrix can incorrectly scale the translation.
                                        float sx = ls[0];
                                        float sy = ls[1];
                                        float sz = ls[2];
                                        for (int i = 0; i < instances.Count; i++)
                                        {
                                            var m = instances[i];
                                            // Scale column 0 (basis X)
                                            m.M11 *= sx; m.M12 *= sx; m.M13 *= sx;
                                            // Scale column 1 (basis Y)
                                            m.M21 *= sy; m.M22 *= sy; m.M23 *= sy;
                                            // Scale column 2 (basis Z)
                                            m.M31 *= sz; m.M32 *= sz; m.M33 *= sz;
                                            // Preserve translation (M41..M43)
                                            instances[i] = m;
                                        }
                                    }
                                }
                            }
                            catch { }
                        }

                        // 2) Pivot correction: shift instances so mesh base (minY) sits on terrain
                        // Determine model GUID: prefer explicit ModelGuid, otherwise try to extract from prefab
                        Guid? modelGuid = layer.ModelGuid;
                        if ((!modelGuid.HasValue || modelGuid == Guid.Empty) && layer.PrefabGuid.HasValue)
                        {
                            try
                            {
                                if (Engine.Assets.AssetDatabase.TryGet(layer.PrefabGuid.Value, out var rec) && System.IO.File.Exists(rec.Path))
                                {
                                    var prefab = Engine.Assets.PrefabAsset.Load(rec.Path);
                                    if (prefab?.RootEntity != null)
                                    {
                                        // Look for MeshRendererComponent in root or children JSON
                                        System.Text.Json.JsonElement? meshRendererJson = null;
                                        if (prefab.RootEntity.Components != null && prefab.RootEntity.Components.TryGetValue("MeshRendererComponent", out var rm)) meshRendererJson = rm;
                                        if (!meshRendererJson.HasValue)
                                        {
                                            foreach (var child in prefab.RootEntity.Children)
                                            {
                                                if (child.Components != null && child.Components.TryGetValue("MeshRendererComponent", out var cm))
                                                {
                                                    meshRendererJson = cm;
                                                    break;
                                                }
                                            }
                                        }

                                        if (meshRendererJson.HasValue && meshRendererJson.Value.TryGetProperty("customMeshGuid", out var guidElem))
                                        {
                                            var guidStr = guidElem.GetString();
                                            if (!string.IsNullOrEmpty(guidStr) && Guid.TryParse(guidStr, out var parsed)) modelGuid = parsed;
                                        }
                                    }
                                }
                            }
                            catch { }
                        }

                        // If we have a mesh asset, compute minY for relevant submesh(es) and adjust instance translations
                        if (modelGuid.HasValue && modelGuid != Guid.Empty)
                        {
                            try
                            {
                                var meshAsset = Engine.Assets.AssetDatabase.LoadMeshAsset(modelGuid.Value);
                                if (meshAsset != null)
                                {
                                    // Determine which submeshes to consider
                                    var submeshIndices = new System.Collections.Generic.List<int>();
                                    if (layer.SubmeshIndex == -1)
                                    {
                                        for (int s = 0; s < meshAsset.SubMeshes.Count; s++) submeshIndices.Add(s);
                                        if (submeshIndices.Count == 0) submeshIndices.Add(0);
                                    }
                                    else
                                    {
                                        int sidx = Math.Max(0, Math.Min(layer.SubmeshIndex, meshAsset.SubMeshes.Count - 1));
                                        submeshIndices.Add(sidx);
                                    }

                                    // Compute global minY across chosen submeshes (mesh-local space)
                                    float globalMinY = float.MaxValue;
                                    foreach (var s in submeshIndices)
                                    {
                                        var sub = meshAsset.SubMeshes[s];
                                        for (int vi = 0; vi < sub.Vertices.Length; vi += 8)
                                        {
                                            float vy = sub.Vertices[vi + 1];
                                            if (vy < globalMinY) globalMinY = vy;
                                        }
                                    }

                                    if (globalMinY != float.MaxValue && Math.Abs(globalMinY) > 1e-6)
                                    {
                                        // Adjust each instance's translation by -minY * instanceScaleY
                                        for (int i = 0; i < instances.Count; i++)
                                        {
                                            var m = instances[i];
                                            // Extract Y-scale from matrix columns (length of second column)
                                            float scaleY = new OpenTK.Mathematics.Vector3(m.M21, m.M22, m.M23).Length;
                                            // Adjust translation Y (M42) so mesh base sits on terrain
                                            m.M42 -= globalMinY * scaleY;
                                            instances[i] = m;
                                        }
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }

                    _vegetationInstances[layerIndex] = instances;

                    // GPU INSTANCING MODE: Don't create individual entities, let VegetationRenderer handle all instances
                    // The instances are stored in _vegetationInstances and passed to VegetationRenderer via UpdateBatch()
                    // This provides much better performance (1 draw call instead of N entities)
                    // CreateVegetationEntities(scene, layer, instances, layerParent);
                }
                
                // Clear the "intentionally cleared" flag since we just generated vegetation
                VegetationCleared = false;

                // CRITICAL: Invalidate component cache after adding/removing vegetation entities
                // This ensures ComponentCache picks up the newly created vegetation entities
                scene.Cache?.Invalidate();

                // Notify listeners that vegetation has been regenerated
                try { VegetationRegenerated?.Invoke(); } catch { }
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

            Console.WriteLine($"[Terrain] GenerateLayerInstances: TerrainWidth={TerrainWidth}, TerrainLength={TerrainLength}, TerrainHeight={TerrainHeight}");

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
            // If MinDistance is set, use a spatial hash grid to efficiently reject close placements
            float baseMinDistance = Math.Max(0f, layer.MinDistance);
            float baseCellSize = baseMinDistance > 0f ? baseMinDistance : 1.0f;
            var spatial = new System.Collections.Generic.Dictionary<(int, int), System.Collections.Generic.List<OpenTK.Mathematics.Vector3>>();

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

                // Build transform matrix: first rotation + scale, then set translation manually
                var scaleMatrix = OpenTK.Mathematics.Matrix4.CreateScale(scale);
                var rotationMatrix = OpenTK.Mathematics.Matrix4.CreateFromQuaternion(rotation);

                // Combine rotation and scale first
                var matrix = rotationMatrix * scaleMatrix;

                // Set translation in the last ROW (M41, M42, M43) for row-major OpenTK Matrix4
                // This ensures translation is not affected by rotation/scale
                matrix.M41 = worldPosition.X;
                matrix.M42 = worldPosition.Y;
                matrix.M43 = worldPosition.Z;

                // Enforce minimum distance between instances if requested
                bool tooClose = false;
                if (baseMinDistance > 0f)
                {
                    // Effective minimum distance scales with instance scale to avoid overlaps for large prefabs
                    float effectiveMinDist = baseMinDistance * scale;
                    float effMinDistSq = effectiveMinDist * effectiveMinDist;

                    int gx = (int)Math.Floor(worldPosition.X / baseCellSize);
                    int gz = (int)Math.Floor(worldPosition.Z / baseCellSize);

                    // Check neighbor cells
                    for (int nx = gx - 1; nx <= gx + 1 && !tooClose; nx++)
                    {
                        for (int nz = gz - 1; nz <= gz + 1; nz++)
                        {
                            var key = (nx, nz);
                            if (spatial.TryGetValue(key, out var list))
                            {
                                foreach (var p in list)
                                {
                                    float dx = p.X - worldPosition.X;
                                    float dz = p.Z - worldPosition.Z;
                                    if (dx * dx + dz * dz <= effMinDistSq)
                                    {
                                        tooClose = true;
                                        break;
                                    }
                                }
                                if (tooClose) break;
                            }
                        }
                    }

                    if (!tooClose)
                    {
                        var key = (gx, gz);
                        if (!spatial.TryGetValue(key, out var l))
                        {
                            l = new System.Collections.Generic.List<OpenTK.Mathematics.Vector3>();
                            spatial[key] = l;
                        }
                        l.Add(worldPosition);
                    }
                }

                // Debug: log first instance
                if (instances.Count == 0)
                {
                    Console.WriteLine($"[Terrain] First instance: worldPos={worldPosition}, localPos=({localX}, {worldY}, {localZ}), terrainPos={terrainPos}");
                    Console.WriteLine($"[Terrain] Matrix translation: M41={matrix.M41}, M42={matrix.M42}, M43={matrix.M43}");
                }

                if (!tooClose)
                {
                    instances.Add(matrix);
                }
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
        /// <param name="setCleared">If true, sets VegetationCleared flag to prevent auto-regeneration</param>
        public void ClearVegetation(Engine.Scene.Scene scene, bool setCleared = true)
        {
            if (scene == null || Entity == null) return;

            // Clear the vegetation instances data
            // IMPORTANT: Create empty dict instead of null to distinguish "cleared" from "never generated"
            // null = never generated (will auto-generate)
            // empty = intentionally cleared (will NOT auto-generate)
            _vegetationInstances = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<OpenTK.Mathematics.Matrix4>>();

            // Mark as intentionally cleared so we don't auto-regenerate on scene load (optional)
            if (setCleared)
            {
                VegetationCleared = true;
            }

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

        // ===========================
        // COLLISION & PHYSICS
        // ===========================

        /// <summary>
        /// Get terrain height at world position using bilinear interpolation.
        /// Returns 0 if position is outside terrain bounds.
        /// </summary>
        /// <param name="worldX">World X coordinate</param>
        /// <param name="worldZ">World Z coordinate</param>
        /// <returns>Terrain height in world space</returns>
        public float GetHeightAtPosition(float worldX, float worldZ)
        {
            // === INFINITE STREAMING MODE ===
            if (Mode == TerrainMode.InfiniteStreaming)
            {
                // Use infinite terrain height sampling (no bounds checking needed)
                return Engine.Rendering.Terrain.Tile.TileCpuGenerator.SampleHeightInfinite(this, worldX, worldZ);
            }

            // === SINGLE TERRAIN MODE ===
            if (_heightData == null)
            {
                Console.WriteLine("[Terrain] Cannot get height: heightmap not loaded");
                return 0f;
            }

            // Get terrain world position (from entity transform)
            var terrainWorldPos = Entity?.Transform.Position ?? OpenTK.Mathematics.Vector3.Zero;

            // Convert world coordinates to local terrain space
            float localX = worldX - terrainWorldPos.X;
            float localZ = worldZ - terrainWorldPos.Z;

            // Convert local coords to normalized [0, 1] range
            float normalizedX = (localX / TerrainWidth) + 0.5f;
            float normalizedZ = (localZ / TerrainLength) + 0.5f;

            // Clamp to valid range (outside terrain = return 0)
            if (normalizedX < 0f || normalizedX > 1f || normalizedZ < 0f || normalizedZ > 1f)
                return terrainWorldPos.Y; // Return terrain base height

            // Sample heightmap with bilinear interpolation
            int width = _heightData.GetLength(0);
            int height = _heightData.GetLength(1);

            float u = normalizedX * (width - 1);
            float v = normalizedZ * (height - 1);

            int x0 = (int)Math.Floor(u);
            int z0 = (int)Math.Floor(v);
            int x1 = Math.Min(x0 + 1, width - 1);
            int z1 = Math.Min(z0 + 1, height - 1);

            float fx = u - x0;
            float fz = v - z0;

            // Bilinear interpolation
            float h00 = _heightData[x0, z0];
            float h10 = _heightData[x1, z0];
            float h01 = _heightData[x0, z1];
            float h11 = _heightData[x1, z1];

            float h0 = h00 * (1f - fx) + h10 * fx;
            float h1 = h01 * (1f - fx) + h11 * fx;
            float normalizedHeight = h0 * (1f - fz) + h1 * fz;

            // Convert to world height
            return terrainWorldPos.Y + (normalizedHeight * TerrainHeight);
        }

        /// <summary>
        /// Get terrain surface normal at world position.
        /// Used for slope calculations, physics, lighting, etc.
        /// </summary>
        /// <param name="worldX">World X coordinate</param>
        /// <param name="worldZ">World Z coordinate</param>
        /// <returns>Normal vector (normalized)</returns>
        public OpenTK.Mathematics.Vector3 GetNormalAtPosition(float worldX, float worldZ)
        {
            // Sample heights in a cross pattern around the point
            float sampleDistance = 1.0f; // 1 meter sampling distance

            // Use GetHeightAtPosition which handles both Single and Infinite modes
            float h = GetHeightAtPosition(worldX, worldZ);
            float hL = GetHeightAtPosition(worldX - sampleDistance, worldZ);
            float hR = GetHeightAtPosition(worldX + sampleDistance, worldZ);
            float hD = GetHeightAtPosition(worldX, worldZ - sampleDistance);
            float hU = GetHeightAtPosition(worldX, worldZ + sampleDistance);

            // Calculate normal using central differences
            // This gives a smooth, accurate normal
            OpenTK.Mathematics.Vector3 normal = new OpenTK.Mathematics.Vector3(
                hL - hR,                    // X component (slope in X direction)
                2.0f * sampleDistance,      // Y component (vertical)
                hD - hU                     // Z component (slope in Z direction)
            );

            return OpenTK.Mathematics.Vector3.Normalize(normal);
        }

        /// <summary>
        /// Raycast against terrain heightmap.
        /// Performs heightmap-based raycast for accurate terrain collision.
        /// </summary>
        /// <param name="origin">Ray origin in world space</param>
        /// <param name="direction">Ray direction (should be normalized)</param>
        /// <param name="maxDistance">Maximum raycast distance</param>
        /// <param name="hit">Output hit information</param>
        /// <returns>True if ray hit the terrain</returns>
        public bool RaycastTerrain(OpenTK.Mathematics.Vector3 origin, OpenTK.Mathematics.Vector3 direction, float maxDistance, out Engine.Physics.RaycastHit hit)
        {
            hit = default;

            if (_heightData == null)
                return false;

            // Normalize direction
            direction = OpenTK.Mathematics.Vector3.Normalize(direction);

            // Ray marching parameters
            float stepSize = 0.5f; // Step 0.5m at a time
            int maxSteps = (int)(maxDistance / stepSize);

            OpenTK.Mathematics.Vector3 currentPos = origin;

            for (int i = 0; i < maxSteps; i++)
            {
                // Get terrain height at current position
                float terrainHeight = GetHeightAtPosition(currentPos.X, currentPos.Z);

                // Check if we're below terrain (hit!)
                if (currentPos.Y <= terrainHeight)
                {
                    // Refine hit point with binary search for accuracy
                    OpenTK.Mathematics.Vector3 hitPoint = RefinedHitPoint(currentPos - direction * stepSize, currentPos, 4);

                    float distance = (hitPoint - origin).Length;
                    OpenTK.Mathematics.Vector3 normal = GetNormalAtPosition(hitPoint.X, hitPoint.Z);

                    hit = new Engine.Physics.RaycastHit(
                        Entity,
                        null, // Terrain doesn't have a traditional collider
                        hitPoint,
                        normal,
                        distance
                    );

                    return true;
                }

                // Move along ray
                currentPos += direction * stepSize;
            }

            return false;
        }

        /// <summary>
        /// Refine raycast hit point using binary search for better accuracy
        /// </summary>
        private OpenTK.Mathematics.Vector3 RefinedHitPoint(OpenTK.Mathematics.Vector3 start, OpenTK.Mathematics.Vector3 end, int iterations)
        {
            for (int i = 0; i < iterations; i++)
            {
                OpenTK.Mathematics.Vector3 mid = (start + end) * 0.5f;
                float terrainHeight = GetHeightAtPosition(mid.X, mid.Z);

                if (mid.Y > terrainHeight)
                {
                    start = mid; // Hit is further along
                }
                else
                {
                    end = mid; // Hit is earlier
                }
            }

            return (start + end) * 0.5f;
        }

        /// <summary>
        /// Check if a world position is within terrain bounds.
        /// In Infinite Streaming mode, always returns true (no bounds).
        /// </summary>
        /// <param name="worldX">World X coordinate</param>
        /// <param name="worldZ">World Z coordinate</param>
        /// <returns>True if position is on terrain</returns>
        public bool IsPositionOnTerrain(float worldX, float worldZ)
        {
            // === INFINITE STREAMING MODE ===
            if (Mode == TerrainMode.InfiniteStreaming)
            {
                // Infinite terrain has no bounds - always return true
                return true;
            }

            // === SINGLE TERRAIN MODE ===
            if (_heightData == null)
                return false;

            var terrainWorldPos = Entity?.Transform.Position ?? OpenTK.Mathematics.Vector3.Zero;

            float localX = worldX - terrainWorldPos.X;
            float localZ = worldZ - terrainWorldPos.Z;

            float normalizedX = (localX / TerrainWidth) + 0.5f;
            float normalizedZ = (localZ / TerrainLength) + 0.5f;

            return normalizedX >= 0f && normalizedX <= 1f && normalizedZ >= 0f && normalizedZ <= 1f;
        }

        /// <summary>
        /// Get slope angle at position in degrees (0 = flat, 90 = vertical)
        /// Useful for gameplay (can character walk here? can place object?)
        /// </summary>
        /// <param name="worldX">World X coordinate</param>
        /// <param name="worldZ">World Z coordinate</param>
        /// <returns>Slope angle in degrees</returns>
        public float GetSlopeAngleAtPosition(float worldX, float worldZ)
        {
            var normal = GetNormalAtPosition(worldX, worldZ);
            float dotProduct = OpenTK.Mathematics.Vector3.Dot(normal, OpenTK.Mathematics.Vector3.UnitY);
            float angleRad = (float)Math.Acos(Math.Clamp(dotProduct, -1.0, 1.0));
            return angleRad * (180f / (float)Math.PI); // Convert to degrees
        }
    }
}

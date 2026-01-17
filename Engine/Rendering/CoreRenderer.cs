using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using SceneClass = Engine.Scene.Scene;
using Engine.Scene;
using Engine.Components;
using Engine.Assets;

namespace Engine.Rendering;

/// <summary>
/// Abstract base renderer with shared rendering infrastructure.
/// Used by both Editor (ViewportRenderer) and standalone builds (GameRenderer).
/// Ensures identical rendering output in both contexts.
/// </summary>
public abstract class CoreRenderer : IDisposable
{
    // === Scene & Camera ===
    protected SceneClass? _scene;
    protected CameraComponent? _mainCamera;
    protected Matrix4 _viewMatrix = Matrix4.Identity;
    protected Matrix4 _projectionMatrix = Matrix4.Identity;
    protected Vector3 _cameraPosition = Vector3.Zero;

    // === Dimensions ===
    protected int _width = 1;
    protected int _height = 1;

    // === Main Framebuffer ===
    protected int _framebuffer = 0;
    protected int _colorTexture = 0;
    protected int _depthTexture = 0;

    // === Post-Processing Framebuffers (ping-pong) ===
    protected int _postFbo = 0;
    protected int _postTex = 0;
    protected int _postFbo2 = 0;
    protected int _postTex2 = 0;

    // === Global UBO ===
    protected int _globalUBO = 0;

    // === Mesh Cache ===
    protected readonly Dictionary<MeshKind, MeshData> _meshCache = new();
    protected readonly Dictionary<Guid, MeshData> _customMeshCache = new();

    // === Specialized Renderers ===
    protected Terrain.TerrainRenderer? _terrainRenderer;
    protected VegetationRenderer? _vegetationRenderer;
    protected ParticleRenderer? _particleRenderer;
    protected SkyboxRenderer? _skyboxRenderer;
    protected Shadows.ShadowManager? _shadowManager;
    protected WaterPlaneRenderer? _waterPlaneRenderer;
    protected GrassRenderer? _grassRenderer;
    protected RockRenderer? _rockRenderer;

    // === Weather System ===
    protected Systems.WeatherSystem _weatherSystem = new();

    // === Time Tracking ===
    protected System.Diagnostics.Stopwatch _timeStopwatch = System.Diagnostics.Stopwatch.StartNew();
    protected System.Diagnostics.Stopwatch _deltaStopwatch = System.Diagnostics.Stopwatch.StartNew();

    // === Terrain Subscription Tracking ===
    protected HashSet<Engine.Components.Terrain> _subscribedTerrains = new();

    // === Dispose Guard ===
    protected bool _disposed = false;

    // === Public Properties ===
    public SceneClass? Scene => _scene;
    public int Width => _width;
    public int Height => _height;
    public int ColorTexture => _colorTexture;
    public int DepthTexture => _depthTexture;

    /// <summary>
    /// Global UBO structure matching shader layout (std140).
    /// Shared by all renderers to ensure consistent lighting/weather/fog.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    protected struct GlobalUniforms
    {
        // === CAMERA & TRANSFORMS ===
        public Matrix4 ViewMatrix;
        public Matrix4 ProjectionMatrix;
        public Matrix4 ViewProjectionMatrix;
        public Vector3 CameraPosition; public float _pad1;

        // === DIRECTIONAL LIGHT (Sun/Moon) ===
        public Vector3 DirLightDirection; public float _pad2;
        public Vector3 DirLightColor; public float DirLightIntensity;

        // === POINT LIGHTS (max 4) ===
        public int PointLightCount; public float _pad3; public float _pad4; public float _pad5;
        public Vector4 PointLightPos0; public Vector4 PointLightColor0;
        public Vector4 PointLightPos1; public Vector4 PointLightColor1;
        public Vector4 PointLightPos2; public Vector4 PointLightColor2;
        public Vector4 PointLightPos3; public Vector4 PointLightColor3;

        // === SPOT LIGHTS (max 2) ===
        public int SpotLightCount; public float _pad6; public float _pad7; public float _pad8;
        public Vector4 SpotLightPos0; public Vector4 SpotLightDir0; public Vector4 SpotLightColor0; public float SpotLightAngle0; public float SpotLightInnerAngle0; public float _pad9; public float _pad10;
        public Vector4 SpotLightPos1; public Vector4 SpotLightDir1; public Vector4 SpotLightColor1; public float SpotLightAngle1; public float SpotLightInnerAngle1; public float _pad11; public float _pad12;

        // === AMBIENT & SKYBOX ===
        public Vector3 AmbientColor; public float AmbientIntensity;
        public Vector3 SkyboxTint; public float SkyboxExposure;

        // === FOG (from WeatherComponent) ===
        public int FogEnabled; public float FogDensity; public float FogOpacity; public float FogNoiseScale;
        public Vector3 FogColor; public float FogStart;
        public float FogEnd; public float FogNoiseSpeed; public float FogLayerHeight; public float FogThickness;
        public int FogFBMOctaves; public float FogFBMLacunarity; public float FogFBMGain; public float FogScattering;
        public int FogColorMode; public float _fogpad1; public float _fogpad2; public float _fogpad3;

        // === CLIP PLANE ===
        public float ClipPlaneEnabled; public float _pad16; public float _pad17; public float _pad18;
        public Vector4 ClipPlane;

        // === TIME & WEATHER SYSTEM ===
        public float Time;
        public float TimeOfDay;
        public float DayNightBlend;
        public float GoldenHourBlend;

        // Wind parameters
        public Vector2 WindDirection;
        public float WindStrength;
        public float WindSpeed;
        public float WindGustiness;
        public float _pad19; public float _pad20; public float _pad21;

        // Advanced wind (vegetation)
        public float BranchAmplitude;
        public float BranchSpeed;
        public float BranchTurbulence;
        public float TrunkStiffness;
        public float TrunkBendAmount;
        public float LeafFlutter;
        public float LeafFlutterSpeed;
        public float _pad22;

        // Precipitation
        public float RainIntensity;
        public float SnowAccumulation;
        public float SnowIntensity;
        public float Wetness;

        // Snow parameters
        public float SnowSlopeMin;
        public float SnowSlopeMax;
        public float SnowSparkle;
        public float SnowDisplacement;
    }

    /// <summary>
    /// Mesh data container for VAO/VBO/EBO.
    /// </summary>
    protected class MeshData
    {
        public int VAO { get; set; }
        public int VBO { get; set; }
        public int EBO { get; set; }
        public int VertexCount { get; set; }
    }

    #region Initialization

    /// <summary>
    /// Initialize all OpenGL resources. Call from derived class constructor.
    /// </summary>
    protected virtual void InitializeOpenGL()
    {
        // Initialize texture cache
        TextureCache.Initialize();

        // Create framebuffers
        CreateFramebuffers();

        // Create Global UBO
        CreateGlobalUBO();

        // Load primitive meshes
        LoadBasicMeshes();

        // Initialize specialized renderers
        InitializeSpecializedRenderers();

        // Initialize post-process manager
        try
        {
            PostProcessManager.Initialize();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CoreRenderer] Failed to init PostProcessManager: {ex.Message}");
        }
    }

    /// <summary>
    /// Create main and post-processing framebuffers.
    /// Override in derived classes if different FBO setup is needed.
    /// </summary>
    protected virtual void CreateFramebuffers()
    {
        // Main framebuffer
        _framebuffer = GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);

        // Color texture (HDR for post-processing)
        _colorTexture = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, _colorTexture);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f, _width, _height, 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _colorTexture, 0);

        // Depth texture
        _depthTexture = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, _depthTexture);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent24, _width, _height, 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, _depthTexture, 0);

        CheckFramebufferComplete("Main");

        // Post-processing framebuffer 1
        _postFbo = GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _postFbo);

        _postTex = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, _postTex);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f, _width, _height, 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _postTex, 0);

        CheckFramebufferComplete("Post1");

        // Post-processing framebuffer 2 (ping-pong)
        _postFbo2 = GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _postFbo2);

        _postTex2 = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, _postTex2);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f, _width, _height, 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _postTex2, 0);

        CheckFramebufferComplete("Post2");

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    protected void CheckFramebufferComplete(string name)
    {
        var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != FramebufferErrorCode.FramebufferComplete)
        {
            Console.WriteLine($"[CoreRenderer] {name} framebuffer incomplete: {status}");
        }
    }

    protected void CreateGlobalUBO()
    {
        _globalUBO = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.UniformBuffer, _globalUBO);
        GL.BufferData(BufferTarget.UniformBuffer, Marshal.SizeOf<GlobalUniforms>(), IntPtr.Zero, BufferUsageHint.DynamicDraw);
        GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, _globalUBO);
    }

    /// <summary>
    /// Initialize all specialized renderers (terrain, vegetation, particles, etc.).
    /// Override in derived classes to customize which renderers are created.
    /// </summary>
    protected virtual void InitializeSpecializedRenderers()
    {
        // Terrain renderer
        try
        {
            _terrainRenderer = new Terrain.TerrainRenderer();
            Console.WriteLine("[CoreRenderer] TerrainRenderer initialized");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CoreRenderer] Failed to init TerrainRenderer: {ex.Message}");
        }

        // Vegetation renderer
        try
        {
            _vegetationRenderer = new VegetationRenderer();
            Console.WriteLine("[CoreRenderer] VegetationRenderer initialized");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CoreRenderer] Failed to init VegetationRenderer: {ex.Message}");
        }

        // Particle renderer
        try
        {
            _particleRenderer = new ParticleRenderer();
            Console.WriteLine("[CoreRenderer] ParticleRenderer initialized");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CoreRenderer] Failed to init ParticleRenderer: {ex.Message}");
        }

        // Shadow manager
        try
        {
            _shadowManager = new Shadows.ShadowManager();
            Console.WriteLine("[CoreRenderer] ShadowManager initialized");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CoreRenderer] Failed to init ShadowManager: {ex.Message}");
        }

        // Skybox renderer
        try
        {
            _skyboxRenderer = new SkyboxRenderer();
            Console.WriteLine("[CoreRenderer] SkyboxRenderer initialized");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CoreRenderer] Failed to init SkyboxRenderer: {ex.Message}");
        }

        // Water plane renderer
        try
        {
            _waterPlaneRenderer = new WaterPlaneRenderer();
            _waterPlaneRenderer.Initialize();
            Console.WriteLine("[CoreRenderer] WaterPlaneRenderer initialized");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CoreRenderer] Failed to init WaterPlaneRenderer: {ex.Message}");
        }

        // Grass renderer
        try
        {
            _grassRenderer = new GrassRenderer();
            Console.WriteLine("[CoreRenderer] GrassRenderer initialized");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CoreRenderer] Failed to init GrassRenderer: {ex.Message}");
        }

        // Rock renderer
        try
        {
            _rockRenderer = new RockRenderer();
            Console.WriteLine("[CoreRenderer] RockRenderer initialized");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CoreRenderer] Failed to init RockRenderer: {ex.Message}");
        }
    }

    protected void LoadBasicMeshes()
    {
        LoadCubeMesh();
        LoadPlaneMesh();
        LoadSphereMesh();
    }

    protected void LoadCubeMesh()
    {
        float[] vertices = {
            // Positions         // Normals           // UVs
            -0.5f, -0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  0.0f, 0.0f,
             0.5f, -0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  1.0f, 0.0f,
             0.5f,  0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  1.0f, 1.0f,
             0.5f,  0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  1.0f, 1.0f,
            -0.5f,  0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  0.0f, 1.0f,
            -0.5f, -0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  0.0f, 0.0f,

            -0.5f, -0.5f,  0.5f,  0.0f,  0.0f,  1.0f,  0.0f, 0.0f,
             0.5f, -0.5f,  0.5f,  0.0f,  0.0f,  1.0f,  1.0f, 0.0f,
             0.5f,  0.5f,  0.5f,  0.0f,  0.0f,  1.0f,  1.0f, 1.0f,
             0.5f,  0.5f,  0.5f,  0.0f,  0.0f,  1.0f,  1.0f, 1.0f,
            -0.5f,  0.5f,  0.5f,  0.0f,  0.0f,  1.0f,  0.0f, 1.0f,
            -0.5f, -0.5f,  0.5f,  0.0f,  0.0f,  1.0f,  0.0f, 0.0f,

            -0.5f,  0.5f,  0.5f, -1.0f,  0.0f,  0.0f,  1.0f, 0.0f,
            -0.5f,  0.5f, -0.5f, -1.0f,  0.0f,  0.0f,  1.0f, 1.0f,
            -0.5f, -0.5f, -0.5f, -1.0f,  0.0f,  0.0f,  0.0f, 1.0f,
            -0.5f, -0.5f, -0.5f, -1.0f,  0.0f,  0.0f,  0.0f, 1.0f,
            -0.5f, -0.5f,  0.5f, -1.0f,  0.0f,  0.0f,  0.0f, 0.0f,
            -0.5f,  0.5f,  0.5f, -1.0f,  0.0f,  0.0f,  1.0f, 0.0f,

             0.5f,  0.5f,  0.5f,  1.0f,  0.0f,  0.0f,  1.0f, 0.0f,
             0.5f,  0.5f, -0.5f,  1.0f,  0.0f,  0.0f,  1.0f, 1.0f,
             0.5f, -0.5f, -0.5f,  1.0f,  0.0f,  0.0f,  0.0f, 1.0f,
             0.5f, -0.5f, -0.5f,  1.0f,  0.0f,  0.0f,  0.0f, 1.0f,
             0.5f, -0.5f,  0.5f,  1.0f,  0.0f,  0.0f,  0.0f, 0.0f,
             0.5f,  0.5f,  0.5f,  1.0f,  0.0f,  0.0f,  1.0f, 0.0f,

            -0.5f, -0.5f, -0.5f,  0.0f, -1.0f,  0.0f,  0.0f, 1.0f,
             0.5f, -0.5f, -0.5f,  0.0f, -1.0f,  0.0f,  1.0f, 1.0f,
             0.5f, -0.5f,  0.5f,  0.0f, -1.0f,  0.0f,  1.0f, 0.0f,
             0.5f, -0.5f,  0.5f,  0.0f, -1.0f,  0.0f,  1.0f, 0.0f,
            -0.5f, -0.5f,  0.5f,  0.0f, -1.0f,  0.0f,  0.0f, 0.0f,
            -0.5f, -0.5f, -0.5f,  0.0f, -1.0f,  0.0f,  0.0f, 1.0f,

            -0.5f,  0.5f, -0.5f,  0.0f,  1.0f,  0.0f,  0.0f, 1.0f,
             0.5f,  0.5f, -0.5f,  0.0f,  1.0f,  0.0f,  1.0f, 1.0f,
             0.5f,  0.5f,  0.5f,  0.0f,  1.0f,  0.0f,  1.0f, 0.0f,
             0.5f,  0.5f,  0.5f,  0.0f,  1.0f,  0.0f,  1.0f, 0.0f,
            -0.5f,  0.5f,  0.5f,  0.0f,  1.0f,  0.0f,  0.0f, 0.0f,
            -0.5f,  0.5f, -0.5f,  0.0f,  1.0f,  0.0f,  0.0f, 1.0f
        };

        _meshCache[MeshKind.Cube] = UploadMesh(vertices, null);
    }

    protected void LoadPlaneMesh()
    {
        float[] vertices = {
            -5.0f, 0.0f, -5.0f,  0.0f,  1.0f,  0.0f,  0.0f, 0.0f,
             5.0f, 0.0f, -5.0f,  0.0f,  1.0f,  0.0f,  10.0f, 0.0f,
             5.0f, 0.0f,  5.0f,  0.0f,  1.0f,  0.0f,  10.0f, 10.0f,
             5.0f, 0.0f,  5.0f,  0.0f,  1.0f,  0.0f,  10.0f, 10.0f,
            -5.0f, 0.0f,  5.0f,  0.0f,  1.0f,  0.0f,  0.0f, 10.0f,
            -5.0f, 0.0f, -5.0f,  0.0f,  1.0f,  0.0f,  0.0f, 0.0f
        };

        _meshCache[MeshKind.Plane] = UploadMesh(vertices, null);
    }

    protected void LoadSphereMesh()
    {
        int segments = 16;
        int rings = 12;
        var vertices = new List<float>();
        var indices = new List<uint>();

        for (int ring = 0; ring <= rings; ring++)
        {
            float v = (float)ring / rings;
            float phi = v * MathF.PI;

            for (int seg = 0; seg <= segments; seg++)
            {
                float u = (float)seg / segments;
                float theta = u * MathF.PI * 2;

                float x = MathF.Sin(phi) * MathF.Cos(theta);
                float y = MathF.Cos(phi);
                float z = MathF.Sin(phi) * MathF.Sin(theta);

                vertices.Add(x * 0.5f); vertices.Add(y * 0.5f); vertices.Add(z * 0.5f);
                vertices.Add(x); vertices.Add(y); vertices.Add(z);
                vertices.Add(u); vertices.Add(v);
            }
        }

        for (int ring = 0; ring < rings; ring++)
        {
            for (int seg = 0; seg < segments; seg++)
            {
                uint current = (uint)(ring * (segments + 1) + seg);
                uint next = current + (uint)(segments + 1);

                indices.Add(current);
                indices.Add(next);
                indices.Add(current + 1);

                indices.Add(current + 1);
                indices.Add(next);
                indices.Add(next + 1);
            }
        }

        _meshCache[MeshKind.Sphere] = UploadMesh(vertices.ToArray(), indices.ToArray());
    }

    protected MeshData UploadMesh(float[] vertices, uint[]? indices)
    {
        var meshData = new MeshData();
        meshData.VAO = GL.GenVertexArray();
        meshData.VBO = GL.GenBuffer();

        GL.BindVertexArray(meshData.VAO);

        GL.BindBuffer(BufferTarget.ArrayBuffer, meshData.VBO);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

        if (indices != null && indices.Length > 0)
        {
            meshData.EBO = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, meshData.EBO);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);
            meshData.VertexCount = indices.Length;
        }
        else
        {
            meshData.VertexCount = vertices.Length / 8; // 8 floats per vertex (pos3 + normal3 + uv2)
        }

        // Position attribute (location = 0)
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 0);
        GL.EnableVertexAttribArray(0);

        // Normal attribute (location = 1)
        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 3 * sizeof(float));
        GL.EnableVertexAttribArray(1);

        // UV attribute (location = 2)
        GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, 8 * sizeof(float), 6 * sizeof(float));
        GL.EnableVertexAttribArray(2);

        GL.BindVertexArray(0);

        return meshData;
    }

    /// <summary>
    /// Load a custom mesh from asset database and upload to GPU.
    /// </summary>
    protected MeshData? LoadCustomMesh(Guid meshGuid)
    {
        if (_customMeshCache.TryGetValue(meshGuid, out var cachedMesh))
            return cachedMesh;

        try
        {
            var meshAsset = AssetDatabase.LoadMeshAsset(meshGuid);
            if (meshAsset == null || meshAsset.SubMeshes.Count == 0)
                return null;

            // Load first submesh (TODO: support multiple submeshes)
            var subMesh = meshAsset.SubMeshes[0];
            var meshData = UploadMesh(subMesh.Vertices, subMesh.Indices);
            _customMeshCache[meshGuid] = meshData;
            return meshData;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CoreRenderer] Error loading custom mesh {meshGuid}: {ex.Message}");
            return null;
        }
    }

    #endregion

    #region Scene Management

    /// <summary>
    /// Set the scene to render. Override in derived classes to add scene-specific setup.
    /// </summary>
    public virtual void SetScene(SceneClass scene)
    {
        _scene = scene;
        Console.WriteLine($"[CoreRenderer] Scene set with {scene.Entities.Count} entities");

        // Find main camera
        FindMainCamera();

        // Subscribe to terrain vegetation events and initialize existing vegetation
        foreach (var entity in scene.Entities)
        {
            var terrain = entity.GetComponent<Engine.Components.Terrain>();
            if (terrain != null && !_subscribedTerrains.Contains(terrain))
            {
                Console.WriteLine($"[CoreRenderer] Subscribing to VegetationRegenerated event for terrain on entity '{entity.Name}'");
                terrain.VegetationRegenerated += () =>
                {
                    Console.WriteLine($"[CoreRenderer] VegetationRegenerated event fired for terrain on '{entity.Name}'");
                    OnTerrainVegetationRegenerated(terrain);
                };
                _subscribedTerrains.Add(terrain);

                // CRITICAL: Initialize vegetation batches from existing instances
                // This handles the case where terrain is loaded from scene file with pre-existing vegetation
                if (terrain.VegetationInstances != null && terrain.VegetationInstances.Count > 0)
                {
                    Console.WriteLine($"[CoreRenderer] Initializing existing vegetation for terrain: {terrain.VegetationInstances.Count} layers");
                    OnTerrainVegetationRegenerated(terrain);
                }
            }
        }
    }

    /// <summary>
    /// Find and cache the main camera from the scene.
    /// </summary>
    protected virtual void FindMainCamera()
    {
        if (_scene == null) return;

        foreach (var entity in _scene.Entities)
        {
            if (!entity.Active) continue;
            var cam = entity.GetComponent<CameraComponent>();
            if (cam != null && cam.Enabled)
            {
                _mainCamera = cam;

                entity.GetWorldTRS(out var worldPos, out _, out _);
                _cameraPosition = worldPos;

                _viewMatrix = cam.ViewMatrix;
                float aspect = (float)_width / _height;
                _projectionMatrix = cam.ProjectionMatrix(aspect);

                Console.WriteLine($"[CoreRenderer] Main camera found: {entity.Name} at {_cameraPosition}");
                break;
            }
        }

        if (_mainCamera == null)
        {
            Console.WriteLine("[CoreRenderer] WARNING: No main camera found in scene!");
        }
    }

    /// <summary>
    /// Handle terrain vegetation regeneration event.
    /// </summary>
    protected virtual void OnTerrainVegetationRegenerated(Engine.Components.Terrain terrain)
    {
        if (_vegetationRenderer == null || terrain.VegetationLayers == null)
        {
            Console.WriteLine($"[CoreRenderer] OnTerrainVegetationRegenerated: early return - vegRenderer={_vegetationRenderer != null}, layers={terrain.VegetationLayers != null}");
            return;
        }

        // Get vegetation instances based on terrain mode
        Dictionary<int, List<Matrix4>>? vegetationInstances = null;

        // Check if terrain actually has streaming tiles loaded
        bool hasStreamingTiles = _terrainRenderer?.HasLoadedTiles() ?? false;

        if (terrain.Mode == TerrainMode.InfiniteStreaming && _terrainRenderer != null && hasStreamingTiles)
        {
            // INFINITE STREAMING: Get instances from visible tiles only
            var viewProjMatrix = _viewMatrix * _projectionMatrix;
            var frustumCuller = new FrustumCuller();
            frustumCuller.ExtractPlanes(viewProjMatrix);
            vegetationInstances = _terrainRenderer.GetStreamingVegetationInstances(_cameraPosition, viewProjMatrix, frustumCuller, terrain);
            Console.WriteLine($"[CoreRenderer] Streaming vegetation from tiles: {vegetationInstances?.Count ?? 0} layer sets");
        }
        else
        {
            // SINGLE TERRAIN or streaming without tiles yet: Use terrain's VegetationInstances directly
            vegetationInstances = terrain.VegetationInstances;
            Console.WriteLine($"[CoreRenderer] Using terrain VegetationInstances: {vegetationInstances?.Count ?? 0} layers, hasStreamingTiles={hasStreamingTiles}");
        }

        if (vegetationInstances == null || vegetationInstances.Count == 0)
        {
            Console.WriteLine($"[CoreRenderer] OnTerrainVegetationRegenerated: no vegetation instances");
            _vegetationRenderer.ClearBatches();
            return;
        }

        int totalInstances = vegetationInstances.Values.Sum(list => list?.Count ?? 0);
        Console.WriteLine($"[CoreRenderer] Vegetation regenerated: {terrain.VegetationLayers.Length} layers, {totalInstances} total instances");

        for (int layerIndex = 0; layerIndex < terrain.VegetationLayers.Length; layerIndex++)
        {
            var layer = terrain.VegetationLayers[layerIndex];
            if (layer == null)
            {
                Console.WriteLine($"[CoreRenderer] Layer {layerIndex}: null, skipping");
                continue;
            }

            // Note: A layer can be grass/rock AND have tree models at the same time
            // GrassRenderer/RockRenderer handle grass/rocks, VegetationRenderer handles tree models
            // Don't skip based on IsGrassLayer/IsRockLayer - check for modelGuid instead

            Guid? modelGuid = GetModelGuidFromLayer(layer);
            Console.WriteLine($"[CoreRenderer] Layer {layerIndex}: modelGuid={modelGuid}, PrefabGuid={layer.PrefabGuid}, ModelGuid={layer.ModelGuid}");

            if (modelGuid == null || modelGuid == Guid.Empty)
            {
                Console.WriteLine($"[CoreRenderer] Layer {layerIndex}: no valid modelGuid, skipping");
                continue;
            }

            if (!vegetationInstances.TryGetValue(layerIndex, out var transforms) || transforms == null || transforms.Count == 0)
            {
                Console.WriteLine($"[CoreRenderer] Layer {layerIndex}: no transforms, skipping");
                continue;
            }

            Console.WriteLine($"[CoreRenderer] Layer {layerIndex}: {transforms.Count} transforms, SubmeshIndex={layer.SubmeshIndex}");

            // Handle multi-submesh rendering when SubmeshIndex == -1
            if (layer.SubmeshIndex == -1)
            {
                // Render all submeshes of the model
                try
                {
                    var meshAsset = AssetDatabase.LoadMeshAsset(modelGuid.Value);
                    if (meshAsset != null)
                    {
                        int submeshCount = meshAsset.SubMeshes?.Count ?? 1;
                        if (submeshCount == 0) submeshCount = 1;
                        Console.WriteLine($"[CoreRenderer] Layer {layerIndex}: rendering {submeshCount} submeshes");

                        for (int s = 0; s < submeshCount; s++)
                        {
                            // Determine per-submesh culling from material
                            CullingMode cullMode = CullingMode.Back;
                            try
                            {
                                if (meshAsset.MaterialGuids != null && s < meshAsset.MaterialGuids.Count)
                                {
                                    var matGuid = meshAsset.MaterialGuids[s];
                                    if (matGuid != null && matGuid != Guid.Empty)
                                    {
                                        var mat = AssetDatabase.LoadMaterial(matGuid.Value);
                                        if (mat != null)
                                        {
                                            cullMode = (CullingMode)mat.CullingMode;
                                        }
                                    }
                                }
                            }
                            catch { }

                            _vegetationRenderer.UpdateBatch(modelGuid.Value, s, transforms, cullMode,
                                layer.MaxRenderDistance, layer.CullingSphereRadius,
                                layer.AlignToNormal, layer.AlignmentStrength / 100f);
                        }
                    }
                    else
                    {
                        // Fallback to submesh 0
                        _vegetationRenderer.UpdateBatch(modelGuid.Value, 0, transforms, CullingMode.Back,
                            layer.MaxRenderDistance, layer.CullingSphereRadius,
                            layer.AlignToNormal, layer.AlignmentStrength / 100f);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CoreRenderer] Failed to load mesh for multi-submesh: {ex.Message}");
                    _vegetationRenderer.UpdateBatch(modelGuid.Value, 0, transforms, CullingMode.Back,
                        layer.MaxRenderDistance, layer.CullingSphereRadius,
                        layer.AlignToNormal, layer.AlignmentStrength / 100f);
                }
            }
            else
            {
                // Single submesh rendering
                int submeshIndex = layer.SubmeshIndex >= 0 ? layer.SubmeshIndex : 0;

                // Load culling mode from material
                CullingMode cullMode = CullingMode.Back;
                try
                {
                    var meshAsset = AssetDatabase.LoadMeshAsset(modelGuid.Value);
                    if (meshAsset?.SubMeshes != null && submeshIndex < meshAsset.SubMeshes.Count)
                    {
                        var sub = meshAsset.SubMeshes[submeshIndex];
                        if (meshAsset.MaterialGuids != null && sub.MaterialIndex >= 0 && sub.MaterialIndex < meshAsset.MaterialGuids.Count)
                        {
                            var matGuid = meshAsset.MaterialGuids[sub.MaterialIndex];
                            if (matGuid != null && matGuid != Guid.Empty)
                            {
                                var mat = AssetDatabase.LoadMaterial(matGuid.Value);
                                if (mat != null)
                                {
                                    cullMode = (CullingMode)mat.CullingMode;
                                }
                            }
                        }
                    }
                }
                catch { }

                _vegetationRenderer.UpdateBatch(modelGuid.Value, submeshIndex, transforms, cullMode,
                    layer.MaxRenderDistance, layer.CullingSphereRadius,
                    layer.AlignToNormal, layer.AlignmentStrength / 100f);
            }
        }
    }

    /// <summary>
    /// Get model GUID from vegetation layer (supports both Prefab and direct Model).
    /// </summary>
    protected Guid? GetModelGuidFromLayer(VegetationLayer layer)
    {
        // Priority 1: Direct ModelGuid
        if (layer.ModelGuid.HasValue && layer.ModelGuid.Value != Guid.Empty)
        {
            Console.WriteLine($"[CoreRenderer] GetModelGuidFromLayer: using direct ModelGuid={layer.ModelGuid.Value}");
            return layer.ModelGuid.Value;
        }

        // Priority 2: Extract from Prefab
        if (layer.PrefabGuid.HasValue && layer.PrefabGuid.Value != Guid.Empty)
        {
            Console.WriteLine($"[CoreRenderer] GetModelGuidFromLayer: trying to extract from PrefabGuid={layer.PrefabGuid.Value}");
            try
            {
                if (AssetDatabase.TryGet(layer.PrefabGuid.Value, out var prefabRecord))
                {
                    Console.WriteLine($"[CoreRenderer] GetModelGuidFromLayer: found prefab at path={prefabRecord.Path}");
                    var prefab = PrefabAsset.Load(prefabRecord.Path);
                    if (prefab?.RootEntity != null)
                    {
                        Console.WriteLine($"[CoreRenderer] GetModelGuidFromLayer: prefab loaded, root={prefab.RootEntity.Name}, children={prefab.RootEntity.Children?.Count ?? 0}");
                        // Search recursively through entity and children for MeshRendererComponent
                        var guid = ExtractMeshGuidFromPrefabEntity(prefab.RootEntity);
                        if (guid.HasValue)
                        {
                            Console.WriteLine($"[CoreRenderer] GetModelGuidFromLayer: extracted mesh guid={guid.Value}");
                            return guid;
                        }
                        else
                        {
                            Console.WriteLine($"[CoreRenderer] GetModelGuidFromLayer: no mesh guid found in prefab hierarchy");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[CoreRenderer] GetModelGuidFromLayer: prefab RootEntity is null");
                    }
                }
                else
                {
                    Console.WriteLine($"[CoreRenderer] GetModelGuidFromLayer: prefab not found in AssetDatabase");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CoreRenderer] GetModelGuidFromLayer prefab load error: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine($"[CoreRenderer] GetModelGuidFromLayer: no ModelGuid or PrefabGuid set");
        }

        return null;
    }

    /// <summary>
    /// Recursively search prefab entity hierarchy for MeshRendererComponent with customMeshGuid
    /// </summary>
    private Guid? ExtractMeshGuidFromPrefabEntity(PrefabEntityData entity)
    {
        // Check this entity's components
        if (entity.Components != null && entity.Components.TryGetValue("MeshRendererComponent", out var meshRendererJson))
        {
            // Try "customMeshGuid" (lowercase, as stored in JSON)
            if (meshRendererJson.TryGetProperty("customMeshGuid", out var guidElement))
            {
                string? guidStr = guidElement.GetString();
                if (!string.IsNullOrEmpty(guidStr) && Guid.TryParse(guidStr, out var modelGuid) && modelGuid != Guid.Empty)
                    return modelGuid;
            }
            // Also try "CustomMeshGuid" (PascalCase fallback)
            if (meshRendererJson.TryGetProperty("CustomMeshGuid", out guidElement))
            {
                string? guidStr = guidElement.GetString();
                if (!string.IsNullOrEmpty(guidStr) && Guid.TryParse(guidStr, out var modelGuid) && modelGuid != Guid.Empty)
                    return modelGuid;
            }
        }

        // Recursively check children
        if (entity.Children != null)
        {
            foreach (var child in entity.Children)
            {
                var guid = ExtractMeshGuidFromPrefabEntity(child);
                if (guid.HasValue)
                    return guid;
            }
        }

        return null;
    }

    #endregion

    #region Resize

    /// <summary>
    /// Resize framebuffers. Call when viewport size changes.
    /// </summary>
    public virtual void Resize(int width, int height)
    {
        if (_width == width && _height == height) return;

        _width = Math.Max(1, width);
        _height = Math.Max(1, height);

        // Resize main framebuffer textures
        GL.BindTexture(TextureTarget.Texture2D, _colorTexture);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f, _width, _height, 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);

        GL.BindTexture(TextureTarget.Texture2D, _depthTexture);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent24, _width, _height, 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);

        // Resize post-processing textures
        GL.BindTexture(TextureTarget.Texture2D, _postTex);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f, _width, _height, 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);

        GL.BindTexture(TextureTarget.Texture2D, _postTex2);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f, _width, _height, 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);

        GL.BindTexture(TextureTarget.Texture2D, 0);

        Console.WriteLine($"[CoreRenderer] Resized to {_width}x{_height}");
    }

    #endregion

    #region Global Uniforms

    /// <summary>
    /// Build and upload GlobalUniforms to the UBO.
    /// </summary>
    protected virtual GlobalUniforms BuildGlobalUniforms()
    {
        var uniforms = new GlobalUniforms
        {
            ViewMatrix = _viewMatrix,
            ProjectionMatrix = _projectionMatrix,
            ViewProjectionMatrix = _viewMatrix * _projectionMatrix,
            CameraPosition = _cameraPosition,

            // Defaults
            DirLightDirection = new Vector3(0, -1, 0),
            DirLightColor = new Vector3(1, 1, 1),
            DirLightIntensity = 1.0f,
            PointLightCount = 0,
            SpotLightCount = 0,
            AmbientColor = new Vector3(0.3f, 0.3f, 0.3f),
            AmbientIntensity = 1.0f,
            SkyboxTint = new Vector3(1, 1, 1),
            SkyboxExposure = 1.0f,

            FogEnabled = 0,
            FogDensity = 0.0f,
            FogOpacity = 1.0f,
            FogNoiseScale = 0.1f,
            FogColor = new Vector3(0.7f, 0.7f, 0.8f),
            FogStart = 0.0f,
            FogEnd = 300.0f,
            FogNoiseSpeed = 0.5f,
            FogLayerHeight = 50.0f,
            FogThickness = 0.5f, // 0.0-1.0 range (was incorrectly 100.0f)
            FogFBMOctaves = 4,
            FogFBMLacunarity = 2.0f,
            FogFBMGain = 0.5f,
            FogScattering = 0.5f,
            FogColorMode = 0, // 0 = Custom, 1 = Ambient, 2 = Skybox, 3 = IBL

            ClipPlaneEnabled = 0,

            Time = (float)_timeStopwatch.Elapsed.TotalSeconds,
            TimeOfDay = 12.0f,
            DayNightBlend = 1.0f,
            GoldenHourBlend = 0.0f,

            WindDirection = new Vector2(1, 0),
            WindStrength = 0.0f,
            WindSpeed = 1.0f,
            WindGustiness = 0.0f,

            BranchAmplitude = 2.5f,
            BranchSpeed = 4.0f,
            BranchTurbulence = 0.8f,
            TrunkStiffness = 0.85f,
            TrunkBendAmount = 0.3f,
            LeafFlutter = 0.6f,
            LeafFlutterSpeed = 8.0f,

            RainIntensity = 0.0f,
            SnowAccumulation = 0.0f,
            SnowIntensity = 0.0f,
            Wetness = 0.0f,

            SnowSlopeMin = 0.0f,
            SnowSlopeMax = 45.0f,
            SnowSparkle = 0.5f,
            SnowDisplacement = 0.5f
        };

        if (_scene == null) return uniforms;

        // Get scene components
        TimeComponent? timeComponent = null;
        WeatherComponent? weatherComponent = null;
        EnvironmentSettings? envSettings = null;

        foreach (var entity in _scene.Entities.Where(e => e.Active))
        {
            if (timeComponent == null)
                timeComponent = entity.GetComponent<TimeComponent>();
            if (weatherComponent == null)
                weatherComponent = entity.GetComponent<WeatherComponent>();
            if (envSettings == null)
                envSettings = entity.GetComponent<EnvironmentSettings>();

            if (timeComponent != null && weatherComponent != null && envSettings != null)
                break;
        }

        // Override with TimeComponent
        if (timeComponent != null)
        {
            uniforms.TimeOfDay = timeComponent.TimeOfDay;
            uniforms.DayNightBlend = timeComponent.GetDayNightBlend();
            uniforms.GoldenHourBlend = timeComponent.GetGoldenHourBlend();
        }

        // Override with WeatherComponent
        if (weatherComponent != null)
        {
            var windDir = weatherComponent.GetWindDirection();
            uniforms.WindDirection = new Vector2(windDir.X, windDir.Y);
            uniforms.WindStrength = weatherComponent.WindStrength;
            uniforms.WindSpeed = weatherComponent.WindSpeed;
            uniforms.WindGustiness = weatherComponent.WindGustiness;

            uniforms.BranchAmplitude = weatherComponent.BranchAmplitude;
            uniforms.BranchSpeed = weatherComponent.BranchSpeed;
            uniforms.BranchTurbulence = weatherComponent.BranchTurbulence;
            uniforms.TrunkStiffness = weatherComponent.TrunkStiffness;
            uniforms.TrunkBendAmount = weatherComponent.TrunkBendAmount;
            uniforms.LeafFlutter = weatherComponent.LeafFlutter;
            uniforms.LeafFlutterSpeed = weatherComponent.LeafFlutterSpeed;

            uniforms.RainIntensity = weatherComponent.RainIntensity;
            uniforms.SnowAccumulation = weatherComponent.SnowAccumulation;
            uniforms.SnowIntensity = weatherComponent.SnowIntensity;
            uniforms.Wetness = weatherComponent.Wetness;

            uniforms.SnowSlopeMin = weatherComponent.SnowSlopeMin;
            uniforms.SnowSlopeMax = weatherComponent.SnowSlopeMax;
            uniforms.SnowSparkle = weatherComponent.SnowSparkle;
            uniforms.SnowDisplacement = weatherComponent.SnowDisplacement;

            if (weatherComponent.FogEnabled)
            {
                uniforms.FogEnabled = 1;
                uniforms.FogDensity = weatherComponent.FogDensity;
                uniforms.FogOpacity = weatherComponent.FogOpacity;
                uniforms.FogNoiseScale = weatherComponent.FogNoiseScale;
                uniforms.FogColor = new Vector3(weatherComponent.FogColor.X, weatherComponent.FogColor.Y, weatherComponent.FogColor.Z);
                uniforms.FogStart = weatherComponent.FogStart;
                uniforms.FogEnd = weatherComponent.FogEnd;
                uniforms.FogNoiseSpeed = weatherComponent.FogNoiseSpeed;
                uniforms.FogLayerHeight = weatherComponent.FogLayerHeight;
                uniforms.FogThickness = weatherComponent.FogThickness;
                uniforms.FogFBMOctaves = weatherComponent.FogFBMOctaves;
                uniforms.FogFBMLacunarity = weatherComponent.FogFBMLacunarity;
                uniforms.FogFBMGain = weatherComponent.FogFBMGain;
                uniforms.FogScattering = weatherComponent.FogScattering;
                uniforms.FogColorMode = (int)weatherComponent.FogColorMode;
            }
        }

        // Override with EnvironmentSettings
        if (envSettings != null)
        {
            uniforms.AmbientColor = new Vector3(envSettings.AmbientColor.X, envSettings.AmbientColor.Y, envSettings.AmbientColor.Z);
            uniforms.AmbientIntensity = envSettings.AmbientIntensity;
            uniforms.SkyboxTint = new Vector3(envSettings.SkyboxTint.X, envSettings.SkyboxTint.Y, envSettings.SkyboxTint.Z);
            uniforms.SkyboxExposure = envSettings.SkyboxExposure;
        }

        // Get directional light
        foreach (var entity in _scene.Entities.Where(e => e.Active))
        {
            var light = entity.GetComponent<LightComponent>();
            if (light != null && light.Enabled && light.Type == LightType.Directional)
            {
                uniforms.DirLightDirection = light.Direction;
                uniforms.DirLightColor = new Vector3(light.Color.X, light.Color.Y, light.Color.Z);
                uniforms.DirLightIntensity = light.Intensity;
                break;
            }
        }

        return uniforms;
    }

    protected void UploadGlobalUniforms(ref GlobalUniforms uniforms)
    {
        GL.BindBuffer(BufferTarget.UniformBuffer, _globalUBO);
        GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, Marshal.SizeOf<GlobalUniforms>(), ref uniforms);
        GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, _globalUBO);
    }

    #endregion

    #region Abstract Methods

    /// <summary>
    /// Main render method. Must be implemented by derived classes.
    /// </summary>
    public abstract void RenderFrame();

    #endregion

    #region Culling Helpers

    protected static void ApplyCullingMode(CullingMode cullingMode)
    {
        switch (cullingMode)
        {
            case CullingMode.Back:
                GL.Enable(EnableCap.CullFace);
                GL.CullFace(TriangleFace.Back);
                break;
            case CullingMode.Front:
                GL.Enable(EnableCap.CullFace);
                GL.CullFace(TriangleFace.Front);
                break;
            case CullingMode.None:
                GL.Disable(EnableCap.CullFace);
                break;
        }
    }

    protected static void RestoreDefaultCulling()
    {
        GL.Enable(EnableCap.CullFace);
        GL.CullFace(TriangleFace.Back);
    }

    #endregion

    #region Dispose

    public virtual void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Dispose specialized renderers
        try { _terrainRenderer?.Dispose(); } catch { }
        try { _vegetationRenderer?.Dispose(); } catch { }
        try { _particleRenderer?.Dispose(); } catch { }
        try { _shadowManager?.Dispose(); } catch { }
        try { _skyboxRenderer?.Dispose(); } catch { }
        try { _waterPlaneRenderer?.Dispose(); } catch { }
        try { _grassRenderer?.Dispose(); } catch { }
        try { _rockRenderer?.Dispose(); } catch { }

        // Dispose mesh caches
        foreach (var mesh in _customMeshCache.Values)
        {
            GL.DeleteVertexArray(mesh.VAO);
            GL.DeleteBuffer(mesh.VBO);
            if (mesh.EBO != 0) GL.DeleteBuffer(mesh.EBO);
        }
        _customMeshCache.Clear();

        foreach (var mesh in _meshCache.Values)
        {
            GL.DeleteVertexArray(mesh.VAO);
            GL.DeleteBuffer(mesh.VBO);
            if (mesh.EBO != 0) GL.DeleteBuffer(mesh.EBO);
        }
        _meshCache.Clear();

        // Dispose framebuffers
        if (_framebuffer != 0) { GL.DeleteFramebuffer(_framebuffer); _framebuffer = 0; }
        if (_colorTexture != 0) { GL.DeleteTexture(_colorTexture); _colorTexture = 0; }
        if (_depthTexture != 0) { GL.DeleteTexture(_depthTexture); _depthTexture = 0; }
        if (_postFbo != 0) { GL.DeleteFramebuffer(_postFbo); _postFbo = 0; }
        if (_postTex != 0) { GL.DeleteTexture(_postTex); _postTex = 0; }
        if (_postFbo2 != 0) { GL.DeleteFramebuffer(_postFbo2); _postFbo2 = 0; }
        if (_postTex2 != 0) { GL.DeleteTexture(_postTex2); _postTex2 = 0; }

        // Dispose UBO
        if (_globalUBO != 0) { GL.DeleteBuffer(_globalUBO); _globalUBO = 0; }

        Console.WriteLine("[CoreRenderer] Disposed");
    }

    #endregion
}

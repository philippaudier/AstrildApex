using System;
using System.Collections.Generic;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Engine.Assets;
using Engine.Components;

namespace Engine.Rendering
{
    /// <summary>
    /// High-performance vegetation renderer using GPU instancing.
    /// Renders thousands of vegetation instances (trees, rocks, grass) with a single draw call per mesh.
    /// Inspired by Unity's Terrain Detail system, Unreal's Foliage Instanced Static Mesh, and Godot's MultiMesh.
    /// </summary>
    public sealed class VegetationRenderer : IDisposable
    {
        // === INSTANCE DATA STRUCTURE ===
        
        /// <summary>
        /// Per-instance data sent to GPU (16 floats = 64 bytes per instance).
        /// Layout: Model Matrix (4x4) stored as 4 vec4s in column-major order.
        /// This allows full transformation (position, rotation, scale) per instance.
        /// </summary>
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
        private struct VegetationInstance
        {
            // Model matrix columns (column-major order for OpenGL)
            public Vector4 Column0; // X axis + X position
            public Vector4 Column1; // Y axis + Y position
            public Vector4 Column2; // Z axis + Z position
            public Vector4 Column3; // Translation + W=1

            public static VegetationInstance FromMatrix(Matrix4 matrix)
            {
                // OpenGL expects column-major order (each column = vec4)
                // Matrix4 in OpenTK is row-major, so we need to TRANSPOSE by extracting COLUMNS not rows
                // Column 0 = (M11, M21, M31, M41) = first column of the matrix
                // Column 1 = (M12, M22, M32, M42) = second column
                // Column 2 = (M13, M23, M33, M43) = third column
                // Column 3 should contain the translation when transposing a row-major Matrix4
                // Transpose mapping (row-major -> column-major): columnN = (M1N, M2N, M3N, M4N)
                return new VegetationInstance
                {
                    Column0 = new Vector4(matrix.M11, matrix.M12, matrix.M13, matrix.M14),
                    Column1 = new Vector4(matrix.M21, matrix.M22, matrix.M23, matrix.M24),
                    Column2 = new Vector4(matrix.M31, matrix.M32, matrix.M33, matrix.M34),
                    Column3 = new Vector4(matrix.M41, matrix.M42, matrix.M43, matrix.M44)
                };
            }
        }

        // === BATCHING SYSTEM ===
        
        /// <summary>
        /// A batch groups all instances of the same model+submesh combination.
        /// </summary>
        private class VegetationBatch
        {
            public Guid ModelGuid;
            public int SubmeshIndex;
            public List<Matrix4> Transforms = new(); // All instances (unculled)
            public List<Matrix4> VisibleTransforms = new(); // After culling

            // GPU resources
            public int VAO;
            public int VBO; // Mesh vertex data
            public int EBO; // Mesh index data
            public int InstanceVBO; // Instance transforms
            public int IndexCount;
            public Guid? MaterialGuid; // Material from the model's submesh
            public int InstanceCount => VisibleTransforms.Count; // Use visible count

            // Culling mode from MeshRendererComponent
            public Engine.Components.CullingMode CullingMode = Engine.Components.CullingMode.Back;

            // Culling parameters
            public float MaxRenderDistance = 500f;
            public float CullingSphereRadius = 5f;

            // Normal alignment (for grass/ground cover on slopes)
            public bool AlignToNormal = false;
            public float AlignmentStrength = 1.0f;

            // Track if data needs GPU update
            public bool NeedsGPUUpdate = true;
            public int LastTransformCount = 0;
        }

        // === RENDERER STATE ===

        private readonly Dictionary<string, VegetationBatch> _batches = new();
        // Exposed for diagnostics
        public int BatchCount => _batches.Count;
        private VegetationInstance[] _instanceBuffer = new VegetationInstance[10000];
        private const int MaxInstancesPerBatch = 50000; // Reduced from 100k for better memory usage
        private const int InitialInstanceBufferSize = 10000;

        // PERFORMANCE: Limit total rendered instances per frame to prevent lag
        private const int MaxTotalInstancesPerFrame = 500000; // Cap at 500k instances total (was 100k)
        private const int MaxBatchesPerFrame = 200; // Cap at 200 batches per frame (was 50)

        private ShaderProgram? _vegetationShader = null;
        private ShaderProgram? _shadowDepthShader = null;
        private ShaderProgram? _shadowDepthAlphaClipShader = null;

        // Default white texture for materials without albedo
        private int _defaultWhiteTexture = 0;

        // Frustum culler for visibility testing
        private readonly FrustumCuller _frustumCuller = new();

        private bool _disposed = false;

        // Debug counter for logging
        private static int _renderCallCounter = 0;
        // Throttle for per-batch cull logs to avoid console spam
        private static DateTime _lastCullLogTime = DateTime.MinValue;
        private static readonly TimeSpan CullLogInterval = TimeSpan.FromSeconds(1.0);

        // Culling statistics for debugging
        public int TotalInstances { get; private set; }
        public int VisibleInstances { get; private set; }
        public int CulledInstances => TotalInstances - VisibleInstances;

        // === CONSTRUCTOR ===
        
        public VegetationRenderer()
        {
            LoadShader();
            LoadShadowDepthShader();
            LoadShadowDepthAlphaClipShader();
            CreateDefaultWhiteTexture();
        }

        /// <summary>
        /// Force all batches to be marked for GPU update on next render.
        /// Use this when runtime parameters changed to ensure
        /// instance buffers are uploaded even if transforms references/counts did not change.
        /// </summary>
        public void RefreshAllBatches()
        {
            try
            {
                foreach (var batch in _batches.Values)
                {
                    batch.NeedsGPUUpdate = true;
                }
            }
            catch { }
        }

        private void CreateDefaultWhiteTexture()
        {
            _defaultWhiteTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _defaultWhiteTexture);

            // Create 1x1 white texture
            byte[] whitePixel = new byte[] { 255, 255, 255, 255 };
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, 1, 1, 0,
                PixelFormat.Rgba, PixelType.UnsignedByte, whitePixel);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

            GL.BindTexture(TextureTarget.Texture2D, 0);
        }

        private void LoadShader()
        {
            try
            {
                string vertPath = "Engine/Rendering/Shaders/Forward/VegetationForward.vert";
                string fragPath = "Engine/Rendering/Shaders/Forward/VegetationForward.frag";

                // Check if files exist
                if (!System.IO.File.Exists(vertPath))
                {
                    Console.WriteLine($"[VegetationRenderer] *** Vegetation shader vertex not found: {vertPath} ***");
                    _vegetationShader = null;
                    return;
                }
                if (!System.IO.File.Exists(fragPath))
                {
                    Console.WriteLine($"[VegetationRenderer] *** Vegetation shader fragment not found: {fragPath} ***");
                    _vegetationShader = null;
                    return;
                }

                _vegetationShader = ShaderProgram.FromFiles(vertPath, fragPath);

                if (_vegetationShader == null)
                {
                    Console.WriteLine($"[VegetationRenderer] *** Vegetation shader failed to create program (FromFiles returned null) ***");
                    return;
                }

                // Verify shader program is valid
                GL.GetProgram(_vegetationShader.Handle, GetProgramParameterName.LinkStatus, out int linkStatus);
                if (linkStatus == 0)
                {
                    string infoLog = GL.GetProgramInfoLog(_vegetationShader.Handle);
                    Console.WriteLine($"[VegetationRenderer] *** VegetationForward LINK ERROR ***:\n{infoLog}");
                    _vegetationShader = null;
                    return;
                }

                // Bind Global UBO (binding point 0) if shader defines it so lighting/uniforms work
                try
                {
                    _vegetationShader.Use();
                    int globalBlockIndex = OpenTK.Graphics.OpenGL4.GL.GetUniformBlockIndex(_vegetationShader.Handle, "Global");
                    if (globalBlockIndex != -1) OpenTK.Graphics.OpenGL4.GL.UniformBlockBinding(_vegetationShader.Handle, globalBlockIndex, 0);
                }
                catch { }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VegetationRenderer] *** Vegetation shader load EXCEPTION ***:\n{ex.Message}\n{ex.StackTrace}");
                _vegetationShader = null;
            }
        }

        private void LoadShadowDepthShader()
        {
            try
            {
                string vertPath = "Engine/Rendering/Shaders/Shadow/ShadowDepthInstanced.vert";
                string fragPath = "Engine/Rendering/Shaders/Shadow/ShadowDepth.frag";

                // Check if files exist
                if (!System.IO.File.Exists(vertPath))
                {
                    Console.WriteLine($"[VegetationRenderer] *** Shadow depth shader vertex not found: {vertPath} ***");
                    _shadowDepthShader = null;
                    return;
                }
                if (!System.IO.File.Exists(fragPath))
                {
                    Console.WriteLine($"[VegetationRenderer] *** Shadow depth shader fragment not found: {fragPath} ***");
                    _shadowDepthShader = null;
                    return;
                }

                _shadowDepthShader = ShaderProgram.FromFiles(vertPath, fragPath);

                if (_shadowDepthShader == null)
                {
                    Console.WriteLine($"[VegetationRenderer] *** Shadow depth shader failed to create program (FromFiles returned null) ***");
                    return;
                }

                // Verify shader program is valid
                GL.GetProgram(_shadowDepthShader.Handle, GetProgramParameterName.LinkStatus, out int linkStatus);
                if (linkStatus == 0)
                {
                    string infoLog = GL.GetProgramInfoLog(_shadowDepthShader.Handle);
                    Console.WriteLine($"[VegetationRenderer] *** ShadowDepthInstanced LINK ERROR ***:\n{infoLog}");
                    _shadowDepthShader = null;
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VegetationRenderer] *** Shadow depth shader load EXCEPTION ***:\n{ex.Message}\n{ex.StackTrace}");
                _shadowDepthShader = null;
            }
        }

        private void LoadShadowDepthAlphaClipShader()
        {
            try
            {
                string vertPath = "Engine/Rendering/Shaders/Shadow/ShadowDepthInstanced.vert";
                string fragPath = "Engine/Rendering/Shaders/Shadow/ShadowDepthAlphaClip.frag";

                // Check if files exist
                if (!System.IO.File.Exists(vertPath))
                {
                    Console.WriteLine($"[VegetationRenderer] *** Shadow depth alpha clip shader vertex not found: {vertPath} ***");
                    _shadowDepthAlphaClipShader = null;
                    return;
                }
                if (!System.IO.File.Exists(fragPath))
                {
                    Console.WriteLine($"[VegetationRenderer] *** Shadow depth alpha clip shader fragment not found: {fragPath} ***");
                    _shadowDepthAlphaClipShader = null;
                    return;
                }

                _shadowDepthAlphaClipShader = ShaderProgram.FromFiles(vertPath, fragPath);

                if (_shadowDepthAlphaClipShader == null)
                {
                    Console.WriteLine($"[VegetationRenderer] *** Shadow depth alpha clip shader failed to create program (FromFiles returned null) ***");
                    return;
                }

                // Verify shader program is valid
                GL.GetProgram(_shadowDepthAlphaClipShader.Handle, GetProgramParameterName.LinkStatus, out int linkStatus);
                if (linkStatus == 0)
                {
                    string infoLog = GL.GetProgramInfoLog(_shadowDepthAlphaClipShader.Handle);
                    Console.WriteLine($"[VegetationRenderer] *** ShadowDepthAlphaClip LINK ERROR ***:\n{infoLog}");
                    _shadowDepthAlphaClipShader = null;
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VegetationRenderer] *** Shadow depth alpha clip shader load EXCEPTION ***:\n{ex.Message}\n{ex.StackTrace}");
                _shadowDepthAlphaClipShader = null;
            }
        }

        // === PUBLIC API ===

        /// <summary>
        /// Update a batch for a specific model+submesh with instance transforms and culling parameters.
        /// </summary>
        public void UpdateBatch(Guid modelGuid, int submeshIndex, List<Matrix4>? transforms,
                               Engine.Components.CullingMode cullingMode = Engine.Components.CullingMode.Back,
                               float maxRenderDistance = 500f, float cullingSphereRadius = 5f,
                               bool alignToNormal = false, float alignmentStrength = 1.0f)
        {
            // Treat a null transforms list as empty to avoid callers needing to allocate an empty list
            transforms ??= new List<Matrix4>();

            string batchKey = $"{modelGuid}_{submeshIndex}";

            // Get or create batch
            if (!_batches.TryGetValue(batchKey, out var batch))
            {
                batch = new VegetationBatch
                {
                    ModelGuid = modelGuid,
                    SubmeshIndex = submeshIndex,
                    CullingMode = cullingMode,
                    MaxRenderDistance = maxRenderDistance,
                    CullingSphereRadius = cullingSphereRadius,
                    AlignToNormal = alignToNormal,
                    AlignmentStrength = alignmentStrength
                };
                _batches[batchKey] = batch;
            }
            else
            {
                // Update culling parameters for existing batch
                batch.CullingMode = cullingMode;
                batch.MaxRenderDistance = maxRenderDistance;
                batch.CullingSphereRadius = cullingSphereRadius;
                batch.AlignToNormal = alignToNormal;
                batch.AlignmentStrength = alignmentStrength;

                // DEBUG: Log culling mode updates
                if (_renderCallCounter % 120 == 0)
                {
                    Console.WriteLine($"[VegetationRenderer] UpdateBatch existing: {batchKey}, CullingMode={cullingMode}");
                }
            }

            // Batch is guaranteed to be non-null at this point
            // Only flag for update if:
            // 1. New batch (always needs initial upload)
            // 2. Different transform list reference
            // 3. Transform count changed
            bool isNewBatch = batch.Transforms.Count == 0 && batch.LastTransformCount == 0;
            bool needsUpdate = isNewBatch ||
                             batch.Transforms != transforms ||
                             batch.LastTransformCount != transforms.Count;

            if (needsUpdate)
            {
                // CRITICAL FIX: Clone the list instead of copying the reference
                // This prevents infinite accumulation if the caller continues to modify the list
                batch.Transforms = transforms != null ? new List<Matrix4>(transforms) : new List<Matrix4>();
                batch.LastTransformCount = batch.Transforms.Count;
                batch.NeedsGPUUpdate = true; // Will trigger culling + GPU upload in Render()
            }

            // Load mesh if needed
            if (batch.VAO == 0)
            {
                LoadMeshForBatch(batch);
            }
            else
            {
                // CRITICAL FIX: Reload MaterialGuid AND CullingMode even if mesh is already loaded
                // This ensures material changes (CullingMode, AlphaClipping, etc.) are picked up
                // in infinite streaming mode when batches are updated without full reload
                try
                {
                    var meshAsset = AssetDatabase.LoadMeshAsset(batch.ModelGuid);
                    if (meshAsset != null && meshAsset.SubMeshes != null && batch.SubmeshIndex < meshAsset.SubMeshes.Count)
                    {
                        var submesh = meshAsset.SubMeshes[batch.SubmeshIndex];
                        if (submesh.MaterialIndex >= 0 && submesh.MaterialIndex < meshAsset.MaterialGuids.Count)
                        {
                            var materialGuid = meshAsset.MaterialGuids[submesh.MaterialIndex];
                            batch.MaterialGuid = materialGuid;

                            // CRITICAL: Also reload CullingMode from the material file
                            // Otherwise we use the old CullingMode from the parameter even though MaterialGuid changed
                            if (materialGuid.HasValue && materialGuid.Value != Guid.Empty)
                            {
                                try
                                {
                                    // CRITICAL FIX: Invalidate material cache before loading to ensure fresh values
                                    // This prevents using stale CullingMode from cache in Infinite Streaming mode
                                    AssetDatabase.InvalidateMaterial(materialGuid.Value);

                                    var material = AssetDatabase.LoadMaterial(materialGuid.Value);
                                    if (material != null)
                                    {
                                        batch.CullingMode = (Engine.Components.CullingMode)material.CullingMode;
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore material reload failures - batch will use existing MaterialGuid
                }
            }
        }

        /// <summary>
        /// Render all vegetation batches with wind, weather, and LOD parameters.
        /// </summary>
        public void Render(Matrix4 view, Matrix4 projection, float time,
            float windStrength = 0.3f, Vector2 windDirection = default, float windSpeed = 1.0f, float windGustiness = 0.5f,
            float branchAmplitude = 1.0f, float branchSpeed = 3.0f, float branchTurbulence = 0.5f,
            float trunkStiffness = 0.7f, float trunkBendAmount = 0.5f,
            float leafFlutter = 0.3f, float leafFlutterSpeed = 5.0f,
            float rainIntensity = 0.0f, float snowAccumulation = 0.0f, float snowIntensity = 0.0f, float wetness = 0.0f,
            float snowSlopeMin = 0.0f, float snowSlopeMax = 45.0f, float snowSparkle = 0.5f, float snowDisplacement = 0.02f,
            Vector3 cameraPos = default, Vector3 lightDir = default, Vector3 lightColor = default, Vector3 ambientColor = default,
            uint objectId = 0,
            // Shadow parameters
            int shadowTexture = 0, Matrix4? shadowMatrix = null, bool useShadows = false,
            float shadowBias = 0.001f, float shadowMapSize = 2048f, float shadowStrength = 0.7f,
            int shadowQuality = 1, float shadowNormalBias = 0.001f, int pcfSamples = 9, float lightSize = 0.05f)
        {
            using (Profiling.Profiler.Profile("VegetationRenderer.Render"))
            {
                Profiling.GPUProfiler.BeginGPUScope("VegetationRenderer");
                Profiling.RenderProfiler.BeginRenderPass("Vegetation");

                if (_batches.Count == 0)
                {
                    // No batches to render (this is normal if no vegetation was generated)
                    Profiling.RenderProfiler.EndRenderPass();
                    Profiling.GPUProfiler.EndGPUScope();
                    return;
                }

                if (_vegetationShader == null)
            {
                Engine.Utils.DebugLogger.Log("[VegetationRenderer] Cannot render vegetation - shader is null; attempting reload.");
                LoadShader();
                if (_vegetationShader == null)
                {
                    Engine.Utils.DebugLogger.Log("[VegetationRenderer] Vegetation shader reload failed!");
                    return;
                }
            }

            // CRITICAL FIX: Ensure correct winding order for vegetation rendering
            // Skybox and other renderers may have changed this to CW, so reset to CCW (standard for imported models)
            GL.FrontFace(FrontFaceDirection.Ccw);

            // Fetch authoritative weather state from WeatherManager to avoid caller desync
            try
            {
                var wm = Engine.Systems.WeatherManager.GetCurrentWeather();
                if (wm == null) throw new InvalidOperationException("WeatherManager returned null weather state");

                _renderCallCounter++;

                // Override caller-supplied weather params to ensure consistent, thread-safe values
                windStrength = wm.WindStrength;
                windDirection = new OpenTK.Mathematics.Vector2(wm.GetWindDirection().X, wm.GetWindDirection().Y);
                windSpeed = wm.WindSpeed;
                windGustiness = wm.WindGustiness;

                branchAmplitude = wm.BranchAmplitude;
                branchSpeed = wm.BranchSpeed;
                branchTurbulence = wm.BranchTurbulence;
                trunkStiffness = wm.TrunkStiffness;
                trunkBendAmount = wm.TrunkBendAmount;
                leafFlutter = wm.LeafFlutter;
                leafFlutterSpeed = wm.LeafFlutterSpeed;

                rainIntensity = wm.RainIntensity;
                snowAccumulation = wm.SnowAccumulation;
                snowIntensity = wm.SnowIntensity;
                wetness = wm.Wetness;

                snowSlopeMin = wm.SnowSlopeMin;
                snowSlopeMax = wm.SnowSlopeMax;
                snowSparkle = wm.SnowSparkle;
                snowDisplacement = wm.SnowDisplacement;
            }
            catch { }

            // We'll select and bind the shader per-batch (to allow per-material shader overrides)
            // The Global UBO (binding = 0) must already be bound by the caller (ViewportRenderer).

            // Extract frustum planes from view-projection matrix for culling
            // Use View * Projection order (same as grass/rocks which works correctly)
            Matrix4 viewProjection = view * projection;
            _frustumCuller.ExtractPlanes(viewProjection);

            // Reset culling statistics
            TotalInstances = 0;
            VisibleInstances = 0;

            // OPTIMIZATION: Sort batches by distance to camera (render closest first)
            var sortedBatches = _batches.Values
                .Where(b => b.Transforms.Count > 0 && b.VAO != 0)
                .Select(b => {
                    // Calculate batch center (average of all instance positions)
                    Vector3 center = Vector3.Zero;
                    int sampleCount = Math.Min(b.Transforms.Count, 10); // Sample first 10 instances for speed
                    for (int i = 0; i < sampleCount; i++)
                    {
                        center.X += b.Transforms[i].M41;
                        center.Y += b.Transforms[i].M42;
                        center.Z += b.Transforms[i].M43;
                    }
                    center /= sampleCount;
                    float distSqr = (center - cameraPos).LengthSquared;
                    return (batch: b, distSqr: distSqr);
                })
                .OrderBy(x => x.distSqr)
                .ToList();

            // Render each batch (sorted by distance, closest first)
            int batchesRendered = 0;
            int instancesRendered = 0;
            int totalInstancesThisFrame = 0;

            foreach (var (batch, batchDistSqr) in sortedBatches)
            {
                TotalInstances += batch.Transforms.Count;

                // Perform frustum and distance culling to populate VisibleTransforms
                CullBatch(batch, cameraPos);

                // Update GPU buffer only if data changed (lazy update)
                if (batch.NeedsGPUUpdate)
                {
                    UpdateInstanceBuffer(batch);
                    batch.NeedsGPUUpdate = false;
                }

                VisibleInstances += batch.InstanceCount;

                if (batch.InstanceCount == 0) continue;

                // OPTIMIZATION: Stop rendering if we hit frame budget limits
                if (batchesRendered >= MaxBatchesPerFrame)
                {
                    // Too many batches this frame, skip remaining batches
                    continue;
                }

                if (totalInstancesThisFrame + batch.InstanceCount > MaxTotalInstancesPerFrame)
                {
                    // Would exceed instance budget, skip this batch
                    continue;
                }

                totalInstancesThisFrame += batch.InstanceCount;

                // Choose shader program for this batch. Prefer material's declared shader when available
                ShaderProgram shToUse = _vegetationShader!; // fallback
                Engine.Assets.MaterialAsset? matAsset = null;
                if (batch.MaterialGuid.HasValue)
                {
                    try
                    {
                        matAsset = AssetDatabase.LoadMaterial(batch.MaterialGuid.Value);
                        if (matAsset != null && !string.IsNullOrEmpty(matAsset.Shader))
                        {
                            var alt = Engine.Rendering.ShaderLibrary.GetShaderByName(matAsset.Shader);
                            if (alt != null) shToUse = alt;
                        }
                    }
                    catch { }
                }

                if (shToUse == null)
                {
                    // No valid shader to use for this batch
                    continue;
                }

                // Bind the selected shader for this draw
                GL.UseProgram(shToUse.Handle);

                // Verify wind-related uniform locations exist for this shader.
                try
                {
                    // If the selected material shader does not expose vegetation wind uniforms,
                    // prefer using our built-in vegetation shader so wind animation works without
                    // requiring a manual regenerate. Also attempt reload if our built-in shader
                    // itself is missing the uniforms (compile race).
                    int checkLoc2 = GL.GetUniformLocation(shToUse.Handle, "u_WindStrength");
                    if (checkLoc2 < 0)
                    {
                        // If built-in vegetation shader exists, switch to it
                        if (_vegetationShader != null && shToUse != _vegetationShader)
                        {
                            shToUse = _vegetationShader;
                            GL.UseProgram(shToUse.Handle);
                        }
                        else if (ReferenceEquals(shToUse, _vegetationShader))
                        {
                            // If our built-in shader is the one missing uniforms, try reloading it
                            LoadShader();
                            if (_vegetationShader != null)
                            {
                                shToUse = _vegetationShader;
                                GL.UseProgram(shToUse.Handle);
                            }
                        }
                    }
                }
                catch { }

                // Set shadow uniforms (must be set before material binding to ensure shadow map is bound)
                try
                {
                    if (useShadows && shadowTexture != 0 && shadowMatrix.HasValue)
                    {
                        shToUse.SetInt("u_UseShadows", 1);
                        shToUse.SetFloat("u_ShadowBias", shadowBias);
                        shToUse.SetFloat("u_ShadowMapSize", shadowMapSize);
                        shToUse.SetFloat("u_ShadowStrength", shadowStrength);

                        // Shadow quality uniforms (improved shadow system - Levels 1 & 2)
                        shToUse.SetInt("u_ShadowQuality", shadowQuality); // 0 = PCF Grid, 1 = Rotated Poisson, 2 = PCSS
                        shToUse.SetFloat("u_ShadowNormalBias", shadowNormalBias);
                        shToUse.SetFloat("u_LightSize", lightSize);
                        shToUse.SetInt("u_PCFSamples", pcfSamples);

                        // Bind shadow map texture to unit 17 (same as PBR shader)
                        GL.ActiveTexture(TextureUnit.Texture17);
                        GL.BindTexture(TextureTarget.Texture2D, shadowTexture);
                        shToUse.SetInt("u_ShadowMap", 17);

                        // Set shadow matrix
                        shToUse.SetMat4("u_ShadowMatrix", shadowMatrix.Value);
                    }
                    else
                    {
                        shToUse.SetInt("u_UseShadows", 0);
                    }
                }
                catch { }

                // Set some basic vegetation uniforms now (these may be overridden by MaterialRuntime.Bind)
                    try
                    {
                        shToUse.SetUInt("u_ObjectId", objectId);
                        shToUse.SetFloat("u_Time", time);
                        // Other wind/weather uniforms will be set again AFTER material binding to ensure
                        // MaterialRuntime doesn't accidentally clobber them.
                    }
                    catch { }

                // Bind material runtime (textures + per-material uniforms) if available
                Engine.Rendering.MaterialRuntime? mr = null;
                if (batch.MaterialGuid.HasValue && matAsset != null)
                {
                    // Use MaterialRuntime to bind all textures/uniforms for the selected shader
                    try
                    {
                        Func<Guid, string?> resolver = g => AssetDatabase.TryGet(g, out var r) ? r.Path : null;
                        mr = Engine.Rendering.MaterialRuntime.FromAsset(matAsset, resolver);
                        mr.Bind(shToUse, time);
                    }
                    catch (Exception ex)
                    {
                        Engine.Utils.DebugLogger.Log($"[VegetationRenderer] Failed to bind material runtime: {ex.Message}");
                        // Fallback: bind default white
                        GL.ActiveTexture(TextureUnit.Texture0);
                        GL.BindTexture(TextureTarget.Texture2D, _defaultWhiteTexture);
                        try { shToUse.SetInt("u_AlbedoTex", 0); } catch { }
                    }
                }
                else
                {
                    // No material -> bind default white albedo
                    GL.ActiveTexture(TextureUnit.Texture0);
                    GL.BindTexture(TextureTarget.Texture2D, _defaultWhiteTexture);
                    try { shToUse.SetInt("u_AlbedoTex", 0); } catch { }
                }

                // CRITICAL FIX: DON'T set individual uniforms for weather/time!
                // The shader reads uTime, uWindStrength, etc. from the Global UBO (binding=0).
                // Setting individual u_Time/u_WindStrength uniforms CONFLICTS with the UBO!
                // The Global UBO is already updated in UpdateGlobalUniforms() and bound at binding point 0.
                
                // HOWEVER: We MUST set u_WindMode and local wind parameters so the shader knows
                // whether to use Global UBO or material-specific parameters!
                try
                {
                    if (matAsset?.VegetationProperties != null)
                    {
                        var veg = matAsset.VegetationProperties;
                        
                        // Set wind mode (0=Global, 1=Local, 2=Blend)
                        shToUse.SetInt("u_WindMode", veg.WindMode);
                        
                        // Set blend factor (only used when WindMode=2)
                        shToUse.SetFloat("u_WindBlendFactor", veg.WindBlendFactor);
                        
                        // Set local wind parameters (used when WindMode=1 or 2)
                        shToUse.SetFloat("u_WindStrength_Local", veg.WindStrength);
                        shToUse.SetVec2("u_WindDirection_Local", new OpenTK.Mathematics.Vector2(veg.WindDirection[0], veg.WindDirection[1]));
                        shToUse.SetFloat("u_WindSpeed_Local", veg.WindSpeed);
                        shToUse.SetFloat("u_WindGustiness_Local", veg.WindGustiness);
                        shToUse.SetFloat("u_BranchAmplitude_Local", veg.BranchAmplitude);
                        shToUse.SetFloat("u_BranchSpeed_Local", veg.BranchSpeed);
                        shToUse.SetFloat("u_BranchTurbulence_Local", veg.BranchTurbulence);
                        shToUse.SetFloat("u_TrunkStiffness_Local", veg.TrunkStiffness);
                        shToUse.SetFloat("u_TrunkBendAmount_Local", veg.TrunkBendAmount);
                        shToUse.SetFloat("u_LeafFlutter_Local", veg.LeafFlutter);
                        shToUse.SetFloat("u_LeafFlutterSpeed_Local", veg.LeafFlutterSpeed);
                    }
                    else
                    {
                        // No VegetationProperties - default to Global mode
                        shToUse.SetInt("u_WindMode", 0);
                    }
                }
                catch (Exception ex)
                {
                    Engine.Utils.DebugLogger.Log($"[VegetationRenderer] Failed to set wind mode uniforms: {ex.Message}");
                }
                
                try
                {
                    // REMOVED: shToUse.SetFloat("u_Time", time);
                    // REMOVED: shToUse.SetFloat("u_WindStrength", windStrength);
                    // ... all weather/time uniforms are now read from Global UBO
                    
                    // Snow parameters are still set individually (not in Global UBO structure yet)
                    shToUse.SetFloat("u_SnowSlopeMin", snowSlopeMin);
                    shToUse.SetFloat("u_SnowSlopeMax", snowSlopeMax);
                    shToUse.SetFloat("u_SnowSparkle", snowSparkle);
                    shToUse.SetFloat("u_SnowDisplacement", snowDisplacement);

                    // CRITICAL: Bind default snow textures FIRST (always) to avoid shader crash
                    BindDefaultSnowTextures(shToUse);

                    // Load and bind snow material if assigned (will replace defaults if available)
                    try
                    {
                        var wm = Engine.Systems.WeatherManager.GetCurrentWeather();

                        if (wm != null && wm.SnowMapMaterial.HasValue)
                        {
                            var snowMat = AssetDatabase.LoadMaterial(wm.SnowMapMaterial.Value);
                            if (snowMat != null)
                            {
                                // Load snow material runtime from cache
                                // Cache is updated by ApplyLiveMaterialUpdate when inspector changes values
                                Func<Guid, string?> snowResolver = g => AssetDatabase.TryGet(g, out var r) ? r.Path : null;
                                var snowRuntime = Engine.Rendering.MaterialRuntime.FromAsset(snowMat, snowResolver);

                                // CRITICAL FIX: Use units 13-15 to avoid conflict with IBL textures (10-12)
                                // Bind snow textures to dedicated texture units
                                GL.ActiveTexture(TextureUnit.Texture13);
                                GL.BindTexture(TextureTarget.Texture2D, snowRuntime.AlbedoTex);
                                shToUse.SetInt("u_SnowAlbedoTex", 13);

                                GL.ActiveTexture(TextureUnit.Texture14);
                                GL.BindTexture(TextureTarget.Texture2D, snowRuntime.NormalTex);
                                shToUse.SetInt("u_SnowNormalTex", 14);

                                GL.ActiveTexture(TextureUnit.Texture15);
                                GL.BindTexture(TextureTarget.Texture2D, snowRuntime.MetallicRoughnessTex);
                                shToUse.SetInt("u_SnowMetallicRoughnessTex", 15);

                                // Send snow material properties
                                shToUse.SetVec4("u_SnowAlbedoColor", new OpenTK.Mathematics.Vector4(
                                    snowRuntime.AlbedoColor[0], snowRuntime.AlbedoColor[1],
                                    snowRuntime.AlbedoColor[2], snowRuntime.AlbedoColor[3]));
                                shToUse.SetFloat("u_SnowMetallic", snowRuntime.Metallic);
                                shToUse.SetFloat("u_SnowRoughness", 1.0f - snowRuntime.Smoothness);
                                shToUse.SetFloat("u_SnowNormalStrength", snowRuntime.NormalStrength);

                                // Use material's texture tiling instead of hardcoded value
                                shToUse.SetVec2("u_SnowTextureTiling", new Vector2(
                                    snowRuntime.TextureTiling[0],
                                    snowRuntime.TextureTiling[1]));
                            }
                            else
                            {
                                BindDefaultSnowTextures(shToUse);
                            }
                        }
                        else
                        {
                            BindDefaultSnowTextures(shToUse);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"[VegetationRenderer] Error loading snow material: {ex.Message}");
                        BindDefaultSnowTextures(shToUse);
                    }

                    // CRITICAL: Restore texture unit 0 as active after binding snow textures
                    GL.ActiveTexture(TextureUnit.Texture0);

                    // NOTE: Do NOT override material parameters here!
                    // MaterialRuntime.Bind() already set all material parameters (tiling, offset, albedo color, etc.)
                    // Overriding them here would break material settings in the inspector
                }
                catch { }

                // CRITICAL FIX: Re-bind shadow map AFTER all material/texture bindings
                // MaterialRuntime.Bind() may have changed texture unit states, so we ensure
                // the shadow map is still bound to the correct unit before drawing
                try
                {
                    if (useShadows && shadowTexture != 0 && shadowMatrix.HasValue)
                    {
                        GL.ActiveTexture(TextureUnit.Texture17);
                        GL.BindTexture(TextureTarget.Texture2D, shadowTexture);
                        shToUse.SetInt("u_ShadowMap", 17);

                        // Restore active texture unit to 0
                        GL.ActiveTexture(TextureUnit.Texture0);
                    }
                }
                catch { }

                // === TRANSPARENCY HANDLING ===
                // Enable/disable blending based on material TransparencyMode (CRITICAL FIX)
                bool isTransparent = mr != null && mr.TransparencyMode != 0;
                if (isTransparent)
                {
                    GL.Enable(EnableCap.Blend);
                    GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                    GL.DepthMask(false); // Transparent objects don't write to depth buffer
                }
                else
                {
                    GL.Disable(EnableCap.Blend);
                    GL.DepthMask(true); // Opaque objects write to depth buffer
                }

                // Apply culling mode from MeshRenderer
                switch (batch.CullingMode)
                {
                    case Engine.Components.CullingMode.Back:
                        GL.Enable(EnableCap.CullFace);
                        GL.CullFace(TriangleFace.Back);
                        break;
                    case Engine.Components.CullingMode.Front:
                        GL.Enable(EnableCap.CullFace);
                        GL.CullFace(TriangleFace.Front);
                        break;
                    case Engine.Components.CullingMode.None:
                        GL.Disable(EnableCap.CullFace);
                        break;
                }

                // Set distance fade dithering parameters
                shToUse.SetFloat("u_MaxRenderDistance", batch.MaxRenderDistance);
                // Fade range is 20% of max distance (configurable later if needed)
                float ditherFadeRange = batch.MaxRenderDistance > 0 ? batch.MaxRenderDistance * 0.2f : 0f;
                shToUse.SetFloat("u_DitherFadeRange", ditherFadeRange);

                // Set normal alignment parameters (for grass/ground cover on slopes)
                shToUse.SetFloat("u_AlignToNormal", batch.AlignToNormal ? 1.0f : 0.0f);
                shToUse.SetFloat("u_AlignmentStrength", batch.AlignmentStrength);

                // Draw instanced
                GL.BindVertexArray(batch.VAO);
                GL.DrawElementsInstanced(
                    PrimitiveType.Triangles,
                    batch.IndexCount,
                    DrawElementsType.UnsignedInt,
                    IntPtr.Zero,
                    batch.InstanceCount
                );

                // Record profiling stats for this batch
                int trianglesRendered = (batch.IndexCount / 3) * batch.InstanceCount;
                Profiling.RenderProfiler.RecordDrawCall(batch.InstanceCount, trianglesRendered);

                int culled = batch.Transforms.Count - batch.VisibleTransforms.Count;
                Profiling.RenderProfiler.RecordInstancingBatch(
                    $"Vegetation_{batch.ModelGuid.ToString().Substring(0, 8)}",
                    batch.VisibleTransforms.Count,
                    trianglesRendered,
                    culled
                );

                batchesRendered++;
                instancesRendered += batch.InstanceCount;
            }

                // CRITICAL: Restore OpenGL state
                GL.BindVertexArray(0);
                GL.UseProgram(0);
                GL.Disable(EnableCap.Blend); // Ensure blending is off after rendering
                GL.DepthMask(true); // Restore depth writing

                Profiling.RenderProfiler.EndRenderPass();
                Profiling.GPUProfiler.EndGPUScope();
            } // End using Profiler.Profile
        }

        /// <summary>
        /// Render vegetation instances to the shadow map.
        /// Called during the shadow pass to make vegetation cast shadows.
        /// </summary>
        public void RenderShadowPass(Matrix4 lightSpaceMatrix, Vector3 cameraPos, float time = 0f)
        {
            if (_batches.Count == 0) return;

            if (_shadowDepthShader == null)
            {
                // Try reloading shader
                LoadShadowDepthShader();
                if (_shadowDepthShader == null)
                {
                    Engine.Utils.DebugLogger.Log("[VegetationRenderer] Cannot render shadow pass - shadow depth shader is null");
                    return;
                }
            }

            if (_shadowDepthAlphaClipShader == null)
            {
                // Try reloading alpha clip shader
                LoadShadowDepthAlphaClipShader();
                if (_shadowDepthAlphaClipShader == null)
                {
                    Engine.Utils.DebugLogger.Log("[VegetationRenderer] Cannot render shadow pass - alpha clip shader is null");
                    return;
                }
            }

            // Fetch authoritative weather state from WeatherManager (same as forward pass)
            float windStrength = 0f, windSpeed = 1f, windGustiness = 0f;
            Vector2 windDirection = Vector2.Zero;
            float branchAmplitude = 1f, branchSpeed = 3f, branchTurbulence = 0.5f;
            float trunkStiffness = 0.7f, trunkBendAmount = 0.5f;
            float leafFlutter = 0.3f, leafFlutterSpeed = 5f;
            float rainIntensity = 0f, snowAccumulation = 0f, snowIntensity = 0f;
            float snowSlopeMin = 0f, snowSlopeMax = 45f, snowDisplacement = 0.02f;

            try
            {
                var wm = Engine.Systems.WeatherManager.GetCurrentWeather();
                if (wm != null)
                {
                    windStrength = wm.WindStrength;
                    windDirection = new Vector2(wm.GetWindDirection().X, wm.GetWindDirection().Y);
                    windSpeed = wm.WindSpeed;
                    windGustiness = wm.WindGustiness;

                    branchAmplitude = wm.BranchAmplitude;
                    branchSpeed = wm.BranchSpeed;
                    branchTurbulence = wm.BranchTurbulence;
                    trunkStiffness = wm.TrunkStiffness;
                    trunkBendAmount = wm.TrunkBendAmount;
                    leafFlutter = wm.LeafFlutter;
                    leafFlutterSpeed = wm.LeafFlutterSpeed;

                    rainIntensity = wm.RainIntensity;
                    snowAccumulation = wm.SnowAccumulation;
                    snowIntensity = wm.SnowIntensity;

                    snowSlopeMin = wm.SnowSlopeMin;
                    snowSlopeMax = wm.SnowSlopeMax;
                    snowDisplacement = wm.SnowDisplacement;
                }
            }
            catch { }

            // Extract frustum planes from light-space matrix for culling
            _frustumCuller.ExtractPlanes(lightSpaceMatrix);

            // Render each batch
            foreach (var batch in _batches.Values)
            {
                if (batch.Transforms.Count == 0 || batch.VAO == 0) continue;

                // Perform culling to populate VisibleTransforms
                CullBatch(batch, cameraPos);

                // Update GPU buffer if needed
                if (batch.NeedsGPUUpdate)
                {
                    UpdateInstanceBuffer(batch);
                    batch.NeedsGPUUpdate = false;
                }

                if (batch.InstanceCount == 0) continue;

                // Load material to determine alpha clipping and transparency
                Engine.Assets.MaterialAsset? matAsset = null;
                Engine.Rendering.MaterialRuntime? mr = null;
                bool hasAlphaClipping = false;
                float alphaClipThreshold = 0.5f;

                if (batch.MaterialGuid.HasValue)
                {
                    try
                    {
                        matAsset = AssetDatabase.LoadMaterial(batch.MaterialGuid.Value);
                        if (matAsset != null)
                        {
                            Func<Guid, string?> resolver = g => AssetDatabase.TryGet(g, out var r) ? r.Path : null;
                            mr = Engine.Rendering.MaterialRuntime.FromAsset(matAsset, resolver);

                            if (mr != null)
                            {
                                // Skip fully transparent objects in shadow pass
                                if (mr.TransparencyMode != 0)
                                {
                                    continue;
                                }

                                // Check if alpha clipping is enabled
                                hasAlphaClipping = mr.AlphaClippingEnabled != 0;
                                alphaClipThreshold = mr.AlphaClipThreshold;
                            }
                        }
                    }
                    catch { }
                }

                // Choose shader based on alpha clipping requirement
                ShaderProgram shader = hasAlphaClipping ? _shadowDepthAlphaClipShader! : _shadowDepthShader!;

                shader.Use();
                shader.SetMat4("u_LightSpaceMatrix", lightSpaceMatrix);

                // Set texture tiling/offset (needed for UVs in vertex shader)
                if (mr != null)
                {
                    shader.SetVec2("u_TextureTiling", new Vector2(mr.TextureTiling[0], mr.TextureTiling[1]));
                    shader.SetVec2("u_TextureOffset", new Vector2(mr.TextureOffset[0], mr.TextureOffset[1]));
                }
                else
                {
                    shader.SetVec2("u_TextureTiling", new Vector2(1f, 1f));
                    shader.SetVec2("u_TextureOffset", new Vector2(0f, 0f));
                }

                // If alpha clipping, bind albedo texture and set parameters
                if (hasAlphaClipping && mr != null)
                {
                    GL.ActiveTexture(TextureUnit.Texture0);
                    GL.BindTexture(TextureTarget.Texture2D, mr.AlbedoTex);
                    shader.SetInt("u_AlbedoTex", 0);
                    shader.SetInt("u_AlphaClippingEnabled", 1);
                    shader.SetFloat("u_AlphaClipThreshold", alphaClipThreshold);
                }
                else
                {
                    shader.SetInt("u_AlphaClippingEnabled", 0);
                }

                // CRITICAL: Set wind/weather uniforms for animated shadows
                // The shadow depth shader has the same wind animation code as the forward shader
                // Use material-specific parameters if available (to match forward shader behavior)
                try
                {
                    shader.SetFloat("u_Time", time);

                    // Determine which wind parameters to use based on material WindMode
                    float effectiveWindStrength = windStrength;
                    OpenTK.Mathematics.Vector2 effectiveWindDirection = windDirection;
                    float effectiveWindSpeed = windSpeed;
                    float effectiveWindGustiness = windGustiness;
                    float effectiveBranchAmplitude = branchAmplitude;
                    float effectiveBranchSpeed = branchSpeed;
                    float effectiveBranchTurbulence = branchTurbulence;
                    float effectiveTrunkStiffness = trunkStiffness;
                    float effectiveTrunkBendAmount = trunkBendAmount;
                    float effectiveLeafFlutter = leafFlutter;
                    float effectiveLeafFlutterSpeed = leafFlutterSpeed;

                    // If material runtime is available, check its WindMode
                    if (mr != null)
                    {
                        int windMode = mr.VegetationWindMode;
                        float blendFactor = mr.VegetationWindBlendFactor;

                        if (windMode == 1) // Local mode - use material parameters
                        {
                            effectiveWindStrength = mr.VegetationWindStrength;
                            effectiveWindDirection = new OpenTK.Mathematics.Vector2(mr.VegetationWindDirection[0], mr.VegetationWindDirection[1]);
                            effectiveWindSpeed = mr.VegetationWindSpeed;
                            effectiveWindGustiness = mr.VegetationWindGustiness;
                            effectiveBranchAmplitude = mr.VegetationBranchAmplitude;
                            effectiveBranchSpeed = mr.VegetationBranchSpeed;
                            effectiveBranchTurbulence = mr.VegetationBranchTurbulence;
                            effectiveTrunkStiffness = mr.VegetationTrunkStiffness;
                            effectiveTrunkBendAmount = mr.VegetationTrunkBendAmount;
                            effectiveLeafFlutter = mr.VegetationLeafFlutter;
                            effectiveLeafFlutterSpeed = mr.VegetationLeafFlutterSpeed;
                        }
                        else if (windMode == 2) // Blend mode - blend between local and global
                        {
                            effectiveWindStrength = mr.VegetationWindStrength * (1 - blendFactor) + windStrength * blendFactor;
                            effectiveWindDirection = new OpenTK.Mathematics.Vector2(
                                mr.VegetationWindDirection[0] * (1 - blendFactor) + windDirection.X * blendFactor,
                                mr.VegetationWindDirection[1] * (1 - blendFactor) + windDirection.Y * blendFactor
                            );
                            effectiveWindSpeed = mr.VegetationWindSpeed * (1 - blendFactor) + windSpeed * blendFactor;
                            effectiveWindGustiness = mr.VegetationWindGustiness * (1 - blendFactor) + windGustiness * blendFactor;
                            effectiveBranchAmplitude = mr.VegetationBranchAmplitude * (1 - blendFactor) + branchAmplitude * blendFactor;
                            effectiveBranchSpeed = mr.VegetationBranchSpeed * (1 - blendFactor) + branchSpeed * blendFactor;
                            effectiveBranchTurbulence = mr.VegetationBranchTurbulence * (1 - blendFactor) + branchTurbulence * blendFactor;
                            effectiveTrunkStiffness = mr.VegetationTrunkStiffness * (1 - blendFactor) + trunkStiffness * blendFactor;
                            effectiveTrunkBendAmount = mr.VegetationTrunkBendAmount * (1 - blendFactor) + trunkBendAmount * blendFactor;
                            effectiveLeafFlutter = mr.VegetationLeafFlutter * (1 - blendFactor) + leafFlutter * blendFactor;
                            effectiveLeafFlutterSpeed = mr.VegetationLeafFlutterSpeed * (1 - blendFactor) + leafFlutterSpeed * blendFactor;
                        }
                        // else: windMode == 0 (Global) - use default values from WeatherManager
                    }

                    // Bind effective wind parameters (either global, local, or blended)
                    shader.SetFloat("u_WindStrength", effectiveWindStrength);
                    shader.SetVec2("u_WindDirection", effectiveWindDirection);
                    shader.SetFloat("u_WindSpeed", effectiveWindSpeed);
                    shader.SetFloat("u_WindGustiness", effectiveWindGustiness);
                    shader.SetFloat("u_BranchAmplitude", effectiveBranchAmplitude);
                    shader.SetFloat("u_BranchSpeed", effectiveBranchSpeed);
                    shader.SetFloat("u_BranchTurbulence", effectiveBranchTurbulence);
                    shader.SetFloat("u_TrunkStiffness", effectiveTrunkStiffness);
                    shader.SetFloat("u_TrunkBendAmount", effectiveTrunkBendAmount);
                    shader.SetFloat("u_LeafFlutter", effectiveLeafFlutter);
                    shader.SetFloat("u_LeafFlutterSpeed", effectiveLeafFlutterSpeed);

                    // Weather effects (always use global values)
                    shader.SetFloat("u_RainIntensity", rainIntensity);
                    shader.SetFloat("u_SnowCoverage", snowIntensity);
                    shader.SetFloat("u_SnowAccumulation", snowAccumulation);
                    shader.SetFloat("u_SnowDisplacement", snowDisplacement);
                    shader.SetFloat("u_SnowSlopeMin", snowSlopeMin);
                    shader.SetFloat("u_SnowSlopeMax", snowSlopeMax);
                }
                catch { }

                // Draw instanced (depth only)
                GL.BindVertexArray(batch.VAO);
                GL.DrawElementsInstanced(
                    PrimitiveType.Triangles,
                    batch.IndexCount,
                    DrawElementsType.UnsignedInt,
                    IntPtr.Zero,
                    batch.InstanceCount
                );
            }

            // Restore OpenGL state
            GL.BindVertexArray(0);
            GL.UseProgram(0);
        }

        /// <summary>
        /// Clear all batches and free GPU resources.
        /// </summary>
        public void ClearBatches()
        {
            foreach (var batch in _batches.Values)
            {
                if (batch.InstanceVBO != 0)
                {
                    GL.DeleteBuffer(batch.InstanceVBO);
                    batch.InstanceVBO = 0;
                }
                
                // Note: VAO/VBO/EBO belong to the mesh cache, don't delete them here
            }

            _batches.Clear();
        }

        // === PRIVATE METHODS ===

        private void LoadMeshForBatch(VegetationBatch batch)
        {
            // Load model from asset database
            var meshAsset = AssetDatabase.LoadMeshAsset(batch.ModelGuid);
            if (meshAsset == null)
            {
                Engine.Utils.DebugLogger.Log($"[VegetationRenderer] Model asset not found: {batch.ModelGuid}");
                return;
            }

            try
            {
                if (batch.SubmeshIndex >= meshAsset.SubMeshes.Count)
                {
                    Engine.Utils.DebugLogger.Log($"[VegetationRenderer] Invalid submesh index {batch.SubmeshIndex} (model has {meshAsset.SubMeshes.Count} submeshes)");
                    return;
                }

                var submesh = meshAsset.SubMeshes[batch.SubmeshIndex];
                
                // Get the material from the model
                if (submesh.MaterialIndex >= 0 && submesh.MaterialIndex < meshAsset.MaterialGuids.Count)
                {
                    batch.MaterialGuid = meshAsset.MaterialGuids[submesh.MaterialIndex];
                }

                // Preload material textures and ensure GL uploads are flushed so the first render doesn't show white placeholders.
                if (batch.MaterialGuid.HasValue)
                {
                    try
                    {
                        Engine.Assets.MaterialAsset? matPre = null;
                        try
                        {
                            matPre = AssetDatabase.LoadMaterial(batch.MaterialGuid.Value);
                        }
                        catch { }

                        // Fallback: attempt to load from disk via recorded path if LoadMaterial failed
                        if (matPre == null)
                        {
                            try
                            {
                                if (AssetDatabase.TryGet(batch.MaterialGuid.Value, out var rec) && !string.IsNullOrEmpty(rec.Path) && System.IO.File.Exists(rec.Path))
                                {
                                    matPre = Engine.Assets.MaterialAsset.Load(rec.Path);
                                }
                            }
                            catch { }
                        }

                        if (matPre != null)
                        {
                            Func<Guid, string?> resolver = g => AssetDatabase.TryGet(g, out var r) ? r.Path : null;
                            try { var mrt = Engine.Rendering.MaterialRuntime.FromAsset(matPre, resolver); } catch { }
                            // MaterialRuntime.FromAsset already updates the global cache, no need for explicit UpdateCacheEntry
                            try { Engine.Rendering.TextureCache.FlushPendingUploads(64); } catch { }
                        }
                    }
                    catch (Exception ex)
                    {
                        Engine.Utils.DebugLogger.Log($"[VegetationRenderer] Exception while preloading material: {ex.Message}");
                    }
                }

                // Upload mesh to GPU
                batch.VAO = GL.GenVertexArray();
                batch.VBO = GL.GenBuffer();
                batch.EBO = GL.GenBuffer();

                GL.BindVertexArray(batch.VAO);

                // Upload vertex data (interleaved: pos(3) + normal(3) + uv(2) = 8 floats)
                GL.BindBuffer(BufferTarget.ArrayBuffer, batch.VBO);
                GL.BufferData(BufferTarget.ArrayBuffer, submesh.Vertices.Length * sizeof(float), 
                    submesh.Vertices, BufferUsageHint.StaticDraw);

                // Vertex attributes
                int stride = 8 * sizeof(float);
                
                // Location 0: Position
                GL.EnableVertexAttribArray(0);
                GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);

                // Location 1: Normal
                GL.EnableVertexAttribArray(1);
                GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));

                // Location 2: UV
                GL.EnableVertexAttribArray(2);
                GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, 6 * sizeof(float));

                // Upload indices
                GL.BindBuffer(BufferTarget.ElementArrayBuffer, batch.EBO);
                GL.BufferData(BufferTarget.ElementArrayBuffer, submesh.Indices.Length * sizeof(uint), 
                    submesh.Indices, BufferUsageHint.StaticDraw);

                batch.IndexCount = submesh.Indices.Length;

                // Create instance buffer (for model matrices)
                batch.InstanceVBO = GL.GenBuffer();
                GL.BindBuffer(BufferTarget.ArrayBuffer, batch.InstanceVBO);
                GL.BufferData(BufferTarget.ArrayBuffer, MaxInstancesPerBatch * sizeof(float) * 16, 
                    IntPtr.Zero, BufferUsageHint.DynamicDraw);

                // Instance attributes (model matrix as 4 vec4s)
                int mat4Size = sizeof(float) * 16;
                
                for (int i = 0; i < 4; i++)
                {
                    int location = 3 + i; // Locations 3, 4, 5, 6
                    GL.EnableVertexAttribArray(location);
                    GL.VertexAttribPointer(location, 4, VertexAttribPointerType.Float, false, 
                        mat4Size, i * sizeof(float) * 4);
                    GL.VertexAttribDivisor(location, 1); // One matrix per instance
                }

                GL.BindVertexArray(0);
            }
            catch (Exception ex)
            {
                Engine.Utils.DebugLogger.Log($"[VegetationRenderer] Failed to load mesh: {ex.Message}");
            }
        }

        /// <summary>
        /// Perform frustum and distance culling on a batch to populate VisibleTransforms.
        /// This is called every frame before rendering.
        /// </summary>
        private void CullBatch(VegetationBatch batch, Vector3 cameraPos)
        {
            batch.VisibleTransforms.Clear();

            float maxDistSqr = batch.MaxRenderDistance * batch.MaxRenderDistance;
            bool hasDistanceCulling = batch.MaxRenderDistance > 0;

            // OPTIMIZATION: Limit instances tested per batch to prevent lag
            const int MaxInstancesTestedPerBatch = 50000; // Was 10k, increased to 50k
            int instancesTested = 0;

            for (int i = 0; i < batch.Transforms.Count; i++)
            {
                // OPTIMIZATION: Stop testing if we've tested too many
                if (instancesTested >= MaxInstancesTestedPerBatch)
                {
                    break;
                }

                instancesTested++;
                var transform = batch.Transforms[i];

                // Extract position from transform matrix
                // In OpenTK Matrix4 (row-major), translation is in last ROW: M41, M42, M43
                Vector3 position = new Vector3(transform.M41, transform.M42, transform.M43);

                // Distance culling
                if (hasDistanceCulling)
                {
                    float distSqr = (position - cameraPos).LengthSquared;
                    if (distSqr > maxDistSqr)
                    {
                        continue; // Too far, skip
                    }
                }


                // Frustum culling réactivé (même logique que grass/rocks)
                if (!_frustumCuller.TestSphere(position, batch.CullingSphereRadius))
                {
                    continue; // Outside frustum, skip
                }

                // Instance is visible
                batch.VisibleTransforms.Add(transform);
            }

            // Mark for GPU update if visible set changed
            if (batch.VisibleTransforms.Count != batch.LastTransformCount)
            {
                batch.NeedsGPUUpdate = true;
                batch.LastTransformCount = batch.VisibleTransforms.Count;
            }
        }

        private void UpdateInstanceBuffer(VegetationBatch batch)
        {
            if (batch.InstanceVBO == 0 || batch.VisibleTransforms.Count == 0) return;

            // Use visible transforms (after culling) instead of all transforms
            int instanceCount = Math.Min(batch.VisibleTransforms.Count, MaxInstancesPerBatch);

            // Ensure buffer is large enough (grow by 1.5x to avoid frequent reallocations)
            if (_instanceBuffer.Length < instanceCount)
            {
                int newSize = Math.Max(instanceCount, (int)(_instanceBuffer.Length * 1.5f));
                newSize = Math.Min(newSize, MaxInstancesPerBatch); // Cap at max
                _instanceBuffer = new VegetationInstance[newSize];
            }

            // Convert visible transforms to instance data
            for (int i = 0; i < instanceCount; i++)
            {
                _instanceBuffer[i] = VegetationInstance.FromMatrix(batch.VisibleTransforms[i]);
            }

            // Upload to GPU
            GL.BindBuffer(BufferTarget.ArrayBuffer, batch.InstanceVBO);

            int dataSize = instanceCount * sizeof(float) * 16;
            unsafe
            {
                fixed (VegetationInstance* ptr = _instanceBuffer)
                {
                    GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, dataSize, (IntPtr)ptr);
                }
            }

            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        }

        

        private void BindDefaultSnowTextures(ShaderProgram shader)
        {
            // CRITICAL FIX: Use units 13-15 to avoid conflict with IBL textures (10-12)
            // MaterialRuntime.Bind() uses units 10-12 for IBL, which was overwriting snow textures!

            // Bind default white textures for snow material
            GL.ActiveTexture(TextureUnit.Texture13);
            GL.BindTexture(TextureTarget.Texture2D, _defaultWhiteTexture);
            shader.SetInt("u_SnowAlbedoTex", 13);

            GL.ActiveTexture(TextureUnit.Texture14);
            GL.BindTexture(TextureTarget.Texture2D, _defaultWhiteTexture);
            shader.SetInt("u_SnowNormalTex", 14);

            GL.ActiveTexture(TextureUnit.Texture15);
            GL.BindTexture(TextureTarget.Texture2D, _defaultWhiteTexture);
            shader.SetInt("u_SnowMetallicRoughnessTex", 15);

            // Default snow material properties (realistic snow albedo ~65-70%)
            shader.SetVec4("u_SnowAlbedoColor", new OpenTK.Mathematics.Vector4(0.65f, 0.68f, 0.75f, 1.0f));
            shader.SetFloat("u_SnowMetallic", 0.0f);
            shader.SetFloat("u_SnowRoughness", 0.3f);
            shader.SetFloat("u_SnowNormalStrength", 1.0f);
            shader.SetVec2("u_SnowTextureTiling", new Vector2(0.1f, 0.1f));

            // CRITICAL: Restore texture unit 0 as active
            GL.ActiveTexture(TextureUnit.Texture0);
        }

        public void Dispose()
        {
            if (_disposed) return;

            ClearBatches();
            _vegetationShader?.Dispose();
            _shadowDepthShader?.Dispose();
            _shadowDepthAlphaClipShader?.Dispose();

            if (_defaultWhiteTexture != 0)
            {
                GL.DeleteTexture(_defaultWhiteTexture);
                _defaultWhiteTexture = 0;
            }

            _disposed = true;
        }
    }
}

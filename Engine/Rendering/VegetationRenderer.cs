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
                // Column 3 = (M14, M24, M34, M44) = fourth column (translation)
                return new VegetationInstance
                {
                    Column0 = new Vector4(matrix.M11, matrix.M21, matrix.M31, matrix.M41), // First COLUMN
                    Column1 = new Vector4(matrix.M12, matrix.M22, matrix.M32, matrix.M42), // Second COLUMN
                    Column2 = new Vector4(matrix.M13, matrix.M23, matrix.M33, matrix.M43), // Third COLUMN
                    Column3 = new Vector4(matrix.M14, matrix.M24, matrix.M34, matrix.M44)  // Fourth COLUMN (translation)
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
            public List<Matrix4> Transforms = new();

            // GPU resources
            public int VAO;
            public int VBO; // Mesh vertex data
            public int EBO; // Mesh index data
            public int InstanceVBO; // Instance transforms
            public int IndexCount;
            public Guid? MaterialGuid; // Material from the model's submesh
            public int InstanceCount => Transforms.Count;

            // Culling mode from MeshRendererComponent
            public Engine.Components.CullingMode CullingMode = Engine.Components.CullingMode.Back;

            // (Removed) Per-batch render distance

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

        private ShaderProgram? _vegetationShader = null;

        // Default white texture for materials without albedo
        private int _defaultWhiteTexture = 0;

        private bool _disposed = false;

        // Debug counter for logging
        private static int _renderCallCounter = 0;

        // === CONSTRUCTOR ===
        
        public VegetationRenderer()
        {
            LoadShader();
            CreateDefaultWhiteTexture();
            try { if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[VegetationRenderer] Created VegetationRenderer (BatchCount={BatchCount})"); } catch { }
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

            try { if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[VegetationRenderer] Created default white texture (handle={_defaultWhiteTexture})"); } catch { }
        }

        private void LoadShader()
        {
            try
            {
                string vertPath = "Engine/Rendering/Shaders/Forward/VegetationForward.vert";
                string fragPath = "Engine/Rendering/Shaders/Forward/VegetationForward.frag";

                Console.WriteLine($"[VegetationRenderer] Loading VegetationForward shader from {vertPath} and {fragPath}");

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

                Console.WriteLine($"[VegetationRenderer] VegetationForward shader loaded (handle={_vegetationShader.Handle})");

                // Verify shader program is valid
                GL.GetProgram(_vegetationShader.Handle, GetProgramParameterName.LinkStatus, out int linkStatus);
                if (linkStatus == 0)
                {
                    string infoLog = GL.GetProgramInfoLog(_vegetationShader.Handle);
                    Console.WriteLine($"[VegetationRenderer] *** VegetationForward LINK ERROR ***:\n{infoLog}");
                    _vegetationShader = null;
                    return;
                }
                else
                {
                    Console.WriteLine("[VegetationRenderer] VegetationForward shader linked successfully");
                }

                // Bind Global UBO (binding point 0) if shader defines it so lighting/uniforms work
                try
                {
                    _vegetationShader.Use();
                    int globalBlockIndex = OpenTK.Graphics.OpenGL4.GL.GetUniformBlockIndex(_vegetationShader.Handle, "Global");
                    if (globalBlockIndex != -1) OpenTK.Graphics.OpenGL4.GL.UniformBlockBinding(_vegetationShader.Handle, globalBlockIndex, 0);
                }
                catch { }

                Console.WriteLine($"[VegetationRenderer] VegetationForward shader ready for use");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VegetationRenderer] *** Vegetation shader load EXCEPTION ***:\n{ex.Message}\n{ex.StackTrace}");
                _vegetationShader = null;
            }
        }

        // === PUBLIC API ===

        /// <summary>
        /// Update a batch for a specific model+submesh with instance transforms and culling mode.
        /// </summary>
        public void UpdateBatch(Guid modelGuid, int submeshIndex, List<Matrix4>? transforms, Engine.Components.CullingMode cullingMode = Engine.Components.CullingMode.Back)
        {
            // Treat a null transforms list as empty to avoid callers needing to allocate an empty list
            transforms ??= new List<Matrix4>();

            string batchKey = $"{modelGuid}_{submeshIndex}";

            try { if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[VegetationRenderer] UpdateBatch called: model={modelGuid} submesh={submeshIndex} instances={transforms.Count}"); } catch { }

            // Get or create batch
            if (!_batches.TryGetValue(batchKey, out var batch))
            {
                batch = new VegetationBatch
                {
                    ModelGuid = modelGuid,
                    SubmeshIndex = submeshIndex,
                    CullingMode = cullingMode
                };
                _batches[batchKey] = batch;
            }
            else
            {
                // Update culling mode for existing batch
                batch.CullingMode = cullingMode;
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
                batch.Transforms = transforms;
                batch.LastTransformCount = transforms.Count;
                batch.NeedsGPUUpdate = true;
            }

            // Load mesh if needed
            if (batch.VAO == 0)
            {
                LoadMeshForBatch(batch);
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
            uint objectId = 0)
        {
            if (_batches.Count == 0)
            {
                // No batches to render (this is normal if no vegetation was generated)
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

            // Fetch authoritative weather state from WeatherManager to avoid caller desync
            try
            {
                var wm = Engine.Systems.WeatherManager.GetCurrentWeather();
                if (wm == null) throw new InvalidOperationException("WeatherManager returned null weather state");

                // DEBUG: Print every 120 frames to see WeatherManager state
                if (_renderCallCounter++ % 120 == 0)
                {
                    // System.Console.WriteLine($"[VegetationRenderer] WeatherManager: SnowAccumulation={wm.SnowAccumulation:F3}, SnowMapMaterial={wm.SnowMapMaterial?.ToString() ?? "null"}");
                }

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

            // Render each batch
            int batchesRendered = 0;
            int instancesRendered = 0;

            foreach (var batch in _batches.Values)
            {
                if (batch.Transforms.Count == 0 || batch.VAO == 0) continue;

                // Update GPU buffer only if data changed (lazy update)
                if (batch.NeedsGPUUpdate)
                {
                    UpdateInstanceBuffer(batch);
                    batch.NeedsGPUUpdate = false;
                }

                if (batch.InstanceCount == 0) continue;

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

                // DIAGNOSTIC: verify wind-related uniform locations exist for this shader.
                try
                {
                    if (Engine.Utils.DebugLogger.EnableVerbose)
                    {
                        int locWind = GL.GetUniformLocation(shToUse.Handle, "u_WindStrength");
                        int locWindDir = GL.GetUniformLocation(shToUse.Handle, "u_WindDirection");
                        int locTime = GL.GetUniformLocation(shToUse.Handle, "u_Time");
                        Engine.Utils.DebugLogger.Log($"[VegetationRenderer] Shader handle={shToUse.Handle} name={shToUse.GetType().Name} locs: u_WindStrength={locWind}, u_WindDirection={locWindDir}, u_Time={locTime}");
                    }

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
                            Engine.Utils.DebugLogger.Log($"[VegetationRenderer] Material shader (handle={shToUse.Handle}) lacks wind uniforms — switching to built-in vegetation shader (handle={_vegetationShader.Handle})");
                            shToUse = _vegetationShader;
                            GL.UseProgram(shToUse.Handle);
                        }
                        else if (ReferenceEquals(shToUse, _vegetationShader))
                        {
                            // If our built-in shader is the one missing uniforms, try reloading it
                            Engine.Utils.DebugLogger.Log("[VegetationRenderer] Vegetation shader missing wind uniforms - reloading shader...");
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
                        if (Engine.Utils.DebugLogger.EnableVerbose)
                        {
                            try { Engine.Utils.DebugLogger.Log($"[VegetationRenderer] Binding material {batch.MaterialGuid} -> shader={shToUse.Handle} mr.AlbedoTex={mr.AlbedoTex} Transparency={mr.TransparencyMode} AlbedoColor=({mr.AlbedoColor[0]:F2},{mr.AlbedoColor[1]:F2},{mr.AlbedoColor[2]:F2},{mr.AlbedoColor[3]:F2})"); } catch { }
                        }
                        mr.Bind(shToUse, time);
                        if (Engine.Utils.DebugLogger.EnableVerbose)
                        {
                            try { var err = GL.GetError(); Engine.Utils.DebugLogger.Log($"[VegetationRenderer] After Bind GL.GetError={err}"); } catch { }
                        }
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

                // CRITICAL: Re-apply wind/weather and animation uniforms AFTER material binding.
                // Some material bind implementations may set common uniforms like u_Time; ensure
                // vegetation-specific uniforms are final before draw.
                try
                {
                    shToUse.SetFloat("u_Time", time);

                    // Primary wind parameters
                    shToUse.SetFloat("u_WindStrength", windStrength);
                    shToUse.SetVec2("u_WindDirection", windDirection);
                    shToUse.SetFloat("u_WindSpeed", windSpeed);
                    shToUse.SetFloat("u_WindGustiness", windGustiness);

                    // Advanced wind parameters (from weather component)
                    shToUse.SetFloat("u_BranchAmplitude", branchAmplitude);
                    shToUse.SetFloat("u_BranchSpeed", branchSpeed);
                    shToUse.SetFloat("u_BranchTurbulence", branchTurbulence);
                    shToUse.SetFloat("u_TrunkStiffness", trunkStiffness);
                    shToUse.SetFloat("u_TrunkBendAmount", trunkBendAmount);
                    shToUse.SetFloat("u_LeafFlutter", leafFlutter);
                    shToUse.SetFloat("u_LeafFlutterSpeed", leafFlutterSpeed);

                    shToUse.SetFloat("u_RainIntensity", rainIntensity);
                    shToUse.SetFloat("u_SnowAccumulation", snowAccumulation);
                    shToUse.SetFloat("u_SnowIntensity", snowIntensity);
                    shToUse.SetFloat("u_Wetness", wetness);

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

                        // DEBUG: Log EVERY time we check for snow material (not just every 120 frames)
                        if (_renderCallCounter % 120 == 0 && wm != null)
                        {
                            System.Console.WriteLine($"[VegetationRenderer] Checking snow material: HasValue={wm.SnowMapMaterial.HasValue}, GUID={wm.SnowMapMaterial?.ToString() ?? "null"}");
                        }

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

                                // DEBUG: Log snow material properties every 120 frames
                                if (_renderCallCounter % 120 == 0)
                                {
                                    // System.Console.WriteLine($"[VegetationRenderer] Snow material: {snowMat.Name}, NormalStrength={snowRuntime.NormalStrength:F2}, Tiling=({snowRuntime.TextureTiling[0]:F2}, {snowRuntime.TextureTiling[1]:F2})");
                                }
                            }
                            else
                            {
                                //System.Console.WriteLine($"[VegetationRenderer] Snow material GUID found but LoadMaterial returned null: {wm.SnowMapMaterial.Value}");
                                BindDefaultSnowTextures(shToUse);
                            }
                        }
                        else
                        {
                            if (wm == null)
                                System.Console.WriteLine("[VegetationRenderer] WeatherManager returned null");
                            else
                                System.Console.WriteLine("[VegetationRenderer] No SnowMapMaterial assigned");
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

                // Draw instanced
                GL.BindVertexArray(batch.VAO);
                GL.DrawElementsInstanced(
                    PrimitiveType.Triangles,
                    batch.IndexCount,
                    DrawElementsType.UnsignedInt,
                    IntPtr.Zero,
                    batch.InstanceCount
                );

                if (Engine.Utils.DebugLogger.EnableVerbose)
                {
                    try { var err2 = GL.GetError(); Engine.Utils.DebugLogger.Log($"[VegetationRenderer] After Draw GL.GetError={err2}"); } catch { }
                }

                batchesRendered++;
                instancesRendered += batch.InstanceCount;
            }

            // CRITICAL: Restore OpenGL state
            GL.BindVertexArray(0);
            GL.UseProgram(0);
            GL.Disable(EnableCap.Blend); // Ensure blending is off after rendering
            GL.DepthMask(true); // Restore depth writing

            if (batchesRendered > 0)
            {
                    try { if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[VegetationRenderer] Rendered {batchesRendered} batches with {instancesRendered} total instances"); } catch { }
            }
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
            try { if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[VegetationRenderer] Loading mesh for model {batch.ModelGuid}, submesh {batch.SubmeshIndex}"); } catch { }

            // Load model from asset database
            var meshAsset = AssetDatabase.LoadMeshAsset(batch.ModelGuid);
            if (meshAsset == null)
            {
                Engine.Utils.DebugLogger.Log($"[VegetationRenderer] Model asset not found: {batch.ModelGuid}");
                return;
            }

            try { if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[VegetationRenderer] Found mesh asset: {meshAsset.Name} with {meshAsset.SubMeshes.Count} submeshes"); } catch { }

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
                                    if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[VegetationRenderer] Loaded material from disk fallback: {rec.Path}");
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
                        else
                        {
                            if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[VegetationRenderer] Warning: material {batch.MaterialGuid.Value} could not be loaded during mesh load");
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

                try { if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[VegetationRenderer] Loaded mesh {meshAsset.Name}: {batch.IndexCount} indices"); } catch { }
            }
            catch (Exception ex)
            {
                Engine.Utils.DebugLogger.Log($"[VegetationRenderer] Failed to load mesh: {ex.Message}");
            }
        }

        private void UpdateInstanceBuffer(VegetationBatch batch)
        {
            if (batch.InstanceVBO == 0 || batch.Transforms.Count == 0) return;

            // Clamp instance count to max batch size to prevent excessive memory usage
            int instanceCount = Math.Min(batch.Transforms.Count, MaxInstancesPerBatch);
            if (instanceCount < batch.Transforms.Count)
            {
                try { if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[VegetationRenderer] Warning: Batch has {batch.Transforms.Count} instances but only {MaxInstancesPerBatch} will be rendered"); } catch { }
            }

            // Ensure buffer is large enough (grow by 1.5x to avoid frequent reallocations)
            if (_instanceBuffer.Length < instanceCount)
            {
                int newSize = Math.Max(instanceCount, (int)(_instanceBuffer.Length * 1.5f));
                newSize = Math.Min(newSize, MaxInstancesPerBatch); // Cap at max
                _instanceBuffer = new VegetationInstance[newSize];
                try { if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[VegetationRenderer] Reallocated instance buffer to {newSize} instances"); } catch { }
            }

            // Convert transforms to instance data
            for (int i = 0; i < instanceCount; i++)
            {
                _instanceBuffer[i] = VegetationInstance.FromMatrix(batch.Transforms[i]);
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

            if (_defaultWhiteTexture != 0)
            {
                GL.DeleteTexture(_defaultWhiteTexture);
                _defaultWhiteTexture = 0;
            }

            _disposed = true;
        }
    }
}

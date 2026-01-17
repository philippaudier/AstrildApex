using System;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Engine.Assets;
using Engine.Components;

namespace Engine.Rendering.Terrain
{
    /// <summary>
    /// Simplified terrain renderer for Unity-style heightmap terrains.
    /// No tessellation, no layers, just clean rendering with material support.
    /// </summary>
    public class TerrainRenderer : IDisposable
    {
        private Engine.Rendering.ShaderProgram? _shader;
        private bool _disposed = false;
        private bool _loggedFirstFrame = false;

        // Material cache to avoid reloading from disk every frame
        private readonly System.Collections.Generic.Dictionary<Guid, MaterialAsset> _materialCache = new();
        private bool _subscribedToMaterialChanges = false;

        // Tile streaming manager (for infinite terrain mode)
        private Tile.TerrainTileManager? _tileManager = null;
        private int _lastStreamingSeed = 0;
        private float _lastNoiseScale = 0;
        private int _lastOctaves = 0;

        // Reusable frustum culler for tile culling (avoids per-frame allocation)
        private Engine.Rendering.FrustumCuller _tileFrustumCuller = new Engine.Rendering.FrustumCuller();

        public TerrainRenderer()
        {
            // Subscribe to material changes to invalidate cache
            if (!_subscribedToMaterialChanges)
            {
                AssetDatabase.MaterialSaved += OnMaterialSaved;
                _subscribedToMaterialChanges = true;
            }

            // Load default terrain shader (TerrainForward or TerrainDebug if env var set)
            string shaderName = "TerrainForward";
            try
            {
                if (Environment.GetEnvironmentVariable("TERRAIN_DEBUG_SHADER") == "1")
                {
                    shaderName = "TerrainDebug";
                }
            }
            catch { }

            _shader = LoadTerrainShader(shaderName);
            if (_shader == null)
            {
                Engine.Utils.DebugLogger.Log($"[TerrainRenderer] CRITICAL: Failed to load {shaderName} shader - terrain will not render!");
            }
        }

        private Engine.Rendering.ShaderProgram? LoadTerrainShader(string shaderName)
        {
            try
            {
                // Use ShaderLibrary instead of loading directly to ensure proper path resolution
                var shader = Engine.Rendering.ShaderLibrary.GetShaderByName(shaderName);

                if (shader == null)
                {
                    Engine.Utils.DebugLogger.Log($"[TerrainRenderer] ERROR: Shader '{shaderName}' not found in ShaderLibrary");
                    return null;
                }

                // Verify shader compiled correctly
                if (shader.Handle == 0 || !GL.IsProgram(shader.Handle))
                {
                    Engine.Utils.DebugLogger.Log($"[TerrainRenderer] ERROR: Shader '{shaderName}' has invalid handle");
                    return null;
                }

                // Check if shader linked successfully
                GL.GetProgram(shader.Handle, GetProgramParameterName.LinkStatus, out int linkStatus);
                if (linkStatus == 0)
                {
                    string infoLog = GL.GetProgramInfoLog(shader.Handle);
                    Engine.Utils.DebugLogger.Log($"[TerrainRenderer] ERROR: Shader '{shaderName}' failed to link:\n{infoLog}");
                    return null;
                }

                return shader;
            }
            catch (Exception ex)
            {
                Engine.Utils.DebugLogger.Log($"[TerrainRenderer] ERROR: Failed to load shader '{shaderName}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Render terrain with material support, SSAO, and shadows.
        /// </summary>
        public void RenderTerrain(
            Engine.Components.Terrain terrain,
            Matrix4 view,
            Matrix4 projection,
            Vector3 viewPos,
            Vector3 lightDir,
            Vector3 lightColor,
            bool ssaoEnabled = false,
            int ssaoTexture = 0,
            float ssaoStrength = 1.0f,
            Vector2 screenSize = default,
            bool shadowsEnabled = false,
            int shadowTexture = 0,
            Matrix4 shadowMatrix = default,
            float shadowBias = 0.005f,
            float shadowMapSize = 1024f,
            float shadowStrength = 0.7f,
            Matrix4 modelMatrix = default,
            float shadowBiasConst = 0.004f,
            float shadowSlopeScale = 1.5f,
            int globalUBO = 0,
            uint entityId = 0)
        {
            // Log first call only to avoid spamming
            try
            {
                if (!_loggedFirstFrame)
                {
                    if (Engine.Utils.DebugLogger.EnableVerbose)
                        Engine.Utils.DebugLogger.Log($"[TerrainRenderer] RenderTerrain FIRST CALL: shadows={shadowsEnabled}, ssao={ssaoEnabled}");
                    _loggedFirstFrame = true;
                }
            }
            catch { }

            if (terrain == null) return;

            if (_shader == null)
            {
                _shader = LoadTerrainShader("TerrainForward");
                if (_shader == null) return;
            }

            // Verify shader is still valid (handle might be invalidated after PlayMode changes)
            if (!GL.IsProgram(_shader.Handle) || _shader.Handle == 0)
            {
                // Force reload from ShaderLibrary to clear cache
                Engine.Rendering.ShaderLibrary.ReloadShader("TerrainForward");
                _shader = Engine.Rendering.ShaderLibrary.GetShaderByName("TerrainForward");
                if (_shader == null || _shader.Handle == 0) return;
            }

            // Bind GlobalUBO if provided (for clip plane support in reflections)
            if (globalUBO > 0)
            {
                GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, globalUBO);
            }

            // Save some mutable GL state so we can restore it afterwards and avoid surprising the rest of the renderer
            var prevProgram = GL.GetInteger(GetPName.CurrentProgram);
            var prevActiveTex = GL.GetInteger(GetPName.ActiveTexture);
            // cannot query polygon mode reliably here; assume default and let higher-level renderer set state as needed
            // Setup render state (explicitly set what we need)
            GL.Disable(EnableCap.Blend);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthMask(true);

            // Apply terrain-specific render settings
            if (terrain.CullMode == Engine.Components.TerrainCullingMode.None)
            {
                GL.Disable(EnableCap.CullFace);
            }
            else
            {
                GL.Enable(EnableCap.CullFace);
                // Map TerrainCullingMode to OpenTK TriangleFace
                var triangleFace = terrain.CullMode switch
                {
                    Engine.Components.TerrainCullingMode.Back => TriangleFace.Back,
                    Engine.Components.TerrainCullingMode.Front => TriangleFace.Front,
                    Engine.Components.TerrainCullingMode.FrontAndBack => TriangleFace.FrontAndBack,
                    _ => TriangleFace.Back
                };
                GL.CullFace(triangleFace);
            }
            GL.PolygonMode(TriangleFace.FrontAndBack, terrain.RenderMode);  // Apply render mode from terrain
            
            // Check if debug face coloring is enabled
            bool debugFaceColor = false;
            try { 
                debugFaceColor = Environment.GetEnvironmentVariable("TERRAIN_DEBUG_FACE_COLOR") == "1";
            } catch { }

            // Clear any previous GL errors before shader activation
            GL.GetError();

            _shader.Use();

            // CRITICAL: Apply clipping uniforms for water reflections if caller has set them globally
            // We check for a special marker in the environment to know if we're in reflection pass
            // Alternative: always try to set from a static/global source
            try
            {
                // Access the reflection clipping state from a global source
                // The ViewportRenderer should have set this before calling RenderTerrain
                if (Engine.Rendering.ReflectionClippingState.IsEnabled)
                {
                    _shader.SetInt("u_ClipPlaneEnabled", 1);
                    var plane = Engine.Rendering.ReflectionClippingState.ClipPlane;
                    _shader.SetVec4("u_ClipPlane", new OpenTK.Mathematics.Vector4(plane.X, plane.Y, plane.Z, plane.W));
                }
                else
                {
                    _shader.SetInt("u_ClipPlaneEnabled", 0);
                }
            }
            catch { }

            // CRITICAL: Bind GlobalUBO to the shader's "Global" uniform block
            // This must be done AFTER shader.Use() to ensure the shader is active
            if (globalUBO > 0)
            {
                // Get the uniform block index for "Global" block in the shader
                int blockIndex = GL.GetUniformBlockIndex(_shader.Handle, "Global");
                if (blockIndex >= 0)
                {
                    // Bind the uniform block to binding point 0
                    GL.UniformBlockBinding(_shader.Handle, blockIndex, 0);
                    // Bind our GlobalUBO buffer to binding point 0
                    GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, globalUBO);
                }
                else
                {
                    Engine.Utils.DebugLogger.Log($"[TerrainRenderer] WARNING: Could not find 'Global' uniform block in shader!");
                }
            }

            // Check for OpenGL errors after shader activation
            var shaderError = GL.GetError();
            if (shaderError != ErrorCode.NoError)
            {
                Engine.Utils.DebugLogger.Log($"[TerrainRenderer] GL error after shader.Use(): {shaderError}");
            }

            // Verify shader is active (only in debug mode for performance)
            #if DEBUG
            int currentProgram = GL.GetInteger(GetPName.CurrentProgram);
            if (currentProgram != _shader.Handle)
            {
                Engine.Utils.DebugLogger.Log($"[TerrainRenderer] ERROR: Failed to activate shader! Expected {_shader.Handle}, got {currentProgram}");
                return;
            }
            #endif

            // Set matrices - use provided model matrix or Identity if not specified
            var model = modelMatrix == default ? Matrix4.Identity : modelMatrix;
            _shader.SetMat4("u_Model", model);
            _shader.SetMat4("u_View", view);
            _shader.SetMat4("u_Projection", projection);

            // Calculate normal matrix from model matrix (transpose of inverse)
            var normalMat = new Matrix3(model);
            normalMat.Invert();
            normalMat.Transpose();
            _shader.SetMat3("u_NormalMat", normalMat);

            // Set entity ID for object picking and selection outline
            _shader.SetUInt("u_ObjectId", entityId);

            // Set camera and lighting
            _shader.SetVec3("u_ViewPos", viewPos);
            _shader.SetVec3("uCameraPos", viewPos); // Compatibility
            _shader.SetVec3("u_LightDir", lightDir);
            _shader.SetVec3("u_LightColor", lightColor);

            // CRITICAL: Initialize u_FlipNormalY (required by Common.glsl)
            _shader.SetInt("u_FlipNormalY", 1); // 1 = flip Y for OpenGL convention

            // Asset resolver function (needed for texture loading)
            Func<Guid, string?> resolver = guid => AssetDatabase.TryGet(guid, out var r) ? r.Path : null;

            // Set terrain material properties
            _shader.SetVec4("u_TerrainBaseColor", new Vector4(1f, 1f, 1f, 1f));
            _shader.SetFloat("u_TerrainMetallic", 0.0f);
            _shader.SetFloat("u_TerrainRoughness", 0.8f);

            // Set compatibility uniforms (required by shader but not used with layers)
            _shader.SetVec4("u_AlbedoColor", new Vector4(1f, 1f, 1f, 1f));
            _shader.SetFloat("u_Metallic", 0.0f);
            _shader.SetFloat("u_Smoothness", 0.5f);
            _shader.SetInt("u_TransparencyMode", 0);

            // Bind dummy textures for compatibility uniforms
            GL.ActiveTexture(TextureUnit.Texture18);
            GL.BindTexture(TextureTarget.Texture2D, Engine.Rendering.TextureCache.White1x1);
            _shader.SetInt("u_AlbedoTex", 18);
            GL.ActiveTexture(TextureUnit.Texture19);
            GL.BindTexture(TextureTarget.Texture2D, Engine.Rendering.TextureCache.White1x1);
            _shader.SetInt("u_NormalTex", 19);

            // SSAO
            _shader.SetInt("u_SSAOEnabled", ssaoEnabled ? 1 : 0);
            _shader.SetFloat("u_SSAOStrength", ssaoStrength);
            _shader.SetVec2("u_ScreenSize", screenSize);

            if (ssaoEnabled && ssaoTexture != 0)
            {
                GL.ActiveTexture(TextureUnit.Texture16);
                GL.BindTexture(TextureTarget.Texture2D, ssaoTexture);
                _shader.SetInt("u_SSAOTexture", 16);
            }
            else
            {
                GL.ActiveTexture(TextureUnit.Texture16);
                GL.BindTexture(TextureTarget.Texture2D, Engine.Rendering.TextureCache.White1x1);
                _shader.SetInt("u_SSAOTexture", 16);
            }

            // IBL - Bind environment maps if available (use SkyboxRenderer static handles)
            try
            {
                // Default to disabled
                _shader.SetInt("u_HasIBL", 0);

                // Use higher texture units to avoid collision with layer/splatmap bindings (which start at Texture0..)
                const TextureUnit irrUnit = TextureUnit.Texture23;
                const TextureUnit prefUnit = TextureUnit.Texture24;
                const TextureUnit brdfUnit = TextureUnit.Texture25;

                var irrHandle = Engine.Rendering.SkyboxRenderer.IrradianceMap;
                var prefHandle = Engine.Rendering.SkyboxRenderer.PrefilteredEnvMap;
                var brdfHandle = Engine.Rendering.SkyboxRenderer.BRDFLUTTexture;

                if (irrHandle != 0 && prefHandle != 0 && brdfHandle != 0)
                {
                    _shader.SetInt("u_HasIBL", 1);

                    GL.ActiveTexture(irrUnit);
                    GL.BindTexture(TextureTarget.TextureCubeMap, (int)irrHandle);
                    _shader.SetInt("u_IrradianceMap", (int)(irrUnit - TextureUnit.Texture0));

                    GL.ActiveTexture(prefUnit);
                    GL.BindTexture(TextureTarget.TextureCubeMap, (int)prefHandle);
                    _shader.SetInt("u_PrefilteredEnvMap", (int)(prefUnit - TextureUnit.Texture0));

                    GL.ActiveTexture(brdfUnit);
                    GL.BindTexture(TextureTarget.Texture2D, (int)brdfHandle);
                    _shader.SetInt("u_BRDFLUT", (int)(brdfUnit - TextureUnit.Texture0));

                    // Prefilter max LOD is provided by SkyboxRenderer
                    _shader.SetFloat("u_PrefilterMaxLod", Engine.Rendering.SkyboxRenderer.PrefilterMaxLod);
                }
                else
                {
                    // Bind safe defaults (disable IBL path in shader)
                    _shader.SetInt("u_HasIBL", 0);
                    GL.ActiveTexture(irrUnit);
                    GL.BindTexture(TextureTarget.TextureCubeMap, 0);
                    _shader.SetInt("u_IrradianceMap", (int)(irrUnit - TextureUnit.Texture0));
                    GL.ActiveTexture(prefUnit);
                    GL.BindTexture(TextureTarget.TextureCubeMap, 0);
                    _shader.SetInt("u_PrefilteredEnvMap", (int)(prefUnit - TextureUnit.Texture0));
                    GL.ActiveTexture(brdfUnit);
                    GL.BindTexture(TextureTarget.Texture2D, Engine.Rendering.TextureCache.White1x1);
                    _shader.SetInt("u_BRDFLUT", (int)(brdfUnit - TextureUnit.Texture0));
                    _shader.SetFloat("u_PrefilterMaxLod", Engine.Rendering.SkyboxRenderer.PrefilterMaxLod);
                }
            }
            catch { /* Non-fatal: fall back to disabled IBL */ }

            // Shadows - CRITICAL: Always bind a texture to u_ShadowMap to avoid InvalidOperation
            GL.ActiveTexture(TextureUnit.Texture17);
            if (shadowsEnabled && shadowTexture != 0)
            {
                // Console.WriteLine($"[TerrainRenderer] Shadows ENABLED - texture={shadowTexture}, bias={shadowBias}");
                GL.BindTexture(TextureTarget.Texture2D, shadowTexture);
                _shader.SetInt("u_ShadowMap", 17);
                _shader.SetInt("u_UseShadows", 1);
                _shader.SetFloat("u_ShadowBias", shadowBias);
                _shader.SetFloat("u_ShadowBiasConst", shadowBiasConst);
                _shader.SetFloat("u_ShadowSlopeScale", shadowSlopeScale);
                _shader.SetFloat("u_ShadowMapSize", shadowMapSize);
                _shader.SetFloat("u_ShadowStrength", shadowStrength);
                _shader.SetMat4("u_ShadowMatrix", shadowMatrix);
                _shader.SetInt("u_CascadeCount", 1); // Simple shadow mode

                var err = GL.GetError();
                if (err != ErrorCode.NoError)
                    Engine.Utils.DebugLogger.Log($"[TerrainRenderer] ERROR setting shadow uniforms: {err}");
            }
            else
            {
                // Bind a dummy white texture to avoid InvalidOperation on the sampler
                GL.BindTexture(TextureTarget.Texture2D, Engine.Rendering.TextureCache.White1x1);
                _shader.SetInt("u_ShadowMap", 17);
                _shader.SetInt("u_UseShadows", 0);
                _shader.SetFloat("u_ShadowBias", 0.005f);
                _shader.SetFloat("u_ShadowBiasConst", shadowBiasConst);
                _shader.SetFloat("u_ShadowSlopeScale", shadowSlopeScale);
                _shader.SetFloat("u_ShadowMapSize", 1024f);
                _shader.SetFloat("u_ShadowStrength", shadowStrength);
                _shader.SetMat4("u_ShadowMatrix", Matrix4.Identity);
                _shader.SetInt("u_CascadeCount", 1);
            }

            // Bind debug shader uniforms if using TerrainDebug
            try
            {
                if (Environment.GetEnvironmentVariable("TERRAIN_DEBUG_SHADER") == "1")
                {
                    // Set height range for color mapping
                    _shader.SetFloat("u_MinHeight", -100f);
                    _shader.SetFloat("u_MaxHeight", terrain.TerrainHeight);
                }
            }
            catch { }

            // Bind terrain material and configure layers
            GL.ActiveTexture(TextureUnit.Texture0);
            _shader.SetInt("u_TerrainTexture", 0);

            if (terrain.TerrainMaterialGuid.HasValue)
            {
                try
                {
                    var material = GetMaterialCached(terrain.TerrainMaterialGuid.Value);
                    if (material != null)
                    {
                        if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[TerrainRenderer] Using material {material.Guid} (name={material.Name}) for terrain");

                        // NEW: Layers are now in Terrain component, not in MaterialAsset
                        if (terrain.TerrainLayers == null)
                        {
                            if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log("[TerrainRenderer] Terrain has no TerrainLayers (null)");
                        }
                        else
                        {
                            if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[TerrainRenderer] Terrain TerrainLayers length={terrain.TerrainLayers.Length}");
                        }
                        // Configure terrain layers if they exist
                        if (terrain.TerrainLayers != null && terrain.TerrainLayers.Length > 0)
                        {
                            int layerCount = Math.Min(terrain.TerrainLayers.Length, 8); // MAX_LAYERS = 8
                            _shader.SetInt("u_LayerCount", layerCount);
                            _shader.SetInt("u_UseSplatmap", 0); // No splatmap support yet
                            _shader.SetInt("u_DebugFaceColor", debugFaceColor ? 1 : 0); // Enable debug face coloring if requested

                            // Bind dummy splatmap textures to avoid InvalidOperation errors
                            GL.ActiveTexture(TextureUnit.Texture20);
                            GL.BindTexture(TextureTarget.Texture2D, Engine.Rendering.TextureCache.White1x1);
                            _shader.SetInt("u_Splatmap[0]", 20);
                            GL.ActiveTexture(TextureUnit.Texture21);
                            GL.BindTexture(TextureTarget.Texture2D, Engine.Rendering.TextureCache.White1x1);
                            _shader.SetInt("u_Splatmap[1]", 21);

                            // OPTIMIZATION: Only bind textures and upload uniforms for ACTIVE layers (not all 8)
                            // Shader reads u_LayerCount to know how many layers to sample, so dummy layers are unused
                            // This reduces ~120 redundant GL calls per frame (from 170 to 50 with 2 layers)

                            #pragma warning disable CS0618 // Disable obsolete warnings for legacy texture properties
                            for (int i = 0; i < layerCount; i++)  // OPTIMIZED: Loop only active layers
                            {
                                var layer = terrain.TerrainLayers[i];

                                // Load Material for this layer (NEW SYSTEM with caching)
                                MaterialAsset? layerMaterial = null;
                                if (layer.Material.HasValue)
                                {
                                    layerMaterial = GetMaterialCached(layer.Material.Value);
                                }

                                    // Bind albedo texture (from Material or legacy property)
                                    GL.ActiveTexture(TextureUnit.Texture0 + i * 2);
                                    if (layerMaterial != null && layerMaterial.AlbedoTexture.HasValue)
                                    {
                                        // NEW: Load from Material
                                        int texId = Engine.Rendering.TextureCache.GetOrLoad(layerMaterial.AlbedoTexture.Value, resolver);
                                        GL.BindTexture(TextureTarget.Texture2D, texId != 0 ? texId : Engine.Rendering.TextureCache.White1x1);
                                    }
                                    else if (layer.AlbedoTexture.HasValue)
                                    {
                                        // LEGACY: Load from layer property
                                        int texId = Engine.Rendering.TextureCache.GetOrLoad(layer.AlbedoTexture.Value, resolver);
                                        GL.BindTexture(TextureTarget.Texture2D, texId != 0 ? texId : Engine.Rendering.TextureCache.White1x1);
                                    }
                                    else
                                    {
                                        GL.BindTexture(TextureTarget.Texture2D, Engine.Rendering.TextureCache.White1x1);
                                    }

                                    // Check if uniform exists before setting it
                                    int albedoLoc = GL.GetUniformLocation(_shader.Handle, $"u_LayerAlbedo[{i}]");
                                    if (albedoLoc >= 0)
                                    {
                                        GL.Uniform1(albedoLoc, i * 2);
                                        var err = GL.GetError();
                                        if (err != ErrorCode.NoError && i == 0)
                                            Engine.Utils.DebugLogger.Log($"[TerrainRenderer] ERROR setting u_LayerAlbedo[{i}]: {err}");
                                    }

                                        // Debug: log bound texture handle for this layer's albedo
                                        try { if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[TerrainRenderer] Bound Layer {i} Albedo -> unit={i*2}, handleBound={(GL.GetInteger(GetPName.TextureBinding2D))}"); } catch { }

                                    // Bind normal texture (from Material or legacy property)
                                    GL.ActiveTexture(TextureUnit.Texture0 + i * 2 + 1);
                                    if (layerMaterial != null && layerMaterial.NormalTexture.HasValue)
                                    {
                                        // NEW: Load from Material
                                        int texId = Engine.Rendering.TextureCache.GetOrLoad(layerMaterial.NormalTexture.Value, resolver);
                                        GL.BindTexture(TextureTarget.Texture2D, texId != 0 ? texId : Engine.Rendering.TextureCache.White1x1);
                                    }
                                    else if (layer.NormalTexture.HasValue)
                                    {
                                        // LEGACY: Load from layer property
                                        int texId = Engine.Rendering.TextureCache.GetOrLoad(layer.NormalTexture.Value, resolver);
                                        GL.BindTexture(TextureTarget.Texture2D, texId != 0 ? texId : Engine.Rendering.TextureCache.White1x1);
                                    }
                                    else
                                    {
                                        GL.BindTexture(TextureTarget.Texture2D, Engine.Rendering.TextureCache.White1x1);
                                    }

                                    int normalLoc = GL.GetUniformLocation(_shader.Handle, $"u_LayerNormal[{i}]");
                                    if (normalLoc >= 0)
                                    {
                                        GL.Uniform1(normalLoc, i * 2 + 1);
                                        var err = GL.GetError();
                                        if (err != ErrorCode.NoError && i == 0)
                                            Engine.Utils.DebugLogger.Log($"[TerrainRenderer] ERROR setting u_LayerNormal[{i}]: {err}");
                                    }

                                        try { if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[TerrainRenderer] Bound Layer {i} Normal -> unit={i*2+1}, handleBound={(GL.GetInteger(GetPName.TextureBinding2D))}"); } catch { }

                                    // Set layer parameters (UV transform comes from layer, not material)
                                    // Prefer tiling/offset from the referenced material when available
                                    float tilingX = layer.Tiling[0];
                                    float tilingY = layer.Tiling[1];
                                    float offsetX = layer.Offset[0];
                                    float offsetY = layer.Offset[1];
                                    int useTriplanar = 0;
                                    float triplanarScale = 1.0f;
                                    float triplanarBlend = 4.0f;
                                    Vector4 stylize = new Vector4(1f, 1f, 1f, 0f); // sat,bright,contrast,hue
                                    float emission = 0f;

                                    if (layerMaterial != null)
                                    {
                                        try
                                        {
                                            if (layerMaterial.TextureTiling != null && layerMaterial.TextureTiling.Length >= 2)
                                            {
                                                tilingX = layerMaterial.TextureTiling[0];
                                                tilingY = layerMaterial.TextureTiling[1];
                                            }
                                            if (layerMaterial.TextureOffset != null && layerMaterial.TextureOffset.Length >= 2)
                                            {
                                                offsetX = layerMaterial.TextureOffset[0];
                                                offsetY = layerMaterial.TextureOffset[1];
                                            }
                                            useTriplanar = layerMaterial.UseTriplanar == 1 ? 1 : 0;
                                            triplanarScale = layerMaterial.TriplanarScale > 0f ? layerMaterial.TriplanarScale : 1.0f;
                                            triplanarBlend = layerMaterial.TriplanarBlendSharpness > 0f ? layerMaterial.TriplanarBlendSharpness : 4.0f;
                                            stylize = new Vector4(layerMaterial.Saturation, layerMaterial.Brightness, layerMaterial.Contrast, layerMaterial.Hue);
                                            emission = layerMaterial.Emission;
                                        }
                                        catch { }
                                    }

                                    _shader.SetVec4($"u_LayerTilingOffset[{i}]", new Vector4(
                                        tilingX, tilingY,
                                        offsetX, offsetY));

                                    _shader.SetInt($"u_LayerUseTriplanar[{i}]", useTriplanar);
                                    _shader.SetFloat($"u_LayerTriplanarScale[{i}]", triplanarScale);
                                    _shader.SetFloat($"u_LayerTriplanarBlend[{i}]", triplanarBlend);
                                    _shader.SetVec4($"u_LayerStylize[{i}]", stylize);
                                    _shader.SetFloat($"u_LayerEmission[{i}]", emission);

                                    // UNIFIED SLOPE SYSTEM: Send slopes as DEGREES (0-90), NOT normalized
                                    // This matches the snow system and makes the shader code consistent
                                    _shader.SetVec4($"u_LayerHeightSlope[{i}]", new Vector4(
                                        layer.HeightMin, layer.HeightMax,
                                        layer.SlopeMinDeg, layer.SlopeMaxDeg)); // Degrees, not normalized!

                                    _shader.SetFloat($"u_LayerStrength[{i}]", layer.Strength);

                                    // Underwater parameters (slopes also in degrees now)
                                    _shader.SetInt($"u_LayerIsUnderwater[{i}]", layer.IsUnderwater ? 1 : 0);
                                    _shader.SetVec4($"u_LayerUnderwaterParams[{i}]", new Vector4(
                                        layer.UnderwaterHeightMax,
                                        layer.UnderwaterBlendDistance,
                                        layer.UnderwaterSlopeMin,  // Degrees, not normalized!
                                        layer.UnderwaterSlopeMax)); // Degrees, not normalized!
                                    _shader.SetFloat($"u_LayerUnderwaterBlend[{i}]", layer.UnderwaterBlendWithOthers);

                                    // PBR properties (from Material or legacy properties)
                                    float metallic = 0f;
                                    float smoothness = 0.5f;
                                    Vector4 albedoColor = Vector4.One;
                                    float normalStrength = 1.0f;
                                    int transparencyMode = 0; // 0 = Opaque
                                    
                                    if (layerMaterial != null)
                                    {
                                        // NEW: Load from Material (convert Roughness to Smoothness)
                                        metallic = layerMaterial.Metallic;
                                        smoothness = 1.0f - layerMaterial.Roughness; // Smoothness = 1 - Roughness
                                        albedoColor = new Vector4(
                                            layerMaterial.AlbedoColor[0],
                                            layerMaterial.AlbedoColor[1],
                                            layerMaterial.AlbedoColor[2],
                                            layerMaterial.AlbedoColor[3]);
                                        normalStrength = layerMaterial.NormalStrength;
                                        transparencyMode = (int)layerMaterial.TransparencyMode;
                                    }
                                    else
                                    {
                                        // LEGACY: Load from layer properties
                                        metallic = layer.Metallic;
                                        smoothness = layer.Smoothness;
                                    }
                                    
                                    _shader.SetFloat($"u_LayerMetallic[{i}]", metallic);
                                    _shader.SetFloat($"u_LayerSmoothness[{i}]", smoothness);
                                _shader.SetVec4($"u_LayerAlbedoColor[{i}]", albedoColor);
                                _shader.SetFloat($"u_LayerNormalStrength[{i}]", normalStrength);
                                _shader.SetInt($"u_LayerTransparencyMode[{i}]", transparencyMode);

                                // Debug log removed - was spamming console every frame
                            }
                            // OPTIMIZED: Removed else block for dummy layer initialization
                            // Shader only samples first u_LayerCount layers, so dummy data is unused
#pragma warning restore CS0618

                            // Configured layers successfully
                        }
                        else
                        {
                            // No terrain layers defined on this material.
                            // If the material is a regular ForwardBase-like material with an Albedo/Normal texture,
                            // bind those to the shader so the terrain can use them as a base instead of white.
                            _shader.SetInt("u_LayerCount", 0);
                            _shader.SetInt("u_DebugFaceColor", 0);

                            try
                            {
                                // Bind material albedo to texture unit 18 and normal to 19 (compatibility units)
                                int albedoTexId = Engine.Rendering.TextureCache.White1x1;
                                int normalTexId = Engine.Rendering.TextureCache.White1x1;
                                if (material.AlbedoTexture.HasValue)
                                {
                                    albedoTexId = Engine.Rendering.TextureCache.GetOrLoad(material.AlbedoTexture.Value, resolver);
                                }
                                if (material.NormalTexture.HasValue)
                                {
                                    normalTexId = Engine.Rendering.TextureCache.GetOrLoad(material.NormalTexture.Value, resolver);
                                }

                                GL.ActiveTexture(TextureUnit.Texture18);
                                GL.BindTexture(TextureTarget.Texture2D, albedoTexId != 0 ? albedoTexId : Engine.Rendering.TextureCache.White1x1);
                                _shader.SetInt("u_AlbedoTex", 18);

                                GL.ActiveTexture(TextureUnit.Texture19);
                                GL.BindTexture(TextureTarget.Texture2D, normalTexId != 0 ? normalTexId : Engine.Rendering.TextureCache.White1x1);
                                _shader.SetInt("u_NormalTex", 19);

                                // Set material properties
                                if (material.AlbedoColor != null && material.AlbedoColor.Length >= 4)
                                    _shader.SetVec4("u_AlbedoColor", new Vector4(material.AlbedoColor[0], material.AlbedoColor[1], material.AlbedoColor[2], material.AlbedoColor[3]));
                                _shader.SetFloat("u_Metallic", material.Metallic);
                                _shader.SetFloat("u_Smoothness", 1.0f - material.Roughness);
                                _shader.SetInt("u_TransparencyMode", material.TransparencyMode);
                            }
                            catch (Exception ex)
                            {
                                Engine.Utils.DebugLogger.Log($"[TerrainRenderer] Error binding fallback material textures: {ex.Message}");
                                GL.BindTexture(TextureTarget.Texture2D, Engine.Rendering.TextureCache.White1x1);
                            }
                        }
                    }
                    else
                    {
                        // Material not found, use fallback
                        _shader.SetInt("u_LayerCount", 0);
                        _shader.SetInt("u_DebugFaceColor", 0);
                        GL.BindTexture(TextureTarget.Texture2D, Engine.Rendering.TextureCache.White1x1);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TerrainRenderer] ❌ ERROR loading terrain material: {ex.Message}");
                    Console.WriteLine($"[TerrainRenderer] ❌ StackTrace: {ex.StackTrace}");
                    _shader.SetInt("u_LayerCount", 0);
                    GL.BindTexture(TextureTarget.Texture2D, Engine.Rendering.TextureCache.White1x1);
                }
            }
            else
            {
                // No material assigned, use fallback
                _shader.SetInt("u_LayerCount", 0);
                _shader.SetInt("u_DebugFaceColor", 0);
                GL.BindTexture(TextureTarget.Texture2D, Engine.Rendering.TextureCache.White1x1);
            }

            // CRITICAL: Must send weather uniforms AFTER shader.Use() (line 166)
            // Get weather values from WeatherManager with fallback to defaults
            try
            {
                var weather = Engine.Systems.WeatherManager.GetCurrentWeather();
                if (weather != null)
                {
                    _shader.SetFloat("u_RainIntensity", weather.RainIntensity);
                    _shader.SetFloat("u_SnowAccumulation", weather.SnowAccumulation);
                    _shader.SetFloat("u_SnowIntensity", weather.SnowIntensity);
                    _shader.SetFloat("u_Wetness", weather.Wetness);

                    _shader.SetFloat("u_SnowSlopeMin", weather.SnowSlopeMin);
                    _shader.SetFloat("u_SnowSlopeMax", weather.SnowSlopeMax);
                    _shader.SetFloat("u_SnowSparkle", weather.SnowSparkle);
                    _shader.SetFloat("u_SnowDisplacement", weather.SnowDisplacement);

                    // Load and bind snow material if assigned
                    if (weather.SnowMapMaterial.HasValue)
                    {
                        try
                        {
                            var snowMat = Engine.Assets.AssetDatabase.LoadMaterial(weather.SnowMapMaterial.Value);
                            if (snowMat != null)
                            {
                                // Load snow material runtime from cache
                                // Cache is updated by ApplyLiveMaterialUpdate when inspector changes values
                                Func<Guid, string?> snowResolver = g => Engine.Assets.AssetDatabase.TryGet(g, out var r) ? r.Path : null;
                                var snowRuntime = Engine.Rendering.MaterialRuntime.FromAsset(snowMat, snowResolver);

                                // CRITICAL FIX: Use units 13-15 to avoid conflict with IBL textures (10-12)
                                // Bind snow textures to dedicated texture units
                                GL.ActiveTexture(TextureUnit.Texture13);
                                GL.BindTexture(TextureTarget.Texture2D, snowRuntime.AlbedoTex);
                                _shader.SetInt("u_SnowAlbedoTex", 13);

                                GL.ActiveTexture(TextureUnit.Texture14);
                                GL.BindTexture(TextureTarget.Texture2D, snowRuntime.NormalTex);
                                _shader.SetInt("u_SnowNormalTex", 14);

                                GL.ActiveTexture(TextureUnit.Texture15);
                                GL.BindTexture(TextureTarget.Texture2D, snowRuntime.MetallicRoughnessTex);
                                _shader.SetInt("u_SnowMetallicRoughnessTex", 15);

                                // Send snow material properties
                                _shader.SetVec4("u_SnowAlbedoColor", new OpenTK.Mathematics.Vector4(
                                    snowRuntime.AlbedoColor[0], snowRuntime.AlbedoColor[1],
                                    snowRuntime.AlbedoColor[2], snowRuntime.AlbedoColor[3]));
                                _shader.SetFloat("u_SnowMetallic", snowRuntime.Metallic);
                                _shader.SetFloat("u_SnowRoughness", 1.0f - snowRuntime.Smoothness); // Convert smoothness to roughness
                                _shader.SetFloat("u_SnowNormalStrength", snowRuntime.NormalStrength);
                                _shader.SetVec2("u_SnowTextureTiling", new OpenTK.Mathematics.Vector2(
                                    snowRuntime.TextureTiling[0],
                                    snowRuntime.TextureTiling[1]));

                                // CRITICAL: Restore texture unit 0 as active
                                GL.ActiveTexture(TextureUnit.Texture0);
                            }
                            else
                            {
                                // Snow material not found - bind default white texture
                                BindDefaultSnowTextures();
                            }
                        }
                        catch (Exception ex)
                        {
                            Engine.Utils.DebugLogger.Log($"[TerrainRenderer] Failed to load snow material: {ex.Message}");
                            BindDefaultSnowTextures();
                        }
                    }
                    else
                    {
                        // No snow material assigned - use defaults
                        BindDefaultSnowTextures();
                    }
                }
                else
                {
                    // Fallback to safe defaults
                    _shader.SetFloat("u_RainIntensity", 0.0f);
                    _shader.SetFloat("u_SnowAccumulation", 0.0f);
                    _shader.SetFloat("u_SnowIntensity", 0.0f);
                    _shader.SetFloat("u_Wetness", 0.0f);
                    _shader.SetFloat("u_SnowSlopeMin", 0.0f);
                    _shader.SetFloat("u_SnowSlopeMax", 45.0f);
                    _shader.SetFloat("u_SnowSparkle", 0.5f);
                    _shader.SetFloat("u_SnowDisplacement", 0.5f);
                }
            }
            catch (Exception ex)
            {
                // Weather system not available - use safe defaults
                Engine.Utils.DebugLogger.Log($"[TerrainRenderer] Weather system unavailable: {ex.Message}");
                _shader.SetFloat("u_RainIntensity", 0.0f);
                _shader.SetFloat("u_SnowAccumulation", 0.0f);
                _shader.SetFloat("u_SnowIntensity", 0.0f);
                _shader.SetFloat("u_Wetness", 0.0f);
                _shader.SetFloat("u_SnowSlopeMin", 0.0f);
                _shader.SetFloat("u_SnowSlopeMax", 45.0f);
                _shader.SetFloat("u_SnowSparkle", 0.5f);
                _shader.SetFloat("u_SnowDisplacement", 0.5f);
            }

            // Check for errors before rendering
            var preRenderError = GL.GetError();
            if (preRenderError != ErrorCode.NoError)
            {
                Engine.Utils.DebugLogger.Log($"[TerrainRenderer] GL error BEFORE terrain.Render(): {preRenderError}");
            }

            // === INFINITE STREAMING TERRAIN MODE ===
            if (terrain.Mode == TerrainMode.InfiniteStreaming)
            {
                // Check if terrain parameters changed - reset tiles if needed
                bool parametersChanged = (_lastStreamingSeed != terrain.ProceduralSeed ||
                                         _lastNoiseScale != terrain.NoiseScale ||
                                         _lastOctaves != terrain.Octaves);

                if (parametersChanged && _tileManager != null)
                {
                    Engine.Utils.DebugLogger.Log("[TerrainRenderer] Terrain parameters changed - resetting tiles");
                    _tileManager.Reset();
                    _lastStreamingSeed = terrain.ProceduralSeed;
                    _lastNoiseScale = terrain.NoiseScale;
                    _lastOctaves = terrain.Octaves;
                }

                // Initialize tile streaming if not already done
                if (_tileManager == null)
                {
                    _tileManager = new Tile.TerrainTileManager();
                    _tileManager.TileGenerator = (tile) =>
                        Tile.TileCpuGenerator.GenerateCachedTile(terrain, tile.X, tile.Y, tile.Lod, tile.NeighborLods);
                    _tileManager.VegetationGenerator = (t, tx, ty) =>
                        VegetationGenerator.GenerateVegetationForTile(t, tx, ty, t.StreamingTileSize);
                    _tileManager.StartBackgroundWorker();
                    _lastStreamingSeed = terrain.ProceduralSeed;
                    _lastNoiseScale = terrain.NoiseScale;
                    _lastOctaves = terrain.Octaves;
                    Engine.Utils.DebugLogger.Log("[TerrainRenderer] Initialized infinite terrain streaming");
                }

                // Request tiles around camera (use world coordinates)
                // Tiles are generated in world space already (see TileCpuGenerator)
                _tileManager.RequestTilesAround(terrain, viewPos.X, viewPos.Z, terrain.StreamingRadius);

                // Update auto water plane (creates/repositions water at WaterLevel)
                if (terrain.EnableAutoWater)
                {
                    UpdateAutoWaterPlane(terrain, viewPos);
                }
                else if (terrain.AutoWaterEntity != null)
                {
                    // Remove water plane if auto water was disabled
                    RemoveAutoWaterPlane(terrain);
                }

                // Process GPU resource deletions from evicted tiles (thread-safe)
                _tileManager.ProcessGpuDeletions();

                // OPTIMIZED: Process up to 20 tile GPU uploads per frame for faster streaming
                // At 60fps: 20×60 = 1200 tiles/sec (was 5×60 = 300 tiles/sec)
                // Prevents visible holes when rotating camera quickly
                _tileManager.TryProcessUploads(tile =>
                {
                    if (tile.VerticesCpu == null || tile.IndicesCpu == null) return;

                    // Create GL buffers
                    int vao = GL.GenVertexArray();
                    int vbo = GL.GenBuffer();
                    int ebo = GL.GenBuffer();

                    GL.BindVertexArray(vao);

                    // Upload vertices
                    GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
                    GL.BufferData(BufferTarget.ArrayBuffer, tile.VerticesCpu.Length * sizeof(float), tile.VerticesCpu, BufferUsageHint.StaticDraw);

                    // Upload indices
                    GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
                    GL.BufferData(BufferTarget.ElementArrayBuffer, tile.IndicesCpu.Length * sizeof(uint), tile.IndicesCpu, BufferUsageHint.StaticDraw);

                    // Setup vertex attributes (pos, normal, uv)
                    GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 0);
                    GL.EnableVertexAttribArray(0);
                    GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 3 * sizeof(float));
                    GL.EnableVertexAttribArray(1);
                    GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, 8 * sizeof(float), 6 * sizeof(float));
                    GL.EnableVertexAttribArray(2);

                    GL.BindVertexArray(0);

                    // Generate vegetation for this tile
                    if (terrain.VegetationLayers != null && terrain.VegetationLayers.Length > 0)
                    {
                        var vegInstances = VegetationGenerator.GenerateVegetationForTile(terrain, tile.X, tile.Y, terrain.StreamingTileSize);
                        if (vegInstances.Count > 0)
                        {
                            tile.AttachVegetation(vegInstances);
                        }
                    }

                    // Mark as uploaded
                    tile.OnUploadedToGpu(vao, vbo, ebo);
                });

                // Use real deltaTime for smooth LOD transitions
                float deltaTime = Engine.Core.Time.DeltaTime;

                // Update fading-out tiles (cross-fade cleanup)
                _tileManager.UpdateFadingTiles(deltaTime);

                // === TILE CULLING ===
                // Setup frustum culling if enabled (OPTIMIZED: reuse instance instead of allocating)
                bool useFrustumCulling = terrain.EnableTileFrustumCulling;
                float cullingDistance = terrain.TileCullingDistance;
                Engine.Rendering.FrustumCuller? frustumCuller = null;

                if (useFrustumCulling)
                {
                    frustumCuller = _tileFrustumCuller;  // Reuse pre-allocated instance
                    Matrix4 viewProj = view * projection;
                    frustumCuller.ExtractPlanes(viewProj);
                }

                // Pre-compute squared culling distance to avoid sqrt in tight loop
                float cullingDistanceSq = cullingDistance * cullingDistance;

                // Frustum test function (System.Numerics.Vector3 for tile manager compatibility)
                Func<System.Numerics.Vector3, System.Numerics.Vector3, bool> frustumTest = (worldMin, worldMax) =>
                {
                    // Convert System.Numerics.Vector3 to OpenTK.Mathematics.Vector3 for frustum test
                    Vector3 worldMinOtk = new Vector3(worldMin.X, worldMin.Y, worldMin.Z);
                    Vector3 worldMaxOtk = new Vector3(worldMax.X, worldMax.Y, worldMax.Z);

                    // Distance culling (check tile center) - OPTIMIZED: use LengthSquared to avoid sqrt
                    if (cullingDistance > 0)
                    {
                        Vector3 tileCenter = (worldMinOtk + worldMaxOtk) * 0.5f;
                        float distanceSqToCamera = (tileCenter - viewPos).LengthSquared;
                        if (distanceSqToCamera > cullingDistanceSq)
                            return false;
                    }

                    // Frustum culling (check AABB)
                    if (frustumCuller != null)
                    {
                        return frustumCuller.TestAABB(worldMinOtk, worldMaxOtk);
                    }

                    return true;
                };

                // Render tiles with optional culling
                if (useFrustumCulling)
                {
                    // Use ForEachVisible with frustum test
                    _tileManager.ForEachVisible(frustumTest, tile =>
                    {
                        if (tile.Vao != 0 && tile.IndexCount > 0)
                        {
                            // Update LOD transition (dithered fade-in/fade-out)
                            tile.UpdateTransition(deltaTime);

                            // Set transition factor for dithered alpha in shader
                            _shader.SetFloat("u_LodTransition", tile.TransitionFactor);

                            GL.BindVertexArray(tile.Vao);
                            GL.DrawElements(PrimitiveType.Triangles, tile.IndexCount, DrawElementsType.UnsignedInt, 0);
                            // OPTIMIZATION: Don't unbind VAO between tiles (reduces state changes)
                        }
                    });
                }
                else
                {
                    // Render all tiles without culling (legacy behavior)
                    _tileManager.ForEachRenderable(tile =>
                    {
                        if (tile.Vao != 0 && tile.IndexCount > 0)
                        {
                            // Update LOD transition (dithered fade-in/fade-out)
                            tile.UpdateTransition(deltaTime);

                            // Set transition factor for dithered alpha in shader
                            _shader.SetFloat("u_LodTransition", tile.TransitionFactor);

                            GL.BindVertexArray(tile.Vao);
                            GL.DrawElements(PrimitiveType.Triangles, tile.IndexCount, DrawElementsType.UnsignedInt, 0);
                            // OPTIMIZATION: Don't unbind VAO between tiles (reduces state changes)
                        }
                    });
                }

                // Unbind VAO once after all tiles rendered
                GL.BindVertexArray(0);
            }
            else
            {
                // === CLASSIC SINGLE TERRAIN MODE ===
                // No LOD transition for single terrain (always fully opaque)
                _shader.SetFloat("u_LodTransition", 1.0f);
                terrain.Render(new System.Numerics.Vector3(viewPos.X, viewPos.Y, viewPos.Z));
            }

            // Check for errors after rendering
            var postRenderError = GL.GetError();
            if (postRenderError != ErrorCode.NoError)
            {
                Engine.Utils.DebugLogger.Log($"[TerrainRenderer] GL error AFTER terrain.Render(): {postRenderError}");
            }
            
            // Log first-frame debug info (only once per terrain)
            try
            {
                if (!_loggedFirstFrame)
                {
                    Engine.Utils.DebugLogger.Log($"[TerrainRenderer] First frame rendered: shader={_shader?.Handle}, VAO bound, LayerCount uniform set");
                    _loggedFirstFrame = true;
                }
            }
            catch { }

            // Water rendering removed: water plane and WaterForward shader have been purged

            // Cleanup: restore some GL state we modified
            GL.ActiveTexture(TextureUnit.Texture0);
            // restore previous program if any
            GL.UseProgram(prevProgram);
            // Restore culling state - let higher-level renderer manage culling
            // Don't force it off as that breaks backface culling for all subsequent renders
            GL.Enable(EnableCap.CullFace);
        }

        private void BindDefaultSnowTextures()
        {
            if (_shader == null) return;

            // CRITICAL FIX: Use units 13-15 to avoid conflict with IBL textures (10-12)
            // MaterialRuntime.Bind() uses units 10-12 for IBL, which was overwriting snow textures!

            // Bind default white textures for snow material
            GL.ActiveTexture(TextureUnit.Texture13);
            GL.BindTexture(TextureTarget.Texture2D, Engine.Rendering.TextureCache.White1x1);
            _shader.SetInt("u_SnowAlbedoTex", 13);

            GL.ActiveTexture(TextureUnit.Texture14);
            GL.BindTexture(TextureTarget.Texture2D, Engine.Rendering.TextureCache.White1x1); // Use white for default normal
            _shader.SetInt("u_SnowNormalTex", 14);

            GL.ActiveTexture(TextureUnit.Texture15);
            GL.BindTexture(TextureTarget.Texture2D, Engine.Rendering.TextureCache.White1x1);
            _shader.SetInt("u_SnowMetallicRoughnessTex", 15);

            // Default snow material properties (realistic snow albedo ~65-70%)
            _shader.SetVec4("u_SnowAlbedoColor", new OpenTK.Mathematics.Vector4(0.65f, 0.68f, 0.75f, 1.0f));
            _shader.SetFloat("u_SnowMetallic", 0.0f);
            _shader.SetFloat("u_SnowRoughness", 0.3f);
            _shader.SetFloat("u_SnowNormalStrength", 1.0f);
            _shader.SetVec2("u_SnowTextureTiling", new OpenTK.Mathematics.Vector2(0.1f, 0.1f));

            // CRITICAL: Restore texture unit 0 as active
            GL.ActiveTexture(TextureUnit.Texture0);
        }

        private void OnMaterialSaved(Guid materialGuid)
        {
            // Invalidate cached material when it's saved (edited in inspector)
            Engine.Utils.DebugLogger.Log($"[TerrainRenderer] ⚠️ OnMaterialSaved CALLED for Material {materialGuid}");
            if (_materialCache.Remove(materialGuid))
            {
                Engine.Utils.DebugLogger.Log($"[TerrainRenderer] Material {materialGuid} invalidated from cache - will reload on next frame");
            }
            else
            {
                Engine.Utils.DebugLogger.Log($"[TerrainRenderer] Material {materialGuid} was NOT in cache (already invalidated or never loaded)");
            }
        }

        /// <summary>
        /// Clear material cache - called when AssetDatabase cache is cleared during PlayMode transitions
        /// CRITICAL: This prevents using stale material/texture references after PlayMode exit
        /// </summary>
        public void ClearMaterialCache()
        {
            _materialCache.Clear();
            Console.WriteLine($"[TerrainRenderer] Material cache cleared ({_materialCache.Count} entries removed)");
        }

        /// <summary>
        /// CRITICAL FIX: Force reload shader from ShaderLibrary
        /// Call this after ShaderLibrary.ReloadShader() to update the cached reference
        /// </summary>
        public void ReloadShader()
        {
            try
            {
                Console.WriteLine($"[TerrainRenderer] Forcing shader reload, old handle={_shader?.Handle ?? 0}");
                _shader = null; // Clear old reference

                string shaderName = "TerrainForward";
                try
                {
                    if (Environment.GetEnvironmentVariable("TERRAIN_DEBUG_SHADER") == "1")
                    {
                        shaderName = "TerrainDebug";
                    }
                }
                catch { }

                _shader = LoadTerrainShader(shaderName);
                if (_shader == null)
                {
                    Engine.Utils.DebugLogger.Log($"[TerrainRenderer] CRITICAL: Failed to reload {shaderName} shader!");
                }
                else
                {
                    Console.WriteLine($"[TerrainRenderer] ✓ Shader reloaded successfully, new handle={_shader.Handle}");
                }
            }
            catch (Exception ex)
            {
                Engine.Utils.DebugLogger.Log($"[TerrainRenderer] ERROR reloading shader: {ex.Message}");
            }
        }

        /// <summary>
        /// Render terrain tiles for shadow pass (InfiniteStreaming mode only)
        /// Simple geometry-only rendering with provided shadow shader
        /// </summary>
        public void RenderShadowPass(Engine.Rendering.ShaderProgram shadowShader, System.Numerics.Vector3 cameraPos)
        {
            if (_tileManager == null || shadowShader == null)
                return;

            // Render all visible tiles with shadow shader
            _tileManager.ForEachRenderable(tile =>
            {
                if (tile.Vao != 0 && tile.IndexCount > 0)
                {
                    // Bind tile VAO and render
                    GL.BindVertexArray(tile.Vao);
                    GL.DrawElements(PrimitiveType.Triangles, tile.IndexCount, DrawElementsType.UnsignedInt, 0);
                }
            });

            GL.BindVertexArray(0);
        }

        private MaterialAsset? GetMaterialCached(Guid materialGuid)
        {
            // Try to get from cache first
            if (_materialCache.TryGetValue(materialGuid, out var cached))
            {
                return cached;
            }

            // Not in cache, load from disk
            Console.WriteLine($"[TerrainRenderer] CACHE MISS - Loading material {materialGuid} from disk");
            try
            {
                var material = AssetDatabase.LoadMaterial(materialGuid);
                if (material != null)
                {
                    Console.WriteLine($"[TerrainRenderer] Material loaded successfully: Name={material.Name}, Guid={material.Guid}");
                    // Ensure texture streaming is initiated and upload pending textures
                    try
                    {
                        Engine.Rendering.TextureCache.Initialize();

                        // Schedule loads for common material textures so they can be uploaded
                        if (material.AlbedoTexture.HasValue) Engine.Rendering.TextureCache.GetOrLoad(material.AlbedoTexture.Value, guid => AssetDatabase.TryGet(guid, out var r) ? r.Path : null);
                        if (material.NormalTexture.HasValue) Engine.Rendering.TextureCache.GetOrLoad(material.NormalTexture.Value, guid => AssetDatabase.TryGet(guid, out var r) ? r.Path : null);
                        if (material.MetallicTexture.HasValue) Engine.Rendering.TextureCache.GetOrLoad(material.MetallicTexture.Value, guid => AssetDatabase.TryGet(guid, out var r) ? r.Path : null);
                        if (material.RoughnessTexture.HasValue) Engine.Rendering.TextureCache.GetOrLoad(material.RoughnessTexture.Value, guid => AssetDatabase.TryGet(guid, out var r) ? r.Path : null);
                        if (material.MetallicRoughnessTexture.HasValue) Engine.Rendering.TextureCache.GetOrLoad(material.MetallicRoughnessTexture.Value, guid => AssetDatabase.TryGet(guid, out var r) ? r.Path : null);
                        if (material.OcclusionTexture.HasValue) Engine.Rendering.TextureCache.GetOrLoad(material.OcclusionTexture.Value, guid => AssetDatabase.TryGet(guid, out var r) ? r.Path : null);
                        if (material.EmissiveTexture.HasValue) Engine.Rendering.TextureCache.GetOrLoad(material.EmissiveTexture.Value, guid => AssetDatabase.TryGet(guid, out var r) ? r.Path : null);
                        if (material.HeightTexture.HasValue) Engine.Rendering.TextureCache.GetOrLoad(material.HeightTexture.Value, guid => AssetDatabase.TryGet(guid, out var r) ? r.Path : null);

                        // Process pending uploads immediately (main thread / GL context)
                        try { Engine.Rendering.TextureCache.FlushPendingUploads(50); } catch { }
                    }
                    catch { }

                    _materialCache[materialGuid] = material;
                    Console.WriteLine($"[TerrainRenderer] Material {materialGuid} loaded and cached successfully");
                    return material;
                }
                else
                {
                    Console.WriteLine($"[TerrainRenderer] ERROR: LoadMaterial returned NULL for {materialGuid}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TerrainRenderer] EXCEPTION loading material {materialGuid}: {ex.Message}");
                Engine.Utils.DebugLogger.Log($"[TerrainRenderer] Failed to load material {materialGuid}: {ex.Message}");
            }

            Console.WriteLine($"[TerrainRenderer] Failed to load material {materialGuid} - returning null");
            return null;
        }

        /// <summary>
        /// Invalidate cached material to force reload on next frame (for live editing)
        /// </summary>
        public void InvalidateMaterialCache(Guid materialGuid)
        {
            if (_materialCache.Remove(materialGuid))
            {
                // Material removed from cache, will be reloaded on next frame
            }
        }

        /// <summary>
        /// Update cached material with new values for live editing (without disk reload)
        /// </summary>
        public void UpdateMaterialCache(Guid materialGuid, MaterialAsset material)
        {
            // Update or add to cache with the new material data
            _materialCache[materialGuid] = material;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                // Unsubscribe from material changes
                if (_subscribedToMaterialChanges)
                {
                    AssetDatabase.MaterialSaved -= OnMaterialSaved;
                    _subscribedToMaterialChanges = false;
                }

                _shader?.Dispose();
                _materialCache.Clear();

                // Dispose tile manager
                _tileManager?.Dispose();
                _tileManager = null;

                _disposed = true;
            }
        }

        /// <summary>
        /// Get streaming statistics (for inspector/debugging).
        /// </summary>
        /// <summary>
        /// Reset streaming tile manager - clears all tiles and cache.
        /// </summary>
        public void ResetStreamingTiles()
        {
            if (_tileManager != null)
            {
                _tileManager.Reset();
            }
        }

        /// <summary>
        /// Clear and dispose the tile manager.
        /// Call this when switching from Infinite Streaming to Single Terrain mode.
        /// </summary>
        public void ClearTileManager()
        {
            if (_tileManager != null)
            {
                try
                {
                    _tileManager.Dispose();
                }
                catch { }
                _tileManager = null;
                _lastStreamingSeed = 0;
                _lastNoiseScale = 0;
                _lastOctaves = 0;
            }
        }

        /// <summary>
        /// Initialize tile manager and force synchronous tile generation/upload.
        /// Call this after scene load to ensure tiles are ready before first shadow pass.
        /// </summary>
        public void InitializeAndProcessTiles(Engine.Components.Terrain terrain, OpenTK.Mathematics.Vector3 cameraPos, int maxTiles = 30, Matrix4 modelMatrix = default)
        {
            if (terrain.Mode != TerrainMode.InfiniteStreaming) return;

            // Initialize tile manager if not already done
            if (_tileManager == null)
            {
                _tileManager = new Tile.TerrainTileManager();
                _tileManager.TileGenerator = (tile) =>
                    Tile.TileCpuGenerator.GenerateCachedTile(terrain, tile.X, tile.Y, tile.Lod, tile.NeighborLods);
                _tileManager.VegetationGenerator = (t, tx, ty) =>
                    VegetationGenerator.GenerateVegetationForTile(t, tx, ty, t.StreamingTileSize);
                _tileManager.StartBackgroundWorker();
                _lastStreamingSeed = terrain.ProceduralSeed;
                _lastNoiseScale = terrain.NoiseScale;
                _lastOctaves = terrain.Octaves;
                Engine.Utils.DebugLogger.Log("[TerrainRenderer] Initialized tile manager for scene load");
            }

            // Request tiles around camera (use world coordinates)
            // Tiles are generated in world space already (see TileCpuGenerator)
            _tileManager.RequestTilesAround(terrain, cameraPos.X, cameraPos.Z, terrain.StreamingRadius);

            // Force synchronous processing of tiles for immediate availability
            ForceProcessTileUploads(terrain, maxTiles);
        }

        /// <summary>
        /// Force synchronous processing of pending tile uploads.
        /// Useful after scene load to ensure vegetation is immediately available.
        /// </summary>
        public void ForceProcessTileUploads(Engine.Components.Terrain terrain, int maxTiles = 20)
        {
            if (_tileManager == null) return;

            // Process GPU resource deletions first (thread-safe)
            _tileManager.ProcessGpuDeletions();

            int processed = 0;
            _tileManager.TryProcessUploads(tile =>
            {
                if (processed >= maxTiles) return;
                if (tile.VerticesCpu == null || tile.IndicesCpu == null) return;

                // Create GL buffers
                int vao = OpenTK.Graphics.OpenGL4.GL.GenVertexArray();
                int vbo = OpenTK.Graphics.OpenGL4.GL.GenBuffer();
                int ebo = OpenTK.Graphics.OpenGL4.GL.GenBuffer();

                OpenTK.Graphics.OpenGL4.GL.BindVertexArray(vao);

                // Upload vertices
                OpenTK.Graphics.OpenGL4.GL.BindBuffer(OpenTK.Graphics.OpenGL4.BufferTarget.ArrayBuffer, vbo);
                OpenTK.Graphics.OpenGL4.GL.BufferData(OpenTK.Graphics.OpenGL4.BufferTarget.ArrayBuffer, tile.VerticesCpu.Length * sizeof(float), tile.VerticesCpu, OpenTK.Graphics.OpenGL4.BufferUsageHint.StaticDraw);

                // Upload indices
                OpenTK.Graphics.OpenGL4.GL.BindBuffer(OpenTK.Graphics.OpenGL4.BufferTarget.ElementArrayBuffer, ebo);
                OpenTK.Graphics.OpenGL4.GL.BufferData(OpenTK.Graphics.OpenGL4.BufferTarget.ElementArrayBuffer, tile.IndicesCpu.Length * sizeof(uint), tile.IndicesCpu, OpenTK.Graphics.OpenGL4.BufferUsageHint.StaticDraw);

                // Setup vertex attributes (pos, normal, uv)
                OpenTK.Graphics.OpenGL4.GL.VertexAttribPointer(0, 3, OpenTK.Graphics.OpenGL4.VertexAttribPointerType.Float, false, 8 * sizeof(float), 0);
                OpenTK.Graphics.OpenGL4.GL.EnableVertexAttribArray(0);
                OpenTK.Graphics.OpenGL4.GL.VertexAttribPointer(1, 3, OpenTK.Graphics.OpenGL4.VertexAttribPointerType.Float, false, 8 * sizeof(float), 3 * sizeof(float));
                OpenTK.Graphics.OpenGL4.GL.EnableVertexAttribArray(1);
                OpenTK.Graphics.OpenGL4.GL.VertexAttribPointer(2, 2, OpenTK.Graphics.OpenGL4.VertexAttribPointerType.Float, false, 8 * sizeof(float), 6 * sizeof(float));
                OpenTK.Graphics.OpenGL4.GL.EnableVertexAttribArray(2);

                OpenTK.Graphics.OpenGL4.GL.BindVertexArray(0);

                // Generate vegetation for this tile
                if (terrain.VegetationLayers != null && terrain.VegetationLayers.Length > 0)
                {
                    var vegInstances = VegetationGenerator.GenerateVegetationForTile(terrain, tile.X, tile.Y, terrain.StreamingTileSize);
                    if (vegInstances.Count > 0)
                    {
                        tile.AttachVegetation(vegInstances);
                    }
                }

                // Mark as uploaded
                tile.OnUploadedToGpu(vao, vbo, ebo);
                processed++;
            });
        }

        /// <summary>
        /// Get all vegetation instances from all renderable tiles (Infinite Streaming mode only).
        /// Returns a dictionary of layer index -> list of transforms, ready for VegetationRenderer.UpdateBatch().
        /// OPTIMIZATION: Filters tiles by distance and frustum to avoid spawning trees on all loaded tiles.
        /// </summary>
        public System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<OpenTK.Mathematics.Matrix4>>? GetStreamingVegetationInstances(
            Vector3 cameraPos, OpenTK.Mathematics.Matrix4 viewProjMatrix, Engine.Rendering.FrustumCuller frustumCuller,
            Engine.Components.Terrain terrain)
        {
            if (_tileManager == null || terrain == null) return null;

            var result = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<OpenTK.Mathematics.Matrix4>>();
            float tileSize = terrain.StreamingTileSize;

            // Collect instances from visible tiles only (distance + frustum culling)
            _tileManager.ForEachRenderable(tile =>
            {
                if (tile.VegetationInstances == null || tile.VegetationInstances.Count == 0)
                    return;

                // Compute tile center position (world space)
                float tileCenterX = tile.X * tileSize + tileSize * 0.5f;
                float tileCenterZ = tile.Y * tileSize + tileSize * 0.5f;
                var tileCenterPos = new Vector3(tileCenterX, 0, tileCenterZ);

                // Find max render distance for this tile's vegetation layers
                float maxRenderDist = 0f;
                float maxCullingSphereRadius = 0f;
                if (terrain.VegetationLayers != null)
                {
                    foreach (var layer in terrain.VegetationLayers)
                    {
                        if (layer == null || layer.IsGrassLayer || layer.IsRockLayer) continue; // Skip grass/rocks (handled separately)
                        maxRenderDist = Math.Max(maxRenderDist, layer.MaxRenderDistance);
                        maxCullingSphereRadius = Math.Max(maxCullingSphereRadius, layer.CullingSphereRadius);
                    }
                }
                if (maxRenderDist <= 0) maxRenderDist = 500f; // Default fallback
                if (maxCullingSphereRadius <= 0) maxCullingSphereRadius = 5f;

                // OPTIMIZATION: Distance culling - skip tiles beyond render distance
                float dx = tileCenterPos.X - cameraPos.X;
                float dz = tileCenterPos.Z - cameraPos.Z;
                float distToTile = (float)Math.Sqrt(dx * dx + dz * dz);
                if (distToTile > maxRenderDist + tileSize * 0.71f)
                    return; // Skip this tile

                // FRUSTUM CULLING DISABLED - was causing trees to disappear when looked at
                // The frustum planes extracted from ViewProjection seem inverted
                // TODO: Fix FrustumCuller to work correctly with OpenTK matrices
                // float maxTreeHeight = Math.Max(50f, maxCullingSphereRadius * 5f);
                // var tileMin = new Vector3(tile.X * tileSize, -10f, tile.Y * tileSize);
                // var tileMax = new Vector3((tile.X + 1) * tileSize, maxTreeHeight, (tile.Y + 1) * tileSize);
                // if (!frustumCuller.TestAABB(tileMin, tileMax))
                //     return; // Skip this tile

                // Tile is visible - collect instances
                foreach (var kvp in tile.VegetationInstances)
                {
                    int layerIndex = kvp.Key;
                    var instances = kvp.Value;

                    if (!result.ContainsKey(layerIndex))
                    {
                        result[layerIndex] = new System.Collections.Generic.List<OpenTK.Mathematics.Matrix4>();
                    }

                    result[layerIndex].AddRange(instances);
                }
            });

            return result.Count > 0 ? result : null;
        }

        public (int loaded, int renderable, int loading, float memoryMB)? GetStreamingStats()
        {
            if (_tileManager == null) return null;
            return (_tileManager.LoadedTiles, _tileManager.RenderableTiles, _tileManager.LoadingTiles, _tileManager.GetMemoryUsageMB());
        }

        /// <summary>
        /// Check if there are any loaded streaming tiles.
        /// Returns false if not in streaming mode or no tiles loaded yet.
        /// </summary>
        public bool HasLoadedTiles()
        {
            return _tileManager != null && _tileManager.LoadedTiles > 0;
        }

        /// <summary>
        /// Iterate over all renderable tiles (for grass rendering, etc.)
        /// Each tile provides its VAO, IndexCount, and world position (X, Y are tile coordinates).
        /// </summary>
        /// <param name="action">Action receiving (VAO, IndexCount, tileX, tileY, tileSize)</param>
        public void ForEachRenderableTile(Action<int, int, int, int, float> action, float tileSize)
        {
            if (_tileManager == null) return;
            _tileManager.ForEachRenderable(tile =>
            {
                if (tile.Vao != 0 && tile.IndexCount > 0)
                {
                    action(tile.Vao, tile.IndexCount, tile.X, tile.Y, tileSize);
                }
            });
        }

        /// <summary>
        /// Check if the terrain is in streaming mode and has active tiles.
        /// </summary>
        public bool HasActiveTiles => _tileManager != null && _tileManager.RenderableTiles > 0;

        // === AUTO WATER PLANE MANAGEMENT ===

        /// <summary>
        /// Update or create the auto water plane for infinite streaming mode.
        /// Creates a large water plane centered on the camera that covers all visible terrain.
        /// </summary>
        public void UpdateAutoWaterPlane(Engine.Components.Terrain terrain, Vector3 cameraPos)
        {
            if (terrain == null) return;
            if (terrain.Mode != TerrainMode.InfiniteStreaming) return;
            if (!terrain.EnableAutoWater) return;

            var scene = terrain.Entity?.Scene;
            if (scene == null) return;

            // Calculate water plane size based on streaming radius and tile size
            float tileSize = terrain.StreamingTileSize;
            int radius = terrain.StreamingRadius;
            float waterSize = (radius * 2 + 1) * tileSize * 2f + terrain.AutoWaterMargin * 2f;

            // Check if we need to create the water entity
            if (terrain.AutoWaterEntity == null || !scene.Entities.Contains(terrain.AutoWaterEntity))
            {
                // Create new water entity
                var waterEntity = scene.CreateEntity("Auto Water Plane");

                // Add transform
                var transform = new Engine.Components.TransformComponent();
                transform.Position = new OpenTK.Mathematics.Vector3(cameraPos.X, terrain.WaterLevel, cameraPos.Z);
                transform.Scale = OpenTK.Mathematics.Vector3.One;
                waterEntity.AddComponent(transform);

                // Add water plane component
                var waterPlane = new Engine.Components.WaterPlaneComponent();
                waterPlane.Size = waterSize;
                waterPlane.Resolution = terrain.AutoWaterResolution;
                waterPlane.TessellationEnabled = true;
                waterPlane.TessellationFactor = 16f;
                waterPlane.WaveMode = 0; // Global (follow weather)
                waterEntity.AddComponent(waterPlane);

                terrain.AutoWaterEntity = waterEntity;
                Console.WriteLine($"[TerrainRenderer] Created auto water plane: size={waterSize:F0}, level={terrain.WaterLevel:F1}");
            }
            else
            {
                // Update existing water entity position and size
                var waterEntity = terrain.AutoWaterEntity;
                var transform = waterEntity.GetComponent<Engine.Components.TransformComponent>();
                var waterPlane = waterEntity.GetComponent<Engine.Components.WaterPlaneComponent>();

                if (transform != null)
                {
                    // Keep water centered on camera (snap to grid to reduce jitter)
                    float snapSize = tileSize * 0.5f;
                    float snappedX = (float)Math.Floor(cameraPos.X / snapSize) * snapSize;
                    float snappedZ = (float)Math.Floor(cameraPos.Z / snapSize) * snapSize;

                    transform.Position = new OpenTK.Mathematics.Vector3(snappedX, terrain.WaterLevel, snappedZ);
                }

                if (waterPlane != null)
                {
                    // Update size if streaming radius changed
                    if (Math.Abs(waterPlane.Size - waterSize) > 10f)
                    {
                        waterPlane.Size = waterSize;
                        waterPlane.Resolution = terrain.AutoWaterResolution;
                        waterPlane.NeedsRegeneration = true;
                    }
                }
            }
        }

        /// <summary>
        /// Remove the auto water plane if it exists.
        /// Called when DisableAutoWater is set or when terrain is disposed.
        /// </summary>
        public void RemoveAutoWaterPlane(Engine.Components.Terrain terrain)
        {
            if (terrain?.AutoWaterEntity == null) return;

            var scene = terrain.Entity?.Scene;
            if (scene != null && scene.Entities.Contains(terrain.AutoWaterEntity))
            {
                // Call cleanup on components before removal
                foreach (var comp in terrain.AutoWaterEntity.GetAllComponents())
                {
                    try { comp.OnDetached(); } catch { }
                }
                scene.Entities.Remove(terrain.AutoWaterEntity);
                scene.Cache?.Invalidate();
                Console.WriteLine("[TerrainRenderer] Removed auto water plane");
            }
            terrain.AutoWaterEntity = null;
        }
    }
}

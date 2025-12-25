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
            // Culling ENABLED for terrain - triangles are wound CCW to face upward
            GL.Enable(EnableCap.CullFace);
            GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
            
            // Check if debug face coloring is enabled
            bool debugFaceColor = false;
            try { 
                debugFaceColor = Environment.GetEnvironmentVariable("TERRAIN_DEBUG_FACE_COLOR") == "1";
            } catch { }

            // Clear any previous GL errors before shader activation
            GL.GetError();

            _shader.Use();

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

                            // CRITICAL FIX: Initialize ALL array elements (MAX_LAYERS = 8) to avoid InvalidOperation
                            // GLSL requires all array elements to be initialized, even if not used
                            
                            #pragma warning disable CS0618 // Disable obsolete warnings for legacy texture properties
                            for (int i = 0; i < 8; i++)
                            {
                                if (i < layerCount)
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

                                    _shader.SetVec4($"u_LayerHeightSlope[{i}]", new Vector4(
                                        layer.HeightMin, layer.HeightMax,
                                        layer.SlopeMinDeg / 90f, layer.SlopeMaxDeg / 90f)); // Normalize slope to [0,1]

                                    _shader.SetFloat($"u_LayerStrength[{i}]", layer.Strength);

                                    // Underwater parameters
                                    _shader.SetInt($"u_LayerIsUnderwater[{i}]", layer.IsUnderwater ? 1 : 0);
                                    _shader.SetVec4($"u_LayerUnderwaterParams[{i}]", new Vector4(
                                        layer.UnderwaterHeightMax,
                                        layer.UnderwaterBlendDistance,
                                        layer.UnderwaterSlopeMin / 90f,
                                        layer.UnderwaterSlopeMax / 90f));
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
                                else
                                {
                                    // Initialize unused layers with dummy textures and default values
                                    GL.ActiveTexture(TextureUnit.Texture0 + i * 2);
                                    GL.BindTexture(TextureTarget.Texture2D, Engine.Rendering.TextureCache.White1x1);

                                    int albedoLoc = GL.GetUniformLocation(_shader.Handle, $"u_LayerAlbedo[{i}]");
                                    if (albedoLoc >= 0)
                                        GL.Uniform1(albedoLoc, i * 2);

                                    GL.ActiveTexture(TextureUnit.Texture0 + i * 2 + 1);
                                    GL.BindTexture(TextureTarget.Texture2D, Engine.Rendering.TextureCache.White1x1);

                                    int normalLoc = GL.GetUniformLocation(_shader.Handle, $"u_LayerNormal[{i}]");
                                    if (normalLoc >= 0)
                                        GL.Uniform1(normalLoc, i * 2 + 1);

                                    _shader.SetVec4($"u_LayerTilingOffset[{i}]", new Vector4(1f, 1f, 0f, 0f));
                                    _shader.SetInt($"u_LayerUseTriplanar[{i}]", 0);
                                    _shader.SetFloat($"u_LayerTriplanarScale[{i}]", 1.0f);
                                    _shader.SetFloat($"u_LayerTriplanarBlend[{i}]", 4.0f);
                                    _shader.SetVec4($"u_LayerStylize[{i}]", new Vector4(1f,1f,1f,0f));
                                    _shader.SetFloat($"u_LayerEmission[{i}]", 0f);
                                    _shader.SetVec4($"u_LayerHeightSlope[{i}]", new Vector4(0f, 0f, 0f, 0f));
                                    _shader.SetFloat($"u_LayerStrength[{i}]", 0f);
                                    _shader.SetInt($"u_LayerIsUnderwater[{i}]", 0);
                                    _shader.SetVec4($"u_LayerUnderwaterParams[{i}]", new Vector4(0f, 0f, 0f, 0f));
                                    _shader.SetFloat($"u_LayerUnderwaterBlend[{i}]", 0f);
                                    _shader.SetFloat($"u_LayerMetallic[{i}]", 0f);
                                    _shader.SetFloat($"u_LayerSmoothness[{i}]", 0.5f);
                                    _shader.SetVec4($"u_LayerAlbedoColor[{i}]", Vector4.One);
                                    _shader.SetFloat($"u_LayerNormalStrength[{i}]", 1.0f);
                                    _shader.SetInt($"u_LayerTransparencyMode[{i}]", 0);
                                }
                            }
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
                    Engine.Utils.DebugLogger.Log($"[TerrainRenderer] Error loading terrain material: {ex.Message}");
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

            // Render terrain
            terrain.Render(new System.Numerics.Vector3(viewPos.X, viewPos.Y, viewPos.Z));

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

        private MaterialAsset? GetMaterialCached(Guid materialGuid)
        {
            // Try to get from cache first
            if (_materialCache.TryGetValue(materialGuid, out var cached))
            {
                return cached;
            }

            // Not in cache, load from disk
            // PERFORMANCE: Disabled log
            // Console.WriteLine($"[TerrainRenderer] Loading material {materialGuid} from disk (cache miss)");
            try
            {
                var material = AssetDatabase.LoadMaterial(materialGuid);
                if (material != null)
                {
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
                    // PERFORMANCE: Disabled log
                    // Console.WriteLine($"[TerrainRenderer] Material {materialGuid} loaded and cached");
                    return material;
                }
            }
            catch (Exception ex)
            {
                Engine.Utils.DebugLogger.Log($"[TerrainRenderer] Failed to load material {materialGuid}: {ex.Message}");
            }

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
                _disposed = true;
            }
        }
    }
}

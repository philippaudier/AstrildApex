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
                    Console.WriteLine("[TerrainRenderer] Using debug shader: TerrainDebug");
                }
            }
            catch { }
            
            _shader = LoadTerrainShader(shaderName);
            if (_shader == null)
            {
                Console.WriteLine($"[TerrainRenderer] CRITICAL: Failed to load {shaderName} shader - terrain will not render!");
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
                    Console.WriteLine($"[TerrainRenderer] ERROR: Shader '{shaderName}' not found in ShaderLibrary");
                }
                
                return shader;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TerrainRenderer] ERROR: Failed to load shader '{shaderName}': {ex.Message}");
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
            int globalUBO = 0)
        {
            // Log first call only to avoid spamming
            try
            {
                if (!_loggedFirstFrame)
                {
                    if (Engine.Utils.DebugLogger.EnableVerbose)
                        Console.WriteLine($"[TerrainRenderer] RenderTerrain FIRST CALL: shadows={shadowsEnabled}, ssao={ssaoEnabled}");
                    _loggedFirstFrame = true;
                }
            }
            catch { }

            if (terrain == null)
            {
                Console.WriteLine("[TerrainRenderer] Terrain is null!");
                return;
            }
            if (_shader == null)
            {
                _shader = LoadTerrainShader("TerrainForward");
                if (_shader == null)
                {
                    Console.WriteLine("[TerrainRenderer] CRITICAL: Failed to load TerrainForward shader - terrain will not render!");
                    return;
                }
            }
            
            // Verify shader is still valid (handle might be invalidated after PlayMode changes)
            if (!GL.IsProgram(_shader.Handle) || _shader.Handle == 0)
            {
                // Force reload from ShaderLibrary to clear cache
                Engine.Rendering.ShaderLibrary.ReloadShader("TerrainForward");
                _shader = Engine.Rendering.ShaderLibrary.GetShaderByName("TerrainForward");
                
                if (_shader == null || _shader.Handle == 0)
                {
                    Console.WriteLine("[TerrainRenderer] CRITICAL: Failed to reload shader after invalidation!");
                    return;
                }
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
            GL.Disable(EnableCap.CullFace);
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
                    Console.WriteLine($"[TerrainRenderer] WARNING: Could not find 'Global' uniform block in shader!");
                }
            }

            // Check for OpenGL errors after shader activation
            var shaderError = GL.GetError();
            if (shaderError != ErrorCode.NoError)
            {
                Console.WriteLine($"[TerrainRenderer] GL error after shader.Use(): {shaderError}");
            }

            // Verify shader is active (only in debug mode for performance)
            #if DEBUG
            int currentProgram = GL.GetInteger(GetPName.CurrentProgram);
            if (currentProgram != _shader.Handle)
            {
                Console.WriteLine($"[TerrainRenderer] ERROR: Failed to activate shader! Expected {_shader.Handle}, got {currentProgram}");
                return;
            }
            #endif

            // Set matrices - use provided model matrix or Identity if not specified
            var model = modelMatrix == default ? Matrix4.Identity : modelMatrix;
            _shader.SetMat4("u_Model", model);
            _shader.SetMat4("u_View", view);
            _shader.SetMat4("u_Projection", projection);

            // Calculate normal matrix from model matrix
            var normalMat = new Matrix3(model);
            _shader.SetMat3("u_NormalMat", normalMat);

            // Set camera and lighting
            _shader.SetVec3("u_ViewPos", viewPos);
            _shader.SetVec3("uCameraPos", viewPos); // Compatibility
            _shader.SetVec3("u_LightDir", lightDir);
            _shader.SetVec3("u_LightColor", lightColor);

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
                    Console.WriteLine($"[TerrainRenderer] ERROR setting shadow uniforms: {err}");
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
                        if (Engine.Utils.DebugLogger.EnableVerbose) Console.WriteLine($"[TerrainRenderer] Using material {material.Guid} (name={material.Name}) for terrain");
                        if (material.TerrainLayers == null)
                        {
                            if (Engine.Utils.DebugLogger.EnableVerbose) Console.WriteLine("[TerrainRenderer] Material has no TerrainLayers (null)");
                        }
                        else
                        {
                            if (Engine.Utils.DebugLogger.EnableVerbose) Console.WriteLine($"[TerrainRenderer] Material TerrainLayers length={material.TerrainLayers.Length}");
                        }
                        // Configure terrain layers if they exist
                        if (material.TerrainLayers != null && material.TerrainLayers.Length > 0)
                        {
                            int layerCount = Math.Min(material.TerrainLayers.Length, 8); // MAX_LAYERS = 8
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
                                    var layer = material.TerrainLayers[i];

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
                                            Console.WriteLine($"[TerrainRenderer] ERROR setting u_LayerAlbedo[{i}]: {err}");
                                    }

                                        // Debug: log bound texture handle for this layer's albedo
                                        try { if (Engine.Utils.DebugLogger.EnableVerbose) Console.WriteLine($"[TerrainRenderer] Bound Layer {i} Albedo -> unit={i*2}, handleBound={(GL.GetInteger(GetPName.TextureBinding2D))}"); } catch { }

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
                                            Console.WriteLine($"[TerrainRenderer] ERROR setting u_LayerNormal[{i}]: {err}");
                                    }

                                        try { if (Engine.Utils.DebugLogger.EnableVerbose) Console.WriteLine($"[TerrainRenderer] Bound Layer {i} Normal -> unit={i*2+1}, handleBound={(GL.GetInteger(GetPName.TextureBinding2D))}"); } catch { }

                                    // Set layer parameters (UV transform comes from layer, not material)
                                    _shader.SetVec4($"u_LayerTilingOffset[{i}]", new Vector4(
                                        layer.Tiling[0], layer.Tiling[1],
                                        layer.Offset[0], layer.Offset[1]));

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
                                Console.WriteLine($"[TerrainRenderer] Error binding fallback material textures: {ex.Message}");
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
                    Console.WriteLine($"[TerrainRenderer] Error loading terrain material: {ex.Message}");
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

            // Check for errors before rendering
            var preRenderError = GL.GetError();
            if (preRenderError != ErrorCode.NoError)
            {
                Console.WriteLine($"[TerrainRenderer] GL error BEFORE terrain.Render(): {preRenderError}");
            }

            // Render terrain
            terrain.Render(new System.Numerics.Vector3(viewPos.X, viewPos.Y, viewPos.Z));

            // Check for errors after rendering
            var postRenderError = GL.GetError();
            if (postRenderError != ErrorCode.NoError)
            {
                Console.WriteLine($"[TerrainRenderer] ❌ GL error AFTER terrain.Render(): {postRenderError}");
            }
            
            // Log first-frame debug info (only once per terrain)
            try
            {
                if (!_loggedFirstFrame)
                {
                    Console.WriteLine($"[TerrainRenderer] First frame rendered: shader={_shader?.Handle}, VAO bound, LayerCount uniform set");
                    _loggedFirstFrame = true;
                }
            }
            catch { }

            // Render water plane if enabled
            if (terrain.EnableWater && terrain.WaterMaterialGuid.HasValue)
            {
                try
                {
                    // Load water material (with caching)
                    var waterMaterial = GetMaterialCached(terrain.WaterMaterialGuid.Value);
                    if (waterMaterial != null)
                    {
                        // Get the appropriate shader for the water material
                        Engine.Rendering.ShaderProgram? waterShader = null;

                        // Try to get shader from material or use default ForwardBase
                        if (!string.IsNullOrEmpty(waterMaterial.Shader))
                        {
                            waterShader = Engine.Rendering.ShaderLibrary.GetShaderByName(waterMaterial.Shader);
                        }

                        if (waterShader == null)
                        {
                            waterShader = Engine.Rendering.ShaderLibrary.GetShaderByName("ForwardBase");
                        }

                        if (waterShader != null)
                        {
                            waterShader.Use();

                            // Set matrices
                            waterShader.SetMat4("u_Model", modelMatrix);
                            waterShader.SetMat4("u_View", view);
                            waterShader.SetMat4("u_Projection", projection);
                            waterShader.SetMat3("u_NormalMat", new Matrix3(modelMatrix));

                            // Set camera and lighting
                            waterShader.SetVec3("u_ViewPos", viewPos);
                            waterShader.SetVec3("uCameraPos", viewPos);
                            waterShader.SetVec3("u_LightDir", lightDir);
                            waterShader.SetVec3("u_LightColor", lightColor);

                            // Load material into runtime format and bind it
                            var waterMaterialRuntime = new Engine.Rendering.MaterialRuntime();
                            Func<Guid, string?> waterResolver = guid => Engine.Assets.AssetDatabase.TryGet(guid, out var r) ? r.Path : null;

                            // Load albedo texture
                            if (waterMaterial.AlbedoTexture.HasValue)
                            {
                                waterMaterialRuntime.AlbedoTex = Engine.Rendering.TextureCache.GetOrLoad(waterMaterial.AlbedoTexture.Value, waterResolver);
                            }
                            else
                            {
                                waterMaterialRuntime.AlbedoTex = Engine.Rendering.TextureCache.White1x1;
                            }

                            // Load normal texture
                            if (waterMaterial.NormalTexture.HasValue)
                            {
                                waterMaterialRuntime.NormalTex = Engine.Rendering.TextureCache.GetOrLoad(waterMaterial.NormalTexture.Value, waterResolver);
                            }
                            else
                            {
                                waterMaterialRuntime.NormalTex = Engine.Rendering.TextureCache.White1x1;
                            }

                            // Set material properties
                            waterMaterialRuntime.AlbedoColor = waterMaterial.AlbedoColor;
                            waterMaterialRuntime.Metallic = waterMaterial.Metallic;
                            waterMaterialRuntime.Smoothness = 1.0f - waterMaterial.Roughness; // Convert roughness to smoothness
                            waterMaterialRuntime.TransparencyMode = waterMaterial.TransparencyMode;
                            waterMaterialRuntime.NormalStrength = waterMaterial.NormalStrength;
                            waterMaterialRuntime.TextureTiling = waterMaterial.TextureTiling;
                            waterMaterialRuntime.TextureOffset = waterMaterial.TextureOffset;

                            // Bind material to shader
                            waterMaterialRuntime.Bind(waterShader);

                            // Set SSAO uniforms
                            waterShader.SetInt("u_SSAOEnabled", ssaoEnabled ? 1 : 0);
                            waterShader.SetFloat("u_SSAOStrength", ssaoStrength);
                            waterShader.SetVec2("u_ScreenSize", screenSize);

                            if (ssaoEnabled && ssaoTexture != 0)
                            {
                                GL.ActiveTexture(TextureUnit.Texture3);
                                GL.BindTexture(TextureTarget.Texture2D, ssaoTexture);
                                waterShader.SetInt("u_SSAOTexture", 3);
                            }

                            // Set shadow uniforms
                            if (shadowsEnabled && shadowTexture != 0)
                            {
                                GL.ActiveTexture(TextureUnit.Texture4);
                                GL.BindTexture(TextureTarget.Texture2D, shadowTexture);
                                waterShader.SetInt("u_ShadowMap", 4);
                                waterShader.SetInt("u_UseShadows", 1);
                                waterShader.SetFloat("u_ShadowBias", shadowBias);
                                waterShader.SetFloat("u_ShadowMapSize", shadowMapSize);
                                waterShader.SetFloat("u_ShadowStrength", shadowStrength);
                                waterShader.SetMat4("u_ShadowMatrix", shadowMatrix);
                            }
                            else
                            {
                                waterShader.SetInt("u_UseShadows", 0);
                            }

                            // Render water plane
                            terrain.RenderWater();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TerrainRenderer] Error rendering water plane: {ex.Message}");
                }
            }

            // Cleanup: restore some GL state we modified
            GL.ActiveTexture(TextureUnit.Texture0);
            // restore previous program if any
            GL.UseProgram(prevProgram);
            // Note: we deliberately do not attempt to restore polygon mode or cull face precisely here because
            // the higher-level renderer will set its preferred state; keep face culling disabled to match ViewportRenderer.
            GL.Disable(EnableCap.CullFace);
        }

        private void OnMaterialSaved(Guid materialGuid)
        {
            // Invalidate cached material when it's saved (edited in inspector)
            Console.WriteLine($"[TerrainRenderer] ⚠️ OnMaterialSaved CALLED for Material {materialGuid}");
            if (_materialCache.Remove(materialGuid))
            {
                Console.WriteLine($"[TerrainRenderer] Material {materialGuid} invalidated from cache - will reload on next frame");
            }
            else
            {
                Console.WriteLine($"[TerrainRenderer] Material {materialGuid} was NOT in cache (already invalidated or never loaded)");
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
                    _materialCache[materialGuid] = material;
                    // PERFORMANCE: Disabled log
                    // Console.WriteLine($"[TerrainRenderer] Material {materialGuid} loaded and cached");
                    return material;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TerrainRenderer] Failed to load material {materialGuid}: {ex.Message}");
            }

            return null;
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

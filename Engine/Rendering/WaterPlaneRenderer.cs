using System;
using System.IO;
using System.Collections.Generic;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Engine.Components;

namespace Engine.Rendering
{
    /// <summary>
    /// Renderer for WaterPlaneComponent - handles realistic ocean water with tessellation
    /// Based on GPU Gems techniques and Shadertoy implementations
    /// </summary>
    public sealed class WaterPlaneRenderer : IDisposable
    {
        private ShaderProgram? _shader;
        private ShaderProgram? _shaderNoTess; // Fallback without tessellation
        private bool _initialized = false;
        private bool _useTessellation = true;
        private bool _loggedOnce = false; // Prevent log spam

        // Cached mesh data per component (by entity ID)
        private readonly Dictionary<uint, WaterPlaneMesh> _meshes = new();

        private struct WaterPlaneMesh
        {
            public int Vao;
            public int Vbo;
            public int Ebo;
            public int IndexCount;
            public int VertexCount;
        }

        /// <summary>
        /// Initialize shaders
        /// </summary>
        public void Initialize()
        {
            if (_initialized) return;

            try
            {
                string basePath = Path.Combine(AppContext.BaseDirectory, "Engine", "Rendering", "Shaders", "Forward");
                string vertPath = Path.Combine(basePath, "WaterOcean.vert");
                string fragPath = Path.Combine(basePath, "WaterOcean.frag");
                string tescPath = Path.Combine(basePath, "WaterOcean.tesc");
                string tesePath = Path.Combine(basePath, "WaterOcean.tese");

                Utils.DebugLogger.Log($"[WaterPlaneRenderer] Looking for shaders at: {basePath}");
                Utils.DebugLogger.Log($"[WaterPlaneRenderer] Vert exists: {File.Exists(vertPath)}, Frag exists: {File.Exists(fragPath)}");
                Utils.DebugLogger.Log($"[WaterPlaneRenderer] Tesc exists: {File.Exists(tescPath)}, Tese exists: {File.Exists(tesePath)}");

                // Try to load with tessellation first
                if (File.Exists(tescPath) && File.Exists(tesePath))
                {
                    try
                    {
                        Utils.DebugLogger.Log("[WaterPlaneRenderer] Attempting to load tessellated shader...");
                        _shader = ShaderProgram.FromFiles(vertPath, fragPath, tescPath, tesePath);
                        _useTessellation = _shader.UsesTessellation;
                        Utils.DebugLogger.Log($"[WaterPlaneRenderer] Loaded shader with tessellation: {_useTessellation}, Program Handle: {_shader.Handle}");
                    }
                    catch (Exception ex)
                    {
                        Utils.DebugLogger.Log($"[WaterPlaneRenderer] Tessellation shader failed: {ex.Message}");
                        _useTessellation = false;
                    }
                }

                // Load fallback without tessellation
                if (_shader == null || !_useTessellation)
                {
                    Utils.DebugLogger.Log("[WaterPlaneRenderer] Loading non-tessellated shader...");
                    _shaderNoTess = ShaderProgram.FromFiles(vertPath, fragPath);
                    if (_shader == null) _shader = _shaderNoTess;
                    Utils.DebugLogger.Log($"[WaterPlaneRenderer] Using non-tessellated shader, Program Handle: {_shader?.Handle}");
                }

                _initialized = true;
                Utils.DebugLogger.Log("[WaterPlaneRenderer] Initialization complete");
            }
            catch (Exception ex)
            {
                Utils.DebugLogger.Log($"[WaterPlaneRenderer] Failed to initialize: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Upload mesh data to GPU for a water plane component
        /// </summary>
        public void UploadMesh(WaterPlaneComponent water, uint entityId)
        {
            if (!water.HasPendingMeshData) return;

            var vertices = water.GetPendingVertices();
            var indices = water.GetPendingIndices();
            if (vertices == null || indices == null) return;

            // Clean up old mesh if exists
            if (_meshes.TryGetValue(entityId, out var oldMesh))
            {
                if (oldMesh.Vao != 0) GL.DeleteVertexArray(oldMesh.Vao);
                if (oldMesh.Vbo != 0) GL.DeleteBuffer(oldMesh.Vbo);
                if (oldMesh.Ebo != 0) GL.DeleteBuffer(oldMesh.Ebo);
            }

            // Create VAO/VBO/EBO
            int vao = GL.GenVertexArray();
            int vbo = GL.GenBuffer();
            int ebo = GL.GenBuffer();

            GL.BindVertexArray(vao);

            // Upload vertex data
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

            // Upload index data
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

            // Vertex layout: position (3), normal (3), uv (2) = 8 floats = 32 bytes
            int stride = 8 * sizeof(float);

            // Position (location 0)
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);

            // Normal (location 1)
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));

            // UV (location 2)
            GL.EnableVertexAttribArray(2);
            GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, 6 * sizeof(float));

            GL.BindVertexArray(0);

            // Store mesh
            _meshes[entityId] = new WaterPlaneMesh
            {
                Vao = vao,
                Vbo = vbo,
                Ebo = ebo,
                IndexCount = indices.Length,
                VertexCount = vertices.Length / 8
            };

            // Update component GPU handles
            water.MeshVao = vao;
            water.MeshVbo = vbo;
            water.MeshEbo = ebo;
            water.IndexCount = indices.Length;

            // Clear pending data
            water.ClearPendingMeshData();

            Utils.DebugLogger.Log($"[WaterPlaneRenderer] Uploaded mesh: {water.VertexCount} vertices, {water.IndexCount / 3} triangles");
        }

        /// <summary>
        /// Render all water plane components in the scene
        /// </summary>
        public void Render(
            Scene.Scene scene,
            Matrix4 view,
            Matrix4 projection,
            Vector3 cameraPos,
            float time,
            WeatherComponent? weather,
            int depthTexture,
            int sceneColorTexture,
            int reflectionTexture,
            bool shadowsEnabled = false,
            int shadowTexture = 0,
            Matrix4? shadowMatrix = null,
            float shadowBias = 0.005f,
            float shadowMapSize = 2048f,
            float shadowStrength = 0.8f)
        {
            // Auto-initialize if needed
            if (!_initialized)
            {
                Initialize();
            }

            if (!_initialized || _shader == null)
            {
                Utils.DebugLogger.Log("[WaterPlaneRenderer] Cannot render - not initialized or no shader");
                return;
            }
            if (scene == null) return;

            // Find all water plane components
            foreach (var entity in scene.Entities)
            {
                var water = entity.GetComponent<WaterPlaneComponent>();
                if (water == null || !water.Enabled) continue;

                if (!_loggedOnce)
                    Utils.DebugLogger.Log($"[WaterPlaneRenderer] Found WaterPlaneComponent on entity {entity.Id}");

                // Generate mesh if needed
                if (water.NeedsRegeneration || !water.MeshGenerated)
                {
                    Utils.DebugLogger.Log("[WaterPlaneRenderer] Generating mesh...");
                    water.GenerateMesh();
                }

                // Upload mesh if pending
                if (water.HasPendingMeshData)
                {
                    Utils.DebugLogger.Log($"[WaterPlaneRenderer] Uploading mesh data...");
                    UploadMesh(water, entity.Id);
                }

                // Skip if no mesh
                if (!_meshes.TryGetValue(entity.Id, out var mesh) || mesh.Vao == 0)
                {
                    Utils.DebugLogger.Log($"[WaterPlaneRenderer] No mesh found for entity {entity.Id}, skipping");
                    continue;
                }

                if (!_loggedOnce)
                {
                    Utils.DebugLogger.Log($"[WaterPlaneRenderer] Rendering mesh: VAO={mesh.Vao}, IndexCount={mesh.IndexCount}");
                    _loggedOnce = true;
                }

                // Get transform
                var transform = entity.GetComponent<TransformComponent>();
                Matrix4 model = Matrix4.Identity;
                if (transform != null)
                {
                    model = Matrix4.CreateScale(transform.Scale.X, transform.Scale.Y, transform.Scale.Z) *
                            Matrix4.CreateRotationX(MathHelper.DegreesToRadians(transform.Rotation.X)) *
                            Matrix4.CreateRotationY(MathHelper.DegreesToRadians(transform.Rotation.Y)) *
                            Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(transform.Rotation.Z)) *
                            Matrix4.CreateTranslation(transform.Position.X, transform.Position.Y, transform.Position.Z);
                }

                // Select shader
                var shader = (_useTessellation && water.TessellationEnabled) ? _shader : (_shaderNoTess ?? _shader);
                shader.Use();

                // Set uniforms
                SetUniforms(shader, water, model, view, projection, cameraPos, time, weather,
                           depthTexture, sceneColorTexture, reflectionTexture, entity.Id,
                           shadowsEnabled, shadowTexture, shadowMatrix, shadowBias, shadowMapSize, shadowStrength);

                // Set up GL state for water rendering
                GL.Enable(EnableCap.Blend);
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                GL.Enable(EnableCap.DepthTest);
                GL.DepthFunc(DepthFunction.Less); // Use Less instead of Lequal to prevent water from rendering over ForwardBase objects
                GL.DepthMask(false); // Don't write to depth for transparent water
                GL.Disable(EnableCap.CullFace); // Double-sided rendering (visible from below and above)

                // Bind mesh
                GL.BindVertexArray(mesh.Vao);

                // Render with tessellation or regular triangles
                if (_useTessellation && water.TessellationEnabled && shader.UsesTessellation)
                {
                    GL.PatchParameter(PatchParameterInt.PatchVertices, 3);
                    GL.DrawElements(PrimitiveType.Patches, mesh.IndexCount, DrawElementsType.UnsignedInt, 0);
                }
                else
                {
                    GL.DrawElements(PrimitiveType.Triangles, mesh.IndexCount, DrawElementsType.UnsignedInt, 0);
                }

                GL.BindVertexArray(0);

                // Restore GL state
                GL.DepthMask(true);
                GL.DepthFunc(DepthFunction.Lequal); // Restore depth function to default
                GL.Disable(EnableCap.Blend);
            }
        }

        private void SetUniforms(
            ShaderProgram shader,
            WaterPlaneComponent water,
            Matrix4 model,
            Matrix4 view,
            Matrix4 projection,
            Vector3 cameraPos,
            float time,
            WeatherComponent? weather,
            int depthTexture,
            int sceneColorTexture,
            int reflectionTexture,
            uint objectId,
            bool shadowsEnabled,
            int shadowTexture,
            Matrix4? shadowMatrix,
            float shadowBias,
            float shadowMapSize,
            float shadowStrength)
        {
            // Transform matrices
            shader.SetMat4("u_Model", model);
            var normalMat = new Matrix3(model.Inverted().Transposed());
            shader.SetMat3("u_NormalMat", normalMat);
            shader.SetVec2("u_TextureTiling", new Vector2(1.0f, 1.0f));
            shader.SetVec2("u_TextureOffset", new Vector2(0.0f, 0.0f));

            // Time
            shader.SetFloat("u_Time", time);

            // Object ID
            shader.SetUInt("u_ObjectId", objectId);

            // === GERSTNER WAVES ===
            shader.SetInt("u_WaveIterations", water.WaveIterations);
            shader.SetFloat("u_WaveAmplitude", water.WaveAmplitude);
            shader.SetFloat("u_WaveFrequency", water.WaveFrequency);
            shader.SetFloat("u_WaveSpeed", water.WaveSpeed);
            shader.SetFloat("u_WaveSteepness", water.WaveSteepness);
            shader.SetFloat("u_WaveDrag", water.WaveDrag);
            shader.SetFloat("u_WaveDepth", water.WaveDepth);
            shader.SetVec2("u_WaveDirection", new Vector2(water.WaveDirectionX, water.WaveDirectionZ));

            // === FBM ===
            shader.SetInt("u_FbmEnabled", water.FbmEnabled ? 1 : 0);
            shader.SetInt("u_FbmOctaves", water.FbmOctaves);
            shader.SetFloat("u_FbmAmplitude", water.FbmAmplitude);
            shader.SetFloat("u_FbmFrequency", water.FbmFrequency);
            shader.SetFloat("u_FbmLacunarity", water.FbmLacunarity);
            shader.SetFloat("u_FbmPersistence", water.FbmPersistence);

            // === WEATHER INTEGRATION ===
            shader.SetInt("u_WaveMode", water.WaveMode);
            shader.SetFloat("u_WaveBlendFactor", water.WaveBlendFactor);
            shader.SetFloat("u_WaveSpeed_Local", water.WaveSpeed);
            shader.SetFloat("u_WaveAmplitude_Local", water.WaveAmplitude);
            shader.SetVec2("u_WaveDirection_Local", new Vector2(water.WaveDirectionX, water.WaveDirectionZ));

            if (weather != null)
            {
                shader.SetFloat("u_WindStrength", weather.WindStrength);
                shader.SetVec2("u_WindDirection", new Vector2(weather.WindDirectionX, weather.WindDirectionZ));
                shader.SetFloat("u_WindSpeed", weather.WindSpeed);
                shader.SetFloat("u_WindGustiness", weather.WindGustiness);
            }
            else
            {
                shader.SetFloat("u_WindStrength", 0.5f);
                shader.SetVec2("u_WindDirection", new Vector2(1.0f, 0.0f));
                shader.SetFloat("u_WindSpeed", 1.0f);
                shader.SetFloat("u_WindGustiness", 0.0f);
            }

            // === TESSELLATION ===
            shader.SetFloat("u_TessellationFactor", water.TessellationFactor);
            shader.SetFloat("u_TessellationMinDistance", water.TessellationMinDistance);
            shader.SetFloat("u_TessellationMaxDistance", water.TessellationMaxDistance);
            shader.SetFloat("u_TessellationMinLevel", water.TessellationMinLevel);
            shader.SetFloat("u_TessellationMaxLevel", water.TessellationMaxLevel);

            // === LOD ===
            shader.SetInt("u_LodEnabled", water.LodEnabled ? 1 : 0);
            shader.SetFloat("u_LodDistance1", water.LodDistance1);
            shader.SetFloat("u_LodDistance2", water.LodDistance2);
            shader.SetFloat("u_LodDistance3", water.LodDistance3);

            // === COLORS ===
            shader.SetVec4("u_ShallowColor", new Vector4(water.ShallowColor.X, water.ShallowColor.Y, water.ShallowColor.Z, water.ShallowColor.W));
            shader.SetVec4("u_DeepColor", new Vector4(water.DeepColor.X, water.DeepColor.Y, water.DeepColor.Z, water.DeepColor.W));
            shader.SetVec4("u_HorizonColor", new Vector4(water.HorizonColor.X, water.HorizonColor.Y, water.HorizonColor.Z, water.HorizonColor.W));
            shader.SetFloat("u_ColorDepthFade", water.ColorDepthFade);

            // === FRESNEL ===
            shader.SetFloat("u_FresnelPower", water.FresnelPower);
            shader.SetFloat("u_FresnelBias", water.FresnelBias);
            shader.SetFloat("u_FresnelScale", water.FresnelScale);

            // Debug modes: 0=off, 1=fresnel, 2=dot(N,V), 3=normal RGB, 4=tess normal, 5=fresnel power=2
            shader.SetInt("u_DebugFresnel", 0);

            // DEBUG: Log fresnel values and check uniform location
            if (!_loggedOnce)
            {
                int fresnelPowerLoc = OpenTK.Graphics.OpenGL4.GL.GetUniformLocation(shader.Handle, "u_FresnelPower");
                Utils.DebugLogger.Log($"[WaterOcean] FresnelPower={water.FresnelPower} (uniform loc={fresnelPowerLoc}), FresnelBias={water.FresnelBias}, FresnelScale={water.FresnelScale}");
            }

            // === SSS ===
            shader.SetInt("u_SSSEnabled", water.SSSEnabled ? 1 : 0);
            shader.SetVec3("u_SSSColor", new Vector3(water.SSSColor.X, water.SSSColor.Y, water.SSSColor.Z));
            shader.SetFloat("u_SSSIntensity", water.SSSIntensity);
            shader.SetFloat("u_SSSDistortion", water.SSSDistortion);
            shader.SetFloat("u_SSSPower", water.SSSPower);

            // === CREST FOAM ===
            shader.SetInt("u_CrestFoamEnabled", water.CrestFoamEnabled ? 1 : 0);
            shader.SetFloat("u_CrestFoamThreshold", water.CrestFoamThreshold);
            shader.SetFloat("u_CrestFoamIntensity", water.CrestFoamIntensity);
            shader.SetVec4("u_CrestFoamColor", new Vector4(water.CrestFoamColor.X, water.CrestFoamColor.Y, water.CrestFoamColor.Z, water.CrestFoamColor.W));
            shader.SetFloat("u_CrestFoamScale", water.CrestFoamScale);
            shader.SetFloat("u_CrestFoamSpeed", water.CrestFoamSpeed);

            // Foam texture (unit 13) - optional
            int foamTexture = 0;
            if (water.CrestFoamTextureGuid.HasValue && water.CrestFoamTextureGuid.Value != Guid.Empty)
            {
                foamTexture = TextureCache.GetOrLoad(
                    water.CrestFoamTextureGuid.Value,
                    guid => Engine.Assets.AssetDatabase.TryGet(guid, out var r) ? r.Path : null
                );
            }
            shader.SetInt("u_UseFoamTexture", foamTexture > 0 ? 1 : 0);

            // === SHORE FOAM ===
            shader.SetInt("u_ShoreFoamEnabled", water.ShoreFoamEnabled ? 1 : 0);
            shader.SetFloat("u_ShoreFoamDepth", water.ShoreFoamDepth);
            shader.SetFloat("u_ShoreFoamIntensity", water.ShoreFoamIntensity);
            shader.SetVec4("u_ShoreFoamColor", new Vector4(water.ShoreFoamColor.X, water.ShoreFoamColor.Y, water.ShoreFoamColor.Z, water.ShoreFoamColor.W));
            shader.SetFloat("u_ShoreFoamScale", water.ShoreFoamScale);
            shader.SetFloat("u_ShoreFoamSpeed", water.ShoreFoamSpeed);
            shader.SetFloat("u_ShoreFoamFade", water.ShoreFoamFade);
            shader.SetFloat("u_ShoreFoamEdgeSharpness", water.ShoreFoamEdgeSharpness);

            // === SPECULAR ===
            shader.SetFloat("u_SpecularIntensity", water.SpecularIntensity);
            shader.SetFloat("u_SpecularPower", water.SpecularPower);
            shader.SetFloat("u_Roughness", water.Roughness);

            // DEBUG: Log specular/roughness values
            if (!_loggedOnce)
            {
                Utils.DebugLogger.Log($"[WaterOcean] SpecularIntensity={water.SpecularIntensity}, SpecularPower={water.SpecularPower}, Roughness={water.Roughness}");
                _loggedOnce = true;
            }

            // === REFLECTIONS ===
            shader.SetInt("u_ReflectionEnabled", water.ReflectionEnabled ? 1 : 0);
            shader.SetFloat("u_ReflectionIntensity", water.ReflectionIntensity);
            shader.SetFloat("u_ReflectionDistortion", water.ReflectionDistortion);
            shader.SetInt("u_UsePlanarReflection", water.UsePlanarReflection ? 1 : 0);
            // CRITICAL: Use ReflectionBuffer.ReflectionViewProj just like WaterForward
            // This matrix is properly calculated during the reflection render pass
            shader.SetMat4("u_ReflectionViewProj", ReflectionBuffer.ReflectionViewProj);
            // Use same flip values as WaterForward (both off by default)
            shader.SetInt("u_FlipReflectionX", 0);
            shader.SetInt("u_FlipReflectionY", 0);

            // === REFRACTION ===
            shader.SetInt("u_RefractionEnabled", water.RefractionEnabled ? 1 : 0);
            shader.SetFloat("u_RefractionStrength", water.RefractionStrength);
            shader.SetFloat("u_RefractionChromatic", water.RefractionChromatic);

            // === CAUSTICS ===
            shader.SetInt("u_CausticsEnabled", water.CausticsEnabled ? 1 : 0);
            shader.SetFloat("u_CausticsIntensity", water.CausticsIntensity);
            shader.SetFloat("u_CausticsScale", water.CausticsScale);
            shader.SetFloat("u_CausticsSpeed", water.CausticsSpeed);
            shader.SetInt("u_CausticsOctaves", water.CausticsOctaves);
            shader.SetFloat("u_CausticsBrightness", water.CausticsBrightness);
            shader.SetFloat("u_CausticsSharpness", water.CausticsSharpness);
            shader.SetFloat("u_CausticsDistortion", water.CausticsDistortion);
            shader.SetFloat("u_CausticsDepthFalloff", water.CausticsDepthFalloff);
            shader.SetFloat("u_CausticsChromatic", water.CausticsChromatic);

            // === ABSORPTION ===
            shader.SetVec3("u_AbsorptionColor", new Vector3(water.AbsorptionColor.X, water.AbsorptionColor.Y, water.AbsorptionColor.Z));
            shader.SetFloat("u_AbsorptionStrength", water.AbsorptionStrength);

            // === NORMAL DETAIL ===
            shader.SetFloat("u_NormalStrength", water.NormalStrength);
            shader.SetInt("u_NormalIterations", water.NormalIterations);
            shader.SetFloat("u_NormalEpsilon", water.NormalEpsilon);

            // === TEXTURES ===
            // Depth texture (unit 10)
            GL.ActiveTexture(TextureUnit.Texture10);
            GL.BindTexture(TextureTarget.Texture2D, depthTexture);
            shader.SetInt("u_DepthTex", 10);

            // Scene color texture (unit 11)
            GL.ActiveTexture(TextureUnit.Texture11);
            GL.BindTexture(TextureTarget.Texture2D, sceneColorTexture);
            shader.SetInt("u_SceneColorTex", 11);

            // Planar reflection texture (unit 12)
            GL.ActiveTexture(TextureUnit.Texture12);
            GL.BindTexture(TextureTarget.Texture2D, reflectionTexture);
            shader.SetInt("u_PlanarReflectionTex", 12);

            // Foam texture (unit 13) - optional
            GL.ActiveTexture(TextureUnit.Texture13);
            GL.BindTexture(TextureTarget.Texture2D, foamTexture);
            shader.SetInt("u_FoamTex", 13);

            // === SHADOWS (for specular occlusion) ===
            if (shadowsEnabled && shadowTexture > 0 && shadowMatrix.HasValue)
            {
                shader.SetInt("u_UseShadows", 1);
                shader.SetFloat("u_ShadowBias", shadowBias);
                shader.SetFloat("u_ShadowMapSize", shadowMapSize);
                shader.SetFloat("u_ShadowStrength", shadowStrength);
                shader.SetMat4("u_ShadowMatrix", shadowMatrix.Value);

                // Shadow map (unit 17 - same as other renderers)
                GL.ActiveTexture(TextureUnit.Texture17);
                GL.BindTexture(TextureTarget.Texture2D, shadowTexture);
                shader.SetInt("u_ShadowMap", 17);
            }
            else
            {
                shader.SetInt("u_UseShadows", 0);
            }

            GL.ActiveTexture(TextureUnit.Texture0);
        }

        /// <summary>
        /// Remove mesh for an entity
        /// </summary>
        public void RemoveMesh(uint entityId)
        {
            if (_meshes.TryGetValue(entityId, out var mesh))
            {
                if (mesh.Vao != 0) GL.DeleteVertexArray(mesh.Vao);
                if (mesh.Vbo != 0) GL.DeleteBuffer(mesh.Vbo);
                if (mesh.Ebo != 0) GL.DeleteBuffer(mesh.Ebo);
                _meshes.Remove(entityId);
            }
        }

        public void Dispose()
        {
            // Clean up all meshes
            foreach (var kvp in _meshes)
            {
                var mesh = kvp.Value;
                if (mesh.Vao != 0) GL.DeleteVertexArray(mesh.Vao);
                if (mesh.Vbo != 0) GL.DeleteBuffer(mesh.Vbo);
                if (mesh.Ebo != 0) GL.DeleteBuffer(mesh.Ebo);
            }
            _meshes.Clear();

            // Clean up shaders
            _shader?.Dispose();
            _shaderNoTess?.Dispose();
            _shader = null;
            _shaderNoTess = null;
            _initialized = false;
        }
    }
}

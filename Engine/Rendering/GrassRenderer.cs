using System;
using System.Collections.Generic;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Engine.Assets;
using Engine.Components;
using Engine.Profiling;

namespace Engine.Rendering
{
    /// <summary>
    /// Renders dense grass coverage on terrain using geometry shaders.
    /// Generates grass blades directly from terrain mesh vertices on GPU.
    /// </summary>
    public sealed class GrassRenderer : IDisposable
    {
        private ShaderProgram? _grassShader = null;
        private bool _disposed = false;
        private int _debugFrameCount = 0;

        /// <summary>
        /// Stores grass data per terrain + layer combination
        /// </summary>
        private class GrassLayer
        {
            public Guid TerrainGuid;
            public int LayerIndex;
            public int TerrainVAO;  // Reference to terrain's VAO
            public int TerrainIndexCount;
            public GrassProperties Properties = new GrassProperties();
            public bool NeedsRebuild = true;
            public Matrix4 ModelMatrix = Matrix4.Identity;    // Terrain's model matrix (for shader)
            public Matrix3 NormalMatrix = Matrix3.Identity;   // Terrain's normal matrix
            public Vector3 TileCenterPosition = Vector3.Zero; // Tile center for distance culling (separate from ModelMatrix)
        }

        private readonly Dictionary<string, GrassLayer> _grassLayers = new();

        public GrassRenderer()
        {
            LoadShader();
        }

        private void LoadShader()
        {
            try
            {
                string vertPath = "Engine/Rendering/Shaders/Grass/TerrainGrass.vert";
                string geomPath = "Engine/Rendering/Shaders/Grass/TerrainGrass.geom";
                string fragPath = "Engine/Rendering/Shaders/Grass/TerrainGrass.frag";

                // Load and preprocess shaders
                string vertSrc = ShaderPreprocessor.ProcessShaderCached(vertPath);
                string geomSrc = ShaderPreprocessor.ProcessShaderCached(geomPath);
                string fragSrc = ShaderPreprocessor.ProcessShaderCached(fragPath);

                // Compile vertex shader
                int vs = GL.CreateShader(ShaderType.VertexShader);
                GL.ShaderSource(vs, vertSrc);
                GL.CompileShader(vs);
                GL.GetShader(vs, ShaderParameter.CompileStatus, out int vsStatus);
                if (vsStatus == 0)
                {
                    string log = GL.GetShaderInfoLog(vs);
                    Console.WriteLine($"[GrassRenderer] Vertex shader compile error: {log}");
                    GL.DeleteShader(vs);
                    return;
                }

                // Compile geometry shader
                int gs = GL.CreateShader(ShaderType.GeometryShader);
                GL.ShaderSource(gs, geomSrc);
                GL.CompileShader(gs);
                GL.GetShader(gs, ShaderParameter.CompileStatus, out int gsStatus);
                if (gsStatus == 0)
                {
                    string log = GL.GetShaderInfoLog(gs);
                    Console.WriteLine($"[GrassRenderer] Geometry shader compile error: {log}");
                    GL.DeleteShader(vs);
                    GL.DeleteShader(gs);
                    return;
                }

                // Compile fragment shader
                int fs = GL.CreateShader(ShaderType.FragmentShader);
                GL.ShaderSource(fs, fragSrc);
                GL.CompileShader(fs);
                GL.GetShader(fs, ShaderParameter.CompileStatus, out int fsStatus);
                if (fsStatus == 0)
                {
                    string log = GL.GetShaderInfoLog(fs);
                    Console.WriteLine($"[GrassRenderer] Fragment shader compile error: {log}");
                    GL.DeleteShader(vs);
                    GL.DeleteShader(gs);
                    GL.DeleteShader(fs);
                    return;
                }

                // Link program
                int program = GL.CreateProgram();
                GL.AttachShader(program, vs);
                GL.AttachShader(program, gs);
                GL.AttachShader(program, fs);
                GL.LinkProgram(program);
                GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int linkStatus);
                if (linkStatus == 0)
                {
                    string log = GL.GetProgramInfoLog(program);
                    Console.WriteLine($"[GrassRenderer] Shader link error: {log}");
                    GL.DeleteShader(vs);
                    GL.DeleteShader(gs);
                    GL.DeleteShader(fs);
                    GL.DeleteProgram(program);
                    return;
                }

                // Clean up shaders (no longer needed after linking)
                GL.DeleteShader(vs);
                GL.DeleteShader(gs);
                GL.DeleteShader(fs);

                // Create ShaderProgram wrapper
                _grassShader = ShaderProgram.FromHandle(program);
                
                Console.WriteLine("[GrassRenderer] Grass shader loaded successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GrassRenderer] Exception loading shader: {ex.Message}");
            }
        }

        /// <summary>
        /// Register a grass layer for a terrain layer.
        /// Uses the terrain's mesh as the base geometry.
        /// </summary>
        /// <param name="tileCenterPosition">Optional: tile center position for distance culling (for streaming tiles with vertices already in world space)</param>
        public void RegisterGrassLayer(Guid terrainGuid, int layerIndex, GrassProperties properties,
            int terrainVAO, int terrainIndexCount, Matrix4 modelMatrix, Matrix3 normalMatrix, Vector3? tileCenterPosition = null)
        {
            string key = $"{terrainGuid}_{layerIndex}";

            // Only log when creating new layers (avoid spam)
            if (!_grassLayers.TryGetValue(key, out var layer) || layer == null)
            {
                layer = new GrassLayer
                {
                    TerrainGuid = terrainGuid,
                    LayerIndex = layerIndex
                };
                _grassLayers[key] = layer;
                
            }

            layer.Properties = properties;
            layer.TerrainVAO = terrainVAO;
            layer.TerrainIndexCount = terrainIndexCount;
            layer.ModelMatrix = modelMatrix;
            layer.NormalMatrix = normalMatrix;
            // Use provided tile center position, or extract from model matrix translation
            layer.TileCenterPosition = tileCenterPosition ?? new Vector3(modelMatrix.M41, modelMatrix.M42, modelMatrix.M43);
            layer.NeedsRebuild = true;
        }

        /// <summary>
        /// Remove grass layer from rendering
        /// </summary>
        public void ClearGrassLayer(Guid terrainGuid, int layerIndex)
        {
            string key = $"{terrainGuid}_{layerIndex}";
            _grassLayers.Remove(key);
        }

        /// <summary>
        /// Clear all grass layers
        /// </summary>
        public void ClearAll()
        {
            _grassLayers.Clear();
        }

        /// <summary>
        /// Render all grass layers
        /// </summary>
        public void Render(Vector3 cameraPos, float time, Vector3 ambientColor, float ambientIntensity,
            float lightIntensity = 1.0f,
            bool useShadows = false, int shadowTexture = 0, Matrix4? shadowMatrix = null,
            float shadowBias = 0.005f, float shadowMapSize = 2048f, float shadowStrength = 0.8f)
        {
            using (Profiler.Profile("GrassRenderer.Render"))
            {
                Profiling.GPUProfiler.BeginGPUScope("GrassRenderer");
                Profiling.RenderProfiler.BeginRenderPass("Grass");

                if (_grassShader == null)
                {
                    Profiling.RenderProfiler.EndRenderPass();
                    Profiling.GPUProfiler.EndGPUScope();
                    return;
                }

                if (_grassLayers.Count == 0)
                {
                    Profiling.RenderProfiler.EndRenderPass();
                    Profiling.GPUProfiler.EndGPUScope();
                    return;
                }

            // Enable blending for soft grass tips
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.DepthMask(true);
            GL.Disable(EnableCap.CullFace); // Double-sided grass blades

            // Use shader
            _grassShader.Use();

            // Shadow uniforms
            if (useShadows && shadowTexture != 0 && shadowMatrix.HasValue)
            {
                _grassShader.SetInt("u_UseShadows", 1);
                _grassShader.SetFloat("u_ShadowBias", shadowBias);
                _grassShader.SetFloat("u_ShadowMapSize", shadowMapSize);
                _grassShader.SetFloat("u_ShadowStrength", shadowStrength);
                _grassShader.SetMat4("u_ShadowMatrix", shadowMatrix.Value);

                // Bind shadow map to texture unit 17
                GL.ActiveTexture(TextureUnit.Texture17);
                GL.BindTexture(TextureTarget.Texture2D, shadowTexture);
                _grassShader.SetInt("u_ShadowMap", 17);
                GL.ActiveTexture(TextureUnit.Texture0); // Restore to default
            }
            else
            {
                _grassShader.SetInt("u_UseShadows", 0);
            }

            // Ambient lighting (common to all layers)
            _grassShader.SetVec3("u_AmbientColor", ambientColor);
            _grassShader.SetFloat("u_AmbientIntensity", ambientIntensity);

            // Directional light intensity (time-based: bright during day, dim at night)
            _grassShader.SetFloat("uDirLightIntensity", lightIntensity);

            // Render each grass layer with its own transform
            int layersRendered = 0;
            int layersSkipped = 0;
            const int MaxGrassLayersPerFrame = 50; // Limit grass layers rendered (reduced from 100 with frustum culling)

            foreach (var layer in _grassLayers.Values)
            {
                if (layer.TerrainVAO == 0 || layer.TerrainIndexCount == 0)
                    continue;

                // OPTIMIZATION: Stop if too many layers rendered this frame
                if (layersRendered >= MaxGrassLayersPerFrame)
                {
                    layersSkipped++;
                    break;
                }

                // Get properties early
                var props = layer.Properties;

                // Distance culling is now handled in ViewportRenderer (before RegisterGrassLayer)
                // This avoids double culling and gives user control via inspector MaxRenderDistance

                layersRendered++;

                // Set per-layer model transform
                _grassShader.SetMat4("u_Model", layer.ModelMatrix);
                _grassShader.SetMat3("u_NormalMat", layer.NormalMatrix);

                // Set grass parameters
                _grassShader.SetFloat("u_BladeHeight", props.BladeHeight);
                _grassShader.SetFloat("u_BladeHeightVariation", props.BladeHeightVariation);
                _grassShader.SetFloat("u_BladeWidth", props.BladeWidth);
                _grassShader.SetFloat("u_BladeCurvature", props.BladeCurvature);
                _grassShader.SetInt("u_BladesPerVertex", props.BladesPerVertex);
                _grassShader.SetFloat("u_Density", props.Density);
                _grassShader.SetFloat("u_CoverageNoiseScale", props.CoverageNoiseScale);
                _grassShader.SetFloat("u_CoverageThreshold", props.CoverageThreshold);

                // Slope constraints (convert degrees to cos for shader comparison with normal.y)
                // normal.y = cos(slopeAngle), so:
                // - minSlope 0° → cos(0) = 1.0 (flat)
                // - maxSlope 90° → cos(90) = 0.0 (vertical)
                float minSlopeY = (float)Math.Cos(props.MaxSlope * Math.PI / 180.0); // cos(maxSlope) = min Y
                float maxSlopeY = (float)Math.Cos(props.MinSlope * Math.PI / 180.0); // cos(minSlope) = max Y
                _grassShader.SetFloat("u_MinSlopeY", minSlopeY);
                _grassShader.SetFloat("u_MaxSlopeY", maxSlopeY);

                // Height constraints
                _grassShader.SetFloat("u_MinHeight", props.MinHeight);
                _grassShader.SetFloat("u_MaxHeight", props.MaxHeight);

                // Colors
                _grassShader.SetVec4("u_ColorTop", new Vector4(props.ColorTop[0], props.ColorTop[1], props.ColorTop[2], 1.0f));
                _grassShader.SetVec4("u_ColorBottom", new Vector4(props.ColorBottom[0], props.ColorBottom[1], props.ColorBottom[2], 1.0f));
                _grassShader.SetFloat("u_ColorVariation", props.ColorVariation);

                // Wind - support Global/Local/Blend modes
                float effectiveWindStrength = props.WindStrength;
                float effectiveWindSpeed = props.WindSpeed;
                float effectiveWindTurbulence = props.WindTurbulence;
                Vector2 effectiveWindDirection = new Vector2(props.WindDirection[0], props.WindDirection[1]);

                // Get global wind from weather system
                try
                {
                    var weather = Engine.Systems.WeatherManager.GetCurrentWeather();
                    if (weather != null)
                    {
                        Vector2 globalWindDir = new Vector2(weather.GetWindDirection().X, weather.GetWindDirection().Y);
                        float globalWindStrength = weather.WindStrength;
                        float globalWindSpeed = weather.WindSpeed;
                        float globalWindGustiness = weather.WindGustiness;

                        if (props.WindMode == 0) // Global mode
                        {
                            effectiveWindStrength = globalWindStrength;
                            effectiveWindSpeed = globalWindSpeed;
                            effectiveWindTurbulence = globalWindGustiness;
                            effectiveWindDirection = globalWindDir;
                        }
                        else if (props.WindMode == 2) // Blend mode
                        {
                            float blend = props.WindBlendFactor;
                            effectiveWindStrength = props.WindStrength * (1 - blend) + globalWindStrength * blend;
                            effectiveWindSpeed = props.WindSpeed * (1 - blend) + globalWindSpeed * blend;
                            effectiveWindTurbulence = props.WindTurbulence * (1 - blend) + globalWindGustiness * blend;
                            effectiveWindDirection = new Vector2(
                                props.WindDirection[0] * (1 - blend) + globalWindDir.X * blend,
                                props.WindDirection[1] * (1 - blend) + globalWindDir.Y * blend
                            );
                        }
                        // else WindMode == 1 (Local): use props values directly (already set)
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GrassRenderer] Exception getting weather: {ex.Message}");
                }

                // Debug: Log effective wind values (first few frames only)
                if (layersRendered == 1 && _debugFrameCount < 3)
                {
                    Console.WriteLine($"[GrassRenderer] Effective wind: Strength={effectiveWindStrength}, Speed={effectiveWindSpeed}, Dir=({effectiveWindDirection.X:F2},{effectiveWindDirection.Y:F2}), Turbulence={effectiveWindTurbulence}, WindMode={props.WindMode}");
                    _debugFrameCount++;
                }

                _grassShader.SetFloat("u_WindStrength", effectiveWindStrength);
                _grassShader.SetFloat("u_WindSpeed", effectiveWindSpeed);
                _grassShader.SetFloat("u_WindTurbulence", effectiveWindTurbulence);
                _grassShader.SetVec2("u_WindDirection", effectiveWindDirection);

                // LOD
                _grassShader.SetFloat("u_MaxRenderDistance", props.MaxRenderDistance);
                _grassShader.SetFloat("u_FadeRange", props.FadeRange);
                _grassShader.SetInt("u_LodEnabled", props.LodEnabled ? 1 : 0);
                _grassShader.SetFloat("u_LodDistance1", props.LodDistance1);
                _grassShader.SetFloat("u_LodDistance2", props.LodDistance2);
                _grassShader.SetFloat("u_LodDistance3", props.LodDistance3);
                _grassShader.SetInt("u_MaxBladesPerTriangle", props.MaxBladesPerTriangle);

                // Snow coverage from weather system
                try
                {
                    var weather = Engine.Systems.WeatherManager.GetCurrentWeather();
                    if (weather != null)
                    {
                        _grassShader.SetFloat("u_SnowCoverage", weather.SnowIntensity);
                        _grassShader.SetFloat("u_SnowAccumulation", weather.SnowAccumulation);
                    }
                    else
                    {
                        _grassShader.SetFloat("u_SnowCoverage", 0f);
                        _grassShader.SetFloat("u_SnowAccumulation", 0f);
                    }
                }
                catch
                {
                    _grassShader.SetFloat("u_SnowCoverage", 0f);
                    _grassShader.SetFloat("u_SnowAccumulation", 0f);
                }

                // Density map (optional) - for painting grass coverage
                if (props.DensityMap.HasValue)
                {
                    int densityTexHandle = TextureCache.GetOrLoad(props.DensityMap.Value,
                        guid => AssetDatabase.TryGet(guid, out var r) ? r.Path : null);
                    
                    if (densityTexHandle != 0)
                    {
                        GL.ActiveTexture(TextureUnit.Texture1);
                        GL.BindTexture(TextureTarget.Texture2D, densityTexHandle);
                        _grassShader.SetInt("u_DensityMap", 1);
                        _grassShader.SetInt("u_HasDensityMap", 1);
                        _grassShader.SetFloat("u_DensityMapScale", props.DensityMapScale);
                    }
                    else
                    {
                        _grassShader.SetInt("u_HasDensityMap", 0);
                    }
                }
                else
                {
                    _grassShader.SetInt("u_HasDensityMap", 0);
                }

                // Albedo texture (optional)
                bool hasAlbedoTex = false;
                if (props.AlbedoTexture.HasValue)
                {
                    int texHandle = TextureCache.GetOrLoad(props.AlbedoTexture.Value,
                        guid => AssetDatabase.TryGet(guid, out var r) ? r.Path : null);

                    if (texHandle != 0 && texHandle != TextureCache.White1x1)
                    {
                        GL.ActiveTexture(TextureUnit.Texture0);
                        GL.BindTexture(TextureTarget.Texture2D, texHandle);
                        _grassShader.SetInt("u_AlbedoTex", 0);
                        hasAlbedoTex = true;
                    }
                }
                _grassShader.SetInt("u_HasAlbedoTex", hasAlbedoTex ? 1 : 0);

                // === PBR Parameters ===
                _grassShader.SetFloat("u_GrassRoughness", props.Roughness);
                _grassShader.SetFloat("u_SubsurfaceStrength", props.SubsurfaceStrength);
                _grassShader.SetFloat("u_AmbientOcclusionBase", props.AmbientOcclusionBase);

                // Terrain color texture (optional - for matching grass to terrain)
                if (props.TerrainColorTexture.HasValue)
                {
                    int terrainColorHandle = TextureCache.GetOrLoad(props.TerrainColorTexture.Value,
                        guid => AssetDatabase.TryGet(guid, out var r) ? r.Path : null);

                    if (terrainColorHandle != 0)
                    {
                        GL.ActiveTexture(TextureUnit.Texture2);
                        GL.BindTexture(TextureTarget.Texture2D, terrainColorHandle);
                        _grassShader.SetInt("u_TerrainColorTex", 2);
                        _grassShader.SetInt("u_HasTerrainColorTex", 1);
                        _grassShader.SetFloat("u_TerrainColorScale", props.TerrainColorScale);
                        _grassShader.SetFloat("u_TerrainColorInfluence", props.TerrainColorInfluence);
                    }
                    else
                    {
                        _grassShader.SetInt("u_HasTerrainColorTex", 0);
                    }
                }
                else
                {
                    _grassShader.SetInt("u_HasTerrainColorTex", 0);
                }

                // Draw using terrain's VAO/indices
                GL.BindVertexArray(layer.TerrainVAO);

                GL.DrawElements(PrimitiveType.Triangles, layer.TerrainIndexCount,
                    DrawElementsType.UnsignedInt, IntPtr.Zero);

                // Record draw call - geometry shader generates grass blades from terrain triangles
                // Each terrain triangle generates multiple grass blades (BladesPerVertex)
                int terrainTriangles = layer.TerrainIndexCount / 3;
                int grassBladesGenerated = terrainTriangles * props.BladesPerVertex;
                Profiling.RenderProfiler.RecordDrawCall(1, terrainTriangles);
                Profiling.Profiler.SetCounter($"Grass.{layer.TerrainGuid}_BladesGenerated", grassBladesGenerated);
            }

                // Restore state
                GL.BindVertexArray(0);
                GL.UseProgram(0);
                GL.DepthMask(true);
                GL.Disable(EnableCap.Blend);
                GL.Enable(EnableCap.CullFace);

                Profiling.RenderProfiler.EndRenderPass();
                Profiling.GPUProfiler.EndGPUScope();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _grassShader?.Dispose();
            _grassLayers.Clear();

            _disposed = true;
        }
    }
}

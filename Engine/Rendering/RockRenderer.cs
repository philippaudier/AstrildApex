using System;
using System.Collections.Generic;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Engine.Assets;
using Engine.Components;

namespace Engine.Rendering
{
    /// <summary>
    /// Renders procedural rocks on terrain using geometry shaders.
    /// Generates realistic rock meshes from terrain triangles using noise-based deformation.
    /// Techniques: FBM displacement, Voronoi cracks, faceted polyhedra.
    /// </summary>
    public sealed class RockRenderer : IDisposable
    {
        private ShaderProgram? _rockShader = null;
        private bool _disposed = false;

        /// <summary>
        /// Stores rock data per terrain + layer combination
        /// </summary>
        private class RockLayer
        {
            public Guid TerrainGuid;
            public int LayerIndex;
            public int TerrainVAO;
            public int TerrainIndexCount;
            public RockProperties Properties = new RockProperties();
            public bool NeedsRebuild = true;
            public Matrix4 ModelMatrix = Matrix4.Identity;
            public Matrix3 NormalMatrix = Matrix3.Identity;
            public Vector3 TileCenterPosition = Vector3.Zero; // Tile center for distance culling (separate from ModelMatrix)
        }

        private readonly Dictionary<string, RockLayer> _rockLayers = new();

        public RockRenderer()
        {
            LoadShader();
        }

        private void LoadShader()
        {
            try
            {
                string vertPath = "Engine/Rendering/Shaders/Rock/TerrainRock.vert";
                string geomPath = "Engine/Rendering/Shaders/Rock/TerrainRock.geom";
                string fragPath = "Engine/Rendering/Shaders/Rock/TerrainRock.frag";

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
                    Console.WriteLine($"[RockRenderer] Vertex shader compile error: {log}");
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
                    Console.WriteLine($"[RockRenderer] Geometry shader compile error: {log}");
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
                    Console.WriteLine($"[RockRenderer] Fragment shader compile error: {log}");
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
                    Console.WriteLine($"[RockRenderer] Shader link error: {log}");
                    GL.DeleteShader(vs);
                    GL.DeleteShader(gs);
                    GL.DeleteShader(fs);
                    GL.DeleteProgram(program);
                    return;
                }

                // Clean up shaders
                GL.DeleteShader(vs);
                GL.DeleteShader(gs);
                GL.DeleteShader(fs);

                // Create ShaderProgram wrapper
                _rockShader = ShaderProgram.FromHandle(program);
                
                Console.WriteLine("[RockRenderer] Rock shader loaded successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RockRenderer] Exception loading shader: {ex.Message}");
            }
        }

        /// <summary>
        /// Register a rock layer for a terrain.
        /// </summary>
        /// <param name="tileCenterPosition">Optional: tile center position for distance culling (for streaming tiles with vertices already in world space)</param>
        public void RegisterRockLayer(Guid terrainGuid, int layerIndex, RockProperties properties,
            int terrainVAO, int terrainIndexCount, Matrix4 modelMatrix, Matrix3 normalMatrix, Vector3? tileCenterPosition = null)
        {
            string key = $"{terrainGuid}_{layerIndex}";

            if (!_rockLayers.TryGetValue(key, out var layer) || layer == null)
            {
                layer = new RockLayer
                {
                    TerrainGuid = terrainGuid,
                    LayerIndex = layerIndex
                };
                _rockLayers[key] = layer;
                Console.WriteLine($"[RockRenderer] Created new rock layer: {key}, VAO={terrainVAO}, indexCount={terrainIndexCount}");
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
        /// Remove rock layer from rendering
        /// </summary>
        public void ClearRockLayer(Guid terrainGuid, int layerIndex)
        {
            string key = $"{terrainGuid}_{layerIndex}";
            _rockLayers.Remove(key);
        }

        /// <summary>
        /// Clear all rock layers
        /// </summary>
        public void ClearAll()
        {
            _rockLayers.Clear();
        }

        /// <summary>
        /// Render all rock layers
        /// </summary>
        public void Render(Vector3 cameraPos, float time, Vector3 ambientColor, float ambientIntensity,
            Vector3 sunDirection, Vector3 sunColor, float sunIntensity)
        {
            using (Profiling.Profiler.Profile("RockRenderer.Render"))
            {
                Profiling.GPUProfiler.BeginGPUScope("RockRenderer");
                Profiling.RenderProfiler.BeginRenderPass("Rocks");

                if (_rockShader == null)
                {
                    Profiling.RenderProfiler.EndRenderPass();
                    Profiling.GPUProfiler.EndGPUScope();
                    return;
                }

                if (_rockLayers.Count == 0)
                {
                    Profiling.RenderProfiler.EndRenderPass();
                    Profiling.GPUProfiler.EndGPUScope();
                    return;
                }

                // Enable depth testing, disable blending for solid rocks
                GL.Enable(EnableCap.DepthTest);
                GL.DepthMask(true);
                // Disable culling - procedural rocks have inconsistent winding
                GL.Disable(EnableCap.CullFace);

                // Use shader
                _rockShader.Use();

                // Common uniforms
                _rockShader.SetVec3("u_AmbientColor", ambientColor);
                _rockShader.SetFloat("u_AmbientIntensity", ambientIntensity);
                _rockShader.SetVec3("u_SunDirection", sunDirection);
                _rockShader.SetVec3("u_SunColor", sunColor);
                _rockShader.SetFloat("u_SunIntensity", sunIntensity);

                // Render each rock layer
                int layersRendered = 0;
                int layersSkipped = 0;
                const int MaxRockLayersPerFrame = 50; // Limit rock layers rendered (reduced from 100 with frustum culling)

                foreach (var layer in _rockLayers.Values)
                {
                    if (layer.TerrainVAO == 0 || layer.TerrainIndexCount == 0)
                        continue;

                    // OPTIMIZATION: Stop if too many layers rendered this frame
                    if (layersRendered >= MaxRockLayersPerFrame)
                    {
                        layersSkipped++;
                        break;
                    }

                    // Get properties early
                    var props = layer.Properties;

                    // Distance culling is now handled in ViewportRenderer (before RegisterRockLayer)
                    // This avoids double culling and gives user control via inspector MaxRenderDistance

                    layersRendered++;

                    // Per-layer transforms
                    _rockShader.SetMat4("u_Model", layer.ModelMatrix);
                    _rockShader.SetMat3("u_NormalMat", layer.NormalMatrix);

                    // Density & Distribution
                    _rockShader.SetFloat("u_Density", props.Density);
                    _rockShader.SetFloat("u_ClusteringStrength", props.ClusteringStrength);
                    _rockShader.SetFloat("u_ClusterNoiseScale", props.ClusterNoiseScale);
                    _rockShader.SetFloat("u_PlacementThreshold", props.PlacementThreshold);

                    // Slope & Height constraints (convert to shader format)
                    float minSlopeY = (float)Math.Cos(props.MaxSlope * Math.PI / 180.0);
                    float maxSlopeY = (float)Math.Cos(props.MinSlope * Math.PI / 180.0);
                    _rockShader.SetFloat("u_MinSlopeY", minSlopeY);
                    _rockShader.SetFloat("u_MaxSlopeY", maxSlopeY);
                    _rockShader.SetFloat("u_MinHeight", props.MinHeight);
                    _rockShader.SetFloat("u_MaxHeight", props.MaxHeight);

                    // Size
                    _rockShader.SetFloat("u_MinSize", props.MinSize);
                    _rockShader.SetFloat("u_MaxSize", props.MaxSize);
                    _rockShader.SetFloat("u_SizeVariation", props.SizeVariation);
                    _rockShader.SetFloat("u_FlattenY", props.FlattenY);

                    // Noise displacement
                    _rockShader.SetFloat("u_NoiseFrequency", props.NoiseFrequency);
                    _rockShader.SetFloat("u_NoiseAmplitude", props.NoiseAmplitude);
                    _rockShader.SetInt("u_NoiseOctaves", props.NoiseOctaves);
                    _rockShader.SetFloat("u_NoiseLacunarity", props.NoiseLacunarity);
                    _rockShader.SetFloat("u_NoisePersistence", props.NoisePersistence);

                    // Shape features
                    _rockShader.SetFloat("u_Sharpness", props.Sharpness);
                    _rockShader.SetFloat("u_FacetStrength", props.FacetStrength);
                    _rockShader.SetFloat("u_CrackDepth", props.CrackDepth);
                    _rockShader.SetFloat("u_CrackScale", props.CrackScale);

                    // Colors
                    _rockShader.SetVec4("u_BaseColor", new Vector4(props.BaseColor[0], props.BaseColor[1], props.BaseColor[2], 1.0f));
                    _rockShader.SetVec4("u_DarkColor", new Vector4(props.DarkColor[0], props.DarkColor[1], props.DarkColor[2], 1.0f));
                    _rockShader.SetVec4("u_HighlightColor", new Vector4(props.HighlightColor[0], props.HighlightColor[1], props.HighlightColor[2], 1.0f));
                    _rockShader.SetFloat("u_ColorVariation", props.ColorVariation);
                    _rockShader.SetFloat("u_Roughness", props.Roughness);
                    _rockShader.SetFloat("u_Metallic", props.Metallic);

                    // Moss
                    _rockShader.SetFloat("u_MossAmount", props.MossAmount);
                    _rockShader.SetVec4("u_MossColor", new Vector4(props.MossColor[0], props.MossColor[1], props.MossColor[2], 1.0f));
                    _rockShader.SetFloat("u_MossTopBias", props.MossTopBias);

                    // Embedding & orientation
                    _rockShader.SetFloat("u_EmbedDepth", props.EmbedDepth);
                    _rockShader.SetFloat("u_AlignToTerrain", props.AlignToTerrain);
                    _rockShader.SetFloat("u_RotationRandomness", props.RotationRandomness);

                    // LOD
                    _rockShader.SetFloat("u_MaxRenderDistance", props.MaxRenderDistance);
                    _rockShader.SetFloat("u_FadeRange", props.FadeRange);
                    _rockShader.SetFloat("u_LodBias", props.LodBias);

                    // Draw using terrain geometry
                    GL.BindVertexArray(layer.TerrainVAO);
                    GL.DrawElements(PrimitiveType.Triangles, layer.TerrainIndexCount, DrawElementsType.UnsignedInt, IntPtr.Zero);

                    // Record draw call - geometry shader generates rocks from terrain triangles
                    // Each terrain triangle can potentially generate a rock (based on density)
                    int terrainTriangles = layer.TerrainIndexCount / 3;
                    int estimatedRocks = (int)(terrainTriangles * props.Density);
                    Profiling.RenderProfiler.RecordDrawCall(1, terrainTriangles);
                    Profiling.Profiler.SetCounter($"Rocks.{layer.TerrainGuid}_RocksGenerated", estimatedRocks);
                }

                // Restore state
                GL.BindVertexArray(0);
                GL.UseProgram(0);

                Profiling.RenderProfiler.EndRenderPass();
                Profiling.GPUProfiler.EndGPUScope();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _rockShader?.Dispose();
            _rockLayers.Clear();
        }
    }
}

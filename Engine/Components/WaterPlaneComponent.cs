using System;
using OpenTK.Mathematics;

namespace Engine.Components
{
    /// <summary>
    /// Component for realistic ocean/water plane with tessellation and Gerstner waves.
    /// Based on GPU Gems techniques and Shadertoy implementations.
    /// Supports Global/Local/Blend weather integration.
    /// </summary>
    public sealed class WaterPlaneComponent : Component
    {
        // === MESH GENERATION ===

        [Serialization.SerializableAttribute("resolution")]
        public int Resolution { get; set; } = 128; // Grid resolution (vertices per side)

        [Serialization.SerializableAttribute("size")]
        public float Size { get; set; } = 100.0f; // Size of water plane in world units

        [Serialization.SerializableAttribute("meshGenerated")]
        public bool MeshGenerated { get; set; } = false; // Track if mesh has been generated

        // === TESSELLATION ===

        [Serialization.SerializableAttribute("tessellationEnabled")]
        public bool TessellationEnabled { get; set; } = true; // Enable/disable GPU tessellation

        [Serialization.SerializableAttribute("tessellationFactor")]
        public float TessellationFactor { get; set; } = 8.0f; // Base tessellation level (1-64)

        [Serialization.SerializableAttribute("tessellationMinDistance")]
        public float TessellationMinDistance { get; set; } = 10.0f; // Distance for max tessellation

        [Serialization.SerializableAttribute("tessellationMaxDistance")]
        public float TessellationMaxDistance { get; set; } = 200.0f; // Distance for min tessellation

        [Serialization.SerializableAttribute("tessellationMinLevel")]
        public float TessellationMinLevel { get; set; } = 1.0f; // Min tessellation at far distance

        [Serialization.SerializableAttribute("tessellationMaxLevel")]
        public float TessellationMaxLevel { get; set; } = 64.0f; // Max tessellation at close distance

        // === LOD SETTINGS ===

        [Serialization.SerializableAttribute("lodEnabled")]
        public bool LodEnabled { get; set; } = true; // Enable distance-based LOD

        [Serialization.SerializableAttribute("lodDistance1")]
        public float LodDistance1 { get; set; } = 50.0f; // Distance for LOD level 1

        [Serialization.SerializableAttribute("lodDistance2")]
        public float LodDistance2 { get; set; } = 150.0f; // Distance for LOD level 2

        [Serialization.SerializableAttribute("lodDistance3")]
        public float LodDistance3 { get; set; } = 300.0f; // Distance for LOD level 3 (lowest detail)

        // === GERSTNER WAVES ===

        [Serialization.SerializableAttribute("waveIterations")]
        public int WaveIterations { get; set; } = 8; // Number of wave octaves (4-16)

        [Serialization.SerializableAttribute("waveAmplitude")]
        public float WaveAmplitude { get; set; } = 1.0f; // Base wave height multiplier

        [Serialization.SerializableAttribute("waveFrequency")]
        public float WaveFrequency { get; set; } = 1.0f; // Base wave frequency

        [Serialization.SerializableAttribute("waveSpeed")]
        public float WaveSpeed { get; set; } = 2.0f; // Wave animation speed

        [Serialization.SerializableAttribute("waveSteepness")]
        public float WaveSteepness { get; set; } = 0.5f; // Wave sharpness (0 = sine, 1 = peaked)

        [Serialization.SerializableAttribute("waveDrag")]
        public float WaveDrag { get; set; } = 0.38f; // How much waves pull on water

        [Serialization.SerializableAttribute("waveDepth")]
        public float WaveDepth { get; set; } = 1.0f; // Water depth for wave calculation

        // === WAVE DIRECTION ===

        [Serialization.SerializableAttribute("waveDirectionX")]
        public float WaveDirectionX { get; set; } = 1.0f;

        [Serialization.SerializableAttribute("waveDirectionZ")]
        public float WaveDirectionZ { get; set; } = 0.3f;

        // === CREST / FOAM ===

        [Serialization.SerializableAttribute("crestFoamEnabled")]
        public bool CrestFoamEnabled { get; set; } = true;

        [Serialization.SerializableAttribute("crestFoamThreshold")]
        public float CrestFoamThreshold { get; set; } = 0.7f; // Wave height threshold for foam

        [Serialization.SerializableAttribute("crestFoamIntensity")]
        public float CrestFoamIntensity { get; set; } = 1.0f;

        [Serialization.SerializableAttribute("crestFoamColor")]
        public Vector4 CrestFoamColor { get; set; } = new Vector4(1.0f, 1.0f, 1.0f, 0.8f);

        [Serialization.SerializableAttribute("crestFoamScale")]
        public float CrestFoamScale { get; set; } = 5.0f; // Foam texture tiling

        [Serialization.SerializableAttribute("crestFoamTextureGuid")]
        public Guid? CrestFoamTextureGuid { get; set; } = null; // Foam texture (optional)

        [Serialization.SerializableAttribute("crestFoamSpeed")]
        public float CrestFoamSpeed { get; set; } = 0.1f; // Foam animation speed

        // === SHORE FOAM ===

        [Serialization.SerializableAttribute("shoreFoamEnabled")]
        public bool ShoreFoamEnabled { get; set; } = true; // Enable shore foam near shallow water

        [Serialization.SerializableAttribute("shoreFoamDepth")]
        public float ShoreFoamDepth { get; set; } = 2.0f; // Maximum depth for shore foam appearance

        [Serialization.SerializableAttribute("shoreFoamIntensity")]
        public float ShoreFoamIntensity { get; set; } = 1.5f; // Shore foam brightness multiplier

        [Serialization.SerializableAttribute("shoreFoamColor")]
        public Vector4 ShoreFoamColor { get; set; } = new Vector4(1.0f, 1.0f, 1.0f, 0.9f); // Shore foam color

        [Serialization.SerializableAttribute("shoreFoamScale")]
        public float ShoreFoamScale { get; set; } = 8.0f; // Shore foam texture tiling

        [Serialization.SerializableAttribute("shoreFoamSpeed")]
        public float ShoreFoamSpeed { get; set; } = 0.05f; // Shore foam animation speed

        [Serialization.SerializableAttribute("shoreFoamFade")]
        public float ShoreFoamFade { get; set; } = 0.5f; // How smoothly foam fades with depth (0-1)

        [Serialization.SerializableAttribute("shoreFoamEdgeSharpness")]
        public float ShoreFoamEdgeSharpness { get; set; } = 2.0f; // Edge contrast (1-10)

        // === SUBSURFACE SCATTERING ===

        [Serialization.SerializableAttribute("sssEnabled")]
        public bool SSSEnabled { get; set; } = true;

        [Serialization.SerializableAttribute("sssColor")]
        public Vector3 SSSColor { get; set; } = new Vector3(0.0293f, 0.0698f, 0.1717f); // Subsurface scattering color

        [Serialization.SerializableAttribute("sssIntensity")]
        public float SSSIntensity { get; set; } = 0.3f;

        [Serialization.SerializableAttribute("sssDistortion")]
        public float SSSDistortion { get; set; } = 0.5f;

        [Serialization.SerializableAttribute("sssPower")]
        public float SSSPower { get; set; } = 2.0f;

        // === WATER COLORS ===

        [Serialization.SerializableAttribute("shallowColor")]
        public Vector4 ShallowColor { get; set; } = new Vector4(0.1f, 0.4f, 0.5f, 1.0f);

        [Serialization.SerializableAttribute("deepColor")]
        public Vector4 DeepColor { get; set; } = new Vector4(0.02f, 0.08f, 0.15f, 1.0f);

        [Serialization.SerializableAttribute("horizonColor")]
        public Vector4 HorizonColor { get; set; } = new Vector4(0.4f, 0.6f, 0.7f, 1.0f);

        [Serialization.SerializableAttribute("colorDepthFade")]
        public float ColorDepthFade { get; set; } = 10.0f;

        // === FRESNEL ===

        [Serialization.SerializableAttribute("fresnelPower")]
        public float FresnelPower { get; set; } = 5.0f;

        [Serialization.SerializableAttribute("fresnelBias")]
        public float FresnelBias { get; set; } = 0.04f;

        [Serialization.SerializableAttribute("fresnelScale")]
        public float FresnelScale { get; set; } = 0.96f;

        // === REFLECTIONS ===

        [Serialization.SerializableAttribute("reflectionEnabled")]
        public bool ReflectionEnabled { get; set; } = true;

        [Serialization.SerializableAttribute("reflectionIntensity")]
        public float ReflectionIntensity { get; set; } = 1.0f;

        [Serialization.SerializableAttribute("reflectionDistortion")]
        public float ReflectionDistortion { get; set; } = 0.05f;

        [Serialization.SerializableAttribute("usePlanarReflection")]
        public bool UsePlanarReflection { get; set; } = true;

        [Serialization.SerializableAttribute("reflectionResolution")]
        public int ReflectionResolution { get; set; } = 1024;

        // === SPECULAR ===

        [Serialization.SerializableAttribute("specularIntensity")]
        public float SpecularIntensity { get; set; } = 2.0f;

        [Serialization.SerializableAttribute("specularPower")]
        public float SpecularPower { get; set; } = 720.0f; // Sharp sun reflection

        [Serialization.SerializableAttribute("roughness")]
        public float Roughness { get; set; } = 0.1f;

        // === NORMAL MAPPING ===

        [Serialization.SerializableAttribute("normalStrength")]
        public float NormalStrength { get; set; } = 1.0f;

        [Serialization.SerializableAttribute("normalIterations")]
        public int NormalIterations { get; set; } = 36; // Iterations for normal calculation

        [Serialization.SerializableAttribute("normalEpsilon")]
        public float NormalEpsilon { get; set; } = 0.01f; // Sample distance for normal calculation

        // === CAUSTICS ===

        [Serialization.SerializableAttribute("causticsEnabled")]
        public bool CausticsEnabled { get; set; } = true;

        [Serialization.SerializableAttribute("causticsIntensity")]
        public float CausticsIntensity { get; set; } = 0.5f;

        [Serialization.SerializableAttribute("causticsScale")]
        public float CausticsScale { get; set; } = 1.0f;

        [Serialization.SerializableAttribute("causticsSpeed")]
        public float CausticsSpeed { get; set; } = 1.0f;

        [Serialization.SerializableAttribute("causticsOctaves")]
        public int CausticsOctaves { get; set; } = 3; // Number of caustic layers (GPU Gems technique)

        [Serialization.SerializableAttribute("causticsBrightness")]
        public float CausticsBrightness { get; set; } = 1.0f; // Overall brightness multiplier

        [Serialization.SerializableAttribute("causticsSharpness")]
        public float CausticsSharpness { get; set; } = 3.0f; // Focus/sharpness (power function)

        [Serialization.SerializableAttribute("causticsDistortion")]
        public float CausticsDistortion { get; set; } = 0.5f; // Distortion based on water normals

        [Serialization.SerializableAttribute("causticsDepthFalloff")]
        public float CausticsDepthFalloff { get; set; } = 0.2f; // Depth attenuation rate

        [Serialization.SerializableAttribute("causticsChromatic")]
        public float CausticsChromatic { get; set; } = 0.05f; // RGB color separation

        // === REFRACTION ===

        [Serialization.SerializableAttribute("refractionEnabled")]
        public bool RefractionEnabled { get; set; } = true;

        [Serialization.SerializableAttribute("refractionStrength")]
        public float RefractionStrength { get; set; } = 0.1f;

        [Serialization.SerializableAttribute("refractionChromatic")]
        public float RefractionChromatic { get; set; } = 0.02f; // Chromatic aberration

        // === ABSORPTION ===

        [Serialization.SerializableAttribute("absorptionColor")]
        public Vector3 AbsorptionColor { get; set; } = new Vector3(0.4f, 0.1f, 0.02f);

        [Serialization.SerializableAttribute("absorptionStrength")]
        public float AbsorptionStrength { get; set; } = 0.5f;

        // === FBM DETAIL ===

        [Serialization.SerializableAttribute("fbmEnabled")]
        public bool FbmEnabled { get; set; } = true;

        [Serialization.SerializableAttribute("fbmOctaves")]
        public int FbmOctaves { get; set; } = 4;

        [Serialization.SerializableAttribute("fbmAmplitude")]
        public float FbmAmplitude { get; set; } = 0.1f;

        [Serialization.SerializableAttribute("fbmFrequency")]
        public float FbmFrequency { get; set; } = 2.0f;

        [Serialization.SerializableAttribute("fbmLacunarity")]
        public float FbmLacunarity { get; set; } = 2.0f;

        [Serialization.SerializableAttribute("fbmPersistence")]
        public float FbmPersistence { get; set; } = 0.5f;

        // === GLOBAL/LOCAL/BLEND WEATHER INTEGRATION ===

        [Serialization.SerializableAttribute("waveMode")]
        public int WaveMode { get; set; } = 0; // 0 = Global (Weather), 1 = Local, 2 = Blend

        [Serialization.SerializableAttribute("waveBlendFactor")]
        public float WaveBlendFactor { get; set; } = 1.0f; // 0 = local, 1 = global

        // === RUNTIME DATA ===

        [NonSerialized]
        public int MeshVao = 0;

        [NonSerialized]
        public int MeshVbo = 0;

        [NonSerialized]
        public int MeshEbo = 0;

        [NonSerialized]
        public int IndexCount = 0;

        [NonSerialized]
        public int VertexCount = 0;

        [NonSerialized]
        public bool NeedsRegeneration = false;

        // === COMPONENT LIFECYCLE ===

        public override void OnAttached()
        {
            base.OnAttached();
            // Mark for mesh generation on first frame
            NeedsRegeneration = true;
        }

        public override void OnDetached()
        {
            base.OnDetached();
            // Clean up GPU resources
            DisposeMesh();
        }

        /// <summary>
        /// Generate the water plane mesh with the current settings
        /// </summary>
        public void GenerateMesh()
        {
            DisposeMesh();

            int res = Math.Max(2, Resolution);
            float halfSize = Size * 0.5f;

            // Calculate vertex and index counts
            int vertexCount = res * res;
            int quadCount = (res - 1) * (res - 1);
            int indexCount = quadCount * 6;

            // Create vertex data (position, normal, uv)
            // Position: 3 floats, Normal: 3 floats, UV: 2 floats = 8 floats per vertex
            float[] vertices = new float[vertexCount * 8];

            int vi = 0;
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    float fx = (float)x / (res - 1);
                    float fz = (float)z / (res - 1);

                    // Position (centered at origin)
                    vertices[vi++] = (fx - 0.5f) * Size;
                    vertices[vi++] = 0.0f; // Y = 0, waves are calculated in shader
                    vertices[vi++] = (fz - 0.5f) * Size;

                    // Normal (pointing up, will be recalculated in shader)
                    vertices[vi++] = 0.0f;
                    vertices[vi++] = 1.0f;
                    vertices[vi++] = 0.0f;

                    // UV
                    vertices[vi++] = fx;
                    vertices[vi++] = fz;
                }
            }

            // Create indices
            uint[] indices = new uint[indexCount];
            int ii = 0;
            for (int z = 0; z < res - 1; z++)
            {
                for (int x = 0; x < res - 1; x++)
                {
                    uint topLeft = (uint)(z * res + x);
                    uint topRight = topLeft + 1;
                    uint bottomLeft = (uint)((z + 1) * res + x);
                    uint bottomRight = bottomLeft + 1;

                    // First triangle (CCW winding when viewed from above)
                    indices[ii++] = topLeft;
                    indices[ii++] = topRight;
                    indices[ii++] = bottomLeft;

                    // Second triangle (CCW winding when viewed from above)
                    indices[ii++] = topRight;
                    indices[ii++] = bottomRight;
                    indices[ii++] = bottomLeft;
                }
            }

            // Store counts
            VertexCount = vertexCount;
            IndexCount = indexCount;
            MeshGenerated = true;
            NeedsRegeneration = false;

            // GPU upload will be done by the renderer
            // Store the data temporarily for the renderer to pick up
            _pendingVertices = vertices;
            _pendingIndices = indices;
        }

        [NonSerialized]
        internal float[]? _pendingVertices = null;

        [NonSerialized]
        internal uint[]? _pendingIndices = null;

        /// <summary>
        /// Check if there's pending mesh data to upload to GPU
        /// </summary>
        public bool HasPendingMeshData => _pendingVertices != null && _pendingIndices != null;

        /// <summary>
        /// Clear pending mesh data after GPU upload
        /// </summary>
        public void ClearPendingMeshData()
        {
            _pendingVertices = null;
            _pendingIndices = null;
        }

        /// <summary>
        /// Get pending vertex data
        /// </summary>
        public float[]? GetPendingVertices() => _pendingVertices;

        /// <summary>
        /// Get pending index data
        /// </summary>
        public uint[]? GetPendingIndices() => _pendingIndices;

        /// <summary>
        /// Dispose mesh GPU resources
        /// </summary>
        public void DisposeMesh()
        {
            // GPU cleanup will be handled by renderer
            MeshVao = 0;
            MeshVbo = 0;
            MeshEbo = 0;
            IndexCount = 0;
            VertexCount = 0;
            MeshGenerated = false;
            _pendingVertices = null;
            _pendingIndices = null;
        }

        /// <summary>
        /// Get the water level (Y position) from entity transform
        /// </summary>
        public float GetWaterLevel()
        {
            if (Entity == null) return 0.0f;
            var transform = Entity.GetComponent<TransformComponent>();
            if (transform == null) return 0.0f;
            return transform.Position.Y;
        }

        /// <summary>
        /// Get normalized wave direction
        /// </summary>
        public Vector2 GetWaveDirection()
        {
            var dir = new Vector2(WaveDirectionX, WaveDirectionZ);
            float len = dir.Length;
            if (len < 0.001f) return new Vector2(1.0f, 0.0f);
            return dir / len;
        }
    }
}

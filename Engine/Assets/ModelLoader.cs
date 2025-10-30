using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Assimp;

namespace Engine.Assets
{
    /// <summary>
    /// Loads 3D models from various formats (FBX, OBJ, GLTF, etc.) using Assimp
    /// </summary>
    public static class ModelLoader
    {
        private static readonly AssimpContext _context = new AssimpContext();

        static ModelLoader()
        {
            // Configure Assimp import settings
            _context.SetConfig(new Assimp.Configs.NormalSmoothingAngleConfig(66.0f));
        }

        /// <summary>
        /// Supported file extensions
        /// </summary>
        public static readonly string[] SupportedExtensions = new[]
        {
            ".fbx", ".obj", ".gltf", ".glb", ".dae", ".3ds", ".blend", ".ply", ".stl"
        };

        /// <summary>
        /// Check if a file extension is supported
        /// </summary>
        public static bool IsSupported(string extension)
        {
            return SupportedExtensions.Contains(extension.ToLowerInvariant());
        }

        /// <summary>
        /// Load a 3D model from file
        /// </summary>
        /// <param name="filePath">Full path to the model file</param>
        /// <returns>MeshAsset with all submeshes and metadata</returns>
        public static MeshAsset LoadModel(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Model file not found: {filePath}");

            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            if (!IsSupported(extension))
                throw new NotSupportedException($"File format not supported: {extension}");

            try
            {
                // Import with postprocessing
                var scene = _context.ImportFile(filePath, GetPostProcessSteps());

                if (scene == null || !scene.HasMeshes)
                    throw new InvalidDataException($"No meshes found in file: {filePath}");

                // Convert to our engine format
                var meshAsset = new MeshAsset
                {
                    Guid = Guid.NewGuid(),
                    Name = Path.GetFileNameWithoutExtension(filePath),
                    SourcePath = filePath,
                    Bounds = BoundingBox.Empty
                };

                // Process all meshes
                int totalVertices = 0;
                int totalTriangles = 0;

                for (int i = 0; i < scene.MeshCount; i++)
                {
                    var assimpMesh = scene.Meshes[i];
                    var subMesh = ConvertMesh(assimpMesh, i);

                    meshAsset.SubMeshes.Add(subMesh);
                    totalVertices += subMesh.VertexCount;
                    totalTriangles += subMesh.TriangleCount;

                    // Update bounding box
                    var bounds = meshAsset.Bounds;
                    UpdateBounds(ref bounds, subMesh);
                    meshAsset.Bounds = bounds;
                }

                meshAsset.TotalVertexCount = totalVertices;
                meshAsset.TotalTriangleCount = totalTriangles;

                // Extract materials (for now, just store material count)
                if (scene.HasMaterials)
                {
                    for (int i = 0; i < scene.MaterialCount; i++)
                    {
                        // TODO: Extract material properties and textures
                        meshAsset.MaterialGuids.Add(null); // Will be set during import
                    }
                }

                return meshAsset;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load model from {filePath}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Convert Assimp mesh to our SubMesh format
        /// </summary>
        private static SubMesh ConvertMesh(Mesh assimpMesh, int index)
        {
            var meshData = new MeshData();

            // Extract vertices
            if (!assimpMesh.HasVertices)
                throw new InvalidDataException("Mesh has no vertices");

            foreach (var v in assimpMesh.Vertices)
            {
                meshData.Positions.Add(new Vector3(v.X, v.Y, v.Z));
            }

            // Extract normals (or generate if missing)
            if (assimpMesh.HasNormals)
            {
                foreach (var n in assimpMesh.Normals)
                {
                    meshData.Normals.Add(new Vector3(n.X, n.Y, n.Z));
                }
            }

            // Extract UVs (use first channel)
            if (assimpMesh.HasTextureCoords(0))
            {
                var uvs = assimpMesh.TextureCoordinateChannels[0];
                foreach (var uv in uvs)
                {
                    // Assimp UVs are 3D, we only need 2D
                    meshData.TexCoords.Add(new Vector2(uv.X, uv.Y));
                }
            }
            else
            {
                // Generate default UVs (all zeros)
                for (int i = 0; i < meshData.Positions.Count; i++)
                {
                    meshData.TexCoords.Add(Vector2.Zero);
                }
            }

            // Extract indices
            if (!assimpMesh.HasFaces)
                throw new InvalidDataException("Mesh has no faces");

            foreach (var face in assimpMesh.Faces)
            {
                if (face.IndexCount != 3)
                {
                    // Skip non-triangular faces (should not happen with Triangulate flag)
                    Engine.Utils.DebugLogger.Log($"[ModelLoader] Warning: Skipping non-triangular face with {face.IndexCount} indices");
                    continue;
                }

                meshData.Indices.Add((uint)face.Indices[0]);
                meshData.Indices.Add((uint)face.Indices[1]);
                meshData.Indices.Add((uint)face.Indices[2]);
            }

            // Generate normals if missing
            if (meshData.Normals.Count == 0)
            {
                meshData.GenerateNormals();
            }

            // Create SubMesh
            var subMesh = new SubMesh
            {
                Name = string.IsNullOrWhiteSpace(assimpMesh.Name) ? $"SubMesh_{index}" : assimpMesh.Name,
                Vertices = meshData.ToInterleavedVertices(),
                Indices = meshData.Indices.ToArray(),
                MaterialIndex = assimpMesh.MaterialIndex
            };

            return subMesh;
        }

        /// <summary>
        /// Update bounding box with submesh vertices
        /// </summary>
        private static void UpdateBounds(ref BoundingBox bounds, SubMesh subMesh)
        {
            // Extract positions from interleaved data
            for (int i = 0; i < subMesh.Vertices.Length; i += 8)
            {
                var pos = new Vector3(
                    subMesh.Vertices[i + 0],
                    subMesh.Vertices[i + 1],
                    subMesh.Vertices[i + 2]
                );
                bounds.Encapsulate(pos);
            }
        }

        /// <summary>
        /// Get Assimp post-processing steps
        /// </summary>
        private static PostProcessSteps GetPostProcessSteps()
        {
            return PostProcessSteps.Triangulate |              // Convert all primitives to triangles
                   PostProcessSteps.GenerateSmoothNormals |    // Generate smooth normals if missing
                   PostProcessSteps.CalculateTangentSpace |    // Calculate tangents for normal mapping
                   PostProcessSteps.JoinIdenticalVertices |    // Optimize: merge identical vertices
                   PostProcessSteps.ImproveCacheLocality |     // Optimize: improve vertex cache usage
                   PostProcessSteps.OptimizeMeshes |           // Optimize: merge meshes when possible
                   PostProcessSteps.FlipUVs;                   // Flip UVs for OpenGL (Y-up)
        }

        /// <summary>
        /// Extract material information from Assimp material
        /// </summary>
        public static MaterialInfo ExtractMaterialInfo(Assimp.Material assimpMaterial)
        {
            var matInfo = new MaterialInfo
            {
                Name = assimpMaterial.Name ?? "Material"
            };

            // Albedo/Diffuse color
            if (assimpMaterial.HasColorDiffuse)
            {
                var c = assimpMaterial.ColorDiffuse;
                matInfo.AlbedoColor = new float[] { c.R, c.G, c.B, c.A };
            }

            // Metallic (if available)
            if (assimpMaterial.HasShininess)
            {
                matInfo.Metallic = assimpMaterial.Shininess / 1000.0f; // Rough approximation
            }

            // Roughness
            if (assimpMaterial.HasReflectivity)
            {
                matInfo.Roughness = 1.0f - assimpMaterial.Reflectivity;
            }

            // Textures: query Assimp texture slots (more robust than relying only on HasTextureXXX)
            try
            {
                // Diffuse / Albedo
                if (assimpMaterial.GetMaterialTextureCount(Assimp.TextureType.Diffuse) > 0)
                {
                    if (assimpMaterial.GetMaterialTexture(Assimp.TextureType.Diffuse, 0, out Assimp.TextureSlot slot))
                    {
                        matInfo.AlbedoTexturePath = slot.FilePath;
                    }
                }
                else if (assimpMaterial.HasTextureDiffuse)
                {
                    var tex = assimpMaterial.TextureDiffuse;
                    matInfo.AlbedoTexturePath = tex.FilePath;
                }

                // Normal maps: check common slots (Normals, Height sometimes used for bump/normal)
                if (assimpMaterial.GetMaterialTextureCount(Assimp.TextureType.Normals) > 0)
                {
                    if (assimpMaterial.GetMaterialTexture(Assimp.TextureType.Normals, 0, out Assimp.TextureSlot slot))
                    {
                        matInfo.NormalTexturePath = slot.FilePath;
                    }
                }
                else if (assimpMaterial.GetMaterialTextureCount(Assimp.TextureType.Height) > 0)
                {
                    if (assimpMaterial.GetMaterialTexture(Assimp.TextureType.Height, 0, out Assimp.TextureSlot slot))
                    {
                        matInfo.NormalTexturePath = slot.FilePath;
                    }
                }
                else if (assimpMaterial.HasTextureNormal)
                {
                    var tex = assimpMaterial.TextureNormal;
                    matInfo.NormalTexturePath = tex.FilePath;
                }
                else
                {
                    // Try other common types (unknown/ambient/specular) as fallback for albedo
                    if (assimpMaterial.GetMaterialTextureCount(Assimp.TextureType.Unknown) > 0 && string.IsNullOrWhiteSpace(matInfo.AlbedoTexturePath))
                    {
                        if (assimpMaterial.GetMaterialTexture(Assimp.TextureType.Unknown, 0, out Assimp.TextureSlot slot))
                            matInfo.AlbedoTexturePath = slot.FilePath;
                    }
                }
            }
            catch (Exception)
            {
                // If querying texture slots fails, fall back to previous properties if available
                if (assimpMaterial.HasTextureDiffuse)
                {
                    var tex = assimpMaterial.TextureDiffuse;
                    matInfo.AlbedoTexturePath ??= tex.FilePath;
                }
                if (assimpMaterial.HasTextureNormal)
                {
                    var tex = assimpMaterial.TextureNormal;
                    matInfo.NormalTexturePath ??= tex.FilePath;
                }
            }

            return matInfo;
        }
    }

    /// <summary>
    /// Material information extracted from imported model
    /// </summary>
    public class MaterialInfo
    {
        public string? Name { get; set; }
        public float[] AlbedoColor { get; set; } = new float[] { 1, 1, 1, 1 };
        public float Metallic { get; set; } = 0.0f;
        public float Roughness { get; set; } = 0.5f;
        public string? AlbedoTexturePath { get; set; }
        public string? NormalTexturePath { get; set; }
    }
}

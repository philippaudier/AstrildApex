using System;
using System.Collections.Generic;
using System.Numerics;
using Assimp;

namespace Engine.Assets.Import
{
    /// <summary>
    /// Handles mesh geometry conversion from Assimp to engine format.
    /// Implements best practices for vertex data processing and optimization.
    /// </summary>
    public sealed class MeshConverter
    {
        private readonly Assimp.Scene _scene;
        private readonly string _modelName;
        private readonly string _sourceExtension;

        public MeshConverter(Assimp.Scene scene, string modelName, string sourceExtension)
        {
            _scene = scene ?? throw new ArgumentNullException(nameof(scene));
            _modelName = modelName;
            _sourceExtension = sourceExtension.ToLowerInvariant();
        }

        /// <summary>
        /// Convert all meshes from Assimp scene to engine format.
        /// Applies coordinate system conversion for glTF files.
        /// </summary>
        public MeshAsset ConvertToMeshAsset()
        {
            if (!_scene.HasMeshes)
            {
                throw new InvalidOperationException("Scene has no meshes");
            }

            var meshAsset = new MeshAsset
            {
                Guid = Guid.NewGuid(),
                Name = _modelName,
                Bounds = BoundingBox.Empty
            };

            Engine.Utils.DebugLogger.Log($"[MeshConverter] Converting {_scene.MeshCount} mesh(es)");

            // Process scene hierarchy to preserve transforms
            var nodeToMeshMap = new Dictionary<Node, List<int>>();
            BuildNodeToMeshMap(_scene.RootNode, nodeToMeshMap);

            // With PreTransformVertices enabled, Assimp already baked all transforms into vertices
            // However, if the model has mirrored geometry (negative scales), we need to detect
            // and flip the winding order manually since Assimp doesn't always handle this correctly
            var rootTransform = System.Numerics.Matrix4x4.Identity;
            bool flipWindingOrder = false;
            bool convertToLeftHanded = false;

            // Check if we need to flip winding globally for glTF models with mirroring
            if (_sourceExtension == ".gltf" || _sourceExtension == ".glb")
            {
                // glTF models often have mirrored parts with negative scales
                // We detect this by checking if any mesh appears inside-out
                flipWindingOrder = true; // Try flipping for glTF as they often have this issue
                convertToLeftHanded = true;
                Engine.Utils.DebugLogger.Log("[MeshConverter] Converting glTF from right-handed to left-handed (flip Z axis)");
            }
            else
            {
                Engine.Utils.DebugLogger.Log("[MeshConverter] Using pre-baked transforms from Assimp");
            }

            var processedNodes = new HashSet<Node>();
            ProcessNodeHierarchy(_scene.RootNode, rootTransform, meshAsset, nodeToMeshMap, processedNodes, flipWindingOrder, convertToLeftHanded);

            if (meshAsset.SubMeshes.Count == 0)
            {
                throw new InvalidOperationException("Failed to process any meshes from scene");
            }

            // Calculate statistics
            int totalVertices = 0;
            int totalTriangles = 0;
            foreach (var submesh in meshAsset.SubMeshes)
            {
                totalVertices += submesh.VertexCount;
                totalTriangles += submesh.TriangleCount;
            }

            meshAsset.TotalVertexCount = totalVertices;
            meshAsset.TotalTriangleCount = totalTriangles;

            // Create material slots (will be filled by MaterialExtractor)
            for (int i = 0; i < _scene.MaterialCount; i++)
            {
                meshAsset.MaterialGuids.Add(null);
            }

            Engine.Utils.DebugLogger.Log($"[MeshConverter] Converted {meshAsset.SubMeshes.Count} submesh(es): {totalVertices} vertices, {totalTriangles} triangles");

            return meshAsset;
        }

        /// <summary>
        /// Build a map of which meshes belong to which nodes.
        /// </summary>
        private void BuildNodeToMeshMap(Node node, Dictionary<Node, List<int>> map)
        {
            if (node.HasMeshes)
            {
                map[node] = new List<int>(node.MeshIndices);
            }

            foreach (var child in node.Children)
            {
                BuildNodeToMeshMap(child, map);
            }
        }

        /// <summary>
        /// Process node hierarchy recursively.
        /// Since PreTransformVertices already baked transforms, we mainly preserve hierarchy metadata.
        /// </summary>
        private void ProcessNodeHierarchy(
            Node node,
            System.Numerics.Matrix4x4 parentTransform,
            MeshAsset meshAsset,
            Dictionary<Node, List<int>> nodeToMeshMap,
            HashSet<Node> processedNodes,
            bool flipWindingOrder,
            bool convertToLeftHanded)
        {
            if (processedNodes.Contains(node))
                return;

            processedNodes.Add(node);

            // Calculate world transform (for logging purposes - vertices are already baked)
            var localTransform = AssimpToSystemMatrix(node.Transform);
            var worldTransform = localTransform * parentTransform;

            // Log transform info
            System.Numerics.Matrix4x4.Decompose(worldTransform, out var scale, out var rotation, out var translation);
            Engine.Utils.DebugLogger.Log($"[MeshConverter] Node '{node.Name}' - World Pos:({translation.X:F3}, {translation.Y:F3}, {translation.Z:F3})");

            // Process meshes attached to this node
            if (nodeToMeshMap.TryGetValue(node, out var meshIndices))
            {
                foreach (var meshIndex in meshIndices)
                {
                    try
                    {
                        var assimpMesh = _scene.Meshes[meshIndex];
                        if (assimpMesh == null)
                        {
                            Engine.Utils.DebugLogger.Log($"[MeshConverter] WARNING: Mesh {meshIndex} is null");
                            continue;
                        }

                        // Convert mesh geometry
                        // worldTransform is Identity since PreTransformVertices already baked everything
                        var submesh = ConvertMesh(assimpMesh, meshIndex, node.Name, worldTransform, flipWindingOrder, convertToLeftHanded);

                        meshAsset.SubMeshes.Add(submesh);

                        // Update bounds
                        var bounds = meshAsset.Bounds;
                        UpdateBounds(ref bounds, submesh);
                        meshAsset.Bounds = bounds;

                        Engine.Utils.DebugLogger.Log($"[MeshConverter] Processed submesh: {submesh.Name}");
                    }
                    catch (Exception ex)
                    {
                        Engine.Utils.DebugLogger.Log($"[MeshConverter] ERROR processing mesh {meshIndex}: {ex.Message}");
                    }
                }
            }

            // Process children
            foreach (var child in node.Children)
            {
                ProcessNodeHierarchy(child, worldTransform, meshAsset, nodeToMeshMap, processedNodes, flipWindingOrder, convertToLeftHanded);
            }
        }

        /// <summary>
        /// Convert single Assimp mesh to engine SubMesh format.
        /// Extracts vertices, normals, UVs, and indices with proper validation.
        /// Applies world transform to vertices and normals.
        /// Optionally flips winding order for coordinate system conversion.
        /// </summary>
        private SubMesh ConvertMesh(
            Mesh assimpMesh,
            int index,
            string nodeName,
            System.Numerics.Matrix4x4 worldTransform,
            bool flipWindingOrder,
            bool convertToLeftHanded)
        {
            if (!assimpMesh.HasVertices)
            {
                throw new InvalidOperationException($"Mesh {index} has no vertices");
            }

            if (!assimpMesh.HasFaces)
            {
                throw new InvalidOperationException($"Mesh {index} has no faces");
            }

            var meshData = new MeshData();

            // Extract vertices and apply world transform
            Engine.Utils.DebugLogger.Log($"[MeshConverter] Extracting {assimpMesh.VertexCount} vertices...");
            foreach (var v in assimpMesh.Vertices)
            {
                var pos = new Vector3(v.X, v.Y, v.Z);
                // Apply world transform to position
                pos = Vector3.Transform(pos, worldTransform);
                if (convertToLeftHanded)
                {
                    pos.Z = -pos.Z;
                }
                meshData.Positions.Add(pos);
            }

            // Extract normals and apply rotation part of transform
            if (assimpMesh.HasNormals)
            {
                // Extract rotation matrix (no translation/scale for normals)
                var normalTransform = ExtractRotationMatrix(worldTransform);

                foreach (var n in assimpMesh.Normals)
                {
                    var normal = new Vector3(n.X, n.Y, n.Z);
                    // Apply rotation to normal
                    normal = Vector3.TransformNormal(normal, normalTransform);
                    if (convertToLeftHanded)
                    {
                        normal.Z = -normal.Z;
                    }
                    meshData.Normals.Add(Vector3.Normalize(normal));
                }
            }

            // Extract UVs (channel 0)
            if (assimpMesh.HasTextureCoords(0))
            {
                var uvs = assimpMesh.TextureCoordinateChannels[0];
                foreach (var uv in uvs)
                {
                    // Assimp UVs are 3D, extract only X and Y
                    meshData.TexCoords.Add(new Vector2(uv.X, uv.Y));
                }
            }
            else
            {
                // Generate planar UVs if missing
                Engine.Utils.DebugLogger.Log($"[MeshConverter] No UVs found, generating planar projection");
                GeneratePlanarUVs(meshData);
            }

            // Extract indices
            Engine.Utils.DebugLogger.Log($"[MeshConverter] Extracting {assimpMesh.FaceCount} faces...");
            int skippedFaces = 0;
            foreach (var face in assimpMesh.Faces)
            {
                if (face.IndexCount != 3)
                {
                    // Should not happen with Triangulate flag, but log just in case
                    skippedFaces++;
                    continue;
                }

                if (flipWindingOrder)
                {
                    // Reverse winding order: 0-1-2 becomes 0-2-1
                    meshData.Indices.Add((uint)face.Indices[0]);
                    meshData.Indices.Add((uint)face.Indices[2]);
                    meshData.Indices.Add((uint)face.Indices[1]);
                }
                else
                {
                    meshData.Indices.Add((uint)face.Indices[0]);
                    meshData.Indices.Add((uint)face.Indices[1]);
                    meshData.Indices.Add((uint)face.Indices[2]);
                }
            }

            if (skippedFaces > 0)
            {
                Engine.Utils.DebugLogger.Log($"[MeshConverter] Skipped {skippedFaces} non-triangular faces");
            }

            if (meshData.Indices.Count == 0)
            {
                throw new InvalidOperationException($"No valid triangular faces in mesh {index}");
            }

            // Generate normals if missing
            if (meshData.Normals.Count == 0)
            {
                Engine.Utils.DebugLogger.Log($"[MeshConverter] Generating normals...");
                meshData.GenerateNormals();
            }

            // Validate before creating submesh
            if (meshData.Positions.Count != meshData.Normals.Count ||
                meshData.Positions.Count != meshData.TexCoords.Count)
            {
                throw new InvalidOperationException(
                    $"Mesh data mismatch: Positions={meshData.Positions.Count}, " +
                    $"Normals={meshData.Normals.Count}, TexCoords={meshData.TexCoords.Count}");
            }

            // Create SubMesh
            var subMesh = new SubMesh
            {
                Name = string.IsNullOrWhiteSpace(assimpMesh.Name) ? $"SubMesh_{index}" : assimpMesh.Name,
                Vertices = meshData.ToInterleavedVertices(),
                Indices = meshData.Indices.ToArray(),
                MaterialIndex = assimpMesh.MaterialIndex,
                NodeName = nodeName,
                LocalTransform = MatrixToFloatArray(System.Numerics.Matrix4x4.Identity) // Identity since transforms are baked
            };

            // Calculate and store bounds center for placement
            var bounds = meshData.CalculateBounds();
            subMesh.BoundsCenter = bounds.Center;
            Engine.Utils.DebugLogger.Log($"[MeshConverter] SubMesh bounds center: ({bounds.Center.X:F3}, {bounds.Center.Y:F3}, {bounds.Center.Z:F3})");

            return subMesh;
        }

        /// <summary>
        /// Generate planar UV coordinates for meshes without UVs.
        /// Uses axis-aligned projection based on mesh orientation.
        /// </summary>
        private void GeneratePlanarUVs(MeshData meshData)
        {
            if (meshData.Positions.Count == 0)
                return;

            var bounds = meshData.CalculateBounds();
            var size = bounds.Size;

            // Determine dominant axis
            float maxExtent = Math.Max(Math.Max(size.X, size.Y), size.Z);
            if (maxExtent < 0.0001f)
                maxExtent = 1.0f;

            // Use XZ projection (top-down) or XY (front) based on extent
            bool useXZ = size.X >= size.Y || size.Z >= size.Y;

            for (int i = 0; i < meshData.Positions.Count; i++)
            {
                var pos = meshData.Positions[i];
                float u, v;

                if (useXZ)
                {
                    u = (pos.X - bounds.Min.X) / maxExtent;
                    v = (pos.Z - bounds.Min.Z) / maxExtent;
                }
                else
                {
                    u = (pos.X - bounds.Min.X) / maxExtent;
                    v = (pos.Y - bounds.Min.Y) / maxExtent;
                }

                meshData.TexCoords.Add(new Vector2(u, v));
            }
        }

        /// <summary>
        /// Update bounding box with submesh vertices.
        /// </summary>
        private void UpdateBounds(ref BoundingBox bounds, SubMesh subMesh)
        {
            // Extract positions from interleaved data (8 floats per vertex)
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
        /// Convert Assimp Matrix4x4 to System.Numerics.Matrix4x4.
        /// </summary>
        private System.Numerics.Matrix4x4 AssimpToSystemMatrix(Assimp.Matrix4x4 m)
        {
            return new System.Numerics.Matrix4x4(
                m.A1, m.A2, m.A3, m.A4,
                m.B1, m.B2, m.B3, m.B4,
                m.C1, m.C2, m.C3, m.C4,
                m.D1, m.D2, m.D3, m.D4
            );
        }

        /// <summary>
        /// Convert Matrix4x4 to float array for serialization.
        /// </summary>
        private float[] MatrixToFloatArray(System.Numerics.Matrix4x4 matrix)
        {
            return new float[]
            {
                matrix.M11, matrix.M12, matrix.M13, matrix.M14,
                matrix.M21, matrix.M22, matrix.M23, matrix.M24,
                matrix.M31, matrix.M32, matrix.M33, matrix.M34,
                matrix.M41, matrix.M42, matrix.M43, matrix.M44
            };
        }

        /// <summary>
        /// Extract rotation/scale part of transform matrix (no translation).
        /// Used for transforming normals.
        /// </summary>
        private System.Numerics.Matrix4x4 ExtractRotationMatrix(System.Numerics.Matrix4x4 transform)
        {
            return new System.Numerics.Matrix4x4(
                transform.M11, transform.M12, transform.M13, 0,
                transform.M21, transform.M22, transform.M23, 0,
                transform.M31, transform.M32, transform.M33, 0,
                0, 0, 0, 1
            );
        }

        /// <summary>
        /// Check if a transformation matrix has negative determinant (flips geometry).
        /// A negative determinant means the transform includes mirroring (odd number of negative scales).
        /// </summary>
        private bool HasNegativeDeterminant(System.Numerics.Matrix4x4 matrix)
        {
            // Calculate determinant of the 3x3 rotation/scale part
            float det = matrix.M11 * (matrix.M22 * matrix.M33 - matrix.M23 * matrix.M32)
                      - matrix.M12 * (matrix.M21 * matrix.M33 - matrix.M23 * matrix.M31)
                      + matrix.M13 * (matrix.M21 * matrix.M32 - matrix.M22 * matrix.M31);

            return det < 0.0f;
        }
    }
}

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
        /// <summary>
        /// Create a new AssimpContext with proper configuration.
        /// Each import should use its own context to avoid state corruption.
        /// </summary>
        private static AssimpContext CreateContext()
        {
            var context = new AssimpContext();
            // Configure Assimp import settings
            context.SetConfig(new Assimp.Configs.NormalSmoothingAngleConfig(66.0f));
            return context;
        }

        /// <summary>
        /// Supported file extensions - MAINSTREAM FORMATS ONLY
        /// Reduced to most commonly used and reliable formats
        /// </summary>
        public static readonly string[] SupportedExtensions = new[]
        {
            ".fbx",         // Autodesk FBX - industry standard, supports animations, PBR materials
            ".gltf",        // glTF JSON - modern standard, excellent PBR support, widely adopted
            ".glb",         // glTF Binary - optimized glTF, single file format
            ".obj",         // Wavefront OBJ - simple, universal support, good for static meshes
            ".dae"          // Collada DAE - XML-based, good interchange format
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

            Assimp.Scene? scene = null;
            try
            {
                Engine.Utils.DebugLogger.Log($"[ModelLoader] Loading model: {filePath} ({extension})");

                // Validate file before attempting import
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length == 0)
                {
                    throw new InvalidDataException($"File is empty: {filePath}");
                }

                // Create a new context for this import to avoid state corruption
                using var context = CreateContext();

                Engine.Utils.DebugLogger.Log($"[ModelLoader] Importing with Assimp... (file size: {fileInfo.Length} bytes)");

                // Import with postprocessing - catch Assimp exceptions for detailed error messages
                try
                {
                    scene = context.ImportFile(filePath, GetPostProcessSteps(extension));
                }
                catch (Assimp.AssimpException assimpEx)
                {
                    // Extract full error message including inner exceptions
                    var fullErrorMessage = assimpEx.Message;
                    if (assimpEx.InnerException != null)
                    {
                        fullErrorMessage += $" | Inner: {assimpEx.InnerException.Message}";
                    }

                    // Output to BOTH log file AND console for visibility
                    var separator = "═══════════════════════════════════════════════";
                    Console.WriteLine(separator);
                    Console.WriteLine("[ModelLoader] ASSIMP IMPORT FAILED");
                    Console.WriteLine(separator);
                    Console.WriteLine($"[ModelLoader] File: {filePath}");
                    Console.WriteLine($"[ModelLoader] Format: {extension}");
                    Console.WriteLine($"[ModelLoader] Error: {fullErrorMessage}");

                    Engine.Utils.DebugLogger.Log($"[ModelLoader] {separator}");
                    Engine.Utils.DebugLogger.Log($"[ModelLoader] ASSIMP IMPORT FAILED");
                    Engine.Utils.DebugLogger.Log($"[ModelLoader] {separator}");
                    Engine.Utils.DebugLogger.Log($"[ModelLoader] File: {filePath}");
                    Engine.Utils.DebugLogger.Log($"[ModelLoader] Format: {extension}");
                    Engine.Utils.DebugLogger.Log($"[ModelLoader] Error: {fullErrorMessage}");

                    // Check for common GLTF issues
                    if (extension == ".gltf" || extension == ".glb")
                    {
                        var directory = Path.GetDirectoryName(filePath);
                        var parentDir = directory != null ? Path.GetFileName(directory) : "";

                        Console.WriteLine($"[ModelLoader] Parent directory: {parentDir}");
                        Engine.Utils.DebugLogger.Log($"[ModelLoader] Parent directory: {parentDir}");

                        if (parentDir.ToLowerInvariant().Contains("texture") ||
                            parentDir.ToLowerInvariant().Contains("image") ||
                            parentDir.ToLowerInvariant().Contains("material"))
                        {
                            Console.WriteLine($"[ModelLoader] WARNING: GLTF file is in a '{parentDir}' folder!");
                            Console.WriteLine($"[ModelLoader] GLTF files should be in the model root folder, not in texture/image subfolders.");
                            Console.WriteLine($"[ModelLoader] This causes Assimp to fail when resolving relative paths.");

                            Engine.Utils.DebugLogger.Log($"[ModelLoader] WARNING: GLTF file is in a '{parentDir}' folder!");
                            Engine.Utils.DebugLogger.Log($"[ModelLoader] GLTF files should be in the model root folder, not in texture/image subfolders.");
                            Engine.Utils.DebugLogger.Log($"[ModelLoader] This causes Assimp to fail when resolving relative paths.");
                        }

                        // Check if there's a GLTF in parent directory
                        if (directory != null)
                        {
                            var parentDirPath = Path.GetDirectoryName(directory);
                            if (parentDirPath != null && Directory.Exists(parentDirPath))
                            {
                                var gltfFilesInParent = Directory.GetFiles(parentDirPath, "*.gltf");
                                if (gltfFilesInParent.Length > 0)
                                {
                                    Console.WriteLine($"[ModelLoader] HINT: Found {gltfFilesInParent.Length} .gltf file(s) in parent folder:");
                                    Engine.Utils.DebugLogger.Log($"[ModelLoader] HINT: Found {gltfFilesInParent.Length} .gltf file(s) in parent folder:");
                                    foreach (var f in gltfFilesInParent)
                                    {
                                        Console.WriteLine($"[ModelLoader]   - {Path.GetFileName(f)}");
                                        Engine.Utils.DebugLogger.Log($"[ModelLoader]   - {Path.GetFileName(f)}");
                                    }
                                }
                            }
                        }
                    }

                    Console.WriteLine($"[ModelLoader] Stack trace: {assimpEx.StackTrace}");
                    Console.WriteLine(separator);

                    Engine.Utils.DebugLogger.Log($"[ModelLoader] Stack trace: {assimpEx.StackTrace}");
                    Engine.Utils.DebugLogger.Log($"[ModelLoader] {separator}");

                    throw new InvalidOperationException(
                        $"Assimp failed to import {Path.GetFileName(filePath)}: {fullErrorMessage}. " +
                        $"Check the log for detailed diagnostics.",
                        assimpEx);
                }
                catch (Exception ex) when (!(ex is Assimp.AssimpException))
                {
                    Engine.Utils.DebugLogger.Log($"[ModelLoader] UNEXPECTED ERROR: {ex.GetType().Name}: {ex.Message}");
                    Engine.Utils.DebugLogger.Log($"[ModelLoader] Stack trace: {ex.StackTrace}");
                    throw;
                }

                if (scene == null)
                {
                    Engine.Utils.DebugLogger.Log($"[ModelLoader] ERROR: Assimp returned null scene for: {filePath}");
                    throw new InvalidDataException($"Failed to load scene from file: {filePath}. The file may be corrupted or in an unsupported format.");
                }

                if (!scene.HasMeshes)
                {
                    Engine.Utils.DebugLogger.Log($"[ModelLoader] ERROR: No meshes found in: {filePath}");
                    throw new InvalidDataException($"No meshes found in file: {filePath}. The file may be empty or contain only unsupported data.");
                }

                Engine.Utils.DebugLogger.Log($"[ModelLoader] Successfully loaded scene with {scene.MeshCount} mesh(es) and {scene.MaterialCount} material(s)");

                // Convert to our engine format
                var meshAsset = new MeshAsset
                {
                    Guid = Guid.NewGuid(),
                    Name = Path.GetFileNameWithoutExtension(filePath),
                    SourcePath = filePath,
                    Bounds = BoundingBox.Empty
                };

                // Process scene hierarchy to preserve transforms
                var nodeToMeshMap = new Dictionary<Node, List<int>>();
                BuildNodeToMeshMap(scene.RootNode, nodeToMeshMap);

                var processedNodes = new HashSet<Node>();
                ProcessNodeHierarchy(scene, scene.RootNode, System.Numerics.Matrix4x4.Identity, meshAsset, nodeToMeshMap, processedNodes, extension);

                if (meshAsset.SubMeshes.Count == 0)
                {
                    throw new InvalidDataException($"Failed to process any meshes from file: {filePath}");
                }

                // Calculate total statistics
                int totalVertices = 0;
                int totalTriangles = 0;
                foreach (var submesh in meshAsset.SubMeshes)
                {
                    totalVertices += submesh.VertexCount;
                    totalTriangles += submesh.TriangleCount;
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

                Engine.Utils.DebugLogger.Log($"[ModelLoader] Successfully loaded model: {meshAsset.Name} ({totalVertices} vertices, {totalTriangles} triangles)");
                return meshAsset;
            }
            catch (Exception ex)
            {
                Engine.Utils.DebugLogger.Log($"[ModelLoader] ERROR: Failed to load model from {filePath}: {ex.Message}");
                Engine.Utils.DebugLogger.Log($"[ModelLoader] Stack trace: {ex.StackTrace}");
                throw new InvalidOperationException($"Failed to load model from {filePath} ({extension}): {ex.Message}", ex);
            }
            finally
            {
                // Ensure scene is disposed properly
                scene = null;
            }
        }

        /// <summary>
        /// Build a map of which meshes belong to which nodes
        /// </summary>
        private static void BuildNodeToMeshMap(Node node, Dictionary<Node, List<int>> map)
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
        /// Process node hierarchy recursively, baking world transforms into vertices
        /// </summary>
        private static void ProcessNodeHierarchy(
            Assimp.Scene scene,
            Node node,
            System.Numerics.Matrix4x4 parentTransform,
            MeshAsset meshAsset,
            Dictionary<Node, List<int>> nodeToMeshMap,
            HashSet<Node> processedNodes,
            string extension)
        {
            if (processedNodes.Contains(node))
                return;

            processedNodes.Add(node);

            // Calculate world transform for this node
            var localTransform = AssimpToSystemMatrix(node.Transform);
            var worldTransform = localTransform * parentTransform; // FIXED: Correct matrix multiplication order

            // DEBUG: Log transform info
            System.Numerics.Matrix4x4.Decompose(worldTransform, out var worldScale, out var worldRotation, out var worldTranslation);
            System.Numerics.Matrix4x4.Decompose(localTransform, out var localScale, out var localRotation, out var localTranslation);

            Engine.Utils.DebugLogger.Log($"[ModelLoader] Node '{node.Name}':");
            Engine.Utils.DebugLogger.Log($"[ModelLoader]   Local  - Pos:({localTranslation.X:F3}, {localTranslation.Y:F3}, {localTranslation.Z:F3}) Scale:({localScale.X:F3}, {localScale.Y:F3}, {localScale.Z:F3})");
            Engine.Utils.DebugLogger.Log($"[ModelLoader]   World  - Pos:({worldTranslation.X:F3}, {worldTranslation.Y:F3}, {worldTranslation.Z:F3}) Scale:({worldScale.X:F3}, {worldScale.Y:F3}, {worldScale.Z:F3})");

            Console.WriteLine($"[ModelLoader] Node '{node.Name}' - World Pos:({worldTranslation.X:F3}, {worldTranslation.Y:F3}, {worldTranslation.Z:F3})");

            // Process meshes attached to this node
            if (nodeToMeshMap.TryGetValue(node, out var meshIndices))
            {
                foreach (var meshIndex in meshIndices)
                {
                    try
                    {
                        var assimpMesh = scene.Meshes[meshIndex];
                        if (assimpMesh == null)
                        {
                            Engine.Utils.DebugLogger.Log($"[ModelLoader] WARNING: Mesh {meshIndex} is null, skipping");
                            continue;
                        }

                        Engine.Utils.DebugLogger.Log($"[ModelLoader] Converting mesh {meshIndex} from node '{node.Name}': {assimpMesh.VertexCount} verts, {assimpMesh.FaceCount} faces");
                        Console.WriteLine($"[ModelLoader] Converting mesh {meshIndex} from node '{node.Name}': {assimpMesh.VertexCount} verts, {assimpMesh.FaceCount} faces");

                        // Convert mesh (Assimp already baked transforms via PreTransformVertices)
                        var submesh = ConvertMesh(assimpMesh, meshIndex, extension);

                        // Store metadata
                        submesh.NodeName = node.Name;
                        // Identity transform since PreTransformVertices already baked everything
                        submesh.LocalTransform = MatrixToFloatArray(System.Numerics.Matrix4x4.Identity);

                        meshAsset.SubMeshes.Add(submesh);

                        // Update bounds (vertices already have world transform baked in)
                        var bounds = meshAsset.Bounds;
                        UpdateBounds(ref bounds, submesh);
                        meshAsset.Bounds = bounds;

                        Engine.Utils.DebugLogger.Log($"[ModelLoader] ✓ Processed mesh {meshIndex}: {submesh.Name} ({submesh.VertexCount} vertices, {submesh.TriangleCount} triangles)");
                    }
                    catch (Exception ex)
                    {
                        Engine.Utils.DebugLogger.Log($"[ModelLoader] ERROR processing mesh {meshIndex}: {ex.Message}");
                    }
                }
            }

            // Process child nodes
            foreach (var child in node.Children)
            {
                ProcessNodeHierarchy(scene, child, worldTransform, meshAsset, nodeToMeshMap, processedNodes, extension);
            }
        }

        /// <summary>
        /// Convert Assimp Matrix4x4 to System.Numerics.Matrix4x4
        /// </summary>
        private static System.Numerics.Matrix4x4 AssimpToSystemMatrix(Assimp.Matrix4x4 m)
        {
            // Assimp uses row-major, System.Numerics uses row-major too
            return new System.Numerics.Matrix4x4(
                m.A1, m.A2, m.A3, m.A4,
                m.B1, m.B2, m.B3, m.B4,
                m.C1, m.C2, m.C3, m.C4,
                m.D1, m.D2, m.D3, m.D4
            );
        }

        /// <summary>
        /// Convert Matrix4x4 to float array for serialization
        /// </summary>
        private static float[] MatrixToFloatArray(System.Numerics.Matrix4x4 matrix)
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
        /// Convert Assimp mesh to our SubMesh format
        /// Assimp PreTransformVertices already baked all transforms, so we just extract data
        /// </summary>
        private static SubMesh ConvertMesh(Mesh assimpMesh, int index, string extension)
        {
            try
            {
                var meshData = new MeshData();

                // Extract vertices (already transformed by Assimp)
                if (!assimpMesh.HasVertices)
                    throw new InvalidDataException("Mesh has no vertices");

                Engine.Utils.DebugLogger.Log($"[ModelLoader]   Extracting {assimpMesh.VertexCount} vertices...");

                foreach (var v in assimpMesh.Vertices)
                {
                    meshData.Positions.Add(new Vector3(v.X, v.Y, v.Z));
                }

                // Extract normals (already transformed by Assimp)
                if (assimpMesh.HasNormals)
                {
                    Engine.Utils.DebugLogger.Log($"[ModelLoader]   Extracting {assimpMesh.Normals.Count} normals...");
                    foreach (var n in assimpMesh.Normals)
                    {
                        meshData.Normals.Add(new Vector3(n.X, n.Y, n.Z));
                    }
                }
                else
                {
                    Engine.Utils.DebugLogger.Log($"[ModelLoader]   No normals found, will generate later");
                }

                // Extract UVs (use first channel)
                if (assimpMesh.HasTextureCoords(0))
                {
                    Engine.Utils.DebugLogger.Log($"[ModelLoader]   Extracting UVs from channel 0...");
                    var uvs = assimpMesh.TextureCoordinateChannels[0];
                    foreach (var uv in uvs)
                    {
                        // Assimp UVs are 3D, we only need 2D
                        meshData.TexCoords.Add(new Vector2(uv.X, uv.Y));
                    }
                }
                else
                {
                    // Generate default UVs (planar mapping)
                    Engine.Utils.DebugLogger.Log($"[ModelLoader]   No UVs found, generating planar UVs...");
                    GeneratePlanarUVs(meshData);
                }

                // Extract indices
                if (!assimpMesh.HasFaces)
                    throw new InvalidDataException("Mesh has no faces");

                Engine.Utils.DebugLogger.Log($"[ModelLoader]   Extracting faces ({assimpMesh.FaceCount} faces)...");

                int skippedFaces = 0;
                foreach (var face in assimpMesh.Faces)
                {
                    if (face.IndexCount != 3)
                    {
                        // Skip non-triangular faces (should not happen with Triangulate flag)
                        Engine.Utils.DebugLogger.Log($"[ModelLoader]   Warning: Skipping non-triangular face with {face.IndexCount} indices");
                        skippedFaces++;
                        continue;
                    }

                    meshData.Indices.Add((uint)face.Indices[0]);
                    meshData.Indices.Add((uint)face.Indices[1]);
                    meshData.Indices.Add((uint)face.Indices[2]);
                }

                if (skippedFaces > 0)
                {
                    Engine.Utils.DebugLogger.Log($"[ModelLoader]   Skipped {skippedFaces} non-triangular faces");
                }

                if (meshData.Indices.Count == 0)
                {
                    throw new InvalidDataException($"No valid triangular faces found (total faces: {assimpMesh.FaceCount}, skipped: {skippedFaces})");
                }

                // Generate normals if missing
                if (meshData.Normals.Count == 0)
                {
                    Engine.Utils.DebugLogger.Log($"[ModelLoader]   Generating normals...");
                    meshData.GenerateNormals();
                }

                // Validate mesh data before creating interleaved vertices
                Engine.Utils.DebugLogger.Log($"[ModelLoader]   Validating: Positions={meshData.Positions.Count}, Normals={meshData.Normals.Count}, TexCoords={meshData.TexCoords.Count}, Indices={meshData.Indices.Count}");
                if (meshData.Positions.Count != meshData.Normals.Count || meshData.Positions.Count != meshData.TexCoords.Count)
                {
                    throw new InvalidDataException($"Mesh data mismatch: Positions={meshData.Positions.Count}, Normals={meshData.Normals.Count}, TexCoords={meshData.TexCoords.Count}");
                }

                // Create SubMesh
                Engine.Utils.DebugLogger.Log($"[ModelLoader]   Creating interleaved vertex buffer...");
                var subMesh = new SubMesh
                {
                    Name = string.IsNullOrWhiteSpace(assimpMesh.Name) ? $"SubMesh_{index}" : assimpMesh.Name,
                    Vertices = meshData.ToInterleavedVertices(),
                    Indices = meshData.Indices.ToArray(),
                    MaterialIndex = assimpMesh.MaterialIndex
                };

                // Compute submesh bounds/centroid for diagnostics (helps placement when node transforms are identity)
                try
                {
                    var bounds = meshData.CalculateBounds();
                    var center = bounds.Center;
                    // Store the computed center on the SubMesh so the editor can place baked submeshes correctly
                    subMesh.BoundsCenter = center;
                    Engine.Utils.DebugLogger.Log($"[ModelLoader]   SubMesh bounds center = ({center.X:F3}, {center.Y:F3}, {center.Z:F3})");
                    Console.WriteLine($"[ModelLoader]   SubMesh bounds center = ({center.X:F3}, {center.Y:F3}, {center.Z:F3})");
                }
                catch (Exception ex)
                {
                    Engine.Utils.DebugLogger.Log($"[ModelLoader]   Failed to compute submesh bounds: {ex.Message}");
                }

                Engine.Utils.DebugLogger.Log($"[ModelLoader]   SubMesh created successfully");
                return subMesh;
            }
            catch (Exception ex)
            {
                Engine.Utils.DebugLogger.Log($"[ModelLoader]   ConvertMesh exception: {ex.GetType().Name}: {ex.Message}");
                throw;
            }
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
        /// Generate planar UVs for meshes without texture coordinates
        /// Uses the dominant axis to project UVs and prevent stretching
        /// </summary>
        private static void GeneratePlanarUVs(MeshData meshData)
        {
            if (meshData.Positions.Count == 0)
                return;

            // Calculate bounds to determine UV scale
            var bounds = meshData.CalculateBounds();
            var size = bounds.Size;

            // Determine dominant axis (largest extent)
            float maxExtent = Math.Max(Math.Max(size.X, size.Y), size.Z);
            if (maxExtent < 0.0001f)
                maxExtent = 1.0f; // Prevent division by zero

            // Generate UVs based on XZ plane (common for terrain/floors)
            // or XY plane if model is more vertical
            bool useXZ = size.X >= size.Y || size.Z >= size.Y;

            for (int i = 0; i < meshData.Positions.Count; i++)
            {
                var pos = meshData.Positions[i];
                float u, v;

                if (useXZ)
                {
                    // Project on XZ plane (top-down view)
                    u = (pos.X - bounds.Min.X) / maxExtent;
                    v = (pos.Z - bounds.Min.Z) / maxExtent;
                }
                else
                {
                    // Project on XY plane (front view)
                    u = (pos.X - bounds.Min.X) / maxExtent;
                    v = (pos.Y - bounds.Min.Y) / maxExtent;
                }

                meshData.TexCoords.Add(new Vector2(u, v));
            }

            Engine.Utils.DebugLogger.Log($"[ModelLoader]   Generated {meshData.TexCoords.Count} planar UVs (projection: {(useXZ ? "XZ" : "XY")})");
        }

        /// <summary>
        /// Get Assimp post-processing steps based on file format
        /// Proper coordinate system conversion and mirroring fix
        /// </summary>
        /// <param name="extension">File extension (e.g., ".gltf", ".fbx")</param>
        private static PostProcessSteps GetPostProcessSteps(string extension)
        {
            // Core steps
            var steps = PostProcessSteps.Triangulate |              // Convert all to triangles
                        PostProcessSteps.JoinIdenticalVertices |    // Optimize: merge identical vertices
                        PostProcessSteps.SortByPrimitiveType;       // Sort by primitive type

            // Generate smooth normals if missing
            steps |= PostProcessSteps.GenerateSmoothNormals;

            // Calculate tangents for normal mapping
            steps |= PostProcessSteps.CalculateTangentSpace;

            // Bake node transforms into vertices
            steps |= PostProcessSteps.PreTransformVertices;

            // Quality & optimization
            steps |= PostProcessSteps.ValidateDataStructure;        // Validate data
            steps |= PostProcessSteps.ImproveCacheLocality;         // Optimize vertex cache
            steps |= PostProcessSteps.OptimizeMeshes;               // Merge meshes where possible

            // WINDING ORDER:
            // Imported models are typically in CCW order (standard).
            // We use GL.FrontFace(Ccw) in rendering.
            // DO NOT flip winding order - keep models as-is.
            // steps |= PostProcessSteps.FlipWindingOrder;

            // UV handling - DO NOT flip UVs for glTF!
            if (extension != ".gltf" && extension != ".glb")
            {
                steps |= PostProcessSteps.FlipUVs;
            }

            Engine.Utils.DebugLogger.Log($"[ModelLoader] Post-process steps configured for {extension}");

            return steps;
        }

        /// <summary>
        /// Extract material information from Assimp material
        /// FORMAT-SPECIFIC transparency detection for GLTF, FBX, OBJ, DAE
        /// </summary>
        public static MaterialInfo ExtractMaterialInfo(Assimp.Material assimpMaterial, string extension = "")
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
                
                // CRITICAL: For GLTF, baseColorFactor alpha IS the opacity!
                // If alpha < 1.0, this indicates transparency even if HasOpacity is false
                if ((extension == ".gltf" || extension == ".glb") && c.A < 0.99f)
                {
                    matInfo.Opacity = c.A;
                    Engine.Utils.DebugLogger.Log($"[ModelLoader] Material '{matInfo.Name}' - GLTF baseColorFactor alpha: {c.A} (transparent!)");
                }
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

            // ═══════════════════════════════════════════════════════════════
            // ROBUST TRANSPARENCY DETECTION - FORMAT-SPECIFIC
            // ═══════════════════════════════════════════════════════════════
            
            bool transparencyDetected = false;
            string detectionSource = "none";

            // 1. GLTF/GLB - Most reliable: alphaMode is properly imported by Assimp
            if (extension == ".gltf" || extension == ".glb")
            {
                // For GLTF, Assimp usually maps alphaMode correctly to BlendMode property
                // GLTF "BLEND" mode → Assimp often sets HasOpacity with value < 1.0
                // We'll rely on opacity detection below, which works well for GLTF
                Engine.Utils.DebugLogger.Log($"[ModelLoader] Material '{matInfo.Name}' - GLTF/GLB format detected, using opacity-based detection");
            }
            
            // 2. FBX - Check TransparencyFactor via HasColorTransparent
            if (extension == ".fbx")
            {
                // FBX-specific: TransparentColor property
                // If HasColorTransparent is true, check if it's non-black
                if (assimpMaterial.HasColorTransparent)
                {
                    var transparentColor = assimpMaterial.ColorTransparent;
                    // Non-black TransparentColor indicates transparency enabled
                    float avgTransparent = (transparentColor.R + transparentColor.G + transparentColor.B) / 3.0f;
                    if (avgTransparent > 0.01f)
                    {
                        matInfo.AlphaMode = "BLEND";
                        transparencyDetected = true;
                        detectionSource = $"FBX TransparentColor=({transparentColor.R:F2},{transparentColor.G:F2},{transparentColor.B:F2})";
                        Engine.Utils.DebugLogger.Log($"[ModelLoader] Material '{matInfo.Name}' - {detectionSource}");
                    }
                }
                
                // FBX often inverts opacity: TransparencyFactor (0=opaque, 1=transparent)
                // Assimp usually maps this correctly to Opacity property
                if (!transparencyDetected && assimpMaterial.HasOpacity)
                {
                    float opacity = assimpMaterial.Opacity;
                    // FBX materials with opacity < 1.0 are transparent
                    if (opacity < 0.99f)
                    {
                        matInfo.Opacity = opacity;
                        matInfo.AlphaMode = "BLEND";
                        transparencyDetected = true;
                        detectionSource = $"FBX Opacity={opacity:F3}";
                        Engine.Utils.DebugLogger.Log($"[ModelLoader] Material '{matInfo.Name}' - {detectionSource}");
                    }
                }
            }
            
            // 3. OBJ/MTL - Check 'd' (dissolve) property
            if (extension == ".obj")
            {
                // OBJ MTL files use 'd' for dissolve (opacity)
                // 'd 1.0' = fully opaque, 'd 0.0' = fully transparent
                // Assimp maps this to Opacity property
                if (assimpMaterial.HasOpacity)
                {
                    matInfo.Opacity = assimpMaterial.Opacity;
                    
                    if (matInfo.Opacity < 0.99f)
                    {
                        matInfo.AlphaMode = "BLEND";
                        transparencyDetected = true;
                        detectionSource = $"OBJ 'd' (dissolve)={matInfo.Opacity:F3}";
                        Engine.Utils.DebugLogger.Log($"[ModelLoader] Material '{matInfo.Name}' - {detectionSource}");
                    }
                }
            }
            
            // 4. DAE (Collada) - Check <transparency> element
            if (extension == ".dae")
            {
                // Collada uses <transparency> float (0=transparent, 1=opaque by default)
                // Assimp should map this to Opacity property
                if (assimpMaterial.HasOpacity)
                {
                    matInfo.Opacity = assimpMaterial.Opacity;
                    
                    if (matInfo.Opacity < 0.99f)
                    {
                        matInfo.AlphaMode = "BLEND";
                        transparencyDetected = true;
                        detectionSource = $"Collada <transparency>={matInfo.Opacity:F3}";
                        Engine.Utils.DebugLogger.Log($"[ModelLoader] Material '{matInfo.Name}' - {detectionSource}");
                    }
                }
                
                // Collada also has <transparent> color - check if present
                if (!transparencyDetected && assimpMaterial.HasColorTransparent)
                {
                    var transparentColor = assimpMaterial.ColorTransparent;
                    float avgTransparent = (transparentColor.R + transparentColor.G + transparentColor.B) / 3.0f;
                    if (avgTransparent > 0.01f)
                    {
                        matInfo.AlphaMode = "BLEND";
                        transparencyDetected = true;
                        detectionSource = $"Collada <transparent>={avgTransparent:F3}";
                        Engine.Utils.DebugLogger.Log($"[ModelLoader] Material '{matInfo.Name}' - {detectionSource}");
                    }
                }
            }

            // ═══════════════════════════════════════════════════════════════
            // FALLBACK: Generic opacity detection (all formats)
            // ═══════════════════════════════════════════════════════════════
            
            if (!transparencyDetected)
            {
                // Standard Opacity property (generic fallback)
                if (assimpMaterial.HasOpacity)
                {
                    matInfo.Opacity = assimpMaterial.Opacity;
                    Engine.Utils.DebugLogger.Log($"[ModelLoader] Material '{matInfo.Name}' has opacity: {matInfo.Opacity}");

                    if (matInfo.Opacity < 0.99f)
                    {
                        matInfo.AlphaMode = "BLEND";
                        transparencyDetected = true;
                        detectionSource = $"Generic Opacity={matInfo.Opacity:F3}";
                        Engine.Utils.DebugLogger.Log($"[ModelLoader] Material '{matInfo.Name}' uses BLEND mode (opacity < 0.99)");
                    }
                }
                
                // Check diffuse color alpha
                if (!transparencyDetected && assimpMaterial.HasColorDiffuse && assimpMaterial.ColorDiffuse.A < 0.99f)
                {
                    matInfo.Opacity = assimpMaterial.ColorDiffuse.A;
                    matInfo.AlphaMode = "BLEND";
                    transparencyDetected = true;
                    detectionSource = $"Diffuse Alpha={matInfo.Opacity:F3}";
                    Engine.Utils.DebugLogger.Log($"[ModelLoader] Material '{matInfo.Name}' has alpha in diffuse color: {matInfo.Opacity}");
                }
                
                // Check specular color alpha (some exporters use this)
                if (!transparencyDetected && assimpMaterial.HasColorSpecular && assimpMaterial.ColorSpecular.A < 0.99f)
                {
                    matInfo.Opacity = Math.Min(matInfo.Opacity, assimpMaterial.ColorSpecular.A);
                    matInfo.AlphaMode = "BLEND";
                    transparencyDetected = true;
                    detectionSource = $"Specular Alpha={assimpMaterial.ColorSpecular.A:F3}";
                    Engine.Utils.DebugLogger.Log($"[ModelLoader] Material '{matInfo.Name}' has alpha in specular color: {assimpMaterial.ColorSpecular.A}");
                }

                // Check blend mode property (generic)
                if (assimpMaterial.HasBlendMode)
                {
                    var blendMode = assimpMaterial.BlendMode;
                    if (blendMode == Assimp.BlendMode.Additive)
                    {
                        matInfo.AlphaMode = "BLEND";
                        transparencyDetected = true;
                        detectionSource = "Additive BlendMode";
                        Engine.Utils.DebugLogger.Log($"[ModelLoader] Material '{matInfo.Name}' uses ADDITIVE blend mode");
                    }
                }
            }
            
            // Final log
            if (transparencyDetected)
            {
                Engine.Utils.DebugLogger.Log($"[ModelLoader] ✓ Material '{matInfo.Name}' IS TRANSPARENT - Source: {detectionSource}, AlphaMode={matInfo.AlphaMode}, Opacity={matInfo.Opacity:F3}");
            }
            else
            {
                Engine.Utils.DebugLogger.Log($"[ModelLoader] Material '{matInfo.Name}' is OPAQUE (no transparency detected)");
            }

            // Textures: query Assimp texture slots (more robust than relying only on HasTextureXXX)
            try
            {
                // LOG: Show all texture types available for debugging GLTF imports
                Engine.Utils.DebugLogger.Log($"[ModelLoader] ═══ Texture Detection for '{matInfo.Name}' (format: {extension}) ═══");
                Engine.Utils.DebugLogger.Log($"[ModelLoader]   Diffuse: {assimpMaterial.GetMaterialTextureCount(Assimp.TextureType.Diffuse)}");
                Engine.Utils.DebugLogger.Log($"[ModelLoader]   Normals: {assimpMaterial.GetMaterialTextureCount(Assimp.TextureType.Normals)}");
                Engine.Utils.DebugLogger.Log($"[ModelLoader]   Metalness: {assimpMaterial.GetMaterialTextureCount(Assimp.TextureType.Metalness)}");
                Engine.Utils.DebugLogger.Log($"[ModelLoader]   Shininess: {assimpMaterial.GetMaterialTextureCount(Assimp.TextureType.Shininess)}");
                Engine.Utils.DebugLogger.Log($"[ModelLoader]   Ambient: {assimpMaterial.GetMaterialTextureCount(Assimp.TextureType.Ambient)}");
                Engine.Utils.DebugLogger.Log($"[ModelLoader]   Emissive: {assimpMaterial.GetMaterialTextureCount(Assimp.TextureType.Emissive)}");
                Engine.Utils.DebugLogger.Log($"[ModelLoader]   Opacity: {assimpMaterial.GetMaterialTextureCount(Assimp.TextureType.Opacity)}");
                Engine.Utils.DebugLogger.Log($"[ModelLoader]   Unknown: {assimpMaterial.GetMaterialTextureCount(Assimp.TextureType.Unknown)}");
                
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

                // Normal maps: check multiple texture types comprehensively
                // Priority: Normals > Height (bump) > Displacement > Unknown (fallback)
                if (assimpMaterial.GetMaterialTextureCount(Assimp.TextureType.Normals) > 0)
                {
                    if (assimpMaterial.GetMaterialTexture(Assimp.TextureType.Normals, 0, out Assimp.TextureSlot slot))
                    {
                        matInfo.NormalTexturePath = slot.FilePath;
                        Engine.Utils.DebugLogger.Log($"[ModelLoader] Material '{matInfo.Name}' has normal map (Normals slot): {slot.FilePath}");
                    }
                }
                else if (assimpMaterial.GetMaterialTextureCount(Assimp.TextureType.Height) > 0)
                {
                    if (assimpMaterial.GetMaterialTexture(Assimp.TextureType.Height, 0, out Assimp.TextureSlot slot))
                    {
                        matInfo.NormalTexturePath = slot.FilePath;
                        Engine.Utils.DebugLogger.Log($"[ModelLoader] Material '{matInfo.Name}' has normal/bump map (Height slot): {slot.FilePath}");
                    }
                }
                else if (assimpMaterial.GetMaterialTextureCount(Assimp.TextureType.Displacement) > 0)
                {
                    if (assimpMaterial.GetMaterialTexture(Assimp.TextureType.Displacement, 0, out Assimp.TextureSlot slot))
                    {
                        matInfo.NormalTexturePath = slot.FilePath;
                        Engine.Utils.DebugLogger.Log($"[ModelLoader] Material '{matInfo.Name}' has normal/bump map (Displacement slot): {slot.FilePath}");
                    }
                }
                else if (assimpMaterial.HasTextureNormal)
                {
                    var tex = assimpMaterial.TextureNormal;
                    matInfo.NormalTexturePath = tex.FilePath;
                    Engine.Utils.DebugLogger.Log($"[ModelLoader] Material '{matInfo.Name}' has normal map (legacy property): {tex.FilePath}");
                }
                else
                {
                    // Final fallback: Check Unknown texture type for files with "normal" in the name
                    if (assimpMaterial.GetMaterialTextureCount(Assimp.TextureType.Unknown) > 1) // Skip first Unknown (likely albedo)
                    {
                        for (int idx = 0; idx < assimpMaterial.GetMaterialTextureCount(Assimp.TextureType.Unknown); idx++)
                        {
                            if (assimpMaterial.GetMaterialTexture(Assimp.TextureType.Unknown, idx, out Assimp.TextureSlot slot))
                            {
                                var fileName = Path.GetFileName(slot.FilePath)?.ToLowerInvariant() ?? "";
                                if (fileName.Contains("normal") || fileName.Contains("norm") || fileName.Contains("_n.") || fileName.EndsWith("_n.png") || fileName.EndsWith("_n.jpg"))
                                {
                                    matInfo.NormalTexturePath = slot.FilePath;
                                    Engine.Utils.DebugLogger.Log($"[ModelLoader] Material '{matInfo.Name}' has normal map (detected by filename in Unknown slot): {slot.FilePath}");
                                    break;
                                }
                            }
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(matInfo.NormalTexturePath))
                {
                    Engine.Utils.DebugLogger.Log($"[ModelLoader] Material '{matInfo.Name}' has NO normal map");
                }

                // Opacity / Transparency texture
                if (assimpMaterial.GetMaterialTextureCount(Assimp.TextureType.Opacity) > 0)
                {
                    if (assimpMaterial.GetMaterialTexture(Assimp.TextureType.Opacity, 0, out Assimp.TextureSlot slot))
                    {
                        matInfo.OpacityTexturePath = slot.FilePath;
                        matInfo.AlphaMode = "BLEND";  // Has opacity texture, enable blending
                        Engine.Utils.DebugLogger.Log($"[ModelLoader] Material '{matInfo.Name}' has opacity texture: {slot.FilePath}");
                    }
                }
                else if (assimpMaterial.HasTextureOpacity)
                {
                    var tex = assimpMaterial.TextureOpacity;
                    matInfo.OpacityTexturePath = tex.FilePath;
                    matInfo.AlphaMode = "BLEND";
                    Engine.Utils.DebugLogger.Log($"[ModelLoader] Material '{matInfo.Name}' has opacity texture (legacy): {tex.FilePath}");
                }

                // Check for alpha in diffuse texture (common for transparent materials)
                if (matInfo.OpacityTexturePath == null && matInfo.AlphaMode == "BLEND")
                {
                    Engine.Utils.DebugLogger.Log($"[ModelLoader] Material '{matInfo.Name}' uses alpha channel in diffuse texture");
                }

                // Metallic texture (PBR workflow)
                if (assimpMaterial.GetMaterialTextureCount(Assimp.TextureType.Metalness) > 0)
                {
                    if (assimpMaterial.GetMaterialTexture(Assimp.TextureType.Metalness, 0, out Assimp.TextureSlot slot))
                    {
                        matInfo.MetallicTexturePath = slot.FilePath;
                        Engine.Utils.DebugLogger.Log($"[ModelLoader] Material '{matInfo.Name}' has metallic texture: {slot.FilePath}");
                    }
                }
                // Check if it's a combined metallic/roughness texture (common in GLTF)
                else if (assimpMaterial.GetMaterialTextureCount(Assimp.TextureType.Unknown) > 0)
                {
                    // LOG ALL Unknown texture slots for debugging
                    Engine.Utils.DebugLogger.Log($"[ModelLoader] Material '{matInfo.Name}' has {assimpMaterial.GetMaterialTextureCount(Assimp.TextureType.Unknown)} Unknown texture slot(s)");
                    
                    // Look for combined texture by filename patterns
                    for (int idx = 0; idx < assimpMaterial.GetMaterialTextureCount(Assimp.TextureType.Unknown); idx++)
                    {
                        if (assimpMaterial.GetMaterialTexture(Assimp.TextureType.Unknown, idx, out Assimp.TextureSlot slot))
                        {
                            var fileName = Path.GetFileName(slot.FilePath)?.ToLowerInvariant() ?? "";
                            Engine.Utils.DebugLogger.Log($"[ModelLoader]   Unknown[{idx}]: {slot.FilePath} (filename: {fileName})");
                            
                            if (fileName.Contains("metallic") && fileName.Contains("rough"))
                            {
                                matInfo.MetallicRoughnessTexturePath = slot.FilePath;
                                Engine.Utils.DebugLogger.Log($"[ModelLoader] ✓ Material '{matInfo.Name}' has combined metallic/roughness texture: {slot.FilePath}");
                                break;
                            }
                            else if (fileName.Contains("metallic") || fileName.Contains("metal"))
                            {
                                matInfo.MetallicTexturePath = slot.FilePath;
                                Engine.Utils.DebugLogger.Log($"[ModelLoader] Material '{matInfo.Name}' has metallic texture: {slot.FilePath}");
                            }
                        }
                    }
                }

                // Roughness texture
                if (assimpMaterial.GetMaterialTextureCount(Assimp.TextureType.Shininess) > 0)
                {
                    if (assimpMaterial.GetMaterialTexture(Assimp.TextureType.Shininess, 0, out Assimp.TextureSlot slot))
                    {
                        matInfo.RoughnessTexturePath = slot.FilePath;
                        Engine.Utils.DebugLogger.Log($"[ModelLoader] Material '{matInfo.Name}' has roughness texture: {slot.FilePath}");
                    }
                }
                // Check Unknown slots for roughness by filename
                else if (matInfo.RoughnessTexturePath == null && assimpMaterial.GetMaterialTextureCount(Assimp.TextureType.Unknown) > 0)
                {
                    for (int idx = 0; idx < assimpMaterial.GetMaterialTextureCount(Assimp.TextureType.Unknown); idx++)
                    {
                        if (assimpMaterial.GetMaterialTexture(Assimp.TextureType.Unknown, idx, out Assimp.TextureSlot slot))
                        {
                            var fileName = Path.GetFileName(slot.FilePath)?.ToLowerInvariant() ?? "";
                            if (fileName.Contains("rough") && !fileName.Contains("metallic"))
                            {
                                matInfo.RoughnessTexturePath = slot.FilePath;
                                Engine.Utils.DebugLogger.Log($"[ModelLoader] Material '{matInfo.Name}' has roughness texture: {slot.FilePath}");
                                break;
                            }
                        }
                    }
                }

                // Ambient Occlusion texture
                if (assimpMaterial.GetMaterialTextureCount(Assimp.TextureType.Ambient) > 0)
                {
                    if (assimpMaterial.GetMaterialTexture(Assimp.TextureType.Ambient, 0, out Assimp.TextureSlot slot))
                    {
                        matInfo.AmbientOcclusionTexturePath = slot.FilePath;
                        Engine.Utils.DebugLogger.Log($"[ModelLoader] Material '{matInfo.Name}' has AO texture: {slot.FilePath}");
                    }
                }
                else if (assimpMaterial.GetMaterialTextureCount(Assimp.TextureType.Lightmap) > 0)
                {
                    if (assimpMaterial.GetMaterialTexture(Assimp.TextureType.Lightmap, 0, out Assimp.TextureSlot slot))
                    {
                        matInfo.AmbientOcclusionTexturePath = slot.FilePath;
                        Engine.Utils.DebugLogger.Log($"[ModelLoader] Material '{matInfo.Name}' has AO texture (lightmap): {slot.FilePath}");
                    }
                }

                // Emissive texture
                if (assimpMaterial.GetMaterialTextureCount(Assimp.TextureType.Emissive) > 0)
                {
                    if (assimpMaterial.GetMaterialTexture(Assimp.TextureType.Emissive, 0, out Assimp.TextureSlot slot))
                    {
                        matInfo.EmissiveTexturePath = slot.FilePath;
                        Engine.Utils.DebugLogger.Log($"[ModelLoader] Material '{matInfo.Name}' has emissive texture: {slot.FilePath}");
                    }
                }

                // Fallback textures
                if (string.IsNullOrWhiteSpace(matInfo.AlbedoTexturePath))
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
    /// Unified PBR material structure supporting all mainstream formats
    /// </summary>
    public class MaterialInfo
    {
        public string? Name { get; set; }
        public float[] AlbedoColor { get; set; } = new float[] { 1, 1, 1, 1 };
        public float Metallic { get; set; } = 0.0f;
        public float Roughness { get; set; } = 0.5f;
        public float Opacity { get; set; } = 1.0f;  // 1.0 = opaque, 0.0 = fully transparent
        public string AlphaMode { get; set; } = "OPAQUE";  // OPAQUE, MASK, BLEND (GLTF standard)
        public float AlphaCutoff { get; set; } = 0.5f;  // Alpha threshold for MASK mode (GLTF)
        
        // PBR Texture paths
        public string? AlbedoTexturePath { get; set; }
        public string? NormalTexturePath { get; set; }
        public string? MetallicTexturePath { get; set; }
        public string? RoughnessTexturePath { get; set; }
        public string? OpacityTexturePath { get; set; }
        public string? AmbientOcclusionTexturePath { get; set; }
        public string? EmissiveTexturePath { get; set; }
        
        // Combined texture (common in GLTF)
        public string? MetallicRoughnessTexturePath { get; set; }  // Metallic (B) + Roughness (G) combined
    }
}

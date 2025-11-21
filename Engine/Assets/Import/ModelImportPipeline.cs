using System;
using System.IO;
using System.Linq;
using Assimp;

namespace Engine.Assets.Import
{
    /// <summary>
    /// Modern 3D model import pipeline for game engines.
    ///
    /// Architecture inspired by Unity, Unreal Engine, and glTF 2.0 specification.
    ///
    /// Pipeline stages:
    /// 1. Load scene with Assimp (validation, post-processing)
    /// 2. Extract textures (embedded + external, with naming conventions)
    /// 3. Extract materials (PBR metallic-roughness workflow)
    /// 4. Convert mesh geometry (optimized vertex format)
    /// 5. Save assets with metadata
    ///
    /// Key features:
    /// - Clean separation of concerns (texture/material/mesh extraction)
    /// - Robust texture resolution (multiple search strategies)
    /// - Intelligent texture type detection (naming patterns + pixel analysis)
    /// - PBR workflow following glTF 2.0 standard
    /// - Proper transparency handling (OPAQUE/MASK/BLEND modes)
    /// - Comprehensive error handling and logging
    /// </summary>
    public sealed class ModelImportPipeline
    {
        private readonly string _sourceFilePath;
        private readonly string _assetsRootPath;
        private readonly string _modelFolderPath;
        private readonly string _modelName;

        /// <summary>
        /// Initialize import pipeline.
        /// </summary>
        /// <param name="sourceFilePath">Full path to source model file</param>
        /// <param name="assetsRootPath">Root Assets directory</param>
        /// <param name="modelFolderPath">Output folder for this specific model</param>
        public ModelImportPipeline(string sourceFilePath, string assetsRootPath, string modelFolderPath)
        {
            _sourceFilePath = sourceFilePath ?? throw new ArgumentNullException(nameof(sourceFilePath));
            _assetsRootPath = assetsRootPath ?? throw new ArgumentNullException(nameof(assetsRootPath));
            _modelFolderPath = modelFolderPath ?? throw new ArgumentNullException(nameof(modelFolderPath));
            _modelName = Path.GetFileNameWithoutExtension(sourceFilePath);

            if (!File.Exists(sourceFilePath))
            {
                throw new FileNotFoundException($"Source model file not found: {sourceFilePath}");
            }

            Directory.CreateDirectory(modelFolderPath);
        }

        /// <summary>
        /// Execute the complete import pipeline.
        /// Returns the GUID of the imported model asset.
        /// </summary>
        public Guid Import()
        {
            Engine.Utils.DebugLogger.Log("═══════════════════════════════════════════════════════════");
            Engine.Utils.DebugLogger.Log($"[ModelImportPipeline] Starting import: {Path.GetFileName(_sourceFilePath)}");
            Engine.Utils.DebugLogger.Log("═══════════════════════════════════════════════════════════");

            Assimp.Scene? scene = null;
            try
            {
                // ═══════════════════════════════════════════════════════════
                // STAGE 1: Load scene with Assimp
                // ═══════════════════════════════════════════════════════════
                scene = LoadScene();

                // ═══════════════════════════════════════════════════════════
                // STAGE 2: Extract textures
                // ═══════════════════════════════════════════════════════════
                var texturesDir = Path.Combine(_modelFolderPath, "Textures");
                var textureExtractor = new TextureExtractor(_sourceFilePath, texturesDir);

                // Get material info for texture extraction
                var extension = Path.GetExtension(_sourceFilePath).ToLowerInvariant();
                var materialInfos = scene.Materials.Select(m => ModelLoader.ExtractMaterialInfo(m, extension)).ToList();
                var textureMap = textureExtractor.ExtractTextures(scene, materialInfos);

                Engine.Utils.DebugLogger.Log($"[ModelImportPipeline] Extracted {textureMap.Count} texture(s)");

                // ═══════════════════════════════════════════════════════════
                // STAGE 3: Extract materials
                // ═══════════════════════════════════════════════════════════
                var materialsDir = Path.Combine(_modelFolderPath, "Materials");
                var materialExtractor = new MaterialExtractor(_modelName, materialsDir, textureExtractor, extension, _sourceFilePath);
                var materials = materialExtractor.ExtractMaterials(scene, textureMap);

                Engine.Utils.DebugLogger.Log($"[ModelImportPipeline] Extracted {materials.Count} material(s)");

                // ═══════════════════════════════════════════════════════════
                // STAGE 4: Convert mesh geometry
                // ═══════════════════════════════════════════════════════════
                var meshConverter = new MeshConverter(scene, _modelName, extension);
                var meshAsset = meshConverter.ConvertToMeshAsset();

                // Assign material GUIDs to mesh asset
                for (int i = 0; i < materials.Count && i < meshAsset.MaterialGuids.Count; i++)
                {
                    meshAsset.MaterialGuids[i] = materials[i].Guid;
                }

                // ═══════════════════════════════════════════════════════════
                // STAGE 5: Save assets
                // ═══════════════════════════════════════════════════════════
                var guid = SaveAssets(meshAsset);

                Engine.Utils.DebugLogger.Log("═══════════════════════════════════════════════════════════");
                Engine.Utils.DebugLogger.Log($"[ModelImportPipeline] Import completed successfully!");
                Engine.Utils.DebugLogger.Log($"[ModelImportPipeline] GUID: {guid}");
                Engine.Utils.DebugLogger.Log($"[ModelImportPipeline] Meshes: {meshAsset.SubMeshes.Count}");
                Engine.Utils.DebugLogger.Log($"[ModelImportPipeline] Materials: {materials.Count}");
                Engine.Utils.DebugLogger.Log($"[ModelImportPipeline] Textures: {textureMap.Count}");
                Engine.Utils.DebugLogger.Log($"[ModelImportPipeline] Vertices: {meshAsset.TotalVertexCount}");
                Engine.Utils.DebugLogger.Log($"[ModelImportPipeline] Triangles: {meshAsset.TotalTriangleCount}");
                Engine.Utils.DebugLogger.Log("═══════════════════════════════════════════════════════════");

                return guid;
            }
            catch (Exception ex)
            {
                Engine.Utils.DebugLogger.Log("═══════════════════════════════════════════════════════════");
                Engine.Utils.DebugLogger.Log($"[ModelImportPipeline] IMPORT FAILED: {ex.Message}");
                Engine.Utils.DebugLogger.Log($"[ModelImportPipeline] Stack trace: {ex.StackTrace}");
                Engine.Utils.DebugLogger.Log("═══════════════════════════════════════════════════════════");
                throw;
            }
            finally
            {
                scene = null; // Ensure disposal
            }
        }

        /// <summary>
        /// Load scene using Assimp with proper post-processing.
        /// Implements best practices for OpenGL right-handed coordinate system.
        /// </summary>
        private Assimp.Scene LoadScene()
        {
            Engine.Utils.DebugLogger.Log($"[ModelImportPipeline] Loading scene with Assimp...");

            var extension = Path.GetExtension(_sourceFilePath).ToLowerInvariant();

            // Validate file
            var fileInfo = new FileInfo(_sourceFilePath);
            if (fileInfo.Length == 0)
            {
                throw new InvalidDataException("Model file is empty");
            }

            // Create Assimp context
            using var context = new AssimpContext();
            context.SetConfig(new Assimp.Configs.NormalSmoothingAngleConfig(66.0f));

            // Post-processing steps for modern OpenGL pipeline
            var postProcess = GetPostProcessSteps(extension);

            Engine.Utils.DebugLogger.Log($"[ModelImportPipeline] File size: {fileInfo.Length} bytes");
            Engine.Utils.DebugLogger.Log($"[ModelImportPipeline] Post-processing: {postProcess}");

            // Import with error handling
            Assimp.Scene? scene;
            try
            {
                scene = context.ImportFile(_sourceFilePath, postProcess);
            }
            catch (Assimp.AssimpException ex)
            {
                throw new InvalidOperationException(
                    $"Assimp failed to load {Path.GetFileName(_sourceFilePath)}: {ex.Message}. " +
                    $"The file may be corrupted or in an unsupported format.", ex);
            }

            if (scene == null)
            {
                throw new InvalidDataException("Assimp returned null scene");
            }

            if (!scene.HasMeshes)
            {
                throw new InvalidDataException("Scene contains no meshes");
            }

            Engine.Utils.DebugLogger.Log($"[ModelImportPipeline] Scene loaded: {scene.MeshCount} mesh(es), {scene.MaterialCount} material(s)");

            return scene;
        }

        /// <summary>
        /// Get Assimp post-processing steps optimized for OpenGL.
        /// Based on modern game engine practices and glTF recommendations.
        ///
        /// IMPORTANT NOTES:
        /// - glTF uses Y-up, right-handed coordinate system (same as OpenGL) - no conversion needed
        /// - glTF UVs have origin at top-left, OpenGL expects bottom-left - but we handle this in shaders
        /// - DO NOT use FlipUVs for glTF files - it breaks texture mapping
        /// - PreTransformVertices bakes transforms which can cause mirroring issues
        /// </summary>
        private PostProcessSteps GetPostProcessSteps(string extension)
        {
            // Core processing steps
            var steps = PostProcessSteps.Triangulate |              // Convert all to triangles
                        PostProcessSteps.CalculateTangentSpace |    // Calculate tangents for normal mapping
                        PostProcessSteps.JoinIdenticalVertices |    // Optimize: merge identical vertices
                        PostProcessSteps.SortByPrimitiveType;       // Sort by primitive type

            // Generate normals ONLY if missing (glTF usually has them)
            steps |= PostProcessSteps.GenerateSmoothNormals;

            // Flatten hierarchy into vertex data
            steps |= PostProcessSteps.PreTransformVertices;

            // Validate and optimise raw geometry
            steps |= PostProcessSteps.ValidateDataStructure;
            steps |= PostProcessSteps.FindDegenerates;
            steps |= PostProcessSteps.FindInvalidData;
            steps |= PostProcessSteps.ImproveCacheLocality;
            steps |= PostProcessSteps.OptimizeMeshes;

            Engine.Utils.DebugLogger.Log($"[ModelImportPipeline] Applying PreTransformVertices + mesh optimisations");

            // UV handling - DO NOT flip UVs for glTF!
            // glTF UVs are already in the correct format
            // If texture appears upside down, it's a shader issue, not import issue
            if (extension != ".gltf" && extension != ".glb")
            {
                // Only flip UVs for legacy formats (FBX, OBJ, etc.)
                steps |= PostProcessSteps.FlipUVs;
                Engine.Utils.DebugLogger.Log($"[ModelImportPipeline] Applying FlipUVs for {extension}");
            }
            else
            {
                Engine.Utils.DebugLogger.Log($"[ModelImportPipeline] Skipping FlipUVs for glTF format");
            }

            Engine.Utils.DebugLogger.Log($"[ModelImportPipeline] Post-process flags: {steps}");

            return steps;
        }

        /// <summary>
        /// Save mesh asset and metadata files.
        /// </summary>
        private Guid SaveAssets(MeshAsset meshAsset)
        {
            var guid = Guid.NewGuid();
            meshAsset.Guid = guid;

            // Copy source file to model folder
            var targetModelPath = Path.Combine(_modelFolderPath, Path.GetFileName(_sourceFilePath));
            if (!File.Exists(targetModelPath))
            {
                File.Copy(_sourceFilePath, targetModelPath, overwrite: false);
                Engine.Utils.DebugLogger.Log($"[ModelImportPipeline] Copied source model to: {targetModelPath}");
            }

            // Set relative source path
            meshAsset.SourcePath = GetRelativePath(targetModelPath, _assetsRootPath);

            // Save .meshasset file
            var meshAssetPath = targetModelPath + ".meshasset";
            MeshAsset.Save(meshAssetPath, meshAsset);
            Engine.Utils.DebugLogger.Log($"[ModelImportPipeline] Saved mesh asset: {meshAssetPath}");

            // Create metadata for source model file
            CreateMetaFile(targetModelPath, guid, GetAssetType(Path.GetExtension(_sourceFilePath)));

            // Create metadata for .meshasset file
            CreateMetaFile(meshAssetPath, guid, "MeshAsset");

            return guid;
        }

        /// <summary>
        /// Create .meta file for asset.
        /// </summary>
        private void CreateMetaFile(string assetPath, Guid guid, string assetType)
        {
            var metaPath = assetPath + ".meta";

            var metaData = new AssetDatabase.MetaData
            {
                guid = guid,
                type = assetType
            };

            var json = System.Text.Json.JsonSerializer.Serialize(metaData,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(metaPath, json);
        }

        /// <summary>
        /// Get asset type string from file extension.
        /// </summary>
        private string GetAssetType(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".fbx" => "ModelFBX",
                ".obj" => "ModelOBJ",
                ".gltf" => "ModelGLTF",
                ".glb" => "ModelGLB",
                ".dae" => "ModelDAE",
                _ => "Model"
            };
        }

        /// <summary>
        /// Get relative path from base path.
        /// </summary>
        private string GetRelativePath(string fullPath, string basePath)
        {
            var fullUri = new Uri(fullPath);
            var baseUri = new Uri(basePath.EndsWith(Path.DirectorySeparatorChar.ToString())
                ? basePath
                : basePath + Path.DirectorySeparatorChar);

            var relativeUri = baseUri.MakeRelativeUri(fullUri);
            return Uri.UnescapeDataString(relativeUri.ToString()).Replace('/', Path.DirectorySeparatorChar);
        }
    }
}

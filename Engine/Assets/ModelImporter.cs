using System;
using System.IO;
using Engine.Assets.Import;

namespace Engine.Assets
{
    /// <summary>
    /// Handles importing 3D model files into the asset database.
    /// Uses modern ModelImportPipeline with clean separation of concerns.
    ///
    /// This is the public-facing API - internal implementation is delegated to
    /// Engine.Assets.Import namespace classes for better organization.
    /// </summary>
    public static class ModelImporter
    {
        public const string MeshAssetExtension = ".meshasset";

        /// <summary>
        /// Import a model file into the assets folder using the modern clean pipeline.
        /// </summary>
        /// <param name="sourceFilePath">Full path to the source model file</param>
        /// <param name="assetsRootPath">Root path of the Assets folder</param>
        /// <param name="targetFolder">Optional subfolder within Assets (e.g., "Models")</param>
        /// <returns>GUID of the imported asset</returns>
        public static Guid ImportModel(string sourceFilePath, string assetsRootPath, string? targetFolder = null)
        {
            if (!File.Exists(sourceFilePath))
                throw new FileNotFoundException($"Source file not found: {sourceFilePath}");

            var extension = Path.GetExtension(sourceFilePath).ToLowerInvariant();
            if (!ModelLoader.IsSupported(extension))
                throw new NotSupportedException($"File format not supported: {extension}");

            try
            {
                // Determine base target directory (Models folder)
                var modelsBaseDir = string.IsNullOrWhiteSpace(targetFolder)
                    ? Path.Combine(assetsRootPath, "Models")
                    : Path.Combine(assetsRootPath, targetFolder);

                Directory.CreateDirectory(modelsBaseDir);

                // Create a dedicated folder for this model
                var fileName = Path.GetFileName(sourceFilePath);
                var baseFileName = Path.GetFileNameWithoutExtension(fileName);
                var modelFolderName = SanitizeFileName(baseFileName);

                // Handle duplicate folder names
                int counter = 1;
                var modelFolder = Path.Combine(modelsBaseDir, modelFolderName);
                while (Directory.Exists(modelFolder))
                {
                    modelFolderName = $"{SanitizeFileName(baseFileName)}_{counter}";
                    modelFolder = Path.Combine(modelsBaseDir, modelFolderName);
                    counter++;
                }

                Directory.CreateDirectory(modelFolder);
                Engine.Utils.DebugLogger.Log($"[ModelImporter] Created model folder: {modelFolder}");

                // Use new clean import pipeline
                var pipeline = new ModelImportPipeline(sourceFilePath, assetsRootPath, modelFolder);
                var guid = pipeline.Import();

                // Refresh AssetDatabase to index new files
                try
                {
                    Engine.Utils.DebugLogger.Log("[ModelImporter] Refreshing AssetDatabase...");
                    AssetDatabase.Refresh();
                }
                catch (Exception ex)
                {
                    Engine.Utils.DebugLogger.Log($"[ModelImporter] Failed to refresh AssetDatabase: {ex.Message}");
                }

                // Clear material runtime cache
                try
                {
                    Engine.Rendering.MaterialRuntime.ClearGlobalCache();
                }
                catch { }

                Engine.Utils.DebugLogger.Log($"[ModelImporter] Import completed: {fileName} (GUID: {guid})");
                return guid;
            }
            catch (Exception ex)
            {
                Engine.Utils.DebugLogger.Log($"[ModelImporter] Failed to import model: {ex.Message}");
                throw new InvalidOperationException($"Failed to import model from {sourceFilePath}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Process an existing model file in the assets folder (e.g., after AssetDatabase.Refresh).
        /// This is a simplified version for refresh scenarios that doesn't recreate everything.
        /// </summary>
        public static void ProcessExistingModel(string modelFilePath, Guid existingGuid)
        {
            try
            {
                var meshAssetPath = modelFilePath + MeshAssetExtension;

                // Check if .meshasset already exists and is up-to-date
                if (File.Exists(meshAssetPath))
                {
                    var sourceModified = File.GetLastWriteTimeUtc(modelFilePath);
                    var assetModified = File.GetLastWriteTimeUtc(meshAssetPath);

                    // If source is newer, regenerate
                    if (sourceModified <= assetModified)
                    {
                        Engine.Utils.DebugLogger.Log($"[ModelImporter] Mesh asset is up-to-date: {meshAssetPath}");
                        return;
                    }
                }

                // Load and process the model
                Engine.Utils.DebugLogger.Log($"[ModelImporter] Processing model: {modelFilePath}");
                var meshAsset = ModelLoader.LoadModel(modelFilePath);
                meshAsset.Guid = existingGuid;
                meshAsset.SourcePath = Path.GetFileName(modelFilePath);

                // Save .meshasset file
                MeshAsset.Save(meshAssetPath, meshAsset);
                Engine.Utils.DebugLogger.Log($"[ModelImporter] Updated mesh asset: {meshAssetPath}");
            }
            catch (Exception ex)
            {
                Engine.Utils.DebugLogger.Log($"[ModelImporter] Failed to process model {modelFilePath}: {ex.Message}");
            }
        }

        /// <summary>
        /// Delete imported model and its generated files.
        /// </summary>
        public static void DeleteImportedModel(string modelFilePath)
        {
            try
            {
                // Delete .meshasset file
                var meshAssetPath = modelFilePath + MeshAssetExtension;
                if (File.Exists(meshAssetPath))
                {
                    File.Delete(meshAssetPath);
                    Engine.Utils.DebugLogger.Log($"[ModelImporter] Deleted mesh asset: {meshAssetPath}");
                }

                // Delete .meta files
                var metaPath = modelFilePath + AssetDatabase.MetaExt;
                if (File.Exists(metaPath))
                {
                    File.Delete(metaPath);
                }

                var meshAssetMetaPath = meshAssetPath + AssetDatabase.MetaExt;
                if (File.Exists(meshAssetMetaPath))
                {
                    File.Delete(meshAssetMetaPath);
                }

                // Delete source file
                if (File.Exists(modelFilePath))
                {
                    File.Delete(modelFilePath);
                    Engine.Utils.DebugLogger.Log($"[ModelImporter] Deleted model file: {modelFilePath}");
                }
            }
            catch (Exception ex)
            {
                Engine.Utils.DebugLogger.Log($"[ModelImporter] Failed to delete model {modelFilePath}: {ex.Message}");
            }
        }

        /// <summary>
        /// Sanitize name for use as filename.
        /// </summary>
        private static string SanitizeFileName(string fileName)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '_');
            }
            return fileName.Trim();
        }
    }
}

using System;
using System.IO;
using System.Linq;
using Assimp;

namespace Engine.Assets
{
    /// <summary>
    /// Handles importing 3D model files into the asset database
    /// </summary>
    public static class ModelImporter
    {
        public const string MeshAssetExtension = ".meshasset";

        /// <summary>
        /// Import a model file into the assets folder
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

                // Copy source file to the model's dedicated folder
                var targetPath = Path.Combine(modelFolder, fileName);
                File.Copy(sourceFilePath, targetPath, overwrite: false);
                Engine.Utils.DebugLogger.Log($"[ModelImporter] Copied model to: {targetPath}");

                // Load and process the model
                var meshAsset = ModelLoader.LoadModel(targetPath);
                meshAsset.SourcePath = GetRelativePath(targetPath, assetsRootPath);

                // Generate GUID for this asset
                var guid = Guid.NewGuid();
                meshAsset.Guid = guid;

                // Extract and create materials from the model
                try
                {
                    // First, try to auto-detect textures from the source folder
                    AutoDetectAndCopyTextures(sourceFilePath, modelFolder, baseFileName);

                    ExtractMaterialsAndTextures(targetPath, assetsRootPath, meshAsset, modelFolder);
                }
                catch (Exception matEx)
                {
                    Engine.Utils.DebugLogger.Log($"[ModelImporter] Warning: Failed to extract materials: {matEx.Message}");
                }

                // Save .meshasset file alongside the source file
                var meshAssetPath = targetPath + MeshAssetExtension;
                MeshAsset.Save(meshAssetPath, meshAsset);
                Engine.Utils.DebugLogger.Log($"[ModelImporter] Saved mesh asset: {meshAssetPath}");

                // Create .meta file for the source model file
                CreateMetaFile(targetPath, guid, GetAssetType(extension));

                // Also create .meta file for the .meshasset file
                CreateMetaFile(meshAssetPath, guid, "MeshAsset");

                Engine.Utils.DebugLogger.Log($"[ModelImporter] Successfully imported model: {fileName} (GUID: {guid})");

                return guid;
            }
            catch (Exception ex)
            {
                Engine.Utils.DebugLogger.Log($"[ModelImporter] Failed to import model: {ex.Message}");
                throw new InvalidOperationException($"Failed to import model from {sourceFilePath}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Process an existing model file in the assets folder (e.g., after AssetDatabase.Refresh)
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
        /// Create a .meta file for an asset
        /// </summary>
        private static void CreateMetaFile(string assetPath, Guid guid, string assetType)
        {
            var metaPath = assetPath + AssetDatabase.MetaExt;

            var metaData = new AssetDatabase.MetaData
            {
                guid = guid,
                type = assetType
            };

            var json = System.Text.Json.JsonSerializer.Serialize(metaData, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(metaPath, json);
        }

        /// <summary>
        /// Get asset type string from file extension
        /// </summary>
        private static string GetAssetType(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".fbx" => "ModelFBX",
                ".obj" => "ModelOBJ",
                ".gltf" => "ModelGLTF",
                ".glb" => "ModelGLB",
                ".dae" => "ModelDAE",
                ".3ds" => "Model3DS",
                ".blend" => "ModelBlend",
                _ => "Model"
            };
        }

        /// <summary>
        /// Get relative path from base path
        /// </summary>
        private static string GetRelativePath(string fullPath, string basePath)
        {
            var fullUri = new Uri(fullPath);
            var baseUri = new Uri(basePath.EndsWith(Path.DirectorySeparatorChar.ToString())
                ? basePath
                : basePath + Path.DirectorySeparatorChar);

            var relativeUri = baseUri.MakeRelativeUri(fullUri);
            return Uri.UnescapeDataString(relativeUri.ToString()).Replace('/', Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// Delete imported model and its generated files
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
        /// Auto-detect and copy textures from the source model folder to the asset folder
        /// </summary>
        private static void AutoDetectAndCopyTextures(string sourceModelPath, string targetModelFolder, string modelName)
        {
            var sourceDir = Path.GetDirectoryName(sourceModelPath);
            if (string.IsNullOrEmpty(sourceDir) || !Directory.Exists(sourceDir))
            {
                Engine.Utils.DebugLogger.Log($"[ModelImporter] Source directory not found for texture detection");
                return;
            }

            // Create Textures subdirectory in the model folder
            var texturesDir = Path.Combine(targetModelFolder, "Textures");
            Directory.CreateDirectory(texturesDir);

            // Get all image files in the source directory
            var imageExtensions = new[] { ".png", ".jpg", ".jpeg", ".tga", ".bmp", ".tiff" };
            var imageFiles = Directory.GetFiles(sourceDir)
                .Where(f => imageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .ToList();

            Console.WriteLine($"[ModelImporter] Found {imageFiles.Count} image files in source directory");
            Engine.Utils.DebugLogger.Log($"[ModelImporter] Found {imageFiles.Count} image files in source directory");

            foreach (var imageFile in imageFiles)
            {
                var fileName = Path.GetFileName(imageFile);
                var fileNameLower = fileName.ToLowerInvariant();
                var baseName = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();
                var modelNameLower = modelName.ToLowerInvariant();

                // Check if this texture belongs to this model (contains model name or is the only texture)
                bool belongsToModel = baseName.Contains(modelNameLower) ||
                                     baseName == modelNameLower ||
                                     imageFiles.Count == 1; // If only one texture, assume it's for this model

                if (belongsToModel)
                {
                    try
                    {
                        var targetPath = Path.Combine(texturesDir, fileName);

                        // Avoid overwriting if already exists
                        if (!File.Exists(targetPath))
                        {
                            File.Copy(imageFile, targetPath, overwrite: false);

                            // Create .meta file for the texture
                            var texGuid = Guid.NewGuid();
                            CreateMetaFile(targetPath, texGuid, "Texture2D");

                            Engine.Utils.DebugLogger.Log($"[ModelImporter] Auto-detected and copied texture: {fileName}");
                        }
                        else
                        {
                            Engine.Utils.DebugLogger.Log($"[ModelImporter] Texture already exists, skipping: {fileName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Engine.Utils.DebugLogger.Log($"[ModelImporter] Failed to copy texture {fileName}: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Extract materials and textures from the model and create PBR materials
        /// </summary>
        private static void ExtractMaterialsAndTextures(string modelFilePath, string assetsRootPath, MeshAsset meshAsset, string modelFolder)
        {
            try
            {
                // Load the model with Assimp to extract material information
                using var context = new Assimp.AssimpContext();
                var scene = context.ImportFile(modelFilePath, Assimp.PostProcessSteps.None); // Don't process, just read materials

                if (scene == null || !scene.HasMaterials)
                {
                    Engine.Utils.DebugLogger.Log($"[ModelImporter] No materials found in model");
                    return;
                }

                // Create Textures and Materials subdirectories inside the model folder
                var texturesDir = Path.Combine(modelFolder, "Textures");
                var materialsDir = Path.Combine(modelFolder, "Materials");
                Directory.CreateDirectory(texturesDir);
                Directory.CreateDirectory(materialsDir);

                var modelDir = Path.GetDirectoryName(modelFilePath);
                var modelName = Path.GetFileNameWithoutExtension(modelFilePath);

                // --- Pre-extract any compressed embedded textures ---
                // Some formats (GLB/gltf) embed textures inside the model. We'll write any
                // compressed embedded textures into the Textures folder so they can be
                // auto-assigned to materials later.
                try
                {
                    for (int ti = 0; ti < scene.TextureCount; ti++)
                    {
                        var emb = scene.Textures[ti];
                        try
                        {
                            if (emb != null && emb.IsCompressed)
                            {
                                Directory.CreateDirectory(texturesDir);
                                var ext = ".png"; // default (don't rely on FormatHint for compatibility)

                                var outName = $"{modelName}_embedded_{ti}{ext}";
                                var outPath = Path.Combine(texturesDir, outName);
                                int c = 1;
                                while (File.Exists(outPath)) { outName = $"{modelName}_embedded_{ti}_{c}{ext}"; outPath = Path.Combine(texturesDir, outName); c++; }
                                File.WriteAllBytes(outPath, emb.CompressedData);
                                CreateMetaFile(outPath, Guid.NewGuid(), "Texture2D");
                                Engine.Utils.DebugLogger.Log($"[ModelImporter] Wrote embedded texture file: {outName}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Engine.Utils.DebugLogger.Log($"[ModelImporter] Failed to write embedded texture {ti}: {ex.Message}");
                        }
                    }
                }
                catch { }

                Engine.Utils.DebugLogger.Log($"[ModelImporter] Extracting {scene.MaterialCount} materials from model...");
                Engine.Utils.DebugLogger.Log($"[ModelImporter] Model directory: {modelDir}");

                // Process each material
                for (int i = 0; i < scene.MaterialCount && i < meshAsset.MaterialGuids.Count; i++)
                {
                    var assimpMaterial = scene.Materials[i];
                    Engine.Utils.DebugLogger.Log($"[ModelImporter] Processing material {i}: {assimpMaterial.Name}");
                    var matInfo = ModelLoader.ExtractMaterialInfo(assimpMaterial);
                    Engine.Utils.DebugLogger.Log($"[ModelImporter] Extracted material info - Name: {matInfo.Name}, HasAlbedo: {!string.IsNullOrWhiteSpace(matInfo.AlbedoTexturePath)}, HasNormal: {!string.IsNullOrWhiteSpace(matInfo.NormalTexturePath)}");

                    // Create material name
                    var materialName = string.IsNullOrWhiteSpace(matInfo.Name) || matInfo.Name == "DefaultMaterial"
                        ? $"{modelName}_Mat{i}"
                        : matInfo.Name;

                    // Create PBR material
                    var material = new MaterialAsset
                    {
                        Guid = Guid.NewGuid(),
                        Name = materialName,
                        Shader = "ForwardBase",
                        AlbedoColor = matInfo.AlbedoColor,
                        Metallic = matInfo.Metallic,
                        Roughness = matInfo.Roughness
                    };

                    // Extract and assign textures
                    Engine.Utils.DebugLogger.Log($"[ModelImporter] Material '{materialName}' - AlbedoTexture: {matInfo.AlbedoTexturePath ?? "none"}");
                    Engine.Utils.DebugLogger.Log($"[ModelImporter] Material '{materialName}' - NormalTexture: {matInfo.NormalTexturePath ?? "none"}");

                    if (!string.IsNullOrWhiteSpace(matInfo.AlbedoTexturePath))
                    {
                        var texGuid = ExtractTexture(matInfo.AlbedoTexturePath, modelDir, texturesDir, modelName, scene);
                        if (texGuid.HasValue)
                            material.AlbedoTexture = texGuid.Value;
                    }

                    if (!string.IsNullOrWhiteSpace(matInfo.NormalTexturePath))
                    {
                        var texGuid = ExtractTexture(matInfo.NormalTexturePath, modelDir, texturesDir, modelName, scene);
                        if (texGuid.HasValue)
                        {
                            material.NormalTexture = texGuid.Value;
                            material.NormalStrength = 1.0f;
                        }
                    }

                    // Auto-assign textures from the Textures folder if not already assigned
                    AutoAssignTexturesFromFolder(material, texturesDir, modelName);

                    // Save material
                    var materialFileName = SanitizeFileName(materialName) + AssetDatabase.MaterialExt;
                    var materialPath = Path.Combine(materialsDir, materialFileName);

                    // Handle duplicates
                    int counter = 1;
                    while (File.Exists(materialPath))
                    {
                        materialFileName = $"{SanitizeFileName(materialName)}_{counter}{AssetDatabase.MaterialExt}";
                        materialPath = Path.Combine(materialsDir, materialFileName);
                        counter++;
                    }

                    MaterialAsset.Save(materialPath, material);
                    Engine.Utils.DebugLogger.Log($"[ModelImporter] Created material: {materialFileName}");

                    // Create .meta file for material
                    CreateMetaFile(materialPath, material.Guid, "Material");

                    // Assign material GUID to mesh asset
                    if (i < meshAsset.MaterialGuids.Count)
                        meshAsset.MaterialGuids[i] = material.Guid;
                }
            }
            catch (Exception ex)
            {
                Engine.Utils.DebugLogger.Log($"[ModelImporter] Error extracting materials: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Auto-assign textures from the Textures folder based on naming conventions
        /// </summary>
        private static void AutoAssignTexturesFromFolder(MaterialAsset material, string texturesDir, string modelName)
        {
            if (!Directory.Exists(texturesDir))
                return;

            var textureFiles = Directory.GetFiles(texturesDir);
            var modelNameLower = modelName.ToLowerInvariant();

            foreach (var texFile in textureFiles)
            {
                var fileName = Path.GetFileName(texFile);
                var fileNameLower = fileName.ToLowerInvariant();
                var baseName = Path.GetFileNameWithoutExtension(fileNameLower);

                // Try to get texture GUID from .meta file (meta files are JSON)
                var metaPath = texFile + AssetDatabase.MetaExt;
                if (!File.Exists(metaPath))
                    continue;

                Guid texGuid;
                try
                {
                    var metaJson = File.ReadAllText(metaPath);
                    var metaData = System.Text.Json.JsonSerializer.Deserialize<AssetDatabase.MetaData>(metaJson);
                    if (metaData == null || metaData.guid == Guid.Empty)
                        continue;

                    texGuid = metaData.guid;
                }
                catch
                {
                    // If meta file can't be read/deserialized, skip this texture
                    continue;
                }

                // Detect texture type based on naming conventions
                // Albedo/BaseColor/Diffuse (assign if not already set)
                if ((!material.AlbedoTexture.HasValue || material.AlbedoTexture.Value == Guid.Empty) &&
                    (baseName == modelNameLower || // Exact match (e.g., "ModelName.png")
                     baseName.Contains("basecolor") ||
                     baseName.Contains("albedo") ||
                     baseName.Contains("diffuse") ||
                     baseName.Contains("color") ||
                     baseName.Contains("diff") ||
                     baseName.Contains("_d") ||
                     (!baseName.Contains("normal") && !baseName.Contains("metallic") && !baseName.Contains("roughness") && !baseName.Contains("ao"))))
                {
                    material.AlbedoTexture = texGuid;
                    Engine.Utils.DebugLogger.Log($"[ModelImporter] Auto-assigned Albedo texture: {fileName}");
                }
                // Normal (assign if not already set)
                else if ((!material.NormalTexture.HasValue || material.NormalTexture.Value == Guid.Empty) &&
                         (baseName.Contains("normal") || baseName.Contains("norm") || baseName.Contains("normalmap") || baseName.EndsWith("_n")))
                {
                    material.NormalTexture = texGuid;
                    material.NormalStrength = 1.0f;
                    Engine.Utils.DebugLogger.Log($"[ModelImporter] Auto-assigned Normal texture: {fileName}");
                }
            }
        }

        /// <summary>
        /// Extract a texture file from the model directory or embedded data
        /// Supports external files referenced by path and embedded textures referenced by Assimp (e.g. "*0").
        /// </summary>
        private static Guid? ExtractTexture(string texturePath, string? modelDir, string texturesDir, string modelName, Assimp.Scene? scene = null)
        {
            try
            {
                // Handle embedded textures (Assimp uses "*<index>" for embedded textures)
                if (!string.IsNullOrEmpty(texturePath) && texturePath.StartsWith("*") && scene != null)
                {
                    if (int.TryParse(texturePath.TrimStart('*'), out int idx))
                    {
                        if (idx >= 0 && idx < scene.TextureCount)
                        {
                            var embedded = scene.Textures[idx];

                            // Use a default extension; many embedded textures are PNG/JPEG compressed blobs
                            var ext = ".png";

                            byte[]? data = null;
                            try
                            {
                                // Prefer compressed data only (handling of raw texels is not implemented)
                                if (embedded.IsCompressed)
                                {
                                    data = embedded.CompressedData;
                                }
                                else
                                {
                                    // Non-compressed embedded textures (raw texel arrays) are not currently
                                    // supported by the importer. Many real-world GLB/gltf files embed
                                    // compressed PNG/JPEG blobs and those are handled above. For raw
                                    // texel data we skip extraction and let the material fallback to
                                    // default until a proper encoder is added.
                                    Engine.Utils.DebugLogger.Log($"[ModelImporter] Embedded texture {texturePath} is non-compressed; skipping extraction (not supported)");
                                    return null;
                                }
                            }
                            catch
                            {
                                // fallthrough
                            }

                            if (data == null || data.Length == 0)
                            {
                                Engine.Utils.DebugLogger.Log($"[ModelImporter] Embedded texture {texturePath} has no data");
                                return null;
                            }

                            // Write embedded texture to textures folder
                            Directory.CreateDirectory(texturesDir);
                            var targetName = $"{modelName}_embedded_{idx}{ext}";
                            var targetPath = Path.Combine(texturesDir, targetName);

                            int counter = 1;
                            while (File.Exists(targetPath))
                            {
                                targetName = $"{modelName}_embedded_{idx}_{counter}{ext}";
                                targetPath = Path.Combine(texturesDir, targetName);
                                counter++;
                            }

                            File.WriteAllBytes(targetPath, data);
                            Engine.Utils.DebugLogger.Log($"[ModelImporter] Extracted embedded texture: {targetName}");

                            var texGuid = Guid.NewGuid();
                            CreateMetaFile(targetPath, texGuid, "Texture2D");
                            return texGuid;
                        }
                    }

                    Engine.Utils.DebugLogger.Log($"[ModelImporter] Embedded texture reference not found or invalid: {texturePath}");
                    return null;
                }

                // Handle relative paths from the model file
                string sourceTexPath = texturePath;

                if (!Path.IsPathRooted(texturePath) && modelDir != null)
                {
                    sourceTexPath = Path.Combine(modelDir, texturePath);
                }

                if (!File.Exists(sourceTexPath))
                {
                    Engine.Utils.DebugLogger.Log($"[ModelImporter] Texture not found: {texturePath}");
                    return null;
                }

                // Copy texture to Textures folder
                Directory.CreateDirectory(texturesDir);
                var texFileName = Path.GetFileName(sourceTexPath);
                var texExtension = Path.GetExtension(texFileName);
                var texBaseName = Path.GetFileNameWithoutExtension(texFileName);

                // Prefix with model name to avoid conflicts
                var targetTexName = $"{modelName}_{texBaseName}{texExtension}";
                var targetTexPath = Path.Combine(texturesDir, targetTexName);

                // Handle duplicates
                int dupCounter = 1;
                while (File.Exists(targetTexPath))
                {
                    targetTexName = $"{modelName}_{texBaseName}_{dupCounter}{texExtension}";
                    targetTexPath = Path.Combine(texturesDir, targetTexName);
                    dupCounter++;
                }

                File.Copy(sourceTexPath, targetTexPath, overwrite: false);
                Engine.Utils.DebugLogger.Log($"[ModelImporter] Copied texture: {targetTexName}");

                // Generate GUID and create .meta file
                var texGuid2 = Guid.NewGuid();
                CreateMetaFile(targetTexPath, texGuid2, "Texture2D");

                return texGuid2;
            }
            catch (Exception ex)
            {
                Engine.Utils.DebugLogger.Log($"[ModelImporter] Failed to extract texture {texturePath}: {ex.Message}");
                return null;
            }
        }

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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assimp;

namespace Engine.Assets.Import
{
    /// <summary>
    /// Handles material extraction and PBR material creation during model import.
    /// Implements glTF 2.0 PBR metallic-roughness workflow with fallbacks for other formats.
    /// </summary>
    public sealed class MaterialExtractor
    {
        private readonly string _modelName;
        private readonly string _materialsOutputDirectory;
        private readonly TextureExtractor _textureExtractor;
        private readonly string _modelFileExtension;
        private readonly Dictionary<int, MaterialAsset> _extractedMaterials = new();
        private readonly Dictionary<string, GltfMaterialParser.GltfTransparencyInfo>? _gltfTransparencyData;
        private readonly Dictionary<int, string>? _gltfTextureImages; // Texture index -> image file path
        private readonly Dictionary<string, FbxMaterialParser.FbxTransparencyInfo>? _fbxTransparencyData;
        private readonly Dictionary<string, ObjMaterialParser.ObjTransparencyInfo>? _objTransparencyData;
        private readonly Dictionary<string, DaeMaterialParser.DaeTransparencyInfo>? _daeTransparencyData;

        public MaterialExtractor(string modelName, string outputMaterialsDirectory, TextureExtractor textureExtractor, string modelFileExtension = "", string modelFilePath = "")
        {
            _modelName = modelName;
            _materialsOutputDirectory = outputMaterialsDirectory;
            _textureExtractor = textureExtractor;
            _modelFileExtension = modelFileExtension;

            // Parse format-specific material data for accurate transparency detection (Blender-style approach)
            if (!string.IsNullOrEmpty(modelFilePath) && File.Exists(modelFilePath))
            {
                switch (modelFileExtension.ToLowerInvariant())
                {
                    case ".gltf":
                        Engine.Utils.DebugLogger.Log($"[MaterialExtractor] Parsing GLTF JSON for transparency data: {Path.GetFileName(modelFilePath)}");
                        _gltfTransparencyData = GltfMaterialParser.ParseGltfMaterials(modelFilePath);
                        _gltfTextureImages = GltfMaterialParser.ParseGltfTextureImages(modelFilePath);
                        Engine.Utils.DebugLogger.Log($"[MaterialExtractor] Found {_gltfTransparencyData.Count} material(s) and {_gltfTextureImages.Count} texture(s) in GLTF JSON");
                        break;

                    case ".fbx":
                        Engine.Utils.DebugLogger.Log($"[MaterialExtractor] Parsing FBX for transparency data: {Path.GetFileName(modelFilePath)}");
                        _fbxTransparencyData = FbxMaterialParser.ParseFbxMaterials(modelFilePath);
                        if (_fbxTransparencyData.Count > 0)
                            Engine.Utils.DebugLogger.Log($"[MaterialExtractor] Found {_fbxTransparencyData.Count} material(s) in FBX");
                        break;

                    case ".obj":
                        Engine.Utils.DebugLogger.Log($"[MaterialExtractor] Parsing OBJ/MTL for transparency data: {Path.GetFileName(modelFilePath)}");
                        _objTransparencyData = ObjMaterialParser.ParseObjMaterials(modelFilePath);
                        if (_objTransparencyData.Count > 0)
                            Engine.Utils.DebugLogger.Log($"[MaterialExtractor] Found {_objTransparencyData.Count} material(s) in MTL");
                        break;

                    case ".dae":
                        Engine.Utils.DebugLogger.Log($"[MaterialExtractor] Parsing Collada DAE for transparency data: {Path.GetFileName(modelFilePath)}");
                        _daeTransparencyData = DaeMaterialParser.ParseDaeMaterials(modelFilePath);
                        if (_daeTransparencyData.Count > 0)
                            Engine.Utils.DebugLogger.Log($"[MaterialExtractor] Found {_daeTransparencyData.Count} material(s) in DAE");
                        break;
                }
            }

            Directory.CreateDirectory(_materialsOutputDirectory);
        }

        /// <summary>
        /// Extract all materials from Assimp scene.
        /// Returns list of MaterialAssets with GUIDs assigned.
        /// </summary>
        public List<MaterialAsset> ExtractMaterials(Assimp.Scene scene, Dictionary<string, Guid> textureMap)
        {
            var materials = new List<MaterialAsset>();

            if (!scene.HasMaterials)
            {
                Engine.Utils.DebugLogger.Log("[MaterialExtractor] No materials found in scene");
                return materials;
            }

            Engine.Utils.DebugLogger.Log($"[MaterialExtractor] Extracting {scene.MaterialCount} material(s)");

            for (int i = 0; i < scene.MaterialCount; i++)
            {
                try
                {
                    var assimpMaterial = scene.Materials[i];
                    var materialAsset = ExtractMaterial(assimpMaterial, i, textureMap);

                    if (materialAsset != null)
                    {
                        materials.Add(materialAsset);
                        _extractedMaterials[i] = materialAsset;
                    }
                }
                catch (Exception ex)
                {
                    Engine.Utils.DebugLogger.Log($"[MaterialExtractor] Failed to extract material {i}: {ex.Message}");
                }
            }

            return materials;
        }

        /// <summary>
        /// Extract a single material following PBR metallic-roughness workflow.
        /// Based on glTF 2.0 specification and Assimp material property mapping.
        /// </summary>
        private MaterialAsset? ExtractMaterial(Assimp.Material assimpMaterial, int index, Dictionary<string, Guid> textureMap)
        {
            // Extract material info using ModelLoader's method with format-specific transparency detection
            var matInfo = ModelLoader.ExtractMaterialInfo(assimpMaterial, _modelFileExtension);

            // Generate material name
            string materialName = string.IsNullOrWhiteSpace(matInfo.Name) || matInfo.Name == "DefaultMaterial"
                ? $"{_modelName}_Mat{index}"
                : SanitizeName(matInfo.Name);

            Engine.Utils.DebugLogger.Log($"[MaterialExtractor] Processing material '{materialName}'");

            // For GLTF, use direct JSON baseColorFactor if available (more accurate than Assimp)
            float[] albedoColor = matInfo.AlbedoColor;
            if (_gltfTransparencyData != null && _gltfTransparencyData.TryGetValue(materialName, out var gltfInfo))
            {
                // Override with GLTF JSON baseColorFactor (Blender-accurate)
                albedoColor = gltfInfo.BaseColorFactor;
                Engine.Utils.DebugLogger.Log(
                    $"[MaterialExtractor] Using GLTF baseColorFactor for '{materialName}': " +
                    $"R={albedoColor[0]:F3}, G={albedoColor[1]:F3}, B={albedoColor[2]:F3}, A={albedoColor[3]:F3}");
            }

            // Create PBR material asset
            var material = new MaterialAsset
            {
                Guid = Guid.NewGuid(),
                Name = materialName,
                Shader = "ForwardBase",
                AlbedoColor = albedoColor,
                Metallic = matInfo.Metallic,
                Roughness = matInfo.Roughness,
                Opacity = matInfo.Opacity
            };

            // Determine transparency mode
            // Following glTF 2.0 alphaMode specification: OPAQUE, MASK, BLEND
            bool isTransparent = DetermineTransparency(matInfo, materialName);
            material.TransparencyMode = isTransparent ? 1 : 0;

            Engine.Utils.DebugLogger.Log($"[MaterialExtractor] '{materialName}' - Opacity: {matInfo.Opacity}, AlphaMode: {matInfo.AlphaMode}, Transparent: {isTransparent}");

            // Assign textures from material references
            AssignTexturesFromMaterial(material, matInfo, textureMap);

            // Auto-assign missing textures from naming conventions
            AutoAssignMissingTextures(material, materialName, isTransparent);

            // Save material to disk
            SaveMaterial(material);

            return material;
        }

        /// <summary>
        /// Determine if material should use transparency.
        /// Uses format-specific parsers (Blender-style approach) for maximum accuracy:
        /// - GLTF: Direct JSON parsing for alphaMode and baseColorFactor
        /// - FBX: Parse TransparencyFactor and Opacity properties
        /// - OBJ: Parse MTL file for dissolve (d) and transparency (Tr) values
        /// - DAE: Parse Collada XML for transparency and blend mode
        /// Falls back to Assimp-based detection if format-specific data unavailable.
        /// </summary>
        private bool DetermineTransparency(MaterialInfo matInfo, string materialName)
        {
            // ═══════════════════════════════════════════════════════════════
            // PRIORITY 1: Format-specific direct file parsing (most reliable!)
            // ═══════════════════════════════════════════════════════════════

            // --- GLTF: Direct JSON parsing ---
            if (_gltfTransparencyData != null && _gltfTransparencyData.TryGetValue(materialName, out var gltfInfo))
            {
                Engine.Utils.DebugLogger.Log(
                    $"[MaterialExtractor] Using GLTF JSON data for '{materialName}': " +
                    $"alphaMode={gltfInfo.AlphaMode}, baseColorAlpha={gltfInfo.BaseColorAlpha:F3}");

                if (gltfInfo.AlphaMode == "BLEND")
                {
                    Engine.Utils.DebugLogger.Log($"[MaterialExtractor] Material uses BLEND alpha mode (transparent)");
                    return true;
                }

                if (gltfInfo.AlphaMode == "MASK")
                {
                    Engine.Utils.DebugLogger.Log($"[MaterialExtractor] Material uses MASK alpha mode with cutoff {gltfInfo.AlphaCutoff} (transparent)");
                    return true;
                }

                if (gltfInfo.BaseColorAlpha < 0.99f)
                {
                    Engine.Utils.DebugLogger.Log($"[MaterialExtractor] Material has baseColorAlpha < 0.99 ({gltfInfo.BaseColorAlpha:F3}) (transparent)");
                    return true;
                }

                Engine.Utils.DebugLogger.Log($"[MaterialExtractor] GLTF JSON indicates OPAQUE material");
                return false;
            }

            // --- FBX: Direct file parsing or naming conventions ---
            if (_fbxTransparencyData != null && _fbxTransparencyData.TryGetValue(materialName, out var fbxInfo))
            {
                Engine.Utils.DebugLogger.Log(
                    $"[MaterialExtractor] Using FBX data for '{materialName}': " +
                    $"opacity={fbxInfo.Opacity:F3}, transparencyFactor={fbxInfo.TransparencyFactor:F3}");

                if (fbxInfo.IsTransparent)
                {
                    Engine.Utils.DebugLogger.Log($"[MaterialExtractor] FBX material is transparent");
                    return true;
                }
            }
            // FBX fallback: naming conventions
            else if (_modelFileExtension == ".fbx" && FbxMaterialParser.IsTransparentByName(materialName))
            {
                Engine.Utils.DebugLogger.Log($"[MaterialExtractor] FBX material detected as transparent by name: '{materialName}'");
                return true;
            }

            // --- OBJ: Parse MTL file ---
            if (_objTransparencyData != null && _objTransparencyData.TryGetValue(materialName, out var objInfo))
            {
                Engine.Utils.DebugLogger.Log(
                    $"[MaterialExtractor] Using OBJ/MTL data for '{materialName}': " +
                    $"dissolve={objInfo.Dissolve:F3}, opacity={objInfo.Opacity:F3}, hasOpacityMap={objInfo.HasOpacityMap}");

                if (objInfo.IsTransparent)
                {
                    Engine.Utils.DebugLogger.Log($"[MaterialExtractor] OBJ/MTL material is transparent");
                    return true;
                }
            }

            // --- DAE: Parse Collada XML ---
            if (_daeTransparencyData != null && _daeTransparencyData.TryGetValue(materialName, out var daeInfo))
            {
                Engine.Utils.DebugLogger.Log(
                    $"[MaterialExtractor] Using DAE/Collada data for '{materialName}': " +
                    $"transparency={daeInfo.Transparency:F3}, hasTransparentTag={daeInfo.HasTransparentTag}, blendMode={daeInfo.BlendMode ?? "none"}");

                if (daeInfo.IsTransparent)
                {
                    Engine.Utils.DebugLogger.Log($"[MaterialExtractor] DAE/Collada material is transparent");
                    return true;
                }
            }

            // ═══════════════════════════════════════════════════════════════
            // PRIORITY 2: Assimp-based detection (fallback when format-specific parsing unavailable)
            // ═══════════════════════════════════════════════════════════════
            
            // Explicit BLEND mode from glTF
            if (matInfo.AlphaMode == "BLEND")
            {
                Engine.Utils.DebugLogger.Log($"[MaterialExtractor] Material uses BLEND alpha mode (transparent)");
                return true;
            }

            // Low opacity indicates intentional transparency
            if (matInfo.Opacity < 0.95f)
            {
                Engine.Utils.DebugLogger.Log($"[MaterialExtractor] Material has low opacity: {matInfo.Opacity} (transparent)");
                return true;
            }

            // Has explicit opacity texture
            if (!string.IsNullOrWhiteSpace(matInfo.OpacityTexturePath))
            {
                Engine.Utils.DebugLogger.Log($"[MaterialExtractor] Material has opacity texture (transparent)");
                return true;
            }

            // NOTE: We do NOT automatically enable transparency just because texture has alpha channel.
            // The model's AlphaMode should specify this explicitly (many textures use alpha for cutout masks).

            return false;
        }

        /// <summary>
        /// Assign textures that are explicitly referenced in the material.
        /// </summary>
        private void AssignTexturesFromMaterial(MaterialAsset material, MaterialInfo matInfo, Dictionary<string, Guid> textureMap)
        {
            // GLTF-SPECIFIC: Enrich MaterialInfo with texture paths from GLTF JSON
            // Assimp doesn't always correctly map GLTF 2.0 PBR textures, so we read them directly
            if (_gltfTransparencyData != null && _gltfTextureImages != null)
            {
                if (_gltfTransparencyData.TryGetValue(matInfo.Name ?? "", out var gltfInfo))
                {
                    Engine.Utils.DebugLogger.Log($"[MaterialExtractor] ✓ Found GLTF material data for '{matInfo.Name}', enriching texture paths...");
                    
                    // Resolve texture indices to file paths
                    if (gltfInfo.BaseColorTextureIndex.HasValue && _gltfTextureImages.TryGetValue(gltfInfo.BaseColorTextureIndex.Value, out var baseColorPath))
                    {
                        if (string.IsNullOrWhiteSpace(matInfo.AlbedoTexturePath))
                        {
                            matInfo.AlbedoTexturePath = baseColorPath;
                            Engine.Utils.DebugLogger.Log($"[MaterialExtractor]   Enriched AlbedoTexture: {baseColorPath}");
                        }
                    }
                    
                    if (gltfInfo.NormalTextureIndex.HasValue && _gltfTextureImages.TryGetValue(gltfInfo.NormalTextureIndex.Value, out var normalPath))
                    {
                        if (string.IsNullOrWhiteSpace(matInfo.NormalTexturePath))
                        {
                            matInfo.NormalTexturePath = normalPath;
                            Engine.Utils.DebugLogger.Log($"[MaterialExtractor]   Enriched NormalTexture: {normalPath}");
                        }
                    }
                    
                    // CRITICAL: MetallicRoughness combined texture (GLTF 2.0 PBR!)
                    if (gltfInfo.MetallicRoughnessTextureIndex.HasValue && _gltfTextureImages.TryGetValue(gltfInfo.MetallicRoughnessTextureIndex.Value, out var mrPath))
                    {
                        if (string.IsNullOrWhiteSpace(matInfo.MetallicRoughnessTexturePath))
                        {
                            matInfo.MetallicRoughnessTexturePath = mrPath;
                            Engine.Utils.DebugLogger.Log($"[MaterialExtractor]   ✓✓✓ Enriched MetallicRoughnessTexture: {mrPath}");
                        }
                    }
                    
                    if (gltfInfo.OcclusionTextureIndex.HasValue && _gltfTextureImages.TryGetValue(gltfInfo.OcclusionTextureIndex.Value, out var aoPath))
                    {
                        if (string.IsNullOrWhiteSpace(matInfo.AmbientOcclusionTexturePath))
                        {
                            matInfo.AmbientOcclusionTexturePath = aoPath;
                            Engine.Utils.DebugLogger.Log($"[MaterialExtractor]   Enriched OcclusionTexture: {aoPath}");
                        }
                    }
                    
                    if (gltfInfo.EmissiveTextureIndex.HasValue && _gltfTextureImages.TryGetValue(gltfInfo.EmissiveTextureIndex.Value, out var emissivePath))
                    {
                        if (string.IsNullOrWhiteSpace(matInfo.EmissiveTexturePath))
                        {
                            matInfo.EmissiveTexturePath = emissivePath;
                            Engine.Utils.DebugLogger.Log($"[MaterialExtractor]   Enriched EmissiveTexture: {emissivePath}");
                        }
                    }
                }
            }
            
            // Albedo/Base Color
            if (!string.IsNullOrWhiteSpace(matInfo.AlbedoTexturePath))
            {
                if (TryResolveTextureGuid(matInfo.AlbedoTexturePath, textureMap, out var albedoGuid))
                {
                    material.AlbedoTexture = albedoGuid;
                    Engine.Utils.DebugLogger.Log($"[MaterialExtractor] Assigned albedo: {matInfo.AlbedoTexturePath}");
                }
            }

            // Normal Map
            if (!string.IsNullOrWhiteSpace(matInfo.NormalTexturePath))
            {
                if (TryResolveTextureGuid(matInfo.NormalTexturePath, textureMap, out var normalGuid))
                {
                    material.NormalTexture = normalGuid;
                    material.NormalStrength = 1.0f;
                    Engine.Utils.DebugLogger.Log($"[MaterialExtractor] Assigned normal: {matInfo.NormalTexturePath}");
                }
            }

            // Metallic-Roughness combined texture (glTF 2.0 standard)
            if (!string.IsNullOrWhiteSpace(matInfo.MetallicRoughnessTexturePath))
            {
                if (TryResolveTextureGuid(matInfo.MetallicRoughnessTexturePath, textureMap, out var mrGuid))
                {
                    material.MetallicRoughnessTexture = mrGuid;
                    Engine.Utils.DebugLogger.Log($"[MaterialExtractor] Assigned combined metallic-roughness texture: {matInfo.MetallicRoughnessTexturePath}");
                }
            }

            // Individual Metallic texture
            if (!string.IsNullOrWhiteSpace(matInfo.MetallicTexturePath))
            {
                if (TryResolveTextureGuid(matInfo.MetallicTexturePath, textureMap, out var metallicGuid))
                {
                    material.MetallicTexture = metallicGuid;
                    Engine.Utils.DebugLogger.Log($"[MaterialExtractor] Assigned metallic texture: {matInfo.MetallicTexturePath}");
                }
            }

            // Individual Roughness texture
            if (!string.IsNullOrWhiteSpace(matInfo.RoughnessTexturePath))
            {
                if (TryResolveTextureGuid(matInfo.RoughnessTexturePath, textureMap, out var roughnessGuid))
                {
                    material.RoughnessTexture = roughnessGuid;
                    Engine.Utils.DebugLogger.Log($"[MaterialExtractor] Assigned roughness texture: {matInfo.RoughnessTexturePath}");
                }
            }

            // Ambient Occlusion
            if (!string.IsNullOrWhiteSpace(matInfo.AmbientOcclusionTexturePath))
            {
                if (TryResolveTextureGuid(matInfo.AmbientOcclusionTexturePath, textureMap, out var aoGuid))
                {
                    material.OcclusionTexture = aoGuid;
                    Engine.Utils.DebugLogger.Log($"[MaterialExtractor] Assigned AO texture: {matInfo.AmbientOcclusionTexturePath}");
                }
            }

            // Emissive
            if (!string.IsNullOrWhiteSpace(matInfo.EmissiveTexturePath))
            {
                if (TryResolveTextureGuid(matInfo.EmissiveTexturePath, textureMap, out var emissiveGuid))
                {
                    material.EmissiveTexture = emissiveGuid;
                    // Set default Emission strength to 1.0 when emissive texture is present
                    // (default is 0.0 which makes emissive invisible)
                    material.Emission = 1.0f;
                    Engine.Utils.DebugLogger.Log($"[MaterialExtractor] Assigned emissive texture: {matInfo.EmissiveTexturePath} (Emission=1.0)");
                }
            }

            // Opacity
            if (!string.IsNullOrWhiteSpace(matInfo.OpacityTexturePath))
            {
                if (TryResolveTextureGuid(matInfo.OpacityTexturePath, textureMap, out var opacityGuid))
                {
                    Engine.Utils.DebugLogger.Log($"[MaterialExtractor] Found opacity texture: {matInfo.OpacityTexturePath}");
                    // TODO: Add OpacityTexture property to MaterialAsset if needed for future transparency workflows
                }
            }
        }

        /// <summary>
        /// Auto-assign missing textures based on filename patterns.
        /// Follows naming conventions from Substance Painter, Blender, Quixel, etc.
        /// </summary>
        private void AutoAssignMissingTextures(MaterialAsset material, string materialName, bool isTransparent)
        {
            // Skip auto-assignment for transparent materials without any textures
            // (they're likely glass/clear materials that shouldn't have textures)
            bool hasSomeTexture = material.AlbedoTexture.HasValue || material.NormalTexture.HasValue;
            if (isTransparent && !hasSomeTexture)
            {
                Engine.Utils.DebugLogger.Log($"[MaterialExtractor] Skipping auto-assignment for transparent material without textures");
                return;
            }

            Engine.Utils.DebugLogger.Log($"[MaterialExtractor] Running auto-assignment for '{materialName}'");

            var textureSet = _textureExtractor.AutoAssignTextures(materialName);

            // Assign auto-detected textures (only if not already set)
            if (!material.AlbedoTexture.HasValue && textureSet.Albedo.HasValue)
            {
                material.AlbedoTexture = textureSet.Albedo;
                Engine.Utils.DebugLogger.Log($"[MaterialExtractor] Auto-assigned albedo texture");
            }

            if (!material.NormalTexture.HasValue && textureSet.Normal.HasValue)
            {
                material.NormalTexture = textureSet.Normal;
                material.NormalStrength = 1.0f;
                Engine.Utils.DebugLogger.Log($"[MaterialExtractor] Auto-assigned normal texture");
            }

            // TODO: Assign other texture types when MaterialAsset supports them
        }

        /// <summary>
        /// Try to resolve texture GUID from texture map.
        /// Handles embedded textures (e.g., "*0") and regular file paths.
        /// </summary>
        private bool TryResolveTextureGuid(string texturePath, Dictionary<string, Guid> textureMap, out Guid guid)
        {
            guid = Guid.Empty;

            // Try direct path match
            if (textureMap.TryGetValue(texturePath, out guid))
                return true;

            // Try filename only
            string filename = Path.GetFileName(texturePath);
            if (!string.IsNullOrEmpty(filename) && textureMap.TryGetValue(filename, out guid))
                return true;

            return false;
        }

        /// <summary>
        /// Save material asset to disk with metadata.
        /// </summary>
        private void SaveMaterial(MaterialAsset material)
        {
            string materialFileName = SanitizeName(material.Name ?? "Material") + ".material";
            string materialPath = Path.Combine(_materialsOutputDirectory, materialFileName);

            // Handle duplicates
            int counter = 1;
            while (File.Exists(materialPath))
            {
                materialFileName = $"{SanitizeName(material.Name ?? "Material")}_{counter}.material";
                materialPath = Path.Combine(_materialsOutputDirectory, materialFileName);
                counter++;
            }

            // Save material
            MaterialAsset.Save(materialPath, material);
            Engine.Utils.DebugLogger.Log($"[MaterialExtractor] Saved material: {materialFileName}");

            // Create metadata
            CreateMaterialMetadata(materialPath, material.Guid);
        }

        /// <summary>
        /// Create .meta file for material.
        /// </summary>
        private void CreateMaterialMetadata(string materialPath, Guid guid)
        {
            var metaPath = materialPath + ".meta";

            var metaData = new AssetDatabase.MetaData
            {
                guid = guid,
                type = "Material"
            };

            var json = System.Text.Json.JsonSerializer.Serialize(metaData,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(metaPath, json);
        }

        /// <summary>
        /// Sanitize name for use as filename.
        /// </summary>
        private string SanitizeName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name.Trim();
        }

        /// <summary>
        /// Get extracted material by original Assimp index.
        /// </summary>
        public MaterialAsset? GetMaterial(int index)
        {
            return _extractedMaterials.TryGetValue(index, out var material) ? material : null;
        }
    }
}

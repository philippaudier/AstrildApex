using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assimp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using BCnEncoder.Decoder;
using BCnEncoder.ImageSharp;

namespace Engine.Assets.Import
{
    /// <summary>
    /// Handles texture extraction, detection, and management during model import.
    /// Implements best practices from modern game engines (Unity, Unreal, glTF spec).
    /// </summary>
    public sealed class TextureExtractor
    {
        private readonly string _modelDirectory;
        private readonly string _texturesOutputDirectory;
        private readonly string _modelName;
        private readonly Dictionary<string, Guid> _extractedTextures = new();

        public TextureExtractor(string modelFilePath, string outputTexturesDirectory)
        {
            _modelDirectory = Path.GetDirectoryName(modelFilePath) ?? throw new ArgumentException("Invalid model path");
            _texturesOutputDirectory = outputTexturesDirectory;
            _modelName = Path.GetFileNameWithoutExtension(modelFilePath);

            Directory.CreateDirectory(_texturesOutputDirectory);
        }

        /// <summary>
        /// Extract all textures referenced by the model.
        /// Returns mapping of original paths to GUIDs.
        /// </summary>
        public Dictionary<string, Guid> ExtractTextures(Assimp.Scene scene, IEnumerable<MaterialInfo> materials)
        {
            // Phase 1: Extract embedded textures from scene
            ExtractEmbeddedTextures(scene);

            // Phase 2: Copy external textures referenced by materials
            foreach (var material in materials)
            {
                ExtractMaterialTextures(material);
            }

            // Phase 3: Scan source directory for additional textures (fallback)
            ScanAndCopyAdditionalTextures();

            return _extractedTextures;
        }

        /// <summary>
        /// Extract embedded textures from GLB/GLTF files.
        /// Follows glTF 2.0 specification for embedded texture handling.
        /// </summary>
        private void ExtractEmbeddedTextures(Assimp.Scene scene)
        {
            if (!scene.HasTextures)
                return;

            Engine.Utils.DebugLogger.Log($"[TextureExtractor] Extracting {scene.TextureCount} embedded texture(s)");

            for (int i = 0; i < scene.TextureCount; i++)
            {
                try
                {
                    var texture = scene.Textures[i];
                    if (texture == null || !texture.IsCompressed)
                        continue; // Skip raw texel data (uncommon in modern formats)

                    // Use PNG as default extension for embedded textures
                    string extension = ".png";
                    string filename = $"{_modelName}_embedded_{i}{extension}";
                    string outputPath = Path.Combine(_texturesOutputDirectory, filename);

                    // Handle duplicates
                    outputPath = EnsureUniqueFilePath(outputPath);

                    // Write compressed data directly
                    File.WriteAllBytes(outputPath, texture.CompressedData);

                    // Create metadata and track
                    var guid = CreateTextureMetadata(outputPath);
                    _extractedTextures[$"*{i}"] = guid; // Assimp uses "*<index>" for embedded textures

                    Engine.Utils.DebugLogger.Log($"[TextureExtractor] Extracted embedded texture: {filename}");
                }
                catch (Exception ex)
                {
                    Engine.Utils.DebugLogger.Log($"[TextureExtractor] Failed to extract embedded texture {i}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Extract and copy textures referenced by a material.
        /// Implements texture search strategies from Unity/Unreal FBX pipelines.
        /// </summary>
        private void ExtractMaterialTextures(MaterialInfo material)
        {
            // Extract all texture types following PBR workflow
            TryExtractTexture(material.AlbedoTexturePath, TextureType.Albedo);
            TryExtractTexture(material.NormalTexturePath, TextureType.Normal);
            TryExtractTexture(material.MetallicTexturePath, TextureType.Metallic);
            TryExtractTexture(material.RoughnessTexturePath, TextureType.Roughness);
            TryExtractTexture(material.MetallicRoughnessTexturePath, TextureType.MetallicRoughness);
            TryExtractTexture(material.AmbientOcclusionTexturePath, TextureType.AO);
            TryExtractTexture(material.EmissiveTexturePath, TextureType.Emissive);
            TryExtractTexture(material.OpacityTexturePath, TextureType.Opacity);
        }

        /// <summary>
        /// Try to extract a single texture file using multiple search strategies.
        /// Based on Assimp texture resolution best practices.
        /// </summary>
        private Guid? TryExtractTexture(string? texturePath, TextureType type)
        {
            if (string.IsNullOrWhiteSpace(texturePath))
                return null;

            // Check if already extracted
            if (_extractedTextures.TryGetValue(texturePath, out var existingGuid))
                return existingGuid;

            // Handle embedded texture reference (e.g., "*0")
            if (texturePath.StartsWith("*"))
            {
                // Already handled in ExtractEmbeddedTextures
                return _extractedTextures.TryGetValue(texturePath, out var guid) ? guid : null;
            }

            // Resolve texture file using multiple search strategies
            string? resolvedPath = ResolveTexturePath(texturePath);
            if (resolvedPath == null)
            {
                Engine.Utils.DebugLogger.Log($"[TextureExtractor] Texture not found: {texturePath} ({type})");
                return null;
            }

            // Validate texture is not a normal map misclassified as albedo
            if (type == TextureType.Albedo && IsLikelyNormalMap(resolvedPath))
            {
                Engine.Utils.DebugLogger.Log($"[TextureExtractor] Skipping {Path.GetFileName(resolvedPath)} - detected as normal map");
                return null;
            }

            // Copy to output directory
            try
            {
                string filename = Path.GetFileName(resolvedPath);
                string outputPath = Path.Combine(_texturesOutputDirectory, filename);

                // Avoid duplicates
                if (!File.Exists(outputPath))
                {
                    File.Copy(resolvedPath, outputPath, overwrite: false);
                    Engine.Utils.DebugLogger.Log($"[TextureExtractor] Copied {type} texture: {filename}");
                }

                var guid = CreateTextureMetadata(outputPath);
                _extractedTextures[texturePath] = guid;
                _extractedTextures[filename] = guid; // Also index by filename for easier lookup

                return guid;
            }
            catch (Exception ex)
            {
                Engine.Utils.DebugLogger.Log($"[TextureExtractor] Failed to copy texture {texturePath}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Resolve texture path using multiple search strategies.
        /// Strategy order based on Assimp documentation and Unity FBX import behavior.
        /// </summary>
        private string? ResolveTexturePath(string texturePath)
        {
            // Normalize path separators
            string normalizedPath = texturePath.Replace('/', Path.DirectorySeparatorChar);

            var searchPaths = new List<string>();

            // Strategy 1: Relative to model file
            if (!Path.IsPathRooted(normalizedPath))
            {
                searchPaths.Add(Path.Combine(_modelDirectory, normalizedPath));
            }

            // Strategy 2: Filename only in model directory
            string filename = Path.GetFileName(normalizedPath);
            if (!string.IsNullOrEmpty(filename))
            {
                searchPaths.Add(Path.Combine(_modelDirectory, filename));
            }

            // Strategy 3: Common texture subdirectories
            if (!string.IsNullOrEmpty(filename))
            {
                searchPaths.Add(Path.Combine(_modelDirectory, "textures", filename));
                searchPaths.Add(Path.Combine(_modelDirectory, "Textures", filename));
                searchPaths.Add(Path.Combine(_modelDirectory, "texture", filename));
                searchPaths.Add(Path.Combine(_modelDirectory, "images", filename));
                searchPaths.Add(Path.Combine(_modelDirectory, "Images", filename));
            }

            // Strategy 4: Absolute path (if provided)
            if (Path.IsPathRooted(normalizedPath))
            {
                searchPaths.Add(normalizedPath);
            }

            // Try each search path
            foreach (var searchPath in searchPaths)
            {
                try
                {
                    string fullPath = Path.GetFullPath(searchPath);
                    if (File.Exists(fullPath))
                    {
                        return fullPath;
                    }
                }
                catch
                {
                    // Invalid path, continue
                }
            }

            // Strategy 5: Deep recursive search as last resort
            if (!string.IsNullOrEmpty(filename))
            {
                try
                {
                    var candidates = Directory.GetFiles(_modelDirectory, filename, SearchOption.AllDirectories);
                    if (candidates.Length > 0)
                    {
                        Engine.Utils.DebugLogger.Log($"[TextureExtractor] Found texture via deep search: {filename}");
                        return candidates[0];
                    }
                }
                catch
                {
                    // Search failed
                }
            }

            return null;
        }

        /// <summary>
        /// Scan source directory for additional textures and copy them.
        /// Helps with models that don't properly reference all textures in metadata.
        /// </summary>
        private void ScanAndCopyAdditionalTextures()
        {
            var supportedExtensions = new[] { ".png", ".jpg", ".jpeg", ".tga", ".bmp", ".dds", ".tiff", ".exr", ".hdr" };

            try
            {
                var textureFiles = Directory.GetFiles(_modelDirectory, "*.*", SearchOption.AllDirectories)
                    .Where(f => supportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .ToList();

                Engine.Utils.DebugLogger.Log($"[TextureExtractor] Scanning directory: found {textureFiles.Count} texture file(s)");

                foreach (var textureFile in textureFiles)
                {
                    string filename = Path.GetFileName(textureFile);

                    // Skip if already extracted
                    if (_extractedTextures.ContainsKey(filename))
                        continue;

                    string outputPath = Path.Combine(_texturesOutputDirectory, filename);

                    // Handle duplicates
                    if (File.Exists(outputPath))
                        continue;

                    try
                    {
                        File.Copy(textureFile, outputPath, overwrite: false);
                        var guid = CreateTextureMetadata(outputPath);
                        _extractedTextures[filename] = guid;
                    }
                    catch (Exception ex)
                    {
                        Engine.Utils.DebugLogger.Log($"[TextureExtractor] Failed to copy additional texture {filename}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Engine.Utils.DebugLogger.Log($"[TextureExtractor] Failed to scan directory: {ex.Message}");
            }
        }

        /// <summary>
        /// Auto-assign textures to material based on naming conventions.
        /// Supports common naming patterns from Blender, Substance Painter, Quixel, etc.
        /// </summary>
        public MaterialTextureSet AutoAssignTextures(string materialName)
        {
            var result = new MaterialTextureSet();
            var textureFiles = Directory.GetFiles(_texturesOutputDirectory);

            // Naming pattern detection (ordered by priority)
            var patterns = new Dictionary<TextureType, string[]>
            {
                [TextureType.Albedo] = new[] { "basecolor", "albedo", "diffuse", "color", "_d.", "_col.", "_base.", "diff" },
                [TextureType.Normal] = new[] { "normal", "norm", "_n.", "nm.", "normalmap", "bump" },
                [TextureType.Metallic] = new[] { "metallic", "metal", "_m.", "metalness" },
                [TextureType.Roughness] = new[] { "rough", "_r.", "roughness" },
                [TextureType.MetallicRoughness] = new[] { "metallicroughness", "metalrough", "orm" },
                [TextureType.AO] = new[] { "ao", "ambientocclusion", "ambient", "occlusion" },
                [TextureType.Emissive] = new[] { "emissive", "emit", "emission", "glow" },
                [TextureType.Opacity] = new[] { "opacity", "alpha", "transparency", "transparent" }
            };

            foreach (var textureFile in textureFiles)
            {
                string filename = Path.GetFileName(textureFile);
                string filenameLower = filename.ToLowerInvariant();
                string baseName = Path.GetFileNameWithoutExtension(filenameLower);

                // Try to get GUID
                if (!_extractedTextures.TryGetValue(filename, out var guid))
                {
                    // Try loading from meta file
                    var metaPath = textureFile + ".meta";
                    if (File.Exists(metaPath))
                    {
                        try
                        {
                            var metaJson = File.ReadAllText(metaPath);
                            var metaData = System.Text.Json.JsonSerializer.Deserialize<AssetDatabase.MetaData>(metaJson);
                            if (metaData != null && metaData.guid != Guid.Empty)
                            {
                                guid = metaData.guid;
                            }
                        }
                        catch { }
                    }
                }

                if (guid == Guid.Empty)
                    continue;

                // Match texture to type
                foreach (var (type, typePatterns) in patterns)
                {
                    if (result.HasTextureType(type))
                        continue; // Already assigned

                    bool matches = typePatterns.Any(pattern => baseName.Contains(pattern));

                    // Also check if filename matches material name (common for albedo)
                    if (type == TextureType.Albedo && !matches)
                    {
                        matches = baseName == materialName.ToLowerInvariant();
                    }

                    if (matches)
                    {
                        result.SetTexture(type, guid, filename);
                        Engine.Utils.DebugLogger.Log($"[TextureExtractor] Auto-assigned {type}: {filename}");
                        break;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Ensure file path is unique by appending a number if needed.
        /// </summary>
        private string EnsureUniqueFilePath(string path)
        {
            if (!File.Exists(path))
                return path;

            string directory = Path.GetDirectoryName(path) ?? "";
            string filename = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path);

            int counter = 1;
            string newPath;
            do
            {
                newPath = Path.Combine(directory, $"{filename}_{counter}{extension}");
                counter++;
            } while (File.Exists(newPath));

            return newPath;
        }

        /// <summary>
        /// Create .meta file for texture and return GUID.
        /// </summary>
        private Guid CreateTextureMetadata(string texturePath)
        {
            var guid = Guid.NewGuid();
            var metaPath = texturePath + ".meta";

            var metaData = new AssetDatabase.MetaData
            {
                guid = guid,
                type = "Texture2D"
            };

            var json = System.Text.Json.JsonSerializer.Serialize(metaData,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(metaPath, json);

            return guid;
        }

        /// <summary>
        /// Heuristic: Check if image is likely a normal map.
        /// Based on average color distribution (normal maps are typically bluish-purple).
        /// </summary>
        private bool IsLikelyNormalMap(string imagePath)
        {
            try
            {
                // Quick filename check first
                string filename = Path.GetFileNameWithoutExtension(imagePath).ToLowerInvariant();
                if (filename.Contains("normal") || filename.Contains("_n.") || filename.EndsWith("_n") ||
                    filename.Contains("nm") || filename.EndsWith("_nm"))
                {
                    return true;
                }

                // Pixel analysis for files without clear naming
                var ext = Path.GetExtension(imagePath).ToLowerInvariant();
                const int maxSamples = 2048;
                long sumR = 0, sumG = 0, sumB = 0;
                int samples = 0;

                if (ext == ".dds")
                {
                    using var fs = File.OpenRead(imagePath);
                    var decoder = new BcDecoder();
                    var image = decoder.DecodeToImageRgba32(fs);
                    image.ProcessPixelRows(accessor =>
                    {
                        for (int y = 0; y < accessor.Height && samples < maxSamples; y++)
                        {
                            var row = accessor.GetRowSpan(y);
                            for (int x = 0; x < accessor.Width && samples < maxSamples; x++)
                            {
                                var p = row[x];
                                sumR += p.R;
                                sumG += p.G;
                                sumB += p.B;
                                samples++;
                            }
                        }
                    });
                }
                else if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".tga" || ext == ".bmp")
                {
                    using var image = Image.Load<Rgba32>(imagePath);
                    image.ProcessPixelRows(accessor =>
                    {
                        for (int y = 0; y < accessor.Height && samples < maxSamples; y++)
                        {
                            var row = accessor.GetRowSpan(y);
                            for (int x = 0; x < accessor.Width && samples < maxSamples; x++)
                            {
                                var p = row[x];
                                sumR += p.R;
                                sumG += p.G;
                                sumB += p.B;
                                samples++;
                            }
                        }
                    });
                }

                if (samples == 0)
                    return false;

                // Normal maps: R≈128, G≈128, B>140 (bluish-purple)
                double avgR = sumR / (double)samples;
                double avgG = sumG / (double)samples;
                double avgB = sumB / (double)samples;

                return Math.Abs(avgR - 128.0) < 25.0 &&
                       Math.Abs(avgG - 128.0) < 25.0 &&
                       avgB > 135.0;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Texture types following PBR (Physically Based Rendering) workflow.
    /// Based on glTF 2.0 specification and modern game engine standards.
    /// </summary>
    public enum TextureType
    {
        Albedo,              // Base color (RGB) or diffuse
        Normal,              // Tangent-space normal map
        Metallic,            // Metalness (grayscale)
        Roughness,           // Roughness (grayscale)
        MetallicRoughness,   // Combined: Metallic (B) + Roughness (G) - glTF 2.0 standard
        AO,                  // Ambient Occlusion
        Emissive,            // Emissive/glow
        Opacity,             // Alpha/transparency
        Height,              // Displacement/parallax
        Specular             // Specular (legacy PBR workflow)
    }

    /// <summary>
    /// Container for all textures assigned to a material.
    /// </summary>
    public sealed class MaterialTextureSet
    {
        public Guid? Albedo { get; private set; }
        public Guid? Normal { get; private set; }
        public Guid? Metallic { get; private set; }
        public Guid? Roughness { get; private set; }
        public Guid? MetallicRoughness { get; private set; }
        public Guid? AO { get; private set; }
        public Guid? Emissive { get; private set; }
        public Guid? Opacity { get; private set; }

        public void SetTexture(TextureType type, Guid guid, string filename)
        {
            switch (type)
            {
                case TextureType.Albedo: Albedo = guid; break;
                case TextureType.Normal: Normal = guid; break;
                case TextureType.Metallic: Metallic = guid; break;
                case TextureType.Roughness: Roughness = guid; break;
                case TextureType.MetallicRoughness: MetallicRoughness = guid; break;
                case TextureType.AO: AO = guid; break;
                case TextureType.Emissive: Emissive = guid; break;
                case TextureType.Opacity: Opacity = guid; break;
            }
        }

        public bool HasTextureType(TextureType type)
        {
            return type switch
            {
                TextureType.Albedo => Albedo.HasValue,
                TextureType.Normal => Normal.HasValue,
                TextureType.Metallic => Metallic.HasValue,
                TextureType.Roughness => Roughness.HasValue,
                TextureType.MetallicRoughness => MetallicRoughness.HasValue,
                TextureType.AO => AO.HasValue,
                TextureType.Emissive => Emissive.HasValue,
                TextureType.Opacity => Opacity.HasValue,
                _ => false
            };
        }
    }
}

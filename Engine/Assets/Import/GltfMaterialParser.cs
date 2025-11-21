using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Engine.Assets.Import
{
    /// <summary>
    /// Direct GLTF JSON parser for robust transparency detection.
    /// Inspired by Blender's approach - read alphaMode and baseColorFactor directly from GLTF JSON.
    /// Assimp sometimes fails to correctly map GLTF 2.0 transparency properties.
    /// </summary>
    public static class GltfMaterialParser
    {
        public class GltfTransparencyInfo
        {
            public string MaterialName { get; set; } = "";
            public string AlphaMode { get; set; } = "OPAQUE"; // OPAQUE, MASK, BLEND
            public float AlphaCutoff { get; set; } = 0.5f;
            public float[] BaseColorFactor { get; set; } = new float[] { 1f, 1f, 1f, 1f }; // RGBA
            public float BaseColorAlpha => BaseColorFactor.Length >= 4 ? BaseColorFactor[3] : 1.0f;
            public bool IsTransparent => AlphaMode == "BLEND" || (AlphaMode == "MASK" && AlphaCutoff > 0);
            
            // GLTF 2.0 PBR texture indices (reference into textures/images arrays)
            public int? BaseColorTextureIndex { get; set; }
            public int? MetallicRoughnessTextureIndex { get; set; }
            public int? NormalTextureIndex { get; set; }
            public int? OcclusionTextureIndex { get; set; }
            public int? EmissiveTextureIndex { get; set; }
        }

        /// <summary>
        /// Parse GLTF JSON file to extract material transparency information.
        /// Returns dictionary mapping material name to transparency info.
        /// </summary>
        public static Dictionary<string, GltfTransparencyInfo> ParseGltfMaterials(string gltfPath)
        {
            var result = new Dictionary<string, GltfTransparencyInfo>(StringComparer.OrdinalIgnoreCase);

            try
            {
                if (!File.Exists(gltfPath) || !gltfPath.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase))
                {
                    return result; // Only works with .gltf JSON files, not .glb binary
                }

                string json = File.ReadAllText(gltfPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Check if materials array exists
                if (!root.TryGetProperty("materials", out var materialsArray))
                {
                    return result;
                }

                // Parse each material
                foreach (var mat in materialsArray.EnumerateArray())
                {
                    var info = new GltfTransparencyInfo();

                    // Get material name
                    if (mat.TryGetProperty("name", out var nameElement))
                    {
                        info.MaterialName = nameElement.GetString() ?? "";
                    }

                    // Get alphaMode (OPAQUE, MASK, BLEND)
                    if (mat.TryGetProperty("alphaMode", out var alphaModeElement))
                    {
                        info.AlphaMode = alphaModeElement.GetString()?.ToUpperInvariant() ?? "OPAQUE";
                    }

                    // Get alphaCutoff (for MASK mode)
                    if (mat.TryGetProperty("alphaCutoff", out var alphaCutoffElement))
                    {
                        info.AlphaCutoff = alphaCutoffElement.GetSingle();
                    }

                    // Get baseColorFactor (RGBA) from pbrMetallicRoughness
                    if (mat.TryGetProperty("pbrMetallicRoughness", out var pbr))
                    {
                        if (pbr.TryGetProperty("baseColorFactor", out var baseColorArray))
                        {
                            var colorComponents = new List<float>();
                            foreach (var component in baseColorArray.EnumerateArray())
                            {
                                colorComponents.Add(component.GetSingle());
                            }
                            
                            // Ensure we have 4 components (RGBA)
                            while (colorComponents.Count < 4)
                            {
                                colorComponents.Add(colorComponents.Count == 3 ? 1.0f : 0.0f); // Default alpha=1, RGB=0
                            }
                            
                            info.BaseColorFactor = colorComponents.ToArray();
                            
                            Engine.Utils.DebugLogger.Log(
                                $"[GltfMaterialParser] Material '{info.MaterialName}' baseColorFactor: " +
                                $"R={info.BaseColorFactor[0]:F3}, G={info.BaseColorFactor[1]:F3}, " +
                                $"B={info.BaseColorFactor[2]:F3}, A={info.BaseColorFactor[3]:F3}");
                        }
                        
                        // Parse baseColorTexture
                        if (pbr.TryGetProperty("baseColorTexture", out var baseColorTex))
                        {
                            if (baseColorTex.TryGetProperty("index", out var baseColorIndex))
                            {
                                info.BaseColorTextureIndex = baseColorIndex.GetInt32();
                                Engine.Utils.DebugLogger.Log($"[GltfMaterialParser] Material '{info.MaterialName}' baseColorTexture: index={info.BaseColorTextureIndex}");
                            }
                        }
                        
                        // Parse metallicRoughnessTexture (CRITICAL for GLTF 2.0 PBR!)
                        if (pbr.TryGetProperty("metallicRoughnessTexture", out var mrTex))
                        {
                            if (mrTex.TryGetProperty("index", out var mrIndex))
                            {
                                info.MetallicRoughnessTextureIndex = mrIndex.GetInt32();
                                Engine.Utils.DebugLogger.Log($"[GltfMaterialParser] Material '{info.MaterialName}' metallicRoughnessTexture: index={info.MetallicRoughnessTextureIndex}");
                            }
                        }
                    }
                    
                    // Parse normalTexture
                    if (mat.TryGetProperty("normalTexture", out var normalTex))
                    {
                        if (normalTex.TryGetProperty("index", out var normalIndex))
                        {
                            info.NormalTextureIndex = normalIndex.GetInt32();
                            Engine.Utils.DebugLogger.Log($"[GltfMaterialParser] Material '{info.MaterialName}' normalTexture: index={info.NormalTextureIndex}");
                        }
                    }
                    
                    // Parse occlusionTexture
                    if (mat.TryGetProperty("occlusionTexture", out var occlusionTex))
                    {
                        if (occlusionTex.TryGetProperty("index", out var occlusionIndex))
                        {
                            info.OcclusionTextureIndex = occlusionIndex.GetInt32();
                            Engine.Utils.DebugLogger.Log($"[GltfMaterialParser] Material '{info.MaterialName}' occlusionTexture: index={info.OcclusionTextureIndex}");
                        }
                    }
                    
                    // Parse emissiveTexture
                    if (mat.TryGetProperty("emissiveTexture", out var emissiveTex))
                    {
                        if (emissiveTex.TryGetProperty("index", out var emissiveIndex))
                        {
                            info.EmissiveTextureIndex = emissiveIndex.GetInt32();
                            Engine.Utils.DebugLogger.Log($"[GltfMaterialParser] Material '{info.MaterialName}' emissiveTexture: index={info.EmissiveTextureIndex}");
                        }
                    }

                    // Add to dictionary
                    if (!string.IsNullOrEmpty(info.MaterialName))
                    {
                        result[info.MaterialName] = info;
                        
                        // Log transparency detection
                        if (info.IsTransparent || info.BaseColorAlpha < 0.99f)
                        {
                            Engine.Utils.DebugLogger.Log(
                                $"[GltfMaterialParser] Material '{info.MaterialName}': " +
                                $"alphaMode={info.AlphaMode}, baseColorAlpha={info.BaseColorAlpha:F3}, " +
                                $"isTransparent={info.IsTransparent}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Engine.Utils.DebugLogger.Log($"[GltfMaterialParser] Error parsing GLTF: {ex.Message}");
            }

            return result;
        }
        
        /// <summary>
        /// Parse GLTF textures and images arrays to build texture index -> file path mapping.
        /// Returns dictionary mapping texture index to image file path.
        /// </summary>
        public static Dictionary<int, string> ParseGltfTextureImages(string gltfPath)
        {
            var result = new Dictionary<int, string>();
            
            try
            {
                if (!File.Exists(gltfPath) || !gltfPath.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase))
                {
                    return result;
                }
                
                string json = File.ReadAllText(gltfPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                
                // Parse images array first to get URI/file paths
                var imageUris = new List<string>();
                if (root.TryGetProperty("images", out var imagesArray))
                {
                    foreach (var image in imagesArray.EnumerateArray())
                    {
                        string uri = "";
                        if (image.TryGetProperty("uri", out var uriElement))
                        {
                            uri = uriElement.GetString() ?? "";
                        }
                        imageUris.Add(uri);
                    }
                }
                
                // Parse textures array to map texture index -> image index
                if (root.TryGetProperty("textures", out var texturesArray))
                {
                    int textureIndex = 0;
                    foreach (var texture in texturesArray.EnumerateArray())
                    {
                        if (texture.TryGetProperty("source", out var sourceElement))
                        {
                            int imageIndex = sourceElement.GetInt32();
                            if (imageIndex >= 0 && imageIndex < imageUris.Count)
                            {
                                result[textureIndex] = imageUris[imageIndex];
                                Engine.Utils.DebugLogger.Log($"[GltfMaterialParser] Texture[{textureIndex}] -> Image[{imageIndex}] = '{imageUris[imageIndex]}'");
                            }
                        }
                        textureIndex++;
                    }
                }
            }
            catch (Exception ex)
            {
                Engine.Utils.DebugLogger.Log($"[GltfMaterialParser] Error parsing GLTF textures/images: {ex.Message}");
            }
            
            return result;
        }
    }
}

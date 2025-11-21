using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Engine.Assets.Import
{
    /// <summary>
    /// FBX material parser for robust transparency detection.
    /// FBX stores transparency in TransparencyFactor, OpacityFactor properties.
    /// Blender approach: Read FBX nodes directly to extract material properties.
    /// 
    /// NOTE: FBX is a proprietary binary format. We use text-based detection when available,
    /// or rely on Assimp's parsing with enhanced heuristics.
    /// </summary>
    public static class FbxMaterialParser
    {
        public class FbxTransparencyInfo
        {
            public string MaterialName { get; set; } = "";
            public float Opacity { get; set; } = 1.0f;
            public float TransparencyFactor { get; set; } = 0.0f; // 0 = opaque, 1 = transparent
            public bool HasTransparency { get; set; } = false;
            public bool IsTransparent => HasTransparency || Opacity < 0.99f || TransparencyFactor > 0.01f;
        }

        /// <summary>
        /// Parse FBX file to extract material transparency information.
        /// FBX can be ASCII or binary - we handle both cases.
        /// Returns dictionary mapping material name to transparency info.
        /// </summary>
        public static Dictionary<string, FbxTransparencyInfo> ParseFbxMaterials(string fbxPath)
        {
            var result = new Dictionary<string, FbxTransparencyInfo>(StringComparer.OrdinalIgnoreCase);

            try
            {
                if (!File.Exists(fbxPath) || !fbxPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                {
                    return result;
                }

                // Check if FBX is ASCII format (starts with "; FBX")
                using var fs = new FileStream(fbxPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var header = new byte[10];
                fs.Read(header, 0, 10);
                var headerText = Encoding.ASCII.GetString(header);

                if (headerText.StartsWith("; FBX"))
                {
                    // ASCII FBX - we can parse it
                    Engine.Utils.DebugLogger.Log("[FbxMaterialParser] Detected ASCII FBX format");
                    result = ParseAsciiFbx(fbxPath);
                }
                else
                {
                    // Binary FBX - too complex to parse manually, rely on Assimp with enhanced heuristics
                    Engine.Utils.DebugLogger.Log("[FbxMaterialParser] Detected binary FBX format - using Assimp fallback");
                    // For binary FBX, we'll use naming conventions and Assimp data
                }
            }
            catch (Exception ex)
            {
                Engine.Utils.DebugLogger.Log($"[FbxMaterialParser] Error parsing FBX: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Parse ASCII FBX format to extract material properties.
        /// ASCII FBX uses a simple text format with material nodes.
        /// </summary>
        private static Dictionary<string, FbxTransparencyInfo> ParseAsciiFbx(string fbxPath)
        {
            var result = new Dictionary<string, FbxTransparencyInfo>(StringComparer.OrdinalIgnoreCase);

            try
            {
                string[] lines = File.ReadAllLines(fbxPath);
                string? currentMaterial = null;
                FbxTransparencyInfo? currentInfo = null;

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();

                    // Look for Material definitions
                    if (line.Contains("Material:") && line.Contains("\""))
                    {
                        // Extract material name from: Material: "Material::MaterialName", ""
                        int startQuote = line.IndexOf('"') + 1;
                        int endQuote = line.IndexOf('"', startQuote);
                        if (startQuote > 0 && endQuote > startQuote)
                        {
                            string fullName = line.Substring(startQuote, endQuote - startQuote);
                            // Remove "Material::" prefix if present
                            currentMaterial = fullName.Replace("Material::", "");
                            currentInfo = new FbxTransparencyInfo { MaterialName = currentMaterial };
                            result[currentMaterial] = currentInfo;
                            Engine.Utils.DebugLogger.Log($"[FbxMaterialParser] Found material: {currentMaterial}");
                        }
                    }

                    if (currentInfo != null)
                    {
                        // Look for transparency properties
                        if (line.Contains("P: \"TransparencyFactor\""))
                        {
                            // Extract value: P: "TransparencyFactor", "Number", "", "A",0.5
                            var parts = line.Split(',');
                            if (parts.Length >= 5)
                            {
                                if (float.TryParse(parts[4].Trim(), System.Globalization.NumberStyles.Float, 
                                    System.Globalization.CultureInfo.InvariantCulture, out float value))
                                {
                                    currentInfo.TransparencyFactor = value;
                                    currentInfo.HasTransparency = true;
                                    Engine.Utils.DebugLogger.Log($"[FbxMaterialParser] {currentMaterial} - TransparencyFactor: {value}");
                                }
                            }
                        }
                        else if (line.Contains("P: \"Opacity\""))
                        {
                            var parts = line.Split(',');
                            if (parts.Length >= 5)
                            {
                                if (float.TryParse(parts[4].Trim(), System.Globalization.NumberStyles.Float, 
                                    System.Globalization.CultureInfo.InvariantCulture, out float value))
                                {
                                    currentInfo.Opacity = value;
                                    currentInfo.HasTransparency = value < 0.99f;
                                    Engine.Utils.DebugLogger.Log($"[FbxMaterialParser] {currentMaterial} - Opacity: {value}");
                                }
                            }
                        }
                        else if (line.Contains("P: \"TransparentColor\""))
                        {
                            // TransparentColor indicates this material has transparency
                            currentInfo.HasTransparency = true;
                            Engine.Utils.DebugLogger.Log($"[FbxMaterialParser] {currentMaterial} - Has TransparentColor");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Engine.Utils.DebugLogger.Log($"[FbxMaterialParser] Error parsing ASCII FBX: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Apply naming convention heuristics for transparent materials.
        /// Blender often uses these conventions for glass, water, etc.
        /// </summary>
        public static bool IsTransparentByName(string materialName)
        {
            if (string.IsNullOrEmpty(materialName))
                return false;

            string lower = materialName.ToLowerInvariant();
            
            // Common transparent material names
            return lower.Contains("glass") ||
                   lower.Contains("window") ||
                   lower.Contains("transparent") ||
                   lower.Contains("alpha") ||
                   lower.Contains("opacity") ||
                   lower.Contains("water") ||
                   lower.Contains("ice") ||
                   lower.Contains("crystal");
        }
    }
}

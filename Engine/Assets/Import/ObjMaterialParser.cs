using System;
using System.Collections.Generic;
using System.IO;

namespace Engine.Assets.Import
{
    /// <summary>
    /// OBJ/MTL material parser for robust transparency detection.
    /// OBJ uses separate .mtl files with 'd' (dissolve) and 'Tr' (transparency) parameters.
    /// Blender approach: Parse MTL file directly to extract accurate opacity values.
    /// 
    /// MTL format transparency:
    /// - d 0.5        # dissolve (0=transparent, 1=opaque)
    /// - Tr 0.5       # transparency (0=opaque, 1=transparent) - inverse of 'd'
    /// - map_d file   # opacity texture map
    /// </summary>
    public static class ObjMaterialParser
    {
        public class ObjTransparencyInfo
        {
            public string MaterialName { get; set; } = "";
            public float Dissolve { get; set; } = 1.0f;        // d value (1 = opaque, 0 = transparent)
            public float Transparency { get; set; } = 0.0f;     // Tr value (0 = opaque, 1 = transparent)
            public string? OpacityMapPath { get; set; }         // map_d texture
            public bool HasOpacityMap => !string.IsNullOrEmpty(OpacityMapPath);
            
            // Calculate final opacity (prefer 'd' over 'Tr' as it's more standard)
            public float Opacity => Dissolve < 1.0f ? Dissolve : (1.0f - Transparency);
            public bool IsTransparent => Opacity < 0.99f || HasOpacityMap;
        }

        /// <summary>
        /// Parse OBJ file to find MTL file reference, then parse MTL for transparency.
        /// Returns dictionary mapping material name to transparency info.
        /// </summary>
        public static Dictionary<string, ObjTransparencyInfo> ParseObjMaterials(string objPath)
        {
            var result = new Dictionary<string, ObjTransparencyInfo>(StringComparer.OrdinalIgnoreCase);

            try
            {
                if (!File.Exists(objPath) || !objPath.EndsWith(".obj", StringComparison.OrdinalIgnoreCase))
                {
                    return result;
                }

                // Find MTL file reference in OBJ
                string? mtlFileName = null;
                string[] objLines = File.ReadAllLines(objPath);
                
                foreach (string line in objLines)
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("mtllib "))
                    {
                        mtlFileName = trimmed.Substring(7).Trim();
                        break;
                    }
                }

                if (string.IsNullOrEmpty(mtlFileName))
                {
                    Engine.Utils.DebugLogger.Log("[ObjMaterialParser] No MTL file reference found in OBJ");
                    return result;
                }

                // Construct full path to MTL file
                string? objDirectory = Path.GetDirectoryName(objPath);
                if (objDirectory == null)
                {
                    return result;
                }

                string mtlPath = Path.Combine(objDirectory, mtlFileName);
                if (!File.Exists(mtlPath))
                {
                    Engine.Utils.DebugLogger.Log($"[ObjMaterialParser] MTL file not found: {mtlPath}");
                    return result;
                }

                Engine.Utils.DebugLogger.Log($"[ObjMaterialParser] Parsing MTL file: {Path.GetFileName(mtlPath)}");
                result = ParseMtlFile(mtlPath, objDirectory);
            }
            catch (Exception ex)
            {
                Engine.Utils.DebugLogger.Log($"[ObjMaterialParser] Error parsing OBJ: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Parse MTL file to extract material transparency properties.
        /// MTL format is text-based with simple key-value pairs.
        /// </summary>
        private static Dictionary<string, ObjTransparencyInfo> ParseMtlFile(string mtlPath, string baseDirectory)
        {
            var result = new Dictionary<string, ObjTransparencyInfo>(StringComparer.OrdinalIgnoreCase);

            try
            {
                string[] lines = File.ReadAllLines(mtlPath);
                ObjTransparencyInfo? currentMaterial = null;

                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    
                    // Skip comments and empty lines
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                        continue;

                    // New material definition
                    if (trimmed.StartsWith("newmtl "))
                    {
                        string materialName = trimmed.Substring(7).Trim();
                        currentMaterial = new ObjTransparencyInfo { MaterialName = materialName };
                        result[materialName] = currentMaterial;
                        Engine.Utils.DebugLogger.Log($"[ObjMaterialParser] Found material: {materialName}");
                        continue;
                    }

                    if (currentMaterial == null)
                        continue;

                    // Parse transparency properties
                    string[] parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2)
                        continue;

                    string key = parts[0].ToLowerInvariant();
                    string value = parts[1];

                    switch (key)
                    {
                        case "d": // Dissolve (1 = opaque, 0 = transparent)
                            if (float.TryParse(value, System.Globalization.NumberStyles.Float, 
                                System.Globalization.CultureInfo.InvariantCulture, out float dissolve))
                            {
                                currentMaterial.Dissolve = dissolve;
                                Engine.Utils.DebugLogger.Log(
                                    $"[ObjMaterialParser] {currentMaterial.MaterialName} - Dissolve (d): {dissolve}");
                            }
                            break;

                        case "tr": // Transparency (0 = opaque, 1 = transparent) - inverse of 'd'
                            if (float.TryParse(value, System.Globalization.NumberStyles.Float, 
                                System.Globalization.CultureInfo.InvariantCulture, out float transparency))
                            {
                                currentMaterial.Transparency = transparency;
                                Engine.Utils.DebugLogger.Log(
                                    $"[ObjMaterialParser] {currentMaterial.MaterialName} - Transparency (Tr): {transparency}");
                            }
                            break;

                        case "map_d": // Opacity texture map
                            currentMaterial.OpacityMapPath = value;
                            Engine.Utils.DebugLogger.Log(
                                $"[ObjMaterialParser] {currentMaterial.MaterialName} - Opacity map: {value}");
                            break;
                    }
                }

                // Log final transparency results
                foreach (var mat in result.Values)
                {
                    if (mat.IsTransparent)
                    {
                        Engine.Utils.DebugLogger.Log(
                            $"[ObjMaterialParser] Material '{mat.MaterialName}' is TRANSPARENT " +
                            $"(opacity: {mat.Opacity:F3}, hasMap: {mat.HasOpacityMap})");
                    }
                }
            }
            catch (Exception ex)
            {
                Engine.Utils.DebugLogger.Log($"[ObjMaterialParser] Error parsing MTL file: {ex.Message}");
            }

            return result;
        }
    }
}

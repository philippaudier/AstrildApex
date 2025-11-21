using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace Engine.Assets.Import
{
    /// <summary>
    /// DAE/Collada material parser for robust transparency detection.
    /// Collada uses XML format with transparency specified in material effects.
    /// Blender approach: Parse Collada XML to extract accurate transparency values.
    /// 
    /// Collada transparency:
    /// - <transparency> float value (0=transparent, 1=opaque by default, but can vary)
    /// - <transparent> color/texture reference
    /// - <blend_mode> BLEND indicates alpha blending
    /// </summary>
    public static class DaeMaterialParser
    {
        public class DaeTransparencyInfo
        {
            public string MaterialName { get; set; } = "";
            public string EffectId { get; set; } = "";
            public float Transparency { get; set; } = 1.0f;     // Default opaque
            public bool HasTransparentTag { get; set; } = false;
            public string? BlendMode { get; set; }              // BLEND, ADD, etc.
            public bool HasBlendMode => !string.IsNullOrEmpty(BlendMode) && BlendMode != "OPAQUE";
            
            // Collada transparency can be interpreted differently by exporters
            // Some use 0=transparent/1=opaque, others use 0=opaque/1=transparent
            // We check multiple indicators
            public bool IsTransparent => HasTransparentTag || HasBlendMode || 
                                        (Transparency > 0.0f && Transparency < 0.99f);
        }

        /// <summary>
        /// Parse Collada DAE file to extract material transparency information.
        /// Returns dictionary mapping material name to transparency info.
        /// </summary>
        public static Dictionary<string, DaeTransparencyInfo> ParseDaeMaterials(string daePath)
        {
            var result = new Dictionary<string, DaeTransparencyInfo>(StringComparer.OrdinalIgnoreCase);

            try
            {
                if (!File.Exists(daePath) || !daePath.EndsWith(".dae", StringComparison.OrdinalIgnoreCase))
                {
                    return result;
                }

                Engine.Utils.DebugLogger.Log($"[DaeMaterialParser] Parsing Collada file: {Path.GetFileName(daePath)}");

                var doc = new XmlDocument();
                doc.Load(daePath);

                // Collada structure:
                // <COLLADA>
                //   <library_materials>
                //     <material id="material_id" name="MaterialName">
                //       <instance_effect url="#effect_id"/>
                //     </material>
                //   </library_materials>
                //   <library_effects>
                //     <effect id="effect_id">
                //       <profile_COMMON>
                //         <technique>
                //           <phong> or <lambert> or <blinn>
                //             <transparent>...</transparent>
                //             <transparency>0.5</transparency>
                //           </phong>
                //         </technique>
                //       </profile_COMMON>
                //     </effect>
                //   </library_effects>
                // </COLLADA>

                var nsmgr = new XmlNamespaceManager(doc.NameTable);
                nsmgr.AddNamespace("dae", "http://www.collada.org/2005/11/COLLADASchema");

                // First pass: map materials to effects
                var materialToEffect = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                XmlNodeList? materialNodes = doc.SelectNodes("//dae:library_materials/dae:material", nsmgr);
                if (materialNodes == null || materialNodes.Count == 0)
                {
                    // Try without namespace
                    materialNodes = doc.SelectNodes("//library_materials/material");
                }

                if (materialNodes != null)
                {
                    foreach (XmlNode matNode in materialNodes)
                    {
                        string? matId = matNode.Attributes?["id"]?.Value;
                        string? matName = matNode.Attributes?["name"]?.Value ?? matId;
                        
                        XmlNode? instanceEffect = matNode.SelectSingleNode("dae:instance_effect", nsmgr) 
                                                 ?? matNode.SelectSingleNode("instance_effect");
                        
                        string? effectUrl = instanceEffect?.Attributes?["url"]?.Value;
                        
                        if (!string.IsNullOrEmpty(matName) && !string.IsNullOrEmpty(effectUrl))
                        {
                            string effectId = effectUrl.TrimStart('#');
                            materialToEffect[matName] = effectId;
                            
                            var info = new DaeTransparencyInfo 
                            { 
                                MaterialName = matName,
                                EffectId = effectId 
                            };
                            result[matName] = info;
                            
                            Engine.Utils.DebugLogger.Log($"[DaeMaterialParser] Found material: {matName} -> effect: {effectId}");
                        }
                    }
                }

                // Second pass: parse effects for transparency
                XmlNodeList? effectNodes = doc.SelectNodes("//dae:library_effects/dae:effect", nsmgr);
                if (effectNodes == null || effectNodes.Count == 0)
                {
                    effectNodes = doc.SelectNodes("//library_effects/effect");
                }

                if (effectNodes != null)
                {
                    foreach (XmlNode effectNode in effectNodes)
                    {
                        string? effectId = effectNode.Attributes?["id"]?.Value;
                        if (string.IsNullOrEmpty(effectId))
                            continue;

                        // Find material using this effect
                        DaeTransparencyInfo? info = null;
                        foreach (var kvp in result)
                        {
                            if (kvp.Value.EffectId == effectId)
                            {
                                info = kvp.Value;
                                break;
                            }
                        }

                        if (info == null)
                            continue;

                        // Parse transparency from technique (phong/lambert/blinn)
                        var techniques = new[] { "phong", "lambert", "blinn", "constant" };
                        foreach (string technique in techniques)
                        {
                            XmlNode? shaderNode = effectNode.SelectSingleNode($".//dae:{technique}", nsmgr)
                                                ?? effectNode.SelectSingleNode($".//{technique}");
                            
                            if (shaderNode == null)
                                continue;

                            // <transparent> tag indicates transparency is used
                            XmlNode? transparentNode = shaderNode.SelectSingleNode("dae:transparent", nsmgr)
                                                     ?? shaderNode.SelectSingleNode("transparent");
                            if (transparentNode != null)
                            {
                                info.HasTransparentTag = true;
                                Engine.Utils.DebugLogger.Log($"[DaeMaterialParser] {info.MaterialName} has <transparent> tag");
                            }

                            // <transparency> value (interpretation varies: some use 0=transparent, others 1=transparent)
                            XmlNode? transparencyNode = shaderNode.SelectSingleNode("dae:transparency", nsmgr)
                                                      ?? shaderNode.SelectSingleNode("transparency");
                            if (transparencyNode != null)
                            {
                                if (float.TryParse(transparencyNode.InnerText, System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture, out float transValue))
                                {
                                    info.Transparency = transValue;
                                    Engine.Utils.DebugLogger.Log($"[DaeMaterialParser] {info.MaterialName} - transparency value: {transValue}");
                                }
                            }

                            break; // Found shader type
                        }

                        // Check for blend mode in extra tags
                        XmlNode? extraNode = effectNode.SelectSingleNode(".//dae:blend_mode", nsmgr)
                                           ?? effectNode.SelectSingleNode(".//blend_mode");
                        if (extraNode != null)
                        {
                            info.BlendMode = extraNode.InnerText.ToUpperInvariant();
                            Engine.Utils.DebugLogger.Log($"[DaeMaterialParser] {info.MaterialName} - blend mode: {info.BlendMode}");
                        }
                    }
                }

                // Log final transparency results
                foreach (var mat in result.Values)
                {
                    if (mat.IsTransparent)
                    {
                        Engine.Utils.DebugLogger.Log(
                            $"[DaeMaterialParser] Material '{mat.MaterialName}' is TRANSPARENT " +
                            $"(transparency: {mat.Transparency:F3}, hasTransparentTag: {mat.HasTransparentTag}, blendMode: {mat.BlendMode ?? "none"})");
                    }
                }
            }
            catch (Exception ex)
            {
                Engine.Utils.DebugLogger.Log($"[DaeMaterialParser] Error parsing DAE: {ex.Message}");
            }

            return result;
        }
    }
}

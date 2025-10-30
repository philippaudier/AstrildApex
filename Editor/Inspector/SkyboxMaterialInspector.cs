using System;
using ImGuiNET;
using OpenTK.Mathematics;
using Engine.Assets;
using Editor.Icons;
using Numerics = System.Numerics;

namespace Editor.Inspector
{
    /// <summary>
    /// Unity-like inspector for Skybox Materials
    /// </summary>
    public static class SkyboxMaterialInspector
    {
        public static void Draw(Guid skyboxMaterialGuid)
        {
            if (!Engine.Assets.AssetDatabase.TryGet(skyboxMaterialGuid, out var record))
            {
                ImGui.TextColored(new Numerics.Vector4(1, 0.5f, 0.5f, 1), "Skybox Material not found");
                return;
            }

            if (!string.Equals(record.Type, "SkyboxMaterial", StringComparison.OrdinalIgnoreCase))
            {
                ImGui.TextColored(new Numerics.Vector4(1, 0.5f, 0.5f, 1), "Invalid skybox material type");
                return;
            }

            // Load the skybox material asset
            SkyboxMaterialAsset skyboxMat;
            try
            {
                skyboxMat = SkyboxMaterialAsset.Load(record.Path);
                // Ensure GUID consistency
                if (skyboxMat.Guid == Guid.Empty || skyboxMat.Guid != record.Guid)
                {
                    skyboxMat.Guid = record.Guid;
                    SkyboxMaterialAsset.Save(record.Path, skyboxMat);
                }
            }
            catch (Exception e)
            {
                ImGui.TextColored(new Numerics.Vector4(1, 0.5f, 0.5f, 1), $"Failed to load skybox material: {e.Message}");
                return;
            }

            ImGui.PushItemWidth(150f);
            bool dirty = false;
            
            // Skybox Type Selector (Unity-style) — mark dirty when changed
            dirty |= DrawSkyboxTypeSelector(skyboxMat);
            ImGui.Separator();
            ImGui.Spacing();
            
            // Type-specific properties
        switch (skyboxMat.Type)
            {
                case SkyboxType.Procedural:
            dirty |= DrawProceduralProperties(skyboxMat);
                    break;
                case SkyboxType.Cubemap:
            dirty |= DrawCubemapProperties(skyboxMat);
                    break;
                case SkyboxType.SixSided:
            dirty |= DrawSixSidedProperties(skyboxMat);
                    break;
                case SkyboxType.Panoramic:
            dirty |= DrawPanoramicProperties(skyboxMat);
                    break;
            }
            
            ImGui.PopItemWidth();
            
            // Save changes immediately when dirty so next frame reload reflects edits
            if (dirty)
            {
                SkyboxMaterialAsset.Save(record.Path, skyboxMat);
            }
        }
        
        private static bool DrawSkyboxTypeSelector(SkyboxMaterialAsset skyboxMat)
        {
            bool changed = false;
            ImGui.Text("Skybox");
            ImGui.SameLine();
            
            var skyboxTypeNames = new[] { "Procedural", "Cubemap", "6 Sided", "Panoramic" };
            var currentType = (int)skyboxMat.Type;
            
            ImGui.SetNextItemWidth(-1);
            if (ImGui.Combo("##SkyboxType", ref currentType, skyboxTypeNames, skyboxTypeNames.Length))
            {
                skyboxMat.Type = (SkyboxType)currentType;
                changed = true;
            }
            return changed;
        }
        
        private static bool DrawProceduralProperties(SkyboxMaterialAsset skyboxMat)
        {
            bool changed = false;
            ImGui.TextColored(new Numerics.Vector4(0.7f, 0.7f, 0.7f, 1), "Procedural Skybox");
            ImGui.Spacing();
            
            // Sky Tint
            var skyTint = new Numerics.Vector4(skyboxMat.SkyTint[0], skyboxMat.SkyTint[1], skyboxMat.SkyTint[2], skyboxMat.SkyTint[3]);
            if (ImGui.ColorEdit4("Sky Tint", ref skyTint))
            {
                skyboxMat.SkyTint = new float[] { skyTint.X, skyTint.Y, skyTint.Z, skyTint.W };
                changed = true;
            }
            
            // Ground Color
            var groundColor = new Numerics.Vector4(skyboxMat.GroundColor[0], skyboxMat.GroundColor[1], skyboxMat.GroundColor[2], skyboxMat.GroundColor[3]);
            if (ImGui.ColorEdit4("Ground", ref groundColor))
            {
                skyboxMat.GroundColor = new float[] { groundColor.X, groundColor.Y, groundColor.Z, groundColor.W };
                changed = true;
            }
            
            // Exposure
            var exposure = skyboxMat.Exposure;
            if (ImGui.DragFloat("Exposure", ref exposure, 0.01f, 0.0f, 8.0f))
            {
                skyboxMat.Exposure = exposure;
                changed = true;
            }
            
            // Atmosphere Thickness
            var atmosphereThickness = skyboxMat.AtmosphereThickness;
            if (ImGui.SliderFloat("Atmosphere Thickness", ref atmosphereThickness, 0.0f, 5.0f))
            {
                skyboxMat.AtmosphereThickness = atmosphereThickness;
                changed = true;
            }
            
            ImGui.Spacing();
            ImGui.TextColored(new Numerics.Vector4(0.7f, 0.7f, 0.7f, 1), "Sun");
            
            // Sun Tint
            var sunTint = new Numerics.Vector4(skyboxMat.SunTint[0], skyboxMat.SunTint[1], skyboxMat.SunTint[2], skyboxMat.SunTint[3]);
            if (ImGui.ColorEdit4("Sun Tint", ref sunTint))
            {
                skyboxMat.SunTint = new float[] { sunTint.X, sunTint.Y, sunTint.Z, sunTint.W };
                changed = true;
            }
            
            // Sun Size
            var sunSize = skyboxMat.SunSize;
            if (ImGui.SliderFloat("Sun Size", ref sunSize, 0.0f, 1.0f))
            {
                skyboxMat.SunSize = sunSize;
                changed = true;
            }
            
            // Sun Size Convergence
            var sunSizeConvergence = skyboxMat.SunSizeConvergence;
            if (ImGui.SliderFloat("Sun Size Convergence", ref sunSizeConvergence, 1.0f, 10.0f))
            {
                skyboxMat.SunSizeConvergence = sunSizeConvergence;
                changed = true;
            }

            return changed;
        }
        
        private static bool DrawCubemapProperties(SkyboxMaterialAsset skyboxMat)
        {
            bool changed = false;
            ImGui.TextColored(new Numerics.Vector4(0.7f, 0.7f, 0.7f, 1), "Cubemap Skybox");
            ImGui.Spacing();
            
            // Cubemap Texture
            changed |= DrawTextureField("Cubemap (HDR)", skyboxMat.CubemapTexture, 
                guid => { skyboxMat.CubemapTexture = guid; });
            
            // Tint
            var tint = new Numerics.Vector4(skyboxMat.CubemapTint[0], skyboxMat.CubemapTint[1], skyboxMat.CubemapTint[2], skyboxMat.CubemapTint[3]);
            if (ImGui.ColorEdit4("Tint", ref tint))
            {
                skyboxMat.CubemapTint = new float[] { tint.X, tint.Y, tint.Z, tint.W };
                changed = true;
            }
            
            // Exposure
            var exposure = skyboxMat.CubemapExposure;
            if (ImGui.DragFloat("Exposure", ref exposure, 0.01f, 0.0f, 8.0f))
            {
                skyboxMat.CubemapExposure = exposure;
                changed = true;
            }
            
            // Rotation
            var rotation = skyboxMat.CubemapRotation;
            if (ImGui.SliderFloat("Rotation", ref rotation, 0.0f, 360.0f, "%.1f°"))
            {
                skyboxMat.CubemapRotation = rotation;
                changed = true;
            }

            return changed;
        }
        
        private static bool DrawSixSidedProperties(SkyboxMaterialAsset skyboxMat)
        {
            bool changed = false;
            ImGui.TextColored(new Numerics.Vector4(0.7f, 0.7f, 0.7f, 1), "6 Sided Skybox");
            ImGui.Spacing();
            
            // Six texture slots
            changed |= DrawTextureField("Front (+Z)", skyboxMat.FrontTexture, guid => { skyboxMat.FrontTexture = guid; });
            changed |= DrawTextureField("Back (-Z)", skyboxMat.BackTexture, guid => { skyboxMat.BackTexture = guid; });
            changed |= DrawTextureField("Left (-X)", skyboxMat.LeftTexture, guid => { skyboxMat.LeftTexture = guid; });
            changed |= DrawTextureField("Right (+X)", skyboxMat.RightTexture, guid => { skyboxMat.RightTexture = guid; });
            changed |= DrawTextureField("Up (+Y)", skyboxMat.UpTexture, guid => { skyboxMat.UpTexture = guid; });
            changed |= DrawTextureField("Down (-Y)", skyboxMat.DownTexture, guid => { skyboxMat.DownTexture = guid; });
            
            ImGui.Spacing();
            
            // Tint
            var tint = new Numerics.Vector4(skyboxMat.SixSidedTint[0], skyboxMat.SixSidedTint[1], skyboxMat.SixSidedTint[2], skyboxMat.SixSidedTint[3]);
            if (ImGui.ColorEdit4("Tint", ref tint))
            {
                skyboxMat.SixSidedTint = new float[] { tint.X, tint.Y, tint.Z, tint.W };
                changed = true;
            }
            
            // Exposure
            var exposure = skyboxMat.SixSidedExposure;
            if (ImGui.DragFloat("Exposure", ref exposure, 0.01f, 0.0f, 8.0f))
            {
                skyboxMat.SixSidedExposure = exposure;
                changed = true;
            }

            return changed;
        }
        
        private static bool DrawPanoramicProperties(SkyboxMaterialAsset skyboxMat)
        {
            bool changed = false;
            ImGui.TextColored(new Numerics.Vector4(0.7f, 0.7f, 0.7f, 1), "Panoramic Skybox");
            ImGui.Spacing();
            
            // Panoramic Texture
            changed |= DrawTextureField("Panoramic Texture (HDR)", skyboxMat.PanoramicTexture, 
                guid => { skyboxMat.PanoramicTexture = guid; });
            
            // Tint
            var tint = new Numerics.Vector4(skyboxMat.PanoramicTint[0], skyboxMat.PanoramicTint[1], skyboxMat.PanoramicTint[2], skyboxMat.PanoramicTint[3]);
            if (ImGui.ColorEdit4("Tint", ref tint))
            {
                skyboxMat.PanoramicTint = new float[] { tint.X, tint.Y, tint.Z, tint.W };
                changed = true;
            }
            
            // Exposure
            var exposure = skyboxMat.PanoramicExposure;
            if (ImGui.DragFloat("Exposure", ref exposure, 0.01f, 0.0f, 8.0f))
            {
                skyboxMat.PanoramicExposure = exposure;
                changed = true;
            }
            
            // Rotation
            var rotation = skyboxMat.PanoramicRotation;
            if (ImGui.SliderFloat("Rotation", ref rotation, 0.0f, 360.0f, "%.1f°"))
            {
                skyboxMat.PanoramicRotation = rotation;
                changed = true;
            }
            
            // Mapping
            var mappingNames = new[] { "Latitude-Longitude Layout", "Mirror Ball", "Mirror Ball (Front Only)" };
            var currentMapping = (int)skyboxMat.Mapping;
            if (ImGui.Combo("Mapping", ref currentMapping, mappingNames, mappingNames.Length))
            {
                skyboxMat.Mapping = (PanoramicMapping)currentMapping;
                changed = true;
            }
            
            // Image Type
            var imageTypeNames = new[] { "360°", "180°" };
            var currentImageType = (int)skyboxMat.ImageType;
            if (ImGui.Combo("Image Type", ref currentImageType, imageTypeNames, imageTypeNames.Length))
            {
                skyboxMat.ImageType = (PanoramicImageType)currentImageType;
                changed = true;
            }
            
            // Mirror on Back
            var mirrorOnBack = skyboxMat.MirrorOnBack;
            if (ImGui.Checkbox("Mirror on Back", ref mirrorOnBack))
            {
                skyboxMat.MirrorOnBack = mirrorOnBack;
                changed = true;
            }

            return changed;
        }
        
        private static bool DrawTextureField(string label, Guid? textureGuid, Action<Guid?> onChanged)
        {
            bool changed = false;
            ImGui.Text(label);
            ImGui.SameLine();
            
            string textureName = "None (Texture2D)";
            if (textureGuid.HasValue && textureGuid.Value != Guid.Empty)
            {
                textureName = Engine.Assets.AssetDatabase.GetName(textureGuid.Value) ?? "Unknown Texture";
            }
            
            ImGui.SetNextItemWidth(-1);
            
            var buttonColor = textureGuid.HasValue && textureGuid.Value != Guid.Empty
                ? new Numerics.Vector4(0.3f, 0.6f, 1.0f, 1.0f)  // Blue for assigned texture
                : new Numerics.Vector4(0.4f, 0.4f, 0.4f, 1.0f);  // Gray for none
                
            ImGui.PushStyleColor(ImGuiCol.Button, buttonColor);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, buttonColor * 1.2f);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, buttonColor * 0.8f);
            
            bool textureClicked = ImGui.Button($"{textureName}##{label}Field", new Numerics.Vector2(-1, 20));
            
            ImGui.PopStyleColor(3);
            
            // Handle drag & drop
            if (ImGui.BeginDragDropTarget())
            {
                var payload = ImGui.AcceptDragDropPayload("ASSET_MULTI");
                unsafe
                {
                    if (payload.NativePtr != null && payload.Data != IntPtr.Zero && payload.DataSize >= 16)
                    {
                        try
                        {
                            var span = new ReadOnlySpan<byte>((void*)payload.Data, 16);
                            var droppedTextureGuid = new Guid(span);
                            
                            // Check if it's a texture asset
                            if (Engine.Assets.AssetDatabase.TryGet(droppedTextureGuid, out var record) && 
                                (string.Equals(record.Type, "Texture2D", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(record.Type, "TextureHDR", StringComparison.OrdinalIgnoreCase)))
                            {
                                onChanged(droppedTextureGuid);
                                changed = true;
                            }
                        }
                        catch (Exception)
                        {
                            // Ignore drag & drop errors
                        }
                    }
                }
                ImGui.EndDragDropTarget();
            }
            
            // Right-click context menu
            if (ImGui.BeginPopupContextItem($"{label}ContextMenu"))
            {
                if (ImGui.MenuItem("Clear"))
                {
                    onChanged(null);
                    changed = true;
                }
                ImGui.EndPopup();
            }

            return changed;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using Engine.Assets;
using Editor.State;
using Editor.Themes;

namespace Editor.UI
{
    /// <summary>
    /// Unified UX system for AstrildApex Editor
    /// Provides consistent, professional widgets following modern editor conventions
    /// All colors and sizes come from ThemeManager.UI for easy tweaking
    /// </summary>
    public static class EditorWidgets
    {
        // Quick access to theme
        private static UITheme UI => ThemeManager.UI;

        #region Helper Methods

        /// <summary>
        /// Check if an asset type matches the requested type.
        /// Supports wildcard matching: "Model" matches "ModelGLTF", "ModelFBX", "ModelOBJ", etc.
        /// </summary>
        private static bool IsAssetTypeMatch(string assetType, string requestedType)
        {
            // Exact match
            if (assetType == requestedType)
                return true;

            // Wildcard matching for common categories
            if (requestedType == "Model" && assetType.StartsWith("Model"))
                return true;

            if (requestedType == "Texture" && assetType.StartsWith("Texture"))
                return true;

            return false;
        }

        #endregion

        #region Asset Field Widget
        
        /// <summary>
        /// Professional asset field with drag-drop, click-to-select popup, and clear button
        /// </summary>
        public static Guid? AssetField(
            string label,
            Guid? currentAssetGuid,
            string assetType,
            string? description = null,
            bool showPreview = false,
            float dragDropHeight = 40f)
        {
            Guid? result = currentAssetGuid;
            
            ImGui.PushID(label);
            
            // Label with description
            if (!string.IsNullOrEmpty(description))
            {
                ImGui.TextColored(UI.TextAccent, label);
                ImGui.SameLine();
                HelpIcon(description);
            }
            else
            {
                ImGui.Text(label);
            }
            
            // Asset is assigned - show info and actions
            if (currentAssetGuid.HasValue && currentAssetGuid.Value != Guid.Empty)
            {
                if (AssetDatabase.TryGet(currentAssetGuid.Value, out var record))
                {
                    // Display asset name with success color
                    string fileName = System.IO.Path.GetFileName(record.Path);
                    ImGui.TextColored(UI.Success, $"✓ {fileName}");
                    
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip(record.Path);
                    
                    // Preview if requested (for textures)
                    if (showPreview && record.Type == "Texture2D")
                    {
                        try
                        {
                            Engine.Rendering.TextureCache.Initialize();
                            int handle = Engine.Rendering.TextureCache.GetOrLoad(
                                currentAssetGuid.Value, 
                                g => AssetDatabase.TryGet(g, out var r) ? r.Path : null);
                            
                            var previewSize = new Vector2(200, 120);
                            ImGui.Image((IntPtr)handle, previewSize, new Vector2(0, 1), new Vector2(1, 0));
                        }
                        catch { /* Silently fail */ }
                    }
                    
                    // Action buttons (use global theme styles like "Remove Component")
                    ImGui.BeginGroup();

                    // Select button - opens asset in inspector
                    if (ThemedImGui.Button("Select", new Vector2(70, UI.ButtonHeightSmall)))
                    {
                        Selection.SetActiveAsset(currentAssetGuid.Value, record.Type);
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Open in Inspector");

                    ImGui.SameLine();

                    // Ping button - highlights in Assets panel
                    if (ThemedImGui.Button("Ping", new Vector2(70, UI.ButtonHeightSmall)))
                    {
                        try
                        {
                            // Ping asset in Assets panel - expand folders and scroll to it
                            Panels.AssetsPanel.PingAsset(currentAssetGuid.Value);
                        }
                        catch { /* Silently fail if panel not available */ }
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Highlight in Assets panel");

                    ImGui.SameLine();

                    // Clear button
                    if (ThemedImGui.Button("Clear", new Vector2(70, UI.ButtonHeightSmall)))
                    {
                        result = null;
                        ImGui.EndGroup();
                        ImGui.PopID();
                        return result;
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Remove asset assignment");

                    ImGui.EndGroup();
                }
                else
                {
                    // Asset not found in database
                    ImGui.TextColored(UI.Error, "✗ Asset not found!");
                    if (ThemedImGui.Button("Clear##missing", new Vector2(-1, UI.ButtonHeightSmall)))
                    {
                        result = null;
                        ImGui.PopID();
                        return result;
                    }
                }
            }
            else
            {
                // No asset assigned - show selection options
                
                // Large drag-drop button
                string buttonText = $"Drag & Drop {assetType} Here";
                bool clicked = ThemedImGui.ButtonColored(buttonText, UI.Background, new Vector2(-1, dragDropHeight));
                
                // Handle drag-drop
                if (ImGui.BeginDragDropTarget())
                {
                    unsafe
                    {
                        var payload = ImGui.AcceptDragDropPayload("ASSET_MULTI");
                        if (payload.NativePtr != null && payload.Data != IntPtr.Zero && payload.DataSize >= 16)
                        {
                            var span = new ReadOnlySpan<byte>((void*)payload.Data, (int)payload.DataSize);
                            var assetGuid = new Guid(span.Slice(0, 16));

                            if (AssetDatabase.TryGet(assetGuid, out var rec) && IsAssetTypeMatch(rec.Type, assetType))
                            {
                                result = assetGuid;
                            }
                        }
                    }
                    ImGui.EndDragDropTarget();
                }
                
                // Handle click - open asset selector popup
                if (clicked)
                {
                    ImGui.OpenPopup($"AssetSelector_{label}");
                }
                
                // Asset selector popup
                if (ImGui.BeginPopup($"AssetSelector_{label}"))
                {
                    ImGui.TextColored(UI.Primary, $"Select {assetType}");
                    ImGui.Separator();

                    // Get all assets of this type (with support for wildcard matching)
                    var assets = AssetDatabase.All()
                        .Where(r => IsAssetTypeMatch(r.Type, assetType))
                        .OrderBy(r => System.IO.Path.GetFileName(r.Path))
                        .ToList();
                    
                    if (assets.Count == 0)
                    {
                        ImGui.TextDisabled($"No {assetType} assets found");
                    }
                    else
                    {
                        // Search filter
                        string searchText = "";
                        ImGui.SetNextItemWidth(200);
                        if (ImGui.InputTextWithHint("##search", "Search...", ref searchText, 256))
                        {
                            // Filter will be applied below
                        }
                        
                        ImGui.Separator();
                        
                        // Asset list (with scrolling)
                        if (ImGui.BeginChild("AssetList", new Vector2(300, 300), ImGuiChildFlags.Borders, ImGuiWindowFlags.None))
                        {
                            foreach (var asset in assets)
                            {
                                string fileName = System.IO.Path.GetFileName(asset.Path);
                                
                                // Apply search filter
                                if (!string.IsNullOrEmpty(searchText) && 
                                    !fileName.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                                    continue;
                                
                                if (ImGui.Selectable(fileName))
                                {
                                    result = asset.Guid;
                                    ImGui.CloseCurrentPopup();
                                }
                                
                                if (ImGui.IsItemHovered())
                                    ImGui.SetTooltip(asset.Path);
                            }
                        }
                        ImGui.EndChild();
                    }
                    
                    ImGui.EndPopup();
                }
            }
            
            ImGui.PopID();
            
            return result;
        }
        
        #endregion
        
        #region Section Widgets
        
        /// <summary>
        /// Collapsible section header with consistent styling and automatic text contrast
        /// </summary>
        public static bool Section(string label, bool defaultOpen = true, string? tooltip = null)
        {
            // Use ThemedImGui for automatic text contrast
            var flags = defaultOpen ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None;
            bool isOpen = ThemedImGui.CollapsingHeader(label, flags);
            
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.DelayShort) && !string.IsNullOrEmpty(tooltip))
            {
                ImGui.SetTooltip(tooltip);
            }
            
            if (isOpen)
            {
                ImGui.Indent(UI.IndentWidth);
            }
            
            return isOpen;
        }
        
        public static void EndSection()
        {
            ImGui.Unindent(UI.IndentWidth);
        }
        
        public static void Separator()
        {
            ImGui.Spacing();
            ImGui.PushStyleColor(ImGuiCol.Separator, UI.Separator);
            ImGui.Separator();
            ImGui.PopStyleColor();
            ImGui.Spacing();
        }
        
        #endregion
        
        #region Helper Widgets
        
        public static void HelpIcon(string helpText)
        {
            ImGui.TextDisabled("(?)");
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.DelayShort))
            {
                ImGui.SetTooltip(helpText);
            }
        }
        
        public static void InfoBox(string message, Vector4? color = null)
        {
            var boxColor = color ?? UI.Info;
            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(boxColor.X * 0.3f, boxColor.Y * 0.3f, boxColor.Z * 0.3f, 0.5f));
            ImGui.PushStyleColor(ImGuiCol.Border, boxColor);
            
            ImGui.BeginChild("InfoBox", new Vector2(-1, 0), ImGuiChildFlags.Borders, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.AlwaysAutoResize);
            ImGui.TextWrapped(message);
            ImGui.EndChild();
            
            ImGui.PopStyleColor(2);
        }
        
        public static void WarningBox(string message)
        {
            InfoBox("⚠ " + message, UI.Warning);
        }
        
        public static void ErrorBox(string message)
        {
            InfoBox("✗ " + message, UI.Error);
        }
        
        public static void SuccessBox(string message)
        {
            InfoBox("✓ " + message, UI.Success);
        }
        
        #endregion
        
        #region Button Widgets
        
        public static bool PrimaryButton(string label, Vector2? size = null)
        {
            return ThemedImGui.ButtonColored(label, UI.Primary, size ?? new Vector2(-1, UI.ButtonHeightMedium));
        }
        
        public static bool DangerButton(string label, Vector2? size = null)
        {
            return ThemedImGui.ButtonColored(label, new Vector4(0.8f, 0.2f, 0.2f, 1f), size ?? new Vector2(-1, UI.ButtonHeightMedium));
        }
        
        public static bool SuccessButton(string label, Vector2? size = null)
        {
            return ThemedImGui.ButtonColored(label, new Vector4(0.2f, 0.8f, 0.2f, 1f), size ?? new Vector2(-1, UI.ButtonHeightMedium));
        }
        
        #endregion
        
        #region Combo/Dropdown Widgets
        
        public static bool EnumCombo<T>(string label, ref T value, string? tooltip = null) where T : Enum
        {
            var values = Enum.GetValues(typeof(T)).Cast<T>().ToArray();
            var names = values.Select(v => v.ToString()).ToArray();
            
            int currentIndex = Array.IndexOf(values, value);
            
            ImGui.PushItemWidth(UI.ControlWidthAuto);
            bool changed = ImGui.Combo(label, ref currentIndex, names, names.Length);
            ImGui.PopItemWidth();
            
            if (changed && currentIndex >= 0 && currentIndex < values.Length)
            {
                value = values[currentIndex];
            }
            
            if (!string.IsNullOrEmpty(tooltip) && ImGui.IsItemHovered(ImGuiHoveredFlags.DelayShort))
            {
                ImGui.SetTooltip(tooltip);
            }
            
            return changed;
        }
        
        #endregion
        
        #region Field Widgets
        
        public static bool TextField(string label, ref string value, int maxLength = 256, string? placeholder = null, string? tooltip = null)
        {
            ImGui.PushItemWidth(UI.ControlWidthAuto);
            
            bool changed = placeholder != null 
                ? ImGui.InputTextWithHint(label, placeholder, ref value, (uint)maxLength)
                : ImGui.InputText(label, ref value, (uint)maxLength);
            
            ImGui.PopItemWidth();
            
            if (!string.IsNullOrEmpty(tooltip) && ImGui.IsItemHovered(ImGuiHoveredFlags.DelayShort))
            {
                ImGui.SetTooltip(tooltip);
            }
            
            return changed;
        }
        
        public static bool FloatSlider(string label, ref float value, float min, float max, string format = "%.2f", string? tooltip = null)
        {
            ImGui.PushItemWidth(UI.ControlWidthAuto);
            bool changed = ImGui.SliderFloat(label, ref value, min, max, format);
            ImGui.PopItemWidth();
            
            if (!string.IsNullOrEmpty(tooltip) && ImGui.IsItemHovered(ImGuiHoveredFlags.DelayShort))
            {
                ImGui.SetTooltip(tooltip);
            }
            
            return changed;
        }
        
        public static bool IntSlider(string label, ref int value, int min, int max, string? tooltip = null)
        {
            ImGui.PushItemWidth(UI.ControlWidthAuto);
            bool changed = ImGui.SliderInt(label, ref value, min, max);
            ImGui.PopItemWidth();
            
            if (!string.IsNullOrEmpty(tooltip) && ImGui.IsItemHovered(ImGuiHoveredFlags.DelayShort))
            {
                ImGui.SetTooltip(tooltip);
            }
            
            return changed;
        }
        
        public static bool ColorPicker(string label, ref Vector4 color, bool showAlpha = true, string? tooltip = null)
        {
            ImGui.PushItemWidth(UI.ControlWidthAuto);
            
            var flags = ImGuiColorEditFlags.DisplayRGB;
            if (showAlpha)
                flags |= ImGuiColorEditFlags.AlphaBar;
            
            bool changed;
            if (showAlpha)
            {
                changed = ImGui.ColorEdit4(label, ref color, flags);
            }
            else
            {
                var color3 = new Vector3(color.X, color.Y, color.Z);
                changed = ImGui.ColorEdit3(label, ref color3, flags);
                if (changed)
                {
                    color.X = color3.X;
                    color.Y = color3.Y;
                    color.Z = color3.Z;
                }
            }
            
            ImGui.PopItemWidth();
            
            if (!string.IsNullOrEmpty(tooltip) && ImGui.IsItemHovered(ImGuiHoveredFlags.DelayShort))
            {
                ImGui.SetTooltip(tooltip);
            }
            
            return changed;
        }
        
        #endregion
    }
}

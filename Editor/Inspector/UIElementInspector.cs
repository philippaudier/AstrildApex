using System;
using System.Numerics;
using ImGuiNET;
using Engine.Components.UI;
using Engine.Scene;
using Editor.State;
using NumVector3 = System.Numerics.Vector3;

namespace Editor.Inspector
{
    public static class UIElementInspector
    {
        public static void DrawInspector(Entity entity, UIElementComponent elem)
        {
            uint entityId = entity.Id;

            // === UI ELEMENT ===
            if (InspectorWidgets.Section("UI Element", true, "Base properties for all UI elements"))
            {
                // Element Type dropdown
                var elemType = elem.Type;
                if (InspectorWidgets.EnumField("Element Type", ref elemType, entityId, "Type",
                    tooltip: "The visual/functional type of this UI element",
                    helpText: "Controls which properties and rendering are available"))
                {
                    elem.Type = elemType;
                }

                // Enabled checkbox
                bool enabled = elem.Enabled;
                InspectorWidgets.Checkbox("Enabled", ref enabled, entityId, "Enabled",
                    tooltip: "Whether this element is visible and interactable");
                elem.Enabled = enabled;

                // Sort Order
                int sortOrder = elem.SortOrder;
                InspectorWidgets.IntField("Sort Order", ref sortOrder, entityId, "SortOrder",
                    tooltip: "Higher values render on top",
                    helpText: "Controls draw order within the same canvas");
                elem.SortOrder = sortOrder;
            }

            // === RECT TRANSFORM ===
            if (InspectorWidgets.Section("Rect Transform", true, "Position and size within parent canvas"))
            {
                // Anchor Presets
                ImGui.Text("Anchor Presets:");
                ImGui.Indent();
                
                if (InspectorWidgets.PresetButton("Center", "Anchor to center point"))
                {
                    elem.AnchorMin = new Vector2(0.5f, 0.5f);
                    elem.AnchorMax = new Vector2(0.5f, 0.5f);
                    elem.Pivot = new Vector2(0.5f, 0.5f);
                }
                ImGui.SameLine();
                if (InspectorWidgets.PresetButton("Stretch All", "Stretch to fill parent"))
                {
                    elem.AnchorMin = Vector2.Zero;
                    elem.AnchorMax = Vector2.One;
                    elem.Pivot = new Vector2(0.5f, 0.5f);
                    elem.SizeDelta = Vector2.Zero;
                }
                ImGui.SameLine();
                if (InspectorWidgets.PresetButton("Top Left", "Anchor to top-left corner"))
                {
                    elem.AnchorMin = new Vector2(0f, 1f);
                    elem.AnchorMax = new Vector2(0f, 1f);
                    elem.Pivot = new Vector2(0f, 1f);
                }
                
                ImGui.Unindent();

                // Anchor values
                ImGui.PushItemWidth(InspectorLayout.ControlWidth);
                
                var anchorMin = elem.AnchorMin;
                if (ImGui.DragFloat2("Anchor Min##AnchorMin", ref anchorMin, 0.01f, 0f, 1f))
                    elem.AnchorMin = anchorMin;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Bottom-left anchor point (0-1 normalized)");

                var anchorMax = elem.AnchorMax;
                if (ImGui.DragFloat2("Anchor Max##AnchorMax", ref anchorMax, 0.01f, 0f, 1f))
                    elem.AnchorMax = anchorMax;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Top-right anchor point (0-1 normalized)");

                ImGui.PopItemWidth();

                // Validation warning for invalid anchors
                if (elem.AnchorMin.X > elem.AnchorMax.X || elem.AnchorMin.Y > elem.AnchorMax.Y)
                {
                    InspectorWidgets.WarningBox("Anchor Min should be less than Anchor Max");
                }

                // Position & Size
                ImGui.PushItemWidth(InspectorLayout.ControlWidth);
                
                var anchoredPos = elem.AnchoredPosition;
                if (ImGui.DragFloat2("Anchored Position##AnchoredPos", ref anchoredPos, 1f))
                    elem.AnchoredPosition = anchoredPos;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Offset from anchor point in pixels");

                var sizeDelta = elem.SizeDelta;
                if (ImGui.DragFloat2("Size Delta##SizeDelta", ref sizeDelta, 1f))
                    elem.SizeDelta = sizeDelta;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Size difference from anchored rect. When stretched, this adds to the anchor-defined size");

                var pivot = elem.Pivot;
                if (ImGui.DragFloat2("Pivot##Pivot", ref pivot, 0.01f, 0f, 1f))
                    elem.Pivot = pivot;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("The pivot point of this rect (0-1 normalized). 0,0 = bottom-left, 1,1 = top-right");
                
                ImGui.PopItemWidth();
            }

            // === VISUAL PROPERTIES (Image/Button) ===
            if (elem.Type == UIElementComponent.ElementType.Image || 
                elem.Type == UIElementComponent.ElementType.Button)
            {
                if (InspectorWidgets.Section("Visual", true, "Appearance settings for images and buttons"))
                {
                    // Color tint
                    var color = elem.Color;
                    var colorVec4 = new Vector4(color.X, color.Y, color.Z, color.W);
                    if (InspectorWidgets.ColorFieldAlpha("Color", ref colorVec4, entityId, "Color",
                        tooltip: "Tint color multiplied with texture"))
                    {
                        elem.Color = colorVec4;
                    }

                    // Texture selector
                    string textureName = elem.TextureGuid.HasValue ? 
                        GetAssetName(elem.TextureGuid.Value) : "None";
                    
                    ImGui.Text("Texture:");
                    ImGui.SameLine();
                    if (ImGui.Button($"{textureName}##TextureBtn"))
                    {
                        // TODO: Open asset picker
                        ImGui.OpenPopup("SelectTexture");
                    }

                    if (elem.TextureGuid.HasValue)
                    {
                        ImGui.SameLine();
                        if (ImGui.SmallButton("Clear##ClearTexture"))
                        {
                            elem.TextureGuid = null;
                        }
                    }
                    
                    // TODO: Drag-drop support for textures
                }
            }

            // === TEXT PROPERTIES ===
            if (elem.Type == UIElementComponent.ElementType.Text || 
                elem.Type == UIElementComponent.ElementType.Button)
            {
                if (InspectorWidgets.Section("Text", true, "Text content and typography"))
                {
                    // Text content
                    string text = elem.Text ?? "";
                    if (InspectorWidgets.TextField("Text Content", ref text, 1024, entityId, "Text",
                        tooltip: "The text to display"))
                    {
                        elem.Text = text;
                    }

                    // Font size
                    float fontSize = elem.FontSize;
                    if (InspectorWidgets.SliderFloat("Font Size", ref fontSize, 8f, 128f, "%.1f", entityId, "FontSize",
                        tooltip: "Size of the text in pixels",
                        validate: fs => fs > 0 ? null : "Font size must be positive"))
                    {
                        elem.FontSize = fontSize;
                    }
                    
                    // Font Size presets
                    ImGui.Text("Size Presets:");
                    ImGui.Indent();
                    if (InspectorWidgets.PresetButton("Small (12)", "Small UI text")) 
                        elem.FontSize = 12f;
                    ImGui.SameLine();
                    if (InspectorWidgets.PresetButton("Medium (16)", "Standard body text")) 
                        elem.FontSize = 16f;
                    ImGui.SameLine();
                    if (InspectorWidgets.PresetButton("Large (24)", "Heading text")) 
                        elem.FontSize = 24f;
                    ImGui.SameLine();
                    if (InspectorWidgets.PresetButton("Title (32)", "Large title text")) 
                        elem.FontSize = 32f;
                    ImGui.Unindent();

                    // Text alignment
                    var textAlign = elem.TextAlignment;
                    if (InspectorWidgets.EnumField("Alignment", ref textAlign, entityId, "TextAlignment",
                        tooltip: "Horizontal text alignment within the rect"))
                    {
                        elem.TextAlignment = textAlign;
                    }

                    // Font selector
                    string fontName = elem.FontGuid.HasValue ? 
                        GetAssetName(elem.FontGuid.Value) : "Default";
                    
                    ImGui.Text("Font:");
                    ImGui.SameLine();
                    if (ImGui.Button($"{fontName}##FontBtn"))
                    {
                        // TODO: Open font asset picker
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Click to select a font asset");
                }
            }

            // === BUTTON PROPERTIES ===
            if (elem.Type == UIElementComponent.ElementType.Button)
            {
                if (InspectorWidgets.Section("Button", true, "Interactive button settings"))
                {
                    // Interactable
                    bool interactable = elem.Interactable;
                    InspectorWidgets.Checkbox("Interactable", ref interactable, entityId, "Interactable",
                        tooltip: "Whether the button can be clicked");
                    elem.Interactable = interactable;

                    // Hover color
                    var hoverColor = elem.HoverColor;
                    InspectorWidgets.ColorFieldAlpha("Hover Color", ref hoverColor, entityId, "HoverColor",
                        tooltip: "Color when mouse is over the button");
                    elem.HoverColor = hoverColor;

                    // Pressed color
                    var pressedColor = elem.PressedColor;
                    InspectorWidgets.ColorFieldAlpha("Pressed Color", ref pressedColor, entityId, "PressedColor",
                        tooltip: "Color when button is being clicked");
                    elem.PressedColor = pressedColor;
                    
                    // Button presets
                    ImGui.Text("Interaction Presets:");
                    ImGui.Indent();
                    if (InspectorWidgets.PresetButton("Default", "Standard button colors"))
                    {
                        elem.Color = new Vector4(1f, 1f, 1f, 1f);
                        elem.HoverColor = new Vector4(0.9f, 0.9f, 0.9f, 1f);
                        elem.PressedColor = new Vector4(0.7f, 0.7f, 0.7f, 1f);
                    }
                    ImGui.SameLine();
                    if (InspectorWidgets.PresetButton("Primary", "Blue accent button"))
                    {
                        elem.Color = new Vector4(0.2f, 0.5f, 1f, 1f);
                        elem.HoverColor = new Vector4(0.3f, 0.6f, 1f, 1f);
                        elem.PressedColor = new Vector4(0.15f, 0.4f, 0.8f, 1f);
                    }
                    ImGui.SameLine();
                    if (InspectorWidgets.PresetButton("Success", "Green action button"))
                    {
                        elem.Color = new Vector4(0.2f, 0.8f, 0.2f, 1f);
                        elem.HoverColor = new Vector4(0.3f, 0.9f, 0.3f, 1f);
                        elem.PressedColor = new Vector4(0.15f, 0.6f, 0.15f, 1f);
                    }
                    ImGui.Unindent();
                }
            }

            // === FLEXBOX LAYOUT (Advanced) ===
            if (InspectorWidgets.Section("Flexbox Layout", false, "Advanced CSS-like flexbox layout system"))
            {
                InspectorWidgets.InfoBox("Flexbox provides automatic child element positioning similar to CSS Flexbox. Useful for creating dynamic layouts.");

                // Use Flexbox toggle
                bool useFlexbox = elem.UseFlexbox;
                InspectorWidgets.Checkbox("Enable Flexbox", ref useFlexbox, entityId, "UseFlexbox",
                    tooltip: "Enable flexbox layout for child elements");
                elem.UseFlexbox = useFlexbox;

                if (elem.UseFlexbox)
                {
                    // Flex Direction
                    var flexDir = elem.FlexDirection;
                    InspectorWidgets.EnumField("Direction", ref flexDir, entityId, "FlexDirection",
                        tooltip: "Direction children are laid out (Row = horizontal, Column = vertical)");
                    elem.FlexDirection = flexDir;

                    // Justify Content
                    var flexJustify = elem.JustifyContent;
                    InspectorWidgets.EnumField("Justify Content", ref flexJustify, entityId, "JustifyContent",
                        tooltip: "How items are distributed along the main axis");
                    elem.JustifyContent = flexJustify;

                    // Align Items
                    var flexAlign = elem.AlignItems;
                    InspectorWidgets.EnumField("Align Items", ref flexAlign, entityId, "AlignItems",
                        tooltip: "How items are aligned along the cross axis");
                    elem.AlignItems = flexAlign;

                    // Gap
                    float gap = elem.FlexGap;
                    InspectorWidgets.FloatField("Gap", ref gap, entityId, "FlexGap", 0.5f,
                        tooltip: "Space between child elements in pixels",
                        validate: g => g >= 0 ? null : "Gap cannot be negative");
                    elem.FlexGap = gap;
                    
                    // Flexbox presets
                    ImGui.Text("Layout Presets:");
                    ImGui.Indent();
                    if (InspectorWidgets.PresetButton("Horizontal Center", "Center items horizontally"))
                    {
                        elem.FlexDirection = FlexDirection.Row;
                        elem.JustifyContent = FlexJustify.Center;
                        elem.AlignItems = FlexAlign.Center;
                        elem.FlexGap = 10f;
                    }
                    ImGui.SameLine();
                    if (InspectorWidgets.PresetButton("Vertical Stack", "Stack items vertically"))
                    {
                        elem.FlexDirection = FlexDirection.Column;
                        elem.JustifyContent = FlexJustify.FlexStart;
                        elem.AlignItems = FlexAlign.Stretch;
                        elem.FlexGap = 5f;
                    }
                    ImGui.SameLine();
                    if (InspectorWidgets.PresetButton("Space Between", "Spread items evenly"))
                    {
                        elem.FlexDirection = FlexDirection.Row;
                        elem.JustifyContent = FlexJustify.SpaceBetween;
                        elem.AlignItems = FlexAlign.Center;
                        elem.FlexGap = 0f;
                    }
                    ImGui.Unindent();
                }
            }
        }

        private static string GetAssetName(Guid guid)
        {
            if (Engine.Assets.AssetDatabase.TryGet(guid, out var record))
            {
                return System.IO.Path.GetFileNameWithoutExtension(record.Path);
            }
            return "Unknown";
        }
    }
}

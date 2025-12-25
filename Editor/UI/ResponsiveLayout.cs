using System;
using System.Numerics;
using ImGuiNET;

namespace Editor.UI
{
    /// <summary>
    /// Responsive layout helpers to prevent UI element overlap
    /// </summary>
    public static class ResponsiveLayout
    {
        /// <summary>
        /// Begin a two-column layout with automatic sizing
        /// </summary>
        public static void BeginTwoColumns(string id, float leftRatio = 0.5f, float minLeftWidth = 100f, float minRightWidth = 100f)
        {
            float availWidth = ImGui.GetContentRegionAvail().X;
            float spacing = ImGui.GetStyle().ItemSpacing.X;
            
            float leftWidth = Math.Max(minLeftWidth, availWidth * leftRatio - spacing * 0.5f);
            float rightWidth = Math.Max(minRightWidth, availWidth - leftWidth - spacing);
            
            ImGui.BeginGroup();
            ImGui.BeginChild($"{id}_Left", new Vector2(leftWidth, 0), ImGuiChildFlags.None);
        }
        
        /// <summary>
        /// Switch from left column to right column
        /// </summary>
        public static void NextColumn(string id, float minRightWidth = 100f)
        {
            ImGui.EndChild(); // End left
            ImGui.EndGroup();
            
            ImGui.SameLine();
            
            float rightWidth = Math.Max(minRightWidth, ImGui.GetContentRegionAvail().X);
            ImGui.BeginGroup();
            ImGui.BeginChild($"{id}_Right", new Vector2(rightWidth, 0), ImGuiChildFlags.None);
        }
        
        /// <summary>
        /// End the two-column layout
        /// </summary>
        public static void EndTwoColumns()
        {
            ImGui.EndChild(); // End right
            ImGui.EndGroup();
        }
        
        /// <summary>
        /// Draw a list item with text on left and button/widget on right (no overlap)
        /// </summary>
        public static bool ListItemWithButton(string label, string buttonLabel, float buttonWidth = 100f)
        {
            bool clicked = false;
            float availWidth = ImGui.GetContentRegionAvail().X;
            float spacing = ImGui.GetStyle().ItemSpacing.X;
            
            // Reserve space for button
            float textWidth = Math.Max(50f, availWidth - buttonWidth - spacing);
            
            // Draw text (truncated if needed)
            ImGui.BeginGroup();
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + textWidth);
            ImGui.TextUnformatted(label);
            ImGui.PopTextWrapPos();
            ImGui.EndGroup();
            
            // Draw button on same line
            ImGui.SameLine(availWidth - buttonWidth);
            clicked = ImGui.Button(buttonLabel, new Vector2(buttonWidth, 0));
            
            return clicked;
        }
        
        /// <summary>
        /// Draw a selectable item with status text on right (no overlap)
        /// </summary>
        public static bool SelectableWithStatus(string id, string label, bool selected, string status, Vector4 statusColor)
        {
            bool clicked = false;
            float availWidth = ImGui.GetContentRegionAvail().X;
            float spacing = ImGui.GetStyle().ItemSpacing.X;
            
            // Calculate status width
            Vector2 statusSize = ImGui.CalcTextSize(status);
            float statusWidth = statusSize.X + spacing * 2;
            float selectableWidth = Math.Max(100f, availWidth - statusWidth - spacing);
            
            ImGui.PushID(id);
            
            // Selectable takes only the left portion
            clicked = ImGui.Selectable(label, selected, ImGuiSelectableFlags.None, new Vector2(selectableWidth, 0));
            
            // Status on the right
            ImGui.SameLine(availWidth - statusWidth);
            ImGui.PushStyleColor(ImGuiCol.Text, statusColor);
            ImGui.TextUnformatted(status);
            ImGui.PopStyleColor();
            
            ImGui.PopID();
            
            return clicked;
        }
        
        /// <summary>
        /// Begin a responsive grid layout
        /// </summary>
        public static int BeginResponsiveGrid(float itemWidth, float itemHeight, out int columns)
        {
            float availWidth = ImGui.GetContentRegionAvail().X;
            float spacing = ImGui.GetStyle().ItemSpacing.X;
            
            columns = Math.Max(1, (int)((availWidth + spacing) / (itemWidth + spacing)));
            
            return columns;
        }
        
        /// <summary>
        /// Calculate if we should add SameLine for grid item
        /// </summary>
        public static bool GridItemSameLine(int index, int columns)
        {
            return (index % columns) != 0;
        }
        
        /// <summary>
        /// Begin a split panel layout (left/right or top/bottom)
        /// </summary>
        public static void BeginSplitPanel(string id, bool horizontal, float splitRatio, float minSize, out float size1, out float size2)
        {
            Vector2 avail = ImGui.GetContentRegionAvail();
            float spacing = horizontal ? ImGui.GetStyle().ItemSpacing.X : ImGui.GetStyle().ItemSpacing.Y;
            float totalSize = horizontal ? avail.X : avail.Y;
            
            size1 = Math.Max(minSize, totalSize * splitRatio - spacing * 0.5f);
            size2 = Math.Max(minSize, totalSize - size1 - spacing);
        }
        
        /// <summary>
        /// Ensure text fits in available width with ellipsis
        /// </summary>
        public static void TextFit(string text, float maxWidth = -1)
        {
            if (maxWidth < 0)
                maxWidth = ImGui.GetContentRegionAvail().X;
            
            Vector2 textSize = ImGui.CalcTextSize(text);
            
            if (textSize.X <= maxWidth)
            {
                ImGui.TextUnformatted(text);
            }
            else
            {
                // Truncate with ellipsis
                string truncated = text;
                Vector2 ellipsisSize = ImGui.CalcTextSize("...");
                float targetWidth = maxWidth - ellipsisSize.X;
                
                // Binary search for best fit
                int left = 0, right = text.Length;
                while (left < right)
                {
                    int mid = (left + right + 1) / 2;
                    Vector2 size = ImGui.CalcTextSize(text.Substring(0, mid));
                    if (size.X <= targetWidth)
                        left = mid;
                    else
                        right = mid - 1;
                }
                
                truncated = text.Substring(0, left) + "...";
                ImGui.TextUnformatted(truncated);
            }
            
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted(text);
                ImGui.EndTooltip();
            }
        }
        
        /// <summary>
        /// Draw a label-value pair with proper alignment (no overlap)
        /// </summary>
        public static void LabelValue(string label, string value, float labelWidth = -1)
        {
            if (labelWidth < 0)
                labelWidth = ImGui.GetContentRegionAvail().X * 0.4f;
            
            float availWidth = ImGui.GetContentRegionAvail().X;
            float valueWidth = Math.Max(50f, availWidth - labelWidth - ImGui.GetStyle().ItemSpacing.X);
            
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(label);
            ImGui.SameLine(labelWidth);
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + valueWidth);
            ImGui.TextUnformatted(value);
            ImGui.PopTextWrapPos();
        }
        
        /// <summary>
        /// Begin a scrollable content area with proper sizing
        /// </summary>
        public static void BeginScrollableContent(string id, float reservedBottomSpace = 0)
        {
            Vector2 avail = ImGui.GetContentRegionAvail();
            float height = Math.Max(100f, avail.Y - reservedBottomSpace);
            ImGui.BeginChild(id, new Vector2(0, height), ImGuiChildFlags.Borders);
        }
        
        /// <summary>
        /// Draw buttons in a row that wrap if needed
        /// </summary>
        public static void ButtonRow(Action<int> drawButton, int buttonCount, float buttonWidth, float spacing = -1)
        {
            if (spacing < 0)
                spacing = ImGui.GetStyle().ItemSpacing.X;
            
            float availWidth = ImGui.GetContentRegionAvail().X;
            int buttonsPerRow = Math.Max(1, (int)((availWidth + spacing) / (buttonWidth + spacing)));
            
            for (int i = 0; i < buttonCount; i++)
            {
                if (i > 0 && (i % buttonsPerRow) != 0)
                    ImGui.SameLine();
                
                drawButton(i);
            }
        }
        
        /// <summary>
        /// Center content horizontally
        /// </summary>
        public static void CenterContent(float contentWidth)
        {
            float availWidth = ImGui.GetContentRegionAvail().X;
            if (contentWidth < availWidth)
            {
                float offset = (availWidth - contentWidth) * 0.5f;
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
            }
        }
        
        /// <summary>
        /// Align content to right
        /// </summary>
        public static void AlignRight(float contentWidth)
        {
            float availWidth = ImGui.GetContentRegionAvail().X;
            if (contentWidth < availWidth)
            {
                float offset = availWidth - contentWidth;
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
            }
        }
    }
}

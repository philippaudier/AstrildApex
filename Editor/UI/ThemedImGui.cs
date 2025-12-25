using System;
using System.Numerics;
using ImGuiNET;
using Editor.Themes;

namespace Editor.UI
{
    /// <summary>
    /// Theme-aware wrappers for ImGui collapsible sections
    /// Ensures proper text contrast based on background luminosity
    /// </summary>
    public static class ThemedImGui
    {
        /// <summary>
        /// Calculate relative luminance (WCAG formula)
        /// </summary>
        private static float GetLuminance(Vector4 color)
        {
            // Convert sRGB to linear RGB
            float r = color.X <= 0.03928f ? color.X / 12.92f : MathF.Pow((color.X + 0.055f) / 1.055f, 2.4f);
            float g = color.Y <= 0.03928f ? color.Y / 12.92f : MathF.Pow((color.Y + 0.055f) / 1.055f, 2.4f);
            float b = color.Z <= 0.03928f ? color.Z / 12.92f : MathF.Pow((color.Z + 0.055f) / 1.055f, 2.4f);
            
            return 0.2126f * r + 0.7152f * g + 0.0722f * b;
        }
        
        /// <summary>
        /// Get appropriate text color for given background
        /// Returns white for dark backgrounds, black for light backgrounds
        /// Uses the same WCAG threshold as ThemeManager for consistency
        /// </summary>
        private static Vector4 GetContrastTextColor(Vector4 backgroundColor)
        {
            float luminance = GetLuminance(backgroundColor);

            // WCAG threshold: 0.179 is the sweet spot (same as ThemeManager)
            // For luminance > 0.179, use dark text (better contrast on bright backgrounds)
            if (luminance > 0.179f)
            {
                // Light background → Dark text (near black)
                return new Vector4(0.05f, 0.05f, 0.05f, 1f);
            }
            else
            {
                // Dark background → Light text (near white)
                return new Vector4(0.98f, 0.98f, 0.98f, 1f);
            }
        }
        
        /// <summary>
        /// Theme-aware CollapsingHeader with automatic text contrast
        /// </summary>
        public static bool CollapsingHeader(string label, ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.None)
        {
            var theme = ThemeManager.CurrentTheme;
            
            // Get appropriate text color based on header background
            var textColor = GetContrastTextColor(theme.Header);
            
            // Apply text color override
            ImGui.PushStyleColor(ImGuiCol.Text, textColor);
            
            bool result = ImGui.CollapsingHeader(label, flags);
            
            ImGui.PopStyleColor();
            
            return result;
        }
        
        /// <summary>
        /// Theme-aware TreeNodeEx with automatic text contrast
        /// </summary>
        public static bool TreeNodeEx(string label, ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.None)
        {
            var theme = ThemeManager.CurrentTheme;
            
            // Get appropriate text color based on header background
            var textColor = GetContrastTextColor(theme.Header);
            
            // Apply text color override
            ImGui.PushStyleColor(ImGuiCol.Text, textColor);
            
            bool result = ImGui.TreeNodeEx(label, flags);
            
            ImGui.PopStyleColor();
            
            return result;
        }
        
        /// <summary>
        /// Theme-aware TreeNode with automatic text contrast
        /// </summary>
        public static bool TreeNode(string label)
        {
            return TreeNodeEx(label, ImGuiTreeNodeFlags.None);
        }
        
        /// <summary>
        /// Theme-aware BeginTabItem with automatic text contrast
        /// </summary>
        public static bool BeginTabItem(string label, ImGuiTabItemFlags flags = ImGuiTabItemFlags.None)
        {
            var theme = ThemeManager.CurrentTheme;
            
            // Get appropriate text color based on tab background
            // For active tabs, use TabHovered color as reference
            var textColor = GetContrastTextColor(theme.TabHovered);
            
            // Apply text color override
            ImGui.PushStyleColor(ImGuiCol.Text, textColor);
            
            bool result = ImGui.BeginTabItem(label);
            
            ImGui.PopStyleColor();
            
            return result;
        }
        
        /// <summary>
        /// Theme-aware Button with automatic text contrast based on button state
        /// </summary>
        public static bool Button(string label, Vector2 size = default)
        {
            var theme = ThemeManager.CurrentTheme;

            // Get appropriate text color based on button background
            var textColor = GetContrastTextColor(theme.Button);

            // Apply text color override and ensure centered alignment
            ImGui.PushStyleColor(ImGuiCol.Text, textColor);
            ImGui.PushStyleVar(ImGuiStyleVar.ButtonTextAlign, new Vector2(0.5f, 0.5f));

            bool result = ImGui.Button(label, size);

            ImGui.PopStyleVar();
            ImGui.PopStyleColor();

            return result;
        }
        
        /// <summary>
        /// Theme-aware Button with custom background color and automatic text contrast
        /// </summary>
        public static bool ButtonColored(string label, Vector4 backgroundColor, Vector2 size = default)
        {
            // Get appropriate text color for this specific background
            var textColor = GetContrastTextColor(backgroundColor);

            // Apply both background and text colors, and ensure centered alignment
            ImGui.PushStyleColor(ImGuiCol.Button, backgroundColor);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, backgroundColor * 1.1f);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, backgroundColor * 1.2f);
            ImGui.PushStyleColor(ImGuiCol.Text, textColor);
            ImGui.PushStyleVar(ImGuiStyleVar.ButtonTextAlign, new Vector2(0.5f, 0.5f));

            bool result = ImGui.Button(label, size);

            ImGui.PopStyleVar();
            ImGui.PopStyleColor(4);

            return result;
        }
    }
}

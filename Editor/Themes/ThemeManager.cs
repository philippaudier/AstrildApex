using System;
using System.Numerics;
using ImGuiNET;
using Editor.Inspector;
using Editor.Logging;

namespace Editor.Themes
{
    /// <summary>
    /// Manages theme application and switching for the editor
    /// </summary>
    public static class ThemeManager
    {
        private static EditorTheme? _currentTheme;
        private static UITheme _uiTheme = new UITheme();
        
        /// <summary>
        /// Currently active theme
        /// </summary>
        public static EditorTheme CurrentTheme
        {
            get
            {
                if (_currentTheme == null)
                    _currentTheme = BuiltInThemes.AstrildLight();
                return _currentTheme;
            }
        }
        
        /// <summary>
        /// Unified UI theme for all editor widgets
        /// THIS is where you tweak the entire editor look and feel!
        /// </summary>
        public static UITheme UI => _uiTheme;
        
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
        /// Calculate appropriate text color for given background
        /// </summary>
        private static Vector4 CalculateTextColorForBackground(Vector4 backgroundColor)
        {
            float luminance = GetLuminance(backgroundColor);
            
            // WCAG threshold: 0.5 is the sweet spot
            // For luminance > 0.179, use dark text (better contrast on bright backgrounds)
            // This ensures readable text on even moderately bright backgrounds
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
        /// Apply a theme to ImGui
        /// </summary>
        public static void ApplyTheme(EditorTheme theme)
        {
            _currentTheme = theme;
            
            var style = ImGui.GetStyle();
            var colors = style.Colors;
            
            // Apply all colors from theme (use theme.Text directly, not calculated)
            colors[(int)ImGuiCol.Text] = theme.Text;
            colors[(int)ImGuiCol.TextDisabled] = theme.TextDisabled;
            colors[(int)ImGuiCol.TextSelectedBg] = theme.TextSelectedBg;
            
            colors[(int)ImGuiCol.WindowBg] = theme.WindowBackground;
            colors[(int)ImGuiCol.ChildBg] = theme.ChildBackground;
            colors[(int)ImGuiCol.PopupBg] = theme.PopupBackground;
            colors[(int)ImGuiCol.Border] = theme.Border;
            colors[(int)ImGuiCol.BorderShadow] = new Vector4(0, 0, 0, 0);
            
            colors[(int)ImGuiCol.FrameBg] = theme.FrameBg;
            colors[(int)ImGuiCol.FrameBgHovered] = theme.FrameBgHovered;
            colors[(int)ImGuiCol.FrameBgActive] = theme.FrameBgActive;
            
            colors[(int)ImGuiCol.TitleBg] = theme.TitleBg;
            colors[(int)ImGuiCol.TitleBgActive] = theme.TitleBgActive;
            colors[(int)ImGuiCol.TitleBgCollapsed] = theme.TitleBgCollapsed;
            
            colors[(int)ImGuiCol.MenuBarBg] = theme.MenuBarBg;
            
            colors[(int)ImGuiCol.ScrollbarBg] = theme.ScrollbarBg;
            colors[(int)ImGuiCol.ScrollbarGrab] = theme.ScrollbarGrab;
            colors[(int)ImGuiCol.ScrollbarGrabHovered] = theme.ScrollbarGrabHovered;
            colors[(int)ImGuiCol.ScrollbarGrabActive] = theme.ScrollbarGrabActive;
            
            colors[(int)ImGuiCol.CheckMark] = theme.CheckMark;
            
            colors[(int)ImGuiCol.SliderGrab] = theme.SliderGrab;
            colors[(int)ImGuiCol.SliderGrabActive] = theme.SliderGrabActive;
            
            colors[(int)ImGuiCol.Button] = theme.Button;
            colors[(int)ImGuiCol.ButtonHovered] = theme.ButtonHovered;
            colors[(int)ImGuiCol.ButtonActive] = theme.ButtonActive;
            
            colors[(int)ImGuiCol.Header] = theme.Header;
            colors[(int)ImGuiCol.HeaderHovered] = theme.HeaderHovered;
            colors[(int)ImGuiCol.HeaderActive] = theme.HeaderActive;
            
            colors[(int)ImGuiCol.Separator] = theme.Separator;
            colors[(int)ImGuiCol.SeparatorHovered] = theme.SeparatorHovered;
            colors[(int)ImGuiCol.SeparatorActive] = theme.SeparatorActive;
            
            colors[(int)ImGuiCol.ResizeGrip] = theme.ResizeGrip;
            colors[(int)ImGuiCol.ResizeGripHovered] = theme.ResizeGripHovered;
            colors[(int)ImGuiCol.ResizeGripActive] = theme.ResizeGripActive;
            
            colors[(int)ImGuiCol.Tab] = theme.Tab;
            colors[(int)ImGuiCol.TabHovered] = theme.TabHovered;
            // Note: TabActive, TabUnfocused, TabUnfocusedActive not available in this ImGui.NET version
            // ImGui will use Tab for inactive and TabHovered for active tabs
            
            colors[(int)ImGuiCol.DockingPreview] = theme.DockingPreview;
            colors[(int)ImGuiCol.DockingEmptyBg] = theme.DockingEmptyBg;
            
            colors[(int)ImGuiCol.PlotLines] = new Vector4(0.61f, 0.61f, 0.61f, 1f);
            colors[(int)ImGuiCol.PlotLinesHovered] = new Vector4(1f, 0.43f, 0.35f, 1f);
            colors[(int)ImGuiCol.PlotHistogram] = new Vector4(0.90f, 0.70f, 0f, 1f);
            colors[(int)ImGuiCol.PlotHistogramHovered] = new Vector4(1f, 0.60f, 0f, 1f);
            
            colors[(int)ImGuiCol.TableHeaderBg] = theme.TableHeaderBg;
            colors[(int)ImGuiCol.TableBorderStrong] = theme.TableBorderStrong;
            colors[(int)ImGuiCol.TableBorderLight] = theme.TableBorderLight;
            colors[(int)ImGuiCol.TableRowBg] = theme.TableRowBg;
            colors[(int)ImGuiCol.TableRowBgAlt] = theme.TableRowBgAlt;
            
            colors[(int)ImGuiCol.DragDropTarget] = theme.DragDropTarget;
            
            // Note: NavHighlight not available in this ImGui version
            colors[(int)ImGuiCol.NavWindowingHighlight] = theme.NavWindowingHighlight;
            colors[(int)ImGuiCol.NavWindowingDimBg] = theme.NavWindowingDimBg;
            
            colors[(int)ImGuiCol.ModalWindowDimBg] = theme.ModalWindowDimBg;
            
            // Apply extended style variables from theme
            style.Alpha = theme.Alpha;
            style.DisabledAlpha = theme.DisabledAlpha;
            
            // Rounding
            style.WindowRounding = theme.WindowRounding;
            style.ChildRounding = theme.ChildRounding;
            style.FrameRounding = theme.FrameRounding;
            style.PopupRounding = theme.PopupRounding;
            style.ScrollbarRounding = theme.ScrollbarRounding;
            style.GrabRounding = theme.GrabRounding;
            style.TabRounding = theme.TabRounding;
            
            // Borders
            style.WindowBorderSize = theme.WindowBorderSize;
            style.ChildBorderSize = theme.ChildBorderSize;
            style.PopupBorderSize = theme.PopupBorderSize;
            style.FrameBorderSize = theme.FrameBorderSize;
            style.TabBorderSize = theme.TabBorderSize;
            
            // Padding & Spacing
            style.WindowPadding = theme.WindowPadding;
            style.FramePadding = theme.FramePadding;
            style.ItemSpacing = theme.ItemSpacing;
            style.ItemInnerSpacing = theme.ItemInnerSpacing;
            style.CellPadding = theme.CellPadding;
            
            // Sizes
            style.WindowMinSize = theme.WindowMinSize;
            style.IndentSpacing = theme.IndentSpacing;
            style.ScrollbarSize = theme.ScrollbarSize;
            style.GrabMinSize = theme.GrabMinSize;
            
            // Alignment
            style.WindowTitleAlign = theme.WindowTitleAlign;
            style.ButtonTextAlign = theme.ButtonTextAlign;
            style.SelectableTextAlign = theme.SelectableTextAlign;
            
            // Misc
            style.LogSliderDeadzone = theme.LogSliderDeadzone;
            style.TabMinWidthForCloseButton = theme.TabMinWidthForCloseButton;
            style.ColorButtonPosition = (ImGuiDir)((int)theme.ColorButtonPosition);
            
            // Update InspectorStyles to use current theme colors
            UpdateInspectorStyles();
        }
        
        /// <summary>
        /// Apply theme by name
        /// </summary>
        public static void ApplyThemeByName(string themeName)
        {
            var theme = BuiltInThemes.GetThemeByName(themeName);
            if (theme != null)
            {
                ApplyTheme(theme);
            }
            else
            {
                LogManager.LogWarning($"Theme '{themeName}' not found, using default theme.", "ThemeManager");
                ApplyTheme(BuiltInThemes.DarkUnity());
            }
        }
        
        /// <summary>
        /// Update InspectorStyles to use current theme colors
        /// </summary>
        private static void UpdateInspectorStyles()
        {
            // FIXED: Sync UITheme with EditorTheme for complete consistency
            // UI.Section should match Header colors (used for collapsible sections)
            _uiTheme.Section = CurrentTheme.Header;
            _uiTheme.SectionHovered = CurrentTheme.HeaderHovered;
            _uiTheme.SectionActive = CurrentTheme.HeaderActive;
            
            // Semantic colors
            _uiTheme.Primary = CurrentTheme.AccentColor;
            _uiTheme.Success = CurrentTheme.InspectorSuccess;
            _uiTheme.Warning = CurrentTheme.InspectorWarning;
            _uiTheme.Error = CurrentTheme.InspectorError;
            _uiTheme.Info = CurrentTheme.InspectorInfo;
            
            // Text colors
            _uiTheme.TextLabel = CurrentTheme.InspectorLabel;
            _uiTheme.TextValue = CurrentTheme.InspectorValue;
            
            // Gradients
            _uiTheme.GradientStart = CurrentTheme.GradientStart;
            _uiTheme.GradientEnd = CurrentTheme.GradientEnd;
            
            // Update InspectorColors to match current theme
            InspectorColors.Label = CurrentTheme.InspectorLabel;
            InspectorColors.LabelDisabled = new Vector4(
                CurrentTheme.InspectorLabel.X * 0.6f,
                CurrentTheme.InspectorLabel.Y * 0.6f,
                CurrentTheme.InspectorLabel.Z * 0.6f,
                CurrentTheme.InspectorLabel.W
            );
            InspectorColors.Value = CurrentTheme.InspectorValue;
            InspectorColors.Warning = CurrentTheme.InspectorWarning;
            InspectorColors.Error = CurrentTheme.InspectorError;
            InspectorColors.Success = CurrentTheme.InspectorSuccess;
            InspectorColors.Info = CurrentTheme.InspectorInfo;
            InspectorColors.Section = CurrentTheme.InspectorSection;
            
            // Update button states
            InspectorColors.Button = CurrentTheme.Button;
            InspectorColors.ButtonHovered = CurrentTheme.ButtonHovered;
            InspectorColors.ButtonActive = CurrentTheme.ButtonActive;
            
            // DropZone uses accent color with transparency
            InspectorColors.DropZone = new Vector4(
                CurrentTheme.AccentColor.X,
                CurrentTheme.AccentColor.Y,
                CurrentTheme.AccentColor.Z,
                0.2f
            );
        }
        
        /// <summary>
        /// Initialize theme system (call on editor startup)
        /// </summary>
        public static void Initialize(string? themeName = null)
        {
            if (!string.IsNullOrEmpty(themeName))
            {
                ApplyThemeByName(themeName);
            }
            else
            {
                // Apply default theme
                ApplyTheme(BuiltInThemes.DarkUnity());
            }
        }
        
        /// <summary>
        /// Get a gradient color between GradientStart and GradientEnd
        /// </summary>
        /// <param name="t">Interpolation value (0-1)</param>
        public static Vector4 GetGradientColor(float t)
        {
            var start = CurrentTheme.GradientStart;
            var end = CurrentTheme.GradientEnd;
            
            return new Vector4(
                start.X + (end.X - start.X) * t,
                start.Y + (end.Y - start.Y) * t,
                start.Z + (end.Z - start.Z) * t,
                start.W + (end.W - start.W) * t
            );
        }
        
        /// <summary>
        /// Draw a gradient header (for panels, sections, etc.)
        /// </summary>
        public static void DrawGradientHeader(string label, Vector2 size)
        {
            var drawList = ImGui.GetWindowDrawList();
            var pos = ImGui.GetCursorScreenPos();
            
            var colStart = ImGui.ColorConvertFloat4ToU32(CurrentTheme.GradientStart);
            var colEnd = ImGui.ColorConvertFloat4ToU32(CurrentTheme.GradientEnd);
            
            drawList.AddRectFilledMultiColor(
                pos,
                new Vector2(pos.X + size.X, pos.Y + size.Y),
                colStart,
                colEnd,
                colEnd,
                colStart
            );
            
            // Use the left gradient color (GradientStart) for text contrast since text is on the left
            // This ensures better readability as the text aligns with the start of the gradient
            var textColor = CalculateTextColorForBackground(CurrentTheme.GradientStart);
            
            // Add text centered with dynamic color
            ImGui.SetCursorScreenPos(new Vector2(pos.X + 10, pos.Y + (size.Y - ImGui.GetTextLineHeight()) * 0.5f));
            ImGui.PushStyleColor(ImGuiCol.Text, textColor);
            ImGui.TextUnformatted(label);
            ImGui.PopStyleColor();
            
            // Advance cursor
            ImGui.SetCursorScreenPos(new Vector2(pos.X, pos.Y + size.Y));
        }
        
        /// <summary>
        /// Draw a glassmorphism panel (background with blur effect simulation)
        /// </summary>
        public static void DrawGlassPanel(Vector2 pos, Vector2 size, float rounding = 15.0f, float alpha = 0.8f)
        {
            var drawList = ImGui.GetWindowDrawList();
            
            // Background with alpha
            var bgColor = CurrentTheme.WindowBackground;
            bgColor.W = alpha;
            var colBg = ImGui.ColorConvertFloat4ToU32(bgColor);
            
            drawList.AddRectFilled(pos, new Vector2(pos.X + size.X, pos.Y + size.Y), colBg, rounding);
            
            // Border
            var borderColor = CurrentTheme.Border;
            var colBorder = ImGui.ColorConvertFloat4ToU32(borderColor);
            drawList.AddRect(pos, new Vector2(pos.X + size.X, pos.Y + size.Y), colBorder, rounding, ImDrawFlags.None, 1.0f);
        }
    }
}

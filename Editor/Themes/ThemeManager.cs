using System;
using System.Numerics;
using ImGuiNET;
using Editor.Inspector;

namespace Editor.Themes
{
    /// <summary>
    /// Manages theme application and switching for the editor
    /// </summary>
    public static class ThemeManager
    {
        private static EditorTheme? _currentTheme;
        
        /// <summary>
        /// Currently active theme
        /// </summary>
        public static EditorTheme CurrentTheme
        {
            get
            {
                if (_currentTheme == null)
                    _currentTheme = BuiltInThemes.DarkUnity();
                return _currentTheme;
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
            
            // Apply all colors
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
            // Note: TabActive, TabUnfocused, TabUnfocusedActive not available in this ImGui version
            // Using Tab and TabHovered colors instead
            
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
            
            // Apply style variables (rounding, spacing, etc.)
            style.WindowRounding = theme.WindowRounding;
            style.ChildRounding = theme.ChildRounding;
            style.FrameRounding = theme.FrameRounding;
            style.PopupRounding = theme.PopupRounding;
            style.ScrollbarRounding = theme.ScrollbarRounding;
            style.GrabRounding = theme.GrabRounding;
            style.TabRounding = theme.TabRounding;
            
            style.WindowBorderSize = 1.0f;
            style.ChildBorderSize = 1.0f;
            style.PopupBorderSize = 1.0f;
            style.FrameBorderSize = 0.0f;
            style.TabBorderSize = 0.0f;
            
            style.WindowPadding = new Vector2(12, 12);
            style.FramePadding = new Vector2(8, 4);
            style.ItemSpacing = new Vector2(8, 4);
            style.ItemInnerSpacing = new Vector2(4, 4);
            style.IndentSpacing = 21.0f;
            style.ScrollbarSize = 16.0f;
            style.GrabMinSize = 10.0f;
            
            style.Alpha = theme.Alpha;
            style.DisabledAlpha = theme.DisabledAlpha;
            
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
                Console.WriteLine($"Theme '{themeName}' not found, using default theme.");
                ApplyTheme(BuiltInThemes.DarkUnity());
            }
        }
        
        /// <summary>
        /// Update InspectorStyles to use current theme colors
        /// </summary>
        private static void UpdateInspectorStyles()
        {
            // Update InspectorColors to match current theme
            // This will be called after theme change to update inspector widgets
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
            
            // Add text centered
            ImGui.SetCursorScreenPos(new Vector2(pos.X + 10, pos.Y + (size.Y - ImGui.GetTextLineHeight()) * 0.5f));
            ImGui.TextUnformatted(label);
            
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

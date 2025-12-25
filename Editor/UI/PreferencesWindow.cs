using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using ImGuiNET;
using Editor.Themes;
using Editor.State;
using Editor.Logging;
using Editor.Utils;

namespace Editor.UI
{
    /// <summary>
    /// Preferences window - Unity-style editor preferences
    /// </summary>
    public class PreferencesWindow
    {
        private bool _isOpen = false;
        private string _selectedCategory = "Appearance";
        private readonly List<string> _categories = new List<string>
        {
            "Appearance",
            "External Tools",
            "Shortcuts",
            "Input",
            "Editor",
            "Scene View",
            "Grid & Snap"
        };
        
        private string _selectedThemeName = "Dark Unity";
        private string _previewThemeName = "";
        
        // Font settings
        private int _selectedFontIndex = 0;
        private float _selectedFontSize = 14f;
        private List<FontManager.FontInfo> _availableFonts = new();
        private string _fontSearchFilter = "";
        
        // Google Fonts integration
        private List<GoogleFontsManager.GoogleFontInfo> _googleFonts = new();
        private string _googleFontSearch = "";
        private GoogleFontsManager.GoogleFontInfo? _selectedGoogleFont = null;
        private bool _isFetchingGoogleFonts = false;
        private Dictionary<string, bool> _downloadingFonts = new();
        
        // Style customization tracking
        private bool _hasUnsavedStyleChanges = false;
        
        /// <summary>
        /// Open the preferences window
        /// </summary>
        public void Open()
        {
            _isOpen = true;
            _selectedThemeName = ThemeManager.CurrentTheme.Name;
            _previewThemeName = _selectedThemeName;

            // Load available fonts from system
            _availableFonts = FontManager.GetAvailableFonts();
            LogManager.LogInfo($"Loaded {_availableFonts.Count} system fonts", "Preferences");
            
            // Initialize Google Fonts
            GoogleFontsManager.Initialize();
            _googleFonts = GoogleFontsManager.GetPopularFonts(200);
            LogManager.LogInfo($"Loaded {_googleFonts.Count} Google Fonts", "Preferences");

            // Load font settings from EditorSettings
            string savedFont = EditorSettings.InterfaceFont;
            _selectedFontSize = EditorSettings.InterfaceFontSize;

            // Find the index of the saved font by display name
            _selectedFontIndex = _availableFonts.FindIndex(f => f.DisplayName == savedFont);
            if (_selectedFontIndex == -1)
            {
                // Try to find by family name (backward compatibility)
                _selectedFontIndex = _availableFonts.FindIndex(f => f.Family == savedFont);
            }
            if (_selectedFontIndex == -1)
            {
                _selectedFontIndex = 0; // Fallback to default
            }
        }
        
        /// <summary>
        /// Close the preferences window
        /// </summary>
        public void Close()
        {
            _isOpen = false;
        }
        
        /// <summary>
        /// Toggle preferences window
        /// </summary>
        public void Toggle()
        {
            if (_isOpen)
                Close();
            else
                Open();
        }
        
        /// <summary>
        /// Is the window currently open?
        /// </summary>
        public bool IsOpen => _isOpen;
        
        /// <summary>
        /// Draw the preferences window
        /// </summary>
        public void Draw()
        {
            if (!_isOpen)
                return;
            
            // Center the window on first appearance
            var viewport = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(new Vector2(viewport.Size.X * 0.5f, viewport.Size.Y * 0.5f), ImGuiCond.FirstUseEver, new Vector2(0.5f, 0.5f));
            ImGui.SetNextWindowSize(new Vector2(900, 600), ImGuiCond.FirstUseEver);
            
            var windowFlags = ImGuiWindowFlags.NoCollapse;
            
            if (ImGui.Begin("Preferences", ref _isOpen, windowFlags))
            {
                // Apply window defaults (wrapping) so descriptive texts don't overflow
                UIHelpers.BeginWindowDefaults();

                // Cache current theme for this Draw call
                var currentTheme = ThemeManager.CurrentTheme;
                DrawContent(currentTheme);

                // Pop wrapping defaults
                UIHelpers.EndWindowDefaults();
            }
            ImGui.End();
            
            // If window was closed, revert preview
            if (!_isOpen && _previewThemeName != _selectedThemeName)
            {
                ThemeManager.ApplyThemeByName(_selectedThemeName);
            }
        }
        
        private void DrawContent(Editor.Themes.EditorTheme currentTheme)
        {
            var avail = ImGui.GetContentRegionAvail();
            
            // Left sidebar - Categories (200px width)
            ImGui.BeginChild("Categories", new Vector2(200, avail.Y), ImGuiChildFlags.Borders);
            DrawCategories(currentTheme);
            ImGui.EndChild();
            
            ImGui.SameLine();
            
            // Right panel - Settings content
            ImGui.BeginChild("Settings", new Vector2(avail.X - 205, avail.Y), ImGuiChildFlags.Borders);
            DrawSettingsPanel(currentTheme);
            ImGui.EndChild();
        }
        
        private void DrawCategories(Editor.Themes.EditorTheme currentTheme)
        {
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(12, 8));
            
            foreach (var category in _categories)
            {
                bool isSelected = _selectedCategory == category;
                
                // Use ThemedImGui.ButtonColored for proper text contrast
                Vector4 buttonColor = isSelected ? currentTheme.ButtonActive : currentTheme.Button;
                
                if (ThemedImGui.ButtonColored(category, buttonColor, new Vector2(-1, 0)))
                {
                    _selectedCategory = category;
                }
                
                ImGui.Spacing();
            }
            
            ImGui.PopStyleVar(2);
        }
        
        private void DrawSettingsPanel(Editor.Themes.EditorTheme currentTheme)
        {
            switch (_selectedCategory)
            {
                case "Appearance":
                    DrawAppearanceSettings(currentTheme);
                    break;
                case "External Tools":
                    DrawExternalToolsSettings(currentTheme);
                    break;
                case "Shortcuts":
                    DrawShortcutsSettings(currentTheme);
                    break;
                case "Input":
                    DrawInputSettings(currentTheme);
                    break;
                case "Editor":
                    DrawEditorSettings(currentTheme);
                    break;
                case "Scene View":
                    DrawSceneViewSettings(currentTheme);
                    break;
                case "Grid & Snap":
                    DrawGridSnapSettings(currentTheme);
                    break;
            }
        }
        
        private void DrawAppearanceSettings(Editor.Themes.EditorTheme currentTheme)
        {
            var gradient = currentTheme.GradientStart;
            var accentColor = currentTheme.AccentColor;
            
            // Header with gradient (like your HTML design)
            ThemeManager.DrawGradientHeader("? Appearance", new Vector2(ImGui.GetContentRegionAvail().X, 50));
            
            ImGui.Spacing();
            ImGui.Spacing();

            // Use the full settings area (no external two-column split) so sections align left like Theme
            
            // Theme Section
            ImGui.PushStyleColor(ImGuiCol.Text, currentTheme.InspectorSection);
            ImGui.TextUnformatted("Theme");
            ImGui.PopStyleColor();
            ImGui.Separator();
            ImGui.Spacing();
            
            // Description
            ImGui.PushStyleColor(ImGuiCol.Text, currentTheme.TextDisabled);
            ImGui.TextWrapped("Choose a theme for the editor. Glassmorphism themes feature modern gradients and transparent effects.");
            ImGui.PopStyleColor();
            ImGui.Spacing();
            ImGui.Spacing();
            
            // Theme Selection
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted("Theme:");
            ImGui.SameLine(150);
            ImGui.SetNextItemWidth(300);
            
            var allThemes = BuiltInThemes.GetAllThemes();
            int currentIndex = allThemes.FindIndex(t => t.Name == _previewThemeName);
            if (currentIndex == -1) currentIndex = 0;
            
            if (ImGui.BeginCombo("##Theme", allThemes[currentIndex].Name))
            {
                for (int i = 0; i < allThemes.Count; i++)
                {
                    var theme = allThemes[i];
                    bool isSelected = _previewThemeName == theme.Name;
                    
                    // Draw color swatch
                    var pos = ImGui.GetCursorScreenPos();
                    var drawList = ImGui.GetWindowDrawList();
                    
                    // Gradient preview (20x20 px)
                    var colStart = ImGui.ColorConvertFloat4ToU32(theme.GradientStart);
                    var colEnd = ImGui.ColorConvertFloat4ToU32(theme.GradientEnd);
                    drawList.AddRectFilledMultiColor(
                        pos,
                        new Vector2(pos.X + 20, pos.Y + 20),
                        colStart,
                        colEnd,
                        colEnd,
                        colStart
                    );
                    drawList.AddRect(pos, new Vector2(pos.X + 20, pos.Y + 20), ImGui.ColorConvertFloat4ToU32(theme.Border));
                    
                    ImGui.Dummy(new Vector2(24, 20));
                    ImGui.SameLine();
                    
                    if (ImGui.Selectable(theme.Name, isSelected))
                    {
                        _previewThemeName = theme.Name;
                        // Apply preview
                        ThemeManager.ApplyThemeByName(_previewThemeName);
                    }
                    
                    if (isSelected)
                        ImGui.SetItemDefaultFocus();
                    
                    // Show description on hover
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.TextUnformatted(theme.Description);
                        ImGui.EndTooltip();
                    }
                }
                ImGui.EndCombo();
            }
            
            ImGui.Spacing();
            
            // Theme preview panel - auto-sizing to avoid scrollbar
            var previewTheme = BuiltInThemes.GetThemeByName(_previewThemeName);
            if (previewTheme != null)
            {
                ImGui.BeginChild("ThemePreview", new Vector2(0, 0), ImGuiChildFlags.Borders | ImGuiChildFlags.AutoResizeY);
                
                ImGui.PushStyleColor(ImGuiCol.Text, currentTheme.InspectorSection);
                ImGui.TextUnformatted("Theme Preview");
                ImGui.PopStyleColor();
                ImGui.Separator();
                ImGui.Spacing();
                
                // Show theme info
                ImGui.TextUnformatted($"Name: {previewTheme.Name}");
                ImGui.TextUnformatted($"Description: {previewTheme.Description}");
                ImGui.Spacing();
                
                // Color swatches
                ImGui.TextUnformatted("Color Palette:");
                ImGui.Spacing();
                
                DrawColorSwatch("Gradient Start", previewTheme.GradientStart, previewTheme.Border);
                ImGui.SameLine();
                DrawColorSwatch("Gradient End", previewTheme.GradientEnd, previewTheme.Border);
                ImGui.SameLine();
                DrawColorSwatch("Accent", previewTheme.AccentColor, previewTheme.Border);
                
                ImGui.Spacing();
                DrawColorSwatch("Background", previewTheme.WindowBackground, previewTheme.Border);
                ImGui.SameLine();
                DrawColorSwatch("Frame", previewTheme.FrameBg, previewTheme.Border);
                ImGui.SameLine();
                DrawColorSwatch("Button", previewTheme.Button, previewTheme.Border);
                
                ImGui.Spacing();
                ImGui.TextUnformatted("Sample UI:");
                if (ImGui.Button("Sample Button"))
                {
                    // Nothing
                }
                ImGui.SameLine();
                bool checkbox = false;
                ImGui.Checkbox("Sample Checkbox", ref checkbox);
                
                ImGui.EndChild();
            }
            
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Theme Apply/Reset buttons - aligned to the right
            float buttonWidth = 100f;
            float spacing = ImGui.GetStyle().ItemSpacing.X;
            float totalButtonWidth = buttonWidth * 2 + spacing;
            float availWidth = ImGui.GetContentRegionAvail().X;
            float indentAmount = availWidth - totalButtonWidth;

            if (indentAmount > 0)
            {
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + indentAmount);
            }

                if (ImGui.Button("Apply##Theme", new Vector2(buttonWidth, 0)))
            {
                // Apply the current preview theme
                _selectedThemeName = _previewThemeName;
                SaveThemeToSettings(_selectedThemeName);
                ThemeManager.ApplyThemeByName(_selectedThemeName);
                    LogManager.LogInfo($"Theme '{_selectedThemeName}' applied and saved.", "Preferences");
            }
            ImGui.SameLine();
                if (ImGui.Button("Reset##Theme", new Vector2(buttonWidth, 0)))
            {
                // Reset to the last saved theme
                _previewThemeName = _selectedThemeName;
                ThemeManager.ApplyThemeByName(_selectedThemeName);
                    LogManager.LogInfo($"Theme reset to '{_selectedThemeName}'.", "Preferences");
            }

            ImGui.Spacing();
            ImGui.Spacing();

            // Interface Font - Modern UI with tabs
            ImGui.PushStyleColor(ImGuiCol.Text, currentTheme.InspectorSection);
            ImGui.TextUnformatted("Interface Font");
            ImGui.PopStyleColor();
            ImGui.Separator();
            ImGui.Spacing();

            // Font source tabs
            if (ImGui.BeginTabBar("FontSourceTabs"))
            {
                if (ThemedImGui.BeginTabItem("System Fonts"))
                {
                    DrawSystemFontsTab(currentTheme);
                    ImGui.EndTabItem();
                }

                if (ThemedImGui.BeginTabItem("Google Fonts"))
                {
                    DrawGoogleFontsTab(currentTheme);
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }
            
            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.Spacing();
            
            // Style Customization Section
            ImGui.PushStyleColor(ImGuiCol.Text, currentTheme.InspectorSection);
            ImGui.TextUnformatted("🎨 Style Customization");
            ImGui.PopStyleColor();
            ImGui.Separator();
            ImGui.Spacing();
            
            ImGui.PushStyleColor(ImGuiCol.Text, currentTheme.TextDisabled);
            ImGui.TextWrapped("Customize the look and feel of the editor interface. Changes are applied immediately.");
            ImGui.PopStyleColor();
            ImGui.Spacing();
            
            DrawStyleCustomization(currentTheme);
        }
        
        private void DrawStyleCustomization(Editor.Themes.EditorTheme currentTheme)
        {
            // Collapsible sections for different style categories
            
            // Rounding Section
            if (ImGui.CollapsingHeader("Corner Rounding", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Indent();
                
                float windowRounding = currentTheme.WindowRounding;
                if (ImGui.SliderFloat("Window Rounding", ref windowRounding, 0f, 24f, "%.1f"))
                {
                    currentTheme.WindowRounding = windowRounding;
                    ThemeManager.ApplyTheme(currentTheme);
                    _hasUnsavedStyleChanges = true;
                }
                
                float childRounding = currentTheme.ChildRounding;
                if (ImGui.SliderFloat("Child Rounding", ref childRounding, 0f, 24f, "%.1f"))
                {
                    currentTheme.ChildRounding = childRounding;
                    ThemeManager.ApplyTheme(currentTheme);
                    _hasUnsavedStyleChanges = true;
                }
                
                float frameRounding = currentTheme.FrameRounding;
                if (ImGui.SliderFloat("Frame Rounding", ref frameRounding, 0f, 24f, "%.1f"))
                {
                    currentTheme.FrameRounding = frameRounding;
                    ThemeManager.ApplyTheme(currentTheme);
                    _hasUnsavedStyleChanges = true;
                }
                
                float popupRounding = currentTheme.PopupRounding;
                if (ImGui.SliderFloat("Popup Rounding", ref popupRounding, 0f, 24f, "%.1f"))
                {
                    currentTheme.PopupRounding = popupRounding;
                    ThemeManager.ApplyTheme(currentTheme);
                    _hasUnsavedStyleChanges = true;
                }
                
                float scrollbarRounding = currentTheme.ScrollbarRounding;
                if (ImGui.SliderFloat("Scrollbar Rounding", ref scrollbarRounding, 0f, 24f, "%.1f"))
                {
                    currentTheme.ScrollbarRounding = scrollbarRounding;
                    ThemeManager.ApplyTheme(currentTheme);
                    _hasUnsavedStyleChanges = true;
                }
                
                float grabRounding = currentTheme.GrabRounding;
                if (ImGui.SliderFloat("Grab Rounding", ref grabRounding, 0f, 24f, "%.1f"))
                {
                    currentTheme.GrabRounding = grabRounding;
                    ThemeManager.ApplyTheme(currentTheme);
                    _hasUnsavedStyleChanges = true;
                }
                
                float tabRounding = currentTheme.TabRounding;
                if (ImGui.SliderFloat("Tab Rounding", ref tabRounding, 0f, 24f, "%.1f"))
                {
                    currentTheme.TabRounding = tabRounding;
                    ThemeManager.ApplyTheme(currentTheme);
                    _hasUnsavedStyleChanges = true;
                }
                
                ImGui.Unindent();
                ImGui.Spacing();
            }
            
            // Borders Section
            if (ImGui.CollapsingHeader("Border Sizes"))
            {
                ImGui.Indent();
                
                float windowBorderSize = currentTheme.WindowBorderSize;
                if (ImGui.SliderFloat("Window Border", ref windowBorderSize, 0f, 5f, "%.1f"))
                {
                    currentTheme.WindowBorderSize = windowBorderSize;
                    ThemeManager.ApplyTheme(currentTheme);
                    _hasUnsavedStyleChanges = true;
                }
                
                float childBorderSize = currentTheme.ChildBorderSize;
                if (ImGui.SliderFloat("Child Border", ref childBorderSize, 0f, 5f, "%.1f"))
                {
                    currentTheme.ChildBorderSize = childBorderSize;
                    ThemeManager.ApplyTheme(currentTheme);
                    _hasUnsavedStyleChanges = true;
                }
                
                float popupBorderSize = currentTheme.PopupBorderSize;
                if (ImGui.SliderFloat("Popup Border", ref popupBorderSize, 0f, 5f, "%.1f"))
                {
                    currentTheme.PopupBorderSize = popupBorderSize;
                    ThemeManager.ApplyTheme(currentTheme);
                    _hasUnsavedStyleChanges = true;
                }
                
                float frameBorderSize = currentTheme.FrameBorderSize;
                if (ImGui.SliderFloat("Frame Border", ref frameBorderSize, 0f, 5f, "%.1f"))
                {
                    currentTheme.FrameBorderSize = frameBorderSize;
                    ThemeManager.ApplyTheme(currentTheme);
                    _hasUnsavedStyleChanges = true;
                }
                
                float tabBorderSize = currentTheme.TabBorderSize;
                if (ImGui.SliderFloat("Tab Border", ref tabBorderSize, 0f, 5f, "%.1f"))
                {
                    currentTheme.TabBorderSize = tabBorderSize;
                    ThemeManager.ApplyTheme(currentTheme);
                    _hasUnsavedStyleChanges = true;
                }
                
                ImGui.Unindent();
                ImGui.Spacing();
            }
            
            // Padding & Spacing Section
            if (ImGui.CollapsingHeader("Padding & Spacing"))
            {
                ImGui.Indent();
                
                var windowPadding = currentTheme.WindowPadding;
                if (ImGui.DragFloat2("Window Padding", ref windowPadding, 0.1f, 0f, 32f, "%.1f"))
                {
                    currentTheme.WindowPadding = windowPadding;
                    ThemeManager.ApplyTheme(currentTheme);
                    _hasUnsavedStyleChanges = true;
                }
                
                var framePadding = currentTheme.FramePadding;
                if (ImGui.DragFloat2("Frame Padding", ref framePadding, 0.1f, 0f, 32f, "%.1f"))
                {
                    currentTheme.FramePadding = framePadding;
                    ThemeManager.ApplyTheme(currentTheme);
                    _hasUnsavedStyleChanges = true;
                }
                
                var itemSpacing = currentTheme.ItemSpacing;
                if (ImGui.DragFloat2("Item Spacing", ref itemSpacing, 0.1f, 0f, 32f, "%.1f"))
                {
                    currentTheme.ItemSpacing = itemSpacing;
                    ThemeManager.ApplyTheme(currentTheme);
                    _hasUnsavedStyleChanges = true;
                }
                
                var itemInnerSpacing = currentTheme.ItemInnerSpacing;
                if (ImGui.DragFloat2("Item Inner Spacing", ref itemInnerSpacing, 0.1f, 0f, 32f, "%.1f"))
                {
                    currentTheme.ItemInnerSpacing = itemInnerSpacing;
                    ThemeManager.ApplyTheme(currentTheme);
                    _hasUnsavedStyleChanges = true;
                }
                
                var cellPadding = currentTheme.CellPadding;
                if (ImGui.DragFloat2("Cell Padding", ref cellPadding, 0.1f, 0f, 32f, "%.1f"))
                {
                    currentTheme.CellPadding = cellPadding;
                    ThemeManager.ApplyTheme(currentTheme);
                    _hasUnsavedStyleChanges = true;
                }
                
                float indentSpacing = currentTheme.IndentSpacing;
                if (ImGui.SliderFloat("Indent Spacing", ref indentSpacing, 0f, 50f, "%.1f"))
                {
                    currentTheme.IndentSpacing = indentSpacing;
                    ThemeManager.ApplyTheme(currentTheme);
                    _hasUnsavedStyleChanges = true;
                }
                
                ImGui.Unindent();
                ImGui.Spacing();
            }
            
            // Sizes Section
            if (ImGui.CollapsingHeader("Element Sizes"))
            {
                ImGui.Indent();
                
                var windowMinSize = currentTheme.WindowMinSize;
                if (ImGui.DragFloat2("Window Min Size", ref windowMinSize, 1f, 16f, 256f, "%.0f"))
                {
                    currentTheme.WindowMinSize = windowMinSize;
                    ThemeManager.ApplyTheme(currentTheme);
                    _hasUnsavedStyleChanges = true;
                }
                
                float scrollbarSize = currentTheme.ScrollbarSize;
                if (ImGui.SliderFloat("Scrollbar Size", ref scrollbarSize, 8f, 32f, "%.1f"))
                {
                    currentTheme.ScrollbarSize = scrollbarSize;
                    ThemeManager.ApplyTheme(currentTheme);
                    _hasUnsavedStyleChanges = true;
                }
                
                float grabMinSize = currentTheme.GrabMinSize;
                if (ImGui.SliderFloat("Grab Min Size", ref grabMinSize, 4f, 32f, "%.1f"))
                {
                    currentTheme.GrabMinSize = grabMinSize;
                    ThemeManager.ApplyTheme(currentTheme);
                    _hasUnsavedStyleChanges = true;
                }
                
                ImGui.Unindent();
                ImGui.Spacing();
            }
            
            // Alignment Section
            if (ImGui.CollapsingHeader("Alignment"))
            {
                ImGui.Indent();
                
                var windowTitleAlign = currentTheme.WindowTitleAlign;
                if (ImGui.DragFloat2("Window Title Align", ref windowTitleAlign, 0.01f, 0f, 1f, "%.2f"))
                {
                    currentTheme.WindowTitleAlign = windowTitleAlign;
                    ThemeManager.ApplyTheme(currentTheme);
                    _hasUnsavedStyleChanges = true;
                }
                
                var buttonTextAlign = currentTheme.ButtonTextAlign;
                if (ImGui.DragFloat2("Button Text Align", ref buttonTextAlign, 0.01f, 0f, 1f, "%.2f"))
                {
                    currentTheme.ButtonTextAlign = buttonTextAlign;
                    ThemeManager.ApplyTheme(currentTheme);
                    _hasUnsavedStyleChanges = true;
                }
                
                var selectableTextAlign = currentTheme.SelectableTextAlign;
                if (ImGui.DragFloat2("Selectable Text Align", ref selectableTextAlign, 0.01f, 0f, 1f, "%.2f"))
                {
                    currentTheme.SelectableTextAlign = selectableTextAlign;
                    ThemeManager.ApplyTheme(currentTheme);
                    _hasUnsavedStyleChanges = true;
                }
                
                ImGui.Unindent();
                ImGui.Spacing();
            }
            
            // Reset button
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            
            if (_hasUnsavedStyleChanges)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.7f, 0.2f, 1f));
                ImGui.TextUnformatted("⚠ You have unsaved style changes");
                ImGui.PopStyleColor();
                ImGui.Spacing();
            }
            
            float buttonWidth = 150f;
            float spacing = ImGui.GetStyle().ItemSpacing.X;
            float totalWidth = buttonWidth * 2 + spacing;
            float availWidth = ImGui.GetContentRegionAvail().X;
            float indent = Math.Max(0, availWidth - totalWidth);
            
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + indent);
            
            if (ImGui.Button("Reset to Defaults", new Vector2(buttonWidth, 0)))
            {
                ResetStyleToDefaults(currentTheme);
            }
            
            ImGui.SameLine();
            
            if (ImGui.Button("Save Style", new Vector2(buttonWidth, 0)))
            {
                SaveCurrentTheme(currentTheme);
                _hasUnsavedStyleChanges = false;
            }
        }
        
        private void ResetStyleToDefaults(Editor.Themes.EditorTheme theme)
        {
            // Reset to default values
            theme.WindowRounding = 12.0f;
            theme.ChildRounding = 8.0f;
            theme.FrameRounding = 6.0f;
            theme.PopupRounding = 8.0f;
            theme.ScrollbarRounding = 9.0f;
            theme.GrabRounding = 6.0f;
            theme.TabRounding = 8.0f;
            
            theme.WindowBorderSize = 1.0f;
            theme.ChildBorderSize = 1.0f;
            theme.PopupBorderSize = 1.0f;
            theme.FrameBorderSize = 0.0f;
            theme.TabBorderSize = 0.0f;
            
            theme.WindowPadding = new Vector2(8, 8);
            theme.FramePadding = new Vector2(5, 4);
            theme.ItemSpacing = new Vector2(8, 4);
            theme.ItemInnerSpacing = new Vector2(4, 4);
            theme.CellPadding = new Vector2(4, 2);
            
            theme.WindowMinSize = new Vector2(32, 32);
            theme.IndentSpacing = 21.0f;
            theme.ScrollbarSize = 14.0f;
            theme.GrabMinSize = 12.0f;
            
            theme.WindowTitleAlign = new Vector2(0.0f, 0.5f);
            theme.ButtonTextAlign = new Vector2(0.5f, 0.5f);
            theme.SelectableTextAlign = new Vector2(0.0f, 0.0f);
            
            ThemeManager.ApplyTheme(theme);
            _hasUnsavedStyleChanges = true;
            
            LogManager.LogInfo("Style reset to default values", "Preferences");
        }
        
        private void SaveCurrentTheme(Editor.Themes.EditorTheme theme)
        {
            // Save the theme with its customized style values
            SaveThemeToSettings(theme.Name);
            LogManager.LogInfo($"Style customization saved to theme '{theme.Name}'", "Preferences");
        }
        
        private void DrawSystemFontsTab(Editor.Themes.EditorTheme currentTheme)
        {
            ImGui.Spacing();
            
            ImGui.PushStyleColor(ImGuiCol.Text, currentTheme.TextDisabled);
            ImGui.TextWrapped($"{_availableFonts.Count} system fonts available");
            ImGui.PopStyleColor();
            ImGui.Spacing();

            // Font search filter
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted("Search:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##FontSearch", "Type to filter fonts...", ref _fontSearchFilter, 256);
            ImGui.Spacing();

            // Left side: Font list (60% width)
            float availWidth = ImGui.GetContentRegionAvail().X;
            float listWidth = MathF.Floor(availWidth * 0.58f);
            float spacing = ImGui.GetStyle().ItemSpacing.X;
            float previewWidth = availWidth - listWidth - spacing;
            
            // Ensure minimum widths to prevent overlap
            if (listWidth < 200f) listWidth = 200f;
            if (previewWidth < 250f) previewWidth = 250f;

            ImGui.BeginChild("SystemFontsList", new Vector2(listWidth, 400), ImGuiChildFlags.Borders);
            
            var filteredFonts = string.IsNullOrWhiteSpace(_fontSearchFilter)
                ? _availableFonts
                : _availableFonts.Where(f =>
                    f.DisplayName.Contains(_fontSearchFilter, StringComparison.OrdinalIgnoreCase) ||
                    f.Family.Contains(_fontSearchFilter, StringComparison.OrdinalIgnoreCase)
                ).ToList();

            if (filteredFonts.Count == 0)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, currentTheme.TextDisabled);
                ImGui.TextUnformatted("No fonts match your search");
                ImGui.PopStyleColor();
            }
            else
            {
                for (int i = 0; i < filteredFonts.Count; i++)
                {
                    var font = filteredFonts[i];
                    int actualIndex = _availableFonts.IndexOf(font);
                    bool isSelected = _selectedFontIndex == actualIndex;

                    // Use unique ID based on actual index to avoid duplicates
                    ImGui.PushID(actualIndex);
                    
                    if (ImGui.Selectable(font.DisplayName, isSelected))
                    {
                        _selectedFontIndex = actualIndex;
                        _fontSearchFilter = "";
                    }

                    if (isSelected)
                        ImGui.SetItemDefaultFocus();

                    if (ImGui.IsItemHovered() && !string.IsNullOrEmpty(font.FilePath))
                    {
                        ImGui.BeginTooltip();
                        ImGui.PushStyleColor(ImGuiCol.Text, currentTheme.TextDisabled);
                        ImGui.TextUnformatted($"File: {font.FilePath}");
                        ImGui.PopStyleColor();
                        ImGui.EndTooltip();
                    }
                    
                    ImGui.PopID();
                }
            }
            
            ImGui.EndChild();
            
            ImGui.SameLine();
            
            // Right side: Preview and controls
            ImGui.BeginGroup();
            DrawFontPreview(currentTheme, previewWidth);
            ImGui.Spacing();
            DrawFontSizeControls();
            ImGui.Spacing();
            DrawFontApplyButtons(currentTheme, false);
            ImGui.EndGroup();
        }
        
        private void DrawGoogleFontsTab(Editor.Themes.EditorTheme currentTheme)
        {
            ImGui.Spacing();
            
            // Header with refresh button
            ImGui.PushStyleColor(ImGuiCol.Text, currentTheme.TextDisabled);
            ImGui.TextWrapped($"{_googleFonts.Count} Google Fonts available ({_googleFonts.Count(f => f.IsDownloaded)} downloaded)");
            ImGui.PopStyleColor();
            
            ImGui.SameLine();
            if (_isFetchingGoogleFonts)
            {
                ImGui.TextColored(new Vector4(0.4f, 0.8f, 1f, 1f), "Fetching...");
            }
            else
            {
                if (ImGui.SmallButton("🔄 Refresh List"))
                {
                    _ = RefreshGoogleFontsAsync();
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Fetch latest Google Fonts from API");
            }
            
            ImGui.Spacing();

            // Search
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted("Search:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##GoogleFontSearch", "Type to search Google Fonts...", ref _googleFontSearch, 256);
            ImGui.Spacing();

            // Left side: Font list (60% width)
            float availWidth = ImGui.GetContentRegionAvail().X;
            float listWidth = MathF.Floor(availWidth * 0.58f);
            float spacing = ImGui.GetStyle().ItemSpacing.X;
            float previewWidth = availWidth - listWidth - spacing;
            
            // Ensure minimum widths to prevent overlap
            if (listWidth < 200f) listWidth = 200f;
            if (previewWidth < 250f) previewWidth = 250f;

            ImGui.BeginChild("GoogleFontsList", new Vector2(listWidth, 400), ImGuiChildFlags.Borders);
            
            var filteredGoogleFonts = string.IsNullOrWhiteSpace(_googleFontSearch)
                ? _googleFonts
                : _googleFonts.Where(f => f.Family.Contains(_googleFontSearch, StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var font in filteredGoogleFonts.Take(100)) // Show top 100 to avoid performance issues
            {
                ImGui.PushID(font.Family);
                
                bool isSelected = _selectedGoogleFont?.Family == font.Family;
                
                // Calculate exact widths to prevent overlap
                float totalWidth = ImGui.GetContentRegionAvail().X;
                float statusWidth = 110f; // Width for download button/status
                float nameWidth = totalWidth - statusWidth - ImGui.GetStyle().ItemSpacing.X;
                
                // Font name selectable
                if (ImGui.Selectable(font.Family, isSelected, ImGuiSelectableFlags.None, new Vector2(nameWidth, 0)))
                {
                    _selectedGoogleFont = font;
                }
                
                // Position cursor for status on same line
                ImGui.SameLine(nameWidth + ImGui.GetStyle().ItemSpacing.X);
                ImGui.SetCursorPosY(ImGui.GetCursorPosY()); // Align vertically
                
                // Download button or status with fixed width
                if (font.IsDownloaded)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.3f, 0.8f, 0.3f, 1f));
                    ImGui.TextUnformatted("✓ Downloaded");
                    ImGui.PopStyleColor();
                }
                else if (_downloadingFonts.ContainsKey(font.Family) && _downloadingFonts[font.Family])
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.8f, 0.8f, 0.3f, 1f));
                    ImGui.TextUnformatted("⏳ Downloading");
                    ImGui.PopStyleColor();
                }
                else
                {
                    if (ImGui.SmallButton("⬇ Download"))
                    {
                        _ = DownloadGoogleFontAsync(font);
                    }
                }
                
                ImGui.PopID();
            }
            
            ImGui.EndChild();
            
            ImGui.SameLine();
            
            // Right side: Preview and controls
            ImGui.BeginGroup();
            
            if (_selectedGoogleFont != null)
            {
                // Selected font info at top
                ImGui.PushStyleColor(ImGuiCol.Text, currentTheme.InspectorSection);
                ImGui.TextUnformatted($"Selected: {_selectedGoogleFont.Family}");
                ImGui.PopStyleColor();
                
                ImGui.PushStyleColor(ImGuiCol.Text, currentTheme.TextDisabled);
                ImGui.TextUnformatted($"Category: {_selectedGoogleFont.Category}");
                ImGui.TextUnformatted($"Variants: {string.Join(", ", _selectedGoogleFont.Variants.Take(5))}");
                ImGui.PopStyleColor();
                ImGui.Spacing();
                
                // Preview if downloaded
                if (_selectedGoogleFont.IsDownloaded)
                {
                    DrawFontPreview(currentTheme, previewWidth, _selectedGoogleFont);
                    ImGui.Spacing();
                    DrawFontSizeControls();
                    ImGui.Spacing();
                    DrawFontApplyButtons(currentTheme, true);
                }
                else
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, currentTheme.TextDisabled);
                    ImGui.TextWrapped("Download this font to preview and use it in the editor");
                    ImGui.PopStyleColor();
                    ImGui.Spacing();
                    if (ImGui.Button("⬇ Download Font"))
                    {
                        _ = DownloadGoogleFontAsync(_selectedGoogleFont);
                    }
                }
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Text, currentTheme.TextDisabled);
                ImGui.TextWrapped("Select a Google Font from the list");
                ImGui.PopStyleColor();
            }
            
            ImGui.EndGroup();
        }
        
        private void DrawFontSizeControls()
        {
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted("Size:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(200);
            float fontSize = _selectedFontSize;
            if (ImGui.SliderFloat("##FontSize", ref fontSize, 10f, 24f, "%.0f px"))
                _selectedFontSize = fontSize;

            ImGui.SameLine();
            if (ImGui.SmallButton("S")) _selectedFontSize = 12f;
            ImGui.SameLine();
            if (ImGui.SmallButton("M")) _selectedFontSize = 14f;
            ImGui.SameLine();
            if (ImGui.SmallButton("L")) _selectedFontSize = 16f;
            ImGui.SameLine();
            if (ImGui.SmallButton("XL")) _selectedFontSize = 18f;
        }
        
        private void DrawFontPreview(Editor.Themes.EditorTheme currentTheme, float width = 0, GoogleFontsManager.GoogleFontInfo? googleFont = null)
        {
            // Use explicit width or auto-width with minimum
            float previewWidth = width > 0 ? width : ImGui.GetContentRegionAvail().X;
            if (previewWidth < 200f) previewWidth = 200f;
            
            ImGui.BeginChild("FontPreview", new Vector2(previewWidth, 300), ImGuiChildFlags.Borders);
            
            ImGui.PushStyleColor(ImGuiCol.Text, currentTheme.InspectorSection);
            ImGui.TextUnformatted("Font Preview");
            ImGui.PopStyleColor();
            ImGui.Separator();
            ImGui.Spacing();

            string previewFontName;
            string? previewFontPath = null;
            
            if (googleFont != null)
            {
                previewFontName = googleFont.Family;
                if (googleFont.IsDownloaded)
                {
                    previewFontPath = GoogleFontsManager.GetFontPath(googleFont.Family);
                }
            }
            else
            {
                if (_selectedFontIndex >= 0 && _selectedFontIndex < _availableFonts.Count)
                {
                    var selectedFont = _availableFonts[_selectedFontIndex];
                    previewFontName = selectedFont.DisplayName;
                    previewFontPath = selectedFont.FilePath;
                }
                else
                {
                    previewFontName = "Unknown";
                }
            }
            
            ImGui.TextUnformatted($"Font: {previewFontName}");
            ImGui.TextUnformatted($"Size: {_selectedFontSize:F0}px");
            ImGui.Spacing();

            // Load and use actual font for real preview
            if (previewFontPath != null && Editor.Utils.ImGuiControllerManager.IsInitialized)
            {
                var imguiController = Editor.Utils.ImGuiControllerManager.Instance;
                if (imguiController != null)
                {
                    var previewFont = imguiController.LoadPreviewFont(previewFontPath, _selectedFontSize);
                    ImGui.PushFont(previewFont);
                }
            }
            
            ImGui.TextUnformatted("The quick brown fox jumps over the lazy dog");
            ImGui.TextUnformatted("ABCDEFGHIJKLMNOPQRSTUVWXYZ");
            ImGui.TextUnformatted("abcdefghijklmnopqrstuvwxyz");
            ImGui.TextUnformatted("0123456789 !@#$%^&*()");
            
            if (previewFontPath != null && Editor.Utils.ImGuiControllerManager.IsInitialized)
            {
                ImGui.PopFont();
            }

            ImGui.EndChild();
        }
        
        private void DrawFontApplyButtons(Editor.Themes.EditorTheme currentTheme, bool isGoogleFont)
        {
            float fontButtonWidth = 100f;
            float fontSpacing = ImGui.GetStyle().ItemSpacing.X;
            float fontTotalButtonWidth = fontButtonWidth * 2 + fontSpacing;
            float fontAvailWidth = ImGui.GetContentRegionAvail().X;
            
            // Only center if there's enough space, otherwise align left
            if (fontAvailWidth >= fontTotalButtonWidth)
            {
                float fontIndentAmount = (fontAvailWidth - fontTotalButtonWidth) * 0.5f;
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + fontIndentAmount);
            }

            if (ImGui.Button("Apply##Font", new Vector2(fontButtonWidth, 0)))
            {
                if (isGoogleFont && _selectedGoogleFont != null)
                {
                    var fontPath = GoogleFontsManager.GetFontPath(_selectedGoogleFont.Family);
                    if (fontPath != null)
                    {
                        SaveFontSettings(_selectedGoogleFont.Family, fontPath, _selectedFontSize);
                        LogManager.LogInfo($"Font changed to: {_selectedGoogleFont.Family} ({_selectedFontSize}px)", "Preferences");
                        
                        // Reload fonts immediately without restart
                        if (Editor.Utils.ImGuiControllerManager.IsInitialized)
                        {
                            Editor.Utils.ImGuiControllerManager.Instance?.ReloadFonts();
                            LogManager.LogInfo("Font applied successfully!", "Preferences");
                        }
                        else
                        {
                            LogManager.LogInfo("Restart editor to apply changes.", "Preferences");
                        }
                    }
                }
                else if (_selectedFontIndex >= 0 && _selectedFontIndex < _availableFonts.Count)
                {
                    var selectedFont = _availableFonts[_selectedFontIndex];
                    SaveFontSettings(selectedFont.DisplayName, selectedFont.FilePath, _selectedFontSize);
                    LogManager.LogInfo($"Font changed to: {selectedFont.DisplayName} ({_selectedFontSize}px)", "Preferences");
                    
                    // Reload fonts immediately without restart
                    if (Editor.Utils.ImGuiControllerManager.IsInitialized)
                    {
                        Editor.Utils.ImGuiControllerManager.Instance?.ReloadFonts();
                        LogManager.LogInfo("Font applied successfully!", "Preferences");
                    }
                    else
                    {
                        LogManager.LogInfo("Restart editor to apply changes.", "Preferences");
                    }
                }
            }
            
            ImGui.SameLine();
            
            if (ImGui.Button("Reset##Font", new Vector2(fontButtonWidth, 0)))
            {
                string savedFont = EditorSettings.InterfaceFont;
                float savedSize = EditorSettings.InterfaceFontSize;

                _selectedFontIndex = _availableFonts.FindIndex(f => f.DisplayName == savedFont);
                if (_selectedFontIndex == -1)
                    _selectedFontIndex = 0;

                _selectedFontSize = savedSize;
                LogManager.LogInfo($"Font reset to saved settings: {savedFont} ({savedSize}px)", "Preferences");
            }
        }
        
        private async Task RefreshGoogleFontsAsync()
        {
            _isFetchingGoogleFonts = true;
            bool success = await GoogleFontsManager.FetchFontsListAsync();
            if (success)
            {
                _googleFonts = GoogleFontsManager.GetPopularFonts(200);
                LogManager.LogInfo($"Refreshed Google Fonts: {_googleFonts.Count} fonts available", "Preferences");
            }
            _isFetchingGoogleFonts = false;
        }
        
        private async Task DownloadGoogleFontAsync(GoogleFontsManager.GoogleFontInfo font)
        {
            _downloadingFonts[font.Family] = true;
            
            var path = await GoogleFontsManager.DownloadFontAsync(font.Family);
            
            _downloadingFonts[font.Family] = false;
            
            if (path != null)
            {
                font.IsDownloaded = true;
                LogManager.LogInfo($"Google Font '{font.Family}' downloaded successfully", "Preferences");
            }
        }
        
        private void DrawColorSwatch(string label, Vector4 color, Vector4 borderColor)
        {
            var pos = ImGui.GetCursorScreenPos();
            var drawList = ImGui.GetWindowDrawList();
            
            // Draw color square
            var colU32 = ImGui.ColorConvertFloat4ToU32(color);
            drawList.AddRectFilled(pos, new Vector2(pos.X + 24, pos.Y + 24), colU32, 4.0f);
            drawList.AddRect(pos, new Vector2(pos.X + 24, pos.Y + 24), ImGui.ColorConvertFloat4ToU32(borderColor), 4.0f);
            
            ImGui.Dummy(new Vector2(28, 24));
            
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted($"{label}");
                ImGui.TextUnformatted($"RGB: ({(int)(color.X * 255)}, {(int)(color.Y * 255)}, {(int)(color.Z * 255)})");
                ImGui.TextUnformatted($"Alpha: {color.W:F2}");
                ImGui.EndTooltip();
            }
        }
        
        private string _tempScriptEditorPath = "";
        private string _tempScriptEditorArgs = "";
        private bool _externalToolsInitialized = false;
        
    private void DrawShortcutsSettings(Editor.Themes.EditorTheme currentTheme)
        {
            ThemeManager.DrawGradientHeader("⌨️ Keyboard Shortcuts", new Vector2(ImGui.GetContentRegionAvail().X, 50));
            ImGui.Spacing();
            ImGui.Spacing();
            
            // Disable shortcuts in play mode option
            ImGui.PushStyleColor(ImGuiCol.Text, currentTheme.InspectorSection);
            ImGui.TextUnformatted("Play Mode");
            ImGui.PopStyleColor();
            ImGui.Separator();
            ImGui.Spacing();
            
            bool disableInPlayMode = EditorSettings.ShortcutsDisableInPlayMode;
            if (ImGui.Checkbox("Disable Editor Shortcuts in Play Mode", ref disableInPlayMode))
            {
                EditorSettings.ShortcutsDisableInPlayMode = disableInPlayMode;
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted("When enabled, editor shortcuts (Ctrl+S, Ctrl+D, etc.) are disabled during Play Mode");
                ImGui.TextColored(currentTheme.TextDisabled, "This allows your game to use these keys without interference");
                ImGui.EndTooltip();
            }
            
            ImGui.Spacing();
            ImGui.Spacing();
            
            // File operations
            ImGui.PushStyleColor(ImGuiCol.Text, currentTheme.InspectorSection);
            ImGui.TextUnformatted("File");
            ImGui.PopStyleColor();
            ImGui.Separator();
            ImGui.Spacing();
            
            DrawShortcutRow("New Scene", "ShortcutNewScene", currentTheme);
            DrawShortcutRow("Open Scene", "ShortcutOpenScene", currentTheme);
            DrawShortcutRow("Save Scene", "ShortcutSaveScene", currentTheme);
            DrawShortcutRow("Save Scene As", "ShortcutSaveSceneAs", currentTheme);
            
            ImGui.Spacing();
            ImGui.Spacing();
            
            // Edit operations
            ImGui.PushStyleColor(ImGuiCol.Text, currentTheme.InspectorSection);
            ImGui.TextUnformatted("Edit");
            ImGui.PopStyleColor();
            ImGui.Separator();
            ImGui.Spacing();
            
            DrawShortcutRow("Undo", "ShortcutUndo", currentTheme);
            DrawShortcutRow("Redo", "ShortcutRedo", currentTheme);
            DrawShortcutRow("Duplicate", "ShortcutDuplicate", currentTheme);
            DrawShortcutRow("Delete", "ShortcutDelete", currentTheme);
            DrawShortcutRow("Select All", "ShortcutSelectAll", currentTheme);
            DrawShortcutRow("Deselect All", "ShortcutDeselectAll", currentTheme);
            
            ImGui.Spacing();
            ImGui.Spacing();
            
            // GameObject operations
            ImGui.PushStyleColor(ImGuiCol.Text, currentTheme.InspectorSection);
            ImGui.TextUnformatted("GameObject");
            ImGui.PopStyleColor();
            ImGui.Separator();
            ImGui.Spacing();
            
            DrawShortcutRow("Create Empty", "ShortcutCreateEmpty", currentTheme);
            DrawShortcutRow("Rename", "ShortcutRename", currentTheme);
            
            ImGui.Spacing();
            ImGui.Spacing();
            
            // View operations
            ImGui.PushStyleColor(ImGuiCol.Text, currentTheme.InspectorSection);
            ImGui.TextUnformatted("View");
            ImGui.PopStyleColor();
            ImGui.Separator();
            ImGui.Spacing();
            
            DrawShortcutRow("Frame Selected", "ShortcutFrameSelected", currentTheme);
            
            ImGui.Spacing();
            ImGui.Spacing();
            
            // Play Mode
            ImGui.PushStyleColor(ImGuiCol.Text, currentTheme.InspectorSection);
            ImGui.TextUnformatted("Play Mode");
            ImGui.PopStyleColor();
            ImGui.Separator();
            ImGui.Spacing();
            
            DrawShortcutRow("Play/Pause", "ShortcutPlayPause", currentTheme);
            
            ImGui.Spacing();
            ImGui.Spacing();
            
            // Reset to defaults button
            if (ImGui.Button("Reset to Defaults", new Vector2(150, 0)))
            {
                EditorSettings.ResetShortcutsToDefaults();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted("Reset all shortcuts to their default values");
                ImGui.EndTooltip();
            }
        }
        
        private void DrawShortcutRow(string label, string propertyName, Editor.Themes.EditorTheme currentTheme)
        {
            ImGui.AlignTextToFramePadding();
            ImGui.Text(label);
            ImGui.SameLine(200);
            ImGui.SetNextItemWidth(150);
            
            // Get property value via reflection
            var property = typeof(EditorSettings).GetProperty(propertyName, 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (property != null)
            {
                string value = (property.GetValue(null) as string) ?? "";
                if (ImGui.InputText($"##{label}", ref value, 64))
                {
                    property.SetValue(null, value);
                }
                
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.TextUnformatted("Format: Ctrl+Key, Shift+Key, Alt+Key, or just Key");
                    ImGui.TextColored(currentTheme.TextDisabled, "Examples: Ctrl+S, Ctrl+Shift+N, F2, Del");
                    ImGui.EndTooltip();
                }
            }
        }
    
    private void DrawExternalToolsSettings(Editor.Themes.EditorTheme currentTheme)
        {
            // Load settings once when switching to this category
            if (!_externalToolsInitialized)
            {
                _tempScriptEditorPath = EditorSettings.ScriptEditor;
                _tempScriptEditorArgs = EditorSettings.ScriptEditorArgs;
                _externalToolsInitialized = true;
            }
            
            ThemeManager.DrawGradientHeader("🛠️ External Tools", new Vector2(ImGui.GetContentRegionAvail().X, 50));
            ImGui.Spacing();
            ImGui.Spacing();
            
            // Script Editor section
            ImGui.PushStyleColor(ImGuiCol.Text, currentTheme.InspectorSection);
            ImGui.TextUnformatted("External Script Editor");
            ImGui.PopStyleColor();
            ImGui.Separator();
            ImGui.Spacing();
            
            // Editor path
            ImGui.Text("Editor Application:");
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("##scriptEditor", ref _tempScriptEditorPath, 512))
            {
                EditorSettings.ScriptEditor = _tempScriptEditorPath;
            }
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.TextUnformatted("Path to your preferred script editor executable");
                    ImGui.TextColored(currentTheme.TextDisabled, "Examples: VS Code, Rider, Visual Studio");
                    ImGui.EndTooltip();
                }
            
            ImGui.Spacing();
            
            // Buttons row
            if (ImGui.Button("Browse...", new Vector2(120, 0)))
            {
                // TODO: Open file dialog
                ImGui.OpenPopup("##selectEditor");
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted("Select editor executable from file system");
                ImGui.EndTooltip();
            }
            
            ImGui.SameLine();
            
                if (ImGui.Button("Auto-detect VS Code", new Vector2(150, 0)))
            {
                var detectedPath = TryDetectVSCode();
                if (!string.IsNullOrEmpty(detectedPath))
                {
                    _tempScriptEditorPath = detectedPath;
                    EditorSettings.ScriptEditor = detectedPath;
                        LogManager.LogInfo($"VS Code detected at: {detectedPath}", "Preferences");
                }
                else
                {
                        LogManager.LogInfo("VS Code not found in standard locations", "Preferences");
                }
            }
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.TextUnformatted("Automatically detect VS Code installation");
                    ImGui.EndTooltip();
                }
            
            ImGui.SameLine();
            
            if (ImGui.Button("Test Editor", new Vector2(100, 0)))
            {
                var testFile = System.IO.Path.Combine(ProjectPaths.ProjectRoot, "README.md");
                if (System.IO.File.Exists(testFile))
                {
                    EditorSettings.OpenScript(testFile, 1);
                }
                else
                {
                    LogManager.LogInfo("No test file available", "Preferences");
                }
            }
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.TextUnformatted("Test opening README.md with configured editor");
                    ImGui.EndTooltip();
                }
            
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            
            // Editor arguments
            ImGui.Text("External Script Editor Args:");
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("##scriptEditorArgs", ref _tempScriptEditorArgs, 512))
            {
                EditorSettings.ScriptEditorArgs = _tempScriptEditorArgs;
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted("Arguments passed to the editor when opening files");
                ImGui.TextColored(currentTheme.TextDisabled, "Use placeholders: $(File), $(Line), $(Column)");
                ImGui.EndTooltip();
            }
            
            ImGui.Spacing();
            
            // Help text
            ImGui.PushStyleColor(ImGuiCol.Text, currentTheme.InspectorSection);
            ImGui.TextUnformatted("Argument Placeholders:");
            ImGui.PopStyleColor();
            ImGui.Indent();
            ImGui.TextColored(currentTheme.TextDisabled, "$(File) - Full file path");
            ImGui.TextColored(currentTheme.TextDisabled, "$(Line) - Line number");
            // (font controls removed here — handled in Appearance section)
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted("JetBrains Rider arguments");
                ImGui.EndTooltip();
            }
            ImGui.Unindent();
            
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            
            // Current configuration display
            ImGui.PushStyleColor(ImGuiCol.Text, currentTheme.InspectorSection);
            ImGui.TextUnformatted("Current Configuration:");
            ImGui.PopStyleColor();
            ImGui.Spacing();
            
            ImGui.BeginChild("##currentConfig", new Vector2(0, 80), ImGuiChildFlags.Borders);
            ImGui.TextColored(currentTheme.TextDisabled, "Editor:");
            ImGui.SameLine();
            ImGui.Text(_tempScriptEditorPath != "" ? _tempScriptEditorPath : "(Not configured)");
            
            ImGui.TextColored(currentTheme.TextDisabled, "Arguments:");
            ImGui.SameLine();
            ImGui.Text(_tempScriptEditorArgs);
            ImGui.EndChild();
        }
        
        private static string TryDetectVSCode()
        {
            string[] possiblePaths = new[]
            {
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Microsoft VS Code", "Code.exe"),
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft VS Code", "Code.exe"),
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft VS Code", "Code.exe"),
                "C:\\Program Files\\Microsoft VS Code\\Code.exe",
                "C:\\Program Files (x86)\\Microsoft VS Code\\Code.exe"
            };

            foreach (var path in possiblePaths)
            {
                if (System.IO.File.Exists(path))
                    return path;
            }

            return "";
        }
        
        private void DrawInputSettings(Editor.Themes.EditorTheme currentTheme)
        {
            ThemeManager.DrawGradientHeader("? Input", new Vector2(ImGui.GetContentRegionAvail().X, 50));
            ImGui.Spacing();
            ImGui.Spacing();
            
            ImGui.PushStyleColor(ImGuiCol.Text, currentTheme.TextDisabled);
            ImGui.TextWrapped("Input settings will be available soon. Configure input actions, mappings, and sensitivity.");
            ImGui.PopStyleColor();
        }
        
        private void DrawEditorSettings(Editor.Themes.EditorTheme currentTheme)
        {
            ThemeManager.DrawGradientHeader("? Editor", new Vector2(ImGui.GetContentRegionAvail().X, 50));
            ImGui.Spacing();
            ImGui.Spacing();
            
            ImGui.PushStyleColor(ImGuiCol.Text, currentTheme.TextDisabled);
            ImGui.TextWrapped("Editor settings will be available soon. Configure auto-save, recent files, and editor behavior.");
            ImGui.PopStyleColor();
        }
        
        private void DrawSceneViewSettings(Editor.Themes.EditorTheme currentTheme)
        {
            ThemeManager.DrawGradientHeader("? Scene View", new Vector2(ImGui.GetContentRegionAvail().X, 50));
            ImGui.Spacing();
            ImGui.Spacing();
            
            // Selection Outline Settings
            ImGui.SeparatorText("Selection Outline");
            
            var outline = EditorSettings.Outline;
            bool outlineEnabled = outline.Enabled;
            if (ImGui.Checkbox("Enable Selection Outline", ref outlineEnabled))
            {
                outline.Enabled = outlineEnabled;
                EditorSettings.Outline = outline; // Trigger save via setter
            }
            
            if (outlineEnabled)
            {
                float thickness = outline.Thickness;
                if (ImGui.SliderFloat("Outline Thickness", ref thickness, 1.0f, 5.0f, "%.1f px"))
                {
                    outline.Thickness = thickness;
                    EditorSettings.Outline = outline;
                }
                
                var color = new System.Numerics.Vector4(
                    outline.ColorR,
                    outline.ColorG,
                    outline.ColorB,
                    outline.ColorA
                );
                
                if (ImGui.ColorEdit4("Outline Color", ref color))
                {
                    outline.ColorR = color.X;
                    outline.ColorG = color.Y;
                    outline.ColorB = color.Z;
                    outline.ColorA = color.W;
                    EditorSettings.Outline = outline;
                }
                
                ImGui.Spacing();
                ImGui.SeparatorText("Pulse Effect");
                
                bool pulseEnabled = outline.EnablePulse;
                if (ImGui.Checkbox("Enable Soft Pulse", ref pulseEnabled))
                {
                    outline.EnablePulse = pulseEnabled;
                    EditorSettings.Outline = outline;
                }
                
                if (pulseEnabled)
                {
                    float pulseSpeed = outline.PulseSpeed;
                    if (ImGui.SliderFloat("Pulse Speed", ref pulseSpeed, 0.5f, 5.0f, "%.1f Hz"))
                    {
                        outline.PulseSpeed = pulseSpeed;
                        EditorSettings.Outline = outline;
                    }
                    
                    float pulseMin = outline.PulseMinAlpha;
                    if (ImGui.SliderFloat("Min Alpha", ref pulseMin, 0.0f, 1.0f, "%.2f"))
                    {
                        outline.PulseMinAlpha = pulseMin;
                        EditorSettings.Outline = outline;
                    }
                    
                    float pulseMax = outline.PulseMaxAlpha;
                    if (ImGui.SliderFloat("Max Alpha", ref pulseMax, 0.0f, 1.0f, "%.2f"))
                    {
                        outline.PulseMaxAlpha = pulseMax;
                        EditorSettings.Outline = outline;
                    }
                }
            }
            
            ImGui.Spacing();
            ImGui.Spacing();
        }
        
        private void DrawGridSnapSettings(Editor.Themes.EditorTheme currentTheme)
        {
            ThemeManager.DrawGradientHeader("? Grid & Snap", new Vector2(ImGui.GetContentRegionAvail().X, 50));
            ImGui.Spacing();
            ImGui.Spacing();
            
            ImGui.PushStyleColor(ImGuiCol.Text, currentTheme.TextDisabled);
            ImGui.TextWrapped("Grid & snap settings will be available soon. Configure grid size, snap increments, and snapping behavior.");
            ImGui.PopStyleColor();
        }
        
        private void SaveThemeToSettings(string themeName)
        {
                try
                {
                    EditorSettings.ThemeName = themeName;
                    LogManager.LogInfo($"Theme '{themeName}' saved to EditorSettings.", "Preferences");
                }
                catch (Exception ex)
                {
                    LogManager.LogError($"Failed to save theme to settings: {ex.Message}", "Preferences");
                }
        }
        
        private void SaveFontSettings(string fontDisplayName, string fontFilePath, float fontSize)
        {
                try
                {
                    // Save to EditorSettings
                    EditorSettings.InterfaceFont = fontDisplayName;
                    EditorSettings.InterfaceFontPath = fontFilePath; // Add this to EditorSettings if needed
                    EditorSettings.InterfaceFontSize = fontSize;

                    LogManager.LogInfo($"Font settings saved: {fontDisplayName} @ {fontSize}px", "Preferences");
                    LogManager.LogInfo($"Font path: {fontFilePath}", "Preferences");
                    LogManager.LogInfo("Font changes will be applied on next editor restart.", "Preferences");
                }
                catch (Exception ex)
                {
                    LogManager.LogError($"Failed to save font settings: {ex.Message}", "Preferences");
                }
        }
    }
}

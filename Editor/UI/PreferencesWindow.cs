using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using Editor.Themes;
using Editor.State;
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
            Console.WriteLine($"[Preferences] Loaded {_availableFonts.Count} system fonts");

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
                
                if (isSelected)
                {
                    ImGui.PushStyleColor(ImGuiCol.Button, currentTheme.ButtonActive);
                    ImGui.PushStyleColor(ImGuiCol.Text, currentTheme.Text);
                }
                else
                {
                    ImGui.PushStyleColor(ImGuiCol.Button, currentTheme.Button);
                }
                
                if (ImGui.Button(category, new Vector2(-1, 0)))
                {
                    _selectedCategory = category;
                }
                
                if (isSelected)
                    ImGui.PopStyleColor(2);
                else
                    ImGui.PopStyleColor(1);
                
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
                Console.WriteLine($"[Preferences] Theme '{_selectedThemeName}' applied and saved.");
            }
            ImGui.SameLine();
            if (ImGui.Button("Reset##Theme", new Vector2(buttonWidth, 0)))
            {
                // Reset to the last saved theme
                _previewThemeName = _selectedThemeName;
                ThemeManager.ApplyThemeByName(_selectedThemeName);
                Console.WriteLine($"[Preferences] Theme reset to '{_selectedThemeName}'.");
            }

            ImGui.Spacing();
            ImGui.Spacing();

            // Interface Font (fresh, theme-style layout)
                ImGui.PushStyleColor(ImGuiCol.Text, currentTheme.InspectorSection);
            ImGui.TextUnformatted("Interface Font");
            ImGui.PopStyleColor();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.PushStyleColor(ImGuiCol.Text, currentTheme.TextDisabled);
            ImGui.TextWrapped($"Choose from {_availableFonts.Count} system fonts. All TrueType (.ttf) and OpenType (.otf) fonts installed on your system are available.");
            ImGui.PopStyleColor();
            ImGui.Spacing();
            ImGui.Spacing();

            // Font search filter
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted("Search:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##FontSearch", "Type to filter fonts...", ref _fontSearchFilter, 256);
            ImGui.Spacing();

            // Full-width left-aligned controls to match Theme visually
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted("Font:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(-1);

            string currentFontName = _selectedFontIndex >= 0 && _selectedFontIndex < _availableFonts.Count
                ? _availableFonts[_selectedFontIndex].DisplayName
                : "Select a font...";

            if (ImGui.BeginCombo("##FontFamily", currentFontName))
            {
                // Filter fonts based on search
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

                        if (ImGui.Selectable(font.DisplayName, isSelected))
                        {
                            _selectedFontIndex = actualIndex;
                            _fontSearchFilter = ""; // Clear search after selection
                        }

                        if (isSelected)
                            ImGui.SetItemDefaultFocus();

                        // Show file path on hover
                        if (ImGui.IsItemHovered() && !string.IsNullOrEmpty(font.FilePath))
                        {
                            ImGui.BeginTooltip();
                            ImGui.PushStyleColor(ImGuiCol.Text, currentTheme.TextDisabled);
                            ImGui.TextUnformatted($"File: {font.FilePath}");
                            ImGui.PopStyleColor();
                            ImGui.EndTooltip();
                        }
                    }
                }

                ImGui.EndCombo();
            }

            ImGui.Spacing();

            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted("Size:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(-1);
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

            ImGui.Spacing();

            // Preview (scale only)
            ImGui.BeginChild("FontPreview", new Vector2(0, 200), ImGuiChildFlags.Borders);
            ImGui.PushStyleColor(ImGuiCol.Text, currentTheme.InspectorSection);
            ImGui.TextUnformatted("Font Preview");
            ImGui.PopStyleColor();
            ImGui.Separator();
            ImGui.Spacing();

            string previewFontName = _selectedFontIndex >= 0 && _selectedFontIndex < _availableFonts.Count
                ? _availableFonts[_selectedFontIndex].DisplayName
                : "Unknown";
            ImGui.TextUnformatted($"Font: {previewFontName}");
            ImGui.TextUnformatted($"Size: {_selectedFontSize:F0}px");
            ImGui.Spacing();

            float currentFontSize = ImGui.GetFontSize();
            float scaleRatio = _selectedFontSize / currentFontSize;
            ImGui.SetWindowFontScale(scaleRatio);
            ImGui.TextUnformatted("The quick brown fox jumps over the lazy dog");
            ImGui.TextUnformatted("ABCDEFGHIJKLMNOPQRSTUVWXYZ");
            ImGui.TextUnformatted("abcdefghijklmnopqrstuvwxyz");
            ImGui.TextUnformatted("0123456789");
            ImGui.SetWindowFontScale(1.0f);

            ImGui.EndChild();

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Font Apply/Reset buttons - aligned to the right
            float fontButtonWidth = 100f;
            float fontSpacing = ImGui.GetStyle().ItemSpacing.X;
            float fontTotalButtonWidth = fontButtonWidth * 2 + fontSpacing;
            float fontAvailWidth = ImGui.GetContentRegionAvail().X;
            float fontIndentAmount = fontAvailWidth - fontTotalButtonWidth;

            if (fontIndentAmount > 0)
            {
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + fontIndentAmount);
            }

            if (ImGui.Button("Apply##Font", new Vector2(fontButtonWidth, 0)))
            {
                if (_selectedFontIndex >= 0 && _selectedFontIndex < _availableFonts.Count)
                {
                    var selectedFont = _availableFonts[_selectedFontIndex];
                    SaveFontSettings(selectedFont.DisplayName, selectedFont.FilePath, _selectedFontSize);
                    Console.WriteLine($"[Preferences] Font changed to: {selectedFont.DisplayName} ({_selectedFontSize}px)");
                    Console.WriteLine("[Preferences] Restart editor to apply changes.");
                }
            }
            ImGui.SameLine();
            if (ImGui.Button("Reset##Font", new Vector2(fontButtonWidth, 0)))
            {
                // Reset to current saved settings
                string savedFont = EditorSettings.InterfaceFont;
                float savedSize = EditorSettings.InterfaceFontSize;

                _selectedFontIndex = _availableFonts.FindIndex(f => f.DisplayName == savedFont);
                if (_selectedFontIndex == -1)
                    _selectedFontIndex = 0;

                _selectedFontSize = savedSize;
                Console.WriteLine($"[Preferences] Font reset to saved settings: {savedFont} ({savedSize}px)");
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
                    Console.WriteLine($"[Preferences] VS Code detected at: {detectedPath}");
                }
                else
                {
                    Console.WriteLine("[Preferences] VS Code not found in standard locations");
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
                    Console.WriteLine("[Preferences] No test file available");
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
            
            ImGui.PushStyleColor(ImGuiCol.Text, currentTheme.TextDisabled);
            ImGui.TextWrapped("Scene view settings will be available soon. Configure gizmos, grid, and viewport rendering.");
            ImGui.PopStyleColor();
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
                Console.WriteLine($"Theme '{themeName}' saved to EditorSettings.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save theme to settings: {ex.Message}");
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

                Console.WriteLine($"[Preferences] Font settings saved: {fontDisplayName} @ {fontSize}px");
                Console.WriteLine($"[Preferences] Font path: {fontFilePath}");
                Console.WriteLine("[Preferences] Font changes will be applied on next editor restart.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Preferences] Failed to save font settings: {ex.Message}");
            }
        }
    }
}

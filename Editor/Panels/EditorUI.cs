using Engine.Assets;
using System;
using ImGuiNET;
using Editor.Serialization;
using Editor.State;
using Editor.Panels;
using System.Numerics;
using Editor.Icons;
using Editor.SceneManagement;
using Editor.UI;
using Editor.Tasks;
using Editor.Logging;
using Editor.UIManager.Profiling;
using Editor.Themes;
using Editor.Utils;

namespace Editor.Panels;

public static class EditorUI
{
    private static UITheme UI => ThemeManager.UI;

    // Panel instances
    // Using the modern ViewportPanel for iterative work (handles both Edit and Play modes)
    public static ViewportPanelModern MainViewport = new ViewportPanelModern();
    // REMOVED: GamePanelModern - ViewportPanelModern now handles both Edit and Play modes
    public static PreferencesWindow Preferences = new PreferencesWindow();
    public static AudioMixerPanel AudioMixer = new AudioMixerPanel();
    //private static int _debugFrameCounter = 0;

    // View toggles
    static bool ShowHierarchy = true;
    static bool ShowInspector = true;
    static bool ShowAssets = true;
    static bool ShowConsole = true;
    public static bool ShowGame = true;
    static bool ShowEnvironment = true;
    public static bool ShowRenderingSettings = LoadUIPreference("ShowRenderingSettings", false);
    static bool ShowAudioMixer = LoadUIPreference("ShowAudioMixer", false);

    // --- Scene commands - now using SceneManager ---

    public static bool ShowDemoWindow = false;
    public static bool ShowIconManager = false;
    public static bool ShowPerformanceOverlay = false;

    // PERF FIX: IconManager initialized once on first use instead of checking every frame
    private static bool _iconManagerInitialized = false;

    public static void DrawDockspaceAndMainMenu()
    {
        // NOTE: AssetDatabase initialization moved to Program.cs Load event (line 222)
        // IconManager initialization moved to one-time init to avoid per-frame overhead
        // These operations should NEVER be called every frame as they cause severe performance degradation

        // PERF FIX: Initialize IconManager only once (moved out of per-frame loop)
        if (!_iconManagerInitialized)
        {
            var iconsPath = System.IO.Path.Combine(Editor.State.ProjectPaths.ProjectRoot, "Editor", "Icons", "astrild-apex-icons.json");
            IconManager.Initialize(iconsPath);
            _iconManagerInitialized = true;
        }

        var vp = ImGui.GetMainViewport();

        // Barre de menu globale
        if (ImGui.BeginMainMenuBar())
        {
            // ===== FILE MENU =====
            if (ImGui.BeginMenu("File"))
            {
                if (ImGui.MenuItem("New Scene", EditorSettings.ShortcutNewScene)) SceneManager.NewScene();
                if (ImGui.MenuItem("Open Scene...", EditorSettings.ShortcutOpenScene)) SceneManager.OpenScene();
                ImGui.Separator();
                if (ImGui.MenuItem("Save Scene", EditorSettings.ShortcutSaveScene)) SceneManager.SaveScene();
                if (ImGui.MenuItem("Save Scene As...", EditorSettings.ShortcutSaveSceneAs)) SceneManager.SaveSceneAs();
                ImGui.Separator();
                if (ImGui.MenuItem("Import 3D Model...", "Ctrl+Shift+I")) ImportModel();
                ImGui.Separator();
                
                // New Project submenu
                if (ImGui.BeginMenu("New Project"))
                {
                    ImGui.TextDisabled("Create new project (Coming Soon)");
                    if (ImGui.MenuItem("Empty Project"))
                    {
                        LogManager.LogInfo("📦 New Project: Feature coming soon!", "EditorUI");
                    }
                    if (ImGui.MenuItem("3D Project Template"))
                    {
                        LogManager.LogInfo("📦 3D Template: Feature coming soon!", "EditorUI");
                    }
                    if (ImGui.MenuItem("2D Project Template"))
                    {
                        LogManager.LogInfo("📦 2D Template: Feature coming soon!", "EditorUI");
                    }
                    ImGui.EndMenu();
                }
                
                ImGui.Separator();
                if (ImGui.MenuItem("Exit")) System.Environment.Exit(0);
                ImGui.EndMenu();
            }

            // ===== EDIT MENU =====
            if (ImGui.BeginMenu("Edit"))
            {
                if (ImGui.MenuItem("Undo", EditorSettings.ShortcutUndo))
                {
                    var sc = MainViewport.Renderer?.Scene;
                    if (sc != null) UndoRedo.Undo(sc);
                }
                if (ImGui.MenuItem("Redo", EditorSettings.ShortcutRedo))
                {
                    var sc = MainViewport.Renderer?.Scene;
                    if (sc != null) UndoRedo.Redo(sc);
                }
                ImGui.Separator();

                if (ImGui.MenuItem("Duplicate", EditorSettings.ShortcutDuplicate))
                {
                    var sc = MainViewport.Renderer?.Scene;
                    if (sc != null && Selection.Selected.Count > 0)
                    {
                        var duplicatedIds = new HashSet<uint>();
                        foreach (var id in Selection.Selected.ToArray())
                        {
                            var entity = sc.GetById(id);
                            if (entity != null)
                            {
                                var duplicate = HierarchyPanel.DuplicateEntity(sc, entity);
                                if (duplicate != null)
                                    duplicatedIds.Add(duplicate.Id);
                            }
                        }
                        if (duplicatedIds.Count > 0)
                            Selection.ReplaceMany(duplicatedIds);
                    }
                }

                if (ImGui.MenuItem("Delete", EditorSettings.ShortcutDelete))
                {
                    var sc = MainViewport.Renderer?.Scene;
                    if (sc != null && Selection.Selected.Count > 0)
                    {
                        foreach (var id in Selection.Selected.ToArray())
                        {
                            var entity = sc.GetById(id);
                            if (entity != null)
                                HierarchyPanel.DeleteRecursive(sc, entity);
                        }
                        Selection.Clear();
                    }
                }
                
                ImGui.Separator();
                
                // Selection operations
                if (ImGui.MenuItem("Select All", EditorSettings.ShortcutSelectAll))
                {
                    var scene = MainViewport.Renderer?.Scene;
                    if (scene != null)
                    {
                        Selection.Clear();
                        Selection.AddMany(scene.Entities.Select(e => e.Id));
                    }
                }
                if (ImGui.MenuItem("Deselect All", EditorSettings.ShortcutDeselectAll))
                {
                    Selection.Clear();
                }
                
                ImGui.Separator();
                if (ImGui.MenuItem("Preferences...", "Ctrl+,"))
                {
                    Preferences.Open();
                }
                ImGui.EndMenu();
            }

            // ===== ASSETS MENU =====
            if (ImGui.BeginMenu("Assets"))
            {
                if (ImGui.MenuItem("Create Material"))
                {
                    LogManager.LogInfo("➕ Create Material: Use Assets panel right-click menu", "EditorUI");
                }
                if (ImGui.MenuItem("Create Skybox Material"))
                {
                    LogManager.LogInfo("➕ Create Skybox: Use Assets panel right-click menu", "EditorUI");
                }
                ImGui.Separator();
                
                if (ImGui.MenuItem("Import Package..."))
                {
                    LogManager.LogInfo("📦 Import Package: Feature coming soon!", "EditorUI");
                }
                if (ImGui.MenuItem("Export Package..."))
                {
                    LogManager.LogInfo("📦 Export Package: Feature coming soon!", "EditorUI");
                }
                
                ImGui.Separator();
                if (ImGui.MenuItem("Refresh Asset Database", "Ctrl+R"))
                {
                    try
                    {
                        AssetDatabase.Refresh();
                        LogManager.LogInfo("✅ Asset database refreshed!", "EditorUI");
                    }
                    catch (Exception ex)
                    {
                        LogManager.LogError($"❌ Failed to refresh assets: {ex.Message}", "EditorUI");
                    }
                }
                ImGui.EndMenu();
            }

            // ===== GAMEOBJECT MENU =====
            if (ImGui.BeginMenu("GameObject"))
            {
                if (ImGui.BeginMenu("3D Object"))
                {
                    if (ImGui.MenuItem("Cube"))
                    {
                        // Create cube entity
                        var scene = MainViewport.Renderer?.Scene;
                        if (scene != null)
                        {
                            var cube = new Engine.Scene.Entity
                            {
                                Id = scene.GetNextEntityId(),
                                Name = "Cube",
                                Guid = Guid.NewGuid(),
                                Active = true
                            };
                            // TransformComponent already added by Entity constructor
                            // TODO: Add MeshRenderer with cube mesh
                            LogManager.LogInfo("➕ Created Cube", "EditorUI");
                            scene.Entities.Add(cube);
                            Selection.SetSingle(cube.Id);
                            SceneManager.MarkSceneAsModified();
                        }
                    }
                    if (ImGui.MenuItem("Sphere")) LogManager.LogInfo("➕ Create Sphere: Coming soon!", "EditorUI");
                    if (ImGui.MenuItem("Plane")) LogManager.LogInfo("➕ Create Plane: Coming soon!", "EditorUI");
                    if (ImGui.MenuItem("Cylinder")) LogManager.LogInfo("➕ Create Cylinder: Coming soon!", "EditorUI");
                    ImGui.EndMenu();
                }
                
                if (ImGui.BeginMenu("Light"))
                {
                    if (ImGui.MenuItem("Directional Light")) LogManager.LogInfo("➕ Create Directional Light: Coming soon!", "EditorUI");
                    if (ImGui.MenuItem("Point Light")) LogManager.LogInfo("➕ Create Point Light: Coming soon!", "EditorUI");
                    if (ImGui.MenuItem("Spot Light")) LogManager.LogInfo("➕ Create Spot Light: Coming soon!", "EditorUI");
                    ImGui.EndMenu();
                }
                
                if (ImGui.BeginMenu("Audio"))
                {
                    if (ImGui.MenuItem("Audio Source")) LogManager.LogInfo("➕ Create Audio Source: Coming soon!", "EditorUI");
                    if (ImGui.MenuItem("Audio Listener")) LogManager.LogInfo("➕ Create Audio Listener: Coming soon!", "EditorUI");
                    ImGui.EndMenu();
                }
                
                if (ImGui.BeginMenu("Camera"))
                {
                    if (ImGui.MenuItem("Camera")) LogManager.LogInfo("➕ Create Camera: Coming soon!", "EditorUI");
                    ImGui.EndMenu();
                }
                
                ImGui.Separator();
                if (ImGui.MenuItem("Create Empty", EditorSettings.ShortcutCreateEmpty))
                {
                    var scene = MainViewport.Renderer?.Scene;
                    if (scene != null)
                    {
                        var empty = new Engine.Scene.Entity
                        {
                            Id = scene.GetNextEntityId(),
                            Name = "Empty",
                            Guid = Guid.NewGuid(),
                            Active = true
                        };
                        // TransformComponent already added by Entity constructor
                        scene.Entities.Add(empty);
                        Selection.SetSingle(empty.Id);
                        SceneManager.MarkSceneAsModified();
                        LogManager.LogInfo("➕ Created Empty GameObject", "EditorUI");
                    }
                }
                ImGui.EndMenu();
            }

            // ===== WINDOW MENU =====
            if (ImGui.BeginMenu("Window"))
            {
                ImGui.TextDisabled("Panels:");
                var sa = ShowAssets; if (ImGui.MenuItem("Assets", null, sa)) ShowAssets = !ShowAssets;
                var sh = ShowHierarchy; if (ImGui.MenuItem("Hierarchy", null, sh)) ShowHierarchy = !ShowHierarchy;
                var si = ShowInspector; if (ImGui.MenuItem("Inspector", null, si)) ShowInspector = !ShowInspector;
                var sc = ShowConsole; if (ImGui.MenuItem("Console", null, sc)) ShowConsole = !ShowConsole;
                var sg = ShowGame; if (ImGui.MenuItem("Game", null, sg)) ShowGame = !ShowGame;
                
                ImGui.Separator();
                ImGui.TextDisabled("Settings:");
                var se = ShowEnvironment; if (ImGui.MenuItem("Environment", null, se)) ShowEnvironment = !ShowEnvironment;
                var sr = ShowRenderingSettings; if (ImGui.MenuItem("Rendering Settings", null, sr)) { ShowRenderingSettings = !ShowRenderingSettings; SaveUIPreference("ShowRenderingSettings", ShowRenderingSettings); }
                var sam = ShowAudioMixer; if (ImGui.MenuItem("🎵 Audio Mixer", null, sam)) { ShowAudioMixer = !ShowAudioMixer; SaveUIPreference("ShowAudioMixer", ShowAudioMixer); }
                
                ImGui.Separator();
                ImGui.TextDisabled("Debug Tools:");
                var sd = ShowDemoWindow; if (ImGui.MenuItem("ImGui Demo Window", null, sd)) ShowDemoWindow = !ShowDemoWindow;
                var sim = ShowIconManager; if (ImGui.MenuItem("🎨 SVG Icons Manager", null, sim)) ShowIconManager = !ShowIconManager;
                var spo = ShowPerformanceOverlay; if (ImGui.MenuItem("⚡ Performance Overlay", null, spo)) ShowPerformanceOverlay = !ShowPerformanceOverlay;
                var ssp = SystemsProfilerPanel.IsOpen; if (ImGui.MenuItem("🔧 Systems Profiler", null, ssp)) SystemsProfilerPanel.IsOpen = !SystemsProfilerPanel.IsOpen;
                ImGui.EndMenu();
            }

            // ===== BUILD MENU =====
            if (ImGui.BeginMenu("Build"))
            {
                if (ImGui.MenuItem("Build Settings..."))
                {
                    LogManager.LogInfo("🔨 Build Settings: Feature coming soon!", "EditorUI");
                }
                if (ImGui.MenuItem("Build and Run", "Ctrl+B"))
                {
                    LogManager.LogInfo("🔨 Build and Run: Feature coming soon!", "EditorUI");
                }
                ImGui.Separator();
                if (ImGui.MenuItem("Player Settings..."))
                {
                    LogManager.LogInfo("🔨 Player Settings: Feature coming soon!", "EditorUI");
                }
                ImGui.EndMenu();
            }

            // ===== TOOLS MENU =====
            if (ImGui.BeginMenu("Tools"))
            {
                if (ImGui.MenuItem("Reload Water Shader", "F5"))
                {
                    try
                    {
                        Engine.Rendering.ShaderLibrary.ReloadShader("Water");
                        LogManager.LogInfo("✅ Water shader reloaded!", "EditorUI");
                    }
                    catch (Exception ex)
                    {
                        LogManager.LogWarning($"❌ Failed to reload Water shader: {ex.Message}", "EditorUI");
                    }
                }
                if (ImGui.MenuItem("Reload All Shaders"))
                {
                    LogManager.LogInfo("🔄 Reload All Shaders: Feature coming soon!", "EditorUI");
                }
                
                ImGui.Separator();
                if (ImGui.BeginMenu("Project Settings"))
                {
                    if (ImGui.MenuItem("Input Settings..."))
                    {
                        InputSettingsPanel.Open();
                    }
                    if (ImGui.MenuItem("Collision Layers..."))
                    {
                        CollisionLayersPanel.Open();
                    }
                    if (ImGui.MenuItem("Quality Settings..."))
                    {
                        LogManager.LogInfo("⚙️ Quality Settings: Feature coming soon!", "EditorUI");
                    }
                    ImGui.EndMenu();
                }
                
                ImGui.Separator();
                if (ImGui.MenuItem("Package Manager..."))
                {
                    LogManager.LogInfo("📦 Package Manager: Feature coming soon!", "EditorUI");
                }
                ImGui.EndMenu();
            }

            // ===== HELP MENU =====
            if (ImGui.BeginMenu("Help"))
            {
                ImGui.PushStyleColor(ImGuiCol.Text, UI.Primary);
                ImGui.Text($"AstrildApex Engine v{Engine.Core.EngineInfo.Version}");
                ImGui.PopStyleColor();
                
                ImGui.Separator();
                if (ImGui.MenuItem("Documentation"))
                {
                    LogManager.LogInfo("📚 Documentation: Opening browser...", "EditorUI");
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "https://github.com/philippaudier/AstrildApex",
                        UseShellExecute = true
                    });
                }
                if (ImGui.MenuItem("Report Bug"))
                {
                    LogManager.LogInfo("🐛 Report Bug: Opening GitHub Issues...", "EditorUI");
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "https://github.com/philippaudier/AstrildApex/issues",
                        UseShellExecute = true
                    });
                }
                ImGui.Separator();
                if (ImGui.MenuItem("About AstrildApex"))
                {
                    LogManager.LogInfo($"ℹ️ AstrildApex Engine v{Engine.Core.EngineInfo.Version}", "EditorUI");
                }
                ImGui.EndMenu();
            }
            
            // Play Mode Controls - centered in menu bar
            DrawPlayModeControls();
            
            ImGui.EndMainMenuBar();
        }
        
        // Dockspace sous la barre de titre OS & la main menu bar
        ImGui.DockSpaceOverViewport(0, vp, ImGuiDockNodeFlags.None, IntPtr.Zero);

        // --- Global shortcuts ---
        // À placer après ImGui.DockSpaceOverViewport(...)
        var io = ImGui.GetIO();

        // Ne pas voler le clavier si on tape du texte dans un InputText/Drag
        bool typingText = io.WantTextInput;

        // Skip editor shortcuts if in play mode and setting is enabled
        if (EditorSettings.ShortcutsDisableInPlayMode && PlayMode.IsPlaying)
            return;

        var sc2 = MainViewport.Renderer?.Scene;
        
        // Undo/Redo shortcuts
        if (!typingText && sc2 != null)
        {
            if (ShortcutHelper.IsShortcutPressed(EditorSettings.ShortcutUndo))
                UndoRedo.Undo(sc2);

            if (ShortcutHelper.IsShortcutPressed(EditorSettings.ShortcutRedo))
                UndoRedo.Redo(sc2);
        }

        // Scene shortcuts
        if (!typingText)
        {
            if (ShortcutHelper.IsShortcutPressed(EditorSettings.ShortcutNewScene))
                SceneManager.NewScene();
            
            if (ShortcutHelper.IsShortcutPressed(EditorSettings.ShortcutOpenScene))
                SceneManager.OpenScene();
            
            if (ShortcutHelper.IsShortcutPressed(EditorSettings.ShortcutSaveScene))
                SceneManager.SaveScene();
            
            if (ShortcutHelper.IsShortcutPressed(EditorSettings.ShortcutSaveSceneAs))
                SceneManager.SaveSceneAs();

            // Duplicate selected entities (Ctrl+D)
            if (ShortcutHelper.IsShortcutPressed(EditorSettings.ShortcutDuplicate))
            {
                if (sc2 != null && Selection.Selected.Count > 0)
                {
                    var duplicatedIds = new HashSet<uint>();
                    foreach (var id in Selection.Selected.ToArray())
                    {
                        var entity = sc2.GetById(id);
                        if (entity != null)
                        {
                            var duplicate = HierarchyPanel.DuplicateEntity(sc2, entity);
                            if (duplicate != null)
                                duplicatedIds.Add(duplicate.Id);
                        }
                    }
                    // Select the duplicates
                    if (duplicatedIds.Count > 0)
                        Selection.ReplaceMany(duplicatedIds);
                }
            }

            // Delete selected entities (Del key)
            if (ShortcutHelper.IsShortcutPressed(EditorSettings.ShortcutDelete))
            {
                if (sc2 != null && Selection.Selected.Count > 0)
                {
                    foreach (var id in Selection.Selected.ToArray())
                    {
                        var entity = sc2.GetById(id);
                        if (entity != null)
                            HierarchyPanel.DeleteRecursive(sc2, entity);
                    }
                    Selection.Clear();
                }
            }

            // Select all entities (Ctrl+A)
            if (ShortcutHelper.IsShortcutPressed(EditorSettings.ShortcutSelectAll))
            {
                if (sc2 != null)
                {
                    var allIds = sc2.Entities.Select(e => e.Id).ToList();
                    Selection.ReplaceMany(allIds);
                }
            }

            // Deselect all (Ctrl+Shift+A)
            if (ShortcutHelper.IsShortcutPressed(EditorSettings.ShortcutDeselectAll))
            {
                Selection.Clear();
            }

            // Create empty entity (Ctrl+Shift+N)
            if (ShortcutHelper.IsShortcutPressed(EditorSettings.ShortcutCreateEmpty))
            {
                if (sc2 != null)
                {
                    var newEntity = new Engine.Scene.Entity
                    {
                        Id = sc2.GetNextEntityId()
                    };
                    newEntity.Name = "Empty Entity";
                    sc2.Entities.Add(newEntity);
                    Selection.SetSingle(newEntity.Id);
                }
            }

            // Play/Pause toggle
            if (ShortcutHelper.IsShortcutPressed(EditorSettings.ShortcutPlayPause))
            {
                if (PlayMode.State == PlayMode.PlayState.Edit)
                    PlayMode.Play();
                else if (PlayMode.State == PlayMode.PlayState.Playing)
                    PlayMode.Stop();
            }
        }
    }

    public static void DrawDefaultLayoutWindows()
    {
        // Panneaux réels (branchés sur la scène + sélection)
        if (ShowHierarchy) { PanelProfiler.BeginPanel("Hierarchy"); HierarchyPanel.Draw(); PanelProfiler.EndPanel("Hierarchy"); }
        if (ShowInspector) { PanelProfiler.BeginPanel("Inspector"); InspectorPanel.Draw(); PanelProfiler.EndPanel("Inspector"); }
        if (ShowEnvironment) { PanelProfiler.BeginPanel("Environment"); EnvironmentPanel.Draw(); PanelProfiler.EndPanel("Environment"); }
        if (ShowRenderingSettings) { PanelProfiler.BeginPanel("RenderingSettings"); RenderingSettingsPanel.Draw(); PanelProfiler.EndPanel("RenderingSettings"); }
        if (ShowAssets) { PanelProfiler.BeginPanel("Assets"); AssetsPanel.Draw(); PanelProfiler.EndPanel("Assets"); }
        if (ShowConsole) { PanelProfiler.BeginPanel("Console"); ConsolePanel.Draw(); PanelProfiler.EndPanel("Console"); }
        // REMOVED: GamePanel is now integrated into ViewportPanelModern (switches between Edit/Play modes)
        // if (ShowGame) { PanelProfiler.BeginPanel("Game"); GamePanel.Draw(); PanelProfiler.EndPanel("Game"); }
        if (ShowAudioMixer) { PanelProfiler.BeginPanel("AudioMixer"); AudioMixer.Draw(); PanelProfiler.EndPanel("AudioMixer"); }

        PanelProfiler.BeginPanel("Viewport");
        MainViewport.Draw();
        PanelProfiler.EndPanel("Viewport");

        if (ShowDemoWindow) ImGui.ShowDemoWindow(ref ShowDemoWindow);
        if (ShowIconManager) IconManager.RenderIconsTestWindow();

        // Engine systems profiler panel
        PanelProfiler.BeginPanel("SystemsProfiler");
        SystemsProfilerPanel.Draw();
        PanelProfiler.EndPanel("SystemsProfiler");

        // Render settings panels
        PanelProfiler.BeginPanel("InputSettings");
        InputSettingsPanel.Draw();
        PanelProfiler.EndPanel("InputSettings");

        PanelProfiler.BeginPanel("CollisionLayers");
        CollisionLayersPanel.Draw();
        PanelProfiler.EndPanel("CollisionLayers");

        PanelProfiler.BeginPanel("Preferences");
        Preferences.Draw();
        PanelProfiler.EndPanel("Preferences");

        // Render scene management dialogs
        SceneManager.RenderDialogs();

        // Render global progress popup (if any)
        Editor.UI.ProgressManager.Render();

        // Performance overlay (drawn last, after all panels)
        if (ShowPerformanceOverlay)
        {
            PerformanceOverlay.Visible = true;
            PerformanceOverlay.Draw(ImGui.GetIO().DeltaTime);
        }
        else
        {
            PerformanceOverlay.Visible = false;
        }
    }

    private static void DrawPlayModeControls()
    {
        // Center the play controls in the menu bar
        var menuBarWidth = ImGui.GetContentRegionAvail().X;
        var buttonWidth = 30f;
        // We'll include a small width for the resolution dropdown to the left of play
        var resDropdownWidth = 160f;
        var totalWidth = resDropdownWidth + buttonWidth * 3 + ImGui.GetStyle().ItemSpacing.X * 3; // dropdown + 3 buttons + spacing
        var centerPos = (menuBarWidth - totalWidth) * 0.5f;
        
        // Start the group at centerPos
        ImGui.SameLine(centerPos);

        // --- Resolution dropdown (left of play) ---
        ImGui.PushItemWidth(resDropdownWidth - 8);
        try
        {
            var presets = new string[] {
                "Panel Size (Auto)",
                "1920 x 1080 (16:9)",
                "1600 x 900 (16:9)",
                "1280 x 720 (16:9)",
                "1366 x 768 (16:9)",
                "1024 x 768 (4:3)",
                "800 x 600 (4:3)",
                "Custom..."
            };

            // Stored index mapping: EditorSettings.ViewportResolutionPresetIndex
            // -1 => Panel Size (we'll map to 0 in UI)
            int stored = Editor.State.EditorSettings.ViewportResolutionPresetIndex;
            int uiIndex = stored == -1 ? 0 : stored + 1; // shift by 1 because 0=Panel Size

            if (ImGui.BeginCombo("##viewport_res", presets[uiIndex]))
            {
                for (int i = 0; i < presets.Length; i++)
                {
                    bool selected = (i == uiIndex);
                    if (ImGui.Selectable(presets[i], selected))
                    {
                        uiIndex = i;
                        // Map back to stored value
                        if (uiIndex == 0) Editor.State.EditorSettings.ViewportResolutionPresetIndex = -1;
                        else if (uiIndex == presets.Length - 1) Editor.State.EditorSettings.ViewportResolutionPresetIndex = presets.Length - 2; // custom -> special index (we'll treat last as custom)
                        else Editor.State.EditorSettings.ViewportResolutionPresetIndex = uiIndex - 1;
                    }
                    if (selected) ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }

            // If Custom selected, show compact inputs for width/height and aspect lock
            if (uiIndex == presets.Length - 1)
            {
                ImGui.SameLine();
                int w = Editor.State.EditorSettings.ViewportCustomWidth;
                int h = Editor.State.EditorSettings.ViewportCustomHeight;
                ImGui.PushItemWidth(60);
                if (ImGui.InputInt("W##vp", ref w)) Editor.State.EditorSettings.ViewportCustomWidth = Math.Max(8, w);
                ImGui.SameLine();
                if (ImGui.InputInt("H##vp", ref h)) Editor.State.EditorSettings.ViewportCustomHeight = Math.Max(8, h);
                ImGui.PopItemWidth();

                ImGui.SameLine();
                bool lockRatio = Editor.State.EditorSettings.ViewportLockAspect;
                if (ImGui.Checkbox("Lock Ratio", ref lockRatio)) Editor.State.EditorSettings.ViewportLockAspect = lockRatio;
            }
        }
        finally { ImGui.PopItemWidth(); }

        ImGui.SameLine();
        
        // Play Mode state indicator and controls
        var state = PlayMode.State;
        
        // Play button (or Stop if playing)
        if (state == PlayMode.PlayState.Edit)
        {
            ImGui.PushID("play_btn");
            if (IconManager.IconButton("play", "Enter Play Mode"))
            {
                PlayMode.Play();
            }
            ImGui.PopID();
        }
        else
        {
            ImGui.PushID("stop_btn");
            bool stopButtonPressed = IconManager.IconButton("stop", "Stop Play Mode");
            if (stopButtonPressed)
            {
                PlayMode.Stop();
            }
            else
            {
            }
            ImGui.PopID();
        }
        
        ImGui.SameLine();
        
        // Pause/Resume button (only available in Play Mode)
        if (state != PlayMode.PlayState.Edit)
        {
            if (state == PlayMode.PlayState.Playing)
            {
                if (IconManager.IconButton("pause", "Pause"))
                {
                    PlayMode.TogglePause();
                }
            }
            else if (state == PlayMode.PlayState.Paused)
            {
                if (IconManager.IconButton("play", "Resume"))
                {
                    PlayMode.TogglePause();
                }
            }
        }
        else
        {
            ImGui.BeginDisabled();
            IconManager.IconButton("pause", "Available in Play Mode");
            ImGui.EndDisabled();
        }
        
        ImGui.SameLine();
        
        // Step button (only available when paused)
        if (state == PlayMode.PlayState.Paused)
        {
            if (IconManager.IconButton("step", "Step One Frame"))
            {
                PlayMode.Step();
            }
        }
        else
        {
            ImGui.BeginDisabled();
            IconManager.IconButton("step", "Available when Paused");
            ImGui.EndDisabled();
        }
        
        // State indicator text
        ImGui.SameLine();
        ImGui.Spacing();
        ImGui.SameLine();
        var stateColor = state switch
        {
            PlayMode.PlayState.Edit => new Vector4(0.7f, 0.7f, 0.7f, 1.0f),      // Gray
            PlayMode.PlayState.Playing => new Vector4(0.2f, 0.8f, 0.2f, 1.0f),  // Green
            PlayMode.PlayState.Paused => new Vector4(1.0f, 0.8f, 0.2f, 1.0f),   // Orange
            _ => Vector4.One
        };
        
        var stateText = state switch
        {
            PlayMode.PlayState.Edit => "EDIT",
            PlayMode.PlayState.Playing => "PLAYING",
            PlayMode.PlayState.Paused => "PAUSED",
            _ => "UNKNOWN"
        };
        
        ImGui.TextColored(stateColor, stateText);
    }

    // Simple UI preferences persistence
    private static string GetPreferencesPath() =>
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AstrildApex", "ui_preferences.txt");

    private static bool LoadUIPreference(string key, bool defaultValue)
    {
        try
        {
            var path = GetPreferencesPath();
            if (!System.IO.File.Exists(path)) return defaultValue;

            var lines = System.IO.File.ReadAllLines(path);
            foreach (var line in lines)
            {
                var parts = line.Split('=');
                if (parts.Length == 2 && parts[0].Trim() == key)
                {
                    return bool.Parse(parts[1].Trim());
                }
            }
        }
        catch { }
        return defaultValue;
    }

    private static void SaveUIPreference(string key, bool value)
    {
        try
        {
            var path = GetPreferencesPath();
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);

            var preferences = new System.Collections.Generic.Dictionary<string, string>();

            // Load existing preferences
            if (System.IO.File.Exists(path))
            {
                var lines = System.IO.File.ReadAllLines(path);
                foreach (var line in lines)
                {
                    var parts = line.Split('=');
                    if (parts.Length == 2)
                    {
                        preferences[parts[0].Trim()] = parts[1].Trim();
                    }
                }
            }

            // Update preference
            preferences[key] = value.ToString();

            // Save all preferences
            var output = new System.Text.StringBuilder();
            foreach (var kvp in preferences)
            {
                output.AppendLine($"{kvp.Key}={kvp.Value}");
            }

            System.IO.File.WriteAllText(path, output.ToString());
        }
        catch { }
    }

    private static void ImportModel()
    {
        try
        {
            Engine.Utils.DebugLogger.Log("[EditorUI] Opening Import Model dialog...");

            // Open native file dialog - only mainstream formats
            var result = NativeFileDialogSharp.Dialog.FileOpen("fbx,obj,gltf,glb,dae");

            if (result.IsOk)
            {
                var sourceFile = result.Path;
                var assetsRoot = Editor.State.ProjectPaths.AssetsDir;
                var fileName = System.IO.Path.GetFileName(sourceFile);

                LogManager.LogInfo($"Importing model: {fileName}", "EditorUI");
                Engine.Utils.DebugLogger.Log($"[EditorUI] Importing model: {sourceFile}");

                // Defer to end of frame, then launch background job
                Editor.Utils.DeferredActions.Enqueue(() =>
                {
                    try
                    {
                        ModelImportJob.Run(sourceFile, assetsRoot, "Models", fileName, guid =>
                        {
                            Engine.Utils.DebugLogger.Log($"[EditorUI] Model imported successfully with GUID: {guid}");
                            LogManager.LogInfo($"✓ Model imported successfully: {fileName}", "EditorUI");
                            LogManager.LogInfo($"GUID: {guid}", "EditorUI");
                            LogManager.LogInfo($"Location: Assets/Models/{fileName}", "EditorUI");
                        });
                    }
                    catch (Exception ex)
                    {
                        LogManager.LogError($"[EditorUI] Failed to start import job: {ex.Message}", "EditorUI");
                    }
                });
            }
            else if (result.IsCancelled)
            {
                Engine.Utils.DebugLogger.Log("[EditorUI] Import cancelled by user");
            }
                else
            {
                Engine.Utils.DebugLogger.Log($"[EditorUI] Import dialog error");
                LogManager.LogWarning("✗ Error opening file dialog", "EditorUI");
            }
        }
        catch (Exception ex)
        {
            Editor.UI.ProgressManager.Hide(); // Hide progress on error
            Engine.Utils.DebugLogger.Log($"[EditorUI] Failed to import model: {ex.Message}");
            LogManager.LogError($"✗ Failed to import model: {ex.Message}", "EditorUI");
            LogManager.LogError($"{ex.StackTrace}", "EditorUI");
        }
    }
}
using System;
using System.IO;
using System.Linq;
using Editor.Logging;
using Editor.Serialization;
using Editor.State;
using Editor.Panels;
using Engine.Scene;
using ImGuiNET;
using Editor.UI;

namespace Editor.SceneManagement
{
    /// <summary>
    /// Unity-like scene management system
    /// </summary>
    public static class SceneManager
    {
        private static string? _currentScenePath = null;
        private static bool _isSceneModified = false;
        private static bool _showSaveAsDialog = false;
        private static bool _showOpenDialog = false;
        private static string _saveAsFileName = "NewScene";
        private static string _selectedFolder = "";
        private static string[] _availableFolders = Array.Empty<string>();
        private static int _selectedFolderIndex = 0;
        private static string[] _availableScenes = Array.Empty<string>();
        private static int _selectedSceneIndex = 0;
        
        public static string? CurrentScenePath => _currentScenePath;
        public static bool HasCurrentScene => !string.IsNullOrEmpty(_currentScenePath);
        public static bool IsSceneModified => _isSceneModified;
        public static string CurrentSceneName => HasCurrentScene ? Path.GetFileNameWithoutExtension(_currentScenePath!) : "Untitled Scene";
        
        static SceneManager()
        {
            RefreshFolders();
            RefreshScenes();
        }
        
        public static void LoadLastSceneOnStartup()
        {
            LogManager.LogInfo("LoadLastSceneOnStartup() called", "SceneManager");
            if (EditorSettings.HasValidLastScene())
            {
                var lastScenePath = EditorSettings.LastOpenedScene;
                LogManager.LogInfo($"Last scene path: {lastScenePath}", "SceneManager");
                if (!Path.IsPathRooted(lastScenePath))
                {
                    lastScenePath = Path.Combine(ProjectPaths.ProjectRoot, lastScenePath);
                }

                LogManager.LogInfo($"Loading scene from: {lastScenePath}", "SceneManager");
                LoadSceneFromPath(lastScenePath, showProgress: false); // Don't show progress during startup
            }
            else
            {
                LogManager.LogInfo("No valid last scene to load", "SceneManager");
            }
        }
        
        public static void NewScene()
        {
            var sc = EditorUI.MainViewport.Renderer?.Scene;
            if (sc == null) return;
            
            sc.Entities.Clear();
            Selection.ActiveEntityId = 0;
            _currentScenePath = null;
            _isSceneModified = false;
            
            // Reset selection and inspector
            Selection.ClearAll();
            EditorUI.MainViewport.UpdateGizmoPivot();
            UndoRedo.RaiseAfterChange();
            
            // Update window title
            Program.UpdateWindowTitle();
        }
        
        public static void SaveScene()
        {
            if (HasCurrentScene)
            {
                // Save to existing path - defer to after ImGui frame to avoid nested ImGui NewFrame
                var fullPath = _currentScenePath!;
                Editor.Utils.DeferredActions.Enqueue(() => SaveSceneToPath(fullPath));
            }
            else
            {
                // First time save - show Save As dialog (UI popup is safe to open now)
                ShowSaveAsDialog();
            }
        }
        
        public static void SaveSceneAs()
        {
            ShowSaveAsDialog();
        }
        
        public static void OpenScene()
        {
            ShowOpenDialog();
        }
        
        public static void MarkSceneAsModified()
        {
            if (!_isSceneModified)
            {
                _isSceneModified = true;
                Program.UpdateWindowTitle();
            }
        }
        
        private static void ShowSaveAsDialog()
        {
            _showSaveAsDialog = true;
            _saveAsFileName = HasCurrentScene ? CurrentSceneName : "NewScene";
            RefreshFolders();
        }
        
        private static void ShowOpenDialog()
        {
            _showOpenDialog = true;
            RefreshScenes();
        }
        
        private static void RefreshFolders()
        {
            try
            {
                var assetsDir = ProjectPaths.AssetsDir;
                Directory.CreateDirectory(assetsDir);
                
                var folders = Directory.GetDirectories(assetsDir, "*", SearchOption.AllDirectories)
                    .Select(d => Path.GetRelativePath(assetsDir, d))
                    .Prepend("") // Root Assets folder
                    .ToArray();
                    
                _availableFolders = folders;
                _selectedFolder = _availableFolders.FirstOrDefault() ?? "";
                _selectedFolderIndex = 0;
            }
            catch
            {
                _availableFolders = new[] { "" };
                _selectedFolder = "";
                _selectedFolderIndex = 0;
            }
        }
        
        private static void RefreshScenes()
        {
            try
            {
                var projectRoot = ProjectPaths.ProjectRoot;
                
                var scenes = Directory.GetFiles(projectRoot, "*.scene", SearchOption.AllDirectories)
                    .Where(f => !Path.GetFileName(f).Contains(".backup")) // Exclude backup files
                    .Select(f => Path.GetRelativePath(projectRoot, f))
                    .ToArray();
                    
                _availableScenes = scenes;
                _selectedSceneIndex = 0;
            }
            catch
            {
                _availableScenes = Array.Empty<string>();
                _selectedSceneIndex = 0;
            }
        }
        
        private static void SaveSceneToPath(string fullPath)
        {
            var sc = EditorUI.MainViewport.Renderer?.Scene;
            if (sc == null) return;
            ProgressManager.StepTracker? tracker = null;
            try
            {
                tracker = new ProgressManager.StepTracker("Saving Scene", 2);
                tracker.NextStep("Serializing scene...");

                LogManager.LogInfo($"Saving scene to: {fullPath}", "SceneManager");
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? "");
                var result = SceneSerializer.Save(sc, fullPath);

                if (result.Success)
                {
                    tracker.NextStep("Finalizing...");

                    _currentScenePath = fullPath;
                    _isSceneModified = false; // Reset modified flag after save
                    // Save relative path for portability
                    var relativePath = Path.GetRelativePath(ProjectPaths.ProjectRoot, fullPath);
                    EditorSettings.LastOpenedScene = relativePath;

                    var renderer = EditorUI.MainViewport.Renderer;
                    if (renderer != null)
                    {
                        try
                        {
                            EditorSettings.ViewportCameraState = renderer.GetOrbitCameraState();
                        }
                        catch (Exception ex)
                        {
                            LogManager.LogWarning($"Failed to persist camera state: {ex.Message}", "SceneManager");
                        }
                    }

                    Program.UpdateWindowTitle();
                    LogManager.LogInfo("✓ Scene saved successfully", "SceneManager");
                    tracker.Complete("Scene saved");
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"✗ Failed to save scene: {ex.Message}", "SceneManager");
                try { tracker?.Complete("Failed"); } catch { }
            }
        }
        
        private static void LoadSceneFromPath(string fullPath, bool showProgress = true)
        {
            var sc = EditorUI.MainViewport.Renderer?.Scene;
            if (sc == null) return;

            ProgressManager.StepTracker? tracker = null;
            try
            {
                LogManager.LogInfo($"Loading scene from: {fullPath}", "SceneManager");

                // Show progress UI if requested
                if (showProgress)
                {
                    tracker = new ProgressManager.StepTracker("Loading Scene", 4);
                    tracker.NextStep("Parsing scene file...");
                }

                Editor.Utils.StartupProfiler.BeginSection("  SceneSerializer.Load()");
                var result = SceneSerializer.Load(sc, fullPath);
                Editor.Utils.StartupProfiler.EndSection();

                if (result.Success)
                {
                    _currentScenePath = fullPath;
                    _isSceneModified = false; // Reset modified flag after load

                    // PERFORMANCE: Give background threads time to decode images, then flush all pending uploads
                    // This avoids texture pop-in over multiple frames during scene loading
                    try
                    {
                        if (tracker != null) tracker.NextStep("Waiting for background tasks...");
                        LogManager.LogInfo("Waiting for background texture loading...", "SceneManager");

                        Editor.Utils.StartupProfiler.BeginSection("  Wait for background threads");
                        System.Threading.Thread.Sleep(100); // Give background threads time to finish decoding
                        Editor.Utils.StartupProfiler.EndSection();

                        if (tracker != null) tracker.NextStep("Uploading textures to GPU...");
                        Editor.Utils.StartupProfiler.BeginSection("  FlushPendingUploads()");
                        var sw = System.Diagnostics.Stopwatch.StartNew();

                        // Upload all pending textures in batches until complete
                        int totalUploaded = 0;
                        int batchCount = 0;
                        int uploaded;
                        const int maxBatches = 50; // Safety limit to prevent infinite loop

                        do
                        {
                            // Wait a bit for background decoding to catch up
                            if (batchCount > 0)
                                System.Threading.Thread.Sleep(50);

                            uploaded = Engine.Rendering.TextureCache.FlushPendingUploads(100);
                            totalUploaded += uploaded;
                            batchCount++;
                        }
                        while (uploaded > 0 && batchCount < maxBatches);

                        sw.Stop();
                        Editor.Utils.StartupProfiler.EndSection();

                        if (totalUploaded > 0)
                        {
                            LogManager.LogInfo($"⚡ Uploaded {totalUploaded} texture(s) to GPU in {sw.ElapsedMilliseconds}ms ({batchCount} batches)", "SceneManager");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogManager.LogWarning($"FlushPendingUploads failed: {ex.Message}", "SceneManager");
                    }

                    // Clear material cache to force reload with fresh texture handles
                    Engine.Rendering.MaterialRuntime.ClearGlobalCache();

                    // Save as last opened scene for auto-loading
                    var relativePath = Path.GetRelativePath(ProjectPaths.ProjectRoot, fullPath);
                    EditorSettings.LastOpenedScene = relativePath;

                    // Initialize terrain generators from persisted data
                    foreach (var entity in sc.Entities)
                    {
                        if (entity.HasComponent<Engine.Components.Terrain>())
                        {
                            // Terrain initialization happens on demand via GenerateTerrain()
                        }
                    }

                    // Reset selection and inspector
                    if (tracker != null) tracker.NextStep("Finalizing...");
                    Selection.ClearAll();
                    Selection.ActiveEntityId = 0;
                    EditorUI.MainViewport.UpdateGizmoPivot();
                    UndoRedo.RaiseAfterChange();

                    Program.UpdateWindowTitle();

                    foreach (var warning in result.Warnings)
                    {
                    }

                    LogManager.LogInfo("✓ Scene loaded successfully", "SceneManager");
                    tracker?.Complete("Scene loaded");
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"✗ Failed to load scene: {ex.Message}", "SceneManager");
                try { tracker?.Complete("Failed"); } catch { }
            }
        }
        
        public static void RenderDialogs()
        {
            RenderSaveAsDialog();
            RenderOpenDialog();
        }
        
        private static void RenderSaveAsDialog()
        {
            if (!_showSaveAsDialog) return;
            
            ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new System.Numerics.Vector2(0.5f));
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(500, 300), ImGuiCond.FirstUseEver);
            
            if (ImGui.Begin("Save Scene As", ref _showSaveAsDialog, ImGuiWindowFlags.Modal | ImGuiWindowFlags.NoResize))
            {
                ImGui.Text("Save scene in Assets folder:");
                ImGui.Separator();
                
                // Folder selection
                ImGui.Text("Folder:");
                ImGui.SetNextItemWidth(-1);
                if (ImGui.Combo("##Folder", ref _selectedFolderIndex, _availableFolders, _availableFolders.Length))
                {
                    _selectedFolder = _availableFolders[_selectedFolderIndex];
                }
                
                ImGui.Spacing();
                
                // File name input
                ImGui.Text("Scene name:");
                ImGui.SetNextItemWidth(-1);
                ImGui.InputText("##FileName", ref _saveAsFileName, 255);
                
                // Ensure .scene extension
                if (!_saveAsFileName.EndsWith(".scene", StringComparison.OrdinalIgnoreCase))
                {
                    if (ImGui.IsItemDeactivatedAfterEdit())
                    {
                        if (!string.IsNullOrEmpty(_saveAsFileName) && !_saveAsFileName.EndsWith(".scene", StringComparison.OrdinalIgnoreCase))
                        {
                            _saveAsFileName += ".scene";
                        }
                    }
                }
                
                ImGui.Spacing();
                ImGui.Separator();
                
                // Preview path
                var previewPath = Path.Combine(_selectedFolder, _saveAsFileName);
                ImGui.Text($"Save to: Assets/{previewPath}");
                
                ImGui.Spacing();
                
                // Buttons
                if (ImGui.Button("Save", new System.Numerics.Vector2(100, 0)))
                {
                    if (!string.IsNullOrEmpty(_saveAsFileName))
                    {
                        var assetsDir = ProjectPaths.AssetsDir;
                        var targetFolder = Path.Combine(assetsDir, _selectedFolder);
                        var fullPath = Path.Combine(targetFolder, _saveAsFileName);
                        
                        SaveSceneToPath(fullPath);
                        _showSaveAsDialog = false;
                    }
                }
                
                ImGui.SameLine();
                if (ImGui.Button("Cancel", new System.Numerics.Vector2(100, 0)))
                {
                    _showSaveAsDialog = false;
                }
                
                // Auto-add .scene extension helper
                if (!_saveAsFileName.EndsWith(".scene", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(_saveAsFileName))
                {
                    ImGui.Spacing();
                    ImGui.TextColored(new System.Numerics.Vector4(1f, 1f, 0f, 1f), "Note: .scene extension will be added automatically");
                }
            }
            ImGui.End();
        }
        
        private static void RenderOpenDialog()
        {
            if (!_showOpenDialog) return;
            
            ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new System.Numerics.Vector2(0.5f));
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(400, 300), ImGuiCond.FirstUseEver);
            
            if (ImGui.Begin("Open Scene", ref _showOpenDialog, ImGuiWindowFlags.Modal | ImGuiWindowFlags.NoResize))
            {
                ImGui.Text("Select scene to open (searching from project root):");
                ImGui.Separator();
                
                if (_availableScenes.Length == 0)
                {
                    ImGui.Text("No .scene files found in project.");
                    ImGui.Spacing();
                    if (ImGui.Button("Refresh"))
                    {
                        RefreshScenes();
                    }
                }
                else
                {
                    // Scene list
                    ImGui.BeginChild("SceneList", new System.Numerics.Vector2(-1, 150), ImGuiChildFlags.Borders);
                    
                    for (int i = 0; i < _availableScenes.Length; i++)
                    {
                        var isSelected = i == _selectedSceneIndex;
                        if (ImGui.Selectable(_availableScenes[i], isSelected))
                        {
                            _selectedSceneIndex = i;
                        }
                        
                        // Double-click to open
                        if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                        {
                            var projectRoot = ProjectPaths.ProjectRoot;
                            var fullPath = Path.Combine(projectRoot, _availableScenes[_selectedSceneIndex]);
                            // Defer scene load to after ImGui frame to avoid nested NewFrame/ForceRender
                            Editor.Utils.DeferredActions.Enqueue(() => LoadSceneFromPath(fullPath));
                            _showOpenDialog = false;
                        }
                    }
                    
                    ImGui.EndChild();
                    
                    ImGui.Spacing();
                    ImGui.Separator();
                    
                    // Selected scene info
                    if (_selectedSceneIndex < _availableScenes.Length)
                    {
                        ImGui.Text($"Selected: {_availableScenes[_selectedSceneIndex]}");
                    }
                }
                
                ImGui.Spacing();
                
                // Buttons
                var canOpen = _availableScenes.Length > 0 && _selectedSceneIndex < _availableScenes.Length;
                
                if (!canOpen)
                {
                    ImGui.BeginDisabled();
                }
                
                if (ImGui.Button("Open", new System.Numerics.Vector2(100, 0)))
                {
                    var projectRoot = ProjectPaths.ProjectRoot;
                    var fullPath = Path.Combine(projectRoot, _availableScenes[_selectedSceneIndex]);
                    Editor.Utils.DeferredActions.Enqueue(() => LoadSceneFromPath(fullPath));
                    _showOpenDialog = false;
                }
                
                if (!canOpen)
                {
                    ImGui.EndDisabled();
                }
                
                ImGui.SameLine();
                if (ImGui.Button("Refresh", new System.Numerics.Vector2(100, 0)))
                {
                    RefreshScenes();
                }
                
                ImGui.SameLine();
                if (ImGui.Button("Cancel", new System.Numerics.Vector2(100, 0)))
                {
                    _showOpenDialog = false;
                }
            }
            ImGui.End();
        }
    }
}
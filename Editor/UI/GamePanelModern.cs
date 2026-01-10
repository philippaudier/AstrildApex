using ImGuiNET;
using System.Numerics;
using System.Linq;
using Engine.Scene;
using Engine.Components;
using Editor.Rendering;
using Editor.Panels;
using Editor;
using Editor.State;

namespace AstrildApex.Editor.UI;

/// <summary>
/// Modern Game Panel with play controls, camera selection and performance overlays
/// </summary>
public class GamePanelModern
{
    private readonly GamePlayControls _playControls;
    private readonly GameTopRightControls _topRightControls;
    private readonly GamePerformanceOverlays _overlays;
    
    private ViewportRenderer? _gameRenderer;
    private uint _selectedCameraEntityId = 0;
    
    // Cache last render dimensions
    private int _lastRenderWidth = 0;
    private int _lastRenderHeight = 0;

    public GamePanelModern()
    {
        _playControls = new GamePlayControls();
        _topRightControls = new GameTopRightControls();
        _overlays = new GamePerformanceOverlays();
    }

    // Reusable temporaries to avoid per-frame allocations when enumerating cameras
    private static readonly System.Collections.Generic.List<uint> _tmpCameraIds = new System.Collections.Generic.List<uint>();
    private static readonly System.Collections.Generic.List<string> _tmpCameraNames = new System.Collections.Generic.List<string>();

    public void Draw(ImGuiIOPtr io)
    {
        // Check if Escape key pressed in fullscreen mode (exit fullscreen)
        if (PlayMode.IsInPlayMode && EditorSettings.ViewportFullscreen && ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            EditorSettings.ViewportFullscreen = false;
            EditorSettings.ViewportResolutionPresetIndex = -1; // Reset to Panel Size
        }

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));

        // Fullscreen mode in Play Mode: create borderless fullscreen window
        bool isFullscreen = PlayMode.IsInPlayMode && EditorSettings.ViewportFullscreen;
        ImGuiWindowFlags windowFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;

        if (isFullscreen)
        {
            // Fullscreen: no title bar, no resize, no move, covering entire viewport
            windowFlags |= ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse;
            var viewport = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(viewport.Pos);
            ImGui.SetNextWindowSize(viewport.Size);
        }

        bool open = true;
        if (ImGui.Begin(isFullscreen ? "##GameFullscreen" : "Game", ref open, windowFlags))
        {
            // Get scene (runtime in Play Mode, otherwise editor scene)
            Scene? scene;
            if (PlayMode.IsInPlayMode)
            {
                scene = PlayMode.PlayScene;
            }
            else
            {
                scene = EditorUI.MainViewport.Renderer?.Scene;
            }

            if (scene == null)
            {
                ImGui.TextDisabled("Scene not available.");
                ImGui.End();
                ImGui.PopStyleVar();
                return;
            }

            // Find camera to use
            CameraComponent? camera = GetSelectedCamera(scene);

            // Build camera list for selector (avoid LINQ allocations)
            _tmpCameraIds.Clear();
            _tmpCameraNames.Clear();
            // Utilise ImGuiListClipper pour optimiser le parcours si beaucoup d'entités
            int entityCount = scene.Entities.Count;
            for (int i = 0; i < entityCount; i++)
            {
                var e = scene.Entities[i];
                var cam = e.GetComponent<CameraComponent>();
                if (cam == null) continue;
                _tmpCameraIds.Add(e.Id);
                _tmpCameraNames.Add(!string.IsNullOrEmpty(e.Name) ? e.Name : $"Entity {e.Id}");
            }
            uint[] cameraEntityIds = _tmpCameraIds.Count > 0 ? _tmpCameraIds.ToArray() : Array.Empty<uint>();
            string[] cameraNames = _tmpCameraNames.Count > 0 ? _tmpCameraNames.ToArray() : Array.Empty<string>();

            // Auto-select main camera if none selected
            if (_selectedCameraEntityId == 0 && cameraEntityIds.Length > 0)
            {
                var mainCam = scene.GetMainCamera();
                if (mainCam?.Entity != null)
                {
                    _selectedCameraEntityId = mainCam.Entity.Id;
                }
                else
                {
                    _selectedCameraEntityId = cameraEntityIds[0];
                }
            }

            // Determine resolution to use
            var avail = ImGui.GetContentRegionAvail();
            int w, h;

            if (PlayMode.IsInPlayMode && !isFullscreen)
            {
                // In Play Mode (not fullscreen): use resolution from dropdown
                GetPlayModeResolution(avail, out w, out h);
            }
            else
            {
                // Edit Mode OR Fullscreen: use full content region
                w = Math.Max(1, (int)avail.X);
                h = Math.Max(1, (int)avail.Y);
            }

            // Initialize renderer if needed
            if (_gameRenderer == null)
            {
                _gameRenderer = new ViewportRenderer();
                _gameRenderer.SetGameMode(true);
                _gameRenderer.ForceEditorCamera = false;
                // Ensure the editor grid isn't visible in the Game panel viewport.
                _gameRenderer.GridVisible = false;
            }

            // Resize if needed
            if (w != _lastRenderWidth || h != _lastRenderHeight)
            {
                _gameRenderer.Resize(w, h);
                _lastRenderWidth = w;
                _lastRenderHeight = h;
            }
            
            // Update renderer scene
            _gameRenderer.SetScene(scene);

            // Render with selected camera
            if (camera != null)
            {
                float aspect = (float)w / Math.Max(1, h);
                var viewMat = camera.ViewMatrix;
                var projMat = camera.ProjectionMatrix(aspect);
                
                _gameRenderer.SetCameraMatrices(viewMat, projMat);

                // PERF FIX: Only render when necessary. Avoid unconditionally rendering
                // because docked/hidden tabs can report a positive content region even
                // when not visible. Render when in Play Mode, when the window is appearing,
                // hovered, or focused. This mirrors the guard used in GamePanel.cs.
                bool shouldRender = PlayMode.IsInPlayMode ||
                                    ImGui.IsWindowAppearing() ||
                                    ImGui.IsWindowFocused(ImGuiNET.ImGuiFocusedFlags.RootAndChildWindows) ||
                                    ImGui.IsWindowHovered(ImGuiNET.ImGuiHoveredFlags.RootAndChildWindows);

                if (shouldRender)
                {
                    _gameRenderer.RenderScene();
                }
            }

            // Display rendered texture (centered if resolution is smaller than panel)
            Vector2 imageSize = new Vector2(w, h);

            // Center the image if it's smaller than the available region
            if (imageSize.X < avail.X || imageSize.Y < avail.Y)
            {
                float offsetX = Math.Max(0, (avail.X - imageSize.X) * 0.5f);
                float offsetY = Math.Max(0, (avail.Y - imageSize.Y) * 0.5f);
                ImGui.SetCursorPos(new Vector2(offsetX, offsetY));
            }

            ImGui.Image((nint)_gameRenderer.ColorTexture, imageSize, new Vector2(0, 1), new Vector2(1, 0));

            Vector2 itemMin = ImGui.GetItemRectMin();
            Vector2 itemMax = ImGui.GetItemRectMax();

            // === Draw Modern UI Components ===
            if ((itemMax.X - itemMin.X) > 0 && (itemMax.Y - itemMin.Y) > 0)
            {
                // Camera selector (top-left, below performance overlay)
                DrawCameraSelector(itemMin, itemMax, cameraNames, cameraEntityIds);
                
                // Play controls (centered at top)
                _playControls.Draw(itemMin, itemMax);
                
                // Top-right controls (resolution + actions)
                _topRightControls.Draw(itemMin, itemMax);
                
                // Performance overlays (3 corners: Performance top-left, Memory top-right, Rendering bottom-left)
                _overlays.DrawPerformanceStats(itemMin, itemMax);
                _overlays.DrawMemoryStats(itemMin, itemMax);
                _overlays.DrawRenderingStats(itemMin, itemMax);
            }
        }
        ImGui.End();
        
        ImGui.PopStyleVar();
    }

    /// <summary>
    /// Get the resolution for Play Mode based on Editor Settings dropdown selection
    /// </summary>
    private void GetPlayModeResolution(Vector2 availableRegion, out int width, out int height)
    {
        int presetIndex = EditorSettings.ViewportResolutionPresetIndex;

        // Resolution presets (must match EditorUI.cs dropdown order after "Panel Size" and "Fullscreen")
        // Index: 0=1920x1080, 1=1600x900, 2=1280x720, 3=1366x768, 4=1024x768, 5=800x600, 6=Custom
        switch (presetIndex)
        {
            case -1: // Panel Size (Auto)
                width = Math.Max(1, (int)availableRegion.X);
                height = Math.Max(1, (int)availableRegion.Y);
                break;
            case -2: // Fullscreen (shouldn't reach here, handled in Draw)
                width = Math.Max(1, (int)availableRegion.X);
                height = Math.Max(1, (int)availableRegion.Y);
                break;
            case 0: // 1920 x 1080
                width = 1920;
                height = 1080;
                break;
            case 1: // 1600 x 900
                width = 1600;
                height = 900;
                break;
            case 2: // 1280 x 720
                width = 1280;
                height = 720;
                break;
            case 3: // 1366 x 768
                width = 1366;
                height = 768;
                break;
            case 4: // 1024 x 768
                width = 1024;
                height = 768;
                break;
            case 5: // 800 x 600
                width = 800;
                height = 600;
                break;
            case 6: // Custom
                width = Math.Max(8, EditorSettings.ViewportCustomWidth);
                height = Math.Max(8, EditorSettings.ViewportCustomHeight);
                break;
            default:
                width = Math.Max(1, (int)availableRegion.X);
                height = Math.Max(1, (int)availableRegion.Y);
                break;
        }

        // Constrain to available region (don't exceed panel size)
        if (width > availableRegion.X || height > availableRegion.Y)
        {
            float scale = Math.Min(availableRegion.X / width, availableRegion.Y / height);
            width = Math.Max(1, (int)(width * scale));
            height = Math.Max(1, (int)(height * scale));
        }
    }

    private CameraComponent? GetSelectedCamera(Scene scene)
    {
        // Try explicit selection first
        if (_selectedCameraEntityId != 0)
        {
            var ent = scene.GetById(_selectedCameraEntityId);
            if (ent != null)
            {
                var cam = ent.GetComponent<CameraComponent>();
                if (cam != null) return cam;
            }
        }

        // Fallback to main camera
        foreach (var e in scene.Entities)
        {
            var cam = e.GetComponent<CameraComponent>();
            if (cam == null) continue;
            if (cam.IsMain) return cam;
        }

        // Fallback to first active camera
        foreach (var e in scene.Entities)
        {
            var cam = e.GetComponent<CameraComponent>();
            if (cam == null) continue;
            if (!e.Active || !cam.Enabled) continue;
            return cam;
        }

        return null;
    }

    private void DrawCameraSelector(Vector2 imageMin, Vector2 imageMax, string[] cameraNames, uint[] cameraEntityIds)
    {
        if (cameraNames.Length == 0) return;

        int currentIndex = Array.FindIndex(cameraEntityIds, id => id == _selectedCameraEntityId);
        if (currentIndex < 0) currentIndex = 0;

        // Position at top-left, below the performance overlay
        float offsetX = 15f;
        float offsetY = 100f; // Below performance overlay
        float width = 180f;

        ImGui.SetNextWindowPos(new Vector2(imageMin.X + offsetX, imageMin.Y + offsetY));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10, 8));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 10f);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0, 0, 0, 0.6f));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(1, 1, 1, 0.2f));

        if (ImGui.Begin("##camera_selector", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 1, 1, 0.7f));
            ImGui.Text("CAMERA");
            ImGui.PopStyleColor();
            
            ImGui.SetNextItemWidth(width);
            if (ImGui.Combo("##camera", ref currentIndex, cameraNames, cameraNames.Length))
            {
                _selectedCameraEntityId = cameraEntityIds[currentIndex];
            }
        }
        ImGui.End();

        ImGui.PopStyleColor(2);
        ImGui.PopStyleVar(2);
    }
}

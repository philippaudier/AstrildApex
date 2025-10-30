using ImGuiNET;
using System.Numerics;
using System.Linq;
using Engine.Scene;
using Engine.Components;
using Editor.Rendering;
using Editor.Panels;
using Editor;

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
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));
        
        bool open = true;
        if (ImGui.Begin("Game", ref open, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
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
            foreach (var e in scene.Entities)
            {
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

            var avail = ImGui.GetContentRegionAvail();
            int w = Math.Max(1, (int)avail.X);
            int h = Math.Max(1, (int)avail.Y);

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
                _gameRenderer.RenderScene();
            }

            // Display rendered texture
            ImGui.Image((nint)_gameRenderer.ColorTexture, avail, new Vector2(0, 1), new Vector2(1, 0));
            
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

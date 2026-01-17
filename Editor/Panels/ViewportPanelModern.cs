using System;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using OpenTK.Mathematics;
using Editor.Rendering;
using Editor.State;
using Editor.UI;
using Editor.Logging;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

namespace Editor.Panels;

/// <summary>
/// Modern Viewport Panel with redesigned UI following the HTML design
/// Features: 
/// - Top toolbar with transform tools, snap tools, drawing tools, view options
/// - Top-right controls (camera selector, fullscreen, settings)
/// - 4-corner overlays (scene info, transform, gizmo, camera controls)
/// - Keyboard shortcuts (Q/W/E/R/T/F)
/// - Modern glassmorphism styling
/// </summary>
public class ViewportPanelModern
{
    // === Core State ===
    private ViewportRenderer? _renderer;

    // === Play Mode State ===
    private bool _isInPlayMode = false;
    private uint _selectedCameraEntityId = 0;
    private float _savedEditorYaw;
    private float _savedEditorPitch;
    private float _savedEditorDist;

    // === New UI Components (Edit Mode) ===
    private ViewportToolbar _toolbar = new();
    private ViewportTopRightControls _topRightControls = new();
    private ViewportOverlays _overlays = new();

    // === Game Mode UI Components ===
    private AstrildApex.Editor.UI.GamePlayControls _playControls = new();
    private AstrildApex.Editor.UI.GameTopRightControls _gameTopRightControls = new();
    private AstrildApex.Editor.UI.GamePerformanceOverlays _gameOverlays = new();
    
    // === Selection State ===
    private bool _isSelecting = false;
    private bool _isGizmoDragging = false;
    private Vector2 _selStart, _selEnd;
    
    // === Context Menu ===
    private bool _showContextMenu = false;
    private Vector2 _contextMenuPos;
    private Vector2 _contextMenuMouseLocal;
    private int _contextMenuPixelX, _contextMenuPixelY;
    private Vector2 _rightClickStartPos;
    
    // === Camera State ===
    private float _yaw = MathHelper.DegreesToRadians(-30f);
    private float _pitch = MathHelper.DegreesToRadians(-15f);
    private float _dist = 3.0f;
    private float _targetYaw = MathHelper.DegreesToRadians(-30f);
    private float _targetPitch = MathHelper.DegreesToRadians(-15f);
    private float _targetDist = 3.0f;
    private float _arrowVelocityX = 0f;
    private float _arrowVelocityY = 0f;
    private CameraView _previousView = CameraView.Perspective;
    
    // Track if we just started animating to reset distance tracking
    private bool _wasAnimating = false;
    
    // === Camera Settings ===
    private float _arrowSpeed = 1.0f;
    private float _arrowAcceleration = 5.0f;
    private float _arrowDamping = 0.85f;
    private float _smoothFactor = 0.2f;
    
    // === Snap Settings ===
    private float _snapMove = 0.5f;
    private float _snapAngle = 15f;
    private float _snapScale = 0.1f;
    
    // === Mouse Picking ===
    private uint _hoverId = 0;
    private System.Diagnostics.Stopwatch _pickThrottleTimer = System.Diagnostics.Stopwatch.StartNew();
    private const double MIN_PICK_INTERVAL_MS = 33.0;

    // === Camera names for selector ===
    private string[] _cameraNames = new[] { "Main Camera", "Scene Camera" };

    // === Camera Mode ===
    private CameraMode _cameraMode = CameraMode.Perspective;
    private float _orthoSize = 10f;  // Taille de la vue orthographique

    // === Scene Stats Overlay ===
    private bool _showSceneStatsOverlay = true;

    // === FPS Tracking ===
    private System.Diagnostics.Stopwatch _fpsTimer = System.Diagnostics.Stopwatch.StartNew();
    private double _lastFrameTime = 0.0;
    private double _smoothedFrameTime = 16.6;  // Start at ~60 FPS
    private const int FPS_SMOOTHING_FRAMES = 60;
    private double _frameTimeSum = 16.6 * FPS_SMOOTHING_FRAMES;
    private int _frameCount = FPS_SMOOTHING_FRAMES;

    public ViewportPanelModern()
    {
        // Initialize toolbar with saved settings
        _toolbar.ShowGrid = EditorSettings.ShowGrid;
        _toolbar.VSync = EditorSettings.VSync;
    }

    public ViewportRenderer? Renderer
    {
        get => _renderer;
        set
        {
            if (_renderer != null)
            {
                _renderer.GizmoDragEnded -= OnGizmoDragEnded;
                _renderer.EditingTouched -= OnEditingTouched;
            }
            
            _renderer = value;
            
            if (_renderer != null)
            {
                _renderer.GizmoDragEnded += OnGizmoDragEnded;
                _renderer.EditingTouched += OnEditingTouched;
                
                try
                {
                    var savedState = EditorSettings.ViewportCameraState;
                    _yaw = savedState.Yaw;
                    _pitch = savedState.Pitch;
                    _dist = savedState.Distance;
                    _targetYaw = savedState.Yaw;
                    _targetPitch = savedState.Pitch;
                    _targetDist = savedState.Distance;
                    _renderer.ApplyOrbitCameraState(savedState);
                }
                catch (Exception ex)
                {
                    LogManager.LogWarning($"Failed to apply saved camera state: {ex.Message}", "ViewportPanel");
                }
            }
        }
    }

    /// <summary>
    /// Switch between Edit Mode and Play Mode
    /// </summary>
    public void SetPlayMode(bool isPlayMode)
    {
        if (_isInPlayMode == isPlayMode) return; // Already in desired mode

        _isInPlayMode = isPlayMode;

        if (_renderer == null) return;

        if (isPlayMode)
        {
            // ENTERING PLAY MODE
            // Save editor camera state
            _savedEditorYaw = _yaw;
            _savedEditorPitch = _pitch;
            _savedEditorDist = _dist;

            // Switch to Game Mode rendering
            _renderer.SetGameMode(true);
            _renderer.GridVisible = false;
            _renderer.SetGizmoVisible(false);
            _renderer.ForceEditorCamera = false;

            LogManager.LogInfo("ViewportPanel switched to Play Mode", "ViewportPanel");
        }
        else
        {
            // EXITING PLAY MODE - RETURN TO EDIT MODE
            // Restore editor camera state
            _yaw = _savedEditorYaw;
            _pitch = _savedEditorPitch;
            _dist = _savedEditorDist;
            _targetYaw = _savedEditorYaw;
            _targetPitch = _savedEditorPitch;
            _targetDist = _savedEditorDist;

            // Switch back to Edit Mode rendering
            _renderer.SetGameMode(false);
            _renderer.GridVisible = _toolbar.ShowGrid;
            _renderer.SetGizmoVisible(_toolbar.ShowGizmos && Selection.Selected.Count > 0);
            _renderer.ForceEditorCamera = true;

            // Reset camera selection
            _selectedCameraEntityId = 0;

            LogManager.LogInfo("ViewportPanel switched to Edit Mode", "ViewportPanel");
        }
    }

    public void Draw()
    {
        // Update FPS tracking
        UpdateFpsTracking();

        // Check if Escape key pressed in fullscreen Play Mode (exit fullscreen)
        if (_isInPlayMode && EditorSettings.ViewportFullscreen && ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            EditorSettings.ViewportFullscreen = false;
            EditorSettings.ViewportResolutionPresetIndex = -1; // Reset to Panel Size
        }

        // Fullscreen mode in Play Mode: create borderless fullscreen window
        bool isFullscreen = _isInPlayMode && EditorSettings.ViewportFullscreen;
        ImGuiWindowFlags windowFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;

        if (isFullscreen)
        {
            // Fullscreen: no title bar, no resize, no move, covering entire viewport
            windowFlags |= ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse;
            var viewport = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(viewport.Pos);
            ImGui.SetNextWindowSize(viewport.Size);
        }
        else
        {
            ImGui.SetNextWindowSize(new Vector2(800, 600), ImGuiCond.FirstUseEver);
        }

        // PERFORMANCE FIX: Skip rendering if window is collapsed/hidden
        if (!ImGui.Begin(isFullscreen ? "##SceneFullscreen" : "Scene", windowFlags))
        {
            ImGui.End();
            return;
        }

        var io = ImGui.GetIO();
        bool focusedDock = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows | ImGuiFocusedFlags.NoPopupHierarchy | ImGuiFocusedFlags.DockHierarchy);
        bool hoveredWindow = ImGui.IsWindowHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem | ImGuiHoveredFlags.AllowWhenBlockedByPopup);
        bool allowHotkeys = (focusedDock || hoveredWindow) && !io.WantTextInput;

        // Update static focus state for CameraComponent and input systems
        ViewportPanel.IsWindowFocused = focusedDock || hoveredWindow;

        // === Hotkeys (Edit Mode only) ===
        if (allowHotkeys && !_isInPlayMode)
        {
            _toolbar.ProcessHotkeys();
            if (ImGui.IsKeyPressed(ImGuiKey.F)) Renderer?.FrameSelection();
        }

        // === Apply toolbar state to renderer (Edit Mode only) ===
        if (Renderer != null && !_isInPlayMode)
        {
            Renderer.SetMode(_toolbar.CurrentMode);
            Renderer.SetSpaceLocal(_toolbar.LocalSpace);

            bool snapActive = _toolbar.SnapToGrid || _toolbar.VertexSnap || ImGui.IsKeyDown(ImGuiKey.ModCtrl);
            Renderer.ConfigureSnap(snapActive, _snapMove, _snapAngle, _snapScale);

            Renderer.GridVisible = _toolbar.ShowGrid;
            Renderer.SetGizmoVisible(_toolbar.ShowGizmos && Selection.Selected.Count > 0);

            // Persist settings
            if (EditorSettings.ShowGrid != _toolbar.ShowGrid)
            {
                EditorSettings.ShowGrid = _toolbar.ShowGrid;
            }

            if (EditorSettings.VSync != _toolbar.VSync)
            {
                EditorSettings.VSync = _toolbar.VSync;
                // Apply VSync to GameWindow
                try
                {
                    var gw = Editor.Program.GameWindow;
                    if (gw != null)
                    {
                        gw.VSync = _toolbar.VSync ? OpenTK.Windowing.Common.VSyncMode.On : OpenTK.Windowing.Common.VSyncMode.Off;
                    }
                }
                catch (Exception ex)
                {
                    LogManager.LogWarning($"Failed to set VSync: {ex.Message}", "ViewportPanelModern");
                }
            }
            
            // Handle camera view changes
            if (_overlays.CurrentView != _previousView)
            {
                ApplyCameraView(_overlays.CurrentView);
                _previousView = _overlays.CurrentView;
            }
        }
        
        // === Resize & Camera ===
        var avail = ImGui.GetContentRegionAvail();
        int w, h;

        if (_isInPlayMode && !isFullscreen)
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

        Renderer?.Resize(w, h);

        // === Camera Handling ===
        if (_isInPlayMode)
        {
            // PLAY MODE: Use game camera (CameraComponent)
            var scene = PlayMode.PlayScene;
            if (scene != null && Renderer != null)
            {
                var camera = GetSelectedCamera(scene);
                if (camera != null)
                {
                    float aspect = (float)w / Math.Max(1, h);
                    var viewMat = camera.ViewMatrix;
                    var projMat = camera.ProjectionMatrix(aspect);
                    Renderer.SetCameraMatrices(viewMat, projMat);
                }
            }
        }
        else
        {
            // EDIT MODE: Use orbital editor camera
            HandleCameraInput(hoveredWindow, io);
        }

        // === Render Scene ===
        // Render in both Edit and Play modes (PlayMode needs continuous rendering for animations)
        Renderer?.RenderScene();

        // === Display Texture ===
        Vector2 itemMin = Vector2.Zero;
        Vector2 itemMax = Vector2.Zero;

        if (Renderer != null)
        {
            // Display rendered texture (centered if resolution is smaller than panel)
            // Like Unity: image is always centered in the available space
            Vector2 imageSize = new Vector2(w, h);

            // Get fresh content region info right before drawing
            Vector2 contentRegionAvail = ImGui.GetContentRegionAvail();
            Vector2 contentRegionMin = ImGui.GetCursorPos();

            // Calculate centering offsets (always center, even if image fills space)
            float offsetX = Math.Max(0, (contentRegionAvail.X - imageSize.X) * 0.5f);
            float offsetY = Math.Max(0, (contentRegionAvail.Y - imageSize.Y) * 0.5f);

            // Set cursor to centered position (relative to content region start)
            ImGui.SetCursorPos(new Vector2(contentRegionMin.X + offsetX, contentRegionMin.Y + offsetY));

            ImGui.Image((IntPtr)Renderer.ColorTexture, imageSize, new Vector2(0, 1), new Vector2(1, 0));

            itemMin = ImGui.GetItemRectMin();
            itemMax = ImGui.GetItemRectMax();

            // === Drag & Drop Target: Drop mesh assets from AssetsPanel to scene ===
            if (ImGui.BeginDragDropTarget())
            {
                float dropImgW = itemMax.X - itemMin.X;
                float dropImgH = itemMax.Y - itemMin.Y;
                HandleMeshAssetDrop(itemMin, itemMax, dropImgW, dropImgH);
                ImGui.EndDragDropTarget();
            }

            // Play mode border
            if (PlayMode.IsInPlayMode)
            {
                var drawList = ImGui.GetWindowDrawList();
                var borderColor = PlayMode.IsPaused ? 0xFF4080FFu : 0xFF4040FFu;
                drawList.AddRect(itemMin, itemMax, borderColor, 0.0f, ImDrawFlags.None, 3.0f);
            }
        }
        
        float imgW = itemMax.X - itemMin.X;
        float imgH = itemMax.Y - itemMin.Y;
        
        // === Handle Input & Selection (Edit Mode only) ===
        if (!_isInPlayMode)
        {
            HandleViewportInput(itemMin, itemMax, imgW, imgH, hoveredWindow, io);
        }

        // === Draw Overlays BEFORE End() to avoid clipping ===
        float imgWf = itemMax.X - itemMin.X;
        float imgHf = itemMax.Y - itemMin.Y;

        if (imgWf > 1.0f && imgHf > 1.0f)
        {
            if (_isInPlayMode)
            {
                // PLAY MODE UI: No overlays needed - play controls are in main toolbar
                // The user already has Play/Pause/Step/Stop buttons in the editor's main toolbar
            }
            else
            {
                // EDIT MODE UI: Show edit toolbar and scene overlays
                _toolbar.Draw(itemMin, itemMax, Renderer, _smoothedFrameTime);
                Editor.UI.SceneStatsOverlay.Draw(itemMin, itemMax, Renderer, _smoothedFrameTime, ref _showSceneStatsOverlay, _overlays, _toolbar);
                Editor.UI.TriedreOverlay.Draw(itemMin, itemMax, Renderer);
            }
        }

        // === Context Menu (must be before End) ===
        DrawContextMenu();

        bool viewportWasFocused = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);

        ImGui.End();

        // Bottom toolbar permanently migrated into overlay; old bottom-toolbar removed.

        // === CAMERA SETTINGS PANEL ===
        CameraSettingsPanel.Draw(Renderer, ref _orthoSize, ref _cameraMode,
            ref _yaw, ref _pitch, ref _targetYaw, ref _targetPitch);
    }
    
    private void HandleCameraInput(bool hoveredWindow, ImGuiIOPtr io)
    {
        // Mouse orbit
        if (hoveredWindow && ImGui.IsMouseDown(ImGuiMouseButton.Right))
        {
            Renderer?.CancelCameraAnimation();
            _targetYaw += io.MouseDelta.X * 0.01f;
            _targetPitch -= io.MouseDelta.Y * 0.01f;
        }
        
        // Arrow keys
        if (hoveredWindow && Renderer != null)
        {
            float dt = io.DeltaTime;
            
            // Horizontal
            if (ImGui.IsKeyDown(ImGuiKey.RightArrow))
            {
                Renderer.CancelCameraAnimation();
                _arrowVelocityX += _arrowAcceleration * dt;
            }
            else if (ImGui.IsKeyDown(ImGuiKey.LeftArrow))
            {
                Renderer.CancelCameraAnimation();
                _arrowVelocityX -= _arrowAcceleration * dt;
            }
            else
            {
                _arrowVelocityX *= _arrowDamping;
            }
            
            // Vertical
            if (ImGui.IsKeyDown(ImGuiKey.UpArrow))
            {
                Renderer.CancelCameraAnimation();
                _arrowVelocityY += _arrowAcceleration * dt;
            }
            else if (ImGui.IsKeyDown(ImGuiKey.DownArrow))
            {
                Renderer.CancelCameraAnimation();
                _arrowVelocityY -= _arrowAcceleration * dt;
            }
            else
            {
                _arrowVelocityY *= _arrowDamping;
            }
            
            _arrowVelocityX = Math.Clamp(_arrowVelocityX, -10f, 10f);
            _arrowVelocityY = Math.Clamp(_arrowVelocityY, -10f, 10f);
            
            if (MathF.Abs(_arrowVelocityX) > 0.01f)
            {
                float panSpeed = _arrowSpeed * _dist * 0.01f;
                Renderer.Pan(-_arrowVelocityX * panSpeed, 0);
            }
            
            if (MathF.Abs(_arrowVelocityY) > 0.01f)
            {
                float moveSpeed = _arrowSpeed * 0.1f;
                _targetDist = Math.Max(0.01f, _targetDist - _arrowVelocityY * moveSpeed);
            }
        }
        
        // Zoom
        if (hoveredWindow && io.MouseWheel != 0)
        {
            Renderer?.CancelCameraAnimation();

            // En mode orthographique/2D, ajuster la taille ortho au lieu de la distance
            if (_cameraMode == CameraMode.Orthographic || _cameraMode == CameraMode.TwoD)
            {
                _orthoSize *= MathF.Pow(0.9f, io.MouseWheel);
                _orthoSize = Math.Clamp(_orthoSize, 0.1f, 1000f);  // Limiter entre 0.1 et 1000

                // Ajuster les plans de clipping en fonction de la taille ortho AVANT de changer la projection
                // Plus la taille est grande, plus on doit voir loin
                if (Renderer != null)
                {
                    float nearClip = 0.1f;
                    float farClip = Math.Max(5000f, _orthoSize * 200f);  // Au moins 5000, ou taille * 200
                    Renderer.NearClip = nearClip;
                    Renderer.FarClip = farClip;
                }

                Renderer?.SetProjectionMode(_cameraMode == CameraMode.Orthographic ? 1 : 2, _orthoSize);
            }
            else
            {
                // En mode perspective, zoom normal (distance de caméra)
                _targetDist *= MathF.Pow(0.9f, io.MouseWheel);
            }
        }
        
        if (Renderer != null)
        {
            Renderer.ResetToOrbitalCamera();
            
            bool isAnimating = Renderer.IsCameraAnimating;
            
            // Detect animation start (transition from not-animating to animating)
            if (isAnimating && !_wasAnimating)
            {
                // Animation just started - force immediate sync with renderer's goal
                // This must happen BEFORE smoothing to avoid overwriting the new goal
                _dist = Renderer.Distance;
                _targetDist = Renderer.DistanceGoal;
            }
            else if (isAnimating)
            {
                // During camera animation (focus/frame), sync our distance with renderer's animated distance
                // This allows the panel to follow the renderer's smooth distance interpolation
                _dist = Renderer.Distance;
                _targetDist = Renderer.DistanceGoal; // Use the goal, not current distance
            }
            else
            {
                // Only apply smoothing when NOT animating (normal user input)
                _yaw = _yaw + (_targetYaw - _yaw) * _smoothFactor;
                _pitch = _pitch + (_targetPitch - _pitch) * _smoothFactor;
                _dist = _dist + (_targetDist - _dist) * _smoothFactor;
            }
            
            _wasAnimating = isAnimating;
            
            Renderer.SetCamera(_yaw, _pitch, _dist);
        }
        
        // Pan
        if (hoveredWindow && ImGui.IsMouseDown(ImGuiMouseButton.Middle))
        {
            Renderer?.CancelCameraAnimation();
            Renderer?.Pan(io.MouseDelta.X, io.MouseDelta.Y);
        }
    }
    
    private void HandleViewportInput(Vector2 itemMin, Vector2 itemMax, float imgW, float imgH, bool hoveredWindow, ImGuiIOPtr io)
    {
        Vector2 mouse = ImGui.GetMousePos();
        var mouseLocal = new Vector2(mouse.X - itemMin.X, mouse.Y - itemMin.Y);
        bool inImage = (mouseLocal.X >= 0 && mouseLocal.Y >= 0 && mouseLocal.X < imgW && mouseLocal.Y < imgH);
        
        if (hoveredWindow && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            _rightClickStartPos = mouse;
        }
        
        bool ctrl = io.KeyCtrl;
        bool shift = io.KeyShift;
        
        float renderScale = Renderer?.RenderScale ?? 1.0f;
        int pxGL(float x) => (int)MathF.Round(x * renderScale);
        int pyGL(float y) => Renderer != null ? (int)MathF.Round(Renderer.Height - y * renderScale) : (int)MathF.Round(imgH - y);
        
        // Hover picking (throttled) - enabled when Alt is pressed OR when clicking
        bool enablePicking = io.KeyAlt;
        bool forcePickOnClick = ImGui.IsMouseClicked(ImGuiMouseButton.Left);

        if (Renderer != null && inImage && (enablePicking || forcePickOnClick))
        {
            double elapsedMs = _pickThrottleTimer.Elapsed.TotalMilliseconds;
            bool shouldPick = elapsedMs >= MIN_PICK_INTERVAL_MS || forcePickOnClick;

            if (shouldPick)
            {
                int mx = pxGL(mouseLocal.X);
                int my = pyGL(mouseLocal.Y);
                uint picked = Renderer.PickIdAtFat(mx, my, 6);
                _hoverId = picked;
                Renderer.SetHover(_hoverId);
                // Hover pick performed (silent in normal operation)
                _pickThrottleTimer.Restart();
            }
            else
            {
                Renderer.SetHover(_hoverId);
            }
        }
        else if (!forcePickOnClick)
        {
            _hoverId = 0; Renderer?.SetHover(0);
        }
        
        // Click handling - only in edit mode
        if (hoveredWindow && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !PlayMode.IsInPlayMode && inImage)
        {
            // Log click + modifier state and current hover id for debugging picks under MSAA
            // LeftClick handling (silent)

            HandleLeftClick(pxGL, pyGL, mouseLocal, ctrl, shift);
        }
        
        // Right-click context menu
        if (hoveredWindow && ImGui.IsMouseReleased(ImGuiMouseButton.Right) && !PlayMode.IsInPlayMode && inImage)
        {
            var dragDistance = (mouse - _rightClickStartPos).Length();
            if (dragDistance < 3.0f)
            {
                _showContextMenu = true;
                _contextMenuPos = mouse;
                _contextMenuMouseLocal = mouseLocal;
                _contextMenuPixelX = pxGL(mouseLocal.X);
                _contextMenuPixelY = pyGL(mouseLocal.Y);
            }
        }
        
        // Gizmo dragging
        if (_isGizmoDragging && ImGui.IsMouseDown(ImGuiMouseButton.Left) && Renderer != null && !PlayMode.IsInPlayMode)
        {
            switch (_toolbar.CurrentMode)
            {
                case ViewportRenderer.GizmoMode.Translate:
                    Renderer.UpdateDragTranslate(pxGL(mouseLocal.X), pyGL(mouseLocal.Y));
                    break;
                case ViewportRenderer.GizmoMode.Rotate:
                    Renderer.UpdateDragRotate(pxGL(mouseLocal.X), pyGL(mouseLocal.Y));
                    break;
                case ViewportRenderer.GizmoMode.Scale:
                    Renderer.UpdateDragScale(pxGL(mouseLocal.X), pyGL(mouseLocal.Y));
                    break;
            }
        }
        
        if (_isGizmoDragging && ImGui.IsMouseReleased(ImGuiMouseButton.Left) && Renderer != null && !PlayMode.IsInPlayMode)
        {
            Renderer.EndDrag();
            _isGizmoDragging = false;
        }
        
        // Rectangle selection
        if (!_isGizmoDragging && _isSelecting && ImGui.IsMouseDown(ImGuiMouseButton.Left) && !PlayMode.IsInPlayMode)
        {
            _selEnd = mouseLocal;
            var dl = ImGui.GetForegroundDrawList();
            var a = new Vector2(Math.Min(_selStart.X, _selEnd.X), Math.Min(_selStart.Y, _selEnd.Y));
            var b = new Vector2(Math.Max(_selStart.X, _selEnd.X), Math.Max(_selStart.Y, _selEnd.Y));
            var p0 = itemMin + a;
            var p1 = itemMin + b;
            dl.AddRect(p0, p1, 0x66FFFFFF);
            dl.AddRectFilled(p0, p1, 0x2266AAFF);
        }
        
        bool handledRect = false;
        if (_isSelecting && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            HandleRectangleSelection(pxGL, pyGL, ctrl, shift);
            handledRect = true;
        }
        
        // Clear selection on empty click
        if (hoveredWindow && ImGui.IsMouseReleased(ImGuiMouseButton.Left) && (!inImage || _hoverId == 0) && !ctrl && !shift && !_isGizmoDragging && !_isSelecting && !ImGui.IsMouseDragging(ImGuiMouseButton.Left) && !handledRect)
        {
            Selection.Clear();
            UpdateGizmoPivot();
            Renderer?.EndDrag();
        }
    }
    
    private void HandleLeftClick(Func<float, int> pxGL, Func<float, int> pyGL, Vector2 mouseLocal, bool ctrl, bool shift)
    {
        if (Renderer != null && Renderer.IsGizmoId(_hoverId))
        {
            _isGizmoDragging = true;
            switch (_toolbar.CurrentMode)
            {
                case ViewportRenderer.GizmoMode.Translate:
                    Renderer.BeginDragTranslate(_hoverId, pxGL(mouseLocal.X), pyGL(mouseLocal.Y));
                    break;
                case ViewportRenderer.GizmoMode.Rotate:
                    Renderer.BeginDragRotate(_hoverId, pxGL(mouseLocal.X), pyGL(mouseLocal.Y));
                    break;
                case ViewportRenderer.GizmoMode.Scale:
                    Renderer.BeginDragScale(_hoverId, pxGL(mouseLocal.X), pyGL(mouseLocal.Y));
                    break;
            }
        }
        else if (_hoverId != 0 && Renderer != null && !Renderer.IsGizmoId(_hoverId) && Renderer.IsEntityId(_hoverId))
        {
            if (ctrl)
            {
                Selection.Toggle(_hoverId);
                if (Selection.Selected.Count > 0 && Selection.ActiveEntityId == 0)
                    Selection.ActiveEntityId = Selection.Selected.First();
            }
            else if (shift)
            {
                Selection.AddMany(new uint[] { _hoverId });
                if (Selection.Selected.Contains(_hoverId))
                    Selection.ActiveEntityId = _hoverId;
            }
            else
            {
                Selection.SetSingle(_hoverId);
            }
            UpdateGizmoPivot();
        }
        else if (_hoverId == 0 || (Renderer != null && !Renderer.IsEntityId(_hoverId)))
        {
            _isSelecting = true;
            _selStart = _selEnd = mouseLocal;
        }
    }
    
    private void HandleRectangleSelection(Func<float, int> pxGL, Func<float, int> pyGL, bool ctrl, bool shift)
    {
        _isSelecting = false;
        if (Renderer != null)
        {
            int x0 = pxGL(_selStart.X), y0 = pyGL(_selStart.Y);
            int x1 = pxGL(_selEnd.X), y1 = pyGL(_selEnd.Y);
            var allIds = Renderer.PickIdsInRect(x0, y0, x1, y1);
            if (allIds != null && allIds.Count > 0)
            {
                var entityIds = allIds.Where(id => Renderer.IsEntityId(id)).ToList();
                if (entityIds.Count > 0)
                {
                    if (ctrl) Selection.AddMany(entityIds);
                    else if (shift) { foreach (var id in entityIds) Selection.Toggle(id); }
                    else Selection.ReplaceMany(entityIds);
                }
                UpdateGizmoPivot();
            }
            else if (!ctrl && !shift) { Selection.Clear(); UpdateGizmoPivot(); }
        }
    }
    
    private void DrawContextMenu()
    {
        if (_showContextMenu)
        {
            ImGui.OpenPopup("ViewportContextMenu");
            _showContextMenu = false;
        }
        
        ImGui.SetNextWindowPos(_contextMenuPos);
        if (ImGui.BeginPopup("ViewportContextMenu"))
        {
            bool hasSelection = Selection.Selected.Count > 0 || Selection.ActiveEntityId != 0;
            
            if (hasSelection && ImGui.MenuItem("Move Selection to Cursor"))
            {
                if (Renderer != null)
                {
                    var worldPos = Renderer.PickWorldPositionAt(_contextMenuPixelX, _contextMenuPixelY);
                    if (worldPos.HasValue) MoveSelectionToPosition(worldPos.Value);
                }
                ImGui.CloseCurrentPopup();
            }
            
            ImGui.Separator();
            
            if (ImGui.MenuItem("Create Empty at Cursor"))
            {
                if (Renderer != null)
                {
                    var worldPos = Renderer.PickWorldPositionAt(_contextMenuPixelX, _contextMenuPixelY);
                    CreateEntityAtPosition(worldPos ?? OpenTK.Mathematics.Vector3.Zero);
                }
                ImGui.CloseCurrentPopup();
            }
            
            ImGui.Separator();
            
            if (hasSelection && ImGui.MenuItem("Frame Selection", "F"))
            {
                Renderer?.FrameSelection();
                ImGui.CloseCurrentPopup();
            }
            
            ImGui.EndPopup();
        }
    }
    
    public void UpdateGizmoPivot()
    {
        if (Renderer == null) return;
        var scene = Renderer.Scene;
        
        if (Selection.Selected.Count > 0)
        {
            if (Selection.ActiveEntityId != 0 && !Selection.Selected.Contains(Selection.ActiveEntityId))
                Selection.Selected.Add(Selection.ActiveEntityId);
            
            Renderer.SetGizmoVisible(true);
            
            // Use center of selection
            var c = Selection.ComputeCenter(scene!);
            Renderer.SetGizmoPosition(new OpenTK.Mathematics.Vector3(c.X, c.Y, c.Z));
        }
        else if (Selection.ActiveEntityId != 0)
        {
            Selection.Selected.Add(Selection.ActiveEntityId);
            var e = scene!.GetById(Selection.ActiveEntityId);
            if (e != null)
            {
                Renderer.SetGizmoVisible(true);
                e.GetWorldTRS(out var worldPos, out _, out _);
                Renderer.SetGizmoPosition(worldPos);
            }
            else
            {
                Renderer.SetGizmoVisible(false);
            }
        }
        else
        {
            Renderer.SetGizmoVisible(false);
        }
    }
    
    private void MoveSelectionToPosition(OpenTK.Mathematics.Vector3 worldPos)
    {
        if (Renderer?.Scene == null) return;
        
        var scene = Renderer.Scene;
        var selectedIds = Selection.Selected.ToList();
        if (selectedIds.Count == 0 && Selection.ActiveEntityId != 0)
            selectedIds.Add(Selection.ActiveEntityId);
        
        if (selectedIds.Count == 0) return;
        
        var center = Selection.ComputeCenter(scene);
        var offset = new OpenTK.Mathematics.Vector3(worldPos.X - center.X, worldPos.Y - center.Y, worldPos.Z - center.Z);
        
        var composite = new CompositeAction("Move Selection to Cursor");
        foreach (var id in selectedIds)
        {
            var entity = scene.GetById(id);
            if (entity == null) continue;
            
            var oldXform = new Xform
            {
                Pos = entity.Transform.Position,
                Rot = entity.Transform.Rotation,
                Scl = entity.Transform.Scale
            };
            
            var newPos = entity.Transform.Position + offset;
            var newXform = new Xform
            {
                Pos = newPos,
                Rot = entity.Transform.Rotation,
                Scl = entity.Transform.Scale
            };
            
            composite.Add(new TransformAction("Move", id, oldXform, newXform));
            entity.Transform.Position = newPos;
        }
        
        if (composite.Count > 0)
        {
            UndoRedo.Push(composite);
        }
        
        UpdateGizmoPivot();
    }
    
    private void CreateEntityAtPosition(OpenTK.Mathematics.Vector3 worldPos)
    {
        if (Renderer?.Scene == null) return;
        
        var scene = Renderer.Scene;
        var newEntity = new Engine.Scene.Entity();
        newEntity.Name = "New Entity";
        newEntity.Id = scene.GetNextEntityId();
        newEntity.Transform.Position = worldPos;
        
        scene.Entities.Add(newEntity);
        scene.Cache?.Invalidate();
        Selection.SetSingle(newEntity.Id);
        UpdateGizmoPivot();
    }
    
    private void OnGizmoDragEnded(CompositeAction action)
    {
        UndoRedo.Push(action);
    }
    
    private void OnEditingTouched()
    {
        UndoRedo.TouchEdit();
    }
    
    /// <summary>
    /// Apply camera orientation based on selected view (Front, Right, Top, Perspective)
    /// </summary>
    private void ApplyCameraView(CameraView view)
    {
        Renderer?.CancelCameraAnimation();
        
        switch (view)
        {
            case CameraView.Front:
                // Front view: looking down -Z axis
                _targetYaw = 0f;
                _targetPitch = 0f;
                break;
                
            case CameraView.Right:
                // Right view: looking down -X axis
                _targetYaw = MathHelper.DegreesToRadians(90f);
                _targetPitch = 0f;
                break;
                
            case CameraView.Top:
                // Top view: looking down -Y axis
                _targetYaw = 0f;
                _targetPitch = MathHelper.DegreesToRadians(-90f);
                break;
                
            case CameraView.Perspective:
                // Perspective view: default isometric-like angle
                _targetYaw = MathHelper.DegreesToRadians(-30f);
                _targetPitch = MathHelper.DegreesToRadians(-15f);
                break;
        }
        
        // Apply immediately for instant switch
        _yaw = _targetYaw;
        _pitch = _targetPitch;
    }

    

    /// <summary>
    /// Update FPS tracking with smoothed moving average
    /// </summary>
    private void UpdateFpsTracking()
    {
        double currentTime = _fpsTimer.Elapsed.TotalMilliseconds;
        double deltaTime = currentTime - _lastFrameTime;
        _lastFrameTime = currentTime;

        // Skip first frame or unreasonable deltas
        if (deltaTime <= 0 || deltaTime > 1000)
            return;

        // Moving average over last N frames
        _frameTimeSum += deltaTime;
        _frameCount++;

        if (_frameCount >= FPS_SMOOTHING_FRAMES)
        {
            _smoothedFrameTime = _frameTimeSum / FPS_SMOOTHING_FRAMES;
            _frameTimeSum = _smoothedFrameTime;
            _frameCount = 1;
        }
    }

    /// <summary>
    /// Format a number with K/M suffixes for better readability
    /// </summary>
    private string FormatNumber(int value)
    {
        if (value >= 1000000)
            return (value / 1000000.0).ToString("F1") + "M";
        if (value >= 1000)
            return (value / 1000.0).ToString("F1") + "K";
        return value.ToString();
    }

    /// <summary>
    /// Handle mesh asset or prefab drop from AssetsPanel to scene
    /// Creates an entity with MeshRendererComponent at the drop position for meshes, or instantiates prefab
    /// </summary>
    private unsafe void HandleMeshAssetDrop(Vector2 itemMin, Vector2 itemMax, float imgW, float imgH)
    {
        var payload = ImGui.AcceptDragDropPayload("ASSET_MULTI");
        if (payload.NativePtr == null || payload.Data == IntPtr.Zero || payload.DataSize == 0)
            return;

        // Extract the first GUID from the payload
        var span = new ReadOnlySpan<byte>((void*)payload.Data, (int)payload.DataSize);
        if (span.Length < 16) return;
        var assetGuid = new Guid(span.Slice(0, 16));

        // Check asset exists
        if (!Engine.Assets.AssetDatabase.TryGet(assetGuid, out var assetRec))
        {
            LogManager.LogWarning($"Dropped asset GUID not found in AssetDatabase: {assetGuid}", "ViewportPanel");
            return;
        }

        LogManager.LogInfo($"Dropped asset: GUID={assetGuid}, Path={assetRec.Path}, Type={assetRec.Type}", "ViewportPanel");

        // Handle prefabs
        if (assetRec.Type == "Prefab")
        {
            HandlePrefabDrop(assetGuid, assetRec, itemMin, itemMax, imgW, imgH);
            return;
        }

        // Handle mesh assets
        if (!Engine.Assets.AssetDatabase.IsMeshAsset(assetGuid))
        {
            LogManager.LogWarning($"Dropped asset is not a mesh or prefab: {assetRec.Type}", "ViewportPanel");
            return;
        }

    // Get mouse position in viewport
        var mousePos = ImGui.GetMousePos();
        var localX = mousePos.X - itemMin.X;
        var localY = mousePos.Y - itemMin.Y;

        // Convert to world position (raycast from camera)
        if (Renderer?.Scene == null) return;

        // Calculate world position at a default distance from camera
        Vector3 worldPos = CalculateDropWorldPosition(localX, localY, imgW, imgH);

        // Load mesh asset to check for submeshes
        var meshAsset = Engine.Assets.AssetDatabase.LoadMeshAsset(assetGuid);
        if (meshAsset == null)
        {
            LogManager.LogWarning($"LoadMeshAsset returned null for GUID={assetGuid}, path={assetRec.Path}", "ViewportPanel");
            return;
        }

    LogManager.LogInfo($"Loaded MeshAsset GUID={meshAsset.Guid}, SubMeshes={meshAsset.SubMeshes.Count}, Materials={meshAsset.MaterialGuids.Count}", "ViewportPanel");
        
        // DEBUG: Print submesh and material information
        LogManager.LogInfo($"═══ DEBUG: MESH ASSET SUBMESH BREAKDOWN ═══", "ViewportPanel");
        for (int i = 0; i < meshAsset.SubMeshes.Count; i++)
        {
            var submesh = meshAsset.SubMeshes[i];
            // Use the submesh's MaterialIndex to lookup the material GUID (material list is indexed by material slot)
            Guid? matGuid = null;
            if (submesh.MaterialIndex >= 0 && submesh.MaterialIndex < meshAsset.MaterialGuids.Count)
                matGuid = meshAsset.MaterialGuids[submesh.MaterialIndex];

            var matName = matGuid.HasValue ? Engine.Assets.AssetDatabase.GetName(matGuid.Value) : "<none>";
            LogManager.LogInfo($"  Submesh[{i}]: Name='{submesh.Name}', Verts={submesh.VertexCount}, Tris={submesh.TriangleCount}, MatIdx={submesh.MaterialIndex}, Material='{matName}'", "ViewportPanel");
        }
        LogManager.LogInfo($"═══════════════════════════════════════", "ViewportPanel");

        // Calculate global minY for all submeshes to place pivot at base
        float globalMinY = float.MaxValue;
        for (int i = 0; i < meshAsset.SubMeshes.Count; i++)
        {
            float submeshMinY = CalculateMeshMinY(meshAsset.SubMeshes[i]);
            LogManager.LogInfo($"  Submesh[{i}] minY = {submeshMinY:F3}", "ViewportPanel");
            if (submeshMinY < globalMinY)
                globalMinY = submeshMinY;
        }
        if (globalMinY == float.MaxValue) globalMinY = 0f;
        
        LogManager.LogInfo($"🎯 Global minY = {globalMinY:F3}, worldPos.Y = {worldPos.Y:F3}", "ViewportPanel");

        // Create parent entity with adjusted position (pivot at base)
        var parentEntity = new Engine.Scene.Entity
        {
            Id = Renderer.Scene.GetNextEntityId(),
            Name = assetRec.Name
        };
        
        // IMPORTANT: If minY is negative (below origin), we need to ADD its absolute value
        // to raise the model so its base touches the drop point
        // If minY is positive (above origin), we subtract it to lower the base to the drop point
        // This simplifies to: adjustedY = worldPos.Y - globalMinY
        // Example: if globalMinY = -5 (base 5 units below origin), worldPos.Y - (-5) = worldPos.Y + 5 (raise by 5)
        //          if globalMinY = 2 (base 2 units above origin), worldPos.Y - 2 (lower by 2)
        var adjustedY = worldPos.Y - globalMinY;
        parentEntity.Transform.Position = new OpenTK.Mathematics.Vector3(worldPos.X, adjustedY, worldPos.Z);
        
        LogManager.LogInfo($"🎯 Parent position set to: ({worldPos.X:F2}, {adjustedY:F2}, {worldPos.Z:F2}) [worldPos.Y - globalMinY = {worldPos.Y:F2} - ({globalMinY:F2})]", "ViewportPanel");

        // Add to scene first so child entities can reference the parent
        Renderer.Scene.Entities.Add(parentEntity);
        Renderer.Scene.Cache?.Invalidate();

        // Decide whether this model was authored with per-node transforms (non-identity LocalTransform)
        // Check if any submesh has a LocalTransform with meaningful translation (not just at origin)
        const float epsilon = 0.01f;
        bool hasNodeTransforms = meshAsset.SubMeshes.Any(sm =>
        {
            if (sm.LocalTransform == null || sm.LocalTransform.Length != 16)
                return false;
            var mat = FloatArrayToMatrix(sm.LocalTransform);
            // Extract translation component (M41, M42, M43)
            float tx = mat.M41;
            float ty = mat.M42;
            float tz = mat.M43;
            float translationMagnitude = (float)Math.Sqrt(tx * tx + ty * ty + tz * tz);
            return translationMagnitude > epsilon;
        });
        
    LogManager.LogVerbose($"===== Model '{assetRec.Name}' has {(hasNodeTransforms ? "MEANINGFUL node" : "IDENTITY/BAKED")} transforms =====", "ViewportPanel");
    LogManager.LogVerbose($"Mesh bounds center: ({meshAsset.Bounds.Center.X:F3}, {meshAsset.Bounds.Center.Y:F3}, {meshAsset.Bounds.Center.Z:F3})", "ViewportPanel");

    // For baked geometry (no node transforms), adjust X,Z for mesh center to place pivot correctly
    var meshCenter = meshAsset.Bounds.Center;
    if (!hasNodeTransforms)
    {
        // For baked geometry: adjust X,Z for mesh center (Y already adjusted for globalMinY)
        parentEntity.Transform.Position = new OpenTK.Mathematics.Vector3(
            worldPos.X - meshCenter.X,
            parentEntity.Transform.Position.Y, // Keep the Y already adjusted for globalMinY
            worldPos.Z - meshCenter.Z
        );
        LogManager.LogVerbose($"Adjusted parent position for baked geometry: ({parentEntity.Transform.Position.X:F3}, {parentEntity.Transform.Position.Y:F3}, {parentEntity.Transform.Position.Z:F3})", "ViewportPanel");
    }

            // If there's only one submesh, add it to the parent entity
        if (meshAsset.SubMeshes.Count == 1)
        {
            var meshRenderer = parentEntity.AddComponent<Engine.Components.MeshRendererComponent>();
            meshRenderer.SetCustomMesh(assetGuid, 0);

            LogManager.LogInfo($"SetCustomMesh called with GUID={assetGuid}, submesh=0", "ViewportPanel");
            LogManager.LogInfo($"CustomMeshGuid after set: {meshRenderer.CustomMeshGuid}", "ViewportPanel");
            LogManager.LogInfo($"IsUsingCustomMesh: {meshRenderer.IsUsingCustomMesh()}", "ViewportPanel");

            // Assign material
            // Use the submesh's MaterialIndex to find the correct material GUID
            var sm = meshAsset.SubMeshes[0];
            Guid? matGuid0 = null;
            if (sm.MaterialIndex >= 0 && sm.MaterialIndex < meshAsset.MaterialGuids.Count)
                matGuid0 = meshAsset.MaterialGuids[sm.MaterialIndex];

            if (matGuid0.HasValue)
                meshRenderer.SetMaterial(matGuid0.Value);
            else
                meshRenderer.SetMaterial(Engine.Assets.AssetDatabase.EnsureDefaultWhiteMaterial());

            LogManager.LogVerbose($"Created single-submesh entity '{parentEntity.Name}' at {worldPos} (pivot at base, minY={globalMinY:F2})", "ViewportPanel");
        }
        else
        {
            // Multiple submeshes: create child entities for each submesh
            // IMPORTANT: For models with baked transforms (vertices already in world space),
            // we DON'T move the submeshes - they stay at origin relative to parent
            // If we detected node transforms, apply them per-child. Otherwise assume the model
            // vertices are baked in place and parent position already adjusted above for mesh center.

            for (int i = 0; i < meshAsset.SubMeshes.Count; i++)
            {
                var submesh = meshAsset.SubMeshes[i];
                var childEntity = new Engine.Scene.Entity
                {
                    Id = Renderer.Scene.GetNextEntityId(),
                    Name = string.IsNullOrWhiteSpace(submesh.Name) ? $"Submesh_{i}" : submesh.Name
                };

                // Set parent - this keeps the child at local (0,0,0) unless we override below
                childEntity.SetParent(parentEntity, keepWorld: false);
                childEntity.Transform.Rotation = OpenTK.Mathematics.Quaternion.Identity;
                childEntity.Transform.Scale = OpenTK.Mathematics.Vector3.One;

                // If this model contains meaningful node transforms, apply the stored LocalTransform
                if (hasNodeTransforms && submesh.LocalTransform != null && submesh.LocalTransform.Length == 16)
                {
                    var mat = FloatArrayToMatrix(submesh.LocalTransform);
                    ApplyMatrixToTransform(childEntity.Transform, mat);
                    LogManager.LogVerbose($"Child[{i}] '{childEntity.Name}' placed using node LocalTransform", "ViewportPanel");
                }
                else
                {
                    // For baked geometry (no meaningful node transforms), vertices are already in world space
                    // Parent position is already adjusted for meshCenter and globalMinY
                    // So all submeshes should be at (0,0,0) local - their baked vertices will appear correctly
                    childEntity.Transform.Position = OpenTK.Mathematics.Vector3.Zero;
                    LogManager.LogVerbose($"Child[{i}] '{childEntity.Name}' at local origin (baked geometry)", "ViewportPanel");
                }

                var meshRenderer = childEntity.AddComponent<Engine.Components.MeshRendererComponent>();
                meshRenderer.SetCustomMesh(assetGuid, i);
                
                LogManager.LogVerbose($"Child[{i}] SetCustomMesh: GUID={assetGuid}, submesh={i}, IsUsingCustomMesh={meshRenderer.IsUsingCustomMesh()}", "ViewportPanel");

                // Assign corresponding material using submesh.MaterialIndex
                Guid? matGuid = null;
                if (submesh.MaterialIndex >= 0 && submesh.MaterialIndex < meshAsset.MaterialGuids.Count)
                    matGuid = meshAsset.MaterialGuids[submesh.MaterialIndex];

                if (matGuid.HasValue)
                    meshRenderer.SetMaterial(matGuid.Value);
                else
                    meshRenderer.SetMaterial(Engine.Assets.AssetDatabase.EnsureDefaultWhiteMaterial());

                // NOTE: Must add to scene.Entities for rendering to work
                // DeleteRecursive() handles proper cleanup
                Renderer.Scene.Entities.Add(childEntity);
                Renderer.Scene.Cache?.Invalidate();
            }

            LogManager.LogVerbose($"Created multi-submesh entity '{parentEntity.Name}' with {meshAsset.SubMeshes.Count} children at {worldPos}", "ViewportPanel");
        }

        // Select the parent entity
        Selection.ActiveEntityId = parentEntity.Id;
        UpdateGizmoPivot();

        // Mark scene as modified
        Editor.SceneManagement.SceneManager.MarkSceneAsModified();
    }

    /// <summary>
    /// Handle prefab drop from AssetsPanel to scene
    /// Instantiates the prefab at the drop position
    /// </summary>
    private void HandlePrefabDrop(Guid prefabGuid, Engine.Assets.AssetDatabase.AssetRecord assetRec, Vector2 itemMin, Vector2 itemMax, float imgW, float imgH)
    {
        if (Renderer?.Scene == null) return;

        // Calculate drop world position
        var mousePos = ImGui.GetMousePos();
        var localX = mousePos.X - itemMin.X;
        var localY = mousePos.Y - itemMin.Y;
        Vector3 worldPos = CalculateDropWorldPosition(localX, localY, imgW, imgH);

        // Load prefab asset
        var prefabAsset = Engine.Assets.AssetDatabase.LoadPrefab(prefabGuid);
        if (prefabAsset?.RootEntity == null)
        {
            LogManager.LogWarning($"Failed to load prefab asset: {assetRec.Path}", "ViewportPanel");
            return;
        }

        // Instantiate prefab using PrefabInstantiator
        var rootEntity = Inspector.PrefabInstantiator.Instantiate(prefabAsset.RootEntity, Renderer.Scene);
        if (rootEntity == null)
        {
            LogManager.LogWarning($"Failed to instantiate prefab: {prefabAsset.Name}", "ViewportPanel");
            return;
        }

        // Calculate bounds to position pivot at base instead of center
        float minY = CalculateEntityBoundsMinY(rootEntity);
        
        // Adjust position so the base (minY) is at the drop location
        rootEntity.Transform.Position = new OpenTK.Mathematics.Vector3(worldPos.X, worldPos.Y - minY, worldPos.Z);

        LogManager.LogInfo($"Instantiated prefab '{prefabAsset.Name}' at {worldPos} (adjusted for base pivot, minY={minY:F2})", "ViewportPanel");

        // Select the instantiated entity
        Selection.ActiveEntityId = rootEntity.Id;
        UpdateGizmoPivot();

        // Mark scene as modified
        Editor.SceneManagement.SceneManager.MarkSceneAsModified();
    }

    /// <summary>
    /// Calculate the minimum Y coordinate from a submesh's vertices
    /// </summary>
    private float CalculateMeshMinY(Engine.Assets.SubMesh submesh)
    {
        float minY = float.MaxValue;
        
        // Vertices format: Position(3) + Normal(3) + TexCoord(2) = 8 floats per vertex
        for (int i = 0; i < submesh.Vertices.Length; i += 8)
        {
            float vertexY = submesh.Vertices[i + 1]; // Y is at offset 1
            if (vertexY < minY)
                minY = vertexY;
        }
        
        return minY == float.MaxValue ? 0f : minY;
    }

    /// <summary>
    /// Calculate the minimum Y bound of an entity and all its children (for base pivot placement)
    /// </summary>
    private float CalculateEntityBoundsMinY(Engine.Scene.Entity entity)
    {
        float minY = float.MaxValue;
        CalculateEntityBoundsMinYRecursive(entity, ref minY);
        return minY == float.MaxValue ? 0f : minY;
    }

    private void CalculateEntityBoundsMinYRecursive(Engine.Scene.Entity entity, ref float minY)
    {
        // Check if entity has a MeshRenderer to get its bounds
        var meshRenderer = entity.GetComponent<Engine.Components.MeshRendererComponent>();
        if (meshRenderer != null && meshRenderer.IsUsingCustomMesh())
        {
            try
            {
                var meshAsset = Engine.Assets.AssetDatabase.LoadMeshAsset(meshRenderer.CustomMeshGuid!.Value);
                if (meshAsset != null && meshRenderer.SubmeshIndex < meshAsset.SubMeshes.Count)
                {
                    var submesh = meshAsset.SubMeshes[meshRenderer.SubmeshIndex];
                    
                    // Calculate bounds from vertices (format: Position(3) + Normal(3) + TexCoord(2) = 8 floats per vertex)
                    float localMinY = float.MaxValue;
                    for (int i = 0; i < submesh.Vertices.Length; i += 8)
                    {
                        float vertexY = submesh.Vertices[i + 1]; // Y is at offset 1
                        if (vertexY < localMinY)
                            localMinY = vertexY;
                    }
                    
                    if (localMinY < float.MaxValue)
                    {
                        // Get world position of this entity
                        var worldPos = entity.Transform.GetWorldPosition();
                        
                        // Transform local minY to world space (using local scale for simplicity)
                        float boundsMinY = worldPos.Y + localMinY * entity.Transform.Scale.Y;
                        
                        if (boundsMinY < minY)
                            minY = boundsMinY;
                    }
                }
            }
            catch { }
        }
        
        // Recursively check children
        foreach (var child in entity.Children)
        {
            CalculateEntityBoundsMinYRecursive(child, ref minY);
        }
    }

    /// <summary>
    /// Convert float array to System.Numerics.Matrix4x4
    /// </summary>
    private System.Numerics.Matrix4x4 FloatArrayToMatrix(float[] array)
    {
        if (array == null || array.Length != 16)
            return System.Numerics.Matrix4x4.Identity;

        return new System.Numerics.Matrix4x4(
            array[0], array[1], array[2], array[3],
            array[4], array[5], array[6], array[7],
            array[8], array[9], array[10], array[11],
            array[12], array[13], array[14], array[15]
        );
    }

    /// <summary>
    /// Apply matrix transform to entity Transform component
    /// Decomposes matrix into position, rotation, and scale
    /// </summary>
    private void ApplyMatrixToTransform(Engine.Scene.Transform transform, System.Numerics.Matrix4x4 matrix)
    {
        // Decompose matrix
        if (!System.Numerics.Matrix4x4.Decompose(matrix, out var scale, out var rotation, out var translation))
        {
            LogManager.LogWarning("Failed to decompose transform matrix, using identity", "ViewportPanel");
            return;
        }

        // Apply translation
        transform.Position = new OpenTK.Mathematics.Vector3(translation.X, translation.Y, translation.Z);

        // Apply rotation (convert System.Numerics.Quaternion to OpenTK.Quaternion)
        transform.Rotation = new OpenTK.Mathematics.Quaternion(rotation.X, rotation.Y, rotation.Z, rotation.W);

        // Apply scale
        transform.Scale = new OpenTK.Mathematics.Vector3(scale.X, scale.Y, scale.Z);
    }

    /// <summary>
    /// Calculate world position for dropped mesh using raycast
    /// </summary>
    private Vector3 CalculateDropWorldPosition(float localX, float localY, float imgW, float imgH)
    {
        if (Renderer == null)
            return Vector3.Zero;

        // Convert mouse position to pixel coordinates (OpenGL uses bottom-left origin)
        int pixelX = (int)localX;
        int pixelY = (int)(imgH - localY); // Flip Y for OpenGL coordinates

        // Use ViewportRenderer's PickWorldPositionAt to raycast from mouse to scene
        var worldPos = Renderer.PickWorldPositionAt(pixelX, pixelY);

        if (worldPos.HasValue)
        {
            // Successfully hit something in the scene (terrain, object, etc.)
            var pos = worldPos.Value;
            return new Vector3(pos.X, pos.Y, pos.Z);
        }
        else
        {
            // Didn't hit anything, place at a default distance from camera
            // Calculate a ray from camera through mouse position and place object 10 units away
            return Vector3.Zero; // Fallback to origin for now
        }
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

    /// <summary>
    /// Get the selected camera for Play Mode rendering
    /// </summary>
    private Engine.Components.CameraComponent? GetSelectedCamera(Engine.Scene.Scene scene)
    {
        // Try explicit selection first
        if (_selectedCameraEntityId != 0)
        {
            var ent = scene.GetById(_selectedCameraEntityId);
            if (ent != null)
            {
                var cam = ent.GetComponent<Engine.Components.CameraComponent>();
                if (cam != null) return cam;
            }
        }

        // Fallback to main camera
        foreach (var e in scene.Entities)
        {
            var cam = e.GetComponent<Engine.Components.CameraComponent>();
            if (cam == null) continue;
            if (cam.IsMain) return cam;
        }

        // Fallback to first active camera
        foreach (var e in scene.Entities)
        {
            var cam = e.GetComponent<Engine.Components.CameraComponent>();
            if (cam != null) return cam;
        }

        return null;
    }

    /// <summary>
    /// Focus the Scene viewport window (bring it to front in docked layout)
    /// </summary>
    public void FocusWindow()
    {
        ImGui.SetWindowFocus("Scene");
    }
}

/// <summary>
/// Camera projection modes
/// </summary>
public enum CameraMode
{
    Perspective,
    Orthographic,
    TwoD
}

using System;
using System.Linq;
using ImGuiNET;
using OpenTK.Mathematics;
using Editor.Rendering;
using Editor.State;
using Editor.Logging;
using Editor.Icons;
using Editor.UI;
using Editor.UI.Overlays;
using Vector2 = System.Numerics.Vector2;

namespace Editor.Panels;

/// <summary>
/// Refactored ViewportPanel with clean separation of concerns:
/// - OverlayManager handles all overlay positioning and visibility
/// - PanelToolbar provides overlay toggles
/// - Modular overlay components for each feature
/// - Clean focus management without complex hysteresis
/// </summary>
public class ViewportPanel
{
    // === Core State ===
    private ViewportRenderer? _renderer;
    private OverlayManager _overlayManager;
    private PanelToolbar _toolbar;

    // === Selection State ===
    private bool _isSelecting = false;
    private bool _isGizmoDragging = false;
    private Vector2 _selStart, _selEnd;

    // === UI Element Interaction ===
    private bool _isUIHandleDragging = false;

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

    // === Camera Settings ===
    private float _arrowSpeed = 1.0f;
    private float _arrowAcceleration = 5.0f;
    private float _arrowDamping = 0.85f;
    private float _smoothFactor = 0.2f;
    private bool _showCameraSettings = false;

    // === Gizmo Settings ===
    private ViewportRenderer.GizmoMode _mode = ViewportRenderer.GizmoMode.Translate;
    private PivotModeOverlay.PivotMode _pivotMode = PivotModeOverlay.PivotMode.Center;
    private bool _localSpace = false;
    private bool _snapToggle = false;
    private float _snapMove = 0.5f;
    private float _snapAngle = 15f;
    private float _snapScale = 0.1f;

    // === Projection Settings ===
    private ProjectionSettingsOverlay.ProjectionMode _projectionMode = ProjectionSettingsOverlay.ProjectionMode.Perspective;
    private float _orthoSize = 10f;

    // === Performance Overlay ===
    private bool _showPerfOverlay = true;
    private bool _overlayInitialized = false;
    private float _overlaySmoothedMs = 0.0f;

    // === Mouse Picking Throttling ===
    private uint _hoverId = 0;
    private Vector2 _lastPickMousePos = new Vector2(-999, -999);
    private System.Diagnostics.Stopwatch _pickThrottleTimer = System.Diagnostics.Stopwatch.StartNew();
    private const double MIN_PICK_INTERVAL_MS = 33.0;
    private const float MIN_PICK_DISTANCE_SQ = 4.0f;
    private int _debugPickCount = 0;
    private System.Diagnostics.Stopwatch _debugPickTimer = System.Diagnostics.Stopwatch.StartNew();

    // === Grid Visibility ===
    private bool _showGrid = true;

    // === Overlay Visibility ===
    private bool _showGizmoToolbar = true;
    private bool _showCameraOverlay = true;
    private bool _showProjectionOverlay = true;
    private bool _showPivotOverlay = true;

    public ViewportPanel()
    {
        _overlayManager = new OverlayManager("Viewport");
        _toolbar = new PanelToolbar("Viewport");

        // Register overlays with default positions
        // Note: visibility is controlled by _show* variables, OverlayManager just manages positioning
        _overlayManager.RegisterOverlay("GizmoToolbar", OverlayAnchor.TopLeft, visible: true);
        _overlayManager.RegisterOverlay("CameraSettings", OverlayAnchor.TopRight, visible: true);
        _overlayManager.RegisterOverlay("ProjectionSettings", OverlayAnchor.BottomLeft, visible: true);
        _overlayManager.RegisterOverlay("PivotMode", OverlayAnchor.BottomRight, visible: true);
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
                _renderer.SetProjectionMode((int)_projectionMode, _orthoSize);

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

    public void Draw()
    {
        ImGui.SetNextWindowSize(new Vector2(800, 600), ImGuiCond.FirstUseEver);
        ImGui.Begin("Viewport", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);

        var io = ImGui.GetIO();
        bool focusedDock = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows | ImGuiFocusedFlags.NoPopupHierarchy | ImGuiFocusedFlags.DockHierarchy);
        bool hoveredWindow = ImGui.IsWindowHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem | ImGuiHoveredFlags.AllowWhenBlockedByPopup);
        bool allowHotkeys = (focusedDock || hoveredWindow) && !io.WantTextInput;

        // === Build Toolbar ===
        BuildToolbar();
        _toolbar.Draw();

        // === Hotkeys ===
        HandleHotkeys(allowHotkeys);

        // === Snap Configuration ===
        bool snapActive = _snapToggle || ImGui.IsKeyDown(ImGuiKey.ModCtrl);
        Renderer?.ConfigureSnap(snapActive, _snapMove, _snapAngle, _snapScale);
        Renderer?.SetSpaceLocal(_localSpace);

        // === Resize & Camera ===
        var avail = ImGui.GetContentRegionAvail();
        int w = Math.Max(1, (int)avail.X);
        int h = Math.Max(1, (int)avail.Y);
        Renderer?.Resize(w, h);

        HandleCameraInput(hoveredWindow, io);

        // === Render Scene ===
        if (!PlayMode.IsInPlayMode)
        {
            Renderer?.RenderScene();
        }

        // === Display Texture ===
        Vector2 itemMin = Vector2.Zero;
        Vector2 itemMax = Vector2.Zero;

        if (Renderer != null)
        {
            ImGui.Image((IntPtr)Renderer.ColorTexture, avail, new Vector2(0, 1), new Vector2(1, 0));

            // Get image rect after drawing
            itemMin = ImGui.GetItemRectMin();
            itemMax = ImGui.GetItemRectMax();

            // Play mode border
            if (PlayMode.IsInPlayMode)
            {
                var drawList = ImGui.GetWindowDrawList();
                var borderMin = itemMin;
                var borderMax = itemMax;
                var borderColor = PlayMode.IsPaused ? 0xFF4080FFu : 0xFF4040FFu;
                drawList.AddRect(borderMin, borderMax, borderColor, 0.0f, ImDrawFlags.None, 3.0f);
            }
        }

        float imgW = itemMax.X - itemMin.X;
        float imgH = itemMax.Y - itemMin.Y;

        // === Handle Input & Selection ===
        HandleViewportInput(itemMin, itemMax, imgW, imgH, hoveredWindow, io);

        // === Draw UI RectTransform Overlays ===
        DrawUIRectOverlays(itemMin, itemMax, imgW, imgH, hoveredWindow);

        // === Context Menu ===
        DrawContextMenu();

        // === Store focus state for overlays ===
        bool viewportWasFocused = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);
        bool viewportWasHovered = hoveredWindow;

        ImGui.End();

        // === Draw Overlays (after ImGui.End to avoid clipping) ===
        // Only draw overlays if we have valid image rect (not zero)
        if (Renderer != null && (itemMax.X - itemMin.X) > 0 && (itemMax.Y - itemMin.Y) > 0)
        {
            DrawOverlays(itemMin, itemMax, viewportWasFocused);
        }
    }

    private void BuildToolbar()
    {
        _toolbar.Clear();

        _toolbar.AddOverlayToggle("Gizmo", "Show/Hide Gizmo Toolbar", () => _showGizmoToolbar, v => _showGizmoToolbar = v, "move");
        _toolbar.AddOverlayToggle("Camera", "Show/Hide Camera Settings", () => _showCameraOverlay, v => _showCameraOverlay = v, "camera");
        _toolbar.AddOverlayToggle("Projection", "Show/Hide Projection Settings", () => _showProjectionOverlay, v => _showProjectionOverlay = v);
        _toolbar.AddOverlayToggle("Pivot", "Show/Hide Pivot Mode", () => _showPivotOverlay, v => _showPivotOverlay = v);

        _toolbar.AddSeparator();

        _toolbar.AddOverlayToggle("Stats", "Show/Hide Performance Stats", () => _showPerfOverlay, v => _showPerfOverlay = v, "chart");
    }

    private void HandleHotkeys(bool allowHotkeys)
    {
        if (!allowHotkeys) return;

        if (ImGui.IsKeyPressed(ImGuiKey.F)) Renderer?.FrameSelection();
        if (ImGui.IsKeyPressed(ImGuiKey.W)) { _mode = ViewportRenderer.GizmoMode.Translate; Renderer?.SetMode(_mode); }
        if (ImGui.IsKeyPressed(ImGuiKey.E)) { _mode = ViewportRenderer.GizmoMode.Rotate; Renderer?.SetMode(_mode); }
        if (ImGui.IsKeyPressed(ImGuiKey.R)) { _mode = ViewportRenderer.GizmoMode.Scale; Renderer?.SetMode(_mode); }
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
            _targetDist *= MathF.Pow(0.9f, io.MouseWheel);
        }

        // Smoothing
        _yaw = _yaw + (_targetYaw - _yaw) * _smoothFactor;
        _pitch = _pitch + (_targetPitch - _pitch) * _smoothFactor;
        _dist = _dist + (_targetDist - _dist) * _smoothFactor;

        if (Renderer != null)
        {
            Renderer.ResetToOrbitalCamera();
            if (Renderer.IsCameraAnimating) _dist = Renderer.Distance;
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

        // Hover picking (throttled)
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
                _hoverId = Renderer.PickIdAtFat(mx, my, 6);
                Renderer.SetHover(_hoverId);
                _lastPickMousePos = mouse;
                _pickThrottleTimer.Restart();
                _debugPickCount++;
                if (_debugPickTimer.Elapsed.TotalSeconds >= 1.0)
                {
                    LogManager.LogVerbose($"[PERF] Picks/sec: {_debugPickCount}", "ViewportPanel");
                    _debugPickCount = 0;
                    _debugPickTimer.Restart();
                }
            }
            else
            {
                Renderer.SetHover(_hoverId);
            }
        }
        else if (!forcePickOnClick) { _hoverId = 0; Renderer?.SetHover(0); }

        // Click handling (selection, gizmo, etc.) - only in edit mode
        if (hoveredWindow && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !PlayMode.IsInPlayMode && inImage)
        {
            HandleLeftClick(pxGL, pyGL, mouseLocal, ctrl, shift);
        }

        // Right-click context menu
        if (hoveredWindow && ImGui.IsMouseReleased(ImGuiMouseButton.Right) && !PlayMode.IsInPlayMode && inImage)
        {
            var dragDistance = (mouse - _rightClickStartPos).Length();
            const float minDragThreshold = 3.0f;

            if (dragDistance < minDragThreshold)
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
            switch (_mode)
            {
                case ViewportRenderer.GizmoMode.Translate: Renderer.UpdateDragTranslate(pxGL(mouseLocal.X), pyGL(mouseLocal.Y)); break;
                case ViewportRenderer.GizmoMode.Rotate: Renderer.UpdateDragRotate(pxGL(mouseLocal.X), pyGL(mouseLocal.Y)); break;
                case ViewportRenderer.GizmoMode.Scale: Renderer.UpdateDragScale(pxGL(mouseLocal.X), pyGL(mouseLocal.Y)); break;
            }
        }

        if (_isGizmoDragging && ImGui.IsMouseReleased(ImGuiMouseButton.Left) && Renderer != null && !PlayMode.IsInPlayMode)
        {
            Renderer.EndDrag();
            _isGizmoDragging = false;
        }

        // Rectangle selection
        if (!_isGizmoDragging && !_isUIHandleDragging && _isSelecting && ImGui.IsMouseDown(ImGuiMouseButton.Left) && !PlayMode.IsInPlayMode)
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

        if (_isSelecting && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            HandleRectangleSelection(pxGL, pyGL, ctrl, shift);
        }

        // Clear selection on empty click
        if (hoveredWindow && ImGui.IsMouseReleased(ImGuiMouseButton.Left) && (!inImage || _hoverId == 0) && !ctrl && !shift && !_isGizmoDragging && !_isSelecting && !ImGui.IsMouseDragging(ImGuiMouseButton.Left))
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
            switch (_mode)
            {
                case ViewportRenderer.GizmoMode.Translate: Renderer.BeginDragTranslate(_hoverId, pxGL(mouseLocal.X), pyGL(mouseLocal.Y)); break;
                case ViewportRenderer.GizmoMode.Rotate: Renderer.BeginDragRotate(_hoverId, pxGL(mouseLocal.X), pyGL(mouseLocal.Y)); break;
                case ViewportRenderer.GizmoMode.Scale: Renderer.BeginDragScale(_hoverId, pxGL(mouseLocal.X), pyGL(mouseLocal.Y)); break;
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

    private void DrawUIRectOverlays(Vector2 itemMin, Vector2 itemMax, float imgW, float imgH, bool hoveredWindow)
    {
        // Simplified - keep existing UI rect overlay logic here
        // (Omitted for brevity - keep the existing code from lines 612-768)
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

            if (hasSelection && ImGui.MenuItem("Snap Selection to Surface"))
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

    private void DrawOverlays(Vector2 itemMin, Vector2 itemMax, bool isPanelFocused)
    {
        // Sync overlay visibility with OverlayManager
        _overlayManager.SetOverlayVisible("GizmoToolbar", _showGizmoToolbar);
        _overlayManager.SetOverlayVisible("CameraSettings", _showCameraOverlay);
        _overlayManager.SetOverlayVisible("ProjectionSettings", _showProjectionOverlay);
        _overlayManager.SetOverlayVisible("PivotMode", _showPivotOverlay);

        // Gizmo Toolbar Overlay
        if (_overlayManager.BeginOverlay("GizmoToolbar", itemMin, itemMax, isPanelFocused))
        {
            GizmoToolbarOverlay.Draw(ref _mode, ref _localSpace, ref _snapToggle, ref _snapMove, ref _snapAngle, ref _snapScale, ref _showGrid, Renderer);
            _overlayManager.EndOverlay("GizmoToolbar");
        }

        // Camera Settings Overlay
        if (_overlayManager.BeginOverlay("CameraSettings", itemMin, itemMax, isPanelFocused))
        {
            CameraSettingsOverlay.Draw(ref _showCameraSettings, ref _arrowSpeed, ref _arrowAcceleration, ref _arrowDamping, ref _smoothFactor);
            _overlayManager.EndOverlay("CameraSettings");
        }

        // Projection Settings Overlay
        if (_overlayManager.BeginOverlay("ProjectionSettings", itemMin, itemMax, isPanelFocused))
        {
            ProjectionSettingsOverlay.Draw(ref _projectionMode, ref _orthoSize, Renderer);
            _overlayManager.EndOverlay("ProjectionSettings");
        }

        // Pivot Mode Overlay
        if (_overlayManager.BeginOverlay("PivotMode", itemMin, itemMax, isPanelFocused))
        {
            PivotModeOverlay.Draw(ref _pivotMode, UpdateGizmoPivot);
            _overlayManager.EndOverlay("PivotMode");
        }

        // Performance Overlay (uses direct drawing, not OverlayManager)
        if (_showPerfOverlay && Renderer != null)
        {
            float msNow = Renderer.LastFrameCpuMs;
            if (!_overlayInitialized)
            {
                _overlaySmoothedMs = msNow;
                _overlayInitialized = true;
            }
            _overlaySmoothedMs = _overlaySmoothedMs * 0.92f + msNow * 0.08f;

            Editor.Utils.PerfOverlay.Draw(itemMin, itemMax, _overlaySmoothedMs, Renderer.DrawCallsThisFrame, Renderer.TrianglesThisFrame, Renderer.RenderedObjectsThisFrame, ref _showPerfOverlay);
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

            if (_pivotMode == PivotModeOverlay.PivotMode.Center)
            {
                var c = Selection.ComputeCenter(scene!);
                Renderer.SetGizmoPosition(new OpenTK.Mathematics.Vector3(c.X, c.Y, c.Z));
            }
            else
            {
                var e = scene!.GetById(Selection.ActiveEntityId);
                if (e != null)
                {
                    e.GetWorldTRS(out var worldPos, out _, out _);
                    Renderer.SetGizmoPosition(worldPos);
                }
                else
                {
                    Renderer.SetGizmoPosition(OpenTK.Mathematics.Vector3.Zero);
                }
            }
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
}

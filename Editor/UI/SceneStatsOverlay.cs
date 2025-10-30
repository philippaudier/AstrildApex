using System;
using System.Numerics;
using ImGuiNET;
// OpenTK types are referenced explicitly where needed to avoid conflicts with System.Numerics
using Editor.Rendering;
using Editor.State;

namespace Editor.UI
{
    /// <summary>
    /// Floating scene stats overlay drawn over the viewport image (anchored top-left).
    /// Uses an ImGui window so standard widgets (Checkbox, Combo, Buttons) have correct
    /// hit testing and keyboard/mouse behaviour. Organized with separators and sections.
    /// </summary>
    public static class SceneStatsOverlay
    {
    // Track dragging state so we persist position only when user finishes dragging
    private static System.Numerics.Vector2 s_lastWindowPos = new System.Numerics.Vector2(-9999f, -9999f);
    private static bool s_wasDragging = false;
    // Lock state: when true the overlay cannot be moved
    private static bool s_locked = false;

    public static void Draw(Vector2 imgMin, Vector2 imgMax, ViewportRenderer? renderer, double smoothedMs, ref bool showOverlay, Editor.UI.ViewportOverlays? overlays = null, Editor.UI.ViewportToolbar? toolbar = null)
        {
            // Basic stats
            float fpsVal = smoothedMs > 0.0 ? (float)(1000.0 / smoothedMs) : 0f;
            int drawCalls = renderer?.DrawCallsThisFrame ?? 0;
            int tris = renderer?.TrianglesThisFrame ?? 0;
            int objects = renderer?.RenderedObjectsThisFrame ?? 0;

            string sFps = $"FPS: {fpsVal:0.0} ({smoothedMs:0.0} ms)";
            string sDraw = $"DrawCalls: {drawCalls}";
            string sTris = $"Tris: {tris}";
            string sObj = $"Objects: {objects}";

            // Position: prefer saved position from settings if available, otherwise anchor to top-left of viewport
            float margin = 8f;
            var saved = EditorSettings.ViewportHudPosition;
            Vector2 preferred;
            if (saved.X >= 0f && saved.Y >= 0f)
            {
                preferred = new Vector2(saved.X, saved.Y);
            }
            else
            {
                preferred = new Vector2(MathF.Min(imgMax.X - 16f - 320f, imgMin.X + margin), imgMin.Y + margin);
                preferred.X = MathF.Max(imgMin.X + margin, preferred.X);
            }

            // Use a window with a title bar so users can drag it easily. Theme colors are applied globally by ThemeManager.
            // Allow the window to receive focus but don't bring it to front on focus to avoid reordering/docking flicker
            var wndFlags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBringToFrontOnFocus;
            ImGui.SetNextWindowBgAlpha(0.68f);
            // Use Once by default so ImGui does not keep forcing position every frame (allows dragging)
            ImGui.SetNextWindowPos(preferred, ImGuiCond.Once);

            // When collapsed show a small + button to expand
            if (!showOverlay)
            {
                // Force the collapsed '+' button to the preferred position so it is always reachable
                ImGui.SetNextWindowPos(preferred, ImGuiCond.Always);
                if (ImGui.Begin("##ToolbarCollapsed", wndFlags))
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 1, 1, 1));
                    if (ImGui.SmallButton("+")) showOverlay = true;
                    ImGui.PopStyleColor();
                }
                ImGui.End();
                return;
            }

            // Keyboard shortcut: Ctrl+Shift+H toggles the overlay (safety to recover if hidden)
            var io = ImGui.GetIO();
            if (io.KeyCtrl && io.KeyShift && ImGui.IsKeyPressed(ImGuiKey.H))
            {
                showOverlay = !showOverlay;
            }

            // If locked, prevent moving
            if (s_locked) wndFlags |= ImGuiWindowFlags.NoMove;

            // Begin the draggable overlay window. Using a unique name ensures we don't conflict with other windows.
            if (ImGui.Begin("Toolbar##ViewportOverlay", wndFlags))
            {
                // NOTE: we intentionally avoid programmatically forcing focus here (SetWindowFocus)
                // because it can change docking focus and cause the UI to flicker when the mouse
                // hovers the toolbar. Allow ImGui to handle focus naturally; NoBringToFrontOnFocus
                // prevents aggressive reordering while still allowing item activation on click.
                // Debug: show ImGui input/capture state to help diagnose interaction issues
                var _io = ImGui.GetIO();
                ImGui.TextDisabled($"io.WantCaptureMouse: {_io.WantCaptureMouse}  MouseDown0: {_io.MouseDown[0]}");
                ImGui.SameLine();
                ImGui.TextDisabled($"AnyItemActive: {ImGui.IsAnyItemActive()} WindowHovered: {ImGui.IsWindowHovered()} WindowFocused: {ImGui.IsWindowFocused()} ");

                // Enforce clamping to the scene rect so the window cannot leave the image area.
                // Only set the window pos when it is actually outside the allowed rect to avoid
                // cancelling item activation / mouse clicks caused by forcing the position every frame.
                var _winPos = ImGui.GetWindowPos();
                var _winSize = ImGui.GetWindowSize();

                // If the window is completely outside the viewport image rect (user likely persisted a bad position)
                // then reset it to the preferred default position. This recovers the toolbar when it's lost off-screen.
                var winMin = _winPos;
                var winMax = _winPos + _winSize;
                bool overlaps = !(winMax.X < imgMin.X || winMin.X > imgMax.X || winMax.Y < imgMin.Y || winMin.Y > imgMax.Y);
                if (!overlaps)
                {
                    // Reset to computed preferred position (anchored top-left inside viewport)
                    ImGui.SetWindowPos(preferred, ImGuiCond.Always);
                    // Update local values after forcing pos
                    _winPos = ImGui.GetWindowPos();
                    _winSize = ImGui.GetWindowSize();
                }

                float _clampX = Math.Max(imgMin.X + margin, Math.Min(imgMax.X - _winSize.X - margin, _winPos.X));
                float _clampY = Math.Max(imgMin.Y + margin, Math.Min(imgMax.Y - _winSize.Y - margin, _winPos.Y));
                var _clampedPos = new System.Numerics.Vector2(_clampX, _clampY);
                // Only force the position when it differs to avoid interfering with normal ImGui input handling
                if (Math.Abs(_clampedPos.X - _winPos.X) > 0.5f || Math.Abs(_clampedPos.Y - _winPos.Y) > 0.5f)
                {
                    ImGui.SetWindowPos(_clampedPos, ImGuiCond.Always);
                }

                // Header
                ImGui.TextUnformatted("Toolbar");
                ImGui.SameLine();
                // Lock/unlock button
                if (ImGui.SmallButton(s_locked ? "🔒" : "🔓"))
                {
                    s_locked = !s_locked;
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(s_locked ? "Overlay locked (no move)" : "Unlock overlay to allow moving");
                ImGui.Separator();

                // Stats
                ImGui.TextUnformatted(sFps);
                ImGui.TextUnformatted(sDraw);
                ImGui.TextUnformatted(sTris);
                ImGui.TextUnformatted(sObj);

                ImGui.Separator();

                // Controls section
                // --- Transform tools & snap (migrated from bottom toolbar) ---
                if (toolbar != null)
                {
                    ImGui.TextUnformatted("Tools");
                    ImGui.SameLine();

                    // Move
                    if (ImGui.Button("Move (W)")) toolbar.CurrentMode = ViewportRenderer.GizmoMode.Translate;
                    if (toolbar.CurrentMode == ViewportRenderer.GizmoMode.Translate)
                    {
                        var dl = ImGui.GetWindowDrawList();
                        dl.AddRect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(0.3f, 0.5f, 1f, 1f)), 3f, ImDrawFlags.None, 2f);
                    }
                    ImGui.SameLine();

                    // Rotate
                    if (ImGui.Button("Rotate (E)")) toolbar.CurrentMode = ViewportRenderer.GizmoMode.Rotate;
                    if (toolbar.CurrentMode == ViewportRenderer.GizmoMode.Rotate)
                    {
                        var dl = ImGui.GetWindowDrawList();
                        dl.AddRect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(0.3f, 0.5f, 1f, 1f)), 3f, ImDrawFlags.None, 2f);
                    }
                    ImGui.SameLine();

                    // Scale
                    if (ImGui.Button("Scale (R)")) toolbar.CurrentMode = ViewportRenderer.GizmoMode.Scale;
                    if (toolbar.CurrentMode == ViewportRenderer.GizmoMode.Scale)
                    {
                        var dl = ImGui.GetWindowDrawList();
                        dl.AddRect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(0.3f, 0.5f, 1f, 1f)), 3f, ImDrawFlags.None, 2f);
                    }

                    ImGui.Separator();

                    // Snap toggles (properties -> local ref then write back)
                    bool snapGrid = toolbar.SnapToGrid;
                    if (ImGui.Checkbox("Snap Grid", ref snapGrid)) toolbar.SnapToGrid = snapGrid;
                    ImGui.SameLine();
                    bool vertexSnap = toolbar.VertexSnap;
                    if (ImGui.Checkbox("Vertex Snap", ref vertexSnap)) toolbar.VertexSnap = vertexSnap;

                    ImGui.Separator();
                }

                bool gridVisible = overlays != null ? overlays.ShowGrid : EditorSettings.ShowGrid;
                if (ImGui.Checkbox("🔲 Show Grid", ref gridVisible))
                {
                    // Update both the overlays state (so ViewportPanelModern reads the change)
                    if (overlays != null) overlays.ShowGrid = gridVisible;
                    // Persist to settings
                    EditorSettings.ShowGrid = gridVisible;
                    // Apply immediately to renderer if available
                    try { if (renderer != null) renderer.GridVisible = gridVisible; } catch { }
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Toggle the scene grid visibility (persisted)");

                bool vsync = EditorSettings.VSync;
                if (ImGui.Checkbox("⏱ VSync", ref vsync))
                {
                    EditorSettings.VSync = vsync;
                    try { var gw = Editor.Program.GameWindow; if (gw != null) gw.VSync = vsync ? OpenTK.Windowing.Common.VSyncMode.On : OpenTK.Windowing.Common.VSyncMode.Off; } catch { }
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Enable/disable vertical sync (persisted)");

                ImGui.Separator();

                // Camera section
                int camMode = renderer != null ? renderer.ProjectionMode : EditorSettings.CameraProjectionMode;
                var camModes = new[] { "Perspective", "Orthographic", "2D" };
                if (ImGui.Combo("Camera Mode", ref camMode, camModes, camModes.Length))
                {
                    try
                    {
                        var orthoSize = renderer != null ? renderer.OrthoSize : EditorSettings.CameraOrthoSize;
                        renderer?.SetProjectionMode(camMode, orthoSize);
                        EditorSettings.CameraProjectionMode = camMode;
                    }
                    catch { }
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Choose camera projection mode. Ortho mode reveals the Ortho Size slider.");
                if (camMode == 1)
                {
                    float ort = renderer != null ? renderer.OrthoSize : EditorSettings.CameraOrthoSize;
                    if (ImGui.SliderFloat("Ortho Size", ref ort, 0.1f, 500f, "%.1f"))
                    {
                        try { renderer?.SetProjectionMode(camMode, ort); EditorSettings.CameraOrthoSize = ort; } catch { }
                    }
                }

                ImGui.Separator();

                // Shortcuts
                ImGui.TextUnformatted("Shortcuts");
                ImGui.BeginGroup();
                // Reset Camera with icon + tooltip
                if (ImGui.Button("⟲ Reset Camera"))
                {
                    try
                    {
                        var reset = new OrbitCameraState
                        {
                            Yaw = OpenTK.Mathematics.MathHelper.DegreesToRadians(-30f),
                            Pitch = OpenTK.Mathematics.MathHelper.DegreesToRadians(-15f),
                            Distance = 3.0f,
                            Target = new OpenTK.Mathematics.Vector3(0f, 0f, 0f)
                        };
                        renderer?.ApplyOrbitCameraState(reset, true);
                        EditorSettings.ViewportCameraState = reset;
                    }
                    catch { }
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Reset camera to a default orbit (persisted)");
                ImGui.SameLine();
                if (ImGui.Button("🔎 Frame Selection")) { try { renderer?.FrameSelection(true); } catch { } }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Frame the currently selected objects");
                ImGui.SameLine();
                if (ImGui.Button("◎ Focus Scene Center"))
                {
                    try
                    {
                        var focus = new OrbitCameraState
                        {
                            Yaw = OpenTK.Mathematics.MathHelper.DegreesToRadians(-30f),
                            Pitch = OpenTK.Mathematics.MathHelper.DegreesToRadians(-15f),
                            Distance = 3.0f,
                            Target = new OpenTK.Mathematics.Vector3(0f, 0f, 0f)
                        };
                        renderer?.ApplyOrbitCameraState(focus, true);
                        EditorSettings.ViewportCameraState = focus;
                    }
                    catch { }
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Center camera on scene origin (persisted)");
                ImGui.EndGroup();

                ImGui.Separator();

                // Save window position when moved, but only persist on drag-end to reduce writes
                var winPos = ImGui.GetWindowPos();
                var winSize = ImGui.GetWindowSize();
                // Clamp inside viewport rect (imgMin..imgMax) with margin
                float clampX = Math.Max(imgMin.X + margin, Math.Min(imgMax.X - winSize.X - margin, winPos.X));
                float clampY = Math.Max(imgMin.Y + margin, Math.Min(imgMax.Y - winSize.Y - margin, winPos.Y));
                var clamped = new System.Numerics.Vector2(clampX, clampY);

                // Detect movement and dragging
                if (s_lastWindowPos.X != winPos.X || s_lastWindowPos.Y != winPos.Y)
                {
                    if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
                    {
                        s_wasDragging = true;
                    }
                    s_lastWindowPos = winPos;
                }

                // Persist only when the user finished dragging (mouse released)
                if (s_wasDragging && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                {
                    EditorSettings.ViewportHudPosition = (clamped.X, clamped.Y);
                    s_wasDragging = false;
                    s_lastWindowPos = winPos;
                }

                // Small hide button
                if (ImGui.SmallButton("Hide")) showOverlay = false;
            }
            ImGui.End();
        }
    }
}

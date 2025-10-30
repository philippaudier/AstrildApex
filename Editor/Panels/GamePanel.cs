using System;
using System.Linq;
using System.Threading.Tasks;
using ImGuiNET;
using Engine.Scene;
using Engine.Components;
using Engine.Systems;
using Editor.Rendering;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Mathematics;

namespace Editor.Panels
{
    public static class GamePanel
    {
        private static ViewportRenderer? _gameRenderer;
        // Temporary reusable lists to avoid per-frame LINQ allocations when enumerating cameras
        private static readonly System.Collections.Generic.List<uint> _tmpCameraIds = new System.Collections.Generic.List<uint>();
        private static readonly System.Collections.Generic.List<string> _tmpCameraNames = new System.Collections.Generic.List<string>();
        // User-selected camera entity id for the Game panel. If 0, use scene.GetMainCamera().
        private static uint _selectedCameraEntityId = 0;

        // Perf overlay state for GamePanel
        private static bool _showGamePerfOverlay = true;
        private static bool _gameOverlayInitialized = false;
        private static float _gameOverlaySmoothedMs = 0.0f;

        // Cache last-known locked state to avoid repeatedly clearing confine rect every frame
        private static bool _lastKnownLockedState = false;

        // ImGui menu system for in-game UI
        private static Engine.UI.ImGuiMenu.ImGuiMenuSystem? _imguiMenu;

        // Game Panel Options (Unity-style settings)
        private static GamePanelOptions _options = new GamePanelOptions();
        
        // Cache last render dimensions to avoid unnecessary Resize() calls
        private static int _lastRenderWidth = 0;
        private static int _lastRenderHeight = 0;
        
        // Maximize on Play state
        private static bool _isMaximized = false;

        public static void Draw()
        {
            // Maximize on Play support: fullscreen window
            bool isMaximizedMode = _isMaximized;
            
            if (isMaximizedMode)
            {
                // Fullscreen maximized window
                var viewport = ImGui.GetMainViewport();
                ImGui.SetNextWindowPos(viewport.Pos);
                ImGui.SetNextWindowSize(viewport.Size);
                ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f);
                ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f);
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, System.Numerics.Vector2.Zero);
                
                var windowFlags = ImGuiWindowFlags.NoDecoration | 
                                ImGuiWindowFlags.NoMove | 
                                ImGuiWindowFlags.NoResize | 
                                ImGuiWindowFlags.NoSavedSettings;
                
                bool visible = ImGui.Begin("Game (Maximized)", windowFlags);
                ImGui.PopStyleVar(3);
                
                if (!visible) {
                    Engine.Input.Cursor.ClearSystemConfine();
                    ImGui.End();
                    return;
                }
                
                // ESC to exit maximized mode
                if (ImGui.IsKeyPressed(ImGuiKey.Escape))
                {
                    _isMaximized = false;
                }
                
                // Draw hint
                var windowSize = ImGui.GetWindowSize();
                string hint = "Press ESC to exit fullscreen";
                var textSize = ImGui.CalcTextSize(hint);
                ImGui.SetCursorPos(new System.Numerics.Vector2(windowSize.X - textSize.X - 10, 10));
                ImGui.TextColored(new System.Numerics.Vector4(1, 1, 1, 0.7f), hint);
                ImGui.SetCursorPos(System.Numerics.Vector2.Zero);
            }
            else
            {
                // Normal docked window
                bool visible = ImGui.Begin("Game");
                if (!visible) {
                    Engine.Input.Cursor.ClearSystemConfine();
                    ImGui.End();
                    return;
                }
            }
            
            // Utiliser la scène runtime en Play Mode, sinon la scène d'édition
            Scene? scene;
            if (PlayMode.IsInPlayMode) {
                scene = PlayMode.PlayScene;    // Utilise la scène runtime en mode Play/Paused

                // Initialize ImGui menu system when entering play mode
                if (_imguiMenu == null)
                {
                    Console.WriteLine("[GamePanel] Initializing ImGui menu system...");
                    _imguiMenu = new Engine.UI.ImGuiMenu.ImGuiMenuSystem();
                }

                // Update ImGui menu
                if (_imguiMenu != null)
                {
                    var io = ImGui.GetIO();
                    _imguiMenu.Update(io.DeltaTime);
                }
            }
            else {
                // In Edit mode: render the scene's Main Camera component (if any).
                // This allows the Game panel to show what the Main Camera would see in the editor
                // without swapping or replacing the Viewport's scene.
                scene = EditorUI.MainViewport.Renderer?.Scene;
                
                // Cleanup ImGui menu when exiting play mode
                if (_imguiMenu != null)
                {
                    Console.WriteLine("[GamePanel] Disposing ImGui menu system (exited play mode)");
                    _imguiMenu.Dispose();
                    _imguiMenu = null;
                }
            }

            if (scene == null) {
                ImGui.TextDisabled("Scene not available.");
                ImGui.End();
                return;
            }

            // Camera selection: use explicit user selection if present, otherwise prefer IsMain
            CameraComponent? camera = null;
            if (_selectedCameraEntityId != 0)
            {
                var ent = scene.GetById(_selectedCameraEntityId);
                if (ent != null) camera = ent.GetComponent<CameraComponent>();
            }

            if (camera == null)
            {
                // Prioritize CameraComponent flagged as IsMain
                foreach (var e in scene.Entities)
                {
                    var cam = e.GetComponent<CameraComponent>();
                    if (cam == null) continue;
                    if (cam.IsMain)
                    {
                        camera = cam;
                        break;
                    }
                }
            }

            if (camera == null)
            {
                // Fallback: first active & enabled camera
                foreach (var e in scene.Entities)
                {
                    var cam = e.GetComponent<CameraComponent>();
                    if (cam == null) continue;
                    if (!e.Active || !cam.Enabled) continue;
                    camera = cam;
                    break;
                }
            }

            // Camera selection UI: let user pick which camera entity the Game panel should render
            // Build list of cameras in the scene (avoid LINQ allocations per-frame)
            uint[] cameraEntityIds = Array.Empty<uint>();
            string[] cameraNames = Array.Empty<string>();
            try
            {
                // Reuse temporary lists to minimize per-frame allocations
                // Note: these lists are private static to the GamePanel class
                _tmpCameraIds.Clear();
                _tmpCameraNames.Clear();
                foreach (var e in scene.Entities)
                {
                    var cam = e.GetComponent<CameraComponent>();
                    if (cam == null) continue;
                    _tmpCameraIds.Add(e.Id);
                    _tmpCameraNames.Add(!string.IsNullOrEmpty(e.Name) ? e.Name : $"Entity {e.Id}");
                }
                if (_tmpCameraIds.Count > 0)
                {
                    cameraEntityIds = _tmpCameraIds.ToArray();
                    cameraNames = _tmpCameraNames.ToArray();
                }

                // Default selection: if none chosen and there is a main camera, pick it
                if (_selectedCameraEntityId == 0)
                {
                    var mainCam = scene.GetMainCamera();
                    if (mainCam != null && mainCam.Entity != null)
                        _selectedCameraEntityId = mainCam.Entity.Id;
                }
            }
            catch { }

            // Build camera selector state but defer drawing into an overlay anchored to the rendered image
            int currentIndex = -1;
            if (cameraNames.Length > 0)
            {
                currentIndex = Array.FindIndex(cameraEntityIds, id => id == _selectedCameraEntityId);
                if (currentIndex < 0) currentIndex = 0;
            }

            // Initialiser le renderer du GamePanel (ViewportRenderer dédié)
            var avail = ImGui.GetContentRegionAvail();
            int availWidth = Math.Max(1, (int)avail.X);
            int availHeight = Math.Max(1, (int)avail.Y);
            
            // Calculate render dimensions with aspect ratio constraints (Unity-style)
            int renderWidth = availWidth;
            int renderHeight = availHeight;
            float offsetX = 0f;
            float offsetY = 0f;
            
            float targetAspect = GetTargetAspectRatio();
            if (targetAspect > 0)
            {
                float panelAspect = (float)availWidth / availHeight;
                
                if (panelAspect > targetAspect)
                {
                    // Panel too wide - fit to height, add pillarbox (black bars on sides)
                    renderWidth = (int)(availHeight * targetAspect);
                    renderHeight = availHeight;
                    offsetX = (availWidth - renderWidth) * 0.5f;
                }
                else
                {
                    // Panel too tall - fit to width, add letterbox (black bars top/bottom)
                    renderWidth = availWidth;
                    renderHeight = (int)(availWidth / targetAspect);
                    offsetY = (availHeight - renderHeight) * 0.5f;
                }
            }
            
            // Apply offset to center the render
            if (offsetX > 0 || offsetY > 0)
            {
                var cursorPos = ImGui.GetCursorPos();
                ImGui.SetCursorPos(new System.Numerics.Vector2(cursorPos.X + offsetX, cursorPos.Y + offsetY));
            }
            
            int w = renderWidth;
            int h = renderHeight;

            if (_gameRenderer == null)
            {
                _gameRenderer = new ViewportRenderer();
                _gameRenderer.SetGameMode(true); // Marquer comme mode jeu
                _gameRenderer.ForceEditorCamera = false; // Pas de caméra éditeur
                // The Game panel should not display the editor grid overlay.
                // Force grid off for the dedicated game viewport renderer.
                _gameRenderer.GridVisible = false;

                // Copy some perf-related settings from the main editor viewport so the
                // Game panel doesn't accidentally render at a higher resolution or
                // with different debug options which would halve the observed FPS.
                try
                {
                    var main = EditorUI.MainViewport.Renderer;
                    if (main != null)
                    {
                        // Do NOT copy GridVisible from the editor main viewport - the Game panel
                        // should not show the editor grid overlay. Copy other visual settings.
                        _gameRenderer.SSAOSettings = main.SSAOSettings;
                    }
                }
                catch (Exception) { }

                Console.WriteLine($"[GamePanel] Créé ViewportRenderer pour GamePanel: {_gameRenderer.GetHashCode()}");
            }

            // Apply editor SSAO settings to the GamePanel renderer so Game view matches the
            // SSAO controls in the Rendering inspector (instead of using hardcoded debug values).
            // PERF: Use the public property instead of reflection (if available), or cache reflection
            _gameRenderer.SSAOSettings = Editor.State.EditorSettings.SSAOSettings;
            
            // Apply resolution scale from options
            _gameRenderer.RenderScale = _options.ResolutionScale;

            // Only resize if dimensions actually changed to avoid recreating framebuffers every frame
            if (w != _lastRenderWidth || h != _lastRenderHeight)
            {
                _gameRenderer.Resize(w, h);
                _lastRenderWidth = w;
                _lastRenderHeight = h;
            }
            
            _gameRenderer.SetScene(scene);

            // Storage for menu viewport bounds
            System.Numerics.Vector2 gameImageMin = default;
            System.Numerics.Vector2 gameImageMax = default;
            bool hasGameImage = false;

            if (camera != null)
            {
                // Calculer les matrices de la caméra
                float aspect = (float)w / Math.Max(1, h);
                var viewMat = camera.ViewMatrix;
                var projMat = camera.ProjectionMatrix(aspect);

                // Forcer l'utilisation des matrices de la caméra de jeu
                _gameRenderer.SetCameraMatrices(viewMat, projMat);

                // Rendu de la scène
                _gameRenderer.RenderScene();

                // Afficher la texture avec la taille calculée (pas la taille du panel entier)
                var renderSize = new System.Numerics.Vector2(renderWidth, renderHeight);
                ImGui.Image((IntPtr)_gameRenderer.ColorTexture, renderSize, new System.Numerics.Vector2(0, 1), new System.Numerics.Vector2(1, 0));
                
                // Store image rect for UI overlay and input
                gameImageMin = ImGui.GetItemRectMin();
                gameImageMax = ImGui.GetItemRectMax();
                hasGameImage = true;
                var imgMin = gameImageMin;
                var imgMax = gameImageMax;
                var imgSize = new System.Numerics.Vector2(imgMax.X - imgMin.X, imgMax.Y - imgMin.Y);
                
                // Avoid using a full invisible button (it prevented overlay interactions).
                // Compute input state from mouse position relative to the game image rect.
                var io = ImGui.GetIO();
                var mouse = ImGui.GetMousePos();
                var itemMin = imgMin;
                var itemMax = imgMax;
                bool hoveredImg = mouse.X >= itemMin.X && mouse.X <= itemMax.X && mouse.Y >= itemMin.Y && mouse.Y <= itemMax.Y;

                // If cursor is Locked or Confined, prevent ImGui from capturing mouse globally
                // This allows gameplay scripts (e.g., CameraController) to receive mouse deltas
                if (Engine.Input.Cursor.isLocked || Engine.Input.Cursor.isConfined)
                {
                    io.WantCaptureMouse = false; // Let game consume mouse deltas
                }

                // --- Themed, draggable HUD overlay anchored to the game image (persisted) ---
                // Cache theme locally to avoid repeated property access every frame
                var _theme = Editor.Themes.ThemeManager.CurrentTheme;
                var savedPos = Editor.State.EditorSettings.GameHudPosition;
                System.Numerics.Vector2 hudPos;
                if (savedPos.X >= 0 && savedPos.Y >= 0)
                {
                    hudPos = new System.Numerics.Vector2(savedPos.X, savedPos.Y);
                }
                else
                {
                    hudPos = imgMin + new System.Numerics.Vector2(8, 8);
                }

                // Apply theme colors (cached)
                var winBg = _theme.ChildBackground;
                var border = _theme.Border;
                ImGui.PushStyleColor(ImGuiCol.WindowBg, winBg);
                ImGui.PushStyleColor(ImGuiCol.Border, border);
                ImGui.SetNextWindowPos(hudPos, ImGuiCond.Always);
                ImGui.SetNextWindowBgAlpha(winBg.W);
                if (ImGui.Begin("##GameHUD", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking))
                {
                    // Allow dragging the HUD: if hovered and left mouse dragging, update saved position
                    if (ImGui.IsWindowHovered() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                    {
                        var drag = ImGui.GetIO().MouseDelta;
                        hudPos += drag;
                        Editor.State.EditorSettings.GameHudPosition = (hudPos.X, hudPos.Y);
                        ImGui.SetWindowPos(hudPos);
                    }

                    // Content
                    if (!PlayMode.IsInPlayMode && cameraNames.Length > 0)
                    {
                        ImGui.TextColored(_theme.InspectorSection, "Camera:"); ImGui.SameLine();
                        ImGui.SetNextItemWidth(160);
                        if (ImGui.Combo("##gameCameraSelectOverlay", ref currentIndex, cameraNames, cameraNames.Length))
                        {
                            _selectedCameraEntityId = cameraEntityIds[currentIndex];
                        }
                        ImGui.SameLine();
                        DrawGameOptionsMenu();
                    }

                    if (PlayMode.IsInPlayMode)
                    {
                        ImGui.TextColored(new System.Numerics.Vector4(1f, 0.6f, 0.1f, 1f), "PLAY MODE");
                    }
                }
                ImGui.End();
                ImGui.PopStyleColor(2);

                if (_gameRenderer != null)
                {
                    // Use actual render time from ViewportRenderer instead of io.DeltaTime
                    // for accurate FPS measurement (io.DeltaTime includes simulation overhead)
                    float msNow = _gameRenderer.LastFrameCpuMs;
                    if (!_gameOverlayInitialized) { _gameOverlaySmoothedMs = msNow; _gameOverlayInitialized = true; }
                    _gameOverlaySmoothedMs = _gameOverlaySmoothedMs * 0.92f + msNow * 0.08f;

                    // Performance overlay for GamePanel is disabled to avoid duplication with
                    // the Scene viewport perf HUD. The viewport (Scene) is the canonical
                    // place to display FPS/Perf info.
                    // Previously: Editor.Utils.PerfOverlay.Draw(...)
                }

                // Configure OS-level confine to the Game image rectangle when in Confined or Locked mode.
                // Unity behaves similarly: when the cursor is locked for FPS-style control it is hidden/grabbed
                // and the OS cursor is confined to the view so it cannot escape the editor window, while
                // the engine consumes relative deltas from the OS (no per-frame warping required).
                var currentLockMode = Engine.Input.Cursor.lockState;
                if (currentLockMode == Engine.Input.CursorLockMode.Locked || currentLockMode == Engine.Input.CursorLockMode.Confined)
                {
                    // Set GamePanel bounds for both Locked and Confined modes
                    var screenMin = ImGui.GetItemRectMin();
                    var screenMax = ImGui.GetItemRectMax();
                    Engine.Input.Cursor.SetGamePanelBounds(screenMin.X, screenMin.Y,
                        screenMax.X - screenMin.X, screenMax.Y - screenMin.Y);

                    // Convert to integer screen coords and apply OS-level confine
                    // via InputManager so it can manage suppression and state.
                    int left = (int)Math.Floor(screenMin.X);
                    int top = (int)Math.Floor(screenMin.Y);
                    int right = (int)Math.Ceiling(screenMax.X);
                    int bottom = (int)Math.Ceiling(screenMax.Y);

                    // Only set the confine once when the state transitions to locked/confined
                    if (!_lastKnownLockedState)
                    {
                        if (currentLockMode == Engine.Input.CursorLockMode.Locked)
                        {
                            Console.WriteLine($"[GamePanel] Setting confine rect: ({left},{top}) to ({right},{bottom})");
                            Engine.Input.InputManager.Instance?.SetConfineRect(left, top, right, bottom);
                        }
                        else if (currentLockMode == Engine.Input.CursorLockMode.Confined)
                        {
                            Console.WriteLine($"[GamePanel] Setting confine rect (Confined mode): ({left},{top}) to ({right},{bottom})");
                            // For Confined mode, use ClipCursor to physically confine cursor to GamePanel
                            // Cursor stays visible but can't leave the panel
                            Engine.Input.Cursor.SetConfineToScreenRect(left, top, right, bottom);
                        }
                        _lastKnownLockedState = true;
                    }
                }
                else
                {
                    // Only clear confine when unlocking (state changed)
                    if (_lastKnownLockedState)
                    {
                        Engine.Input.InputManager.Instance?.ClearConfineRect();
                        _lastKnownLockedState = false;
                    }
                    Engine.Input.Cursor.ClearGamePanelBounds();
                    Engine.Input.Cursor.ClearConfineRect();
                }
            }
            else
            {
                ImGui.TextColored(new System.Numerics.Vector4(1, 1, 0, 1), "Aucune caméra active dans la scène");
                ImGui.Text("Ajoutez une caméra (IsMain) pour voir le rendu Play.");
            }
            
            // Render ImGui menu system on top (after End() so it's not clipped by panel)
            ImGui.End();
            
            if (_imguiMenu != null && PlayMode.IsInPlayMode)
            {
                // Use the game image rect as the viewport for the menu if available
                if (hasGameImage)
                {
                    var menuViewportSize = new System.Numerics.Vector2(
                        gameImageMax.X - gameImageMin.X,
                        gameImageMax.Y - gameImageMin.Y
                    );
                    _imguiMenu.Render(gameImageMin, menuViewportSize);
                }
                else
                {
                    // Fallback to full viewport if no camera
                    _imguiMenu.Render();
                }
            }
            
            // Render HUD overlays (RPGHudController, etc.) on top of game viewport
            if (PlayMode.IsInPlayMode && scene != null && hasGameImage)
            {
                var hudViewportSize = new System.Numerics.Vector2(
                    gameImageMax.X - gameImageMin.X,
                    gameImageMax.Y - gameImageMin.Y
                );
                
                int hudCount = 0;
                foreach (var entity in scene.Entities)
                {
                    // Iterate through ALL components and check type by name (more reliable than GetComponent<>)
                    foreach (var comp in entity.GetAllComponents())
                    {
                        var typeName = comp.GetType().FullName;
                        if (typeName == "Editor.Assets.Scripts.RPGHudController")
                        {
                            // Found it! Cast to dynamic to call methods without type reference
                            dynamic hud = comp;
                            hudCount++;
                            hud.SetViewportBounds(gameImageMin, hudViewportSize);
                            
                            // Pass camera matrices for 3D world-to-screen conversion
                            if (camera != null)
                            {
                                float aspect = (float)w / (float)h;
                                var viewMatrix = camera.ViewMatrix;
                                var projMatrix = camera.ProjectionMatrix(aspect);
                                hud.SetCameraMatrices(viewMatrix, projMatrix);
                            }
                            
                            hud.RenderHUDOverlay();
                            break; // Only one HUD per entity
                        }
                    }
                }
                
                if (hudCount == 0)
                {
                    Console.WriteLine($"[GamePanel] WARNING: No RPGHudController found in scene!");
                }
            }
        }
        
        public static void Dispose() {
            // Full dispose: release GL resources owned by the GamePanel renderer
            _gameRenderer?.Dispose();
            _gameRenderer = null;
            _imguiMenu?.Dispose();
            _imguiMenu = null;
            
            // Reset dimension cache to force Resize() on next frame after dispose
            // This prevents black screen when exiting PlayMode with aspect ratio constraints
            _lastRenderWidth = 0;
            _lastRenderHeight = 0;
            
            // Exit maximized mode when disposing
            _isMaximized = false;
        }

        /// <summary>
        /// Reset GamePanel state when exiting Play Mode without disposing GL resources.
        /// This avoids expensive GL resource deletion/recreation when toggling Play Mode frequently.
        /// </summary>
        public static void ResetForExit()
        {
            // Detach scene and stop using the renderer, but keep GL objects alive for reuse
            if (_gameRenderer != null)
            {
                try
                {
                    // Set an empty scene instance to detach runtime scene without disposing GL resources
                    _gameRenderer.SetScene(new Engine.Scene.Scene());
                    _gameRenderer.SetGameMode(false);
                }
                catch { }
            }

            _imguiMenu?.Dispose();
            _imguiMenu = null;

            // Reset dimension cache to force Resize() on next frame after reset
            _lastRenderWidth = 0;
            _lastRenderHeight = 0;

            // Exit maximized mode when resetting
            _isMaximized = false;
        }

        /// <summary>
        /// Maximize or unmaximize the Game Panel (Unity-style)
        /// </summary>
        public static void SetMaximized(bool maximized)
        {
            _isMaximized = maximized;
        }

        /// <summary>
        /// Check if Game Panel is currently maximized
        /// </summary>
        public static bool IsMaximized => _isMaximized;
        
        /// <summary>
        /// Access to Game Panel options (Unity-style settings)
        /// </summary>
        public static GamePanelOptions Options => _options;

        // Debug accessor for other panels to inspect current game color texture
        public static int? CurrentColorTexture => _gameRenderer?.ColorTexture;

        // Reset cursor state when exiting PlayMode
        public static void ResetCursorState() {
            // Cursor state is now managed by gameplay scripts (CursorStateController)
            // GamePanel no longer manages cursor directly
        }

        /// <summary>
        /// Draw the Game Panel Options dropdown menu (Unity-style)
        /// </summary>
        private static void DrawGameOptionsMenu()
        {
            // Icon button for the dropdown (using gear/settings icon)
            if (ImGui.Button("⚙##gameOptions"))
            {
                ImGui.OpenPopup("GameOptionsPopup");
            }
            
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Game Panel Options");
            }
            
            // Options popup
            if (ImGui.BeginPopup("GameOptionsPopup"))
            {
                ImGui.SeparatorText("Play Mode Behavior");
                
                bool focusOnPlay = _options.FocusOnPlay;
                if (ImGui.Checkbox("Focus on Play", ref focusOnPlay))
                    _options.FocusOnPlay = focusOnPlay;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Automatically focus this panel when entering Play Mode");
                
                bool maximizeOnPlay = _options.MaximizeOnPlay;
                if (ImGui.Checkbox("Maximize on Play", ref maximizeOnPlay))
                    _options.MaximizeOnPlay = maximizeOnPlay;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Maximize this panel when entering Play Mode");
                
                ImGui.Separator();
                ImGui.SeparatorText("Display Options");
                
                bool muteAudio = _options.MuteAudio;
                if (ImGui.Checkbox("Mute Audio", ref muteAudio))
                    _options.MuteAudio = muteAudio;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Mute all audio in the Game panel");
                
                bool showStats = _options.ShowStats;
                if (ImGui.Checkbox("Show Stats", ref showStats))
                {
                    _options.ShowStats = showStats;
                    _showGamePerfOverlay = showStats; // Sync with existing perf overlay
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Show performance statistics overlay");
                
                bool showGizmos = _options.ShowGizmos;
                if (ImGui.Checkbox("Show Gizmos", ref showGizmos))
                    _options.ShowGizmos = showGizmos;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Show gizmos in the Game view (normally only in Scene)");
                
                ImGui.Separator();
                ImGui.SeparatorText("Aspect Ratio");
                
                int aspectMode = (int)_options.AspectMode;
                string[] aspectModes = new[] { "Free Aspect", "16:9", "16:10", "4:3", "5:4", "1:1 (Square)", "Custom" };
                ImGui.SetNextItemWidth(150);
                if (ImGui.Combo("##aspectMode", ref aspectMode, aspectModes, aspectModes.Length))
                {
                    _options.AspectMode = (AspectRatioMode)aspectMode;
                }
                
                if (_options.AspectMode == AspectRatioMode.Custom)
                {
                    float customRatio = _options.CustomAspectRatio;
                    ImGui.SetNextItemWidth(100);
                    if (ImGui.DragFloat("Custom Ratio", ref customRatio, 0.01f, 0.1f, 10f, "%.2f"))
                        _options.CustomAspectRatio = customRatio;
                }
                
                ImGui.Separator();
                ImGui.SeparatorText("Quality & Performance");
                
                float resScale = _options.ResolutionScale;
                ImGui.SetNextItemWidth(150);
                if (ImGui.SliderFloat("Resolution Scale", ref resScale, 0.25f, 2.0f, "%.2fx"))
                    _options.ResolutionScale = resScale;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Render scale multiplier (0.5 = half res, 2.0 = supersampling)");
                
                bool vsync = _options.VSync;
                if (ImGui.Checkbox("VSync", ref vsync))
                    _options.VSync = vsync;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Enable vertical synchronization");
                
                int targetFps = _options.TargetFrameRate;
                ImGui.SetNextItemWidth(100);
                if (ImGui.DragInt("Target FPS", ref targetFps, 1f, 0, 300))
                    _options.TargetFrameRate = targetFps;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Target frame rate limit (0 = unlimited)");
                
                ImGui.EndPopup();
            }
        }

        /// <summary>
        /// Get the target aspect ratio based on options (0 = free aspect)
        /// </summary>
        private static float GetTargetAspectRatio()
        {
            return _options.AspectMode switch
            {
                AspectRatioMode.Free => 0f,
                AspectRatioMode.Aspect16_9 => 16f / 9f,
                AspectRatioMode.Aspect16_10 => 16f / 10f,
                AspectRatioMode.Aspect4_3 => 4f / 3f,
                AspectRatioMode.Aspect5_4 => 5f / 4f,
                AspectRatioMode.Aspect1_1 => 1f,
                AspectRatioMode.Custom => _options.CustomAspectRatio,
                _ => 0f
            };
        }
    }
}

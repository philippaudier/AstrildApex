using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.Common;

namespace Engine.Input
{
    /// <summary>
    /// Gestionnaire central du système d'entrées.
    /// - Deltas souris fiables via événements.
    /// - Intègre la "capture ImGui" configurée par l'UI.
    /// - Mode "binding capture" pour assigner Space & co sans conflit.
    /// </summary>
    public sealed class InputManager
    {
        // Suppress the next mouse delta accumulation (used when warping the cursor programmatically)
    private static int _suppressNextMouseDeltaCount;
        public static InputManager? Instance { get; private set; }

        private GameWindow? _window;
        private readonly Dictionary<string, InputActionMap> _actionMaps = new();

        // États courants
        private KeyboardState? _keyboardState;
    private KeyboardState? _prevKeyboardState;
        private MouseState _mouseState;

        // Deltas accumulés frame courante (événements)
        private Vector2 _mouseDeltaAccum;   // cumulé par MouseMove
        private Vector2 _scrollDeltaAccum;  // cumulé par MouseWheel
        private Vector2 _mouseDelta;        // exposé pour la frame
        private Vector2 _scrollDelta;       // exposé pour la frame
    // Fallback: last polled mouse position and delta computed from it
    private Vector2 _lastPolledMousePos;
    private Vector2 _posDeltaFromState;

        // Capture ImGui (à régler depuis ta couche UI)
        private bool _imguiWantsKeyboard;
        private bool _imguiWantsMouse;
        
        // Menu capture (set when menu is visible - blocks game input)
    private bool _menuVisible;

        // Système de contextes d'input prioritaires
        private readonly List<InputContext> _contexts = new();
        private InputCaptureContext? _captureContext;
        
        // PlayMode state tracking for cursor management
        private bool _isPlayModeActive = false;

    // Track previous Escape key state to detect rising edge and avoid spam
    private bool _prevEscapeDown = false;


    // Cursor management state
    private bool _isCursorLocked = false;
    private bool _hasConfineRect = false;
    private int _confineLeft, _confineTop, _confineRight, _confineBottom;
    
    // Time-based suppression to ignore mouse deltas for a short window after locking
    private System.Diagnostics.Stopwatch? _lockSuppressTimer;
    // Unity-like: short suppression (50ms) to only ignore initial warp
    private readonly TimeSpan _lockSuppressDuration = TimeSpan.FromMilliseconds(50);

        public static void Initialize(GameWindow window)
        {
            Instance = new InputManager(window);
        }

        // Expose window for internal engine utilities (e.g., Cursor API)
        internal static GameWindow? Window => Instance?._window;

        private InputManager(GameWindow window)
        {
            _window = window;

            // Init états
            _mouseState = window.MouseState;
            _lastPolledMousePos = new Vector2(_mouseState.X, _mouseState.Y);

            // Abonnements événements
            window.MouseMove        += OnMouseMove;
            window.MouseWheel       += OnMouseWheel;
            window.KeyDown          += OnKeyDown;
            window.KeyUp            += OnKeyUp;
            window.MouseDown        += OnMouseDown;
            window.FocusedChanged   += OnFocusedChanged;

            // Initialiser les contextes d'input par priorité
            InitializeContexts();
        }

        private void InitializeContexts()
        {
            // Créer le contexte de capture (priorité la plus haute)
            _captureContext = new InputCaptureContext();
            _contexts.Add(_captureContext);

            // Créer le contexte éditeur (priorité haute)
            var editorContext = new EditorInputContext();
            _contexts.Add(editorContext);

            // Créer le contexte play mode (priorité basse)
            var playModeContext = new PlayModeInputContext(_actionMaps);
            _contexts.Add(playModeContext);

            // Trier par priorité (plus haute en premier)
            _contexts.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        private void OnFocusedChanged(FocusedChangedEventArgs e)
        {
            // Quand on (re)prend le focus, on neutralise les deltas résiduels
            _mouseDeltaAccum = Vector2.Zero;
            _scrollDeltaAccum = Vector2.Zero;

            // Release any OS-level cursor confinement on focus changes to avoid trapping the cursor
            Cursor.ClearSystemConfine();
        }

        private void OnMouseMove(MouseMoveEventArgs e)
        {
            // Ignore synthetic deltas after programmatic cursor warps (count-based)
            if (_suppressNextMouseDeltaCount > 0)
            {
                _suppressNextMouseDeltaCount--;
                return;
            }
            // Also ignore deltas during time-based suppression window after LockCursor
            if (_lockSuppressTimer != null && _lockSuppressTimer.IsRunning && _lockSuppressTimer.Elapsed < _lockSuppressDuration)
            {
                return;
            }
            // Accumule les deltas (OpenTK fournit DeltaX/DeltaY)
            _mouseDeltaAccum += new Vector2(e.DeltaX, e.DeltaY);
        }

        private void OnMouseWheel(MouseWheelEventArgs e)
        {
            _scrollDeltaAccum += new Vector2(e.OffsetX, e.OffsetY);
        }

        private void OnKeyDown(KeyboardKeyEventArgs e)
        {
            // Passer l'input aux contextes par ordre de priorité
            foreach (var context in _contexts)
            {
                if (context.CanProcessInput() && context.HandleKeyDown(e.Key, e.IsRepeat))
                {
                    return; // Input consommé, arrêter la propagation
                }
            }

            // Si aucun contexte n'a consommé, vérifier ImGui
            if (_imguiWantsKeyboard) return;
        }

        private void OnKeyUp(KeyboardKeyEventArgs e)
        {
            // Key up handled by contexts if needed
        }

        private void OnMouseDown(MouseButtonEventArgs e)
        {
            // Passer l'input aux contextes par ordre de priorité
            foreach (var context in _contexts)
            {
                if (context.CanProcessInput() && context.HandleMouseDown(e.Button))
                {
                    return; // Input consommé, arrêter la propagation
                }
            }

            // Si aucun contexte n'a consommé, vérifier ImGui
            if (_imguiWantsMouse) return;
        }

        /// <summary>
        /// À appeler chaque frame, avant Update() des systèmes de gameplay.
        /// </summary>
        public void Update()
        {
            if (_window == null) return;

            // États bruts
            _keyboardState = _window.KeyboardState;
            // Compute polled position delta (fallback) before overwriting _mouseState
            var prevPolled = _lastPolledMousePos;
            _mouseState    = _window.MouseState;
            var curPolled = new Vector2(_mouseState.X, _mouseState.Y);
            _posDeltaFromState = curPolled - prevPolled;
            _lastPolledMousePos = curPolled;

            // Publie les deltas de la frame puis reset les accumulateurs
            _mouseDelta  = _mouseDeltaAccum;
            _scrollDelta = _scrollDeltaAccum;
            _mouseDeltaAccum  = Vector2.Zero;
            _scrollDeltaAccum = Vector2.Zero;

            // Fallback to polled delta if no event-based delta was accumulated this frame.
            if (_mouseDelta.LengthSquared() < 1e-6f)
            {
                // If we have suppressed synthetic deltas outstanding, don't accept the polled
                // fallback position (it might reflect a warp). Wait until suppression clears.
                if (_suppressNextMouseDeltaCount > 0)
                {
                    _mouseDelta = Vector2.Zero;
                    if (_isCursorLocked && Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[InputManager] Delta SUPPRESSED (count={_suppressNextMouseDeltaCount})");
                }
                // If time-based suppression active, also ignore the polled fallback
                else if (_lockSuppressTimer != null && _lockSuppressTimer.IsRunning && _lockSuppressTimer.Elapsed < _lockSuppressDuration)
                {
                    _mouseDelta = Vector2.Zero;
                    if (_isCursorLocked && Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[InputManager] Delta SUPPRESSED (timer={_lockSuppressTimer.Elapsed.TotalMilliseconds:F0}ms)");
                }
                else
                {
                    _mouseDelta = _posDeltaFromState;
                    if (_isCursorLocked && _mouseDelta.LengthSquared() > 0.1f && Engine.Utils.DebugLogger.EnableVerbose)
                        Engine.Utils.DebugLogger.Log($"[InputManager] Delta OK: ({_mouseDelta.X:F2}, {_mouseDelta.Y:F2})");
                }
            }

            // Additional guard when locked: handle implausibly large polled fallback deltas.
            if (_isCursorLocked)
            {
                // When locked we normally get small per-frame deltas from relative mode.
                // Two strategies:
                //  - If the polled delta is extremely large (likely synthetic due to OS wrapping), drop it entirely.
                //  - If it's moderately large, clamp it to avoid a camera snap.
                const float dropLockedDeltaThreshold = 512f; // pixels/frame -> treat as synthetic and drop
                const float maxLockedDelta = 256f; // pixels/frame sensible max for locked mode (increased from 64 to allow fast mouse movements)

                if (MathF.Abs(_mouseDelta.X) > dropLockedDeltaThreshold || MathF.Abs(_mouseDelta.Y) > dropLockedDeltaThreshold)
                {
                    // Drop the delta completely; don't drive camera with obviously bogus values
                    _mouseDelta = Vector2.Zero;
                    // Keep suppression unchanged; dropping is safest and avoids false recovery pulses
                }
                else if (MathF.Abs(_mouseDelta.X) > maxLockedDelta || MathF.Abs(_mouseDelta.Y) > maxLockedDelta)
                {
                    _mouseDelta.X = (float) Math.Clamp(_mouseDelta.X, -maxLockedDelta, maxLockedDelta);
                    _mouseDelta.Y = (float) Math.Clamp(_mouseDelta.Y, -maxLockedDelta, maxLockedDelta);
                    // If suppression is active, also decrement it to recover gracefully
                    if (_suppressNextMouseDeltaCount > 0)
                    {
                        _suppressNextMouseDeltaCount = Math.Max(0, _suppressNextMouseDeltaCount - 1);
                    }
                }
            }
            
            // Cursor modes
            if (_isCursorLocked || Cursor.lockState == CursorLockMode.Locked)
            {
                // In Locked mode, OpenTK's CursorState.Grabbed keeps cursor locked automatically
                // NO manual re-centering needed - Grabbed mode provides relative deltas without moving cursor
                // Manual centering causes repeated delta suppression which blocks camera rotation
                // OpenTK handles this internally on all platforms
            }
            else if (Cursor.lockState == CursorLockMode.Confined)
            {
                // Unity's Confined mode: cursor visible, confined to bounds, stops at edges
                // NO virtual deltas - cursor just stops (like Unity)
                // This mode is for RTS/strategy games, NOT for FPS cameras
                Cursor.ClampToClient();
            }

            // Escape should release lock and show cursor (editor-like behavior)
            // Use edge detection so we only act once when the key is pressed, avoiding per-frame spam.
            bool escapeDown = _keyboardState?.IsKeyDown(Keys.Escape) ?? false;
            if (escapeDown && !_prevEscapeDown)
            {
                UnlockCursor();
            }
            _prevEscapeDown = escapeDown;

            // Met à jour tous les contextes d'input
            foreach (var context in _contexts)
            {
                if (context.IsEnabled)
                {
                    context.Update(0.016f); // TODO: Passer le vrai deltaTime
                }
            }

            // Store previous keyboard state for edge detection APIs
            _prevKeyboardState = _keyboardState;
        }
        
        /// <summary>
        /// Ignore the next MouseMove delta emitted by OpenTK (e.g., after warping the cursor).
        /// </summary>
        public static void IgnoreNextMouseDelta()
        {
            _suppressNextMouseDeltaCount = Math.Max(_suppressNextMouseDeltaCount, 1);
        }

        /// <summary>
        /// Ignore the next N MouseMove deltas emitted by OpenTK (e.g., after warping the cursor).
        /// </summary>
        public static void IgnoreNextMouseDeltaCount(int n)
        {
            if (n <= 0) return;
            _suppressNextMouseDeltaCount = Math.Max(_suppressNextMouseDeltaCount, n);
        }

        // -------- Cursor management API --------
        public void LockCursor()
        {
            if (_window == null) return;
            // Idempotent: if already locked, silently no-op to avoid re-triggering suppression.
            if (_isCursorLocked)
            {
                // Already locked: silently no-op (caller should avoid calling every frame)
                Console.WriteLine("[InputManager] LockCursor() - ALREADY LOCKED, ignoring");
                return;
            }
            Console.WriteLine("[InputManager] LockCursor() - LOCKING NOW");
            _isCursorLocked = true;
            try
            {
                // Set CursorState.Grabbed - this locks cursor and provides relative deltas
                // OpenTK handles cursor positioning automatically, no manual centering needed
                _window.CursorState = OpenTK.Windowing.Common.CursorState.Grabbed;
                Cursor.visible = false; // Hide cursor for FPS mode
                Console.WriteLine("[InputManager] CursorState.Grabbed applied - cursor locked and hidden");
            }
            catch { }
            // Reset polled position and clear any accumulated deltas so no single-frame
            // polled fallback can cause a large rotation spike when entering locked mode.
            try
            {
                var ms = _window.MouseState;
                _mouseState = ms;
                _lastPolledMousePos = new System.Numerics.Vector2(ms.X, ms.Y);
                _mouseDeltaAccum = Vector2.Zero;
                _mouseDelta = Vector2.Zero;
            }
            catch { }
            // Apply confine rect at OS level if available
            if (_hasConfineRect)
            {
                Cursor.SetConfineToScreenRect(_confineLeft, _confineTop, _confineRight, _confineBottom);
            }
            // Unity-like behavior: only suppress initial warp deltas (1-2 frames)
            // Too much suppression blocks legitimate camera rotation input
            IgnoreNextMouseDeltaCount(2);
            // Start time-based suppression window (now 50ms, set in field initializer)
            if (_lockSuppressTimer == null) _lockSuppressTimer = new System.Diagnostics.Stopwatch();
            _lockSuppressTimer.Restart();
            // Also zero any currently accumulated delta to avoid one-frame spikes
            _mouseDelta = Vector2.Zero;
        }

        public void UnlockCursor()
        {
            if (_window == null) return;
            // Idempotent: if already unlocked, no-op
            if (!_isCursorLocked)
            {
                // Already unlocked: silent no-op to avoid noisy per-frame logs when UI polls state
                Console.WriteLine("[InputManager] UnlockCursor() - ALREADY UNLOCKED, ignoring");
                return;
            }
            Console.WriteLine("[InputManager] UnlockCursor() - UNLOCKING NOW");
            _isCursorLocked = false;
            try
            {
                // CRITICAL: Must set CursorState.Normal FIRST to exit Grabbed mode
                // Then set visible=true will work correctly
                _window.CursorState = OpenTK.Windowing.Common.CursorState.Normal;
                Cursor.visible = true;
                Console.WriteLine("[InputManager] Cursor unlocked: CursorState.Normal + visible=true");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[InputManager] UnlockCursor failed: {ex.Message}");
            }
            Cursor.ClearSystemConfine();
            // Also clear any pending suppression so we don't accidentally drop legitimate deltas
            // after unlocking.
            _suppressNextMouseDeltaCount = 0;
        }

        public bool IsCursorLocked => _isCursorLocked;

        public void SetConfineRect(int left, int top, int right, int bottom)
        {
            Console.WriteLine($"[InputManager] SetConfineRect called: ({left},{top}) to ({right},{bottom}), locked={_isCursorLocked}");
            _hasConfineRect = true;
            _confineLeft = left; _confineTop = top; _confineRight = right; _confineBottom = bottom;
            if (_isCursorLocked)
            {
                Console.WriteLine($"[InputManager] Applying ClipCursor to confine rect");
                Cursor.SetConfineToScreenRect(left, top, right, bottom);
            }
        }

        public void ClearConfineRect()
        {
            // Only clear if a confine rect was set; otherwise silently no-op to avoid log spam
            if (!_hasConfineRect)
            {
                return;
            }
            _hasConfineRect = false;
            Cursor.ClearSystemConfine();
            // Also clear any pending suppression so we don't accidentally drop legitimate deltas
            _suppressNextMouseDeltaCount = 0;
            if (_lockSuppressTimer != null) _lockSuppressTimer.Reset();
        }

        // -------- API ImGui / intégration UI --------

        /// <summary>
        /// À appeler depuis ta couche ImGui, typiquement chaque frame :
        /// SetImGuiCapture(io.WantCaptureKeyboard, io.WantCaptureMouse);
        /// </summary>
        public void SetImGuiCapture(bool wantsKeyboard, bool wantsMouse)
        {
            _imguiWantsKeyboard = wantsKeyboard;
            _imguiWantsMouse = wantsMouse;
        }
        
        /// <summary>
        /// Sets whether a menu is visible and should block game input.
        /// Call this when the menu opens/closes (e.g., ImGuiMenu).
        /// </summary>
        public void SetMenuVisible(bool visible)
        {
            _menuVisible = visible;
        }

        /// <summary>
        /// Gets whether a menu is currently visible and blocking game input.
        /// </summary>
        public bool IsMenuVisible => _menuVisible;

        /// <summary>
        /// Démarre la capture d'input pour réassignation de touches
        /// </summary>
        public void BeginBindingCapture(Action<Keys> onKeyCaptured, Action<MouseButton> onMouseCaptured, Action onCaptureCancelled)
        {
            if (_captureContext != null)
            {
                _captureContext.BeginCapture(onKeyCaptured, onMouseCaptured, onCaptureCancelled);
            }
        }

        /// <summary>
        /// Overload: Begin binding capture using KeyCode (Unity-like) callback for convenience
        /// </summary>
        public void BeginBindingCapture(Action<KeyCode> onKeyCaptured, Action<MouseButton> onMouseCaptured, Action onCaptureCancelled)
        {
            if (_captureContext != null)
            {
                // Wrap KeyCode callback into existing Keys callback
                void Wrapped(Keys k) => onKeyCaptured?.Invoke(k.FromOpenTK());
                _captureContext.BeginCapture(Wrapped, onMouseCaptured, onCaptureCancelled);
            }
        }

        /// <summary>
        /// Arrête la capture d'input
        /// </summary>
        public void EndBindingCapture()
        {
            _captureContext?.EndCapture();
        }

        /// <summary>
        /// Vérifie si une capture d'input est en cours
        /// </summary>
        public bool IsCapturingBinding => _captureContext?.IsEnabled ?? false;

        /// <summary>
        /// API legacy - deprecated, utiliser le nouveau système de contextes
        /// </summary>
        public bool TryConsumeLastKeyPressed(out Keys key)
        {
            key = Keys.Unknown;
            return false; // Deprecated
        }

        /// <summary>
        /// API legacy - deprecated, utiliser le nouveau système de contextes
        /// </summary>
        public bool TryConsumeLastMouseButton(out MouseButton button)
        {
            button = MouseButton.Left;
            return false; // Deprecated
        }

        // -------- Action maps --------

        public InputActionMap CreateActionMap(string name)
        {
            if (_actionMaps.ContainsKey(name))
                throw new InvalidOperationException($"ActionMap '{name}' already exists");

            var map = new InputActionMap(name);
            _actionMaps[name] = map;
            return map;
        }

        public InputActionMap? FindActionMap(string name)
            => _actionMaps.TryGetValue(name, out var map) ? map : null;

        public InputActionMap this[string name] => _actionMaps[name];
        public IEnumerable<InputActionMap> ActionMaps => _actionMaps.Values;

        // -------- Accès bruts (respectent WantCapture sauf en binding capture) --------

        internal bool IsKeyDown(Keys key)
        {
            // Block all input when menu is visible
            if (_menuVisible) return false;
            
            // Respect ImGui capture only while the Editor context is enabled (i.e., not in Play Mode)
            bool editorActive = _contexts.OfType<EditorInputContext>().Any(c => c.IsEnabled);
            if (!IsCapturingBinding && editorActive && _imguiWantsKeyboard) return false;
            return _keyboardState?.IsKeyDown(key) ?? false;
        }

        internal bool IsMouseButtonDown(MouseButton button)
        {
            // Block all input when menu is visible
            if (_menuVisible) return false;
            
            bool editorActive = _contexts.OfType<EditorInputContext>().Any(c => c.IsEnabled);
            if (!IsCapturingBinding && editorActive && _imguiWantsMouse) return false;
            return _mouseState.IsButtonDown(button);
        }

        internal float GetMouseAxisDelta(MouseAxis axis)
        {
            // Block all input when menu is visible
            if (_menuVisible) return 0f;
            
            bool editorActive = _contexts.OfType<EditorInputContext>().Any(c => c.IsEnabled);
            if (!IsCapturingBinding && editorActive && _imguiWantsMouse) return 0f;

            return axis switch
            {
                MouseAxis.X       => _mouseDelta.X,
                MouseAxis.Y       => _mouseDelta.Y,
                MouseAxis.ScrollX => _scrollDelta.X,
                MouseAxis.ScrollY => _scrollDelta.Y,
                _ => 0f
            };
        }

        // -------- API Play Mode / Context Management --------

        /// <summary>
        /// Active/désactive les contextes selon le mode Play Mode
        /// </summary>
        public void SetPlayModeActive(bool playing)
        {
            _isPlayModeActive = playing;
            
            // Toggle input contexts for Editor vs Play; keep silent in normal operation
            
            // Trouver et configurer les contextes
            var editorContext = _contexts.OfType<EditorInputContext>().FirstOrDefault();
            var playModeContext = _contexts.OfType<PlayModeInputContext>().FirstOrDefault();
            
            if (editorContext != null) 
            {
                editorContext.IsEnabled = !playing;
            }
            
            if (playModeContext != null) 
            {
                playModeContext.IsEnabled = playing;
            }
            
            // Si on entre en mode Play, recharger les bindings persistés
            if (playing)
            {
                ReloadPersistedBindings?.Invoke();
                
                // Activer seulement les ActionMaps appropriées pour le gameplay
                EnableActionMap("Player");
                DisableActionMap("Vehicle"); // Par défaut, pas en véhicule
                DisableActionMap("Menu"); // Par défaut, pas en menu
            }
            else
            {
                // En mode éditeur, désactiver toutes les ActionMaps de gameplay
                DisableActionMap("Player");
                DisableActionMap("Vehicle");
                DisableActionMap("Menu");
            }
        }

        /// <summary>
        /// Active une ActionMap spécifique
        /// </summary>
        public void EnableActionMap(string mapName)
        {
            if (_actionMaps.TryGetValue(mapName, out var map))
            {
                map.Enable();
            }
        }

        /// <summary>
        /// Désactive une ActionMap spécifique
        /// </summary>
        public void DisableActionMap(string mapName)
        {
            if (_actionMaps.TryGetValue(mapName, out var map))
            {
                map.Disable();
            }
        }

        /// <summary>
        /// Active une ActionMap et désactive toutes les autres (mode exclusif)
        /// </summary>
        public void SetActiveActionMap(string mapName)
        {
            foreach (var map in _actionMaps.Values)
            {
                map.Disable();
            }
            EnableActionMap(mapName);
        }

        /// <summary>
        /// Retourne les noms de toutes les ActionMaps disponibles
        /// </summary>
        public string[] GetAvailableActionMaps()
        {
            return _actionMaps.Keys.ToArray();
        }

        /// <summary>
        /// Retourne les noms des ActionMaps actuellement activées
        /// </summary>
        public string[] GetEnabledActionMaps()
        {
            return _actionMaps.Where(kvp => kvp.Value.IsEnabled).Select(kvp => kvp.Key).ToArray();
        }

        /// <summary>
        /// Action appelée pour recharger les bindings persistés
        /// Sera configurée par le code Editor au démarrage
        /// </summary>
        public static Action? ReloadPersistedBindings { get; set; }

        // -------- Conveniences publics --------
    public bool GetKey(Keys key)                     => IsKeyDown(key);
    public bool GetMouseButton(MouseButton button)   => IsMouseButtonDown(button);
        
    // Unity-like KeyCode overloads
    public bool GetKey(KeyCode key)                  => IsKeyDown(key.ToOpenTK());
    public bool GetKeyDown(Keys key)                 => _keyboardState != null && _prevKeyboardState != null && _keyboardState.IsKeyDown(key) && !_prevKeyboardState.IsKeyDown(key);
    public bool GetKeyUp(Keys key)                   => _keyboardState != null && _prevKeyboardState != null && !_keyboardState.IsKeyDown(key) && _prevKeyboardState.IsKeyDown(key);

    public bool GetKeyDown(KeyCode key)              => GetKeyDown(key.ToOpenTK());
    public bool GetKeyUp(KeyCode key)                => GetKeyUp(key.ToOpenTK());
        public Vector2 MousePosition => new Vector2(_mouseState.X, _mouseState.Y);
        public Vector2 MouseDelta    => _menuVisible ? Vector2.Zero : _mouseDelta;
        public Vector2 ScrollDelta   => _menuVisible ? Vector2.Zero : _scrollDelta;
    // Diagnostic polled info
    public Vector2 LastPolledPosition => _lastPolledMousePos;
    public Vector2 PolledDelta => _posDeltaFromState;

        // Diagnostic accessors (used by editor debug overlays)
        public int GetSuppressionCount() => _suppressNextMouseDeltaCount;

        public struct ConfineRect
        {
            public int left, top, right, bottom;
        }

        public ConfineRect GetConfineRect()
        {
            return new ConfineRect
            {
                left = _confineLeft,
                top = _confineTop,
                right = _confineRight,
                bottom = _confineBottom
            };
        }

        /// <summary>
        /// Crée des bindings par défaut (ZQSD si AZERTY détecté).
        /// </summary>
        public void SetupDefaultPlayerControls()
        {
            var playerMap = CreateActionMap("Player");

            // Détection ultra simple d'un clavier de type AZERTY (W non au-dessus de S)
            // NB: on ne peut pas lire directement la layout OS ici; on propose un fallback.
            // Default to false (WASD) because most users have QWERTY layout.
            bool useZqsd = false; // set true for AZERTY (ZQSD)

            playerMap.ConfigureAction("MoveForward",
                InputBinding.FromKey(useZqsd ? Keys.Z : Keys.W));
            playerMap.ConfigureAction("MoveBackward",
                InputBinding.FromKey(useZqsd ? Keys.S : Keys.S));
            playerMap.ConfigureAction("MoveLeft",
                InputBinding.FromKey(useZqsd ? Keys.Q : Keys.A));
            playerMap.ConfigureAction("MoveRight",
                InputBinding.FromKey(useZqsd ? Keys.D : Keys.D));

            playerMap.ConfigureAction("Run",
                InputBinding.FromKey(Keys.LeftShift));
            playerMap.ConfigureAction("Jump",
                InputBinding.FromKey(Keys.Space));

            playerMap.ConfigureAction("Look",
                InputBinding.FromMouseAxis(MouseAxis.X),
                InputBinding.FromMouseAxis(MouseAxis.Y));

            playerMap.ConfigureAction("Fire",
                InputBinding.FromMouseButton(MouseButton.Left));
            playerMap.ConfigureAction("AltFire",
                InputBinding.FromMouseButton(MouseButton.Right));

            playerMap.Enable();
        }
    }
}

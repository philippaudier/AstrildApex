using System;
using OpenTK.Windowing.Common;
using System.Runtime.InteropServices;
using OpenTK.Windowing.Desktop;
using System.Numerics;

namespace Engine.Input
{
    /// <summary>
    /// Unity-like Cursor API: lock mode, visibility and confinement.
    /// </summary>
    public static class Cursor
    {
    // Optional confinement rectangle in client coordinates
    private static bool _hasConfineRect;
    private static float _confX, _confY, _confW, _confH;
    
    // Track current lock mode explicitly
    private static CursorLockMode _currentLockMode = CursorLockMode.None;
    
    // Game panel bounds for virtual delta generation
    private static bool _hasGamePanelBounds;
    private static float _gamePanelX, _gamePanelY, _gamePanelW, _gamePanelH;
    
    /// <summary>
    /// Gets whether the cursor is currently locked (FPS mode).
    /// </summary>
    public static bool isLocked => _currentLockMode == CursorLockMode.Locked;
    
    /// <summary>
    /// Gets whether the cursor is currently confined (RTS mode).
    /// </summary>
    public static bool isConfined => _currentLockMode == CursorLockMode.Confined;
    
    /// <summary>
    /// Gets whether the cursor is in normal mode (free).
    /// </summary>
    public static bool isFree => _currentLockMode == CursorLockMode.None;

    public static bool visible
        {
            get
            {
                var win = InputManager.Window;
                if (win == null) return true;
                // Consider visible only in Normal state
                return win.CursorState == CursorState.Normal;
            }
            set
            {
                var win = InputManager.Window;
                if (win == null) return;
                
                try
                {
                    // SIMPLIFIED: Always use CursorState to control visibility
                    // If we want visible, we MUST use CursorState.Normal (ShowCursor is unreliable)
                    // If we want hidden, use CursorState.Hidden
                    // For Locked mode, visibility will be controlled by LockCursor/UnlockCursor
                    
                    if (value)
                    {
                        // Force visible by setting CursorState.Normal
                        win.CursorState = CursorState.Normal;
                        
                        // CRITICAL FIX: Force ShowCursor display counter positive
                        // After Grabbed mode, the ShowCursor counter might be negative
                        // Force it positive to ensure cursor is actually visible on screen
                        int count = 0;
                        while (ShowCursor(true) < 0 && count++ < 100) { }
                        if (count > 0)
                        {
                            Console.WriteLine($"[Cursor] visible=true -> CursorState.Normal + ShowCursor fixed ({count} calls)");
                        }
                        else
                        {
                            Console.WriteLine("[Cursor] visible=true -> CursorState.Normal");
                        }
                    }
                    else
                    {
                        // In Grabbed mode, cursor should stay hidden (Grabbed already hides it)
                        // Just ignore the request to hide - cursor is already hidden by Grabbed state
                        Console.WriteLine("[Cursor] visible=false -> ignored (Grabbed mode keeps cursor hidden)");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Cursor] Failed to set visible={value}: {ex.Message}");
                }
            }
        }

    public static CursorLockMode lockState
        {
            get
            {
                return _currentLockMode;
            }
            set
            {
                var win = InputManager.Window;
                if (win == null) return;

                _currentLockMode = value;
                
                switch (value)
                {
                    case CursorLockMode.None:
                        // Unlock: restore to Normal by default
                        if (win.CursorState == CursorState.Grabbed)
                            win.CursorState = CursorState.Normal;
                        // Also release any OS-level confinement
                        ClearSystemConfine();
                        ClearConfineRect();
                        break;
                    case CursorLockMode.Locked:
                        // Use Grabbed for relative input; center once to stabilize deltas
                        win.CursorState = CursorState.Grabbed;
                        CenterToWindow();
                        break;
                    case CursorLockMode.Confined:
                        // Emulate confinement: keep visible cursor but clamp inside client rect
                        // OpenTK doesn't expose Confined directly; we'll synthesize by setting Normal
                        // and correcting position in InputManager.Update() via ClampToClient().
                        win.CursorState = CursorState.Normal;
                        break;
                }
            }
        }

        public static void CenterToWindow()
        {
            var win = InputManager.Window;
            if (win == null) return;
            var centerX = win.ClientSize.X / 2f;
            var centerY = win.ClientSize.Y / 2f;
            win.MousePosition = new OpenTK.Mathematics.Vector2(centerX, centerY);
            // Prevent the synthetic MouseMove delta that may be emitted after programmatic
            // cursor warps. The InputManager will ignore the next MouseMove delta so
            // the rotation state is not corrupted by this initial centering.
            try { InputManager.IgnoreNextMouseDelta(); } catch { }
        }
        
        /// <summary>
        /// Center cursor to the GamePanel (not the entire window).
        /// Used in Locked mode to keep cursor in the play area.
        /// </summary>
        public static void CenterToGamePanel()
        {
            if (!_hasGamePanelBounds)
            {
                // Fallback to window center if no GamePanel bounds set
                CenterToWindow();
                return;
            }
            
            float centerX = _gamePanelX + _gamePanelW / 2f;
            float centerY = _gamePanelY + _gamePanelH / 2f;
            
            SetMousePositionScreen(centerX, centerY);
            // Suppress only 1 delta for the warp itself (not 3)
            try { InputManager.IgnoreNextMouseDeltaCount(1); } catch { }
        }

        public static void SetMousePositionClient(float x, float y)
        {
            var win = InputManager.Window;
            if (win == null) return;
            win.MousePosition = new OpenTK.Mathematics.Vector2(x, y);
        }

        public static void SetConfineToClientRect(float x, float y, float width, float height)
        {
            _hasConfineRect = true;
            _confX = x; _confY = y; _confW = width; _confH = height;
        }

        public static void ClearConfineRect()
        {
            _hasConfineRect = false;
        }

        internal static void ClampToClient()
        {
            var win = InputManager.Window;
            if (win == null) return;
            var pos = win.MousePosition;
            float minX, minY, maxX, maxY;
            if (_hasConfineRect)
            {
                minX = _confX; minY = _confY; maxX = _confX + _confW - 1f; maxY = _confY + _confH - 1f;
            }
            else
            {
                minX = 0f; minY = 0f; maxX = win.ClientSize.X - 1f; maxY = win.ClientSize.Y - 1f;
            }
            float x = Clamp(pos.X, minX, maxX);
            float y = Clamp(pos.Y, minY, maxY);
            if (x != pos.X || y != pos.Y)
            {
                win.MousePosition = new OpenTK.Mathematics.Vector2(x, y);
            }
        }

        private static float Clamp(float v, float min, float max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        // -------- Windows OS-level confinement (accurate confine to any screen rect) --------

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ClipCursor(ref RECT rect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ClipCursor(IntPtr rectNull);

    [DllImport("user32.dll")]
    private static extern int ShowCursor(bool bShow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetCursorPos(int X, int Y);

        public static void SetConfineToScreenRect(int left, int top, int right, int bottom)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // Convert ImGui window-relative coordinates to absolute screen coordinates
                    var win = InputManager.Window;
                    if (win != null)
                    {
                        // ImGui coordinates are relative to the window client area
                        // Add window position to get absolute screen coordinates
                        var windowPos = win.ClientLocation;
                        int screenLeft = left + windowPos.X;
                        int screenTop = top + windowPos.Y;
                        int screenRight = right + windowPos.X;
                        int screenBottom = bottom + windowPos.Y;
                        
                        RECT r = new RECT { Left = screenLeft, Top = screenTop, Right = screenRight, Bottom = screenBottom };
                        bool ok = ClipCursor(ref r);
                        if (ok)
                        {
                            Console.WriteLine($"[Cursor] ClipCursor SUCCESS: window-relative ({left},{top})-({right},{bottom}) → screen ({screenLeft},{screenTop})-({screenRight},{screenBottom})");
                        }
                        else
                        {
                            int error = Marshal.GetLastWin32Error();
                            Console.WriteLine($"[Cursor] ClipCursor FAILED: error code {error}");
                        }
                    }
                }
                else
                {
                    // Fallback to client-rect emulation if not on Windows
                    SetConfineToClientRect(left, top, right - left, bottom - top);
                }
            }
            catch
            {
                // Ignore failures, fall back to client clamp
                SetConfineToClientRect(left, top, right - left, bottom - top);
            }
        }

        public static void ClearSystemConfine()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    ClipCursor(IntPtr.Zero);
                }
            }
            catch
            {
                // ignore
            }
        }
        
        public static void SetMousePositionScreen(float x, float y)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    SetCursorPos((int)MathF.Round(x), (int)MathF.Round(y));
                }
                else
                {
                    // Best effort: assume client coords ~ screen coords on other platforms
                    SetMousePositionClient(x, y);
                }
            }
            catch
            {
                // ignore
            }
        }
        
        public static void SetGamePanelBounds(float x, float y, float width, float height)
        {
            _hasGamePanelBounds = true;
            _gamePanelX = x; _gamePanelY = y; _gamePanelW = width; _gamePanelH = height;
        }
        
        public static void ClearGamePanelBounds()
        {
            _hasGamePanelBounds = false;
        }
        
        internal static bool IsAtGamePanelEdge(Vector2 mousePos, out Vector2 virtualDelta)
        {
            virtualDelta = Vector2.Zero;
            if (!_hasGamePanelBounds) return false;
            
            bool atLeftEdge = mousePos.X <= _gamePanelX;
            bool atRightEdge = mousePos.X >= _gamePanelX + _gamePanelW - 1;
            bool atTopEdge = mousePos.Y <= _gamePanelY;
            bool atBottomEdge = mousePos.Y >= _gamePanelY + _gamePanelH - 1;
            
            if (atLeftEdge || atRightEdge || atTopEdge || atBottomEdge)
            {
                // In Unity's Confined mode, cursor just stops at edges (no virtual deltas)
                // This method should only be used for debugging, not for gameplay
                virtualDelta = Vector2.Zero;
                return false;
            }
            
            return false;
        }
    }

    public enum CursorLockMode
    {
        None,
        Locked,
        Confined
    }
}

using System;
using System.Numerics;
using ImGuiNET;
using Engine.Input;

namespace Engine.UI.ImGuiMenu
{
    /// <summary>
    /// Système de menu RPG ImGui.NET natif avec style custom
    /// Performances optimales (60+ FPS) avec rendering natif
    /// </summary>
    public class ImGuiMenuSystem : IDisposable
    {
        // État du menu
        private bool _isVisible = false;
        private MenuTab _currentTab = MenuTab.Inventory;
        
        // Animation
        private float _animationProgress = 0f;
        private const float AnimationSpeed = 8f;
        
        // Cursor state restoration
        private bool _previousCursorVisible = true;
        private Input.CursorLockMode _previousLockMode = Input.CursorLockMode.None;
        
        // Composants des tabs
        private readonly InventoryPanel _inventoryPanel;
        private readonly CharacterPanel _characterPanel;
        private readonly MapPanel _mapPanel;
        private readonly SettingsPanel _settingsPanel;
        
        public bool IsVisible => _isVisible;
        
        public enum MenuTab
        {
            Inventory,
            Character,
            Map,
            Settings
        }
        
        public ImGuiMenuSystem()
        {
            _inventoryPanel = new InventoryPanel();
            _characterPanel = new CharacterPanel();
            _mapPanel = new MapPanel();
            _settingsPanel = new SettingsPanel();
            
            Console.WriteLine("[ImGuiMenuSystem] ✓ Initialized");
        }
        
        /// <summary>
        /// Toggle menu visibility
        /// </summary>
        public void Toggle()
        {
            _isVisible = !_isVisible;
            Console.WriteLine($"[ImGuiMenuSystem] Menu {(_isVisible ? "SHOWN" : "HIDDEN")}");
            
            if (_isVisible)
            {
                OnMenuOpened();
            }
            else
            {
                OnMenuClosed();
            }
        }
        
        public void Show() => SetVisible(true);
        public void Hide() => SetVisible(false);
        
        public void SetVisible(bool visible)
        {
            if (_isVisible != visible)
            {
                _isVisible = visible;
                Console.WriteLine($"[ImGuiMenuSystem] Menu {(_isVisible ? "SHOWN" : "HIDDEN")}");
                
                if (_isVisible)
                {
                    OnMenuOpened();
                }
                else
                {
                    OnMenuClosed();
                }
            }
        }
        
        private void OnMenuOpened()
        {
            Console.WriteLine("[ImGuiMenuSystem] OnMenuOpened() called");
            
            // Save current cursor state
            _previousCursorVisible = Input.Cursor.visible;
            _previousLockMode = Input.Cursor.lockState;
            Console.WriteLine($"[ImGuiMenuSystem] Previous cursor state: visible={_previousCursorVisible}, lock={_previousLockMode}");
            
            // Show cursor and unlock
            Console.WriteLine("[ImGuiMenuSystem] Setting cursor: lockState=None");
            Input.Cursor.lockState = Input.CursorLockMode.None;
            
            Console.WriteLine("[ImGuiMenuSystem] Setting cursor: visible=true");
            Input.Cursor.visible = true;
            
            // Verify
            Console.WriteLine($"[ImGuiMenuSystem] After setting - visible={Input.Cursor.visible}, lock={Input.Cursor.lockState}");
            
            // Notify InputManager to block game input
            if (InputManager.Instance != null)
            {
                InputManager.Instance.SetMenuVisible(true);
            }
            
            Console.WriteLine($"[ImGuiMenuSystem] ✓ Menu opened, cursor should be visible now");
        }
        
        private void OnMenuClosed()
        {
            Console.WriteLine("[ImGuiMenuSystem] OnMenuClosed() called");
            Console.WriteLine($"[ImGuiMenuSystem] Restoring cursor: visible={_previousCursorVisible}, lock={_previousLockMode}");
            
            // Restore previous cursor state FIRST
            Input.Cursor.lockState = _previousLockMode;
            Input.Cursor.visible = _previousCursorVisible;
            
            // Force hide cursor if it was locked (FPS mode)
            if (_previousLockMode == Input.CursorLockMode.Locked)
            {
                Console.WriteLine("[ImGuiMenuSystem] Forcing cursor hidden for Locked mode");
                // Input.Cursor.visible = false;
            }
            
            // THEN notify InputManager to unblock game input
            // This ensures cursor state is set before game resumes control
            if (InputManager.Instance != null)
            {
                InputManager.Instance.SetMenuVisible(false);
            }
            
            // Verify final state
            Console.WriteLine($"[ImGuiMenuSystem] After restore - visible={Input.Cursor.visible}, lock={Input.Cursor.lockState}");
            Console.WriteLine($"[ImGuiMenuSystem] ✓ Menu closed, cursor restored");
        }
        
        public void SwitchTab(MenuTab tab)
        {
            _currentTab = tab;
            Console.WriteLine($"[ImGuiMenuSystem] Switched to tab: {tab}");
        }
        
        /// <summary>
        /// Update - appelé chaque frame
        /// Gère le toggle ESC
        /// </summary>
        public void Update(float deltaTime)
        {
            // NOTE: ESC detection is done in Render() using ImGui.IsKeyPressed()
            // to avoid InputManager blocking it when menu is visible
            
            // Smooth animation
            float targetProgress = _isVisible ? 1f : 0f;
            _animationProgress = Lerp(_animationProgress, targetProgress, AnimationSpeed * deltaTime);
        }
        
        /// <summary>
        /// Render - appelé chaque frame pour dessiner le menu
        /// </summary>
        /// <param name="viewportPos">Position du viewport (GamePanel) - si null, utilise la fenêtre entière</param>
        /// <param name="viewportSize">Taille du viewport (GamePanel) - si null, utilise la fenêtre entière</param>
        public void Render(Vector2? viewportPos = null, Vector2? viewportSize = null)
        {
            // Check ESC for toggle (using ImGui to bypass InputManager blocking)
            if (ImGui.IsKeyPressed(ImGuiKey.Escape))
            {
                Console.WriteLine($"[ImGuiMenuSystem] ESC pressed via ImGui! Current state: {(_isVisible ? "VISIBLE" : "HIDDEN")}");
                Toggle();
            }
            
            // Skip rendering if animation is at 0
            if (_animationProgress < 0.01f)
                return;
            
            // Apply RPG theme
            RPGTheme.PushStyle();
            
            try
            {
                RenderMenu(viewportPos, viewportSize);
            }
            finally
            {
                RPGTheme.PopStyle();
            }
        }
        
        private void RenderMenu(Vector2? viewportPos = null, Vector2? viewportSize = null)
        {
            // Use provided viewport bounds (GamePanel) or fallback to main viewport
            Vector2 vpPos, vpSize;
            
            if (viewportPos.HasValue && viewportSize.HasValue)
            {
                vpPos = viewportPos.Value;
                vpSize = viewportSize.Value;
            }
            else
            {
                var viewport = ImGui.GetMainViewport();
                vpPos = viewport.Pos;
                vpSize = viewport.Size;
            }
            
            // Calculate menu size (80% of viewport, centered)
            var menuSize = new Vector2(vpSize.X * 0.8f, vpSize.Y * 0.8f);
            var menuPos = new Vector2(
                vpPos.X + (vpSize.X - menuSize.X) * 0.5f,
                vpPos.Y + (vpSize.Y - menuSize.Y) * 0.5f
            );
            
            // Apply animation (fade + scale)
            float alpha = _animationProgress;
            float scale = 0.8f + (_animationProgress * 0.2f); // Scale from 0.8 to 1.0
            
            menuSize *= scale;
            menuPos += (1f - scale) * menuSize * 0.5f; // Center while scaling
            
            // Dark overlay (semi-transparent background) - MUST be drawn FIRST (background)
            RenderOverlay(vpPos, vpSize, alpha * 0.7f);
            
            // Menu window - drawn AFTER overlay so it appears on top
            ImGui.SetNextWindowPos(menuPos);
            ImGui.SetNextWindowSize(menuSize);
            ImGui.SetNextWindowBgAlpha(alpha);
            
            var flags = ImGuiWindowFlags.NoDecoration |
                       ImGuiWindowFlags.NoMove |
                       ImGuiWindowFlags.NoResize |
                       ImGuiWindowFlags.NoSavedSettings;
            
            if (ImGui.Begin("##RPGMenu", flags))
            {
                // Header with title and close button
                RenderHeader();
                
                ImGui.Separator();
                
                // Tabs
                RenderTabs();
                
                ImGui.Separator();
                
                // Content area
                ImGui.BeginChild("##ContentArea", new Vector2(0, 0), ImGuiChildFlags.None);
                
                switch (_currentTab)
                {
                    case MenuTab.Inventory:
                        _inventoryPanel.Render();
                        break;
                    case MenuTab.Character:
                        _characterPanel.Render();
                        break;
                    case MenuTab.Map:
                        _mapPanel.Render();
                        break;
                    case MenuTab.Settings:
                        _settingsPanel.Render();
                        break;
                }
                
                ImGui.EndChild();
            }
            ImGui.End();
        }
        
        private void RenderOverlay(Vector2 pos, Vector2 size, float alpha)
        {
            // Use BackgroundDrawList so the overlay is behind ImGui windows
            var drawList = ImGui.GetBackgroundDrawList();
            uint overlayColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, alpha));
            drawList.AddRectFilled(pos, pos + size, overlayColor);
        }
        
        private void RenderHeader()
        {
            // Title
            ImGui.PushFont(ImGui.GetFont()); // TODO: Use custom larger font if available
            RPGTheme.TextWithIcon("⚔️", "Menu RPG");
            ImGui.PopFont();
            
            // Close button (top-right)
            ImGui.SameLine(ImGui.GetContentRegionAvail().X - 30f);
            if (ImGui.Button("✕##Close", new Vector2(30, 30)))
            {
                Hide();
            }
        }
        
        private void RenderTabs()
        {
            ImGui.BeginGroup();
            
            // Inventory tab
            if (RPGTheme.TabButton("🎒 Inventaire", _currentTab == MenuTab.Inventory))
            {
                SwitchTab(MenuTab.Inventory);
            }
            
            ImGui.SameLine();
            
            // Character tab
            if (RPGTheme.TabButton("👤 Personnage", _currentTab == MenuTab.Character))
            {
                SwitchTab(MenuTab.Character);
            }
            
            ImGui.SameLine();
            
            // Map tab
            if (RPGTheme.TabButton("🗺️ Carte", _currentTab == MenuTab.Map))
            {
                SwitchTab(MenuTab.Map);
            }
            
            ImGui.SameLine();
            
            // Settings tab
            if (RPGTheme.TabButton("⚙️ Paramètres", _currentTab == MenuTab.Settings))
            {
                SwitchTab(MenuTab.Settings);
            }
            
            ImGui.EndGroup();
        }
        
        private float Lerp(float a, float b, float t)
        {
            return a + (b - a) * Math.Clamp(t, 0f, 1f);
        }
        
        public void Dispose()
        {
            Console.WriteLine("[ImGuiMenuSystem] Disposed");
        }
    }
}

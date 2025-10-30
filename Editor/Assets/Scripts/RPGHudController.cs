using System;
using System.Linq;
using System.Numerics;
using Engine.Components;
using Engine.Scripting;
using Engine.UI.AstrildUI;
using ImGuiNET;

namespace Editor.Assets.Scripts
{
    /// <summary>
    /// HUD RPG fancy avec barre de vie, mana, stamina, mini-map, compass, buffs, etc.
    /// Utilise l'API UIBuilder pour créer l'interface de manière déclarative
    /// </summary>
    public class RPGHudController : MonoBehaviour
    {
        // Player stats (simulées pour démo)
        private float _playerHealth = 850f;
        private float _playerMaxHealth = 1000f;
        private float _playerMana = 420f;
        private float _playerMaxMana = 500f;
        private float _playerStamina = 90f;
        private float _playerMaxStamina = 100f;
        private int _playerLevel = 42;
        private float _playerXP = 2650f;
        private float _playerXPToNextLevel = 5000f;
        
        // UI State
        private UIBuilder? _ui;
        private float _compassRotation = 0f;
        private string[] _activeBuffs = { "🔥 Fire Shield", "⚡ Haste", "🛡️ Iron Skin" };
        private string[] _quickSlots = { "⚔️", "🏹", "🧪", "📜", "🍖" };
        private int _selectedQuickSlot = 0;
        
        // Animation
        private float _pulseTimer = 0f;
        private bool _showDamageFlash = false;
        private float _damageFlashTimer = 0f;
        
        // Crosshair
        private float _crosshairSpread = 0f; // Spread dynamique (0 = précis, augmente avec le mouvement)
        private float _crosshairTargetSpread = 0f;
        private bool _isAiming = false;
        
        // Mouse movement tracking
        private Vector2 _previousMousePos = Vector2.Zero;
        private float _mouseDeltaMagnitude = 0f;
        private bool _mouseInitialized = false;
        
        // GamePanel viewport bounds (set by GamePanel)
        private Vector2 _viewportPos = Vector2.Zero;
        private Vector2 _viewportSize = new Vector2(800, 600);
        
        // Camera matrices (for 3D to 2D projection)
        private OpenTK.Mathematics.Matrix4 _viewMatrix = OpenTK.Mathematics.Matrix4.Identity;
        private OpenTK.Mathematics.Matrix4 _projMatrix = OpenTK.Mathematics.Matrix4.Identity;

        public override void Start()
        {
            _ui = new UIBuilder(); // Default stylesheet
            Console.WriteLine("[RPGHudController] HUD initialized");
        }
        
        /// <summary>
        /// Called by GamePanel to set viewport bounds for HUD positioning
        /// </summary>
        public void SetViewportBounds(Vector2 position, Vector2 size)
        {
            _viewportPos = position;
            _viewportSize = size;
            // Console.WriteLine($"[RPGHudController] Viewport bounds updated: pos={position}, size={size}");
        }
        
        /// <summary>
        /// Called by GamePanel to set camera matrices for 3D to 2D projection
        /// </summary>
        public void SetCameraMatrices(OpenTK.Mathematics.Matrix4 viewMatrix, OpenTK.Mathematics.Matrix4 projMatrix)
        {
            _viewMatrix = viewMatrix;
            _projMatrix = projMatrix;
        }
        
        /// <summary>
        /// Convert world position (3D) to screen position (2D)
        /// Returns null if position is behind camera or outside viewport
        /// </summary>
        private Vector2? WorldToScreen(OpenTK.Mathematics.Vector3 worldPos)
        {
            // Transform to clip space
            var worldPos4 = new OpenTK.Mathematics.Vector4(worldPos.X, worldPos.Y, worldPos.Z, 1.0f);
            var viewPos = worldPos4 * _viewMatrix;
            var clipPos = viewPos * _projMatrix;
            
            // Behind camera check
            if (clipPos.W <= 0.0f)
                return null;
            
            // Perspective divide (NDC space: -1 to 1)
            var ndc = new OpenTK.Mathematics.Vector3(
                clipPos.X / clipPos.W,
                clipPos.Y / clipPos.W,
                clipPos.Z / clipPos.W
            );
            
            // Check if outside NDC bounds
            if (ndc.X < -1.0f || ndc.X > 1.0f || ndc.Y < -1.0f || ndc.Y > 1.0f)
                return null;
            
            // Convert NDC to screen space (viewport coordinates)
            var screenX = (ndc.X + 1.0f) * 0.5f * _viewportSize.X + _viewportPos.X;
            var screenY = (1.0f - ndc.Y) * 0.5f * _viewportSize.Y + _viewportPos.Y; // Flip Y
            
            return new Vector2(screenX, screenY);
        }

        public override void Update(float dt)
        {
            // Simulate stat changes for demo
            _pulseTimer += dt;
            _compassRotation += dt * 20f; // Rotate compass
            
            // Damage flash effect
            if (_damageFlashTimer > 0)
            {
                _damageFlashTimer -= dt;
                _showDamageFlash = _damageFlashTimer > 0;
            }
            
            // Crosshair spread dynamics based on mouse movement
            var currentMousePos = ImGuiNET.ImGui.GetMousePos();
            
            if (!_mouseInitialized)
            {
                _previousMousePos = currentMousePos;
                _mouseInitialized = true;
            }
            
            // Calculate mouse delta (movement speed)
            Vector2 mouseDelta = currentMousePos - _previousMousePos;
            float deltaLength = mouseDelta.Length();
            
            // Update previous position
            _previousMousePos = currentMousePos;
            
            // Smooth the delta magnitude (avoid instant jumps)
            _mouseDeltaMagnitude = Lerp(_mouseDeltaMagnitude, deltaLength, dt * 15f);
            
            // Calculate spread based on mouse movement
            // More movement = more spread (0 to 20 pixels)
            float maxSpread = 20f;
            float spreadFromMovement = MathF.Min(_mouseDeltaMagnitude * 0.5f, maxSpread);
            
            _crosshairTargetSpread = _isAiming ? 0f : spreadFromMovement;
            
            // Smooth lerp towards target spread
            // Fast expansion when moving, slower contraction when stopping
            float lerpSpeed = spreadFromMovement > _crosshairSpread ? 20f : 8f;
            _crosshairSpread = Lerp(_crosshairSpread, _crosshairTargetSpread, dt * lerpSpeed);
            
            // NOTE: RenderHUD() is now called by GamePanel after setting viewport bounds
        }
        
        /// <summary>
        /// Called by GamePanel to render the HUD after viewport bounds are set
        /// </summary>
        public void RenderHUDOverlay()
        {
            RenderHUD();
        }

        private void RenderHUD()
        {
            // ==============================================
            // TOP LEFT: Player Stats Panel
            // ==============================================
            ImGui.SetNextWindowPos(_viewportPos + new Vector2(20, 20), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(320, 200), ImGuiCond.Always);
            ImGui.SetNextWindowBgAlpha(0.85f);
            
            if (ImGui.Begin("##PlayerStats", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove))
            {
                // Player name & level with fancy border
                DrawFancyHeader($"⚔️ Hero of Astrild  •  Lv.{_playerLevel}", new Vector4(0.9f, 0.7f, 0.2f, 1f));
                
                ImGui.Spacing();
                
                // Health bar (red)
                DrawStatBar("❤️ Health", _playerHealth, _playerMaxHealth, 
                    new Vector4(0.8f, 0.2f, 0.2f, 1f), 
                    new Vector4(1.0f, 0.3f, 0.3f, 1f));
                
                ImGui.Spacing();
                
                // Mana bar (blue)
                DrawStatBar("💧 Mana", _playerMana, _playerMaxMana, 
                    new Vector4(0.2f, 0.4f, 0.9f, 1f),
                    new Vector4(0.3f, 0.6f, 1.0f, 1f));
                
                ImGui.Spacing();
                
                // Stamina bar (green)
                DrawStatBar("⚡ Stamina", _playerStamina, _playerMaxStamina, 
                    new Vector4(0.2f, 0.8f, 0.3f, 1f),
                    new Vector4(0.3f, 1.0f, 0.4f, 1f));
                
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
                
                // XP Progress bar with glow effect
                DrawXPBar(_playerXP, _playerXPToNextLevel);
            }
            ImGui.End();
            
            // ==============================================
            // TOP RIGHT: Active Buffs & Debuffs
            // ==============================================
            ImGui.SetNextWindowPos(_viewportPos + new Vector2(_viewportSize.X - 220, 20), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(200, 150), ImGuiCond.Always);
            ImGui.SetNextWindowBgAlpha(0.85f);
            
            if (ImGui.Begin("##Buffs", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove))
            {
                DrawFancyHeader("✨ Active Effects", new Vector4(0.6f, 0.4f, 0.9f, 1f));
                ImGui.Spacing();
                
                foreach (var buff in _activeBuffs)
                {
                    DrawBuffIcon(buff);
                    ImGui.Spacing();
                }
            }
            ImGui.End();
            
            // ==============================================
            // BOTTOM CENTER: Quick Slots
            // ==============================================
            float quickSlotWidth = 350f;
            ImGui.SetNextWindowPos(_viewportPos + new Vector2((_viewportSize.X - quickSlotWidth) * 0.5f, _viewportSize.Y - 100), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(quickSlotWidth, 80), ImGuiCond.Always);
            ImGui.SetNextWindowBgAlpha(0.85f);
            
            if (ImGui.Begin("##QuickSlots", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove))
            {
                DrawQuickSlots();
            }
            ImGui.End();
            
            // ==============================================
            // TOP CENTER: Compass & Mini-map
            // ==============================================
            ImGui.SetNextWindowPos(_viewportPos + new Vector2((_viewportSize.X - 120) * 0.5f, 20), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(120, 120), ImGuiCond.Always);
            ImGui.SetNextWindowBgAlpha(0.7f);
            
            if (ImGui.Begin("##Compass", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove))
            {
                DrawCompass();
            }
            ImGui.End();
            
            // ==============================================
            // BOTTOM RIGHT: Notifications / Quest Tracker
            // ==============================================
            ImGui.SetNextWindowPos(_viewportPos + new Vector2(_viewportSize.X - 280, _viewportSize.Y - 180), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(260, 160), ImGuiCond.Always);
            ImGui.SetNextWindowBgAlpha(0.85f);
            
            if (ImGui.Begin("##Quests", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove))
            {
                DrawFancyHeader("📜 Active Quests", new Vector4(0.9f, 0.7f, 0.2f, 1f));
                ImGui.Spacing();
                
                DrawQuestItem("Slay the Dragon", 1, 1, new Vector4(0.2f, 0.8f, 0.3f, 1f));
                DrawQuestItem("Collect Herbs", 7, 10, new Vector4(0.9f, 0.7f, 0.2f, 1f));
                DrawQuestItem("Find the Artifact", 0, 1, new Vector4(0.8f, 0.3f, 0.3f, 1f));
            }
            ImGui.End();
            
            // ==============================================
            // Damage Flash Overlay
            // ==============================================
            if (_showDamageFlash)
            {
                DrawDamageFlash();
            }
            
            // ==============================================
            // Crosshair (Center)
            // ==============================================
            RenderCrosshair();
            
            // ==============================================
            // Floating Info (3D World Space -> 2D Screen)
            // ==============================================
            RenderFloatingInfo();
        }

        // ============================================
        // Helper Methods - Fancy UI Components
        // ============================================
        
        /// <summary>
        /// Header avec bordure fancy et glow effect
        /// </summary>
        private void DrawFancyHeader(string text, Vector4 color)
        {
            var drawList = ImGui.GetWindowDrawList();
            var pos = ImGui.GetCursorScreenPos();
            var textSize = ImGui.CalcTextSize(text);
            
            // Glow background
            drawList.AddRectFilled(
                pos - new Vector2(5, 2),
                pos + new Vector2(textSize.X + 5, textSize.Y + 2),
                ImGui.ColorConvertFloat4ToU32(new Vector4(color.X * 0.3f, color.Y * 0.3f, color.Z * 0.3f, 0.5f)),
                3f
            );
            
            // Border
            drawList.AddRect(
                pos - new Vector2(5, 2),
                pos + new Vector2(textSize.X + 5, textSize.Y + 2),
                ImGui.ColorConvertFloat4ToU32(color),
                3f,
                ImDrawFlags.None,
                2f
            );
            
            ImGui.TextColored(color, text);
        }
        
        /// <summary>
        /// Barre de stat avec gradient et glow
        /// </summary>
        private void DrawStatBar(string label, float current, float max, Vector4 colorStart, Vector4 colorEnd)
        {
            ImGui.Text(label);
            ImGui.SameLine(200);
            ImGui.Text($"{current:F0} / {max:F0}");
            
            float fraction = max > 0 ? current / max : 0f;
            
            var drawList = ImGui.GetWindowDrawList();
            var pos = ImGui.GetCursorScreenPos();
            var barSize = new Vector2(ImGui.GetContentRegionAvail().X, 18);
            
            // Background (dark)
            drawList.AddRectFilled(pos, pos + barSize, ImGui.ColorConvertFloat4ToU32(new Vector4(0.1f, 0.1f, 0.12f, 1f)), 3f);
            
            // Filled bar with gradient
            if (fraction > 0)
            {
                var filledSize = new Vector2(barSize.X * fraction, barSize.Y);
                drawList.AddRectFilledMultiColor(
                    pos,
                    pos + filledSize,
                    ImGui.ColorConvertFloat4ToU32(colorStart),
                    ImGui.ColorConvertFloat4ToU32(colorEnd),
                    ImGui.ColorConvertFloat4ToU32(colorEnd),
                    ImGui.ColorConvertFloat4ToU32(colorStart)
                );
            }
            
            // Border
            drawList.AddRect(pos, pos + barSize, ImGui.ColorConvertFloat4ToU32(new Vector4(0.3f, 0.3f, 0.35f, 1f)), 3f, ImDrawFlags.None, 1.5f);
            
            // Pulse effect when low
            if (fraction < 0.3f)
            {
                float pulse = MathF.Abs(MathF.Sin(_pulseTimer * 3f)) * 0.5f;
                drawList.AddRect(pos - new Vector2(2, 2), pos + barSize + new Vector2(2, 2), 
                    ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.2f, 0.2f, pulse)), 3f, ImDrawFlags.None, 2f);
            }
            
            ImGui.Dummy(barSize);
        }
        
        /// <summary>
        /// XP Bar avec effet de progression fancy
        /// </summary>
        private void DrawXPBar(float current, float max)
        {
            ImGui.Text("🌟 Experience");
            
            float fraction = max > 0 ? current / max : 0f;
            
            var drawList = ImGui.GetWindowDrawList();
            var pos = ImGui.GetCursorScreenPos();
            var barSize = new Vector2(ImGui.GetContentRegionAvail().X, 15);
            
            // Background
            drawList.AddRectFilled(pos, pos + barSize, ImGui.ColorConvertFloat4ToU32(new Vector4(0.1f, 0.1f, 0.15f, 1f)), 5f);
            
            // XP bar (golden gradient)
            if (fraction > 0)
            {
                var filledSize = new Vector2(barSize.X * fraction, barSize.Y);
                drawList.AddRectFilledMultiColor(
                    pos,
                    pos + filledSize,
                    ImGui.ColorConvertFloat4ToU32(new Vector4(0.9f, 0.7f, 0.2f, 1f)),
                    ImGui.ColorConvertFloat4ToU32(new Vector4(1.0f, 0.9f, 0.3f, 1f)),
                    ImGui.ColorConvertFloat4ToU32(new Vector4(1.0f, 0.9f, 0.3f, 1f)),
                    ImGui.ColorConvertFloat4ToU32(new Vector4(0.9f, 0.7f, 0.2f, 1f))
                );
                
                // Shimmer effect
                float shimmer = (_pulseTimer * 2f) % 1f;
                float shimmerX = pos.X + (barSize.X * fraction * shimmer);
                drawList.AddLine(
                    new Vector2(shimmerX, pos.Y),
                    new Vector2(shimmerX, pos.Y + barSize.Y),
                    ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 0.8f, 0.8f)),
                    3f
                );
            }
            
            // Border with glow
            drawList.AddRect(pos, pos + barSize, ImGui.ColorConvertFloat4ToU32(new Vector4(0.7f, 0.5f, 0.1f, 1f)), 5f, ImDrawFlags.None, 1.5f);
            
            // Text overlay
            var text = $"{current:F0} / {max:F0} ({fraction * 100:F1}%)";
            var textSize = ImGui.CalcTextSize(text);
            var textPos = pos + new Vector2((barSize.X - textSize.X) * 0.5f, (barSize.Y - textSize.Y) * 0.5f);
            drawList.AddText(textPos, ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 1f)), text);
            
            ImGui.Dummy(barSize);
        }
        
        /// <summary>
        /// Buff icon avec timer circulaire
        /// </summary>
        private void DrawBuffIcon(string buffName)
        {
            var drawList = ImGui.GetWindowDrawList();
            var pos = ImGui.GetCursorScreenPos();
            
            // Icon circle background
            float radius = 18f;
            var center = pos + new Vector2(radius, radius);
            drawList.AddCircleFilled(center, radius, ImGui.ColorConvertFloat4ToU32(new Vector4(0.2f, 0.2f, 0.25f, 1f)), 32);
            
            // Progress ring (simulated)
            float progress = (_pulseTimer % 10f) / 10f;
            int segments = (int)(progress * 32);
            for (int i = 0; i < segments; i++)
            {
                float angle1 = (float)i / 32 * MathF.PI * 2 - MathF.PI / 2;
                float angle2 = (float)(i + 1) / 32 * MathF.PI * 2 - MathF.PI / 2;
                
                var p1 = center + new Vector2(MathF.Cos(angle1), MathF.Sin(angle1)) * (radius + 2);
                var p2 = center + new Vector2(MathF.Cos(angle2), MathF.Sin(angle2)) * (radius + 2);
                
                drawList.AddLine(p1, p2, ImGui.ColorConvertFloat4ToU32(new Vector4(0.6f, 0.4f, 0.9f, 1f)), 3f);
            }
            
            // Border
            drawList.AddCircle(center, radius, ImGui.ColorConvertFloat4ToU32(new Vector4(0.5f, 0.5f, 0.6f, 1f)), 32, 2f);
            
            ImGui.Dummy(new Vector2(radius * 2 + 5, radius * 2));
            ImGui.SameLine();
            ImGui.Text(buffName);
        }
        
        /// <summary>
        /// Quick slots avec sélection et keybinds
        /// </summary>
        private void DrawQuickSlots()
        {
            float slotSize = 55f;
            float spacing = 5f;
            float totalWidth = (_quickSlots.Length * slotSize) + ((_quickSlots.Length - 1) * spacing);
            float startX = (ImGui.GetContentRegionAvail().X - totalWidth) * 0.5f;
            
            ImGui.SetCursorPosX(startX);
            
            for (int i = 0; i < _quickSlots.Length; i++)
            {
                if (i > 0)
                {
                    ImGui.SameLine(0, spacing);
                }
                
                bool selected = i == _selectedQuickSlot;
                DrawQuickSlot(_quickSlots[i], i + 1, selected);
            }
        }
        
        private void DrawQuickSlot(string icon, int number, bool selected)
        {
            var drawList = ImGui.GetWindowDrawList();
            var pos = ImGui.GetCursorScreenPos();
            float slotSize = 55f;
            
            // Background
            var bgColor = selected 
                ? new Vector4(0.4f, 0.5f, 0.9f, 0.5f)
                : new Vector4(0.15f, 0.15f, 0.18f, 0.9f);
            drawList.AddRectFilled(pos, pos + new Vector2(slotSize, slotSize), ImGui.ColorConvertFloat4ToU32(bgColor), 5f);
            
            // Border
            var borderColor = selected
                ? new Vector4(0.5f, 0.7f, 1.0f, 1f)
                : new Vector4(0.3f, 0.3f, 0.35f, 1f);
            float borderWidth = selected ? 3f : 1.5f;
            drawList.AddRect(pos, pos + new Vector2(slotSize, slotSize), ImGui.ColorConvertFloat4ToU32(borderColor), 5f, ImDrawFlags.None, borderWidth);
            
            // Icon (emoji)
            var iconSize = ImGui.CalcTextSize(icon);
            var iconPos = pos + new Vector2((slotSize - iconSize.X) * 0.5f, (slotSize - iconSize.Y) * 0.5f - 5);
            drawList.AddText(iconPos, ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 1f)), icon);
            
            // Keybind number
            var numText = number.ToString();
            var numSize = ImGui.CalcTextSize(numText);
            var numPos = pos + new Vector2(slotSize - numSize.X - 3, 2);
            drawList.AddText(numPos, ImGui.ColorConvertFloat4ToU32(new Vector4(0.9f, 0.9f, 0.9f, 0.7f)), numText);
            
            ImGui.Dummy(new Vector2(slotSize, slotSize));
            
            // Click to select
            if (ImGui.IsItemClicked())
            {
                _selectedQuickSlot = number - 1;
            }
        }
        
        /// <summary>
        /// Compass rotatif avec directions
        /// </summary>
        private void DrawCompass()
        {
            var drawList = ImGui.GetWindowDrawList();
            var center = ImGui.GetCursorScreenPos() + new Vector2(50, 50);
            float radius = 40f;
            
            // Outer circle (background)
            drawList.AddCircleFilled(center, radius, ImGui.ColorConvertFloat4ToU32(new Vector4(0.1f, 0.1f, 0.12f, 0.9f)), 64);
            
            // Compass markings (rotating)
            string[] directions = { "N", "E", "S", "W" };
            for (int i = 0; i < 4; i++)
            {
                float angle = (_compassRotation + i * 90f) * MathF.PI / 180f;
                var pos = center + new Vector2(MathF.Sin(angle), -MathF.Cos(angle)) * (radius - 15);
                
                var color = i == 0 ? new Vector4(1f, 0.3f, 0.3f, 1f) : new Vector4(0.7f, 0.7f, 0.7f, 1f);
                drawList.AddText(pos - new Vector2(5, 8), ImGui.ColorConvertFloat4ToU32(color), directions[i]);
            }
            
            // Border
            drawList.AddCircle(center, radius, ImGui.ColorConvertFloat4ToU32(new Vector4(0.4f, 0.4f, 0.45f, 1f)), 64, 2f);
            
            // Center dot (player position)
            drawList.AddCircleFilled(center, 5f, ImGui.ColorConvertFloat4ToU32(new Vector4(0.9f, 0.7f, 0.2f, 1f)), 16);
            
            ImGui.Dummy(new Vector2(100, 100));
        }
        
        /// <summary>
        /// Quest item avec progress bar
        /// </summary>
        private void DrawQuestItem(string questName, int current, int max, Vector4 color)
        {
            ImGui.BulletText(questName);
            
            float progress = max > 0 ? (float)current / max : 0f;
            
            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, color);
            ImGui.ProgressBar(progress, new Vector2(-1, 15), $"{current}/{max}");
            ImGui.PopStyleColor();
        }
        
        /// <summary>
        /// Flash rouge sur tout l'écran quand le joueur prend des dégâts
        /// </summary>
        private void DrawDamageFlash()
        {
            ImGui.SetNextWindowPos(_viewportPos);
            ImGui.SetNextWindowSize(_viewportSize);
            ImGui.SetNextWindowBgAlpha(0f);
            
            if (ImGui.Begin("##DamageFlash", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | 
                ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoInputs))
            {
                var drawList = ImGui.GetWindowDrawList();
                float alpha = _damageFlashTimer / 0.5f; // Fade out over 0.5 seconds
                
                drawList.AddRectFilled(
                    _viewportPos,
                    _viewportPos + _viewportSize,
                    ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0f, 0f, alpha * 0.3f))
                );
            }
            ImGui.End();
        }
        
        /// <summary>
        /// Simule des dégâts pour tester le flash effect (appelé par input par exemple)
        /// </summary>
        public void TakeDamage(float amount)
        {
            _playerHealth = MathF.Max(0, _playerHealth - amount);
            _damageFlashTimer = 0.5f;
            _showDamageFlash = true;
            Console.WriteLine($"[RPGHudController] Took {amount} damage! Health: {_playerHealth}/{_playerMaxHealth}");
        }
        
        private void RenderCrosshair()
        {
            // Get center of viewport
            Vector2 center = _viewportPos + (_viewportSize * 0.5f);
            
            // Crosshair settings
            float baseLength = 8f;  // Base line length
            float gap = 4f + _crosshairSpread; // Gap from center (increases with spread)
            float thickness = 2f;
            
            // Colors
            Vector4 outerColor = new Vector4(0.1f, 0.1f, 0.1f, 0.8f); // Dark outline
            Vector4 innerColor = new Vector4(1f, 1f, 1f, 0.9f); // White center
            
            if (_isAiming)
            {
                innerColor = new Vector4(0.2f, 1f, 0.2f, 1f); // Green when aiming
            }
            
            // Use foreground draw list to draw on top of everything
            var drawList = ImGui.GetForegroundDrawList();
            
            // Draw 4 lines forming a cross with gap in the middle
            // Top
            Vector2 topStart = center + new Vector2(0, -gap);
            Vector2 topEnd = center + new Vector2(0, -gap - baseLength);
            drawList.AddLine(topStart, topEnd, ImGui.ColorConvertFloat4ToU32(outerColor), thickness + 2);
            drawList.AddLine(topStart, topEnd, ImGui.ColorConvertFloat4ToU32(innerColor), thickness);
            
            // Bottom
            Vector2 botStart = center + new Vector2(0, gap);
            Vector2 botEnd = center + new Vector2(0, gap + baseLength);
            drawList.AddLine(botStart, botEnd, ImGui.ColorConvertFloat4ToU32(outerColor), thickness + 2);
            drawList.AddLine(botStart, botEnd, ImGui.ColorConvertFloat4ToU32(innerColor), thickness);
            
            // Left
            Vector2 leftStart = center + new Vector2(-gap, 0);
            Vector2 leftEnd = center + new Vector2(-gap - baseLength, 0);
            drawList.AddLine(leftStart, leftEnd, ImGui.ColorConvertFloat4ToU32(outerColor), thickness + 2);
            drawList.AddLine(leftStart, leftEnd, ImGui.ColorConvertFloat4ToU32(innerColor), thickness);
            
            // Right
            Vector2 rightStart = center + new Vector2(gap, 0);
            Vector2 rightEnd = center + new Vector2(gap + baseLength, 0);
            drawList.AddLine(rightStart, rightEnd, ImGui.ColorConvertFloat4ToU32(outerColor), thickness + 2);
            drawList.AddLine(rightStart, rightEnd, ImGui.ColorConvertFloat4ToU32(innerColor), thickness);
            
            // Optional: Center dot
            if (_isAiming)
            {
                drawList.AddCircleFilled(center, 2f, ImGui.ColorConvertFloat4ToU32(new Vector4(0.2f, 1f, 0.2f, 0.8f)));
            }
        }
        
        private void RenderFloatingInfo()
        {
            // Get the Play Mode scene
            var scene = PlayMode.PlayScene;
            if (scene == null) return;
            
            // Example: Find player entity (look for entity named "Player" or with a character controller)
            var playerEntity = scene.Entities.FirstOrDefault(e => 
            {
                var name = e.Name?.ToLower() ?? "";
                return name.Contains("player") || name.Contains("character");
            });
            
            if (playerEntity != null)
            {
                // Get player position (add offset for above head)
                var transform = playerEntity.Transform;
                var playerWorldPos = new OpenTK.Mathematics.Vector3(
                    transform.Position.X,
                    transform.Position.Y + 2.5f, // 2.5 units above player
                    transform.Position.Z
                );
                
                // Convert to screen position
                var screenPos = WorldToScreen(playerWorldPos);
                
                if (screenPos.HasValue)
                {
                    // Draw floating info
                    var drawList = ImGui.GetForegroundDrawList();
                    
                    string playerName = playerEntity.Name ?? "Player";
                    string healthText = $"{(int)_playerHealth}/{(int)_playerMaxHealth}";
                    
                    // Calculate text size for centering
                    var nameSize = ImGui.CalcTextSize(playerName);
                    var healthSize = ImGui.CalcTextSize(healthText);
                    
                    // Center positions
                    Vector2 namePos = new Vector2(screenPos.Value.X - nameSize.X * 0.5f, screenPos.Value.Y - 40);
                    Vector2 healthBarPos = new Vector2(screenPos.Value.X - 50, screenPos.Value.Y - 20);
                    Vector2 healthTextPos = new Vector2(screenPos.Value.X - healthSize.X * 0.5f, screenPos.Value.Y - 18);
                    
                    // Draw name with shadow
                    drawList.AddText(namePos + new Vector2(1, 1), ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, 0.8f)), playerName);
                    drawList.AddText(namePos, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, 1)), playerName);
                    
                    // Draw health bar background
                    Vector2 barSize = new Vector2(100, 8);
                    drawList.AddRectFilled(
                        healthBarPos,
                        healthBarPos + barSize,
                        ImGui.ColorConvertFloat4ToU32(new Vector4(0.2f, 0.2f, 0.2f, 0.8f))
                    );
                    
                    // Draw health bar fill
                    float healthPercent = _playerHealth / _playerMaxHealth;
                    Vector2 fillSize = new Vector2(barSize.X * healthPercent, barSize.Y);
                    Vector4 healthColor = healthPercent > 0.5f ? new Vector4(0.2f, 0.8f, 0.2f, 1f) :
                                         healthPercent > 0.25f ? new Vector4(0.9f, 0.7f, 0.2f, 1f) :
                                         new Vector4(0.9f, 0.2f, 0.2f, 1f);
                    
                    drawList.AddRectFilled(
                        healthBarPos,
                        healthBarPos + fillSize,
                        ImGui.ColorConvertFloat4ToU32(healthColor)
                    );
                    
                    // Draw health bar border
                    drawList.AddRect(
                        healthBarPos,
                        healthBarPos + barSize,
                        ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, 1)),
                        0f, ImDrawFlags.None, 1.5f
                    );
                    
                    // Draw health text centered on health bar
                    drawList.AddText(healthTextPos + new Vector2(1, 1), ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, 0.8f)), healthText);
                    drawList.AddText(healthTextPos, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, 1)), healthText);
                }
            }
            
            // You can loop through all entities to show info for NPCs, enemies, etc.
            // For now, just showing the player as an example
        }
        
        private float Lerp(float a, float b, float t)
        {
            return a + (b - a) * MathF.Max(0f, MathF.Min(1f, t));
        }
    }
}

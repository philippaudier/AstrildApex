using System;
using System.Numerics;
using ImGuiNET;

namespace Engine.UI.AstrildUI
{
    /// <summary>
    /// Système de styles réutilisables avec thèmes prédéfinis
    /// </summary>
    public class UIStyleSheet
    {
        // Singleton default
        private static UIStyleSheet? _default;
        public static UIStyleSheet Default => _default ??= CreateRPGTheme();
        
        // Colors
        public Vector4 PrimaryColor { get; set; }
        public Vector4 PrimaryHoverColor { get; set; }
        public Vector4 PrimaryActiveColor { get; set; }
        public Vector4 BackgroundColor { get; set; }
        public Vector4 WindowBackgroundColor { get; set; }
        public Vector4 BorderColor { get; set; }
        public Vector4 TextColor { get; set; }
        public Vector4 TextDisabledColor { get; set; }
        
        // Sizes
        public float WindowRounding { get; set; } = 8f;
        public float FrameRounding { get; set; } = 4f;
        public float WindowBorderSize { get; set; } = 2f;
        public float FrameBorderSize { get; set; } = 1f;
        public Vector2 WindowPadding { get; set; } = new Vector2(15f, 15f);
        public Vector2 FramePadding { get; set; } = new Vector2(8f, 6f);
        public Vector2 ItemSpacing { get; set; } = new Vector2(12f, 8f);
        
        private bool _isPushed = false;
        private int _colorsPushed = 0;
        private int _varsPushed = 0;
        
        /// <summary>
        /// Applique le style sur la stack ImGui
        /// </summary>
        public void Push()
        {
            if (_isPushed)
            {
                Console.WriteLine("[UIStyleSheet] Warning: Already pushed, skipping");
                return;
            }
            
            _colorsPushed = 0;
            _varsPushed = 0;
            
            // Push colors
            ImGui.PushStyleColor(ImGuiCol.WindowBg, WindowBackgroundColor); _colorsPushed++;
            ImGui.PushStyleColor(ImGuiCol.Border, BorderColor); _colorsPushed++;
            ImGui.PushStyleColor(ImGuiCol.Text, TextColor); _colorsPushed++;
            ImGui.PushStyleColor(ImGuiCol.TextDisabled, TextDisabledColor); _colorsPushed++;
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.25f, 0.25f, 0.3f, 1.0f)); _colorsPushed++;
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, PrimaryHoverColor); _colorsPushed++;
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, PrimaryActiveColor); _colorsPushed++;
            ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.15f, 0.15f, 0.18f, 1.0f)); _colorsPushed++;
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(0.2f, 0.2f, 0.25f, 1.0f)); _colorsPushed++;
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(0.25f, 0.25f, 0.3f, 1.0f)); _colorsPushed++;
            
            // Push vars
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, WindowRounding); _varsPushed++;
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, WindowBorderSize); _varsPushed++;
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, FrameRounding); _varsPushed++;
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, FrameBorderSize); _varsPushed++;
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, WindowPadding); _varsPushed++;
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, FramePadding); _varsPushed++;
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, ItemSpacing); _varsPushed++;
            
            _isPushed = true;
        }
        
        /// <summary>
        /// Retire le style de la stack ImGui
        /// </summary>
        public void Pop()
        {
            if (!_isPushed)
            {
                Console.WriteLine("[UIStyleSheet] Warning: Not pushed, skipping pop");
                return;
            }
            
            ImGui.PopStyleVar(_varsPushed);
            ImGui.PopStyleColor(_colorsPushed);
            
            _isPushed = false;
            _colorsPushed = 0;
            _varsPushed = 0;
        }
        
        // ============================================
        // Thèmes prédéfinis
        // ============================================
        
        /// <summary>
        /// Thème RPG dark fantasy avec accents rouges
        /// </summary>
        public static UIStyleSheet CreateRPGTheme()
        {
            return new UIStyleSheet
            {
                PrimaryColor = new Vector4(0.91f, 0.27f, 0.38f, 1.0f), // #E94560
                PrimaryHoverColor = new Vector4(0.75f, 0.22f, 0.31f, 1.0f),
                PrimaryActiveColor = new Vector4(0.91f, 0.27f, 0.38f, 1.0f),
                BackgroundColor = new Vector4(0.08f, 0.08f, 0.10f, 0.95f),
                WindowBackgroundColor = new Vector4(0.12f, 0.12f, 0.15f, 0.98f),
                BorderColor = new Vector4(0.91f, 0.27f, 0.38f, 0.8f),
                TextColor = new Vector4(0.95f, 0.95f, 0.98f, 1.0f),
                TextDisabledColor = new Vector4(0.5f, 0.5f, 0.55f, 1.0f),
                WindowRounding = 8f,
                FrameRounding = 4f,
                WindowBorderSize = 2f,
                FrameBorderSize = 1f,
                WindowPadding = new Vector2(15f, 15f),
                FramePadding = new Vector2(8f, 6f),
                ItemSpacing = new Vector2(12f, 8f)
            };
        }
        
        /// <summary>
        /// Thème Sci-Fi futuriste avec bleu néon
        /// </summary>
        public static UIStyleSheet CreateSciFiTheme()
        {
            return new UIStyleSheet
            {
                PrimaryColor = new Vector4(0.0f, 0.7f, 1.0f, 1.0f), // Cyan néon
                PrimaryHoverColor = new Vector4(0.2f, 0.8f, 1.0f, 1.0f),
                PrimaryActiveColor = new Vector4(0.0f, 0.6f, 0.9f, 1.0f),
                BackgroundColor = new Vector4(0.02f, 0.05f, 0.10f, 0.95f),
                WindowBackgroundColor = new Vector4(0.05f, 0.08f, 0.12f, 0.98f),
                BorderColor = new Vector4(0.0f, 0.7f, 1.0f, 0.8f),
                TextColor = new Vector4(0.9f, 0.95f, 1.0f, 1.0f),
                TextDisabledColor = new Vector4(0.4f, 0.5f, 0.6f, 1.0f),
                WindowRounding = 2f,
                FrameRounding = 2f,
                WindowBorderSize = 1f,
                FrameBorderSize = 1f,
                WindowPadding = new Vector2(12f, 12f),
                FramePadding = new Vector2(6f, 4f),
                ItemSpacing = new Vector2(10f, 6f)
            };
        }
        
        /// <summary>
        /// Thème minimal moderne clair
        /// </summary>
        public static UIStyleSheet CreateMinimalTheme()
        {
            return new UIStyleSheet
            {
                PrimaryColor = new Vector4(0.3f, 0.5f, 0.9f, 1.0f), // Bleu doux
                PrimaryHoverColor = new Vector4(0.4f, 0.6f, 1.0f, 1.0f),
                PrimaryActiveColor = new Vector4(0.2f, 0.4f, 0.8f, 1.0f),
                BackgroundColor = new Vector4(0.95f, 0.95f, 0.96f, 1.0f),
                WindowBackgroundColor = new Vector4(1.0f, 1.0f, 1.0f, 1.0f),
                BorderColor = new Vector4(0.8f, 0.8f, 0.82f, 1.0f),
                TextColor = new Vector4(0.1f, 0.1f, 0.12f, 1.0f),
                TextDisabledColor = new Vector4(0.5f, 0.5f, 0.52f, 1.0f),
                WindowRounding = 6f,
                FrameRounding = 3f,
                WindowBorderSize = 1f,
                FrameBorderSize = 1f,
                WindowPadding = new Vector2(14f, 14f),
                FramePadding = new Vector2(7f, 5f),
                ItemSpacing = new Vector2(10f, 7f)
            };
        }
        
        /// <summary>
        /// Thème fantasy avec tons verts/dorés
        /// </summary>
        public static UIStyleSheet CreateFantasyTheme()
        {
            return new UIStyleSheet
            {
                PrimaryColor = new Vector4(0.7f, 0.6f, 0.2f, 1.0f), // Or
                PrimaryHoverColor = new Vector4(0.8f, 0.7f, 0.3f, 1.0f),
                PrimaryActiveColor = new Vector4(0.6f, 0.5f, 0.1f, 1.0f),
                BackgroundColor = new Vector4(0.15f, 0.12f, 0.08f, 0.95f),
                WindowBackgroundColor = new Vector4(0.2f, 0.16f, 0.12f, 0.98f),
                BorderColor = new Vector4(0.7f, 0.6f, 0.2f, 0.8f),
                TextColor = new Vector4(0.95f, 0.92f, 0.85f, 1.0f),
                TextDisabledColor = new Vector4(0.5f, 0.48f, 0.45f, 1.0f),
                WindowRounding = 10f,
                FrameRounding = 5f,
                WindowBorderSize = 2f,
                FrameBorderSize = 1f,
                WindowPadding = new Vector2(16f, 16f),
                FramePadding = new Vector2(8f, 6f),
                ItemSpacing = new Vector2(12f, 8f)
            };
        }
    }
}

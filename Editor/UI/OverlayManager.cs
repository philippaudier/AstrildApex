using System;
using System.Collections.Generic;
using ImGuiNET;

namespace Editor.UI
{
    /// <summary>
    /// Unified overlay management system for editor panels.
    /// Handles positioning, dragging, visibility, and focus for all overlays.
    /// Prevents duplication and manages overlay state centrally.
    /// </summary>
    public class OverlayManager
    {
        private readonly Dictionary<string, OverlayState> _overlays = new();
        private readonly string _panelId;

        public OverlayManager(string panelId)
        {
            _panelId = panelId;
        }

        /// <summary>
        /// Register an overlay with default settings
        /// </summary>
        public void RegisterOverlay(string overlayId, OverlayAnchor anchor = OverlayAnchor.TopLeft, bool visible = true)
        {
            if (_overlays.ContainsKey(overlayId)) return;

            _overlays[overlayId] = new OverlayState
            {
                Id = overlayId,
                Anchor = anchor,
                Visible = visible,
                Position = System.Numerics.Vector2.Zero,
                IsPositionInitialized = false
            };
        }

        /// <summary>
        /// Begin drawing an overlay. Returns true if the overlay should be rendered.
        /// </summary>
        public bool BeginOverlay(
            string overlayId,
            System.Numerics.Vector2 imageMin,
            System.Numerics.Vector2 imageMax,
            bool isPanelFocused,
            ImGuiWindowFlags additionalFlags = ImGuiWindowFlags.None)
        {
            if (!_overlays.TryGetValue(overlayId, out var state) || !state.Visible)
                return false;

            // Initialize position if needed
            if (!state.IsPositionInitialized)
            {
                state.Position = CalculateAnchoredPosition(state.Anchor, imageMin, imageMax, System.Numerics.Vector2.Zero);
                state.IsPositionInitialized = true;
            }

            // Clamp position to image bounds
            state.Position = ClampPositionToImage(state.Position, imageMin, imageMax);

            // Cache theme for this overlay draw to avoid repeated property access
            var theme = Themes.ThemeManager.CurrentTheme;
            // Apply theme colors
            var winBg = theme.ChildBackground;
            var border = theme.Border;
            ImGui.PushStyleColor(ImGuiCol.WindowBg, winBg);
            ImGui.PushStyleColor(ImGuiCol.Border, border);

            ImGui.SetNextWindowPos(state.Position, ImGuiCond.Always);
            ImGui.SetNextWindowBgAlpha(winBg.W);

            var flags = ImGuiWindowFlags.NoTitleBar |
                       ImGuiWindowFlags.AlwaysAutoResize |
                       ImGuiWindowFlags.NoSavedSettings |
                       ImGuiWindowFlags.NoCollapse |
                       ImGuiWindowFlags.NoDocking |
                       ImGuiWindowFlags.NoFocusOnAppearing |
                       ImGuiWindowFlags.NoBringToFrontOnFocus |
                       additionalFlags;

            bool opened = ImGui.Begin($"##Overlay_{_panelId}_{overlayId}", flags);

            if (opened)
            {
                // Handle dragging
                if (ImGui.IsWindowHovered() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                {
                    var drag = ImGui.GetIO().MouseDelta;
                    state.Position += drag;
                    state.Position = ClampPositionToImage(state.Position, imageMin, imageMax);
                    ImGui.SetWindowPos(state.Position);
                }
            }

            return opened;
        }

        /// <summary>
        /// End drawing an overlay. Must be called after BeginOverlay.
        /// </summary>
        public void EndOverlay(string overlayId)
        {
            ImGui.End();
            ImGui.PopStyleColor(2); // WindowBg and Border
        }

        /// <summary>
        /// Toggle overlay visibility
        /// </summary>
        public void SetOverlayVisible(string overlayId, bool visible)
        {
            if (_overlays.TryGetValue(overlayId, out var state))
            {
                state.Visible = visible;
            }
        }

        /// <summary>
        /// Check if overlay is visible
        /// </summary>
        public bool IsOverlayVisible(string overlayId)
        {
            return _overlays.TryGetValue(overlayId, out var state) && state.Visible;
        }

        /// <summary>
        /// Reset overlay position to default anchor
        /// </summary>
        public void ResetOverlayPosition(string overlayId)
        {
            if (_overlays.TryGetValue(overlayId, out var state))
            {
                state.IsPositionInitialized = false;
            }
        }

        /// <summary>
        /// Save overlay state to editor settings
        /// </summary>
        public void SaveState()
        {
            // TODO: Implement persistence to EditorSettings
        }

        /// <summary>
        /// Load overlay state from editor settings
        /// </summary>
        public void LoadState()
        {
            // TODO: Implement loading from EditorSettings
        }

        private System.Numerics.Vector2 CalculateAnchoredPosition(
            OverlayAnchor anchor,
            System.Numerics.Vector2 imageMin,
            System.Numerics.Vector2 imageMax,
            System.Numerics.Vector2 overlaySize)
        {
            float margin = 10f;

            return anchor switch
            {
                OverlayAnchor.TopLeft => imageMin + new System.Numerics.Vector2(margin, margin),
                OverlayAnchor.TopRight => new System.Numerics.Vector2(imageMax.X - overlaySize.X - margin, imageMin.Y + margin),
                OverlayAnchor.BottomLeft => new System.Numerics.Vector2(imageMin.X + margin, imageMax.Y - overlaySize.Y - margin),
                OverlayAnchor.BottomRight => imageMax - overlaySize - new System.Numerics.Vector2(margin, margin),
                OverlayAnchor.TopCenter => new System.Numerics.Vector2((imageMin.X + imageMax.X - overlaySize.X) * 0.5f, imageMin.Y + margin),
                OverlayAnchor.BottomCenter => new System.Numerics.Vector2((imageMin.X + imageMax.X - overlaySize.X) * 0.5f, imageMax.Y - overlaySize.Y - margin),
                _ => imageMin + new System.Numerics.Vector2(margin, margin)
            };
        }

        private System.Numerics.Vector2 ClampPositionToImage(
            System.Numerics.Vector2 position,
            System.Numerics.Vector2 imageMin,
            System.Numerics.Vector2 imageMax)
        {
            float margin = 8f;

            position.X = position.X < imageMin.X + margin ? imageMin.X + margin : position.X;
            position.Y = position.Y < imageMin.Y + margin ? imageMin.Y + margin : position.Y;
            position.X = position.X > imageMax.X - margin ? imageMax.X - margin : position.X;
            position.Y = position.Y > imageMax.Y - margin ? imageMax.Y - margin : position.Y;

            return position;
        }

        private class OverlayState
        {
            public string Id { get; set; } = "";
            public OverlayAnchor Anchor { get; set; }
            public bool Visible { get; set; }
            public System.Numerics.Vector2 Position { get; set; }
            public bool IsPositionInitialized { get; set; }
        }
    }

    /// <summary>
    /// Anchor positions for overlays
    /// </summary>
    public enum OverlayAnchor
    {
        TopLeft,
        TopRight,
        TopCenter,
        BottomLeft,
        BottomRight,
        BottomCenter
    }
}

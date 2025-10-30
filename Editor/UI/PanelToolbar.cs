using System;
using System.Collections.Generic;
using ImGuiNET;

namespace Editor.UI
{
    /// <summary>
    /// Reusable toolbar component for panel headers.
    /// Displays overlay toggle buttons and other panel-specific controls.
    /// </summary>
    public class PanelToolbar
    {
        private readonly List<ToolbarButton> _buttons = new();
        private readonly string _panelId;

        public PanelToolbar(string panelId)
        {
            _panelId = panelId;
        }

        /// <summary>
        /// Add a toggle button for an overlay (using callback instead of ref)
        /// </summary>
        public void AddOverlayToggle(string label, string tooltip, Func<bool> getValue, Action<bool> setValue, string? icon = null)
        {
            _buttons.Add(new ToolbarButton
            {
                Label = label,
                Tooltip = tooltip,
                Icon = icon,
                IsToggle = true,
                GetValue = getValue,
                OnClick = () => setValue(!getValue())
            });
        }

        /// <summary>
        /// Add a regular action button
        /// </summary>
        public void AddButton(string label, string tooltip, Action onClick, string? icon = null)
        {
            _buttons.Add(new ToolbarButton
            {
                Label = label,
                Tooltip = tooltip,
                Icon = icon,
                IsToggle = false,
                OnClick = onClick
            });
        }

        /// <summary>
        /// Add a separator between button groups
        /// </summary>
        public void AddSeparator()
        {
            _buttons.Add(new ToolbarButton { IsSeparator = true });
        }

        /// <summary>
        /// Clear all buttons
        /// </summary>
        public void Clear()
        {
            _buttons.Clear();
        }

        /// <summary>
        /// Draw the toolbar. Call this after ImGui.Begin() and before main content.
        /// </summary>
        public void Draw()
        {
            if (_buttons.Count == 0) return;

            // Apply toolbar styling
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new System.Numerics.Vector2(6, 3));
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new System.Numerics.Vector2(4, 4));

            // Background for toolbar area
            var drawList = ImGui.GetWindowDrawList();
            var toolbarMin = ImGui.GetCursorScreenPos();
            var availWidth = ImGui.GetContentRegionAvail().X;

            // Calculate toolbar height
            float toolbarHeight = ImGui.GetFrameHeight() + ImGui.GetStyle().FramePadding.Y * 2;
            var toolbarMax = toolbarMin + new System.Numerics.Vector2(availWidth, toolbarHeight);

            // Draw subtle background
            var bgColor = ImGui.GetColorU32(new System.Numerics.Vector4(0.15f, 0.15f, 0.15f, 0.5f));
            drawList.AddRectFilled(toolbarMin, toolbarMax, bgColor);

            // Draw buttons
            for (int i = 0; i < _buttons.Count; i++)
            {
                var button = _buttons[i];

                if (button.IsSeparator)
                {
                    ImGui.SameLine();
                    DrawSeparator();
                    continue;
                }

                if (i > 0 && !_buttons[i - 1].IsSeparator)
                {
                    ImGui.SameLine();
                }

                DrawButton(button);
            }

            ImGui.PopStyleVar(2);

            // Add spacing after toolbar
            ImGui.Spacing();
            ImGui.Separator();
        }

        private void DrawButton(ToolbarButton button)
        {
            // Get current active state for toggle buttons
            bool isActive = button.IsToggle && button.GetValue != null && button.GetValue();

            // Apply active styling for toggle buttons
            if (button.IsToggle && isActive)
            {
                var activeColor = new System.Numerics.Vector4(0.3f, 0.5f, 0.8f, 0.6f);
                ImGui.PushStyleColor(ImGuiCol.Button, activeColor);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, activeColor * 1.2f);
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, activeColor * 0.8f);
            }

            bool clicked = false;

            // Draw button with icon or text
            if (!string.IsNullOrEmpty(button.Icon))
            {
                clicked = Icons.IconManager.IconButton(button.Icon, button.Tooltip);
            }
            else
            {
                clicked = ImGui.Button(button.Label);
                if (ImGui.IsItemHovered() && !string.IsNullOrEmpty(button.Tooltip))
                {
                    ImGui.SetTooltip(button.Tooltip);
                }
            }

            if (button.IsToggle && isActive)
            {
                ImGui.PopStyleColor(3);
            }

            if (clicked)
            {
                button.OnClick?.Invoke();
            }
        }

        private void DrawSeparator()
        {
            var cursorPos = ImGui.GetCursorScreenPos();
            var drawList = ImGui.GetWindowDrawList();

            float height = ImGui.GetFrameHeight();
            var lineColor = ImGui.GetColorU32(new System.Numerics.Vector4(0.4f, 0.4f, 0.4f, 0.5f));

            drawList.AddLine(
                cursorPos + new System.Numerics.Vector2(0, 2),
                cursorPos + new System.Numerics.Vector2(0, height - 2),
                lineColor,
                1.0f
            );

            ImGui.Dummy(new System.Numerics.Vector2(1, height));
        }

        private class ToolbarButton
        {
            public string Label { get; set; } = "";
            public string Tooltip { get; set; } = "";
            public string? Icon { get; set; }
            public bool IsToggle { get; set; }
            public Func<bool>? GetValue { get; set; }
            public bool IsSeparator { get; set; }
            public Action? OnClick { get; set; }
        }
    }
}

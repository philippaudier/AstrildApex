using System;
using ImGuiNET;
using Editor.Rendering;

namespace Editor.UI.Overlays
{
    /// <summary>
    /// Gizmo mode toolbar overlay for ViewportPanel.
    /// Displays mode selection, space, pivot, snap settings.
    /// </summary>
    public static class GizmoToolbarOverlay
    {
        public static void Draw(
            ref ViewportRenderer.GizmoMode mode,
            ref bool localSpace,
            ref bool snapToggle,
            ref float snapMove,
            ref float snapAngle,
            ref float snapScale,
            ref bool showGrid,
            ViewportRenderer? renderer)
        {
            // Gizmo mode toolbar with animated highlight
            ImGui.Text("Mode:");
            ImGui.SameLine();

            float t = (float)(ImGui.GetTime() * 2.0);
            float pulse = 0.5f + 0.5f * MathF.Sin(t);
            var highlight = new System.Numerics.Vector4(1f, 0.8f, 0.2f, 0.35f + 0.25f * pulse);

            var gizmos = new[]
            {
                (icon: "move", tooltip: "Move Tool (W)", gizmoMode: ViewportRenderer.GizmoMode.Translate),
                (icon: "rotate", tooltip: "Rotate Tool (E)", gizmoMode: ViewportRenderer.GizmoMode.Rotate),
                (icon: "scale", tooltip: "Scale Tool (R)", gizmoMode: ViewportRenderer.GizmoMode.Scale)
            };

            for (int i = 0; i < gizmos.Length; i++)
            {
                var (icon, tooltip, gizmoMode) = gizmos[i];
                bool isCurrent = (mode == gizmoMode);

                if (isCurrent) ImGui.PushStyleColor(ImGuiCol.Button, highlight);

                string fullTooltip = isCurrent ? $"{tooltip} - Active" : tooltip;
                if (Icons.IconManager.IconButton(icon, fullTooltip))
                {
                    mode = gizmoMode;
                    renderer?.SetMode(mode);
                }

                if (isCurrent) ImGui.PopStyleColor();
                if (i < gizmos.Length - 1) ImGui.SameLine();
            }

            ImGui.Separator();

            // Grid toggle
            bool grid = renderer != null ? renderer.GridVisible : showGrid;
            if (ImGui.Checkbox("Show Grid", ref grid))
            {
                if (renderer != null) renderer.GridVisible = grid;
                showGrid = grid;
            }

            ImGui.SameLine();

            // Space selection
            ImGui.Text("Space:");
            ImGui.SameLine();
            bool worldSelected = !localSpace;
            if (ImGui.RadioButton("World", worldSelected)) localSpace = false;
            ImGui.SameLine();
            bool localSelected = localSpace;
            if (ImGui.RadioButton("Local", localSelected)) localSpace = true;

            ImGui.Separator();

            // Snap settings
            ImGui.Checkbox("Snap", ref snapToggle);
            ImGui.SameLine();
            ImGui.TextDisabled("(Ctrl)");

            ImGui.PushItemWidth(120);
            ImGui.DragFloat("Unit", ref snapMove, 0.01f, 0.001f, 100f, "%.3f");
            ImGui.DragFloat("Angle", ref snapAngle, 1f, 1f, 90f, "%.0f°");
            ImGui.DragFloat("Scale", ref snapScale, 0.01f, 0.001f, 10f, "%.3f");
            ImGui.PopItemWidth();
        }
    }
}

using System;
using ImGuiNET;

namespace Editor.UI.Overlays
{
    /// <summary>
    /// Pivot mode overlay for ViewportPanel.
    /// Allows switching between Center and Pivot modes for gizmo positioning.
    /// </summary>
    public static class PivotModeOverlay
    {
        public enum PivotMode { Center, Pivot }

        public static void Draw(ref PivotMode pivotMode, Action onPivotModeChanged)
        {
            ImGui.Text("Pivot:");
            ImGui.SameLine();

            bool center = pivotMode == PivotMode.Center;
            if (ImGui.RadioButton("Center", center))
            {
                pivotMode = PivotMode.Center;
                onPivotModeChanged?.Invoke();
            }

            ImGui.SameLine();

            bool pivot = pivotMode == PivotMode.Pivot;
            if (ImGui.RadioButton("Pivot", pivot))
            {
                pivotMode = PivotMode.Pivot;
                onPivotModeChanged?.Invoke();
            }
        }
    }
}

using System;
using ImGuiNET;

namespace Editor.UI.Overlays
{
    /// <summary>
    /// Projection mode overlay for ViewportPanel.
    /// Allows switching between Perspective, Orthographic, and 2D projection.
    /// </summary>
    public static class ProjectionSettingsOverlay
    {
        public enum ProjectionMode { Perspective, Orthographic, TwoD }

        public static void Draw(
            ref ProjectionMode projectionMode,
            ref float orthoSize,
            Editor.Rendering.ViewportRenderer? renderer)
        {
            ImGui.Text("Projection:");
            int projMode = (int)projectionMode;
            if (ImGui.Combo("##Projection", ref projMode, "Perspective\0Orthographic\02D\0"))
            {
                projectionMode = (ProjectionMode)projMode;
                renderer?.SetProjectionMode((int)projectionMode, orthoSize);
            }

            if (projectionMode != ProjectionMode.Perspective)
            {
                ImGui.PushItemWidth(120);
                if (ImGui.DragFloat("Ortho Size", ref orthoSize, 0.1f, 0.1f, 1000f, "%.2f"))
                {
                    renderer?.SetProjectionMode((int)projectionMode, orthoSize);
                }
                ImGui.PopItemWidth();
            }
        }
    }
}

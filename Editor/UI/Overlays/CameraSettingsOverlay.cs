using System;
using ImGuiNET;

namespace Editor.UI.Overlays
{
    /// <summary>
    /// Camera settings overlay for ViewportPanel.
    /// Displays camera movement, physics, and smoothing parameters.
    /// </summary>
    public static class CameraSettingsOverlay
    {
        public static void Draw(
            ref bool showSettings,
            ref float arrowSpeed,
            ref float arrowAcceleration,
            ref float arrowDamping,
            ref float smoothFactor)
        {
            if (ImGui.Button(showSettings ? "Camera Settings ▼" : "Camera Settings ▶"))
            {
                showSettings = !showSettings;
            }

            if (!showSettings) return;

            ImGui.Separator();
            ImGui.PushItemWidth(150);

            ImGui.Text("Movement:");
            ImGui.DragFloat("Arrow Speed", ref arrowSpeed, 0.1f, 0.1f, 10.0f, "%.1f");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Global speed for arrow key movements");

            ImGui.Separator();
            ImGui.Text("Physics:");
            ImGui.DragFloat("Acceleration", ref arrowAcceleration, 0.1f, 0.5f, 20.0f, "%.1f");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Movement acceleration");

            ImGui.DragFloat("Damping", ref arrowDamping, 0.01f, 0.5f, 0.99f, "%.2f");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Braking (closer to 1 = slower stop)");

            ImGui.Separator();
            ImGui.Text("Smoothing:");
            ImGui.DragFloat("Smooth Factor", ref smoothFactor, 0.01f, 0.05f, 1.0f, "%.2f");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Camera smoothing (lower = smoother)");

            ImGui.PopItemWidth();
        }
    }
}

using System;
using System.Numerics;
using ImGuiNET;

namespace Editor.UI;

/// <summary>
/// Top-right controls for ViewportPanel
/// Features: Camera selector, Fullscreen, Settings
/// </summary>
public class ViewportTopRightControls
{
    // Camera selector removed from overlay in favor of persistent Camera settings
    private bool _showSettingsPopup = false;
    public bool IsFullscreen { get; private set; } = false;
    
    /// <summary>
    /// Draw the top-right controls
    /// </summary>
    public void Draw(Vector2 imageMin, Vector2 imageMax, string[] cameraNames, ref int selectedCameraIndex)
    {
        const float margin = 15f;
        const float estimatedWidth = 230f; // Camera group (~120px) + Actions group (~90px) + spacing
        var controlsPos = new Vector2(imageMax.X - margin - estimatedWidth, imageMin.Y + margin);

        // Create a popup window for the controls
        ImGui.SetNextWindowPos(controlsPos);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0, 0, 0, 0));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0, 0, 0, 0));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);

        if (ImGui.Begin("##ViewportTopRightControls",
            ImGuiWindowFlags.NoTitleBar |
            ImGuiWindowFlags.NoResize |
            ImGuiWindowFlags.NoMove |
            ImGuiWindowFlags.AlwaysAutoResize |
            ImGuiWindowFlags.NoSavedSettings |
            ImGuiWindowFlags.NoFocusOnAppearing |
            ImGuiWindowFlags.NoBringToFrontOnFocus))
        {
                // Camera selector overlay removed; show current camera name instead
                ModernUIHelpers.BeginToolbarGroup();
                ImGui.BeginChild("##CameraSelector", new Vector2(130, ModernUIHelpers.ToolbarButtonSize + 16), ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollbar);
                ImGui.Text("Camera");
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(0.9f,0.9f,0.9f,1f), selectedCameraIndex == 0 ? "Main" : "Scene");
                ImGui.EndChild();
                ModernUIHelpers.EndToolbarGroup();
                ImGui.SameLine();

            // Actions group (Fullscreen, Settings)
            DrawActionsGroup();

            ImGui.End();
        }

        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(2);
    }
    
    // Camera selector overlay removed; functionality moved to Rendering Settings panel
    
    private void DrawActionsGroup()
    {
        ModernUIHelpers.BeginToolbarGroup();

        ImGui.BeginChild("##ActionsGroup", new Vector2(90, ModernUIHelpers.ToolbarButtonSize + 16), ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollbar);

        // Fullscreen button
        if (ModernUIHelpers.ToolButton("[ ]", IsFullscreen, "Fullscreen", 32f))
        {
            IsFullscreen = !IsFullscreen;
        }
        ImGui.SameLine();

        // Settings button
        if (ModernUIHelpers.ToolButton("Set", false, "Settings", 40f))
        {
            _showSettingsPopup = !_showSettingsPopup;
        }

        ImGui.EndChild();
        ModernUIHelpers.EndToolbarGroup();
    }
}

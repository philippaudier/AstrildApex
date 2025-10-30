using System;
using System.Numerics;
using ImGuiNET;
using Editor.UI;

namespace AstrildApex.Editor.UI;

public class GamePlayControls
{
    public bool IsPlaying = false;
    public bool IsPaused = false;
    
    public void Draw(Vector2 imageMin, Vector2 imageMax)
    {
        const float margin = 15f;
    const float buttonWidth = 50f;
        
        float totalWidth = buttonWidth * 4 + 8 * 5 + 1f;
        
        var centerX = (imageMin.X + imageMax.X) * 0.5f;
        var controlsPos = new Vector2(centerX - totalWidth * 0.5f, imageMin.Y + margin);
        
        ImGui.SetCursorScreenPos(controlsPos);
        ImGui.PushClipRect(imageMin, imageMax, true);
        
        ModernUIHelpers.BeginToolbarGroup();
        
        if (ModernUIHelpers.ToolButton("Play", IsPlaying && !IsPaused, "Play", buttonWidth))
        {
            IsPlaying = true;
            IsPaused = false;
        }
        
        ImGui.SameLine();
        
        if (ModernUIHelpers.ToolButton("Pause", IsPaused, "Pause", buttonWidth))
        {
            if (IsPlaying) IsPaused = !IsPaused;
        }
        
        ImGui.SameLine();
        
        if (ModernUIHelpers.ToolButton("Step", false, "Step Frame", buttonWidth))
        {
            // Step
        }
        
        ImGui.SameLine();
        ModernUIHelpers.ToolbarSeparator();
        ImGui.SameLine();
        
        if (ModernUIHelpers.ToolButton("Stop", !IsPlaying, "Stop", buttonWidth))
        {
            IsPlaying = false;
            IsPaused = false;
        }
        
        ModernUIHelpers.EndToolbarGroup();
        ImGui.PopClipRect();
    }
}

public class GameTopRightControls
{
    private string[] _resolutions = new[] { "1920x1080", "1280x720", "2560x1440", "3840x2160", "Free" };
    private int _selectedResolution = 0;
    
    public bool ShowStats = true;
    
    public void Draw(Vector2 imageMin, Vector2 imageMax)
    {
        const float margin = 15f;
        const float groupWidth = 250f;
        var controlsPos = new Vector2(imageMax.X - groupWidth - margin, imageMin.Y + margin);
        
        ImGui.SetCursorScreenPos(controlsPos);
        ImGui.PushClipRect(imageMin, imageMax, true);
        
        ModernUIHelpers.BeginToolbarGroup();
        
        ImGui.Text("Resolution");
        ImGui.SameLine();
        
        ImGui.PushItemWidth(90f);
        ImGui.Combo("##Resolution", ref _selectedResolution, _resolutions, _resolutions.Length);
        ImGui.PopItemWidth();
        
        ModernUIHelpers.EndToolbarGroup();
        
        ImGui.SameLine();
        
        ModernUIHelpers.BeginToolbarGroup();
        
        if (ModernUIHelpers.ToolButton("Stats", ShowStats, "Toggle Stats", 45f))
        {
            ShowStats = !ShowStats;
        }
        
        ImGui.SameLine();
        
        if (ModernUIHelpers.ToolButton("[ ]", false, "Fullscreen", 32f))
        {
            // Fullscreen
        }
        
        ModernUIHelpers.EndToolbarGroup();
        
        ImGui.PopClipRect();
    }
}

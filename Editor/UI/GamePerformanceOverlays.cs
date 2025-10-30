using System;
using System.Numerics;
using ImGuiNET;
using Editor.UI;

namespace AstrildApex.Editor.UI;

/// <summary>
/// Performance overlays for GamePanel
/// Includes: Performance stats, Memory, Rendering, Audio
/// </summary>
public class GamePerformanceOverlays
{
    // Performance tracking
    private float _smoothedFPS = 60f;
    private float _smoothedFrameTime = 16.7f;
    private float _cpuUsage = 0.45f;
    private float _gpuUsage = 0.62f;
    
    // Memory tracking
    private float _ramUsage = 2.4f; // GB
    private float _vramUsage = 1.8f; // GB
    private float _gcMemory = 0.2f; // MB
    
    // Rendering stats
    private int _drawCalls = 124;
    private int _batches = 18;
    private int _triangles = 45200;
    private int _vertices = 28600;
    
    // Audio stats
    private int _audioSources = 8;
    private int _activeSources = 3;
    private float _audioVolume = 0.85f;
    
    /// <summary>
    /// Update performance stats
    /// </summary>
    public void UpdateStats(float deltaTime, int drawCalls, int triangles, int vertices)
    {
        // Smooth FPS
        float currentFPS = 1.0f / Math.Max(deltaTime, 0.001f);
        _smoothedFPS = _smoothedFPS * 0.9f + currentFPS * 0.1f;
        _smoothedFrameTime = _smoothedFrameTime * 0.9f + (deltaTime * 1000f) * 0.1f;
        
        // Update rendering stats
        _drawCalls = drawCalls;
        _triangles = triangles;
        _vertices = vertices;
        
        // Simulate CPU/GPU usage (in real implementation, get from profiler)
        _cpuUsage = Math.Clamp(_cpuUsage + (float)(Random.Shared.NextDouble() - 0.5) * 0.05f, 0.2f, 0.9f);
        _gpuUsage = Math.Clamp(_gpuUsage + (float)(Random.Shared.NextDouble() - 0.5) * 0.05f, 0.3f, 0.95f);
        
        // Simulate memory usage
        _ramUsage += (float)(Random.Shared.NextDouble() - 0.5) * 0.01f;
        _vramUsage += (float)(Random.Shared.NextDouble() - 0.5) * 0.01f;
    }
    
    /// <summary>
    /// Draw performance overlay (top-left)
    /// </summary>
    public void DrawPerformanceStats(Vector2 imageMin, Vector2 imageMax)
    {
        if (ModernUIHelpers.BeginOverlayWindow("##Performance", imageMin, imageMax, OverlayPosition.TopLeft, out var pos))
        {
            ModernUIHelpers.OverlayTitle("Performance");
            
            // FPS with colored dot
            ImGui.BeginGroup();
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 0.7f));
            ImGui.Text("FPS:");
            ImGui.PopStyleColor();
            ImGui.SameLine();
            
            // Determine dot color based on FPS
            StatDotColor dotColor = _smoothedFPS >= 55 ? StatDotColor.Green : 
                                   _smoothedFPS >= 30 ? StatDotColor.Yellow : 
                                   StatDotColor.Red;
            ModernUIHelpers.StatBadge(((int)_smoothedFPS).ToString(), dotColor);
            ImGui.EndGroup();
            
            // Frame time
            ModernUIHelpers.OverlayItem("Frame:", _smoothedFrameTime.ToString("F1") + "ms");
            
            ImGui.Spacing();
            
            // CPU bar
            ImGui.BeginGroup();
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 0.7f));
            ImGui.Text("CPU:");
            ImGui.PopStyleColor();
            ModernUIHelpers.PerformanceBar(_cpuUsage);
            ImGui.EndGroup();
            
            // GPU bar
            ImGui.BeginGroup();
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 0.7f));
            ImGui.Text("GPU:");
            ImGui.PopStyleColor();
            ModernUIHelpers.PerformanceBar(_gpuUsage);
            ImGui.EndGroup();
            
            ModernUIHelpers.EndOverlayWindow();
        }
    }
    
    /// <summary>
    /// Draw memory overlay (top-right)
    /// </summary>
    public void DrawMemoryStats(Vector2 imageMin, Vector2 imageMax)
    {
        if (ModernUIHelpers.BeginOverlayWindow("##Memory", imageMin, imageMax, OverlayPosition.TopRight, out var pos))
        {
            ModernUIHelpers.OverlayTitle("Memory");
            
            ModernUIHelpers.OverlayItem("RAM:", _ramUsage.ToString("F1") + " GB");
            ModernUIHelpers.OverlayItem("VRAM:", _vramUsage.ToString("F1") + " GB");
            ModernUIHelpers.OverlayItem("GC:", _gcMemory.ToString("F1") + " MB");
            
            ModernUIHelpers.EndOverlayWindow();
        }
    }
    
    /// <summary>
    /// Draw rendering overlay (bottom-left)
    /// </summary>
    public void DrawRenderingStats(Vector2 imageMin, Vector2 imageMax)
    {
        if (ModernUIHelpers.BeginOverlayWindow("##Rendering", imageMin, imageMax, OverlayPosition.BottomLeft, out var pos))
        {
            ModernUIHelpers.OverlayTitle("Rendering");
            
            ModernUIHelpers.OverlayItem("Draw Calls:", _drawCalls.ToString());
            ModernUIHelpers.OverlayItem("Batches:", _batches.ToString());
            ModernUIHelpers.OverlayItem("Tris:", FormatNumber(_triangles));
            ModernUIHelpers.OverlayItem("Verts:", FormatNumber(_vertices));
            
            ModernUIHelpers.EndOverlayWindow();
        }
    }
    
    /// <summary>
    /// Draw audio overlay (bottom-right)
    /// </summary>
    public void DrawAudioStats(Vector2 imageMin, Vector2 imageMax)
    {
        if (ModernUIHelpers.BeginOverlayWindow("##Audio", imageMin, imageMax, OverlayPosition.BottomRight, out var pos))
        {
            ModernUIHelpers.OverlayTitle("Audio");
            
            ModernUIHelpers.OverlayItem("Sources:", _audioSources.ToString());
            ModernUIHelpers.OverlayItem("Active:", _activeSources.ToString());
            ModernUIHelpers.OverlayItem("Volume:", ((int)(_audioVolume * 100)).ToString() + "%");
            
            ModernUIHelpers.EndOverlayWindow();
        }
    }
    
    /// <summary>
    /// Draw all overlays
    /// </summary>
    public void DrawAll(Vector2 imageMin, Vector2 imageMax)
    {
        DrawPerformanceStats(imageMin, imageMax);
        DrawMemoryStats(imageMin, imageMax);
        DrawRenderingStats(imageMin, imageMax);
        DrawAudioStats(imageMin, imageMax);
    }
    
    private string FormatNumber(int value)
    {
        if (value >= 1000000)
            return (value / 1000000.0).ToString("F1") + "M";
        if (value >= 1000)
            return (value / 1000.0).ToString("F1") + "K";
        return value.ToString();
    }
}

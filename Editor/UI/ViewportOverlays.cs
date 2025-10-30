using System;
using System.Linq;
using System.Numerics;
using ImGuiNET;

namespace Editor.UI;

/// <summary>
/// Modern overlays for ViewportPanel following the HTML design
/// Includes: Scene Info, Transform, Camera Controls, View Options, Gizmo
/// </summary>
public class ViewportOverlays
{
    // View options state
    public bool ShowGrid = true;
    public bool ShowGizmos = true;
    public bool ShowWireframe = false;
    
    // Camera view state
    public CameraView CurrentView { get; set; } = CameraView.Perspective;
    
    /// <summary>
    /// Draw scene info overlay (top-left)
    /// </summary>
    public void DrawSceneInfo(Vector2 imageMin, Vector2 imageMax, int objectCount, int vertexCount, int triangleCount)
    {
        // Draw Scene Info as a foreground draw-list overlay anchored to the viewport
        var drawList = ImGui.GetForegroundDrawList();

        string title = "SCENE INFO";
        string sObjects = $"Objects: {objectCount}";
        string sVerts = $"Vertices: {FormatNumber(vertexCount)}";
        string sTris = $"Triangles: {FormatNumber(triangleCount)}";

        var pad = 12f;
        var lineH = ImGui.GetFontSize();
        var spacing = 6f;

        var szTitle = ImGui.CalcTextSize(title);
        var szObj = ImGui.CalcTextSize(sObjects);
        var szVerts = ImGui.CalcTextSize(sVerts);
        var szTris = ImGui.CalcTextSize(sTris);

        float maxW = MathF.Max(MathF.Max(szTitle.X, szObj.X), MathF.Max(szVerts.X, szTris.X));
        float totalH = szTitle.Y + spacing + lineH * 3 + pad * 2;
        float totalW = maxW + pad * 2;

        // Position top-left inside the viewport, account for toolbar at top
        var desired = new Vector2(imageMin.X + 15f, imageMin.Y + 15f + 60f);

        var minBound = imageMin + new Vector2(8f, 8f);
        var maxBound = imageMax - new Vector2(8f + totalW, 8f + totalH);
        float px = MathF.Max(minBound.X, MathF.Min(desired.X, maxBound.X));
        float py = MathF.Max(minBound.Y, MathF.Min(desired.Y, maxBound.Y));
        var rectMin = new Vector2(px, py);
        var rectMax = rectMin + new Vector2(totalW, totalH);

        uint bgCol = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.6f));
        uint borderCol = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.08f));
        uint textCol = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.9f));
        uint titleCol = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.75f));

        drawList.AddRectFilled(rectMin, rectMax, bgCol, 8f);
        drawList.AddRect(rectMin, rectMax, borderCol, 8f, ImDrawFlags.None, 1f);

        var y = rectMin.Y + pad;
        drawList.AddText(new Vector2(rectMin.X + pad, y), titleCol, title);
        y += szTitle.Y + spacing;

        drawList.AddText(new Vector2(rectMin.X + pad, y), textCol, sObjects);
        y += lineH;
        drawList.AddText(new Vector2(rectMin.X + pad, y), textCol, sVerts);
        y += lineH;
        drawList.AddText(new Vector2(rectMin.X + pad, y), textCol, sTris);
    }
    
    /// <summary>
    /// Draw transform overlay (bottom-left)
    /// </summary>
    public void DrawTransform(Vector2 imageMin, Vector2 imageMax, Vector3 position)
    {
        // Draw Transform info as a foreground draw-list overlay anchored to the viewport (bottom-left)
        var drawList = ImGui.GetForegroundDrawList();

        string title = "TRANSFORM";
        string sx = $"X: {position.X:F2}";
        string sy = $"Y: {position.Y:F2}";
        string sz = $"Z: {position.Z:F2}";

        var pad = 12f;
        var lineH = ImGui.GetFontSize();
        var spacing = 6f;

        var szTitle = ImGui.CalcTextSize(title);
        var ssz = ImGui.CalcTextSize(sx);
        var ssy = ImGui.CalcTextSize(sy);
        var ssz2 = ImGui.CalcTextSize(sz);

        float maxW = MathF.Max(MathF.Max(szTitle.X, ssz.X), MathF.Max(ssy.X, ssz2.X));
        float totalH = szTitle.Y + spacing + lineH * 3 + pad * 2;
        float totalW = maxW + pad * 2;

        var desired = new Vector2(imageMin.X + 15f, imageMax.Y - 15f - totalH - 60f);

        var minBound = imageMin + new Vector2(8f, 8f);
        var maxBound = imageMax - new Vector2(8f + totalW, 8f + totalH);
        float px = MathF.Max(minBound.X, MathF.Min(desired.X, maxBound.X));
        float py = MathF.Max(minBound.Y, MathF.Min(desired.Y, maxBound.Y));
        var rectMin = new Vector2(px, py);
        var rectMax = rectMin + new Vector2(totalW, totalH);

        uint bgCol = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.6f));
        uint borderCol = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.08f));
        uint textCol = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.9f));
        uint titleCol = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.75f));

        drawList.AddRectFilled(rectMin, rectMax, bgCol, 8f);
        drawList.AddRect(rectMin, rectMax, borderCol, 8f, ImDrawFlags.None, 1f);

        var y = rectMin.Y + pad;
        drawList.AddText(new Vector2(rectMin.X + pad, y), titleCol, title);
        y += szTitle.Y + spacing;

        drawList.AddText(new Vector2(rectMin.X + pad, y), textCol, sx);
        y += lineH;
        drawList.AddText(new Vector2(rectMin.X + pad, y), textCol, sy);
        y += lineH;
        drawList.AddText(new Vector2(rectMin.X + pad, y), textCol, sz);
    }
    
    /// <summary>
    /// Draw 3D Triedre (axis indicator) integrated in bottom toolbar
    /// Rotates based on camera yaw and pitch
    /// </summary>
    private void Draw3DTriedre(float cameraYaw, float cameraPitch)
    {
        ModernUIHelpers.BeginToolbarGroup();
        
        const float triedreSize = 60f;
        const float padding = 8f;
        
        // Reserve space for the triedre inside the toolbar group
        var availableSpace = new Vector2(triedreSize + padding * 2, ModernUIHelpers.ToolbarButtonSize);
        ImGui.Dummy(availableSpace);
        
        // Get the drawing area
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var center = (min + max) * 0.5f;
        
        var drawList = ImGui.GetWindowDrawList();
        
        // Calculate 3D projection based on camera rotation
        var (xDir, yDir, zDir) = GetTriedreProjectionFromCamera(cameraYaw, cameraPitch);
        
        // Scale for visibility
        float scale = triedreSize * 0.35f;
        
        // Draw axes from center (ordered by depth for proper Z-sorting)
        var axes = new[] { 
            ('X', xDir, new Vector4(0.96f, 0.34f, 0.42f, 1f)),  // Red
            ('Y', yDir, new Vector4(0.26f, 0.91f, 0.48f, 1f)),  // Green
            ('Z', zDir, new Vector4(0.31f, 0.68f, 1f, 1f))      // Blue
        };
        
        // Sort by Z depth (draw furthest first) - Z is the depth component
        var sortedAxes = axes.OrderBy(a => a.Item2.Z).ToArray(); // Draw furthest (smallest Z) first
        
        foreach (var (label, dir, color) in sortedAxes)
        {
            var end = center + new Vector2(dir.X, dir.Y) * scale;
            var alpha = dir.Z > 0 ? 1f : 0.4f; // Dim axes pointing away
            var dimmedColor = new Vector4(color.X, color.Y, color.Z, color.W * alpha);
            
            drawList.AddLine(center, end, ImGui.ColorConvertFloat4ToU32(dimmedColor), 2.5f);
            drawList.AddCircleFilled(end, 3.5f, ImGui.ColorConvertFloat4ToU32(dimmedColor));
            
            // Draw label
            var labelOffset = label switch
            {
                'X' => new Vector2(6, -6),
                'Y' => new Vector2(-6, -12),
                'Z' => new Vector2(4, 2),
                _ => Vector2.Zero
            };
            drawList.AddText(end + labelOffset, ImGui.ColorConvertFloat4ToU32(dimmedColor), label.ToString());
        }
        
        // Center dot
        drawList.AddCircleFilled(center, 3f, ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 0.6f)));
        
        ModernUIHelpers.EndToolbarGroup();
    }
    
    /// <summary>
    /// Get triedre axis projections based on camera yaw and pitch
    /// Returns (X direction, Y direction, Z direction) as Vector3 (x, y screen coords + z depth)
    /// </summary>
    private (Vector3, Vector3, Vector3) GetTriedreProjectionFromCamera(float yaw, float pitch)
    {
        // World axes
        var worldX = new System.Numerics.Vector3(1, 0, 0);
        var worldY = new System.Numerics.Vector3(0, 1, 0);
        var worldZ = new System.Numerics.Vector3(0, 0, 1);
        
        // Create view matrix from yaw and pitch
        float cosYaw = MathF.Cos(yaw);
        float sinYaw = MathF.Sin(yaw);
        float cosPitch = MathF.Cos(pitch);
        float sinPitch = MathF.Sin(pitch);
        
        // Camera forward direction
        var forward = new System.Numerics.Vector3(
            cosYaw * cosPitch,
            sinPitch,
            sinYaw * cosPitch
        );
        
        // Camera right direction
        var right = new System.Numerics.Vector3(-sinYaw, 0, cosYaw);
        
        // Camera up direction
        var up = System.Numerics.Vector3.Cross(right, forward);
        up = System.Numerics.Vector3.Normalize(up);
        
        // Project world axes onto camera view
        Vector3 ProjectAxis(System.Numerics.Vector3 axis)
        {
            float screenX = -System.Numerics.Vector3.Dot(axis, right); // Negate to match screen orientation
            float screenY = -System.Numerics.Vector3.Dot(axis, up); // Negate for screen coords
            float depth = System.Numerics.Vector3.Dot(axis, forward); // Positive = toward camera
            
            return new Vector3(screenX, screenY, depth);
        }
        
        return (ProjectAxis(worldX), ProjectAxis(worldY), ProjectAxis(worldZ));
    }
    
    /// <summary>
    /// Draw 3D gizmo (kept for backward compatibility, now uses triedre)
    /// </summary>
    public void Draw3DGizmo(Vector2 imageMin, Vector2 imageMax)
    {
        // This method is now deprecated - triedre is drawn in the toolbar
        // Keeping it empty for compatibility
    }
    
    /// <summary>
    /// Draw bottom toolbar (camera controls + triedre aligned right)
    /// </summary>
    public void DrawBottomToolbar(Vector2 imageMin, Vector2 imageMax, float cameraYaw, float cameraPitch)
    {
        const float margin = 15f;
        const float triedreWidth = 76f; // 60 + 8*2 padding
        var toolbarPos = new Vector2(imageMin.X + margin, imageMax.Y - margin - ModernUIHelpers.ToolbarButtonSize - ModernUIHelpers.Spacing * 2);

        // Create a popup window for camera controls
        ImGui.SetNextWindowPos(toolbarPos);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0, 0, 0, 0));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0, 0, 0, 0));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);

        if (ImGui.Begin("##ViewportBottomToolbar",
            ImGuiWindowFlags.NoTitleBar |
            ImGuiWindowFlags.NoResize |
            ImGuiWindowFlags.NoMove |
            ImGuiWindowFlags.AlwaysAutoResize |
            ImGuiWindowFlags.NoSavedSettings |
            ImGuiWindowFlags.NoFocusOnAppearing |
            ImGuiWindowFlags.NoBringToFrontOnFocus))
        {
            // Camera controls group (left side)
            DrawCameraControls();

            ImGui.End();
        }

        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(2);

        // 3D Triedre (right side, positioned from right edge)
        float triedreX = imageMax.X - margin - triedreWidth;
        var triedrePos = new Vector2(triedreX, toolbarPos.Y);

        ImGui.SetNextWindowPos(triedrePos);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0, 0, 0, 0));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0, 0, 0, 0));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);

        if (ImGui.Begin("##ViewportTriedre",
            ImGuiWindowFlags.NoTitleBar |
            ImGuiWindowFlags.NoResize |
            ImGuiWindowFlags.NoMove |
            ImGuiWindowFlags.AlwaysAutoResize |
            ImGuiWindowFlags.NoSavedSettings |
            ImGuiWindowFlags.NoFocusOnAppearing |
            ImGuiWindowFlags.NoBringToFrontOnFocus))
        {
            Draw3DTriedre(cameraYaw, cameraPitch);

            ImGui.End();
        }

        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(2);
    }
    
    private void DrawCameraControls()
    {
        ModernUIHelpers.BeginToolbarGroup();

        ImGui.BeginChild("##CameraControls", new Vector2(370, ModernUIHelpers.ToolbarButtonSize + 16), ImGuiChildFlags.Borders, ImGuiWindowFlags.NoScrollbar);

        // Front (F)
        if (ModernUIHelpers.ToolButton("Front", CurrentView == CameraView.Front, "Front view (F)", 55f))
        {
            CurrentView = CameraView.Front;
        }
        ImGui.SameLine();

        // Right (R)
        if (ModernUIHelpers.ToolButton("Right", CurrentView == CameraView.Right, "Right view (R)", 55f))
        {
            CurrentView = CameraView.Right;
        }
        ImGui.SameLine();

        // Top (T)
        if (ModernUIHelpers.ToolButton("Top", CurrentView == CameraView.Top, "Top view (T)", 45f))
        {
            CurrentView = CameraView.Top;
        }
        ImGui.SameLine();

        // Perspective (P)
        if (ModernUIHelpers.ToolButton("Persp", CurrentView == CameraView.Perspective, "Perspective (P)", 55f))
        {
            CurrentView = CameraView.Perspective;
        }
        ImGui.SameLine();

        // Separator
        ModernUIHelpers.ToolbarSeparator();
        ImGui.SameLine();

        // Frame Selected
        if (ModernUIHelpers.ToolButton("Frame", false, "Frame selected (F)", 55f))
        {
            // Frame selection callback will be handled by ViewportPanelModern
        }

        ImGui.EndChild();
        ModernUIHelpers.EndToolbarGroup();
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

/// <summary>
/// Camera view modes
/// </summary>
public enum CameraView
{
    Front,
    Right,
    Top,
    Perspective
}

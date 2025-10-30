using System;
using System.Numerics;
using ImGuiNET;
using Editor.Rendering;

namespace Editor.UI;

/// <summary>
/// Viewport toolbar following the HTML design exactly
/// Groups: Transform Tools | Snap Tools | Shading Mode
/// </summary>
public class ViewportToolbar
{
    // Transform tools
    public ViewportRenderer.GizmoMode CurrentMode { get; set; } = ViewportRenderer.GizmoMode.Translate;
    
    // Snap tools
    public bool SnapToGrid { get; set; } = false;
    public bool VertexSnap { get; set; } = false;

    // View options
    public bool ShowGrid { get; set; } = true;
    public bool ShowGizmos { get; set; } = true;
    public bool ShowWireframe { get; set; } = false;
    public bool LocalSpace { get; set; } = false;
    public bool VSync { get; set; } = true;

    // Shading mode
    public ShadingMode CurrentShadingMode { get; set; } = ShadingMode.Shaded;

    
    /// <summary>
    /// Draw the complete toolbar - compact design with stats
    /// </summary>
    public void Draw(Vector2 imageMin, Vector2 imageMax, ViewportRenderer? renderer = null, double smoothedMs = 0.0)
    {
        const float margin = 8f;
        var toolbarPos = new Vector2(imageMin.X + margin, imageMin.Y + margin);

        // Set cursor to toolbar position to draw the child window there
        ImGui.SetCursorScreenPos(toolbarPos);

        // Create a styled child window for the toolbar (more compact)
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.12f, 0.12f, 0.12f, 0.90f));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(1f, 1f, 1f, 0.25f));
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 8f);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 1f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(8f, 6f));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4f, 4f));

        ImGui.BeginChild("##ViewportToolbarChild", new Vector2(0, 0), ImGuiChildFlags.AutoResizeX | ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.Borders, ImGuiWindowFlags.NoScrollbar);

        // === Transform Tools Group ===
        DrawTransformTools();

        ImGui.SameLine();
        DrawSeparator();
        ImGui.SameLine();

        // === Snap Tools Group ===
        DrawSnapTools();

        ImGui.SameLine();
        DrawSeparator();
        ImGui.SameLine();

        // === View Options Group ===
        DrawViewOptions();

        ImGui.SameLine();
        DrawSeparator();
        ImGui.SameLine();

        // === Shading Mode Group ===
        DrawShadingMode();

        ImGui.SameLine();
        DrawSeparator();
        ImGui.SameLine();

        // === Stats Group ===
        DrawStats(renderer, smoothedMs);

        ImGui.SameLine();
        DrawSeparator();
        ImGui.SameLine();

        // === Camera Settings Button ===
        ModernUIHelpers.ToolButton("📷", false, "Camera Settings", 30f);

        // Show help tooltip on hover
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(300f);

            ImGui.TextUnformatted("Camera Controls");
            ImGui.Separator();
            ImGui.TextUnformatted("• RMB: Orbit camera");
            ImGui.TextUnformatted("• MMB: Pan camera");
            ImGui.TextUnformatted("• Scroll: Zoom");
            ImGui.TextUnformatted("• Arrow Keys: Navigate");
            ImGui.TextUnformatted("• F: Frame selection");

            ImGui.Separator();
            ImGui.TextUnformatted("Transform Shortcuts");
            ImGui.Separator();
            ImGui.TextUnformatted("• W: Move mode");
            ImGui.TextUnformatted("• E: Rotate mode");
            ImGui.TextUnformatted("• R: Scale mode");

            ImGui.Separator();
            ImGui.TextUnformatted("View Options");
            ImGui.Separator();
            ImGui.TextUnformatted("• V: Toggle VSync");

            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }

        ImGui.EndChild();

        ImGui.PopStyleVar(4);
        ImGui.PopStyleColor(2);
    }
    
    private void DrawTransformTools()
    {
        // Move (W)
        if (ModernUIHelpers.ToolButton("M", CurrentMode == ViewportRenderer.GizmoMode.Translate, "Move (W)", 28f))
        {
            CurrentMode = ViewportRenderer.GizmoMode.Translate;
        }
        ImGui.SameLine();

        // Rotate (E)
        if (ModernUIHelpers.ToolButton("R", CurrentMode == ViewportRenderer.GizmoMode.Rotate, "Rotate (E)", 28f))
        {
            CurrentMode = ViewportRenderer.GizmoMode.Rotate;
        }
        ImGui.SameLine();

        // Scale (R)
        if (ModernUIHelpers.ToolButton("S", CurrentMode == ViewportRenderer.GizmoMode.Scale, "Scale (R)", 28f))
        {
            CurrentMode = ViewportRenderer.GizmoMode.Scale;
        }
    }
    
    private void DrawSnapTools()
    {
        // Snap to Grid
        if (ModernUIHelpers.ToolButton("⊞", SnapToGrid, "Snap to Grid (Ctrl)", 28f))
        {
            SnapToGrid = !SnapToGrid;
        }
        ImGui.SameLine();

        // Vertex Snap
        if (ModernUIHelpers.ToolButton("◉", VertexSnap, "Vertex Snap", 28f))
        {
            VertexSnap = !VertexSnap;
        }
    }

    private void DrawViewOptions()
    {
        // Show Grid
        if (ModernUIHelpers.ToolButton("#", ShowGrid, "Show Grid", 28f))
        {
            ShowGrid = !ShowGrid;
        }
        ImGui.SameLine();

        // Show Gizmos
        if (ModernUIHelpers.ToolButton("⚙", ShowGizmos, "Show Gizmos", 28f))
        {
            ShowGizmos = !ShowGizmos;
        }
        ImGui.SameLine();

        // VSync
        if (ModernUIHelpers.ToolButton("V", VSync, "VSync (V)", 28f))
        {
            VSync = !VSync;
        }
        ImGui.SameLine();

        // Local/World Space
        if (ModernUIHelpers.ToolButton(LocalSpace ? "L" : "W", LocalSpace, "Local/World Space", 28f))
        {
            LocalSpace = !LocalSpace;
        }
    }
    
    private void DrawShadingMode()
    {
        // Shading Mode combo (compact)
        string shadingText = CurrentShadingMode switch
        {
            ShadingMode.Shaded => "Shd",
            ShadingMode.Wireframe => "Wire",
            ShadingMode.Solid => "Sld",
            ShadingMode.Lit => "Lit",
            ShadingMode.Unlit => "Unlt",
            _ => "Shd"
        };

        ImGui.SetNextItemWidth(45f);
        if (ImGui.BeginCombo("##ShadingMode", shadingText, ImGuiComboFlags.NoArrowButton))
        {
            if (ImGui.Selectable("Shaded", CurrentShadingMode == ShadingMode.Shaded))
            {
                CurrentShadingMode = ShadingMode.Shaded;
            }
            if (ImGui.Selectable("Wireframe", CurrentShadingMode == ShadingMode.Wireframe))
            {
                CurrentShadingMode = ShadingMode.Wireframe;
            }
            if (ImGui.Selectable("Solid", CurrentShadingMode == ShadingMode.Solid))
            {
                CurrentShadingMode = ShadingMode.Solid;
            }
            if (ImGui.Selectable("Lit", CurrentShadingMode == ShadingMode.Lit))
            {
                CurrentShadingMode = ShadingMode.Lit;
            }
            if (ImGui.Selectable("Unlit", CurrentShadingMode == ShadingMode.Unlit))
            {
                CurrentShadingMode = ShadingMode.Unlit;
            }

            ImGui.EndCombo();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Shading Mode");
        }
    }
    
    /// <summary>
    /// Draw compact stats (FPS, Tris, Verts)
    /// </summary>
    private void DrawStats(ViewportRenderer? renderer, double smoothedMs)
    {
        float fps = smoothedMs > 0.0 ? (float)(1000.0 / smoothedMs) : 0f;
        int tris = renderer?.TrianglesThisFrame ?? 0;
        int verts = tris * 3; // Approximation

        // Compact format with small font
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.7f, 0.7f, 0.7f, 1f));
        ImGui.TextUnformatted($"{fps:0}fps");
        ImGui.SameLine();
        ImGui.TextUnformatted($"{FormatNumber(tris)}tri");
        ImGui.SameLine();
        ImGui.TextUnformatted($"{FormatNumber(verts)}vtx");
        ImGui.PopStyleColor();
    }

    private string FormatNumber(int value)
    {
        if (value >= 1000000)
            return (value / 1000000.0).ToString("F1") + "M";
        if (value >= 1000)
            return (value / 1000.0).ToString("F1") + "K";
        return value.ToString();
    }

    /// <summary>
    /// Draw a vertical separator
    /// </summary>
    private void DrawSeparator()
    {
        var drawList = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var col = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 0.2f));

        drawList.AddLine(
            new Vector2(pos.X, pos.Y + 4),
            new Vector2(pos.X, pos.Y + ModernUIHelpers.ToolbarButtonSize - 4),
            col,
            1f
        );

        ImGui.Dummy(new Vector2(1f, ModernUIHelpers.ToolbarButtonSize));
    }

    /// <summary>
    /// Process hotkeys (W, E, R, V)
    /// </summary>
    public bool ProcessHotkeys()
    {
        bool changed = false;

        // Transform mode hotkeys
        if (ImGui.IsKeyPressed(ImGuiKey.W))
        {
            CurrentMode = ViewportRenderer.GizmoMode.Translate;
            changed = true;
        }
        if (ImGui.IsKeyPressed(ImGuiKey.E))
        {
            CurrentMode = ViewportRenderer.GizmoMode.Rotate;
            changed = true;
        }
        if (ImGui.IsKeyPressed(ImGuiKey.R))
        {
            CurrentMode = ViewportRenderer.GizmoMode.Scale;
            changed = true;
        }

        // VSync toggle with V key
        if (ImGui.IsKeyPressed(ImGuiKey.V))
        {
            VSync = !VSync;
            changed = true;
        }

        return changed;
    }
}

/// <summary>
/// Shading modes for the viewport
/// </summary>
public enum ShadingMode
{
    Shaded,
    Wireframe,
    Solid,
    Lit,
    Unlit
}

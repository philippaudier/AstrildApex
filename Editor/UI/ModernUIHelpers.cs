using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using Editor.Themes;

namespace Editor.UI;

/// <summary>
/// Modern UI helpers for ViewportPanel and GamePanel following the HTML design
/// Features: glassmorphism, backdrop blur effects, rounded corners, hover states
/// </summary>
public static class ModernUIHelpers
{
    // === Color Constants (matching HTML design) ===
    private static readonly Vector4 ToolbarBg = new Vector4(0f, 0f, 0f, 0.7f); // Fond noir semi-transparent
    private static readonly Vector4 ToolbarBorder = new Vector4(1f, 1f, 1f, 0.3f);
    private static readonly Vector4 ButtonBg = new Vector4(1f, 1f, 1f, 0.05f);
    private static readonly Vector4 ButtonBgHover = new Vector4(1f, 1f, 1f, 0.15f);
    private static readonly Vector4 ButtonBorder = new Vector4(1f, 1f, 1f, 0.1f);
    private static readonly Vector4 ButtonBorderActive = new Vector4(1f, 1f, 1f, 0.3f);
    private static readonly Vector4 ActiveGradientStart = new Vector4(0.4f, 0.5f, 0.92f, 1f); // #667eea
    private static readonly Vector4 ActiveGradientEnd = new Vector4(0.46f, 0.3f, 0.64f, 1f);  // #764ba2
    private static readonly Vector4 SeparatorColor = new Vector4(1f, 1f, 1f, 0.2f);
    
    // === Sizing Constants ===
    public const float ToolbarButtonSize = 36f;
    public const float IconButtonSize = 28f;
    public const float CamButtonSize = 32f;
    public const float PlayButtonSize = 40f;
    public const float ToolbarRounding = 12f;
    public const float ButtonRounding = 8f;
    public const float Spacing = 8f;
    public const float SmallSpacing = 4f;
    
    /// <summary>
    /// Begin a toolbar group with glassmorphism effect
    /// </summary>
    public static void BeginToolbarGroup()
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg, ToolbarBg);
        ImGui.PushStyleColor(ImGuiCol.Border, ToolbarBorder);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, ToolbarRounding);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 1f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(Spacing, Spacing));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(SmallSpacing, SmallSpacing));
    }
    
    /// <summary>
    /// End a toolbar group
    /// </summary>
    public static void EndToolbarGroup()
    {
        ImGui.PopStyleVar(4);
        ImGui.PopStyleColor(2);
    }
    
    /// <summary>
    /// Draw a modern tool button with icon
    /// </summary>
    public static bool ToolButton(string icon, bool isActive, string tooltip = "", float size = ToolbarButtonSize)
    {
        var io = ImGui.GetIO();
        bool clicked = false;
        
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, ButtonRounding);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(8, 6)); // Better padding for text buttons
        
        if (isActive)
        {
            // Active state: gradient background
            ImGui.PushStyleColor(ImGuiCol.Button, ActiveGradientStart);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ActiveGradientStart);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, ActiveGradientEnd);
            ImGui.PushStyleColor(ImGuiCol.Border, ButtonBorderActive);
        }
        else
        {
            ImGui.PushStyleColor(ImGuiCol.Button, ButtonBg);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ButtonBgHover);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, ButtonBgHover);
            ImGui.PushStyleColor(ImGuiCol.Border, ButtonBorder);
        }
        
        clicked = ImGui.Button(icon, new Vector2(size, ToolbarButtonSize));
        
        ImGui.PopStyleColor(4);
        ImGui.PopStyleVar(3);
        
        if (!string.IsNullOrEmpty(tooltip) && ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.Text(tooltip);
            ImGui.EndTooltip();
        }
        
        return clicked;
    }
    
    /// <summary>
    /// Draw a vertical separator
    /// </summary>
    public static void ToolbarSeparator(float height = ToolbarButtonSize)
    {
        var drawList = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var col = ImGui.ColorConvertFloat4ToU32(SeparatorColor);
        
        drawList.AddLine(
            new Vector2(pos.X + 2, pos.Y + SmallSpacing),
            new Vector2(pos.X + 2, pos.Y + height - SmallSpacing),
            col,
            1f
        );
        
        ImGui.Dummy(new Vector2(SmallSpacing, height));
    }
    
    /// <summary>
    /// Begin a control group (like Camera selector, etc.)
    /// </summary>
    public static void BeginControlGroup()
    {
        BeginToolbarGroup();
    }
    
    /// <summary>
    /// End a control group
    /// </summary>
    public static void EndControlGroup()
    {
        EndToolbarGroup();
    }
    
    /// <summary>
    /// Draw a small icon button
    /// </summary>
    public static bool IconButton(string icon, string tooltip = "", float size = IconButtonSize)
    {
        return ToolButton(icon, false, tooltip, size);
    }
    
    /// <summary>
    /// Draw an overlay window at specified corner
    /// </summary>
    public static bool BeginOverlayWindow(string id, Vector2 imageMin, Vector2 imageMax, OverlayPosition position, out Vector2 windowPos)
    {
        const float margin = 15f;
        const float overlayAlpha = 0.6f;
        
        var overlayBg = new Vector4(0f, 0f, 0f, overlayAlpha);
        var overlayBorder = new Vector4(1f, 1f, 1f, 0.2f);
        
        ImGui.PushStyleColor(ImGuiCol.WindowBg, overlayBg);
        ImGui.PushStyleColor(ImGuiCol.Border, overlayBorder);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 10f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(15, 18));
        
        // Calculate position based on overlay position
        windowPos = position switch
        {
            OverlayPosition.TopLeft => new Vector2(imageMin.X + margin, imageMin.Y + margin + 60), // +60 for toolbar
            OverlayPosition.TopRight => new Vector2(imageMax.X - 200 - margin, imageMin.Y + margin + 60),
            OverlayPosition.BottomLeft => new Vector2(imageMin.X + margin, imageMax.Y - 150 - margin - 60), // -60 for bottom toolbar
            OverlayPosition.BottomRight => new Vector2(imageMax.X - 200 - margin, imageMax.Y - 150 - margin - 60),
            _ => imageMin
        };
        
        ImGui.SetNextWindowPos(windowPos, ImGuiCond.Always);
        bool open = ImGui.Begin(id, ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize | 
                                ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoCollapse | 
                                ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoMove);
        
        return open;
    }
    
    /// <summary>
    /// End an overlay window
    /// </summary>
    public static void EndOverlayWindow()
    {
        ImGui.End();
        ImGui.PopStyleVar(3);
        ImGui.PopStyleColor(2);
    }
    
    /// <summary>
    /// Draw overlay title
    /// </summary>
    public static void OverlayTitle(string title)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 0.7f));
        var oldFont = ImGui.GetFont();
        ImGui.PushFont(oldFont); // Use smaller font if available
        ImGui.TextUnformatted(title.ToUpperInvariant());
        ImGui.PopFont();
        ImGui.PopStyleColor();
        ImGui.Spacing();
    }
    
    /// <summary>
    /// Draw overlay item (label + value)
    /// </summary>
    public static void OverlayItem(string label, string value)
    {
        ImGui.BeginGroup();
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 0.7f));
        ImGui.TextUnformatted(label);
        ImGui.PopStyleColor();
        ImGui.SameLine();
        ModernUIHelpers.Spring(1f);
        ImGui.PushFont(ImGui.GetIO().Fonts.Fonts[0]); // Monospace if available
        ImGui.TextUnformatted(value);
        ImGui.PopFont();
        ImGui.EndGroup();
    }
    
    /// <summary>
    /// Draw a performance bar (CPU/GPU usage)
    /// </summary>
    public static void PerformanceBar(float percentage, float width = 120f, float height = 4f)
    {
        var drawList = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        
        // Background
        var bgCol = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 0.1f));
        drawList.AddRectFilled(pos, new Vector2(pos.X + width, pos.Y + height), bgCol, 2f);
        
        // Fill
        percentage = Math.Clamp(percentage, 0f, 1f);
        Vector4 fillColor;
        if (percentage < 0.5f)
            fillColor = new Vector4(0.26f, 0.91f, 0.48f, 1f); // Green
        else if (percentage < 0.75f)
            fillColor = new Vector4(1f, 0.88f, 0.25f, 1f); // Yellow
        else
            fillColor = new Vector4(0.96f, 0.34f, 0.42f, 1f); // Red
        
        var fillCol = ImGui.ColorConvertFloat4ToU32(fillColor);
        drawList.AddRectFilled(pos, new Vector2(pos.X + width * percentage, pos.Y + height), fillCol, 2f);
        
        ImGui.Dummy(new Vector2(width, height + 4));
    }
    
    /// <summary>
    /// Draw a stat badge with colored dot
    /// </summary>
    public static void StatBadge(string value, StatDotColor dotColor = StatDotColor.Green)
    {
        var badgeBg = new Vector4(1f, 1f, 1f, 0.1f);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, badgeBg);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(8, 3));
        
        ImGui.BeginChild("##badge" + value, new Vector2(0, 20), ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollbar);
        
        // Draw dot
        var drawList = ImGui.GetWindowDrawList();
        var dotPos = ImGui.GetCursorScreenPos() + new Vector2(0, 6);
        var dotCol = dotColor switch
        {
            StatDotColor.Green => ImGui.ColorConvertFloat4ToU32(new Vector4(0.26f, 0.91f, 0.48f, 1f)),
            StatDotColor.Yellow => ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.88f, 0.25f, 1f)),
            StatDotColor.Red => ImGui.ColorConvertFloat4ToU32(new Vector4(0.96f, 0.34f, 0.42f, 1f)),
            _ => ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 1f))
        };
        drawList.AddCircleFilled(new Vector2(dotPos.X + 3, dotPos.Y), 3f, dotCol);
        
        ImGui.Dummy(new Vector2(10, 12));
        ImGui.SameLine();
        ImGui.TextUnformatted(value);
        
        ImGui.EndChild();
        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor();
    }
    
    /// <summary>
    /// Helper to add spacing between items
    /// </summary>
    public static void Spring(float amount = 1f)
    {
        ImGui.Dummy(new Vector2(amount * 15f, 0));
        ImGui.SameLine();
    }
    
    /// <summary>
    /// Add horizontal spacing
    /// </summary>
    public static void HSpace(float width = Spacing)
    {
        ImGui.SameLine();
        ImGui.Dummy(new Vector2(width, 0));
        ImGui.SameLine();
    }
    
    /// <summary>
    /// Add vertical spacing
    /// </summary>
    public static void VSpace(float height = Spacing)
    {
        ImGui.Dummy(new Vector2(0, height));
    }
    
    /// <summary>
    /// Begin a horizontal flex layout with proper spacing
    /// </summary>
    public static void BeginHorizontalLayout(float spacing = Spacing)
    {
        ImGui.BeginGroup();
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(spacing, spacing));
    }
    
    /// <summary>
    /// End a horizontal flex layout
    /// </summary>
    public static void EndHorizontalLayout()
    {
        ImGui.PopStyleVar();
        ImGui.EndGroup();
    }
    
    /// <summary>
    /// Add padding around content
    /// </summary>
    public static void BeginPadded(float padding)
    {
        var pos = ImGui.GetCursorPos();
        ImGui.SetCursorPos(new Vector2(pos.X + padding, pos.Y + padding));
        ImGui.BeginGroup();
    }
    
    /// <summary>
    /// End padded content
    /// </summary>
    public static void EndPadded(float padding)
    {
        ImGui.EndGroup();
        var pos = ImGui.GetCursorPos();
        ImGui.SetCursorPos(new Vector2(pos.X, pos.Y + padding));
    }
    
    // === Flexbox-like Responsive Layout System ===
    
    private static float _flexContainerWidth;
    private static List<(Action drawAction, int priority, float minWidth)> _flexItems = new();
    
    /// <summary>
    /// Begin a flex container (like CSS flexbox)
    /// </summary>
    public static void BeginFlexContainer(float availableWidth)
    {
        _flexContainerWidth = availableWidth;
        _flexItems.Clear();
        ImGui.BeginGroup();
    }
    
    /// <summary>
    /// Add a flex item to the container
    /// Items with lower priority are hidden first when space is tight
    /// </summary>
    public static void FlexItem(Action drawAction, int priority = 0, float minWidth = 0f)
    {
        _flexItems.Add((drawAction, priority, minWidth));
    }
    
    /// <summary>
    /// End flex container and render items responsively
    /// </summary>
    public static void EndFlexContainer()
    {
        // Sort items by priority (0 = always visible, higher = hide first)
        var sortedItems = _flexItems.OrderBy(x => x.priority).ToList();
        
        bool firstItem = true;
        foreach (var item in sortedItems)
        {
            // Check if we should render this item based on available width
            if (item.minWidth > 0 && _flexContainerWidth < item.minWidth)
            {
                continue; // Skip items that don't fit
            }
            
            if (!firstItem)
            {
                ImGui.SameLine();
            }
            
            item.drawAction();
            firstItem = false;
        }
        
        ImGui.EndGroup();
    }
}

/// <summary>
/// Overlay position
/// </summary>
public enum OverlayPosition
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}

/// <summary>
/// Stat dot color for performance indicators
/// </summary>
public enum StatDotColor
{
    Green,
    Yellow,
    Red
}

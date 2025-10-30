using ImGuiNET;
using System.Numerics;

// For accessing the main viewport's renderer texture when available
using Editor.Panels;

namespace Editor.UI;

/// <summary>
/// Displays a centered modal popup with a progress bar and descriptive text.
/// Used for long-running operations like loading assets, compiling shaders, etc.
/// Modern AAA-style design with smooth animations and professional appearance.
/// </summary>
public class ProgressPopup
{
    private bool _isOpen = false;
    private string _title = "Loading...";
    private string _description = "";
    private float _progress = 0f; // 0.0 to 1.0
    private Vector2 _popupSize = new Vector2(520, 180);
    private float _animationTime = 0f;

    /// <summary>
    /// Shows the progress popup with the given title and initial description.
    /// </summary>
    public void Show(string title, string description = "")
    {
        _isOpen = true;
        _title = title;
        _description = description;
        _progress = 0f;
    }

    /// <summary>
    /// Updates the progress value (0.0 to 1.0) and optionally the description text.
    /// </summary>
    public void Update(float progress, string? description = null)
    {
        _progress = Math.Clamp(progress, 0f, 1f);
        if (description != null)
        {
            _description = description;
        }
    }

    /// <summary>
    /// Hides the progress popup.
    /// </summary>
    public void Hide()
    {
        _isOpen = false;
    }

    /// <summary>
    /// Returns true if the popup is currently visible.
    /// </summary>
    public bool IsVisible => _isOpen;

    /// <summary>
    /// Renders the popup. Call this every frame from your main render loop.
    /// Modern AAA-style design with animations and professional appearance.
    /// </summary>
    public void Render()
    {
        if (!_isOpen) return;

        // Update animation
        _animationTime += ImGui.GetIO().DeltaTime;

        var viewport = ImGui.GetMainViewport();
        var drawList = ImGui.GetForegroundDrawList(viewport);

        // ===== MODAL OVERLAY - Blur the background if possible =====
        // Fast fallback blur: sample the main viewport color texture multiple times
        // with small offsets and alpha to simulate a blur. This is cheap and avoids
        // adding a full-screen blur shader or extra FBOs.
        bool drewBlur = false;
        try
        {
            var renderer = EditorUI.MainViewport.Renderer;
            if (renderer != null && renderer.ColorTexture != 0)
            {
                var texId = (IntPtr)renderer.ColorTexture;
                var pos = viewport.Pos;
                var size = viewport.Size;

                // We'll draw the viewport texture scaled to cover the whole window.
                // UV coords: (0,1) -> (1,0) because texture is flipped vertically when used with ImGui.Image
                var uv0 = new Vector2(0, 1);
                var uv1 = new Vector2(1, 0);

                // Draw several passes with offsets and low alpha to simulate blur
                // Center pass (base)
                uint baseCol = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.55f));
                drawList.AddImage(texId, pos, new Vector2(pos.X + size.X, pos.Y + size.Y), uv0, uv1, baseCol);

                // Additional passes
                var offsets = new Vector2[] {
                    new Vector2(-4, 0), new Vector2(4, 0), new Vector2(0, -4), new Vector2(0, 4),
                    new Vector2(-2, -2), new Vector2(2, 2)
                };
                uint passCol = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.09f));
                foreach (var off in offsets)
                {
                    var p0 = new Vector2(pos.X + off.X, pos.Y + off.Y);
                    var p1 = new Vector2(p0.X + size.X, p0.Y + size.Y);
                    drawList.AddImage(texId, p0, p1, uv0, uv1, passCol);
                }

                // Slight dim on top to match previous overlay darkness
                drawList.AddRectFilled(pos, new Vector2(pos.X + size.X, pos.Y + size.Y), ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.15f)));

                drewBlur = true;
            }
        }
        catch { drewBlur = false; }

        if (!drewBlur)
        {
            // Fallback to the original semi-transparent black overlay
            drawList.AddRectFilled(
                viewport.Pos,
                new Vector2(viewport.Pos.X + viewport.Size.X, viewport.Pos.Y + viewport.Size.Y),
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.6f)) // Semi-transparent black overlay
            );
        }

        // Center the popup on the screen
        var center = new Vector2(viewport.Size.X / 2, viewport.Size.Y / 2);
        ImGui.SetNextWindowPos(center, ImGuiCond.Always, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(_popupSize, ImGuiCond.Always);

        // Modal popup style - disable window decorations and make it appear on top
        var flags = ImGuiWindowFlags.NoTitleBar
                  | ImGuiWindowFlags.NoResize
                  | ImGuiWindowFlags.NoMove
                  | ImGuiWindowFlags.NoCollapse
                  | ImGuiWindowFlags.NoScrollbar
                  | ImGuiWindowFlags.NoSavedSettings
                  | ImGuiWindowFlags.NoDocking;

        // Modern AAA styling
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 12f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(30, 30));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1f);

        // Dark modern background with subtle transparency
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.08f, 0.08f, 0.10f, 0.98f));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.25f, 0.25f, 0.30f, 0.8f));

        // Force this window to be on top
        ImGui.SetNextWindowFocus();

        if (ImGui.Begin("##ProgressPopup", flags))
        {
            var windowDrawList = ImGui.GetWindowDrawList();
            var windowPos = ImGui.GetWindowPos();
            var contentWidth = _popupSize.X - 60f; // Account for padding

            // ===== TITLE =====
            var titleSize = ImGui.CalcTextSize(_title);

            // Create invisible dummy to reserve space for title
            ImGui.Dummy(new Vector2(contentWidth, titleSize.Y + 15f));

            var titlePosX = windowPos.X + (_popupSize.X - titleSize.X) * 0.5f;
            var titlePosY = windowPos.Y + 30f;

            // Title with subtle shadow
            windowDrawList.AddText(new Vector2(titlePosX + 1, titlePosY + 1),
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.4f)), _title);
            windowDrawList.AddText(new Vector2(titlePosX, titlePosY),
                ImGui.GetColorU32(new Vector4(0.95f, 0.95f, 1.0f, 1f)), _title);

            // ===== PROGRESS BAR =====
            var progressBarHeight = 32f;

            // Create invisible dummy to reserve space for progress bar
            ImGui.Dummy(new Vector2(contentWidth, progressBarHeight));

            var progressBarY = ImGui.GetCursorPosY() - progressBarHeight; // Get position AFTER dummy
            var progressBarPos = new Vector2(windowPos.X + 30f, windowPos.Y + progressBarY);
            var progressBarSize = new Vector2(contentWidth, progressBarHeight);

            // Background of progress bar (darker with border)
            var barBgColor = ImGui.GetColorU32(new Vector4(0.12f, 0.12f, 0.14f, 1f));
            var barBorderColor = ImGui.GetColorU32(new Vector4(0.2f, 0.2f, 0.25f, 1f));
            windowDrawList.AddRectFilled(progressBarPos,
                new Vector2(progressBarPos.X + progressBarSize.X, progressBarPos.Y + progressBarSize.Y),
                barBgColor, 6f);
            windowDrawList.AddRect(progressBarPos,
                new Vector2(progressBarPos.X + progressBarSize.X, progressBarPos.Y + progressBarSize.Y),
                barBorderColor, 6f, ImDrawFlags.None, 1.5f);

            // Filled progress bar with gradient effect
            if (_progress > 0.01f)
            {
                var filledWidth = contentWidth * _progress;

                // Subtle pulse animation
                var pulseIntensity = (float)Math.Sin(_animationTime * 3f) * 0.08f + 0.92f;

                // Modern blue gradient
                var gradientStart = new Vector4(0.15f, 0.5f, 0.95f, 1f) * pulseIntensity;
                var gradientEnd = new Vector4(0.3f, 0.65f, 1.0f, 1f) * pulseIntensity;

                windowDrawList.AddRectFilledMultiColor(
                    new Vector2(progressBarPos.X + 2, progressBarPos.Y + 2),
                    new Vector2(progressBarPos.X + filledWidth - 2, progressBarPos.Y + progressBarHeight - 2),
                    ImGui.GetColorU32(gradientStart),
                    ImGui.GetColorU32(gradientEnd),
                    ImGui.GetColorU32(gradientEnd),
                    ImGui.GetColorU32(gradientStart));

                // Glossy highlight on top
                var highlightHeight = progressBarHeight * 0.4f;
                windowDrawList.AddRectFilledMultiColor(
                    new Vector2(progressBarPos.X + 2, progressBarPos.Y + 2),
                    new Vector2(progressBarPos.X + filledWidth - 2, progressBarPos.Y + highlightHeight),
                    ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.15f)),
                    ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.15f)),
                    ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.0f)),
                    ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.0f)));
            }

            // Progress percentage text CENTERED in the bar
            var percentText = $"{(int)(_progress * 100)}%";
            var percentSize = ImGui.CalcTextSize(percentText);
            var percentPos = new Vector2(
                progressBarPos.X + (progressBarSize.X - percentSize.X) * 0.5f,
                progressBarPos.Y + (progressBarHeight - percentSize.Y) * 0.5f
            );

            // Text shadow for better readability
            windowDrawList.AddText(new Vector2(percentPos.X + 1, percentPos.Y + 1),
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.7f)), percentText);
            windowDrawList.AddText(percentPos,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f)), percentText);

            // Space after progress bar
            ImGui.Dummy(new Vector2(contentWidth, 20f));

            // ===== DESCRIPTION =====
            if (!string.IsNullOrEmpty(_description))
            {
                var descSize = ImGui.CalcTextSize(_description);

                // Create invisible dummy to reserve space for description
                ImGui.Dummy(new Vector2(contentWidth, descSize.Y));

                var descPosX = windowPos.X + (_popupSize.X - descSize.X) * 0.5f;
                var descPosY = windowPos.Y + ImGui.GetCursorPosY() - descSize.Y;

                // Description with subtle fade effect
                var descAlpha = 0.85f + (float)Math.Sin(_animationTime * 2f) * 0.1f;
                windowDrawList.AddText(new Vector2(descPosX, descPosY),
                    ImGui.GetColorU32(new Vector4(0.7f, 0.7f, 0.8f, descAlpha)), _description);
            }

            ImGui.End();
        }

        ImGui.PopStyleColor(2);
        ImGui.PopStyleVar(3);
    }
}

using System;
using ImGuiNET;

namespace Editor.Utils
{
    public static class PerfOverlay
    {
        // Draw a compact performance HUD inside the provided image rect (imgMin,imgMax).
        // The HUD is drawn anchored bottom-left and includes FPS/ms, drawcalls, tris, objects.
        public static void Draw(System.Numerics.Vector2 imgMin, System.Numerics.Vector2 imgMax, float smoothedMs, int drawCalls, int tris, int objects, ref bool showOverlay)
        {
            // Draw on the foreground draw list so the overlay is anchored to the
            // provided image rect (imgMin/imgMax) rather than to the currently
            // active ImGui window. This prevents the HUD from appearing inside
            // other ImGui windows (eg. stray "Debug" windows).
            var drawList = ImGui.GetForegroundDrawList();

            float fpsVal = smoothedMs > 0 ? 1000.0f / smoothedMs : 0.0f;
            string sFps = $"FPS: {fpsVal:0.0}";
            string sMs = $"ms: {smoothedMs:0.0}";
            string sDraw = $"DrawCalls: {drawCalls}";
            string sTris = $"Tris: {tris}";
            string sObj = $"Objects: {objects}";

            var szFps = ImGui.CalcTextSize(sFps);
            var szMs = ImGui.CalcTextSize(sMs);
            var szDraw = ImGui.CalcTextSize(sDraw);
            var szTris = ImGui.CalcTextSize(sTris);
            var szObj = ImGui.CalcTextSize(sObj);

            float pad = 6.0f;
            float maxW = MathF.Max(MathF.Max(MathF.Max(MathF.Max(szFps.X, szMs.X), szDraw.X), szTris.X), szObj.X);
            float lineH = MathF.Max(szFps.Y, ImGui.GetFontSize());
            float totalH = lineH * 5 + pad * 2 + 4.0f;
            float totalW = maxW + pad * 2;

            // Desired position anchored to bottom-left of the image rect
            var desired = new System.Numerics.Vector2(imgMin.X + 8, imgMax.Y - 8 - totalH);

            // Clamp position to stay within the provided image rect (viewport)
            var minBound = imgMin + new System.Numerics.Vector2(8, 8);
            var maxBound = imgMax - new System.Numerics.Vector2(8 + totalW, 8 + totalH);
            float posX = MathF.Max(minBound.X, MathF.Min(desired.X, maxBound.X));
            float posY = MathF.Max(minBound.Y, MathF.Min(desired.Y, maxBound.Y));
            var rectMin = new System.Numerics.Vector2(posX, posY);
            var rectMax = rectMin + new System.Numerics.Vector2(totalW, totalH);

            uint bgCol = ImGui.GetColorU32(new System.Numerics.Vector4(0f, 0f, 0f, 0.6f));
            uint borderCol = ImGui.GetColorU32(new System.Numerics.Vector4(1f, 1f, 1f, 0.08f));
            drawList.AddRectFilled(rectMin, rectMax, bgCol, 6.0f);
            drawList.AddRect(rectMin, rectMax, borderCol, 6.0f, ImDrawFlags.None, 1.0f);

            // Toggle overlay when user clicks inside the small top-left area of the HUD
            var mouse = ImGui.GetMousePos();
            bool clickedInToggleArea = mouse.X >= rectMin.X + pad && mouse.X <= rectMin.X + pad + 36
                                     && mouse.Y >= rectMin.Y + pad && mouse.Y <= rectMin.Y + pad + lineH
                                     && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
            if (clickedInToggleArea) showOverlay = !showOverlay;

            if (showOverlay)
            {
                var textCol = ImGui.GetColorU32(new System.Numerics.Vector4(1f, 1f, 1f, 1f));
                float y = rectMin.Y + pad;
                drawList.AddText(new System.Numerics.Vector2(rectMin.X + pad, y), textCol, sFps);
                y += lineH;
                drawList.AddText(new System.Numerics.Vector2(rectMin.X + pad, y), textCol, sMs);
                y += lineH;
                drawList.AddText(new System.Numerics.Vector2(rectMin.X + pad, y), textCol, sDraw);
                y += lineH;
                drawList.AddText(new System.Numerics.Vector2(rectMin.X + pad, y), textCol, sTris);
                y += lineH;
                drawList.AddText(new System.Numerics.Vector2(rectMin.X + pad, y), textCol, sObj);
            }
        }
    }
}

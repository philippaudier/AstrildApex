using System;
using System.Numerics;
using ImGuiNET;
using Editor.Rendering;

namespace Editor.UI
{
    /// <summary>
    /// Compact triedre overlay drawn in the top-right of the Scene panel.
    /// Renders a small circular widget with projected X/Y/Z axes based on the viewport camera.
    /// </summary>
    public static class TriedreOverlay
    {
        public static void Draw(Vector2 imgMin, Vector2 imgMax, ViewportRenderer? renderer)
        {
            if (renderer == null) return;

            const float margin = 8f;
            // Increase size so the trièdre is easier to read
            const float size = 88f;

            // Anchor bottom-right inside scene image rect
            var pos = new Vector2(imgMax.X - margin - size, imgMax.Y - margin - size);
            var center = pos + new Vector2(size * 0.5f, size * 0.5f);

            // Use WindowDrawList to draw only in the current window (Scene panel)
            var drawList = ImGui.GetWindowDrawList();

            // Filled circular badge only (no outer border) so the triedre stands out
            float radius = size * 0.5f - 4f;
            drawList.AddCircleFilled(center, radius, ImGui.ColorConvertFloat4ToU32(new Vector4(0.12f, 0.12f, 0.12f, 0.95f)));

            // Build camera basis (use renderer's orbital state if available)
            // Query orbit camera state from renderer to extract yaw/pitch
            float yaw = 0f, pitch = 0f;
            try
            {
                var st = renderer.GetOrbitCameraState();
                yaw = st.Yaw;
                pitch = st.Pitch;
            }
            catch { }

            float cosYaw = MathF.Cos(yaw);
            float sinYaw = MathF.Sin(yaw);
            float cosPitch = MathF.Cos(pitch);
            float sinPitch = MathF.Sin(pitch);

            var forward = new System.Numerics.Vector3(cosYaw * cosPitch, sinPitch, sinYaw * cosPitch);
            var right = new System.Numerics.Vector3(-sinYaw, 0, cosYaw);
            var up = System.Numerics.Vector3.Cross(right, forward);
            up = System.Numerics.Vector3.Normalize(up);

            // World axes
            var worldX = new System.Numerics.Vector3(1, 0, 0);
            var worldY = new System.Numerics.Vector3(0, 1, 0);
            var worldZ = new System.Numerics.Vector3(0, 0, 1);

            Vector2 ProjectAxis(System.Numerics.Vector3 axis)
            {
                float screenX = System.Numerics.Vector3.Dot(axis, right);
                float screenY = -System.Numerics.Vector3.Dot(axis, up);
                return new Vector2(screenX, screenY);
            }

            // Invert X and Z: draw X using world Z projection and Z using world X projection
            var xP = ProjectAxis(worldZ);
            var yP = ProjectAxis(worldY);
            var zP = ProjectAxis(worldX);

            // Reduce axis length slightly so labels remain inside the circle
            float axisLen = size * 0.33f;

            // Draw axes. Color scheme: X red, Y green, Z blue
            drawList.AddLine(center, center + xP * axisLen, ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.3f, 0.3f, 1f)), 3f);
            drawList.AddLine(center, center + yP * axisLen, ImGui.ColorConvertFloat4ToU32(new Vector4(0.3f, 1f, 0.3f, 1f)), 3f);
            drawList.AddLine(center, center + zP * axisLen, ImGui.ColorConvertFloat4ToU32(new Vector4(0.3f, 0.3f, 1f, 1f)), 3f);

            // Labels moved closer to axes to stay inside the badge
            float labelOffset = axisLen + 3f;
            drawList.AddText(center + xP * labelOffset, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0.6f, 0.6f, 1)), "X");
            drawList.AddText(center + yP * labelOffset, ImGui.ColorConvertFloat4ToU32(new Vector4(0.6f, 1, 0.6f, 1)), "Y");
            drawList.AddText(center + zP * labelOffset, ImGui.ColorConvertFloat4ToU32(new Vector4(0.6f, 0.6f, 1, 1)), "Z");
        }
    }
}

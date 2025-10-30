using ImGuiNET;
using System.Numerics;

namespace Editor.UI
{
    /// <summary>
    /// Small UI helper utilities to standardize wrapping and default widths across panels.
    /// Usage: call UIHelpers.BeginWindowDefaults() immediately after ImGui.Begin(...) and
    /// UIHelpers.EndWindowDefaults() just before ImGui.End(). This will push a text wrap
    /// position equal to the current content region width so long descriptive texts are
    /// wrapped automatically. Additional helpers can be added later (default item widths).
    /// </summary>
    public static class UIHelpers
    {
        private static int _pushCount = 0;

        public static void BeginWindowDefaults()
        {
            // Set text wrap position to the available content region width (local coordinate)
            // We compute an absolute wrap position by adding cursor X to available width.
            try
            {
                float cursorX = ImGui.GetCursorPosX();
                float avail = ImGui.GetContentRegionAvail().X;
                ImGui.PushTextWrapPos(cursorX + avail);
                _pushCount++;
            }
            catch { }
        }

        public static void EndWindowDefaults()
        {
            try
            {
                if (_pushCount > 0)
                {
                    ImGui.PopTextWrapPos();
                    _pushCount--;
                }
            }
            catch { }
        }

        /// <summary>
        /// Convenience: push an item width that fills the content region.
        /// Call ImGui.PopItemWidth() after the items that should use this width.
        /// </summary>
        public static void PushItemWidthFull()
        {
            try
            {
                float avail = ImGui.GetContentRegionAvail().X;
                ImGui.PushItemWidth(avail);
            }
            catch { }
        }
    }
}

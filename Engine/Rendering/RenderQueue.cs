using System;

namespace Engine.Rendering
{
    /// <summary>
    /// Render queue values like Unity's system for consistent render ordering
    /// </summary>
    public static class RenderQueue
    {
        // Predefined render queue values (like Unity)
        public const int Background = 1000;      // Skybox, far background
        public const int Geometry = 2000;        // Opaque geometry (default)
        public const int AlphaTest = 2450;       // Alpha tested geometry (cutout)
        public const int GeometryLast = 2500;    // Last opaque objects

        // SSAO and post-processing effects
        public const int SSAOGeometry = 1900;    // SSAO geometry pass (before main geometry)
        public const int SSAOProcess = 2600;     // SSAO calculation (after opaque, before transparent)

        public const int Transparent = 3000;     // Transparent objects (alpha blended)
        public const int Overlay = 4000;         // UI overlays, gizmos

        // Helper methods
        public static bool IsOpaque(int queue) => queue < Transparent;
        public static bool IsTransparent(int queue) => queue >= Transparent && queue < Overlay;
        public static bool IsOverlay(int queue) => queue >= Overlay;

        /// <summary>
        /// Get render queue from transparency mode
        /// </summary>
        public static int FromTransparencyMode(int transparencyMode)
        {
            return transparencyMode == 0 ? Geometry : Transparent;
        }
    }
}
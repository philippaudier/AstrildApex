using System.Numerics;

namespace Engine.Rendering
{
    /// <summary>
    /// Global state for reflection clipping plane.
    /// Used to communicate clipping parameters from ViewportRenderer to TerrainRenderer
    /// </summary>
    public static class ReflectionClippingState
    {
        /// <summary>
        /// Whether clipping is currently enabled (true during reflection pass)
        /// </summary>
        public static bool IsEnabled { get; set; } = false;
        
        /// <summary>
        /// Clipping plane equation: (normal.xyz, d)
        /// Clips fragments when dot(worldPos, ClipPlane) < 0
        /// </summary>
        public static Vector4 ClipPlane { get; set; } = Vector4.Zero;
    }
}

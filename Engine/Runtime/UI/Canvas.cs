using System.Numerics;
using System.Collections.Generic;

namespace Engine.UI
{
    public enum RenderMode { ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace }

    public class Canvas
    {
        public RenderMode RenderMode { get; set; } = RenderMode.ScreenSpaceOverlay;
        public int SortOrder { get; set; } = 0;
        // Optional background for the canvas. If AutoBackground is true, a full-size
        // semi-transparent quad will be emitted before other elements are rendered.
        // Color is ARGB (0xAARRGGBB). Default is 50% black.
        public bool AutoBackground { get; set; } = true;
        public uint BackgroundColor { get; set; } = 0x7F000000;

        private readonly List<UIElement> _roots = new List<UIElement>();

        public IReadOnlyList<UIElement> Roots => _roots;

        // Canvas size in pixels (set by system when rendering)
        public Vector2 Size { get; set; } = new Vector2(800, 600);

        public void AddRoot(UIElement e)
        {
            if (!_roots.Contains(e)) _roots.Add(e);
        }

        public void RemoveRoot(UIElement e)
        {
            _roots.Remove(e);
        }
    }
}

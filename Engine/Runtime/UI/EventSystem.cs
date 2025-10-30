using System.Numerics;
using System.Collections.Generic;

namespace Engine.UI
{
    public class PointerEventData
    {
        public Vector2 Position;
        public bool Pressed;
        public bool Released;
    }

    public class EventSystem
    {
        private static EventSystem? _instance;
        public static EventSystem Instance => _instance ??= new EventSystem();

    private readonly List<Canvas> _canvases = new List<Canvas>();

    // Track current hovered element per canvas for enter/exit events (simple implementation)
    private readonly Dictionary<Canvas, UIElement?> _currentHover = new Dictionary<Canvas, UIElement?>();

    public void RegisterCanvas(Canvas c) { if (!_canvases.Contains(c)) _canvases.Add(c); }
    public void UnregisterCanvas(Canvas c) { _canvases.Remove(c); }

    // Allow renderers to enumerate registered canvases
    public IReadOnlyList<Canvas> Canvases => _canvases;

        // Simple pointer dispatch: find topmost element under pointer and invoke events
        public void ProcessPointer(PointerEventData data)
        {
            // Iterate canvases in insertion order; later we may sort by SortOrder
            for (int i = _canvases.Count - 1; i >= 0; i--)
            {
                var c = _canvases[i];
                // Transform pointer into canvas local space (assume ScreenSpaceOverlay for MVP)
                Vector2 p = data.Position; // already in pixels
                // Walk roots from last to first for topmost
                UIElement? found = null;
                for (int r = c.Roots.Count - 1; r >= 0; r--)
                {
                    var root = c.Roots[r];
                    if (HitTestRecursive(root, p, c.Size, out var target)) { found = target; break; }
                }

                // Handle enter/exit events
                _currentHover.TryGetValue(c, out var prevHover);
                if (prevHover != found)
                {
                    prevHover?.OnPointerExit();
                    found?.OnPointerEnter();
                    _currentHover[c] = found;
                }

                if (found != null)
                {
                    if (data.Pressed) found.OnPointerDown();
                    if (data.Released) { found.OnPointerUp(); found.OnClick(); }
                    return;
                }
            }
        }

        private bool HitTestRecursive(UIElement element, Vector2 p, Vector2 canvasSize, out UIElement? hit)
        {
            // Check children first (topmost)
            for (int i = element.Children.Count - 1; i >= 0; i--)
            {
                var child = element.Children[i];
                if (HitTestRecursive(child, p, canvasSize, out hit)) return true;
            }

            // Check this element
            if (!element.Visible || !element.Interactable) { hit = null; return false; }
            var rect = element.Rect.GetLocalRect(canvasSize);
            if (rect.Contains(p)) { hit = element; return true; }
            hit = null; return false;
        }
    }
}

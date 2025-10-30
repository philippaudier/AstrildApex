using System.Numerics;
using System;

namespace Engine.UI
{
    /// <summary>
    /// Simple RectTransform implementation (anchors/pivot/size) for UI layout.
    /// This is a minimal, serializable container with helper math.
    /// </summary>
    public class RectTransform
    {
        // Anchor values in normalized coordinates [0,1]
        public Vector2 AnchorMin { get; set; } = new Vector2(0f, 0f);
        public Vector2 AnchorMax { get; set; } = new Vector2(1f, 1f);

        // Pivot in local normalized coords
        public Vector2 Pivot { get; set; } = new Vector2(0.5f, 0.5f);

        // Position relative to anchors in pixels
        public Vector2 AnchoredPosition { get; set; } = Vector2.Zero;

        // Size delta in pixels
        public Vector2 SizeDelta { get; set; } = new Vector2(100f, 100f);

        public RectTransform()
        {
        }

        // Returns a simple axis-aligned rectangle in local canvas space
        public RectF GetLocalRect(Vector2 canvasSize)
        {
            // Compute anchor rect in pixels
            Vector2 aMin = new Vector2(AnchorMin.X * canvasSize.X, AnchorMin.Y * canvasSize.Y);
            Vector2 aMax = new Vector2(AnchorMax.X * canvasSize.X, AnchorMax.Y * canvasSize.Y);
            Vector2 anchorRectSize = aMax - aMin;

            // Size in pixels is sizeDelta when anchors are together, or scaled with anchor rect
            Vector2 size = SizeDelta;
            // If anchors are stretched, size is based on anchor rect
            if (Math.Abs(AnchorMax.X - AnchorMin.X) > 0.0001f)
            {
                size.X = anchorRectSize.X + SizeDelta.X;
            }
            if (Math.Abs(AnchorMax.Y - AnchorMin.Y) > 0.0001f)
            {
                size.Y = anchorRectSize.Y + SizeDelta.Y;
            }

            // Compute position: anchor min + anchoredPosition - pivot*size
            Vector2 pos = aMin + AnchoredPosition - Pivot * size;

            return new RectF(pos, size);
        }
    }
}

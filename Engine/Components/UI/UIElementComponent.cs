using System;
using System.Numerics;

namespace Engine.Components.UI
{
    /// <summary>
    /// Unified UI component that replaces Canvas/UIImage/UIText/UIButton.
    /// Simple, modern, and render-ready without runtime layer.
    /// </summary>
    public class UIElementComponent : Component
    {
        public enum ElementType
        {
            Canvas,
            Image,
            Text,
            Button
        }

        // === Core Properties ===
        public ElementType Type { get; set; } = ElementType.Canvas;
        public new bool Enabled { get; set; } = true;  // 'new' keyword to hide base Enabled property
        public int SortOrder { get; set; } = 0;

        // === Rect Transform ===
        public Vector2 AnchorMin { get; set; } = new Vector2(0.5f, 0.5f);
        public Vector2 AnchorMax { get; set; } = new Vector2(0.5f, 0.5f);
        public Vector2 AnchoredPosition { get; set; } = Vector2.Zero;
        public Vector2 SizeDelta { get; set; } = new Vector2(100, 100);
        public Vector2 Pivot { get; set; } = new Vector2(0.5f, 0.5f);

        // === Visual Properties ===
        public Vector4 Color { get; set; } = new Vector4(1, 1, 1, 1); // RGBA (0-1)
        public Guid? TextureGuid { get; set; } = null;

        // === Text Properties ===
        public string Text { get; set; } = "";
        public Guid? FontGuid { get; set; } = null;
        public float FontSize { get; set; } = 16f;
        public TextAlign TextAlignment { get; set; } = TextAlign.Center;

        // === Layout Properties ===
        public bool UseFlexbox { get; set; } = false;
        public FlexDirection FlexDirection { get; set; } = FlexDirection.Row;
        public FlexJustify JustifyContent { get; set; } = FlexJustify.FlexStart;
        public FlexAlign AlignItems { get; set; } = FlexAlign.FlexStart;
        public float FlexGap { get; set; } = 0f;

        // === Button Properties ===
        public Vector4 HoverColor { get; set; } = new Vector4(0.9f, 0.9f, 0.9f, 1f);
        public Vector4 PressedColor { get; set; } = new Vector4(0.7f, 0.7f, 0.7f, 1f);
        public bool Interactable { get; set; } = true;

        // === Internal State (not serialized) ===
        [NonSerialized]
        public bool IsHovered = false;
        [NonSerialized]
        public bool IsPressed = false;

        /// <summary>
        /// Calculate world rect based on parent canvas size
        /// </summary>
        public RectF CalculateWorldRect(Vector2 canvasSize)
        {
            // Calculate anchored rect
            Vector2 anchorPosMin = canvasSize * AnchorMin;
            Vector2 anchorPosMax = canvasSize * AnchorMax;
            Vector2 center = (anchorPosMin + anchorPosMax) * 0.5f + AnchoredPosition;
            
            // Apply size delta
            Vector2 size = SizeDelta;
            
            // Calculate final rect
            return new RectF(
                center.X - size.X * Pivot.X,
                center.Y - size.Y * Pivot.Y,
                size.X,
                size.Y
            );
        }

        /// <summary>
        /// Get current visual color (considering hover/pressed state for buttons)
        /// </summary>
        public Vector4 GetCurrentColor()
        {
            if (Type == ElementType.Button && Interactable)
            {
                if (IsPressed) return PressedColor;
                if (IsHovered) return HoverColor;
            }
            return Color;
        }
    }

    // === Supporting Types ===

    public struct RectF
    {
        public float X, Y, Width, Height;

        public RectF(float x, float y, float width, float height)
        {
            X = x; Y = y; Width = width; Height = height;
        }

        public bool Contains(Vector2 point)
        {
            return point.X >= X && point.X <= X + Width &&
                   point.Y >= Y && point.Y <= Y + Height;
        }
    }

    public enum TextAlign
    {
        Left,
        Center,
        Right,
        TopLeft,
        TopCenter,
        TopRight,
        MiddleLeft,
        MiddleCenter,
        MiddleRight,
        BottomLeft,
        BottomCenter,
        BottomRight
    }

    public enum FlexDirection
    {
        Row,
        Column
    }

    public enum FlexJustify
    {
        FlexStart,
        FlexEnd,
        Center,
        SpaceBetween,
        SpaceAround
    }

    public enum FlexAlign
    {
        FlexStart,
        FlexEnd,
        Center,
        Stretch
    }
}

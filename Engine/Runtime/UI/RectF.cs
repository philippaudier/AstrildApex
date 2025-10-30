using System.Numerics;

namespace Engine.UI
{
    public struct RectF
    {
        public Vector2 Position;
        public Vector2 Size;

        public RectF(Vector2 pos, Vector2 size)
        {
            Position = pos;
            Size = size;
        }

        public float Left => Position.X;
        public float Top => Position.Y;
        public float Right => Position.X + Size.X;
        public float Bottom => Position.Y + Size.Y;

        public bool Contains(Vector2 point)
        {
            return point.X >= Left && point.X <= Right && point.Y >= Top && point.Y <= Bottom;
        }
    }
}

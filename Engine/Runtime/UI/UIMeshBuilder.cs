using System.Numerics;
using System.Collections.Generic;

namespace Engine.UI
{
    public struct UIVertex
    {
        public Vector3 Position;
        public Vector2 UV;
        public uint Color; // ARGB
        public uint UseTexture; // 0 = no, 1 = sample atlas
    }

    public class UIMeshBuilder
    {
        public readonly List<UIVertex> Vertices = new List<UIVertex>();
        public readonly List<uint> Indices = new List<uint>();

        public void Clear()
        {
            Vertices.Clear();
            Indices.Clear();
        }

        // Add a simple quad in canvas-local coordinates
        public void AddQuad(float x, float y, float w, float h, uint color, Vector2 uv0, Vector2 uv1)
        {
            uint baseIdx = (uint)Vertices.Count;
            // If UVs are provided, mark this quad as textured (glyph/image)
            Vertices.Add(new UIVertex { Position = new Vector3(x, y, 0f), UV = new Vector2(uv0.X, uv0.Y), Color = color, UseTexture = 1 });
            Vertices.Add(new UIVertex { Position = new Vector3(x + w, y, 0f), UV = new Vector2(uv1.X, uv0.Y), Color = color, UseTexture = 1 });
            Vertices.Add(new UIVertex { Position = new Vector3(x + w, y + h, 0f), UV = new Vector2(uv1.X, uv1.Y), Color = color, UseTexture = 1 });
            Vertices.Add(new UIVertex { Position = new Vector3(x, y + h, 0f), UV = new Vector2(uv0.X, uv1.Y), Color = color, UseTexture = 1 });

            Indices.Add(baseIdx + 0);
            Indices.Add(baseIdx + 1);
            Indices.Add(baseIdx + 2);
            Indices.Add(baseIdx + 2);
            Indices.Add(baseIdx + 3);
            Indices.Add(baseIdx + 0);
        }

        // Convenience overload: full uv [0,0]-[1,1]
        public void AddQuad(Vector2 pos, Vector2 size, uint color)
        {
            // Convenience: non-textured quad, UVs unused
            uint baseIdx = (uint)Vertices.Count;
            Vertices.Add(new UIVertex { Position = new Vector3(pos.X, pos.Y, 0f), UV = new Vector2(0f,0f), Color = color, UseTexture = 0 });
            Vertices.Add(new UIVertex { Position = new Vector3(pos.X + size.X, pos.Y, 0f), UV = new Vector2(0f,0f), Color = color, UseTexture = 0 });
            Vertices.Add(new UIVertex { Position = new Vector3(pos.X + size.X, pos.Y + size.Y, 0f), UV = new Vector2(0f,0f), Color = color, UseTexture = 0 });
            Vertices.Add(new UIVertex { Position = new Vector3(pos.X, pos.Y + size.Y, 0f), UV = new Vector2(0f,0f), Color = color, UseTexture = 0 });

            Indices.Add(baseIdx + 0);
            Indices.Add(baseIdx + 1);
            Indices.Add(baseIdx + 2);
            Indices.Add(baseIdx + 2);
            Indices.Add(baseIdx + 3);
            Indices.Add(baseIdx + 0);
        }
    }
}

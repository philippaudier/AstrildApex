using System.Numerics;

namespace Engine.UI
{
    public class UIImage : UIElement
    {
        public uint Color { get; set; } = 0xFFFFFFFF; // White ARGB
        public System.Guid? TextureGuid { get; set; } = null; // placeholder to match asset management

        public override void OnPopulateMesh(UIMeshBuilder mb, Vector2 canvasSize)
        {
            // Compute local rect
            var rect = Rect.GetLocalRect(canvasSize);
            mb.AddQuad(rect.Position.X, rect.Position.Y, rect.Size.X, rect.Size.Y, Color, new Vector2(0,0), new Vector2(1,1));
        }
    }
}

using System.Numerics;

namespace Engine.UI
{
    public class UIText : UIElement
    {
        public string Text { get; set; } = "Text";
        public uint Color { get; set; } = 0xFF000000;
        public float FontSize { get; set; } = 14f;
        public System.Guid? FontAssetGuid { get; set; }
        public bool Bold { get; set; } = false;
        public bool Italic { get; set; } = false;
        public TextAlignment Alignment { get; set; } = TextAlignment.Left;

        private static FontAtlas? _defaultAtlas;

        // Helper to access or create a default atlas lazily
        private static FontAtlas? DefaultAtlas
        {
            get
            {
                if (_defaultAtlas != null) return _defaultAtlas;
                try
                {
                    var exeBase = AppContext.BaseDirectory;
                    var path = System.IO.Path.Combine(exeBase, "Resources", "Fonts", "Roboto-Regular.ttf");
                    if (!System.IO.File.Exists(path))
                    {
                        _defaultAtlas = FontAtlas.CreateDefault(14);
                    }
                    else _defaultAtlas = new FontAtlas(path, 14);
                }
                catch { _defaultAtlas = null; }
                return _defaultAtlas;
            }
        }

        public override void OnPopulateMesh(UIMeshBuilder mb, Vector2 canvasSize)
        {
            var rect = Rect.GetLocalRect(canvasSize);
            var atlas = DefaultAtlas;
            if (atlas == null)
            {
                // Fallback to placeholder quad
                mb.AddQuad(rect.Position, rect.Size, Color);
                return;
            }

            // Simple layout: left-to-right baseline, no kerning/complex scripts.
            Vector2 pen = rect.Position;
            foreach (var ch in Text)
            {
                if (!atlas.TryGetGlyph(ch, out var gi))
                {
                    // missing glyph => advance a fixed amount
                    pen.X += FontSize * 0.5f;
                    continue;
                }

                var size = gi!.Size;
                // Place glyph quad with proper UVs into atlas
                mb.AddQuad(pen.X, pen.Y, size.X, size.Y, Color, gi.AtlasUV0, gi.AtlasUV1);
                pen.X += gi.Advance;
            }
        }
    }
}

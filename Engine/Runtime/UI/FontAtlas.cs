using System;
using System.Numerics;
using System.Collections.Generic;
using OpenTK.Graphics.OpenGL4;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Drawing;

namespace Engine.UI
{
    public class GlyphInfo
    {
        public char Character;
        public Vector2 Size;
        public Vector2 AtlasUV0, AtlasUV1;
        public float Advance;
    }

    // Cross-platform FontAtlas that rasterizes basic ASCII glyphs into a single atlas texture using SixLabors
    public class FontAtlas : IDisposable
    {
        private readonly Dictionary<char, GlyphInfo> _glyphs = new();
        private int _glTex = 0;
    // Buffer to hold pixel data until a GL context is available for upload
    private Rgba32[]? _pendingPixels = null;
        public int TextureId => _glTex;
        public int AtlasWidth { get; private set; }
        public int AtlasHeight { get; private set; }

        private static readonly Dictionary<int, FontAtlas> s_cache = new();

        private FontAtlas() { }

        // Create or return a cached default atlas for the requested pixel size
        public static FontAtlas? CreateDefault(int fontSize = 14)
        {
            if (s_cache.TryGetValue(fontSize, out var existing)) return existing;

            string?[] tryPaths = new string?[] {
                System.IO.Path.Combine(AppContext.BaseDirectory, "Resources", "Fonts", "Roboto-Regular.ttf"),
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf"),
                "/Library/Fonts/Arial.ttf",
                "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
            };

            foreach (var p in tryPaths)
            {
                if (string.IsNullOrEmpty(p)) continue;
                try
                {
                    if (System.IO.File.Exists(p))
                    {
                        var fa = new FontAtlas(p, fontSize);
                        s_cache[fontSize] = fa;
                        return fa;
                    }
                }
                catch { }
            }

            return null;
        }

        // Construct from a font file path
        public FontAtlas(string ttfPath, int pixelSize)
        {
            InitializeFromFontFile(ttfPath, pixelSize);
        }

        private void InitializeFromFontFile(string ttfPath, int pixelSize)
        {
            // Load font
            FontCollection collection = new FontCollection();
            FontFamily? family = null;
            if (!string.IsNullOrEmpty(ttfPath) && System.IO.File.Exists(ttfPath))
            {
                family = collection.Add(ttfPath);
            }
            else
            {
                // try system default
                foreach (var f in SystemFonts.Families) { family = f; break; }
            }

            if (family == null) throw new Exception("No font family found for FontAtlas");

            var font = new Font((SixLabors.Fonts.FontFamily)family!, pixelSize);

            // Rasterize ASCII glyphs 32..126 into images using SixLabors.ImageSharp.Drawing APIs
            int padding = 2;
            int maxWidth = 1024;
            var glyphImgs = new List<(char ch, Image<Rgba32> img, int w, int h, float advance)>();

            // Draw each glyph into a temporary canvas and crop to the non-transparent bbox
            for (int c = 32; c < 127; c++)
            {
                char ch = (char)c;
                var txt = ch.ToString();
                int tmpW = pixelSize * 3;
                int tmpH = pixelSize * 3;
                using var tmp = new Image<Rgba32>(tmpW, tmpH);
                // clear
                for (int yy = 0; yy < tmpH; yy++) for (int xx = 0; xx < tmpW; xx++) tmp[xx, yy] = new Rgba32(0, 0, 0, 0);

                tmp.Mutate(ctx => ctx.DrawText(txt, font, SixLabors.ImageSharp.Color.White, new SixLabors.ImageSharp.PointF(0, 0)));

                // find non-transparent bbox
                int minX = tmpW, minY = tmpH, maxX = -1, maxY = -1;
                for (int y = 0; y < tmpH; y++)
                    for (int x = 0; x < tmpW; x++)
                    {
                        if (tmp[x, y].A > 0)
                        {
                            if (x < minX) minX = x;
                            if (y < minY) minY = y;
                            if (x > maxX) maxX = x;
                            if (y > maxY) maxY = y;
                        }
                    }

                int w = 1, h = 1, srcX = 0, srcY = 0;
                if (maxX >= 0)
                {
                    srcX = Math.Max(0, minX);
                    srcY = Math.Max(0, minY);
                    w = maxX - srcX + 1;
                    h = maxY - srcY + 1;
                }

                var glyphImg = tmp.Clone(ctx => ctx.Crop(new SixLabors.ImageSharp.Rectangle(srcX, srcY, w, h)));
                glyphImgs.Add((ch, glyphImg, w, h, (float)w));
            }

            // Pack into atlas (simple row packing)
            AtlasWidth = maxWidth;
            int curX = padding, curY = padding, rowH = 0;
            foreach (var g in glyphImgs)
            {
                if (curX + g.w + padding > AtlasWidth)
                {
                    curX = padding;
                    curY += rowH + padding;
                    rowH = 0;
                }
                curX += g.w + padding;
                rowH = Math.Max(rowH, g.h);
            }
            AtlasHeight = Math.Max(128, curY + rowH + padding);

            using var atlas = new Image<Rgba32>(AtlasWidth, AtlasHeight);
            // Clear atlas
            for (int yy = 0; yy < AtlasHeight; yy++) for (int xx = 0; xx < AtlasWidth; xx++) atlas[xx, yy] = new Rgba32(0, 0, 0, 0);

            curX = padding; curY = padding; rowH = 0;
            foreach (var kv in glyphImgs)
            {
                var ch = kv.ch; var img = kv.img;
                if (curX + img.Width + padding > AtlasWidth)
                {
                    curX = padding;
                    curY += rowH + padding;
                    rowH = 0;
                }
                // Blit img into atlas
                for (int yy = 0; yy < img.Height; yy++) for (int xx = 0; xx < img.Width; xx++) atlas[curX + xx, curY + yy] = img[xx, yy];
                var uv0 = new Vector2((float)curX / AtlasWidth, (float)curY / AtlasHeight);
                var uv1 = new Vector2((float)(curX + img.Width) / AtlasWidth, (float)(curY + img.Height) / AtlasHeight);
                _glyphs[ch] = new GlyphInfo { Character = ch, Size = new Vector2(img.Width, img.Height), AtlasUV0 = uv0, AtlasUV1 = uv1, Advance = kv.advance };

                curX += img.Width + padding;
                rowH = Math.Max(rowH, img.Height);
            }

            // Copy pixel data to pending buffer. Actual GL upload will be deferred
            // until a GL context is current and TextureId is requested.
            var pixelSpan = new Rgba32[AtlasWidth * AtlasHeight];
            atlas.CopyPixelDataTo(pixelSpan.AsSpan());
            _pendingPixels = pixelSpan;
        }

        public bool TryGetGlyph(char c, out GlyphInfo? info) => _glyphs.TryGetValue(c, out info);

        public void Dispose()
        {
            if (_glTex != 0)
            {
                try { GL.DeleteTexture(_glTex); } catch { }
                _glTex = 0;
            }
            _pendingPixels = null;
        }

        // Ensure the GL texture exists. This method will attempt to upload the
        // pending pixel buffer if a GL context is current. Callers should handle
        // exceptions when GL is not available.
        private void EnsureGLTexture()
        {
            if (_glTex != 0) return;
            if (_pendingPixels == null) return;
            try
            {
                _glTex = GL.GenTexture();
                GL.BindTexture(OpenTK.Graphics.OpenGL4.TextureTarget.Texture2D, _glTex);
                GL.TexImage2D(OpenTK.Graphics.OpenGL4.TextureTarget.Texture2D, 0, OpenTK.Graphics.OpenGL4.PixelInternalFormat.Rgba, AtlasWidth, AtlasHeight, 0, OpenTK.Graphics.OpenGL4.PixelFormat.Rgba, OpenTK.Graphics.OpenGL4.PixelType.UnsignedByte, _pendingPixels);
                GL.TexParameter(OpenTK.Graphics.OpenGL4.TextureTarget.Texture2D, OpenTK.Graphics.OpenGL4.TextureParameterName.TextureMinFilter, (int)OpenTK.Graphics.OpenGL4.TextureMinFilter.Linear);
                GL.TexParameter(OpenTK.Graphics.OpenGL4.TextureTarget.Texture2D, OpenTK.Graphics.OpenGL4.TextureParameterName.TextureMagFilter, (int)OpenTK.Graphics.OpenGL4.TextureMagFilter.Linear);
                GL.BindTexture(OpenTK.Graphics.OpenGL4.TextureTarget.Texture2D, 0);
                // Free CPU-side copy
                _pendingPixels = null;
            }
            catch (Exception ex)
            {
                try { Console.WriteLine($"[FontAtlas] Failed to create GL texture: {ex.Message}"); } catch { }
            }
        }

        // Public accessor that ensures the texture is uploaded if possible.
        public int EnsureTextureId()
        {
            EnsureGLTexture();
            return _glTex;
        }
    }
}

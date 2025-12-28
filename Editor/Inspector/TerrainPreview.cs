using System;
using OpenTK.Graphics.OpenGL4;

namespace Editor.Inspector
{
    /// <summary>
    /// Generates and manages 2D preview texture for terrain heightmaps
    /// </summary>
    public class TerrainPreview : IDisposable
    {
        private int _textureId = 0;
        private int _width = 0;
        private int _height = 0;
        private bool _disposed = false;

        public int TextureId => _textureId;
        public int Width => _width;
        public int Height => _height;

        /// <summary>
        /// Generate preview texture from heightmap data
        /// </summary>
        public void GeneratePreview(float[,] heightmap, int previewSize = 256)
        {
            if (heightmap == null) return;

            int srcWidth = heightmap.GetLength(0);
            int srcHeight = heightmap.GetLength(1);

            // Cleanup old texture
            if (_textureId != 0)
            {
                GL.DeleteTexture(_textureId);
                _textureId = 0;
            }

            // Generate downsampled preview data
            byte[] pixels = new byte[previewSize * previewSize * 3]; // RGB

            for (int y = 0; y < previewSize; y++)
            {
                for (int x = 0; x < previewSize; x++)
                {
                    // Sample from heightmap with bilinear interpolation
                    float u = (float)x / (previewSize - 1);
                    float v = (float)y / (previewSize - 1);

                    float sx = u * (srcWidth - 1);
                    float sy = v * (srcHeight - 1);

                    int x0 = (int)Math.Floor(sx);
                    int y0 = (int)Math.Floor(sy);
                    int x1 = Math.Min(x0 + 1, srcWidth - 1);
                    int y1 = Math.Min(y0 + 1, srcHeight - 1);

                    float fx = sx - x0;
                    float fy = sy - y0;

                    float h00 = heightmap[x0, y0];
                    float h10 = heightmap[x1, y0];
                    float h01 = heightmap[x0, y1];
                    float h11 = heightmap[x1, y1];

                    float h0 = h00 * (1f - fx) + h10 * fx;
                    float h1 = h01 * (1f - fx) + h11 * fx;
                    float height = h0 * (1f - fy) + h1 * fy;

                    // Apply height-based coloring (gradient from dark blue to white)
                    byte value = (byte)(height * 255f);

                    int idx = (y * previewSize + x) * 3;

                    // Color gradient: blue (low) -> green (mid) -> yellow (high) -> white (peak)
                    if (height < 0.25f)
                    {
                        // Blue to cyan
                        float t = height / 0.25f;
                        pixels[idx + 0] = 0;
                        pixels[idx + 1] = (byte)(t * 100);
                        pixels[idx + 2] = (byte)(100 + t * 155);
                    }
                    else if (height < 0.5f)
                    {
                        // Cyan to green
                        float t = (height - 0.25f) / 0.25f;
                        pixels[idx + 0] = 0;
                        pixels[idx + 1] = (byte)(100 + t * 155);
                        pixels[idx + 2] = (byte)(255 - t * 155);
                    }
                    else if (height < 0.75f)
                    {
                        // Green to yellow
                        float t = (height - 0.5f) / 0.25f;
                        pixels[idx + 0] = (byte)(t * 255);
                        pixels[idx + 1] = 255;
                        pixels[idx + 2] = (byte)(100 - t * 100);
                    }
                    else
                    {
                        // Yellow to white
                        float t = (height - 0.75f) / 0.25f;
                        pixels[idx + 0] = 255;
                        pixels[idx + 1] = 255;
                        pixels[idx + 2] = (byte)(t * 255);
                    }
                }
            }

            // Create OpenGL texture
            _textureId = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _textureId);

            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgb,
                previewSize, previewSize, 0, PixelFormat.Rgb, PixelType.UnsignedByte, pixels);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            GL.BindTexture(TextureTarget.Texture2D, 0);

            _width = previewSize;
            _height = previewSize;
        }

        public void Dispose()
        {
            if (_disposed) return;

            if (_textureId != 0)
            {
                GL.DeleteTexture(_textureId);
                _textureId = 0;
            }

            _disposed = true;
        }
    }
}

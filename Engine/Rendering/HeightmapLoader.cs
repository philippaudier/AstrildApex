using System;
using System.IO;
using Engine.Assets;
using Engine.Rendering;
using StbImageSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Engine.Rendering
{
    /// <summary>
    /// Simplified heightmap loader - reads raw R channel for smooth terrain
    /// </summary>
    public static class HeightmapLoader
    {
        /// <summary>
        /// Load heightmap data from a texture GUID
        /// </summary>
        public static float[,]? LoadHeightmapFromTexture(Guid textureGuid)
        {
            if (!AssetDatabase.TryGet(textureGuid, out var record))
                return null;

            return LoadHeightmapFromFile(record.Path);
        }

        /// <summary>
        /// Load heightmap data from a file path - SIMPLIFIED for correct smooth terrain
        /// </summary>
        public static float[,]? LoadHeightmapFromFile(string path)
        {
            if (!File.Exists(path))
                return null;

            try
            {
                using var fs = File.OpenRead(path);

                // Check if it's a RAW file
                string extension = Path.GetExtension(path).ToLowerInvariant();
                if (extension == ".raw" || extension == ".r16")
                {
                    return LoadRawHeightmap(fs, path);
                }
                else
                {
                    return LoadImageHeightmap(fs);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HeightmapLoader] Error loading heightmap: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// IMPROVED: Load heightmap with TRUE 16-bit support using SixLabors.ImageSharp
        /// This properly loads 16-bit PNG heightmaps without quantization
        /// </summary>
        private static float[,]? LoadImageHeightmap(Stream stream)
        {
            try
            {
                Stream workingStream;
                if (stream.CanSeek)
                {
                    workingStream = stream;
                }
                else
                {
                    workingStream = new MemoryStream();
                    stream.CopyTo(workingStream);
                    workingStream.Position = 0;
                }

                // Use SixLabors.ImageSharp to properly load 16-bit grayscale PNGs
                using var image = Image.Load<L16>(workingStream);
                int width = image.Width;
                int height = image.Height;

                Console.WriteLine($"[HeightmapLoader] Loading 16-bit grayscale heightmap: {width}x{height}");

                var heightData = new float[width, height];

                // Process pixel data - L16 stores 16-bit grayscale values
                image.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < height; y++)
                    {
                        Span<L16> pixelRow = accessor.GetRowSpan(y);
                        for (int x = 0; x < width; x++)
                        {
                            // L16.PackedValue is a ushort (0-65535)
                            ushort value = pixelRow[x].PackedValue;
                            heightData[x, y] = value / 65535.0f;
                        }
                    }
                });

                if (!ReferenceEquals(workingStream, stream))
                {
                    workingStream.Dispose();
                }

                return heightData;
            }
            catch (Exception ex)
            {
                // If 16-bit loading fails, try 8-bit as fallback
                Console.WriteLine($"[HeightmapLoader] 16-bit load failed ({ex.Message}), trying 8-bit fallback...");

                try
                {
                    stream.Position = 0;

                    using var image = Image.Load<L8>(stream);
                    int width = image.Width;
                    int height = image.Height;

                    Console.WriteLine($"[HeightmapLoader] Loading 8-bit heightmap: {width}x{height} (may have quantization artifacts)");

                    var heightData = new float[width, height];

                    image.ProcessPixelRows(accessor =>
                    {
                        for (int y = 0; y < height; y++)
                        {
                            Span<L8> pixelRow = accessor.GetRowSpan(y);
                            for (int x = 0; x < width; x++)
                            {
                                // L8.PackedValue is a byte (0-255)
                                byte value = pixelRow[x].PackedValue;
                                heightData[x, y] = value / 255.0f;
                            }
                        }
                    });

                    return heightData;
                }
                catch (Exception ex2)
                {
                    Console.WriteLine($"[HeightmapLoader] Failed to load heightmap: {ex2.Message}");
                    return null;
                }
            }
        }

        /// <summary>
        /// Load heightmap from RAW 16-bit format
        /// </summary>
        private static float[,]? LoadRawHeightmap(Stream stream, string filename)
        {
            try
            {
                long fileSize = stream.Length;
                int expectedBytesPerPixel = 2; // 16-bit

                int width = GuessHeightmapDimension(fileSize, expectedBytesPerPixel);
                int height = width; // Assume square

                if (width == 0)
                    return null;

                var heightData = new float[width, height];
                byte[] buffer = new byte[2];

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (stream.Read(buffer, 0, 2) != 2)
                            return null;

                        // Convert 16-bit value to float (0.0 to 1.0)
                        ushort value = (ushort)(buffer[0] | (buffer[1] << 8));
                        heightData[x, y] = value / 65535.0f;
                    }
                }

                return heightData;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Guess heightmap dimensions from file size (assumes square, 16-bit RAW)
        /// </summary>
        private static int GuessHeightmapDimension(long fileSize, int bytesPerPixel)
        {
            int[] commonSizes = { 1024, 2048, 4096, 512, 8192, 256 };

            foreach (int size in commonSizes)
            {
                long expectedSize = (long)size * size * bytesPerPixel;
                if (Math.Abs(fileSize - expectedSize) < 1024)
                {
                    return size;
                }
            }

            double pixelCount = (double)fileSize / bytesPerPixel;
            int dimension = (int)Math.Sqrt(pixelCount);

            if (dimension * dimension * bytesPerPixel == fileSize)
            {
                return dimension;
            }

            return 0;
        }
    }
}

using System;
using System.IO;
using System.Collections.Generic;
using OpenTK.Graphics.OpenGL4;

namespace Engine.Rendering
{
    // Minimal KTX1 loader tailored for cmgen-produced KTX files.
    // Supports uncompressed KTX with unsigned byte or half-float (GL_HALF_FLOAT) data
    // and cubemaps (numberOfFaces == 6). Returns mipmap levels and per-face byte arrays.
    public static class KtxLoader
    {
        public class KtxImage
        {
            public int Width;
            public int Height;
            public int NumFaces;
            public int MipLevels;
            public PixelInternalFormat InternalFormat;
            public PixelFormat Format;
            public PixelType Type;
            // MipLevels x NumFaces -> byte[]
            public List<byte[][]> MipFaces = new();
            public bool IsFloatFormat = false;
            public bool IsCompressed = false; // True if glType == 0 (compressed texture)
            public uint GlInternalFormat; // Store raw GL format for compressed textures
        }

        private static readonly byte[] KtxIdentifier = new byte[] { 0xAB, (byte)'K', (byte)'T', (byte)'X', 0x20, (byte)'1', (byte)'1', 0xBB, 0x0D, 0x0A, 0x1A, 0x0A };

        public static KtxImage Load(string path)
        {
            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);

            // Read identifier
            var id = br.ReadBytes(12);
            for (int i = 0; i < 12; i++)
                if (id[i] != KtxIdentifier[i]) throw new InvalidDataException("Not a KTX file");

            uint endianness = br.ReadUInt32();
            bool little = endianness == 0x04030201;
            if (!little)
                throw new NotSupportedException("Big-endian KTX not supported");

            uint glType = br.ReadUInt32();
            uint glTypeSize = br.ReadUInt32();
            uint glFormat = br.ReadUInt32();
            uint glInternalFormat = br.ReadUInt32();
            uint glBaseInternalFormat = br.ReadUInt32();
            uint pixelWidth = br.ReadUInt32();
            uint pixelHeight = br.ReadUInt32();
            uint pixelDepth = br.ReadUInt32();
            uint numberOfArrayElements = br.ReadUInt32();
            uint numberOfFaces = br.ReadUInt32();
            uint numberOfMipmapLevels = br.ReadUInt32();
            uint bytesOfKeyValueData = br.ReadUInt32();

            try { Console.WriteLine($"[KtxLoader] Header: glType=0x{glType:X}, glTypeSize={glTypeSize}, glFormat=0x{glFormat:X}, glInternalFormat=0x{glInternalFormat:X}, glBaseInternalFormat=0x{glBaseInternalFormat:X}"); } catch { }
            try { Console.WriteLine($"[KtxLoader] Size: {pixelWidth}x{pixelHeight}, faces={numberOfFaces}, mips={numberOfMipmapLevels}"); } catch { }

            // Skip key/value data
            if (bytesOfKeyValueData > 0)
                br.ReadBytes((int)bytesOfKeyValueData);

            int mipLevels = (int)(numberOfMipmapLevels == 0 ? 1 : numberOfMipmapLevels);
            int faces = (int)numberOfFaces;

            // Detect compressed format: glType == 0 means compressed
            // glType != 0 means uncompressed (even if data seems smaller than expected)
            bool isCompressed = (glType == 0);
            
            try { Console.WriteLine($"[KtxLoader] glType={glType}, isCompressed={isCompressed}"); } catch { }
            
            var kimg = new KtxImage
            {
                Width = (int)pixelWidth,
                Height = (int)pixelHeight,
                NumFaces = faces,
                MipLevels = mipLevels,
                IsCompressed = isCompressed,
                GlInternalFormat = glInternalFormat
            };

            // Determine GL upload format/type based on header.
            // cmgen generates KTX with special packed HDR formats like R11F_G11F_B10F
            const uint GL_R11F_G11F_B10F = 0x8C3A;
            const uint GL_RGB16F = 0x881B;
            const uint GL_RGBA16F = 0x881A;

            if (isCompressed)
            {
                // Compressed format - we'll use glCompressedTexImage2D
                // Store the format directly, no conversion needed
                kimg.InternalFormat = (PixelInternalFormat)glInternalFormat;
                kimg.Format = PixelFormat.Rgb; // Not used for compressed
                kimg.Type = PixelType.UnsignedByte; // Not used for compressed
                kimg.IsFloatFormat = true; // Assume HDR for cmgen outputs
                try { Console.WriteLine($"[KtxLoader] Detected COMPRESSED format: 0x{glInternalFormat:X}"); } catch { }
            }
            // Check for packed HDR format (R11F_G11F_B10F)
            else if (glInternalFormat == GL_R11F_G11F_B10F || glType == GL_R11F_G11F_B10F)
            {
                // R11F_G11F_B10F: 32-bit packed HDR format (11+11+10 bits)
                kimg.InternalFormat = PixelInternalFormat.R11fG11fB10f;
                kimg.Format = PixelFormat.Rgb;
                kimg.Type = PixelType.UnsignedInt10F11F11FRev;
                kimg.IsFloatFormat = true;
                try { Console.WriteLine($"[KtxLoader] Detected R11F_G11F_B10F packed HDR format"); } catch { }
            }
            else if (glInternalFormat == GL_RGB16F || glInternalFormat == GL_RGBA16F)
            {
                // RGB16F/RGBA16F: half-float per component
                kimg.IsFloatFormat = true;
                kimg.Type = PixelType.HalfFloat;
                if (glInternalFormat == GL_RGBA16F || glBaseInternalFormat == 0x1908)
                {
                    kimg.InternalFormat = PixelInternalFormat.Rgba16f;
                    kimg.Format = PixelFormat.Rgba;
                }
                else
                {
                    kimg.InternalFormat = PixelInternalFormat.Rgb16f;
                    kimg.Format = PixelFormat.Rgb;
                }
                try { Console.WriteLine($"[KtxLoader] Detected RGB16F half-float format"); } catch { }
            }
            else if (glTypeSize == 1)
            {
                // 1 byte per component -> unsigned byte LDR
                kimg.Type = PixelType.UnsignedByte;
                kimg.IsFloatFormat = false;
                if (glBaseInternalFormat == 0x1908) // GL_RGBA
                {
                    kimg.InternalFormat = PixelInternalFormat.Rgba8;
                    kimg.Format = PixelFormat.Rgba;
                }
                else
                {
                    kimg.InternalFormat = PixelInternalFormat.Rgb8;
                    kimg.Format = PixelFormat.Rgb;
                }
            }
            else if (glTypeSize == 2 || glTypeSize == 4)
            {
                // 2 -> half-float, 4 -> float (per-component)
                kimg.IsFloatFormat = true;
                kimg.Type = (glTypeSize == 2) ? PixelType.HalfFloat : PixelType.Float;
                if (glBaseInternalFormat == 0x1908) // GL_RGBA
                {
                    kimg.InternalFormat = PixelInternalFormat.Rgba16f;
                    kimg.Format = PixelFormat.Rgba;
                }
                else
                {
                    kimg.InternalFormat = PixelInternalFormat.Rgb16f;
                    kimg.Format = PixelFormat.Rgb;
                }
            }
            else
            {
                // Unknown type size — fall back to byte format but log for diagnostics
                kimg.Type = PixelType.UnsignedByte;
                kimg.IsFloatFormat = false;
                kimg.InternalFormat = (glBaseInternalFormat == 0x1908) ? PixelInternalFormat.Rgba8 : PixelInternalFormat.Rgb8;
                kimg.Format = (glBaseInternalFormat == 0x1908) ? PixelFormat.Rgba : PixelFormat.Rgb;
                try { Console.WriteLine($"[KtxLoader] WARNING: Unknown format, falling back to RGB8"); } catch { }
            }

            // Read mip levels
            for (int mip = 0; mip < mipLevels; mip++)
            {
                // Each mip level starts with a uint32 imageSize (total bytes for that mip across faces)
                uint imageSize = 0;
                try { imageSize = br.ReadUInt32(); } catch { throw new EndOfStreamException("Unexpected EOF reading KTX imageSize"); }

                try { Console.WriteLine($"[KtxLoader] Mip {mip}: imageSize={imageSize} bytes"); } catch { }

                var faceData = new byte[faces][];

                // For cmgen-generated KTX cubemaps, data is stored contiguously without per-face size prefixes
                // The imageSize covers all 6 faces, so we split it evenly
                long mipStartPos = fs.Position;

                // Calculate expected size per face
                int mipWidth = Math.Max(1, (int)pixelWidth >> mip);
                int mipHeight = Math.Max(1, (int)pixelHeight >> mip);

                // Calculate bytes per pixel based on actual format
                int bytesPerPixel;
                if (kimg.InternalFormat == PixelInternalFormat.R11fG11fB10f)
                {
                    // R11F_G11F_B10F is a packed 32-bit format (4 bytes per pixel)
                    bytesPerPixel = 4;
                }
                else if (kimg.Type == PixelType.HalfFloat)
                {
                    // Half-float: 2 bytes per channel
                    int channels = (kimg.Format == PixelFormat.Rgba) ? 4 : 3;
                    bytesPerPixel = channels * 2;
                }
                else if (kimg.Type == PixelType.Float)
                {
                    // Float: 4 bytes per channel
                    int channels = (kimg.Format == PixelFormat.Rgba) ? 4 : 3;
                    bytesPerPixel = channels * 4;
                }
                else
                {
                    // Byte formats
                    int channels = (kimg.Format == PixelFormat.Rgba) ? 4 : 3;
                    bytesPerPixel = channels * 1;
                }

                int expectedFaceSize = mipWidth * mipHeight * bytesPerPixel;
                int expectedTotalSize = expectedFaceSize * faces;

                try { Console.WriteLine($"[KtxLoader] Mip {mip}: {mipWidth}x{mipHeight}, expected {expectedFaceSize} bytes/face, total {expectedTotalSize} vs actual {imageSize}"); } catch { }

                // CRITICAL FIX: If imageSize doesn't match expected uncompressed size,
                // the data is actually compressed regardless of glType
                // cmgen sometimes generates KTX files with incorrect headers
                if (!kimg.IsCompressed && imageSize != expectedTotalSize)
                {
                    kimg.IsCompressed = true;
                    try { Console.WriteLine($"[KtxLoader] DATA SIZE MISMATCH - Treating as COMPRESSED: actual {imageSize} != expected {expectedTotalSize}"); } catch { }
                }

                // Read all data at once and split evenly among faces (cmgen format)
                var mipBytes = br.ReadBytes((int)imageSize);
                int faceSize = (int)(imageSize / faces);

                for (int f = 0; f < faces; f++)
                {
                    int start = f * faceSize;
                    int length = (f == faces - 1) ? (int)(imageSize - start) : faceSize;
                    var arr = new byte[length];
                    Array.Copy(mipBytes, start, arr, 0, length);
                    faceData[f] = arr;
                    try { Console.WriteLine($"[KtxLoader] Face {f}: {length} bytes"); } catch { }
                }

                kimg.MipFaces.Add(faceData);

                // If per-face sizes were used, the stream is already positioned at next mip; otherwise it is too.
                // There may be padding to 4 bytes already consumed per-face.
                // Ensure we advance to the next 4-byte boundary after the whole mip block
                long afterMipPos = fs.Position;
                long padTotal = (4 - (afterMipPos % 4)) % 4;
                if (padTotal > 0) br.ReadBytes((int)padTotal);
            }

            return kimg;
        }
    }
}

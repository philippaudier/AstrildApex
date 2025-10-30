using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using OpenTK.Graphics.OpenGL4;
using StbImageSharp;

namespace Engine.Rendering
{
    public static class TextureCache
    {
        private class TextureEntry
        {
            public int GLHandle;
            public int Width, Height;
            public long LastUsedFrame;
            public int SizeInBytes;
            public bool IsResident;
        }
        
        private class LRUNode
        {
            public Guid Key;
            public TextureEntry Value = new TextureEntry();
            public LRUNode Prev, Next;

            public LRUNode()
            {
                Prev = this;
                Next = this;
            }
        }
        
        private static readonly Dictionary<Guid, LRUNode> _textureNodes = new();
        private static readonly Dictionary<string, LRUNode> _pathNodes = new(StringComparer.OrdinalIgnoreCase);
        
        // LRU cache management
        private static LRUNode _head = new LRUNode();
        private static LRUNode _tail = new LRUNode();
        private static long _currentFrame = 0;
        
        // Memory management
        private static long _totalMemoryUsed = 0;
        private static readonly long MAX_TEXTURE_MEMORY = 512 * 1024 * 1024; // 512MB
        private static readonly int MAX_TEXTURE_COUNT = 1000;
        
        // Streaming support
        private static readonly HashSet<Guid> _pendingLoads = new();
        private static readonly Queue<Guid> _compressionQueue = new();
        // Queue for transfers prepared by background loader and processed on main (GL) thread
        private static readonly System.Collections.Concurrent.ConcurrentQueue<PendingLoad> _uploadQueue = new();

        private struct PendingLoad
        {
            public Guid Guid;
            public string Path;
            public bool IsHdr;
            public bool IsNormalMap;
            public bool FlipGreen;
            public byte[]? PixelData; // RGBA8 bytes for LDR and converted HDR
            public int Width;
            public int Height;
            public PixelInternalFormat InternalFormat;
            public int SizeInBytes;
        }
        
        public static int White1x1 { get; private set; } = 0;
        public static long TotalMemoryUsed => _totalMemoryUsed;
        public static int LoadedTextureCount => _textureNodes.Count;
        
        static TextureCache()
        {
            _head.Next = _tail;
            _tail.Prev = _head;
        }
        
        public static void Initialize()
        {
            if (White1x1 != 0) return;
            
            White1x1 = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, White1x1);
            
            byte[] whitePixel = {255, 255, 255, 255};
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, 
                         1, 1, 0, PixelFormat.Rgba, PixelType.UnsignedByte, whitePixel);
            
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }
        
        public static int GetOrLoad(Guid textureGuid, Func<Guid, string?> resolvePath)
        {
            _currentFrame++;

            if (textureGuid == Guid.Empty)
                return White1x1;
                
            // Check if already loaded
            if (_textureNodes.TryGetValue(textureGuid, out var node))
            {
                MoveToHead(node);
                node.Value.LastUsedFrame = _currentFrame;
                return node.Value.GLHandle;
            }
            
            // Prevent multiple simultaneous loads
            if (_pendingLoads.Contains(textureGuid))
                return White1x1;

            var path = resolvePath(textureGuid);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                // Texture path not found - return white placeholder
                return White1x1;
            }

            // Check path cache (maybe another guid already loaded it)
            if (_pathNodes.TryGetValue(path, out node))
            {
                _textureNodes[textureGuid] = node; // Link GUID to same node
                MoveToHead(node);
                node.Value.LastUsedFrame = _currentFrame;
                return node.Value.GLHandle;
            }

            // Not loaded: schedule background load and return placeholder immediately
            _pendingLoads.Add(textureGuid);
            // Capture values for background task
            var captureGuid = textureGuid;
            var capturePath = path;

            Task.Run(() =>
            {
                try
                {
                    // Load image data on background thread (no GL calls)
                    using var fs = File.OpenRead(capturePath);
                    bool isHdr = capturePath.EndsWith(".hdr", StringComparison.OrdinalIgnoreCase);

                    // Read .meta (if exists) to detect normal map flags
                    bool metaIsNormal = false;
                    bool metaFlipGreen = false;
                    try
                    {
                        var metaPath = capturePath + Engine.Assets.AssetDatabase.MetaExt;
                        if (File.Exists(metaPath))
                        {
                            var jm = File.ReadAllText(metaPath);
                            using var docm = System.Text.Json.JsonDocument.Parse(jm);
                            if (docm.RootElement.TryGetProperty("isNormalMap", out var jn)) metaIsNormal = jn.GetBoolean();
                            if (docm.RootElement.TryGetProperty("flipGreen", out var jg)) metaFlipGreen = jg.GetBoolean();
                        }
                    }
                    catch { }

                    if (isHdr)
                    {
                        var hdr = StbImageSharp.ImageResultFloat.FromStream(fs, StbImageSharp.ColorComponents.RedGreenBlue);
                        // Convert HDR to LDR bytes for upload
                        var rgba8 = ConvertHDRToLDR(hdr.Data, hdr.Width, hdr.Height);
                        var size = CalculateTextureSize(hdr.Width, hdr.Height, PixelInternalFormat.Rgba8, false);
                        var pl = new PendingLoad { Guid = captureGuid, Path = capturePath, IsHdr = true, IsNormalMap = metaIsNormal, FlipGreen = metaFlipGreen, PixelData = rgba8, Width = hdr.Width, Height = hdr.Height, InternalFormat = PixelInternalFormat.Rgba8, SizeInBytes = size };
                        _uploadQueue.Enqueue(pl);
                    }
                    else
                    {
                        var img = StbImageSharp.ImageResult.FromStream(fs, StbImageSharp.ColorComponents.RedGreenBlueAlpha);
                        var data = img.Data; // RGBA8

                        // If meta says it's a normal map, preprocess: optionally flip green and renormalize
                        if (metaIsNormal && data != null)
                        {
                            try
                            {
                                // data is RGBA8
                                for (int i = 0; i < data.Length; i += 4)
                                {
                                    // Convert [0..255] -> [-1..1]
                                    float nx = (data[i] / 255f) * 2f - 1f;
                                    float ny = (data[i + 1] / 255f) * 2f - 1f;
                                    float nz = (data[i + 2] / 255f) * 2f - 1f;

                                    if (metaFlipGreen)
                                        ny = -ny;

                                    // Renormalize
                                    var len = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
                                    if (len > 0.0001f)
                                    {
                                        nx /= len; ny /= len; nz /= len;
                                    }

                                    // Back to [0..255]
                                    data[i] = (byte)Math.Max(0, Math.Min(255, (int)((nx * 0.5f + 0.5f) * 255f)));
                                    data[i + 1] = (byte)Math.Max(0, Math.Min(255, (int)((ny * 0.5f + 0.5f) * 255f)));
                                    data[i + 2] = (byte)Math.Max(0, Math.Min(255, (int)((nz * 0.5f + 0.5f) * 255f)));
                                    // alpha left as-is
                                }
                            }
                            catch { }
                        }

                        var actualData = data ?? new byte[] { 255, 255, 255, 255 };
                        var fmt = ChooseOptimalFormat(actualData, img.Width, img.Height);
                        var size = CalculateTextureSize(img.Width, img.Height, fmt, true);
                        var pl = new PendingLoad { Guid = captureGuid, Path = capturePath, IsHdr = false, IsNormalMap = metaIsNormal, FlipGreen = metaFlipGreen, PixelData = actualData, Width = img.Width, Height = img.Height, InternalFormat = fmt, SizeInBytes = size };
                        _uploadQueue.Enqueue(pl);
                    }
                }
                catch
                {
                    // PERFORMANCE: Disabled log (only log critical failures)
                    // Background load failed silently
                }
            });

            // Return placeholder now; the real texture will be created in ProcessPendingUploads()
            return White1x1;
        }

        /// <summary>
        /// Must be called from the main thread (GL context). Processes ALL pending uploads.
        /// Use this during scene loading to avoid texture pop-in over multiple frames.
        /// </summary>
        public static int FlushPendingUploads(int maxUploads = 100)
        {
            int processed = 0;
            while (processed < maxUploads && _uploadQueue.TryDequeue(out var pl))
            {
                try
                {
                    // If already loaded by another path, skip
                    if (_pathNodes.ContainsKey(pl.Path))
                    {
                        // Link GUID to existing node if present
                        var existing = _pathNodes[pl.Path];
                        _textureNodes[pl.Guid] = existing;
                        MoveToHead(existing);
                        existing.Value.LastUsedFrame = _currentFrame;
                        _pendingLoads.Remove(pl.Guid);
                        processed++;
                        continue;
                    }

                    EnsureCacheSpace();

                    // Create GL texture from pixel data
                    int handle = GL.GenTexture();
                    GL.BindTexture(TextureTarget.Texture2D, handle);
                    GL.TexImage2D(TextureTarget.Texture2D, 0, pl.InternalFormat, pl.Width, pl.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pl.PixelData);

                    // Generate mipmaps for non-HDR uploads
                    if (!pl.IsHdr)
                    {
                        GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
                        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
                    }
                    else
                    {
                        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                    }

                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
                    GL.BindTexture(TextureTarget.Texture2D, 0);

                    var entry = new TextureEntry
                    {
                        GLHandle = handle,
                        Width = pl.Width,
                        Height = pl.Height,
                        LastUsedFrame = _currentFrame,
                        SizeInBytes = pl.SizeInBytes,
                        IsResident = true
                    };

                    // PERFORMANCE: Disabled log (only enabled during FlushPendingUploads)
                    // try { Console.WriteLine($"[TextureCache] Uploaded texture: path={pl.Path}, guid={pl.Guid}, handle={handle}, size={pl.Width}x{pl.Height}"); } catch { }

                    var node = new LRUNode { Key = pl.Guid, Value = entry };
                    _textureNodes[pl.Guid] = node;
                    _pathNodes[pl.Path] = node;
                    AddToHead(node);
                    _totalMemoryUsed += entry.SizeInBytes;

                    _pendingLoads.Remove(pl.Guid);
                }
                catch (Exception ex)
                {
                    try { Console.WriteLine($"[TextureCache] Failed to finalize upload: {ex.Message}"); } catch { }
                    _pendingLoads.Remove(pl.Guid);
                }

                processed++;
            }
            return processed;
        }

        /// <summary>
        /// Must be called from the main thread (GL context). Processes a limited number of pending uploads
        /// prepared by background image loads to create GL textures without stalling the main thread for large IO.
        /// </summary>
        public static int ProcessPendingUploads(int maxPerFrame = 2)
        {
            int processed = 0;
            while (processed < maxPerFrame && _uploadQueue.TryDequeue(out var pl))
            {
                try
                {
                    // If already loaded by another path, skip
                    if (_pathNodes.ContainsKey(pl.Path))
                    {
                        // Link GUID to existing node if present
                        var existing = _pathNodes[pl.Path];
                        _textureNodes[pl.Guid] = existing;
                        MoveToHead(existing);
                        existing.Value.LastUsedFrame = _currentFrame;
                        _pendingLoads.Remove(pl.Guid);
                        processed++;
                        continue;
                    }

                    EnsureCacheSpace();

                    // Create GL texture from pixel data
                    int handle = GL.GenTexture();
                    GL.BindTexture(TextureTarget.Texture2D, handle);
                    GL.TexImage2D(TextureTarget.Texture2D, 0, pl.InternalFormat, pl.Width, pl.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pl.PixelData);

                    // Generate mipmaps for non-HDR uploads
                    if (!pl.IsHdr)
                    {
                        GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
                        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
                    }
                    else
                    {
                        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                    }

                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
                    GL.BindTexture(TextureTarget.Texture2D, 0);

                    var entry = new TextureEntry
                    {
                        GLHandle = handle,
                        Width = pl.Width,
                        Height = pl.Height,
                        LastUsedFrame = _currentFrame,
                        SizeInBytes = pl.SizeInBytes,
                        IsResident = true
                    };

                    // PERFORMANCE: Disabled log
                    // try { Console.WriteLine($"[TextureCache] Uploaded texture: path={pl.Path}, guid={pl.Guid}, handle={handle}, size={pl.Width}x{pl.Height}"); } catch { }

                    var node = new LRUNode { Key = pl.Guid, Value = entry };
                    _textureNodes[pl.Guid] = node;
                    _pathNodes[pl.Path] = node;
                    AddToHead(node);
                    _totalMemoryUsed += entry.SizeInBytes;

                    _pendingLoads.Remove(pl.Guid);
                }
                catch
                {
                    // PERFORMANCE: Disabled log - upload failed silently
                    _pendingLoads.Remove(pl.Guid);
                }

                processed++;
            }
            return processed;
        }
        
        private static void EnsureCacheSpace()
        {
            // Remove least recently used textures if over limits
            while ((_totalMemoryUsed > MAX_TEXTURE_MEMORY || _textureNodes.Count > MAX_TEXTURE_COUNT) 
                   && _tail.Prev != _head)
            {
                var lru = _tail.Prev;
                RemoveTexture(lru);
            }
        }
        
        private static void RemoveTexture(LRUNode node)
        {
            if (node?.Value == null) return;
            
            // Remove from OpenGL
            GL.DeleteTexture(node.Value.GLHandle);
            
            // Update memory tracking
            _totalMemoryUsed -= node.Value.SizeInBytes;
            
            // Remove from all caches
            _textureNodes.Remove(node.Key);
            
            // Remove from path cache (find the path)
            var pathToRemove = "";
            foreach (var kvp in _pathNodes)
            {
                if (kvp.Value == node)
                {
                    pathToRemove = kvp.Key;
                    break;
                }
            }
            if (!string.IsNullOrEmpty(pathToRemove))
                _pathNodes.Remove(pathToRemove);
            
            // Remove from LRU list
            RemoveFromList(node);
        }

        /// <summary>
        /// Remove a texture from the cache so the next GetOrLoad will reload it from disk.
        /// </summary>
        public static void Invalidate(Guid guid)
        {
            try
            {
                if (_textureNodes.TryGetValue(guid, out var node))
                {
                    RemoveTexture(node);
                }
                _pendingLoads.Remove(guid);
            }
            catch { }
        }
        
        private static (int Handle, int Width, int Height, int SizeInBytes) LoadTextureFromFile(string path)
        {
            using var fs = File.OpenRead(path);
            bool isHdr = path.EndsWith(".hdr", StringComparison.OrdinalIgnoreCase);
            int handle = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, handle);

            int width, height;
            PixelInternalFormat internalFormat;
            if (isHdr)
            {
                // Load HDR with memory optimization for preview/UI use
                fs.Position = 0;
                var hdr = ImageResultFloat.FromStream(fs, ColorComponents.RedGreenBlue);
                width = hdr.Width; height = hdr.Height;

                // For very large HDR textures (4K+), downsample for UI preview to save memory
                int maxPreviewSize = 1024; // Maximum texture size for UI preview
                bool needsDownsampling = width > maxPreviewSize || height > maxPreviewSize;

                if (needsDownsampling)
                {
                    // Calculate downsampled size maintaining aspect ratio
                    float aspectRatio = (float)width / height;
                    int newWidth, newHeight;
                    if (width > height)
                    {
                        newWidth = maxPreviewSize;
                        newHeight = (int)(maxPreviewSize / aspectRatio);
                    }
                    else
                    {
                        newHeight = maxPreviewSize;
                        newWidth = (int)(maxPreviewSize * aspectRatio);
                    }

                    // Simple box filter downsampling
                    var downsampled = DownsampleHDRData(hdr.Data, width, height, newWidth, newHeight);
                    width = newWidth;
                    height = newHeight;

                    // Use RGBA8 for downsampled preview (much less memory than RGBA16F)
                    internalFormat = PixelInternalFormat.Rgba8;
                    var rgba8 = ConvertHDRToLDR(downsampled, width, height);

                    GL.TexImage2D(TextureTarget.Texture2D, 0, internalFormat, width, height, 0,
                        PixelFormat.Rgba, PixelType.UnsignedByte, rgba8);

                }
                else
                {
                    // For smaller HDR textures, use RGBA8 with tone mapping instead of RGBA16F
                    internalFormat = PixelInternalFormat.Rgba8;
                    var rgba8 = ConvertHDRToLDR(hdr.Data, width, height);

                    GL.TexImage2D(TextureTarget.Texture2D, 0, internalFormat, width, height, 0,
                        PixelFormat.Rgba, PixelType.UnsignedByte, rgba8);
                }
            }
            else
            {
                var img = ImageResult.FromStream(fs, ColorComponents.RedGreenBlueAlpha);
                width = img.Width; height = img.Height;
                internalFormat = ChooseOptimalFormat(img.Data, img.Width, img.Height);
                GL.TexImage2D(TextureTarget.Texture2D, 0, internalFormat,
                             img.Width, img.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, img.Data);
            }

            // For HDR textures, skip mipmaps to save memory (they're mainly for UI preview anyway)
            if (!isHdr)
            {
                // Generate mipmaps for better quality and performance (non-HDR only)
                GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);

                // Use mipmap filtering for non-HDR textures
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);

                // Anisotropic filtering if available (non-HDR only)
                if (GL.GetString(StringName.Extensions).Contains("GL_EXT_texture_filter_anisotropic"))
                {
                    float maxAnisotropy;
                    GL.GetFloat((GetPName)0x84FF, out maxAnisotropy); // GL_MAX_TEXTURE_MAX_ANISOTROPY_EXT
                    GL.TexParameter(TextureTarget.Texture2D, (TextureParameterName)0x84FE, Math.Min(4.0f, maxAnisotropy)); // GL_TEXTURE_MAX_ANISOTROPY_EXT
                }
            }
            else
            {
                // For HDR textures, use simple linear filtering without mipmaps
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            }

            // Basic filtering settings for all textures
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            
            GL.BindTexture(TextureTarget.Texture2D, 0);
            
            int sizeInBytes = CalculateTextureSize(width, height, internalFormat, !isHdr);
            
            return (handle, width, height, sizeInBytes);
        }
        
        private static PixelInternalFormat ChooseOptimalFormat(byte[] data, int width, int height)
        {
            // Simple heuristic: if mostly opaque, use RGB8, otherwise RGBA8
            bool hasAlpha = false;
            for (int i = 3; i < data.Length && !hasAlpha; i += 4)
            {
                if (data[i] < 255)
                    hasAlpha = true;
            }
            
            return hasAlpha ? PixelInternalFormat.Rgba8 : PixelInternalFormat.Rgb8;
        }
        
        private static int CalculateTextureSize(int width, int height, PixelInternalFormat format, bool hasMipmaps = true)
        {
            int bytesPerPixel = format switch
            {
                PixelInternalFormat.Rgb8 => 3,
                PixelInternalFormat.Rgba8 => 4,
                PixelInternalFormat.Rgba16f => 8,
                PixelInternalFormat.CompressedRgbaS3tcDxt1Ext => width * height / 2,
                PixelInternalFormat.CompressedRgbaS3tcDxt5Ext => width * height,
                _ => 4
            };

            int baseSize = width * height * bytesPerPixel;

            // Account for mipmaps (adds ~33% more) only if mipmaps are generated
            return hasMipmaps ? (int)(baseSize * 1.33f) : baseSize;
        }

        /// <summary>
        /// Downsample HDR data using box filtering
        /// </summary>
        private static float[] DownsampleHDRData(float[] sourceData, int srcWidth, int srcHeight, int dstWidth, int dstHeight)
        {
            var destData = new float[dstWidth * dstHeight * 3]; // RGB
            float xRatio = (float)srcWidth / dstWidth;
            float yRatio = (float)srcHeight / dstHeight;

            for (int y = 0; y < dstHeight; y++)
            {
                for (int x = 0; x < dstWidth; x++)
                {
                    // Sample area in source image
                    int srcX = (int)(x * xRatio);
                    int srcY = (int)(y * yRatio);
                    int srcX2 = Math.Min(srcX + (int)Math.Ceiling(xRatio), srcWidth - 1);
                    int srcY2 = Math.Min(srcY + (int)Math.Ceiling(yRatio), srcHeight - 1);

                    // Box filter sampling
                    float r = 0, g = 0, b = 0;
                    int count = 0;

                    for (int sy = srcY; sy <= srcY2; sy++)
                    {
                        for (int sx = srcX; sx <= srcX2; sx++)
                        {
                            int srcIndex = (sy * srcWidth + sx) * 3;
                            r += sourceData[srcIndex];
                            g += sourceData[srcIndex + 1];
                            b += sourceData[srcIndex + 2];
                            count++;
                        }
                    }

                    // Average and store
                    int dstIndex = (y * dstWidth + x) * 3;
                    destData[dstIndex] = r / count;
                    destData[dstIndex + 1] = g / count;
                    destData[dstIndex + 2] = b / count;
                }
            }

            return destData;
        }

        /// <summary>
        /// Convert HDR data to LDR using simple tone mapping
        /// </summary>
        private static byte[] ConvertHDRToLDR(float[] hdrData, int width, int height)
        {
            var ldrData = new byte[width * height * 4]; // RGBA
            float exposure = 1.0f;
            float gamma = 2.2f;

            for (int i = 0, j = 0; i < hdrData.Length; i += 3, j += 4)
            {
                // Simple tone mapping: exposure + gamma correction
                float r = hdrData[i] * exposure;
                float g = hdrData[i + 1] * exposure;
                float b = hdrData[i + 2] * exposure;

                // Reinhard tone mapping
                r = r / (1.0f + r);
                g = g / (1.0f + g);
                b = b / (1.0f + b);

                // Gamma correction
                r = (float)Math.Pow(r, 1.0 / gamma);
                g = (float)Math.Pow(g, 1.0 / gamma);
                b = (float)Math.Pow(b, 1.0 / gamma);

                // Convert to bytes
                ldrData[j] = (byte)Math.Max(0, Math.Min(255, r * 255));
                ldrData[j + 1] = (byte)Math.Max(0, Math.Min(255, g * 255));
                ldrData[j + 2] = (byte)Math.Max(0, Math.Min(255, b * 255));
                ldrData[j + 3] = 255; // Alpha
            }

            return ldrData;
        }
        
        // LRU management
        private static void MoveToHead(LRUNode node)
        {
            RemoveFromList(node);
            AddToHead(node);
        }
        
        private static void AddToHead(LRUNode node)
        {
            node.Prev = _head;
            node.Next = _head.Next;
            _head.Next.Prev = node;
            _head.Next = node;
        }
        
        private static void RemoveFromList(LRUNode node)
        {
            node.Prev.Next = node.Next;
            node.Next.Prev = node.Prev;
        }
        
        // Public management methods
        public static void FreeUnusedTextures(int framesToKeep = 300)
        {
            var cutoffFrame = _currentFrame - framesToKeep;
            var toRemove = new List<LRUNode>();
            
            var current = _tail.Prev;
            while (current != _head)
            {
                if (current.Value.LastUsedFrame < cutoffFrame)
                {
                    toRemove.Add(current);
                }
                current = current.Prev;
            }
            
            foreach (var node in toRemove)
            {
                RemoveTexture(node);
            }
        }
        
        public static void PreloadTexture(Guid textureGuid, Func<Guid, string?> resolvePath)
        {
            // Load texture with low priority
            if (!_textureNodes.ContainsKey(textureGuid))
            {
                GetOrLoad(textureGuid, resolvePath);
            }
        }
        
        public static void ClearCache()
        {
            // Keep White1x1
            var current = _tail.Prev;
            while (current != _head)
            {
                var prev = current.Prev;
                if (current.Value.GLHandle != White1x1)
                {
                    RemoveTexture(current);
                }
                current = prev;
            }
        }
        
        public static void GetCacheStats(out int textureCount, out long memoryUsed, out int maxTextures, out long maxMemory)
        {
            textureCount = _textureNodes.Count;
            memoryUsed = _totalMemoryUsed;
            maxTextures = MAX_TEXTURE_COUNT;
            maxMemory = MAX_TEXTURE_MEMORY;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using OpenTK.Graphics.OpenGL4;

namespace Engine.Profiling
{
    /// <summary>
    /// Tracks GPU memory usage (textures, buffers, VRAM).
    /// Provides memory profiling and leak detection.
    /// </summary>
    public static class GPUMemoryTracker
    {
        private class ResourceInfo
        {
            public int Handle;
            public string Name = "";
            public long SizeBytes;
            public ResourceType Type;
            public DateTime CreatedTime;
            public string CreatedFrom = ""; // Stack trace or source
        }

        private enum ResourceType
        {
            Texture,
            Buffer,
            Framebuffer,
            Other
        }

        private static readonly Dictionary<int, ResourceInfo> _textures = new();
        private static readonly Dictionary<int, ResourceInfo> _buffers = new();
        private static readonly Dictionary<int, ResourceInfo> _framebuffers = new();
        private static readonly object _lock = new object();

        private static bool _enabled = true;

        /// <summary>Enable/disable memory tracking</summary>
        public static bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        #region Texture Tracking

        /// <summary>
        /// Track a texture
        /// </summary>
        public static void TrackTexture(int handle, int width, int height, PixelInternalFormat format, string name = "")
        {
            if (!_enabled) return;

            try
            {
                long sizeBytes = EstimateTextureSize(width, height, format);

                lock (_lock)
                {
                    _textures[handle] = new ResourceInfo
                    {
                        Handle = handle,
                        Name = string.IsNullOrEmpty(name) ? $"Texture_{handle}" : name,
                        SizeBytes = sizeBytes,
                        Type = ResourceType.Texture,
                        CreatedTime = DateTime.Now
                    };
                }
            }
            catch { }
        }

        /// <summary>
        /// Untrack a texture
        /// </summary>
        public static void UntrackTexture(int handle)
        {
            if (!_enabled) return;

            lock (_lock)
            {
                _textures.Remove(handle);
            }
        }

        /// <summary>
        /// Estimate texture memory size
        /// </summary>
        private static long EstimateTextureSize(int width, int height, PixelInternalFormat format)
        {
            int bytesPerPixel = format switch
            {
                PixelInternalFormat.R8 => 1,
                PixelInternalFormat.Rg8 => 2,
                PixelInternalFormat.Rgb8 => 3,
                PixelInternalFormat.Rgba8 => 4,
                PixelInternalFormat.R16f => 2,
                PixelInternalFormat.Rg16f => 4,
                PixelInternalFormat.Rgb16f => 6,
                PixelInternalFormat.Rgba16f => 8,
                PixelInternalFormat.R32f => 4,
                PixelInternalFormat.Rg32f => 8,
                PixelInternalFormat.Rgb32f => 12,
                PixelInternalFormat.Rgba32f => 16,
                PixelInternalFormat.DepthComponent16 => 2,
                PixelInternalFormat.DepthComponent24 => 3,
                PixelInternalFormat.DepthComponent32f => 4,
                PixelInternalFormat.Depth24Stencil8 => 4,
                PixelInternalFormat.Depth32fStencil8 => 5,
                _ => 4 // Default to RGBA8
            };

            // Account for mipmaps (adds ~33% more memory)
            long baseSize = (long)width * height * bytesPerPixel;
            long totalSize = baseSize + (baseSize / 3); // Base + mipmaps

            return totalSize;
        }

        #endregion

        #region Buffer Tracking

        /// <summary>
        /// Track a buffer (VBO, EBO, UBO, etc.)
        /// </summary>
        public static void TrackBuffer(int handle, long sizeBytes, BufferTarget target, string name = "")
        {
            if (!_enabled) return;

            try
            {
                lock (_lock)
                {
                    string bufferType = target switch
                    {
                        BufferTarget.ArrayBuffer => "VBO",
                        BufferTarget.ElementArrayBuffer => "EBO",
                        BufferTarget.UniformBuffer => "UBO",
                        BufferTarget.ShaderStorageBuffer => "SSBO",
                        _ => target.ToString()
                    };

                    _buffers[handle] = new ResourceInfo
                    {
                        Handle = handle,
                        Name = string.IsNullOrEmpty(name) ? $"{bufferType}_{handle}" : name,
                        SizeBytes = sizeBytes,
                        Type = ResourceType.Buffer,
                        CreatedTime = DateTime.Now
                    };
                }
            }
            catch { }
        }

        /// <summary>
        /// Untrack a buffer
        /// </summary>
        public static void UntrackBuffer(int handle)
        {
            if (!_enabled) return;

            lock (_lock)
            {
                _buffers.Remove(handle);
            }
        }

        #endregion

        #region Framebuffer Tracking

        /// <summary>
        /// Track a framebuffer
        /// </summary>
        public static void TrackFramebuffer(int handle, long estimatedSizeBytes, string name = "")
        {
            if (!_enabled) return;

            try
            {
                lock (_lock)
                {
                    _framebuffers[handle] = new ResourceInfo
                    {
                        Handle = handle,
                        Name = string.IsNullOrEmpty(name) ? $"FBO_{handle}" : name,
                        SizeBytes = estimatedSizeBytes,
                        Type = ResourceType.Framebuffer,
                        CreatedTime = DateTime.Now
                    };
                }
            }
            catch { }
        }

        /// <summary>
        /// Untrack a framebuffer
        /// </summary>
        public static void UntrackFramebuffer(int handle)
        {
            if (!_enabled) return;

            lock (_lock)
            {
                _framebuffers.Remove(handle);
            }
        }

        #endregion

        #region Statistics

        /// <summary>
        /// Get total VRAM usage in bytes
        /// </summary>
        public static long GetTotalVRAMBytes()
        {
            lock (_lock)
            {
                long total = 0;
                foreach (var tex in _textures.Values)
                    total += tex.SizeBytes;
                foreach (var buf in _buffers.Values)
                    total += buf.SizeBytes;
                foreach (var fbo in _framebuffers.Values)
                    total += fbo.SizeBytes;
                return total;
            }
        }

        /// <summary>
        /// Get total VRAM usage in megabytes
        /// </summary>
        public static float GetTotalVRAMMB()
        {
            return GetTotalVRAMBytes() / (1024f * 1024f);
        }

        /// <summary>
        /// Get texture memory usage in bytes
        /// </summary>
        public static long GetTextureMemoryBytes()
        {
            lock (_lock)
            {
                long total = 0;
                foreach (var tex in _textures.Values)
                    total += tex.SizeBytes;
                return total;
            }
        }

        /// <summary>
        /// Get texture memory usage in megabytes
        /// </summary>
        public static float GetTextureMemoryMB()
        {
            return GetTextureMemoryBytes() / (1024f * 1024f);
        }

        /// <summary>
        /// Get buffer memory usage in bytes
        /// </summary>
        public static long GetBufferMemoryBytes()
        {
            lock (_lock)
            {
                long total = 0;
                foreach (var buf in _buffers.Values)
                    total += buf.SizeBytes;
                return total;
            }
        }

        /// <summary>
        /// Get buffer memory usage in megabytes
        /// </summary>
        public static float GetBufferMemoryMB()
        {
            return GetBufferMemoryBytes() / (1024f * 1024f);
        }

        /// <summary>
        /// Get memory breakdown by category
        /// </summary>
        public static Dictionary<string, long> GetMemoryByCategory()
        {
            lock (_lock)
            {
                return new Dictionary<string, long>
                {
                    { "Textures", GetTextureMemoryBytes() },
                    { "Buffers", GetBufferMemoryBytes() },
                    { "Framebuffers", _framebuffers.Values.Sum(x => x.SizeBytes) }
                };
            }
        }

        /// <summary>
        /// Get list of largest textures
        /// </summary>
        public static List<(string name, long sizeBytes)> GetTopTextures(int count = 10)
        {
            lock (_lock)
            {
                return _textures.Values
                    .OrderByDescending(x => x.SizeBytes)
                    .Take(count)
                    .Select(x => (x.Name, x.SizeBytes))
                    .ToList();
            }
        }

        /// <summary>
        /// Get list of largest buffers
        /// </summary>
        public static List<(string name, long sizeBytes)> GetTopBuffers(int count = 10)
        {
            lock (_lock)
            {
                return _buffers.Values
                    .OrderByDescending(x => x.SizeBytes)
                    .Take(count)
                    .Select(x => (x.Name, x.SizeBytes))
                    .ToList();
            }
        }

        /// <summary>
        /// Get resource counts
        /// </summary>
        public static (int textures, int buffers, int framebuffers) GetResourceCounts()
        {
            lock (_lock)
            {
                return (_textures.Count, _buffers.Count, _framebuffers.Count);
            }
        }

        /// <summary>
        /// Clear all tracking data
        /// </summary>
        public static void Clear()
        {
            lock (_lock)
            {
                _textures.Clear();
                _buffers.Clear();
                _framebuffers.Clear();
            }
        }

        /// <summary>
        /// Get a summary string for debugging
        /// </summary>
        public static string GetSummary()
        {
            var (texCount, bufCount, fboCount) = GetResourceCounts();
            float totalMB = GetTotalVRAMMB();
            float texMB = GetTextureMemoryMB();
            float bufMB = GetBufferMemoryMB();

            return $"VRAM: {totalMB:F1} MB (Textures: {texMB:F1} MB [{texCount}], Buffers: {bufMB:F1} MB [{bufCount}], FBOs: [{fboCount}])";
        }

        #endregion
    }
}

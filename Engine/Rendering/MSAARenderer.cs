using System;
using OpenTK.Graphics.OpenGL4;

namespace Engine.Rendering
{
    /// <summary>
    /// MSAA (MultiSample Anti-Aliasing) renderer
    /// Hardware-accelerated anti-aliasing using OpenGL multisampling
    /// Fast and simple alternative to TAA for removing jagged edges
    /// </summary>
    public sealed class MSAARenderer : IDisposable
    {
        private uint _msaaFBO;
        private uint _msaaColorTex;
        private uint _msaaColorRBO;
        private uint _msaaIdRBO;      // ID renderbuffer (not resolved, just for shader output)
        private uint _msaaDepthRBO;
        private int _width;
        private int _height;
        private int _samples;

        public int Samples => _samples;
        public uint FramebufferId => _msaaFBO;

        /// <summary>
        /// Create MSAA renderer with specified sample count
        /// </summary>
        /// <param name="width">Framebuffer width</param>
        /// <param name="height">Framebuffer height</param>
        /// <param name="samples">Sample count (2, 4, 8, or 16)</param>
        public MSAARenderer(int width, int height, int samples)
        {
            _width = width;
            _height = height;
            // Clamp requested samples to hardware max supported by GL
            int maxSamples = 0;
            try { GL.GetInteger(GetPName.MaxSamples, out maxSamples); } catch { maxSamples = 0; }
            int requested = Math.Clamp(samples, 2, 16);
            if (maxSamples > 0)
                _samples = Math.Clamp(requested, 1, Math.Max(1, maxSamples));
            else
                _samples = requested; // Fallback if query failed

            CreateFramebuffer();
        }

        private void CreateFramebuffer()
        {
            // Create multisampled framebuffer
            _msaaFBO = (uint)GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _msaaFBO);

            // Create multisampled color texture (ColorAttachment0)
            _msaaColorTex = (uint)GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2DMultisample, _msaaColorTex);
            GL.TexImage2DMultisample(TextureTargetMultisample.Texture2DMultisample, _samples, PixelInternalFormat.Rgba16f, _width, _height, true);
            var err1 = GL.GetError();
            if (err1 != ErrorCode.NoError) Console.WriteLine($"[MSAA] ERROR after color texture creation: {err1}");
            if (err1 == ErrorCode.NoError)
            {
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2DMultisample, _msaaColorTex, 0);
            }
            else
            {
                // Fallback: create a multisampled renderbuffer for color if texture creation failed
                try
                {
                    Console.WriteLine("[MSAA] Falling back to multisample renderbuffer for color attachment");
                    _msaaColorRBO = (uint)GL.GenRenderbuffer();
                    GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _msaaColorRBO);
                    GL.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, _samples, RenderbufferStorage.Rgba8, _width, _height);
                    GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, _msaaColorRBO);
                }
                catch (Exception)
                {
                    // If fallback fails, leave it and continue to detect incomplete FBO later
                }
            }
            var err2 = GL.GetError();
            if (err2 != ErrorCode.NoError) Console.WriteLine($"[MSAA] ERROR after color attachment: {err2}");

            // Create ID renderbuffer with R32ui format (ColorAttachment1)
            // This allows shaders to write to o_EntityId without errors
            // We won't resolve this - it's just a dummy target for shader output
            _msaaIdRBO = (uint)GL.GenRenderbuffer();
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _msaaIdRBO);
            GL.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, _samples, RenderbufferStorage.R32ui, _width, _height);
            var err3 = GL.GetError();
            if (err3 != ErrorCode.NoError)
            {
                Console.WriteLine($"[MSAA] WARNING: R32ui not supported for MSAA renderbuffer (err={err3}), trying R8ui...");
                // Try R8ui as fallback
                GL.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, _samples, RenderbufferStorage.R8ui, _width, _height);
                var err3b = GL.GetError();
                if (err3b != ErrorCode.NoError)
                {
                    Console.WriteLine($"[MSAA] WARNING: R8ui also failed (err={err3b}), trying R8...");
                    // Last resort: normalized format
                    GL.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, _samples, RenderbufferStorage.R8, _width, _height);
                }
            }
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1, RenderbufferTarget.Renderbuffer, _msaaIdRBO);
            var err4 = GL.GetError();
            if (err4 != ErrorCode.NoError) Console.WriteLine($"[MSAA] ERROR after ID attachment: {err4}");

            // Create multisampled depth renderbuffer
            _msaaDepthRBO = (uint)GL.GenRenderbuffer();
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _msaaDepthRBO);
            GL.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, _samples, RenderbufferStorage.DepthComponent32f, _width, _height);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, _msaaDepthRBO);

            // Enable both color attachments for rendering
            GL.DrawBuffers(2, new[] { DrawBuffersEnum.ColorAttachment0, DrawBuffersEnum.ColorAttachment1 });

            // Check framebuffer completeness
            var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != FramebufferErrorCode.FramebufferComplete)
            {
                Console.WriteLine($"[MSAA] ERROR: Framebuffer not complete! Status={status}, samples={_samples}");
                throw new Exception($"MSAA framebuffer not complete! Status={status}, samples={_samples}");
            }

            var err = GL.GetError();
            if (err != ErrorCode.NoError)
            {
                Console.WriteLine($"[MSAA] ERROR: OpenGL error after framebuffer creation: {err}");
            }
            else
            {
                Console.WriteLine($"[MSAA] Framebuffer created successfully: {_width}x{_height} @ {_samples}x samples, FBO={_msaaFBO}");
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        /// <summary>
        /// Resize MSAA framebuffer
        /// </summary>
        public void Resize(int width, int height)
        {
            if (_width == width && _height == height) return;

            _width = width;
            _height = height;

            // Recreate framebuffer
            Dispose();
            CreateFramebuffer();
        }

        /// <summary>
        /// Begin rendering to MSAA framebuffer
        /// </summary>
        public void BeginRender()
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _msaaFBO);
            var err = GL.GetError();
            if (err != ErrorCode.NoError)
            {
                Console.WriteLine($"[MSAA] ERROR in BeginRender after binding FBO: {err}");
            }

            // Verify framebuffer is still complete
            var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != FramebufferErrorCode.FramebufferComplete)
            {
                Console.WriteLine($"[MSAA] ERROR in BeginRender: FBO not complete! Status={status}");
            }

            // BeginRender: silent in normal operation to avoid per-frame console spam
            // Note: Don't clear here - ViewportRenderer will do it
        }

        /// <summary>
        /// Resolve MSAA framebuffer to regular framebuffer (blit color only)
        /// </summary>
        /// <param name="targetFBO">Target framebuffer to blit to</param>
        public void ResolveToFramebuffer(uint targetFBO)
        {
            // ResolveToFramebuffer: perform blit without verbose logging

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _msaaFBO);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, targetFBO);

            // Blit only color attachment 0 (color texture)
            // ID texture (ColorAttachment1) is NOT multisampled and remains in target FBO
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
            GL.BlitFramebuffer(
                0, 0, _width, _height,
                0, 0, _width, _height,
                ClearBufferMask.ColorBufferBit,
                BlitFramebufferFilter.Nearest
            );

            var err = GL.GetError();
            if (err != ErrorCode.NoError)
            {
                Console.WriteLine($"[MSAA] ERROR during blit: {err}");
            }

            // CRITICAL: Restore DrawBuffers to render to both color attachments in target FBO
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, targetFBO);
            GL.DrawBuffers(2, new[] { DrawBuffersEnum.ColorAttachment0, DrawBuffersEnum.ColorAttachment1 });

            // Resolve complete (silent)
        }

        public void Dispose()
        {
            if (_msaaFBO != 0)
            {
                GL.DeleteFramebuffer(_msaaFBO);
                _msaaFBO = 0;
            }
            if (_msaaColorTex != 0)
            {
                GL.DeleteTexture(_msaaColorTex);
                _msaaColorTex = 0;
            }
            if (_msaaIdRBO != 0)
            {
                GL.DeleteRenderbuffer(_msaaIdRBO);
                _msaaIdRBO = 0;
            }
            if (_msaaDepthRBO != 0)
            {
                GL.DeleteRenderbuffer(_msaaDepthRBO);
                _msaaDepthRBO = 0;
            }
        }
    }
}

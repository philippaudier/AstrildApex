using System;
using System.IO;
using Engine.Components;
using Engine.Serialization;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace Engine.Rendering
{
    /// <summary>
    /// Effet de tone mapping pour convertir HDR vers LDR
    /// </summary>
    public class ToneMappingEffect : PostProcessEffect
    {
        public override string EffectName => "Tone Mapping";

        public enum ToneMappingMode
        {
            None,
            Reinhard,
            ReinhardExtended,
            Filmic,
            ACES
        }

        [Engine.Serialization.SerializableAttribute("mode")]
        public ToneMappingMode Mode { get; set; } = ToneMappingMode.Filmic;
        
        [Engine.Serialization.SerializableAttribute("exposure")]
        public float Exposure { get; set; } = 1.0f;
        
        [Engine.Serialization.SerializableAttribute("whitepoint")]
        public float WhitePoint { get; set; } = 1.0f; // Pour Reinhard Extended
        
        [Engine.Serialization.SerializableAttribute("gamma")]
        public float Gamma { get; set; } = 2.2f;

        // Auto-exposure settings
        [Engine.Serialization.SerializableAttribute("autoexposure")]
        public bool AutoExposure { get; set; } = false;

        [Engine.Serialization.SerializableAttribute("minexposure")]
        public float MinExposure { get; set; } = 0.3f;

        [Engine.Serialization.SerializableAttribute("maxexposure")]
        public float MaxExposure { get; set; } = 3.0f;

        [Engine.Serialization.SerializableAttribute("adaptationspeed")]
        public float AdaptationSpeed { get; set; } = 2.0f; // Vitesse d'adaptation (1.0 = lent, 5.0 = rapide)

        [Engine.Serialization.SerializableAttribute("targetbrightness")]
        public float TargetBrightness { get; set; } = 0.5f; // Luminance cible (0.5 = gris moyen)

        [Engine.Serialization.SerializableAttribute("exposurecompensation")]
        public float ExposureCompensation { get; set; } = 0.0f; // Bias manuel en EV (-2 = plus sombre, +2 = plus lumineux)

        public ToneMappingEffect()
        {
            Priority = 10; // Après bloom, avant chromatic aberration
        }

        public override void Apply(PostProcessContext context)
        {
            // L'application sera gérée par ToneMappingRenderer
        }
    }

    /// <summary>
    /// Effet de bloom pour les zones lumineuses HDR
    /// </summary>
    public class BloomEffect : PostProcessEffect
    {
        public override string EffectName => "Bloom";

        [Engine.Serialization.SerializableAttribute("threshold")]
        public float Threshold { get; set; } = 1.0f; // Seuil pour extraire les zones lumineuses

        [Engine.Serialization.SerializableAttribute("softknee")]
        public float SoftKnee { get; set; } = 0.5f; // Transition douce autour du seuil

        [Engine.Serialization.SerializableAttribute("radius")]
        public float Radius { get; set; } = 1.0f; // Rayon du bloom

        [Engine.Serialization.SerializableAttribute("iterations")]
        public int Iterations { get; set; } = 6; // Nombre de passes (réduit de 6 à 4 pour performance)

        [Engine.Serialization.SerializableAttribute("clamp")]
        public float Clamp { get; set; } = 65472.0f; // Clamp pour éviter les valeurs infinies HDR

        [Engine.Serialization.SerializableAttribute("scattering")]
        public float Scattering { get; set; } = 0.7f; // Contrôle de la diffusion du bloom

        public BloomEffect()
        {
            Priority = 0; // Premier post-process (avant tone mapping)
        }

        public override void Apply(PostProcessContext context)
        {
            // L'application sera gérée par BloomRenderer
        }
    }

    /// <summary>
    /// Effet d'aberration chromatique
    /// </summary>
    public class ChromaticAberrationEffect : PostProcessEffect
    {
        public override string EffectName => "Chromatic Aberration";

        [Engine.Serialization.SerializableAttribute("strength")]
        public float Strength { get; set; } = 0.5f;
        
        [Engine.Serialization.SerializableAttribute("usespectrallut")]
        public bool UseSpectralLut { get; set; } = false;
        
        [Engine.Serialization.SerializableAttribute("focallength")]
        public float FocalLength { get; set; } = 50.0f; // Distance focale en mm pour le réalisme

        public ChromaticAberrationEffect()
        {
            Priority = 20; // Dernier effet (sur l'image LDR finale)
        }

        public override void Apply(PostProcessContext context)
        {
            // L'application sera gérée par ChromaticAberrationRenderer
        }
    }

    /// <summary>
    /// FXAA post-process effect (fast approximate anti-aliasing, triple-A variant)
    /// </summary>
    public class FXAAEffect : PostProcessEffect
    {
        public override string EffectName => "FXAA (Triple-A)";

        // Quality factor 0..1 (0 = low perf, 1 = high quality)
        [Engine.Serialization.SerializableAttribute("quality")]
        public float Quality { get; set; } = 1.0f;

        

        public FXAAEffect()
        {
            Priority = 15; // After tone mapping but before chromatic aberration, adjustable
        }

        public override void Apply(PostProcessContext context)
        {
            // Rendered by FXAARenderer
        }
    }

    /// <summary>
    /// Effet SSAO (Screen Space Ambient Occlusion) - post-processing
    /// </summary>
    public class SSAOEffect : PostProcessEffect
    {
        public override string EffectName => "SSAO";

        [Engine.Serialization.SerializableAttribute("radius")]
        public float Radius { get; set; } = 1.0f; // Rayon d'échantillonnage en unités de vue

        [Engine.Serialization.SerializableAttribute("bias")]
        public float Bias { get; set; } = 0.025f; // Biais pour éviter l'acné d'occlusion

        [Engine.Serialization.SerializableAttribute("power")]
        public float Power { get; set; } = 1.5f; // Puissance pour ajuster le contraste

        [Engine.Serialization.SerializableAttribute("samplecount")]
        public int SampleCount { get; set; } = 16; // Nombre d'échantillons (8, 16, 32, 64)

        [Engine.Serialization.SerializableAttribute("blursize")]
        public int BlurSize { get; set; } = 3; // Taille du flou (1-5)

        [Engine.Serialization.SerializableAttribute("maxdistance")]
        public float MaxDistance { get; set; } = 50.0f; // Distance max pour l'effet SSAO (fade out progressif)

        public SSAOEffect()
        {
            Priority = 5; // Après bloom (0), avant tone mapping (10)
        }

        public override void Apply(PostProcessContext context)
        {
            // L'application sera gérée par SSAORenderer
        }
    }

    /// <summary>
    /// Renderer pour l'effet de tone mapping
    /// </summary>
    public class ToneMappingRenderer : IPostProcessRenderer
    {
        private ShaderProgram? _shader;
        private ShaderProgram? _luminanceShader;

        // Auto-exposure resources
        private int _luminanceTexture = 0;
        private int _luminanceFBO = 0;
        private int _lastWidth = 0;
        private int _lastHeight = 0;
        // Temporal smoothing state for adaptive exposure
        private float _lastExposure = 1.0f;
        private bool _exposureInitialized = false; // Track if we've initialized from scene

        // Quick-fix: reduce luminance resolution and sample less frequently to improve CPU/GPU usage
        private int _luminanceMaxSize = 256; // clamp luminance texture to at most 256x256 (tunable)
        private int _frameCounter = 0;
        private int _sampleInterval = 2; // compute luminance every N frames (2 = every other frame)
        // Rate-limited console logs for quick perf checks (only when not in verbose logger)
        private DateTime _lastLumLogTime = DateTime.MinValue;

        public void Initialize()
        {
            try
            {
                var baseDir = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "Engine", "Rendering", "Shaders", "PostProcess"));
                var vertPath = Path.Combine(baseDir, "fullscreen.vert");
                var fragPath = Path.Combine(baseDir, "tonemap.frag");
                var lumPath = Path.Combine(baseDir, "luminance.frag");

                // Load tonemap shader
                string vertexSource = System.IO.File.ReadAllText(vertPath);
                string fragmentSource = System.IO.File.ReadAllText(fragPath);
                _shader = ShaderProgram.FromSource(vertexSource, fragmentSource);

                // Load luminance shader
                if (File.Exists(lumPath))
                {
                    string lumSource = System.IO.File.ReadAllText(lumPath);
                    _luminanceShader = ShaderProgram.FromSource(vertexSource, lumSource);
                }
                else
                {
                    // Luminance shader missing: fall back to instant adaptation (no logging)
                }
            }
            catch (Exception)
            {
                _shader = null;
                _luminanceShader = null;
            }
        }

        private void EnsureLuminanceTexture(int width, int height)
        {
            if (_luminanceTexture != 0 && _lastWidth == width && _lastHeight == height)
                return;

            // Clean up old resources
            if (_luminanceTexture != 0)
            {
                GL.DeleteTexture(_luminanceTexture);
                GL.DeleteFramebuffer(_luminanceFBO);
            }

            // Create luminance texture with mipmaps for average calculation
            _luminanceTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _luminanceTexture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.R16f, width, height, 0, PixelFormat.Red, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            // Create FBO
            _luminanceFBO = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _luminanceFBO);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _luminanceTexture, 0);

            if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
            {
                throw new Exception("Luminance framebuffer is not complete");
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            _lastWidth = width;
            _lastHeight = height;
        }

        public void Render(PostProcessEffect effect, PostProcessContext context)
        {
            if (_shader == null)
            {
                Initialize();
            }

            if (_shader == null || !(effect is ToneMappingEffect toneMap))
            {
                return;
            }

            // (debug logs removed)

            // Use engine delta time for accurate frame timing (DateTime.Now is too imprecise)
            float deltaTime = Engine.Core.Time.DeltaTime;

            // Quick sampling throttling: only compute luminance every N frames to reduce cost
            _frameCounter++;
            bool computeThisFrame = (_frameCounter % _sampleInterval) == 0;

            // If auto-exposure is enabled, generate luminance texture
            bool cpuComputedExposure = false;
            // When CPU computes exposure we store it here and set the uniform after binding the shader
            float exposureToSet = float.NaN;

            // Reset initialization flag when auto-exposure is disabled
            if (!toneMap.AutoExposure)
            {
                _exposureInitialized = false;
            }

            if (toneMap.AutoExposure && _luminanceShader != null)
            {
                int lumW = Math.Min(context.Width, _luminanceMaxSize);
                int lumH = Math.Min(context.Height, _luminanceMaxSize);

                if (computeThisFrame)
                {
                    EnsureLuminanceTexture(lumW, lumH);

                    var swLum = System.Diagnostics.Stopwatch.StartNew();

                    // Render luminance to texture
                    GL.BindFramebuffer(FramebufferTarget.Framebuffer, _luminanceFBO);
                    GL.Viewport(0, 0, lumW, lumH);
                    GL.Clear(ClearBufferMask.ColorBufferBit);

                    _luminanceShader.Use();
                    GL.ActiveTexture(TextureUnit.Texture0);
                    GL.BindTexture(TextureTarget.Texture2D, context.SourceTexture);
                    _luminanceShader.SetInt("u_SourceTexture", 0);
                    GL.DrawArrays(PrimitiveType.Triangles, 0, 3);

                    // Generate mipmaps for average luminance calculation
                    GL.BindTexture(TextureTarget.Texture2D, _luminanceTexture);
                    GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);

                    // Read smallest mip (1x1) to compute average log-luminance on CPU
                    try
                    {
                        int maxDim = Math.Max(lumW, lumH);
                        int maxLevel = Math.Max(0, (int)Math.Floor(Math.Log(Math.Max(1, maxDim), 2)));

                        // Bind luminance texture and read the Red channel of the 1x1 mip
                        GL.BindTexture(TextureTarget.Texture2D, _luminanceTexture);
                        float[] pixel = new float[1];
                        GL.GetTexImage(TextureTarget.Texture2D, maxLevel, PixelFormat.Red, PixelType.Float, pixel);

                        float avgLogLum = pixel.Length > 0 ? pixel[0] : 0.0f;
                        float avgLum = MathF.Exp(avgLogLum);

                        // Avoid degenerate values
                        if (avgLum <= 0) avgLum = 0.0001f;

                        // Compute target exposure and clamp
                        float targetExposure = toneMap.TargetBrightness / avgLum;
                        targetExposure = MathF.Max(toneMap.MinExposure, MathF.Min(toneMap.MaxExposure, targetExposure));

                        // First frame: initialize immediately to avoid jarring transition from default 1.0
                        if (!_exposureInitialized)
                        {
                            _lastExposure = targetExposure;
                            _exposureInitialized = true;
                        }
                        else
                        {
                            // Adaptation smoothing: treat AdaptationSpeed as a time constant (seconds)
                            // lerpFactor = 1 - exp(-dt / tau) where tau = AdaptationSpeed
                            float adaptationTime = MathF.Max(0.001f, toneMap.AdaptationSpeed);
                            float lerpFactor = 1.0f - MathF.Exp(-deltaTime / adaptationTime);
                            _lastExposure = _lastExposure + lerpFactor * (targetExposure - _lastExposure);
                        }

                        // Apply exposure compensation (EV scale: 2^compensation)
                        // +1 EV = 2x brighter, -1 EV = 0.5x darker
                        float compensationMultiplier = MathF.Pow(2.0f, toneMap.ExposureCompensation);

                        // Compose final exposure (base exposure * intensity * adaptive factor * compensation)
                        float finalExposureComputed = toneMap.Exposure * toneMap.Intensity * _lastExposure * compensationMultiplier;

                        // Store computed exposure into a local variable; we'll set the uniform after binding the shader
                        exposureToSet = finalExposureComputed;
                        cpuComputedExposure = true;

                        swLum.Stop();
                        try { if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[ToneMapping] Luminance computed (size={lumW}x{lumH}) in {swLum.Elapsed.TotalMilliseconds:F2} ms (avgExp={_lastExposure:F3})"); } catch { }
                        // Ensure we emit at least occasional console feedback for quick local testing
                        try
                        {
                            if ((DateTime.UtcNow - _lastLumLogTime).TotalSeconds > 1.0)
                            {
                                Console.WriteLine($"[ToneMapping] Luminance computed: {lumW}x{lumH} -> {swLum.Elapsed.TotalMilliseconds:F2} ms, exposure={_lastExposure:F3}");
                                _lastLumLogTime = DateTime.UtcNow;
                            }
                        }
                        catch { }
                    }
                    catch
                    {
                        // If readback fails, fall back to shader auto-exposure (instant adaptation)
                        // This is expected on some GPU/driver combinations
                        GL.BindFramebuffer(FramebufferTarget.Framebuffer, (int)context.TargetFramebuffer);
                        GL.Viewport(0, 0, context.Width, context.Height);
                    }

                    // Restore original framebuffer
                    GL.BindFramebuffer(FramebufferTarget.Framebuffer, (int)context.TargetFramebuffer);
                    GL.Viewport(0, 0, context.Width, context.Height);
                }
                else
                {
                    // Not computing this frame: reuse last computed exposure if available to avoid shader readback
                    if (_exposureInitialized)
                    {
                        float compensationMultiplier = MathF.Pow(2.0f, toneMap.ExposureCompensation);
                        exposureToSet = toneMap.Exposure * toneMap.Intensity * _lastExposure * compensationMultiplier;
                        cpuComputedExposure = true;
                    }
                    else
                    {
                        // No previous exposure yet: leave cpuComputedExposure=false so shader auto-exposure can run as fallback
                    }
                }
            }

            // Render tone mapping
            _shader.Use();

            // Bind source texture
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, context.SourceTexture);
            _shader.SetInt("u_SourceTexture", 0);

            // Bind luminance texture for auto-exposure
            if (toneMap.AutoExposure && _luminanceTexture != 0)
            {
                GL.ActiveTexture(TextureUnit.Texture1);
                GL.BindTexture(TextureTarget.Texture2D, _luminanceTexture);
                _shader.SetInt("u_LuminanceTexture", 1);
            }

            // Tone mapping parameters
            _shader.SetInt("u_ToneMappingMode", (int)toneMap.Mode);

            // If CPU computed exposure, set the exposure uniform now (shader is bound)
            if (cpuComputedExposure)
            {
                _shader.SetFloat("u_Exposure", exposureToSet);
            }

            // If CPU computed exposure, we already wrote u_Exposure above and we must NOT enable
            // the shader-side auto-exposure (otherwise it will override instantly).
            if (!toneMap.AutoExposure && !cpuComputedExposure)
            {
                _shader.SetFloat("u_Exposure", toneMap.Exposure * toneMap.Intensity);
            }

            _shader.SetFloat("u_WhitePoint", toneMap.WhitePoint);
            _shader.SetFloat("u_Gamma", toneMap.Gamma);

            // Auto-exposure parameters (shader uses them only if u_AutoExposure is set)
            // If CPU computed exposure, disable shader auto-exposure to keep temporal smoothing.
            bool shaderAutoExposure = toneMap.AutoExposure && !cpuComputedExposure;
            _shader.SetInt("u_AutoExposure", shaderAutoExposure ? 1 : 0);

            // (debug logs removed)

            _shader.SetFloat("u_MinExposure", toneMap.MinExposure);
            _shader.SetFloat("u_MaxExposure", toneMap.MaxExposure);
            _shader.SetFloat("u_TargetBrightness", toneMap.TargetBrightness);
            _shader.SetFloat("u_AdaptationSpeed", toneMap.AdaptationSpeed);
            _shader.SetFloat("u_DeltaTime", deltaTime);

            // (debug logs removed)

            // Render fullscreen triangle
            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        }

        public void Dispose()
        {
            _shader?.Dispose();
            _shader = null;
            _luminanceShader?.Dispose();
            _luminanceShader = null;

            if (_luminanceTexture != 0)
            {
                GL.DeleteTexture(_luminanceTexture);
                _luminanceTexture = 0;
            }

            if (_luminanceFBO != 0)
            {
                GL.DeleteFramebuffer(_luminanceFBO);
                _luminanceFBO = 0;
            }
        }
    }

    /// <summary>
    /// Renderer pour l'effet de bloom
    /// </summary>
    public class BloomRenderer : IPostProcessRenderer
    {
        private ShaderProgram? _downsampleShader;
        private ShaderProgram? _upsampleShader;
        private ShaderProgram? _combineShader;

        private int[] _downsampleTextures = new int[8]; // Chaîne de downsampling
        private int[] _downsampleFBOs = new int[8];
        private int _width, _height;

        public void Initialize()
        {
            try
            {
                var baseDir = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "Engine", "Rendering", "Shaders", "PostProcess"));
                var vertPath = Path.Combine(baseDir, "fullscreen.vert");

                // Shaders pour bloom
                var downsamplePath = Path.Combine(baseDir, "bloom_downsample.frag");
                var upsamplePath = Path.Combine(baseDir, "bloom_upsample.frag");
                var combinePath = Path.Combine(baseDir, "bloom_combine.frag");



                if (File.Exists(vertPath) && File.Exists(downsamplePath) && File.Exists(upsamplePath) && File.Exists(combinePath))
                {
                    string vertexSource = File.ReadAllText(vertPath);

                    // Test chaque shader individuellement pour isoler l'erreur
                    try
                    {
                        string downsampleSource = File.ReadAllText(downsamplePath);
                        _downsampleShader = ShaderProgram.FromSource(vertexSource, downsampleSource);
                    }
                    catch
                    {
                        throw;
                    }

                    try
                    {
                        string upsampleSource = File.ReadAllText(upsamplePath);
                        _upsampleShader = ShaderProgram.FromSource(vertexSource, upsampleSource);
                    }
                    catch
                    {
                        throw;
                    }

                    try
                    {
                        string combineSource = File.ReadAllText(combinePath);
                        _combineShader = ShaderProgram.FromSource(vertexSource, combineSource);
                    }
                    catch
                    {
                        throw;
                    }

                }
                else
                {
                }
            }
            catch
            {
            }
        }

        public void Render(PostProcessEffect effect, PostProcessContext context)
        {
            if (_downsampleShader == null || _upsampleShader == null || _combineShader == null ||
                !(effect is BloomEffect bloom)) return;

            // Resize buffers if needed
            if (_width != context.Width || _height != context.Height)
            {
                ResizeBuffers(context.Width, context.Height);
            }


            // 1. Extract bright areas (downsample pass 0)
            ExtractBrightAreas(bloom, context);

            // 2. Downsample chain
            PerformDownsampling(bloom, context);

            // 3. Upsample chain with blur
            PerformUpsampling(bloom, context);

            // 4. Combine with original
            CombineWithOriginal(bloom, context);
        }

        private void ResizeBuffers(int width, int height)
        {
            _width = width;
            _height = height;

            // Cleanup existing buffers
            GL.DeleteTextures(_downsampleTextures.Length, _downsampleTextures);
            GL.DeleteFramebuffers(_downsampleFBOs.Length, _downsampleFBOs);

            // Create downsampling chain
            for (int i = 0; i < _downsampleTextures.Length; i++)
            {
                int mipWidth = Math.Max(1, width >> (i + 1));
                int mipHeight = Math.Max(1, height >> (i + 1));

                _downsampleTextures[i] = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2D, _downsampleTextures[i]);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f,
                             mipWidth, mipHeight, 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

                _downsampleFBOs[i] = GL.GenFramebuffer();
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, _downsampleFBOs[i]);
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                                      TextureTarget.Texture2D, _downsampleTextures[i], 0);

                // Clear the FBO to avoid artifacts from uninitialized texture memory
                GL.Viewport(0, 0, mipWidth, mipHeight);
                GL.ClearColor(0, 0, 0, 0);
                GL.Clear(ClearBufferMask.ColorBufferBit);
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        private void ExtractBrightAreas(BloomEffect bloom, PostProcessContext context)
        {
            _downsampleShader!.Use();

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _downsampleFBOs[0]);
            GL.Viewport(0, 0, Math.Max(1, _width >> 1), Math.Max(1, _height >> 1));

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, context.SourceTexture);
            _downsampleShader.SetInt("u_SourceTexture", 0);
            _downsampleShader.SetFloat("u_Threshold", bloom.Threshold);
            _downsampleShader.SetFloat("u_SoftKnee", bloom.SoftKnee);
            _downsampleShader.SetFloat("u_Clamp", bloom.Clamp);
            _downsampleShader.SetInt("u_FirstPass", 1);

            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        }

        private void PerformDownsampling(BloomEffect bloom, PostProcessContext context)
        {
            _downsampleShader!.Use();
            _downsampleShader.SetInt("u_FirstPass", 0);
            _downsampleShader.SetFloat("u_Clamp", bloom.Clamp); // Apply clamp to all passes

            for (int i = 1; i < Math.Min(bloom.Iterations, _downsampleTextures.Length); i++)
            {
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, _downsampleFBOs[i]);
                GL.Viewport(0, 0, Math.Max(1, _width >> (i + 1)), Math.Max(1, _height >> (i + 1)));

                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.Texture2D, _downsampleTextures[i - 1]);
                _downsampleShader.SetInt("u_SourceTexture", 0);

                GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
            }
        }

        private void PerformUpsampling(BloomEffect bloom, PostProcessContext context)
        {
            _upsampleShader!.Use();
            _upsampleShader.SetFloat("u_Radius", bloom.Radius);

            for (int i = Math.Min(bloom.Iterations, _downsampleTextures.Length) - 2; i >= 0; i--)
            {
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, _downsampleFBOs[i]);
                GL.Viewport(0, 0, Math.Max(1, _width >> (i + 1)), Math.Max(1, _height >> (i + 1)));

                // Progressive scattering based on mip level
                float scatterAmount = bloom.Scattering * (1.0f - (float)i / Math.Max(1, bloom.Iterations));
                _upsampleShader.SetFloat("u_Scatter", scatterAmount);

                GL.Enable(EnableCap.Blend);
                GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);

                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.Texture2D, _downsampleTextures[i + 1]);
                _upsampleShader.SetInt("u_SourceTexture", 0);

                GL.DrawArrays(PrimitiveType.Triangles, 0, 3);

                GL.Disable(EnableCap.Blend);
            }
        }

        private void CombineWithOriginal(BloomEffect bloom, PostProcessContext context)
        {
            _combineShader!.Use();

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, context.TargetFramebuffer);
            GL.Viewport(0, 0, _width, _height);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, context.SourceTexture);
            _combineShader.SetInt("u_OriginalTexture", 0);

            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, _downsampleTextures[0]);
            _combineShader.SetInt("u_BloomTexture", 1);

            _combineShader.SetFloat("u_BloomIntensity", bloom.Intensity);

            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        }

        public void Dispose()
        {
            _downsampleShader?.Dispose();
            _upsampleShader?.Dispose();
            _combineShader?.Dispose();

            GL.DeleteTextures(_downsampleTextures.Length, _downsampleTextures);
            GL.DeleteFramebuffers(_downsampleFBOs.Length, _downsampleFBOs);
        }
    }

    /// <summary>
    /// Renderer pour l'effet d'aberration chromatique
    /// </summary>
    public class ChromaticAberrationRenderer : IPostProcessRenderer
    {
        private ShaderProgram? _shader;

        public void Initialize()
        {
            try
            {
                // Chemins absolus pour éviter les problèmes de répertoire de travail
                var baseDir = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "Engine", "Rendering", "Shaders", "PostProcess"));
                var vertPath = Path.Combine(baseDir, "fullscreen.vert");
                var fragPath = Path.Combine(baseDir, "chromatic_aberration.frag");
                
                
                // Contourner le ShaderPreprocessor pour tester
                string vertexSource = System.IO.File.ReadAllText(vertPath);
                string fragmentSource = System.IO.File.ReadAllText(fragPath);
                _shader = ShaderProgram.FromSource(vertexSource, fragmentSource);
            }
            catch
            {
                _shader = null;
            }
        }

        public void Render(PostProcessEffect effect, PostProcessContext context)
        {
            // Force réinitialisation si shader est null
            if (_shader == null)
            {
                Initialize();
            }
            
            if (_shader == null || !(effect is ChromaticAberrationEffect chromatic))
            {
                return;
            }

            _shader.Use();

            // Bind la texture source
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, context.SourceTexture);
            _shader.SetInt("u_SourceTexture", 0);

            // Paramètres de l'aberration chromatique
            _shader.SetFloat("u_Strength", chromatic.Strength * chromatic.Intensity);
            _shader.SetFloat("u_FocalLength", chromatic.FocalLength);
            _shader.SetInt("u_UseSpectralLut", chromatic.UseSpectralLut ? 1 : 0);
            _shader.SetVec2("u_ScreenSize", new Vector2(context.Width, context.Height));

            // Rendu fullscreen triangle (pas besoin de VAO)
            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        }

        public void Dispose()
        {
            _shader?.Dispose();
            _shader = null;
        }
    }

    /// <summary>
    /// Renderer pour l'effet SSAO (Screen Space Ambient Occlusion)
    /// </summary>
    public class SSAOPostEffectRenderer : IPostProcessRenderer
    {
        private ShaderProgram? _ssaoShader;
        private ShaderProgram? _blurShader;
        private ShaderProgram? _blurSeparableShader;

        // Textures pour le rendu SSAO
        private int _ssaoTexture;
        private int _blurTexture;
        private int _blurTempTexture; // Temporary texture for separable blur
        private int _ssaoFBO;
        private int _blurFBO;
        private int _blurTempFBO;
        private int _noiseTexture;

        // Kernel d'échantillonnage
        private Vector3[] _kernel = new Vector3[64];

        private int _width, _height;

        public void Initialize()
        {
            try
            {
                Engine.Utils.DebugLogger.Log("[SSAO] Initializing...");
                var baseDir = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "Engine", "Rendering", "Shaders", "PostProcess"));

                var vertPath = Path.Combine(baseDir, "fullscreen.vert");
                var ssaoFragPath = Path.Combine(baseDir, "ssao.frag");
                var blurFragPath = Path.Combine(baseDir, "ssao_blur.frag");
                var blurSeparableFragPath = Path.Combine(baseDir, "ssao_blur_separable.frag");

                if (File.Exists(vertPath) && File.Exists(ssaoFragPath) && File.Exists(blurSeparableFragPath))
                {
                    string vertexSource = File.ReadAllText(vertPath);
                    string ssaoSource = File.ReadAllText(ssaoFragPath);
                    string blurSeparableSource = File.ReadAllText(blurSeparableFragPath);

                    _ssaoShader = ShaderProgram.FromSource(vertexSource, ssaoSource);
                    _blurSeparableShader = ShaderProgram.FromSource(vertexSource, blurSeparableSource);

                    // Keep old blur shader as fallback
                    if (File.Exists(blurFragPath))
                    {
                        string blurSource = File.ReadAllText(blurFragPath);
                        _blurShader = ShaderProgram.FromSource(vertexSource, blurSource);
                    }

                    Engine.Utils.DebugLogger.Log("[SSAO] ✓ Initialized successfully");
                }
                else
                {
                    Engine.Utils.DebugLogger.Log("[SSAO] ERROR: Shader files not found!");
                }

                // Générer le kernel d'échantillonnage hémisphérique
                GenerateKernel();
                
                // Créer la texture de bruit
                CreateNoiseTexture();
            }
            catch (Exception ex)
            {
                Engine.Utils.DebugLogger.Log($"[SSAO] INITIALIZATION FAILED: {ex.Message}");
                _ssaoShader = null;
                _blurShader = null;
            }
        }

        private void GenerateKernel()
        {
            var random = new Random(42);

            // Improved importance sampling with cosine-weighted hemisphere distribution
            // This places more samples near the surface normal, which is physically correct
            for (int i = 0; i < _kernel.Length; i++)
            {
                // Stratified sampling using Hammersley low-discrepancy sequence
                // This gives better distribution than pure random
                float u1 = (float)i / _kernel.Length + (float)random.NextDouble() * (1.0f / _kernel.Length);
                float u2 = RadicalInverse(i);

                // Cosine-weighted hemisphere sampling (importance sampling)
                // This matches the cosine falloff of ambient occlusion
                float theta = 2.0f * (float)Math.PI * u1;
                float phi = (float)Math.Acos(Math.Sqrt(1.0f - u2)); // Cosine-weighted

                var sample = new Vector3(
                    (float)(Math.Sin(phi) * Math.Cos(theta)),
                    (float)(Math.Sin(phi) * Math.Sin(theta)),
                    (float)(Math.Cos(phi))
                );

                // Better radial distribution: more samples near surface, fewer far away
                // Using cubic falloff for smoother distribution
                float scale = (float)i / _kernel.Length;
                scale = 0.1f + 0.9f * scale * scale * scale; // Cubic instead of quadratic

                _kernel[i] = sample * scale;
            }
        }

        // Van der Corput sequence for low-discrepancy sampling
        private float RadicalInverse(int bits)
        {
            uint b = (uint)bits;
            b = (b << 16) | (b >> 16);
            b = ((b & 0x55555555u) << 1) | ((b & 0xAAAAAAAAu) >> 1);
            b = ((b & 0x33333333u) << 2) | ((b & 0xCCCCCCCCu) >> 2);
            b = ((b & 0x0F0F0F0Fu) << 4) | ((b & 0xF0F0F0F0u) >> 4);
            b = ((b & 0x00FF00FFu) << 8) | ((b & 0xFF00FF00u) >> 8);
            return (float)b * 2.3283064365386963e-10f; // / 0x100000000
        }

        private void CreateNoiseTexture()
        {
            const int size = 4;
            var noise = new Vector3[size * size];
            var random = new Random(42);

            for (int i = 0; i < noise.Length; i++)
            {
                // Vecteurs de rotation aléatoires tangents à la surface
                noise[i] = new Vector3(
                    (float)random.NextDouble() * 2.0f - 1.0f,
                    (float)random.NextDouble() * 2.0f - 1.0f,
                    0.0f
                );
                noise[i] = Vector3.Normalize(noise[i]);
            }

            _noiseTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _noiseTexture);
            
            // Convertir en float array pour OpenGL
            float[] noiseData = new float[noise.Length * 3];
            for (int i = 0; i < noise.Length; i++)
            {
                noiseData[i * 3 + 0] = noise[i].X;
                noiseData[i * 3 + 1] = noise[i].Y;
                noiseData[i * 3 + 2] = noise[i].Z;
            }
            
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgb16f, size, size, 0, 
                         PixelFormat.Rgb, PixelType.Float, noiseData);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
        }

        private void ResizeBuffers(int width, int height)
        {
            if (_width == width && _height == height) return;

            _width = width;
            _height = height;

            // SSAO rendered at half resolution for performance (4x faster)
            int ssaoWidth = width / 2;
            int ssaoHeight = height / 2;

            // Nettoyer les anciennes ressources
            if (_ssaoTexture != 0) GL.DeleteTexture(_ssaoTexture);
            if (_blurTexture != 0) GL.DeleteTexture(_blurTexture);
            if (_blurTempTexture != 0) GL.DeleteTexture(_blurTempTexture);
            if (_ssaoFBO != 0) GL.DeleteFramebuffer(_ssaoFBO);
            if (_blurFBO != 0) GL.DeleteFramebuffer(_blurFBO);
            if (_blurTempFBO != 0) GL.DeleteFramebuffer(_blurTempFBO);

            // Créer la texture SSAO (half resolution)
            _ssaoTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _ssaoTexture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.R16f, ssaoWidth, ssaoHeight, 0,
                         PixelFormat.Red, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            _ssaoFBO = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _ssaoFBO);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                                   TextureTarget.Texture2D, _ssaoTexture, 0);

            // Créer la texture de flou temporaire (half resolution) - for separable blur pass 1
            _blurTempTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _blurTempTexture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.R16f, ssaoWidth, ssaoHeight, 0,
                         PixelFormat.Red, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            _blurTempFBO = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _blurTempFBO);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                                   TextureTarget.Texture2D, _blurTempTexture, 0);

            // Créer la texture de flou finale (half resolution) - for separable blur pass 2
            _blurTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _blurTexture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.R16f, ssaoWidth, ssaoHeight, 0,
                         PixelFormat.Red, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            _blurFBO = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _blurFBO);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                                   TextureTarget.Texture2D, _blurTexture, 0);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        public void Render(PostProcessEffect effect, PostProcessContext context)
        {
            if (_ssaoShader == null || _blurShader == null || !(effect is SSAOEffect ssao))
            {
                if (_ssaoShader == null || _blurShader == null)
                {
                    Engine.Utils.DebugLogger.Log("[SSAO] Shaders not initialized, calling Initialize()...");
                    Initialize();
                }
                
                if (_ssaoShader == null || _blurShader == null)
                {
                    Engine.Utils.DebugLogger.Log("[SSAO] ERROR: Initialize() failed, shaders still null after init!");
                    return;
                }
                
                if (!(effect is SSAOEffect))
                {
                    Engine.Utils.DebugLogger.Log("[SSAO] ERROR: Effect is not SSAOEffect!");
                    return;
                }
                
                // Retry getting ssao after initialization
                ssao = (SSAOEffect)effect;
            }

            // Vérifier que nous avons accès à la profondeur
            if (context.DepthTexture == 0)
            {
                // Pas de texture de profondeur disponible, impossible de faire du SSAO
                Engine.Utils.DebugLogger.Log("[SSAO] ERREUR: Pas de texture de profondeur disponible!");
                return;
            }

            if (!context.ProjectionMatrix.HasValue)
            {
                Engine.Utils.DebugLogger.Log("[SSAO] ERREUR: Pas de matrice de projection disponible!");
                return;
            }

            // Redimensionner les buffers si nécessaire
            ResizeBuffers(context.Width, context.Height);

            int ssaoWidth = context.Width / 2;
            int ssaoHeight = context.Height / 2;

            // === PASSE 1: Calcul SSAO (half resolution) ===
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _ssaoFBO);
            GL.Viewport(0, 0, ssaoWidth, ssaoHeight);
            GL.Clear(ClearBufferMask.ColorBufferBit);

            _ssaoShader.Use();

            // Bind textures
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, context.DepthTexture);
            _ssaoShader.SetInt("u_DepthTexture", 0);

            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, _noiseTexture);
            _ssaoShader.SetInt("u_NoiseTexture", 1);

            // Paramètres SSAO
            _ssaoShader.SetFloat("u_Radius", ssao.Radius);
            _ssaoShader.SetFloat("u_Bias", ssao.Bias);
            _ssaoShader.SetFloat("u_Power", ssao.Power);
            _ssaoShader.SetFloat("u_MaxDistance", ssao.MaxDistance);
            _ssaoShader.SetInt("u_SampleCount", Math.Min(ssao.SampleCount, 64));
            _ssaoShader.SetVec2("u_NoiseScale", new Vector2(_width / 4.0f, _height / 4.0f));

            // Envoyer le kernel d'échantillonnage
            for (int i = 0; i < Math.Min(ssao.SampleCount, 64); i++)
            {
                _ssaoShader.SetVec3($"u_Samples[{i}]", _kernel[i]);
            }

            // Matrices de projection (récupérées du context)
            if (context.ProjectionMatrix.HasValue)
            {
                var projMatrix = context.ProjectionMatrix.Value;

                // Envoyer la matrice de projection normale (pour projeter vers screen space)
                _ssaoShader.SetMat4("u_Projection", projMatrix);

                // Calculer et envoyer la matrice inverse (pour reconstruire position depuis depth)
                var invProj = projMatrix.Inverted();
                _ssaoShader.SetMat4("u_InvProjection", invProj);
            }

            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);

            // === PASSE 2: Separable Bilateral Blur (2 passes: horizontal + vertical) ===
            if (ssao.BlurSize > 0 && _blurSeparableShader != null)
            {
                // Pass 2a: Horizontal blur (SSAO -> BlurTemp)
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, _blurTempFBO);
                GL.Viewport(0, 0, ssaoWidth, ssaoHeight);
                GL.Clear(ClearBufferMask.ColorBufferBit);

                _blurSeparableShader.Use();

                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.Texture2D, _ssaoTexture);
                _blurSeparableShader.SetInt("u_SSAOTexture", 0);

                GL.ActiveTexture(TextureUnit.Texture1);
                GL.BindTexture(TextureTarget.Texture2D, context.DepthTexture);
                _blurSeparableShader.SetInt("u_DepthTexture", 1);

                _blurSeparableShader.SetInt("u_BlurSize", ssao.BlurSize);
                _blurSeparableShader.SetVec2("u_Direction", new Vector2(1.0f, 0.0f)); // Horizontal

                if (context.ProjectionMatrix.HasValue)
                {
                    var invProj = context.ProjectionMatrix.Value.Inverted();
                    _blurSeparableShader.SetMat4("u_InvProjection", invProj);
                }

                GL.DrawArrays(PrimitiveType.Triangles, 0, 3);

                // Pass 2b: Vertical blur (BlurTemp -> Blur)
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, _blurFBO);
                GL.Clear(ClearBufferMask.ColorBufferBit);

                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.Texture2D, _blurTempTexture);
                _blurSeparableShader.SetInt("u_SSAOTexture", 0);

                _blurSeparableShader.SetVec2("u_Direction", new Vector2(0.0f, 1.0f)); // Vertical

                GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
            }
            else if (ssao.BlurSize > 0 && _blurShader != null)
            {
                // Fallback to old single-pass blur if separable shader not available
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, _blurFBO);
                GL.Viewport(0, 0, ssaoWidth, ssaoHeight);
                GL.Clear(ClearBufferMask.ColorBufferBit);

                _blurShader.Use();

                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.Texture2D, _ssaoTexture);
                _blurShader.SetInt("u_SSAOTexture", 0);
                _blurShader.SetInt("u_BlurSize", ssao.BlurSize);

                GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
            }

            // === PASSE 3: Application sur l'image finale ===
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, context.TargetFramebuffer);
            GL.Viewport(0, 0, context.Width, context.Height);

            // Utiliser un shader simple pour multiplier la couleur par l'occlusion
            // Pour l'instant, on va créer un shader inline simple
            ApplySSAOToScene(context, ssao.BlurSize > 0 ? _blurTexture : _ssaoTexture, ssao.Intensity);
        }

        private ShaderProgram? _combineShader;

        private void ApplySSAOToScene(PostProcessContext context, int ssaoTexture, float intensity)
        {
            // Créer le shader de combinaison si nécessaire
            if (_combineShader == null)
            {
                try
                {
                    var baseDir = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "Engine", "Rendering", "Shaders", "PostProcess"));
                    var vertPath = Path.Combine(baseDir, "fullscreen.vert");
                    var fragPath = Path.Combine(baseDir, "ssao_combine.frag");

                    if (File.Exists(vertPath) && File.Exists(fragPath))
                    {
                        string vertexSource = File.ReadAllText(vertPath);
                        string fragSource = File.ReadAllText(fragPath);
                        _combineShader = ShaderProgram.FromSource(vertexSource, fragSource);
                    }
                }
                catch { }
            }

            if (_combineShader == null) return;

            _combineShader.Use();

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, context.SourceTexture);
            _combineShader.SetInt("u_ColorTexture", 0);

            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, ssaoTexture);
            _combineShader.SetInt("u_SSAOTexture", 1);

            GL.ActiveTexture(TextureUnit.Texture2);
            GL.BindTexture(TextureTarget.Texture2D, context.DepthTexture);
            _combineShader.SetInt("u_DepthTexture", 2);

            _combineShader.SetFloat("u_Intensity", intensity);

            // Pass inverse projection for depth-aware bilateral upscale
            if (context.ProjectionMatrix.HasValue)
            {
                var invProj = context.ProjectionMatrix.Value.Inverted();
                _combineShader.SetMat4("u_InvProjection", invProj);
            }

            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        }

        public void Dispose()
        {
            _ssaoShader?.Dispose();
            _blurShader?.Dispose();
            _blurSeparableShader?.Dispose();
            _combineShader?.Dispose();

            if (_ssaoTexture != 0) GL.DeleteTexture(_ssaoTexture);
            if (_blurTexture != 0) GL.DeleteTexture(_blurTexture);
            if (_blurTempTexture != 0) GL.DeleteTexture(_blurTempTexture);
            if (_noiseTexture != 0) GL.DeleteTexture(_noiseTexture);
            if (_ssaoFBO != 0) GL.DeleteFramebuffer(_ssaoFBO);
            if (_blurFBO != 0) GL.DeleteFramebuffer(_blurFBO);
            if (_blurTempFBO != 0) GL.DeleteFramebuffer(_blurTempFBO);
        }
    }

    /// <summary>
    /// Effet GTAO (Ground Truth Ambient Occlusion) - version améliorée de SSAO
    /// </summary>
    public class GTAOEffect : PostProcessEffect
    {
        public override string EffectName => "GTAO";

        [Engine.Serialization.SerializableAttribute("radius")]
        public float Radius { get; set; } = 0.5f; // Rayon en unités de vue

        [Engine.Serialization.SerializableAttribute("thickness")]
        public float Thickness { get; set; } = 1.0f; // Épaisseur des surfaces

        [Engine.Serialization.SerializableAttribute("falloffrange")]
        public float FalloffRange { get; set; } = 0.615f; // Plage de falloff

        [Engine.Serialization.SerializableAttribute("samplecount")]
        public int SampleCount { get; set; } = 3; // Nombre de slices (2-6)

        [Engine.Serialization.SerializableAttribute("slicecount")]
        public int SliceCount { get; set; } = 2; // Nombre de directions (1-4)

        [Engine.Serialization.SerializableAttribute("blurradius")]
        public int BlurRadius { get; set; } = 3; // Spatial blur radius (1-5)

        [Engine.Serialization.SerializableAttribute("maxdistance")]
        public float MaxDistance { get; set; } = 50.0f; // Max distance (fade out)

        // Temporal filtering parameters
        [Engine.Serialization.SerializableAttribute("enabletemporal")]
        public bool EnableTemporal { get; set; } = true; // Enable temporal accumulation

        [Engine.Serialization.SerializableAttribute("temporalblendfactor")]
        public float TemporalBlendFactor { get; set; } = 0.9f; // History blend weight (0.8-0.95)

        [Engine.Serialization.SerializableAttribute("temporalvariancethreshold")]
        public float TemporalVarianceThreshold { get; set; } = 0.15f; // Rejection threshold (0.05-0.3)

        // Multi-scale (Hierarchical) parameters
        [Engine.Serialization.SerializableAttribute("miplevels")]
        public int MipLevels { get; set; } = 1; // Number of mip levels to sample (1-4, default 1 = no multi-scale)

        // Weights for each mip level (should sum to 1.0)
        [Engine.Serialization.SerializableAttribute("mipweight0")]
        public float MipWeight0 { get; set; } = 1.0f; // Weight for mip 0 (full res detail)
        
        [Engine.Serialization.SerializableAttribute("mipweight1")]
        public float MipWeight1 { get; set; } = 0.0f; // Weight for mip 1 (2x downsampled)
        
        [Engine.Serialization.SerializableAttribute("mipweight2")]
        public float MipWeight2 { get; set; } = 0.0f; // Weight for mip 2 (4x downsampled)
        
        [Engine.Serialization.SerializableAttribute("mipweight3")]
        public float MipWeight3 { get; set; } = 0.0f; // Weight for mip 3 (8x downsampled)

        // Radius multipliers for each mip level
        [Engine.Serialization.SerializableAttribute("mipradius0")]
        public float MipRadius0 { get; set; } = 1.0f; // Radius scale for mip 0 (small details)
        
        [Engine.Serialization.SerializableAttribute("mipradius1")]
        public float MipRadius1 { get; set; } = 2.0f; // Radius scale for mip 1 (medium)
        
        [Engine.Serialization.SerializableAttribute("mipradius2")]
        public float MipRadius2 { get; set; } = 4.0f; // Radius scale for mip 2 (large)
        
        [Engine.Serialization.SerializableAttribute("mipradius3")]
        public float MipRadius3 { get; set; } = 8.0f; // Radius scale for mip 3 (very large)

        public GTAOEffect()
        {
            Priority = 5; // Same priority as SSAO
        }

        public override void Apply(PostProcessContext context)
        {
            // L'application sera gérée par GTAORenderer
        }
    }

    /// <summary>
    /// Effet de Depth of Field (DOF) - Triple-A quality bokeh blur
    /// </summary>
    public class DepthOfFieldEffect : PostProcessEffect
    {
        public override string EffectName => "Depth of Field";

        // Focus parameters
        [Engine.Serialization.SerializableAttribute("focusdistance")]
        public float FocusDistance { get; set; } = 10.0f; // Distance to focus plane

        [Engine.Serialization.SerializableAttribute("focusrange")]
        public float FocusRange { get; set; } = 2.0f; // Range around focus that stays sharp

        [Engine.Serialization.SerializableAttribute("focallength")]
        public float FocalLength { get; set; } = 50.0f; // Camera focal length (mm)

        [Engine.Serialization.SerializableAttribute("aperture")]
        public float Aperture { get; set; } = 2.8f; // f-stop (lower = more blur, typical: 1.4, 2.8, 5.6, 11)

        [Engine.Serialization.SerializableAttribute("maxcoc")]
        public float MaxCoC { get; set; } = 20.0f; // Maximum circle of confusion radius (pixels)

        // Quality settings
        [Engine.Serialization.SerializableAttribute("samplecount")]
        public int SampleCount { get; set; } = 64; // Number of bokeh samples (32-128)

        [Engine.Serialization.SerializableAttribute("bokehradius")]
        public float BokehRadius { get; set; } = 4.0f; // Bokeh size multiplier

        // Adaptive DOF (auto-focus) - DISABLED BY DEFAULT (expensive GL.ReadPixels sync)
        [Engine.Serialization.SerializableAttribute("enableadaptive")]
        public bool EnableAdaptiveDOF { get; set; } = false; // Keep false for performance!

        [Engine.Serialization.SerializableAttribute("adaptivespeed")]
        public float AdaptiveSpeed { get; set; } = 2.0f; // Focus adaptation speed (1.0 = slow, 5.0 = fast)

        [Engine.Serialization.SerializableAttribute("adaptivecenterbias")]
        public float AdaptiveCenterBias { get; set; } = 0.5f; // How much to bias towards screen center (0.0 = full screen, 1.0 = center only)

        [Engine.Serialization.SerializableAttribute("adaptivemindistance")]
        public float AdaptiveMinDistance { get; set; } = 1.0f; // Minimum focus distance

        [Engine.Serialization.SerializableAttribute("adaptivemaxdistance")]
        public float AdaptiveMaxDistance { get; set; } = 100.0f; // Maximum focus distance

        public DepthOfFieldEffect()
        {
            Priority = 12; // After tone mapping (10), before chromatic aberration (20)
        }

        public override void Apply(PostProcessContext context)
        {
            // Handled by DepthOfFieldRenderer
        }
    }

    /// <summary>
    /// Renderer pour l'effet de Depth of Field
    /// </summary>
    public class DepthOfFieldRenderer : IPostProcessRenderer
    {
        private ShaderProgram? _cocShader;          // Circle of Confusion calculation
        private ShaderProgram? _downsampleShader;   // Downsample with CoC preservation
        private ShaderProgram? _bokehShader;        // Circular bokeh blur
        private ShaderProgram? _recombineShader;    // Recombine with scene

        // Textures and FBOs
        private int _cocTexture;                    // Full-res with CoC in alpha
        private int _downsampledTexture;            // Half-res for performance
        private int _bokehTexture;                  // Blurred half-res
        private int _cocFBO;
        private int _downsampledFBO;
        private int _bokehFBO;

        private int _width, _height;
        private float _currentFocusDistance = 10.0f; // For adaptive DOF

        // Pooled buffers to avoid allocations (reused across frames)
        private static readonly float[] _depthReadBuffer = new float[1];
        private static readonly float[] _samplesBuffer = new float[5];

        public void Initialize()
        {
            try
            {
                var baseDir = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "Engine", "Rendering", "Shaders", "PostProcess"));
                var vertPath = Path.Combine(baseDir, "fullscreen.vert");
                var cocFragPath = Path.Combine(baseDir, "dof_coc.frag");
                var downsampleFragPath = Path.Combine(baseDir, "dof_downsample.frag");
                var bokehFragPath = Path.Combine(baseDir, "dof_bokeh.frag");
                var recombineFragPath = Path.Combine(baseDir, "dof_recombine.frag");

                if (File.Exists(vertPath) && File.Exists(cocFragPath) &&
                    File.Exists(downsampleFragPath) && File.Exists(bokehFragPath) &&
                    File.Exists(recombineFragPath))
                {
                    string vertexSource = File.ReadAllText(vertPath);

                    _cocShader = ShaderProgram.FromSource(vertexSource, File.ReadAllText(cocFragPath));
                    _downsampleShader = ShaderProgram.FromSource(vertexSource, File.ReadAllText(downsampleFragPath));
                    _bokehShader = ShaderProgram.FromSource(vertexSource, File.ReadAllText(bokehFragPath));
                    _recombineShader = ShaderProgram.FromSource(vertexSource, File.ReadAllText(recombineFragPath));
                }
            }
            catch (Exception ex)
            {
                try { Engine.Utils.DebugLogger.Log($"[DOF] Initialization failed: {ex.Message}"); } catch { }
                _cocShader = null;
                _downsampleShader = null;
                _bokehShader = null;
                _recombineShader = null;
            }
        }

        private void ResizeBuffers(int width, int height)
        {
            if (_width == width && _height == height && _cocTexture != 0) return;

            _width = width;
            _height = height;

            // Cleanup old resources
            if (_cocTexture != 0) GL.DeleteTexture(_cocTexture);
            if (_downsampledTexture != 0) GL.DeleteTexture(_downsampledTexture);
            if (_bokehTexture != 0) GL.DeleteTexture(_bokehTexture);
            if (_cocFBO != 0) GL.DeleteFramebuffer(_cocFBO);
            if (_downsampledFBO != 0) GL.DeleteFramebuffer(_downsampledFBO);
            if (_bokehFBO != 0) GL.DeleteFramebuffer(_bokehFBO);

            // Create CoC texture (full resolution, RGBA16F - RGB = color, A = CoC)
            _cocTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _cocTexture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f, width, height, 0,
                         PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            _cocFBO = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _cocFBO);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                                   TextureTarget.Texture2D, _cocTexture, 0);

            // Create downsampled texture (half resolution for performance)
            int halfWidth = width / 2;
            int halfHeight = height / 2;

            _downsampledTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _downsampledTexture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f, halfWidth, halfHeight, 0,
                         PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            _downsampledFBO = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _downsampledFBO);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                                   TextureTarget.Texture2D, _downsampledTexture, 0);

            // Create bokeh texture (half resolution)
            _bokehTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _bokehTexture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f, halfWidth, halfHeight, 0,
                         PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            _bokehFBO = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _bokehFBO);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                                   TextureTarget.Texture2D, _bokehTexture, 0);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        public void Render(PostProcessEffect effect, PostProcessContext context)
        {
            if (_cocShader == null || _downsampleShader == null || _bokehShader == null || _recombineShader == null)
            {
                Initialize();
                if (_cocShader == null || _downsampleShader == null || _bokehShader == null || _recombineShader == null)
                    return;
            }

            var dof = effect as DepthOfFieldEffect;
            if (dof == null) return;

            if (context.DepthTexture == 0) return; // Need depth for DOF

            ResizeBuffers(context.Width, context.Height);

            // Update adaptive focus if enabled (WARNING: expensive GL.ReadPixels!)
            float focusDistance = dof.FocusDistance;
            if (dof.EnableAdaptiveDOF && context.DepthTexture != 0)
            {
                focusDistance = UpdateAdaptiveFocus(dof, context);
            }

            // 1. Calculate Circle of Confusion
            RenderCoC(dof, context, focusDistance);

            // 2. Downsample
            RenderDownsample(context);

            // 3. Apply bokeh blur
            RenderBokeh(dof, context);

            // 4. Recombine with scene
            RenderRecombine(dof, context);
        }

        private float UpdateAdaptiveFocus(DepthOfFieldEffect dof, PostProcessContext context)
        {
            // Sample depth buffer at screen center (and optionally nearby points)
            float targetDistance = SampleDepthForFocus(context, dof.AdaptiveCenterBias);

            // Smooth lerp towards target
            float deltaTime = 1.0f / 60.0f; // Assume 60 FPS for now
            float adaptSpeed = dof.AdaptiveSpeed * deltaTime;

            _currentFocusDistance = _currentFocusDistance + (targetDistance - _currentFocusDistance) * adaptSpeed;
            _currentFocusDistance = Math.Clamp(_currentFocusDistance, dof.AdaptiveMinDistance, dof.AdaptiveMaxDistance);

            return _currentFocusDistance;
        }

        private int _depthReadFBO = 0;

        private float SampleDepthForFocus(PostProcessContext context, float centerBias)
        {
            if (context.DepthTexture == 0) return 10.0f; // Fallback

            try
            {
                // Create FBO for reading depth if not already created
                if (_depthReadFBO == 0)
                {
                    _depthReadFBO = GL.GenFramebuffer();
                }

                // Bind depth texture to FBO
                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _depthReadFBO);
                GL.FramebufferTexture2D(FramebufferTarget.ReadFramebuffer, FramebufferAttachment.DepthAttachment,
                                       TextureTarget.Texture2D, context.DepthTexture, 0);

                // Read a single pixel at screen center - USING POOLED BUFFER (no allocation!)
                int centerX = context.Width / 2;
                int centerY = context.Height / 2;

                GL.ReadPixels(centerX, centerY, 1, 1, PixelFormat.DepthComponent, PixelType.Float, _depthReadBuffer);

                // Use pooled samples buffer - NO ALLOCATIONS
                _samplesBuffer[0] = _depthReadBuffer[0]; // Center

                // Sample 4 points around center (reuse same buffer)
                int offset = Math.Min(context.Width, context.Height) / 10; // 10% offset
                GL.ReadPixels(centerX + offset, centerY, 1, 1, PixelFormat.DepthComponent, PixelType.Float, _depthReadBuffer);
                _samplesBuffer[1] = _depthReadBuffer[0];
                GL.ReadPixels(centerX - offset, centerY, 1, 1, PixelFormat.DepthComponent, PixelType.Float, _depthReadBuffer);
                _samplesBuffer[2] = _depthReadBuffer[0];
                GL.ReadPixels(centerX, centerY + offset, 1, 1, PixelFormat.DepthComponent, PixelType.Float, _depthReadBuffer);
                _samplesBuffer[3] = _depthReadBuffer[0];
                GL.ReadPixels(centerX, centerY - offset, 1, 1, PixelFormat.DepthComponent, PixelType.Float, _depthReadBuffer);
                _samplesBuffer[4] = _depthReadBuffer[0];

                // Unbind FBO
                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0);

                // Process samples
                float centerDepthLinear = LinearizeDepth(_samplesBuffer[0], 0.1f, 1000.0f);
                float avgDepth = 0.0f;
                for (int i = 0; i < _samplesBuffer.Length; i++)
                {
                    avgDepth += LinearizeDepth(_samplesBuffer[i], 0.1f, 1000.0f);
                }
                avgDepth /= _samplesBuffer.Length;

                // Blend between center and average based on centerBias
                float targetDepth = centerDepthLinear * centerBias + avgDepth * (1.0f - centerBias);

                return Math.Clamp(targetDepth, 0.1f, 1000.0f);
            }
            catch (Exception ex)
            {
                try { Engine.Utils.DebugLogger.Log($"[DOF] Adaptive focus sampling failed: {ex.Message}"); } catch { }
                return 10.0f;
            }
        }

        private float LinearizeDepth(float depth, float near, float far)
        {
            float z = depth * 2.0f - 1.0f; // Back to NDC
            return (2.0f * near * far) / (far + near - z * (far - near));
        }

        private void RenderCoC(DepthOfFieldEffect dof, PostProcessContext context, float focusDistance)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _cocFBO);
            GL.Viewport(0, 0, _width, _height);
            GL.Clear(ClearBufferMask.ColorBufferBit);

            _cocShader!.Use();

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, context.SourceTexture);
            _cocShader.SetInt("u_ColorTexture", 0);

            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, context.DepthTexture);
            _cocShader.SetInt("u_DepthTexture", 1);

            _cocShader.SetFloat("u_FocusDistance", focusDistance);
            _cocShader.SetFloat("u_FocusRange", dof.FocusRange);
            _cocShader.SetFloat("u_FocalLength", dof.FocalLength);
            _cocShader.SetFloat("u_Aperture", dof.Aperture);
            _cocShader.SetFloat("u_MaxCoC", dof.MaxCoC);
            _cocShader.SetFloat("u_NearPlane", 0.1f); // TODO: Get from camera
            _cocShader.SetFloat("u_FarPlane", 1000.0f); // TODO: Get from camera

            if (context.ProjectionMatrix.HasValue)
            {
                var invProj = context.ProjectionMatrix.Value.Inverted();
                _cocShader.SetMat4("u_InvProjection", invProj);
            }

            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        }

        private void RenderDownsample(PostProcessContext context)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _downsampledFBO);
            GL.Viewport(0, 0, _width / 2, _height / 2);

            _downsampleShader!.Use();

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _cocTexture);
            _downsampleShader.SetInt("u_SourceTexture", 0);
            _downsampleShader.SetVec2("u_TexelSize", new Vector2(1.0f / _width, 1.0f / _height));

            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        }

        private void RenderBokeh(DepthOfFieldEffect dof, PostProcessContext context)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _bokehFBO);
            GL.Viewport(0, 0, _width / 2, _height / 2);

            _bokehShader!.Use();

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _downsampledTexture);
            _bokehShader.SetInt("u_SourceTexture", 0);
            _bokehShader.SetVec2("u_TexelSize", new Vector2(2.0f / _width, 2.0f / _height));
            _bokehShader.SetInt("u_SampleCount", dof.SampleCount);
            _bokehShader.SetFloat("u_BokehRadius", dof.BokehRadius);
            _bokehShader.SetFloat("u_BokehRotation", (float)(Environment.TickCount * 0.001) % (2.0f * MathF.PI)); // Temporal rotation

            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        }

        private void RenderRecombine(DepthOfFieldEffect dof, PostProcessContext context)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, context.TargetFramebuffer);
            GL.Viewport(0, 0, _width, _height);

            _recombineShader!.Use();

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _cocTexture);
            _recombineShader.SetInt("u_SharpTexture", 0);

            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, _bokehTexture);
            _recombineShader.SetInt("u_BlurredTexture", 1);

            GL.ActiveTexture(TextureUnit.Texture2);
            GL.BindTexture(TextureTarget.Texture2D, context.DepthTexture);
            _recombineShader.SetInt("u_DepthTexture", 2);

            _recombineShader.SetFloat("u_NearPlane", 0.1f); // TODO: Get from camera
            _recombineShader.SetFloat("u_FarPlane", 1000.0f); // TODO: Get from camera

            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        }

        public void Dispose()
        {
            _cocShader?.Dispose();
            _downsampleShader?.Dispose();
            _bokehShader?.Dispose();
            _recombineShader?.Dispose();

            if (_cocTexture != 0) GL.DeleteTexture(_cocTexture);
            if (_downsampledTexture != 0) GL.DeleteTexture(_downsampledTexture);
            if (_bokehTexture != 0) GL.DeleteTexture(_bokehTexture);
            if (_cocFBO != 0) GL.DeleteFramebuffer(_cocFBO);
            if (_downsampledFBO != 0) GL.DeleteFramebuffer(_downsampledFBO);
            if (_bokehFBO != 0) GL.DeleteFramebuffer(_bokehFBO);
            if (_depthReadFBO != 0) GL.DeleteFramebuffer(_depthReadFBO);
        }
    }

    /// <summary>
    /// Effet de Motion Blur (flou de mouvement)
    /// </summary>
    public class MotionBlurEffect : PostProcessEffect
    {
        public override string EffectName => "Motion Blur";

        [Engine.Serialization.SerializableAttribute("samplecount")]
        public int SampleCount { get; set; } = 16; // Number of samples along motion vector (4-32)

        [Engine.Serialization.SerializableAttribute("maxblurradius")]
        public float MaxBlurRadius { get; set; } = 50.0f; // Maximum blur radius in pixels

        public MotionBlurEffect()
        {
            Priority = 18; // After DOF (12), before chromatic aberration (20)
        }

        public override void Apply(PostProcessContext context)
        {
            // Handled by MotionBlurRenderer
        }
    }

    /// <summary>
    /// Renderer pour l'effet de Motion Blur
    /// </summary>
    public class MotionBlurRenderer : IPostProcessRenderer
    {
        private ShaderProgram? _motionBlurShader;

        // Previous frame matrices for velocity calculation
        private Matrix4 _prevViewProj = Matrix4.Identity;
        private bool _isFirstFrame = true;

        public void Initialize()
        {
            try
            {
                var baseDir = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "Engine", "Rendering", "Shaders", "PostProcess"));
                var vertPath = Path.Combine(baseDir, "fullscreen.vert");
                var fragPath = Path.Combine(baseDir, "motionblur.frag");

                if (File.Exists(vertPath) && File.Exists(fragPath))
                {
                    string vertexSource = File.ReadAllText(vertPath);
                    string fragmentSource = File.ReadAllText(fragPath);
                    _motionBlurShader = ShaderProgram.FromSource(vertexSource, fragmentSource);
                }
            }
            catch (Exception ex)
            {
                try { Engine.Utils.DebugLogger.Log($"[MotionBlur] Initialization failed: {ex.Message}"); } catch { }
                _motionBlurShader = null;
            }
        }

        public void Render(PostProcessEffect effect, PostProcessContext context)
        {
            if (_motionBlurShader == null)
            {
                Initialize();
                if (_motionBlurShader == null) return;
            }

            var motionBlur = effect as MotionBlurEffect;
            if (motionBlur == null) return;

            if (context.DepthTexture == 0) return; // Need depth for motion blur

            // Skip first frame (no previous matrix available)
            if (_isFirstFrame)
            {
                if (context.ProjectionMatrix.HasValue && context.ViewMatrix.HasValue)
                {
                    _prevViewProj = context.ViewMatrix.Value * context.ProjectionMatrix.Value;
                    _isFirstFrame = false;
                }
                return;
            }

            // Bind target framebuffer
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, context.TargetFramebuffer);
            GL.Viewport(0, 0, context.Width, context.Height);

            _motionBlurShader.Use();

            // Bind textures
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, context.SourceTexture);
            _motionBlurShader.SetInt("u_ColorTexture", 0);

            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, context.DepthTexture);
            _motionBlurShader.SetInt("u_DepthTexture", 1);

            // Velocity texture (if available, otherwise will use depth-based reconstruction)
            GL.ActiveTexture(TextureUnit.Texture2);
            int velocityTex = 0; // TODO: Get velocity texture from context if available
            GL.BindTexture(TextureTarget.Texture2D, velocityTex);
            _motionBlurShader.SetInt("u_VelocityTexture", 2);

            // Motion blur parameters
            _motionBlurShader.SetInt("u_SampleCount", motionBlur.SampleCount);
            _motionBlurShader.SetFloat("u_Intensity", motionBlur.Intensity);
            _motionBlurShader.SetFloat("u_MaxBlurRadius", motionBlur.MaxBlurRadius);

            // Camera matrices
            if (context.ProjectionMatrix.HasValue && context.ViewMatrix.HasValue)
            {
                var currentViewProj = context.ViewMatrix.Value * context.ProjectionMatrix.Value;
                var invViewProj = currentViewProj.Inverted();

                _motionBlurShader.SetMat4("u_InvViewProj", invViewProj);
                _motionBlurShader.SetMat4("u_PrevViewProj", _prevViewProj);

                // Update for next frame
                _prevViewProj = currentViewProj;
            }

            _motionBlurShader.SetVec2("u_ScreenSize", new Vector2(context.Width, context.Height));
            _motionBlurShader.SetFloat("u_NearPlane", 0.1f); // TODO: Get from camera
            _motionBlurShader.SetFloat("u_FarPlane", 1000.0f); // TODO: Get from camera

            // Render fullscreen triangle
            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        }

        public void Dispose()
        {
            _motionBlurShader?.Dispose();
            _motionBlurShader = null;
        }
    }

    /// <summary>
    /// Renderer pour l'effet GTAO
    /// </summary>
    public class GTAORenderer : IPostProcessRenderer
    {
        private ShaderProgram? _gtaoShader;
        private ShaderProgram? _temporalShader;
        private ShaderProgram? _blurShader;
        private ShaderProgram? _combineShader;
        private ShaderProgram? _depthMipmapShader;
        
        // Textures and FBOs
        private int _gtaoTexture;
        private int _temporalTexture;
        private int _blurTexture;
        private int _gtaoFBO;
        private int _temporalFBO;
        private int _blurFBO;
        
        // Depth mipmap for multi-scale GTAO
        private int _depthMipmapTexture;
        private int[] _depthMipmapFBOs = new int[4]; // Support up to 4 mip levels
        private const int MAX_MIP_LEVELS = 4;
        
        // History buffers for temporal filtering
        private int _historyGTAOTexture;
        private int _historyDepthTexture;
        private Matrix4 _prevProjectionMatrix;
        private Matrix4 _prevViewMatrix;
        private bool _isFirstFrame = true;
        
        private int _width, _height;

        public void Initialize()
        {
            try
            {
                try { Engine.Utils.DebugLogger.Log("[GTAO] Initializing..."); } catch { }
                var baseDir = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "Engine", "Rendering", "Shaders", "PostProcess"));
                
                var vertPath = Path.Combine(baseDir, "fullscreen.vert");
                var gtaoFragPath = Path.Combine(baseDir, "gtao.frag");
                var temporalFragPath = Path.Combine(baseDir, "gtao_temporal.frag");
                var blurFragPath = Path.Combine(baseDir, "gtao_blur.frag");
                var combineFragPath = Path.Combine(baseDir, "ssao_combine.frag");
                var depthMipmapFragPath = Path.Combine(baseDir, "gtao_depth_mipmap.frag");

                if (File.Exists(vertPath) && File.Exists(gtaoFragPath) && 
                    File.Exists(temporalFragPath) && File.Exists(blurFragPath) && 
                    File.Exists(combineFragPath) && File.Exists(depthMipmapFragPath))
                {
                    string vertexSource = File.ReadAllText(vertPath);
                    string gtaoSource = File.ReadAllText(gtaoFragPath);
                    string temporalSource = File.ReadAllText(temporalFragPath);
                    string blurSource = File.ReadAllText(blurFragPath);
                    string combineSource = File.ReadAllText(combineFragPath);
                    string depthMipmapSource = File.ReadAllText(depthMipmapFragPath);
                    
                    _gtaoShader = ShaderProgram.FromSource(vertexSource, gtaoSource);
                    _temporalShader = ShaderProgram.FromSource(vertexSource, temporalSource);
                    _blurShader = ShaderProgram.FromSource(vertexSource, blurSource);
                    _combineShader = ShaderProgram.FromSource(vertexSource, combineSource);
                    _depthMipmapShader = ShaderProgram.FromSource(vertexSource, depthMipmapSource);
                    try { Engine.Utils.DebugLogger.Log("[GTAO] ✓ Initialized successfully"); } catch { }
                }
                else
                {
                    try { Engine.Utils.DebugLogger.Log("[GTAO] ERROR: Shader files not found!"); } catch { }
                }
            }
            catch (Exception ex)
            {
                try { Engine.Utils.DebugLogger.Log($"[GTAO] INITIALIZATION FAILED: {ex.Message}"); } catch { }
                _gtaoShader = null;
                _temporalShader = null;
                _blurShader = null;
                _combineShader = null;
            }
        }

        private void ResizeBuffers(int width, int height)
        {
            if (_width == width && _height == height && _gtaoTexture != 0) return;

            _width = width;
            _height = height;

            // Cleanup old textures
            if (_gtaoTexture != 0) GL.DeleteTexture(_gtaoTexture);
            if (_temporalTexture != 0) GL.DeleteTexture(_temporalTexture);
            if (_blurTexture != 0) GL.DeleteTexture(_blurTexture);
            if (_historyGTAOTexture != 0) GL.DeleteTexture(_historyGTAOTexture);
            if (_historyDepthTexture != 0) GL.DeleteTexture(_historyDepthTexture);
            if (_depthMipmapTexture != 0) GL.DeleteTexture(_depthMipmapTexture);
            if (_gtaoFBO != 0) GL.DeleteFramebuffer(_gtaoFBO);
            if (_temporalFBO != 0) GL.DeleteFramebuffer(_temporalFBO);
            if (_blurFBO != 0) GL.DeleteFramebuffer(_blurFBO);
            for (int i = 0; i < MAX_MIP_LEVELS; i++)
            {
                if (_depthMipmapFBOs[i] != 0) GL.DeleteFramebuffer(_depthMipmapFBOs[i]);
            }

            // Create GTAO texture (half resolution, RGBA16F for bent normals + AO)
            int gtaoWidth = width / 2;
            int gtaoHeight = height / 2;

            _gtaoTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _gtaoTexture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f, gtaoWidth, gtaoHeight, 0, 
                         PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            _gtaoFBO = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _gtaoFBO);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, 
                                   TextureTarget.Texture2D, _gtaoTexture, 0);

            // Create temporal texture (half resolution, RGBA16F)
            _temporalTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _temporalTexture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f, gtaoWidth, gtaoHeight, 0, 
                         PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            _temporalFBO = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _temporalFBO);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, 
                                   TextureTarget.Texture2D, _temporalTexture, 0);

            // Create blur texture (half resolution, R16F for final AO only)
            _blurTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _blurTexture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.R16f, gtaoWidth, gtaoHeight, 0, 
                         PixelFormat.Red, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            _blurFBO = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _blurFBO);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, 
                                   TextureTarget.Texture2D, _blurTexture, 0);

            // Create history texture (half resolution, RGBA16F for bent normals + AO)
            _historyGTAOTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _historyGTAOTexture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f, gtaoWidth, gtaoHeight, 0, 
                         PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            _historyDepthTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _historyDepthTexture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent32f, gtaoWidth, gtaoHeight, 0, 
                         PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            // Create depth mipmap texture (RG16F for min/max depth per mip level)
            // Base level is half-resolution to match GTAO resolution
            _depthMipmapTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _depthMipmapTexture);
            GL.TexStorage2D(TextureTarget2d.Texture2D, MAX_MIP_LEVELS, SizedInternalFormat.Rg16f, gtaoWidth, gtaoHeight);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            
            // Create FBOs for each mip level
            for (int mip = 0; mip < MAX_MIP_LEVELS; mip++)
            {
                _depthMipmapFBOs[mip] = GL.GenFramebuffer();
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, _depthMipmapFBOs[mip]);
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                                       TextureTarget.Texture2D, _depthMipmapTexture, mip);
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            
            // Mark as first frame after resize
            _isFirstFrame = true;
        }

        public void Render(PostProcessEffect effect, PostProcessContext context)
        {
            if (_gtaoShader == null || _blurShader == null || _combineShader == null)
            {
                Initialize();
                if (_gtaoShader == null || _blurShader == null || _combineShader == null) return;
            }

            var gtaoEffect = effect as GTAOEffect;
            if (gtaoEffect == null) return;

            ResizeBuffers(context.Width, context.Height);

            int gtaoWidth = context.Width / 2;
            int gtaoHeight = context.Height / 2;

            // 0. Generate depth mipmap pyramid (if multi-scale enabled)
            if (gtaoEffect.MipLevels > 1 && _depthMipmapShader != null)
            {
                GenerateDepthMipmaps(context.DepthTexture, gtaoWidth, gtaoHeight);
            }

            // 1. Generate GTAO
            RenderGTAO(context, gtaoEffect, gtaoWidth, gtaoHeight);

            // 2. Temporal filtering (if enabled)
            int aoSource = _gtaoTexture;
            if (gtaoEffect.EnableTemporal && _temporalShader != null)
            {
                RenderTemporal(context, gtaoEffect, gtaoWidth, gtaoHeight);
                aoSource = _temporalTexture;
            }

            // 3. Spatial blur
            RenderBlur(context, gtaoEffect, gtaoWidth, gtaoHeight, aoSource);

            // 4. Combine with scene
            CombineWithScene(context, gtaoEffect.Intensity);
            
            // 5. Update history for next frame
            if (gtaoEffect.EnableTemporal)
            {
                UpdateHistory(context, aoSource, gtaoWidth, gtaoHeight);
            }
        }

        private void GenerateDepthMipmaps(uint depthTexture, int baseWidth, int baseHeight)
        {
            if (_depthMipmapShader == null) return;
            
            _depthMipmapShader.Use();
            
            GL.Disable(EnableCap.DepthTest);
            GL.Disable(EnableCap.Blend);
            
            // Generate each mip level from previous level
            for (int mip = 0; mip < MAX_MIP_LEVELS; mip++)
            {
                int mipWidth = Math.Max(1, baseWidth >> mip);
                int mipHeight = Math.Max(1, baseHeight >> mip);
                
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, _depthMipmapFBOs[mip]);
                GL.Viewport(0, 0, mipWidth, mipHeight);
                
                // Bind source texture (either original depth or previous mip)
                GL.ActiveTexture(TextureUnit.Texture0);
                if (mip == 0)
                {
                    // Mip 0: sample original depth (full resolution)
                    GL.BindTexture(TextureTarget.Texture2D, (int)depthTexture);
                }
                else
                {
                    // Mip 1+: sample previous mip level
                    GL.BindTexture(TextureTarget.Texture2D, _depthMipmapTexture);
                }
                _depthMipmapShader.SetInt("u_SourceDepth", 0);
                _depthMipmapShader.SetInt("u_MipLevel", mip);
                _depthMipmapShader.SetVec2("u_TexelSize", new OpenTK.Mathematics.Vector2(1.0f / mipWidth, 1.0f / mipHeight));
                
                // Draw fullscreen quad
                GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
            }
            
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        private void RenderGTAO(PostProcessContext context, GTAOEffect effect, int width, int height)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _gtaoFBO);
            GL.Viewport(0, 0, width, height);
            GL.Clear(ClearBufferMask.ColorBufferBit);

            _gtaoShader!.Use();

            // Bind depth texture
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, context.DepthTexture);
            _gtaoShader.SetInt("u_DepthTexture", 0);

            // Bind depth mipmap (for multi-scale)
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, _depthMipmapTexture);
            _gtaoShader.SetInt("u_DepthMipmap", 1);

            // Parameters
            _gtaoShader.SetFloat("u_Radius", effect.Radius);
            _gtaoShader.SetFloat("u_Thickness", effect.Thickness);
            _gtaoShader.SetFloat("u_FalloffRange", effect.FalloffRange);
            _gtaoShader.SetFloat("u_MaxDistance", effect.MaxDistance);
            _gtaoShader.SetInt("u_SampleCount", effect.SampleCount);
            _gtaoShader.SetInt("u_SliceCount", effect.SliceCount);

            // Multi-scale parameters
            _gtaoShader.SetInt("u_MipLevels", effect.MipLevels);
            
            float[] mipWeights = { effect.MipWeight0, effect.MipWeight1, effect.MipWeight2, effect.MipWeight3 };
            float[] mipRadii = { effect.MipRadius0, effect.MipRadius1, effect.MipRadius2, effect.MipRadius3 };
            
            for (int i = 0; i < 4; i++)
            {
                _gtaoShader.SetFloat($"u_MipWeights[{i}]", mipWeights[i]);
                _gtaoShader.SetFloat($"u_MipRadii[{i}]", mipRadii[i]);
            }

            // Matrices
            if (context.ProjectionMatrix.HasValue)
            {
                var projMatrix = context.ProjectionMatrix.Value;
                _gtaoShader.SetMat4("u_Projection", projMatrix);
                
                var invProj = projMatrix.Inverted();
                _gtaoShader.SetMat4("u_InvProjection", invProj);
            }

            // Frame counter pour temporal variation (utiliser un compteur simple)
            _gtaoShader.SetInt("u_FrameCounter", Environment.TickCount & 0xFF);

            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        }

        private void RenderBlur(PostProcessContext context, GTAOEffect effect, int width, int height, int sourceTexture)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _blurFBO);
            GL.Viewport(0, 0, width, height);

            _blurShader!.Use();

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, sourceTexture);
            _blurShader.SetInt("u_GTAOTexture", 0);

            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, context.DepthTexture);
            _blurShader.SetInt("u_DepthTexture", 1);

            _blurShader.SetInt("u_BlurRadius", effect.BlurRadius);
            
            if (context.ProjectionMatrix.HasValue)
            {
                var invProj = context.ProjectionMatrix.Value.Inverted();
                _blurShader.SetMat4("u_InvProjection", invProj);
            }

            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        }

        private void RenderTemporal(PostProcessContext context, GTAOEffect effect, int width, int height)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _temporalFBO);
            GL.Viewport(0, 0, width, height);
            
            // On first frame or when matrices are missing, just copy current GTAO
            bool hasValidMatrices = context.ProjectionMatrix.HasValue && context.ViewMatrix.HasValue;
            
            if (_isFirstFrame || !hasValidMatrices)
            {
                if (!hasValidMatrices)
                {
                    try { Engine.Utils.DebugLogger.Log("[GTAO] Warning: ViewMatrix or ProjectionMatrix missing - temporal disabled for this frame"); } catch { }
                }
                
                // Simple copy using a blit or direct texture copy
                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _gtaoFBO);
                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, _temporalFBO);
                GL.BlitFramebuffer(0, 0, width, height, 0, 0, width, height,
                                  ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
                
                _isFirstFrame = false;
                return;
            }

            _temporalShader!.Use();

            // Bind textures
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _gtaoTexture);
            _temporalShader.SetInt("u_CurrentGTAO", 0);

            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, _historyGTAOTexture);
            _temporalShader.SetInt("u_HistoryGTAO", 1);

            GL.ActiveTexture(TextureUnit.Texture2);
            GL.BindTexture(TextureTarget.Texture2D, context.DepthTexture);
            _temporalShader.SetInt("u_CurrentDepth", 2);

            GL.ActiveTexture(TextureUnit.Texture3);
            GL.BindTexture(TextureTarget.Texture2D, _historyDepthTexture);
            _temporalShader.SetInt("u_HistoryDepth", 3);

            // Parameters
            _temporalShader.SetFloat("u_BlendFactor", effect.TemporalBlendFactor);
            _temporalShader.SetFloat("u_VarianceThreshold", effect.TemporalVarianceThreshold);

            // Matrices for reprojection
            if (context.ProjectionMatrix.HasValue && context.ViewMatrix.HasValue)
            {
                var currentProj = context.ProjectionMatrix.Value;
                var currentView = context.ViewMatrix.Value;
                var currentInvProj = currentProj.Inverted();
                var currentInvView = currentView.Inverted();
                
                _temporalShader.SetMat4("u_CurrentInvProjection", currentInvProj);
                _temporalShader.SetMat4("u_CurrentInvView", currentInvView);
                
                // Compute previous view-projection
                var prevViewProj = _prevViewMatrix * _prevProjectionMatrix;
                _temporalShader.SetMat4("u_PrevViewProjection", prevViewProj);
            }
            else if (context.ProjectionMatrix.HasValue)
            {
                // Fallback: Use screen-space reprojection (less accurate but works)
                var currentProj = context.ProjectionMatrix.Value;
                var currentInvProj = currentProj.Inverted();
                
                // Use identity for view (screen-space only)
                _temporalShader.SetMat4("u_CurrentInvProjection", currentInvProj);
                _temporalShader.SetMat4("u_CurrentInvView", Matrix4.Identity);
                _temporalShader.SetMat4("u_PrevViewProjection", _prevProjectionMatrix);
            }

            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        }

        private void UpdateHistory(PostProcessContext context, int currentAOTexture, int width, int height)
        {
            // Copy current GTAO to history
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _temporalFBO);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            
            // Bind history texture as target
            int tempFBO = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, tempFBO);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                                   TextureTarget.Texture2D, _historyGTAOTexture, 0);
            
            // Copy from current to history
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _temporalFBO);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, tempFBO);
            GL.BlitFramebuffer(0, 0, width, height, 0, 0, width, height, 
                              ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
            
            GL.DeleteFramebuffer(tempFBO);
            
            // Copy depth buffer (downsampled)
            // TODO: Proper depth downsample, for now just copy
            
            // Store matrices for next frame
            if (context.ProjectionMatrix.HasValue)
            {
                _prevProjectionMatrix = context.ProjectionMatrix.Value;
            }
            if (context.ViewMatrix.HasValue)
            {
                _prevViewMatrix = context.ViewMatrix.Value;
            }
            
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        private void CombineWithScene(PostProcessContext context, float intensity)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, context.TargetFramebuffer);
            GL.Viewport(0, 0, context.Width, context.Height);

            _combineShader!.Use();

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, context.SourceTexture);
            _combineShader.SetInt("u_ColorTexture", 0);

            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, _blurTexture);
            _combineShader.SetInt("u_SSAOTexture", 1);

            _combineShader.SetFloat("u_Intensity", intensity);

            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        }

        public void Dispose()
        {
            _gtaoShader?.Dispose();
            _temporalShader?.Dispose();
            _blurShader?.Dispose();
            _combineShader?.Dispose();

            if (_gtaoTexture != 0) GL.DeleteTexture(_gtaoTexture);
            if (_temporalTexture != 0) GL.DeleteTexture(_temporalTexture);
            if (_blurTexture != 0) GL.DeleteTexture(_blurTexture);
            if (_historyGTAOTexture != 0) GL.DeleteTexture(_historyGTAOTexture);
            if (_historyDepthTexture != 0) GL.DeleteTexture(_historyDepthTexture);
            if (_gtaoFBO != 0) GL.DeleteFramebuffer(_gtaoFBO);
            if (_temporalFBO != 0) GL.DeleteFramebuffer(_temporalFBO);
            if (_blurFBO != 0) GL.DeleteFramebuffer(_blurFBO);
        }
    }

    /// <summary>
    /// Image Sharpening Effect (AMD FidelityFX CAS)
    /// Contrast Adaptive Sharpening with edge detection
    /// </summary>
    public class ImageSharpeningEffect : PostProcessEffect
    {
        public override string EffectName => "Image Sharpening";

        [Engine.Serialization.SerializableAttribute("sharpness")]
        public float Sharpness { get; set; } = 0.5f; // 0.0 = no sharpening, 1.0 = maximum

        public ImageSharpeningEffect()
        {
            Priority = 25; // After FXAA/TAA, final sharpening pass
        }

        public override void Apply(PostProcessContext context)
        {
            // Rendered by ImageSharpeningRenderer
        }
    }

    /// <summary>
    /// Renderer for Image Sharpening Effect
    /// </summary>
    public class ImageSharpeningRenderer : IPostProcessRenderer
    {
        private ShaderProgram? _sharpeningShader;

        public void Initialize()
        {
            try
            {
                var baseDir = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.Environment.CurrentDirectory, "Engine", "Rendering", "Shaders", "PostProcess"));
                var vertPath = System.IO.Path.Combine(baseDir, "image_sharpening.vert");
                var fragPath = System.IO.Path.Combine(baseDir, "image_sharpening.frag");

                if (System.IO.File.Exists(vertPath) && System.IO.File.Exists(fragPath))
                {
                    // Load shader sources
                    string vertexSource = System.IO.File.ReadAllText(vertPath);
                    string fragmentSource = System.IO.File.ReadAllText(fragPath);
                    _sharpeningShader = ShaderProgram.FromSource(vertexSource, fragmentSource);
                }
            }
            catch (Exception)
            {
                _sharpeningShader = null;
            }
        }

        public void Render(PostProcessEffect effect, PostProcessContext context)
        {
            if (_sharpeningShader == null)
            {
                Initialize();
            }

            if (_sharpeningShader == null || effect is not ImageSharpeningEffect sharpeningEffect)
                return;

            if (!sharpeningEffect.Enabled || sharpeningEffect.Sharpness < 0.01f)
                return;

            _sharpeningShader.Use();

            // Bind source texture
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, context.SourceTexture);
            _sharpeningShader.SetInt("u_InputTexture", 0);

            // Set parameters
            _sharpeningShader.SetFloat("u_Sharpness", sharpeningEffect.Sharpness * sharpeningEffect.Intensity);
            _sharpeningShader.SetVec2("u_TexelSize", new OpenTK.Mathematics.Vector2(1.0f / context.Width, 1.0f / context.Height));

            // Draw fullscreen triangle (project convention - no VAO needed)
            GL.DrawArrays(OpenTK.Graphics.OpenGL4.PrimitiveType.Triangles, 0, 3);
        }

        public void Dispose()
        {
            _sharpeningShader?.Dispose();
            _sharpeningShader = null;
        }
    }
}
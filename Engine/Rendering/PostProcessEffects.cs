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
    /// Renderer pour l'effet de tone mapping
    /// </summary>
    public class ToneMappingRenderer : IPostProcessRenderer
    {
        private ShaderProgram? _shader;

        public void Initialize()
        {
            try
            {
                
                // Chemins absolus pour éviter les problèmes de répertoire de travail
                var baseDir = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "Engine", "Rendering", "Shaders", "PostProcess"));
                var vertPath = Path.Combine(baseDir, "fullscreen.vert");
                var fragPath = Path.Combine(baseDir, "tonemap.frag");
                
                
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
            
            if (_shader == null || !(effect is ToneMappingEffect toneMap))
            {
                return;
            }

            _shader.Use();

            // Bind la texture source
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, context.SourceTexture);
            _shader.SetInt("u_SourceTexture", 0);

            // Paramètres du tone mapping
            _shader.SetInt("u_ToneMappingMode", (int)toneMap.Mode);
            _shader.SetFloat("u_Exposure", toneMap.Exposure * toneMap.Intensity);
            _shader.SetFloat("u_WhitePoint", toneMap.WhitePoint);
            _shader.SetFloat("u_Gamma", toneMap.Gamma);

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
            }
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
}
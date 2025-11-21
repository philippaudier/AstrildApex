using System;
using System.IO;
using OpenTK.Graphics.OpenGL4;

namespace Engine.Rendering.PostProcess
{
    public class FXAARenderer : Engine.Components.IPostProcessRenderer
    {
        private ShaderProgram? _shader;
        private int _uTex = -1;
        private int _uScreenSize = -1;
        private int _uQuality = -1;
        private int _uIntensity = -1;

        public void Initialize()
        {
            try
            {
                var baseDir = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "Engine", "Rendering", "Shaders", "PostProcess"));
                var vertPath = Path.Combine(baseDir, "fxaa.vert");
                var fragPath = Path.Combine(baseDir, "fxaa.frag");

                if (File.Exists(vertPath) && File.Exists(fragPath))
                {
                    var vert = File.ReadAllText(vertPath);
                    var frag = File.ReadAllText(fragPath);
                    _shader = ShaderProgram.FromSource(vert, frag);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FXAA] Shader load failed: {ex.Message}");
                _shader = null;
            }

            if (_shader != null)
            {
                _shader.Use();
                _uTex = GL.GetUniformLocation(_shader.Handle, "u_Texture");
                _uScreenSize = GL.GetUniformLocation(_shader.Handle, "u_ScreenSize");
                _uQuality = GL.GetUniformLocation(_shader.Handle, "u_Quality");
                _uIntensity = GL.GetUniformLocation(_shader.Handle, "u_Intensity");
            }
        }

        public void Render(Engine.Components.PostProcessEffect effect, Engine.Components.PostProcessContext context)
        {
            if (_shader == null)
            {
                Initialize();
            }

            if (_shader == null || !(effect is FXAAEffect fxaa))
                return;

            _shader.Use();

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, context.SourceTexture);
            if (_uTex >= 0) GL.Uniform1(_uTex, 0);
            if (_uScreenSize >= 0) GL.Uniform2(_uScreenSize, new OpenTK.Mathematics.Vector2(context.Width, context.Height));
            if (_uQuality >= 0) GL.Uniform1(_uQuality, fxaa.Quality);
            if (_uIntensity >= 0) GL.Uniform1(_uIntensity, fxaa.Intensity);

            // Fullscreen triangle (project convention)
            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        }

        public void Dispose()
        {
            _shader?.Dispose();
            _shader = null;
        }
    }
}

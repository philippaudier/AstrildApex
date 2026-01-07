using System;
using System.IO;
using System.Linq;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace Engine.Rendering.PostProcess
{
    public class ColorGradingRenderer : Engine.Components.IPostProcessRenderer
    {
        private ShaderProgram? _shader;

        // Uniform locations
        private int _uSourceTexture = -1;
        private int _uSaturation = -1;
        private int _uContrast = -1;
        private int _uBrightness = -1;
        private int _uColorFilter = -1;
        private int _uTemperature = -1;
        private int _uTint = -1;
        private int _uHueShift = -1;
        private int _uVibrance = -1;

        public void Initialize()
        {
            try
            {
                var baseDir = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "Engine", "Rendering", "Shaders", "PostProcess"));
                var vertPath = Path.Combine(baseDir, "color_grading.vert");
                var fragPath = Path.Combine(baseDir, "color_grading.frag");

                if (File.Exists(vertPath) && File.Exists(fragPath))
                {
                    var vert = File.ReadAllText(vertPath);
                    var frag = File.ReadAllText(fragPath);
                    _shader = ShaderProgram.FromSource(vert, frag);
                }
            }
            catch (Exception ex)
            {
                try { Engine.Utils.DebugLogger.Log($"[ColorGrading] Shader load failed: {ex.Message}"); } catch { }
                _shader = null;
            }

            if (_shader != null)
            {
                _shader.Use();

                // Get all uniform locations
                _uSourceTexture = GL.GetUniformLocation(_shader.Handle, "u_SourceTexture");
                _uSaturation = GL.GetUniformLocation(_shader.Handle, "u_Saturation");
                _uContrast = GL.GetUniformLocation(_shader.Handle, "u_Contrast");
                _uBrightness = GL.GetUniformLocation(_shader.Handle, "u_Brightness");
                _uColorFilter = GL.GetUniformLocation(_shader.Handle, "u_ColorFilter");
                _uTemperature = GL.GetUniformLocation(_shader.Handle, "u_Temperature");
                _uTint = GL.GetUniformLocation(_shader.Handle, "u_Tint");
                _uHueShift = GL.GetUniformLocation(_shader.Handle, "u_HueShift");
                _uVibrance = GL.GetUniformLocation(_shader.Handle, "u_Vibrance");
            }
        }

        public void Render(Engine.Components.PostProcessEffect effect, Engine.Components.PostProcessContext context)
        {
            if (_shader == null)
            {
                Initialize();
            }

            if (_shader == null || !(effect is ColorGradingEffect grading))
                return;

            _shader.Use();

            // Bind source texture
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, context.SourceTexture);
            if (_uSourceTexture >= 0) GL.Uniform1(_uSourceTexture, 0);

            // Update color grading based on time of day if needed
            Engine.Components.TimeComponent? timeComponent = null;
            if (context.Scene != null)
            {
                var timeEntities = context.Scene.Cache != null
                    ? context.Scene.Cache.GetEntitiesWithComponent<Engine.Components.TimeComponent>()
                    : context.Scene.Entities.ToList();
                timeComponent = timeEntities.FirstOrDefault()?.GetComponent<Engine.Components.TimeComponent>();
            }
            if (timeComponent != null && grading.Source != ColorGradingEffect.ColorGradingSource.Manual)
            {
                grading.UpdateForTimeOfDay(timeComponent.TimeOfDay);
            }

            // Get effective parameters (considering TimeOfDay mode)
            float saturation = grading.GetEffectiveSaturation();
            float contrast = grading.GetEffectiveContrast();
            float brightness = grading.GetEffectiveBrightness();
            float temperature = grading.GetEffectiveTemperature();
            float tint = grading.GetEffectiveTint();
            float hueShift = grading.HueShift; // HueShift is always manual
            float vibrance = grading.Vibrance; // Vibrance is always manual
            Vector3 colorFilter = grading.ColorFilter; // ColorFilter is always manual

            // Set color grading parameters
            if (_uSaturation >= 0) GL.Uniform1(_uSaturation, saturation);
            if (_uContrast >= 0) GL.Uniform1(_uContrast, contrast);
            if (_uBrightness >= 0) GL.Uniform1(_uBrightness, brightness);
            if (_uColorFilter >= 0) GL.Uniform3(_uColorFilter, colorFilter);
            if (_uTemperature >= 0) GL.Uniform1(_uTemperature, temperature);
            if (_uTint >= 0) GL.Uniform1(_uTint, tint);
            if (_uHueShift >= 0) GL.Uniform1(_uHueShift, hueShift);
            if (_uVibrance >= 0) GL.Uniform1(_uVibrance, vibrance);

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

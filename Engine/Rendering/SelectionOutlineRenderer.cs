using System;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace Engine.Rendering
{
    /// <summary>
    /// Renders selection outline using edge detection post-process
    /// Based on: https://www.marginallyclever.com/2025/09/drawing-thick-outlines-in-opengl/
    /// </summary>
    public class SelectionOutlineRenderer : IDisposable
    {
        private int _edgeShader;
        private int _vao;
        private int _vbo;
        private bool _initialized = false;

        public struct OutlineSettings
        {
            public bool Enabled;
            public float Thickness;
            public Vector4 Color;
            public bool EnablePulse;
            public float PulseSpeed;
            public float PulseMinAlpha;
            public float PulseMaxAlpha;

            public static OutlineSettings Default => new OutlineSettings
            {
                Enabled = true,
                Thickness = 2.0f,
                Color = new Vector4(1.0f, 0.5f, 0.0f, 1.0f), // Orange
                EnablePulse = true,
                PulseSpeed = 2.0f,
                PulseMinAlpha = 0.3f,
                PulseMaxAlpha = 1.0f
            };
        }

        public void Initialize()
        {
            if (_initialized) return;

            // Load edge detection shader
            string vertPath = "Engine/Rendering/Shaders/Effects/SelectionOutlineEdge.vert";
            string fragPath = "Engine/Rendering/Shaders/Effects/SelectionOutlineEdge.frag";

            if (!System.IO.File.Exists(vertPath) || !System.IO.File.Exists(fragPath))
            {
                Console.WriteLine($"[SelectionOutlineRenderer] Shader files not found: {vertPath} or {fragPath}");
                return;
            }

            try
            {
                string vertSource = System.IO.File.ReadAllText(vertPath);
                string fragSource = System.IO.File.ReadAllText(fragPath);

                int vertShader = GL.CreateShader(ShaderType.VertexShader);
                GL.ShaderSource(vertShader, vertSource);
                GL.CompileShader(vertShader);
                CheckShaderCompileStatus(vertShader, "SelectionOutlineEdge.vert");

                int fragShader = GL.CreateShader(ShaderType.FragmentShader);
                GL.ShaderSource(fragShader, fragSource);
                GL.CompileShader(fragShader);
                CheckShaderCompileStatus(fragShader, "SelectionOutlineEdge.frag");

                _edgeShader = GL.CreateProgram();
                GL.AttachShader(_edgeShader, vertShader);
                GL.AttachShader(_edgeShader, fragShader);
                GL.LinkProgram(_edgeShader);
                CheckProgramLinkStatus(_edgeShader);

                GL.DeleteShader(vertShader);
                GL.DeleteShader(fragShader);

                Console.WriteLine("[SelectionOutlineRenderer] Edge shader compiled successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SelectionOutlineRenderer] Failed to compile edge shader: {ex.Message}");
                return;
            }

            // Create fullscreen quad
            float[] quadVertices = {
                // positions   // texCoords
                -1.0f,  1.0f,  0.0f, 1.0f,
                -1.0f, -1.0f,  0.0f, 0.0f,
                 1.0f, -1.0f,  1.0f, 0.0f,

                -1.0f,  1.0f,  0.0f, 1.0f,
                 1.0f, -1.0f,  1.0f, 0.0f,
                 1.0f,  1.0f,  1.0f, 1.0f
            };

            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();

            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, quadVertices.Length * sizeof(float), quadVertices, BufferUsageHint.StaticDraw);

            // Position attribute
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);

            // TexCoord attribute
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));

            GL.BindVertexArray(0);

            _initialized = true;
            Console.WriteLine("[SelectionOutlineRenderer] Initialized successfully");
        }

        /// <summary>
        /// Render selection outline as a post-process pass
        /// </summary>
        /// <param name="colorTexture">Scene color texture</param>
        /// <param name="idTexture">Entity ID texture (R32UI format)</param>
        /// <param name="selectedEntityId">ID of selected entity (0 = none)</param>
        /// <param name="screenWidth">Screen width in pixels</param>
        /// <param name="screenHeight">Screen height in pixels</param>
        /// <param name="settings">Outline rendering settings</param>
        /// <param name="time">Current time in seconds for pulse animation</param>
        public void RenderOutline(
            int colorTexture,
            int idTexture,
            uint selectedEntityId,
            int screenWidth,
            int screenHeight,
            OutlineSettings settings,
            float time)
        {
            if (!_initialized || !settings.Enabled || selectedEntityId == 0)
                return;

            GL.UseProgram(_edgeShader);

            // Set uniforms
            GL.Uniform1(GL.GetUniformLocation(_edgeShader, "u_ColorTexture"), 0);
            GL.Uniform1(GL.GetUniformLocation(_edgeShader, "u_IdTexture"), 1);
            GL.Uniform2(GL.GetUniformLocation(_edgeShader, "u_ScreenSize"), (float)screenWidth, (float)screenHeight);
            GL.Uniform4(GL.GetUniformLocation(_edgeShader, "u_OutlineColor"), settings.Color);
            GL.Uniform1(GL.GetUniformLocation(_edgeShader, "u_OutlineWidth"), settings.Thickness);
            GL.Uniform1(GL.GetUniformLocation(_edgeShader, "u_SelectedId"), (float)selectedEntityId);

            // Pulse parameters
            GL.Uniform1(GL.GetUniformLocation(_edgeShader, "u_Time"), time);
            GL.Uniform1(GL.GetUniformLocation(_edgeShader, "u_EnablePulse"), settings.EnablePulse ? 1 : 0);
            GL.Uniform1(GL.GetUniformLocation(_edgeShader, "u_PulseSpeed"), settings.PulseSpeed);
            GL.Uniform1(GL.GetUniformLocation(_edgeShader, "u_PulseMinAlpha"), settings.PulseMinAlpha);
            GL.Uniform1(GL.GetUniformLocation(_edgeShader, "u_PulseMaxAlpha"), settings.PulseMaxAlpha);

            // Bind textures
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, colorTexture);
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, idTexture);

            // Draw fullscreen quad
            GL.BindVertexArray(_vao);
            GL.Disable(EnableCap.DepthTest);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);

            GL.Disable(EnableCap.Blend);
            GL.Enable(EnableCap.DepthTest);
            GL.BindVertexArray(0);
            GL.UseProgram(0);
        }

        private void CheckShaderCompileStatus(int shader, string name)
        {
            GL.GetShader(shader, ShaderParameter.CompileStatus, out int status);
            if (status == 0)
            {
                string log = GL.GetShaderInfoLog(shader);
                throw new Exception($"Shader compilation failed ({name}):\n{log}");
            }
        }

        private void CheckProgramLinkStatus(int program)
        {
            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int status);
            if (status == 0)
            {
                string log = GL.GetProgramInfoLog(program);
                throw new Exception($"Shader program linking failed:\n{log}");
            }
        }

        public void Dispose()
        {
            if (_edgeShader != 0)
            {
                GL.DeleteProgram(_edgeShader);
                _edgeShader = 0;
            }

            if (_vao != 0)
            {
                GL.DeleteVertexArray(_vao);
                _vao = 0;
            }

            if (_vbo != 0)
            {
                GL.DeleteBuffer(_vbo);
                _vbo = 0;
            }

            _initialized = false;
        }
    }
}

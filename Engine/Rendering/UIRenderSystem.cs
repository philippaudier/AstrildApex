using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Engine.Components.UI;
using Engine.Scene;

namespace Engine.Rendering
{
    /// <summary>
    /// Modern UI rendering system - clean, simple, no runtime layer.
    /// Directly renders UIElementComponent with proper GL state management.
    /// </summary>
    public class UIRenderSystem : IDisposable
    {
        private int _shader;
        private int _vao, _vbo, _ebo;
        private Dictionary<Guid, int> _textureCache = new();
        private Dictionary<Guid, FontAtlasData> _fontCache = new();

        // Shader uniform locations
        private int _uProjection, _uModel, _uColor, _uUseTexture;

        public UIRenderSystem()
        {
            InitializeShader();
            InitializeGeometry();
        }

        /// <summary>
        /// Main render method - call this after scene rendering
        /// </summary>
        public void Render(Engine.Scene.Scene? scene, int viewportWidth, int viewportHeight)
        {
            if (scene == null) return;

            // Save GL state
            var state = SaveGLState();

            try
            {
                // Find all canvas elements
                var canvases = GetCanvasElements(scene);
                if (canvases.Count == 0) return;

                // Setup UI rendering state
                GL.Enable(EnableCap.Blend);
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                GL.Disable(EnableCap.DepthTest);
                GL.Disable(EnableCap.CullFace);

                GL.UseProgram(_shader);
                GL.BindVertexArray(_vao);

                // Setup orthographic projection (screen space)
                var projection = Matrix4x4.CreateOrthographicOffCenter(
                    0, viewportWidth, viewportHeight, 0, -1, 1
                );
                SetMatrix4(_uProjection, projection);

                // Render each canvas
                foreach (var (entity, canvas) in canvases)
                {
                    RenderCanvas(scene, entity, canvas, viewportWidth, viewportHeight);
                }

                // Cleanup
                GL.BindVertexArray(0);
                GL.UseProgram(0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UIRenderSystem] Error: {ex.Message}");
            }
            finally
            {
                // Restore GL state
                RestoreGLState(state);
                ClearGLErrors();
            }
        }

        private void RenderCanvas(Engine.Scene.Scene scene, Entity canvasEntity, UIElementComponent canvas, int viewportWidth, int viewportHeight)
        {
            if (!canvas.Enabled) return;

            Vector2 canvasSize = canvas.SizeDelta;
            if (canvasSize.X <= 0) canvasSize.X = viewportWidth;
            if (canvasSize.Y <= 0) canvasSize.Y = viewportHeight;

            // Get all UI elements that are children of this canvas
            var uiElements = GetUIElementsInCanvas(scene, canvasEntity);

            // Sort by sort order
            uiElements.Sort((a, b) => a.elem.SortOrder.CompareTo(b.elem.SortOrder));

            // Render each element
            foreach (var (entity, elem) in uiElements)
            {
                if (!elem.Enabled) continue;

                RectF rect = elem.CalculateWorldRect(canvasSize);
                
                switch (elem.Type)
                {
                    case UIElementComponent.ElementType.Image:
                        RenderImage(elem, rect);
                        break;
                    case UIElementComponent.ElementType.Text:
                        RenderText(elem, rect);
                        break;
                    case UIElementComponent.ElementType.Button:
                        RenderButton(elem, rect);
                        break;
                }
            }
        }

        private void RenderImage(UIElementComponent elem, RectF rect)
        {
            Vector4 color = elem.GetCurrentColor();
            int textureId = 0;

            if (elem.TextureGuid.HasValue)
            {
                textureId = GetOrLoadTexture(elem.TextureGuid.Value);
            }

            RenderQuad(rect, color, textureId);
        }

        private void RenderButton(UIElementComponent elem, RectF rect)
        {
            // Button is just an image with hover/pressed states
            RenderImage(elem, rect);

            // TODO: If button has text, render text on top
            if (!string.IsNullOrEmpty(elem.Text))
            {
                RenderText(elem, rect);
            }
        }

        private void RenderText(UIElementComponent elem, RectF rect)
        {
            if (string.IsNullOrEmpty(elem.Text)) return;

            // TODO: Implement text rendering using FontAtlas
            // For now, just render a colored quad
            Vector4 color = elem.Color;
            RenderQuad(rect, color, 0);
        }

        private void RenderQuad(RectF rect, Vector4 color, int textureId)
        {
            // Setup model matrix (position and scale)
            var model = Matrix4x4.CreateScale(rect.Width, rect.Height, 1) *
                        Matrix4x4.CreateTranslation(rect.X, rect.Y, 0);
            SetMatrix4(_uModel, model);

            // Setup color
            GL.Uniform4(_uColor, color.X, color.Y, color.Z, color.W);

            // Setup texture
            if (textureId > 0)
            {
                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.Texture2D, textureId);
                GL.Uniform1(_uUseTexture, 1);
            }
            else
            {
                GL.Uniform1(_uUseTexture, 0);
            }

            // Draw
            GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0);
        }

        private List<(Entity entity, UIElementComponent elem)> GetCanvasElements(Engine.Scene.Scene scene)
        {
            var result = new List<(Entity entity, UIElementComponent elem)>();

            foreach (var entity in scene.Entities)
            {
                var elem = entity.GetComponent<UIElementComponent>();
                if (elem != null && elem.Type == UIElementComponent.ElementType.Canvas)
                {
                    result.Add((entity, elem));
                }
            }

            return result;
        }

        private List<(Entity entity, UIElementComponent elem)> GetUIElementsInCanvas(Engine.Scene.Scene scene, Entity canvasEntity)
        {
            var result = new List<(Entity entity, UIElementComponent elem)>();

            // Get all children recursively
            var children = GetAllChildren(canvasEntity);

            foreach (var child in children)
            {
                var elem = child.GetComponent<UIElementComponent>();
                if (elem != null && elem.Type != UIElementComponent.ElementType.Canvas)
                {
                    result.Add((child, elem));
                }
            }

            return result;
        }

        private List<Entity> GetAllChildren(Entity parent)
        {
            var result = new List<Entity>();
            
            // Use Entity.Children property (from Scene.cs line 75)
            foreach (var child in parent.Children)
            {
                result.Add(child);
                result.AddRange(GetAllChildren(child));
            }

            return result;
        }

        private int GetOrLoadTexture(Guid textureGuid)
        {
            if (_textureCache.TryGetValue(textureGuid, out int cachedId))
                return cachedId;

            try
            {
                int textureHandle = TextureCache.GetOrLoad(textureGuid, guid =>
                {
                    if (Engine.Assets.AssetDatabase.TryGet(guid, out var record))
                        return record.Path;
                    return null;
                });

                if (textureHandle > 0)
                {
                    _textureCache[textureGuid] = textureHandle;
                    return textureHandle;
                }
            }
            catch { }

            return 0;
        }

        // === GL State Management ===

        private struct GLState
        {
            public bool BlendEnabled;
            public bool DepthTestEnabled;
            public bool CullFaceEnabled;
            public int BlendSrcRgb, BlendDstRgb;
            public int BlendSrcAlpha, BlendDstAlpha;
            public int Program;
            public int Vao;
        }

        private GLState SaveGLState()
        {
            var state = new GLState();
            state.BlendEnabled = GL.GetBoolean(GetPName.Blend);
            state.DepthTestEnabled = GL.GetBoolean(GetPName.DepthTest);
            state.CullFaceEnabled = GL.GetBoolean(GetPName.CullFace);
            GL.GetInteger(GetPName.BlendSrcRgb, out state.BlendSrcRgb);
            GL.GetInteger(GetPName.BlendDstRgb, out state.BlendDstRgb);
            GL.GetInteger(GetPName.BlendSrcAlpha, out state.BlendSrcAlpha);
            GL.GetInteger(GetPName.BlendDstAlpha, out state.BlendDstAlpha);
            GL.GetInteger(GetPName.CurrentProgram, out state.Program);
            GL.GetInteger(GetPName.VertexArrayBinding, out state.Vao);
            return state;
        }

        private void RestoreGLState(GLState state)
        {
            if (state.BlendEnabled) GL.Enable(EnableCap.Blend);
            else GL.Disable(EnableCap.Blend);

            if (state.DepthTestEnabled) GL.Enable(EnableCap.DepthTest);
            else GL.Disable(EnableCap.DepthTest);

            if (state.CullFaceEnabled) GL.Enable(EnableCap.CullFace);
            else GL.Disable(EnableCap.CullFace);

            GL.BlendFuncSeparate(
                (BlendingFactorSrc)state.BlendSrcRgb,
                (BlendingFactorDest)state.BlendDstRgb,
                (BlendingFactorSrc)state.BlendSrcAlpha,
                (BlendingFactorDest)state.BlendDstAlpha
            );

            GL.UseProgram(state.Program);
            GL.BindVertexArray(state.Vao);
        }

        private void ClearGLErrors()
        {
            int maxErrors = 10;
            for (int i = 0; i < maxErrors; i++)
            {
                if (GL.GetError() == ErrorCode.NoError)
                    break;
            }
        }

        // === Initialization ===

        private void InitializeShader()
        {
            string vertexShaderSource = @"
#version 330 core
layout (location = 0) in vec2 aPos;
layout (location = 1) in vec2 aTexCoord;

uniform mat4 uProjection;
uniform mat4 uModel;

out vec2 TexCoord;

void main()
{
    gl_Position = uProjection * uModel * vec4(aPos, 0.0, 1.0);
    TexCoord = aTexCoord;
}
";

            string fragmentShaderSource = @"
#version 330 core
in vec2 TexCoord;
out vec4 FragColor;

uniform vec4 uColor;
uniform int uUseTexture;
uniform sampler2D uTexture;

void main()
{
    if (uUseTexture == 1)
        FragColor = texture(uTexture, TexCoord) * uColor;
    else
        FragColor = uColor;
}
";

            int vertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vertexShader, vertexShaderSource);
            GL.CompileShader(vertexShader);
            CheckShaderCompilation(vertexShader, "Vertex");

            int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fragmentShader, fragmentShaderSource);
            GL.CompileShader(fragmentShader);
            CheckShaderCompilation(fragmentShader, "Fragment");

            _shader = GL.CreateProgram();
            GL.AttachShader(_shader, vertexShader);
            GL.AttachShader(_shader, fragmentShader);
            GL.LinkProgram(_shader);
            CheckProgramLinking(_shader);

            GL.DeleteShader(vertexShader);
            GL.DeleteShader(fragmentShader);

            // Get uniform locations
            _uProjection = GL.GetUniformLocation(_shader, "uProjection");
            _uModel = GL.GetUniformLocation(_shader, "uModel");
            _uColor = GL.GetUniformLocation(_shader, "uColor");
            _uUseTexture = GL.GetUniformLocation(_shader, "uUseTexture");
        }

        private void InitializeGeometry()
        {
            // Quad vertices (0,0) to (1,1) - will be scaled by model matrix
            float[] vertices = {
                // Pos      // TexCoord
                0f, 0f,     0f, 0f,
                1f, 0f,     1f, 0f,
                1f, 1f,     1f, 1f,
                0f, 1f,     0f, 1f
            };

            uint[] indices = { 0, 1, 2, 2, 3, 0 };

            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();
            _ebo = GL.GenBuffer();

            GL.BindVertexArray(_vao);

            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

            // Position attribute
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            // TexCoord attribute
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));
            GL.EnableVertexAttribArray(1);

            GL.BindVertexArray(0);
        }

        private void SetMatrix4(int location, Matrix4x4 matrix)
        {
            // Convert System.Numerics.Matrix4x4 to float array
            float[] matArray = new float[16];
            matArray[0] = matrix.M11; matArray[1] = matrix.M12; matArray[2] = matrix.M13; matArray[3] = matrix.M14;
            matArray[4] = matrix.M21; matArray[5] = matrix.M22; matArray[6] = matrix.M23; matArray[7] = matrix.M24;
            matArray[8] = matrix.M31; matArray[9] = matrix.M32; matArray[10] = matrix.M33; matArray[11] = matrix.M34;
            matArray[12] = matrix.M41; matArray[13] = matrix.M42; matArray[14] = matrix.M43; matArray[15] = matrix.M44;
            GL.UniformMatrix4(location, 1, false, matArray);
        }

        private void CheckShaderCompilation(int shader, string type)
        {
            GL.GetShader(shader, ShaderParameter.CompileStatus, out int success);
            if (success == 0)
            {
                string infoLog = GL.GetShaderInfoLog(shader);
                Console.WriteLine($"[UIRenderSystem] {type} shader compilation error:\n{infoLog}");
            }
        }

        private void CheckProgramLinking(int program)
        {
            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int success);
            if (success == 0)
            {
                string infoLog = GL.GetProgramInfoLog(program);
                Console.WriteLine($"[UIRenderSystem] Shader program linking error:\n{infoLog}");
            }
        }

        public void Dispose()
        {
            GL.DeleteVertexArray(_vao);
            GL.DeleteBuffer(_vbo);
            GL.DeleteBuffer(_ebo);
            GL.DeleteProgram(_shader);
        }

        private struct FontAtlasData
        {
            public int TextureId { get; set; }
            // TODO: Add glyph data when implementing text rendering
        }
    }
}

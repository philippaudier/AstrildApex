using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace Editor.Rendering
{
    /// <summary>
    /// Dedicated rendering pass for UI elements.
    /// Isolated from main 3D rendering to avoid OpenGL state conflicts.
    /// </summary>
    public class UIRenderPass
    {
        private int _shader;
        private int _locMvp, _locColor, _locTexture, _locUseTexture;
        private int _quadVao, _quadVbo, _quadEbo;

        // Texture cache for UI elements
        private Dictionary<Guid, int> _textureCache = new();

        public void Initialize()
        {
            CreateShader();
            CreateQuadGeometry();
        }

        private void CreateShader()
        {
            string vertexShader = @"
#version 330 core
layout(location = 0) in vec3 aPos;
layout(location = 1) in vec2 aTexCoord;

uniform mat4 u_MVP;
out vec2 TexCoord;

void main()
{
    gl_Position = u_MVP * vec4(aPos, 1.0);
    TexCoord = aTexCoord;
}";

            string fragmentShader = @"
#version 330 core
in vec2 TexCoord;
out vec4 FragColor;

uniform vec4 u_Color;
uniform sampler2D u_Texture;
uniform int u_UseTexture;

void main()
{
    if (u_UseTexture == 1)
        FragColor = texture(u_Texture, TexCoord) * u_Color;
    else
        FragColor = u_Color;
}";

            int vs = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vs, vertexShader);
            GL.CompileShader(vs);
            GL.GetShader(vs, ShaderParameter.CompileStatus, out int vsStatus);
            if (vsStatus == 0)
                throw new Exception($"UI Vertex Shader: {GL.GetShaderInfoLog(vs)}");

            int fs = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fs, fragmentShader);
            GL.CompileShader(fs);
            GL.GetShader(fs, ShaderParameter.CompileStatus, out int fsStatus);
            if (fsStatus == 0)
                throw new Exception($"UI Fragment Shader: {GL.GetShaderInfoLog(fs)}");

            _shader = GL.CreateProgram();
            GL.AttachShader(_shader, vs);
            GL.AttachShader(_shader, fs);
            GL.LinkProgram(_shader);
            GL.GetProgram(_shader, GetProgramParameterName.LinkStatus, out int linkStatus);
            if (linkStatus == 0)
                throw new Exception($"UI Shader Link: {GL.GetProgramInfoLog(_shader)}");

            GL.DeleteShader(vs);
            GL.DeleteShader(fs);

            _locMvp = GL.GetUniformLocation(_shader, "u_MVP");
            _locColor = GL.GetUniformLocation(_shader, "u_Color");
            _locTexture = GL.GetUniformLocation(_shader, "u_Texture");
            _locUseTexture = GL.GetUniformLocation(_shader, "u_UseTexture");
        }

        private void CreateQuadGeometry()
        {
            // Quad vertices with texture coordinates
            float[] vertices = new[]
            {
                // pos(x,y,z)        tex(u,v)
                -0.5f,  0.5f, 0.0f,  0.0f, 0.0f,  // Top-left
                 0.5f,  0.5f, 0.0f,  1.0f, 0.0f,  // Top-right
                 0.5f, -0.5f, 0.0f,  1.0f, 1.0f,  // Bottom-right
                -0.5f, -0.5f, 0.0f,  0.0f, 1.0f   // Bottom-left
            };

            uint[] indices = new uint[] { 0, 1, 2, 2, 3, 0 };

            _quadVao = GL.GenVertexArray();
            _quadVbo = GL.GenBuffer();
            _quadEbo = GL.GenBuffer();

            GL.BindVertexArray(_quadVao);

            GL.BindBuffer(BufferTarget.ArrayBuffer, _quadVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _quadEbo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

            // Position
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 5 * sizeof(float), 0);

            // TexCoords
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 5 * sizeof(float), 3 * sizeof(float));

            GL.BindVertexArray(0);
        }

        /// <summary>
        /// Render all UI canvases in the scene
        /// </summary>
        public void Render(Engine.Scene.Scene scene, int viewportWidth, int viewportHeight, Matrix4 view, Matrix4 projection)
        {
            if (scene == null) return;

            // Find all canvas entities
            var canvasEntities = scene.Entities
                .Where(e => e.HasComponent<Engine.Components.UI.CanvasComponent>())
                .OrderBy(e => e.GetComponent<Engine.Components.UI.CanvasComponent>()?.SortOrder ?? 0)
                .ToList();

            if (canvasEntities.Count == 0) return;

            // Save current OpenGL state
            SaveGLState(out var state);

            // Setup UI rendering state
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.Disable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);

            GL.UseProgram(_shader);
            GL.BindVertexArray(_quadVao);

            foreach (var canvasEntity in canvasEntities)
            {
                var canvas = canvasEntity.GetComponent<Engine.Components.UI.CanvasComponent>();
                if (canvas == null) continue;

                RenderCanvas(canvasEntity, canvas, scene, viewportWidth, viewportHeight, view, projection);
            }

            // Restore OpenGL state
            RestoreGLState(state);

            // Clear any accumulated GL errors to prevent them from affecting other systems
            // GL errors are "sticky" and persist until explicitly cleared
            ErrorCode error;
            int maxErrors = 10; // Safety limit to prevent infinite loop
            int errorCount = 0;
            while ((error = GL.GetError()) != ErrorCode.NoError && errorCount < maxErrors)
            {
                if (errorCount == 0)
                {
                    Editor.Logging.LogManager.LogWarning($"[UIRenderPass] GL error detected: {error}", "Renderer");
                }
                errorCount++;
            }
        }

        private void RenderCanvas(Engine.Scene.Entity canvasEntity, Engine.Components.UI.CanvasComponent canvas,
            Engine.Scene.Scene scene, int viewportWidth, int viewportHeight, Matrix4 view, Matrix4 projection)
        {
            // Find all UI elements that are children of this canvas
            var uiElements = scene.Entities
                .Where(e => e.Parent?.Id == canvasEntity.Id &&
                       (e.HasComponent<Engine.Components.UI.UIImageComponent>() ||
                        e.HasComponent<Engine.Components.UI.UITextComponent>() ||
                        e.HasComponent<Engine.Components.UI.UIButtonComponent>()))
                .ToList();

            // Early return if no UI elements to render
            if (uiElements.Count == 0) return;

            // Get canvas transform for WorldSpace mode
            canvasEntity.GetWorldTRS(out var worldPos, out var worldRot, out var worldScale);

            // Calculate canvas dimensions
            System.Numerics.Vector2 canvasSize = new System.Numerics.Vector2(canvas.Width > 0 ? canvas.Width : 1920, canvas.Height > 0 ? canvas.Height : 1080);

            foreach (var elementEntity in uiElements)
            {
                RenderUIElement(elementEntity, canvas, canvasSize, worldPos, worldRot, worldScale,
                    viewportWidth, viewportHeight, view, projection);
            }
        }

        private void RenderUIElement(Engine.Scene.Entity entity, Engine.Components.UI.CanvasComponent canvas,
            System.Numerics.Vector2 canvasSize, OpenTK.Mathematics.Vector3 canvasWorldPos, OpenTK.Mathematics.Quaternion canvasWorldRot, OpenTK.Mathematics.Vector3 canvasWorldScale,
            int viewportWidth, int viewportHeight, Matrix4 view, Matrix4 projection)
        {
            // Try to get any UI component
            var imageComp = entity.GetComponent<Engine.Components.UI.UIImageComponent>();
            var textComp = entity.GetComponent<Engine.Components.UI.UITextComponent>();
            var buttonComp = entity.GetComponent<Engine.Components.UI.UIButtonComponent>();

            if (imageComp == null && textComp == null && buttonComp == null) return;

            // Get the rect transform (all UI components have it)
            var rectTransform = imageComp?.RectTransform ?? textComp?.RectTransform ?? buttonComp?.RectTransform;
            if (rectTransform == null) return;

            // Calculate screen rectangle from RectTransform
            var rect = CalculateRect(rectTransform, canvasSize);

            // Get color
            System.Numerics.Vector4 color = GetElementColor(imageComp, textComp, buttonComp);

            // Get texture (if image component)
            int textureId = 0;
            bool useTexture = false;
            if (imageComp?.TextureGuid != null)
            {
                textureId = GetOrLoadTexture(imageComp.TextureGuid.Value);
                useTexture = textureId > 0;
            }

            // Calculate transformation matrix
            Matrix4 model = CalculateTransformMatrix(rect, canvas.RenderMode, canvasWorldPos, canvasWorldRot,
                canvasWorldScale, canvasSize, viewportWidth, viewportHeight);

            // Set uniforms and draw
            var mvp = model * (canvas.RenderMode == Engine.UI.RenderMode.WorldSpace ? view * projection : Matrix4.Identity);
            GL.UniformMatrix4(_locMvp, false, ref mvp);
            GL.Uniform4(_locColor, color.X, color.Y, color.Z, color.W);
            GL.Uniform1(_locUseTexture, useTexture ? 1 : 0);

            if (useTexture)
            {
                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.Texture2D, textureId);
                GL.Uniform1(_locTexture, 0);
            }

            GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0);
        }

        private RectF CalculateRect(Engine.UI.RectTransform rectTransform, System.Numerics.Vector2 canvasSize)
        {
            // Calculate anchored position
            var anchorMin = rectTransform.AnchorMin;
            var anchorMax = rectTransform.AnchorMax;
            var pivot = rectTransform.Pivot;
            var anchoredPos = rectTransform.AnchoredPosition;
            var sizeDelta = rectTransform.SizeDelta;

            // Anchor points in pixels
            float anchorMinX = anchorMin.X * canvasSize.X;
            float anchorMinY = anchorMin.Y * canvasSize.Y;
            float anchorMaxX = anchorMax.X * canvasSize.X;
            float anchorMaxY = anchorMax.Y * canvasSize.Y;

            // Element size
            float width = (anchorMaxX - anchorMinX) + sizeDelta.X;
            float height = (anchorMaxY - anchorMinY) + sizeDelta.Y;

            // Element center position
            float centerX = (anchorMinX + anchorMaxX) * 0.5f + anchoredPos.X;
            float centerY = (anchorMinY + anchorMaxY) * 0.5f + anchoredPos.Y;

            // Apply pivot
            float left = centerX - width * pivot.X;
            float top = centerY - height * pivot.Y;

            return new RectF { Left = left, Top = top, Width = width, Height = height };
        }

        private Matrix4 CalculateTransformMatrix(RectF rect, Engine.UI.RenderMode renderMode,
            OpenTK.Mathematics.Vector3 canvasWorldPos, OpenTK.Mathematics.Quaternion canvasWorldRot, OpenTK.Mathematics.Vector3 canvasWorldScale,
            System.Numerics.Vector2 canvasSize, int viewportWidth, int viewportHeight)
        {
            if (renderMode == Engine.UI.RenderMode.WorldSpace)
            {
                // WorldSpace: render in 3D space
                float scale = 0.1f; // Same as Canvas 3D scale
                float x = (rect.Left + rect.Width * 0.5f - canvasSize.X * 0.5f) * scale;
                float y = -(rect.Top + rect.Height * 0.5f - canvasSize.Y * 0.5f) * scale;

                var localTranslation = Matrix4.CreateTranslation(x, y, 0.01f);
                var localScale = Matrix4.CreateScale(rect.Width * scale, rect.Height * scale, 1.0f);
                var localModel = localScale * localTranslation;

                // Apply canvas world transform
                var canvasRotMatrix = Matrix4.CreateFromQuaternion(canvasWorldRot);
                var canvasTransMatrix = Matrix4.CreateTranslation(canvasWorldPos);
                var canvasScaleMatrix = Matrix4.CreateScale(canvasWorldScale);

                return localModel * canvasScaleMatrix * canvasRotMatrix * canvasTransMatrix;
            }
            else
            {
                // ScreenSpace: orthographic 2D
                float x = (rect.Left + rect.Width * 0.5f) / viewportWidth * 2.0f - 1.0f;
                float y = 1.0f - (rect.Top + rect.Height * 0.5f) / viewportHeight * 2.0f;

                float scaleX = rect.Width / viewportWidth;
                float scaleY = rect.Height / viewportHeight;

                var translation = Matrix4.CreateTranslation(x, y, 0.0f);
                var scale = Matrix4.CreateScale(scaleX, scaleY, 1.0f);

                return scale * translation;
            }
        }

        private System.Numerics.Vector4 GetElementColor(Engine.Components.UI.UIImageComponent? img,
            Engine.Components.UI.UITextComponent? text, Engine.Components.UI.UIButtonComponent? button)
        {
            uint colorInt = img?.Color ?? text?.Color ?? 0xFFFFFFFF;

            float r = ((colorInt >> 16) & 0xFF) / 255.0f;
            float g = ((colorInt >> 8) & 0xFF) / 255.0f;
            float b = (colorInt & 0xFF) / 255.0f;
            float a = ((colorInt >> 24) & 0xFF) / 255.0f;

            return new System.Numerics.Vector4(r, g, b, a);
        }

        private int GetOrLoadTexture(Guid textureGuid)
        {
            if (_textureCache.TryGetValue(textureGuid, out int cachedId))
                return cachedId;

            // Use TextureCache to load texture
            try
            {
                int textureHandle = Engine.Rendering.TextureCache.GetOrLoad(textureGuid, guid =>
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

        private struct GLState
        {
            public bool BlendEnabled;
            public bool DepthTestEnabled;
            public bool CullFaceEnabled;
            public bool ScissorTestEnabled;
            public int Program;
            public int Vao;
            public int BlendSrcRgb;
            public int BlendDstRgb;
            public int BlendSrcAlpha;
            public int BlendDstAlpha;
            public int ArrayBuffer;
            public int ElementArrayBuffer;
            public int Texture2D;
            public int ActiveTexture;
        }

        private void SaveGLState(out GLState state)
        {
            state = new GLState
            {
                BlendEnabled = GL.IsEnabled(EnableCap.Blend),
                DepthTestEnabled = GL.IsEnabled(EnableCap.DepthTest),
                CullFaceEnabled = GL.IsEnabled(EnableCap.CullFace),
                ScissorTestEnabled = GL.IsEnabled(EnableCap.ScissorTest)
            };
            GL.GetInteger(GetPName.CurrentProgram, out state.Program);
            GL.GetInteger(GetPName.VertexArrayBinding, out state.Vao);
            GL.GetInteger(GetPName.BlendSrcRgb, out state.BlendSrcRgb);
            GL.GetInteger(GetPName.BlendDstRgb, out state.BlendDstRgb);
            GL.GetInteger(GetPName.BlendSrcAlpha, out state.BlendSrcAlpha);
            GL.GetInteger(GetPName.BlendDstAlpha, out state.BlendDstAlpha);
            GL.GetInteger(GetPName.ArrayBufferBinding, out state.ArrayBuffer);
            GL.GetInteger(GetPName.ElementArrayBufferBinding, out state.ElementArrayBuffer);
            GL.GetInteger(GetPName.TextureBinding2D, out state.Texture2D);
            GL.GetInteger(GetPName.ActiveTexture, out state.ActiveTexture);
        }

        private void RestoreGLState(GLState state)
        {
            // Restore enable states
            if (state.BlendEnabled) GL.Enable(EnableCap.Blend); else GL.Disable(EnableCap.Blend);
            if (state.DepthTestEnabled) GL.Enable(EnableCap.DepthTest); else GL.Disable(EnableCap.DepthTest);
            if (state.CullFaceEnabled) GL.Enable(EnableCap.CullFace); else GL.Disable(EnableCap.CullFace);
            if (state.ScissorTestEnabled) GL.Enable(EnableCap.ScissorTest); else GL.Disable(EnableCap.ScissorTest);

            // Restore blend function
            GL.BlendFuncSeparate(
                (BlendingFactorSrc)state.BlendSrcRgb,
                (BlendingFactorDest)state.BlendDstRgb,
                (BlendingFactorSrc)state.BlendSrcAlpha,
                (BlendingFactorDest)state.BlendDstAlpha);

            // Restore bindings
            GL.UseProgram(state.Program);
            GL.BindVertexArray(state.Vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, state.ArrayBuffer);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, state.ElementArrayBuffer);

            // Restore texture state
            GL.ActiveTexture((TextureUnit)state.ActiveTexture);
            GL.BindTexture(TextureTarget.Texture2D, state.Texture2D);
        }

        public void Dispose()
        {
            if (_shader != 0) GL.DeleteProgram(_shader);
            if (_quadVao != 0) GL.DeleteVertexArray(_quadVao);
            if (_quadVbo != 0) GL.DeleteBuffer(_quadVbo);
            if (_quadEbo != 0) GL.DeleteBuffer(_quadEbo);
        }

        private struct RectF
        {
            public float Left, Top, Width, Height;
        }
    }
}

using System;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Editor.ImGuiBackend;

public sealed class ImGuiController : IDisposable
{
    private bool _frameBegun;

    // GL objects
    private int _fontTexture;
    private int _shader;
    private int _locTex;
    private int _locProj;
    private int _attrPos, _attrUV, _attrColor;
    private int _vbo, _ibo, _vao;
    private int _vboSize = 100000;
    private int _iboSize = 200000;

    private readonly GameWindow _window;

    public ImGuiController(GameWindow window)
    {
        _window = window;

        // Contexte ImGui
        ImGui.CreateContext();
        var io = ImGui.GetIO();
        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
        io.ConfigFlags  |= ImGuiConfigFlags.DockingEnable | ImGuiConfigFlags.NavEnableKeyboard;
        ImGui.StyleColorsDark();
        var style = ImGui.GetStyle();
        style.WindowRounding = 6; style.FrameRounding = 6;

        // Load custom fonts from EditorSettings
        LoadCustomFonts();

        // CRUCIAL: Recréer l'atlas de texture après ajout des fonts
        CreateFontTexture();

        // Shaders + buffers
        _shader = CreateShaderProgram();
        _locTex = GL.GetUniformLocation(_shader, "Texture");
        _locProj = GL.GetUniformLocation(_shader, "ProjMtx");

        _attrPos   = GL.GetAttribLocation(_shader, "Position");
        _attrUV    = GL.GetAttribLocation(_shader, "UV");
        _attrColor = GL.GetAttribLocation(_shader, "Color");

        _vao = GL.GenVertexArray();
        GL.BindVertexArray(_vao);

        _vbo = GL.GenBuffer();
        _ibo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, _vboSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ibo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, _iboSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);

        // Layout des verts (taille/offsets sans Unsafe)
        int stride = Marshal.SizeOf<ImDrawVert>();
        GL.EnableVertexAttribArray(_attrPos);
        GL.EnableVertexAttribArray(_attrUV);
        GL.EnableVertexAttribArray(_attrColor);

        GL.VertexAttribPointer(_attrPos,   2, VertexAttribPointerType.Float,         false, stride, 0);
        GL.VertexAttribPointer(_attrUV,    2, VertexAttribPointerType.Float,         false, stride, 8);
        GL.VertexAttribPointer(_attrColor, 4, VertexAttribPointerType.UnsignedByte,  true,  stride, 16);


        // Inputs / events
        _window.TextInput  += OnTextInput;
        _window.MouseWheel += OnMouseWheel;
        _window.Resize     += OnResize;

        UpdateDisplaySize();
    }

    public void NewFrame(float deltaSeconds)
    {
        if (_frameBegun) ImGui.Render();

        var io = ImGui.GetIO();
        io.DeltaTime = MathF.Max(deltaSeconds, 1f/1000f);
        UpdateInput(); SubmitKeyboardEvents(); ImGui.NewFrame();
        _frameBegun = true;
    }

    public void Render()
    {
        if (!_frameBegun) return;
        _frameBegun = false;
        ImGui.Render();
        RenderDrawData(ImGui.GetDrawData());
    }

    private void RenderDrawData(ImDrawDataPtr dd)
    {
        int fbWidth = (int)(dd.DisplaySize.X * dd.FramebufferScale.X);
        int fbHeight = (int)(dd.DisplaySize.Y * dd.FramebufferScale.Y);
        if (fbWidth <= 0 || fbHeight <= 0) return;

        GL.Enable(EnableCap.Blend);
        GL.BlendEquation(BlendEquationMode.FuncAdd);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.Disable(EnableCap.CullFace);
        GL.Disable(EnableCap.DepthTest);
        GL.Enable(EnableCap.ScissorTest);

        GL.ActiveTexture(TextureUnit.Texture0);
        GL.UseProgram(_shader);
        GL.Uniform1(_locTex, 0);

        // --- Projection ORTHO qui tient compte de DisplayPos ---
        float L = dd.DisplayPos.X;
        float R = dd.DisplayPos.X + dd.DisplaySize.X;
        float T = dd.DisplayPos.Y;
        float B = dd.DisplayPos.Y + dd.DisplaySize.Y;

        // Mat4 comme dans l'exemple OpenGL3 d'ImGui
        var mvp = new OpenTK.Mathematics.Matrix4(
            2f / (R - L), 0, 0, 0,
            0, 2f / (T - B), 0, 0,
            0, 0, -1f, 0,
            (R + L) / (L - R), (T + B) / (B - T), 0, 1f
        );
        GL.UniformMatrix4(_locProj, false, ref mvp);

        GL.BindVertexArray(_vao);

        // NOTE: ne pas appeler dd.ScaleClipRects ici ; on applique nous-mêmes offset + scale
        int vertSize = System.Runtime.InteropServices.Marshal.SizeOf<ImDrawVert>();

        // Resize buffers si besoin
        int needV = dd.TotalVtxCount * vertSize;
        int needI = dd.TotalIdxCount * sizeof(ushort);
        if (needV > _vboSize) { while (_vboSize < needV) _vboSize *= 2; GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo); GL.BufferData(BufferTarget.ArrayBuffer, _vboSize, IntPtr.Zero, BufferUsageHint.DynamicDraw); }
        if (needI > _iboSize) { while (_iboSize < needI) _iboSize *= 2; GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ibo); GL.BufferData(BufferTarget.ElementArrayBuffer, _iboSize, IntPtr.Zero, BufferUsageHint.DynamicDraw); }

        // Upload des listes
        int vtxOffset = 0, idxOffset = 0;
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ibo);
        unsafe
        {
            for (int n = 0; n < dd.CmdListsCount; n++)
            {
                var cl = dd.CmdLists[n];
                GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)(vtxOffset * vertSize), cl.VtxBuffer.Size * vertSize, (IntPtr)cl.VtxBuffer.Data);
                GL.BufferSubData(BufferTarget.ElementArrayBuffer, (IntPtr)(idxOffset * sizeof(ushort)), cl.IdxBuffer.Size * sizeof(ushort), (IntPtr)cl.IdxBuffer.Data);
                vtxOffset += cl.VtxBuffer.Size;
                idxOffset += cl.IdxBuffer.Size;
            }
        }

        // Dessin + scissor (offset DisplayPos + scale, Y inversé)
        int vtxBase = 0, idxBase = 0;
        for (int n = 0; n < dd.CmdListsCount; n++)
        {
            var cl = dd.CmdLists[n];
            for (int i = 0; i < cl.CmdBuffer.Size; i++)
            {
                var pcmd = cl.CmdBuffer[i];

                GL.BindTexture(TextureTarget.Texture2D, (int)pcmd.TextureId);

                var cr = pcmd.ClipRect;
                int clipMinX = (int)((cr.X - dd.DisplayPos.X) * dd.FramebufferScale.X);
                int clipMinY = (int)((cr.Y - dd.DisplayPos.Y) * dd.FramebufferScale.Y);
                int clipMaxX = (int)((cr.Z - dd.DisplayPos.X) * dd.FramebufferScale.X);
                int clipMaxY = (int)((cr.W - dd.DisplayPos.Y) * dd.FramebufferScale.Y);

                GL.Scissor(clipMinX, fbHeight - clipMaxY, clipMaxX - clipMinX, clipMaxY - clipMinY);
                GL.DrawElementsBaseVertex(PrimitiveType.Triangles, (int)pcmd.ElemCount,
                                          DrawElementsType.UnsignedShort, (IntPtr)(idxBase * sizeof(ushort)), vtxBase);

                idxBase += (int)pcmd.ElemCount;
            }
            vtxBase += cl.VtxBuffer.Size;
        }

        GL.Disable(EnableCap.ScissorTest);
        GL.Enable(EnableCap.DepthTest);
        GL.Disable(EnableCap.Blend);
    }

    public void Dispose()
    {
        GL.DeleteBuffer(_vbo);
        GL.DeleteBuffer(_ibo);
        GL.DeleteVertexArray(_vao);
        GL.DeleteTexture(_fontTexture);
        GL.DeleteProgram(_shader);
    }

    // 
    // INPUTS (nouvelle API ImGui.NET: AddKeyEvent / Modifiers)
    // 
    private void UpdateInput()
    {
        var io = ImGui.GetIO();
        var ms = _window.MouseState;

        // Souris uniquement
        io.MouseDown[0] = ms.IsButtonDown(MouseButton.Left);
        io.MouseDown[1] = ms.IsButtonDown(MouseButton.Right);
        io.MouseDown[2] = ms.IsButtonDown(MouseButton.Middle);
        io.MousePos = new System.Numerics.Vector2(ms.X, ms.Y);
    }

    private static void AddKey(KeyboardState kb, Keys k, ImGuiKey ik)
        => ImGui.GetIO().AddKeyEvent(ik, kb.IsKeyDown(k));

    private void OnTextInput(TextInputEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.AsString))
            ImGui.GetIO().AddInputCharactersUTF8(e.AsString);
    }

    private void OnMouseWheel(MouseWheelEventArgs e)
    {
        var io = ImGui.GetIO();
        io.AddMouseWheelEvent(e.OffsetX, e.OffsetY);
    }

    private void OnResize(ResizeEventArgs e) => UpdateDisplaySize();

    private void UpdateDisplaySize()
    {
        var io = ImGui.GetIO();
        io.DisplaySize = new System.Numerics.Vector2(_window.ClientSize.X, _window.ClientSize.Y);
        io.DisplayFramebufferScale = new System.Numerics.Vector2(1, 1); // pas de RenderScale dans OpenTK
    }

    private unsafe void LoadCustomFonts()
    {
        try
        {
            var io = ImGui.GetIO();

            // Load font settings from EditorSettings
            string fontName = Editor.State.EditorSettings.InterfaceFont;
            string fontPath = Editor.State.EditorSettings.InterfaceFontPath;
            float fontSize = Editor.State.EditorSettings.InterfaceFontSize;

            Console.WriteLine($"[ImGuiController] Loading font: {fontName} @ {fontSize}px");

            // If no font path is specified or it's the default font, use ImGui's default
            if (string.IsNullOrEmpty(fontPath) || fontName == "Default (Proggy Clean)")
            {
                Console.WriteLine("[ImGuiController] Using default ImGui font (Proggy Clean)");
                return;
            }

            // Check if font file exists
            if (File.Exists(fontPath))
            {
                // Load the font with specified size
                io.Fonts.AddFontFromFileTTF(fontPath, fontSize);
                Console.WriteLine($"[ImGuiController] Successfully loaded font from: {fontPath}");
            }
            else
            {
                Console.WriteLine($"[ImGuiController] Font file not found: {fontPath}, using default");
                Console.WriteLine($"[ImGuiController] Font was: {fontName}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ImGuiController] Failed to load custom font: {ex.Message}");
        }
    }

    private unsafe void CreateFontTexture()
    {
        var io = ImGui.GetIO();
        io.Fonts.GetTexDataAsRGBA32(out byte* pixels, out int w, out int h, out _);

        _fontTexture = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, _fontTexture);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, w, h, 0, PixelFormat.Rgba, PixelType.UnsignedByte, (IntPtr)pixels);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

        io.Fonts.SetTexID((IntPtr)_fontTexture);
        io.Fonts.ClearTexData();
    }

    private static int CreateShaderProgram()
    {
        const string vs = @"#version 330 core
layout (location = 0) in vec2 Position;
layout (location = 1) in vec2 UV;
layout (location = 2) in vec4 Color;
uniform mat4 ProjMtx;
out vec2 Frag_UV;
out vec4 Frag_Color;
void main() {
    Frag_UV = UV;
    Frag_Color = Color;
    gl_Position = ProjMtx * vec4(Position, 0, 1);
}";
        const string fs = @"#version 330 core
in vec2 Frag_UV;
in vec4 Frag_Color;
uniform sampler2D Texture;
out vec4 Out_Color;
void main() {
    Out_Color = Frag_Color * texture(Texture, Frag_UV);
}";
        int vert = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(vert, vs);
        GL.CompileShader(vert);
        GL.GetShader(vert, ShaderParameter.CompileStatus, out int vOk);
        if (vOk == 0) throw new Exception(GL.GetShaderInfoLog(vert));

        int frag = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(frag, fs);
        GL.CompileShader(frag);
        GL.GetShader(frag, ShaderParameter.CompileStatus, out int fOk);
        if (fOk == 0) throw new Exception(GL.GetShaderInfoLog(frag));

        int prog = GL.CreateProgram();
        GL.AttachShader(prog, vert);
        GL.AttachShader(prog, frag);
        GL.LinkProgram(prog);
        GL.GetProgram(prog, GetProgramParameterName.LinkStatus, out int pOk);
        if (pOk == 0) throw new Exception(GL.GetProgramInfoLog(prog));
        GL.DetachShader(prog, vert); GL.DetachShader(prog, frag);
        GL.DeleteShader(vert); GL.DeleteShader(frag);
        return prog;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ImDrawVert
    {
        public System.Numerics.Vector2 Position; // 0..7
        public System.Numerics.Vector2 UV;       // 8..15
        public uint Color;                       // 16..19
    }
    // --- Backend clavier (ImGui 1.89+) : pousse l'état de touches chaque frame ---
    private static readonly (ImGuiKey key, Keys native)[] _keyMap = new (ImGuiKey, Keys)[] {
        // Navigation
        (ImGuiKey.Tab, Keys.Tab), (ImGuiKey.LeftArrow, Keys.Left), (ImGuiKey.RightArrow, Keys.Right),
        (ImGuiKey.UpArrow, Keys.Up), (ImGuiKey.DownArrow, Keys.Down), (ImGuiKey.PageUp, Keys.PageUp),
        (ImGuiKey.PageDown, Keys.PageDown), (ImGuiKey.Home, Keys.Home), (ImGuiKey.End, Keys.End),
        (ImGuiKey.Delete, Keys.Delete), (ImGuiKey.Backspace, Keys.Backspace), (ImGuiKey.Enter, Keys.Enter),
        (ImGuiKey.Escape, Keys.Escape),
        
        // Lettres A-Z
        (ImGuiKey.A, Keys.A), (ImGuiKey.B, Keys.B), (ImGuiKey.C, Keys.C), (ImGuiKey.D, Keys.D),
        (ImGuiKey.E, Keys.E), (ImGuiKey.F, Keys.F), (ImGuiKey.G, Keys.G), (ImGuiKey.H, Keys.H),
        (ImGuiKey.I, Keys.I), (ImGuiKey.J, Keys.J), (ImGuiKey.K, Keys.K), (ImGuiKey.L, Keys.L),
        (ImGuiKey.M, Keys.M), (ImGuiKey.N, Keys.N), (ImGuiKey.O, Keys.O), (ImGuiKey.P, Keys.P),
        (ImGuiKey.Q, Keys.Q), (ImGuiKey.R, Keys.R), (ImGuiKey.S, Keys.S), (ImGuiKey.T, Keys.T),
        (ImGuiKey.U, Keys.U), (ImGuiKey.V, Keys.V), (ImGuiKey.W, Keys.W), (ImGuiKey.X, Keys.X),
        (ImGuiKey.Y, Keys.Y), (ImGuiKey.Z, Keys.Z),
        
        // Touches de fonction
        (ImGuiKey.F1, Keys.F1), (ImGuiKey.F2, Keys.F2), (ImGuiKey.F3, Keys.F3), (ImGuiKey.F4, Keys.F4),
        (ImGuiKey.F5, Keys.F5), (ImGuiKey.F6, Keys.F6), (ImGuiKey.F7, Keys.F7), (ImGuiKey.F8, Keys.F8),
        (ImGuiKey.F9, Keys.F9), (ImGuiKey.F10, Keys.F10), (ImGuiKey.F11, Keys.F11), (ImGuiKey.F12, Keys.F12),
        
        // Chiffres
        (ImGuiKey._0, Keys.D0), (ImGuiKey._1, Keys.D1), (ImGuiKey._2, Keys.D2), (ImGuiKey._3, Keys.D3),
        (ImGuiKey._4, Keys.D4), (ImGuiKey._5, Keys.D5), (ImGuiKey._6, Keys.D6), (ImGuiKey._7, Keys.D7),
        (ImGuiKey._8, Keys.D8), (ImGuiKey._9, Keys.D9)
    };

    private void SubmitKeyboardEvents()
    {
        var io = ImGui.GetIO();
        var kb = _window.KeyboardState;

        io.AddKeyEvent(ImGuiKey.ModCtrl,  kb.IsKeyDown(Keys.LeftControl)  || kb.IsKeyDown(Keys.RightControl));
        io.AddKeyEvent(ImGuiKey.ModShift, kb.IsKeyDown(Keys.LeftShift)    || kb.IsKeyDown(Keys.RightShift));
        io.AddKeyEvent(ImGuiKey.ModAlt,   kb.IsKeyDown(Keys.LeftAlt)      || kb.IsKeyDown(Keys.RightAlt));
        io.AddKeyEvent(ImGuiKey.ModSuper, kb.IsKeyDown(Keys.LeftSuper)    || kb.IsKeyDown(Keys.RightSuper));

        foreach (var m in _keyMap)
            io.AddKeyEvent(m.key, kb.IsKeyDown(m.native));
    }
}



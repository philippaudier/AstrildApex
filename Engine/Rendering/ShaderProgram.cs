using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace Engine.Rendering
{
    /// <summary>
    /// Wrapper minimal pour charger/lier un programme GL et setter des uniforms.
    /// </summary>
    public sealed class ShaderProgram : IDisposable
    {
        public int Handle { get; private set; }
        // Set to true when any created program had tessellation stages attached successfully
        public static bool TessellationAvailable { get; private set; } = false;
        public bool UsesTessellation { get; private set; } = false;
        private readonly Dictionary<string, int> _uniforms = new();

        private ShaderProgram(int handle) { Handle = handle; }

        /// <summary>
        /// Create a shader program from vertex/fragment files and optional tessellation control/eval files.
        /// If tessellation shader compilation fails, the program will fall back to a VS/FS-only program.
        /// </summary>
        public static ShaderProgram FromFiles(string vertPath, string fragPath, string? tcsPath = null, string? tesPath = null)
        {
            string vs = ShaderPreprocessor.ProcessShaderCached(vertPath);
            string fs = ShaderPreprocessor.ProcessShaderCached(fragPath);

            string? tcs = null;
            string? tes = null;
            if (!string.IsNullOrEmpty(tcsPath)) tcs = ShaderPreprocessor.ProcessShaderCached(tcsPath);
            if (!string.IsNullOrEmpty(tesPath)) tes = ShaderPreprocessor.ProcessShaderCached(tesPath);

            return FromSource(vs, fs, tcs, tes);
        }

        /// <summary>
        /// Create a shader program from sources. Optional tessellation control and evaluation shader sources may be provided.
        /// If TCS/TES compilation fails they are ignored and a VS/FS program is returned.
        /// </summary>
        public static ShaderProgram FromSource(string vertSrc, string fragSrc, string? tcsSrc = null, string? tesSrc = null)
        {
            int v = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(v, vertSrc);
            GL.CompileShader(v);
            GL.GetShader(v, ShaderParameter.CompileStatus, out int okv);
            if (okv == 0)
            {
                var log = GL.GetShaderInfoLog(v);
                throw new Exception("VS: " + log);
            }

            int f = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(f, fragSrc);
            GL.CompileShader(f);
            GL.GetShader(f, ShaderParameter.CompileStatus, out int okf);
            if (okf == 0)
            {
                var log = GL.GetShaderInfoLog(f);
                try
                {
                    // Write the exact fragment source to a temp file to aid debugging of driver compile errors
                    var tmp = Path.Combine(Path.GetTempPath(), "astrild_failed_frag_" + Guid.NewGuid().ToString("N") + ".frag");
                    File.WriteAllText(tmp, fragSrc);
                        var tempPath = Path.Combine(Path.GetTempPath(), $"astrild_failed_frag_{Guid.NewGuid():N}.frag");
                        File.WriteAllText(tempPath, fragSrc);
                        try
                        {
                            var logsDir = Path.Combine(AppContext.BaseDirectory, "Engine", "Logs");
                            Directory.CreateDirectory(logsDir);
                            var localPath = Path.Combine(logsDir, $"failed_frag_{Guid.NewGuid():N}.frag");
                            File.WriteAllText(localPath, fragSrc);
                            throw new InvalidOperationException($"Failed to compile fragment shader. Log:\n{log}\nFragment source dumped to: {tempPath} and {localPath}");
                        }
                        catch
                        {
                            throw new InvalidOperationException($"Failed to compile fragment shader. Log:\n{log}\nFragment source dumped to: {tempPath}");
                        }
                }
                catch (Exception)
                {
                    throw new Exception("FS: " + log);
                }
            }

            int? tcs = null;
            int? tes = null;
            bool tessAttached = false;

            // Try to compile and attach tessellation stages if provided. Failures are non-fatal.
            if (!string.IsNullOrEmpty(tcsSrc))
            {
                try
                {
                    int t = GL.CreateShader(ShaderType.TessControlShader);
                    GL.ShaderSource(t, tcsSrc);
                    GL.CompileShader(t);
                    GL.GetShader(t, ShaderParameter.CompileStatus, out int okt);
                    if (okt == 0)
                    {
                        var log = GL.GetShaderInfoLog(t);
                        Console.WriteLine("TCS compile failed: " + log);
                        GL.DeleteShader(t);
                    }
                    else
                    {
                        tcs = t;
                        tessAttached = true;
                    }
                }
                catch (Exception ex) { Console.WriteLine("TCS compile error: " + ex.Message); }
            }

            if (!string.IsNullOrEmpty(tesSrc))
            {
                try
                {
                    int te = GL.CreateShader(ShaderType.TessEvaluationShader);
                    GL.ShaderSource(te, tesSrc);
                    GL.CompileShader(te);
                    GL.GetShader(te, ShaderParameter.CompileStatus, out int okte);
                    if (okte == 0)
                    {
                        var log = GL.GetShaderInfoLog(te);
                        Console.WriteLine("TES compile failed: " + log);
                        GL.DeleteShader(te);
                    }
                    else
                    {
                        tes = te;
                        tessAttached = true;
                    }
                }
                catch (Exception ex) { Console.WriteLine("TES compile error: " + ex.Message); }
            }

            int p = GL.CreateProgram();
            GL.AttachShader(p, v);
            GL.AttachShader(p, f);
            if (tcs.HasValue) GL.AttachShader(p, tcs.Value);
            if (tes.HasValue) GL.AttachShader(p, tes.Value);
            GL.LinkProgram(p);
            GL.GetProgram(p, GetProgramParameterName.LinkStatus, out int okp);
            if (okp == 0)
            {
                var log = GL.GetProgramInfoLog(p);
                // Clean up shaders
                if (tcs.HasValue) { GL.DetachShader(p, tcs.Value); GL.DeleteShader(tcs.Value); }
                if (tes.HasValue) { GL.DetachShader(p, tes.Value); GL.DeleteShader(tes.Value); }
                GL.DetachShader(p, v); GL.DetachShader(p, f);
                GL.DeleteShader(v); GL.DeleteShader(f);
                throw new Exception("LINK: " + log);
            }

            // Detach and delete shaders we created
            if (tcs.HasValue) { GL.DetachShader(p, tcs.Value); GL.DeleteShader(tcs.Value); }
            if (tes.HasValue) { GL.DetachShader(p, tes.Value); GL.DeleteShader(tes.Value); }
            GL.DetachShader(p, v); GL.DetachShader(p, f);
            GL.DeleteShader(v); GL.DeleteShader(f);

            var program = new ShaderProgram(p);
            program.UsesTessellation = tessAttached;
            if (tessAttached)
            {
                TessellationAvailable = true;
            }
            return program;
        }

        public void Use() => GL.UseProgram(Handle);

        private int GetLoc(string name)
        {
            if (_uniforms.TryGetValue(name, out var loc)) return loc;
            loc = GL.GetUniformLocation(Handle, name);
            _uniforms[name] = loc;
            return loc;
        }

        public void SetInt(string name, int v) => GL.Uniform1(GetLoc(name), v);
        public void SetUInt(string name, uint v) => GL.Uniform1(GetLoc(name), v);
        public void SetFloat(string name, float v) => GL.Uniform1(GetLoc(name), v);
        public void SetVec2(string name, Vector2 v) => GL.Uniform2(GetLoc(name), v);
        public void SetVec3(string name, Vector3 v) => GL.Uniform3(GetLoc(name), v);
        public void SetVec4(string name, Vector4 v) => GL.Uniform4(GetLoc(name), v);

        public void SetMat3(string name, Matrix3 m)
        {
            GL.UniformMatrix3(GetLoc(name), false, ref m);
        }

        public void SetMat4(string name, Matrix4 m)
        {
            GL.UniformMatrix4(GetLoc(name), false, ref m);
        }

        public void Dispose()
        {
            if (Handle != 0)
            {
                GL.DeleteProgram(Handle);
                Handle = 0;
            }
        }
    }
}
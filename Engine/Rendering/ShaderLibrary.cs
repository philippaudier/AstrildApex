using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenTK.Graphics.OpenGL4;

namespace Engine.Rendering
{
    /// <summary>
    /// Simple runtime registry that discovers shaders under Engine/Rendering/Shaders
    /// and exposes ShaderProgram instances by name. Loads programs lazily and
    /// ensures the global uniform block binding is set when a program is created.
    /// </summary>
    public static class ShaderLibrary
    {
        private static readonly Dictionary<string, (string vert, string frag, string? tesc, string? tese)> _pairs = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, ShaderProgram?> _cache = new(StringComparer.OrdinalIgnoreCase);
        private static bool _initialized = false;

        private static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;
            try
            {
                var root = Path.Combine("Engine", "Rendering", "Shaders");
                var fullPath = Path.GetFullPath(root);
                if (!Directory.Exists(root))
                {
                    // Shader directory not found - silently ignore in non-verbose mode
                    return;
                }
                var verts = Directory.GetFiles(root, "*.vert", SearchOption.AllDirectories);
                
                foreach (var v in verts)
                {
                        try
                        {
                            var name = Path.GetFileNameWithoutExtension(v);
                            var dir = Path.GetDirectoryName(v)!;
                            var frag = Path.Combine(dir, name + ".frag");
                            if (File.Exists(frag))
                            {
                                // Check for optional tessellation shaders (.tesc/.tese or .tcs/.tes)
                                var tesc = Path.Combine(dir, name + ".tesc");
                                var tcs = Path.Combine(dir, name + ".tcs");
                                var tese = Path.Combine(dir, name + ".tese");
                                var tes = Path.Combine(dir, name + ".tes");
                                
                                string? tescPath = File.Exists(tesc) ? tesc.Replace('\\', '/') : 
                                                   File.Exists(tcs) ? tcs.Replace('\\', '/') : null;
                                string? tesePath = File.Exists(tese) ? tese.Replace('\\', '/') : 
                                                   File.Exists(tes) ? tes.Replace('\\', '/') : null;

                                if (!_pairs.ContainsKey(name))
                                {
                                    _pairs[name] = (v.Replace('\\', '/'), frag.Replace('\\', '/'), tescPath, tesePath);
                                }
                            }
                        }
                    catch { }
                }
                // Initialization complete (verbose logging removed)
            }
            catch { }
        }

        public static string[] GetAvailableShaderNames()
        {
            EnsureInitialized();
            var names = _pairs.Keys.ToArray();
            Array.Sort(names, StringComparer.OrdinalIgnoreCase);
            return names;
        }

        /// <summary>
        /// Forcefully reloads a shader from disk, recompiling it and updating the cache.
        /// </summary>
        public static void ReloadShader(string name)
        {
            EnsureInitialized();
            if (string.IsNullOrEmpty(name)) return;
            // Remove from cache to force recompilation
            try
            {
                if (_cache.TryGetValue(name, out var oldShader) && oldShader != null)
                {
                    try
                    {
                        if (oldShader.Handle > 0 && GL.IsProgram(oldShader.Handle))
                        {
                            try { GL.DeleteProgram(oldShader.Handle); } catch { }
                        }
                    }
                    catch { }
                }
                _cache.Remove(name);
            }
            catch { }

            // Trigger reload (GetShaderByName will compile and cache)
            GetShaderByName(name);
        }

        /// <summary>
        /// Clear all cached shaders and force recompilation on next use.
        /// Call this when Global UBO layout changes or shader source files are modified.
        /// </summary>
        public static void ClearCache()
        {
            foreach (var kvp in _cache)
            {
                if (kvp.Value != null && kvp.Value.Handle > 0)
                {
                    try { GL.DeleteProgram(kvp.Value.Handle); } catch { }
                }
            }
            _cache.Clear();
        }

        public static ShaderProgram? GetShaderByName(string? name)
        {
            EnsureInitialized();
            if (string.IsNullOrEmpty(name)) return null;
            if (_cache.TryGetValue(name, out var prog))
            {
                // CRITICAL FIX: Re-bind Global UBO even for cached shaders
                // The UBO might have been created after shader compilation
                if (prog != null)
                {
                    try
                    {
                        prog.Use();
                        int globalBlockIndex = GL.GetUniformBlockIndex(prog.Handle, "Global");
                        if (globalBlockIndex != -1)
                        {
                            GL.UniformBlockBinding(prog.Handle, globalBlockIndex, 0);
                        }
                    }
                    catch { }
                }
                return prog;
            }
            if (!_pairs.TryGetValue(name, out var paths))
            {
                try { if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[ShaderLibrary] Shader '{name}' not found. Available: {string.Join(", ", _pairs.Keys)}"); } catch { }
                return null;
            }
            try
            {
                string tescInfo = paths.tesc != null ? $" + tesc" : "";
                string teseInfo = paths.tese != null ? $" + tese" : "";
                try { if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[ShaderLibrary] Loading shader {name} from {paths.vert} and {paths.frag}{tescInfo}{teseInfo}"); } catch { }
                var p = ShaderProgram.FromFiles(paths.vert, paths.frag, paths.tesc, paths.tese);
                try { if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[ShaderLibrary] Successfully compiled shader {name}"); } catch { }
                // Bind global uniform block if present
                try
                {
                    p.Use();
                    int globalBlockIndex = GL.GetUniformBlockIndex(p.Handle, "Global");
                    if (globalBlockIndex != -1)
                    {
                        GL.UniformBlockBinding(p.Handle, globalBlockIndex, 0);
                    }
                }
                catch { }
                _cache[name] = p;
                return p;
            }
            catch (Exception ex)
            {
                // CRITICAL: Always log shader compilation failures (not just in verbose mode)
                Console.WriteLine($"[ShaderLibrary] ❌ FAILED to compile shader '{name}': {ex.Message}");
                try { if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[ShaderLibrary] Stack trace: {ex.StackTrace}"); } catch { }
                _cache[name] = null;
                return null;
            }
        }
    }
}

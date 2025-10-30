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
                if (!Directory.Exists(root)) return;
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
            
            // Remove from cache to force recompilation (don't try to dispose invalid shaders)
            if (_cache.ContainsKey(name))
            {
                try
                {
                    var oldShader = _cache[name];
                    // Only dispose if shader exists and has a valid handle (> 0)
                    if (oldShader != null && oldShader.Handle > 0 && GL.IsProgram(oldShader.Handle))
                    {
                        oldShader.Dispose();
                    }
                }
                catch
                {
                    // Ignore disposal errors - we're reloading anyway
                }
                _cache.Remove(name);
            }
            
            // Reload
            GetShaderByName(name);
        }

        public static ShaderProgram? GetShaderByName(string? name)
        {
            EnsureInitialized();
            if (string.IsNullOrEmpty(name)) return null;
            if (_cache.TryGetValue(name, out var prog)) return prog;
            if (!_pairs.TryGetValue(name, out var paths)) return null;
            try
            {
                try
                {
                    string tescInfo = paths.tesc != null ? $" + tesc" : "";
                    string teseInfo = paths.tese != null ? $" + tese" : "";
                    Engine.Utils.DebugLogger.Log($"[ShaderLibrary] Loading shader {name} from {paths.vert} and {paths.frag}{tescInfo}{teseInfo}");
                }
                catch { }
                var p = ShaderProgram.FromFiles(paths.vert, paths.frag, paths.tesc, paths.tese);
                try { Engine.Utils.DebugLogger.Log($"[ShaderLibrary] Successfully compiled shader {name}"); } catch { }
                // Bind global uniform block if present
                try
                {
                    p.Use();
                    int globalBlockIndex = GL.GetUniformBlockIndex(p.Handle, "Global");
                    if (globalBlockIndex != -1) GL.UniformBlockBinding(p.Handle, globalBlockIndex, 0);
                }
                catch { }
                _cache[name] = p;
                return p;
            }
            catch (Exception ex)
            {
                try { Engine.Utils.DebugLogger.Log($"[ShaderLibrary] FAILED to load shader {name}: {ex.Message}"); } catch { }
                try { Console.WriteLine($"[ShaderLibrary] FAILED to load shader {name}: {ex.Message}"); } catch { }
                _cache[name] = null;
                return null;
            }
        }
    }
}

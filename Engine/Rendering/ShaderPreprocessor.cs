using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Engine.Rendering
{
    /// <summary>
    /// Advanced shader preprocessor with recursive #include support, circular dependency protection,
    /// and intelligent caching for modular shader architecture.
    /// </summary>
    public static class ShaderPreprocessor
    {
        private static readonly Dictionary<string, CachedShader> _cache = new();
        private static string _shaderBasePath = "";

        private struct CachedShader
        {
            public string ProcessedSource;
            public DateTime LastModified;
            public HashSet<string> Dependencies;
        }

        /// <summary>
        /// Set the base path for shader includes resolution.
        /// </summary>
        /// <param name="basePath">Base directory path for shader files</param>
        public static void SetShaderPath(string basePath)
        {
            _shaderBasePath = basePath;
        }

        /// <summary>
        /// Process a shader file with includes, using cache when possible.
        /// </summary>
        /// <param name="shaderPath">Path to the main shader file</param>
        /// <returns>Fully processed shader source with all includes expanded</returns>
        public static string ProcessShaderCached(string shaderPath)
        {
            if (!File.Exists(shaderPath))
                throw new FileNotFoundException($"Shader file not found: {shaderPath}");

            var absolutePath = Path.GetFullPath(shaderPath);
            var lastModified = File.GetLastWriteTime(absolutePath);

            // Check cache validity
            if (_cache.TryGetValue(absolutePath, out var cached))
            {
                // Check if main file or any dependencies were modified
                if (cached.LastModified >= lastModified)
                {
                    bool dependenciesValid = true;
                    foreach (var dep in cached.Dependencies)
                    {
                        if (File.Exists(dep) && File.GetLastWriteTime(dep) > cached.LastModified)
                        {
                            dependenciesValid = false;
                            break;
                        }
                    }

                    if (dependenciesValid)
                        return cached.ProcessedSource;
                }
            }

            // Process shader and update cache
            var dependencies = new HashSet<string>();
            var processedSource = ProcessShader(shaderPath, dependencies);

            _cache[absolutePath] = new CachedShader
            {
                ProcessedSource = processedSource,
                LastModified = DateTime.Now,
                Dependencies = dependencies
            };

            return processedSource;
        }

        /// <summary>
        /// Process a shader file with recursive includes without caching.
        /// </summary>
        /// <param name="shaderPath">Path to the shader file</param>
        /// <returns>Fully processed shader source</returns>
        public static string ProcessShader(string shaderPath)
        {
            var dependencies = new HashSet<string>();
            return ProcessShader(shaderPath, dependencies);
        }

        private static string ProcessShader(string shaderPath, HashSet<string> dependencies)
        {
            if (!File.Exists(shaderPath))
                throw new FileNotFoundException($"Shader file not found: {shaderPath}");

            var absolutePath = Path.GetFullPath(shaderPath);
            var includeStack = new Stack<string>();

            return ProcessShaderRecursive(absolutePath, dependencies, includeStack);
        }

        private static string ProcessShaderRecursive(string shaderPath, HashSet<string> dependencies, Stack<string> includeStack)
        {
            var absolutePath = Path.GetFullPath(shaderPath);

            // Circular dependency protection
            if (includeStack.Contains(absolutePath))
            {
                var stackTrace = string.Join(" -> ", includeStack);
                throw new InvalidOperationException($"Circular include dependency detected: {stackTrace} -> {absolutePath}");
            }

            includeStack.Push(absolutePath);
            dependencies.Add(absolutePath);

            try
            {
                var source = File.ReadAllText(absolutePath);
                var result = new StringBuilder();
                var lines = source.Split(new[] { '\n' }, StringSplitOptions.None);

                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    var trimmedLine = line.Trim();

                    // Support both #include and #import for compatibility
                    if (trimmedLine.StartsWith("#include ") || trimmedLine.StartsWith("#import "))
                    {
                        var includePath = ExtractIncludePath(trimmedLine);
                        if (!string.IsNullOrEmpty(includePath))
                        {
                            try
                            {
                                var resolvedPath = ResolveIncludePath(includePath, absolutePath);

                                // Add a comment indicating the included file for debugging
                                result.AppendLine($"// Begin include: {includePath}");

                                // Recursively process the included file
                                var includedSource = ProcessShaderRecursive(resolvedPath, dependencies, includeStack);
                                result.AppendLine(includedSource);

                                result.AppendLine($"// End include: {includePath}");
                            }
                            catch (Exception e)
                            {
                                // Log error but don't stop compilation - let OpenGL handle it
                                result.AppendLine($"// ERROR: Failed to include '{includePath}': {e.Message}");
                                result.AppendLine(line); // Keep original line for OpenGL error reporting

                                try
                                {
                                    Engine.Utils.DebugLogger.Log($"[ShaderPreprocessor] Failed to include '{includePath}' in '{shaderPath}': {e.Message}");
                                }
                                catch { }
                            }
                        }
                        else
                        {
                            // Invalid include syntax, keep original line
                            result.AppendLine(line);
                        }
                    }
                    else
                    {
                        // Regular line, keep as-is
                        result.AppendLine(line);
                    }
                }

                return result.ToString();
            }
            finally
            {
                includeStack.Pop();
            }
        }

        private static string ExtractIncludePath(string includeLine)
        {
            // Support both "path" and <path> syntax
            int firstQuote = includeLine.IndexOfAny(new char[] { '"', '<' });
            int lastQuote = includeLine.LastIndexOfAny(new char[] { '"', '>' });

            if (firstQuote >= 0 && lastQuote > firstQuote)
            {
                return includeLine.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
            }

            return string.Empty;
        }

        private static string ResolveIncludePath(string includePath, string currentShaderPath)
        {
            // Try relative to current shader file first
            var currentDir = Path.GetDirectoryName(currentShaderPath) ?? ".";
            var relativePath = Path.Combine(currentDir, includePath);

            if (File.Exists(relativePath))
                return Path.GetFullPath(relativePath);

            // Try relative to shader base path if set
            if (!string.IsNullOrEmpty(_shaderBasePath))
            {
                var basePath = Path.Combine(_shaderBasePath, includePath);
                if (File.Exists(basePath))
                    return Path.GetFullPath(basePath);
            }

            // Try as absolute path
            if (File.Exists(includePath))
                return Path.GetFullPath(includePath);

            throw new FileNotFoundException($"Include file not found: {includePath}");
        }

        /// <summary>
        /// Clear the preprocessor cache. Useful when shader files are modified externally.
        /// </summary>
        public static void ClearCache()
        {
            _cache.Clear();
        }

        /// <summary>
        /// Get cache statistics for debugging purposes.
        /// </summary>
        /// <returns>Number of cached shaders</returns>
        public static int GetCacheSize()
        {
            return _cache.Count;
        }
    }
}
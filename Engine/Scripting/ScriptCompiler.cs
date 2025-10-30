using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;

namespace Editor.Scripting
{
    /// <summary>
    /// Compile les .cs sous Editor/Assets/Scripts en un Assembly dynamique.
    /// Hot-reload via FileSystemWatcher: à chaque changement on recompile et on remplace l’AssemblyLoadContext.
    /// </summary>
    public sealed class ScriptCompiler : IDisposable
    {
        public sealed class ScriptDomain : AssemblyLoadContext
        {
            public ScriptDomain() : base(isCollectible: true) {}
        }

        private readonly string _scriptsDir;
        private FileSystemWatcher? _watcher;
        private ScriptDomain? _domain;
        private Assembly? _assembly;

        public event Action<Assembly?>? OnReloaded;

        public ScriptCompiler(string scriptsDir)
        {
            _scriptsDir = scriptsDir;
            Directory.CreateDirectory(_scriptsDir);
            SetupWatcher();
            CompileAndLoad();
        }

        public Assembly? CurrentAssembly => _assembly;

        void SetupWatcher()
        {
            _watcher = new FileSystemWatcher(_scriptsDir, "*.cs")
            {
                IncludeSubdirectories = true,
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
            };
            _watcher.Changed += (_, __) => DebouncedReload();
            _watcher.Created += (_, __) => DebouncedReload();
            _watcher.Deleted += (_, __) => DebouncedReload();
            _watcher.Renamed += (_, __) => DebouncedReload();
        }

        DateTime _last;
        void DebouncedReload()
        {
            var now = DateTime.UtcNow;
            if ((now - _last).TotalMilliseconds < 200) return;
            _last = now;
            CompileAndLoad();
        }

        void CompileAndLoad()
        {
            try
            {
                var files = Directory.GetFiles(_scriptsDir, "*.cs", SearchOption.AllDirectories);
                var syntaxTrees = files.Select(f =>
                    CSharpSyntaxTree.ParseText(File.ReadAllText(f),
                        new CSharpParseOptions(LanguageVersion.Preview),
                        path: f)).ToArray();

                // Références: mscorlib + Engine.dll + OpenTK + ImGui etc. Au minimum Engine.
                var refs = new List<MetadataReference>
                {
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Engine.Components.Component).Assembly.Location),
                };

                // Ajoute toutes les dépendances chargées utiles (option simple et efficace)
                var loaded = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                    .Select(a => a.Location)
                    .Distinct();
                foreach (var p in loaded) refs.Add(MetadataReference.CreateFromFile(p));

                var compilation = CSharpCompilation.Create(
                    assemblyName: "AstrildApex_Scripts",
                    syntaxTrees: syntaxTrees,
                    references: refs,
                    options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                        optimizationLevel: OptimizationLevel.Debug,
                        allowUnsafe: true));

                using var ms = new MemoryStream();
                var result = compilation.Emit(ms);
                if (!result.Success)
                {
                    Console.WriteLine("[ScriptCompiler] ❌ Compilation FAILED:");
                    foreach (var d in result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
                    {
                        Console.WriteLine($"  {d.Location.GetLineSpan().Path}({d.Location.GetLineSpan().StartLinePosition.Line + 1}): {d.GetMessage()}");
                    }
                    return;
                }

                // Décharge ancien domain
                _assembly = null;
                var old = _domain;
                _domain = new ScriptDomain();

                ms.Position = 0;
                _assembly = _domain.LoadFromStream(ms);

                Console.WriteLine($"[ScriptCompiler] ✅ Compilation SUCCESS! {files.Length} file(s) compiled.");

                OnReloaded?.Invoke(_assembly);

                // Permet GC de l'ancien
                old?.Unload();
                // (Optionnel) GC.Collect/WaitForPendingFinalizers pour forcer
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ScriptCompiler] ❌ Exception during compilation: {ex.Message}");
            }
        }

        public void Dispose()
        {
            try
            {
                if (_watcher != null)
                {
                    _watcher.EnableRaisingEvents = false;
                    _watcher.Dispose();
                }
                _assembly = null;
                _domain?.Unload();
            }
            catch { }
        }
    }
}

using System;
using System.IO;

namespace Editor.State
{
    public static class ProjectPaths
    {
        public static readonly string ProjectRoot =
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        

        public static string ScenesDir => Path.Combine(ProjectRoot, "Scenes");
        public static string DefaultScenePath => Path.Combine(ScenesDir, "CurrentScene.scene");
        public static string AssetsDir
        {
            get
            {
                // Prefer the Editor/Assets folder when it exists (common in this project layout).
                var editorAssets = Path.Combine(ProjectRoot, "Editor", "Assets");
                if (Directory.Exists(editorAssets)) return editorAssets;
                // Fallback to top-level Assets folder
                return Path.Combine(ProjectRoot, "Assets");
            }
        }
        public static string ScriptsDir => System.IO.Path.Combine(AssetsDir, "Scripts");
    }

}


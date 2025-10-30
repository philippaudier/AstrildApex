using ImGuiNET;
using System;
using System.IO;
using System.Numerics;

namespace Editor.Inspector
{
    public static class TrueTypeFontInspector
    {
        public static void Draw(Guid ttfGuid)
        {
            if (!Engine.Assets.AssetDatabase.TryGet(ttfGuid, out var record))
            {
                ImGui.TextDisabled("TrueType font not found in database.");
                return;
            }

            ImGui.TextWrapped($"Font File: {Path.GetFileName(record.Path)}");
            ImGui.TextDisabled($"Path: {record.Path}");
            ImGui.Separator();

            // File info
            if (File.Exists(record.Path))
            {
                var fileInfo = new FileInfo(record.Path);
                ImGui.Text($"Size: {fileInfo.Length / 1024.0:F2} KB");
                ImGui.Text($"Modified: {fileInfo.LastWriteTime:g}");
            }
            else
            {
                ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f), "File not found!");
            }

            ImGui.Separator();

            // Create FontAsset button
            ImGui.TextWrapped("Create a Font Asset to use this font in your UI:");
            ImGui.Spacing();

            if (ImGui.Button("Create Font Asset", new Vector2(-1, 30)))
            {
                CreateFontAsset(record.Path);
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.TextDisabled("Font Preview");
            ImGui.TextWrapped("Preview functionality coming soon...");
            ImGui.TextDisabled("ABCDEFGHIJKLMNOPQRSTUVWXYZ");
            ImGui.TextDisabled("abcdefghijklmnopqrstuvwxyz");
            ImGui.TextDisabled("0123456789 !@#$%^&*()");
        }

        private static void CreateFontAsset(string ttfPath)
        {
            try
            {
                // Create FontAsset folder if it doesn't exist
                var fontAssetFolder = Path.Combine(Engine.Assets.AssetDatabase.AssetsRoot, "Fonts");
                Directory.CreateDirectory(fontAssetFolder);

                // Create a new FontAsset
                var fontAsset = new Engine.Assets.FontAsset
                {
                    Guid = Guid.NewGuid(),
                    Name = Path.GetFileNameWithoutExtension(ttfPath),
                    RegularPath = ttfPath,
                    DefaultSize = 14f,
                    LineHeight = 1.2f
                };

                // Save the FontAsset
                var fontAssetPath = Path.Combine(fontAssetFolder, fontAsset.Name + ".fontasset");
                int counter = 1;
                while (File.Exists(fontAssetPath))
                {
                    fontAssetPath = Path.Combine(fontAssetFolder, $"{fontAsset.Name}_{counter++}.fontasset");
                }

                fontAsset.Save(fontAssetPath);

                // Refresh asset database to pick up the new .fontasset
                Engine.Assets.AssetDatabase.Refresh();

                Engine.Utils.DebugLogger.Log($"[TrueTypeFontInspector] Created FontAsset: {fontAssetPath}");

                // Select the newly created FontAsset
                if (Engine.Assets.AssetDatabase.TryGetByPath(fontAssetPath, out var newRecord))
                {
                    Editor.State.Selection.SetActiveAsset(newRecord.Guid, "FontAsset");
                }
            }
            catch (Exception ex)
            {
                Engine.Utils.DebugLogger.Log($"[TrueTypeFontInspector] ERROR: Failed to create FontAsset: {ex.Message}");
            }
        }
    }
}

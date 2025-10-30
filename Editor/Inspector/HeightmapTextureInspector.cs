using System;
using System.IO;
using ImGuiNET;
using Engine.Assets;
using Engine.Rendering;
using OpenTK.Graphics.OpenGL4;

namespace Editor.Inspector
{
    public enum HeightmapTextureFormat
    {
        Auto,
        R8,
        R16,
        RAW16
    }

    public enum HeightmapImportMode
    {
        Texture,
        Heightmap
    }

    public class HeightmapImportSettings
    {
        public HeightmapImportMode ImportMode { get; set; } = HeightmapImportMode.Texture;
        public HeightmapTextureFormat Format { get; set; } = HeightmapTextureFormat.Auto;
        public float HeightScale { get; set; } = 1.0f;
        public bool FlipVertically { get; set; } = false;
        public bool GenerateNormals { get; set; } = true;
        public int Width { get; set; } = 1024;
        public int Height { get; set; } = 1024;
        public bool IsByteOrder { get; set; } = true; // true = Intel, false = Mac
    }

    /// <summary>
    /// Unity-inspired texture inspector with heightmap import settings
    /// </summary>
    public static class HeightmapTextureInspector
    {
        private static readonly HeightmapImportSettings _importSettings = new();

        public static void Draw(Guid guid)
        {
            if (!AssetDatabase.TryGet(guid, out var rec))
            {
                ImGui.TextColored(new System.Numerics.Vector4(1, 0.4f, 0.4f, 1), "Texture not found.");
                return;
            }

            ImGui.Text("Texture Import Settings");
            ImGui.Separator();

            ImGui.Text("Path:");
            ImGui.SameLine();
            ImGui.TextDisabled(rec.Path);

            // Import mode selector
            ImGui.Spacing();
            ImGui.Text("Import Mode:");
            int importMode = (int)_importSettings.ImportMode;
            string[] importModes = { "Texture", "Heightmap" };
            if (ImGui.Combo("##ImportMode", ref importMode, importModes, importModes.Length))
            {
                _importSettings.ImportMode = (HeightmapImportMode)importMode;
            }

            ImGui.Spacing();
            ImGui.Separator();

            if (_importSettings.ImportMode == HeightmapImportMode.Heightmap)
            {
                DrawHeightmapSettings(guid, rec);
            }
            else
            {
                DrawTextureSettings(guid, rec);
            }

            // Preview
            DrawTexturePreview(guid, rec);

            // Apply/Revert buttons
            ImGui.Spacing();
            ImGui.Separator();
            if (ImGui.Button("Apply"))
            {
                ApplyImportSettings(guid, rec);
            }
            ImGui.SameLine();
            if (ImGui.Button("Revert"))
            {
                RevertImportSettings();
            }

            // Utility buttons
            ImGui.Spacing();
            if (ImGui.Button("Reveal in Explorer"))
                RevealFile(rec.Path);
            ImGui.SameLine();
            if (ImGui.Button("Open Externally"))
                OpenFile(rec.Path);
            ImGui.SameLine();
            if (ImGui.Button("Copy GUID"))
                ImGui.SetClipboardText(guid.ToString());
        }

        private static void DrawHeightmapSettings(Guid guid, AssetDatabase.AssetRecord rec)
        {
            ImGui.TextColored(new System.Numerics.Vector4(0.5f, 1.0f, 0.5f, 1), "🏔️ Heightmap Import Settings");
            ImGui.Spacing();

            // Format selection
            ImGui.Text("Format:");
            int format = (int)_importSettings.Format;
            string[] formats = { "Auto", "R8 (8-bit)", "R16 (16-bit)", "RAW16" };
            if (ImGui.Combo("##Format", ref format, formats, formats.Length))
            {
                _importSettings.Format = (HeightmapTextureFormat)format;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Format of the heightmap data");

            // Resolution settings
            ImGui.Text("Resolution:");
            int width = _importSettings.Width;
            if (ImGui.InputInt("Width##HeightmapWidth", ref width))
            {
                _importSettings.Width = Math.Max(1, width);
            }

            int height = _importSettings.Height;
            if (ImGui.InputInt("Height##HeightmapHeight", ref height))
            {
                _importSettings.Height = Math.Max(1, height);
            }

            // Height scale
            float heightScale = _importSettings.HeightScale;
            if (ImGui.DragFloat("Height Scale", ref heightScale, 0.01f, 0.001f, 1000.0f))
            {
                _importSettings.HeightScale = heightScale;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Multiplier for height values");

            // Flip options
            bool flipY = _importSettings.FlipVertically;
            if (ImGui.Checkbox("Flip Vertically", ref flipY))
            {
                _importSettings.FlipVertically = flipY;
            }

            // RAW-specific settings
            if (_importSettings.Format == HeightmapTextureFormat.RAW16)
            {
                ImGui.Spacing();
                ImGui.Text("RAW Import Settings:");
                bool byteOrder = _importSettings.IsByteOrder;
                if (ImGui.Checkbox("Intel Byte Order", ref byteOrder))
                {
                    _importSettings.IsByteOrder = byteOrder;
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Uncheck for Mac byte order");
            }

            // Generation options
            ImGui.Spacing();
            ImGui.Text("Generation:");
            bool generateNormals = _importSettings.GenerateNormals;
            if (ImGui.Checkbox("Generate Normal Map", ref generateNormals))
            {
                _importSettings.GenerateNormals = generateNormals;
            }

            // Info
            ImGui.Spacing();
            ImGui.TextColored(new System.Numerics.Vector4(0.7f, 0.7f, 1.0f, 1), "Usage in Terrain:");
            ImGui.BulletText("Set Terrain component to Heightmap mode");
            ImGui.BulletText("Assign this texture as heightmap source");
            ImGui.BulletText("Configure terrain size and resolution");
        }

        private static void DrawTextureSettings(Guid guid, AssetDatabase.AssetRecord rec)
        {
            ImGui.TextColored(new System.Numerics.Vector4(0.5f, 0.8f, 1.0f, 1), "🖼️ Standard Texture Settings");
            ImGui.Spacing();

            // Standard texture settings would go here
            ImGui.Text("Texture Type: Default");
            ImGui.Text("Alpha Source: Input Texture Alpha");
            ImGui.Text("Alpha Is Transparency: No");

            ImGui.Spacing();
            ImGui.Text("Wrap Mode: Repeat");
            ImGui.Text("Filter Mode: Bilinear");
            ImGui.Text("Aniso Level: 1");

            // Platform settings could be added here
        }

        private static void DrawTexturePreview(Guid guid, AssetDatabase.AssetRecord rec)
        {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text("Preview:");

            // Load texture for preview
            TextureCache.Initialize();
            int handle = TextureCache.GetOrLoad(guid, g =>
                AssetDatabase.TryGet(g, out var r) ? r.Path : null);

            // Get dimensions
            int w = 0, h = 0;
            if (handle != 0)
            {
                GL.BindTexture(TextureTarget.Texture2D, handle);
                GL.GetTexLevelParameter(TextureTarget.Texture2D, 0, GetTextureParameter.TextureWidth, out w);
                GL.GetTexLevelParameter(TextureTarget.Texture2D, 0, GetTextureParameter.TextureHeight, out h);
                GL.BindTexture(TextureTarget.Texture2D, 0);
            }

            // Preview image
            var previewSize = new System.Numerics.Vector2(200, 200);
            if (handle != 0)
            {
                ImGui.Image((IntPtr)handle, previewSize, new System.Numerics.Vector2(0, 1), new System.Numerics.Vector2(1, 0));
            }
            else
            {
                ImGui.Button("Loading...", previewSize);
            }

            // Texture info
            if (w > 0 && h > 0)
            {
                ImGui.Text($"Dimensions: {w} × {h}px");

                if (_importSettings.ImportMode == HeightmapImportMode.Heightmap)
                {
                    // Check if dimensions are power of 2 + 1 (common for heightmaps)
                    bool isPowerOf2Plus1W = IsPowerOf2Plus1(w);
                    bool isPowerOf2Plus1H = IsPowerOf2Plus1(h);

                    if (isPowerOf2Plus1W && isPowerOf2Plus1H)
                    {
                        ImGui.TextColored(new System.Numerics.Vector4(0.5f, 1.0f, 0.5f, 1), "✓ Optimal heightmap resolution");
                    }
                    else if (IsPowerOf2(w) && IsPowerOf2(h))
                    {
                        ImGui.TextColored(new System.Numerics.Vector4(1.0f, 0.8f, 0.5f, 1), "⚠ Consider (2^n + 1) resolution for better terrain");
                    }
                }
            }

            try
            {
                var fi = new FileInfo(rec.Path);
                if (fi.Exists) ImGui.Text($"File Size: {fi.Length / 1024f:0.0} KB");
            }
            catch { }
        }

        private static bool IsPowerOf2(int value)
        {
            return value > 0 && (value & (value - 1)) == 0;
        }

        private static bool IsPowerOf2Plus1(int value)
        {
            return IsPowerOf2(value - 1);
        }

        private static void ApplyImportSettings(Guid guid, AssetDatabase.AssetRecord rec)
        {
            // Here we would apply the import settings
            // For now, just mark the asset as needing reimport
            try
            {
                // Create or update .meta file with import settings
                string metaPath = rec.Path + ".meta";
                var metaContent = new
                {
                    guid = guid.ToString(),
                    type = "Texture2D",
                    importSettings = new
                    {
                        importMode = _importSettings.ImportMode.ToString(),
                        format = _importSettings.Format.ToString(),
                        heightScale = _importSettings.HeightScale,
                        flipVertically = _importSettings.FlipVertically,
                        generateNormals = _importSettings.GenerateNormals,
                        width = _importSettings.Width,
                        height = _importSettings.Height,
                        isByteOrder = _importSettings.IsByteOrder
                    }
                };

                string json = System.Text.Json.JsonSerializer.Serialize(metaContent,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(metaPath, json);

                // Clear texture cache to force reload
                TextureCache.ClearCache();

                // Refresh asset database
                AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to apply import settings: {ex.Message}");
            }
        }

        private static void RevertImportSettings()
        {
            // Reset to defaults
            _importSettings.ImportMode = HeightmapImportMode.Texture;
            _importSettings.Format = HeightmapTextureFormat.Auto;
            _importSettings.HeightScale = 1.0f;
            _importSettings.FlipVertically = false;
            _importSettings.GenerateNormals = true;
            _importSettings.Width = 1024;
            _importSettings.Height = 1024;
            _importSettings.IsByteOrder = true;
        }

        static void RevealFile(string path)
        {
            try
            {
                if (OperatingSystem.IsWindows())
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
                else if (OperatingSystem.IsMacOS())
                    System.Diagnostics.Process.Start("open", $"-R \"{path}\"");
                else
                    System.Diagnostics.Process.Start("xdg-open", Path.GetDirectoryName(path)!);
            }
            catch { }
        }

        static void OpenFile(string path)
        {
            try
            {
                if (OperatingSystem.IsWindows())
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
                else if (OperatingSystem.IsMacOS())
                    System.Diagnostics.Process.Start("open", $"\"{path}\"");
                else
                    System.Diagnostics.Process.Start("xdg-open", $"\"{path}\"");
            }
            catch { }
        }
    }
}
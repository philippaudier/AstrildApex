using System;
using System.IO;
using ImGuiNET;
using Engine.Assets;
using Engine.Rendering;
using OpenTK.Graphics.OpenGL4;

namespace Editor.Inspector
{
    public static class TextureInspector
    {
        public static void Draw(Guid guid)
        {
            if (!AssetDatabase.TryGet(guid, out var rec))
            {
                ImGui.TextColored(new System.Numerics.Vector4(1,0.4f,0.4f,1), "Texture introuvable.");
                return;
            }

            // Check if this is an HDR texture
            if (Path.GetExtension(rec.Path).Equals(".hdr", StringComparison.OrdinalIgnoreCase))
            {
                HDRTextureInspector.Draw(guid);
                return;
            }

            ImGui.Text("Texture Inspector");
            ImGui.Separator();

            // Show heightmap info if detected
            if (IsLikelyHeightmap(rec.Path))
            {
                ImGui.TextColored(new System.Numerics.Vector4(0.5f, 1.0f, 0.5f, 1), "🏔️ Heightmap detected");
                ImGui.TextDisabled("Use as HeightmapTexture in Terrain component for best results");
                ImGui.Separator();
            }

            ImGui.Text("Path:");
            ImGui.SameLine();
            ImGui.TextDisabled(rec.Path);

            // Preview + dimensions OpenGL
            TextureCache.Initialize();
            int handle = TextureCache.GetOrLoad(guid, g =>
                AssetDatabase.TryGet(g, out var r) ? r.Path : null);

            // Dimensions GL (si disponible)
            int w = 0, h = 0;
            if (handle != 0)
            {
                GL.BindTexture(TextureTarget.Texture2D, handle);
                GL.GetTexLevelParameter(TextureTarget.Texture2D, 0, GetTextureParameter.TextureWidth, out w);
                GL.GetTexLevelParameter(TextureTarget.Texture2D, 0, GetTextureParameter.TextureHeight, out h);
                GL.BindTexture(TextureTarget.Texture2D, 0);
            }

            // Preview (UV inversés pour FBO/GL)
            var size = new System.Numerics.Vector2(160, 160);
            ImGui.Image((IntPtr)handle, size, new System.Numerics.Vector2(0,1), new System.Numerics.Vector2(1,0));

            // Infos
            ImGui.Separator();
            ImGui.Text($"Type: Texture2D");
            if (w > 0 && h > 0) ImGui.Text($"Dimensions: {w} × {h}px");
            try
            {
                var fi = new FileInfo(rec.Path);
                if (fi.Exists) ImGui.Text($"File Size: {fi.Length / 1024f:0.0} KB");
            }
            catch { }

            // Utils
            if (ImGui.Button("Reveal in Explorer"))
                RevealFile(rec.Path);
            ImGui.SameLine();
            if (ImGui.Button("Open Externally"))
                OpenFile(rec.Path);

            ImGui.SameLine();
            if (ImGui.Button("Copy GUID"))
                ImGui.SetClipboardText(guid.ToString());

            // Normal map importer toggles
            ImGui.Separator();
            bool isNormal = false;
            bool flipGreen = false;

            void ReadMeta()
            {
                isNormal = false; flipGreen = false;
                try
                {
                    var metaPath = rec.Path + ".meta";
                    if (File.Exists(metaPath))
                    {
                        var json = File.ReadAllText(metaPath);
                        using var doc = System.Text.Json.JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("isNormalMap", out var jn)) isNormal = jn.GetBoolean();
                        if (doc.RootElement.TryGetProperty("flipGreen", out var jg)) flipGreen = jg.GetBoolean();
                    }
                }
                catch { }
            }

            void WriteMeta()
            {
                try
                {
                    var metaPath = rec.Path + ".meta";
                    var dest = new System.Collections.Generic.Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    if (File.Exists(metaPath))
                    {
                        try
                        {
                            var json = File.ReadAllText(metaPath);
                            using var doc = System.Text.Json.JsonDocument.Parse(json);
                            foreach (var prop in doc.RootElement.EnumerateObject())
                            {
                                var name = prop.Name;
                                var el = prop.Value;
                                switch (el.ValueKind)
                                {
                                    case System.Text.Json.JsonValueKind.True:
                                    case System.Text.Json.JsonValueKind.False:
                                        dest[name] = el.GetBoolean(); break;
                                    case System.Text.Json.JsonValueKind.Number:
                                        if (el.TryGetInt64(out var iv)) dest[name] = iv; else if (el.TryGetDouble(out var dv)) dest[name] = dv; else dest[name] = el.GetRawText();
                                        break;
                                    case System.Text.Json.JsonValueKind.String:
                                        dest[name] = el.GetString(); break;
                                    default:
                                        dest[name] = el.GetRawText(); break;
                                }
                            }
                        }
                        catch { }
                    }

                    dest["isNormalMap"] = isNormal;
                    dest["flipGreen"] = flipGreen;
                    File.WriteAllText(metaPath, System.Text.Json.JsonSerializer.Serialize(dest, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

                    // Invalidate and refresh so the asset is re-indexed and reloaded
                    Engine.Rendering.TextureCache.Invalidate(guid);
                    try { Engine.Assets.AssetDatabase.Refresh(); } catch { }

                    // Re-read meta to ensure UI matches persisted values (and update texture path resolution if GUID changed)
                    ReadMeta();
                }
                catch { }
            }

            // Read once at start
            ReadMeta();

            if (ImGui.Checkbox("Is Normal Map", ref isNormal))
            {
                WriteMeta();
            }

            if (ImGui.Checkbox("Flip Green (DX<->GL)", ref flipGreen))
            {
                WriteMeta();
            }
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
            catch {}
        }

        /// <summary>
        /// Check if a texture file is likely to be a heightmap based on filename patterns
        /// </summary>
        private static bool IsLikelyHeightmap(string path)
        {
            string filename = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            return filename.Contains("height") ||
                   filename.Contains("elevation") ||
                   filename.Contains("depth") ||
                   filename.Contains("terrain") ||
                   filename.EndsWith("_h") ||
                   filename.EndsWith("_height") ||
                   filename.EndsWith("_heightmap");
        }
    }
}

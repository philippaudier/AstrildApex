using System;
using System.IO;
using ImGuiNET;
using Engine.Assets;
using Engine.Rendering;
using OpenTK.Graphics.OpenGL4;
using Editor.UI;

namespace Editor.Inspector
{
    public static class TextureInspector
    {
        private static TextureImportSettings? _currentSettings;
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

            // Texture Import Settings
            ImGui.Separator();
            ImGui.Text("Texture Import Settings");
            ImGui.Separator();

            // Load settings
            if (_currentSettings == null)
            {
                _currentSettings = LoadSettings(rec.Path);
            }

            bool settingsChanged = false;

            // Texture Type
            int textureType = (int)_currentSettings.TextureType;
            string[] typeNames = { "Default", "Sprite (2D)", "Normal Map", "HDR", "Lightmap", "Cursor" };
            if (ImGui.Combo("Texture Type", ref textureType, typeNames, typeNames.Length))
            {
                _currentSettings.TextureType = (TextureImportType)textureType;
                settingsChanged = true;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Choose the texture type for optimal import settings");

            ImGui.Spacing();

            // Sprite-specific settings
            if (_currentSettings.TextureType == TextureImportType.Sprite)
            {
                if (ThemedImGui.CollapsingHeader("Sprite Settings", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.Indent();

                    bool useAlphaCutoff = _currentSettings.UseAlphaCutoff;
                    if (ImGui.Checkbox("Use Alpha Cutoff", ref useAlphaCutoff))
                    {
                        _currentSettings.UseAlphaCutoff = useAlphaCutoff;
                        settingsChanged = true;
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Discard pixels below alpha threshold (removes transparent background)");

                    if (_currentSettings.UseAlphaCutoff)
                    {
                        float alphaCutoff = _currentSettings.AlphaCutoff;
                        if (ImGui.SliderFloat("Alpha Cutoff", ref alphaCutoff, 0.0f, 1.0f))
                        {
                            _currentSettings.AlphaCutoff = alphaCutoff;
                            settingsChanged = true;
                        }
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip("Pixels with alpha below this value will be discarded");
                    }

                    ImGui.Unindent();
                }
                ImGui.Spacing();
            }

            // Normal map settings
            if (_currentSettings.TextureType == TextureImportType.NormalMap)
            {
                if (ThemedImGui.CollapsingHeader("Normal Map Settings", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.Indent();

                    bool flipGreen = _currentSettings.FlipGreen;
                    if (ImGui.Checkbox("Flip Green Channel (DX <-> GL)", ref flipGreen))
                    {
                        _currentSettings.FlipGreen = flipGreen;
                        settingsChanged = true;
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Convert between DirectX and OpenGL normal map formats");

                    ImGui.Unindent();
                }
                ImGui.Spacing();
            }

            // Wrap Mode
            int wrapMode = (int)_currentSettings.WrapMode;
            string[] wrapNames = { "Repeat", "Clamp", "Mirror", "Mirror Once" };
            if (ImGui.Combo("Wrap Mode", ref wrapMode, wrapNames, wrapNames.Length))
            {
                _currentSettings.WrapMode = (TextureImportWrapMode)wrapMode;
                settingsChanged = true;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("How texture coordinates outside [0,1] are handled");

            // Filter Mode
            int filterMode = (int)_currentSettings.FilterMode;
            string[] filterNames = { "Point (Pixelated)", "Bilinear (Smooth)", "Trilinear (Best)" };
            if (ImGui.Combo("Filter Mode", ref filterMode, filterNames, filterNames.Length))
            {
                _currentSettings.FilterMode = (TextureImportFilterMode)filterMode;
                settingsChanged = true;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Texture filtering quality");

            // Mipmaps
            bool generateMipmaps = _currentSettings.GenerateMipmaps;
            if (ImGui.Checkbox("Generate Mipmaps", ref generateMipmaps))
            {
                _currentSettings.GenerateMipmaps = generateMipmaps;
                settingsChanged = true;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Improves quality at different distances (recommended)");

            // Max Texture Size
            int maxSize = _currentSettings.MaxTextureSize;
            string[] sizeNames = { "32", "64", "128", "256", "512", "1024", "2048", "4096", "8192" };
            int[] sizeValues = { 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192 };
            int currentSizeIndex = Array.IndexOf(sizeValues, maxSize);
            if (currentSizeIndex == -1) currentSizeIndex = 6; // Default to 2048

            if (ImGui.Combo("Max Size", ref currentSizeIndex, sizeNames, sizeNames.Length))
            {
                _currentSettings.MaxTextureSize = sizeValues[currentSizeIndex];
                settingsChanged = true;
            }

            // Apply button
            if (settingsChanged || ImGui.Button("Apply"))
            {
                SaveSettings(rec.Path, _currentSettings);
                Engine.Rendering.TextureCache.Invalidate(guid);
                try { Engine.Assets.AssetDatabase.Refresh(); } catch { }
            }

            if (settingsChanged)
            {
                ImGui.SameLine();
                ImGui.TextColored(new System.Numerics.Vector4(1, 0.7f, 0.3f, 1), "Modified");
            }
        }

        private static TextureImportSettings LoadSettings(string texturePath)
        {
            var settings = new TextureImportSettings();
            try
            {
                var metaPath = texturePath + ".meta";
                if (File.Exists(metaPath))
                {
                    var json = File.ReadAllText(metaPath);
                    using var doc = System.Text.Json.JsonDocument.Parse(json);

                    if (doc.RootElement.TryGetProperty("textureType", out var jType))
                        settings.TextureType = (TextureImportType)jType.GetInt32();

                    if (doc.RootElement.TryGetProperty("wrapMode", out var jWrap))
                        settings.WrapMode = (TextureImportWrapMode)jWrap.GetInt32();

                    if (doc.RootElement.TryGetProperty("filterMode", out var jFilter))
                        settings.FilterMode = (TextureImportFilterMode)jFilter.GetInt32();

                    if (doc.RootElement.TryGetProperty("generateMipmaps", out var jMip))
                        settings.GenerateMipmaps = jMip.GetBoolean();

                    if (doc.RootElement.TryGetProperty("alphaCutoff", out var jAlpha))
                        settings.AlphaCutoff = (float)jAlpha.GetDouble();

                    if (doc.RootElement.TryGetProperty("useAlphaCutoff", out var jUseAlpha))
                        settings.UseAlphaCutoff = jUseAlpha.GetBoolean();

                    if (doc.RootElement.TryGetProperty("isNormalMap", out var jNormal))
                        settings.IsNormalMap = jNormal.GetBoolean();

                    if (doc.RootElement.TryGetProperty("flipGreen", out var jFlip))
                        settings.FlipGreen = jFlip.GetBoolean();

                    if (doc.RootElement.TryGetProperty("maxTextureSize", out var jMaxSize))
                        settings.MaxTextureSize = jMaxSize.GetInt32();

                    // Auto-detect normal map from TextureType
                    if (settings.TextureType == TextureImportType.NormalMap)
                        settings.IsNormalMap = true;
                }
            }
            catch { }
            return settings;
        }

        private static void SaveSettings(string texturePath, TextureImportSettings settings)
        {
            try
            {
                var metaPath = texturePath + ".meta";
                var dict = new System.Collections.Generic.Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

                // Preserve existing meta properties
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
                                    dict[name] = el.GetBoolean();
                                    break;
                                case System.Text.Json.JsonValueKind.Number:
                                    if (el.TryGetInt64(out var iv))
                                        dict[name] = iv;
                                    else if (el.TryGetDouble(out var dv))
                                        dict[name] = dv;
                                    else
                                        dict[name] = el.GetRawText();
                                    break;
                                case System.Text.Json.JsonValueKind.String:
                                    dict[name] = el.GetString();
                                    break;
                                default:
                                    dict[name] = el.GetRawText();
                                    break;
                            }
                        }
                    }
                    catch { }
                }

                // Update with new settings
                dict["textureType"] = (int)settings.TextureType;
                dict["wrapMode"] = (int)settings.WrapMode;
                dict["filterMode"] = (int)settings.FilterMode;
                dict["generateMipmaps"] = settings.GenerateMipmaps;
                dict["alphaCutoff"] = settings.AlphaCutoff;
                dict["useAlphaCutoff"] = settings.UseAlphaCutoff;
                dict["isNormalMap"] = settings.TextureType == TextureImportType.NormalMap || settings.IsNormalMap;
                dict["flipGreen"] = settings.FlipGreen;
                dict["maxTextureSize"] = settings.MaxTextureSize;

                File.WriteAllText(metaPath, System.Text.Json.JsonSerializer.Serialize(dict,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
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

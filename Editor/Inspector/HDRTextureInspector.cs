using System;
using System.IO;
using ImGuiNET;
using Engine.Assets;
using Engine.Rendering;
using OpenTK.Graphics.OpenGL4;

namespace Editor.Inspector
{
    /// <summary>
    /// Specialized inspector for HDR textures (.hdr files) with skybox integration
    /// </summary>
    public static class HDRTextureInspector
    {
        public static void Draw(Guid guid)
        {
            if (!AssetDatabase.TryGet(guid, out var rec))
            {
                ImGui.TextColored(new System.Numerics.Vector4(1, 0.4f, 0.4f, 1), "HDR Texture not found.");
                return;
            }

            ImGui.Text("HDR Texture Inspector");
            ImGui.Separator();

            ImGui.Text("Path:");
            ImGui.SameLine();
            ImGui.TextDisabled(rec.Path);

            // HDR-specific information
            ImGui.Spacing();
            ImGui.TextColored(new System.Numerics.Vector4(0.8f, 0.9f, 1.0f, 1), "High Dynamic Range Texture");
            ImGui.TextDisabled("Suitable for skybox lighting and reflections");

            // Preview and dimensions
            TextureCache.Initialize();
            int handle = TextureCache.GetOrLoad(guid, g =>
                AssetDatabase.TryGet(g, out var r) ? r.Path : null);

            // Get texture dimensions
            int w = 0, h = 0;
            if (handle != 0)
            {
                GL.BindTexture(TextureTarget.Texture2D, handle);
                GL.GetTexLevelParameter(TextureTarget.Texture2D, 0, GetTextureParameter.TextureWidth, out w);
                GL.GetTexLevelParameter(TextureTarget.Texture2D, 0, GetTextureParameter.TextureHeight, out h);
                GL.BindTexture(TextureTarget.Texture2D, 0);
            }

            // HDR Preview (tone-mapped for display)
            ImGui.Spacing();
            ImGui.Text("Preview (tone-mapped):");
            var previewSize = new System.Numerics.Vector2(200, 100); // HDR is usually 2:1 aspect ratio
            if (handle != 0)
            {
                ImGui.Image((IntPtr)handle, previewSize, new System.Numerics.Vector2(0, 1), new System.Numerics.Vector2(1, 0));
            }
            else
            {
                ImGui.Button("Loading...", previewSize);
            }

            // HDR Information
            ImGui.Separator();
            ImGui.Text("Format: HDR (High Dynamic Range)");
            if (w > 0 && h > 0)
            {
                ImGui.Text($"Dimensions: {w} × {h}px");
                float aspectRatio = (float)w / h;
                ImGui.Text($"Aspect Ratio: {aspectRatio:F2}:1");

                // Check if it's a typical equirectangular format
                if (Math.Abs(aspectRatio - 2.0f) < 0.1f)
                {
                    ImGui.TextColored(new System.Numerics.Vector4(0.5f, 1.0f, 0.5f, 1), "✓ Equirectangular format (ideal for skybox)");
                }
                else
                {
                    ImGui.TextColored(new System.Numerics.Vector4(1.0f, 0.8f, 0.5f, 1), "⚠ Non-standard aspect ratio for skybox");
                }
            }

            try
            {
                var fi = new FileInfo(rec.Path);
                if (fi.Exists) ImGui.Text($"File Size: {fi.Length / (1024f * 1024f):0.2} MB");
            }
            catch { }

            // Skybox Integration
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.TextColored(new System.Numerics.Vector4(1.0f, 1.0f, 0.5f, 1), "Skybox Integration");

            if (ImGui.Button("Create Skybox Material from HDR"))
            {
                CreateSkyboxMaterialFromHDR(guid, rec);
            }

            ImGui.SameLine();
            if (ImGui.Button("Set as Environment Skybox"))
            {
                SetAsEnvironmentSkybox(guid, rec);
            }

            // Usage tips
            ImGui.Spacing();
            ImGui.TextColored(new System.Numerics.Vector4(0.7f, 0.7f, 1.0f, 1), "Usage Tips:");
            ImGui.BulletText("HDR textures provide realistic lighting");
            ImGui.BulletText("Best used with Panoramic skybox type");
            ImGui.BulletText("Adjust exposure for proper brightness");

            // Utility buttons
            ImGui.Spacing();
            ImGui.Separator();
            if (ImGui.Button("Reveal in Explorer"))
                RevealFile(rec.Path);
            ImGui.SameLine();
            if (ImGui.Button("Open Externally"))
                OpenFile(rec.Path);
            ImGui.SameLine();
            if (ImGui.Button("Copy GUID"))
                ImGui.SetClipboardText(guid.ToString());
        }

        private static void CreateSkyboxMaterialFromHDR(Guid hdrGuid, AssetDatabase.AssetRecord hdrRecord)
        {
            try
            {
                // Create a new skybox material
                var skyboxMat = new SkyboxMaterialAsset
                {
                    Guid = Guid.NewGuid(),
                    Name = $"Skybox_{Path.GetFileNameWithoutExtension(hdrRecord.Path)}",
                    Type = SkyboxType.Panoramic,
                    PanoramicTexture = hdrGuid,
                    PanoramicTint = new float[] { 1.0f, 1.0f, 1.0f, 1.0f },
                    PanoramicExposure = 1.0f,
                    PanoramicRotation = 0.0f,
                    Mapping = PanoramicMapping.Latitude_Longitude_Layout,
                    ImageType = PanoramicImageType.Degrees360
                };

                // Save the skybox material
                string skyboxPath = Path.ChangeExtension(hdrRecord.Path, ".skymat");
                skyboxPath = GetUniqueFilePath(skyboxPath);

                SkyboxMaterialAsset.Save(skyboxPath, skyboxMat);

                // Create meta file
                string metaPath = skyboxPath + ".meta";
                var metaContent = new
                {
                    guid = skyboxMat.Guid.ToString(),
                    type = "SkyboxMaterial"
                };
                File.WriteAllText(metaPath, System.Text.Json.JsonSerializer.Serialize(metaContent, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

                // Refresh asset database
                AssetDatabase.Refresh();

            }
            catch (Exception)
            {
            }
        }

        private static void SetAsEnvironmentSkybox(Guid hdrGuid, AssetDatabase.AssetRecord hdrRecord)
        {
            // This would need access to the current scene and environment settings
            // For now, just log the intention
        }

        private static string GetUniqueFilePath(string basePath)
        {
            if (!File.Exists(basePath))
                return basePath;

            string directory = Path.GetDirectoryName(basePath)!;
            string nameWithoutExt = Path.GetFileNameWithoutExtension(basePath);
            string extension = Path.GetExtension(basePath);

            int counter = 1;
            string newPath;
            do
            {
                newPath = Path.Combine(directory, $"{nameWithoutExt}_{counter}{extension}");
                counter++;
            } while (File.Exists(newPath));

            return newPath;
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
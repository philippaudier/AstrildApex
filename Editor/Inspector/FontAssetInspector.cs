using ImGuiNET;
using System;
using System.Numerics;
using System.IO;

namespace Editor.Inspector
{
    public static class FontAssetInspector
    {
        // Draw from GUID (loads FontAsset from file)
        public static void Draw(Guid fontAssetGuid)
        {
            if (!Engine.Assets.AssetDatabase.TryGet(fontAssetGuid, out var record))
            {
                ImGui.TextDisabled("FontAsset not found in database.");
                return;
            }

            // Load the FontAsset
            var fontAsset = Engine.Assets.FontAsset.Load(record.Path);
            if (fontAsset == null)
            {
                ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f), "Failed to load FontAsset!");
                ImGui.TextDisabled($"Path: {record.Path}");
                return;
            }

            // Draw the inspector
            bool modified = DrawInternal(fontAsset);

            // Save if modified
            if (modified)
            {
                try
                {
                    fontAsset.Save(record.Path);
                    Engine.Utils.DebugLogger.Log($"[FontAssetInspector] Saved FontAsset: {record.Path}");
                }
                catch (Exception ex)
                {
                    Engine.Utils.DebugLogger.Log($"[FontAssetInspector] ERROR: Failed to save FontAsset: {ex.Message}");
                }
            }
        }

        // Legacy method for direct FontAsset editing
        public static void Draw(Engine.Assets.FontAsset font)
        {
            if (font == null) return;
            DrawInternal(font);
        }

        // Internal draw method that returns whether the FontAsset was modified
        private static bool DrawInternal(Engine.Assets.FontAsset font)
        {
            bool modified = false;

            ImGui.Text("Font Asset");
            ImGui.Indent();
            ImGui.PushItemWidth(250f);

            // Name
            string name = font.Name ?? "";
            if (ImGui.InputText("Name", ref name, 128))
            {
                font.Name = name;
                modified = true;
            }

            ImGui.Separator();
            ImGui.Text("Font Variants:");

            // Regular font
            var newRegularPath = DrawFontVariantField("Regular", font.RegularPath);
            if (newRegularPath != font.RegularPath)
            {
                font.RegularPath = newRegularPath;
                modified = true;
            }

            // Bold font
            var newBoldPath = DrawFontVariantField("Bold", font.BoldPath);
            if (newBoldPath != font.BoldPath)
            {
                font.BoldPath = newBoldPath;
                modified = true;
            }

            // Italic font
            var newItalicPath = DrawFontVariantField("Italic", font.ItalicPath);
            if (newItalicPath != font.ItalicPath)
            {
                font.ItalicPath = newItalicPath;
                modified = true;
            }

            // Bold + Italic font
            var newBoldItalicPath = DrawFontVariantField("Bold Italic", font.BoldItalicPath);
            if (newBoldItalicPath != font.BoldItalicPath)
            {
                font.BoldItalicPath = newBoldItalicPath;
                modified = true;
            }

            ImGui.Separator();
            ImGui.Text("Font Settings:");

            // Default size
            float defaultSize = font.DefaultSize;
            if (ImGui.DragFloat("Default Size", ref defaultSize, 0.5f, 4f, 128f))
            {
                font.DefaultSize = defaultSize;
                modified = true;
            }

            // Line height
            float lineHeight = font.LineHeight;
            if (ImGui.DragFloat("Line Height", ref lineHeight, 0.01f, 0.5f, 3f))
            {
                font.LineHeight = lineHeight;
                modified = true;
            }

            ImGui.Separator();

            // Preview section
            if (ImGui.CollapsingHeader("Font Preview"))
            {
                DrawFontPreview(font);
            }

            // Glyph atlas info
            if (ImGui.CollapsingHeader("Glyph Atlas Info"))
            {
                ImGui.Text("Atlas generation will be implemented");
                ImGui.TextDisabled("TODO: Display atlas texture and glyph metrics");
            }

            ImGui.PopItemWidth();
            ImGui.Unindent();

            return modified;
        }

        private static string? DrawFontVariantField(string label, string? path)
        {
            ImGui.Text($"{label}:");

            string? result = path;

            if (!string.IsNullOrEmpty(path))
            {
                // Show file name
                var fileName = Path.GetFileName(path);
                ImGui.TextColored(new Vector4(0.4f, 0.8f, 0.4f, 1f), fileName);

                // Validate file exists
                if (!File.Exists(path))
                {
                    ImGui.SameLine();
                    ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f), "(Missing!)");
                }

                ImGui.SameLine();
                if (ImGui.Button($"Clear##{label}"))
                {
                    result = null;
                }

                ImGui.SameLine();
                if (ImGui.Button($"Browse...##{label}"))
                {
                    // TODO: Open file dialog
                    // For now, just show the path input
                }
            }
            else
            {
                ImGui.TextDisabled("None (drag .ttf here)");
                ImGui.SameLine();
                if (ImGui.Button($"Browse...##{label}"))
                {
                    // TODO: Open file dialog
                }
            }

            // Drag & drop support for .ttf files
            if (ImGui.BeginDragDropTarget())
            {
                // Try multi-asset payload first (from AssetsPanel)
                var multiPayload = ImGui.AcceptDragDropPayload("ASSET_MULTI");
                unsafe
                {
                    if (multiPayload.NativePtr != null && multiPayload.Data != IntPtr.Zero && multiPayload.DataSize >= 16)
                    {
                        try
                        {
                            // Extract first GUID from the payload
                            var span = new System.ReadOnlySpan<byte>((void*)multiPayload.Data, 16);
                            var guid = new Guid(span);

                            // Verify it's a TrueType font
                            if (Engine.Assets.AssetDatabase.TryGet(guid, out var record) &&
                                string.Equals(record.Type, "TrueTypeFont", StringComparison.OrdinalIgnoreCase))
                            {
                                result = record.Path;
                            }
                        }
                        catch { }
                    }
                }

                // Also support legacy single GUID payload
                var singlePayload = ImGui.AcceptDragDropPayload("ASSET_GUID");
                unsafe
                {
                    if (singlePayload.NativePtr != null && singlePayload.Data != IntPtr.Zero)
                    {
                        try
                        {
                            var guidBytes = new byte[16];
                            System.Runtime.InteropServices.Marshal.Copy(singlePayload.Data, guidBytes, 0, 16);
                            var guid = new Guid(guidBytes);

                            // Verify it's a TrueType font
                            if (Engine.Assets.AssetDatabase.TryGet(guid, out var record) &&
                                string.Equals(record.Type, "TrueTypeFont", StringComparison.OrdinalIgnoreCase))
                            {
                                result = record.Path;
                            }
                        }
                        catch { }
                    }
                }
                ImGui.EndDragDropTarget();
            }

            // Path input (fallback)
            if (ImGui.TreeNode($"Manual Path##{label}"))
            {
                string pathStr = path ?? "";
                if (ImGui.InputText($"##{label}Path", ref pathStr, 512))
                {
                    result = string.IsNullOrWhiteSpace(pathStr) ? null : pathStr;
                }
                ImGui.TreePop();
            }

            return result;
        }

        private static void DrawFontPreview(Engine.Assets.FontAsset font)
        {
            // Preview text
            const string previewText = "The quick brown fox jumps over the lazy dog\nABCDEFGHIJKLMNOPQRSTUVWXYZ\nabcdefghijklmnopqrstuvwxyz\n0123456789 !@#$%^&*()";

            // Preview at different sizes
            ImGui.Text("Preview:");

            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "Size 12:");
            ImGui.TextWrapped(previewText);

            ImGui.Spacing();
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "Size 18:");
            ImGui.TextWrapped(previewText);

            ImGui.Spacing();
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "Size 24:");
            ImGui.TextWrapped(previewText);

            ImGui.Separator();
            ImGui.TextDisabled("Note: Actual font rendering will be implemented with atlas generation");
        }
    }
}

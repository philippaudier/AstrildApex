using ImGuiNET;
using System.Numerics;

namespace Editor.Inspector
{
    public static class UITextInspector
    {
        public static void Draw(Engine.Components.UI.UITextComponent txt)
        {
            ImGui.Text("UIText");
            ImGui.Indent();

            // Text string
            var textBuf = txt.Text ?? string.Empty;
            var size = new System.Numerics.Vector2(0, 18f * 6);
            if (ImGui.InputTextMultiline("Text", ref textBuf, 1024, size)) txt.Text = textBuf;

            ImGui.Separator();

            // Font Asset selection - drag & drop
            ImGui.Text("Font:");
            if (txt.FontAssetGuid.HasValue)
            {
                var fontName = $"Font {txt.FontAssetGuid.Value.ToString().Substring(0, 8)}...";
                // TODO: Query asset database for actual name
                ImGui.TextColored(new Vector4(0.4f, 0.8f, 0.4f, 1f), fontName);
                ImGui.SameLine();
                if (ImGui.Button("Clear"))
                {
                    txt.FontAssetGuid = null;
                }
            }
            else
            {
                ImGui.TextDisabled("Default (drag font here)");
            }

            // Drag and drop area - support both single and multi-asset drag
            if (ImGui.BeginDragDropTarget())
            {
                // Try multi-asset payload first (from AssetsPanel)
                var multiPayload = ImGui.AcceptDragDropPayload("ASSET_MULTI");
                unsafe
                {
                    if (multiPayload.NativePtr != null && multiPayload.Data != System.IntPtr.Zero && multiPayload.DataSize >= 16)
                    {
                        try
                        {
                            // Extract first GUID from the payload
                            var span = new System.ReadOnlySpan<byte>((void*)multiPayload.Data, 16);
                            var guid = new System.Guid(span);

                            // Verify it's a font asset
                            if (Engine.Assets.AssetDatabase.TryGet(guid, out var record) &&
                                (string.Equals(record.Type, "FontAsset", System.StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(record.Type, "Font", System.StringComparison.OrdinalIgnoreCase)))
                            {
                                txt.FontAssetGuid = guid;
                            }
                        }
                        catch { }
                    }
                }

                // Also support legacy single GUID payload
                var singlePayload = ImGui.AcceptDragDropPayload("ASSET_GUID");
                unsafe
                {
                    if (singlePayload.NativePtr != null && singlePayload.Data != System.IntPtr.Zero)
                    {
                        try
                        {
                            var guidBytes = new byte[16];
                            System.Runtime.InteropServices.Marshal.Copy(singlePayload.Data, guidBytes, 0, 16);
                            var guid = new System.Guid(guidBytes);

                            // Verify it's a font asset
                            if (Engine.Assets.AssetDatabase.TryGet(guid, out var record) &&
                                (string.Equals(record.Type, "FontAsset", System.StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(record.Type, "Font", System.StringComparison.OrdinalIgnoreCase)))
                            {
                                txt.FontAssetGuid = guid;
                            }
                        }
                        catch { }
                    }
                }
                ImGui.EndDragDropTarget();
            }

            ImGui.Separator();

            // Color
            var col = txt.Color;
            var r = ((col >> 16) & 0xFF) / 255.0f;
            var g = ((col >> 8) & 0xFF) / 255.0f;
            var b = (col & 0xFF) / 255.0f;
            var a = ((col >> 24) & 0xFF) / 255.0f;
            var c = new Vector4(r, g, b, a);
            if (ImGui.ColorEdit4("Color", ref c))
            {
                uint ir = (uint)(c.X * 255) & 0xFF;
                uint ig = (uint)(c.Y * 255) & 0xFF;
                uint ib = (uint)(c.Z * 255) & 0xFF;
                uint ia = (uint)(c.W * 255) & 0xFF;
                txt.Color = (ia << 24) | (ir << 16) | (ig << 8) | ib;
            }

            float fs = txt.FontSize;
            if (ImGui.DragFloat("Font Size", ref fs, 0.5f, 4f, 128f)) txt.FontSize = fs;

            // Font Style
            ImGui.Text("Font Style:");
            bool bold = txt.Bold;
            bool italic = txt.Italic;
            if (ImGui.Checkbox("Bold", ref bold)) txt.Bold = bold;
            ImGui.SameLine();
            if (ImGui.Checkbox("Italic", ref italic)) txt.Italic = italic;

            // Alignment
            ImGui.Text("Alignment:");
            int align = (int)txt.Alignment;
            if (ImGui.Combo("##Alignment", ref align, "Left\0Center\0Right\0"))
                txt.Alignment = (Engine.UI.TextAlignment)align;

            // Flexbox Layout
            ImGui.Separator();
            bool useFlex = txt.UseFlexLayout;
            if (ImGui.Checkbox("Use Flexbox Layout", ref useFlex))
                txt.UseFlexLayout = useFlex;

            if (txt.UseFlexLayout)
            {
                CanvasInspector.DrawFlexLayout(txt.FlexLayout);
            }

            // Use shared RectTransform drawer from CanvasInspector
            CanvasInspector.DrawRectTransform(txt.RectTransform);

            ImGui.Unindent();
        }
    }
}

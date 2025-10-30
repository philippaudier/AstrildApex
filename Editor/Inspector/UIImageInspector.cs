using ImGuiNET;
using System.Numerics;

namespace Editor.Inspector
{
    public static class UIImageInspector
    {
        public static void Draw(Engine.Components.UI.UIImageComponent img)
        {
            ImGui.Text("UIImage");
            ImGui.Indent();

            // Texture selection - drag & drop or browse
            ImGui.Text("Texture:");

            // Create a visible drop zone box
            var dropZoneMin = ImGui.GetCursorScreenPos();
            var dropZoneSize = new Vector2(ImGui.GetContentRegionAvail().X, 60);
            var dropZoneMax = new Vector2(dropZoneMin.X + dropZoneSize.X, dropZoneMin.Y + dropZoneSize.Y);

            var drawList = ImGui.GetWindowDrawList();

            if (img.TextureGuid.HasValue)
            {
                // Try to find the texture name from the asset database
                string textureName = "Unknown Texture";
                if (Engine.Assets.AssetDatabase.TryGet(img.TextureGuid.Value, out var record))
                {
                    textureName = System.IO.Path.GetFileNameWithoutExtension(record.Path);
                }

                // Draw texture info box
                drawList.AddRectFilled(dropZoneMin, dropZoneMax, 0xFF2A2A2A);
                drawList.AddRect(dropZoneMin, dropZoneMax, 0xFF4A8A4A, 0, 0, 2.0f);

                ImGui.SetCursorScreenPos(new Vector2(dropZoneMin.X + 8, dropZoneMin.Y + 8));
                ImGui.TextColored(new Vector4(0.4f, 0.8f, 0.4f, 1f), textureName);
                ImGui.SetCursorScreenPos(new Vector2(dropZoneMin.X + 8, dropZoneMin.Y + 30));
                ImGui.TextDisabled($"GUID: {img.TextureGuid.Value.ToString().Substring(0, 8)}...");

                // Clear button
                ImGui.SetCursorScreenPos(new Vector2(dropZoneMax.X - 70, dropZoneMin.Y + 15));
                if (ImGui.Button("Clear", new Vector2(60, 30)))
                {
                    img.TextureGuid = null;
                }
            }
            else
            {
                // Draw empty drop zone
                drawList.AddRectFilled(dropZoneMin, dropZoneMax, 0xFF1A1A1A);
                drawList.AddRect(dropZoneMin, dropZoneMax, 0xFF666666, 0, 0, 2.0f);

                // Draw centered text
                var centerText = "Drag Texture Here";
                var textSize = ImGui.CalcTextSize(centerText);
                ImGui.SetCursorScreenPos(new Vector2(
                    dropZoneMin.X + (dropZoneSize.X - textSize.X) * 0.5f,
                    dropZoneMin.Y + (dropZoneSize.Y - textSize.Y) * 0.5f
                ));
                ImGui.TextDisabled(centerText);
            }

            // Move cursor past the drop zone
            ImGui.SetCursorScreenPos(new Vector2(dropZoneMin.X, dropZoneMax.Y + 4));
            ImGui.Dummy(new Vector2(0, 0));

            // Drag and drop area - support both single and multi-asset drag
            ImGui.SetCursorScreenPos(dropZoneMin);
            ImGui.InvisibleButton("##dropzone", dropZoneSize);
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

                            // Verify it's a texture
                            if (Engine.Assets.AssetDatabase.TryGet(guid, out var record) &&
                                (string.Equals(record.Type, "Texture2D", System.StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(record.Type, "Texture", System.StringComparison.OrdinalIgnoreCase)))
                            {
                                img.TextureGuid = guid;
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

                            // Verify it's a texture
                            if (Engine.Assets.AssetDatabase.TryGet(guid, out var record) &&
                                (string.Equals(record.Type, "Texture2D", System.StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(record.Type, "Texture", System.StringComparison.OrdinalIgnoreCase)))
                            {
                                img.TextureGuid = guid;
                            }
                        }
                        catch { }
                    }
                }
                ImGui.EndDragDropTarget();
            }

            // Manual GUID input (fallback)
            if (ImGui.TreeNode("Manual GUID Input"))
            {
                var guidStr = img.TextureGuid.HasValue ? img.TextureGuid.Value.ToString() : string.Empty;
                if (ImGui.InputText("##TextureGUID", ref guidStr, 64))
                {
                    if (System.Guid.TryParse(guidStr, out var gguid)) img.TextureGuid = gguid;
                }
                ImGui.TreePop();
            }

            ImGui.Separator();

            // Color
            var col = img.Color;
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
                img.Color = (ia << 24) | (ir << 16) | (ig << 8) | ib;
            }

            // Flexbox Layout
            ImGui.Separator();
            bool useFlex = img.UseFlexLayout;
            if (ImGui.Checkbox("Use Flexbox Layout", ref useFlex))
                img.UseFlexLayout = useFlex;

            if (img.UseFlexLayout)
            {
                CanvasInspector.DrawFlexLayout(img.FlexLayout);
            }

            // Use shared RectTransform drawer from CanvasInspector
            CanvasInspector.DrawRectTransform(img.RectTransform);

            ImGui.Unindent();
        }
    }
}

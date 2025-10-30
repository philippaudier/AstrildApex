using System.Numerics;
using ImGuiNET;

namespace Editor.Inspector
{
    public static class CanvasInspector
    {
        public static void Draw(Engine.Components.UI.CanvasComponent canvas)
        {
            ImGui.Text("Canvas");
            ImGui.Indent();
            // Render mode and sort order
            var mode = (int)canvas.RenderMode;
            var modes = new[] { "ScreenSpaceOverlay", "ScreenSpaceCamera", "World" };
            if (ImGui.Combo("Render Mode", ref mode, modes, modes.Length))
                canvas.RenderMode = (Engine.UI.RenderMode)mode;

            int so = canvas.SortOrder;
            if (ImGui.DragInt("Sort Order", ref so, 1, -1000, 1000)) canvas.SortOrder = so;

            // Flexbox Layout
            ImGui.Separator();
            bool useFlex = canvas.UseFlexLayout;
            if (ImGui.Checkbox("Use Flexbox Layout", ref useFlex))
                canvas.UseFlexLayout = useFlex;

            if (canvas.UseFlexLayout)
            {
                DrawFlexLayout(canvas.FlexLayout);
            }

            // RectTransform basic
            DrawRectTransform(canvas.RectTransform);

            ImGui.Unindent();
        }

        public static void DrawRectTransform(Engine.UI.RectTransform rt)
        {
            ImGui.Separator();
            ImGui.Text("Rect Transform");

            // Anchor Presets (like Unity)
            if (ImGui.CollapsingHeader("Anchor Presets"))
            {
                DrawAnchorPresets(rt);
            }

            ImGui.PushItemWidth(180f);
            var aMin = rt.AnchorMin;
            var aMax = rt.AnchorMax;
            var piv = rt.Pivot;
            var pos = rt.AnchoredPosition;
            var size = rt.SizeDelta;

            Vector2 am = new Vector2(aMin.X, aMin.Y);
            Vector2 ax = new Vector2(aMax.X, aMax.Y);
            Vector2 pv = new Vector2(piv.X, piv.Y);
            Vector2 ap = new Vector2(pos.X, pos.Y);
            Vector2 sz = new Vector2(size.X, size.Y);

            if (ImGui.DragFloat2("Anchor Min", ref am, 0.01f, 0f, 1f)) { rt.AnchorMin = new System.Numerics.Vector2(am.X, am.Y); }
            if (ImGui.DragFloat2("Anchor Max", ref ax, 0.01f, 0f, 1f)) { rt.AnchorMax = new System.Numerics.Vector2(ax.X, ax.Y); }
            if (ImGui.DragFloat2("Pivot", ref pv, 0.01f, 0f, 1f)) { rt.Pivot = new System.Numerics.Vector2(pv.X, pv.Y); }
            if (ImGui.DragFloat2("Anchored Pos", ref ap)) { rt.AnchoredPosition = new System.Numerics.Vector2(ap.X, ap.Y); }
            if (ImGui.DragFloat2("Size Delta", ref sz)) { rt.SizeDelta = new System.Numerics.Vector2(sz.X, sz.Y); }

            ImGui.PopItemWidth();
        }

        private static void DrawAnchorPresets(Engine.UI.RectTransform rt)
        {
            // Grid of anchor presets like Unity
            const float buttonSize = 30f;
            const int cols = 4;

            string[] labels = {
                "TL", "T", "TR", "Stretch",
                "L", "Center", "R", "Stretch",
                "BL", "B", "BR", "Stretch",
                "Stretch", "Stretch", "Stretch", "Fill"
            };

            for (int i = 0; i < 16; i++)
            {
                if (i > 0 && i % cols != 0) ImGui.SameLine();

                if (ImGui.Button(labels[i], new Vector2(buttonSize, buttonSize)))
                {
                    ApplyAnchorPreset(rt, i);
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(GetPresetTooltip(i));
                }
            }
        }

        private static void ApplyAnchorPreset(Engine.UI.RectTransform rt, int preset)
        {
            switch (preset)
            {
                case 0: // Top-Left
                    rt.AnchorMin = new Vector2(0f, 1f);
                    rt.AnchorMax = new Vector2(0f, 1f);
                    rt.Pivot = new Vector2(0f, 1f);
                    break;
                case 1: // Top-Center
                    rt.AnchorMin = new Vector2(0.5f, 1f);
                    rt.AnchorMax = new Vector2(0.5f, 1f);
                    rt.Pivot = new Vector2(0.5f, 1f);
                    break;
                case 2: // Top-Right
                    rt.AnchorMin = new Vector2(1f, 1f);
                    rt.AnchorMax = new Vector2(1f, 1f);
                    rt.Pivot = new Vector2(1f, 1f);
                    break;
                case 3: // Top Stretch
                    rt.AnchorMin = new Vector2(0f, 1f);
                    rt.AnchorMax = new Vector2(1f, 1f);
                    rt.Pivot = new Vector2(0.5f, 1f);
                    break;
                case 4: // Middle-Left
                    rt.AnchorMin = new Vector2(0f, 0.5f);
                    rt.AnchorMax = new Vector2(0f, 0.5f);
                    rt.Pivot = new Vector2(0f, 0.5f);
                    break;
                case 5: // Center
                    rt.AnchorMin = new Vector2(0.5f, 0.5f);
                    rt.AnchorMax = new Vector2(0.5f, 0.5f);
                    rt.Pivot = new Vector2(0.5f, 0.5f);
                    break;
                case 6: // Middle-Right
                    rt.AnchorMin = new Vector2(1f, 0.5f);
                    rt.AnchorMax = new Vector2(1f, 0.5f);
                    rt.Pivot = new Vector2(1f, 0.5f);
                    break;
                case 7: // Middle Stretch
                    rt.AnchorMin = new Vector2(0f, 0.5f);
                    rt.AnchorMax = new Vector2(1f, 0.5f);
                    rt.Pivot = new Vector2(0.5f, 0.5f);
                    break;
                case 8: // Bottom-Left
                    rt.AnchorMin = new Vector2(0f, 0f);
                    rt.AnchorMax = new Vector2(0f, 0f);
                    rt.Pivot = new Vector2(0f, 0f);
                    break;
                case 9: // Bottom-Center
                    rt.AnchorMin = new Vector2(0.5f, 0f);
                    rt.AnchorMax = new Vector2(0.5f, 0f);
                    rt.Pivot = new Vector2(0.5f, 0f);
                    break;
                case 10: // Bottom-Right
                    rt.AnchorMin = new Vector2(1f, 0f);
                    rt.AnchorMax = new Vector2(1f, 0f);
                    rt.Pivot = new Vector2(1f, 0f);
                    break;
                case 11: // Bottom Stretch
                    rt.AnchorMin = new Vector2(0f, 0f);
                    rt.AnchorMax = new Vector2(1f, 0f);
                    rt.Pivot = new Vector2(0.5f, 0f);
                    break;
                case 12: // Left Stretch
                    rt.AnchorMin = new Vector2(0f, 0f);
                    rt.AnchorMax = new Vector2(0f, 1f);
                    rt.Pivot = new Vector2(0f, 0.5f);
                    break;
                case 13: // Center Stretch
                    rt.AnchorMin = new Vector2(0.5f, 0f);
                    rt.AnchorMax = new Vector2(0.5f, 1f);
                    rt.Pivot = new Vector2(0.5f, 0.5f);
                    break;
                case 14: // Right Stretch
                    rt.AnchorMin = new Vector2(1f, 0f);
                    rt.AnchorMax = new Vector2(1f, 1f);
                    rt.Pivot = new Vector2(1f, 0.5f);
                    break;
                case 15: // Fill
                    rt.AnchorMin = new Vector2(0f, 0f);
                    rt.AnchorMax = new Vector2(1f, 1f);
                    rt.Pivot = new Vector2(0.5f, 0.5f);
                    break;
            }
        }

        private static string GetPresetTooltip(int preset)
        {
            return preset switch
            {
                0 => "Top-Left",
                1 => "Top-Center",
                2 => "Top-Right",
                3 => "Top Stretch (Horizontal)",
                4 => "Middle-Left",
                5 => "Center",
                6 => "Middle-Right",
                7 => "Middle Stretch (Horizontal)",
                8 => "Bottom-Left",
                9 => "Bottom-Center",
                10 => "Bottom-Right",
                11 => "Bottom Stretch (Horizontal)",
                12 => "Left Stretch (Vertical)",
                13 => "Center Stretch (Vertical)",
                14 => "Right Stretch (Vertical)",
                15 => "Fill (Stretch Both)",
                _ => ""
            };
        }

        public static void DrawFlexLayout(Engine.UI.FlexLayout flex)
        {
            if (ImGui.CollapsingHeader("Flexbox Layout", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Indent();
                ImGui.PushItemWidth(180f);

                // Container properties
                ImGui.Text("Container Properties:");
                ImGui.Separator();

                int dir = (int)flex.Direction;
                if (ImGui.Combo("Flex Direction", ref dir, "Row\0Row Reverse\0Column\0Column Reverse\0"))
                    flex.Direction = (Engine.UI.FlexDirection)dir;

                int justify = (int)flex.JustifyContent;
                if (ImGui.Combo("Justify Content", ref justify, "Flex Start\0Flex End\0Center\0Space Between\0Space Around\0Space Evenly\0"))
                    flex.JustifyContent = (Engine.UI.JustifyContent)justify;

                int align = (int)flex.AlignItems;
                if (ImGui.Combo("Align Items", ref align, "Flex Start\0Flex End\0Center\0Stretch\0Baseline\0"))
                    flex.AlignItems = (Engine.UI.AlignItems)align;

                int wrap = (int)flex.Wrap;
                if (ImGui.Combo("Flex Wrap", ref wrap, "No Wrap\0Wrap\0Wrap Reverse\0"))
                    flex.Wrap = (Engine.UI.FlexWrap)wrap;

                float gap = flex.Gap;
                if (ImGui.DragFloat("Gap", ref gap, 0.5f, 0f, 100f))
                    flex.Gap = gap;

                ImGui.Separator();
                ImGui.Text("Item Properties:");
                ImGui.Separator();

                float grow = flex.FlexGrow;
                if (ImGui.DragFloat("Flex Grow", ref grow, 0.1f, 0f, 10f))
                    flex.FlexGrow = grow;

                float shrink = flex.FlexShrink;
                if (ImGui.DragFloat("Flex Shrink", ref shrink, 0.1f, 0f, 10f))
                    flex.FlexShrink = shrink;

                float basis = flex.FlexBasis;
                if (ImGui.DragFloat("Flex Basis", ref basis, 1f, -1f, 1000f, basis < 0 ? "Auto" : "%.1f"))
                    flex.FlexBasis = basis;

                int alignSelf = (int)flex.AlignSelf;
                if (ImGui.Combo("Align Self", ref alignSelf, "Auto\0Flex Start\0Flex End\0Center\0Stretch\0Baseline\0"))
                    flex.AlignSelf = (Engine.UI.AlignSelf)alignSelf;

                int order = flex.Order;
                if (ImGui.DragInt("Order", ref order, 1, -100, 100))
                    flex.Order = order;

                ImGui.Separator();
                ImGui.Text("Padding:");
                ImGui.Separator();

                float padL = flex.PaddingLeft;
                float padR = flex.PaddingRight;
                float padT = flex.PaddingTop;
                float padB = flex.PaddingBottom;

                if (ImGui.DragFloat("Left", ref padL, 0.5f, 0f, 100f)) flex.PaddingLeft = padL;
                if (ImGui.DragFloat("Right", ref padR, 0.5f, 0f, 100f)) flex.PaddingRight = padR;
                if (ImGui.DragFloat("Top", ref padT, 0.5f, 0f, 100f)) flex.PaddingTop = padT;
                if (ImGui.DragFloat("Bottom", ref padB, 0.5f, 0f, 100f)) flex.PaddingBottom = padB;

                ImGui.PopItemWidth();
                ImGui.Unindent();
            }
        }
    }
}

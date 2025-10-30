using ImGuiNET;
using System.Numerics;

namespace Editor.Inspector
{
    public static class UIButtonInspector
    {
        public static void Draw(Engine.Components.UI.UIButtonComponent btn)
        {
            ImGui.Text("UIButton");
            ImGui.Indent();

            var label = btn.Text ?? "Button";
            var labelBuf = label;
            if (ImGui.InputText("Label", ref labelBuf, 256)) btn.Text = labelBuf;

            // Colors for normal/hover/pressed
            var cN = ColorFromUint(btn.NormalColor);
            var cH = ColorFromUint(btn.HoverColor);
            var cP = ColorFromUint(btn.PressedColor);
            if (ImGui.ColorEdit4("Normal", ref cN)) btn.NormalColor = UintFromColor(cN);
            if (ImGui.ColorEdit4("Hover", ref cH)) btn.HoverColor = UintFromColor(cH);
            if (ImGui.ColorEdit4("Pressed", ref cP)) btn.PressedColor = UintFromColor(cP);

            DrawRectTransform(btn.RectTransform);

            ImGui.Unindent();
        }

        private static System.Numerics.Vector4 ColorFromUint(uint col)
        {
            var r = ((col >> 16) & 0xFF) / 255.0f;
            var g = ((col >> 8) & 0xFF) / 255.0f;
            var b = (col & 0xFF) / 255.0f;
            var a = ((col >> 24) & 0xFF) / 255.0f;
            return new System.Numerics.Vector4(r, g, b, a);
        }
        private static uint UintFromColor(System.Numerics.Vector4 c)
        {
            uint ir = (uint)(c.X * 255) & 0xFF;
            uint ig = (uint)(c.Y * 255) & 0xFF;
            uint ib = (uint)(c.Z * 255) & 0xFF;
            uint ia = (uint)(c.W * 255) & 0xFF;
            return (ia << 24) | (ir << 16) | (ig << 8) | ib;
        }

        private static void DrawRectTransform(Engine.UI.RectTransform rt)
        {
            ImGui.PushItemWidth(180f);
            var aMin = rt.AnchorMin;
            var aMax = rt.AnchorMax;
            var piv = rt.Pivot;
            var pos = rt.AnchoredPosition;
            var size = rt.SizeDelta;

            var am = new Vector2(aMin.X, aMin.Y);
            var ax = new Vector2(aMax.X, aMax.Y);
            var pv = new Vector2(piv.X, piv.Y);
            var ap = new Vector2(pos.X, pos.Y);
            var sz = new Vector2(size.X, size.Y);

            if (ImGui.DragFloat2("Anchor Min", ref am)) { rt.AnchorMin = new System.Numerics.Vector2(am.X, am.Y); }
            if (ImGui.DragFloat2("Anchor Max", ref ax)) { rt.AnchorMax = new System.Numerics.Vector2(ax.X, ax.Y); }
            if (ImGui.DragFloat2("Pivot", ref pv)) { rt.Pivot = new System.Numerics.Vector2(pv.X, pv.Y); }
            if (ImGui.DragFloat2("Anchored Pos", ref ap)) { rt.AnchoredPosition = new System.Numerics.Vector2(ap.X, ap.Y); }
            if (ImGui.DragFloat2("Size Delta", ref sz)) { rt.SizeDelta = new System.Numerics.Vector2(sz.X, sz.Y); }

            ImGui.PopItemWidth();
        }
    }
}

using ImGuiNET;
using Engine.Physics;
using Editor.Inspector;
using Editor.Themes;
using NumVector3 = System.Numerics.Vector3;
using OtkVector3 = OpenTK.Mathematics.Vector3;

namespace Editor.Inspector
{
    public static class BoxColliderInspector
    {
        private static UITheme UI => ThemeManager.UI;

        public static void Draw(BoxCollider box)
        {
            if (box?.Entity == null) return;

            // === SHAPE SECTION ===
            if (InspectorWidgets.Section("Shape", defaultOpen: true))
            {
                var size = new NumVector3(box.Size.X, box.Size.Y, box.Size.Z);
                ImGui.Text("Size");
                ImGui.DragFloat3("##Size", ref size, 0.1f, 0.001f);
                box.Size = new OtkVector3(size.X, size.Y, size.Z);

                var center = new NumVector3(box.Center.X, box.Center.Y, box.Center.Z);
                ImGui.Text("Center");
                ImGui.DragFloat3("##Center", ref center, 0.1f);
                box.Center = new OtkVector3(center.X, center.Y, center.Z);
                
                InspectorWidgets.EndSection();
            }

            // === COLLISION SECTION ===
            if (InspectorWidgets.Section("Collision", defaultOpen: true))
            {
                ImGui.Text("Layer");
                ImGui.DragInt("##Layer", ref box.Layer, 1.0f, 0, 31);

                ImGui.Checkbox("Is Trigger", ref box.IsTrigger);
                
                InspectorWidgets.EndSection();
            }

            // === DEBUG INFO ===
            if (InspectorWidgets.Section("Debug Info", defaultOpen: false))
            {
                var worldSize = box.WorldSize;
                ImGui.Text($"World Size: ({worldSize.X:F2}, {worldSize.Y:F2}, {worldSize.Z:F2})");

                var worldCenter = box.WorldCenter;
                ImGui.Text($"World Center: ({worldCenter.X:F2}, {worldCenter.Y:F2}, {worldCenter.Z:F2})");
                
                InspectorWidgets.EndSection();
            }
        }
    }
}

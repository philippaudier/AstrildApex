using ImGuiNET;
using Engine.Physics;
using Editor.Inspector;
using Editor.Themes;
using NumVector3 = System.Numerics.Vector3;
using OtkVector3 = OpenTK.Mathematics.Vector3;

namespace Editor.Inspector
{
    public static class CapsuleColliderInspector
    {
        private static UITheme UI => ThemeManager.UI;

        public static void Draw(CapsuleCollider capsule)
        {
            if (capsule?.Entity == null) return;

            // === SHAPE SECTION ===
            if (InspectorWidgets.Section("Shape", defaultOpen: true))
            {
                ImGui.Text("Height");
                float height = capsule.Height;
                ImGui.DragFloat("##Height", ref height, 0.1f, capsule.Radius * 2.0f + 0.001f);
                capsule.Height = height;

                ImGui.Text("Radius");
                float radius = capsule.Radius;
                ImGui.DragFloat("##Radius", ref radius, 0.05f, 0.001f);
                capsule.Radius = radius;

                ImGui.Text("Direction");
                int dir = capsule.Direction;
                string[] dirLabels = { "X-Axis", "Y-Axis (Default)", "Z-Axis" };
                if (ImGui.Combo("##Direction", ref dir, dirLabels, dirLabels.Length))
                {
                    capsule.Direction = dir;
                }

                var center = new NumVector3(capsule.Center.X, capsule.Center.Y, capsule.Center.Z);
                ImGui.Text("Center");
                ImGui.DragFloat3("##Center", ref center, 0.1f);
                capsule.Center = new OtkVector3(center.X, center.Y, center.Z);
                InspectorWidgets.EndSection();
            }

            // === COLLISION SECTION ===
            if (InspectorWidgets.Section("Collision", defaultOpen: true))
            {
                ImGui.Text("Layer");
                ImGui.DragInt("##Layer", ref capsule.Layer, 1.0f, 0, 31);

                ImGui.Checkbox("Is Trigger", ref capsule.IsTrigger);
                InspectorWidgets.EndSection();
            }

            // === DEBUG INFO ===
            if (InspectorWidgets.Section("Debug Info", defaultOpen: false))
            {
                ImGui.Text($"World Height: {capsule.WorldHeight:F2}");
                ImGui.Text($"World Radius: {capsule.WorldRadius:F2}");

                var worldCenter = capsule.WorldCenter;
                ImGui.Text($"World Center: ({worldCenter.X:F2}, {worldCenter.Y:F2}, {worldCenter.Z:F2})");
                InspectorWidgets.EndSection();
            }
        }
    }
}

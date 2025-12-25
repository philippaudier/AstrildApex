using ImGuiNET;
using Engine.Physics;
using Editor.Inspector;
using Editor.Themes;
using NumVector3 = System.Numerics.Vector3;
using OtkVector3 = OpenTK.Mathematics.Vector3;

namespace Editor.Inspector
{
    public static class SphereColliderInspector
    {
        private static UITheme UI => ThemeManager.UI;

        public static void Draw(SphereCollider sphere)
        {
            if (sphere?.Entity == null) return;

            // === SHAPE SECTION ===
            if (InspectorWidgets.Section("Shape", defaultOpen: true))
            {
                ImGui.Text("Radius");
                float radius = sphere.Radius;
                ImGui.DragFloat("##Radius", ref radius, 0.05f, 0.001f);
                sphere.Radius = radius;

                var center = new NumVector3(sphere.Center.X, sphere.Center.Y, sphere.Center.Z);
                ImGui.Text("Center");
                ImGui.DragFloat3("##Center", ref center, 0.1f);
                sphere.Center = new OtkVector3(center.X, center.Y, center.Z);
                InspectorWidgets.EndSection();
            }

            // === COLLISION SECTION ===
            if (InspectorWidgets.Section("Collision", defaultOpen: true))
            {
                ImGui.Text("Layer");
                ImGui.DragInt("##Layer", ref sphere.Layer, 1.0f, 0, 31);

                ImGui.Checkbox("Is Trigger", ref sphere.IsTrigger);
                InspectorWidgets.EndSection();
            }

            // === DEBUG INFO ===
            if (InspectorWidgets.Section("Debug Info", defaultOpen: false))
            {
                ImGui.Text($"World Radius: {sphere.WorldRadius:F2}");

                var worldCenter = sphere.WorldCenter;
                ImGui.Text($"World Center: ({worldCenter.X:F2}, {worldCenter.Y:F2}, {worldCenter.Z:F2})");
                InspectorWidgets.EndSection();
            }
        }
    }
}

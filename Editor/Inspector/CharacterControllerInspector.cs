using ImGuiNET;
using Engine.Components;
using Editor.Inspector;
using Editor.Themes;
using NumVector3 = System.Numerics.Vector3;
using OtkVector3 = OpenTK.Mathematics.Vector3;

namespace Editor.Inspector
{
    public static class CharacterControllerInspector
    {
        private static UITheme UI => ThemeManager.UI;

        public static void Draw(CharacterController controller)
        {
            if (controller?.Entity == null) return;

            // === MODE SELECTION ===
            if (InspectorWidgets.Section("Mode", defaultOpen: true))
            {
                ImGui.Text("Controller Mode");

                int modeIndex = (int)controller.Mode;
                string[] modeLabels = { "Kinematic", "Physics (Future)" };

                if (ImGui.Combo("##Mode", ref modeIndex, modeLabels, modeLabels.Length))
                {
                    controller.Mode = (CharacterControllerMode)modeIndex;
                }

                // Info message based on mode
                if (controller.Mode == CharacterControllerMode.Kinematic)
                {
                    ImGui.TextDisabled("Manual control with collision detection. Use Move() method.");
                }
                else if (controller.Mode == CharacterControllerMode.Physics)
                {
                    ImGui.TextColored(UI.Warning, "Physics mode requires BulletSharp integration (coming soon).");
                }

                ImGui.Separator();

                // Interpolation mode
                ImGui.Text("Interpolation");
                int interpolationIndex = (int)controller.Interpolation;
                string[] interpolationLabels = { "None", "Interpolate (Recommended)", "Extrapolate" };

                if (ImGui.Combo("##Interpolation", ref interpolationIndex, interpolationLabels, interpolationLabels.Length))
                {
                    controller.Interpolation = (InterpolationMode)interpolationIndex;
                }

                // Info message based on interpolation
                if (controller.Interpolation == InterpolationMode.None)
                {
                    ImGui.TextDisabled("No smoothing - may appear stuttery.");
                }
                else if (controller.Interpolation == InterpolationMode.Interpolate)
                {
                    ImGui.TextDisabled("Smooth movement with slight latency (~16ms).");
                }
                else if (controller.Interpolation == InterpolationMode.Extrapolate)
                {
                    ImGui.TextDisabled("Responsive but may overshoot on direction changes.");
                }

                InspectorWidgets.EndSection();
            }

            // === SHAPE SECTION ===
            if (InspectorWidgets.Section("Shape", defaultOpen: true))
            {
                ImGui.Text("Height");
                float height = controller.Height;
                if (ImGui.DragFloat("##Height", ref height, 0.1f, controller.Radius * 2.0f + 0.001f, 10.0f))
                {
                    controller.Height = height;
                }

                ImGui.Text("Radius");
                float radius = controller.Radius;
                if (ImGui.DragFloat("##Radius", ref radius, 0.05f, 0.001f, 5.0f))
                {
                    controller.Radius = radius;
                }

                ImGui.Text("Center");
                var center = new NumVector3(controller.Center.X, controller.Center.Y, controller.Center.Z);
                if (ImGui.DragFloat3("##Center", ref center, 0.1f))
                {
                    controller.Center = new OtkVector3(center.X, center.Y, center.Z);
                }

                InspectorWidgets.EndSection();
            }

            // === MOVEMENT SECTION (Kinematic only) ===
            if (controller.Mode == CharacterControllerMode.Kinematic)
            {
                if (InspectorWidgets.Section("Movement (Kinematic)", defaultOpen: true))
                {
                    ImGui.Text("Slope Limit (degrees)");
                    float slopeLimit = controller.SlopeLimit;
                    if (ImGui.DragFloat("##SlopeLimit", ref slopeLimit, 1.0f, 0.0f, 90.0f))
                    {
                        controller.SlopeLimit = slopeLimit;
                    }

                    ImGui.Text("Step Height");
                    float stepHeight = controller.StepHeight;
                    if (ImGui.DragFloat("##StepHeight", ref stepHeight, 0.01f, 0.0f, 1.0f))
                    {
                        controller.StepHeight = stepHeight;
                    }

                    ImGui.Text("Skin Width");
                    float skinWidth = controller.SkinWidth;
                    if (ImGui.DragFloat("##SkinWidth", ref skinWidth, 0.001f, 0.001f, 0.1f))
                    {
                        controller.SkinWidth = skinWidth;
                    }

                    ImGui.Separator();

                    bool enableGravity = controller.EnableGravity;
                    if (ImGui.Checkbox("Enable Gravity", ref enableGravity))
                    {
                        controller.EnableGravity = enableGravity;
                    }

                    if (controller.EnableGravity)
                    {
                        ImGui.Text("Gravity");
                        var gravity = new NumVector3(controller.Gravity.X, controller.Gravity.Y, controller.Gravity.Z);
                        if (ImGui.DragFloat3("##Gravity", ref gravity, 0.5f))
                        {
                            controller.Gravity = new OtkVector3(gravity.X, gravity.Y, gravity.Z);
                        }
                    }

                    InspectorWidgets.EndSection();
                }
            }

            // === COLLISION SECTION ===
            if (InspectorWidgets.Section("Collision", defaultOpen: true))
            {
                ImGui.Text("Layer");
                int layer = controller.Layer;
                if (ImGui.DragInt("##Layer", ref layer, 1.0f, 0, 31))
                {
                    controller.Layer = layer;
                }

                ImGui.Text("Collision Mask");
                int mask = controller.CollisionMask;
                if (ImGui.InputInt("##CollisionMask", ref mask))
                {
                    controller.CollisionMask = mask;
                }

                ImGui.SameLine();
                if (ImGui.Button("All Layers"))
                {
                    controller.CollisionMask = ~0;
                }

                InspectorWidgets.EndSection();
            }

            // === DEBUG INFO (Kinematic only) ===
            if (controller.Mode == CharacterControllerMode.Kinematic)
            {
                if (InspectorWidgets.Section("State (Read-Only)", defaultOpen: false))
                {
                    // Grounded status with color
                    if (controller.IsGrounded)
                    {
                        ImGui.TextColored(UI.Success, "Is Grounded: YES");
                    }
                    else
                    {
                        ImGui.TextColored(UI.TextDisabled, "Is Grounded: NO");
                    }

                    ImGui.Text($"Ground Distance: {controller.GroundDistance:F3}");

                    var groundNormal = controller.GroundNormal;
                    ImGui.Text($"Ground Normal: ({groundNormal.X:F2}, {groundNormal.Y:F2}, {groundNormal.Z:F2})");

                    ImGui.Separator();

                    var velocity = controller.Velocity;
                    ImGui.Text($"Velocity: ({velocity.X:F2}, {velocity.Y:F2}, {velocity.Z:F2})");
                    ImGui.Text($"Speed: {velocity.Length:F2} m/s");

                    InspectorWidgets.EndSection();
                }
            }
        }
    }
}

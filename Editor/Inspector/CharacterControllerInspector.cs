using ImGuiNET;
using Engine.Components;

namespace Editor.Inspector
{
    /// <summary>
    /// Inspector for the new CharacterController (industry-standard phase-based architecture)
    /// </summary>
    public static class CharacterControllerInspector
    {
        public static void Draw(CharacterController cc)
        {
            if (cc?.Entity == null) return;
            uint entityId = cc.Entity.Id;

            // === CHARACTER CONTROLLER ===
            if (InspectorWidgets.Section("Character Controller", defaultOpen: true))
            {
                InspectorWidgets.InfoBox("Industry-standard CharacterController with phase-based collision resolution.\nBased on Unity, Unreal, and Godot best practices.");

                InspectorWidgets.EndSection();
            }

            // === CAPSULE SHAPE ===
            if (InspectorWidgets.Section("Capsule Shape", defaultOpen: true,
                tooltip: "The collision capsule for the character"))
            {
                float height = cc.Height;
                InspectorWidgets.FloatField("Height", ref height, entityId, "Height",
                    speed: 0.01f, min: 0.2f, max: 5f,
                    tooltip: "Total height of the character capsule",
                    validate: (h) => h > cc.Radius * 2 ? null : "Height must be greater than radius × 2",
                    helpText: "Standing human: ~1.8-2.0. Crouched: ~1.0");
                cc.Height = height;

                float radius = cc.Radius;
                InspectorWidgets.FloatField("Radius", ref radius, entityId, "Radius",
                    speed: 0.01f, min: 0.05f, max: 1f,
                    tooltip: "Radius of the character capsule",
                    validate: (r) => r > 0 ? null : "Radius must be positive",
                    helpText: "Human character: ~0.3-0.5");
                cc.Radius = radius;

                if (cc.Height < cc.Radius * 2)
                    InspectorWidgets.WarningBox("Height is less than diameter! Character may appear squashed.");

                // Shape presets
                InspectorWidgets.DisabledLabel("Presets:");
                int preset = InspectorWidgets.PresetButtonRow(
                    ("Human", "1.8 × 0.4"),
                    ("Crouch", "1.0 × 0.4"),
                    ("Child", "1.2 × 0.3"));

                if (preset == 0)
                {
                    cc.Height = 1.8f;
                    cc.Radius = 0.4f;
                }
                else if (preset == 1)
                {
                    cc.Height = 1.0f;
                    cc.Radius = 0.4f;
                }
                else if (preset == 2)
                {
                    cc.Height = 1.2f;
                    cc.Radius = 0.3f;
                }

                InspectorWidgets.EndSection();
            }

            // === MOVEMENT ===
            if (InspectorWidgets.Section("Movement", defaultOpen: true,
                tooltip: "Movement and physics settings"))
            {
                float height = cc.Height;
                InspectorWidgets.FloatField("Height", ref height, entityId, "Height",
                    speed: 0.01f, min: 0.1f, max: 5f,
                    tooltip: "Height of the character capsule",
                    helpText: "Typical human: 1.8-2.0");
                cc.Height = height;

                float radius = cc.Radius;
                InspectorWidgets.FloatField("Radius", ref radius, entityId, "Radius",
                    speed: 0.01f, min: 0.1f, max: 2f,
                    tooltip: "Radius of the character capsule",
                    helpText: "Typical human: 0.3-0.5");
                cc.Radius = radius;

                float skin = cc.SkinWidth;
                InspectorWidgets.FloatField("Skin Width", ref skin, entityId, "SkinWidth",
                    speed: 0.001f, min: 0.0f, max: 0.2f,
                    tooltip: "Collision skin width used to keep the controller slightly offset from geometry",
                    helpText: "Small values help hugging geometry; too small may cause tunnelling");
                cc.SkinWidth = skin;

                float gravity = cc.Gravity;
                InspectorWidgets.FloatField("Gravity", ref gravity, entityId, "Gravity",
                    speed: 0.05f, min: -50f, max: 50f,
                    tooltip: "Downward acceleration (positive = gravity)",
                    helpText: "Earth-like: 9.8-15. Low gravity: 3-5. High gravity: 20-30.");
                cc.Gravity = gravity;

                float slopeLimit = cc.SlopeLimit;
                InspectorWidgets.FloatField("Slope Limit (deg)", ref slopeLimit, entityId, "SlopeLimit",
                    speed: 0.5f, min: 0f, max: 89f,
                    tooltip: "Maximum slope angle (degrees) considered walkable",
                    helpText: "Typical walkable slopes: 30-50°");
                cc.SlopeLimit = slopeLimit;

                float groundCheckDist = cc.GroundCheckDistance;
                InspectorWidgets.FloatField("Ground Check Distance", ref groundCheckDist, entityId, "GroundCheckDistance",
                    speed: 0.01f, min: 0.1f, max: 2f,
                    tooltip: "Maximum distance to raycast downward for ground detection",
                    helpText: "Typical: 0.3-0.5");
                cc.GroundCheckDistance = groundCheckDist;

                InspectorWidgets.EndSection();
            }

            // === STATUS ===
            if (InspectorWidgets.Section("Status", defaultOpen: false))
            {
                // Read-only status
                bool isGrounded = cc.IsGrounded;
                ImGui.BeginDisabled();
                ImGui.Checkbox("Is Grounded", ref isGrounded);
                ImGui.EndDisabled();

                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Whether the character is currently touching the ground (read-only)");

                // Velocity display
                ImGui.BeginDisabled();
                var vel = cc.Velocity;
                ImGui.Text($"Velocity: X={vel.X:F2}, Y={vel.Y:F2}, Z={vel.Z:F2}");
                ImGui.EndDisabled();

                InspectorWidgets.EndSection();
            }
        }
    }
}

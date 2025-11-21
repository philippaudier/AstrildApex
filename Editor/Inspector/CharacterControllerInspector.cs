using ImGuiNET;
using Engine.Components;

namespace Editor.Inspector
{
    /// <summary>
    /// Professional Unity-style Character Controller inspector
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
                InspectorWidgets.InfoBox("Character Controller provides arcade-style character movement with collision detection and slope handling.");
                
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
                float stepOffset = cc.StepOffset;
                InspectorWidgets.FloatField("Step Offset", ref stepOffset, entityId, "StepOffset",
                    speed: 0.01f, min: 0f, max: 1f,
                    tooltip: "Maximum height the character can step up automatically",
                    helpText: "Stairs: 0.3-0.5. Flat ground only: 0.05. Small obstacles: 0.1-0.2");
                cc.StepOffset = stepOffset;

                float skin = cc.SkinWidth;
                InspectorWidgets.FloatField("Skin Width", ref skin, entityId, "SkinWidth",
                    speed: 0.001f, min: 0.0f, max: 0.2f,
                    tooltip: "Collision skin width used to keep the controller slightly offset from geometry",
                    helpText: "Small values help hugging geometry; too small may cause tunnelling");
                cc.SkinWidth = skin;

                float groundOffset = cc.GroundOffset;
                InspectorWidgets.FloatField("Ground Offset", ref groundOffset, entityId, "GroundOffset",
                    speed: 0.001f, min: -0.5f, max: 0.5f,
                    tooltip: "Vertical offset applied when snapping to ground",
                    helpText: "Use to tweak how the capsule sits above the surface");
                cc.GroundOffset = groundOffset;

                float gravity = cc.Gravity;
                InspectorWidgets.FloatField("Gravity", ref gravity, entityId, "Gravity",
                    speed: 0.05f, min: -50f, max: 50f,
                    tooltip: "Downward acceleration (positive = gravity)",
                    helpText: "Earth-like: 9.8-15. Low gravity: 3-5. High gravity: 20-30.");
                cc.Gravity = gravity;

                float maxSlope = cc.MaxSlopeAngleDeg;
                InspectorWidgets.FloatField("Max Slope (deg)", ref maxSlope, entityId, "MaxSlopeAngleDeg",
                    speed: 0.5f, min: 0f, max: 89f,
                    tooltip: "Maximum slope angle (degrees) considered walkable",
                    helpText: "Typical walkable slopes: 30-50°");
                cc.MaxSlopeAngleDeg = maxSlope;

                float maxClimb = cc.MaxClimbPerFrame;
                InspectorWidgets.FloatField("Max Climb Per Frame", ref maxClimb, entityId, "MaxClimbPerFrame",
                    speed: 0.01f, min: 0f, max: 2f,
                    tooltip: "Maximum vertical climb allowed by sliding/projection per frame",
                    helpText: "Prevent large upward hops when sliding on curved surfaces");
                cc.MaxClimbPerFrame = maxClimb;

                float climbSpeed = cc.ClimbSmoothSpeed;
                InspectorWidgets.FloatField("Climb Smooth Speed", ref climbSpeed, entityId, "ClimbSmoothSpeed",
                    speed: 0.1f, min: 0f, max: 50f,
                    tooltip: "Speed used to smooth upward adjustments when moving over geometry",
                    helpText: "Higher = faster snapping up slopes");
                cc.ClimbSmoothSpeed = climbSpeed;

                float descendSpeed = cc.DescendSmoothSpeed;
                InspectorWidgets.FloatField("Descend Smooth Speed", ref descendSpeed, entityId, "DescendSmoothSpeed",
                    speed: 0.1f, min: 0f, max: 100f,
                    tooltip: "Speed used to smooth downward adjustments when moving down slopes",
                    helpText: "Higher = faster descent (less smoothing)");
                cc.DescendSmoothSpeed = descendSpeed;

                float snapEps = cc.SnapEpsilon;
                InspectorWidgets.FloatField("Snap Epsilon", ref snapEps, entityId, "SnapEpsilon",
                    speed: 0.001f, min: 0f, max: 0.2f,
                    tooltip: "Tolerance for considering the controller 'on the ground'",
                    helpText: "Smaller = stricter grounding, larger = more forgiving");
                cc.SnapEpsilon = snapEps;

                float alignSpeed = cc.GroundAlignSpeed;
                InspectorWidgets.FloatField("Ground Align Speed", ref alignSpeed, entityId, "GroundAlignSpeed",
                    speed: 0.1f, min: 0f, max: 100f,
                    tooltip: "How quickly the character's up vector aligns to the ground normal",
                    helpText: "0 = instant alignment, higher = smoother alignment");
                cc.GroundAlignSpeed = alignSpeed;

                InspectorWidgets.EndSection();
            }

            // === STATUS & DEBUG ===
            if (InspectorWidgets.Section("Status & Debug", defaultOpen: false))
            {
                // Read-only status
                bool isGrounded = cc.IsGrounded;
                ImGui.BeginDisabled();
                ImGui.Checkbox("Is Grounded", ref isGrounded);
                ImGui.EndDisabled();
                
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Whether the character is currently touching the ground (read-only)");

                // Debug toggle
                bool debugPhysics = cc.DebugPhysics;
                InspectorWidgets.Checkbox("Debug Physics", ref debugPhysics, entityId, "DebugPhysics",
                    tooltip: "Show debug visualization for physics collisions",
                    helpText: "Renders collision capsule and raycast debug lines in the scene");
                cc.DebugPhysics = debugPhysics;

                InspectorWidgets.EndSection();
            }
        }
    }
}

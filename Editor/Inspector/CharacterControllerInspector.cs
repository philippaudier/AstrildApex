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

                // Interpolation info (not used in Kinematic mode)
                ImGui.TextDisabled("Interpolation:");
                if (controller.Mode == CharacterControllerMode.Kinematic)
                {
                    ImGui.TextDisabled("Not used - Kinematic movement is in Update() (synchronized with camera).");
                }
                else
                {
                    ImGui.TextDisabled("Interpolation will be used in Physics mode (future).");
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

                // === MOVEMENT FEEL SECTION (Kinematic only) ===
                if (InspectorWidgets.Section("Movement Feel", defaultOpen: true))
                {
                    bool enableMovementFeel = controller.EnableMovementFeel;
                    if (ImGui.Checkbox("Enable Movement Feel", ref enableMovementFeel))
                    {
                        controller.EnableMovementFeel = enableMovementFeel;
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("Enable smooth acceleration, air control, coyote time, and jump buffering.\nDisable for legacy direct movement.");
                    }

                    if (controller.EnableMovementFeel)
                    {
                        ImGui.Separator();
                        ImGui.TextColored(UI.Info, "Gravity & Weight");

                        ImGui.Text("Gravity Scale");
                        float gravityScale = controller.GravityScale;
                        if (ImGui.DragFloat("##GravityScale", ref gravityScale, 0.05f, 0.1f, 5.0f))
                        {
                            controller.GravityScale = gravityScale;
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("1.0 = normal, >1 = heavy/fast fall, <1 = floaty/light");
                        }

                        ImGui.Text("Terminal Velocity");
                        float terminalVelocity = controller.TerminalVelocity;
                        if (ImGui.DragFloat("##TerminalVelocity", ref terminalVelocity, 1.0f, 1.0f, 200.0f))
                        {
                            controller.TerminalVelocity = terminalVelocity;
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("Maximum falling speed (m/s)");
                        }

                        ImGui.Separator();
                        ImGui.TextColored(UI.Info, "Ground Movement");

                        ImGui.Text("Ground Acceleration");
                        float groundAccel = controller.GroundAcceleration;
                        if (ImGui.DragFloat("##GroundAccel", ref groundAccel, 1.0f, 1.0f, 100.0f))
                        {
                            controller.GroundAcceleration = groundAccel;
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("How fast you reach max speed (higher = snappier)");
                        }

                        ImGui.Text("Ground Deceleration");
                        float groundDecel = controller.GroundDeceleration;
                        if (ImGui.DragFloat("##GroundDecel", ref groundDecel, 1.0f, 1.0f, 100.0f))
                        {
                            controller.GroundDeceleration = groundDecel;
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("How fast you stop when no input (higher = snappier stop)");
                        }

                        ImGui.Text("Ground Friction");
                        float groundFriction = controller.GroundFriction;
                        if (ImGui.SliderFloat("##GroundFriction", ref groundFriction, 0.0f, 1.0f))
                        {
                            controller.GroundFriction = groundFriction;
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("Surface friction coefficient (lower = more slippery/ice-like)");
                        }

                        ImGui.Separator();
                        ImGui.TextColored(UI.Info, "Air Movement");

                        ImGui.Text("Air Control");
                        float airControl = controller.AirControl;
                        if (ImGui.SliderFloat("##AirControl", ref airControl, 0.0f, 1.0f))
                        {
                            controller.AirControl = airControl;
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("How much control you have while airborne (0 = none, 1 = full)");
                        }

                        ImGui.Text("Air Acceleration");
                        float airAccel = controller.AirAcceleration;
                        if (ImGui.DragFloat("##AirAccel", ref airAccel, 1.0f, 1.0f, 100.0f))
                        {
                            controller.AirAcceleration = airAccel;
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("How fast you can change direction in air");
                        }

                        ImGui.Text("Air Drag");
                        float airDrag = controller.AirDrag;
                        if (ImGui.SliderFloat("##AirDrag", ref airDrag, 0.8f, 1.0f))
                        {
                            controller.AirDrag = airDrag;
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("Air resistance (lower = more drag)");
                        }

                        ImGui.Separator();
                        ImGui.TextColored(UI.Info, "Jump Feel");

                        ImGui.Text("Coyote Time");
                        float coyoteTime = controller.CoyoteTime;
                        if (ImGui.DragFloat("##CoyoteTime", ref coyoteTime, 0.01f, 0.0f, 0.5f, "%.3f s"))
                        {
                            controller.CoyoteTime = coyoteTime;
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("Grace period after leaving ground where you can still jump");
                        }

                        ImGui.Text("Jump Buffer Time");
                        float jumpBuffer = controller.JumpBufferTime;
                        if (ImGui.DragFloat("##JumpBuffer", ref jumpBuffer, 0.01f, 0.0f, 0.5f, "%.3f s"))
                        {
                            controller.JumpBufferTime = jumpBuffer;
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("Grace period before landing where jump input is remembered");
                        }

                        ImGui.Separator();

                        // Quick presets
                        if (ImGui.Button("Preset: Heavy"))
                        {
                            controller.GravityScale = 1.5f;
                            controller.GroundAcceleration = 15f;
                            controller.GroundDeceleration = 10f;
                            controller.AirControl = 0.1f;
                        }
                        ImGui.SameLine();
                        if (ImGui.Button("Preset: Light"))
                        {
                            controller.GravityScale = 0.7f;
                            controller.GroundAcceleration = 50f;
                            controller.GroundDeceleration = 40f;
                            controller.AirControl = 0.5f;
                        }
                        ImGui.SameLine();
                        if (ImGui.Button("Preset: Slippery"))
                        {
                            controller.GroundFriction = 0.95f;
                            controller.GroundDeceleration = 5f;
                            controller.AirDrag = 0.99f;
                        }
                        ImGui.SameLine();
                        if (ImGui.Button("Reset Defaults"))
                        {
                            controller.GravityScale = 1.0f;
                            controller.TerminalVelocity = 50f;
                            controller.GroundAcceleration = 30f;
                            controller.GroundDeceleration = 25f;
                            controller.GroundFriction = 0.92f;
                            controller.AirControl = 0.3f;
                            controller.AirAcceleration = 15f;
                            controller.AirDrag = 0.98f;
                            controller.CoyoteTime = 0.15f;
                            controller.JumpBufferTime = 0.1f;
                        }
                    }
                    else
                    {
                        ImGui.TextDisabled("Movement Feel disabled - using legacy direct movement.");
                    }

                    InspectorWidgets.EndSection();
                }

                // === SLOPE SLIDING SECTION (Kinematic only) ===
                if (InspectorWidgets.Section("Slope Sliding", defaultOpen: true))
                {
                    bool enableSliding = controller.EnableSliding;
                    if (ImGui.Checkbox("Enable Slope Sliding", ref enableSliding))
                    {
                        controller.EnableSliding = enableSliding;
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("Enable realistic sliding on steep slopes instead of falling through.\nAllows you to slide down slopes > SlopeLimit with friction and player control.");
                    }

                    if (controller.EnableSliding)
                    {
                        ImGui.Separator();

                        ImGui.Text("Min Slide Angle (degrees)");
                        float minSlideAngle = controller.MinSlideAngle;
                        if (ImGui.DragFloat("##MinSlideAngle", ref minSlideAngle, 1.0f, controller.SlopeLimit, 90.0f))
                        {
                            controller.MinSlideAngle = minSlideAngle;
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("Minimum angle to start sliding. Slopes between SlopeLimit and this are walkable.");
                        }

                        ImGui.Text("Slide Friction");
                        float slideFriction = controller.SlideFriction;
                        if (ImGui.SliderFloat("##SlideFriction", ref slideFriction, 0.0f, 1.0f))
                        {
                            controller.SlideFriction = slideFriction;
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("How much the slope resists sliding (0 = ice, 1 = high friction)");
                        }

                        ImGui.Text("Slide Gravity Multiplier");
                        float slideGravMult = controller.SlideGravityMultiplier;
                        if (ImGui.DragFloat("##SlideGravMult", ref slideGravMult, 0.1f, 0.1f, 5.0f))
                        {
                            controller.SlideGravityMultiplier = slideGravMult;
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("Gravity multiplier when sliding (higher = faster slide)");
                        }

                        ImGui.Text("Slide Control");
                        float slideControl = controller.SlideControl;
                        if (ImGui.SliderFloat("##SlideControl", ref slideControl, 0.0f, 1.0f))
                        {
                            controller.SlideControl = slideControl;
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("How much player input can affect slide direction (0 = no control, 1 = full control)");
                        }

                        ImGui.Text("Max Slide Speed");
                        float maxSlideSpeed = controller.MaxSlideSpeed;
                        if (ImGui.DragFloat("##MaxSlideSpeed", ref maxSlideSpeed, 1.0f, 1.0f, 100.0f))
                        {
                            controller.MaxSlideSpeed = maxSlideSpeed;
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("Maximum sliding speed (prevents infinite acceleration)");
                        }

                        ImGui.Separator();

                        // Quick presets for sliding
                        if (ImGui.Button("Preset: Icy Slope"))
                        {
                            controller.SlideFriction = 0.05f;
                            controller.SlideGravityMultiplier = 2.0f;
                            controller.SlideControl = 0.2f;
                        }
                        ImGui.SameLine();
                        if (ImGui.Button("Preset: Rocky Slope"))
                        {
                            controller.SlideFriction = 0.5f;
                            controller.SlideGravityMultiplier = 1.2f;
                            controller.SlideControl = 0.6f;
                        }
                        ImGui.SameLine();
                        if (ImGui.Button("Preset: Default"))
                        {
                            controller.SlideFriction = 0.3f;
                            controller.SlideGravityMultiplier = 1.5f;
                            controller.SlideControl = 0.4f;
                            controller.MaxSlideSpeed = 20f;
                            controller.MinSlideAngle = 50f;
                        }
                    }
                    else
                    {
                        ImGui.TextDisabled("Sliding disabled - slopes > SlopeLimit will not be walkable.");
                    }

                    InspectorWidgets.EndSection();
                }

                // === SLOPE MOMENTUM SECTION (Kinematic only) ===
                if (InspectorWidgets.Section("Slope Momentum (Mario 64 style)", defaultOpen: true))
                {
                    bool enableSlopeMomentum = controller.EnableSlopeMomentum;
                    if (ImGui.Checkbox("Enable Slope Momentum", ref enableSlopeMomentum))
                    {
                        controller.EnableSlopeMomentum = enableSlopeMomentum;
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("Enable Mario 64-style slope momentum.\nRun up slopes with momentum, decelerate, and slide back if too slow.");
                    }

                    if (controller.EnableSlopeMomentum)
                    {
                        ImGui.Separator();

                        ImGui.Text("Slope Momentum Retention");
                        float momentumRetention = controller.SlopeMomentumRetention;
                        if (ImGui.SliderFloat("##MomentumRetention", ref momentumRetention, 0.0f, 1.0f))
                        {
                            controller.SlopeMomentumRetention = momentumRetention;
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("How much momentum is kept when climbing (1 = full momentum, 0 = stop immediately)");
                        }

                        ImGui.Text("Slope Climb Deceleration");
                        float climbDecel = controller.SlopeClimbDeceleration;
                        if (ImGui.DragFloat("##ClimbDecel", ref climbDecel, 0.5f, 0.0f, 50.0f))
                        {
                            controller.SlopeClimbDeceleration = climbDecel;
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("Deceleration when climbing slopes (higher = slower climb)");
                        }

                        ImGui.Text("Min Speed Before Backslide");
                        float minSpeedBackslide = controller.MinSpeedBeforeBackslide;
                        if (ImGui.DragFloat("##MinSpeedBackslide", ref minSpeedBackslide, 0.1f, 0.0f, 10.0f))
                        {
                            controller.MinSpeedBeforeBackslide = minSpeedBackslide;
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("Minimum speed to maintain before sliding backwards");
                        }

                        ImGui.Text("Backslide Angle (degrees)");
                        float backslideAngle = controller.BackslideAngle;
                        if (ImGui.DragFloat("##BackslideAngle", ref backslideAngle, 1.0f, 0.0f, controller.SlopeLimit))
                        {
                            controller.BackslideAngle = backslideAngle;
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("Slope angle where backsliding starts if stopped");
                        }

                        ImGui.Separator();

                        // Quick presets
                        if (ImGui.Button("Preset: Mario 64"))
                        {
                            controller.SlopeMomentumRetention = 0.7f;
                            controller.SlopeClimbDeceleration = 8f;
                            controller.MinSpeedBeforeBackslide = 1f;
                            controller.BackslideAngle = 35f;
                        }
                        ImGui.SameLine();
                        if (ImGui.Button("Preset: Easy Climb"))
                        {
                            controller.SlopeMomentumRetention = 0.9f;
                            controller.SlopeClimbDeceleration = 4f;
                            controller.MinSpeedBeforeBackslide = 0.5f;
                            controller.BackslideAngle = 40f;
                        }
                        ImGui.SameLine();
                        if (ImGui.Button("Preset: Realistic"))
                        {
                            controller.SlopeMomentumRetention = 0.5f;
                            controller.SlopeClimbDeceleration = 15f;
                            controller.MinSpeedBeforeBackslide = 2f;
                            controller.BackslideAngle = 30f;
                        }
                    }
                    else
                    {
                        ImGui.TextDisabled("Slope momentum disabled - slopes behave like flat ground.");
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

                    // Sliding status with color
                    if (controller.IsSliding)
                    {
                        ImGui.TextColored(UI.Warning, "Is Sliding: YES");
                    }
                    else
                    {
                        ImGui.TextDisabled("Is Sliding: NO");
                    }

                    ImGui.Text($"Current Slope Angle: {controller.CurrentSlopeAngle:F1}°");
                    ImGui.Text($"Ground Distance: {controller.GroundDistance:F3}");

                    var groundNormal = controller.GroundNormal;
                    ImGui.Text($"Ground Normal: ({groundNormal.X:F2}, {groundNormal.Y:F2}, {groundNormal.Z:F2})");

                    ImGui.Separator();

                    var velocity = controller.Velocity;
                    ImGui.Text($"Velocity: ({velocity.X:F2}, {velocity.Y:F2}, {velocity.Z:F2})");
                    ImGui.Text($"Speed: {velocity.Length:F2} m/s");

                    // Show horizontal speed separately when sliding
                    if (controller.IsSliding)
                    {
                        float horizontalSpeed = new System.Numerics.Vector2(velocity.X, velocity.Z).Length();
                        ImGui.TextColored(UI.Warning, $"Slide Speed: {horizontalSpeed:F2} m/s");
                    }

                    InspectorWidgets.EndSection();
                }
            }
        }
    }
}

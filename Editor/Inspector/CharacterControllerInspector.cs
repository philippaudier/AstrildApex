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

                // === SLOPE BEHAVIOR (SIMPLE) ===
                if (InspectorWidgets.Section("Slope Behavior", defaultOpen: false))
                {
                    ImGui.TextColored(UI.Info, "Simple slope handling:");
                    ImGui.BulletText("Slope > SlopeLimit: Slides down automatically");
                    ImGui.BulletText("Trying to climb steep slope: Blocked (like a wall)");
                    ImGui.BulletText("Slide uses gravity + friction from above");
                    ImGui.BulletText("Deceleration when leaving slope");

                    ImGui.Separator();
                    ImGui.TextDisabled("All slope behavior is controlled by:");
                    ImGui.BulletText("SlopeLimit (above)");
                    ImGui.BulletText("Gravity Scale (Movement Feel)");
                    ImGui.BulletText("Ground Friction (Movement Feel)");

                    InspectorWidgets.EndSection();
                }

                // === SWIMMING SECTION ===
                if (InspectorWidgets.Section("Swimming", defaultOpen: true))
                {
                    bool enableSwimming = controller.EnableSwimming;
                    if (ImGui.Checkbox("Enable Swimming", ref enableSwimming))
                    {
                        controller.EnableSwimming = enableSwimming;
                    }

                    if (controller.EnableSwimming)
                    {
                        ImGui.Separator();
                        ImGui.TextColored(UI.Info, "Water Detection");

                        ImGui.Text("Water Level (Y)");
                        float waterLevel = controller.WaterLevel;
                        if (ImGui.DragFloat("##WaterLevel", ref waterLevel, 0.1f, -1f, 1000f))
                        {
                            controller.WaterLevel = waterLevel;
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("-1 = Auto-detect from WaterPlane component");
                        }

                        ImGui.Text("Swim Depth Threshold");
                        float swimDepth = controller.SwimDepthThreshold;
                        if (ImGui.DragFloat("##SwimDepth", ref swimDepth, 0.05f, 0.1f, 2.0f))
                        {
                            controller.SwimDepthThreshold = swimDepth;
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("How deep to be before swimming starts");
                        }

                        ImGui.Separator();
                        ImGui.TextColored(UI.Info, "Swimming Movement");

                        ImGui.Text("Swim Acceleration");
                        float swimAccel = controller.SwimAcceleration;
                        if (ImGui.DragFloat("##SwimAccel", ref swimAccel, 0.5f, 1f, 30f))
                        {
                            controller.SwimAcceleration = swimAccel;
                        }

                        ImGui.Text("Swim Deceleration");
                        float swimDecel = controller.SwimDeceleration;
                        if (ImGui.DragFloat("##SwimDecel", ref swimDecel, 0.5f, 1f, 30f))
                        {
                            controller.SwimDeceleration = swimDecel;
                        }

                        ImGui.Text("Swim Drag");
                        float swimDrag = controller.SwimDrag;
                        if (ImGui.SliderFloat("##SwimDrag", ref swimDrag, 0.8f, 1.0f))
                        {
                            controller.SwimDrag = swimDrag;
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("Water resistance (lower = more drag)");
                        }

                        ImGui.Text("Vertical Swim Speed");
                        float vertSwimSpeed = controller.VerticalSwimSpeed;
                        if (ImGui.DragFloat("##VertSwimSpeed", ref vertSwimSpeed, 0.1f, 0.1f, 3.0f))
                        {
                            controller.VerticalSwimSpeed = vertSwimSpeed;
                        }

                        ImGui.Separator();
                        ImGui.TextColored(UI.Info, "Buoyancy & Surface");

                        ImGui.Text("Buoyancy");
                        float buoyancy = controller.Buoyancy;
                        if (ImGui.DragFloat("##Buoyancy", ref buoyancy, 0.05f, 0f, 2.0f))
                        {
                            controller.Buoyancy = buoyancy;
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("0 = sink, 1 = neutral, >1 = float up");
                        }

                        ImGui.Text("Surface Swim Offset");
                        float surfaceOffset = controller.SurfaceSwimOffset;
                        if (ImGui.DragFloat("##SurfaceOffset", ref surfaceOffset, 0.05f, -1f, 1f))
                        {
                            controller.SurfaceSwimOffset = surfaceOffset;
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("Height above water surface when surface swimming");
                        }

                        ImGui.Text("Surface Rise Speed");
                        float surfaceRise = controller.SurfaceRiseSpeed;
                        if (ImGui.DragFloat("##SurfaceRise", ref surfaceRise, 0.1f, 0.5f, 10f))
                        {
                            controller.SurfaceRiseSpeed = surfaceRise;
                        }

                        ImGui.Text("Underwater Gravity Scale");
                        float underwaterGrav = controller.UnderwaterGravityScale;
                        if (ImGui.DragFloat("##UnderwaterGrav", ref underwaterGrav, 0.01f, 0f, 1f))
                        {
                            controller.UnderwaterGravityScale = underwaterGrav;
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("Gravity strength when underwater (lower = more floaty)");
                        }

                        ImGui.Separator();
                        ImGui.TextColored(UI.Info, "Water Plunge (Entry)");
                        ImGui.Spacing();

                        bool enablePlunge = controller.EnableWaterPlunge;
                        if (ImGui.Checkbox("Enable Water Plunge", ref enablePlunge))
                        {
                            controller.EnableWaterPlunge = enablePlunge;
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("When enabled, jumping into water from height causes realistic plunge");
                        }

                        if (controller.EnableWaterPlunge)
                        {
                            ImGui.Text("Min Entry Velocity");
                            float plungeMinVel = controller.PlungeMinVelocity;
                            if (ImGui.DragFloat("##PlungeMinVel", ref plungeMinVel, 0.1f, 1f, 20f))
                            {
                                controller.PlungeMinVelocity = plungeMinVel;
                            }
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip("Minimum fall speed (m/s) to trigger plunge. Below this, just float.");
                            }

                            ImGui.Text("Velocity Retention");
                            float plungeRetention = controller.PlungeVelocityRetention;
                            if (ImGui.DragFloat("##PlungeRetention", ref plungeRetention, 0.01f, 0.1f, 1f))
                            {
                                controller.PlungeVelocityRetention = plungeRetention;
                            }
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip("How much entry velocity is kept (0.7 = 70%). Higher = deeper plunge.");
                            }

                            ImGui.Text("Plunge Drag");
                            float plungeDrag = controller.PlungeDrag;
                            if (ImGui.DragFloat("##PlungeDrag", ref plungeDrag, 0.1f, 0.5f, 10f))
                            {
                                controller.PlungeDrag = plungeDrag;
                            }
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip("Water resistance during plunge. Higher = stops faster.");
                            }

                            ImGui.Text("Plunge Duration");
                            float plungeDuration = controller.PlungeDuration;
                            if (ImGui.DragFloat("##PlungeDuration", ref plungeDuration, 0.1f, 0.5f, 5f))
                            {
                                controller.PlungeDuration = plungeDuration;
                            }
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip("Max duration of plunge before normal swimming takes over.");
                            }

                            ImGui.Text("Plunge Buoyancy Scale");
                            float plungeBuoyancy = controller.PlungeBuoyancyScale;
                            if (ImGui.DragFloat("##PlungeBuoyancy", ref plungeBuoyancy, 0.01f, 0f, 1f))
                            {
                                controller.PlungeBuoyancyScale = plungeBuoyancy;
                            }
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip("Buoyancy multiplier during plunge. Lower = sink deeper before rising.");
                            }

                            // Show plunge status
                            if (controller.IsPlunging)
                            {
                                ImGui.TextColored(new System.Numerics.Vector4(1.0f, 0.5f, 0.2f, 1.0f), "PLUNGING!");
                            }
                        }

                        ImGui.Separator();

                        // Progressive Water Drag
                        ImGui.TextColored(UI.Info, "Progressive Water Drag");
                        ImGui.Spacing();

                        bool enableProgressiveDrag = controller.EnableProgressiveWaterDrag;
                        if (ImGui.Checkbox("Enable Progressive Drag", ref enableProgressiveDrag))
                        {
                            controller.EnableProgressiveWaterDrag = enableProgressiveDrag;
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("When enabled, water resistance increases progressively based on how deep you are submerged.");
                        }

                        if (controller.EnableProgressiveWaterDrag)
                        {
                            ImGui.Text("Full Submersion Drag");
                            float fullDrag = controller.FullSubmersionDragMultiplier;
                            if (ImGui.DragFloat("##FullDrag", ref fullDrag, 0.1f, 1f, 5f))
                            {
                                controller.FullSubmersionDragMultiplier = fullDrag;
                            }
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip("Drag multiplier when fully submerged. 1.0 = no extra drag, 2.0 = double drag.");
                            }

                            ImGui.Text("Full Submersion Speed");
                            float fullSpeed = controller.FullSubmersionSpeedMultiplier;
                            if (ImGui.DragFloat("##FullSpeed", ref fullSpeed, 0.05f, 0.1f, 1f))
                            {
                                controller.FullSubmersionSpeedMultiplier = fullSpeed;
                            }
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip("Speed multiplier when fully submerged. 0.5 = half speed underwater.");
                            }

                            // Show current submersion ratio when swimming
                            if (controller.IsSwimming)
                            {
                                ImGui.TextColored(UI.Info, $"Submersion: {controller.SubmersionRatio * 100f:F0}%%");
                            }
                        }

                        ImGui.Separator();

                        // Presets (Minecraft-style swimming)
                        if (ImGui.Button("Preset: Realistic"))
                        {
                            controller.SwimAcceleration = 6f;
                            controller.SwimDeceleration = 4f;
                            controller.SwimDrag = 0.90f;
                            controller.Buoyancy = 1.05f; // Slightly buoyant
                            controller.VerticalSwimSpeed = 0.8f;
                            controller.UnderwaterGravityScale = 0.15f;
                            // Plunge settings
                            controller.EnableWaterPlunge = true;
                            controller.PlungeMinVelocity = 3.0f;
                            controller.PlungeVelocityRetention = 0.7f;
                            controller.PlungeDrag = 2.5f;
                            controller.PlungeDuration = 1.5f;
                            controller.PlungeBuoyancyScale = 0.2f;
                            // Progressive drag: strong resistance when deep
                            controller.EnableProgressiveWaterDrag = true;
                            controller.FullSubmersionDragMultiplier = 2.5f;
                            controller.FullSubmersionSpeedMultiplier = 0.5f;
                        }
                        ImGui.SameLine();
                        if (ImGui.Button("Preset: Arcade"))
                        {
                            controller.SwimAcceleration = 12f;
                            controller.SwimDeceleration = 8f;
                            controller.SwimDrag = 0.95f;
                            controller.Buoyancy = 1.1f; // More buoyant
                            controller.VerticalSwimSpeed = 1.2f;
                            controller.UnderwaterGravityScale = 0.05f;
                            // Plunge: quick recovery
                            controller.EnableWaterPlunge = true;
                            controller.PlungeMinVelocity = 5.0f;
                            controller.PlungeVelocityRetention = 0.5f;
                            controller.PlungeDrag = 4.0f;
                            controller.PlungeDuration = 0.8f;
                            controller.PlungeBuoyancyScale = 0.4f;
                            // Progressive drag: minimal for arcade feel
                            controller.EnableProgressiveWaterDrag = true;
                            controller.FullSubmersionDragMultiplier = 1.3f;
                            controller.FullSubmersionSpeedMultiplier = 0.8f;
                        }
                        ImGui.SameLine();
                        if (ImGui.Button("Preset: Neutral"))
                        {
                            controller.SwimAcceleration = 8f;
                            controller.SwimDeceleration = 5f;
                            controller.SwimDrag = 0.92f;
                            controller.Buoyancy = 1.0f; // Neutral buoyancy
                            controller.VerticalSwimSpeed = 1.0f;
                            controller.UnderwaterGravityScale = 0.0f;
                            // Plunge: controlled descent
                            controller.EnableWaterPlunge = true;
                            controller.PlungeMinVelocity = 2.0f;
                            controller.PlungeVelocityRetention = 0.6f;
                            controller.PlungeDrag = 3.0f;
                            controller.PlungeDuration = 2.0f;
                            controller.PlungeBuoyancyScale = 0.1f;
                            // Progressive drag: balanced
                            controller.EnableProgressiveWaterDrag = true;
                            controller.FullSubmersionDragMultiplier = 2.0f;
                            controller.FullSubmersionSpeedMultiplier = 0.6f;
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

                    // Swimming status
                    if (controller.IsSwimming)
                    {
                        ImGui.TextColored(new System.Numerics.Vector4(0.2f, 0.6f, 1.0f, 1.0f), "Is Swimming: YES");

                        if (controller.IsUnderwater)
                        {
                            ImGui.TextColored(new System.Numerics.Vector4(0.1f, 0.4f, 0.8f, 1.0f), "  Underwater: YES");
                        }
                        else
                        {
                            ImGui.TextColored(new System.Numerics.Vector4(0.3f, 0.7f, 1.0f, 1.0f), "  At Surface: YES");
                        }

                        ImGui.Text($"Water Depth: {controller.WaterDepth:F2}m");
                        ImGui.Text($"Water Level: {controller.CurrentWaterLevel:F2}");
                    }
                    else
                    {
                        ImGui.TextDisabled("Is Swimming: NO");
                    }

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

                    // Show swim speed when swimming
                    if (controller.IsSwimming)
                    {
                        float horizontalSpeed = new System.Numerics.Vector2(velocity.X, velocity.Z).Length();
                        ImGui.TextColored(new System.Numerics.Vector4(0.2f, 0.6f, 1.0f, 1.0f), $"Swim Speed: {horizontalSpeed:F2} m/s (H) | {velocity.Y:F2} m/s (V)");
                    }

                    InspectorWidgets.EndSection();
                }
            }
        }
    }
}

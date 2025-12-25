using ImGuiNET;
using Engine.Components;
using OpenTK.Mathematics;
using Editor.Panels;
using Editor.Inspector;
using Editor.UI;
using Editor.Themes;

namespace Editor.Inspector
{
    /// <summary>
    /// Modern inspector for the unified CameraComponent
    /// Uses unified EditorWidgets system for consistent UX
    /// </summary>
    public static class CameraInspector
    {
        private static UITheme UI => ThemeManager.UI;
        public static void Draw(CameraComponent cam)
        {
            if (cam?.Entity == null) return;
            uint entityId = cam.Entity.Id;

            // === PROJECTION SECTION ===
            if (InspectorWidgets.Section("Projection", defaultOpen: true,
                tooltip: "Camera projection settings"))
            {
                var proj = cam.Projection;
                InspectorWidgets.EnumField("Mode", ref proj, entityId, "Projection",
                    tooltip: "Perspective: 3D with depth. Orthographic: flat 2D/isometric projection");
                cam.Projection = proj;

                if (cam.Projection == CameraComponent.ProjectionMode.Perspective)
                {
                    float fovDeg = MathHelper.RadiansToDegrees(cam.FieldOfView);
                    InspectorWidgets.SliderAngle("Field of View", ref fovDeg, 1f, 170f,
                        entityId, "FieldOfView",
                        tooltip: "Camera's viewing angle. Lower = more zoomed in");
                    cam.FieldOfView = MathHelper.DegreesToRadians(fovDeg);
                }
                else
                {
                    InspectorWidgets.FloatField("Ortho Size", ref cam.OrthoSize, entityId, "OrthoSize",
                        speed: 0.1f, min: 0.1f, max: 1000f,
                        tooltip: "Half-height of the camera view in world units");
                }

                InspectorWidgets.FloatField("Near", ref cam.Near, entityId, "Near",
                    speed: 0.01f, min: 0.001f, max: 10f, format: "%.3f",
                    tooltip: "Closest distance the camera can see");

                InspectorWidgets.FloatField("Far", ref cam.Far, entityId, "Far",
                    speed: 1f, min: 10f, max: 100000f,
                    tooltip: "Farthest distance the camera can see");

                InspectorWidgets.EndSection();
            }

            // === CAMERA CONTROL MODE ===
            if (InspectorWidgets.Section("Camera Control", defaultOpen: true))
            {
                InspectorWidgets.Checkbox("Main Camera", ref cam.IsMain, entityId, "IsMain",
                    tooltip: "This camera will be used for rendering");

                var mode = cam.Mode;
                InspectorWidgets.EnumField("Control Mode", ref mode, entityId, "Mode",
                    tooltip: "How the camera behaves: Manual, FPS, ThirdPerson, TopDown, Isometric, SideScroller2D, Orbit");
                cam.Mode = mode;

                var stage = cam.Stage;
                InspectorWidgets.EnumField("Update Stage", ref stage, entityId, "Stage",
                    tooltip: "When the camera updates");
                cam.Stage = stage;

                InspectorWidgets.EndSection();
            }

            // === COMMON SETTINGS (all non-Manual modes) ===
            if (cam.Mode != CameraComponent.ControlMode.Manual)
            {
                if (InspectorWidgets.Section("Common Settings", defaultOpen: true))
                {
                    // Follow Target
                    var scene = EditorUI.MainViewport.Renderer?.Scene;
                    if (scene != null)
                    {
                        TransformComponent? t = cam.FollowTarget;
                        if (FieldWidgets.ComponentRef("Follow Target", scene, ref t))
                            cam.FollowTarget = t;
                    }

                    var off = cam.TargetOffset;
                    InspectorWidgets.Vector3FieldOTK("Target Offset", ref off, 0.01f, entityId, "TargetOffset",
                        tooltip: "Offset from target position");
                    cam.TargetOffset = off;

                    InspectorWidgets.SliderFloat("Smooth Position", ref cam.SmoothPosition, 0f, 40f, "%.1f",
                        entityId, "SmoothPosition",
                        tooltip: "Position interpolation speed");

                    InspectorWidgets.SliderFloat("Smooth Rotation", ref cam.SmoothRotation, 0f, 40f, "%.1f",
                        entityId, "SmoothRotation",
                        tooltip: "Rotation interpolation speed");

                    InspectorWidgets.EndSection();
                }

                // === MOUSE/LOOK CONTROLS (FPS, ThirdPerson, Orbit, FreeCam) ===
                if (cam.Mode == CameraComponent.ControlMode.FirstPerson ||
                    cam.Mode == CameraComponent.ControlMode.ThirdPerson ||
                    cam.Mode == CameraComponent.ControlMode.Orbit ||
                    cam.Mode == CameraComponent.ControlMode.FreeCam)
                {
                    if (InspectorWidgets.Section("Mouse Controls", defaultOpen: false))
                    {
                        InspectorWidgets.SliderFloat("Sensitivity", ref cam.Sensitivity, 0.001f, 0.2f, "%.3f",
                            entityId, "Sensitivity",
                            tooltip: "Mouse look sensitivity");

                        InspectorWidgets.Checkbox("Invert Y", ref cam.InvertY, entityId, "InvertY");
                        InspectorWidgets.Checkbox("Invert X", ref cam.InvertX, entityId, "InvertX");

                        InspectorWidgets.SliderFloat("Min Pitch", ref cam.MinPitch, -89.9f, 0f, "%.1f°",
                            entityId, "MinPitch",
                            tooltip: "Minimum vertical angle (looking down)");

                        InspectorWidgets.SliderFloat("Max Pitch", ref cam.MaxPitch, 0f, 89.9f, "%.1f°",
                            entityId, "MaxPitch",
                            tooltip: "Maximum vertical angle (looking up)");

                        InspectorWidgets.EndSection();
                    }
                }

                // === DISTANCE/ZOOM CONTROLS (ThirdPerson, Orbit, TopDown, Isometric) ===
                if (cam.Mode == CameraComponent.ControlMode.ThirdPerson ||
                    cam.Mode == CameraComponent.ControlMode.Orbit ||
                    cam.Mode == CameraComponent.ControlMode.TopDown ||
                    cam.Mode == CameraComponent.ControlMode.Isometric)
                {
                    if (InspectorWidgets.Section("Distance & Zoom", defaultOpen: false))
                    {
                        InspectorWidgets.SliderFloat("Distance", ref cam.Distance, 0.1f, 100f, "%.2f",
                            entityId, "Distance");

                        InspectorWidgets.SliderFloat("Min Distance", ref cam.MinDistance, 0.1f, 50f, "%.2f",
                            entityId, "MinDistance");

                        InspectorWidgets.SliderFloat("Max Distance", ref cam.MaxDistance, 1f, 100f, "%.2f",
                            entityId, "MaxDistance");

                        InspectorWidgets.Checkbox("Enable Zoom", ref cam.EnableZoom, entityId, "EnableZoom");

                        if (cam.EnableZoom)
                        {
                            InspectorWidgets.SliderFloat("Zoom Speed", ref cam.ZoomSpeed, 0.1f, 5f, "%.2f",
                                entityId, "ZoomSpeed");

                            InspectorWidgets.Checkbox("Invert Zoom", ref cam.InvertZoomScroll, entityId, "InvertZoomScroll");
                        }

                        InspectorWidgets.EndSection();
                    }
                }

                // === COLLISION (ThirdPerson) ===
                if (cam.Mode == CameraComponent.ControlMode.ThirdPerson)
                {
                    if (InspectorWidgets.Section("Collision", defaultOpen: false))
                    {
                        InspectorWidgets.Checkbox("Enable Collision", ref cam.EnableCollision, entityId, "EnableCollision",
                            tooltip: "Prevent camera from clipping through geometry");

                        if (cam.EnableCollision)
                        {
                            InspectorWidgets.SliderFloat("Collision Margin", ref cam.CollisionMargin, 0.05f, 1f, "%.2f",
                                entityId, "CollisionMargin");

                            InspectorWidgets.IntField("Collision LayerMask", ref cam.CollisionLayerMask, entityId, "CollisionLayerMask");
                        }

                        InspectorWidgets.EndSection();
                    }
                }

                // === FIRST PERSON SPECIFIC ===
                if (cam.Mode == CameraComponent.ControlMode.FirstPerson)
                {
                    if (InspectorWidgets.Section("First Person Settings", defaultOpen: false))
                    {
                        var eyeOff = cam.FPSEyeOffset;
                        InspectorWidgets.Vector3FieldOTK("Eye Offset", ref eyeOff, 0.01f, entityId, "FPSEyeOffset");
                        cam.FPSEyeOffset = eyeOff;

                        InspectorWidgets.Checkbox("Enable WASD Move", ref cam.FPSEnableMove, entityId, "FPSEnableMove");

                        if (cam.FPSEnableMove)
                        {
                            InspectorWidgets.SliderFloat("Move Speed", ref cam.FPSMoveSpeed, 0.5f, 30f, "%.1f",
                                entityId, "FPSMoveSpeed");

                            InspectorWidgets.SliderFloat("Sprint Multiplier", ref cam.FPSSprintMultiplier, 1f, 4f, "%.2f",
                                entityId, "FPSSprintMultiplier");
                        }

                        InspectorWidgets.EndSection();
                    }
                }

                // === TOP DOWN SPECIFIC ===
                if (cam.Mode == CameraComponent.ControlMode.TopDown)
                {
                    if (InspectorWidgets.Section("Top Down Settings", defaultOpen: false))
                    {
                        InspectorWidgets.SliderFloat("View Angle", ref cam.TopDownAngle, 0f, 90f, "%.1f°",
                            entityId, "TopDownAngle",
                            tooltip: "0=straight down, 45=typical, 90=side view");

                        InspectorWidgets.Checkbox("Allow Rotation", ref cam.TopDownAllowRotation, entityId, "TopDownAllowRotation");

                        if (cam.TopDownAllowRotation)
                        {
                            InspectorWidgets.SliderFloat("Rotation Speed", ref cam.TopDownRotationSpeed, 0.1f, 5f, "%.2f",
                                entityId, "TopDownRotationSpeed");
                        }

                        InspectorWidgets.EndSection();
                    }
                }

                // === ISOMETRIC SPECIFIC ===
                if (cam.Mode == CameraComponent.ControlMode.Isometric)
                {
                    if (InspectorWidgets.Section("Isometric Settings", defaultOpen: false))
                    {
                        InspectorWidgets.SliderFloat("Isometric Angle", ref cam.IsometricAngle, 0f, 60f, "%.1f°",
                            entityId, "IsometricAngle",
                            tooltip: "Standard isometric is ~30°");

                        InspectorWidgets.SliderFloat("Isometric Yaw", ref cam.IsometricYaw, 0f, 360f, "%.1f°",
                            entityId, "IsometricYaw",
                            tooltip: "Horizontal rotation (45° for classic isometric)");

                        InspectorWidgets.EndSection();
                    }
                }

                // === 2D SIDE SCROLLER SPECIFIC ===
                if (cam.Mode == CameraComponent.ControlMode.SideScroller2D)
                {
                    if (InspectorWidgets.Section("Side Scroller Settings", defaultOpen: false))
                    {
                        var axis = cam.SideScrollerAxis;
                        InspectorWidgets.Vector3FieldOTK("Follow Axis", ref axis, 0.01f, entityId, "SideScrollerAxis",
                            tooltip: "Which axis to follow (X for side, Y for vertical)");
                        cam.SideScrollerAxis = axis;

                        InspectorWidgets.SliderFloat("Look Ahead", ref cam.SideScrollerLookAhead, 0f, 10f, "%.2f",
                            entityId, "SideScrollerLookAhead");

                        InspectorWidgets.SliderFloat("Dead Zone", ref cam.SideScrollerDeadZone, 0f, 5f, "%.2f",
                            entityId, "SideScrollerDeadZone");

                        InspectorWidgets.EndSection();
                    }
                }

                // === FREE CAM SPECIFIC ===
                if (cam.Mode == CameraComponent.ControlMode.FreeCam)
                {
                    if (InspectorWidgets.Section("Free Camera Settings", defaultOpen: true))
                    {
                        InspectorWidgets.SliderFloat("Move Speed", ref cam.FreeCamMoveSpeed, 0.5f, 100f, "%.1f",
                            entityId, "FreeCamMoveSpeed",
                            tooltip: "Camera movement speed (WASD + Space/Ctrl)");

                        InspectorWidgets.Checkbox("Enable Sprint", ref cam.FreeCamEnableFastMode, entityId, "FreeCamEnableFastMode",
                            tooltip: "Hold Shift to move faster");

                        if (cam.FreeCamEnableFastMode)
                        {
                            InspectorWidgets.SliderFloat("Sprint Multiplier", ref cam.FreeCamSprintMultiplier, 1f, 10f, "%.2f",
                                entityId, "FreeCamSprintMultiplier",
                                tooltip: "Speed multiplier when holding Shift");
                        }

                        ImGui.Spacing();
                        ImGui.TextColored(UI.TextDisabled, "Controls:");
                        ImGui.BulletText("WASD - Move forward/back/left/right");
                        ImGui.BulletText("Space - Move up");
                        ImGui.BulletText("Left Ctrl - Move down");
                        ImGui.BulletText("Mouse - Look around");
                        ImGui.BulletText("Shift - Sprint (if enabled)");

                        InspectorWidgets.EndSection();
                    }
                }
            }
        }
    }
}

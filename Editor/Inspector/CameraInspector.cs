using ImGuiNET;
using Engine.Components;
using OpenTK.Mathematics;
using Editor.Panels;
using Editor.Inspector;

namespace Editor.Inspector
{
    /// <summary>
    /// Professional Unity-style Camera Component inspector with validation and tooltips
    /// </summary>
    public static class CameraInspector
    {
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

                // Show appropriate projection parameters based on mode
                if (cam.Projection == CameraComponent.ProjectionMode.Perspective)
                {
                    float fovDeg = MathHelper.RadiansToDegrees(cam.FieldOfView);
                    InspectorWidgets.SliderAngle("Field of View", ref fovDeg, 1f, 170f,
                        entityId, "FieldOfView",
                        tooltip: "Camera's viewing angle. Lower = more zoomed in",
                        helpText: "Common values: 60° (first-person), 45° (third-person), 90° (wide angle)");
                    cam.FieldOfView = MathHelper.DegreesToRadians(fovDeg);

                    // FOV Presets
                    int preset = InspectorWidgets.PresetButtonRow(
                        ("First Person (60°)", "Standard FPS camera"),
                        ("Third Person (45°)", "Standard over-shoulder camera"),
                        ("Wide Angle (90°)", "Wide field of view"));
                    
                    if (preset == 0) cam.FieldOfView = MathHelper.DegreesToRadians(60f);
                    else if (preset == 1) cam.FieldOfView = MathHelper.DegreesToRadians(45f);
                    else if (preset == 2) cam.FieldOfView = MathHelper.DegreesToRadians(90f);
                }
                else // Orthographic or 2D
                {
                    InspectorWidgets.FloatField("Ortho Size", ref cam.OrthoSize, entityId, "OrthoSize",
                        speed: 0.1f, min: 0.1f, max: 1000f,
                        tooltip: "Half-height of the camera view in world units",
                        helpText: "Larger = more visible area (zoomed out)");
                }

                InspectorWidgets.EndSection();
            }

            // === CLIPPING PLANES SECTION ===
            if (InspectorWidgets.Section("Clipping Planes", defaultOpen: true,
                tooltip: "Near and far render distance"))
            {
                InspectorWidgets.FloatField("Near", ref cam.Near, entityId, "Near",
                    speed: 0.01f, min: 0.001f, max: 10f, format: "%.3f",
                    tooltip: "Closest distance the camera can see",
                    validate: (n) => n < cam.Far ? null : "Near must be less than Far",
                    helpText: "Too small = depth precision issues. Too large = objects clip close to camera");

                InspectorWidgets.FloatField("Far", ref cam.Far, entityId, "Far",
                    speed: 1f, min: 10f, max: 100000f,
                    tooltip: "Farthest distance the camera can see",
                    validate: (f) => f > cam.Near ? null : "Far must be greater than Near",
                    helpText: "Larger = more draw distance but worse depth precision");

                InspectorWidgets.EndSection();
            }

            // === CAMERA SETTINGS ===
            if (InspectorWidgets.Section("Camera Settings", defaultOpen: true))
            {
                InspectorWidgets.Checkbox("Main Camera", ref cam.IsMain, entityId, "IsMain",
                    tooltip: "This camera will be used for rendering (only one should be main)",
                    helpText: "The main camera renders to the viewport. Only one camera should be main at a time");

                var stage = cam.Stage;
                InspectorWidgets.EnumField("Update Stage", ref stage, entityId, "Stage",
                    tooltip: "When the camera updates its position/rotation");
                cam.Stage = stage;

                var mode = cam.Mode;
                InspectorWidgets.EnumField("Behavior", ref mode, entityId, "Mode",
                    tooltip: "Camera control mode: Manual (script-controlled), FPS (WASD+mouse), Orbit (rotate around target)");
                cam.Mode = mode;

                InspectorWidgets.SliderFloat("Smooth Position", ref cam.SmoothPosition, 0f, 40f, "%.1f",
                    entityId, "SmoothPosition",
                    tooltip: "Position interpolation speed (0 = instant, higher = smoother)",
                    helpText: "Reduces jittery movement. 10-20 is good for gameplay cameras");

                InspectorWidgets.SliderFloat("Smooth Rotation", ref cam.SmoothRotation, 0f, 40f, "%.1f",
                    entityId, "SmoothRotation",
                    tooltip: "Rotation interpolation speed (0 = instant, higher = smoother)",
                    helpText: "Reduces jittery rotation. 10-20 is good for gameplay cameras");

                InspectorWidgets.EndSection();
            }

            // === FPS MODE SETTINGS ===
            if (cam.Mode == CameraComponent.Behavior.FPS)
            {
                if (InspectorWidgets.Section("FPS Settings", defaultOpen: false,
                    tooltip: "First-person camera controls with WASD movement"))
                {
                    InspectorWidgets.Checkbox("Enable Move (WASD)", ref cam.FpsEnableMove, entityId, "FpsEnableMove",
                        tooltip: "Allow movement with WASD keys");

                    InspectorWidgets.SliderFloat("Move Speed", ref cam.FpsMoveSpeed, 0.5f, 30f, "%.1f",
                        entityId, "FpsMoveSpeed",
                        tooltip: "Base movement speed (units per second)",
                        helpText: "Typical values: 5-10 for walking, 15-20 for running");

                    InspectorWidgets.SliderFloat("Sprint Multiplier", ref cam.FpsSprintMultiplier, 1f, 4f, "%.2f",
                        entityId, "FpsSprintMultiplier",
                        tooltip: "Speed multiplier when sprinting (hold Shift)");

                    InspectorWidgets.SliderFloat("Mouse Sensitivity", ref cam.FpsSensitivity, 0.001f, 0.2f, "%.3f",
                        entityId, "FpsSensitivity",
                        tooltip: "Mouse look sensitivity");

                    InspectorWidgets.Checkbox("Invert Y Axis", ref cam.FpsInvertY, entityId, "FpsInvertY",
                        tooltip: "Invert vertical mouse look (flight sim style)");

                    InspectorWidgets.EndSection();
                }
            }

            // === ORBIT/FOLLOW MODE SETTINGS ===
            if (cam.Mode == CameraComponent.Behavior.OrbitFollow)
            {
                if (InspectorWidgets.Section("Orbit & Follow Settings", defaultOpen: false,
                    tooltip: "Third-person camera that orbits around a target"))
                {
                    // Follow target reference (component)
                    var scene = EditorUI.MainViewport.Renderer?.Scene;
                    if (scene != null)
                    {
                        TransformComponent? t = cam.FollowTarget;
                        if (FieldWidgets.ComponentRef("Follow Target", scene, ref t))
                            cam.FollowTarget = t;
                        
                        if (cam.FollowTarget == null)
                        {
                            InspectorWidgets.WarningBox("No follow target assigned. Camera will orbit around origin (0,0,0)");
                        }
                    }

                    var off = cam.TargetOffset;
                    InspectorWidgets.Vector3FieldOTK("Target Offset", ref off, 0.01f, entityId, "TargetOffset",
                        tooltip: "Offset from target position to look at",
                        helpText: "Use to look at character's head instead of feet");
                    cam.TargetOffset = off;

                    InspectorWidgets.SliderFloat("Orbit Sensitivity", ref cam.OrbitSensitivity, 0.001f, 0.2f, "%.3f",
                        entityId, "OrbitSensitivity",
                        tooltip: "Mouse sensitivity for orbiting around target");

                    InspectorWidgets.Checkbox("Orbit Behind Target", ref cam.OrbitBehindTarget, entityId, "OrbitBehindTarget",
                        tooltip: "Automatically position camera behind target's forward direction",
                        helpText: "Useful for follow-cam that stays behind moving character");

                    InspectorWidgets.Checkbox("Invert Look X", ref cam.InvertLookX, entityId, "InvertLookX",
                        tooltip: "Invert horizontal mouse look");

                    InspectorWidgets.Checkbox("Invert Look Y", ref cam.InvertLookY, entityId, "InvertLookY",
                        tooltip: "Invert vertical mouse look");

                    InspectorWidgets.Separator();

                    // Pitch limits
                    InspectorWidgets.SliderFloat("Min Pitch", ref cam.MinPitchDeg, -89.9f, 0f, "%.1f°",
                        entityId, "MinPitchDeg",
                        tooltip: "Minimum vertical angle (looking down)",
                        validate: (v) => v < cam.MaxPitchDeg ? null : "Min pitch must be less than max pitch");

                    InspectorWidgets.SliderFloat("Max Pitch", ref cam.MaxPitchDeg, 0f, 89.9f, "%.1f°",
                        entityId, "MaxPitchDeg",
                        tooltip: "Maximum vertical angle (looking up)",
                        validate: (v) => v > cam.MinPitchDeg ? null : "Max pitch must be greater than min pitch");

                    InspectorWidgets.Separator();

                    // Zoom settings
                    InspectorWidgets.Checkbox("Enable Zoom", ref cam.EnableZoom, entityId, "EnableZoom",
                        tooltip: "Allow zooming in/out with mouse wheel");

                    if (cam.EnableZoom)
                    {
                        ImGui.Indent();

                        InspectorWidgets.Checkbox("Invert Zoom Direction", ref cam.InvertZoomScroll, entityId, "InvertZoomScroll",
                            tooltip: "Scroll up = zoom out (inverted)");

                        InspectorWidgets.SliderFloat("Min Distance", ref cam.MinDistance, 0.1f, cam.MaxDistance - 0.01f, "%.2f",
                            entityId, "MinDistance",
                            tooltip: "Minimum distance from target (zoomed in)",
                            validate: (v) => v < cam.MaxDistance ? null : "Min distance must be less than max distance");

                        InspectorWidgets.SliderFloat("Max Distance", ref cam.MaxDistance, cam.MinDistance + 0.01f, 100f, "%.2f",
                            entityId, "MaxDistance",
                            tooltip: "Maximum distance from target (zoomed out)",
                            validate: (v) => v > cam.MinDistance ? null : "Max distance must be greater than min distance");

                        InspectorWidgets.SliderFloat("Zoom Speed", ref cam.ZoomSpeed, 0.1f, 5f, "%.2f",
                            entityId, "ZoomSpeed",
                            tooltip: "How fast the camera zooms in/out");

                        InspectorWidgets.SliderFloat("Zoom Smoothing", ref cam.ZoomSmooth, 0f, 40f, "%.1f",
                            entityId, "ZoomSmooth",
                            tooltip: "Zoom interpolation speed (0 = instant, higher = smoother)");

                        ImGui.Unindent();
                    }

                    InspectorWidgets.Separator();

                    // Collision settings
                    InspectorWidgets.Checkbox("Enable Collision", ref cam.EnableCollision, entityId, "EnableCollision",
                        tooltip: "Prevent camera from clipping through geometry",
                        helpText: "Uses raycasts to keep camera outside colliders");

                    if (cam.EnableCollision)
                    {
                        ImGui.Indent();

                        InspectorWidgets.SliderFloat("Collision Radius", ref cam.CollisionRadius, 0.05f, 1f, "%.2f",
                            entityId, "CollisionRadius",
                            tooltip: "Radius of camera collision sphere");

                        InspectorWidgets.IntField("Collision LayerMask", ref cam.CollisionLayerMask, entityId, "CollisionLayerMask",
                            tooltip: "Which layers to check for collisions (bitfield)",
                            helpText: "Default = -1 (all layers). Use layer masks to ignore certain objects");

                        ImGui.Unindent();
                    }

                    InspectorWidgets.EndSection();
                }
            }
        }
    }
}

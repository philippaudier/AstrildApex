using System;
using ImGuiNET;
using Engine.Components;
using Editor.Icons;
using System.Numerics;

namespace Editor.Inspector
{
    public static class ParticleSystemInspector
    {
        public static void Draw(ParticleSystem ps)
        {
            if (ps == null) return;

            ImGui.PushID(ps.GetHashCode());

            // Playback controls
            DrawPlaybackControls(ps);
            ImGui.Separator();

            // Core settings
            if (ImGui.CollapsingHeader("Core Settings", ImGuiTreeNodeFlags.DefaultOpen))
            {
                DrawCoreSettings(ps);
            }

            // Emission module
            if (ImGui.CollapsingHeader("Emission", ImGuiTreeNodeFlags.DefaultOpen))
            {
                DrawEmissionModule(ps);
            }

            // Shape module
            if (ImGui.CollapsingHeader("Shape", ImGuiTreeNodeFlags.DefaultOpen))
            {
                DrawShapeModule(ps);
            }

            // Particle properties
            if (ImGui.CollapsingHeader("Particle Properties", ImGuiTreeNodeFlags.DefaultOpen))
            {
                DrawParticleProperties(ps);
            }

            // Velocity over lifetime
            if (ImGui.CollapsingHeader("Velocity Over Lifetime"))
            {
                DrawVelocityOverLifetime(ps);
            }

            // Color over lifetime
            if (ImGui.CollapsingHeader("Color Over Lifetime"))
            {
                DrawColorOverLifetime(ps);
            }

            // Size over lifetime
            if (ImGui.CollapsingHeader("Size Over Lifetime"))
            {
                DrawSizeOverLifetime(ps);
            }

            // Rotation over lifetime
            if (ImGui.CollapsingHeader("Rotation Over Lifetime"))
            {
                DrawRotationOverLifetime(ps);
            }

            // Renderer settings
            if (ImGui.CollapsingHeader("Renderer"))
            {
                DrawRendererSettings(ps);
            }

            // Statistics
            if (ImGui.CollapsingHeader("Statistics"))
            {
                DrawStatistics(ps);
            }

            ImGui.PopID();
        }

        private static void DrawPlaybackControls(ParticleSystem ps)
        {
            ImGui.Text("Playback Control");
            ImGui.Spacing();

            // Play/Stop/Pause buttons in a row
            if (ps.IsPlaying && !ps.IsPaused)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.7f, 0.2f, 1.0f));
                if (ImGui.Button("Playing", new Vector2(80, 0)))
                {
                    ps.Stop();
                }
                ImGui.PopStyleColor();
            }
            else
            {
                if (ImGui.Button("Play", new Vector2(80, 0)))
                {
                    ps.Play();
                }
            }

            ImGui.SameLine();

            if (ImGui.Button("Stop", new Vector2(80, 0)))
            {
                ps.Stop();
            }

            ImGui.SameLine();

            if (ps.IsPaused)
            {
                if (ImGui.Button("Resume", new Vector2(80, 0)))
                {
                    ps.Resume();
                }
            }
            else
            {
                if (ImGui.Button("Pause", new Vector2(80, 0)))
                {
                    ps.Pause();
                }
            }

            ImGui.SameLine();

            if (ImGui.Button("Clear", new Vector2(80, 0)))
            {
                ps.Clear();
            }
        }

        private static void DrawCoreSettings(ParticleSystem ps)
        {
            ImGui.Indent();

            // Max particles
            int maxParticles = ps.MaxParticles;
            if (ImGui.DragInt("Max Particles", ref maxParticles, 10, 1, 100000))
            {
                ps.MaxParticles = Math.Max(1, maxParticles);
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Maximum number of particles that can be alive at once");

            // Duration
            float duration = ps.Duration;
            if (ImGui.DragFloat("Duration", ref duration, 0.1f, 0.1f, 100.0f))
            {
                ps.Duration = Math.Max(0.1f, duration);
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("How long the particle system runs before stopping (if not looping)");

            // Looping
            bool looping = ps.Looping;
            if (ImGui.Checkbox("Looping", ref looping))
            {
                ps.Looping = looping;
            }

            // Play on awake
            bool playOnAwake = ps.PlayOnAwake;
            if (ImGui.Checkbox("Play On Awake", ref playOnAwake))
            {
                ps.PlayOnAwake = playOnAwake;
            }

            // Simulation space
            int space = (int)ps.Space;
            string[] spaceNames = { "World", "Local" };
            if (ImGui.Combo("Simulation Space", ref space, spaceNames, spaceNames.Length))
            {
                ps.Space = (SimulationSpace)space;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("World: particles move in world space\nLocal: particles move relative to emitter");

            ImGui.Unindent();
        }

        private static void DrawEmissionModule(ParticleSystem ps)
        {
            ImGui.Indent();

            bool enabled = ps.EmissionEnabled;
            if (ImGui.Checkbox("Enabled##Emission", ref enabled))
            {
                ps.EmissionEnabled = enabled;
            }

            if (enabled)
            {
                float rate = ps.EmissionRate;
                if (ImGui.DragFloat("Rate Over Time", ref rate, 0.1f, 0, 1000))
                {
                    ps.EmissionRate = Math.Max(0, rate);
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Number of particles emitted per second");
            }

            ImGui.Unindent();
        }

        private static void DrawShapeModule(ParticleSystem ps)
        {
            ImGui.Indent();

            // Shape type
            int shape = (int)ps.Shape;
            string[] shapeNames = { "Sphere", "Cone", "Box", "Circle" };
            if (ImGui.Combo("Shape##ShapeType", ref shape, shapeNames, shapeNames.Length))
            {
                ps.Shape = (ShapeType)shape;
            }

            // Shape-specific parameters
            switch (ps.Shape)
            {
                case ShapeType.Sphere:
                case ShapeType.Circle:
                    float radius = ps.ShapeRadius;
                    if (ImGui.DragFloat("Radius", ref radius, 0.1f, 0.01f, 100.0f))
                    {
                        ps.ShapeRadius = Math.Max(0.01f, radius);
                    }
                    break;

                case ShapeType.Cone:
                    float coneRadius = ps.ShapeRadius;
                    if (ImGui.DragFloat("Radius##Cone", ref coneRadius, 0.1f, 0.01f, 100.0f))
                    {
                        ps.ShapeRadius = Math.Max(0.01f, coneRadius);
                    }

                    float angle = ps.ShapeAngle;
                    if (ImGui.SliderFloat("Angle", ref angle, 0, 90))
                    {
                        ps.ShapeAngle = angle;
                    }
                    break;

                case ShapeType.Box:
                    var box = ps.ShapeBox;
                    var boxVec = new Vector3(box.X, box.Y, box.Z);
                    if (ImGui.DragFloat3("Box Size", ref boxVec, 0.1f, 0.01f, 100.0f))
                    {
                        ps.ShapeBox = new OpenTK.Mathematics.Vector3(
                            Math.Max(0.01f, boxVec.X),
                            Math.Max(0.01f, boxVec.Y),
                            Math.Max(0.01f, boxVec.Z)
                        );
                    }
                    break;
            }

            ImGui.Unindent();
        }

        private static void DrawParticleProperties(ParticleSystem ps)
        {
            ImGui.Indent();

            // Start Lifetime
            ImGui.Text("Start Lifetime");
            DrawMinMaxCurve(ps.StartLifetime, 0.1f, 0.1f, 100.0f);

            // Start Speed
            ImGui.Text("Start Speed");
            DrawMinMaxCurve(ps.StartSpeed, 0.1f, 0, 100.0f);

            // Start Size
            ImGui.Text("Start Size");
            DrawMinMaxCurve(ps.StartSize, 0.01f, 0.01f, 10.0f);

            // Start Rotation
            ImGui.Text("Start Rotation (degrees)");
            DrawMinMaxCurve(ps.StartRotation, 1.0f, -360, 360);

            // Start Color
            var color = ps.StartColor;
            var colorVec = new Vector4(color.R, color.G, color.B, color.A);
            if (ImGui.ColorEdit4("Start Color", ref colorVec))
            {
                ps.StartColor = new OpenTK.Mathematics.Color4(colorVec.X, colorVec.Y, colorVec.Z, colorVec.W);
            }

            // Gravity
            float gravity = ps.GravityMultiplier;
            if (ImGui.DragFloat("Gravity Multiplier", ref gravity, 0.01f, -10, 10))
            {
                ps.GravityMultiplier = gravity;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Multiplier for world gravity (9.81 m/s²)");

            ImGui.Unindent();
        }

        private static void DrawMinMaxCurve(MinMaxCurve curve, float speed, float min, float max)
        {
            if (curve == null) return;

            ImGui.PushID(curve.GetHashCode());
            ImGui.Indent();

            int mode = (int)curve.Mode;
            string[] modeNames = { "Constant", "Random Between Two Constants", "Curve" };
            if (ImGui.Combo("Mode", ref mode, modeNames, modeNames.Length))
            {
                curve.Mode = (CurveMode)mode;
            }

            switch (curve.Mode)
            {
                case CurveMode.Constant:
                    float constant = curve.Constant;
                    if (ImGui.DragFloat("Value", ref constant, speed, min, max))
                    {
                        curve.Constant = constant;
                    }
                    break;

                case CurveMode.Random:
                    float minVal = curve.Min;
                    float maxVal = curve.Max;
                    if (ImGui.DragFloat("Min", ref minVal, speed, min, max))
                    {
                        curve.Min = minVal;
                    }
                    if (ImGui.DragFloat("Max", ref maxVal, speed, min, max))
                    {
                        curve.Max = Math.Max(minVal, maxVal);
                    }
                    break;
            }

            ImGui.Unindent();
            ImGui.PopID();
        }

        private static void DrawVelocityOverLifetime(ParticleSystem ps)
        {
            ImGui.Indent();

            bool enabled = ps.VelocityOverLifetimeEnabled;
            if (ImGui.Checkbox("Enabled##VelocityOL", ref enabled))
            {
                ps.VelocityOverLifetimeEnabled = enabled;
            }

            if (enabled)
            {
                var vel = ps.VelocityOverLifetime;
                var velVec = new Vector3(vel.X, vel.Y, vel.Z);
                if (ImGui.DragFloat3("Velocity", ref velVec, 0.1f))
                {
                    ps.VelocityOverLifetime = new OpenTK.Mathematics.Vector3(velVec.X, velVec.Y, velVec.Z);
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Constant velocity added over lifetime (m/s)");
            }

            ImGui.Unindent();
        }

        private static void DrawColorOverLifetime(ParticleSystem ps)
        {
            ImGui.Indent();

            bool enabled = ps.ColorOverLifetimeEnabled;
            if (ImGui.Checkbox("Enabled##ColorOL", ref enabled))
            {
                ps.ColorOverLifetimeEnabled = enabled;
            }

            if (enabled)
            {
                ImGui.TextDisabled("Color gradient editing coming soon");
                // TODO: Implement gradient editor
            }

            ImGui.Unindent();
        }

        private static void DrawSizeOverLifetime(ParticleSystem ps)
        {
            ImGui.Indent();

            bool enabled = ps.SizeOverLifetimeEnabled;
            if (ImGui.Checkbox("Enabled##SizeOL", ref enabled))
            {
                ps.SizeOverLifetimeEnabled = enabled;
            }

            if (enabled)
            {
                ImGui.TextDisabled("Curve editor coming soon");
                // TODO: Implement curve editor
            }

            ImGui.Unindent();
        }

        private static void DrawRotationOverLifetime(ParticleSystem ps)
        {
            ImGui.Indent();

            bool enabled = ps.RotationOverLifetimeEnabled;
            if (ImGui.Checkbox("Enabled##RotationOL", ref enabled))
            {
                ps.RotationOverLifetimeEnabled = enabled;
            }

            if (enabled)
            {
                float speed = ps.RotationOverLifetimeSpeed;
                if (ImGui.DragFloat("Angular Velocity", ref speed, 1.0f, -360, 360))
                {
                    ps.RotationOverLifetimeSpeed = speed;
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Rotation speed in degrees per second");
            }

            ImGui.Unindent();
        }

        private static void DrawRendererSettings(ParticleSystem ps)
        {
            ImGui.Indent();

            // Render mode
            int renderMode = (int)ps.RenderMode;
            string[] renderModeNames = { "Billboard", "Mesh", "Stretched Billboard" };
            if (ImGui.Combo("Render Mode", ref renderMode, renderModeNames, renderModeNames.Length))
            {
                ps.RenderMode = (RenderMode)renderMode;
            }

            // Material
            ImGui.Text("Material: " + (ps.MaterialGuid == Guid.Empty ? "None" : ps.MaterialGuid.ToString()));
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Drag a material here (TODO: implement material drag-drop)");

            // Sorting
            int sorting = (int)ps.SortingMode;
            string[] sortingNames = { "None", "Oldest In Front", "Youngest In Front", "By Distance" };
            if (ImGui.Combo("Sorting Mode", ref sorting, sortingNames, sortingNames.Length))
            {
                ps.SortingMode = (SortingMode)sorting;
            }

            ImGui.Unindent();
        }

        private static void DrawStatistics(ParticleSystem ps)
        {
            ImGui.Indent();

            ImGui.Text($"Particles: {ps.ParticleCount} / {ps.MaxParticles}");

            float fillPercentage = ps.MaxParticles > 0 ? (float)ps.ParticleCount / ps.MaxParticles * 100.0f : 0;
            ImGui.ProgressBar(fillPercentage / 100.0f, new Vector2(-1, 0), $"{fillPercentage:F1}%");

            ImGui.Text($"Playing: {(ps.IsPlaying ? "Yes" : "No")}");
            ImGui.Text($"Paused: {(ps.IsPaused ? "Yes" : "No")}");

            ImGui.Unindent();
        }
    }
}

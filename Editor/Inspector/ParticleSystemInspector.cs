using System;
using ImGuiNET;
using Engine.Components;
using Editor.Icons;
using Editor.UI;
using Editor.Themes;
using System.Numerics;

namespace Editor.Inspector
{
    public static class ParticleSystemInspector
    {
        private static UITheme UI => ThemeManager.UI;
        public static void Draw(ParticleSystem ps)
        {
            if (ps == null) return;

            ImGui.PushID(ps.GetHashCode());

            // Playback controls
            DrawPlaybackControls(ps);
            ImGui.Separator();

            // Core settings
            if (ThemedImGui.CollapsingHeader("Core Settings", ImGuiTreeNodeFlags.DefaultOpen))
            {
                DrawCoreSettings(ps);
            }

            // Emission module
            if (ThemedImGui.CollapsingHeader("Emission", ImGuiTreeNodeFlags.DefaultOpen))
            {
                DrawEmissionModule(ps);
            }

            // Shape module
            if (ThemedImGui.CollapsingHeader("Shape", ImGuiTreeNodeFlags.DefaultOpen))
            {
                DrawShapeModule(ps);
            }

            // Particle properties
            if (ThemedImGui.CollapsingHeader("Particle Properties", ImGuiTreeNodeFlags.DefaultOpen))
            {
                DrawParticleProperties(ps);
            }

            // Velocity over lifetime
            if (ThemedImGui.CollapsingHeader("Velocity Over Lifetime"))
            {
                DrawVelocityOverLifetime(ps);
            }

            // Color over lifetime
            if (ThemedImGui.CollapsingHeader("Color Over Lifetime"))
            {
                DrawColorOverLifetime(ps);
            }

            // Fade
            if (ThemedImGui.CollapsingHeader("Fade"))
            {
                DrawFade(ps);
            }

            // Size over lifetime
            if (ThemedImGui.CollapsingHeader("Size Over Lifetime"))
            {
                DrawSizeOverLifetime(ps);
            }

            // Rotation over lifetime
            if (ThemedImGui.CollapsingHeader("Rotation Over Lifetime"))
            {
                DrawRotationOverLifetime(ps);
            }

            // Trails
            if (ThemedImGui.CollapsingHeader("Trails"))
            {
                DrawTrails(ps);
            }

            // Renderer settings
            if (ThemedImGui.CollapsingHeader("Renderer"))
            {
                DrawRendererSettings(ps);
            }

            // Statistics
            if (ThemedImGui.CollapsingHeader("Statistics"))
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

            // 3D Rotation
            bool use3DRotation = ps.Use3DRotation;
            if (ImGui.Checkbox("Use 3D Rotation", ref use3DRotation))
            {
                ps.Use3DRotation = use3DRotation;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Enable random 3D rotation on all axes (X, Y, Z)");

            if (ps.Use3DRotation)
            {
                ImGui.Indent();

                // Min rotation 3D
                var rotMin = new Vector3(ps.StartRotation3DMin.X, ps.StartRotation3DMin.Y, ps.StartRotation3DMin.Z);
                if (ImGui.DragFloat3("Min Rotation (X, Y, Z)", ref rotMin, 1.0f, -360, 360))
                {
                    ps.StartRotation3DMin = new OpenTK.Mathematics.Vector3(rotMin.X, rotMin.Y, rotMin.Z);
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Minimum rotation in degrees for each axis");

                // Max rotation 3D
                var rotMax = new Vector3(ps.StartRotation3DMax.X, ps.StartRotation3DMax.Y, ps.StartRotation3DMax.Z);
                if (ImGui.DragFloat3("Max Rotation (X, Y, Z)", ref rotMax, 1.0f, -360, 360))
                {
                    ps.StartRotation3DMax = new OpenTK.Mathematics.Vector3(rotMax.X, rotMax.Y, rotMax.Z);
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Maximum rotation in degrees for each axis");

                ImGui.Unindent();
            }

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

        private static void DrawFade(ParticleSystem ps)
        {
            ImGui.Indent();

            // Fade In
            bool fadeInEnabled = ps.FadeInEnabled;
            if (ImGui.Checkbox("Fade In", ref fadeInEnabled))
            {
                ps.FadeInEnabled = fadeInEnabled;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Gradually fade in particles when they spawn");

            if (fadeInEnabled)
            {
                ImGui.Indent();
                float fadeInDuration = ps.FadeInDuration;
                if (ImGui.DragFloat("Duration (seconds)##FadeIn", ref fadeInDuration, 0.01f, 0.0f, 10.0f))
                {
                    ps.FadeInDuration = Math.Max(0.0f, fadeInDuration);
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("How long the fade in takes");
                ImGui.Unindent();
            }

            ImGui.Spacing();

            // Fade Out
            bool fadeOutEnabled = ps.FadeOutEnabled;
            if (ImGui.Checkbox("Fade Out", ref fadeOutEnabled))
            {
                ps.FadeOutEnabled = fadeOutEnabled;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Gradually fade out particles before they die");

            if (fadeOutEnabled)
            {
                ImGui.Indent();
                float fadeOutDuration = ps.FadeOutDuration;
                if (ImGui.DragFloat("Duration (seconds)##FadeOut", ref fadeOutDuration, 0.01f, 0.0f, 10.0f))
                {
                    ps.FadeOutDuration = Math.Max(0.0f, fadeOutDuration);
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("How long before death the fade out starts");
                ImGui.Unindent();
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
                if (ImGui.DragFloat("Angular Velocity (2D)", ref speed, 1.0f, -360, 360))
                {
                    ps.RotationOverLifetimeSpeed = speed;
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("2D rotation speed in degrees per second");
            }

            // 3D Rotation Over Lifetime
            if (ps.Use3DRotation)
            {
                ImGui.Separator();
                ImGui.Text("3D Rotation Over Lifetime");

                var rot3D = new Vector3(ps.RotationOverLifetime3D.X, ps.RotationOverLifetime3D.Y, ps.RotationOverLifetime3D.Z);
                if (ImGui.DragFloat3("Angular Velocity 3D (X, Y, Z)", ref rot3D, 1.0f, -360, 360))
                {
                    ps.RotationOverLifetime3D = new OpenTK.Mathematics.Vector3(rot3D.X, rot3D.Y, rot3D.Z);
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Rotation speed for each axis in degrees per second");
            }

            ImGui.Unindent();
        }

        private static void DrawTrails(ParticleSystem ps)
        {
            ImGui.Indent();

            bool trailsEnabled = ps.TrailsEnabled;
            if (ImGui.Checkbox("Enable Trails", ref trailsEnabled))
            {
                ps.TrailsEnabled = trailsEnabled;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Add trails/streaks behind particles (useful for rain, projectiles, smoke, etc.)");

            if (trailsEnabled)
            {
                ImGui.Separator();

                // Lifetime
                float lifetime = ps.TrailLifetime;
                if (ImGui.DragFloat("Lifetime (seconds)", ref lifetime, 0.01f, 0.01f, 10.0f))
                {
                    ps.TrailLifetime = Math.Max(0.01f, lifetime);
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("How long trail points stay visible");

                // Width
                ImGui.Text("Width");
                ImGui.Indent();
                float widthStart = ps.TrailWidthStart;
                if (ImGui.DragFloat("Start##TrailWidth", ref widthStart, 0.01f, 0.01f, 10.0f))
                {
                    ps.TrailWidthStart = Math.Max(0.01f, widthStart);
                }

                float widthEnd = ps.TrailWidthEnd;
                if (ImGui.DragFloat("End##TrailWidth", ref widthEnd, 0.01f, 0.001f, 10.0f))
                {
                    ps.TrailWidthEnd = Math.Max(0.001f, widthEnd);
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Width tapers from start to end over lifetime");
                ImGui.Unindent();

                // Color
                ImGui.Text("Color");
                ImGui.Indent();
                var colorStart = ps.TrailColorStart;
                var colorStartVec = new Vector4(colorStart.R, colorStart.G, colorStart.B, colorStart.A);
                if (ImGui.ColorEdit4("Start##TrailColor", ref colorStartVec))
                {
                    ps.TrailColorStart = new OpenTK.Mathematics.Color4(colorStartVec.X, colorStartVec.Y, colorStartVec.Z, colorStartVec.W);
                }

                var colorEnd = ps.TrailColorEnd;
                var colorEndVec = new Vector4(colorEnd.R, colorEnd.G, colorEnd.B, colorEnd.A);
                if (ImGui.ColorEdit4("End##TrailColor", ref colorEndVec))
                {
                    ps.TrailColorEnd = new OpenTK.Mathematics.Color4(colorEndVec.X, colorEndVec.Y, colorEndVec.Z, colorEndVec.W);
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Color fades from start to end over lifetime");
                ImGui.Unindent();

                // Generate Mode
                ImGui.Separator();
                int generateMode = (int)ps.TrailGenerateMode;
                string[] generateModeNames = { "Per Second", "Per Unit Distance" };
                if (ImGui.Combo("Generate Mode", ref generateMode, generateModeNames, generateModeNames.Length))
                {
                    ps.TrailGenerateMode = (TrailGenerateMode)generateMode;
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("How trail points are generated:\n- Per Second: Time-based generation\n- Per Unit: Distance-based generation");

                // Generate Rate
                if (ps.TrailGenerateMode == TrailGenerateMode.PerSecond)
                {
                    float rate = ps.TrailGenerateRate;
                    if (ImGui.DragFloat("Points Per Second", ref rate, 0.1f, 0.1f, 100.0f))
                    {
                        ps.TrailGenerateRate = Math.Max(0.1f, rate);
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("How many trail points to generate per second");
                }
                else
                {
                    float minDistance = ps.TrailMinVertexDistance;
                    if (ImGui.DragFloat("Min Vertex Distance", ref minDistance, 0.01f, 0.01f, 10.0f))
                    {
                        ps.TrailMinVertexDistance = Math.Max(0.01f, minDistance);
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Minimum distance particle must travel to create a new trail point");
                }

                // Texture
                ImGui.Separator();
                var newTrailTexture = EditorWidgets.AssetField(
                    "Texture",
                    ps.TrailTextureGuid == Guid.Empty ? (Guid?)null : ps.TrailTextureGuid,
                    "Texture",
                    "Optional texture for the trail ribbon",
                    showPreview: true);

                if (newTrailTexture != ps.TrailTextureGuid)
                {
                    ps.TrailTextureGuid = newTrailTexture ?? Guid.Empty;
                }
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

            // Blend mode
            int blendMode = (int)ps.BlendMode;
            string[] blendModeNames = { "Alpha Blend", "Additive", "Multiply" };
            if (ImGui.Combo("Blend Mode", ref blendMode, blendModeNames, blendModeNames.Length))
            {
                ps.BlendMode = (BlendMode)blendMode;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Alpha Blend: Standard transparency\nAdditive: Glowing/fire effects\nMultiply: Darkening/smoke effects");

            // Texture with drag and drop
            var newTexture = EditorWidgets.AssetField(
                "Texture",
                ps.TextureGuid == Guid.Empty ? (Guid?)null : ps.TextureGuid,
                "Texture",
                "Particle texture or sprite sheet",
                showPreview: true);

            if (newTexture != ps.TextureGuid)
            {
                ps.TextureGuid = newTexture ?? Guid.Empty;
            }

            // Sprite sheet settings
            if (ps.TextureGuid != Guid.Empty)
            {
                ImGui.Spacing();

                int rows = ps.SpriteSheetRows;
                if (ImGui.DragInt("Sprite Rows", ref rows, 0.1f, 1, 16))
                {
                    ps.SpriteSheetRows = Math.Max(1, rows);
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Number of rows in the sprite sheet");

                int columns = ps.SpriteSheetColumns;
                if (ImGui.DragInt("Sprite Columns", ref columns, 0.1f, 1, 16))
                {
                    ps.SpriteSheetColumns = Math.Max(1, columns);
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Number of columns in the sprite sheet");

                bool randomSprite = ps.RandomSpriteIndex;
                if (ImGui.Checkbox("Random Sprite", ref randomSprite))
                {
                    ps.RandomSpriteIndex = randomSprite;
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Each particle gets a random sprite from the sheet");

                ImGui.Spacing();

                bool flipH = ps.FlipHorizontally;
                if (ImGui.Checkbox("Flip Horizontally", ref flipH))
                {
                    ps.FlipHorizontally = flipH;
                }

                bool flipV = ps.FlipVertically;
                if (ImGui.Checkbox("Flip Vertically", ref flipV))
                {
                    ps.FlipVertically = flipV;
                }

                bool randomFlip = ps.RandomFlip;
                if (ImGui.Checkbox("Random Flip", ref randomFlip))
                {
                    ps.RandomFlip = randomFlip;
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Each particle randomly flips horizontally and/or vertically");
            }

            ImGui.Spacing();

            // Material with drag and drop
            var newMaterial = EditorWidgets.AssetField(
                "Material",
                ps.MaterialGuid == Guid.Empty ? (Guid?)null : ps.MaterialGuid,
                "Material",
                "Override material for advanced effects",
                showPreview: false);

            if (newMaterial != ps.MaterialGuid)
            {
                ps.MaterialGuid = newMaterial ?? Guid.Empty;
            }

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

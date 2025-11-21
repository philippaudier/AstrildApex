using System;
using ImGuiNET;
using OpenTK.Mathematics;
using Engine.Rendering;
using Editor.State;
using Numerics = System.Numerics;

namespace Editor.Panels
{
    /// <summary>
    /// Rendering Settings panel for configuring the rendering pipeline and post-processing effects.
    /// Provides intuitive controls for lighting and other rendering features.
    /// </summary>
    public static class RenderingSettingsPanel
    {
        private static bool _showDebugOptions = false;

        public static void Draw()
        {
            ImGui.Begin("Rendering Settings", ImGuiWindowFlags.None);
            // Apply default wrapping for long labels and descriptions inside this window
            Editor.UI.UIHelpers.BeginWindowDefaults();

            var renderer = EditorUI.MainViewport.Renderer;
            if (renderer == null)
            {
                ImGui.TextDisabled("No renderer available");
                ImGui.End();
                return;
            }

            DrawHeader();
            ImGui.Separator();

            // === SECTION 1: QUALITY & ANTI-ALIASING (Most Important) ===
            DrawAntiAliasingSettings(renderer);

            ImGui.Separator();

            // === SECTION 2: LIGHTING & SHADOWS ===
            DrawShadowsSettings(renderer);

            ImGui.Separator();

            // === SECTION 3: CAMERA ===
            // Note: SSAO is now configured via GlobalEffects component, not here
            // Old DrawSSAOSettings() call removed - SSAO is now a post-processing effect
            DrawCameraSettings(renderer);

            ImGui.Separator();

            // === SECTION 5: DEBUG & VISUALIZATION (Last) ===
            DrawDebugOptions(renderer);

            // Pop the wrapping defaults pushed at the start
            Editor.UI.UIHelpers.EndWindowDefaults();
            ImGui.End();
        }

        private static void DrawCameraSettings(Editor.Rendering.ViewportRenderer renderer)
        {
            if (!ImGui.CollapsingHeader("Camera", ImGuiTreeNodeFlags.DefaultOpen))
            {
                return;
            }

            ImGui.Text("Editor viewport camera settings");
            ImGui.Separator();

            // --- Projection Mode ---
            ImGui.Text("Projection");
            ImGui.SameLine();
            ImGui.TextDisabled("(controls projection type and basic defaults)");
            int currentMode = renderer.ProjectionMode; // 0 = Perspective, 1 = Ortho, 2 = 2D
            string[] modes = { "Perspective", "Orthographic", "2D" };
            ImGui.SetNextItemWidth(220);
            if (ImGui.Combo("Projection Mode", ref currentMode, modes, modes.Length))
            {
                // Apply sensible clip defaults and projection
                switch (currentMode)
                {
                    case 0: // Perspective
                        renderer.NearClip = 0.1f;
                        renderer.FarClip = 5000f;
                        renderer.SetProjectionMode(0, renderer.OrthoSize);
                        break;
                    case 1: // Orthographic
                        renderer.NearClip = 0.1f;
                        renderer.FarClip = Math.Max(5000f, renderer.OrthoSize * 200f);
                        renderer.SetProjectionMode(1, renderer.OrthoSize);
                        try
                        {
                            var st = renderer.GetOrbitCameraState();
                            st.Yaw = MathHelper.DegreesToRadians(45f);
                            st.Pitch = MathHelper.DegreesToRadians(-30f);
                            renderer.ApplyOrbitCameraState(st, true);
                        }
                        catch { }
                        break;
                    case 2: // 2D
                        renderer.NearClip = 0.1f;
                        renderer.FarClip = Math.Max(5000f, renderer.OrthoSize * 200f);
                        renderer.SetProjectionMode(2, renderer.OrthoSize);
                        try
                        {
                            var st2 = renderer.GetOrbitCameraState();
                            st2.Yaw = 0f;
                            st2.Pitch = MathHelper.DegreesToRadians(-90f);
                            renderer.ApplyOrbitCameraState(st2, true);
                        }
                        catch { }
                        break;
                }
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Switch between perspective, orthographic and 2D views. Orthographic and 2D adjust projection and camera orientation.");

            ImGui.Spacing();

            // Quick actions row
            ImGui.Text("Quick Actions:");
            ImGui.SameLine();
            if (ImGui.Button("Reset Camera"))
            {
                try
                {
                    var resetState = new Editor.Rendering.OrbitCameraState
                    {
                        Yaw = MathHelper.DegreesToRadians(-30f),
                        Pitch = MathHelper.DegreesToRadians(-15f),
                        Distance = 3.0f,
                        Target = new OpenTK.Mathematics.Vector3(0f, 0f, 0f)
                    };
                    renderer.ApplyOrbitCameraState(resetState, true);
                    Editor.State.EditorSettings.ViewportCameraState = resetState;
                }
                catch { }
            }
            ImGui.SameLine();
            if (ImGui.Button("Frame Selection")) { try { renderer.FrameSelection(true); } catch { } }
            ImGui.SameLine();
            if (ImGui.Button("Focus Scene Center"))
            {
                try
                {
                    var focusState = new Editor.Rendering.OrbitCameraState
                    {
                        Yaw = MathHelper.DegreesToRadians(-30f),
                        Pitch = MathHelper.DegreesToRadians(-15f),
                        Distance = 3.0f,
                        Target = new OpenTK.Mathematics.Vector3(0f, 0f, 0f)
                    };
                    renderer.ApplyOrbitCameraState(focusState, true);
                    Editor.State.EditorSettings.ViewportCameraState = focusState;
                }
                catch { }
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Reset view or frame selection quickly.");

            ImGui.Separator();

            // --- Orthographic settings ---
            if (renderer.ProjectionMode == 1 || renderer.ProjectionMode == 2)
            {
                ImGui.Text("Orthographic");
                ImGui.SameLine();
                ImGui.TextDisabled("(size & far clip)");

                float ortho = renderer.OrthoSize;
                ImGui.SetNextItemWidth(260);
                if (ImGui.SliderFloat("Ortho Size", ref ortho, 0.1f, 500f, "%.1f"))
                {
                    renderer.SetProjectionMode(renderer.ProjectionMode, ortho);
                    renderer.FarClip = Math.Max(5000f, ortho * 200f);
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Controls the vertical size of the orthographic view. Larger = zoomed out.");

                ImGui.Spacing();
                if (ImGui.Button("Auto-Adjust Far Clip")) { try { renderer.FarClip = Math.Max(5000f, ortho * 200f); } catch { } }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Set far clip based on ortho size to avoid clipping in large scenes.");

                ImGui.Separator();
            }

            // --- Clipping planes ---
            ImGui.Text("Clipping Planes");
            ImGui.SameLine();
            ImGui.TextDisabled("(near & far)");
            float near = renderer.NearClip;
            float far = renderer.FarClip;
            ImGui.SetNextItemWidth(200);
            if (ImGui.DragFloat("Near Clip", ref near, 0.01f, 0.0001f, 10f, "%.4f")) { renderer.NearClip = Math.Max(0.0001f, near); }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Near clipping plane. Avoid extremely small values to preserve depth precision.");
            ImGui.SetNextItemWidth(200);
            if (ImGui.DragFloat("Far Clip", ref far, 1.0f, 10f, 100000f, "%.1f")) { renderer.FarClip = Math.Max(renderer.NearClip + 0.001f, far); }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Far clipping plane. Increase for large scenes but watch precision.");

            ImGui.TextDisabled($"Current: Mode={(renderer.ProjectionMode == 0 ? "Persp" : renderer.ProjectionMode == 1 ? "Ortho" : "2D")}, Near={renderer.NearClip:F4}, Far={renderer.FarClip:F1}");

            ImGui.Separator();

            // --- Perspective settings ---
            if (renderer.ProjectionMode == 0)
            {
                ImGui.Text("Perspective");
                ImGui.SameLine();
                ImGui.TextDisabled("(field of view)");
                float fov = renderer.FovDegrees;
                ImGui.SetNextItemWidth(260);
                if (ImGui.SliderFloat("Field of View", ref fov, 10f, 120f, "%.1f°")) { renderer.FovDegrees = fov; }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Vertical field of view in degrees. Larger = wider perspective.");
                ImGui.Separator();
            }

            // --- VSync ---
            bool vsync = Editor.State.EditorSettings.VSync;
            ImGui.SetNextItemWidth(120);
            if (ImGui.Checkbox("VSync", ref vsync))
            {
                Editor.State.EditorSettings.VSync = vsync;
                try { var gw = Editor.Program.GameWindow; if (gw != null) gw.VSync = vsync ? OpenTK.Windowing.Common.VSyncMode.On : OpenTK.Windowing.Common.VSyncMode.Off; } catch { }
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Enable vertical sync to limit frame rate to display refresh (reduces tearing).");

            // Info
            ImGui.Spacing();
            if (ImGui.CollapsingHeader("Info"))
            {
                ImGui.Text($"FOV: {renderer.FovDegrees:F1}°");
                if (renderer.ProjectionMode == 1 || renderer.ProjectionMode == 2) ImGui.Text($"Ortho Size: {renderer.OrthoSize:F1}");
            }
        }

        private static void DrawHeader()
        {
            // Title
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Numerics.Vector2(8, 4));
            ImGui.Text("🎨 Rendering Pipeline");
            ImGui.PopStyleVar();

            ImGui.TextDisabled("Configure post-processing effects and rendering quality");
            ImGui.Spacing();
        }

        // NOTE: SSAO settings UI has been removed. SSAO is now configured via GlobalEffects component.
        // Add a GlobalEffects component to an entity, then add an SSAO effect from the Inspector.
        /*
        private static void DrawSSAOSettings(Editor.Rendering.ViewportRenderer renderer)
        {
            // SSAO Section Header
            if (ImGui.CollapsingHeader("Screen Space Ambient Occlusion (SSAO)", _showSSAOSettings ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None))
            {
                _showSSAOSettings = true;

                var ssaoSettings = renderer.SSAOSettings;
                bool changed = false;

                // Quick Enable/Disable toggle with prominent styling
                ImGui.PushStyleColor(ImGuiCol.Button, ssaoSettings.Enabled ? new Numerics.Vector4(0.2f, 0.7f, 0.2f, 1.0f) : new Numerics.Vector4(0.7f, 0.2f, 0.2f, 1.0f));
                if (ImGui.Button(ssaoSettings.Enabled ? "SSAO: ON" : "SSAO: OFF", new Numerics.Vector2(120, 0)))
                {
                    ssaoSettings.Enabled = !ssaoSettings.Enabled;
                    changed = true;
                }
                ImGui.PopStyleColor();

                ImGui.SameLine();

                // Quality Presets
                ImGui.SetNextItemWidth(150);
                if (ImGui.Combo("Quality Preset", ref _selectedPreset, QualityNames, QualityNames.Length))
                {
                    // If not Custom (index 0), apply the preset
                    if (_selectedPreset > 0)
                    {
                        ssaoSettings = QualityPresets[_selectedPreset - 1]; // -1 because Custom is at index 0
                        changed = true;
                    }
                }

                if (ssaoSettings.Enabled)
                {
                    ImGui.Spacing();

                    // Main SSAO Parameters in two columns
                    if (ImGui.BeginTable("SSAOParams", 2, ImGuiTableFlags.None))
                    {
                        ImGui.TableSetupColumn("Left", ImGuiTableColumnFlags.WidthFixed, 200);
                        ImGui.TableSetupColumn("Right", ImGuiTableColumnFlags.WidthStretch);

                        // Left Column
                        ImGui.TableNextRow();
                        ImGui.TableSetColumnIndex(0);

                        // Radius
                        ImGui.Text("Sampling Radius");
                        ImGui.SetNextItemWidth(-1);
                        if (ImGui.SliderFloat("##Radius", ref ssaoSettings.Radius, 0.1f, 2.0f, "%.2f"))
                        {
                            changed = true;
                            _selectedPreset = 0; // Switch to Custom when manually changed
                        }
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip("View-space radius for occlusion sampling\nSmaller (0.1-0.5) = fine details, Larger (1-2) = broad occlusion");

                        ImGui.Spacing();

                        // Intensity
                        ImGui.Text("Occlusion Intensity");
                        ImGui.SetNextItemWidth(-1);
                        if (ImGui.SliderFloat("##Intensity", ref ssaoSettings.Intensity, 0.0f, 3.0f, "%.2f"))
                        {
                            changed = true;
                            _selectedPreset = 0;
                        }
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip("Strength of the occlusion effect\nHigher = darker shadows");

                        ImGui.Spacing();

                        // Sample Count (power of 2: 4, 8, 16, 32, 64, 128)
                        ImGui.Text("Sample Count");
                        ImGui.SetNextItemWidth(-1);
                        int samples = ssaoSettings.SampleCount;
                        // Map power-of-2 to slider index: 4=0, 8=1, 16=2, 32=3, 64=4, 128=5
                        int sliderIndex = samples switch
                        {
                            4 => 0,
                            8 => 1,
                            16 => 2,
                            32 => 3,
                            64 => 4,
                            128 => 5,
                            _ => 4 // Default to 64
                        };
                        if (ImGui.SliderInt("##Samples", ref sliderIndex, 0, 5))
                        {
                            // Map slider index back to power-of-2
                            ssaoSettings.SampleCount = sliderIndex switch
                            {
                                0 => 4,
                                1 => 8,
                                2 => 16,
                                3 => 32,
                                4 => 64,
                                5 => 128,
                                _ => 64
                            };
                            changed = true;
                            _selectedPreset = 0;
                        }
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip($"Number of samples per pixel: {ssaoSettings.SampleCount}\nPowers of 2 only: 4, 8, 16, 32, 64, 128\nMore = better quality but slower");

                        // Right Column
                        ImGui.TableSetColumnIndex(1);

                        // Bias
                        ImGui.Text("Self-Occlusion Bias");
                        ImGui.SetNextItemWidth(-1);
                        if (ImGui.SliderFloat("##Bias", ref ssaoSettings.Bias, 0.01f, 0.2f, "%.3f"))
                        {
                            changed = true;
                            _selectedPreset = 0;
                        }
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip("Prevents self-shadowing artifacts (world-space)\nIncrease if you see acne patterns, decrease if shadows are missing");

                        ImGui.Spacing();

                        // Blur Settings
                        ImGui.Text("Blur Radius");
                        ImGui.SetNextItemWidth(-1);
                        int blurSize = ssaoSettings.BlurSize;
                        if (ImGui.SliderInt("##BlurSize", ref blurSize, 0, 4))
                        {
                            ssaoSettings.BlurSize = blurSize;
                            changed = true;
                            _selectedPreset = 0;
                        }
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip("Blur kernel radius in pixels\n0 = no blur, 2 = 5x5 kernel (recommended)");

                        ImGui.EndTable();
                    }
                }

                if (changed)
                {
                    renderer.SSAOSettings = ssaoSettings;
                    // Save to persistent settings if needed
                    SaveSSAOSettings(ssaoSettings);
                }
            }
            else
            {
                _showSSAOSettings = false;
            }
        }
        */

        private static void DrawDebugOptions(Editor.Rendering.ViewportRenderer renderer)
        {
            if (ImGui.CollapsingHeader("Debug & Visualization", _showDebugOptions ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None))
            {
                _showDebugOptions = true;

                ImGui.Text("SSAO Debug Views:");
                // Simple G-buffer debug selector
                ImGui.Text("G-Buffer Debug Mode:");
                var rendererInst = EditorUI.MainViewport.Renderer;
                if (rendererInst != null)
                {
                    // Options: Off, Position, Normal, Depth
                    int mode = rendererInst.GBufferDebugMode;
                    string[] names = { "Off", "Position", "Normal", "Depth" };
                    if (ImGui.Combo("Debug View", ref mode, names, names.Length))
                    {
                        rendererInst.GBufferDebugMode = mode;
                    }
                }
                else
                {
                    ImGui.TextDisabled("Renderer not available");
                }

                // Performance info
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Text("Performance Info:");

                // NOTE: SSAO performance info removed - SSAO is now a post-processing effect
                /*
                var ssaoSettings = renderer.SSAOSettings;
                if (ssaoSettings.Enabled)
                {
                    float estimatedCost = EstimateSSAOCost(ssaoSettings);
                    ImGui.Text($"Estimated GPU Cost: {estimatedCost:F1}ms");

                    // Color-code performance impact
                    var color = estimatedCost < 2.0f ? new Numerics.Vector4(0, 1, 0, 1) :  // Green
                               estimatedCost < 5.0f ? new Numerics.Vector4(1, 1, 0, 1) :  // Yellow
                                                      new Numerics.Vector4(1, 0, 0, 1);   // Red

                    ImGui.PushStyleColor(ImGuiCol.Text, color);
                    string impact = estimatedCost < 2.0f ? "Low" : estimatedCost < 5.0f ? "Medium" : "High";
                    ImGui.Text($"Performance Impact: {impact}");
                    ImGui.PopStyleColor();
                }
                else
                {
                    ImGui.TextDisabled("SSAO Disabled - No performance cost");
                }
                */

                // Not implemented popup
                if (ImGui.BeginPopup("NotImplemented"))
                {
                    ImGui.Text("🚧 Feature not implemented yet");
                    ImGui.Text("Coming in future updates!");
                    if (ImGui.Button("OK"))
                        ImGui.CloseCurrentPopup();
                    ImGui.EndPopup();
                }
            }
            else
            {
                _showDebugOptions = false;
            }
        }

        private static void DrawShadowsSettings(Editor.Rendering.ViewportRenderer renderer)
        {
            if (ImGui.CollapsingHeader("Shadows", ImGuiTreeNodeFlags.DefaultOpen))
            {
                var s = Editor.State.EditorSettings.ShadowsSettings;

                // === Enable/Disable ===
                bool enabled = s.Enabled;
                ImGui.PushStyleColor(ImGuiCol.Button, enabled ? new Numerics.Vector4(0.2f, 0.7f, 0.2f, 1.0f) : new Numerics.Vector4(0.7f, 0.2f, 0.2f, 1.0f));
                if (ImGui.Button(enabled ? "Shadows: ON" : "Shadows: OFF", new Numerics.Vector2(120, 0)))
                {
                    s.Enabled = !s.Enabled;
                    Editor.State.EditorSettings.ShadowsSettings = s;
                }
                ImGui.PopStyleColor();

                if (!enabled)
                {
                    ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.6f);
                }

                ImGui.Spacing();

                // === Shadow Strength ===
                ImGui.Text("Shadow Intensity");
                ImGui.SetNextItemWidth(-1);
                float shadowStrength = s.ShadowStrength;
                if (ImGui.SliderFloat("##ShadowStrength", ref shadowStrength, 0.0f, 1.0f, "%.2f"))
                {
                    s.ShadowStrength = shadowStrength;
                    Editor.State.EditorSettings.ShadowsSettings = s;
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("How dark shadows appear\n0.0 = no shadows, 1.0 = full black shadows");

                ImGui.Spacing();
                ImGui.Separator();

                // === Shadow Map Resolution ===
                ImGui.Text("Shadow Quality (Resolution)");
                ImGui.SetNextItemWidth(-1);

                int shadowMapSize = s.ShadowMapSize;
                if (ImGui.SliderInt("##ShadowMapSize", ref shadowMapSize, 1024, 8192))
                {
                    shadowMapSize = (int)Math.Pow(2, Math.Round(Math.Log2(shadowMapSize))); // Force power of 2
                    s.ShadowMapSize = shadowMapSize;
                    Editor.State.EditorSettings.ShadowsSettings = s;
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Shadow map texture resolution\n1024=Fast, 2048=Balanced, 4096=High Quality, 8192=Ultra");

                ImGui.Spacing();
                ImGui.Separator();

                // === Bias Settings ===
                ImGui.Text("Bias (prevents shadow artifacts)");

                float shadowBias = s.ShadowBias;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.SliderFloat("##ShadowBias", ref shadowBias, 0.0f, 0.5f, "%.3f"))
                {
                    s.ShadowBias = shadowBias;
                    Editor.State.EditorSettings.ShadowsSettings = s;
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Prevents shadow acne (striped shadows)\nIncrease if you see striped patterns\nDecrease if shadows detach from objects");

                ImGui.Spacing();
                ImGui.Separator();

                // === Scene Coverage ===
                ImGui.Text("Shadow Distance (Coverage)");
                ImGui.SetNextItemWidth(-1);

                float shadowDistance = s.ShadowDistance;
                if (ImGui.SliderFloat("##ShadowDistance", ref shadowDistance, 50f, 5000f, "%.0f units"))
                {
                    s.ShadowDistance = shadowDistance;
                    Editor.State.EditorSettings.ShadowsSettings = s;
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("How far shadows are rendered from the light\nLarger = more area covered but lower quality\nSmaller = better quality but less coverage");

                ImGui.Spacing();
                ImGui.Separator();

                // === Debug ===
                ImGui.Text("Debug Visualization");

                bool dbg = s.DebugShowShadowMap;
                if (ImGui.Checkbox("Show Shadow Map", ref dbg))
                {
                    s.DebugShowShadowMap = dbg;
                    Editor.State.EditorSettings.ShadowsSettings = s;
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Display the shadow depth map for debugging");

                if (!enabled)
                {
                    ImGui.PopStyleVar();
                }
            }
        }

        private static void DrawAntiAliasingSettings(Editor.Rendering.ViewportRenderer renderer)
        {
            if (!ImGui.CollapsingHeader("Anti-Aliasing", ImGuiTreeNodeFlags.DefaultOpen))
            {
                return;
            }

            ImGui.Text("Anti-aliasing method");
            ImGui.Separator();

            // === AA Mode Selector ===
            var currentMode = renderer.AntiAliasingMode;
            int selectedIndex = currentMode switch
            {
                Engine.Rendering.AntiAliasingMode.None => 0,
                Engine.Rendering.AntiAliasingMode.MSAA2x => 1,
                Engine.Rendering.AntiAliasingMode.MSAA4x => 2,
                Engine.Rendering.AntiAliasingMode.MSAA8x => 3,
                Engine.Rendering.AntiAliasingMode.MSAA16x => 4,
                Engine.Rendering.AntiAliasingMode.TAA => 5,
                _ => 0
            };

            string[] modeNames = { "None", "MSAA 2×", "MSAA 4×", "MSAA 8×", "MSAA 16×", "TAA (Temporal)" };

            ImGui.SetNextItemWidth(-1);
            if (ImGui.Combo("##AAMode", ref selectedIndex, modeNames, modeNames.Length))
            {
                var newMode = selectedIndex switch
                {
                    0 => Engine.Rendering.AntiAliasingMode.None,
                    1 => Engine.Rendering.AntiAliasingMode.MSAA2x,
                    2 => Engine.Rendering.AntiAliasingMode.MSAA4x,
                    3 => Engine.Rendering.AntiAliasingMode.MSAA8x,
                    4 => Engine.Rendering.AntiAliasingMode.MSAA16x,
                    5 => Engine.Rendering.AntiAliasingMode.TAA,
                    _ => Engine.Rendering.AntiAliasingMode.None
                };
                renderer.AntiAliasingMode = newMode;
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(
                    "Anti-Aliasing smooths jagged edges\n\n" +
                    "MSAA: Hardware multisampling (fast, good for geometry)\n" +
                    "  • 2×/4×: Good performance\n" +
                    "  • 8×/16×: High quality, slower\n\n" +
                    "TAA: Temporal smoothing (best quality, experimental)"
                );
            }

            ImGui.Spacing();

            // Show performance info (Note: %% escapes % in ImGui)
            string perfInfo = currentMode switch
            {
                Engine.Rendering.AntiAliasingMode.None => "Best Performance",
                Engine.Rendering.AntiAliasingMode.MSAA2x => "Minimal performance impact (~5-10%% cost)",
                Engine.Rendering.AntiAliasingMode.MSAA4x => "Balanced (~10-20%% performance cost)",
                Engine.Rendering.AntiAliasingMode.MSAA8x => "High quality (~20-30%% performance cost)",
                Engine.Rendering.AntiAliasingMode.MSAA16x => "Ultra quality (~30-40%% performance cost)",
                Engine.Rendering.AntiAliasingMode.TAA => "Cinematic (experimental, may have artifacts)",
                _ => ""
            };

            if (!string.IsNullOrEmpty(perfInfo))
            {
                ImGui.TextColored(new Numerics.Vector4(0.7f, 0.7f, 0.7f, 1f), perfInfo);
            }

            ImGui.Spacing();

            // === TAA Settings (only show if TAA is selected) ===
            if (currentMode == Engine.Rendering.AntiAliasingMode.TAA)
            {
                ImGui.Separator();
                ImGui.Text("TAA Settings");

                var taaSettings = renderer.TAASettings;
                bool changed = false;

                // Jitter Pattern
                ImGui.Text("Jitter Pattern");
                ImGui.SetNextItemWidth(200);
                int jitterPattern = taaSettings.JitterPattern;
                string[] jitterNames = { "Halton (Best Quality)", "R2 Sequence", "None (Disable TAA)" };
                if (ImGui.Combo("##JitterPattern", ref jitterPattern, jitterNames, jitterNames.Length))
                {
                    taaSettings.JitterPattern = jitterPattern;
                    changed = true;
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Halton sequence provides best temporal distribution");

                ImGui.Spacing();

                // Temporal Feedback
                ImGui.Text("Temporal Feedback");
                float feedbackMin = taaSettings.FeedbackMin;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.SliderFloat("##FeedbackMin", ref feedbackMin, 0.0f, 0.95f, "Min: %.2f"))
                {
                    taaSettings.FeedbackMin = feedbackMin;
                    changed = true;
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Minimum history weight (lower = less ghosting)\nRecommended: 0.7-0.85");

                float feedbackMax = taaSettings.FeedbackMax;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.SliderFloat("##FeedbackMax", ref feedbackMax, 0.0f, 0.99f, "Max: %.2f"))
                {
                    taaSettings.FeedbackMax = feedbackMax;
                    changed = true;
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Maximum history weight (higher = smoother)\nRecommended: 0.90-0.97");

                ImGui.Spacing();

                // YCoCg toggle
                bool useYCoCg = taaSettings.UseYCoCg;
                ImGui.SetNextItemWidth(200);
                if (ImGui.Checkbox("Use YCoCg Color Space", ref useYCoCg))
                {
                    taaSettings.UseYCoCg = useYCoCg;
                    changed = true;
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Use YCoCg color space for better temporal stability (may change color slightly)");

                ImGui.Spacing();

                // Jitter scale
                ImGui.Text("Jitter Scale");
                float jitterScale = taaSettings.JitterScale;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.SliderFloat("##JitterScale", ref jitterScale, 0.0f, 1.5f, "Scale: %.2f"))
                {
                    taaSettings.JitterScale = jitterScale;
                    changed = true;
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Scale applied to sub-pixel jitter amplitude (lower = less visible flicker, at cost of AA coverage)");

                if (changed)
                {
                    renderer.TAASettings = taaSettings;
                }
            }
        }

        // NOTE: All SSAO utility methods removed - SSAO is now configured via GlobalEffects component
        /*
        private static float EstimateSSAOCost(SSAORenderer.SSAOSettings settings)
        {
            // Simple heuristic for performance estimation
            float baseCost = 1.0f;
            float sampleCost = settings.SampleCount * 0.05f;
            float blurCost = settings.BlurSize * 0.5f; // BlurSize is now pixels, not radius
            return baseCost + sampleCost + blurCost;
        }

        private static void SaveSSAOSettings(SSAORenderer.SSAOSettings settings)
        {
            EditorSettings.SSAOSettings = settings;
        }

        /// <summary>
        /// Load SSAO settings from persistent storage
        /// </summary>
        public static SSAORenderer.SSAOSettings LoadSSAOSettings()
        {
            return EditorSettings.SSAOSettings;
        }

        /// <summary>
        /// Reset SSAO settings to default values
        /// </summary>
        public static void ResetSSAOToDefault()
        {
            var renderer = EditorUI.MainViewport.Renderer;
            if (renderer != null)
            {
                renderer.SSAOSettings = SSAORenderer.SSAOSettings.Default;
                _selectedPreset = 0; // Custom
            }
        }
        */
    }
}
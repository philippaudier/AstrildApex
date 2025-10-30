using System;
using System.Linq;
using ImGuiNET;
using OpenTK.Mathematics;
using Engine.Components;
using Engine.Scene;
using Editor.State;
using Editor.Icons;
using Numerics = System.Numerics;

namespace Editor.Panels
{
    /// <summary>
    /// Unity-like Environment panel for lighting and skybox settings
    /// </summary>
    public static class EnvironmentPanel
    {
        private static bool _showAdvanced = false;
        
        public static void Draw()
        {
            ImGui.Begin("Environment", ImGuiWindowFlags.None);
            
            var scene = EditorUI.MainViewport.Renderer?.Scene;
            if (scene == null)
            {
                ImGui.TextDisabled("No scene available");
                ImGui.End();
                return;
            }

            // Find or create environment settings
            var envSettings = FindOrCreateEnvironmentSettings(scene);
            if (envSettings == null)
            {
                ImGui.TextDisabled("Could not access environment settings");
                ImGui.End();
                return;
            }

            DrawEnvironmentSettings(scene, envSettings);
            
            ImGui.End();
        }

        private static EnvironmentSettings? FindOrCreateEnvironmentSettings(Scene scene)
        {
            // Look for existing environment settings in scene
            var envEntity = scene.Entities.FirstOrDefault(e => e.HasComponent<EnvironmentSettings>());
            
            if (envEntity == null)
            {
                // Create a new Environment entity
                envEntity = new Entity
                {
                    Id = scene.GetNextEntityId(),
                    Name = "Environment",
                    Guid = Guid.NewGuid(),
                    Active = true
                };
                
                // Add environment settings component
                envEntity.AddComponent<EnvironmentSettings>();
                scene.Entities.Add(envEntity);
            }

            return envEntity.GetComponent<EnvironmentSettings>();
        }

        private static void DrawEnvironmentSettings(Scene scene, EnvironmentSettings env)
        {
            ImGui.PushItemWidth(150f);

            // === SKYBOX SECTION ===
            if (ImGui.CollapsingHeader("Skybox", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Indent();
                
                // Skybox material
                DrawSkyboxMaterial(env);
                
                // Skybox tint
                var tint = new Numerics.Vector3(env.SkyboxTint.X, env.SkyboxTint.Y, env.SkyboxTint.Z);
                if (ImGui.ColorEdit3("Tint", ref tint))
                {
                    env.SkyboxTint = new Vector3(tint.X, tint.Y, tint.Z);
                    Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                }
                
                // Skybox exposure
                var exposure = env.SkyboxExposure;
                if (ImGui.DragFloat("Exposure", ref exposure, 0.01f, 0.0f, 8.0f))
                {
                    env.SkyboxExposure = exposure;
                    Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                }
                
                ImGui.Unindent();
                ImGui.Spacing();
            }

            // === ENVIRONMENT LIGHTING SECTION ===
            if (ImGui.CollapsingHeader("Environment Lighting", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Indent();
                
                var newSun = DrawLightAssignment(scene, "Sun Light", env.SunLight, LightType.Directional);
                if (newSun != env.SunLight)
                {
                    env.SunLight = newSun;
                    Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                }

                var newMoon = DrawLightAssignment(scene, "Moon Light", env.MoonLight, LightType.Directional);
                if (newMoon != env.MoonLight)
                {
                    env.MoonLight = newMoon;
                    Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                }
                
                // Time of day
                var timeOfDay = env.TimeOfDay;
                if (ImGui.SliderFloat("Time of Day", ref timeOfDay, 0.0f, 24.0f, "%.1f"))
                {
                    env.TimeOfDay = timeOfDay;
                    Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                }
                ImGui.SameLine();
                ImGui.TextDisabled("(6-18 = Day, 18-6 = Night)");
                
                // Show current active light
                var activeId = env.GetActiveLightId();
                var activeName = "None";
                if (activeId.HasValue)
                {
                    var activeLight = scene.GetById(activeId.Value);
                    if (activeLight != null)
                        activeName = activeLight.Name;
                }
                ImGui.Text($"Active Light: {activeName}");
                
                ImGui.Unindent();
                ImGui.Spacing();
            }

            // === AMBIENT LIGHTING SECTION ===
            if (ImGui.CollapsingHeader("Ambient Lighting"))
            {
                ImGui.Indent();
                
                // Ambient mode
                var ambientModeNames = new[] { "Skybox", "Trilight", "Color" };
                var currentMode = (int)env.AmbientMode;
                if (ImGui.Combo("Source", ref currentMode, ambientModeNames, ambientModeNames.Length))
                {
                    env.AmbientMode = (AmbientMode)currentMode;
                    Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                }
                
                // Ambient intensity
                var ambientIntensity = env.AmbientIntensity;
                if (ImGui.DragFloat("Intensity", ref ambientIntensity, 0.01f, 0.0f, 8.0f))
                {
                    env.AmbientIntensity = ambientIntensity;
                    Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                }
                
                // Ambient color (when mode is Color)
                if (env.AmbientMode == AmbientMode.Color)
                {
                    var ambientColor = new Numerics.Vector3(env.AmbientColor.X, env.AmbientColor.Y, env.AmbientColor.Z);
                    if (ImGui.ColorEdit3("Ambient Color", ref ambientColor))
                    {
                        env.AmbientColor = new Vector3(ambientColor.X, ambientColor.Y, ambientColor.Z);
                        Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                    }
                }
                
                ImGui.Unindent();
                ImGui.Spacing();
            }

            // === FOG SECTION ===
            if (ImGui.CollapsingHeader("Fog"))
            {
                ImGui.Indent();
                
                var fogEnabled = env.FogEnabled;
                if (ImGui.Checkbox("Enabled", ref fogEnabled))
                {
                    env.FogEnabled = fogEnabled;
                    Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                }
                
                if (env.FogEnabled)
                {
                    // Fog color
                    var fogColor = new Numerics.Vector3(env.FogColor.X, env.FogColor.Y, env.FogColor.Z);
                    if (ImGui.ColorEdit3("Color", ref fogColor))
                    {
                        env.FogColor = new Vector3(fogColor.X, fogColor.Y, fogColor.Z);
                        Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                    }
                    
                    // Fog mode (for future: Linear, Exponential, ExponentialSquared)
                    ImGui.Text("Mode: Linear"); // TODO: Add fog modes
                    
                    // Fog start/end
                    var fogStart = env.FogStart;
                    if (ImGui.DragFloat("Start", ref fogStart, 0.1f, 0.0f, env.FogEnd - 0.1f))
                    {
                        env.FogStart = fogStart;
                        Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                    }
                    
                    var fogEnd = env.FogEnd;
                    if (ImGui.DragFloat("End", ref fogEnd, 0.1f, env.FogStart + 0.1f, 1000.0f))
                    {
                        env.FogEnd = fogEnd;
                        Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                    }

                    // Fog density (exponential fog control)
                    var fogDensity = env.FogDensity;
                    // Allow larger densities (0..5) with finer step 0.01
                    if (ImGui.DragFloat("Density", ref fogDensity, 0.01f, 0.0f, 5.0f))
                    {
                        // Clamp to reasonable values
                        fogDensity = Math.Max(0.0f, Math.Min(5.0f, fogDensity));
                        env.FogDensity = fogDensity;
                        Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                    }
                }
                
                ImGui.Unindent();
                ImGui.Spacing();
            }

            // === ADVANCED SETTINGS ===
            _showAdvanced = ImGui.CollapsingHeader("Advanced Settings");
            if (_showAdvanced)
            {
                ImGui.Indent();
                ImGui.Text($"Sun/Moon Blend: {env.GetSunMoonBlend():F2}");
                ImGui.TextDisabled("(0 = Moon, 1 = Sun)");
                ImGui.Unindent();
            }

            ImGui.PopItemWidth();
        }

        private static void DrawSkyboxMaterial(EnvironmentSettings env)
        {
            ImGui.Text("Material");
            ImGui.SameLine();
            
            string materialName = "None (Skybox Material)";
            Guid? skyboxMaterialGuid = null;
            
            if (!string.IsNullOrEmpty(env.SkyboxMaterialPath))
            {
                if (Guid.TryParse(env.SkyboxMaterialPath, out var guid))
                {
                    skyboxMaterialGuid = guid;
                    materialName = Engine.Assets.AssetDatabase.GetName(guid) ?? "Unknown Skybox Material";
                }
                else
                {
                    materialName = System.IO.Path.GetFileNameWithoutExtension(env.SkyboxMaterialPath);
                }
            }
            
            ImGui.SetNextItemWidth(-1);
            
            // Create a button that looks like a material field
            var buttonColor = skyboxMaterialGuid.HasValue
                ? new Numerics.Vector4(0.3f, 0.6f, 1.0f, 1.0f)  // Blue for assigned material
                : new Numerics.Vector4(0.4f, 0.4f, 0.4f, 1.0f);  // Gray for none
                
            ImGui.PushStyleColor(ImGuiCol.Button, buttonColor);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, buttonColor * 1.2f);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, buttonColor * 0.8f);
            
            bool materialClicked = ImGui.Button($"{materialName}##SkyboxMaterialField", new Numerics.Vector2(-1, 20));
            
            ImGui.PopStyleColor(3);
            
            // Handle drag & drop for skybox materials
            if (ImGui.BeginDragDropTarget())
            {
                var payload = ImGui.AcceptDragDropPayload("ASSET_MULTI");
                unsafe
                {
                    if (payload.NativePtr != null && payload.Data != IntPtr.Zero && payload.DataSize >= 16)
                    {
                        try
                        {
                            var span = new ReadOnlySpan<byte>((void*)payload.Data, 16);
                            var droppedMaterialGuid = new Guid(span);
                            
                            // Check if it's a skybox material asset
                                    if (Engine.Assets.AssetDatabase.TryGet(droppedMaterialGuid, out var record) && 
                                        string.Equals(record.Type, "SkyboxMaterial", StringComparison.OrdinalIgnoreCase))
                                    {
                                        env.SkyboxMaterialPath = droppedMaterialGuid.ToString();
                                        Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                                    }
                        }
                        catch (Exception)
                        {
                            // Ignore drag & drop errors
                        }
                    }
                }
                ImGui.EndDragDropTarget();
            }
            
            // Right-click context menu
            if (ImGui.BeginPopupContextItem("SkyboxMaterialContextMenu"))
            {
                if (ImGui.MenuItem("Clear Material"))
                {
                    env.SkyboxMaterialPath = "";
                    Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                }
                
                if (ImGui.MenuItem("Create Procedural Skybox"))
                {
                    // Create a new procedural skybox material
                    var skyboxMat = new Engine.Assets.SkyboxMaterialAsset
                    {
                        Guid = Guid.NewGuid(),
                        Name = "Default Procedural Skybox",
                        Type = Engine.Assets.SkyboxType.Procedural,
                        SkyTint = new float[] { 0.5f, 0.5f, 0.5f, 1.0f },
                        GroundColor = new float[] { 0.369f, 0.349f, 0.341f, 1.0f },
                        Exposure = 1.3f
                    };
                    
                    var matPath = System.IO.Path.Combine(Engine.Assets.AssetDatabase.AssetsRoot, "Default Procedural Skybox.skymat");
                    Engine.Assets.SkyboxMaterialAsset.Save(matPath, skyboxMat);
                    
                    // Refresh assets and assign
                    Engine.Assets.AssetDatabase.Refresh();
                    env.SkyboxMaterialPath = skyboxMat.Guid.ToString();
                    Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                }
                ImGui.EndPopup();
            }
        }

        private static Engine.Scene.Entity? DrawLightAssignment(Scene scene, string label, Engine.Scene.Entity? lightEntity, LightType preferredType)
        {
            ImGui.Text(label);
            ImGui.SameLine();

            // Resolve current light name
            string lightName = "None (Light)";
            if (lightEntity != null)
            {
                if (scene.Entities.Contains(lightEntity))
                {
                    lightName = lightEntity.Name;
                }
                else
                {
                    // Entity doesn't belong to this scene anymore
                    lightEntity = null;
                }
            }

            ImGui.SetNextItemWidth(-1);

            // Button color depends on whether a light is assigned
            var buttonColor = lightEntity != null
                ? new Numerics.Vector4(0.3f, 0.6f, 1.0f, 1.0f)
                : new Numerics.Vector4(0.4f, 0.4f, 0.4f, 1.0f);

            ImGui.PushStyleColor(ImGuiCol.Button, buttonColor);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, buttonColor * 1.2f);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, buttonColor * 0.8f);

            bool lightClicked = ImGui.Button($"{lightName}##{label}Field", new Numerics.Vector2(-1, 20));

            ImGui.PopStyleColor(3);

            // Open selection popup when clicked
            if (lightClicked)
                ImGui.OpenPopup($"Select{label.Replace(" ", "")}");

            if (ImGui.BeginPopup($"Select{label.Replace(" ", "")}"))
            {
                ImGui.Text($"Select {label}:");
                ImGui.Separator();

                if (ImGui.Selectable("None"))
                {
                    lightEntity = null;
                    Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                    ImGui.CloseCurrentPopup();
                }

                // List all entities with Light components of the preferred type
                var lightEntities = scene.Entities
                    .Where(e => e.HasComponent<LightComponent>())
                    .Where(e => e.GetComponent<LightComponent>()?.Type == preferredType)
                    .OrderBy(e => e.Name);

                foreach (var entity in lightEntities)
                {
                    bool isSelected = lightEntity != null && lightEntity.Id == entity.Id;
                    if (ImGui.Selectable($"{entity.Name}##{entity.Id}", isSelected))
                    {
                        lightEntity = entity;
                        Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                        ImGui.CloseCurrentPopup();
                    }
                }

                if (!lightEntities.Any())
                    ImGui.TextDisabled($"No {preferredType} lights found");

                ImGui.EndPopup();
            }

            // Right-click context menu for create/clear
            if (ImGui.BeginPopupContextItem($"{label}ContextMenu"))
            {
                if (ImGui.MenuItem("Clear"))
                {
                    lightEntity = null;
                    Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                }

                if (ImGui.MenuItem($"Create {preferredType} Light"))
                {
                    // Create a new light entity with sensible defaults
                    var newLight = new Entity
                    {
                        Id = scene.GetNextEntityId(),
                        Name = label.Replace(" ", ""),
                        Guid = Guid.NewGuid(),
                        Active = true
                    };

                    var lightComp = newLight.AddComponent<LightComponent>();
                    lightComp.Type = preferredType;

                    if (label.Contains("Sun"))
                    {
                        lightComp.Color = new Vector3(1.0f, 0.95f, 0.8f);
                        lightComp.Intensity = 3.0f;
                        newLight.Transform.Rotation = Quaternion.FromEulerAngles(
                            MathHelper.DegreesToRadians(50f),
                            MathHelper.DegreesToRadians(-30f), 0f);
                    }
                    else if (label.Contains("Moon"))
                    {
                        lightComp.Color = new Vector3(0.7f, 0.8f, 1.0f);
                        lightComp.Intensity = 0.5f;
                        newLight.Transform.Rotation = Quaternion.FromEulerAngles(
                            MathHelper.DegreesToRadians(-50f),
                            MathHelper.DegreesToRadians(120f), 0f);
                    }

                    scene.Entities.Add(newLight);
                    lightEntity = newLight;
                    Editor.SceneManagement.SceneManager.MarkSceneAsModified();

                    // Select the new entity
                    Selection.SetSingle(newLight.Id);
                }

                ImGui.EndPopup();
            }

            return lightEntity;
        }
    }
}
// Editor/Inspector/ComponentInspector.cs (NOUVEAU)
using System;
using System.IO;
using System.Linq;
using ImGuiNET;
using Engine.Components;
using Engine.Scene;
using Engine.Assets;

namespace Editor.Inspector
{
    public static class ComponentInspector
    {
        public static void Draw(Engine.Scene.Entity entity, Component component)
        {
            // Header + icône (optionnel: ton IconManager)
            ImGui.PushID(component.GetHashCode());
            
            // Draw header with optional "Edit Script" button
            string componentTypeName = component.GetType().Name;
            bool open = ImGui.CollapsingHeader(componentTypeName, ImGuiTreeNodeFlags.DefaultOpen);
            
            // Add "Edit Script" button on the same line as the header
            string scriptPath = FindComponentScriptPath(component.GetType());
            if (!string.IsNullOrEmpty(scriptPath))
            {
                ImGui.SameLine();
                ImGui.SetCursorPosX(ImGui.GetWindowWidth() - 100);
                if (ImGui.SmallButton("Edit Script"))
                {
                    State.EditorSettings.OpenScript(scriptPath);
                }
            }
            
            if (open)
            {
                ImGui.Indent();
                switch (component)
                {
                    // Modern unified UI system
                    case Engine.Components.UI.UIElementComponent uiElem: Editor.Inspector.UIElementInspector.DrawInspector(entity, uiElem); break;
                    
                    // Legacy UI components (deprecated - will be removed)
                    case Engine.Components.UI.CanvasComponent canvas: Editor.Inspector.CanvasInspector.Draw(canvas); break;
                    case Engine.Components.UI.UIImageComponent image: Editor.Inspector.UIImageInspector.Draw(image); break;
                    case Engine.Components.UI.UITextComponent text: Editor.Inspector.UITextInspector.Draw(text); break;
                    case Engine.Components.UI.UIButtonComponent button: Editor.Inspector.UIButtonInspector.Draw(button); break;
                    case LightComponent light: LightInspector.Draw(light); break;
                    case CameraComponent camera: CameraInspector.Draw(camera); break;
                    case MeshRendererComponent meshRenderer: MeshRendererInspector.Draw(meshRenderer); break;
                    case BoxCollider box: BoxColliderInspector.Draw(box); break;
                    case HeightfieldCollider heightfield: HeightfieldColliderInspector.Draw(heightfield); break;
                    case Engine.Components.Terrain terrain: TerrainInspector.Draw(entity, terrain); break;
                    case Engine.Components.WaterComponent water: WaterComponentInspector.Draw(entity, water); break;
                    default:
                        ImGui.TextDisabled("No custom inspector for this component type.");
                        break;
                }
                ImGui.Unindent();
            }
            ImGui.PopID();
        }
        
        /// <summary>
        /// Find the source file path for a component type.
        /// Searches in Engine/Components and its subdirectories.
        /// </summary>
        private static string FindComponentScriptPath(Type componentType)
        {
            try
            {
                // Get the simple name of the type (e.g., "LightComponent")
                string typeName = componentType.Name;
                
                // Search in Engine/Components directory
                string engineDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Engine", "Components");
                engineDir = Path.GetFullPath(engineDir);
                
                if (!Directory.Exists(engineDir))
                    return string.Empty;
                
                // Search for the file recursively
                string[] files = Directory.GetFiles(engineDir, $"{typeName}.cs", SearchOption.AllDirectories);
                
                if (files.Length > 0)
                    return files[0];
                
                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}

using System;
using ImGuiNET;

namespace Editor.Inspector
{
    public sealed class PrefabAssetInspector
    {
        private Engine.Assets.PrefabAsset? _asset;
        private Guid _assetGuid;

        public void Draw(Guid assetGuid)
        {
            _assetGuid = assetGuid;
            
            try
            {
                _asset = Engine.Assets.AssetDatabase.LoadPrefab(assetGuid);
            }
            catch (Exception ex)
            {
                ImGui.TextColored(new System.Numerics.Vector4(1, 0, 0, 1), $"Failed to load prefab: {ex.Message}");
                return;
            }

            if (_asset == null)
            {
                ImGui.Text("No prefab data");
                return;
            }

            // Display prefab info
            ImGui.SeparatorText("Prefab Info");
            
            ImGui.Text($"Name: {_asset.Name ?? "Unnamed"}");
            ImGui.Text($"GUID: {_asset.Guid}");
            ImGui.Text($"Created: {_asset.CreatedAt:yyyy-MM-dd HH:mm:ss}");
            ImGui.Text($"Modified: {_asset.ModifiedAt:yyyy-MM-dd HH:mm:ss}");
            
            ImGui.Spacing();
            
            // Root entity info
            if (_asset.RootEntity != null)
            {
                ImGui.SeparatorText("Root Entity");
                ImGui.Text($"Entity Name: {_asset.RootEntity.Name}");
                ImGui.Text($"Components: {_asset.RootEntity.Components.Count}");
                ImGui.Text($"Children: {_asset.RootEntity.Children.Count}");
                
                // Display component types
                if (_asset.RootEntity.Components.Count > 0)
                {
                    ImGui.Spacing();
                    ImGui.Text("Components:");
                    ImGui.Indent();
                    foreach (var componentType in _asset.RootEntity.Components.Keys)
                    {
                        ImGui.BulletText(componentType);
                    }
                    ImGui.Unindent();
                }
                
                // Display hierarchy recursively
                if (_asset.RootEntity.Children.Count > 0)
                {
                    ImGui.Spacing();
                    ImGui.Text("Hierarchy:");
                    ImGui.Indent();
                    DrawEntityHierarchy(_asset.RootEntity, 0);
                    ImGui.Unindent();
                }
            }
            else
            {
                ImGui.TextColored(new System.Numerics.Vector4(1, 0.5f, 0, 1), "No root entity data");
            }
            
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            
            // Actions
            ImGui.SeparatorText("Actions");
            
            ImGui.TextDisabled("Drag & drop this prefab into the viewport to instantiate");
            
            if (ImGui.Button("Refresh", new System.Numerics.Vector2(150, 0)))
            {
                // Force reload
                _asset = Engine.Assets.AssetDatabase.LoadPrefab(assetGuid);
            }
        }

        private void DrawEntityHierarchy(Engine.Assets.PrefabEntityData entity, int depth)
        {
            string indent = new string(' ', depth * 2);
            ImGui.BulletText($"{indent}{entity.Name} ({entity.Components.Count} components)");
            
            foreach (var child in entity.Children)
            {
                DrawEntityHierarchy(child, depth + 1);
            }
        }

    }
    
    /// <summary>
    /// Helper class to create prefabs from entities and instantiate them
    /// </summary>
    public static class PrefabInstantiator
    {
        /// <summary>
        /// Create a PrefabAsset from an entity (serialize all components and children)
        /// </summary>
        public static Engine.Assets.PrefabAsset CreateFromEntity(Engine.Scene.Entity entity)
        {
            var prefab = new Engine.Assets.PrefabAsset
            {
                Guid = Guid.NewGuid(),
                Name = entity.Name,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow,
                RootEntity = SerializeEntity(entity)
            };
            
            return prefab;
        }
        
        /// <summary>
        /// Serialize an entity to PrefabEntityData (including all components and children)
        /// </summary>
        private static Engine.Assets.PrefabEntityData SerializeEntity(Engine.Scene.Entity entity)
        {
            var data = new Engine.Assets.PrefabEntityData
            {
                Guid = entity.Guid,
                Name = entity.Name,
                Enabled = true, // Default to enabled
                LocalPosition = new[] { entity.Transform.Position.X, entity.Transform.Position.Y, entity.Transform.Position.Z },
                LocalScale = new[] { entity.Transform.Scale.X, entity.Transform.Scale.Y, entity.Transform.Scale.Z }
            };
            
            // Convert quaternion to Euler angles (degrees)
            var euler = entity.Transform.Rotation.ToEulerAngles();
            data.LocalRotation = new[]
            {
                OpenTK.Mathematics.MathHelper.RadiansToDegrees(euler.X),
                OpenTK.Mathematics.MathHelper.RadiansToDegrees(euler.Y),
                OpenTK.Mathematics.MathHelper.RadiansToDegrees(euler.Z)
            };
            
            // Serialize components (skip TransformComponent, it's implicit in position/rotation/scale)
            foreach (var component in entity.GetAllComponents())
            {
                if (component is Engine.Components.TransformComponent) continue; // Skip transform, handled above
                
                try
                {
                    var componentType = component.GetType().Name;
                    
                    // Use ComponentSerializer to avoid circular references (Entity->Parent->Children->...)
                    var componentData = Engine.Serialization.ComponentSerializer.Serialize(component);
                    if (componentData != null && componentData.Count > 0)
                    {
                        var json = System.Text.Json.JsonSerializer.Serialize(componentData);
                        var jsonElement = System.Text.Json.JsonDocument.Parse(json).RootElement;
                        data.Components[componentType] = jsonElement.Clone();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PrefabInstantiator] Failed to serialize component {component.GetType().Name}: {ex.Message}");
                }
            }
            
            // Recursively serialize children
            if (entity.Children.Count > 0)
            {
                foreach (var child in entity.Children)
                {
                    data.Children.Add(SerializeEntity(child));
                }
            }
            
            return data;
        }

        public static Engine.Scene.Entity? Instantiate(Engine.Assets.PrefabEntityData prefabData, Engine.Scene.Scene scene, Engine.Scene.Entity? parent = null)
        {
            if (prefabData == null || scene == null) return null;
            
            try
            {
                // Create entity with proper ID
                var entity = new Engine.Scene.Entity
                {
                    Id = scene.GetNextEntityId(),
                    Guid = System.Guid.NewGuid(),
                    Name = prefabData.Name,
                    Active = true
                };
                
                // Set local transform
                entity.Transform.Position = new OpenTK.Mathematics.Vector3(
                    prefabData.LocalPosition[0],
                    prefabData.LocalPosition[1],
                    prefabData.LocalPosition[2]
                );
                
                entity.Transform.Rotation = OpenTK.Mathematics.Quaternion.FromEulerAngles(
                    OpenTK.Mathematics.MathHelper.DegreesToRadians(prefabData.LocalRotation[0]),
                    OpenTK.Mathematics.MathHelper.DegreesToRadians(prefabData.LocalRotation[1]),
                    OpenTK.Mathematics.MathHelper.DegreesToRadians(prefabData.LocalRotation[2])
                );
                
                entity.Transform.Scale = new OpenTK.Mathematics.Vector3(
                    prefabData.LocalScale[0],
                    prefabData.LocalScale[1],
                    prefabData.LocalScale[2]
                );
                
                // Deserialize and attach components
                foreach (var (componentType, componentData) in prefabData.Components)
                {
                    DeserializeAndAttachComponent(entity, componentType, componentData, scene);
                }
                
                // Add to scene BEFORE setting parent (so children can reference it)
                scene.Entities.Add(entity);
                
                // Set parent after adding to scene
                if (parent != null)
                {
                    entity.SetParent(parent, keepWorld: false);
                }
                
                // Recursively instantiate children
                foreach (var childData in prefabData.Children)
                {
                    Instantiate(childData, scene, entity);
                }
                
                return entity;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PrefabInstantiator] Failed to instantiate entity: {ex.Message}");
                return null;
            }
        }
        
        private static void DeserializeAndAttachComponent(Engine.Scene.Entity entity, string componentType, System.Text.Json.JsonElement componentData, Engine.Scene.Scene scene)
        {
            // Use the same deserialization logic as SceneSerializer
            try
            {
                // Try to find the type in Engine assembly first
                var type = Type.GetType($"Engine.Components.{componentType}, Engine");
                
                // If not found, try Audio components
                if (type == null)
                    type = Type.GetType($"Engine.Audio.Components.{componentType}, Engine");
                
                if (type == null)
                {
                    Console.WriteLine($"[PrefabInstantiator] Unknown component type: {componentType}");
                    return;
                }
                
                // Create component instance using reflection (because AddComponent<T> needs compile-time type)
                var addComponentMethod = typeof(Engine.Scene.Entity).GetMethod("AddComponent", new Type[] { })?.MakeGenericMethod(type);
                if (addComponentMethod == null)
                {
                    Console.WriteLine($"[PrefabInstantiator] Failed to find AddComponent method for {componentType}");
                    return;
                }
                
                var component = addComponentMethod.Invoke(entity, null) as Engine.Components.Component;
                if (component == null)
                {
                    Console.WriteLine($"[PrefabInstantiator] Failed to create component instance for {componentType}");
                    return;
                }
                
                // Deserialize component data using ComponentSerializer (handles proper type conversion)
                var dataDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(componentData.GetRawText());
                if (dataDict != null)
                {
                    Engine.Serialization.ComponentSerializer.Deserialize(component, dataDict);
                    
                    // Resolve references (critical for MeshRenderer to find mesh/material GUIDs)
                    try
                    {
                        Engine.Serialization.ComponentSerializer.ResolveReferences(component, dataDict, scene);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[PrefabInstantiator] Failed to resolve references for {componentType}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PrefabInstantiator] Failed to deserialize component {componentType}: {ex.Message}");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenTK.Mathematics;
using Engine.Scene;
using Engine.Components;

namespace Engine.Serialization
{
    /// <summary>
    /// Scene serializer for Engine/Game runtime - loads scenes without Editor dependencies
    /// </summary>
    public static class SceneSerializer
    {
        private const int CURRENT_VERSION = 5;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            IncludeFields = true
        };

        // Scene file format
        private class SceneFileV4
        {
            [JsonPropertyName("version")] public int Version { get; set; } = CURRENT_VERSION;
            [JsonPropertyName("metadata")] public SceneMetadata Metadata { get; set; } = new();
            [JsonPropertyName("entities")] public List<EntityData> Entities { get; set; } = new();
            [JsonPropertyName("globalSettings")] public GlobalSettings GlobalSettings { get; set; } = new();
        }

        public class SceneMetadata
        {
            [JsonPropertyName("createdAt")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
            [JsonPropertyName("modifiedAt")] public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
            [JsonPropertyName("engineVersion")] public string EngineVersion { get; set; } = Engine.Core.EngineInfo.Version;
            [JsonPropertyName("entityCount")] public int EntityCount { get; set; }
            [JsonPropertyName("checksum")] public string Checksum { get; set; } = "";
        }

        private class GlobalSettings
        {
            [JsonPropertyName("ambientLight")] public float[] AmbientLight { get; set; } = new float[] { 0.2f, 0.2f, 0.3f };
            [JsonPropertyName("gravity")] public float[] Gravity { get; set; } = new float[] { 0f, -9.81f, 0f };
            [JsonPropertyName("renderSettings")] public RenderSettings RenderSettings { get; set; } = new();
        }

        private class RenderSettings
        {
            [JsonPropertyName("shadowsEnabled")] public bool ShadowsEnabled { get; set; } = true;
            [JsonPropertyName("antiAliasing")] public string AntiAliasing { get; set; } = "MSAA4x";
            [JsonPropertyName("fogEnabled")] public bool FogEnabled { get; set; } = false;
        }

        private class EntityData
        {
            [JsonPropertyName("guid")] public Guid Guid { get; set; }
            [JsonPropertyName("name")] public string Name { get; set; } = "Entity";
            [JsonPropertyName("parentGuid")] public Guid? ParentGuid { get; set; }
            [JsonPropertyName("components")] public Dictionary<string, JsonElement> Components { get; set; } = new();
            [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;
        }

        /// <summary>
        /// Deserialize a scene from JSON string
        /// </summary>
        public static Scene.Scene? Deserialize(string json)
        {
            try
            {
                var scene = new Scene.Scene();
                var result = Load(scene, json);
                return result.Success ? scene : null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SceneSerializer] Deserialize failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Load a scene from a file path
        /// </summary>
        public static LoadResult LoadFromFile(Scene.Scene scene, string filePath)
        {
            var result = new LoadResult { Success = false, FilePath = filePath };

            if (!File.Exists(filePath))
            {
                result.ErrorMessage = "File not found";
                return result;
            }

            try
            {
                string json;

                // Check if compressed
                if (IsCompressedFile(filePath))
                {
                    json = ReadCompressedFile(filePath);
                }
                else
                {
                    json = File.ReadAllText(filePath);
                }

                return Load(scene, json, filePath);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Load failed: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// Load scene from JSON string
        /// </summary>
        public static LoadResult Load(Scene.Scene scene, string json, string? filePath = null)
        {
            var result = new LoadResult { Success = false, FilePath = filePath ?? "" };

            try
            {
                SceneFileV4? sceneFile = null;

                // Detect version
                var version = DetectFileVersion(json);
                result.DetectedVersion = version;

                switch (version)
                {
                    case 4:
                    case 5:
                        sceneFile = JsonSerializer.Deserialize<SceneFileV4>(json, _jsonOptions);
                        break;
                    case 3:
                        sceneFile = JsonSerializer.Deserialize<SceneFileV4>(json, _jsonOptions);
                        break;
                    default:
                        result.ErrorMessage = $"Unsupported file version: {version}";
                        return result;
                }

                if (sceneFile == null)
                {
                    result.ErrorMessage = "Failed to deserialize scene file";
                    return result;
                }

                // Load into scene
                ApplySceneData(scene, sceneFile, result);

                // Invalidate component cache
                scene.Cache?.Invalidate();

                result.Success = true;
                result.LoadedEntityCount = sceneFile.Entities.Count;
                result.SceneMetadata = sceneFile.Metadata;
            }
            catch (Exception e)
            {
                result.ErrorMessage = $"Load failed: {e.Message}";
            }

            return result;
        }

        private static void ApplySceneData(Scene.Scene scene, SceneFileV4 sceneFile, LoadResult result)
        {
            // Clear existing entities
            foreach (var entity in scene.Entities.ToList())
            {
                foreach (var component in entity.GetAllComponents())
                {
                    component.OnDetached();
                }
            }
            scene.Entities.Clear();

            // Force cleanup of orphaned colliders
            try
            {
                Engine.Physics.PhysicsManager.Instance.CleanupInvalidColliders();
            }
            catch { }

            var guidToEntity = new Dictionary<Guid, Entity>();
            var parentRelationships = new List<(Entity child, Guid parentGuid)>();

            // First pass: create all entities
            foreach (var entityData in sceneFile.Entities)
            {
                try
                {
                    var entity = CreateEntityFromData(scene, entityData);
                    if (entity != null)
                    {
                        guidToEntity[entityData.Guid] = entity;

                        if (entityData.ParentGuid.HasValue)
                        {
                            parentRelationships.Add((entity, entityData.ParentGuid.Value));
                        }
                    }
                }
                catch (Exception e)
                {
                    result.Warnings.Add($"Failed to load entity {entityData.Name}: {e.Message}");
                }
            }

            // Second pass: establish parent-child relationships
            foreach (var (child, parentGuid) in parentRelationships)
            {
                if (guidToEntity.TryGetValue(parentGuid, out var parent))
                {
                    child.SetParent(parent, keepWorld: false);
                }
                else
                {
                    result.Warnings.Add($"Entity {child.Name} references missing parent {parentGuid}");
                }
            }

            // Third pass: resolve deferred references
            ResolveAllDeferredReferences(scene, sceneFile, result);
        }

        private static Entity? CreateEntityFromData(Scene.Scene scene, EntityData entityData)
        {
            var transform = ExtractTransformData(entityData.Components);

            var entity = new Entity { Name = entityData.Name ?? "Entity" };
            entity.Guid = entityData.Guid;
            entity.Transform.Position = transform.position;
            entity.Transform.Rotation = transform.rotation;
            entity.Transform.Scale = transform.scale;

            // Load components
            LoadEntityComponents(scene, entity, entityData.Components);

            // Backward compatibility: if no MeshRenderer but old Render data exists
            var render = ExtractRenderData(entityData.Components);
            if (!entity.HasComponent<MeshRendererComponent>() && render.mesh != MeshKind.None)
            {
                var meshRenderer = entity.AddComponent<MeshRendererComponent>();
                meshRenderer.Mesh = render.mesh;
                if (render.materialGuid.HasValue)
                    meshRenderer.SetMaterial(render.materialGuid.Value);
            }

            entity.Id = scene.GetNextEntityId();
            scene.AddEntity(entity);  // Use AddEntity to properly set Entity.Scene reference
            return entity;
        }

        private static (Vector3 position, Quaternion rotation, Vector3 scale) ExtractTransformData(Dictionary<string, JsonElement> components)
        {
            var position = Vector3.Zero;
            var rotation = Quaternion.Identity;
            var scale = Vector3.One;

            if (components.TryGetValue("Transform", out var transformElement))
            {
                try
                {
                    var transformData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(transformElement);

                    if (transformData != null)
                    {
                        if (transformData.TryGetValue("position", out var pos))
                        {
                            var posArray = JsonSerializer.Deserialize<float[]>(pos);
                            if (posArray?.Length == 3)
                                position = new Vector3(posArray[0], posArray[1], posArray[2]);
                        }

                        if (transformData.TryGetValue("rotation", out var rot))
                        {
                            var rotArray = JsonSerializer.Deserialize<float[]>(rot);
                            if (rotArray?.Length == 4)
                                rotation = new Quaternion(rotArray[0], rotArray[1], rotArray[2], rotArray[3]);
                        }

                        if (transformData.TryGetValue("scale", out var scl))
                        {
                            var sclArray = JsonSerializer.Deserialize<float[]>(scl);
                            if (sclArray?.Length == 3)
                                scale = new Vector3(sclArray[0], sclArray[1], sclArray[2]);
                        }
                    }
                }
                catch { }
            }

            return (position, rotation, scale);
        }

        private static (Vector4 color, MeshKind mesh, Guid? materialGuid) ExtractRenderData(Dictionary<string, JsonElement> components)
        {
            var color = new Vector4(1, 1, 1, 1);
            var mesh = MeshKind.None;
            Guid? materialGuid = null;

            if (components.TryGetValue("Render", out var renderElement))
            {
                try
                {
                    var renderData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(renderElement);

                    if (renderData != null)
                    {
                        if (renderData.TryGetValue("color", out var col))
                        {
                            var colArray = JsonSerializer.Deserialize<float[]>(col);
                            if (colArray?.Length == 4)
                                color = new Vector4(colArray[0], colArray[1], colArray[2], colArray[3]);
                        }

                        if (renderData.TryGetValue("mesh", out var meshEl))
                        {
                            var meshStr = meshEl.GetString();
                            if (Enum.TryParse<MeshKind>(meshStr, out var parsedMesh))
                                mesh = parsedMesh;
                        }

                        if (renderData.TryGetValue("materialGuid", out var matEl))
                        {
                            var matStr = matEl.GetString();
                            if (!string.IsNullOrEmpty(matStr) && Guid.TryParse(matStr, out var guid))
                                materialGuid = guid;
                        }
                    }
                }
                catch { }
            }

            return (color, mesh, materialGuid);
        }

        private static void LoadEntityComponents(Scene.Scene scene, Entity entity, Dictionary<string, JsonElement> components)
        {
            // Load TransformComponent
            if (components.TryGetValue("Transform", out var transformElement))
            {
                try
                {
                    var transformData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(transformElement);
                    if (transformData != null)
                    {
                        var transformComp = entity.GetComponent<TransformComponent>();
                        if (transformComp != null)
                        {
                            if (transformData.TryGetValue("position", out var pos))
                            {
                                var posArray = JsonSerializer.Deserialize<float[]>(pos);
                                if (posArray?.Length == 3)
                                {
                                    var position = new Vector3(posArray[0], posArray[1], posArray[2]);
                                    transformComp.Position = position;
                                    entity.Transform.Position = position;
                                }
                            }

                            if (transformData.TryGetValue("rotation", out var rot))
                            {
                                var rotArray = JsonSerializer.Deserialize<float[]>(rot);
                                if (rotArray?.Length == 4)
                                {
                                    var rotation = new Quaternion(rotArray[0], rotArray[1], rotArray[2], rotArray[3]);
                                    transformComp.Rotation = rotation;
                                    entity.Transform.Rotation = rotation;
                                }
                            }

                            if (transformData.TryGetValue("scale", out var scl))
                            {
                                var sclArray = JsonSerializer.Deserialize<float[]>(scl);
                                if (sclArray?.Length == 3)
                                {
                                    var scale = new Vector3(sclArray[0], sclArray[1], sclArray[2]);
                                    transformComp.Scale = scale;
                                    entity.Transform.Scale = scale;
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            // Load MeshRenderer
            if (components.TryGetValue("MeshRenderer", out var meshRendererElement))
            {
                try
                {
                    var meshRenderer = entity.AddComponent<MeshRendererComponent>();
                    var renderData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(meshRendererElement);
                    if (renderData != null)
                    {
                        ComponentSerializer.Deserialize(meshRenderer, renderData);
                        try { ComponentSerializer.ResolveReferences(meshRenderer, renderData, scene); } catch { }
                    }
                }
                catch { }
            }

            // Load CameraComponent
            if (components.TryGetValue("CameraComponent", out var cameraElement))
            {
                try
                {
                    var cameraData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(cameraElement);
                    if (cameraData != null)
                    {
                        var camera = entity.AddComponent<CameraComponent>();
                        ComponentSerializer.Deserialize(camera, cameraData);
                    }
                }
                catch { }
            }

            // Load Light
            if (components.TryGetValue("Light", out var lightElement))
            {
                try
                {
                    var light = entity.AddComponent<LightComponent>();
                    var lightData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(lightElement);
                    if (lightData != null)
                    {
                        ComponentSerializer.Deserialize(light, lightData);
                    }
                }
                catch { }
            }

            // Load CharacterController
            if (components.TryGetValue("CharacterController", out var ccElement))
            {
                try
                {
                    var cc = entity.AddComponent<Engine.Components.CharacterController>();
                    var ccData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(ccElement);
                    if (ccData != null)
                    {
                        ComponentSerializer.Deserialize(cc, ccData);
                    }
                }
                catch { }
            }

            // Load colliders
            if (components.TryGetValue("BoxCollider", out var boxElement))
            {
                try
                {
                    var box = entity.AddComponent<Engine.Physics.BoxCollider>();
                    var boxData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(boxElement);
                    if (boxData != null)
                    {
                        ComponentSerializer.Deserialize(box, boxData);
                    }
                }
                catch { }
            }

            if (components.TryGetValue("SphereCollider", out var sphereElement))
            {
                try
                {
                    var sphere = entity.AddComponent<Engine.Physics.SphereCollider>();
                    var sphereData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(sphereElement);
                    if (sphereData != null)
                    {
                        ComponentSerializer.Deserialize(sphere, sphereData);
                    }
                }
                catch { }
            }

            if (components.TryGetValue("CapsuleCollider", out var capsuleElement))
            {
                try
                {
                    var capsule = entity.AddComponent<Engine.Physics.CapsuleCollider>();
                    var capsuleData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(capsuleElement);
                    if (capsuleData != null)
                    {
                        ComponentSerializer.Deserialize(capsule, capsuleData);
                    }
                }
                catch { }
            }

            // Note: MonoBehaviour scripts are skipped in Game runtime
            // They require ScriptHost from Editor which is not available

            // Load other components automatically
            var handledComponents = new HashSet<string>
            {
                "Transform", "MeshRenderer", "CameraComponent", "Light", "CharacterController",
                "BoxCollider", "SphereCollider", "CapsuleCollider"
            };

            foreach (var kvp in components)
            {
                if (handledComponents.Contains(kvp.Key))
                    continue;

                // Handle Script_ components (MonoBehaviour scripts)
                if (kvp.Key.StartsWith("Script_"))
                {
                    try
                    {
                        LoadScriptComponent(scene, entity, kvp.Key, kvp.Value);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[SceneSerializer] Failed to load script {kvp.Key}: {ex.Message}");
                    }
                    continue;
                }

                try
                {
                    LoadComponentAutomatically(scene, entity, kvp.Key, kvp.Value);
                }
                catch { }
            }
        }

        private static void LoadScriptComponent(Scene.Scene scene, Entity entity, string scriptKey, JsonElement scriptElement)
        {
            var scriptData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(scriptElement);
            if (scriptData == null) return;

            // Get the type name from the script data
            if (!scriptData.TryGetValue("typeName", out var typeNameEl))
                return;

            var typeName = typeNameEl.GetString();
            if (string.IsNullOrEmpty(typeName))
                return;

            // Find the script type in loaded assemblies
            Type? scriptType = FindScriptType(typeName);
            if (scriptType == null)
            {
                Console.WriteLine($"[SceneSerializer] Script type not found: {typeName}");
                return;
            }

            // Create script instance
            var script = Activator.CreateInstance(scriptType) as Engine.Scripting.MonoBehaviour;
            if (script == null)
            {
                Console.WriteLine($"[SceneSerializer] Failed to create script instance: {typeName}");
                return;
            }

            // Add script to entity using AddComponent<MonoBehaviour>
            entity.AddComponent(script);

            // Deserialize script fields
            if (scriptData.TryGetValue("fields", out var fieldsElement))
            {
                try
                {
                    var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(fieldsElement);
                    if (fields != null)
                    {
                        DeserializeScriptFields(script, fields, scene);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SceneSerializer] Failed to deserialize script fields: {ex.Message}");
                }
            }

            Console.WriteLine($"[SceneSerializer] Loaded script: {scriptType.Name} on {entity.Name}");
        }

        private static Type? FindScriptType(string typeName)
        {
            // Try to find the type by full name first
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType(typeName);
                    if (type != null && typeof(Engine.Scripting.MonoBehaviour).IsAssignableFrom(type))
                        return type;
                }
                catch { }
            }

            // Try to find by simple name
            string simpleName = typeName.Contains('.') ? typeName.Substring(typeName.LastIndexOf('.') + 1) : typeName;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var types = assembly.GetTypes()
                        .Where(t => t.Name == simpleName && typeof(Engine.Scripting.MonoBehaviour).IsAssignableFrom(t))
                        .ToArray();
                    if (types.Length > 0)
                        return types[0];
                }
                catch { }
            }

            return null;
        }

        private static void DeserializeScriptFields(Engine.Scripting.MonoBehaviour script, Dictionary<string, JsonElement> fields, Scene.Scene scene)
        {
            var scriptType = script.GetType();

            foreach (var kvp in fields)
            {
                var fieldName = kvp.Key;
                var fieldValue = kvp.Value;

                // Try to find field or property
                var field = scriptType.GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                var prop = scriptType.GetProperty(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (field != null)
                {
                    try
                    {
                        var value = DeserializeFieldValue(fieldValue, field.FieldType, scene);
                        if (value != null || !field.FieldType.IsValueType)
                            field.SetValue(script, value);
                    }
                    catch { }
                }
                else if (prop != null && prop.CanWrite)
                {
                    try
                    {
                        var value = DeserializeFieldValue(fieldValue, prop.PropertyType, scene);
                        if (value != null || !prop.PropertyType.IsValueType)
                            prop.SetValue(script, value);
                    }
                    catch { }
                }
            }
        }

        private static object? DeserializeFieldValue(JsonElement element, Type targetType, Scene.Scene scene)
        {
            // Handle entity references
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("entityGuid", out var guidEl))
            {
                var guidStr = guidEl.GetString();
                if (!string.IsNullOrEmpty(guidStr) && Guid.TryParse(guidStr, out var entityGuid))
                {
                    // Find entity by GUID
                    var entity = scene.Entities.FirstOrDefault(e => e.Guid == entityGuid);
                    if (entity != null)
                    {
                        // If target type is Entity, return entity
                        if (targetType == typeof(Entity))
                            return entity;

                        // If target type is a component type, get the component
                        if (typeof(Component).IsAssignableFrom(targetType))
                        {
                            if (element.TryGetProperty("componentType", out var compTypeEl))
                            {
                                var compTypeName = compTypeEl.GetString();
                                if (!string.IsNullOrEmpty(compTypeName))
                                {
                                    // Find the component by type name
                                    foreach (var comp in entity.GetAllComponents())
                                    {
                                        if (comp.GetType().FullName == compTypeName || comp.GetType().Name == compTypeName.Split('.').Last())
                                            return comp;
                                    }
                                }
                            }
                            // Fallback: get first component of target type
                            return entity.GetAllComponents().FirstOrDefault(c => targetType.IsAssignableFrom(c.GetType()));
                        }
                    }
                }
                return null;
            }

            // Handle primitives
            if (targetType == typeof(float))
                return element.ValueKind == JsonValueKind.Number ? element.GetSingle() : 0f;
            if (targetType == typeof(int))
                return element.ValueKind == JsonValueKind.Number ? element.GetInt32() : 0;
            if (targetType == typeof(bool))
                return element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False ? element.GetBoolean() : false;
            if (targetType == typeof(string))
                return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
            if (targetType == typeof(double))
                return element.ValueKind == JsonValueKind.Number ? element.GetDouble() : 0.0;

            // Handle Vector3
            if (targetType == typeof(OpenTK.Mathematics.Vector3) && element.ValueKind == JsonValueKind.Array)
            {
                var arr = element.EnumerateArray().ToArray();
                if (arr.Length >= 3)
                    return new OpenTK.Mathematics.Vector3(arr[0].GetSingle(), arr[1].GetSingle(), arr[2].GetSingle());
            }

            // Handle Vector2
            if (targetType == typeof(OpenTK.Mathematics.Vector2) && element.ValueKind == JsonValueKind.Array)
            {
                var arr = element.EnumerateArray().ToArray();
                if (arr.Length >= 2)
                    return new OpenTK.Mathematics.Vector2(arr[0].GetSingle(), arr[1].GetSingle());
            }

            return null;
        }

        private static void LoadComponentAutomatically(Scene.Scene scene, Entity entity, string componentTypeName, JsonElement componentElement)
        {
            var componentType = FindComponentType(componentTypeName);
            if (componentType == null) return;

            if (entity.GetAllComponents().Any(c => c.GetType() == componentType))
                return;

            try
            {
                var component = Activator.CreateInstance(componentType) as Component;
                if (component == null) return;

                var genericAddMethod = typeof(Entity).GetMethods()
                    .FirstOrDefault(m => m.Name == "AddComponent" &&
                                       m.IsGenericMethod &&
                                       m.GetParameters().Length == 1 &&
                                       m.GetParameters()[0].ParameterType.IsGenericParameter);

                if (genericAddMethod != null)
                {
                    var typedMethod = genericAddMethod.MakeGenericMethod(componentType);
                    typedMethod.Invoke(entity, new[] { component });
                }

                var componentData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(componentElement);
                if (componentData != null)
                {
                    ComponentSerializer.Deserialize(component, componentData);
                    ComponentSerializer.ResolveReferences(component, componentData, scene);

                    // Handle AudioSource clip loading
                    try
                    {
                        if (component is Engine.Audio.Components.AudioSource audioComp && audioComp.ClipGuid.HasValue)
                        {
                            var clipGuid = audioComp.ClipGuid.Value;
                            var loaded = Engine.Audio.Assets.AudioImporter.GetClip(clipGuid);
                            if (loaded == null)
                            {
                                if (Engine.Assets.AssetDatabase.TryGet(clipGuid, out var rec))
                                {
                                    try { loaded = Engine.Audio.Assets.AudioImporter.LoadClip(rec.Path); } catch { }
                                }
                            }
                            if (loaded != null)
                            {
                                audioComp.Clip = loaded;
                            }
                        }

                        if (component is Engine.Audio.Components.AudioSource audioWithFilters && audioWithFilters.Filters != null)
                        {
                            foreach (var f in audioWithFilters.Filters)
                            {
                                Engine.Audio.Components.AudioSourceFilterExtensions.EnsureFilterHandle(f);
                            }
                        }
                    }
                    catch { }

                    // Handle Terrain generation
                    if (component is Engine.Components.Terrain terrain && terrain.HeightmapTextureGuid.HasValue)
                    {
                        try { terrain.GenerateTerrain(); } catch { }
                    }
                }
            }
            catch { }
        }

        private static Type? FindComponentType(string typeName)
        {
            var engineAssembly = typeof(Component).Assembly;
            var componentType = engineAssembly.GetTypes()
                                             .FirstOrDefault(t => t.Name == typeName && t.IsSubclassOf(typeof(Component)));

            if (componentType != null)
                return componentType;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    componentType = assembly.GetTypes()
                                           .FirstOrDefault(t => t.Name == typeName && t.IsSubclassOf(typeof(Component)));
                    if (componentType != null)
                        return componentType;
                }
                catch { }
            }

            return null;
        }

        private static void ResolveAllDeferredReferences(Scene.Scene scene, SceneFileV4 sceneFile, LoadResult result)
        {
            var guidToEntity = scene.Entities.ToDictionary(e => e.Guid);

            foreach (var entityData in sceneFile.Entities)
            {
                if (!guidToEntity.TryGetValue(entityData.Guid, out var entity))
                    continue;

                foreach (var componentKvp in entityData.Components)
                {
                    var componentName = componentKvp.Key;
                    var componentElement = componentKvp.Value;

                    if (componentName == "Transform" || componentName.StartsWith("Script_"))
                        continue;

                    try
                    {
                        var componentData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(componentElement);
                        if (componentData != null)
                        {
                            var componentType = FindComponentType(componentName);
                            if (componentType != null)
                            {
                                var component = entity.GetComponent(componentType);
                                if (component != null)
                                {
                                    ComponentSerializer.ResolveReferences(component, componentData, scene);
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        result.Warnings.Add($"Failed to resolve references for {componentName} on {entity.Name}: {e.Message}");
                    }
                }
            }
        }

        private static int DetectFileVersion(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("version", out var versionProp))
                {
                    return versionProp.GetInt32();
                }
                return doc.RootElement.TryGetProperty("entities", out _) ? 2 : 1;
            }
            catch
            {
                return 1;
            }
        }

        private static bool IsCompressedFile(string filePath)
        {
            try
            {
                using var fs = File.OpenRead(filePath);
                var buffer = new byte[3];
                fs.Read(buffer, 0, 3);
                return buffer[0] == 0x1F && buffer[1] == 0x8B && buffer[2] == 0x08;
            }
            catch
            {
                return false;
            }
        }

        private static string ReadCompressedFile(string filePath)
        {
            using var fileStream = File.OpenRead(filePath);
            using var gzip = new System.IO.Compression.GZipStream(fileStream, System.IO.Compression.CompressionMode.Decompress);
            using var reader = new StreamReader(gzip);
            return reader.ReadToEnd();
        }
    }

    // Result class
    public class LoadResult
    {
        public bool Success { get; set; }
        public string FilePath { get; set; } = "";
        public string? ErrorMessage { get; set; }
        public List<string> Warnings { get; set; } = new();
        public int DetectedVersion { get; set; }
        public int LoadedEntityCount { get; set; }
        public SceneSerializer.SceneMetadata? SceneMetadata { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenTK.Mathematics;
using Engine.Scene;
using Engine.Components;
using Engine.Serialization;

namespace Editor.Serialization
{
    /// <summary>
    /// Serializer robuste avec gestion d'erreurs, compression optionnelle et versioning strict
    /// </summary>
    public static class SceneSerializer
    {
        private const int CURRENT_VERSION = 5;
        private const string BACKUP_SUFFIX = ".backup";
        private const string TEMP_SUFFIX = ".tmp";

        // Configurable options
        public static bool EnableCompression { get; set; } = true;
        public static bool CreateBackups { get; set; } = true;
        public static int MaxBackupFiles { get; set; } = 1;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        // Scene file format v5 (supports custom mesh imports via CustomMeshGuid)
        private class SceneFileV4 // Keep class name for compatibility
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

        public static SaveResult Save(Scene scene, string filePath)
        {
            var result = new SaveResult { Success = false, FilePath = filePath };
            var tempPath = filePath + TEMP_SUFFIX;

            try
            {
                // Create backup if file exists
                if (CreateBackups && File.Exists(filePath))
                {
                    CreateBackupFile(filePath);
                }

                // Prepare scene data
                var sceneFile = PrepareSceneData(scene);

                // Write to temporary file first (atomic operation)
                Directory.CreateDirectory(Path.GetDirectoryName(tempPath) ?? "");

                string finalJson;
                if (EnableCompression)
                {
                    WriteCompressedFile(tempPath, sceneFile);
                    // For compressed files, skip checksum verification for now
                }
                else
                {
                    finalJson = JsonSerializer.Serialize(sceneFile, _jsonOptions);
                    sceneFile.Metadata.Checksum = CalculateChecksum(finalJson);
                    
                    // Re-serialize with the correct checksum
                    finalJson = JsonSerializer.Serialize(sceneFile, _jsonOptions);
                    File.WriteAllText(tempPath, finalJson);
                }

                // Verify written file exists and is readable
                if (File.Exists(tempPath) && new FileInfo(tempPath).Length > 0)
                {
                    // Atomic move
                    if (File.Exists(filePath))
                        File.Delete(filePath);
                    File.Move(tempPath, filePath);

                    result.Success = true;
                    result.EntityCount = sceneFile.Entities.Count;
                }
                else
                {
                    result.ErrorMessage = "File verification failed after write";
                }
            }
            catch (Exception e)
            {
                result.ErrorMessage = $"Save failed: {e.Message}";

                // Cleanup temp file
                try
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch { }
            }

            return result;
        }

        public static LoadResult Load(Scene scene, string filePath)
        {
            var result = new LoadResult { Success = false, FilePath = filePath };

            if (!File.Exists(filePath))
            {
                result.ErrorMessage = "File not found";
                return result;
            }

            try
            {
                SceneFileV4? sceneFile = null;

                // Try loading as compressed first
                if (IsCompressedFile(filePath))
                {
                    sceneFile = ReadCompressedFile(filePath);
                }
                else
                {
                    var json = File.ReadAllText(filePath);

                    // Detect version
                    var version = DetectFileVersion(json);
                    result.DetectedVersion = version;

                    switch (version)
                    {
                        case 4:
                            sceneFile = JsonSerializer.Deserialize<SceneFileV4>(json, _jsonOptions);
                            break;
                        case 3:
                            sceneFile = ConvertFromV3(json);
                            break;
                        case 2:
                            sceneFile = ConvertFromV2(json);
                            break;
                        case 1:
                            sceneFile = ConvertFromV1(json);
                            break;
                        default:
                            result.ErrorMessage = $"Unsupported file version: {version}";
                            return result;
                    }
                }

                if (sceneFile == null)
                {
                    result.ErrorMessage = "Failed to deserialize scene file";
                    return result;
                }

                // Verify integrity
                if (!string.IsNullOrEmpty(sceneFile.Metadata.Checksum))
                {
                    var recalculatedChecksum = CalculateChecksum(JsonSerializer.Serialize(sceneFile, _jsonOptions));
                    if (recalculatedChecksum != sceneFile.Metadata.Checksum)
                    {
                        result.Warnings.Add("File checksum mismatch - file may be corrupted");
                    }
                }

                // PERFORMANCE: AssetDatabase is already initialized and refreshed at startup
                // No need to refresh again here - it just scans the entire asset folder for no reason
                // If assets were added/modified during runtime, they would be indexed on-demand
                // Commented out to save ~3.7 seconds of startup time:
                // try
                // {
                //     Console.WriteLine("[SceneSerializer] Refreshing AssetDatabase before applying scene data");
                //     Editor.Utils.StartupProfiler.BeginSection("    AssetDatabase.Refresh()");
                //     Engine.Assets.AssetDatabase.Refresh();
                //     Editor.Utils.StartupProfiler.EndSection();
                // }
                // catch (Exception ex)
                // {
                //     Console.WriteLine($"[SceneSerializer] AssetDatabase.Refresh failed: {ex.Message}");
                // }

                // Load into scene
                Editor.Utils.StartupProfiler.BeginSection("    ApplySceneData()");
                ApplySceneData(scene, sceneFile, result);
                Editor.Utils.StartupProfiler.EndSection();

                result.Success = true;
                result.LoadedEntityCount = sceneFile.Entities.Count;
                result.SceneMetadata = sceneFile.Metadata;
            }
            catch (Exception e)
            {
                result.ErrorMessage = $"Load failed: {e.Message}";

                // Try loading from backup
                if (CreateBackups && TryLoadFromBackup(scene, filePath, out var backupResult))
                {
                    result = backupResult;
                    result.Warnings.Add("Loaded from backup due to main file corruption");
                }
            }

            return result;
        }

        private static SceneFileV4 PrepareSceneData(Scene scene)
        {
            var sceneFile = new SceneFileV4();
            var entityMap = new Dictionary<uint, Guid>();

            // First pass: assign GUIDs and collect basic data
            foreach (var entity in scene.Entities)
            {
                if (entity.Guid == Guid.Empty)
                    entity.Guid = Guid.NewGuid();

                entityMap[entity.Id] = entity.Guid;
            }

            // Second pass: serialize with proper relationships
            foreach (var entity in scene.Entities)
            {
                var entityData = new EntityData
                {
                    Guid = entity.Guid,
                    Name = entity.Name ?? "Entity",
                    ParentGuid = entity.Parent != null ? entityMap.GetValueOrDefault(entity.Parent.Id) : null,
                    Enabled = true // TODO: Add enabled property to Entity
                };

                // Serialize components
                SerializeEntityComponents(entity, entityData.Components);

                sceneFile.Entities.Add(entityData);
            }

            // Update metadata
            sceneFile.Metadata.ModifiedAt = DateTime.UtcNow;
            sceneFile.Metadata.EntityCount = sceneFile.Entities.Count;

            return sceneFile;
        }

        private static void SerializeEntityComponents(Entity entity, Dictionary<string, JsonElement> components)
        {
            // Serialize all components using the automatic system
            foreach (var component in entity.GetAllComponents())
            {
                var componentTypeName = component.GetType().Name;
                
                // Utiliser le système de sérialisation automatique
                var componentData = ComponentSerializer.Serialize(component);
                
                // Gérer les cas spéciaux pour la compatibilité ascendante
                switch (component)
                {
                    case TransformComponent transformComp:
                        // Garder le format existant pour Transform pour compatibilité
                        var transform = new Dictionary<string, object>
                        {
                            ["position"] = new float[] { transformComp.Position.X, transformComp.Position.Y, transformComp.Position.Z },
                            ["rotation"] = new float[] { transformComp.Rotation.X, transformComp.Rotation.Y, transformComp.Rotation.Z, transformComp.Rotation.W },
                            ["scale"] = new float[] { transformComp.Scale.X, transformComp.Scale.Y, transformComp.Scale.Z }
                        };
                        components["Transform"] = JsonSerializer.SerializeToElement(transform);
                        break;
                        
                    case MeshRendererComponent meshRenderer:
                        // Utiliser le système automatique de sérialisation maintenant que les attributs sont ajoutés
                        var meshRendererData = ComponentSerializer.Serialize(meshRenderer);
                        components["MeshRenderer"] = JsonSerializer.SerializeToElement(meshRendererData);
                        break;
                        
                    case CameraComponent camera:
                        // Utiliser le système automatique de sérialisation pour tous les champs [Serializable]
                        var cameraData = ComponentSerializer.Serialize(camera);
                        components["CameraComponent"] = JsonSerializer.SerializeToElement(cameraData);
                        break;
                        
                    case LightComponent light:
                        // Utiliser le système automatique de sérialisation maintenant que les attributs sont ajoutés
                        var lightData = ComponentSerializer.Serialize(light);
                        components["Light"] = JsonSerializer.SerializeToElement(lightData);
                        break;
                        
                    case CharacterController characterController:
                        // Garder le format existant pour CharacterController pour compatibilité
                        var ccData = new Dictionary<string, object>
                        {
                            ["height"] = characterController.Height,
                            ["radius"] = characterController.Radius,
                            ["stepOffset"] = characterController.StepOffset,
                            // useGravity is always true in the new simple system
                            ["gravity"] = characterController.Gravity,
                            ["isGrounded"] = characterController.IsGrounded
                        };
                        components["CharacterController"] = JsonSerializer.SerializeToElement(ccData);
                        break;
                        
                    case Engine.Scripting.MonoBehaviour script:
                        // Garder le format existant pour MonoBehaviour pour compatibilité
                        var scriptData = new Dictionary<string, object>
                        {
                            ["typeName"] = script.GetType().FullName ?? string.Empty,
                            ["fields"] = SerializeScriptFields(script)
                        };
                        components[$"Script_{script.GetType().Name}"] = JsonSerializer.SerializeToElement(scriptData);
                        break;
                        
                    default:
                        // Utiliser la sérialisation automatique pour tous les autres components
                        if (componentData.Count > 0)
                        {
                                // Debug: log serialization for investigation
                                if (componentTypeName == "EnvironmentSettings" || componentTypeName == "Terrain")
                                {
                                    try
                                    {
                                        var dbg = JsonSerializer.Serialize(componentData, _jsonOptions);
                                        Console.WriteLine($"[SceneSerializer] Serializing {componentTypeName}: {dbg}");
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"[SceneSerializer] Failed to serialize {componentTypeName}: {ex.Message}");
                                    }
                                }

                                components[componentTypeName] = JsonSerializer.SerializeToElement(componentData);
                        }
                        else
                        {
                            Console.WriteLine($"[SceneSerializer] Component {componentTypeName} has no serializable data - skipping");
                        }
                        break;
                }
            }
            
            // Fallback: if no TransformComponent found, use legacy transform
            if (!components.ContainsKey("Transform"))
            {
                var transform = new Dictionary<string, object>
                {
                    ["position"] = new float[] { entity.Transform.Position.X, entity.Transform.Position.Y, entity.Transform.Position.Z },
                    ["rotation"] = new float[] { entity.Transform.Rotation.X, entity.Transform.Rotation.Y, entity.Transform.Rotation.Z, entity.Transform.Rotation.W },
                    ["scale"] = new float[] { entity.Transform.Scale.X, entity.Transform.Scale.Y, entity.Transform.Scale.Z }
                };
                components["Transform"] = JsonSerializer.SerializeToElement(transform);
            }
        }

        private static Dictionary<string, object> SerializeScriptFields(Engine.Scripting.MonoBehaviour script)
        {
            var fields = new Dictionary<string, object>();
            var type = script.GetType();
            
            
            // Serialize fields marked with [Editable]
            foreach (var field in type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                var editableAttr = field.GetCustomAttributes(typeof(Engine.Inspector.EditableAttribute), true).FirstOrDefault();
                if (editableAttr != null)
                {
                    var value = field.GetValue(script);
                    
                    if (value != null)
                    {
                        // Handle supported types explicitly to avoid object graph cycles
                        switch (value)
                        {
                            case Engine.Scene.Entity ent:
                                var entityRef = new Dictionary<string, object>
                                {
                                    ["entityGuid"] = ent.Guid.ToString()
                                };
                                fields[field.Name] = entityRef;
                                break;
                            case Engine.Components.Component comp:
                                var compRef = new Dictionary<string, object>
                                {
                                    ["entityGuid"] = comp.Entity?.Guid.ToString() ?? string.Empty,
                                    ["componentType"] = comp.GetType().FullName ?? comp.GetType().Name
                                };
                                fields[field.Name] = compRef;
                                break;
                            case OpenTK.Mathematics.Vector3 v3:
                                fields[field.Name] = new float[] { v3.X, v3.Y, v3.Z };
                                break;
                            case OpenTK.Mathematics.Vector2 v2:
                                fields[field.Name] = new float[] { v2.X, v2.Y };
                                break;
                            case OpenTK.Mathematics.Vector4 v4:
                                fields[field.Name] = new float[] { v4.X, v4.Y, v4.Z, v4.W };
                                break;
                            case OpenTK.Mathematics.Quaternion q:
                                fields[field.Name] = new float[] { q.X, q.Y, q.Z, q.W };
                                break;
                            case Enum e:
                                fields[field.Name] = e.ToString();
                                break;
                            default:
                                // primitives and strings are fine
                                fields[field.Name] = value;
                                break;
                        }
                    }
                    else
                    {
                        // Store null as null - this will be handled in deserialization
                        fields[field.Name] = null!;
                    }
                }
            }
            
            return fields;
        }

    private static void LoadMonoBehaviourScript(Scene scene, Entity entity, string typeName, Dictionary<string, JsonElement> scriptData)
        {
            // Get the ScriptHost to create the script instance

            var scriptHost = Editor.Program.ScriptHost;
            if (scriptHost == null)
            {
                return;
            }

            // Find the type in available scripts
            var scriptType = scriptHost.AvailableScripts.FirstOrDefault(t => t.FullName == typeName);
            if (scriptType == null)
            {
                return;
            }

            // Add the script to the entity
            var script = scriptHost.AddScriptToEntity(entity, scriptType);
            if (script == null) return;

            // Deserialize fields
            if (scriptData.TryGetValue("fields", out var fieldsElement))
            {
                try
                {
                    var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(fieldsElement);
                    if (fields != null)
                    {
                        DeserializeScriptFields(script, fields, entity, scene);
                    }
                }
                catch { }
            }
        }

        private static void DeserializeScriptFields(Engine.Scripting.MonoBehaviour script, Dictionary<string, JsonElement> fields, Entity entity, Engine.Scene.Scene scene)
        {
            var type = script.GetType();
            
            foreach (var field in type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                var editableAttr = field.GetCustomAttributes(typeof(Engine.Inspector.EditableAttribute), true).FirstOrDefault();
                if (editableAttr != null && fields.TryGetValue(field.Name, out var valueElement))
                {
                    try
                    {
                        // Skip null values
                        if (valueElement.ValueKind == JsonValueKind.Null)
                        {
                            field.SetValue(script, null);
                            continue;
                        }
                        
                        // Skip Entity references - they will be resolved in ResolveScriptReferences
                        if (field.FieldType == typeof(Engine.Scene.Entity))
                        {
                            continue;
                        }
                        // Skip Component references - they will be resolved in ResolveScriptReferences  
                        else if (typeof(Engine.Components.Component).IsAssignableFrom(field.FieldType))
                        {
                            continue;
                        }
                        
                        if (field.FieldType == typeof(float))
                        {
                            field.SetValue(script, valueElement.GetSingle());
                        }
                        else if (field.FieldType == typeof(int))
                        {
                            field.SetValue(script, valueElement.GetInt32());
                        }
                        else if (field.FieldType == typeof(bool))
                        {
                            field.SetValue(script, valueElement.GetBoolean());
                        }
                        else if (field.FieldType == typeof(string))
                        {
                            field.SetValue(script, valueElement.GetString());
                        }
                        else if (field.FieldType == typeof(OpenTK.Mathematics.Vector3))
                        {
                            var arr = JsonSerializer.Deserialize<float[]>(valueElement);
                            if (arr != null && arr.Length >= 3)
                                field.SetValue(script, new OpenTK.Mathematics.Vector3(arr[0], arr[1], arr[2]));
                        }
                        else if (field.FieldType == typeof(OpenTK.Mathematics.Vector2))
                        {
                            var arr = JsonSerializer.Deserialize<float[]>(valueElement);
                            if (arr != null && arr.Length >= 2)
                                field.SetValue(script, new OpenTK.Mathematics.Vector2(arr[0], arr[1]));
                        }
                        else if (field.FieldType == typeof(OpenTK.Mathematics.Quaternion))
                        {
                            var arr = JsonSerializer.Deserialize<float[]>(valueElement);
                            if (arr != null && arr.Length >= 4)
                                field.SetValue(script, new OpenTK.Mathematics.Quaternion(arr[0], arr[1], arr[2], arr[3]));
                        }
                        else if (field.FieldType == typeof(Engine.Scene.Entity))
                        {
                            // Skip Entity references - they will be resolved in ResolveScriptReferences
                            continue;
                        }
                        else if (typeof(Engine.Components.Component).IsAssignableFrom(field.FieldType))
                        {
                            // Skip Component references - they will be resolved in ResolveScriptReferences  
                            continue;
                        }
                        else if (field.FieldType.IsEnum)
                        {
                            var enumStr = valueElement.GetString();
                            if (enumStr != null && Enum.TryParse(field.FieldType, enumStr, out var enumValue))
                                field.SetValue(script, enumValue);
                        }
                        // Add more types as needed
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }

        private static void ApplySceneData(Scene scene, SceneFileV4 sceneFile, LoadResult result)
        {
            scene.Entities.Clear();
            var guidToEntity = new Dictionary<Guid, Entity>();
            var parentRelationships = new List<(Entity child, Guid parentGuid)>();

            // First pass: create all entities
            Editor.Utils.StartupProfiler.BeginSection("      Create entities");
            foreach (var entityData in sceneFile.Entities)
            {
                try
                {
                    var entity = CreateEntityFromData(scene, entityData);
                    if (entity != null)
                    {
                        // Debug: log entity created and its components

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
            Editor.Utils.StartupProfiler.EndSection();

            // Second pass: establish parent-child relationships
            Editor.Utils.StartupProfiler.BeginSection("      Establish hierarchy");
            foreach (var (child, parentGuid) in parentRelationships)
            {
                if (guidToEntity.TryGetValue(parentGuid, out var parent))
                {
                    // Utiliser keepWorld: false car les positions sont déjà sauvegardées en coordonnées locales
                    child.SetParent(parent, keepWorld: false);
                }
                else
                {
                    result.Warnings.Add($"Entity {child.Name} references missing parent {parentGuid}");
                }
            }
            Editor.Utils.StartupProfiler.EndSection();

            // Third pass: resolve deferred references in all components
            Editor.Utils.StartupProfiler.BeginSection("      Resolve references");
            ResolveAllDeferredReferences(scene, sceneFile, result);
            Editor.Utils.StartupProfiler.EndSection();
        }

    private static Entity? CreateEntityFromData(Scene scene, EntityData entityData)
        {
            var transform = ExtractTransformData(entityData.Components);

            // Crée une entité vide avec ID incrémenté par la scene
            var entity = new Entity { Name = entityData.Name ?? "Entity" };
            entity.Guid = entityData.Guid;
            // Appliquer les coordonnées locales sauvegardées
            entity.Transform.Position = transform.position;
            entity.Transform.Rotation = transform.rotation;
            entity.Transform.Scale = transform.scale;

            // Load components (new system)
            Editor.Utils.StartupProfiler.BeginSection($"        Load components ({entityData.Name})");
            LoadEntityComponents(scene, entity, entityData.Components);
            Editor.Utils.StartupProfiler.EndSection();

            // Backward compatibility: if no MeshRenderer but old Render data exists, create MeshRenderer
            var render = ExtractRenderData(entityData.Components);
            // N’ajoute MeshRendererComponent que si un mesh explicite est demandé
            if (!entity.HasComponent<MeshRendererComponent>() && render.mesh != MeshKind.None)
            {
                var meshRenderer = entity.AddComponent<MeshRendererComponent>();
                meshRenderer.Mesh = render.mesh;
                if (render.materialGuid.HasValue)
                    meshRenderer.SetMaterial(render.materialGuid.Value);
            }

            // Assigner un ID unique via la scène
            entity.Id = scene.GetNextEntityId();
            scene.Entities.Add(entity);
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

    private static void LoadEntityComponents(Scene scene, Entity entity, Dictionary<string, JsonElement> components)
        {
            // Load TransformComponent if present (override legacy transform)
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
                                    entity.Transform.Position = position; // Keep in sync
                                }
                            }
                            
                            if (transformData.TryGetValue("rotation", out var rot))
                            {
                                var rotArray = JsonSerializer.Deserialize<float[]>(rot);
                                if (rotArray?.Length == 4)
                                {
                                    var rotation = new Quaternion(rotArray[0], rotArray[1], rotArray[2], rotArray[3]);
                                    transformComp.Rotation = rotation;
                                    entity.Transform.Rotation = rotation; // Keep in sync
                                }
                            }
                            
                            if (transformData.TryGetValue("scale", out var scl))
                            {
                                var sclArray = JsonSerializer.Deserialize<float[]>(scl);
                                if (sclArray?.Length == 3)
                                {
                                    var scale = new Vector3(sclArray[0], sclArray[1], sclArray[2]);
                                    transformComp.Scale = scale;
                                    entity.Transform.Scale = scale; // Keep in sync
                                }
                            }
                        }
                    }
                }
                catch { }
            }
            
            // Load MeshRenderer component using automatic serialization
            if (components.TryGetValue("MeshRenderer", out var meshRendererElement))
            {
                try
                {
                    var meshRenderer = entity.AddComponent<MeshRendererComponent>();
                    var renderData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(meshRendererElement);
                    if (renderData != null)
                    {
                        ComponentSerializer.Deserialize(meshRenderer, renderData);
                        // Resolve any deferred references (material GUIDs, entity/component refs) immediately
                        try
                        {
                            ComponentSerializer.ResolveReferences(meshRenderer, renderData, scene);
                        }
                        catch { }

                        // PERFORMANCE: Debug logs disabled - they slow down scene loading significantly
                        // Debug: print deserialized GUIDs to help trace missing materials/meshes
                        // Console.WriteLine($"[SceneSerializer] MeshRenderer on '{entity.Name}' deserialized: CustomMeshGuid={meshRenderer.CustomMeshGuid}, MaterialGuid={meshRenderer.MaterialGuid}");
                        
                        // Fallback: if the GUIDs were not found (re-imports can change GUIDs),
                        // attempt to locate assets inside a model folder matching the entity name
                        // and remap the component to the available asset GUIDs.
                        try
                        {
                            var assetsRoot = Engine.Assets.AssetDatabase.AssetsRoot;
                            if (!string.IsNullOrWhiteSpace(entity.Name) && !string.IsNullOrWhiteSpace(assetsRoot))
                            {
                                var modelFolder = System.IO.Path.Combine(assetsRoot, "Models", entity.Name);
                                if (meshRenderer.CustomMeshGuid.HasValue && !Engine.Assets.AssetDatabase.TryGet(meshRenderer.CustomMeshGuid.Value, out _))
                                {
                                    if (System.IO.Directory.Exists(modelFolder))
                                    {
                                        var meshFiles = System.IO.Directory.GetFiles(modelFolder, "*" + Engine.Assets.AssetDatabase.MeshAssetExt, System.IO.SearchOption.TopDirectoryOnly);
                                        if (meshFiles.Length > 0)
                                        {
                                            try
                                            {
                                                var fallbackMesh = Engine.Assets.MeshAsset.Load(meshFiles[0]);
                                                meshRenderer.CustomMeshGuid = fallbackMesh.Guid;
                                                // PERFORMANCE: Disabled log
                                                // Console.WriteLine($"[SceneSerializer] Remapped missing mesh GUID to {fallbackMesh.Guid} from {meshFiles[0]}");
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine($"[SceneSerializer] Fallback mesh mapping failed: {ex.Message}");
                                            }
                                        }
                                    }
                                }

                                if (meshRenderer.MaterialGuid.HasValue && !Engine.Assets.AssetDatabase.TryGet(meshRenderer.MaterialGuid.Value, out _))
                                {
                                    // First, check the model's Materials subfolder
                                    var modelMatFolder = System.IO.Path.Combine(modelFolder, "Materials");
                                    if (System.IO.Directory.Exists(modelMatFolder))
                                    {
                                        var mats = System.IO.Directory.GetFiles(modelMatFolder, "*" + Engine.Assets.AssetDatabase.MaterialExt, System.IO.SearchOption.TopDirectoryOnly);
                                        if (mats.Length > 0)
                                        {
                                            try
                                            {
                                                var fallbackMat = Engine.Assets.MaterialAsset.Load(mats[0]);
                                                meshRenderer.MaterialGuid = fallbackMat.Guid;
                                                // PERFORMANCE: Disabled logs
                                                // Console.WriteLine($"[SceneSerializer] Remapped missing material GUID to {fallbackMat.Guid} from {mats[0]}");
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine($"[SceneSerializer] Fallback material mapping failed: {ex.Message}");
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[SceneSerializer] Fallback mapping encountered an error: {ex.Message}");
                        }
                    }
                }
                catch { }
            }

            // Load CameraComponent if present
            if (components.TryGetValue("CameraComponent", out var cameraElement))
            {
                try
                {
                    var cameraData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(cameraElement);
                    if (cameraData != null)
                    {
                        var camera = entity.AddComponent<CameraComponent>();
                        
                        // Utiliser le système automatique de désérialisation pour tous les champs [Serializable]
                        ComponentSerializer.Deserialize(camera, cameraData);
                        // Puis résoudre les références après chargement complet
                        // (sera fait dans ResolveAllDeferredReferences)
                    }
                }
                catch { }
            }

            // Load Light component using automatic serialization
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

            // Load CharacterController component if present
            if (components.TryGetValue("CharacterController", out var ccElement))
            {
                try
                {
                    var ccData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(ccElement);
                    if (ccData != null)
                    {
                        var characterController = entity.AddComponent<CharacterController>();
                        
                        if (ccData.TryGetValue("height", out var heightEl))
                            characterController.Height = heightEl.GetSingle();
                            
                        if (ccData.TryGetValue("radius", out var radiusEl))
                            characterController.Radius = radiusEl.GetSingle();
                            
                        if (ccData.TryGetValue("stepOffset", out var stepEl))
                            characterController.StepOffset = stepEl.GetSingle();
                            
                        // useGravity is always true in the new simple system
                        // Skip loading this property
                            
                        if (ccData.TryGetValue("gravity", out var gravEl))
                            characterController.Gravity = gravEl.GetSingle();
                            
                        // Note: IsGrounded is read-only, no need to deserialize
                    }
                }
                catch { }
            }

            // Load MonoBehaviour scripts if present
            foreach (var component in components)
            {
                if (component.Key.StartsWith("Script_"))
                {
                    try
                    {
                        var scriptData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(component.Value);
                        if (scriptData != null && scriptData.TryGetValue("typeName", out var typeNameEl))
                        {
                            var typeName = typeNameEl.GetString();
                            if (!string.IsNullOrEmpty(typeName))
                            {
                                LoadMonoBehaviourScript(scene, entity, typeName, scriptData);
                            }
                        }
                    }
                    catch { }
                }
            }
            
            // Load other components automatically (excluding already handled ones)
            var handledComponents = new HashSet<string>
            {
                "Transform", "MeshRenderer", "CameraComponent", "Light", "CharacterController"
            };
            
        foreach (var kvp in components)
            {
                if (handledComponents.Contains(kvp.Key) || kvp.Key.StartsWith("Script_"))
                    continue;
                    
                try
                {
            LoadComponentAutomatically(scene, entity, kvp.Key, kvp.Value);
                }
                catch
                {
                    // Ignorer les erreurs de chargement automatique
                }
            }
        }
        
        /// <summary>
        /// Charge automatiquement un component par réflexion
        /// </summary>
    private static void LoadComponentAutomatically(Scene scene, Entity entity, string componentTypeName, JsonElement componentElement)
        {
            // PERFORMANCE: Disabled logs - they slow down scene loading
            // Console.WriteLine($"[SceneSerializer] LoadComponentAutomatically: {componentTypeName} on entity {entity.Name}");

            // Chercher le type de component dans l'assembly
            var componentType = FindComponentType(componentTypeName);
            if (componentType == null)
            {
                // Console.WriteLine($"[SceneSerializer] Component type {componentTypeName} not found");
                return;
            }

            // Console.WriteLine($"[SceneSerializer] Found component type: {componentType.FullName}");

            // Vérifier si l'entité a déjà ce component
            if (entity.GetAllComponents().Any(c => c.GetType() == componentType))
            {
                // Console.WriteLine($"[SceneSerializer] Entity {entity.Name} already has component {componentTypeName}");
                return;
            }

            try
            {
                // Créer une instance du component
                var component = Activator.CreateInstance(componentType) as Component;
                if (component == null)
                {
                    // Console.WriteLine($"[SceneSerializer] Failed to create instance of {componentTypeName}");
                    return;
                }

                // Console.WriteLine($"[SceneSerializer] Created instance of {componentTypeName}");

                // Ajouter le component à l'entité en utilisant AddComponent<T>(T component)
                var genericAddMethod = typeof(Entity).GetMethods()
                    .FirstOrDefault(m => m.Name == "AddComponent" &&
                                       m.IsGenericMethod &&
                                       m.GetParameters().Length == 1 &&
                                       m.GetParameters()[0].ParameterType.IsGenericParameter);

                if (genericAddMethod != null)
                {
                    var typedMethod = genericAddMethod.MakeGenericMethod(componentType);
                    typedMethod.Invoke(entity, new[] { component });
                    // Console.WriteLine($"[SceneSerializer] Added {componentTypeName} to entity {entity.Name}");
                }

                // Désérialiser les données du component
                var componentData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(componentElement);
                if (componentData != null)
                {
                    ComponentSerializer.Deserialize(component, componentData);
                    // After basic value load, resolve entity/component references using current scene
                    ComponentSerializer.ResolveReferences(component, componentData, scene);
                    // Console.WriteLine($"[SceneSerializer] Deserialized data for {componentTypeName}");

                    // Special case: Terrain component needs to generate mesh after deserialization
                    // OnAttached() is called BEFORE deserialization, so it can't generate the mesh there
                    // We only call GenerateTerrain() if the mesh hasn't been generated yet
                    if (component is Engine.Components.Terrain terrain)
                    {
                        if (terrain.HeightmapTextureGuid.HasValue)
                        {
                            // PERFORMANCE: Disabled log
                            // Console.WriteLine($"[SceneSerializer] Calling GenerateTerrain() after deserialization (cache will be used if available)");
                            try
                            {
                                terrain.GenerateTerrain();
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[SceneSerializer] Failed to generate terrain: {ex.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SceneSerializer] Error loading {componentTypeName}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Trouve un type de component par son nom
        /// </summary>
        private static Type? FindComponentType(string typeName)
        {
            // Chercher dans l'assembly des components
            var engineAssembly = typeof(Component).Assembly;
            var componentType = engineAssembly.GetTypes()
                                             .FirstOrDefault(t => t.Name == typeName && t.IsSubclassOf(typeof(Component)));
            
            if (componentType != null)
                return componentType;
            
            // Chercher dans d'autres assemblies si nécessaire
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    componentType = assembly.GetTypes()
                                           .FirstOrDefault(t => t.Name == typeName && t.IsSubclassOf(typeof(Component)));
                    if (componentType != null)
                        return componentType;
                }
                catch
                {
                    // Ignorer les erreurs d'assemblage
                }
            }
            
            return null;
        }

        // Utility methods
        private static string CalculateChecksum(string content)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(content);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        private static void CreateBackupFile(string filePath)
        {
            try
            {
                var backupPath = $"{filePath}{BACKUP_SUFFIX}";
                
                // Simple single backup - just rename existing backup if it exists
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }
                
                File.Copy(filePath, backupPath);
                
                // Hide backup file from Windows Explorer
                var backupInfo = new FileInfo(backupPath);
                backupInfo.Attributes = FileAttributes.Hidden;
            }
            catch { }
        }


        private static bool VerifyWrittenFile(string filePath, string expectedChecksum)
        {
            try
            {
                var content = File.ReadAllText(filePath);
                var checksum = CalculateChecksum(content);
                return checksum == expectedChecksum;
            }
            catch
            {
                return false;
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

                // Legacy detection
                return doc.RootElement.TryGetProperty("entities", out _) ? 2 : 1;
            }
            catch
            {
                return 1; // Assume oldest format on parse error
            }
        }

        // Compression support
        private static void WriteCompressedFile(string filePath, SceneFileV4 sceneFile)
        {
            var json = JsonSerializer.Serialize(sceneFile, _jsonOptions);
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);

            using var fileStream = File.Create(filePath);
            using var gzip = new System.IO.Compression.GZipStream(fileStream, System.IO.Compression.CompressionMode.Compress);
            gzip.Write(bytes);
        }

        private static SceneFileV4? ReadCompressedFile(string filePath)
        {
            using var fileStream = File.OpenRead(filePath);
            using var gzip = new System.IO.Compression.GZipStream(fileStream, System.IO.Compression.CompressionMode.Decompress);
            using var reader = new StreamReader(gzip);
            var json = reader.ReadToEnd();

            return JsonSerializer.Deserialize<SceneFileV4>(json, _jsonOptions);
        }

        private static bool IsCompressedFile(string filePath)
        {
            try
            {
                using var fs = File.OpenRead(filePath);
                var buffer = new byte[3];
                fs.Read(buffer, 0, 3);
                return buffer[0] == 0x1F && buffer[1] == 0x8B && buffer[2] == 0x08; // GZIP magic
            }
            catch
            {
                return false;
            }
        }

        // Legacy conversion methods (simplified)
        private static SceneFileV4 ConvertFromV3(string json)
        {
            // V3 to V4: Convert old Render components to new MeshRenderer components
            var sceneV3 = JsonSerializer.Deserialize<SceneFileV4>(json, _jsonOptions);
            if (sceneV3 != null)
            {
                // V3 and V4 have same structure, just different component handling
                // The component loading logic already handles backward compatibility
                return sceneV3;
            }
            return new SceneFileV4();
        }
        
        private static SceneFileV4 ConvertFromV2(string json) => throw new NotImplementedException("V2 conversion");
        private static SceneFileV4 ConvertFromV1(string json) => throw new NotImplementedException("V1 conversion");

        private static bool TryLoadFromBackup(Scene scene, string filePath, out LoadResult result)
        {
            result = new LoadResult { Success = false };
            // Implementation for backup loading
            return false;
        }

        /// <summary>
        /// Résout toutes les références différées dans les composants après le chargement complet de la scène
        /// </summary>
        private static void ResolveAllDeferredReferences(Scene scene, SceneFileV4 sceneFile, LoadResult result)
        {
            var guidToEntity = scene.Entities.ToDictionary(e => e.Guid);

            foreach (var entityData in sceneFile.Entities)
            {
                if (!guidToEntity.TryGetValue(entityData.Guid, out var entity))
                {
                    continue;
                }


                foreach (var componentKvp in entityData.Components)
                {
                    var componentName = componentKvp.Key;
                    var componentElement = componentKvp.Value;

                    // Skip components that are loaded manually (they don't use deferred references)
                    // MeshRenderer and Light now use automatic serialization, so they need reference resolution
                    if (componentName == "Transform" || componentName == "CharacterController" || componentName.StartsWith("Script_"))
                        continue;

                    // CameraComponent now uses automatic serialization, so it needs reference resolution
                    if (componentName == "CameraComponent")
                    {
                        try
                        {
                            var componentData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(componentElement);
                            if (componentData != null)
                            {
                                var camera = entity.GetComponent<CameraComponent>();
                                if (camera != null)
                                {
                                    ComponentSerializer.ResolveReferences(camera, componentData, scene);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            result.Warnings.Add($"Failed to resolve references for CameraComponent on {entity.Name}: {e.Message}");
                        }
                        continue;
                    }

                    // For other components loaded via LoadComponentAutomatically, resolve references
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

                // Also resolve references in scripts that might have failed during initial load
                var scriptComponents = entity.GetAllComponents().Where(c => c is Engine.Scripting.MonoBehaviour).ToList();
                
                foreach (var component in scriptComponents)
                {
                    if (component is Engine.Scripting.MonoBehaviour script)
                    {
                        // Get the original script data from sceneFile
                        var scriptKey = $"Script_{script.GetType().Name}";  // Use Name, not FullName
                        
                        if (entityData.Components.TryGetValue(scriptKey, out var scriptElement))
                        {
                            try
                            {
                                var scriptData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(scriptElement);
                                if (scriptData != null && scriptData.TryGetValue("fields", out var fieldsElement))
                                {
                                    var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(fieldsElement);
                                    if (fields != null)
                                    {
                                        ResolveScriptReferences(script, fields, scene);
                                    }
                                    else
                                    {
                                    }
                                }
                                else
                                {
                                }
                            }
                            catch (Exception e)
                            {
                                result.Warnings.Add($"Failed to resolve script references for {script.GetType().Name} on {entity.Name}: {e.Message}");
                            }
                        }
                        else
                        {
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Résout les références dans un script MonoBehaviour
        /// </summary>
        private static void ResolveScriptReferences(Engine.Scripting.MonoBehaviour script, Dictionary<string, JsonElement> fields, Engine.Scene.Scene scene)
        {
            var type = script.GetType();
            
            foreach (var field in type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                // Only process fields marked with [Editable]
                var editableAttr = field.GetCustomAttributes(typeof(Engine.Inspector.EditableAttribute), true).FirstOrDefault();
                if (editableAttr == null) continue;
                
                if (fields.TryGetValue(field.Name, out var valueElement))
                {
                    
                    try
                    {
                        // Skip null values
                        if (valueElement.ValueKind == JsonValueKind.Null)
                        {
                            field.SetValue(script, null);
                            continue;
                        }
                        
                        if (field.FieldType == typeof(Engine.Scene.Entity))
                        {
                            if (valueElement.ValueKind == JsonValueKind.Object && valueElement.TryGetProperty("entityGuid", out var guidEl))
                            {
                                if (Guid.TryParse(guidEl.GetString(), out var guid))
                                {
                                    var target = scene.Entities.FirstOrDefault(e => e.Guid == guid);
                                    field.SetValue(script, target);
                                }
                            }
                            else if (valueElement.ValueKind == JsonValueKind.String && Guid.TryParse(valueElement.GetString(), out var guid))
                            {
                                var target = scene.Entities.FirstOrDefault(e => e.Guid == guid);
                                field.SetValue(script, target);
                            }
                        }
                        else if (typeof(Engine.Components.Component).IsAssignableFrom(field.FieldType))
                        {
                            if (valueElement.ValueKind == JsonValueKind.Object &&
                                valueElement.TryGetProperty("entityGuid", out var guidEl) &&
                                valueElement.TryGetProperty("componentType", out var typeEl))
                            {
                                if (Guid.TryParse(guidEl.GetString(), out var guid))
                                {
                                    var targetEnt = scene.Entities.FirstOrDefault(e => e.Guid == guid);
                                    if (targetEnt != null)
                                    {
                                        var compTypeName = typeEl.GetString();
                                        var compType = AppDomain.CurrentDomain.GetAssemblies()
                                            .SelectMany(a => a.GetTypes())
                                            .FirstOrDefault(t => t.FullName == compTypeName || t.Name == compTypeName);
                                        if (compType != null)
                                        {
                                            var comp = targetEnt.GetComponent(compType) as Component;
                                            if (comp != null)
                                            {
                                                field.SetValue(script, comp);
                                            }
                                            else
                                            {
                                            }
                                        }
                                        else
                                        {
                                        }
                                    }
                                    else
                                    {
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
                else
                {
                }
            }
        }
    }

    // Result classes
    public class SaveResult
    {
        public bool Success { get; set; }
        public string FilePath { get; set; } = "";
        public string? ErrorMessage { get; set; }
        public int EntityCount { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public class LoadResult
    {
        public bool Success { get; set; }
        public string FilePath { get; set; } = "";
        public string? ErrorMessage { get; set; }
        public List<string> Warnings { get; set; } = new();
        public int DetectedVersion { get; set; }
        public int LoadedEntityCount { get; set; }
        public SceneSerializer.SceneMetadata? SceneMetadata { get; set; }
        public TimeSpan Duration { get; set; }
    }
}
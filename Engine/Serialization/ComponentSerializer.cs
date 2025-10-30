using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Engine.Components;
using OpenTK.Mathematics;

namespace Engine.Serialization
{
    /// <summary>
    /// Système de sérialisation automatique pour les components
    /// </summary>
    public static class ComponentSerializer
    {
        private static readonly Dictionary<Type, IComponentSerializer> _customSerializers = new();

        static ComponentSerializer()
        {
            // Enregistrer les sérialiseurs spécialisés si nécessaire
            RegisterCustomSerializers();
        }

        /// <summary>
        /// Sérialise automatiquement un component en utilisant la réflexion
        /// </summary>
        public static Dictionary<string, object> Serialize(Component component)
        {
            var type = component.GetType();

            // Utiliser un sérialiseur personnalisé s'il existe
            if (_customSerializers.TryGetValue(type, out var customSerializer))
            {
                return customSerializer.Serialize(component);
            }

            // Sérialisation automatique par réflexion
            return SerializeByReflection(component);
        }

        /// <summary>
        /// Désérialise automatiquement un component
        /// </summary>
        public static void Deserialize(Component component, Dictionary<string, JsonElement> data)
        {
            var type = component.GetType();

            // Utiliser un sérialiseur personnalisé s'il existe
            if (_customSerializers.TryGetValue(type, out var customSerializer))
            {
                customSerializer.Deserialize(component, data);
                return;
            }

            // Désérialisation automatique par réflexion
            DeserializeByReflection(component, data);
        }

        /// <summary>
        /// Sérialisation par réflexion basée sur l'attribut [Serializable]
        /// </summary>
        private static Dictionary<string, object> SerializeByReflection(Component component)
        {
            var result = new Dictionary<string, object>();
            var type = component.GetType();

            // Sérialiser les propriétés marquées avec [Serializable]
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = property.GetCustomAttribute<SerializableAttribute>();
                if (attr == null) continue;

                var value = property.GetValue(component);
                if (value == null) continue;

                var key = attr.Name ?? property.Name.ToLowerInvariant();
                result[key] = SerializeValue(value);
            }

            // Sérialiser les champs marqués avec [Serializable]
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = field.GetCustomAttribute<SerializableAttribute>();
                if (attr == null) continue;

                var value = field.GetValue(component);
                if (value == null) continue;

                var key = attr.Name ?? field.Name.ToLowerInvariant();
                result[key] = SerializeValue(value);
            }

            return result;
        }

        /// <summary>
        /// Désérialisation par réflexion
        /// </summary>
        private static void DeserializeByReflection(Component component, Dictionary<string, JsonElement> data)
        {
            var type = component.GetType();

            // Désérialiser les propriétés
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = property.GetCustomAttribute<SerializableAttribute>();
                if (attr == null || !property.CanWrite) continue;

                var key = attr.Name ?? property.Name.ToLowerInvariant();
                if (!data.TryGetValue(key, out var element)) continue;

                try
                {
                    if (property.PropertyType == typeof(Engine.Scene.Entity) || typeof(Component).IsAssignableFrom(property.PropertyType))
                    {
                        // Defer entity/component references to ResolveReferences
                        continue;
                    }
                    var value = DeserializeValue(element, property.PropertyType);
                    property.SetValue(component, value);
                }
                catch (Exception ex)
                {
                    try { Console.WriteLine($"[ComponentSerializer] Failed to set property '{property.Name}' on {type.Name}: {ex.Message}"); } catch { }
                }
            }

            // Désérialiser les champs
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = field.GetCustomAttribute<SerializableAttribute>();
                if (attr == null) continue;

                var key = attr.Name ?? field.Name.ToLowerInvariant();
                if (!data.TryGetValue(key, out var element)) continue;

                try
                {
                    if (field.FieldType == typeof(Engine.Scene.Entity) || typeof(Component).IsAssignableFrom(field.FieldType))
                    {
                        // Defer entity/component references to ResolveReferences
                        continue;
                    }
                    var value = DeserializeValue(element, field.FieldType);
                    field.SetValue(component, value);
                }
                catch (Exception ex)
                {
                    try { Console.WriteLine($"[ComponentSerializer] Failed to set field '{field.Name}' on {type.Name}: {ex.Message}"); } catch { }
                }
            }
        }

        /// <summary>
        /// Sérialise une valeur selon son type
        /// </summary>
        private static object SerializeValue(object value)
        {
            return value switch
            {
                Engine.Scene.Entity ent => new Dictionary<string, object>
                {
                    ["entityGuid"] = ent.Guid.ToString()
                },
                Engine.Components.Component comp => new Dictionary<string, object>
                {
                    ["entityGuid"] = comp.Entity?.Guid.ToString() ?? string.Empty,
                    ["componentType"] = comp.GetType().FullName ?? comp.GetType().Name
                },
                Vector3 v3 => new[] { v3.X, v3.Y, v3.Z },
                Vector2 v2 => new[] { v2.X, v2.Y },
                Vector4 v4 => new[] { v4.X, v4.Y, v4.Z, v4.W },
                Quaternion q => new[] { q.X, q.Y, q.Z, q.W },
                Matrix4 m => new[] {
                    m.M11, m.M12, m.M13, m.M14,
                    m.M21, m.M22, m.M23, m.M24,
                    m.M31, m.M32, m.M33, m.M34,
                    m.M41, m.M42, m.M43, m.M44
                },
                Enum e => e.ToString(),
                _ => value
            };
        }

        /// <summary>
        /// Désérialise une valeur selon son type
        /// </summary>
        private static object? DeserializeValue(JsonElement element, Type targetType)
        {
            // Entity/Component reference objects
            if (targetType == typeof(Engine.Scene.Entity))
            {
                if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("entityGuid", out var guidEl))
                {
                    // Resolution is deferred; return a lightweight handle (guid string)
                    return guidEl.GetString();
                }
            }
            if (typeof(Engine.Components.Component).IsAssignableFrom(targetType))
            {
                if (element.ValueKind == JsonValueKind.Object &&
                    element.TryGetProperty("entityGuid", out var guidEl) &&
                    element.TryGetProperty("componentType", out var typeEl))
                {
                    // Defer exact instance resolution; return a tuple as string pair
                    return $"{guidEl.GetString()}|{typeEl.GetString()}";
                }
            }
            if (targetType == typeof(Vector3))
            {
                var array = JsonSerializer.Deserialize<float[]>(element);
                return array?.Length >= 3 ? new Vector3(array[0], array[1], array[2]) : Vector3.Zero;
            }

            if (targetType == typeof(Vector2))
            {
                var array = JsonSerializer.Deserialize<float[]>(element);
                return array?.Length >= 2 ? new Vector2(array[0], array[1]) : Vector2.Zero;
            }

            if (targetType == typeof(Vector4))
            {
                var array = JsonSerializer.Deserialize<float[]>(element);
                return array?.Length >= 4 ? new Vector4(array[0], array[1], array[2], array[3]) : Vector4.Zero;
            }

            if (targetType == typeof(Quaternion))
            {
                var array = JsonSerializer.Deserialize<float[]>(element);
                return array?.Length >= 4 ? new Quaternion(array[0], array[1], array[2], array[3]) : Quaternion.Identity;
            }

            if (targetType == typeof(Matrix4))
            {
                var array = JsonSerializer.Deserialize<float[]>(element);
                if (array?.Length >= 16)
                {
                    return new Matrix4(
                        array[0], array[1], array[2], array[3],
                        array[4], array[5], array[6], array[7],
                        array[8], array[9], array[10], array[11],
                        array[12], array[13], array[14], array[15]
                    );
                }
                return Matrix4.Identity;
            }

            if (targetType.IsEnum)
            {
                var str = element.GetString();
                return str != null && Enum.TryParse(targetType, str, out var enumValue) ? enumValue : null;
            }

            if (targetType == typeof(float))
                return element.GetSingle();

            if (targetType == typeof(double))
                return element.GetDouble();

            if (targetType == typeof(int))
                return element.GetInt32();

            if (targetType == typeof(bool))
                return element.GetBoolean();

            if (targetType == typeof(string))
                return element.GetString();

            // Handle GUIDs explicitly (including nullable Guid)
            if (targetType == typeof(Guid) || targetType == typeof(Guid?))
            {
                try
                {
                    var s = element.GetString();
                    if (string.IsNullOrEmpty(s)) return targetType == typeof(Guid) ? Guid.Empty : (Guid?)null;
                    if (Guid.TryParse(s, out var g)) return (object)g;
                    return targetType == typeof(Guid) ? Guid.Empty : (Guid?)null;
                }
                catch { return targetType == typeof(Guid) ? Guid.Empty : (Guid?)null; }
            }

            // Pour les types simples, utiliser la sérialisation JSON standard
            return JsonSerializer.Deserialize(element, targetType);
        }

        /// <summary>
        /// Enregistre les sérialiseurs personnalisés pour certains types de components
        /// </summary>
        private static void RegisterCustomSerializers()
        {
            // Enregistrer le sérialiseur pour GlobalEffects
            RegisterCustomSerializer(new GlobalEffectsSerializer());
        }

        /// <summary>
        /// Enregistre un sérialiseur personnalisé pour un type de component
        /// </summary>
        public static void RegisterCustomSerializer<T>(IComponentSerializer<T> serializer) where T : Component
        {
            _customSerializers[typeof(T)] = serializer;
        }

        /// <summary>
        /// Resolve Entity/Component references for fields/properties marked [Serializable].
        /// Must be called with the same data used to deserialize, so we can read entityGuid/componentType.
        /// </summary>
        public static void ResolveReferences(Component component, Dictionary<string, JsonElement> data, Engine.Scene.Scene scene)
        {
            var type = component.GetType();

            void ResolveMember(Action<object?> setter, Type memberType, string key)
            {
                if (!data.TryGetValue(key, out var element)) return;

                try
                {
                    if (memberType == typeof(Engine.Scene.Entity))
                    {
                        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("entityGuid", out var guidEl))
                        {
                            if (Guid.TryParse(guidEl.GetString(), out var guid))
                            {
                                var target = scene.Entities.FirstOrDefault(e => e.Guid == guid);
                                setter(target);
                            }
                        }
                    }
                    else if (typeof(Component).IsAssignableFrom(memberType))
                    {
                        if (element.ValueKind == JsonValueKind.Object &&
                            element.TryGetProperty("entityGuid", out var guidEl) &&
                            element.TryGetProperty("componentType", out var typeEl))
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
                                            setter(comp);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception)
                {
                }
            }

            // Properties
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = property.GetCustomAttribute<SerializableAttribute>();
                if (attr == null || !property.CanWrite) continue;
                var key = attr.Name ?? property.Name.ToLowerInvariant();
                ResolveMember(v => property.SetValue(component, v), property.PropertyType, key);
            }

            // Fields
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = field.GetCustomAttribute<SerializableAttribute>();
                if (attr == null) continue;
                var key = attr.Name ?? field.Name.ToLowerInvariant();
                ResolveMember(v => field.SetValue(component, v), field.FieldType, key);
            }
        }
    }

    /// <summary>
    /// Interface pour les sérialiseurs de components personnalisés
    /// </summary>
    public interface IComponentSerializer
    {
        Dictionary<string, object> Serialize(Component component);
        void Deserialize(Component component, Dictionary<string, JsonElement> data);
    }

    /// <summary>
    /// Interface générique pour les sérialiseurs de components personnalisés
    /// </summary>
    public interface IComponentSerializer<in T> : IComponentSerializer where T : Component
    {
        Dictionary<string, object> Serialize(T component);
        void Deserialize(T component, Dictionary<string, JsonElement> data);

        // Implémentation par défaut pour l'interface non-générique
        Dictionary<string, object> IComponentSerializer.Serialize(Component component)
        {
            return Serialize((T)component);
        }

        void IComponentSerializer.Deserialize(Component component, Dictionary<string, JsonElement> data)
        {
            Deserialize((T)component, data);
        }
    }
}
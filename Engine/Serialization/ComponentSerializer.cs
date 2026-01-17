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
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            IncludeFields = true,
            AllowTrailingCommas = true
        };
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
                System.Numerics.Vector3 snv3 => new[] { snv3.X, snv3.Y, snv3.Z },
                Vector2 v2 => new[] { v2.X, v2.Y },
                System.Numerics.Vector2 snv2 => new[] { snv2.X, snv2.Y },
                Vector4 v4 => new[] { v4.X, v4.Y, v4.Z, v4.W },
                System.Numerics.Vector4 snv4 => new[] { snv4.X, snv4.Y, snv4.Z, snv4.W },
                Quaternion q => new[] { q.X, q.Y, q.Z, q.W },
                Color4 c => new[] { c.R, c.G, c.B, c.A },
                Engine.Components.MinMaxCurve curve => new Dictionary<string, object>
                {
                    ["constant"] = curve.Constant,
                    ["min"] = curve.Min,
                    ["max"] = curve.Max,
                    ["mode"] = curve.Mode.ToString()
                },
                Engine.Components.AnimationCurve animCurve => new Dictionary<string, object>
                {
                    ["keys"] = animCurve.Keys
                },
                Engine.Components.ColorGradient gradient => new Dictionary<string, object>
                {
                    ["colors"] = gradient.Colors
                },
                Engine.Components.ColorKey colorKey => new Dictionary<string, object>
                {
                    ["time"] = colorKey.Time,
                    ["color"] = new[] { colorKey.Color.R, colorKey.Color.G, colorKey.Color.B, colorKey.Color.A }
                },
                Engine.Components.Keyframe keyframe => new Dictionary<string, object>
                {
                    ["time"] = keyframe.Time,
                    ["value"] = keyframe.Value
                },
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
                var array = JsonSerializer.Deserialize<float[]>(element, _jsonOptions);
                return array?.Length >= 3 ? new Vector3(array[0], array[1], array[2]) : Vector3.Zero;
            }

            if (targetType == typeof(System.Numerics.Vector3))
            {
                var array = JsonSerializer.Deserialize<float[]>(element, _jsonOptions);
                return array?.Length >= 3 ? new System.Numerics.Vector3(array[0], array[1], array[2]) : System.Numerics.Vector3.Zero;
            }

            if (targetType == typeof(Vector2))
            {
                var array = JsonSerializer.Deserialize<float[]>(element, _jsonOptions);
                return array?.Length >= 2 ? new Vector2(array[0], array[1]) : Vector2.Zero;
            }

            if (targetType == typeof(System.Numerics.Vector2))
            {
                var array = JsonSerializer.Deserialize<float[]>(element, _jsonOptions);
                return array?.Length >= 2 ? new System.Numerics.Vector2(array[0], array[1]) : System.Numerics.Vector2.Zero;
            }

            if (targetType == typeof(Vector4))
            {
                var array = JsonSerializer.Deserialize<float[]>(element, _jsonOptions);
                return array?.Length >= 4 ? new Vector4(array[0], array[1], array[2], array[3]) : Vector4.Zero;
            }

            if (targetType == typeof(System.Numerics.Vector4))
            {
                var array = JsonSerializer.Deserialize<float[]>(element, _jsonOptions);
                return array?.Length >= 4 ? new System.Numerics.Vector4(array[0], array[1], array[2], array[3]) : System.Numerics.Vector4.Zero;
            }

            if (targetType == typeof(Quaternion))
            {
                var array = JsonSerializer.Deserialize<float[]>(element, _jsonOptions);
                return array?.Length >= 4 ? new Quaternion(array[0], array[1], array[2], array[3]) : Quaternion.Identity;
            }

            if (targetType == typeof(Color4))
            {
                var array = JsonSerializer.Deserialize<float[]>(element, _jsonOptions);
                return array?.Length >= 4 ? new Color4(array[0], array[1], array[2], array[3]) : Color4.White;
            }

            if (targetType == typeof(Engine.Components.MinMaxCurve))
            {
                if (element.ValueKind == JsonValueKind.Object)
                {
                    var curve = new Engine.Components.MinMaxCurve(0);
                    if (element.TryGetProperty("constant", out var constEl))
                        curve.Constant = constEl.GetSingle();
                    if (element.TryGetProperty("min", out var minEl))
                        curve.Min = minEl.GetSingle();
                    if (element.TryGetProperty("max", out var maxEl))
                        curve.Max = maxEl.GetSingle();
                    if (element.TryGetProperty("mode", out var modeEl))
                    {
                        var modeStr = modeEl.GetString();
                        if (Enum.TryParse<Engine.Components.CurveMode>(modeStr, out var mode))
                            curve.Mode = mode;
                    }
                    return curve;
                }
                return new Engine.Components.MinMaxCurve(0);
            }

            if (targetType == typeof(Engine.Components.AnimationCurve))
            {
                if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("keys", out var keysEl))
                {
                    var curve = new Engine.Components.AnimationCurve();
                    if (keysEl.ValueKind == JsonValueKind.Array)
                    {
                        curve.Keys = new List<Engine.Components.Keyframe>();
                        foreach (var keyEl in keysEl.EnumerateArray())
                        {
                            var key = (Engine.Components.Keyframe?)DeserializeValue(keyEl, typeof(Engine.Components.Keyframe));
                            if (key != null) curve.Keys.Add(key);
                        }
                    }
                    return curve;
                }
                return Engine.Components.AnimationCurve.Linear(0, 1, 1, 0);
            }

            if (targetType == typeof(Engine.Components.ColorGradient))
            {
                if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("colors", out var colorsEl))
                {
                    var gradient = new Engine.Components.ColorGradient();
                    if (colorsEl.ValueKind == JsonValueKind.Array)
                    {
                        gradient.Colors = new List<Engine.Components.ColorKey>();
                        foreach (var colorEl in colorsEl.EnumerateArray())
                        {
                            var colorKey = (Engine.Components.ColorKey?)DeserializeValue(colorEl, typeof(Engine.Components.ColorKey));
                            if (colorKey != null) gradient.Colors.Add(colorKey);
                        }
                    }
                    return gradient;
                }
                return new Engine.Components.ColorGradient();
            }

            if (targetType == typeof(Engine.Components.ColorKey))
            {
                if (element.ValueKind == JsonValueKind.Object)
                {
                    var colorKey = new Engine.Components.ColorKey();
                    if (element.TryGetProperty("time", out var timeEl))
                        colorKey.Time = timeEl.GetSingle();
                    if (element.TryGetProperty("color", out var colorEl))
                    {
                        var color = (Color4?)DeserializeValue(colorEl, typeof(Color4));
                        if (color.HasValue) colorKey.Color = color.Value;
                    }
                    return colorKey;
                }
                return new Engine.Components.ColorKey();
            }

            if (targetType == typeof(Engine.Components.Keyframe))
            {
                if (element.ValueKind == JsonValueKind.Object)
                {
                    var keyframe = new Engine.Components.Keyframe();
                    if (element.TryGetProperty("time", out var timeEl))
                        keyframe.Time = timeEl.GetSingle();
                    if (element.TryGetProperty("value", out var valueEl))
                        keyframe.Value = valueEl.GetSingle();
                    return keyframe;
                }
                return new Engine.Components.Keyframe();
            }

            if (targetType == typeof(Matrix4))
            {
                var array = JsonSerializer.Deserialize<float[]>(element, _jsonOptions);
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
            return JsonSerializer.Deserialize(element, targetType, _jsonOptions);
        }

        /// <summary>
        /// Enregistre les sérialiseurs personnalisés pour certains types de components
        /// </summary>
        private static void RegisterCustomSerializers()
        {
            // Enregistrer le sérialiseur pour GlobalEffects
            RegisterCustomSerializer(new GlobalEffectsSerializer());
            // Register AudioSource serializer to handle filter settings
            RegisterCustomSerializer(new AudioSourceSerializer());
        }
        /// Enregistre un sérialiseur personnalisé pour un type de component

        /// <summary>
        /// Custom serializer for AudioSource that explicitly serializes/deserializes filters
        /// so that the filter settings can be typed correctly.
        /// </summary>
        private class AudioSourceSerializer : IComponentSerializer<Engine.Audio.Components.AudioSource>
        {
            public Dictionary<string, object> Serialize(Engine.Audio.Components.AudioSource audio)
            {
                var data = SerializeByReflection(audio);

                if (audio.Filters != null && audio.Filters.Count > 0)
                {
                    var list = new List<object>();
                    foreach (var f in audio.Filters)
                    {
                        var item = new Dictionary<string, object>
                        {
                            ["type"] = f.Type.ToString(),
                            ["enabled"] = f.Enabled
                        };
                        if (f.Settings != null)
                        {
                            item["settings"] = f.Settings; // only add settings when non-null
                        }
                        list.Add(item);
                    }
                    data["filters"] = list;
                }

                return data;
            }

            public void Deserialize(Engine.Audio.Components.AudioSource audio, Dictionary<string, JsonElement> data)
            {

                // Use default reflection-based deserialization first
                DeserializeByReflection(audio, data);

                // Now handle filters specialized type
                if (data.TryGetValue("filters", out var filtersEl) && filtersEl.ValueKind != JsonValueKind.Null)
                {
                    try
                    {
                        if (filtersEl.ValueKind == JsonValueKind.Array)
                        {
                            var list = new List<Engine.Audio.Components.AudioSourceFilter>();
                            foreach (var el in filtersEl.EnumerateArray())
                            {
                                try
                                {
                                    if (el.ValueKind != JsonValueKind.Object) continue;
                                    if (!el.TryGetProperty("type", out var typeEl)) continue;
                                    var typeStr = typeEl.GetString() ?? "None";
                                    if (!Enum.TryParse<Engine.Audio.Components.AudioSourceFilterType>(typeStr, out var ftype)) ftype = Engine.Audio.Components.AudioSourceFilterType.None;
                                    var f = new Engine.Audio.Components.AudioSourceFilter(ftype);
                                    if (el.TryGetProperty("enabled", out var enabledEl))
                                    {
                                        f.Enabled = enabledEl.GetBoolean();
                                    }
                                    if (el.TryGetProperty("settings", out var settingsEl) && settingsEl.ValueKind != JsonValueKind.Null)
                                    {
                                        switch (f.Type)
                                        {
                                            case Engine.Audio.Components.AudioSourceFilterType.LowPass:
                                                f.Settings = JsonSerializer.Deserialize<Engine.Audio.Effects.LowPassSettings>(settingsEl.GetRawText(), _jsonOptions);
                                                break;
                                            case Engine.Audio.Components.AudioSourceFilterType.HighPass:
                                                f.Settings = JsonSerializer.Deserialize<Engine.Audio.Effects.HighPassSettings>(settingsEl.GetRawText(), _jsonOptions);
                                                break;
                                            case Engine.Audio.Components.AudioSourceFilterType.BandPass:
                                                f.Settings = JsonSerializer.Deserialize<Engine.Audio.Effects.BandPassSettings>(settingsEl.GetRawText(), _jsonOptions);
                                                break;
                                            default:
                                                f.Settings = null;
                                                break;
                                        }
                                    }

                                    // Ensure EFX handle will be created later by EnsureFilterHandle after ResolveReferences
                                    list.Add(f);
                                }
                                catch { }
                            }

                            audio.Filters = list;
                        }
                    }
                    catch { }
                }
            }
        }
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

            // If the component defines an optional post-deserialize hook, invoke it so components
            // can repair or initialize derived/default values after deserialization.
            try
            {
                var hook = type.GetMethod("OnAfterDeserialize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                if (hook != null)
                {
                    hook.Invoke(component, null);
                }
            }
            catch { }
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
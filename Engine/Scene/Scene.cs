using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Engine.Scene;
using OpenTK.Mathematics;
using Engine.Assets;
using Engine.Components;
using Engine.Inspector;

namespace Engine.Scene
{
    public enum MeshKind { None, Cube, Sphere, Capsule, Plane, Quad }

    public sealed class Transform
    {
        private Vector3 _position = Vector3.Zero;
        private Quaternion _rotation = Quaternion.Identity;
        private Vector3 _scale = Vector3.One;
        private Entity? _owner;
        
        internal void SetOwner(Entity owner) => _owner = owner;
        
        [Editable("Position")]
        public Vector3 Position 
        { 
            get => _position; 
            set 
            { 
                if (_position != value) 
                { 
                    _position = value; 
                    _owner?.NotifyTransformChanged(); 
                } 
            } 
        }
        
        [Editable("Rotation")]
        public Quaternion Rotation 
        { 
            get => _rotation; 
            set 
            { 
                if (_rotation != value) 
                { 
                    _rotation = value; 
                    _owner?.NotifyTransformChanged(); 
                } 
            } 
        }
        
        [Editable("Scale")]
        public Vector3 Scale 
        { 
            get => _scale; 
            set 
            { 
                if (_scale != value) 
                { 
                    _scale = value; 
                    _owner?.NotifyTransformChanged(); 
                } 
            } 
        }
    }

    // Entity : remplace ta classe Entity par ceci
    public sealed class Entity
    {
        public uint Id;
        public string Name = "Entity";
        public Guid Guid = Guid.NewGuid();
        public Guid? MaterialGuid = null;

        /// <summary>Local transform (relative to Parent).</summary>
        public Transform Transform = new Transform();

        // --- Parenting ---
        public Entity? Parent { get; private set; }
        public System.Collections.Generic.List<Entity> Children { get; } = new();
        
        // --- Components ---
        private readonly Dictionary<Type, Component> _components = new();

        // Nouveau flag d'activation de l'entité (active en Edition/Play)
        private bool _active = true;
        public bool Active 
        { 
            get => _active;
            set
            {
                if (_active == value) return;
                _active = value;
                // Activer/désactiver tous les composants en conséquence
                foreach (var comp in _components.Values)
                {
                    if (comp.Enabled)
                    {
                        if (_active) comp.OnEnable();
                        else comp.OnDisable();
                    }
                }
            }
        }
        
        public Entity()
        {
            Transform.SetOwner(this);
            // Add mandatory TransformComponent
            var transformComp = AddComponent<TransformComponent>();
            // Synchroniser TransformComponent avec Entity.Transform initial
            SyncTransformComponent();
        }

        public void GetModelAndNormalMatrix(out Matrix4 model, out Matrix3 normal)
        {
            GetWorldTRS(out var position, out var rotation, out var scale);
            model = Matrix4.CreateScale(scale)
                * Matrix4.CreateFromQuaternion(rotation)
                * Matrix4.CreateTranslation(position);
            normal = new Matrix3(model);
            normal.Invert();
            normal.Transpose();
        }
        
        internal void NotifyTransformChanged()
        {
            // Synchroniser TransformComponent avec Entity.Transform
            SyncTransformComponent();
            Scene.NotifyEntityTransformChanged(this);
        }
        
        /// <summary>
        /// Synchronise TransformComponent avec Entity.Transform
        /// </summary>
        private void SyncTransformComponent()
        {
            var transformComp = GetComponent<TransformComponent>();
            if (transformComp != null)
            {
                
                transformComp.Position = Transform.Position;
                transformComp.Rotation = Transform.Rotation;
                transformComp.Scale = Transform.Scale;
            }
        }
        
        public Component AddComponent(Component component)
        {
            var type = component.GetType(); // <-- clé correcte
            if (_components.ContainsKey(type))
                throw new InvalidOperationException($"Entity already has component of type {type.Name}");

            component.Entity = this;
            _components[type] = component;
            component.OnAttached();
            return component;
        }

        // --- Component Management ---
        public T AddComponent<T>() where T : Component, new()
        {
            var c = new T();
            AddComponent((Component)c);
            return c;
        }
        
        public T AddComponent<T>(T component) where T : Component
        {
            AddComponent((Component)component);
            return component;
        }
        
        public T? GetComponent<T>() where T : Component
        {
            _components.TryGetValue(typeof(T), out var component);
            return component as T;
        }

        public Component? GetComponent(Type type)
        {
            _components.TryGetValue(type, out var component);
            return component;
        }
        
        public bool HasComponent<T>() where T : Component
        {
            return _components.ContainsKey(typeof(T));
        }
        
        public bool HasComponent(Type componentType)
        {
            return _components.ContainsKey(componentType);
        }
        
        public bool RemoveComponent<T>() where T : Component
        {
            var type = typeof(T);
            if (_components.TryGetValue(type, out var component))
            {
                component.OnDestroy(); // Call OnDestroy before removal
                component.OnDetached();
                component.Entity = null;
                _components.Remove(type);
                return true;
            }
            return false;
        }
        
        public IEnumerable<Component> GetAllComponents()
        {
            return _components.Values;
        }

        // ===== Monde/Local =====
        public void GetWorldTRS(out Vector3 pos, out Quaternion rot, out Vector3 scl)
        {
            if (Parent == null)
            {
                pos = Transform.Position;
                rot = Transform.Rotation;
                scl = Transform.Scale;
                return;
            }

            Parent.GetWorldTRS(out var pp, out var pr, out var ps);

            scl = new Vector3(ps.X * Transform.Scale.X,
                              ps.Y * Transform.Scale.Y,
                              ps.Z * Transform.Scale.Z);

            rot = Quaternion.Normalize(pr * Transform.Rotation);

            var lpScaled = new Vector3(Transform.Position.X * ps.X,
                                       Transform.Position.Y * ps.Y,
                                       Transform.Position.Z * ps.Z);
            var lpRot = Vector3.Transform(lpScaled, pr);
            pos = pp + lpRot;
        }

        [JsonIgnore]
        public Matrix4 LocalMatrix =>
            TRS(Transform.Position, Transform.Rotation, Transform.Scale);

        [JsonIgnore]
        public Matrix4 WorldMatrix =>
             // column-vectors: world = Parent * Local (standard convention)
            Parent == null ? LocalMatrix : Parent.WorldMatrix * LocalMatrix;

        static Matrix4 TRS(Vector3 p, Quaternion r, Vector3 s)
            => Matrix4.CreateScale(s)
             * Matrix4.CreateFromQuaternion(r)
             * Matrix4.CreateTranslation(p);

        /// <summary>Définit le parent; si keepWorld, ajuste le local pour conserver la pose monde.</summary>
        public void SetParent(Entity? newParent, bool keepWorld = true)
        {
            if (newParent == this) return;
            if (newParent != null && newParent.IsDescendantOf(this)) return;

            // snapshot monde
            GetWorldTRS(out var wpos, out var wrot, out var wscl);

            // détacher/attacher
            if (Parent != null) Parent.Children.Remove(this);
            Parent = newParent;
            if (Parent != null) Parent.Children.Add(this);

            if (!keepWorld) return;

            // recalc local depuis la pose monde
            SetWorldTRS(wpos, wrot, wscl);
        }

        /// <summary>Impose directement une pose monde (converti en local si parent).</summary>
        public void SetWorldTRS(Vector3 wpos, Quaternion wrot, Vector3 wscl)
        {
            if (Parent == null)
            {
                Transform.Position = wpos;
                Transform.Rotation = wrot;
                Transform.Scale = wscl;
                // Notify transform changed after all properties are set
                NotifyTransformChanged();
                return;
            }

            Parent.GetWorldTRS(out var pp, out var pr, out var ps);
            var invPr = Quaternion.Invert(pr);

            Transform.Rotation = Quaternion.Normalize(invPr * wrot);

            Transform.Scale = new Vector3(
                SafeDiv(wscl.X, ps.X),
                SafeDiv(wscl.Y, ps.Y),
                SafeDiv(wscl.Z, ps.Z));

            var delta = wpos - pp;
            var unrot = Vector3.Transform(delta, invPr);
            Transform.Position = new Vector3(
                SafeDiv(unrot.X, ps.X),
                SafeDiv(unrot.Y, ps.Y),
                SafeDiv(unrot.Z, ps.Z));
                
            // Notify transform changed after all properties are set
            NotifyTransformChanged();
        }

        public bool IsDescendantOf(Entity possibleAncestor)
        {
            for (var p = Parent; p != null; p = p.Parent)
                if (p == possibleAncestor) return true;
            return false;
        }

        static float SafeDiv(float a, float b) => MathF.Abs(b) < 1e-8f ? 0f : a / b;

        /// <summary>
        /// Définit position, rotation et scale local en une seule fois sans notifications intermédiaires.
        /// Plus robuste pour éviter la ré-entrée pendant le clonage.
        /// </summary>
        public void SetLocalTRS(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            Transform.Position = position;
            Transform.Rotation = rotation;
            Transform.Scale = scale;
            // Une seule notification à la fin
            NotifyTransformChanged();
        }

        /// <summary>Rayon de bounding-sphere en monde (unit cube → √3/2 ≈ 0.866).</summary>
        public float BoundsRadius
        {
            get
            {
                GetWorldTRS(out _, out _, out var s);
                float m = MathF.Max(MathF.Max(MathF.Abs(s.X), MathF.Abs(s.Y)), MathF.Abs(s.Z));
                return 0.8660254f * m;
            }
        }
    }

    public sealed class Scene
    {
        private uint _nextId = EntityIdRange.MinEntityId + 1;
        public readonly List<Entity> Entities = new List<Entity>();

        // Event for transform changes
        public static event Action<Entity>? EntityTransformChanged;

        public static void NotifyEntityTransformChanged(Entity entity)
        {
            EntityTransformChanged?.Invoke(entity);
        }

        public Entity CreateCube(string name, Vector3 pos, Vector3 scale, Vector4 color)
        {
            var e = new Entity { Id = _nextId++, Name = name };

            e.Transform.Position = pos;
            e.Transform.Scale = scale;

            // Add MeshRenderer component with cube mesh
            var meshRenderer = e.AddComponent<MeshRendererComponent>();
            meshRenderer.Mesh = MeshKind.Cube;

            // Material par défaut (blanc) – un GUID unique stocké dans l'AssetDatabase
            var defaultMaterial = AssetDatabase.EnsureDefaultWhiteMaterial();
            meshRenderer.SetMaterial(defaultMaterial);

            Entities.Add(e);
            return e;
        }

        public Entity CreateSphere(string name, Vector3 pos, float radius, Vector4 color)
        {
            var e = new Entity { Id = _nextId++, Name = name };
            e.Transform.Position = pos;
            e.Transform.Scale = new Vector3(radius * 2f);

            var meshRenderer = e.AddComponent<MeshRendererComponent>();
            meshRenderer.Mesh = MeshKind.Sphere;
            var defaultMaterial = AssetDatabase.EnsureDefaultWhiteMaterial();
            meshRenderer.SetMaterial(defaultMaterial);

            e.AddComponent<Engine.Components.SphereCollider>().Radius = radius;

            Entities.Add(e);
            return e;
        }

        public Entity CreateCapsule(string name, Vector3 pos, float height, float radius, Vector4 color)
        {
            var e = new Entity { Id = _nextId++, Name = name };
            e.Transform.Position = pos;
            e.Transform.Scale = new Vector3(radius * 2f, height, radius * 2f);

            var meshRenderer = e.AddComponent<MeshRendererComponent>();
            meshRenderer.Mesh = MeshKind.Capsule;
            var defaultMaterial = AssetDatabase.EnsureDefaultWhiteMaterial();
            meshRenderer.SetMaterial(defaultMaterial);

            var cap = e.AddComponent<Engine.Components.CapsuleCollider>();
            cap.Height = height;
            cap.Radius = radius;
            cap.Direction = 1; // Y-up

            Entities.Add(e);
            return e;
        }

        public Entity CreatePlane(string name, Vector3 pos, Vector2 size, Vector4 color)
        {
            var e = new Entity { Id = _nextId++, Name = name };
            e.Transform.Position = pos;
            e.Transform.Scale = new Vector3(size.X, 1f, size.Y);

            var meshRenderer = e.AddComponent<MeshRendererComponent>();
            meshRenderer.Mesh = MeshKind.Plane;
            var defaultMaterial = AssetDatabase.EnsureDefaultWhiteMaterial();
            meshRenderer.SetMaterial(defaultMaterial);

            // Use BoxCollider to approximate plane area (thin box)
            var box = e.AddComponent<Engine.Components.BoxCollider>();
            box.Size = new Vector3(1f, 0.01f, 1f);

            Entities.Add(e);
            return e;
        }

        public Entity CreateQuad(string name, Vector3 pos, Vector2 size, Vector4 color)
        {
            var e = new Entity { Id = _nextId++, Name = name };
            e.Transform.Position = pos;
            e.Transform.Scale = new Vector3(size.X, size.Y, 1f);

            var meshRenderer = e.AddComponent<MeshRendererComponent>();
            meshRenderer.Mesh = MeshKind.Quad;
            var defaultMaterial = AssetDatabase.EnsureDefaultWhiteMaterial();
            meshRenderer.SetMaterial(defaultMaterial);

            // Thin box collider in XY plane (Z=1 depth)
            var box = e.AddComponent<Engine.Components.BoxCollider>();
            box.Size = new Vector3(1f, 1f, 0.01f);

            Entities.Add(e);
            return e;
        }

        public Entity CreateDirectionalLight(string name, Vector3 direction, Vector3 color, float intensity = 1.0f)
        {
            var e = new Entity { Id = _nextId++, Name = name };

            // Orient the light according to direction
            if (direction != Vector3.Zero)
            {
                direction = direction.Normalized();
                var forward = Vector3.UnitZ;
                var rotAxis = Vector3.Cross(forward, direction);
                var rotAngle = MathF.Acos(Math.Clamp(Vector3.Dot(forward, direction), -1f, 1f));
                if (rotAxis.LengthSquared > 1e-6f) rotAxis.Normalize();
                e.Transform.Rotation = Quaternion.FromAxisAngle(rotAxis, rotAngle);
            }

            var light = e.AddComponent<LightComponent>();
            light.Type = LightType.Directional;
            light.Color = color;
            light.Intensity = intensity;

            Entities.Add(e);
            return e;
        }

        public Entity CreatePointLight(string name, Vector3 position, Vector3 color, float intensity = 1.0f, float range = 10.0f)
        {
            var e = new Entity { Id = _nextId++, Name = name };
            e.Transform.Position = position;

            var light = e.AddComponent<LightComponent>();
            light.Type = LightType.Point;
            light.Color = color;
            light.Intensity = intensity;
            light.Range = range;

            Entities.Add(e);
            return e;
        }

        public Entity CreateSpotLight(string name, Vector3 position, Vector3 direction, Vector3 color, float intensity = 1.0f, float range = 10.0f, float spotAngle = 30.0f)
        {
            var e = new Entity { Id = _nextId++, Name = name };
            e.Transform.Position = position;

            // Orient the light according to direction
            if (direction != Vector3.Zero)
            {
                direction = direction.Normalized();
                var forward = Vector3.UnitZ;
                var rotAxis = Vector3.Cross(forward, direction);
                var rotAngle = MathF.Acos(Math.Clamp(Vector3.Dot(forward, direction), -1f, 1f));
                if (rotAxis.LengthSquared > 1e-6f) rotAxis.Normalize();
                e.Transform.Rotation = Quaternion.FromAxisAngle(rotAxis, rotAngle);
            }

            var light = e.AddComponent<LightComponent>();
            light.Type = LightType.Spot;
            light.Color = color;
            light.Intensity = intensity;
            light.Range = range;
            light.SpotAngle = spotAngle;

            Entities.Add(e);
            return e;
        }

        public Entity CreateWater(string name, Vector3 position, float width = 100f, float length = 100f, int resolution = 32)
        {
            var e = new Entity { Id = _nextId++, Name = name };
            e.Transform.Position = position;

            // Add WaterComponent
            var waterComponent = e.AddComponent<WaterComponent>();
            waterComponent.WaterWidth = width;
            waterComponent.WaterLength = length;
            waterComponent.Resolution = resolution;

            // Water material will be assigned automatically in WaterComponent.OnAttached()

            Entities.Add(e);
            return e;
        }

        public Entity? GetById(uint id) => Entities.FirstOrDefault(x => x.Id == id);

        // Méthode pour obtenir le prochain ID disponible (utilisé par le sérialiseur)
        public uint GetNextEntityId() 
        {
            if (_nextId >= EntityIdRange.MaxEntityId)
            {
                throw new InvalidOperationException(
                    $"Impossible de créer plus d'entités. Limite atteinte: {EntityIdRange.MaxSupportedEntities}");
            }
            return _nextId++;
        }

        public int CountEntitiesUsingMaterial(Guid materialGuid)
        {
            return Entities.Count(e => e.MaterialGuid == materialGuid);
        }
        
        public CameraComponent? GetMainCamera() {
            CameraComponent? firstCam = null;
            int totalCameras = 0;
            int activeCameras = 0;
            int mainCameras = 0;
            
            foreach (var e in Entities) {
                var cam = e.GetComponent<CameraComponent>();
                if (cam == null) continue;
                totalCameras++;
                
                // Ne considérer que les caméras actives (entité active ET composant activé)
                if (!e.Active || !cam.Enabled) {
                    continue;
                }
                activeCameras++;
                
                if (cam.IsMain) {
                    mainCameras++;
                    return cam;
                }
                firstCam ??= cam;
            }
            
            return firstCam;
        }

        /// <summary>
        /// Clone the entire scene for Play Mode simulation
        /// </summary>
        public Scene Clone()
        {
            return Clone(null);
        }

        /// <summary>
        /// Clone the entire scene for Play Mode simulation with optional ScriptHost
        /// </summary>
        public Scene Clone(object? scriptHost)
        {
            var clonedScene = new Scene();
            var entityMap = new Dictionary<uint, Entity>();

            // --- Première passe : clonage de toutes les entités ---
            foreach (var originalEntity in Entities)
            {
                var clonedEntity = CloneEntity(originalEntity, clonedScene, scriptHost);
                entityMap[originalEntity.Id] = clonedEntity;
            }

            // --- Seconde passe : reconstruction des relations parent-enfant ---
            for (int i = 0; i < Entities.Count; i++)
            {
                var originalEntity = Entities[i];
                var clonedEntity = clonedScene.Entities[i];
                if (originalEntity.Parent != null &&
                    entityMap.TryGetValue(originalEntity.Parent.Id, out var clonedParent))
                {
                    clonedEntity.SetParent(clonedParent, false);
                }
            }

            // --- Force world transform recalculation after hierarchy reconstruction ---
            foreach (var clonedEntity in clonedScene.Entities)
            {
                clonedEntity.NotifyTransformChanged();
            }

            // --- Troisième passe : corriger les références de composants internes sur la scène clonée ---
            // This pass fixes both field and property references that point to Entity or Component instances
            foreach (var kv in entityMap)
            {
                var clonedEntity = kv.Value;
                var components = clonedEntity.GetAllComponents().ToList(); // Take snapshot to avoid modification issues

                foreach (var comp in components)
                {
                    var compType = comp.GetType();

                    // ----- Fix fields referencing Components or Entities -----
                    foreach (var field in compType.GetFields(System.Reflection.BindingFlags.Public
                                                             | System.Reflection.BindingFlags.NonPublic
                                                             | System.Reflection.BindingFlags.Instance))
                    {
                        if (field.IsLiteral || field.IsInitOnly) continue;
                        if (field.Name == "Entity" || field.Name == "_entity") continue;

                        var fieldType = field.FieldType;

                        // Remap Component fields
                        if (typeof(Engine.Components.Component).IsAssignableFrom(fieldType))
                        {
                            var fieldValue = field.GetValue(comp);
                            if (fieldValue is Engine.Components.Component originalComp && originalComp.Entity != null)
                            {
                                if (entityMap.TryGetValue(originalComp.Entity.Id, out var targetEntity))
                                {
                                    var targetComp = targetEntity.GetComponent(originalComp.GetType());
                                    if (targetComp != null)
                                    {
                                        field.SetValue(comp, targetComp);
                                    }
                                }
                            }
                        }

                        // Remap Entity fields
                        else if (typeof(Entity).IsAssignableFrom(fieldType))
                        {
                            var fieldValue = field.GetValue(comp);
                            if (fieldValue is Entity originalEnt)
                            {
                                if (entityMap.TryGetValue(originalEnt.Id, out var targetEntity))
                                {
                                    field.SetValue(comp, targetEntity);
                                }
                                else
                                {
                                    // If the referenced entity doesn't exist in the cloned scene, null it out
                                    field.SetValue(comp, null);
                                }
                            }
                        }
                    }

                    // ----- Fix properties referencing Components or Entities -----
                    foreach (var property in compType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                    {
                        if (!property.CanRead || !property.CanWrite) continue;
                        if (property.Name == "Entity") continue; // Skip entity backrefs

                        var propType = property.PropertyType;
                        try
                        {
                            var propValue = property.GetValue(comp);
                            if (propValue == null) continue;

                            // If the property points to an Entity, remap to cloned entity
                            if (propValue is Entity originalEntityVal)
                            {
                                if (entityMap.TryGetValue(originalEntityVal.Id, out var targetEntity))
                                {
                                    property.SetValue(comp, targetEntity);
                                }
                                else
                                {
                                    property.SetValue(comp, null);
                                }
                            }

                            // If the property points to a Component, remap to the corresponding component on the cloned entity
                            else if (propValue is Engine.Components.Component originalCompVal && originalCompVal.Entity != null)
                            {
                                if (entityMap.TryGetValue(originalCompVal.Entity.Id, out var targetEntity))
                                {
                                    var targetComp = targetEntity.GetComponent(originalCompVal.GetType());
                                    if (targetComp != null)
                                    {
                                        property.SetValue(comp, targetComp);
                                    }
                                    else
                                    {
                                        property.SetValue(comp, null);
                                    }
                                }
                                else
                                {
                                    property.SetValue(comp, null);
                                }
                            }
                        }
                        catch (Exception)
                        {
                            // Ignore properties that cannot be set or cause exceptions during remapping
                        }
                    }
                }
            }

            
            return clonedScene;
        }

        private static Entity CloneEntity(Entity original, Scene targetScene, object? scriptHost = null)
        {
            var cloned = new Entity
            {
                Id = targetScene.GetNextEntityId(),
                Name = original.Name,
                Guid = Guid.NewGuid(), // New GUID for cloned entity
                MaterialGuid = original.MaterialGuid,
                Active = original.Active
            };
            

            // Utilise SetLocalTRS pour éviter les notifications multiples pendant le clonage
            cloned.SetLocalTRS(original.Transform.Position, original.Transform.Rotation, original.Transform.Scale);

            // Clone all components except those already added in constructor
            foreach (var component in original.GetAllComponents())
            {
                var componentType = component.GetType();
                
                // Skip TransformComponent - already added in constructor
                if (componentType == typeof(TransformComponent)) continue;
                
                // Skip if component already exists in cloned entity
                if (cloned.HasComponent(componentType)) {
                    continue;
                }

                try
                {
                    
                    Component clonedComponent;
                    
                    // Special handling for MonoBehaviour scripts - use ScriptHost
                    if (typeof(Engine.Scripting.MonoBehaviour).IsAssignableFrom(componentType) && scriptHost != null)
                    {
                        try
                        {
                            // Use reflection to call ScriptHost.AddScriptToEntity
                            var addScriptMethod = scriptHost.GetType().GetMethod("AddScriptToEntity");
                            if (addScriptMethod != null)
                            {
                                var script = addScriptMethod.Invoke(scriptHost, new object[] { cloned, componentType });
                                if (script != null)
                                {
                                    // Copy component data using reflection
                                    CopyComponentData(component, (Component)script);
                                    continue; // Script already added to entity by ScriptHost
                                }
                            }
                        }
                        catch (System.Exception)
                        {
                        }
                        
                        // Fallback if ScriptHost fails
                        clonedComponent = (Component)Activator.CreateInstance(componentType)!;
                    }
                    else
                    {
                        // Regular component creation
                        clonedComponent = (Component)Activator.CreateInstance(componentType)!;
                    }
                    
                    // Copy component data using reflection
                    CopyComponentData(component, clonedComponent);
                    
                    try {
                        cloned.AddComponent(clonedComponent);
                    }
                    catch (InvalidOperationException) {
                        // If component already exists, copy data to existing one instead
                        var existingComponent = cloned.GetComponent(componentType);
                        if (existingComponent != null) {
                            CopyComponentData(component, existingComponent);
                        }
                    }
                    
                }
                catch (System.Exception)
                {
                }
            }

            targetScene.Entities.Add(cloned);
            return cloned;
        }

        private static void CopyComponentData(Component source, Component target)
        {
            var type = source.GetType();
            
            // Copy all serializable fields and properties
            var fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (field.IsLiteral || field.IsInitOnly) continue;
                if (field.Name == "Entity" || field.Name == "_entity") continue; // Skip entity references
                
                try
                {
                    var value = field.GetValue(source);
                    field.SetValue(target, value);
                }
                catch (System.Exception)
                {
                }
            }

            var properties = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            foreach (var property in properties)
            {
                if (!property.CanRead || !property.CanWrite) continue;
                if (property.Name == "Entity") continue; // Skip entity references
                
                try
                {
                    var value = property.GetValue(source);
                    property.SetValue(target, value);
                }
                catch { }
            }
        }
    }
}



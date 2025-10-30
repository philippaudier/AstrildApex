using System;
using Engine.Scene;
using Engine.Serialization;

namespace Engine.Components
{
    /// <summary>
    /// Base class for all components
    /// </summary>
    public abstract class Component
    {
        public Entity? Entity { get; internal set; }

        // Ajout de la sérialisation du flag Enabled
        [Engine.Serialization.SerializableAttribute("enabled")]
        private bool _enabled = true;
        [Engine.Serialization.SerializableAttribute("enabled")]
        public bool Enabled 
        { 
            get => _enabled;
            set 
            {
                if (_enabled == value) return;
                _enabled = value;
                if (_enabled) 
                    OnEnable();
                else 
                    OnDisable();
            }
        }

        /// <summary>
        /// Called when component is added to an entity (editor or play)
        /// </summary>
        public virtual void OnAttached() { }

        /// <summary>
        /// Called when component is removed from an entity
        /// </summary>
        public virtual void OnDetached() { }

        /// <summary>Called when the component (or its Entity) becomes enabled/active in Play Mode</summary>
        public virtual void OnEnable() { }

        /// <summary>Called when the component (or its Entity) is disabled in Play Mode</summary>
        public virtual void OnDisable() { }

        /// <summary>
        /// Called once at the start of Play Mode (for initialization)
        /// </summary>
        public virtual void Start() { }

        /// <summary>
        /// Called every frame for enabled components (game logic update)
        /// </summary>
        public virtual void Update(float deltaTime) { }

        /// <summary>
        /// Called each frame after all Update() calls (late-stage updates)
        /// </summary>
        public virtual void LateUpdate(float deltaTime) { }

        /// <summary>
        /// Called at a fixed interval for physics updates
        /// </summary>
        public virtual void FixedUpdate(float deltaTime) { }

        /// <summary>
        /// Called when the component is being destroyed (removed from entity or scene unload)
        /// </summary>
        public virtual void OnDestroy() { }

    // ---- Collision/Trigger Callbacks (Unity-like, no-op by default) ----
    public virtual void OnCollisionEnter(Engine.Physics.Collision collision) { }
    public virtual void OnCollisionStay(Engine.Physics.Collision collision) { }
    public virtual void OnCollisionExit(Engine.Physics.Collision collision) { }
    public virtual void OnTriggerEnter(Engine.Physics.Collision collision) { }
    public virtual void OnTriggerStay(Engine.Physics.Collision collision) { }
    public virtual void OnTriggerExit(Engine.Physics.Collision collision) { }
    }
}
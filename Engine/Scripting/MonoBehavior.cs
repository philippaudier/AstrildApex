using System;
using Engine.Components;

namespace Engine.Scripting
{
    /// <summary>
    /// Base script Unity-like. Hérite de Component pour s'attacher à une Entity.
    /// </summary>
    public abstract class MonoBehaviour : Component
    {
        // ===== Unity-like Lifecycle Methods =====
        
        /// <summary>
        /// Called when the script instance is being loaded (before Start).
        /// Use this for initialization that doesn't depend on other objects.
        /// </summary>
        public virtual void Awake() { }

        /// <summary>
        /// Called before the first frame update (after Awake).
        /// Use this for initialization that depends on other objects being ready.
        /// </summary>
        public override void Start() 
        { 
            // Laisser vide – la logique Start utilisateur sera dans les overrides des scripts concrets
        }

        /// <summary>
        /// Called every frame. Use for regular updates.
        /// </summary>
        /// <param name="dt">Delta time in seconds</param>
        public override void Update(float dt)
        {
            // Ne rien faire ici (les scripts utilisateurs peuvent override Update directement)
        }

        /// <summary>
        /// Called every fixed timestep. Use for physics and time-critical updates.
        /// </summary>
        /// <param name="fixedDeltaTime">Fixed delta time in seconds</param>
        public override void FixedUpdate(float fixedDeltaTime) { }

        /// <summary>
        /// Called after all Update functions have been called. Use for camera following, etc.
        /// </summary>
        /// <param name="dt">Delta time in seconds</param>
        public override void LateUpdate(float dt) { }

        /// <summary>
        /// Called when the MonoBehaviour is destroyed (on component removal or scene unload).
        /// Use this to clean up resources, subscriptions, etc.
        /// </summary>
        public override void OnDestroy() { }

        /// <summary>
        /// Called when the script is enabled (on component enable or entity activation).
        /// </summary>
        public override void OnEnable() { }

        /// <summary>
        /// Called when the script is disabled (on component disable or entity deactivation).
        /// </summary>
        public override void OnDisable() { }

        // ===== Unity-like Helper Methods =====
        
        /// <summary>
        /// Gets a component of type T attached to the same Entity.
        /// </summary>
        protected T? GetComponent<T>() where T : Component => Entity?.GetComponent<T>();
        
        /// <summary>
        /// Adds a component of type T to the Entity.
        /// </summary>
        protected T AddComponent<T>() where T : Component, new() => Entity!.AddComponent<T>();
    }
}

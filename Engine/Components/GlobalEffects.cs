using System;
using System.Collections.Generic;
using System.Linq;
using Engine.Scene;
using Engine.Serialization;

namespace Engine.Components
{
    /// <summary>
    /// GlobalEffects component - comme le GlobalVolume de Unity
    /// Permet d'ajouter et de configurer des effets de post-processing
    /// </summary>
    public class GlobalEffects : Component
    {
        [Engine.Serialization.SerializableAttribute("effects")]
        private readonly List<PostProcessEffect> _effects = new();

        public IReadOnlyList<PostProcessEffect> Effects => _effects;

        public override void OnAttached()
        {
            // Enregistrer ce GlobalEffects comme actif dans le système de rendu
            Console.WriteLine($"[GlobalEffects] OnAttached() called! Entity={Entity?.Name}, EffectsCount={_effects.Count}");
            Engine.Rendering.PostProcessManager.RegisterGlobalEffects(this);
            
            // Log each effect
            for (int i = 0; i < _effects.Count; i++)
            {
                var effect = _effects[i];
                Console.WriteLine($"[GlobalEffects]   Effect {i}: {effect?.GetType().Name}, Enabled={effect?.Enabled}");
            }
        }

        public override void OnDetached()
        {
            // Désenregistrer ce GlobalEffects
            Engine.Rendering.PostProcessManager.UnregisterGlobalEffects(this);
        }

        /// <summary>
        /// Ajoute un effet de post-processing
        /// </summary>
        public T AddEffect<T>() where T : PostProcessEffect, new()
        {
            var effect = new T();
            _effects.Add(effect);
            return effect;
        }

        /// <summary>
        /// Supprime un effet de post-processing
        /// </summary>
        public void RemoveEffect<T>() where T : PostProcessEffect
        {
            var effect = _effects.FirstOrDefault(e => e is T);
            if (effect != null)
            {
                _effects.Remove(effect);
            }
        }

        /// <summary>
        /// Récupère un effet de post-processing
        /// </summary>
        public T? GetEffect<T>() where T : PostProcessEffect
        {
            return _effects.FirstOrDefault(e => e is T) as T;
        }

        /// <summary>
        /// Vérifie si un effet existe
        /// </summary>
        public bool HasEffect<T>() where T : PostProcessEffect
        {
            return _effects.Any(e => e is T);
        }

        /// <summary>
        /// Ajoute un effet existant à la liste
        /// </summary>
        public void AddEffect(PostProcessEffect effect)
        {
            if (effect != null)
            {
                _effects.Add(effect);
            }
        }

        /// <summary>
        /// Supprime tous les effets
        /// </summary>
        public void RemoveAllEffects()
        {
            _effects.Clear();
        }
    }

    /// <summary>
    /// Classe de base pour tous les effets de post-processing
    /// </summary>
    public abstract class PostProcessEffect
    {
        public bool Enabled { get; set; } = true;
        public float Intensity { get; set; } = 1.0f;
        public int Priority { get; set; } = 0;

        /// <summary>
        /// Nom de l'effet (pour l'affichage dans l'inspecteur)
        /// </summary>
        public abstract string EffectName { get; }

        /// <summary>
        /// Applique l'effet de post-processing
        /// </summary>
        public abstract void Apply(PostProcessContext context);
    }

    /// <summary>
    /// Contexte contenant les informations nécessaires pour le post-processing
    /// </summary>
    public class PostProcessContext
    {
        public uint SourceTexture { get; set; }
        public uint TargetFramebuffer { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public float DeltaTime { get; set; }
        // Optional scene associated with this post-process pass. When set,
        // PostProcessManager will only apply GlobalEffects whose Entity belongs
        // to the same scene. This prevents effects from one scene (editor/game)
        // being applied to another.
        public Engine.Scene.Scene? Scene { get; set; }
        
        // Depth texture for effects like SSAO
        public uint DepthTexture { get; set; }
        
        // Projection matrix for screen-space effects
        public OpenTK.Mathematics.Matrix4? ProjectionMatrix { get; set; }
        
        // View matrix for temporal reprojection
        public OpenTK.Mathematics.Matrix4? ViewMatrix { get; set; }

        public PostProcessContext(uint sourceTexture, uint targetFramebuffer, int width, int height, float deltaTime = 0f, Engine.Scene.Scene? scene = null)
        {
            SourceTexture = sourceTexture;
            TargetFramebuffer = targetFramebuffer;
            Width = width;
            Height = height;
            DeltaTime = deltaTime;
            Scene = scene;
            DepthTexture = 0;
            ProjectionMatrix = null;
            ViewMatrix = null;
        }
    }

    /// <summary>
    /// Interface pour les renderers d'effets de post-processing
    /// </summary>
    public interface IPostProcessRenderer
    {
        void Render(PostProcessEffect effect, PostProcessContext context);
        void Initialize();
        void Dispose();
    }
}
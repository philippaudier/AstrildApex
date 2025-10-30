using System;
using System.Collections.Generic;
using System.Linq;
using Engine.Components;
using OpenTK.Graphics.OpenGL4;

namespace Engine.Rendering
{
    /// <summary>
    /// Gestionnaire central des effets de post-processing
    /// </summary>
    public static class PostProcessManager
    {
        private static readonly List<GlobalEffects> _globalEffects = new();
        private static readonly Dictionary<Type, IPostProcessRenderer> _renderers = new();
        private static bool _initialized = false;

        /// <summary>
        /// Initialise le système de post-processing
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;


            // Enregistrer les renderers par défaut
            RegisterRenderer<BloomEffect>(new BloomRenderer());
            RegisterRenderer<ToneMappingEffect>(new ToneMappingRenderer());
            RegisterRenderer<ChromaticAberrationEffect>(new ChromaticAberrationRenderer());


            // Initialiser tous les renderers
            foreach (var kvp in _renderers)
            {
                kvp.Value.Initialize();
            }

            _initialized = true;
        }

        /// <summary>
        /// Réinitialise le système (pour debug)
        /// </summary>
        public static void Reinitialize()
        {
            _initialized = false;
            _renderers.Clear();
            Initialize();
        }

        /// <summary>
        /// Enregistre un GlobalEffects actif
        /// </summary>
        public static void RegisterGlobalEffects(GlobalEffects effects)
        {
            if (!_globalEffects.Contains(effects))
            {
                _globalEffects.Add(effects);
            }
            else
            {
            }
        }

        /// <summary>
        /// Désenregistre un GlobalEffects
        /// </summary>
        public static void UnregisterGlobalEffects(GlobalEffects effects)
        {
            if (_globalEffects.Remove(effects))
            {
            }
            else
            {
            }
        }

        /// <summary>
        /// Enregistre un renderer pour un type d'effet
        /// </summary>
        public static void RegisterRenderer<T>(IPostProcessRenderer renderer) where T : PostProcessEffect
        {
            _renderers[typeof(T)] = renderer;
        }

        /// <summary>
        /// Applique tous les effets de post-processing actifs
        /// </summary>
        public static void ApplyEffects(PostProcessContext context)
        {
            // Auto-initialisation si pas encore fait OU si les renderers sont vides
            if (!_initialized || _renderers.Count == 0)
            {
                _initialized = false;
                Initialize();
            }
            

            // If a scene is provided in context, only apply GlobalEffects whose
            // owning entity belongs to the same scene. This prevents effects from
            // the editor/global scene being applied to the GamePanel (and vice versa).
            var applicable = _globalEffects.AsEnumerable();
            if (context.Scene != null)
            {
                // Log which global effects are in the list and whether they belong to the context scene
                foreach (var ge in _globalEffects)
                {
                    try
                    {
                        var id = ge?.Entity != null ? ge.Entity.Id.ToString() : "<no-entity>";
                        var inScene = ge?.Entity != null && context.Scene.Entities.Contains(ge.Entity);
                    }
                    catch { }
                }

                applicable = applicable.Where(ge => ge?.Entity != null && context.Scene.Entities.Contains(ge.Entity));
            }

            foreach (var globalEffect in applicable)
            {
                if (globalEffect == null) continue;
                if (globalEffect.Entity != null && globalEffect.Entity.Active == false) continue;
                if (globalEffect.Enabled == false) continue;
                if (globalEffect.Effects == null) continue;

                foreach (var effect in globalEffect.Effects.OrderBy(e => e?.Priority ?? 0))
                {
                    if (effect?.Enabled == false) continue;

                    try
                    {
                        GL.BindFramebuffer(OpenTK.Graphics.OpenGL4.FramebufferTarget.Framebuffer, (int)context.TargetFramebuffer);
                        GL.Viewport(0, 0, Math.Max(1, context.Width), Math.Max(1, context.Height));
                    }
                    catch (Exception) { }

                    if (_renderers.TryGetValue(effect!.GetType(), out var renderer))
                    {
                        renderer.Render(effect, context);
                    }
                    else
                    {
                    }

                    try { GL.BindFramebuffer(OpenTK.Graphics.OpenGL4.FramebufferTarget.Framebuffer, 0); } catch { }
                }
            }
        }

        /// <summary>
        /// Libère les ressources
        /// </summary>
        public static void Dispose()
        {
            foreach (var renderer in _renderers.Values)
            {
                renderer.Dispose();
            }
            _renderers.Clear();
            _initialized = false;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using Engine.Components;
using OpenTK.Graphics.OpenGL4;
using Engine.Rendering.PostProcess;

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
        private static bool _debugLogOnce = true;
        private static int _fullscreenVAO = 0;

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
            // FXAA anti-aliasing (fast, post-process)
            RegisterRenderer<FXAAEffect>(new FXAARenderer());
            RegisterRenderer<SSAOEffect>(new SSAOPostEffectRenderer());
            RegisterRenderer<GTAOEffect>(new GTAORenderer());
            RegisterRenderer<DepthOfFieldEffect>(new DepthOfFieldRenderer());
            RegisterRenderer<MotionBlurEffect>(new MotionBlurRenderer());
            RegisterRenderer<ImageSharpeningEffect>(new ImageSharpeningRenderer());
            RegisterRenderer<VolumetricFogEffect>(new VolumetricFogRenderer());
            RegisterRenderer<AtmosphericScatteringEffect>(new AtmosphericScatteringRenderer());
            RegisterRenderer<ColorGradingEffect>(new ColorGradingRenderer());
            // Underwater volume effect (Subnautica-style)
            RegisterRenderer<UnderwaterEffect>(new UnderwaterRenderer());


            // Initialiser tous les renderers
            foreach (var kvp in _renderers)
            {
                kvp.Value.Initialize();
            }

            // Use PostProcessHelper's properly configured fullscreen VAO
            // This VAO has actual vertex data which is more compatible across drivers
            _fullscreenVAO = PostProcessHelper.GetFullscreenVAO();
            Console.WriteLine($"[PostProcessManager] Using fullscreen VAO: {_fullscreenVAO}");

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
        /// Essaie de récupérer un renderer pour un type d'effet donné
        /// </summary>
        public static bool TryGetRenderer(Type effectType, out IPostProcessRenderer renderer)
        {
            return _renderers.TryGetValue(effectType, out renderer!);
        }

        /// <summary>
        /// Applique tous les effets de post-processing actifs
        /// </summary>
        private static int _frameCount = 0;

        public static void ApplyEffects(PostProcessContext context)
        {
            _frameCount++;

            // Auto-initialisation si pas encore fait OU si les renderers sont vides
            if (!_initialized || _renderers.Count == 0)
            {
                Console.WriteLine($"[PostProcessManager] Frame {_frameCount}: Re-initializing (initialized={_initialized}, renderers={_renderers.Count})");
                _initialized = false;
                Initialize();
            }

            // Log first 5 frames
            if (_frameCount <= 5)
            {
                Console.WriteLine($"[PostProcessManager] Frame {_frameCount}: GlobalEffects={_globalEffects.Count}, Renderers={_renderers.Count}");
            }
            

            // If a scene is provided in context, only apply GlobalEffects whose
            // owning entity belongs to the same scene. This prevents effects from
            // the editor/global scene being applied to the GamePanel (and vice versa).
            var applicable = _globalEffects.AsEnumerable();
            if (context.Scene != null)
            {
                // Debug: log once per session
                if (_debugLogOnce)
                {
                    _debugLogOnce = false;
                    Console.WriteLine($"[PostProcessManager] ApplyEffects: GlobalEffectsCount={_globalEffects.Count}, SceneEntities={context.Scene.Entities.Count}");
                    foreach (var ge in _globalEffects)
                    {
                        var entityName = ge?.Entity?.Name ?? "<no-entity>";
                        var hasEntityScene = ge?.Entity?.Scene != null;
                        var inScene = ge?.Entity != null && context.Scene.Entities.Contains(ge.Entity);
                        Console.WriteLine($"[PostProcessManager]   GlobalEffect: Entity={entityName}, HasEntityScene={hasEntityScene}, InContextScene={inScene}, Effects={ge?.Effects.Count}");
                    }
                }

                applicable = applicable.Where(ge => ge?.Entity != null && context.Scene.Entities.Contains(ge.Entity));
            }

            var applicableList = applicable.ToList();
            if (_frameCount <= 5)
            {
                Console.WriteLine($"[PostProcessManager] Frame {_frameCount}: ApplicableEffects={applicableList.Count}");
            }

            foreach (var globalEffect in applicableList)
            {
                if (globalEffect == null) continue;
                if (globalEffect.Entity != null && globalEffect.Entity.Active == false) continue;
                if (globalEffect.Enabled == false) continue;
                if (globalEffect.Effects == null) continue;

                // Get all active effects sorted by priority
                // PERFORMANCE: Avoid LINQ allocations with manual filtering
                var activeEffects = new List<PostProcessEffect>(globalEffect.Effects.Count);
                foreach (var e in globalEffect.Effects)
                {
                    if (e?.Enabled == true)
                        activeEffects.Add(e);
                }
                activeEffects.Sort((a, b) => (a?.Priority ?? 0).CompareTo(b?.Priority ?? 0));

                // DEBUG: Log effect count once
                if (_frameCount <= 3)
                {
                    Console.WriteLine($"[PostProcessManager] Frame {_frameCount}: Applying {activeEffects.Count} effects, PingPong={(context.SourceTexture2 != 0)}");
                }

                foreach (var effect in activeEffects)
                {
                    if (effect == null) continue;

                    // PING-PONG: Get the current source texture and target framebuffer
                    // If ping-pong is set up (SourceTexture2 != 0), use the alternating buffers
                    // Otherwise fall back to the original single-buffer behavior
                    uint sourceTexture;
                    uint targetFramebuffer;

                    if (context.SourceTexture2 != 0 && context.TargetFramebuffer2 != 0)
                    {
                        // Ping-pong mode: read from current source, write to alternate target
                        sourceTexture = context.GetCurrentSourceTexture();
                        targetFramebuffer = context.GetCurrentTargetFramebuffer();
                    }
                    else
                    {
                        // Legacy mode: single buffer (undefined behavior, but backwards compatible)
                        sourceTexture = context.SourceTexture;
                        targetFramebuffer = context.TargetFramebuffer;
                    }

                    // DEBUG: Log first 3 frames
                    if (_frameCount <= 3)
                    {
                        Console.WriteLine($"[PostProcessManager]   Effect {effect.GetType().Name}: src={sourceTexture}, dst={targetFramebuffer}, ResultInPrimary={context.ResultInPrimary}");
                    }

                    try
                    {
                        GL.BindFramebuffer(OpenTK.Graphics.OpenGL4.FramebufferTarget.Framebuffer, (int)targetFramebuffer);
                        GL.Viewport(0, 0, Math.Max(1, context.Width), Math.Max(1, context.Height));

                        // Set up clean state for post-processing (like the Editor does)
                        GL.Disable(EnableCap.DepthTest);
                        GL.Disable(EnableCap.Blend);
                        GL.Disable(EnableCap.CullFace);

                        // Bind VAO - required by some OpenGL drivers for rendering
                        if (_fullscreenVAO != 0)
                        {
                            GL.BindVertexArray(_fullscreenVAO);
                        }
                    }
                    catch (Exception) { }

                    if (_renderers.TryGetValue(effect.GetType(), out var renderer))
                    {
                        // Create a temporary context with the correct source/target for this effect
                        // Renderers read from SourceTexture and write to TargetFramebuffer
                        var originalSource = context.SourceTexture;
                        var originalTarget = context.TargetFramebuffer;
                        context.SourceTexture = sourceTexture;
                        context.TargetFramebuffer = targetFramebuffer;

                        // Render the actual post-process effect
                        renderer.Render(effect, context);

                        // Restore original values (for GetFinalResultTexture to work)
                        context.SourceTexture = originalSource;
                        context.TargetFramebuffer = originalTarget;

                        // PING-PONG: Swap buffers so next effect reads from what we just wrote
                        if (context.SourceTexture2 != 0 && context.TargetFramebuffer2 != 0)
                        {
                            context.SwapBuffers();
                        }
                    }
                    else
                    {
                    }

                    try
                    {
                        GL.BindFramebuffer(OpenTK.Graphics.OpenGL4.FramebufferTarget.Framebuffer, 0);
                        GL.BindVertexArray(0);
                    }
                    catch { }
                }
            }

            // Restore OpenGL state after all effects
            try
            {
                GL.Enable(EnableCap.DepthTest);
                GL.Enable(EnableCap.CullFace);
                GL.BindVertexArray(0);
            }
            catch { }
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

            // Don't delete the VAO - it's owned by PostProcessHelper
            _fullscreenVAO = 0;

            _initialized = false;
        }
    }
}
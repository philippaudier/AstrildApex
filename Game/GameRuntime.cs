using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Desktop;
using Engine.Scene;
using Engine.Components;
using Engine.Assets;

namespace Game;

/// <summary>
/// Game Runtime - handles scene loading, game loop, and rendering
/// Simplified version for standalone game builds
/// </summary>
public class GameRuntime
{
    private readonly GameWindow _window;
    private readonly string? _scenePath;

    private Scene? _scene;
    private BuildRenderer? _renderer;

    // Fixed timestep for physics (60 Hz)
    private float _fixedTimeAccumulator = 0f;
    private const float FixedDeltaTime = 1.0f / 60.0f;

    // Component cache for performance
    private readonly Dictionary<uint, List<Component>> _componentCache = new();

    // Weather system
    private readonly Engine.Systems.WeatherSystem _weatherSystem = new();

    public GameRuntime(GameWindow window, string? scenePath)
    {
        _window = window;
        _scenePath = scenePath;
    }

    public void Initialize()
    {
        // Mark as play mode for AudioSource.PlayOnAwake and other play-mode behaviors
        Engine.Core.RuntimeEnvironment.IsPlayMode = true;
        Engine.Core.RuntimeEnvironment.IsEditor = false;
        Console.WriteLine("[GameRuntime] RuntimeEnvironment: IsPlayMode=true, IsEditor=false");

        // OpenGL setup
        GL.Enable(EnableCap.FramebufferSrgb);
        GL.Enable(EnableCap.DepthTest);
        GL.DepthFunc(DepthFunction.Lequal);
        GL.ClearColor(0.1f, 0.1f, 0.15f, 1f);

        string glVersion = GL.GetString(StringName.Version) ?? "Unknown";
        string gpuName = GL.GetString(StringName.Renderer) ?? "Unknown";
        Console.WriteLine($"[GameRuntime] OpenGL: {glVersion}");
        Console.WriteLine($"[GameRuntime] GPU: {gpuName}");

        // Initialize audio engine
        try
        {
            Engine.Audio.Core.AudioEngine.Instance.Initialize();
            Console.WriteLine("[GameRuntime] Audio engine initialized");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameRuntime] Audio init failed: {ex.Message}");
        }

        // Initialize asset database
        var assetsDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets");
        if (Directory.Exists(assetsDir))
        {
            AssetDatabase.Initialize(assetsDir);
            AssetDatabase.EnsureDefaultWhiteMaterial();
            Console.WriteLine($"[GameRuntime] AssetDatabase initialized: {assetsDir}");
        }

        // Initialize input
        Engine.Input.InputManager.Initialize(_window);
        Engine.Input.InputManager.Instance?.SetupDefaultPlayerControls();
        Console.WriteLine("[GameRuntime] Input system initialized");

        // Initialize post-processing
        try
        {
            Engine.Rendering.PostProcessManager.Initialize();
            Console.WriteLine("[GameRuntime] PostProcessManager initialized");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameRuntime] PostProcess init failed: {ex.Message}");
        }

        // Create renderer
        _renderer = new BuildRenderer(_window.ClientSize.X, _window.ClientSize.Y);
        Console.WriteLine($"[GameRuntime] Renderer created: {_window.ClientSize.X}x{_window.ClientSize.Y}");

        // Load scene (or create empty one if no scene path)
        if (!string.IsNullOrEmpty(_scenePath))
        {
            LoadScene(_scenePath);
        }
        else
        {
            Console.WriteLine("[GameRuntime] No scene path - creating empty scene");
            _scene = new Scene();
            _renderer?.SetScene(_scene);
        }

        // Initialize all components (call Start/Awake)
        InitializeComponents();
    }

    private void LoadScene(string path)
    {
        Console.WriteLine($"[GameRuntime] Loading scene: {path}");

        if (!File.Exists(path))
        {
            Console.WriteLine($"[GameRuntime] ERROR: Scene file not found: {path}");
            _scene = new Scene();
            return;
        }

        try
        {
            // Use Engine's SceneSerializer to load the scene
            _scene = new Scene();
            var result = Engine.Serialization.SceneSerializer.LoadFromFile(_scene, path);

            if (result.Success)
            {
                Console.WriteLine($"[GameRuntime] Scene loaded: {result.LoadedEntityCount} entities");
                if (result.Warnings.Count > 0)
                {
                    foreach (var warning in result.Warnings)
                    {
                        Console.WriteLine($"[GameRuntime] Warning: {warning}");
                    }
                }
            }
            else
            {
                Console.WriteLine($"[GameRuntime] Warning: Scene load failed: {result.ErrorMessage}");
                _scene = new Scene();
            }

            _renderer?.SetScene(_scene);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameRuntime] ERROR loading scene: {ex.Message}");
            _scene = new Scene();
        }
    }

    private void InitializeComponents()
    {
        if (_scene == null) return;

        Console.WriteLine("[GameRuntime] Initializing components...");
        _componentCache.Clear();

        // Debug: List all component types found
        int scriptCount = 0;
        int componentCount = 0;

        var entities = CollectionsMarshal.AsSpan(_scene.Entities);

        // PASS 1: Collect components and call OnAttached for all
        for (int i = 0; i < entities.Length; i++)
        {
            var entity = entities[i];
            if (!entity.Active) continue;

            var components = entity.GetAllComponents();
            _componentCache[entity.Id] = new List<Component>(components);

            foreach (var component in components)
            {
                if (IsMonoBehaviour(component))
                {
                    scriptCount++;
                    Console.WriteLine($"[GameRuntime] Found script: {component.GetType().Name} on {entity.Name}");
                }
                else
                {
                    componentCount++;
                }

                // Call OnAttached for all components (engine hook for initialization)
                try
                {
                    component.OnAttached();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GameRuntime] OnAttached error on {component.GetType().Name}: {ex.Message}");
                }
            }
        }

        // PASS 2: Call Awake for enabled components
        for (int i = 0; i < entities.Length; i++)
        {
            var entity = entities[i];
            if (!entity.Active) continue;

            if (!_componentCache.TryGetValue(entity.Id, out var components)) continue;

            foreach (var component in components)
            {
                if (!component.Enabled) continue;

                try
                {
                    // Call Awake (MonoBehaviour lifecycle)
                    var awakeMethod = component.GetType().GetMethod("Awake",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    awakeMethod?.Invoke(component, null);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GameRuntime] Awake error on {component.GetType().Name}: {ex.Message}");
                }
            }
        }

        // PASS 3: Call OnEnable for enabled components (CRITICAL for AudioSource.PlayOnAwake)
        for (int i = 0; i < entities.Length; i++)
        {
            var entity = entities[i];
            if (!entity.Active) continue;

            if (!_componentCache.TryGetValue(entity.Id, out var components)) continue;

            foreach (var component in components)
            {
                if (!component.Enabled) continue;

                try
                {
                    // Call OnEnable (engine hook for component activation)
                    component.OnEnable();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GameRuntime] OnEnable error on {component.GetType().Name}: {ex.Message}");
                }
            }
        }

        // PASS 4: Call Start for enabled components
        for (int i = 0; i < entities.Length; i++)
        {
            var entity = entities[i];
            if (!entity.Active) continue;

            if (!_componentCache.TryGetValue(entity.Id, out var components)) continue;

            foreach (var component in components)
            {
                if (!component.Enabled) continue;

                try
                {
                    var startMethod = component.GetType().GetMethod("Start",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    startMethod?.Invoke(component, null);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GameRuntime] Start error on {component.GetType().Name}: {ex.Message}");
                }
            }
        }

        Console.WriteLine($"[GameRuntime] Initialized {_componentCache.Count} entities, {scriptCount} scripts, {componentCount} components");
    }

    public void Update(float deltaTime)
    {
        if (_scene == null) return;

        // CRITICAL: Update Engine.Core.Time so components can use Time.DeltaTime
        Engine.Core.Time.Update(deltaTime);

        // Update input
        Engine.Input.InputManager.Instance?.Update();

        // Update TimeComponent first (drives day/night cycle)
        UpdateTimeComponent(deltaTime);

        // Update weather system
        try
        {
            _weatherSystem.Update(_scene, deltaTime);
        }
        catch { }

        // Update components (variable timestep)
        UpdateComponents(deltaTime);

        // Fixed update with accumulator (60 Hz)
        _fixedTimeAccumulator += deltaTime;
        while (_fixedTimeAccumulator >= FixedDeltaTime)
        {
            FixedUpdateComponents(FixedDeltaTime);
            _fixedTimeAccumulator -= FixedDeltaTime;
        }

        // Late update
        LateUpdateComponents(deltaTime);

        // Update audio
        try
        {
            Engine.Audio.Core.AudioEngine.Instance.Update(deltaTime);
        }
        catch { }
    }

    private void UpdateTimeComponent(float deltaTime)
    {
        if (_scene == null) return;

        foreach (var entity in _scene.Entities)
        {
            if (!entity.Active) continue;
            var timeComp = entity.GetComponent<TimeComponent>();
            if (timeComp != null)
            {
                timeComp.Update(deltaTime);
                break; // Only one TimeComponent per scene
            }
        }
    }

    private void UpdateComponents(float deltaTime)
    {
        if (_scene == null) return;

        var entities = CollectionsMarshal.AsSpan(_scene.Entities);

        // PASS 1: Update MonoBehaviour scripts FIRST (they set desired velocity, etc.)
        // Note: We check by base type name because direct "is" check may fail across assemblies
        for (int i = 0; i < entities.Length; i++)
        {
            var entity = entities[i];
            if (!entity.Active) continue;

            if (!_componentCache.TryGetValue(entity.Id, out var components)) continue;

            for (int c = 0; c < components.Count; c++)
            {
                var component = components[c];
                if (!component.Enabled) continue;
                if (!IsMonoBehaviour(component)) continue;

                try
                {
                    component.Update(deltaTime);
                }
                catch { }
            }
        }

        // PASS 2: Update built-in components (CharacterController, etc.) AFTER scripts
        for (int i = 0; i < entities.Length; i++)
        {
            var entity = entities[i];
            if (!entity.Active) continue;

            if (!_componentCache.TryGetValue(entity.Id, out var components)) continue;

            for (int c = 0; c < components.Count; c++)
            {
                var component = components[c];
                if (!component.Enabled) continue;
                if (IsMonoBehaviour(component)) continue; // Already updated

                try
                {
                    component.Update(deltaTime);
                }
                catch { }
            }
        }
    }

    /// <summary>
    /// Check if a component is a MonoBehaviour script by checking the type hierarchy.
    /// This works across assemblies where direct "is" check may fail.
    /// </summary>
    private static bool IsMonoBehaviour(Component component)
    {
        var type = component.GetType();
        while (type != null)
        {
            if (type.FullName == "Engine.Scripting.MonoBehaviour" || type.Name == "MonoBehaviour")
                return true;
            type = type.BaseType;
        }
        return false;
    }

    private void FixedUpdateComponents(float fixedDeltaTime)
    {
        if (_scene == null) return;

        var entities = CollectionsMarshal.AsSpan(_scene.Entities);
        for (int i = 0; i < entities.Length; i++)
        {
            var entity = entities[i];
            if (!entity.Active) continue;

            if (!_componentCache.TryGetValue(entity.Id, out var components)) continue;

            for (int c = 0; c < components.Count; c++)
            {
                var component = components[c];
                if (!component.Enabled) continue;

                try
                {
                    component.FixedUpdate(fixedDeltaTime);
                }
                catch { }
            }
        }
    }

    private void LateUpdateComponents(float deltaTime)
    {
        if (_scene == null) return;

        var entities = CollectionsMarshal.AsSpan(_scene.Entities);
        for (int i = 0; i < entities.Length; i++)
        {
            var entity = entities[i];
            if (!entity.Active) continue;

            if (!_componentCache.TryGetValue(entity.Id, out var components)) continue;

            for (int c = 0; c < components.Count; c++)
            {
                var component = components[c];
                if (!component.Enabled) continue;

                try
                {
                    component.LateUpdate(deltaTime);
                }
                catch { }
            }
        }
    }

    public void Render()
    {
        GL.Viewport(0, 0, _window.ClientSize.X, _window.ClientSize.Y);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        _renderer?.RenderFrame();
    }

    public void OnResize(int width, int height)
    {
        if (width <= 0 || height <= 0) return;

        Console.WriteLine($"[GameRuntime] Resize: {width}x{height}");
        _renderer?.Resize(width, height);
    }

    public void Shutdown()
    {
        // Call OnDestroy on all components
        if (_scene != null)
        {
            foreach (var entity in _scene.Entities)
            {
                if (!_componentCache.TryGetValue(entity.Id, out var components)) continue;

                foreach (var component in components)
                {
                    try
                    {
                        var destroyMethod = component.GetType().GetMethod("OnDestroy",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        destroyMethod?.Invoke(component, null);
                    }
                    catch { }
                }
            }
        }

        _componentCache.Clear();
        _renderer?.Dispose();

        try
        {
            Engine.Audio.Core.AudioEngine.Instance.Dispose();
        }
        catch { }

        Console.WriteLine("[GameRuntime] Shutdown complete");
    }
}

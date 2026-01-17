using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Engine.Rendering;
using Engine.Components;
using Engine.Assets;
using SceneClass = Engine.Scene.Scene;

namespace Game;

/// <summary>
/// Renderer for standalone game builds.
/// Extends CoreRenderer for shared rendering infrastructure.
/// Produces identical output to the editor viewport.
/// </summary>
public sealed class BuildRenderer : CoreRenderer
{
    // === Reflection Framebuffer ===
    private int _reflectionFbo = 0;
    private int _reflectionTex = 0;
    private int _reflectionDepthRbo = 0;
    private int _reflectionW = 512;
    private int _reflectionH = 512;
    private float _reflectionResolutionScale = 0.5f;

    // === Basic Shader (for simple mesh rendering) ===
    private int _basicShader = 0;

    // === Blit Shader ===
    private int _blitShader = 0;
    private int _fullscreenQuadVAO = 0;

    // === Shadow Settings ===
    private bool _shadowsEnabled = true;
    private float _shadowDistance = 200f;
    private float _shadowBias = 0.005f;
    private float _shadowStrength = 0.8f;
    private int _shadowMapSize = 2048;

    // === Vegetation Initialization Flag ===
    private bool _vegetationInitialized = false;

    // === Debug ===
    private int _debugFrameCount = 0;
    private bool _skyboxDebugOnce = true;

    // === Post-Processing Control ===
    // Set to true to bypass post-processing and directly show the rendered scene
    private bool _bypassPostProcessing = false;

    // === Scene Color Texture for Water Refraction ===
    // Separate texture to avoid read/write hazard when rendering water
    private int _sceneColorTexture = 0;

    // === Cloud Renderer ===
    private CloudRenderer? _cloudRenderer;

    // === Starfield Rendering ===
    private int _starVao = 0;
    private int _starVbo = 0;
    private int _starCount = 0;
    private ShaderProgram? _starShader = null;
    private bool _starsInitialized = false;
    private readonly Random _starRng = new Random(123456);

    public BuildRenderer(int width, int height)
    {
        _width = Math.Max(1, width);
        _height = Math.Max(1, height);

        InitializeOpenGL();

        Console.WriteLine($"[BuildRenderer] Created {_width}x{_height}");
    }

    #region Initialization Overrides

    protected override void InitializeOpenGL()
    {
        base.InitializeOpenGL();

        // Build-specific initialization
        CreateBasicShader();
        CreateBlitShader();
        CreateFullscreenQuad();
        CreateReflectionFramebuffer();
        CreateSceneColorTexture();

        // Initialize cloud renderer
        try
        {
            _cloudRenderer = new CloudRenderer();
            _cloudRenderer.Initialize();
            Console.WriteLine("[BuildRenderer] Cloud renderer initialized");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BuildRenderer] Failed to initialize cloud renderer: {ex.Message}");
        }
    }

    private void CreateSceneColorTexture()
    {
        // Clean up old texture if exists
        if (_sceneColorTexture != 0)
        {
            GL.DeleteTexture(_sceneColorTexture);
            _sceneColorTexture = 0;
        }

        // Create scene color texture (same format as _colorTexture)
        _sceneColorTexture = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, _sceneColorTexture);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f, _width, _height, 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        GL.BindTexture(TextureTarget.Texture2D, 0);

        Console.WriteLine($"[BuildRenderer] Scene color texture created: {_sceneColorTexture} ({_width}x{_height})");
    }

    private void CreateBasicShader()
    {
        string vertexSource = @"
#version 330 core
layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec3 aNormal;
layout (location = 2) in vec2 aTexCoord;

uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;

out vec3 FragPos;
out vec3 Normal;
out vec2 TexCoord;

void main()
{
    FragPos = vec3(uModel * vec4(aPosition, 1.0));
    Normal = mat3(transpose(inverse(uModel))) * aNormal;
    TexCoord = aTexCoord;
    gl_Position = uProjection * uView * vec4(FragPos, 1.0);
}";

        string fragmentSource = @"
#version 330 core
out vec4 FragColor;

in vec3 FragPos;
in vec3 Normal;
in vec2 TexCoord;

uniform vec3 uColor;
uniform bool uHasTexture;
uniform sampler2D uTexture;
uniform vec3 uLightDir;
uniform vec3 uLightColor;
uniform vec3 uAmbientColor;
uniform vec3 uViewPos;

uniform bool uFogEnabled;
uniform vec3 uFogColor;
uniform float uFogStart;
uniform float uFogEnd;

void main()
{
    vec3 color = uColor;
    if (uHasTexture) {
        vec4 texColor = texture(uTexture, TexCoord);
        color = texColor.rgb * uColor;
    }

    vec3 norm = normalize(Normal);
    vec3 lightDir = normalize(-uLightDir);
    float diff = max(dot(norm, lightDir), 0.0);

    vec3 viewDir = normalize(uViewPos - FragPos);
    vec3 halfwayDir = normalize(lightDir + viewDir);
    float spec = pow(max(dot(norm, halfwayDir), 0.0), 32.0) * 0.3;

    vec3 ambient = uAmbientColor * color;
    vec3 diffuse = uLightColor * diff * color;
    vec3 specular = uLightColor * spec;

    vec3 result = ambient + diffuse + specular;

    if (uFogEnabled) {
        float distance = length(FragPos - uViewPos);
        float fogFactor = clamp((uFogEnd - distance) / (uFogEnd - uFogStart), 0.0, 1.0);
        result = mix(uFogColor, result, fogFactor);
    }

    FragColor = vec4(result, 1.0);
}";

        _basicShader = CompileShaderProgram(vertexSource, fragmentSource, "Basic");
    }

    private void CreateBlitShader()
    {
        string vertexSource = @"
#version 330 core
layout (location = 0) in vec2 aPosition;
layout (location = 1) in vec2 aTexCoord;

out vec2 TexCoord;

void main()
{
    gl_Position = vec4(aPosition, 0.0, 1.0);
    TexCoord = aTexCoord;
}";

        string fragmentSource = @"
#version 330 core
out vec4 FragColor;
in vec2 TexCoord;
uniform sampler2D uTexture;

void main()
{
    FragColor = texture(uTexture, TexCoord);
}";

        _blitShader = CompileShaderProgram(vertexSource, fragmentSource, "Blit");
    }

    private int CompileShaderProgram(string vertexSource, string fragmentSource, string name)
    {
        int vertexShader = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(vertexShader, vertexSource);
        GL.CompileShader(vertexShader);
        CheckShaderCompile(vertexShader, $"{name} vertex");

        int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(fragmentShader, fragmentSource);
        GL.CompileShader(fragmentShader);
        CheckShaderCompile(fragmentShader, $"{name} fragment");

        int program = GL.CreateProgram();
        GL.AttachShader(program, vertexShader);
        GL.AttachShader(program, fragmentShader);
        GL.LinkProgram(program);

        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);

        return program;
    }

    private void CheckShaderCompile(int shader, string type)
    {
        GL.GetShader(shader, ShaderParameter.CompileStatus, out int success);
        if (success == 0)
        {
            string log = GL.GetShaderInfoLog(shader);
            Console.WriteLine($"[BuildRenderer] {type} shader compile error: {log}");
        }
    }

    private void CreateFullscreenQuad()
    {
        float[] vertices = {
            -1f, -1f,  0f, 0f,
             1f, -1f,  1f, 0f,
             1f,  1f,  1f, 1f,
            -1f,  1f,  0f, 1f,
        };

        uint[] indices = { 0, 1, 2, 2, 3, 0 };

        _fullscreenQuadVAO = GL.GenVertexArray();
        int vbo = GL.GenBuffer();
        int ebo = GL.GenBuffer();

        GL.BindVertexArray(_fullscreenQuadVAO);

        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));
        GL.EnableVertexAttribArray(1);

        GL.BindVertexArray(0);
    }

    private void CreateReflectionFramebuffer()
    {
        _reflectionW = Math.Max(64, (int)(_width * _reflectionResolutionScale));
        _reflectionH = Math.Max(64, (int)(_height * _reflectionResolutionScale));

        if (_reflectionTex != 0) GL.DeleteTexture(_reflectionTex);
        if (_reflectionDepthRbo != 0) GL.DeleteRenderbuffer(_reflectionDepthRbo);
        if (_reflectionFbo != 0) GL.DeleteFramebuffer(_reflectionFbo);

        _reflectionFbo = GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _reflectionFbo);

        _reflectionTex = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, _reflectionTex);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, _reflectionW, _reflectionH, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _reflectionTex, 0);

        _reflectionDepthRbo = GL.GenRenderbuffer();
        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _reflectionDepthRbo);
        GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.DepthComponent24, _reflectionW, _reflectionH);
        GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, _reflectionDepthRbo);

        CheckFramebufferComplete("Reflection");
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

        Console.WriteLine($"[BuildRenderer] Reflection framebuffer: {_reflectionW}x{_reflectionH}");
    }

    #endregion

    #region Scene Management Overrides

    public override void SetScene(SceneClass scene)
    {
        base.SetScene(scene);
        _vegetationInitialized = false;
    }

    private void InitializeVegetationIfNeeded()
    {
        if (_vegetationInitialized || _scene == null || _vegetationRenderer == null) return;
        _vegetationInitialized = true;

        foreach (var entity in _scene.Entities)
        {
            var terrain = entity.GetComponent<Engine.Components.Terrain>();
            if (terrain != null && terrain.VegetationInstances != null && terrain.VegetationInstances.Count > 0)
            {
                Console.WriteLine($"[BuildRenderer] Deferred vegetation init: {terrain.VegetationInstances.Count} layers on {entity.Name}");
                OnTerrainVegetationRegenerated(terrain);
            }
        }
    }

    #endregion

    #region Resize Override

    public override void Resize(int width, int height)
    {
        base.Resize(width, height);
        CreateReflectionFramebuffer();
        CreateSceneColorTexture();
    }

    #endregion

    #region Main Render Loop

    public override void RenderFrame()
    {
        if (_scene == null)
        {
            if (_debugFrameCount < 5) Console.WriteLine("[BuildRenderer] ERROR: _scene is null!");
            return;
        }

        _debugFrameCount++;

        // Debug logging for first few frames
        if (_debugFrameCount <= 3)
        {
            Console.WriteLine($"[BuildRenderer] Frame {_debugFrameCount}: Entities={_scene.Entities.Count}, Camera={_mainCamera != null}");
        }

        // Initialize vegetation on first render
        InitializeVegetationIfNeeded();

        // Delta time
        float deltaTime = (float)_deltaStopwatch.Elapsed.TotalSeconds;
        _deltaStopwatch.Restart();

        // Process pending texture uploads
        try { TextureCache.ProcessPendingUploads(2); } catch { }

        // Update time component
        UpdateTimeComponent(deltaTime);

        // Update weather system
        try { _weatherSystem.Update(_scene, deltaTime); } catch { }

        // Update camera matrices
        UpdateCameraMatrices();

        if (_mainCamera == null)
        {
            if (_debugFrameCount <= 10) Console.WriteLine($"[BuildRenderer] Frame {_debugFrameCount}: No camera found!");
            return;
        }

        // Build and upload global uniforms
        var globalUniforms = BuildGlobalUniforms();
        UploadGlobalUniforms(ref globalUniforms);

        // Gather scene data for local use
        var sceneData = GatherSceneData();

        // === SHADOW PASS ===
        if (_shadowsEnabled && _shadowManager != null)
        {
            RenderShadowPass(sceneData.lightDir);
        }

        // === MAIN RENDER PASS ===
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
        GL.Viewport(0, 0, _width, _height);

        GL.ClearColor(0.4f, 0.6f, 0.9f, 1f);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        GL.Enable(EnableCap.DepthTest);
        GL.DepthFunc(DepthFunction.Less);

        // ZFlip compensation
        GL.FrontFace(FrontFaceDirection.Cw);

        // Render skybox
        RenderSkybox(sceneData.envSettings);

        // Render clouds (after skybox, before other geometry)
        RenderClouds(sceneData);

        // Render starfield (after skybox/clouds, visible at night)
        RenderStarfield(sceneData);

        // Render terrains
        RenderTerrains(sceneData.lightDir, sceneData.lightColor);

        // Render GPU grass and rocks
        RenderGrassAndRocks(sceneData.lightDir, sceneData.lightColor, sceneData.ambientColor);

        // Render meshes
        RenderMeshes(sceneData);

        // Render vegetation
        RenderVegetation(sceneData.lightDir, sceneData.lightColor, sceneData.ambientColor);

        // === APPLY SSAO BEFORE TRANSPARENT RENDERING ===
        // This applies SSAO on opaque geometry and copies the result to _sceneColorTexture
        // for water refraction. Transparent objects (water, particles) render ON TOP of SSAO.
        // This matches the editor's behavior in ViewportRenderer.ApplySSAOBeforeTransparents().
        ApplySSAOBeforeTransparents();

        // Render water (with reflection)
        float waterLevel = FindWaterPlaneComponentHeight();
        if (_reflectionFbo != 0 && waterLevel > float.MinValue)
        {
            RenderReflectionPass(waterLevel, sceneData);
            // Re-upload global uniforms after reflection pass
            UploadGlobalUniforms(ref globalUniforms);
        }
        RenderWater(sceneData.weatherComp, _reflectionTex);

        // Render particles
        RenderParticles();

        // Restore front face
        GL.FrontFace(FrontFaceDirection.Ccw);

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

        // === POST-PROCESSING ===
        ApplyPostProcessing(deltaTime);

        // === FINAL BLIT ===
        BlitToScreen();
    }

    #endregion

    #region Helper Methods

    private void UpdateTimeComponent(float deltaTime)
    {
        if (_scene == null) return;

        TimeComponent? timeComp = null;
        GlobalEffects? globalEffects = null;
        EnvironmentSettings? envSettings = null;

        // Find all needed components
        foreach (var entity in _scene.Entities)
        {
            if (!entity.Active) continue;

            if (timeComp == null)
                timeComp = entity.GetComponent<TimeComponent>();
            if (globalEffects == null)
                globalEffects = entity.GetComponent<GlobalEffects>();
            if (envSettings == null)
                envSettings = entity.GetComponent<EnvironmentSettings>();

            if (timeComp != null && globalEffects != null && envSettings != null)
                break;
        }

        // Update TimeComponent (this also updates EnvironmentSettings via UpdateEnvironmentSettings)
        if (timeComp != null)
        {
            timeComp.Update(deltaTime);

            // Explicitly update GlobalEffects for color grading/tonemapping
            // This is needed because TimeComponent.UpdateGlobalEffects() may fail to auto-detect
            // if Entity.Scene isn't properly linked during deserialization
            if (globalEffects != null && globalEffects.Enabled)
            {
                var colorGrading = globalEffects.GetEffect<ColorGradingEffect>();
                if (colorGrading != null && colorGrading.Enabled)
                {
                    colorGrading.UpdateForTimeOfDay(timeComp.TimeOfDay);
                }

                var tonemapping = globalEffects.GetEffect<ToneMappingEffect>();
                if (tonemapping != null && tonemapping.Enabled)
                {
                    tonemapping.UpdateForTimeOfDay(timeComp.TimeOfDay);
                }
            }

            // Also explicitly update EnvironmentSettings if auto-detection failed
            if (envSettings != null)
            {
                envSettings.TimeOfDay = timeComp.TimeOfDay;
                envSettings.DayOfYear = timeComp.DayOfYear;
                envSettings.Latitude = timeComp.Latitude;
                envSettings.UpdateCelestialBodies(timeComp.TimeOfDay, timeComp.DayOfYear, timeComp.Latitude);

                // Calculate and set ProceduralOverrides for skybox (same as TimeComponent.UpdateEnvironmentSettings)
                try
                {
                    float dayNight = timeComp.GetDayNightBlend();
                    float golden = timeComp.GetGoldenHourBlend();
                    bool isMorning = timeComp.TimeOfDay < 12.0f;

                    // Lerp between night and day sky/ground colors
                    var baseSky = LerpVec3(timeComp.NightSkyTint, timeComp.DaySkyTint, dayNight);
                    var baseGround = LerpVec3(timeComp.NightGroundColor, timeComp.DayGroundColor, dayNight);
                    float baseAt = OpenTK.Mathematics.MathHelper.Lerp(timeComp.NightAtmosphereThickness, timeComp.DayAtmosphereThickness, dayNight);

                    // Golden hour blending
                    var goldenTargetSky = isMorning ? timeComp.DawnSkyTint : timeComp.DuskSkyTint;
                    var finalSky = LerpVec3(baseSky, goldenTargetSky, golden);
                    var finalGround = LerpVec3(baseGround, goldenTargetSky, golden);
                    float finalAt = OpenTK.Mathematics.MathHelper.Lerp(baseAt, timeComp.DawnDuskAtmosphereThickness, golden);

                    // Sun/Moon size blending
                    float baseSunSize = OpenTK.Mathematics.MathHelper.Lerp(timeComp.NightMoonSize, timeComp.DaySunSize, dayNight);
                    float baseConvergence = OpenTK.Mathematics.MathHelper.Lerp(timeComp.NightMoonConvergence, timeComp.DaySunConvergence, dayNight);
                    float finalSunSize = OpenTK.Mathematics.MathHelper.Lerp(baseSunSize, timeComp.DawnDuskSunSize, golden);
                    float finalConvergence = OpenTK.Mathematics.MathHelper.Lerp(baseConvergence, timeComp.DawnDuskSunConvergence, golden);

                    var overrides = new Engine.Components.ProceduralSkyboxParameters
                    {
                        SkyTint = new Vector3(finalSky.X, finalSky.Y, finalSky.Z),
                        GroundColor = new Vector3(finalGround.X, finalGround.Y, finalGround.Z),
                        AtmosphereThickness = finalAt,
                        Exposure = 0.0f,
                        SunSize = finalSunSize,
                        SunSizeConvergence = finalConvergence
                    };
                    envSettings.ProceduralOverrides = overrides;
                }
                catch { }
            }
        }
    }

    // Helper to lerp System.Numerics.Vector3 (same as TimeComponent)
    private static System.Numerics.Vector3 LerpVec3(System.Numerics.Vector3 a, System.Numerics.Vector3 b, float t)
    {
        t = Math.Clamp(t, 0.0f, 1.0f);
        return new System.Numerics.Vector3(
            a.X + (b.X - a.X) * t,
            a.Y + (b.Y - a.Y) * t,
            a.Z + (b.Z - a.Z) * t
        );
    }

    private void UpdateCameraMatrices()
    {
        if (_scene == null || _mainCamera == null) return;

        var entity = _mainCamera.Entity;
        if (entity == null) return;

        entity.GetWorldTRS(out var worldPos, out _, out _);
        _cameraPosition = worldPos;

        _viewMatrix = _mainCamera.ViewMatrix;

        float aspect = (float)_width / _height;
        _projectionMatrix = _mainCamera.ProjectionMatrix(aspect);
    }

    private struct SceneData
    {
        public Vector3 lightDir;
        public Vector3 lightColor;
        public float lightIntensity;
        public Vector3 ambientColor;
        public bool fogEnabled;
        public Vector3 fogColor;
        public float fogStart;
        public float fogEnd;
        public float fogDensity;
        public float fogOpacity;
        public WeatherComponent? weatherComp;
        public TimeComponent? timeComp;
        public EnvironmentSettings? envSettings;
        public Engine.Scene.LightingState? lightingState;
    }

    private SceneData GatherSceneData()
    {
        var data = new SceneData
        {
            lightDir = new Vector3(0.5f, -1.0f, 0.3f),
            lightColor = new Vector3(1.0f, 0.95f, 0.9f),
            lightIntensity = 1.0f,
            ambientColor = new Vector3(0.3f, 0.35f, 0.4f),
            fogEnabled = false,
            fogColor = new Vector3(0.7f, 0.7f, 0.8f),
            fogStart = 50f,
            fogEnd = 300f,
            fogDensity = 0.01f,
            fogOpacity = 1.0f
        };

        if (_scene == null) return data;

        // Use Lighting.Build() to get proper lighting state (respects sun/moon from TimeComponent)
        // This is the same approach the editor uses
        var lighting = Engine.Scene.Lighting.Build(_scene);
        data.lightingState = lighting;

        // CRITICAL: Set SkyboxRenderer.CurrentLightingState so skybox procedural shader updates
        // This is what makes the skybox respond to time of day changes
        SkyboxRenderer.CurrentLightingState = lighting;

        // Use lighting state values
        if (lighting.HasDirectional)
        {
            data.lightDir = lighting.DirDirection;
            // Multiply color by intensity (same as original code did)
            data.lightColor = lighting.DirColor * lighting.DirIntensity;
            data.lightIntensity = lighting.DirIntensity;
        }

        // Ambient from lighting state
        data.ambientColor = lighting.AmbientColor * lighting.AmbientIntensity;

        // Fog from lighting state (which reads from WeatherComponent)
        data.fogEnabled = lighting.FogEnabled;
        data.fogColor = lighting.FogColor;
        data.fogStart = lighting.FogStart;
        data.fogEnd = lighting.FogEnd;
        data.fogDensity = lighting.FogDensity;
        data.fogOpacity = lighting.FogOpacity;

        // Also gather components for direct access
        foreach (var entity in _scene.Entities.Where(e => e.Active))
        {
            if (data.weatherComp == null)
                data.weatherComp = entity.GetComponent<WeatherComponent>();

            if (data.timeComp == null)
                data.timeComp = entity.GetComponent<TimeComponent>();

            if (data.envSettings == null)
                data.envSettings = entity.GetComponent<EnvironmentSettings>();
        }

        return data;
    }

    #endregion

    #region Render Passes

    private void RenderShadowPass(Vector3 lightDir)
    {
        if (_shadowManager == null || _scene == null) return;

        try
        {
            Vector3 sceneCenter = _cameraPosition;
            float sceneRadius = _shadowDistance;

            _shadowManager.CalculateLightMatrix(lightDir, sceneCenter, sceneRadius);
            _shadowManager.BeginShadowPass();

            if (_vegetationRenderer != null)
            {
                float time = (float)_timeStopwatch.Elapsed.TotalSeconds;
                _vegetationRenderer.RenderShadowPass(_shadowManager.LightSpaceMatrix, _cameraPosition, time);
            }

            _shadowManager.EndShadowPass();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BuildRenderer] Shadow pass error: {ex.Message}");
        }
    }

    private void RenderSkybox(EnvironmentSettings? env)
    {
        if (_skyboxRenderer == null || env == null) return;

        try
        {
            var tint = new Vector3(env.SkyboxTint.X, env.SkyboxTint.Y, env.SkyboxTint.Z);
            float exposure = env.SkyboxExposure;

            if (!string.IsNullOrEmpty(env.SkyboxMaterialPath))
            {
                string? resolvedPath = null;
                if (Guid.TryParse(env.SkyboxMaterialPath, out Guid skyboxGuid))
                {
                    if (AssetDatabase.TryGet(skyboxGuid, out var assetRecord))
                        resolvedPath = assetRecord.Path;
                }
                else
                {
                    resolvedPath = env.SkyboxMaterialPath;
                }

                if (!string.IsNullOrEmpty(resolvedPath) && System.IO.File.Exists(resolvedPath))
                {
                    var skyboxMat = SkyboxMaterialAsset.Load(resolvedPath);
                    if (skyboxMat != null)
                    {
                        // CRITICAL: For procedural skyboxes, use the parameters from TimeComponent
                        // This includes DaySkyTint, NightSkyTint, etc. configured in the inspector
                        if (skyboxMat.Type == Engine.Assets.SkyboxType.Procedural)
                        {
                            var proc = env.GetProceduralSkyboxParameters();
                            _skyboxRenderer.RenderProceduralWithParams(_viewMatrix, _projectionMatrix, proc, tint, exposure);
                        }
                        else
                        {
                            _skyboxRenderer.RenderWithMaterial(_viewMatrix, _projectionMatrix, skyboxMat, tint, exposure);
                        }
                        return;
                    }
                }
            }

            _skyboxRenderer.Render(_viewMatrix, _projectionMatrix, tint, exposure);
        }
        catch (Exception ex)
        {
            if (_skyboxDebugOnce) { Console.WriteLine($"[BuildRenderer] Skybox error: {ex.Message}"); _skyboxDebugOnce = false; }
        }
    }

    private void RenderClouds(SceneData sceneData)
    {
        if (_cloudRenderer == null || _scene == null) return;

        try
        {
            _cloudRenderer.Render(_scene, _cameraPosition, _viewMatrix, _projectionMatrix);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BuildRenderer] Cloud error: {ex.Message}");
        }
    }

    #region Starfield Rendering

    private void InitializeStarfield(int count, EnvironmentSettings env)
    {
        if (_starsInitialized && _starCount == count) return;

        // Dispose previous
        if (_starVao != 0) { GL.DeleteVertexArray(_starVao); _starVao = 0; }
        if (_starVbo != 0) { GL.DeleteBuffer(_starVbo); _starVbo = 0; }

        _starCount = Math.Max(0, count);
        if (_starCount == 0) { _starsInitialized = true; return; }

        Console.WriteLine($"[BuildRenderer] Initializing {_starCount} stars");

        // Generate interleaved buffer: vec3 pos, vec3 color
        float[] data = new float[_starCount * 6];
        for (int i = 0; i < _starCount; i++)
        {
            // Uniform direction on sphere
            float theta = (float)(_starRng.NextDouble() * 2.0 * Math.PI);
            float phi = (float)Math.Acos(2.0 * _starRng.NextDouble() - 1.0);
            float x = (float)(Math.Sin(phi) * Math.Cos(theta));
            float y = (float)(Math.Sin(phi) * Math.Sin(theta));
            float z = (float)Math.Cos(phi);

            // Only upper hemisphere for stars (y > 0)
            y = Math.Abs(y);

            // Position at a large distance (just direction really, scaled)
            float dist = 900f + (float)_starRng.NextDouble() * 100f;
            data[i * 6 + 0] = x * dist;
            data[i * 6 + 1] = y * dist;
            data[i * 6 + 2] = z * dist;

            // Star color: mostly white with slight color variation
            float brightness = 0.5f + (float)_starRng.NextDouble() * 0.5f;
            float colorVariation = (float)_starRng.NextDouble() * 0.15f;
            data[i * 6 + 3] = brightness * (1.0f - colorVariation + (float)_starRng.NextDouble() * colorVariation * 2f);
            data[i * 6 + 4] = brightness * (1.0f - colorVariation + (float)_starRng.NextDouble() * colorVariation * 2f);
            data[i * 6 + 5] = brightness * (1.0f + (float)_starRng.NextDouble() * colorVariation); // Slightly blue-ish
        }

        _starVao = GL.GenVertexArray();
        _starVbo = GL.GenBuffer();
        GL.BindVertexArray(_starVao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _starVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, data.Length * sizeof(float), data, BufferUsageHint.StaticDraw);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 0);
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 3 * sizeof(float));
        GL.BindVertexArray(0);

        // Load star shader
        if (_starShader == null)
        {
            try
            {
                _starShader = ShaderProgram.FromFiles(
                    "Engine/Rendering/Shaders/Effects/starfield.vert",
                    "Engine/Rendering/Shaders/Effects/starfield.frag"
                );
                Console.WriteLine("[BuildRenderer] Starfield shader loaded");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BuildRenderer] Failed to load starfield shader: {ex.Message}");
            }
        }

        _starsInitialized = true;
    }

    private void RenderStarfield(SceneData sceneData)
    {
        if (sceneData.envSettings == null || !sceneData.envSettings.ShowStars) return;

        InitializeStarfield(sceneData.envSettings.StarCount, sceneData.envSettings);
        if (_starCount == 0 || _starShader == null) return;

        // Calculate night fade based on day/night blend
        float dayNightBlend = sceneData.envSettings.GetDayNightBlend(sceneData.envSettings.TimeOfDay);
        float nightFade = 1.0f - dayNightBlend; // Invert: stars visible at night

        // Early exit if stars are completely invisible (full daylight)
        if (nightFade < 0.01f) return;

        // Render stars: don't write depth and render at far plane
        GL.DepthMask(false);
        GL.DepthFunc(DepthFunction.Lequal);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One); // Additive blending for stars
        GL.Enable(EnableCap.ProgramPointSize);

        _starShader.Use();

        // Remove translation from view matrix (stars at infinity)
        var viewNoTranslate = _viewMatrix;
        viewNoTranslate.M41 = 0; viewNoTranslate.M42 = 0; viewNoTranslate.M43 = 0;

        _starShader.SetMat4("uView", viewNoTranslate);
        _starShader.SetMat4("uProj", _projectionMatrix);
        _starShader.SetFloat("uTime", (float)DateTime.Now.TimeOfDay.TotalSeconds);
        _starShader.SetFloat("uTwinkle", sceneData.envSettings.StarTwinkle);
        _starShader.SetFloat("uNightFade", nightFade);
        _starShader.SetFloat("uPointSize", sceneData.envSettings.StarSize);

        // Compute star angle for rotation
        float starAngle = 0f;
        if (sceneData.envSettings.StarRotation)
        {
            if (sceneData.envSettings.StarFollowTime)
            {
                starAngle = (sceneData.envSettings.TimeOfDay / 24.0f) * 360.0f;
            }
            else
            {
                starAngle = (float)(_timeStopwatch.Elapsed.TotalSeconds * 2.0) % 360f;
            }
        }
        _starShader.SetFloat("uStarAngle", starAngle);

        GL.BindVertexArray(_starVao);
        GL.DrawArrays(PrimitiveType.Points, 0, _starCount);
        GL.BindVertexArray(0);

        // Restore state
        GL.Disable(EnableCap.ProgramPointSize);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.DepthMask(true);
        GL.DepthFunc(DepthFunction.Less);
    }

    #endregion

    private void RenderTerrains(Vector3 lightDir, Vector3 lightColor)
    {
        if (_scene == null || _terrainRenderer == null)
        {
            if (_debugFrameCount <= 3) Console.WriteLine($"[BuildRenderer] RenderTerrains: scene={_scene != null}, terrainRenderer={_terrainRenderer != null}");
            return;
        }

        int terrainCount = 0;
        foreach (var entity in _scene.Entities.Where(e => e.Active))
        {
            var terrain = entity.GetComponent<Engine.Components.Terrain>();
            if (terrain == null) continue;

            terrainCount++;
            if (_debugFrameCount <= 3)
            {
                Console.WriteLine($"[BuildRenderer] Found terrain '{entity.Name}': VAO={terrain.VAO}, IndexCount={terrain.IndexCount}");
            }

            try
            {
                entity.GetModelAndNormalMatrix(out var terrainModel, out _);

                int shadowTex = _shadowManager?.ShadowTexture ?? 0;
                Matrix4 shadowMatrix = _shadowManager?.LightSpaceMatrix ?? Matrix4.Identity;
                bool shadowsEnabled = _shadowsEnabled && _shadowManager != null;

                _terrainRenderer.RenderTerrain(
                    terrain, _viewMatrix, _projectionMatrix, _cameraPosition, lightDir, lightColor,
                    false, 0, 1.0f, new Vector2(_width, _height),
                    shadowsEnabled, shadowTex, shadowMatrix, _shadowBias, _shadowMapSize, _shadowStrength,
                    terrainModel
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BuildRenderer] Terrain error: {ex.Message}");
            }
        }

        if (_debugFrameCount <= 3)
        {
            Console.WriteLine($"[BuildRenderer] RenderTerrains: rendered {terrainCount} terrain(s)");
        }
    }

    private void RenderGrassAndRocks(Vector3 lightDir, Vector3 lightColor, Vector3 ambientColor)
    {
        if (_scene == null || _grassRenderer == null) return;

        int shadowTex = _shadowManager?.ShadowTexture ?? 0;
        Matrix4 shadowMatrix = _shadowManager?.LightSpaceMatrix ?? Matrix4.Identity;
        bool shadowsEnabled = _shadowsEnabled && _shadowManager != null;

        foreach (var entity in _scene.Entities.Where(e => e.Active))
        {
            var terrain = entity.GetComponent<Engine.Components.Terrain>();
            if (terrain?.VegetationLayers == null) continue;

            try
            {
                entity.GetModelAndNormalMatrix(out var terrainModel, out _);

                Matrix3 normalMat;
                try
                {
                    var invTranspose = Matrix4.Transpose(Matrix4.Invert(terrainModel));
                    normalMat = new Matrix3(
                        invTranspose.M11, invTranspose.M12, invTranspose.M13,
                        invTranspose.M21, invTranspose.M22, invTranspose.M23,
                        invTranspose.M31, invTranspose.M32, invTranspose.M33);
                }
                catch { normalMat = Matrix3.Identity; }

                // Register grass layers
                _grassRenderer.ClearAll();

                // Check if terrain actually has streaming tiles loaded
                bool hasStreamingTiles = _terrainRenderer?.HasLoadedTiles() ?? false;
                bool isStreaming = terrain.Mode == Engine.Components.TerrainMode.InfiniteStreaming && hasStreamingTiles;
                float tileSize = terrain.StreamingTileSize;

                for (int vIdx = 0; vIdx < terrain.VegetationLayers.Length; vIdx++)
                {
                    var vLayer = terrain.VegetationLayers[vIdx];
                    if (vLayer == null || !vLayer.IsGrassLayer || vLayer.GrassProperties == null)
                        continue;

                    if (isStreaming && _terrainRenderer != null)
                    {
                        // INFINITE STREAMING MODE: Register grass per visible tile
                        float maxRenderDist = vLayer.GrassProperties.MaxRenderDistance;
                        int layerIdx = vIdx; // Capture for lambda

                        _terrainRenderer.ForEachRenderableTile((tileVao, tileIndexCount, tileX, tileY, tileSz) =>
                        {
                            // Compute tile center position (world space)
                            float tileCenterX = tileX * tileSz + tileSz * 0.5f;
                            float tileCenterZ = tileY * tileSz + tileSz * 0.5f;
                            var tileCenterPos = new Vector3(tileCenterX, 0, tileCenterZ);

                            // Distance culling
                            float dx = tileCenterPos.X - _cameraPosition.X;
                            float dz = tileCenterPos.Z - _cameraPosition.Z;
                            float distToTile = (float)Math.Sqrt(dx * dx + dz * dz);
                            if (maxRenderDist > 0 && distToTile > maxRenderDist + tileSz * 0.71f)
                                return;

                            // Register grass layer for this tile
                            // Tile vertices are already in world space, so use Identity matrix
                            int regIndex = -(layerIdx + 1) * 10000 + tileX * 100 + tileY;
                            _grassRenderer.RegisterGrassLayer(entity.Guid, regIndex, vLayer.GrassProperties,
                                tileVao, tileIndexCount, Matrix4.Identity, Matrix3.Identity, tileCenterPos);
                        }, tileSize);
                    }
                    else
                    {
                        // SINGLE TERRAIN MODE: Use terrain's VAO directly
                        int terrainVAO = terrain.VAO;
                        int terrainIndexCount = terrain.IndexCount;

                        if (terrainVAO > 0 && terrainIndexCount > 0)
                        {
                            int regIndex = -(vIdx + 1);
                            _grassRenderer.RegisterGrassLayer(entity.Guid, regIndex, vLayer.GrassProperties,
                                terrainVAO, terrainIndexCount, terrainModel, normalMat);
                        }
                    }
                }

                // Debug matrices
                if (_debugFrameCount <= 3)
                {
                    Console.WriteLine($"[BuildRenderer] Grass terrain mode: {terrain.Mode}, hasStreamingTiles={hasStreamingTiles}, isStreaming={isStreaming}");
                    Console.WriteLine($"[BuildRenderer] terrainModel: M41={terrainModel.M41:F2}, M42={terrainModel.M42:F2}, M43={terrainModel.M43:F2}");
                    Console.WriteLine($"[BuildRenderer] ViewMatrix: M41={_viewMatrix.M41:F2}, M42={_viewMatrix.M42:F2}, M43={_viewMatrix.M43:F2}");
                    Console.WriteLine($"[BuildRenderer] CameraPos: {_cameraPosition}");
                }

                // Re-bind Global UBO before grass rendering (critical!)
                GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, _globalUBO);

                // Render grass
                float time = (float)_timeStopwatch.Elapsed.TotalSeconds;
                _grassRenderer.Render(_cameraPosition, time, ambientColor, 1.0f,
                    shadowsEnabled, shadowTex, shadowMatrix, _shadowBias, _shadowMapSize, _shadowStrength);

                // Render rocks
                if (_rockRenderer != null)
                {
                    _rockRenderer.ClearAll();
                    for (int vIdx = 0; vIdx < terrain.VegetationLayers.Length; vIdx++)
                    {
                        var vLayer = terrain.VegetationLayers[vIdx];
                        if (vLayer == null || !vLayer.IsRockLayer || vLayer.RockProperties == null)
                            continue;

                        if (isStreaming && _terrainRenderer != null)
                        {
                            // INFINITE STREAMING MODE: Register rocks per visible tile
                            float maxRenderDist = vLayer.RockProperties.MaxRenderDistance;
                            int layerIdx = vIdx;

                            _terrainRenderer.ForEachRenderableTile((tileVao, tileIndexCount, tileX, tileY, tileSz) =>
                            {
                                float tileCenterX = tileX * tileSz + tileSz * 0.5f;
                                float tileCenterZ = tileY * tileSz + tileSz * 0.5f;
                                var tileCenterPos = new Vector3(tileCenterX, 0, tileCenterZ);

                                float dx = tileCenterPos.X - _cameraPosition.X;
                                float dz = tileCenterPos.Z - _cameraPosition.Z;
                                float distToTile = (float)Math.Sqrt(dx * dx + dz * dz);
                                if (maxRenderDist > 0 && distToTile > maxRenderDist + tileSz * 0.71f)
                                    return;

                                int regIndex = -(layerIdx + 1) * 10000 + tileX * 100 + tileY;
                                _rockRenderer.RegisterRockLayer(entity.Guid, regIndex, vLayer.RockProperties,
                                    tileVao, tileIndexCount, Matrix4.Identity, Matrix3.Identity, tileCenterPos);
                            }, tileSize);
                        }
                        else
                        {
                            // SINGLE TERRAIN MODE
                            int terrainVAO = terrain.VAO;
                            int terrainIndexCount = terrain.IndexCount;

                            if (terrainVAO > 0 && terrainIndexCount > 0)
                            {
                                int regIndex = -(vIdx + 1);
                                _rockRenderer.RegisterRockLayer(entity.Guid, regIndex, vLayer.RockProperties,
                                    terrainVAO, terrainIndexCount, terrainModel, normalMat);
                            }
                        }
                    }

                    // Get sun direction and color from scene data
                    Vector3 sunDir = new Vector3(0.5f, -1.0f, 0.3f);
                    Vector3 sunColor = new Vector3(1.0f, 0.95f, 0.9f);
                    foreach (var e in _scene.Entities.Where(x => x.Active))
                    {
                        var l = e.GetComponent<LightComponent>();
                        if (l != null && l.Enabled && l.Type == LightType.Directional)
                        {
                            sunDir = l.Direction;
                            sunColor = new Vector3(l.Color.X, l.Color.Y, l.Color.Z);
                            break;
                        }
                    }

                    _rockRenderer.Render(_cameraPosition, time, ambientColor, 1.0f,
                        sunDir, sunColor, 1.0f,
                        shadowsEnabled, shadowTex, shadowMatrix, _shadowBias, _shadowMapSize, _shadowStrength);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BuildRenderer] Grass/Rock error: {ex.Message}");
            }
        }
    }

    private void RenderMeshes(SceneData sceneData)
    {
        if (_scene == null) return;

        GL.UseProgram(_basicShader);

        GL.UniformMatrix4(GL.GetUniformLocation(_basicShader, "uView"), false, ref _viewMatrix);
        GL.UniformMatrix4(GL.GetUniformLocation(_basicShader, "uProjection"), false, ref _projectionMatrix);
        GL.Uniform3(GL.GetUniformLocation(_basicShader, "uLightDir"), sceneData.lightDir);
        GL.Uniform3(GL.GetUniformLocation(_basicShader, "uLightColor"), sceneData.lightColor);
        GL.Uniform3(GL.GetUniformLocation(_basicShader, "uAmbientColor"), sceneData.ambientColor);
        GL.Uniform3(GL.GetUniformLocation(_basicShader, "uViewPos"), _cameraPosition);
        GL.Uniform1(GL.GetUniformLocation(_basicShader, "uFogEnabled"), sceneData.fogEnabled ? 1 : 0);
        GL.Uniform3(GL.GetUniformLocation(_basicShader, "uFogColor"), sceneData.fogColor);
        GL.Uniform1(GL.GetUniformLocation(_basicShader, "uFogStart"), sceneData.fogStart);
        GL.Uniform1(GL.GetUniformLocation(_basicShader, "uFogEnd"), sceneData.fogEnd);

        foreach (var entity in _scene.Entities.Where(e => e.Active))
        {
            if (entity.HasComponent<Engine.Components.Terrain>()) continue;

            var meshRenderer = entity.GetComponent<MeshRendererComponent>();
            if (meshRenderer == null || !meshRenderer.HasMeshToRender()) continue;

            MeshData? meshData = null;

            if (meshRenderer.IsUsingCustomMesh())
            {
                meshData = LoadCustomMesh(meshRenderer.CustomMeshGuid!.Value);
            }

            if (meshData == null)
            {
                _meshCache.TryGetValue(meshRenderer.Mesh, out meshData);
            }

            if (meshData == null) continue;

            entity.GetWorldTRS(out var pos, out var rot, out var scale);
            var model = Matrix4.CreateScale(scale) * Matrix4.CreateFromQuaternion(rot) * Matrix4.CreateTranslation(pos);

            GL.UniformMatrix4(GL.GetUniformLocation(_basicShader, "uModel"), false, ref model);

            // Load material
            Guid materialGuid = GetMaterialGuid(meshRenderer);
            var materialAsset = AssetDatabase.LoadMaterial(materialGuid);
            MaterialRuntime? matRuntime = null;

            if (materialAsset != null)
            {
                matRuntime = MaterialRuntime.FromAsset(materialAsset, guid =>
                {
                    if (AssetDatabase.TryGet(guid, out var rec)) return rec.Path;
                    return null;
                });
            }

            if (matRuntime != null)
            {
                if (matRuntime.AlbedoTex != 0 && matRuntime.AlbedoTex != TextureCache.White1x1)
                {
                    GL.ActiveTexture(TextureUnit.Texture0);
                    GL.BindTexture(TextureTarget.Texture2D, matRuntime.AlbedoTex);
                    GL.Uniform1(GL.GetUniformLocation(_basicShader, "uTexture"), 0);
                    GL.Uniform1(GL.GetUniformLocation(_basicShader, "uHasTexture"), 1);
                }
                else
                {
                    GL.Uniform1(GL.GetUniformLocation(_basicShader, "uHasTexture"), 0);
                }

                GL.Uniform3(GL.GetUniformLocation(_basicShader, "uColor"),
                    matRuntime.AlbedoColor[0], matRuntime.AlbedoColor[1], matRuntime.AlbedoColor[2]);
            }
            else
            {
                GL.Uniform1(GL.GetUniformLocation(_basicShader, "uHasTexture"), 0);
                GL.Uniform3(GL.GetUniformLocation(_basicShader, "uColor"), 1f, 1f, 1f);
            }

            // Culling
            CullingMode cullMode = materialAsset != null ? (CullingMode)materialAsset.CullingMode : CullingMode.Back;
            ApplyCullingMode(cullMode);

            // Draw
            GL.BindVertexArray(meshData.VAO);
            if (meshData.EBO != 0)
                GL.DrawElements(PrimitiveType.Triangles, meshData.VertexCount, DrawElementsType.UnsignedInt, IntPtr.Zero);
            else
                GL.DrawArrays(PrimitiveType.Triangles, 0, meshData.VertexCount);

            RestoreDefaultCulling();
        }

        GL.BindVertexArray(0);
        GL.UseProgram(0);
    }

    private Guid GetMaterialGuid(MeshRendererComponent meshRenderer)
    {
        if (meshRenderer.MaterialGuid.HasValue && meshRenderer.MaterialGuid.Value != Guid.Empty)
            return meshRenderer.MaterialGuid.Value;

        if (meshRenderer.CustomMeshGuid.HasValue && meshRenderer.CustomMeshGuid.Value != Guid.Empty)
        {
            try
            {
                if (AssetDatabase.TryGet(meshRenderer.CustomMeshGuid.Value, out var rec))
                {
                    var meshAsset = MeshAsset.Load(rec.Path);
                    if (meshAsset?.MaterialGuids != null && meshAsset.MaterialGuids.Count > meshRenderer.SubmeshIndex &&
                        meshAsset.MaterialGuids[meshRenderer.SubmeshIndex].HasValue)
                    {
                        return meshAsset.MaterialGuids[meshRenderer.SubmeshIndex]!.Value;
                    }
                }
            }
            catch { }
        }

        return AssetDatabase.EnsureDefaultWhiteMaterial();
    }

    private void RenderVegetation(Vector3 lightDir, Vector3 lightColor, Vector3 ambientColor)
    {
        if (_vegetationRenderer == null || _scene == null) return;

        // CRITICAL: Update vegetation batches for Infinite Streaming mode
        // Tiles load/unload dynamically as player moves, so we must refresh batches each frame
        foreach (var entity in _scene.Entities.Where(e => e.Active))
        {
            var terrain = entity.GetComponent<Engine.Components.Terrain>();
            if (terrain != null && terrain.Mode == Engine.Components.TerrainMode.InfiniteStreaming)
            {
                if (terrain.VegetationLayers != null && terrain.VegetationLayers.Length > 0)
                {
                    try
                    {
                        OnTerrainVegetationRegenerated(terrain);
                    }
                    catch { }
                }
            }
        }

        // Debug: check batch count
        if (_debugFrameCount <= 3)
        {
            int batchCount = _vegetationRenderer.BatchCount;
            Console.WriteLine($"[BuildRenderer] RenderVegetation: batchCount={batchCount}");
        }

        // Re-bind Global UBO before vegetation rendering
        GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, _globalUBO);

        try
        {
            var weather = Engine.Systems.WeatherManager.GetCurrentWeather();
            var weatherWindDir = weather.GetWindDirection();
            var windDir = new Vector2(weatherWindDir.X, weatherWindDir.Y);

            float time = (float)_timeStopwatch.Elapsed.TotalSeconds;

            int shadowTex = _shadowManager?.ShadowTexture ?? 0;
            Matrix4? shadowMatrix = _shadowManager?.LightSpaceMatrix;
            bool shadowsEnabled = _shadowsEnabled && _shadowManager != null;

            _vegetationRenderer.Render(
                _viewMatrix, _projectionMatrix, time,
                weather.WindStrength, windDir, weather.WindSpeed, weather.WindGustiness,
                weather.BranchAmplitude, weather.BranchSpeed, weather.BranchTurbulence,
                weather.TrunkStiffness, weather.TrunkBendAmount,
                weather.LeafFlutter, weather.LeafFlutterSpeed,
                weather.RainIntensity, weather.SnowAccumulation, weather.SnowIntensity, weather.Wetness,
                weather.SnowSlopeMin, weather.SnowSlopeMax, weather.SnowSparkle, weather.SnowDisplacement,
                _cameraPosition, lightDir, lightColor, ambientColor,
                0, shadowTex, shadowMatrix, shadowsEnabled,
                _shadowBias, _shadowMapSize, _shadowStrength,
                0, 0.001f, 1, 1.0f
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BuildRenderer] Vegetation error: {ex.Message}");
        }
    }

    private float FindWaterPlaneComponentHeight()
    {
        if (_scene == null) return float.MinValue;

        foreach (var entity in _scene.Entities.Where(e => e.Active))
        {
            var water = entity.GetComponent<WaterPlaneComponent>();
            if (water != null)
            {
                entity.GetWorldTRS(out var pos, out _, out _);
                return pos.Y;
            }
        }
        return float.MinValue;
    }

    // Z-flip matrix for coordinate system conversion (same as editor)
    private static readonly Matrix4 ZFlip = Matrix4.CreateScale(1f, 1f, -1f);

    /// <summary>
    /// Left-handed look-at matrix (same as editor)
    /// </summary>
    private static Matrix4 LookAtLH(in Vector3 eye, in Vector3 target, in Vector3 up)
    {
        var f = Vector3.Normalize(target - eye);
        var s = Vector3.Normalize(Vector3.Cross(up, f));
        var u = Vector3.Cross(f, s);
        return new Matrix4(
            new Vector4(s.X, u.X, f.X, 0f),
            new Vector4(s.Y, u.Y, f.Y, 0f),
            new Vector4(s.Z, u.Z, f.Z, 0f),
            new Vector4(-Vector3.Dot(s, eye), -Vector3.Dot(u, eye), -Vector3.Dot(f, eye), 1f)
        );
    }

    private void RenderReflectionPass(float waterLevel, SceneData sceneData)
    {
        if (_reflectionFbo == 0) return;

        // Save current state
        var savedView = _viewMatrix;
        var savedProj = _projectionMatrix;
        var savedCamPos = _cameraPosition;

        // Bind reflection FBO
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _reflectionFbo);
        GL.Viewport(0, 0, _reflectionW, _reflectionH);
        GL.ClearColor(0.5f, 0.7f, 0.9f, 1f);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        // Reflect camera position across water plane
        Vector3 reflectedCamPos = _cameraPosition;
        reflectedCamPos.Y = 2.0f * waterLevel - _cameraPosition.Y;

        // Extract view direction from view matrix (same as editor PlayMode)
        var invView = _viewMatrix.Inverted();
        Vector3 forward = new Vector3(-invView.M31, -invView.M32, -invView.M33);
        Vector3 viewDir = Vector3.Normalize(forward);

        // Reflect ONLY the Y component of view direction
        Vector3 reflectedViewDir = new Vector3(viewDir.X, -viewDir.Y, viewDir.Z);

        // Calculate new target from reflected position and direction
        Vector3 reflectedTarget = reflectedCamPos + reflectedViewDir * 100.0f;

        // Create view matrix for reflection using the same LH + Z-flip convention
        // as the editor to keep matrix handedness consistent
        var reflectedViewLH = LookAtLH(reflectedCamPos, reflectedTarget, Vector3.UnitY);
        Matrix4 reflectedView = reflectedViewLH * ZFlip;

        // Use the main camera projection (same as editor PlayMode)
        Matrix4 reflectedProj = _projectionMatrix;

        // Set up clip plane to prevent geometry below water from appearing in reflections
        // Clip plane equation: dot(worldPos, vec4(0, 1, 0, -waterLevel)) < 0
        // Note: Small offset (0.05) applied in shader to avoid artifacts at water surface

        // Enable clipping
        GL.Enable(EnableCap.ClipDistance0);

        // Set up culling for reflected camera (flip winding for Y-mirrored reflection)
        GL.Enable(EnableCap.CullFace);
        GL.CullFace(TriangleFace.Back);
        GL.FrontFace(FrontFaceDirection.Cw);

        // Render skybox into reflection
        RenderSkybox(sceneData.envSettings);

        // Render terrain into reflection
        if (_terrainRenderer != null && _scene != null)
        {
            foreach (var entity in _scene.Entities.Where(e => e.Active))
            {
                var terrain = entity.GetComponent<Engine.Components.Terrain>();
                if (terrain == null) continue;

                try
                {
                    entity.GetModelAndNormalMatrix(out var terrainModel, out _);
                    _terrainRenderer.RenderTerrain(
                        terrain, reflectedView, reflectedProj, reflectedCamPos,
                        sceneData.lightDir, sceneData.lightColor,
                        false, 0, 1.0f, new Vector2(_reflectionW, _reflectionH),
                        false, 0, Matrix4.Identity, 0, 0, 0,
                        terrainModel
                    );
                }
                catch { }
            }
        }

        // Restore culling state
        GL.FrontFace(FrontFaceDirection.Ccw);
        GL.Disable(EnableCap.ClipDistance0);

        // CRITICAL: Set ReflectionBuffer for water shader to use
        ReflectionBuffer.ReflectionTexture = _reflectionTex;
        ReflectionBuffer.ReflectionViewProj = reflectedView * reflectedProj;

        // Restore state
        _viewMatrix = savedView;
        _projectionMatrix = savedProj;
        _cameraPosition = savedCamPos;

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
        GL.Viewport(0, 0, _width, _height);
    }

    private void RenderWater(WeatherComponent? weather, int reflectionTex)
    {
        if (_waterPlaneRenderer == null || _scene == null) return;

        // Check if scene has any water planes
        bool hasWater = _scene.Entities.Any(e => e.Active && e.GetComponent<WaterPlaneComponent>() != null);
        if (!hasWater) return;

        try
        {
            float time = (float)_timeStopwatch.Elapsed.TotalSeconds;

            // Get shadow parameters for specular occlusion
            int shadowTex = _shadowManager?.ShadowTexture ?? 0;
            Matrix4? shadowMatrix = _shadowManager?.LightSpaceMatrix;
            bool shadowsEnabled = _shadowsEnabled && _shadowManager != null && shadowTex > 0;

            // WaterPlaneRenderer renders all water planes in the scene
            // CRITICAL: Use _sceneColorTexture (copy of scene) instead of _colorTexture
            // to avoid read/write hazard (water reads for refraction while writing to _colorTexture)
            _waterPlaneRenderer.Render(_scene, _viewMatrix, _projectionMatrix, _cameraPosition,
                time, weather, _depthTexture, _sceneColorTexture, reflectionTex,
                shadowsEnabled, shadowTex, shadowMatrix, _shadowBias, _shadowMapSize, _shadowStrength);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BuildRenderer] Water error: {ex.Message}");
        }
    }

    private void RenderParticles()
    {
        if (_particleRenderer == null || _scene == null) return;

        try
        {
            _particleRenderer.RenderParticleSystems(_scene, _viewMatrix, _projectionMatrix, _cameraPosition);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BuildRenderer] Particle error: {ex.Message}");
        }
    }

    // Track final post-process result texture (separate from _colorTexture which is the main FBO)
    private int _finalPostProcessTexture = 0;

    // Track if SSAO was applied before transparents (to skip in post-processing)
    private bool _ssaoAppliedBeforeTransparents = false;

    /// <summary>
    /// Apply SSAO BEFORE transparent rendering so transparent objects render on top of SSAO.
    /// This matches the editor's behavior in ViewportRenderer.ApplySSAOBeforeTransparents().
    /// </summary>
    private void ApplySSAOBeforeTransparents()
    {
        _ssaoAppliedBeforeTransparents = false;
        if (_scene == null || _colorTexture <= 0 || _width <= 0 || _height <= 0) return;

        try
        {
            // Find SSAO effect
            SSAOEffect? ssaoEffect = null;
            IPostProcessRenderer? ssaoRenderer = null;

            foreach (var entity in _scene.Entities)
            {
                if (!entity.Active) continue;
                var globalEffects = entity.GetComponent<GlobalEffects>();
                if (globalEffects == null || !globalEffects.Enabled) continue;

                foreach (var effect in globalEffects.Effects.Where(e => e?.Enabled == true))
                {
                    if (effect is SSAOEffect ssao)
                    {
                        ssaoEffect = ssao;
                        if (PostProcessManager.TryGetRenderer(effect.GetType(), out var renderer))
                        {
                            ssaoRenderer = renderer;
                        }
                        break;
                    }
                }
                if (ssaoEffect != null) break;
            }

            // If no SSAO effect, just copy _colorTexture to _sceneColorTexture without SSAO
            if (ssaoEffect == null || ssaoRenderer == null)
            {
                if (_sceneColorTexture != 0 && _colorTexture != 0)
                {
                    try
                    {
                        GL.CopyImageSubData(
                            _colorTexture, ImageTarget.Texture2D, 0, 0, 0, 0,
                            _sceneColorTexture, ImageTarget.Texture2D, 0, 0, 0, 0,
                            _width, _height, 1
                        );
                    }
                    catch { }
                }
                return;
            }

            // Apply SSAO: _colorTexture -> _postTex
            if (_postFbo != 0 && _postTex != 0)
            {
                var context = new PostProcessContext(
                    (uint)_colorTexture,
                    (uint)_postFbo,
                    _width, _height,
                    0.016f,
                    _scene
                );

                // Add depth texture and matrices for SSAO
                context.DepthTexture = (uint)_depthTexture;
                context.ProjectionMatrix = _projectionMatrix;
                context.ViewMatrix = _viewMatrix;

                try
                {
                    GL.BindFramebuffer(FramebufferTarget.Framebuffer, _postFbo);
                    GL.Viewport(0, 0, _width, _height);
                    GL.Disable(EnableCap.DepthTest);
                    GL.Disable(EnableCap.Blend);
                    GL.Disable(EnableCap.CullFace);

                    GL.BindVertexArray(_fullscreenQuadVAO);
                    ssaoRenderer.Render(ssaoEffect, context);
                }
                catch { }
                finally
                {
                    GL.BindVertexArray(0);
                }

                // Copy _postTex (with SSAO) -> _sceneColorTexture (for water refraction)
                if (_sceneColorTexture != 0)
                {
                    try
                    {
                        GL.CopyImageSubData(
                            _postTex, ImageTarget.Texture2D, 0, 0, 0, 0,
                            _sceneColorTexture, ImageTarget.Texture2D, 0, 0, 0, 0,
                            _width, _height, 1
                        );
                    }
                    catch { }
                }

                // Copy _postTex (with SSAO) -> _colorTexture (to continue rendering transparents on top)
                try
                {
                    GL.CopyImageSubData(
                        _postTex, ImageTarget.Texture2D, 0, 0, 0, 0,
                        _colorTexture, ImageTarget.Texture2D, 0, 0, 0, 0,
                        _width, _height, 1
                    );
                }
                catch { }

                // Restore main FBO for transparent rendering
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
                GL.Viewport(0, 0, _width, _height);
                GL.Enable(EnableCap.DepthTest);

                _ssaoAppliedBeforeTransparents = true;

                if (_debugFrameCount <= 3)
                    Console.WriteLine("[BuildRenderer] SSAO applied before transparents");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BuildRenderer] ApplySSAOBeforeTransparents error: {ex.Message}");
        }
    }

    private void ApplyPostProcessing(float deltaTime)
    {
        if (_scene == null) return;

        // Bypass post-processing entirely if requested (for debugging)
        if (_bypassPostProcessing)
        {
            if (_debugFrameCount <= 3)
                Console.WriteLine($"[BuildRenderer] Post-processing BYPASSED, using color texture directly");
            _finalPostProcessTexture = _colorTexture;
            return;
        }

        // If post-process FBOs aren't set up, just use the color texture directly
        if (_postFbo == 0 || _postTex == 0 || _postFbo2 == 0 || _postTex2 == 0)
        {
            if (_debugFrameCount <= 3)
                Console.WriteLine($"[BuildRenderer] Post-process FBOs not ready, using color texture directly");
            _finalPostProcessTexture = _colorTexture;
            return;
        }

        try
        {
            // Collect all active effects and their renderers (like the editor does)
            var allEffects = new List<(PostProcessEffect effect, IPostProcessRenderer renderer)>();

            foreach (var entity in _scene.Entities)
            {
                if (!entity.Active) continue;
                var globalEffects = entity.GetComponent<GlobalEffects>();
                if (globalEffects == null || !globalEffects.Enabled) continue;

                foreach (var effect in globalEffects.Effects.Where(e => e?.Enabled == true).OrderBy(e => e?.Priority ?? 0))
                {
                    if (effect == null) continue;

                    // CRITICAL: Skip SSAO if it was already applied before transparent rendering
                    // This prevents SSAO from being applied twice and showing through water
                    if (_ssaoAppliedBeforeTransparents && effect is SSAOEffect)
                    {
                        if (_debugFrameCount <= 3)
                            Console.WriteLine("[BuildRenderer] Skipping SSAO in post-processing (already applied before transparents)");
                        continue;
                    }

                    // Get renderer for this effect type
                    if (PostProcessManager.TryGetRenderer(effect.GetType(), out var renderer))
                    {
                        allEffects.Add((effect, renderer));
                    }
                }
            }

            if (_debugFrameCount <= 3)
                Console.WriteLine($"[BuildRenderer] Collected {allEffects.Count} post-process effects");

            // If no effects, just use color texture directly
            if (allEffects.Count == 0)
            {
                _finalPostProcessTexture = _colorTexture;
                return;
            }

            // Apply effects with ping-pong (same approach as editor)
            // Start by reading from _colorTexture (the rendered scene)
            int srcTex = _colorTexture;

            // Choose starting buffer based on effect count to ensure final result lands in _postFbo/_postTex
            // With odd number of effects: start with _postFbo (1st->_postFbo, 2nd->_postFbo2, 3rd->_postFbo)
            // With even number of effects: start with _postFbo2 (1st->_postFbo2, 2nd->_postFbo)
            int dstFbo = (allEffects.Count % 2 == 1) ? _postFbo : _postFbo2;
            int dstTex = (allEffects.Count % 2 == 1) ? _postTex : _postTex2;

            if (_debugFrameCount <= 3)
                Console.WriteLine($"[BuildRenderer] Starting ping-pong: effectCount={allEffects.Count}, startDstFbo={dstFbo}, startDstTex={dstTex}");

            for (int i = 0; i < allEffects.Count; i++)
            {
                var (effect, renderer) = allEffects[i];

                // Create context for this effect (fresh context per effect, like editor)
                var context = new PostProcessContext(
                    (uint)srcTex,
                    (uint)dstFbo,
                    _width, _height,
                    deltaTime,
                    _scene
                );

                // Add depth texture and matrices for effects that need them
                context.DepthTexture = (uint)_depthTexture;
                context.ProjectionMatrix = _projectionMatrix;
                context.ViewMatrix = _viewMatrix;

                if (_debugFrameCount <= 3)
                    Console.WriteLine($"[BuildRenderer] Effect {i} ({effect.GetType().Name}): src={srcTex}, dstFbo={dstFbo}, dstTex={dstTex}");

                // Bind target and render effect
                try
                {
                    GL.BindFramebuffer(FramebufferTarget.Framebuffer, dstFbo);
                    GL.Viewport(0, 0, _width, _height);

                    // Clear the target to ensure no artifacts from previous content
                    GL.ClearColor(0, 0, 0, 1);
                    GL.Clear(ClearBufferMask.ColorBufferBit);

                    // Ensure clean state for post-processing
                    GL.Disable(EnableCap.DepthTest);
                    GL.Disable(EnableCap.Blend);
                    GL.Disable(EnableCap.CullFace);

                    // Bind our fullscreen quad VAO (some renderers expect this)
                    GL.BindVertexArray(_fullscreenQuadVAO);

                    renderer.Render(effect, context);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[BuildRenderer] Post-effect error ({effect.GetType().Name}): {ex.Message}");
                }
                finally
                {
                    // Clean up GL state
                    try
                    {
                        GL.BindVertexArray(0);
                    }
                    catch { }
                }

                // Ping-pong: for next effect, read from what we just wrote
                if (i < allEffects.Count - 1)
                {
                    // Next effect reads from the texture we just wrote to
                    srcTex = dstTex;

                    // Swap buffers for next iteration
                    if (dstFbo == _postFbo)
                    {
                        dstFbo = _postFbo2;
                        dstTex = _postTex2;
                    }
                    else
                    {
                        dstFbo = _postFbo;
                        dstTex = _postTex;
                    }
                }
            }

            // Final result is in dstTex (what we last wrote to)
            _finalPostProcessTexture = dstTex;

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            if (_debugFrameCount <= 3)
                Console.WriteLine($"[BuildRenderer] PostProcess complete, final texture: {_finalPostProcessTexture}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BuildRenderer] Post-process error: {ex.Message}");
            Console.WriteLine($"[BuildRenderer] Stack: {ex.StackTrace}");
            _finalPostProcessTexture = _colorTexture;
        }
    }

    private void BlitToScreen()
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        GL.Viewport(0, 0, _width, _height);

        GL.Disable(EnableCap.DepthTest);

        // Use the post-processed result if available, otherwise use main color texture
        int textureToBlit = _finalPostProcessTexture != 0 ? _finalPostProcessTexture : _colorTexture;

        if (_debugFrameCount <= 3)
        {
            Console.WriteLine($"[BuildRenderer] BlitToScreen: textureToBlit={textureToBlit}, finalPostProcessTex={_finalPostProcessTexture}, colorTex={_colorTexture}");
        }

        GL.UseProgram(_blitShader);
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, textureToBlit);
        GL.Uniform1(GL.GetUniformLocation(_blitShader, "uTexture"), 0);

        GL.BindVertexArray(_fullscreenQuadVAO);
        GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, IntPtr.Zero);
        GL.BindVertexArray(0);

        GL.UseProgram(0);
        GL.Enable(EnableCap.DepthTest);
    }

    #endregion

    #region Dispose Override

    public override void Dispose()
    {
        if (_disposed) return;

        // Dispose build-specific resources
        if (_basicShader != 0) { GL.DeleteProgram(_basicShader); _basicShader = 0; }
        if (_blitShader != 0) { GL.DeleteProgram(_blitShader); _blitShader = 0; }
        if (_fullscreenQuadVAO != 0) { GL.DeleteVertexArray(_fullscreenQuadVAO); _fullscreenQuadVAO = 0; }
        if (_reflectionFbo != 0) { GL.DeleteFramebuffer(_reflectionFbo); _reflectionFbo = 0; }
        if (_reflectionTex != 0) { GL.DeleteTexture(_reflectionTex); _reflectionTex = 0; }
        if (_reflectionDepthRbo != 0) { GL.DeleteRenderbuffer(_reflectionDepthRbo); _reflectionDepthRbo = 0; }
        if (_sceneColorTexture != 0) { GL.DeleteTexture(_sceneColorTexture); _sceneColorTexture = 0; }
        _cloudRenderer?.Dispose();

        // Dispose starfield resources
        if (_starVao != 0) { GL.DeleteVertexArray(_starVao); _starVao = 0; }
        if (_starVbo != 0) { GL.DeleteBuffer(_starVbo); _starVbo = 0; }
        _starShader?.Dispose();

        // Call base dispose
        base.Dispose();

        Console.WriteLine("[BuildRenderer] Disposed");
    }

    #endregion
}

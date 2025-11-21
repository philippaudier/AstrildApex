using System;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Engine.Mathx;
using Engine.Scene;
using Editor.State;
using Editor.Panels;
using Engine.Components;
using Editor.Logging;


// en tête du fichier si absents :
using Engine.Assets;
using Engine.Rendering;

namespace Editor.Rendering
{
    public struct OrbitCameraState
    {
        public float Yaw;
        public float Pitch;
        public float Distance;
        public Vector3 Target;
    }

    public sealed class ViewportRenderer : IDisposable
    {
        // Simple static instance counter for diagnostics (helps detect leaks)
        private static int _instanceCount = 0;
        public static int InstanceCount => _instanceCount;

    // One-shot SSAO debug coordination is handled by ViewportRendererTypeSafeHelpers

        // Minimal UI renderer instance (mirrors GameRenderer behavior)
        private Engine.UI.UIRenderer? _uiRenderer;
        private Engine.UI.StandaloneInputModule? _inputModule;
        // Events for decoupling editing logic from rendering
        public event Action<CompositeAction>? GizmoDragEnded;
        public event Action? EditingTouched; // For TouchEdit calls

        private uint[] _pickBuffer = new uint[169];
        private readonly Dictionary<uint, int> _pickCount = new();
        // Ajoute ce champ dans la classe ViewportRenderer
        public bool ForceEditorCamera { get; set; } = true;

        // Material cache to avoid loading from disk every frame
        private readonly Dictionary<Guid, Engine.Rendering.MaterialRuntime> _materialCache = new();
    // When true, force re-binding of material uniforms on next render pass
    private volatile bool _forceMaterialRebind = false;

        // Custom mesh cache (for imported 3D models)
        // Key is (MeshGuid, SubmeshIndex)
        private readonly Dictionary<(Guid, int), CustomMeshData> _customMeshCache = new();

        // Time tracking for animated shaders (e.g., Water shader)
        private System.Diagnostics.Stopwatch _timeStopwatch = System.Diagnostics.Stopwatch.StartNew();
    // Frame timing instrumentation (lightweight)
    private System.Diagnostics.Stopwatch? _frameTimer = null;
    private float _lastFrameCpuMs = 0f;
    private float _lastShadowsMs = 0f;
    private float _lastOpaqueMs = 0f;
    private float _lastTransparentMs = 0f;
    private float _lastPostProcessMs = 0f;

    // Expose timing for overlays/logging
    public float LastFrameCpuMs => _lastFrameCpuMs;
    public float LastShadowsMs => _lastShadowsMs;
    public float LastOpaqueMs => _lastOpaqueMs;
    public float LastTransparentMs => _lastTransparentMs;
    public float LastPostProcessMs => _lastPostProcessMs;

        // === Batching & Performance ===
        //private readonly List<RenderBatch> _batches = new();
        //private readonly Dictionary<uint, CachedEntityData> _entityCache = new();
        //private int _frameCount = 0;

        // === Frustum Culling ===
        private readonly FrustumPlanes _frustum = new();
        private readonly List<Entity> _visibleEntities = new();

        // Reusable lists to avoid per-frame allocations for transparent item sorting
        private readonly System.Collections.Generic.List<RenderItem> _fbTransparentItems = new System.Collections.Generic.List<RenderItem>();
        private readonly System.Collections.Generic.List<RenderItem> _transparentItems = new System.Collections.Generic.List<RenderItem>();

    // === Render Scale (performance optimization for high-res displays) ===
    private float _renderScale = 1.0f; // 1.0 = native, 0.8 = 80% (big perf gain on 21:9/4K)
    private int _displayWidth = 1;  // Target display size (before scaling)
    private int _displayHeight = 1;
    private int _maxTexSize = 0; // GL_MAX_TEXTURE_SIZE (queried once)
    
    public float RenderScale 
    { 
        get => _renderScale; 
        set 
        { 
            value = Math.Clamp(value, 0.25f, 1.0f);
            if (Math.Abs(_renderScale - value) > 0.01f)
            {
                _renderScale = value;
                // Force resize with last known display size
                Resize(_displayWidth, _displayHeight);
            }
        } 
    }
    
    // === Debug/Gizmos toggles ===
    private bool _showColliderGizmos = true;
    public void SetColliderGizmosVisible(bool v) { _showColliderGizmos = v; }
        // Toggle horizontal grid rendering
        private bool _showGrid = true;
        public bool GridVisible
        {
            get => _showGrid;
            set
            {
                if (_showGrid == value) return;
                _showGrid = value;
                // Ensure grid resource is created/disposed on GL thread where this renderer runs
                if (!_showGrid)
                {
                    if (_grid != null)
                    {
                        _grid.Dispose();
                        _grid = null;
                    }
                }
                else
                {
                    if (_grid == null) _grid = new GridRenderer();
                }
                // Persist the choice
                Editor.State.EditorSettings.ShowGrid = _showGrid;
            }
        }

        // === GPU Resources pooling ===
        private readonly Dictionary<Guid, int> _materialUniformBuffers = new();
        private int _globalUBO = 0;
        private GlobalUniforms _globalUniforms = new();

    // === Terrain Rendering ===

        // === Optimized render data ===
       /*  private struct RenderBatch
        {
            public Guid MaterialGuid;
            //public int MaterialHandle;
            public List<Matrix4> Transforms;
            public List<uint> EntityIds;
            //public int UniformBuffer;
        } */
        
        private struct CachedEntityData
        {
            public Matrix4 LastWorldMatrix;
            public Guid LastMaterialGuid;
            public bool IsVisible;
            public BoundingSphere Bounds;
        }

    // Reflection update throttling (frames)
        // (removed unused frame counter fields to avoid compiler warnings)

    // Lightweight per-frame renderer stats (reset each frame by RenderScene)
    private int _frameDrawCalls = 0;
    private int _frameTriangles = 0;
    private int _frameRenderedObjects = 0;

    // Public read-only properties for debug overlay
    public int DrawCallsThisFrame => _frameDrawCalls;
    public int TrianglesThisFrame => _frameTriangles;
    public int RenderedObjectsThisFrame => _frameRenderedObjects;

        private struct FrustumPlanes
        {
            public Vector4 Left, Right, Top, Bottom, Near, Far;

            public void ExtractFromMatrix(Matrix4 mvp)
            {
                // Left
                Left = new Vector4(
                    mvp.M14 + mvp.M11,
                    mvp.M24 + mvp.M21,
                    mvp.M34 + mvp.M31,
                    mvp.M44 + mvp.M41);

                // Right
                Right = new Vector4(
                    mvp.M14 - mvp.M11,
                    mvp.M24 - mvp.M21,
                    mvp.M34 - mvp.M31,
                    mvp.M44 - mvp.M41);

                // Bottom
                Bottom = new Vector4(
                    mvp.M14 + mvp.M12,
                    mvp.M24 + mvp.M22,
                    mvp.M34 + mvp.M32,
                    mvp.M44 + mvp.M42);

                // Top
                Top = new Vector4(
                    mvp.M14 - mvp.M12,
                    mvp.M24 - mvp.M22,
                    mvp.M34 - mvp.M32,
                    mvp.M44 - mvp.M42);

                // Near
                Near = new Vector4(
                    mvp.M14 + mvp.M13,
                    mvp.M24 + mvp.M23,
                    mvp.M34 + mvp.M33,
                    mvp.M44 + mvp.M43);

                // Far
                Far = new Vector4(
                    mvp.M14 - mvp.M13,
                    mvp.M24 - mvp.M23,
                    mvp.M34 - mvp.M33,
                    mvp.M44 - mvp.M43);
            }
            
            public bool IsVisible(BoundingSphere sphere)
            {
                // Test sphere against all planes
                return TestPlane(Left, sphere) && TestPlane(Right, sphere) &&
                       TestPlane(Top, sphere) && TestPlane(Bottom, sphere) &&
                       TestPlane(Near, sphere) && TestPlane(Far, sphere);
            }
            
            private bool TestPlane(Vector4 plane, BoundingSphere sphere)
            {
                float distance = Vector3.Dot(new Vector3(plane.X, plane.Y, plane.Z), sphere.Center) + plane.W;
                return distance >= -sphere.Radius;
            }
        }
        
        private struct BoundingSphere
        {
            public Vector3 Center;
            public float Radius;
        }

        /// <summary>
        /// Calculate bounding sphere radius for an entity based on its mesh type and scale.
        /// </summary>
        private float CalculateEntityBoundsRadius(Entity entity, Vector3 scale)
        {
            var meshRenderer = entity.GetComponent<MeshRendererComponent>();
            if (meshRenderer == null) return 1.0f;

            // Base radius for each primitive mesh type (approximate)
            float baseRadius = meshRenderer.Mesh switch
            {
                MeshKind.Cube => 0.866f,      // sqrt(3)/2 for unit cube
                MeshKind.Sphere => 0.5f,      // Unit sphere
                MeshKind.Capsule => 1.0f,     // Capsule is taller
                MeshKind.Plane => 0.707f,     // sqrt(2)/2 for unit quad
                MeshKind.Quad => 0.707f,      // sqrt(2)/2 for unit quad
                _ => 1.0f
            };

            // Apply scale (use max component for conservative estimate)
            float maxScale = MathF.Max(MathF.Max(MathF.Abs(scale.X), MathF.Abs(scale.Y)), MathF.Abs(scale.Z));
            return baseRadius * maxScale;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct GlobalUniforms
        {
            public Matrix4 ViewMatrix;
            public Matrix4 ProjectionMatrix;
            public Matrix4 ViewProjectionMatrix;
            public Vector3 CameraPosition; private float _pad1;
            
            // Directional Light (main sun)
            public Vector3 DirLightDirection;  private float _pad2;
            public Vector3 DirLightColor;      public float DirLightIntensity;

            // Point Lights (max 4) - MOVED to match shader layout
            public int PointLightCount; private float _pad3; private float _pad4; private float _pad5;
            public Vector4 PointLightPos0; public Vector4 PointLightColor0;  // pos.xyz + range, color.rgb + intensity
            public Vector4 PointLightPos1; public Vector4 PointLightColor1;
            public Vector4 PointLightPos2; public Vector4 PointLightColor2;
            public Vector4 PointLightPos3; public Vector4 PointLightColor3;
            
            // Spot Lights (max 2 for now)
            public int SpotLightCount; private float _pad6; private float _pad7; private float _pad8;
            public Vector4 SpotLightPos0; public Vector4 SpotLightDir0; public Vector4 SpotLightColor0; public float SpotLightAngle0; public float SpotLightInnerAngle0; private float _pad9; private float _pad10;
            public Vector4 SpotLightPos1; public Vector4 SpotLightDir1; public Vector4 SpotLightColor1; public float SpotLightAngle1; public float SpotLightInnerAngle1; private float _pad11; private float _pad12;

            // Ambient / skybox tint - MOVED to end to match shader layout
            public Vector3 AmbientColor; public float AmbientIntensity;
            public Vector3 SkyboxTint; public float SkyboxExposure;

            // Fog settings - MOVED to end to match shader layout
            public int FogEnabled; private float _pad13; private float _pad14; private float _pad15;
            public Vector3 FogColor; public float FogStart;
            public float FogEnd; private Vector3 _pad16;

            // Clipping plane for water reflections
            public int ClipPlaneEnabled; private float _pad17; private float _pad18; private float _pad19;
            public Vector4 ClipPlane; // plane equation: normal.xyz, d
        }

        private struct RenderItem
        {
            public int Vao;
            public int Ebo;
            public int IndexCount;
            public Matrix4 Model;
            public Matrix3 NormalMat3;
            public Guid MaterialGuid;
            public Engine.Rendering.MaterialRuntime MaterialRuntime;
            public uint ObjectId;
            public MeshKind MeshType;  // Added to track mesh type for culling
        }

        public ViewportRenderer()
        {
            // Increment instance count (thread-safe)
            Interlocked.Increment(ref _instanceCount);
            try { if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[ViewportRenderer] Constructor: instances={_instanceCount}, this={this.GetHashCode()}"); } catch { }

            InitializeUBOs();
            // Initialize grid visibility from persisted settings
            try { _showGrid = Editor.State.EditorSettings.ShowGrid; } catch { _showGrid = true; }
            // Load persisted camera near/far from settings
            try
            {
                _nearClip = Editor.State.EditorSettings.CameraNear;
                _farClip = Editor.State.EditorSettings.CameraFar;
            }
            catch { /* ignore */ }

            // Load TAA settings from EditorSettings or use defaults (DISABLED by default)
            try
            {
                var loadedSettings = Editor.State.EditorSettings.TAASettings;
                // If the loaded settings have invalid feedback values, use defaults
                if (loadedSettings.FeedbackMin <= 0 || loadedSettings.FeedbackMax <= 0)
                {
                    _taaSettings = Engine.Rendering.PostProcess.TAARenderer.TAASettings.Default;
                    _taaSettings.Enabled = false; // Disable by default
                }
                else
                {
                    _taaSettings = loadedSettings;
                }
            }
            catch
            {
                _taaSettings = Engine.Rendering.PostProcess.TAARenderer.TAASettings.Default;
                _taaSettings.Enabled = false; // Disable by default
            }

            // Load Anti-Aliasing mode from EditorSettings (defaults to None)
            try
            {
                _antiAliasingMode = Editor.State.EditorSettings.AntiAliasingMode;
                // Ensure TAA enabled flag matches the selected AA mode at startup.
                // Previously we loaded TAA settings from disk which could have Enabled=true
                // even when AntiAliasingMode was None. Make the initial TAAEnabled follow
                // the current AntiAliasingMode so the renderer respects the selection.
                try
                {
                    _taaSettings.Enabled = (_antiAliasingMode == Engine.Rendering.AntiAliasingMode.TAA);
                }
                catch { /* ignore if _taaSettings not yet initialized */ }
            }
            catch
            {
                _antiAliasingMode = Engine.Rendering.AntiAliasingMode.None;
            }

            //Scene.EntityTransformChanged += OnEntityTransformChanged;

            // Initialize terrain renderer for specialized slope-based splatting
            // Note: Create TerrainRenderer later when OpenGL context is ready

            // Initialize UI renderer (best-effort; may throw if GL not ready)
            try
            {
                _uiRenderer = new Engine.UI.UIRenderer();
                _inputModule = new Engine.UI.StandaloneInputModule();
            }
            catch (Exception ex)
            {
                if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[ViewportRenderer] Failed to initialize UIRenderer: {ex.Message}");
                _uiRenderer = null;
                _inputModule = null;
            }
        }

        // Expose near/far so UI can bind to them
        public float NearClip
        {
            get => _nearClip;
            set
            {
                _nearClip = MathF.Max(0.0001f, value);
                // persist
                try { Editor.State.EditorSettings.CameraNear = _nearClip; } catch { }
            }
        }

        public float FarClip
        {
            get => _farClip;
            set
            {
                _farClip = MathF.Max(_nearClip + 0.001f, value);
                // persist
                try { Editor.State.EditorSettings.CameraFar = _farClip; } catch { }
            }
        }
        
        private void InitializeUBOs()
        {
            // Create global uniforms UBO
            _globalUBO = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.UniformBuffer, _globalUBO);
            GL.BufferData(BufferTarget.UniformBuffer, 
                System.Runtime.InteropServices.Marshal.SizeOf<GlobalUniforms>(), 
                IntPtr.Zero, BufferUsageHint.DynamicDraw);
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, _globalUBO);
        }
        
        /* private void OnEntityTransformChanged(Entity entity)
        {
            if (_entityCache.ContainsKey(entity.Id))
            {
                // TODO: Implement scene dirty tracking for render optimization
            }
        } */

        // 1. Ajoutez cette constante avec les autres ID constants
        // Note: ID_GIZMO_RESERVED maintenant défini dans EntityIdRange.GizmoReservedId
        // === Public/props ===
    public int Width => _w;
    public int Height => _h;
    public float Distance => _distance;
    public float DistanceGoal => _distanceGoal;
    public bool IsCameraAnimating => _camAnimating;
        public enum GizmoMode { Translate, Rotate, Scale }

        // === Scene ===
        private Scene _scene = new Scene();
        public Scene Scene => _scene;

        // Ajoute cette méthode publique dans ViewportRenderer :
        public void SetScene(Scene scene)
        {
            _scene = scene ?? throw new ArgumentNullException(nameof(scene));

            // Subscribe to material changes to invalidate cache (only once)
            if (!_subscribedToMaterialChanges)
            {
                Engine.Assets.AssetDatabase.MaterialSaved += OnMaterialSaved;
                _subscribedToMaterialChanges = true;
            }
        }

        private bool _subscribedToMaterialChanges = false;

        // === FBO ===
            private int _fbo, _colorTex, _idTex, _depthTex;
        // Post-process ping-pong targets to avoid reading/writing same texture
        private int _postFbo = 0, _postTex = 0;
        private int _postFbo2 = 0, _postTex2 = 0;  // Second buffer for ping-pong
        // Velocity buffer (camera motion) - generated each frame from depth
        private int _velocityFbo = 0, _velocityTex = 0;
        // Indicates whether the post texture / fbo are healthy and can be used as final target
        private bool _postTexHealthy = false;
        private int _w = 1, _h = 1;

        // Helper: Get the correct target FBO (MSAA or normal)
        private int GetTargetFBO()
        {
            return (_msaaRenderer != null && _antiAliasingMode.IsMSAA()) ? (int)_msaaRenderer.FramebufferId : _fbo;
        }
    
    // === Water Reflection FBO ===
    private int _reflectionFbo = 0, _reflectionTex = 0, _reflectionDepthRbo = 0;
    private int _reflectionW = 512, _reflectionH = 512;
    private float _reflectionResolutionScale = 0.5f; // 0.5 = half resolution for performance
    // Return the post-processed texture when available, otherwise the raw color texture
    public int ColorTexture => (_postTex != 0 && _postTexHealthy) ? _postTex : _colorTex;

    // === Shaders & géométrie ===
    private int _cubeVao, _cubeVbo, _cubeEbo;
    private int _sphereVao, _sphereVbo, _sphereEbo;
    private int _capsuleVao, _capsuleVbo, _capsuleEbo;
    private int _planeVao, _planeVbo, _planeEbo;
    private int _quadVao, _quadVbo, _quadEbo;

    private int _legacyCubeVao, _legacyCubeVbo; // For fallback rendering
    private int _legacySphereVao, _legacySphereVbo, _legacySphereEbo;
    private int _legacyCapsuleVao, _legacyCapsuleVbo, _legacyCapsuleEbo;
    private int _legacyPlaneVao, _legacyPlaneVbo, _legacyPlaneEbo;
    private int _legacyQuadVao, _legacyQuadVbo, _legacyQuadEbo;

    private int _sphereIndexCount = 0, _capsuleIndexCount = 0, _planeIndexCount = 0, _quadIndexCount = 0;
        
        // === Light icon geometry ===
        private int _lightIconVao, _lightIconVbo, _lightIconEbo;
        // Cube indices - CCW winding (standard OpenGL)
        private readonly uint[] _cubeIdx = new uint[]
        {
            // Standard CCW winding (but will appear CW after Z-flip in view matrix)
            0,1,2, 0,2,3,     4,5,6, 4,6,7,
            8,9,10, 8,10,11,  12,13,14, 12,14,15,
            16,17,18, 16,18,19, 20,21,22, 20,22,23
        };

        private int _gfxShader;
        private int _locMvp = -1, _locId = -1;
        private int _locAlbColor = -1, _locUseTex = -1, _locAlbTex = -1;
    // Debug packing removed: ID pass writes only to integer attachment in normal flow

        private GridRenderer? _grid;
            // Velocity pass shader (fullscreen) - computes camera-space motion vector
            private Engine.Rendering.ShaderProgram? _velocityShader = null;

            // Previous frame view-projection (for velocity computation)
            private OpenTK.Mathematics.Matrix4 _prevViewProj = OpenTK.Mathematics.Matrix4.Identity;

        // Gizmo geometry
        private int _lineVao, _lineVbo;
        private int _triVao, _triVbo;

        // Caméra LH (+Z forward)  GL par Z-Flip
        private float _yaw = MathHelper.DegreesToRadians(-30f);
        private float _pitch = MathHelper.DegreesToRadians(-15f);
        private float _distance = 3.0f;
        private bool _useCustomMatrices = false;
        private Vector3 _target = Vector3.Zero;
        private static readonly Matrix4 ZFlip = Matrix4.CreateScale(1f, 1f, -1f);
        private Matrix4 _viewGL, _projGL;
        private Matrix4 _projGLNoJitter; // Projection matrix without TAA jitter (for reprojection)
    private float _fovY = MathHelper.DegreesToRadians(60f);
    // Camera near/far planes - INCREASED near from 0.1f to 0.5f to reduce Z-fighting
    // A larger near clip improves depth buffer precision (ratio near:far = 1:10000 instead of 1:50000)
    private float _nearClip = 0.5f;
    private float _farClip = 5000f;

    // Projection mode: 0=Perspective, 1=Orthographic, 2=2D
    private int _projectionMode = 0;
    private float _orthoSize = 10f;

        // Expose a couple of read-only properties so UI panels can query current state
        public int ProjectionMode => _projectionMode;
        public float OrthoSize => _orthoSize;
        public float FovDegrees
        {
            get => MathHelper.RadiansToDegrees(_fovY);
            set
            {
                float clamped = (float)Math.Clamp(value, 1.0, 179.0);
                _fovY = MathHelper.DegreesToRadians(clamped);
                try { Editor.State.EditorSettings.CameraFov = clamped; } catch { }
            }
        }

        public OrbitCameraState GetOrbitCameraState()
        {
            return new OrbitCameraState
            {
                Yaw = _yaw,
                Pitch = _pitch,
                Distance = _distance,
                Target = _target
            };
        }

        public void ApplyOrbitCameraState(OrbitCameraState state, bool snapToState = true)
        {
            _yaw = state.Yaw;
            _pitch = Math.Clamp(state.Pitch, -1.553f, 1.553f);
            _distance = Math.Max(0.01f, state.Distance); // Zoom infini
            _target = state.Target;
            if (snapToState)
            {
                _targetGoal = _target;
                _distanceGoal = _distance;
                _camAnimating = false;
            }
            _useCustomMatrices = false;
        }

        // Animation focus
        private bool _camAnimating = false;
        private Vector3 _targetGoal;
        private float _distanceGoal;

        // --- Scene & sélection ---
        // Transform courant (miroir pour gizmo)
        private Vector3 _cubePos = Vector3.Zero;
        private Quaternion _cubeRot = Quaternion.Identity;
        private Vector3 _cubeScale = Vector3.One;

        // Local/World space
        private bool _localSpace = false;         // false = World, true = Local
        public void SetSpaceLocal(bool local) => _localSpace = local;

        // IDs picking (gizmos)
        private const uint ID_NONE = 0;
        private const uint ID_GZ_X = 11, ID_GZ_Y = 12, ID_GZ_Z = 13;
        private const uint ID_GZ_PLANE_XY = 21, ID_GZ_PLANE_XZ = 22, ID_GZ_PLANE_YZ = 23;
        private const uint ID_GZ_ROT_X = 31, ID_GZ_ROT_Y = 32, ID_GZ_ROT_Z = 33;
        private const uint ID_GZ_S_UNI = 40, ID_GZ_S_X = 41, ID_GZ_S_Y = 42, ID_GZ_S_Z = 43;

        private static bool IsGizmo(uint id)
            => id == ID_GZ_X || id == ID_GZ_Y || id == ID_GZ_Z
            || id == ID_GZ_PLANE_XY || id == ID_GZ_PLANE_XZ || id == ID_GZ_PLANE_YZ
            || id == ID_GZ_ROT_X || id == ID_GZ_ROT_Y || id == ID_GZ_ROT_Z
            || id == ID_GZ_S_UNI || id == ID_GZ_S_X || id == ID_GZ_S_Y || id == ID_GZ_S_Z;
        public bool IsGizmoId(uint id) => IsGizmo(id);
        
        // Vérifie si un ID correspond à une entité (pas gizmo, pas grille)
        public bool IsEntityId(uint id) 
        {
            // Utilise la classe centralisée pour éviter les magic numbers
            return Engine.Scene.EntityIdRange.IsEntityId(id) && !IsGizmo(id);
        }

        // Mode & Hover
        private GizmoMode _mode = GizmoMode.Translate;
        public void SetMode(GizmoMode m) => _mode = m;

        // Game Mode flag pour différencier les instances
        private bool _gameMode = false;
        public void SetGameMode(bool isGameMode) => _gameMode = isGameMode;

        private uint _hoverId = ID_NONE;
        public void SetHover(uint id) => _hoverId = id;

        // Snapping
        private bool _snapOn = false;
        private float _snapMove = 0.5f;
        private float _snapAngleRad = MathHelper.DegreesToRadians(15f);
        private float _snapScale = 0.1f;
        public void ConfigureSnap(bool enabled, float unitStep, float angleDeg, float scaleStep)
        {
            _snapOn = enabled;
            _snapMove = MathF.Max(1e-6f, unitStep);
            _snapAngleRad = MathHelper.DegreesToRadians(Math.Clamp(angleDeg, 0.1f, 180f));
            _snapScale = MathF.Max(1e-6f, scaleStep);
        }

        // Drag state
        private enum DragKind { None, TranslateAxis, TranslatePlane, Rotate, ScaleAxis, ScaleUniform }
        private DragKind _dragKind = DragKind.None;
        private uint _activeId = ID_NONE;

        // Translate
        private Vector3 _drag_axisDir;
        private Vector3 _drag_startObjPos;
        private float _drag_t0;
        private Vector3 _drag_planeNormal;
        private Vector3 _drag_planeHit0;

        // Rotate
        private Vector3 _rot_axis;
        private Vector3 _rot_u, _rot_v;
        private float _rot_angle0;
        private Quaternion _rot_start;

        // Scale
        private Vector3 _scale_axis;
        private Vector3 _scale_start;
        private float _scale_t0;
        private Vector3 _scale_camU, _scale_camV; // uniform
        private float _scale_r0;

        private bool _gizmoVisible = true;
        private Vector3 _gizmoPos = Vector3.Zero;

        public void SetGizmoVisible(bool v) { _gizmoVisible = v; }
        public void SetGizmoPosition(Vector3 p) { _gizmoPos = p; }

        // Snapshots multi-sélection
        private struct TRS { public Vector3 P; public Quaternion R; public Vector3 S; }
        private readonly Dictionary<uint, TRS> _preDragLocal = new(); // locals au début du drag
        private readonly Dictionary<uint, TRS> _preDragWorld = new(); // monde au début du drag

        private uint _preDragPrimaryId = 0; // pour compat ancien flux
        private Xform _preDragPrimaryLocal;

        // PBR expérimental (laissé au cas où tu le réactives plus tard)
        private Engine.Rendering.ShaderProgram? _pbrShader = null;

    // === NEW Modern Shadow System ===
    private Engine.Rendering.Shadows.ShadowManager? _shadowManager = null;
    private Engine.Rendering.ShaderProgram? _shadowDepthShader = null;

    // === OLD Shadow System (deprecated, will be removed) ===
    // Keep these for now to avoid breaking existing code, will remove after full integration
    private int _shadowFbo = 0;
    private int _shadowProg = 0;
    private int _shadowOverlayProg = 0;
    private int _shadowDebugColorTex = 0;

    // G-buffer debug mode: 0=off, 1=position, 2=normal, 3=depth
    private int _gBufferDebugMode = 0;

    // Selection Outline Renderer
    private Engine.Rendering.SelectionOutlineRenderer? _outlineRenderer = null;

    // TAA Renderer (Temporal Anti-Aliasing)
    private Engine.Rendering.PostProcess.TAARenderer? _taaRenderer = null;

    // MSAA Renderer (MultiSample Anti-Aliasing)
    private Engine.Rendering.MSAARenderer? _msaaRenderer = null;

        // Public accessor for UI
        public int GBufferDebugMode
        {
            get => _gBufferDebugMode;
            set => _gBufferDebugMode = value;
        }

        // Terrain Renderer
        private Engine.Rendering.Terrain.TerrainRenderer? _terrainRenderer = null;

        public Engine.Rendering.PostProcess.TAARenderer.TAASettings TAASettings
        {
            get => _taaSettings;
            set
            {
                _taaSettings = value;
                // Save to persistent settings
                Editor.State.EditorSettings.TAASettings = value;
                // Synchronize with TAARenderer
                if (_taaRenderer != null)
                {
                    _taaRenderer.Settings = value;
                }
            }
        }
        private Engine.Rendering.PostProcess.TAARenderer.TAASettings _taaSettings;

        // Anti-Aliasing Mode (None, MSAA, TAA)
        public Engine.Rendering.AntiAliasingMode AntiAliasingMode
        {
            get => _antiAliasingMode;
            set
            {
                if (_antiAliasingMode == value) return;
                _antiAliasingMode = value;

                // Save to persistent settings
                Editor.State.EditorSettings.AntiAliasingMode = value;

                // Recreate MSAA renderer if needed
                if (value.IsMSAA())
                {
                    _msaaRenderer?.Dispose();
                    _msaaRenderer = new Engine.Rendering.MSAARenderer(_w, _h, value.GetSampleCount());
                }
                else
                {
                    _msaaRenderer?.Dispose();
                    _msaaRenderer = null;
                }

                // Enable/disable TAA based on mode
                _taaSettings.Enabled = (value == Engine.Rendering.AntiAliasingMode.TAA);
                if (_taaRenderer != null)
                {
                    _taaRenderer.Settings = _taaSettings;
                }
            }
        }
        private Engine.Rendering.AntiAliasingMode _antiAliasingMode = Engine.Rendering.AntiAliasingMode.None;

        // ========================= LIFECYCLE =========================

        public void Resize(int w, int h)
        {
            w = Math.Max(1, w); h = Math.Max(1, h);

            // Check GL max texture size on first resize
            if (_maxTexSize == 0)
            {
                GL.GetInteger(GetPName.MaxTextureSize, out _maxTexSize);
                LogManager.LogInfo($"GL_MAX_TEXTURE_SIZE: {_maxTexSize}", "ViewportRenderer");
            }

            // Clamp to GL max texture size to avoid artifacts
            if (w > _maxTexSize || h > _maxTexSize)
            {
                LogManager.LogWarning($"Viewport size {w}x{h} exceeds GL_MAX_TEXTURE_SIZE {_maxTexSize}, clamping", "ViewportRenderer");
                w = Math.Min(w, _maxTexSize);
                h = Math.Min(h, _maxTexSize);
            }

            // Store display size for when render scale changes
            _displayWidth = w;
            _displayHeight = h;

            // Apply render scale (render at lower res, upscale to display res)
            int renderW = Math.Max(1, (int)(w * _renderScale));
            int renderH = Math.Max(1, (int)(h * _renderScale));

            // Force even dimensions to avoid mipmap artifacts in bloom downsampling
            // (odd dimensions cause rounding errors when repeatedly dividing by 2)
            renderW = (renderW + 1) & ~1;  // Round up to nearest even number
            renderH = (renderH + 1) & ~1;

            // Early return if size hasn't changed
            bool sizeChanged = (_fbo == 0) || (renderW != _w) || (renderH != _h);
            if (!sizeChanged)
            {
                return; // Size hasn't changed, nothing to do
            }

            // Update size
            _w = renderW; _h = renderH;

            LogManager.LogInfo($"Resize: display={_displayWidth}x{_displayHeight}, render={_w}x{_h}, scale={_renderScale}", "ViewportRenderer");

            if (_fbo == 0) _fbo = GL.GenFramebuffer();
            // Debug: try { if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[ViewportRenderer] Instance={this.GetHashCode()} Resize({w}x{h}) -> FBO={_fbo}, previousColorTex={_colorTex}, previousIdTex={_idTex}"); } catch { }
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);

            // Always regenerate color texture to avoid sharing between renderers
            if (_colorTex != 0) GL.DeleteTexture(_colorTex);
            _colorTex = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _colorTex);
            // Use a 16-bit float color buffer to keep HDR precision until tonemapping
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f, _w, _h, 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _colorTex, 0);

            // Always regenerate ID texture to avoid sharing between renderers
            if (_idTex != 0) GL.DeleteTexture(_idTex);
            _idTex = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _idTex);

            // Initialize texture with zeros to avoid garbage data on borders
            uint[] zeroData = new uint[_w * _h];
            Array.Fill(zeroData, 0u);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.R32ui, _w, _h, 0, PixelFormat.RedInteger, PixelType.UnsignedInt, zeroData);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1, TextureTarget.Texture2D, _idTex, 0);

            // Debug: try { Console.WriteLine($"[ViewportRenderer] Instance={this.GetHashCode()} Resize COMPLETE -> newColorTex={_colorTex}, newIdTex={_idTex}"); } catch { }

            // Create depth texture (instead of renderbuffer) to enable depth reading for raycasting
            if (_depthTex != 0) GL.DeleteTexture(_depthTex);
            _depthTex = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _depthTex);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent24, _w, _h, 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, _depthTex, 0);

            var bufs = new DrawBuffersEnum[] { DrawBuffersEnum.ColorAttachment0, DrawBuffersEnum.ColorAttachment1 };
            GL.DrawBuffers(bufs.Length, bufs);

            var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != FramebufferErrorCode.FramebufferComplete) throw new Exception("FBO incomplete: " + status);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            // Create or resize post-process target (separate texture + fbo)
            if (_postTex != 0) { GL.DeleteTexture(_postTex); _postTex = 0; }
            if (_postFbo != 0) { GL.DeleteFramebuffer(_postFbo); _postFbo = 0; }

            _postTex = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _postTex);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f, _w, _h, 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            _postFbo = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _postFbo);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _postTex, 0);
            var pstatus = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (pstatus != FramebufferErrorCode.FramebufferComplete)
            {
                if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[ViewportRenderer.Resize] Warning: post FBO incomplete: {pstatus}");
                // mark unhealthy - we'll fallback to raw color texture later
                _postTexHealthy = false;
            }
            else
            {
                _postTexHealthy = true;
                // Clear the FBO to avoid artifacts from uninitialized texture memory
                GL.Viewport(0, 0, _w, _h);
                GL.ClearColor(0, 0, 0, 0);
                GL.Clear(ClearBufferMask.ColorBufferBit);
            }
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            // Create second post-process target for ping-pong between effects
            if (_postTex2 != 0) { GL.DeleteTexture(_postTex2); _postTex2 = 0; }
            if (_postFbo2 != 0) { GL.DeleteFramebuffer(_postFbo2); _postFbo2 = 0; }

            _postTex2 = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _postTex2);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f, _w, _h, 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            _postFbo2 = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _postFbo2);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _postTex2, 0);
            var p2status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (p2status != FramebufferErrorCode.FramebufferComplete)
            {
                if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[ViewportRenderer.Resize] Warning: post FBO2 incomplete: {p2status}");
            }
            else
            {
                // Clear the FBO to avoid artifacts from uninitialized texture memory
                GL.Viewport(0, 0, _w, _h);
                GL.ClearColor(0, 0, 0, 0);
                GL.Clear(ClearBufferMask.ColorBufferBit);
            }
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            // Create or resize velocity target (separate FBO so we can read depth from main FBO safely)
            if (_velocityTex != 0) { GL.DeleteTexture(_velocityTex); _velocityTex = 0; }
            if (_velocityFbo != 0) { try { GL.DeleteFramebuffer(_velocityFbo); } catch { } _velocityFbo = 0; }

            _velocityTex = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _velocityTex);
            // Store 2-channel float velocity (x,y) in RG16F
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rg16f, _w, _h, 0, PixelFormat.Rg, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            _velocityFbo = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _velocityFbo);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _velocityTex, 0);
            var vstatus = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (vstatus != FramebufferErrorCode.FramebufferComplete)
            {
                if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[ViewportRenderer.Resize] Warning: velocity FBO incomplete: {vstatus}");
                // If incomplete, free resources and fall back to not using velocity
                try { GL.DeleteTexture(_velocityTex); } catch { }
                try { GL.DeleteFramebuffer(_velocityFbo); } catch { }
                _velocityTex = 0; _velocityFbo = 0;
            }
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            if (_gfxShader == 0) InitResources();
            // Lazy-create grid only if enabled
            if (_showGrid && _grid == null) _grid = new GridRenderer();

            // Initialize Selection Outline renderer
            if (_outlineRenderer != null)
            {
                try { _outlineRenderer.Dispose(); } catch { }
            }

            try
            {
                _outlineRenderer = new Engine.Rendering.SelectionOutlineRenderer();
                _outlineRenderer.Initialize();
                Editor.Logging.LogManager.LogInfo("Selection Outline Renderer initialized", "Renderer");
            }
            catch (Exception ex)
            {
                if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[ViewportRenderer] Failed to create Selection Outline renderer: {ex.Message}");
                _outlineRenderer = null;
            }

            // Initialize or recreate TAA renderer when size changes
            if (_taaRenderer != null)
            {
                try { _taaRenderer.Dispose(); } catch { }
            }

            try
            {
                _taaRenderer = new Engine.Rendering.PostProcess.TAARenderer(_w, _h);
                _taaRenderer.Settings = TAASettings;
                Editor.Logging.LogManager.LogInfo($"TAA Renderer initialized/resized: {_w}x{_h}", "Renderer");
            }
            catch (Exception ex)
            {
                if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[ViewportRenderer] Failed to create TAA renderer: {ex.Message}");
                _taaRenderer = null;
            }

            // Initialize or recreate MSAA renderer when size changes (if MSAA is enabled)
            if (_antiAliasingMode.IsMSAA())
            {
                try
                {
                    _msaaRenderer?.Dispose();
                    _msaaRenderer = new Engine.Rendering.MSAARenderer(_w, _h, _antiAliasingMode.GetSampleCount());
                    Editor.Logging.LogManager.LogInfo($"MSAA Renderer initialized/resized: {_w}x{_h} @ {_antiAliasingMode.GetSampleCount()}x samples", "Renderer");
                }
                catch (Exception ex)
                {
                    if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[ViewportRenderer] Failed to create MSAA renderer: {ex.Message}");
                    _msaaRenderer = null;
                }
            }

            // Initialize Terrain renderer
            if (_terrainRenderer == null)
            {
                try
                {
                    _terrainRenderer = new Engine.Rendering.Terrain.TerrainRenderer();
                    Editor.Logging.LogManager.LogInfo("Terrain Renderer initialized successfully", "Renderer");
                }
                catch (Exception)
                {
                    _terrainRenderer = null;
                }
            }

            // Initialize Water Reflection framebuffer (DISABLED - TO BE REIMPLEMENTED LATER)
            // InitReflectionFramebuffer();

            // seed - à la fin de Resize()
if (_scene.Entities.Count == 0)
{
    _scene.CreateCube("Cube A", new Vector3(-1.0f, 0.5f, +0.0f), new Vector3(1, 1, 1), new Vector4(0.85f, 0.55f, 0.55f, 1));
    _scene.CreateCube("Cube B", new Vector3(+0.0f, 0.5f, +0.0f), new Vector3(1, 1, 1), new Vector4(0.55f, 0.85f, 0.60f, 1));
    _scene.CreateCube("Cube C", new Vector3(+1.2f, 0.5f, -0.8f), new Vector3(1, 1, 1), new Vector4(0.50f, 0.70f, 1.00f, 1));
    
    // ✅ FIX : Sélection initiale correcte
    if (Selection.ActiveEntityId == 0) 
    {
        Selection.ActiveEntityId = _scene.Entities[0].Id;
        Selection.Selected.Add(Selection.ActiveEntityId);
        // Ne pas appeler UpdateGizmoPivot() ici car ViewportPanel n'est pas encore prêt
    }
}
        }

        // en-tête demandé + méthode mise à jour :
        private void InitResources()
        {
            // --- texture blanche 1x1 (cache runtime)
            TextureCache.Initialize();

            // --- 24 sommets cube (pos + normal + uv) ---
            // 6 faces * 4 sommets (pos.xyz, normal.xyz, uv.xy)
            // All faces have consistent CCW winding when viewed from outside
            float[] verts =
            {
                // face -Z (normal = 0,0,-1) - CORRECTED WINDING
                -0.5f,-0.5f,-0.5f,   0f,0f,-1f,   0f,0f,
                -0.5f, 0.5f,-0.5f,   0f,0f,-1f,   1f,0f,
                 0.5f, 0.5f,-0.5f,   0f,0f,-1f,   1f,1f,
                 0.5f,-0.5f,-0.5f,   0f,0f,-1f,   0f,1f,
                // face +Z (normal = 0,0,1) - already correct
                -0.5f,-0.5f, 0.5f,   0f,0f,1f,    0f,0f,
                 0.5f,-0.5f, 0.5f,   0f,0f,1f,    1f,0f,
                 0.5f, 0.5f, 0.5f,   0f,0f,1f,    1f,1f,
                -0.5f, 0.5f, 0.5f,   0f,0f,1f,    0f,1f,
                // face -X (normal = -1,0,0) - CORRECTED WINDING
                -0.5f,-0.5f,-0.5f,   -1f,0f,0f,   0f,0f,
                -0.5f,-0.5f, 0.5f,   -1f,0f,0f,   1f,0f,
                -0.5f, 0.5f, 0.5f,   -1f,0f,0f,   1f,1f,
                -0.5f, 0.5f,-0.5f,   -1f,0f,0f,   0f,1f,
                // face +X (normal = 1,0,0) - already correct
                 0.5f,-0.5f,-0.5f,   1f,0f,0f,    0f,0f,
                 0.5f, 0.5f,-0.5f,   1f,0f,0f,    1f,0f,
                 0.5f, 0.5f, 0.5f,   1f,0f,0f,    1f,1f,
                 0.5f,-0.5f, 0.5f,   1f,0f,0f,    0f,1f,
                // face -Y (normal = 0,-1,0) - CORRECTED WINDING
                -0.5f,-0.5f,-0.5f,   0f,-1f,0f,   0f,0f,
                 0.5f,-0.5f,-0.5f,   0f,-1f,0f,   1f,0f,
                 0.5f,-0.5f, 0.5f,   0f,-1f,0f,   1f,1f,
                -0.5f,-0.5f, 0.5f,   0f,-1f,0f,   0f,1f,
                // face +Y (normal = 0,1,0) - already correct
                -0.5f, 0.5f,-0.5f,   0f,1f,0f,    0f,0f,
                -0.5f, 0.5f, 0.5f,   0f,1f,0f,    1f,0f,
                 0.5f, 0.5f, 0.5f,   0f,1f,0f,    1f,1f,
                 0.5f, 0.5f,-0.5f,   0f,1f,0f,    0f,1f,
            };

            _cubeVao = GL.GenVertexArray();
            _cubeVbo = GL.GenBuffer();
            _cubeEbo = GL.GenBuffer();

            GL.BindVertexArray(_cubeVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _cubeVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, verts.Length * sizeof(float), verts, BufferUsageHint.StaticDraw);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _cubeEbo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, _cubeIdx.Length * sizeof(uint), _cubeIdx, BufferUsageHint.StaticDraw);

            // aPos (loc=0), aNormal (loc=1), aUV (loc=2)
            int stride = (3 + 3 + 2) * sizeof(float);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
            GL.EnableVertexAttribArray(2);
            GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, 6 * sizeof(float));

            // --- Legacy VAO for fallback (pos + UV only) ---
            _legacyCubeVao = GL.GenVertexArray();
            _legacyCubeVbo = GL.GenBuffer();
            
            // Create legacy vertex data (pos.xyz + uv.xy only)
            float[] legacyVerts = new float[]
            {
                // face -Z
                -0.5f,-0.5f,-0.5f,   0f,0f,
                 0.5f,-0.5f,-0.5f,   1f,0f,
                 0.5f, 0.5f,-0.5f,   1f,1f,
                -0.5f, 0.5f,-0.5f,   0f,1f,
                // face +Z
                -0.5f,-0.5f, 0.5f,   0f,0f,
                 0.5f,-0.5f, 0.5f,   1f,0f,
                 0.5f, 0.5f, 0.5f,   1f,1f,
                -0.5f, 0.5f, 0.5f,   0f,1f,
                // face -X
                -0.5f,-0.5f,-0.5f,   0f,0f,
                -0.5f, 0.5f,-0.5f,   1f,0f,
                -0.5f, 0.5f, 0.5f,   1f,1f,
                -0.5f,-0.5f, 0.5f,   0f,1f,
                // face +X
                 0.5f,-0.5f,-0.5f,   0f,0f,
                 0.5f, 0.5f,-0.5f,   1f,0f,
                 0.5f, 0.5f, 0.5f,   1f,1f,
                 0.5f,-0.5f, 0.5f,   0f,1f,
                // face -Y
                -0.5f,-0.5f,-0.5f,   0f,0f,
                -0.5f,-0.5f, 0.5f,   1f,0f,
                 0.5f,-0.5f, 0.5f,   1f,1f,
                 0.5f,-0.5f,-0.5f,   0f,1f,
                // face +Y
                -0.5f, 0.5f,-0.5f,   0f,0f,
                -0.5f, 0.5f, 0.5f,   1f,0f,
                 0.5f, 0.5f, 0.5f,   1f,1f,
                 0.5f, 0.5f,-0.5f,   0f,1f,
            };
            
            GL.BindVertexArray(_legacyCubeVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _legacyCubeVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, legacyVerts.Length * sizeof(float), legacyVerts, BufferUsageHint.StaticDraw);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _cubeEbo); // Reuse same EBO
            
            // aPos (loc=0), aUV (loc=1) - legacy layout
            int legacyStride = (3 + 2) * sizeof(float);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, legacyStride, 0);
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, legacyStride, 3 * sizeof(float));

            // Initialize legacy (unlit) meshes
            CreateLegacyPlaneXZ();
            CreateLegacyQuadXY();
            CreateLegacySphere(32, 64);  // Increased from (16, 24) - must match modern mesh
            CreateLegacyCapsule(24, 32); // Increased from (12, 16) - must match modern mesh

            // --- shader unlit + picking (couleur * texture optionnelle) ---
            const string VS = @"#version 330 core
layout(location=0) in vec3 aPos;
layout(location=1) in vec2 aUV;
uniform mat4 u_MVP;
out vec2 vUV;
void main(){
    vUV = aUV;
    gl_Position = u_MVP * vec4(aPos,1.0);
}";
            const string FS = @"#version 330 core
layout(location=0) out vec4 outColor;
layout(location=1) out uint outId;
in vec2 vUV;
uniform vec4  u_AlbedoColor;
uniform bool  u_UseTex;
uniform sampler2D u_AlbedoTex;
uniform uint  u_ObjectId;
void main(){
    vec4 col = u_AlbedoColor;
    if(u_UseTex) col *= texture(u_AlbedoTex, vUV);
    outColor = col;
    outId    = u_ObjectId;
}";

            int v = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(v, VS); GL.CompileShader(v);
            GL.GetShader(v, ShaderParameter.CompileStatus, out int okv); if (okv==0) throw new Exception(GL.GetShaderInfoLog(v));
            int f = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(f, FS); GL.CompileShader(f);
            GL.GetShader(f, ShaderParameter.CompileStatus, out int okf); if (okf==0) throw new Exception(GL.GetShaderInfoLog(f));

            _gfxShader = GL.CreateProgram();
            GL.AttachShader(_gfxShader, v);
            GL.AttachShader(_gfxShader, f);
            GL.LinkProgram(_gfxShader);
            GL.GetProgram(_gfxShader, GetProgramParameterName.LinkStatus, out int okp); if (okp==0) throw new Exception(GL.GetProgramInfoLog(_gfxShader));
            GL.DetachShader(_gfxShader, v); GL.DetachShader(_gfxShader, f);
            GL.DeleteShader(v); GL.DeleteShader(f);

            _locMvp      = GL.GetUniformLocation(_gfxShader, "u_MVP");
            _locId       = GL.GetUniformLocation(_gfxShader, "u_ObjectId");
            _locAlbColor = GL.GetUniformLocation(_gfxShader, "u_AlbedoColor");
            _locUseTex   = GL.GetUniformLocation(_gfxShader, "u_UseTex");
            _locAlbTex   = GL.GetUniformLocation(_gfxShader, "u_AlbedoTex");

            GL.UseProgram(_gfxShader);
            GL.Uniform1(_locAlbTex, 0); // sampler sur l'unité 0
            GL.UseProgram(0);

            // --- PBR shader initialization ---
            try
            {
                // Clear shader preprocessor cache to ensure fresh shader compilation
                Engine.Rendering.ShaderPreprocessor.ClearCache();

                // Prefer the ShaderLibrary-managed instance so all users get the same ShaderProgram
                _pbrShader = Engine.Rendering.ShaderLibrary.GetShaderByName("ForwardBase");
                if (_pbrShader == null)
                {
                    // Fallback if ShaderLibrary didn't find it (rare)
                    _pbrShader = Engine.Rendering.ShaderProgram.FromFiles("Engine/Rendering/Shaders/Forward/ForwardBase.vert", "Engine/Rendering/Shaders/Forward/ForwardBase.frag");
                }

                // Ensure Global UBO binding is set on the chosen shader program
                try
                {
                    _pbrShader.Use();
                    int globalBlockIndex = GL.GetUniformBlockIndex(_pbrShader.Handle, "Global");
                    if (globalBlockIndex != -1)
                    {
                        GL.UniformBlockBinding(_pbrShader.Handle, globalBlockIndex, 0);
                    }
                }
                catch { }
                GL.UseProgram(0);
                
            }
            catch (System.Exception ex)
            {
                // Fallback - continue without PBR
                if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[ViewportRenderer] CRITICAL: Failed to load PBR shader ForwardBase: {ex.Message}");
                if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log("[ViewportRenderer] Terrain will NOT render without PBR shader!");
                _pbrShader = null;
            }

            // === NEW Modern Shadow System ===
            try
            {
                // Initialize shadow manager with settings from editor
                var shadowSettings = Editor.State.EditorSettings.ShadowsSettings;
                _shadowManager = new Engine.Rendering.Shadows.ShadowManager(shadowSettings.ShadowMapSize);

                // Load shadow depth shader from files (clean, modern approach)
                _shadowDepthShader = Engine.Rendering.ShaderProgram.FromFiles(
                    "Engine/Rendering/Shaders/Shadow/ShadowDepth.vert",
                    "Engine/Rendering/Shaders/Shadow/ShadowDepth.frag"
                );

                if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log("[ViewportRenderer] Simple shadow system initialized successfully");
                if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[ViewportRenderer] Shadow map size: {_shadowManager.ShadowMapSize}x{_shadowManager.ShadowMapSize}");
            }
            catch (Exception ex)
            {
                if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[ViewportRenderer] Failed to initialize modern shadow system: {ex.Message}");
                if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[ViewportRenderer] Stack trace: {ex.StackTrace}");
                _shadowManager = null;
                _shadowDepthShader = null;
            }

            // === OLD Shadow System (keep for compatibility, will remove later) ===
            try
            {
                // Keep old FBO for now to avoid breaking existing code
                _shadowFbo = GL.GenFramebuffer();

                // Old inline shader compilation (deprecated)
                const string shadowVs = "#version 330 core\nlayout(location=0) in vec3 aPos; uniform mat4 u_LightMatrix; uniform mat4 u_Model; void main(){ gl_Position = u_LightMatrix * u_Model * vec4(aPos,1.0); }";
                const string shadowFs = "#version 330 core\nvoid main(){ }";
                int vs = GL.CreateShader(ShaderType.VertexShader);
                GL.ShaderSource(vs, shadowVs);
                GL.CompileShader(vs);
                int fs = GL.CreateShader(ShaderType.FragmentShader);
                GL.ShaderSource(fs, shadowFs);
                GL.CompileShader(fs);
                _shadowProg = GL.CreateProgram();
                GL.AttachShader(_shadowProg, vs);
                GL.AttachShader(_shadowProg, fs);
                GL.LinkProgram(_shadowProg);
                GL.DeleteShader(vs);
                GL.DeleteShader(fs);
            }
            catch { }

            // Initialize modern (PBR) meshes (pos+normal+uv)
            CreateModernPlaneXZ();
            CreateModernQuadXY();
            CreateModernSphere(32, 64);  // Increased from (16, 24) for smoother SSAO
            CreateModernCapsule(24, 32); // Increased from (12, 16) for smoother geometry

            // --- VAOs temporaires pour gizmo (lignes/triangles immédiats) ---
            _lineVao = GL.GenVertexArray();
            _lineVbo = GL.GenBuffer();
            GL.BindVertexArray(_lineVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _lineVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, 2 * 3 * sizeof(float), IntPtr.Zero, BufferUsageHint.DynamicDraw);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);

            _triVao = GL.GenVertexArray();
            _triVbo = GL.GenBuffer();
            GL.BindVertexArray(_triVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _triVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, 16384 * sizeof(float), IntPtr.Zero, BufferUsageHint.DynamicDraw);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);

            // --- Light icon geometry (diamond/bulb shape) ---
            InitLightIconGeometry();
        }

        private void InitLightIconGeometry()
        {
            // Create a diamond-like shape for light icons (like Unity's light icon)
            float[] lightVerts = 
            {
                // Diamond shape vertices (pos.xyz, uv.xy)
                // Top vertex
                 0.0f,  0.5f,  0.0f,   0.5f, 1.0f,
                // Bottom vertex  
                 0.0f, -0.5f,  0.0f,   0.5f, 0.0f,
                // Front vertex
                 0.0f,  0.0f,  0.5f,   0.0f, 0.5f,
                // Back vertex
                 0.0f,  0.0f, -0.5f,   1.0f, 0.5f,
                // Left vertex
                -0.5f,  0.0f,  0.0f,   0.25f, 0.5f,
                // Right vertex
                 0.5f,  0.0f,  0.0f,   0.75f, 0.5f,
            };

            uint[] lightIndices = 
            {
                // Top faces
                0, 2, 4,  // top-front-left
                0, 4, 3,  // top-left-back
                0, 3, 5,  // top-back-right
                0, 5, 2,  // top-right-front
                
                // Bottom faces  
                1, 4, 2,  // bottom-left-front
                1, 3, 4,  // bottom-back-left
                1, 5, 3,  // bottom-right-back
                1, 2, 5,  // bottom-front-right
            };

            _lightIconVao = GL.GenVertexArray();
            _lightIconVbo = GL.GenBuffer();
            _lightIconEbo = GL.GenBuffer();

            GL.BindVertexArray(_lightIconVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _lightIconVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, lightVerts.Length * sizeof(float), lightVerts, BufferUsageHint.StaticDraw);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _lightIconEbo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, lightIndices.Length * sizeof(uint), lightIndices, BufferUsageHint.StaticDraw);

            // aPos (loc=0), aUV (loc=1) 
            int stride = (3 + 2) * sizeof(float);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
        }

        // ========================= MESH BUILDERS (LEGACY) =========================
        private void CreateLegacyPlaneXZ()
        {
            // Unit plane on XZ, centered, size 1x1, pos+uv
            float[] verts = new float[]
            {
                // x, y, z,   u, v
                -0.5f, 0f, -0.5f, 0f, 0f,
                 0.5f, 0f, -0.5f, 1f, 0f,
                 0.5f, 0f,  0.5f, 1f, 1f,
                -0.5f, 0f,  0.5f, 0f, 1f,
            };
            uint[] idx = new uint[] { 0,1,2, 2,3,0 };
            _planeIndexCount = idx.Length;
            _legacyPlaneVao = GL.GenVertexArray();
            _legacyPlaneVbo = GL.GenBuffer();
            _legacyPlaneEbo = GL.GenBuffer();
            GL.BindVertexArray(_legacyPlaneVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _legacyPlaneVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, verts.Length * sizeof(float), verts, BufferUsageHint.StaticDraw);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _legacyPlaneEbo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, idx.Length * sizeof(uint), idx, BufferUsageHint.StaticDraw);
            int stride = 5 * sizeof(float);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
        }

        private void CreateLegacyQuadXY()
        {
            // Unit quad on XY, centered, size 1x1, pos+uv
            float[] verts = new float[]
            {
                -0.5f, -0.5f, 0f, 0f, 0f,
                 0.5f, -0.5f, 0f, 1f, 0f,
                 0.5f,  0.5f, 0f, 1f, 1f,
                -0.5f,  0.5f, 0f, 0f, 1f,
            };
            uint[] idx = new uint[] { 0,1,2, 2,3,0 };
            _quadIndexCount = idx.Length;
            _legacyQuadVao = GL.GenVertexArray();
            _legacyQuadVbo = GL.GenBuffer();
            _legacyQuadEbo = GL.GenBuffer();
            GL.BindVertexArray(_legacyQuadVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _legacyQuadVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, verts.Length * sizeof(float), verts, BufferUsageHint.StaticDraw);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _legacyQuadEbo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, idx.Length * sizeof(uint), idx, BufferUsageHint.StaticDraw);
            int stride = 5 * sizeof(float);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
        }

        private void CreateLegacySphere(int latSegments, int lonSegments)
        {
            var verts = new List<float>();
            var indices = new List<uint>();
            for (int lat = 0; lat <= latSegments; lat++)
            {
                float v = (float)lat / latSegments;
                float phi = v * MathF.PI; // 0..PI
                float y = MathF.Cos(phi) * 0.5f;
                float r = MathF.Sin(phi) * 0.5f;
                for (int lon = 0; lon <= lonSegments; lon++)
                {
                    float u = (float)lon / lonSegments;
                    float theta = u * MathF.PI * 2f;
                    float x = MathF.Cos(theta) * r;
                    float z = MathF.Sin(theta) * r;
                    verts.AddRange(new float[] { x, y, z, u, v });
                }
            }
            int stride = lonSegments + 1;
            for (int lat = 0; lat < latSegments; lat++)
            {
                for (int lon = 0; lon < lonSegments; lon++)
                {
                    uint i0 = (uint)(lat * stride + lon);
                    uint i1 = (uint)((lat + 1) * stride + lon);
                    uint i2 = (uint)((lat + 1) * stride + lon + 1);
                    uint i3 = (uint)(lat * stride + lon + 1);
                    // Reversed winding: i0,i2,i1 instead of i0,i1,i2 to get CCW from outside
                    indices.Add(i0); indices.Add(i2); indices.Add(i1);
                    indices.Add(i2); indices.Add(i0); indices.Add(i3);
                }
            }
            _sphereIndexCount = indices.Count;
            _legacySphereVao = GL.GenVertexArray();
            _legacySphereVbo = GL.GenBuffer();
            _legacySphereEbo = GL.GenBuffer();
            GL.BindVertexArray(_legacySphereVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _legacySphereVbo);
            var vArrS = verts.ToArray();
            GL.BufferData(BufferTarget.ArrayBuffer, vArrS.Length * sizeof(float), vArrS, BufferUsageHint.StaticDraw);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _legacySphereEbo);
            var iArrS = indices.ToArray();
            GL.BufferData(BufferTarget.ElementArrayBuffer, iArrS.Length * sizeof(uint), iArrS, BufferUsageHint.StaticDraw);
            int vstride = 5 * sizeof(float);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, vstride, 0);
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, vstride, 3 * sizeof(float));
        }

        private void CreateLegacyCapsule(int latSegmentsHemisphere, int radialSegments)
        {
            // Y-up: cylinder height 1 (from -0.5 to 0.5), radius 0.5, plus hemispheres
            var verts = new List<float>();
            var indices = new List<uint>();

            var ringOffsets = new List<int>();
            // Bottom hemisphere (phi: -PI/2..0)
            for (int i = 0; i <= latSegmentsHemisphere; i++)
            {
                float v = (float)i / latSegmentsHemisphere;
                float phi = (-MathF.PI * 0.5f) + v * (MathF.PI * 0.5f);
                float y = MathF.Sin(phi) * 0.5f - 0.5f;
                float r = MathF.Cos(phi) * 0.5f;
                ringOffsets.Add(verts.Count / 5);
                for (int j = 0; j <= radialSegments; j++)
                {
                    float u = (float)j / radialSegments;
                    float theta = u * MathF.PI * 2f;
                    float x = MathF.Cos(theta) * r;
                    float z = MathF.Sin(theta) * r;
                    verts.AddRange(new float[] { x, y, z, u, 0.25f * v });
                }
            }
            // Cylinder (y: -0.5..0.5)
            for (int i = 1; i < radialSegments; i++)
            {
                float t = (float)i / radialSegments; // (0,1)
                float y = -0.5f + t * 1.0f;
                float r = 0.5f;
                ringOffsets.Add(verts.Count / 5);
                for (int j = 0; j <= radialSegments; j++)
                {
                    float u = (float)j / radialSegments;
                    float theta = u * MathF.PI * 2f;
                    float x = MathF.Cos(theta) * r;
                    float z = MathF.Sin(theta) * r;
                    verts.AddRange(new float[] { x, y, z, u, 0.25f + 0.5f * t });
                }
            }
            // Top hemisphere (phi: 0..PI/2)
            for (int i = 0; i <= latSegmentsHemisphere; i++)
            {
                float v = (float)i / latSegmentsHemisphere;
                float phi = v * (MathF.PI * 0.5f);
                float y = MathF.Sin(phi) * 0.5f + 0.5f;
                float r = MathF.Cos(phi) * 0.5f;
                ringOffsets.Add(verts.Count / 5);
                for (int j = 0; j <= radialSegments; j++)
                {
                    float u = (float)j / radialSegments;
                    float theta = u * MathF.PI * 2f;
                    float x = MathF.Cos(theta) * r;
                    float z = MathF.Sin(theta) * r;
                    verts.AddRange(new float[] { x, y, z, u, 0.75f + 0.25f * v });
                }
            }

            // Build indices between consecutive rings
            for (int r = 0; r < ringOffsets.Count - 1; r++)
            {
                int base0 = ringOffsets[r];
                int base1 = ringOffsets[r + 1];
                for (int j = 0; j < radialSegments; j++)
                {
                    uint i0 = (uint)(base0 + j);
                    uint i1 = (uint)(base1 + j);
                    uint i2 = (uint)(base1 + j + 1);
                    uint i3 = (uint)(base0 + j + 1);
                    // Corrected winding: i0,i1,i2 for proper CCW from outside
                    indices.Add(i0); indices.Add(i1); indices.Add(i2);
                    indices.Add(i0); indices.Add(i2); indices.Add(i3);
                }
            }

            _capsuleIndexCount = indices.Count;
            _legacyCapsuleVao = GL.GenVertexArray();
            _legacyCapsuleVbo = GL.GenBuffer();
            _legacyCapsuleEbo = GL.GenBuffer();
            GL.BindVertexArray(_legacyCapsuleVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _legacyCapsuleVbo);
            var vArrC = verts.ToArray();
            GL.BufferData(BufferTarget.ArrayBuffer, vArrC.Length * sizeof(float), vArrC, BufferUsageHint.StaticDraw);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _legacyCapsuleEbo);
            var iArrC = indices.ToArray();
            GL.BufferData(BufferTarget.ElementArrayBuffer, iArrC.Length * sizeof(uint), iArrC, BufferUsageHint.StaticDraw);
            int vstride = 5 * sizeof(float);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, vstride, 0);
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, vstride, 3 * sizeof(float));
        }

        // ========================= MESH BUILDERS (MODERN/PBR) =========================
        private void CreateModernPlaneXZ()
        {
            // Simple plane (will be tessellated by Water shader if used)
            // pos(3)+normal(3)+uv(2)
            // Winding matches cube +Y face (CCW when viewed from above)
            float[] verts = new float[]
            {
                -0.5f,0f,-0.5f, 0f,1f,0f, 0f,0f,  // back-left
                -0.5f,0f, 0.5f, 0f,1f,0f, 1f,0f,  // front-left
                 0.5f,0f, 0.5f, 0f,1f,0f, 1f,1f,  // front-right
                 0.5f,0f,-0.5f, 0f,1f,0f, 0f,1f,  // back-right
            };
            uint[] idx = new uint[] { 0,1,2, 0,2,3 };
            _planeIndexCount = idx.Length;

            _planeVao = GL.GenVertexArray();
            _planeVbo = GL.GenBuffer();
            _planeEbo = GL.GenBuffer();
            GL.BindVertexArray(_planeVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _planeVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, verts.Length * sizeof(float), verts, BufferUsageHint.StaticDraw);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _planeEbo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, idx.Length * sizeof(uint), idx, BufferUsageHint.StaticDraw);
            int stride = 8 * sizeof(float);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
            GL.EnableVertexAttribArray(2);
            GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, 6 * sizeof(float));
        }

        private void CreateModernQuadXY()
        {
            // Quad in XY plane (facing +Z)
            // Winding matches cube +Z face (CCW when viewed from +Z)
            float[] verts = new float[]
            {
                -0.5f,-0.5f,0f, 0f,0f,1f, 0f,0f,  // bottom-left
                 0.5f,-0.5f,0f, 0f,0f,1f, 1f,0f,  // bottom-right
                 0.5f, 0.5f,0f, 0f,0f,1f, 1f,1f,  // top-right
                -0.5f, 0.5f,0f, 0f,0f,1f, 0f,1f,  // top-left
            };
            uint[] idx = new uint[] { 0,1,2, 0,2,3 };
            _quadVao = GL.GenVertexArray();
            _quadVbo = GL.GenBuffer();
            _quadEbo = GL.GenBuffer();
            GL.BindVertexArray(_quadVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _quadVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, verts.Length * sizeof(float), verts, BufferUsageHint.StaticDraw);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _quadEbo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, idx.Length * sizeof(uint), idx, BufferUsageHint.StaticDraw);
            int stride = 8 * sizeof(float);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
            GL.EnableVertexAttribArray(2);
            GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, 6 * sizeof(float));
        }

        private void CreateModernSphere(int latSegments, int lonSegments)
        {
            var verts = new List<float>();
            var indices = new List<uint>();
            for (int lat = 0; lat <= latSegments; lat++)
            {
                float v = (float)lat / latSegments;
                float phi = v * MathF.PI;
                float y = MathF.Cos(phi) * 0.5f;
                float r = MathF.Sin(phi) * 0.5f;
                for (int lon = 0; lon <= lonSegments; lon++)
                {
                    float u = (float)lon / lonSegments;
                    float theta = u * MathF.PI * 2f;
                    float x = MathF.Cos(theta) * r;
                    float z = MathF.Sin(theta) * r;
                    var n = new Vector3(x, y, z);
                    if (n.LengthSquared > 0f) n.Normalize();
                    verts.AddRange(new float[] { x, y, z, n.X, n.Y, n.Z, u, v });
                }
            }
            int stride = lonSegments + 1;
            for (int lat = 0; lat < latSegments; lat++)
            {
                for (int lon = 0; lon < lonSegments; lon++)
                {
                    uint i0 = (uint)(lat * stride + lon);
                    uint i1 = (uint)((lat + 1) * stride + lon);
                    uint i2 = (uint)((lat + 1) * stride + lon + 1);
                    uint i3 = (uint)(lat * stride + lon + 1);
                    // Reversed winding: i0,i2,i1 instead of i0,i1,i2 to get CCW from outside
                    indices.Add(i0); indices.Add(i2); indices.Add(i1);
                    indices.Add(i2); indices.Add(i0); indices.Add(i3);
                }
            }
            _sphereIndexCount = indices.Count;
            _sphereVao = GL.GenVertexArray();
            _sphereVbo = GL.GenBuffer();
            _sphereEbo = GL.GenBuffer();
            GL.BindVertexArray(_sphereVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _sphereVbo);
            var vArrSM = verts.ToArray();
            GL.BufferData(BufferTarget.ArrayBuffer, vArrSM.Length * sizeof(float), vArrSM, BufferUsageHint.StaticDraw);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _sphereEbo);
            var iArrSM = indices.ToArray();
            GL.BufferData(BufferTarget.ElementArrayBuffer, iArrSM.Length * sizeof(uint), iArrSM, BufferUsageHint.StaticDraw);
            int vstride = 8 * sizeof(float);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, vstride, 0);
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, vstride, 3 * sizeof(float));
            GL.EnableVertexAttribArray(2);
            GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, vstride, 6 * sizeof(float));
        }

        private void CreateModernCapsule(int latSegmentsHemisphere, int radialSegments)
        {
            var verts = new List<float>();
            var indices = new List<uint>();
            var ringOffsets = new List<int>();

            // Bottom hemisphere
            for (int i = 0; i <= latSegmentsHemisphere; i++)
            {
                float v = (float)i / latSegmentsHemisphere;
                float phi = (-MathF.PI * 0.5f) + v * (MathF.PI * 0.5f);
                float y = MathF.Sin(phi) * 0.5f - 0.5f;
                float r = MathF.Cos(phi) * 0.5f;
                ringOffsets.Add(verts.Count / 8);
                for (int j = 0; j <= radialSegments; j++)
                {
                    float u = (float)j / radialSegments;
                    float theta = u * MathF.PI * 2f;
                    float x = MathF.Cos(theta) * r;
                    float z = MathF.Sin(theta) * r;
                    var n = new Vector3(x, y + 0.5f, z);
                    if (n.LengthSquared > 0f) n.Normalize();
                    verts.AddRange(new float[] { x, y, z, n.X, n.Y, n.Z, u, 0.25f * v });
                }
            }
            // Cylinder
            for (int i = 1; i < radialSegments; i++)
            {
                float t = (float)i / radialSegments;
                float y = -0.5f + t * 1.0f;
                float r = 0.5f;
                ringOffsets.Add(verts.Count / 8);
                for (int j = 0; j <= radialSegments; j++)
                {
                    float u = (float)j / radialSegments;
                    float theta = u * MathF.PI * 2f;
                    float x = MathF.Cos(theta) * r;
                    float z = MathF.Sin(theta) * r;
                    var n = new Vector3(x, 0f, z);
                    if (n.LengthSquared > 0f) n.Normalize();
                    verts.AddRange(new float[] { x, y, z, n.X, n.Y, n.Z, u, 0.25f + 0.5f * t });
                }
            }
            // Top hemisphere
            for (int i = 0; i <= latSegmentsHemisphere; i++)
            {
                float v = (float)i / latSegmentsHemisphere;
                float phi = v * (MathF.PI * 0.5f);
                float y = MathF.Sin(phi) * 0.5f + 0.5f;
                float r = MathF.Cos(phi) * 0.5f;
                ringOffsets.Add(verts.Count / 8);
                for (int j = 0; j <= radialSegments; j++)
                {
                    float u = (float)j / radialSegments;
                    float theta = u * MathF.PI * 2f;
                    float x = MathF.Cos(theta) * r;
                    float z = MathF.Sin(theta) * r;
                    var n = new Vector3(x, y - 0.5f, z);
                    if (n.LengthSquared > 0f) n.Normalize();
                    verts.AddRange(new float[] { x, y, z, n.X, n.Y, n.Z, u, 0.75f + 0.25f * v });
                }
            }

            for (int r = 0; r < ringOffsets.Count - 1; r++)
            {
                int base0 = ringOffsets[r];
                int base1 = ringOffsets[r + 1];
                for (int j = 0; j < radialSegments; j++)
                {
                    uint i0 = (uint)(base0 + j);
                    uint i1 = (uint)(base1 + j);
                    uint i2 = (uint)(base1 + j + 1);
                    uint i3 = (uint)(base0 + j + 1);
                    // Corrected winding: i0,i1,i2 for proper CCW from outside
                    indices.Add(i0); indices.Add(i1); indices.Add(i2);
                    indices.Add(i0); indices.Add(i2); indices.Add(i3);
                }
            }

            _capsuleIndexCount = indices.Count;
            _capsuleVao = GL.GenVertexArray();
            _capsuleVbo = GL.GenBuffer();
            _capsuleEbo = GL.GenBuffer();
            GL.BindVertexArray(_capsuleVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _capsuleVbo);
            var vArrCM = verts.ToArray();
            GL.BufferData(BufferTarget.ArrayBuffer, vArrCM.Length * sizeof(float), vArrCM, BufferUsageHint.StaticDraw);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _capsuleEbo);
            var iArrCM = indices.ToArray();
            GL.BufferData(BufferTarget.ElementArrayBuffer, iArrCM.Length * sizeof(uint), iArrCM, BufferUsageHint.StaticDraw);
            int vstride = 8 * sizeof(float);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, vstride, 0);
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, vstride, 3 * sizeof(float));
            GL.EnableVertexAttribArray(2);
            GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, vstride, 6 * sizeof(float));
        }

        public void SetCamera(float yawRad, float pitchRad, float distance)
        {
            _yaw = yawRad;
            _pitch = Math.Clamp(pitchRad, -1.553f, 1.553f);
            
            // CRITICAL: Only update distance/distanceGoal when NOT animating
            // During focus/frame animation, the renderer manages its own distance interpolation
            // and we must not overwrite _distanceGoal
            distance = Math.Max(0.01f, distance); // Zoom infini, minimum très proche
            
            if (!_camAnimating)
            {
                _distanceGoal = distance;
                _distance = distance;
            }
            // When animating, ignore the distance parameter - renderer's animation takes precedence
        }
        
        /// <summary>
        /// Définit directement les matrices de vue et de projection pour le rendu à partir d'une caméra
        /// </summary>
        public void SetCameraMatrices(Matrix4 viewMatrix, Matrix4 projectionMatrix)
        {
            _viewGL = viewMatrix;
            _projGL = projectionMatrix;
            _useCustomMatrices = true;
        }
        
        /// <summary>
        /// Revient au mode de caméra orbitale normale
        /// </summary>
        public void ResetToOrbitalCamera()
        {
            _useCustomMatrices = false;
        }

        /// <summary>
        /// Définit le mode de projection (0=Perspective, 1=Orthographic, 2=2D)
        /// </summary>
        public void SetProjectionMode(int mode, float orthoSize)
        {
            _projectionMode = mode;
            _orthoSize = orthoSize;
            // Persist projection mode & ortho size so UI changes are kept across sessions
            try
            {
                Editor.State.EditorSettings.CameraProjectionMode = _projectionMode;
                Editor.State.EditorSettings.CameraOrthoSize = _orthoSize;
            }
            catch { }
        }

        /// <summary>
        /// Crée la matrice de projection appropriée selon le mode de projection
        /// </summary>
        private Matrix4 CreateProjectionMatrix(float aspect)
        {
            return _projectionMode switch
            {
                1 => CreateOrthographicMatrix(aspect), // Orthographic
                2 => CreateOrthographicMatrix(aspect), // 2D (identique à ortho pour l'instant)
                _ => Matrix4.CreatePerspectiveFieldOfView(_fovY, aspect, _nearClip, _farClip) // Perspective
            };
        }

        private Matrix4 CreateOrthographicMatrix(float aspect)
        {
            float height = _orthoSize;
            float width = height * aspect;
            return Matrix4.CreateOrthographic(width, height, _nearClip, _farClip);
        }

        public void Pan(float dx, float dy)
        {
            _camAnimating = false;
            var forward = Forward();
            var right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, forward));
            var up = Vector3.Normalize(Vector3.Cross(forward, right));
            float s = _distance * 0.0025f;
            _target += (-dx * s) * right + (dy * s) * up;
        }
        public void CancelCameraAnimation() => _camAnimating = false;

        // ========================= RENDER =========================

        private bool HasValidSelection()
        {
            if (Scene == null) return false;
            if (Selection.ActiveEntityId == 0) return false;
            return Scene.GetById(Selection.ActiveEntityId) != null;
        }

        // ========================= WATER REFLECTION FRAMEBUFFER =========================
        
        private void InitReflectionFramebuffer()
        {
            // Calculate reflection texture resolution
            _reflectionW = Math.Max(1, (int)(_w * _reflectionResolutionScale));
            _reflectionH = Math.Max(1, (int)(_h * _reflectionResolutionScale));

            // Clean up old resources
            if (_reflectionTex != 0) GL.DeleteTexture(_reflectionTex);
            if (_reflectionDepthRbo != 0) GL.DeleteRenderbuffer(_reflectionDepthRbo);
            if (_reflectionFbo != 0) GL.DeleteFramebuffer(_reflectionFbo);

            // Create reflection framebuffer
            _reflectionFbo = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _reflectionFbo);

            // Create reflection color texture
            _reflectionTex = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _reflectionTex);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, _reflectionW, _reflectionH, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _reflectionTex, 0);

            // Create reflection depth renderbuffer
            _reflectionDepthRbo = GL.GenRenderbuffer();
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _reflectionDepthRbo);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.DepthComponent24, _reflectionW, _reflectionH);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, _reflectionDepthRbo);

            // Check framebuffer status
            var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != FramebufferErrorCode.FramebufferComplete)
            {
                LogManager.LogWarning($"Reflection FBO incomplete: {status}", "ViewportRenderer");
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        private Matrix4 CalculateReflectionMatrix(float waterLevel = 0f)
        {
            // Create a reflection matrix that flips Y around the water plane
            // This is a scale matrix that inverts Y, then translates to reflect around waterLevel
            return Matrix4.CreateScale(1f, -1f, 1f) * Matrix4.CreateTranslation(0f, 2f * waterLevel, 0f);
        }

        private Matrix4 CalculateObliqueMatrix(Matrix4 projection, Vector4 clipPlane)
        {
            // Calculate the clip-space corner point opposite the clipping plane
            // This is used to adjust the near plane to match the clipping plane
            Vector4 q;
            q.X = (MathF.Sign(clipPlane.X) + projection.M13) / projection.M11;
            q.Y = (MathF.Sign(clipPlane.Y) + projection.M23) / projection.M22;
            q.Z = -1.0f;
            q.W = (1.0f + projection.M33) / projection.M34;

            // Calculate the scaled plane vector
            Vector4 c = clipPlane * (2.0f / Vector4.Dot(clipPlane, q));

            // Replace the third row of the projection matrix
            projection.M31 = c.X;
            projection.M32 = c.Y;
            projection.M33 = c.Z + 1.0f;
            projection.M34 = c.W;

            return projection;
        }

        private void RenderEnvironmentForReflection(Matrix4 viewMatrix, Matrix4 projMatrix)
        {
            if (_scene?.Entities == null || _pbrShader == null) return;

            try
            {
                // NOTE: DO NOT set face culling here - it's already configured by the caller
                // The RenderReflectionPass sets CullFace(Front) to hide back faces correctly
                // If we set it here, it would override that configuration
                GL.Enable(EnableCap.CullFace);
                // GL.CullFace is already set by caller - don't override it!

                // Update Global UBO with reflection matrices
                _globalUniforms.ViewMatrix = viewMatrix;
                _globalUniforms.ProjectionMatrix = projMatrix;
                _globalUniforms.ViewProjectionMatrix = viewMatrix * projMatrix;

                // Get camera position from view matrix (inverse translation)
                var invView = viewMatrix.Inverted();
                _globalUniforms.CameraPosition = new Vector3(invView.M41, invView.M42, invView.M43);

                // NOTE: ClipPlaneEnabled and ClipPlane should already be set by caller
                // We just upload everything here

                // Upload to GPU
                GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, _globalUBO);
                GL.BindBuffer(BufferTarget.UniformBuffer, _globalUBO);
                GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero,
                    System.Runtime.InteropServices.Marshal.SizeOf<GlobalUniforms>(),
                    ref _globalUniforms);

                _pbrShader.Use();

                // Disable shadows and SSAO for reflection (performance optimization)
                _pbrShader.SetInt("u_SSAOEnabled", 0);
                _pbrShader.SetInt("u_UseShadows", 0);

                Guid lastBound = Guid.Empty;
                Engine.Rendering.MaterialRuntime? mr = null;
                int renderedCount = 0;

                foreach (var entity in _scene.Entities)
                {
                    // Skip water entities (avoid infinite recursion)
                    if (entity.HasComponent<Engine.Components.WaterComponent>()) continue;

                    // Render terrain as well so environment reflects properly
                    // (previously skipped for perf; enable for reflection pass)
                    // if (entity.HasComponent<Engine.Components.Terrain>()) continue;

                    // Only render mesh entities
                    var meshRenderer = entity.GetComponent<Engine.Components.MeshRendererComponent>();
                    if (meshRenderer == null) continue;
                    if (meshRenderer.MaterialGuid == null || meshRenderer.MaterialGuid == Guid.Empty) continue;

                    var materialGuid = meshRenderer.MaterialGuid.Value;

                    // Bind material if changed
                    if (materialGuid != lastBound)
                    {
                        // Use material cache
                        if (!_materialCache.TryGetValue(materialGuid, out mr))
                        {
                            try
                            {
                                var asset = Engine.Assets.AssetDatabase.LoadMaterial(materialGuid);
                                Func<Guid, string?> resolver = guid => Engine.Assets.AssetDatabase.TryGet(guid, out var rec) ? rec.Path : null;
                                mr = Engine.Rendering.MaterialRuntime.FromAsset(asset, resolver);
                                _materialCache[materialGuid] = mr;
                            }
                            catch
                            {
                                mr = new Engine.Rendering.MaterialRuntime { AlbedoTex = Engine.Rendering.TextureCache.White1x1 };
                            }
                        }

                        if (mr != null)
                        {
                            _pbrShader.Use();
                            float time = (float)_timeStopwatch.Elapsed.TotalSeconds;
                            mr.Bind(_pbrShader, time);
                        }

                        lastBound = materialGuid;
                    }

                    // Set per-object uniforms
                    entity.GetModelAndNormalMatrix(out var model, out var normalMat3);
                    _pbrShader.SetMat4("u_Model", model);
                    _pbrShader.SetMat3("u_NormalMat", normalMat3);
                    _pbrShader.SetUInt("u_ObjectId", entity.Id);

                    // Get mesh data based on mesh type
                    int vao = _cubeVao, ebo = _cubeEbo, idxCount = _cubeIdx.Length;
                    switch (meshRenderer.Mesh)
                    {
                        case MeshKind.Cube:
                            vao = _cubeVao; ebo = _cubeEbo; idxCount = _cubeIdx.Length; break;
                        case MeshKind.Plane:
                            vao = _planeVao; ebo = _planeEbo; idxCount = _planeIndexCount; break;
                        case MeshKind.Quad:
                            vao = _quadVao; ebo = _quadEbo; idxCount = _quadIndexCount; break;
                        case MeshKind.Sphere:
                            vao = _sphereVao; ebo = _sphereEbo; idxCount = _sphereIndexCount; break;
                        case MeshKind.Capsule:
                            vao = _capsuleVao; ebo = _capsuleEbo; idxCount = _capsuleIndexCount; break;
                    }

                    if (vao != 0 && ebo != 0 && idxCount > 0)
                    {
                        // Disable culling for double-sided meshes (plane only - quad is single-sided like Unity)
                        bool isDoubleSided = meshRenderer.Mesh == MeshKind.Plane;
                        if (isDoubleSided)
                        {
                            GL.Disable(EnableCap.CullFace);
                            if (_frameDrawCalls % 120 == 0) // Log every 2 seconds at 60fps
                                LogManager.LogInfo($"Plane: Culling DISABLED for rendering", "ViewportRenderer");
                        }

                        GL.BindVertexArray(vao);
                        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
                        GL.DrawElements(PrimitiveType.Triangles, idxCount, DrawElementsType.UnsignedInt, 0);
                        renderedCount++;

                        if (isDoubleSided) GL.Enable(EnableCap.CullFace);
                    }
                }

                // Render terrain in reflection (simplified for performance)
                if (_terrainRenderer != null)
                {
                    foreach (var entity in _scene.Entities)
                    {
                        if (entity.HasComponent<Engine.Components.Terrain>())
                        {
                            var terrain = entity.GetComponent<Engine.Components.Terrain>();
                            if (terrain != null)
                            {
                                try
                                {
                                    entity.GetModelAndNormalMatrix(out var terrainModel, out var terrainNormalMat);
                                    var viewPos = new OpenTK.Mathematics.Vector3(
                                        _globalUniforms.CameraPosition.X,
                                        _globalUniforms.CameraPosition.Y,
                                        _globalUniforms.CameraPosition.Z
                                    );

                                    var lightDir = new OpenTK.Mathematics.Vector3(
                                        _globalUniforms.DirLightDirection.X,
                                        _globalUniforms.DirLightDirection.Y,
                                        _globalUniforms.DirLightDirection.Z
                                    );

                                    var lightColor = new OpenTK.Mathematics.Vector3(
                                        _globalUniforms.DirLightColor.X,
                                        _globalUniforms.DirLightColor.Y,
                                        _globalUniforms.DirLightColor.Z
                                    );

                                    // Render terrain with MINIMAL quality for reflection (no shadows, no SSAO)
                                    _terrainRenderer.RenderTerrain(
                                        terrain,
                                        viewMatrix,
                                        projMatrix,
                                        viewPos,
                                        lightDir,
                                        lightColor,
                                        ssaoEnabled: false,
                                        ssaoTexture: 0,
                                        ssaoStrength: 0f,
                                        screenSize: new OpenTK.Mathematics.Vector2(_reflectionW, _reflectionH),
                                        shadowsEnabled: false,
                                        shadowTexture: 0,
                                        shadowMatrix: Matrix4.Identity,
                                        shadowBias: 0.1f,
                                        shadowMapSize: 1024f,
                                        shadowStrength: 0.7f,
                                        modelMatrix: terrainModel,
                                        shadowBiasConst: 0.004f,
                                        shadowSlopeScale: 1.5f,
                                        globalUBO: _globalUBO
                                    );
                                }
                                catch (Exception ex)
                                {
                                    LogManager.LogWarning($"Reflection: Error rendering terrain: {ex.Message}", "ViewportRenderer");
                                }
                            }
                        }
                    }
                }

                // Render skybox last (for proper depth testing)
                if (_skyboxRenderer != null)
                {
                    try
                    {
                        // Get environment for skybox tint
                        var env = _scene.Entities.FirstOrDefault(e => e.HasComponent<Engine.Components.EnvironmentSettings>())?.GetComponent<Engine.Components.EnvironmentSettings>();
                        var skyTint = env != null ? new OpenTK.Mathematics.Vector3(env.SkyboxTint[0], env.SkyboxTint[1], env.SkyboxTint[2]) : OpenTK.Mathematics.Vector3.One;
                        var skyExposure = env?.SkyboxExposure ?? 1.0f;

                        _skyboxRenderer.Render(viewMatrix, projMatrix, skyTint, skyExposure);
                    }
                    catch { }
                }

                if (Engine.Utils.DebugLogger.EnableVerbose)
                {
                    LogManager.LogVerbose($"Rendered {renderedCount} objects in reflection", "ViewportRenderer");
                }

                // Restore normal face culling
                GL.CullFace(TriangleFace.Back);
            }
            catch (Exception ex)
            {
                try { Engine.Utils.DebugLogger.Log($"[ViewportRenderer] Error rendering environment for reflection: {ex.Message}"); } catch { }

                // Ensure face culling is restored even on error
                GL.CullFace(TriangleFace.Back);
            }
        }

        /// <summary>
        /// NEW: Render planar reflection - simple and clean implementation
        /// </summary>
        private void RenderReflectionPass(float waterLevel = 0f)
        {
            if (_reflectionFbo == 0) return;

            // Save state
            var savedViewport = new int[4];
            GL.GetInteger(GetPName.Viewport, savedViewport);
            var savedView = _globalUniforms.ViewMatrix;
            var savedProj = _globalUniforms.ProjectionMatrix;
            var savedViewProj = _globalUniforms.ViewProjectionMatrix;
            var savedCamPos = _globalUniforms.CameraPosition;

            // Bind reflection FBO
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _reflectionFbo);
            GL.Viewport(0, 0, _reflectionW, _reflectionH);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
            GL.ClearColor(0.5f, 0.7f, 0.9f, 1.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            // Calculate reflected camera (mirror across water plane)
            Vector3 camPos = CameraPosition();
            Vector3 reflectedCamPos = new Vector3(camPos.X, 2f * waterLevel - camPos.Y, camPos.Z);
            Vector3 reflectedTarget = new Vector3(_target.X, 2f * waterLevel - _target.Y, _target.Z);

            // Standard LookAt with normal up vector
            var reflectedView = Matrix4.LookAt(
                new Vector3(reflectedCamPos.X, reflectedCamPos.Y, reflectedCamPos.Z),
                new Vector3(reflectedTarget.X, reflectedTarget.Y, reflectedTarget.Z),
                Vector3.UnitY
            );

            float aspect = _reflectionW / Math.Max(1.0f, (float)_reflectionH);
            var reflectedProj = CreateProjectionMatrix(aspect);

            // Enable clipping for reflection pass
            // Clip geometry BELOW the water plane (in world space)
            // Normal points UP (+Y), clip fragments where Y < waterLevel
            //
            // Add small offset to prevent terrain exactly at water level from appearing
            _globalUniforms.ClipPlaneEnabled = 1;
            _globalUniforms.ClipPlane = new Vector4(0f, 1f, 0f, -(waterLevel + 0.01f));

            // Render reflection with correct culling
            // The reflected camera is mirrored, so we need to flip the front face definition
            // This ensures we cull the correct faces and don't see the bottom of geometry
            try
            {
                var savedCullFaceEnabled = GL.IsEnabled(EnableCap.CullFace);
                var savedFrontFace = GL.GetInteger(GetPName.FrontFace);

                GL.Enable(EnableCap.CullFace);
                GL.CullFace(TriangleFace.Back); // Cull back faces as normal
                GL.FrontFace(FrontFaceDirection.Cw); // Flip for reflected camera (Y-mirrored)

                RenderEnvironmentForReflection(reflectedView, reflectedProj);

                // Restore state
                if (!savedCullFaceEnabled)
                    GL.Disable(EnableCap.CullFace);
                GL.FrontFace((FrontFaceDirection)savedFrontFace);
            }
            catch (Exception ex)
            {
                LogManager.LogWarning($"Reflection: Error: {ex.Message}", "ViewportRenderer");
            }

            // Restore state (use MSAA FBO if active)
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, GetTargetFBO());
            GL.Viewport(savedViewport[0], savedViewport[1], savedViewport[2], savedViewport[3]);
            _globalUniforms.ViewMatrix = savedView;
            _globalUniforms.ProjectionMatrix = savedProj;
            _globalUniforms.ViewProjectionMatrix = savedViewProj;
            _globalUniforms.CameraPosition = savedCamPos;
            _globalUniforms.ClipPlaneEnabled = 0;

            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, _globalUBO);
            GL.BindBuffer(BufferTarget.UniformBuffer, _globalUBO);
            GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero,
                System.Runtime.InteropServices.Marshal.SizeOf<GlobalUniforms>(),
                ref _globalUniforms);
        }

        // DEBUG: Frame counter (disabled)
        // private static int _debugFrameCount = 0;
        // private static System.Diagnostics.Stopwatch _debugFrameTimer = System.Diagnostics.Stopwatch.StartNew();

        public void RenderScene()
        {
            // Start frame timer to measure render time
            if (_frameTimer == null) _frameTimer = System.Diagnostics.Stopwatch.StartNew();
            _frameTimer.Restart();
            
            // === Clear any pending GL errors from previous frames ===
            // This prevents "sticky" errors from corrupting subsequent rendering
            ErrorCode error;
            int clearCount = 0;
            while ((error = GL.GetError()) != ErrorCode.NoError && clearCount < 10)
            {
                clearCount++;
            }

            // DEBUG: Count RenderScene calls (disabled)
            // _debugFrameCount++;
            // if (_debugFrameTimer.Elapsed.TotalSeconds >= 1.0)
            // {
            //     Console.WriteLine($"[PERF] RenderScene calls/sec: {_debugFrameCount}");
            //     _debugFrameCount = 0;
            //     _debugFrameTimer.Restart();
            // }

            // Reset per-frame stats for overlay/debugging
            _frameDrawCalls = 0;
            _frameTriangles = 0;
            _frameRenderedObjects = 0;


            // Ne forcer la caméra orbitale que si demandé (Viewport de l’éditeur)
            if (ForceEditorCamera)
                ResetToOrbitalCamera();

            bool hasSel = HasValidSelection();
            if (hasSel)
            {
                var ent = Scene!.GetById(Selection.ActiveEntityId)!;
                ent.GetWorldTRS(out _cubePos, out _cubeRot, out _cubeScale);
            }
            else
            {
                _activeId = ID_NONE;
                _dragKind = DragKind.None;
            }

            if (_gizmoVisible) _cubePos = _gizmoPos;

            UpdateCameraAnimation();

            // Si on n'utilise pas des matrices personnalisées, on calcule la caméra orbitale
            Vector3 camPos;
            if (!_useCustomMatrices)
            {
                float aspect = _w / Math.Max(1.0f, (float)_h);
                _projGL = CreateProjectionMatrix(aspect);
                _projGLNoJitter = _projGL; // Save copy before jitter

                // Apply TAA jitter to projection matrix (OPTIMIZED - minimal overhead)
                if (_taaRenderer != null && TAASettings.Enabled)
                {
                    var jitter = _taaRenderer.CalculateJitter();
                    // Apply jitter offset to projection matrix (sub-pixel offset)
                    _projGL.M31 += jitter.X * 2.0f; // X offset (column 3, row 1)
                    _projGL.M32 += jitter.Y * 2.0f; // Y offset (column 3, row 2)

                    // DEBUG: Log jitter once every 60 frames
                    if (_frameDrawCalls % 60 == 0)
                        LogManager.LogVerbose($"TAA Jitter active: ({jitter.X:F6}, {jitter.Y:F6})", "ViewportRenderer");
                }

                camPos = CameraPosition();
                var viewLH = LookAtLH(camPos, _target, Vector3.UnitY);
                _viewGL = viewLH * ZFlip;
            }
            else
            {
                camPos = Vector3.Zero;
            }

            // Update global uniforms (lighting, camera, etc.) BEFORE rendering
            UpdateGlobalUniforms();

            // MSAA: If enabled, render to MSAA framebuffer instead of regular FBO
            if (_msaaRenderer != null && _antiAliasingMode.IsMSAA())
            {
                _msaaRenderer.BeginRender();
            }
            else
            {
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
            }
            // Finalize a limited number of pending texture uploads prepared by TextureCache background loader.
            // This must run on the GL thread/context so GL texture handles can be created.
            try
            {
                var swUploads = System.Diagnostics.Stopwatch.StartNew();
                var uploads = Engine.Rendering.TextureCache.ProcessPendingUploads(10); // Increased from 1 to 10 for faster loading
                swUploads.Stop();

                if (uploads > 0)
                {
                    // New textures were uploaded this frame: clear material runtime cache so
                    // materials will re-resolve texture handles and display the newly uploaded images.
                    Engine.Rendering.MaterialRuntime.ClearGlobalCache();

                    // Also clear local material cache to force reload with new texture handles
                    _materialCache.Clear();
                    // PERFORMANCE: Disabled log
                    // Console.WriteLine("[ViewportRenderer] Cleared local material cache after texture upload");
                }

                // If the upload processing itself is slow, log it (helps find IO/CPU hotspots)
                if (swUploads.Elapsed.TotalMilliseconds > 2.0)
                {
                    try { LogManager.LogVerbose($"Texture upload processing took {swUploads.Elapsed.TotalMilliseconds:F2} ms (uploads={uploads})", "ViewportRenderer"); } catch { }
                }
            }
            catch (Exception ex)
            {
                try { LogManager.LogWarning($"TextureCache.ProcessPendingUploads failed: {ex.Message}", "ViewportRenderer"); } catch { }
            }
            // ✅ S'assurer que ce viewport utilise son UBO global
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, _globalUBO);
            GL.Disable(EnableCap.FramebufferSrgb);

            // Reset état GL
            GL.Disable(EnableCap.Blend);
            GL.Disable(EnableCap.ScissorTest);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);

            // ✅ TOUJOURS écrire dans les deux buffers
            GL.ColorMask(true, true, true, true);
            GL.ColorMask(1, true, true, true, true);
            var bufs = new[] { DrawBuffersEnum.ColorAttachment0, DrawBuffersEnum.ColorAttachment1 };
            GL.DrawBuffers(bufs.Length, bufs);

            GL.Viewport(0, 0, _w, _h);
            
            // Clear all buffers first - skybox will provide background color
            if (_gameMode)
            {
                GL.ClearColor(0.4f, 0.6f, 0.9f, 1f); // Couleur bleu ciel pour Game Mode
            }
            else
            {
                GL.ClearColor(0.15f, 0.16f, 0.18f, 1f); // Couleur grise pour Viewport
            }
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            // Clear ID texture (ColorAttachment1) to 0 for object picking
            uint zero = 0;
            GL.ClearBuffer(ClearBuffer.Color, 1, ref zero);

            // Grid will be rendered later as an overlay so it appears on top of the scene.

            // === RENDER PIPELINE WITH PROPER ORDERING ===

            // === QUEUE 1000: BACKGROUND/SKYBOX ===
            UpdateGlobalUniforms();
            RenderSkybox();


            // === QUEUE 2000: OPAQUE GEOMETRY ===
            try
            {
                // Debug: list entities with MeshRenderer and material GUIDs
                try
                {
                    var entsList = Scene?.Entities;
                    var ents = entsList != null ? System.Runtime.InteropServices.CollectionsMarshal.AsSpan(entsList).ToArray() : Array.Empty<Engine.Scene.Entity>();
                    int haveMesh = 0;
                    for (int i = 0; i < ents.Length; i++)
                    {
                        var e = ents[i];
                        if (e.HasComponent<Engine.Components.MeshRendererComponent>())
                        {
                            var mr = e.GetComponent<Engine.Components.MeshRendererComponent>();
                            if (mr != null && mr.MaterialGuid.HasValue && mr.MaterialGuid.Value != Guid.Empty)
                            {
                                haveMesh++;
                            }
                        }
                    }
                }
                catch { }

                // Render shadow maps for directional light before opaque rendering
                try {
                    var swSh = System.Diagnostics.Stopwatch.StartNew();
                    bool shadowsRendered = RenderShadowMaps();
                    swSh.Stop();
                    _lastShadowsMs = (float)swSh.Elapsed.TotalMilliseconds;
                    if (!shadowsRendered) {
                        if (Engine.Utils.DebugLogger.EnableVerbose) LogManager.LogVerbose("RenderShadowMaps returned false - shadows disabled", "ViewportRenderer");
                    }
                } catch (Exception ex) { 
                    if (Engine.Utils.DebugLogger.EnableVerbose) LogManager.LogVerbose($"RenderShadowMaps exception: {ex.Message}", "ViewportRenderer");
                }
                var swOpaque = System.Diagnostics.Stopwatch.StartNew();
                DrawForwardOpaque();
                swOpaque.Stop();
                _lastOpaqueMs = (float)swOpaque.Elapsed.TotalMilliseconds;
            }
            catch
            {
            }

            // === QUEUE 4000: OVERLAY ===
            // Light icons (always visible)
            RenderLightIcons();

            // Grid overlay placeholder: actual draw happens after post-processing so it's not overwritten

            // === POST-PROCESS EFFECTS ===
            // Note: Gizmos are rendered AFTER post-processing to avoid being detected by outline
            var swPost = System.Diagnostics.Stopwatch.StartNew();
            ApplyPostProcessEffects();
            swPost.Stop();

            // === GIZMOS RENDERED AFTER OUTLINE ===
            // Render gizmos into _postFbo so they appear on top of outline but not in ID texture during outline pass
            if (_gizmoVisible && HasValidSelection())
            {
                int targetFbo = (_postTexHealthy && _postFbo != 0) ? _postFbo : _fbo;
                if (targetFbo != 0)
                {
                    GL.BindFramebuffer(FramebufferTarget.Framebuffer, targetFbo);
                    var db = new DrawBuffersEnum[] { DrawBuffersEnum.ColorAttachment0 };
                    GL.DrawBuffers(db.Length, db);
                    GL.Viewport(0, 0, _w, _h);
                    
                    RenderGizmosOnTop();
                    
                    // Restore main FBO
                    GL.BindFramebuffer(FramebufferTarget.Framebuffer, GetTargetFBO());
                    var mainBufs = new DrawBuffersEnum[] { DrawBuffersEnum.ColorAttachment0, DrawBuffersEnum.ColorAttachment1 };
                    GL.DrawBuffers(mainBufs.Length, mainBufs);
                }
            }

            // === COLLIDER GIZMOS (wireframe), rendered on top ===
            if (_showColliderGizmos)
            {
                int targetFbo = (_postTexHealthy && _postFbo != 0) ? _postFbo : _fbo;
                if (targetFbo != 0)
                {
                    GL.BindFramebuffer(FramebufferTarget.Framebuffer, targetFbo);
                    var db = new DrawBuffersEnum[] { DrawBuffersEnum.ColorAttachment0 };
                    GL.DrawBuffers(db.Length, db);
                    GL.Viewport(0, 0, _w, _h);
                    
                    RenderColliderGizmosOnTop();
                    
                    // Restore main FBO
                    GL.BindFramebuffer(FramebufferTarget.Framebuffer, GetTargetFBO());
                    var mainBufs = new DrawBuffersEnum[] { DrawBuffersEnum.ColorAttachment0, DrawBuffersEnum.ColorAttachment1 };
                    GL.DrawBuffers(mainBufs.Length, mainBufs);
                }
            }

            // Render grid into the post-process target so it appears inside the renderer's ColorTexture
            try
            {
                if (_showGrid && _grid != null)
                {
                    int targetFbo = (_postTexHealthy && _postFbo != 0) ? _postFbo : _fbo;
                    if (targetFbo != 0)
                    {
                        try { if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[ViewportRenderer] Blitting grid into post target fbo={targetFbo}"); } catch { }
                        // Bind the post target and ensure we draw to its color attachment
                        GL.BindFramebuffer(FramebufferTarget.Framebuffer, targetFbo);
                        var db = new DrawBuffersEnum[] { DrawBuffersEnum.ColorAttachment0 };
                        GL.DrawBuffers(db.Length, db);
                        GL.Viewport(0, 0, _w, _h);

                        // Disable depth test during debug overlay so it is visible
                        bool depthWas = GL.IsEnabled(EnableCap.DepthTest);
                        GL.Disable(EnableCap.DepthTest);
                        _grid.Render(_viewGL, _projGL, camPos, _target, _fovY, _w, _h);
                        if (depthWas) GL.Enable(EnableCap.DepthTest);

                        // Restore main FBO so subsequent code can assume it (use MSAA FBO if active)
                        GL.BindFramebuffer(FramebufferTarget.Framebuffer, GetTargetFBO());
                        var mainBufs = new DrawBuffersEnum[] { DrawBuffersEnum.ColorAttachment0, DrawBuffersEnum.ColorAttachment1 };
                        GL.DrawBuffers(mainBufs.Length, mainBufs);
                    }
                }
            }
            catch { }
            _lastPostProcessMs = (float)swPost.Elapsed.TotalMilliseconds;

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            // After post-processing we are bound to the default framebuffer (presentation target).
            // Render the grid here so it won't be overwritten by post-processing.
            try
            {
                if (_showGrid && _grid != null)
                {
                    try { if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log("[ViewportRenderer] Rendering grid after post-process (default FB)"); } catch { }
                    // We want the grid to overlay on top of the final image; ensure depth test won't hide it during debug.
                    GL.Disable(EnableCap.DepthTest);
                    _grid.Render(_viewGL, _projGL, camPos, _target, _fovY, _w, _h);
                    GL.Enable(EnableCap.DepthTest);
                }
            }
            catch { }

            // Draw debug overlays after post-processing and after binding default framebuffer
            try { DrawShadowAtlasOverlay(); } catch { }
            GL.Enable(EnableCap.FramebufferSrgb);

            // Stop frame timer and record render time
            _frameTimer.Stop();
            _lastFrameCpuMs = (float)_frameTimer.Elapsed.TotalMilliseconds;

            // If a frame is slow, emit a compact perf summary to help debugging
            try
            {
                // PERFORMANCE: Disabled per-frame logging (causes severe FPS drops)
                // if (_lastFrameCpuMs > 16.0f) // > ~60 FPS threshold
                // {
                //     Console.WriteLine($"[PerfSummary] frameMs={_lastFrameCpuMs:F2}, drawCalls={_frameDrawCalls}, tris={_frameTriangles}, textures={Engine.Rendering.TextureCache.LoadedTextureCount}, texMem={Engine.Rendering.TextureCache.TotalMemoryUsed}");
                // }
            }
            catch { }
        }
        
    private SkyboxRenderer? _skyboxRenderer;
    
    private void RenderSkybox()
    {

        try
        {
            _skyboxRenderer ??= new SkyboxRenderer();
            var viewMat = _viewGL; // already LH->GL converted
            var projMat = _projGL;

            var envEntity = _scene.Entities.FirstOrDefault(e => e.HasComponent<EnvironmentSettings>());
            bool skyboxRendered = false;

            // Auto-create Environment entity if none exists
            if (envEntity == null)
            {
                envEntity = new Engine.Scene.Entity
                {
                    Id = _scene.GetNextEntityId(),
                    Name = "Environment",
                    Guid = System.Guid.NewGuid(),
                    Active = true
                };
                envEntity.AddComponent<Engine.Components.EnvironmentSettings>();
                _scene.Entities.Add(envEntity);
            }

            if (envEntity != null)
            {
                var env = envEntity.GetComponent<EnvironmentSettings>();

                // Auto-configure default skybox material if none is set
                if (env != null && string.IsNullOrWhiteSpace(env.SkyboxMaterialPath))
                {
                    // Create and configure a default procedural skybox material
                    try
                    {
                        var defaultSkybox = new SkyboxMaterialAsset
                        {
                            Guid = new Guid("99204530-428c-4238-84c4-ff773bda4e9a"),
                            Name = "Default Procedural Skybox",
                            Type = Engine.Assets.SkyboxType.Procedural,
                            SkyTint = new float[] { 0.5f, 0.8f, 1.0f, 1.0f }, // Light blue sky
                            GroundColor = new float[] { 0.369f, 0.349f, 0.341f, 1.0f }, // Brownish ground
                            Exposure = 1.3f,
                            AtmosphereThickness = 1.0f,
                            SunTint = new float[] { 1.0f, 0.95f, 0.8f, 1.0f }, // Warm sun
                            SunSize = 0.04f,
                            SunSizeConvergence = 5.0f
                        };
                        
                        var skyboxPath = Path.Combine(AssetDatabase.AssetsRoot, "Default Procedural Skybox.skymat");
                        Directory.CreateDirectory(Path.GetDirectoryName(skyboxPath)!);
                        SkyboxMaterialAsset.Save(skyboxPath, defaultSkybox);
                        AssetDatabase.Refresh(); // Make sure it's indexed
                        
                        env.SkyboxMaterialPath = defaultSkybox.Guid.ToString();
                    }
                    catch (Exception)
                    {
                    }
                }

                if (env != null && !string.IsNullOrWhiteSpace(env.SkyboxMaterialPath))
                {
                    var envTint = new OpenTK.Mathematics.Vector3(env.SkyboxTint.X, env.SkyboxTint.Y, env.SkyboxTint.Z);

                    // Try GUID first
                    if (Guid.TryParse(env.SkyboxMaterialPath, out Guid skyGuid))
                    {
                        if (AssetDatabase.TryGet(skyGuid, out var rec) &&
                            string.Equals(rec.Type, "SkyboxMaterial", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                var sky = SkyboxMaterialAsset.Load(rec.Path);
                                _skyboxRenderer.RenderWithMaterial(viewMat, projMat, sky, envTint, env.SkyboxExposure);
                                skyboxRendered = true;
                            }
                            catch (Exception)
                            {
                            }
                        }
                        else
                        {
                        }
                    }
                    // Try direct path
                    else if (System.IO.File.Exists(env.SkyboxMaterialPath))
                    {
                        try
                        {
                            var sky = SkyboxMaterialAsset.Load(env.SkyboxMaterialPath);
                            _skyboxRenderer.RenderWithMaterial(viewMat, projMat, sky, envTint, env.SkyboxExposure);
                            skyboxRendered = true;
                        }
                        catch (Exception)
                        {
                        }
                    }
                    else
                    {
                    }
                }
                else
                {
                }
            }
            else
            {
            }

            // Fallback: render a default procedural skybox if nothing was rendered
            if (!skyboxRendered)
            {
                _skyboxRenderer.CreateProceduralSkybox(
                    new OpenTK.Mathematics.Vector3(0.5f, 0.8f, 1.0f), // Sky blue
                    new OpenTK.Mathematics.Vector3(0.2f, 0.3f, 0.4f)  // Darker blue
                );
                _skyboxRenderer.Render(viewMat, projMat, OpenTK.Mathematics.Vector3.One, 1.0f);
            }
        }
        catch (Exception)
        {
        }
    }

    // ===================== SELECTION OUTLINE =====================
    private void RenderSelectionOutline()
        {
            if (_outlineRenderer == null || Scene == null) return;

            // Get outline settings from EditorSettings
            var outlineData = Editor.State.EditorSettings.Outline;
            if (!outlineData.Enabled) return;

            // Get selected entity ID
            uint selectedId = Editor.State.Selection.ActiveEntityId;
            if (selectedId == 0) return;

            // Convert settings to renderer format
            var settings = new Engine.Rendering.SelectionOutlineRenderer.OutlineSettings
            {
                Enabled = outlineData.Enabled,
                Thickness = outlineData.Thickness,
                Color = new Vector4(outlineData.ColorR, outlineData.ColorG, outlineData.ColorB, outlineData.ColorA),
                EnablePulse = outlineData.EnablePulse,
                PulseSpeed = outlineData.PulseSpeed,
                PulseMinAlpha = outlineData.PulseMinAlpha,
                PulseMaxAlpha = outlineData.PulseMaxAlpha
            };

            try
            {
                // DEBUG: Check texture sizes before rendering outline
                GL.BindTexture(TextureTarget.Texture2D, _postTex);
                GL.GetTexLevelParameter(TextureTarget.Texture2D, 0, GetTextureParameter.TextureWidth, out int postTexW);
                GL.GetTexLevelParameter(TextureTarget.Texture2D, 0, GetTextureParameter.TextureHeight, out int postTexH);

                GL.BindTexture(TextureTarget.Texture2D, _idTex);
                GL.GetTexLevelParameter(TextureTarget.Texture2D, 0, GetTextureParameter.TextureWidth, out int idTexW);
                GL.GetTexLevelParameter(TextureTarget.Texture2D, 0, GetTextureParameter.TextureHeight, out int idTexH);

                if (postTexW != _w || postTexH != _h || idTexW != _w || idTexH != _h)
                {
                    LogManager.LogWarning($"Texture size mismatch! _w={_w}, _h={_h}, postTex={postTexW}x{postTexH}, idTex={idTexW}x{idTexH}", "ViewportRenderer");
                }

                // Bind _postFbo2 as render target (NOT _postFbo) to avoid read/write conflict
                // We read from _postTex and write to _postFbo2, then copy back to _postFbo
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, _postFbo2);
                GL.Viewport(0, 0, _w, _h);

                // Render outline using _postTex (color) and _idTex (entity IDs)
                float time = (float)System.DateTime.Now.TimeOfDay.TotalSeconds;
                _outlineRenderer.RenderOutline(_postTex, _idTex, selectedId, _w, _h, settings, time);

                // Restore state
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
            }
            catch (Exception ex)
            {
                if (Engine.Utils.DebugLogger.EnableVerbose)
                    Engine.Utils.DebugLogger.Log($"[ViewportRenderer] Failed to render selection outline: {ex.Message}");
            }
        }

        // Calculate cascade split distances using practical split scheme (logarithmic + linear blend)
        // https://developer.nvidia.com/gpugems/gpugems3/part-ii-light-and-shadows/chapter-10-parallel-split-shadow-maps-programmable-gpus
        private float[] CalculateCascadeSplits(int cascadeCount, float near, float far, float lambda = 0.5f)
        {
            float[] splits = new float[cascadeCount + 1];
            splits[0] = near;
            splits[cascadeCount] = far;

            for (int i = 1; i < cascadeCount; i++)
            {
                float fIDM = (float)i / cascadeCount;
                // Logarithmic split
                float fLog = near * MathF.Pow(far / near, fIDM);
                // Linear split
                float fLinear = near + (far - near) * fIDM;
                // Blend between log and linear using lambda
                splits[i] = lambda * fLog + (1.0f - lambda) * fLinear;
            }

            return splits;
        }

        // ========================================================================
        // === NEW MODERN SHADOW SYSTEM ===
        // ========================================================================

        /// <summary>
        /// NEW: Modern shadow rendering using single directional light shadow map.
        /// Renders the scene from the light's perspective to generate the shadow map.
        /// </summary>
        private void RenderShadowPass()
        {
            var shadowSettings = Editor.State.EditorSettings.ShadowsSettings;

            // Check if shadow system is ready
            if (_shadowDepthShader == null || !shadowSettings.Enabled)
                return;

            // Check if shadow map size changed and recreate if needed
            int shadowMapSize = Math.Clamp(shadowSettings.ShadowMapSize, 512, 8192);
            if (_shadowManager == null || _shadowManager.ShadowMapSize != shadowMapSize)
            {
                LogManager.LogInfo($"Recreating ShadowManager with size {shadowMapSize} (was {_shadowManager?.ShadowMapSize ?? 0})", "ViewportRenderer");
                _shadowManager?.Dispose();
                _shadowManager = new Engine.Rendering.Shadows.ShadowManager(shadowMapSize);
            }

            // Get directional light direction from global uniforms
            Vector3 lightDir = _globalUniforms.DirLightDirection;
            if (lightDir.Length < 0.01f)
            {
                // Fallback to default light direction if not set
                lightDir = new Vector3(0.5f, -1.0f, 0.3f);
            }

            // Calculate scene bounds
            // Use camera position as scene center for dynamic coverage
            Vector3 sceneCenter = CameraPosition();
            float sceneRadius = shadowSettings.ShadowDistance; // From settings

            // Calculate light-space matrix (orthographic projection from light's viewpoint)
            _shadowManager.CalculateLightMatrix(lightDir, sceneCenter, sceneRadius);

            // Begin shadow rendering pass
            _shadowManager.BeginShadowPass();

            // Use shadow depth shader
            _shadowDepthShader.Use();
            _shadowDepthShader.SetMat4("u_LightSpaceMatrix", _shadowManager.LightSpaceMatrix);

            // Render all shadow-casting objects
            RenderShadowCasters();

            // End shadow pass (restores back-face culling)
            _shadowManager.EndShadowPass();
        }

        /// <summary>
        /// Render all objects that should cast shadows.
        /// This includes terrain and all objects with mesh filters.
        /// </summary>
        private void RenderShadowCasters()
        {
            if (_scene == null || _shadowDepthShader == null)
                return;

            // === Render Terrain ===
            foreach (var entity in _scene.Entities)
            {
                if (entity.HasComponent<Engine.Components.Terrain>())
                {
                    try
                    {
                        var terrain = entity.GetComponent<Engine.Components.Terrain>();
                        if (terrain == null) continue;

                        entity.GetModelAndNormalMatrix(out var model, out _);

                        _shadowDepthShader.SetMat4("u_Model", model);

                        // Render terrain mesh to shadow map
                        var camPos = CameraPosition();
                        terrain.Render(new System.Numerics.Vector3(camPos.X, camPos.Y, camPos.Z));
                    }
                    catch (Exception ex)
                    {
                        // Silently skip terrain that fails to render
                        LogManager.LogWarning($"Shadow: Failed to render terrain: {ex.Message}", "ViewportRenderer");
                    }
                }
            }

            // === Render Regular Objects with Mesh Renderer ===
            foreach (var entity in _scene.Entities)
            {
                if (entity.HasComponent<Engine.Components.MeshRendererComponent>())
                {
                    try
                    {
                        var meshRenderer = entity.GetComponent<Engine.Components.MeshRendererComponent>();
                        if (meshRenderer == null) continue;

                        entity.GetModelAndNormalMatrix(out var model, out _);
                        _shadowDepthShader.SetMat4("u_Model", model);

                        // Get mesh data based on mesh type. Handle imported custom meshes explicitly
                        int vao = 0, ebo = 0, idxCount = 0;
                        try
                        {
                            if (meshRenderer.IsUsingCustomMesh() && meshRenderer.CustomMeshGuid.HasValue)
                            {
                                var customMesh = LoadCustomMesh(meshRenderer.CustomMeshGuid.Value, meshRenderer.SubmeshIndex);
                                if (customMesh.HasValue)
                                {
                                    vao = customMesh.Value.VAO;
                                    ebo = customMesh.Value.EBO;
                                    idxCount = customMesh.Value.IndexCount;
                                }
                            }
                        }
                        catch { /* ignore custom mesh load failures and fallback to primitives below */ }

                        if (vao == 0 || idxCount == 0)
                        {
                            switch (meshRenderer.Mesh)
                            {
                                case Engine.Scene.MeshKind.Cube:
                                    vao = _cubeVao; ebo = _cubeEbo; idxCount = _cubeIdx.Length; break;
                                case Engine.Scene.MeshKind.Plane:
                                    vao = _planeVao; ebo = _planeEbo; idxCount = _planeIndexCount; break;
                                case Engine.Scene.MeshKind.Quad:
                                    vao = _quadVao; ebo = _quadEbo; idxCount = _quadIndexCount; break;
                                case Engine.Scene.MeshKind.Sphere:
                                    vao = _sphereVao; ebo = _sphereEbo; idxCount = _sphereIndexCount; break;
                                case Engine.Scene.MeshKind.Capsule:
                                    vao = _capsuleVao; ebo = _capsuleEbo; idxCount = _capsuleIndexCount; break;
                            }
                        }

                        // Render mesh to shadow map
                        if (vao != 0 && ebo != 0 && idxCount > 0)
                        {
                            GL.BindVertexArray(vao);
                            GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
                            GL.DrawElements(PrimitiveType.Triangles, idxCount, DrawElementsType.UnsignedInt, 0);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogManager.LogWarning($"Shadow: Failed to render mesh: {ex.Message}", "ViewportRenderer");
                    }
                }
            }

            GL.BindVertexArray(0);
        }

        // ========================================================================
        // === END NEW MODERN SHADOW SYSTEM ===
        // ========================================================================

        // Set shadow-related uniforms on a shader (both legacy and CSM)
        private void SetShadowUniforms(Engine.Rendering.ShaderProgram shader, bool enabled)
        {
            if (_shadowManager == null || !enabled)
            {
                shader.SetInt("u_UseShadows", 0);
                shader.SetInt("u_CascadeCount", 1); // Default to avoid shader errors
                
                // Initialize default cascade data to avoid uninitialized uniforms
                for (int i = 0; i < 4; i++)
                {
                    Matrix4 identity = Matrix4.Identity;
                    int locMat = GL.GetUniformLocation(shader.Handle, $"u_CascadeMatrices[{i}]");
                    if (locMat >= 0) GL.UniformMatrix4(locMat, false, ref identity);
                    
                    shader.SetFloat($"u_CascadeSplits[{i}]", 1000.0f);
                    shader.SetVec4($"u_AtlasTransforms[{i}]", new Vector4(1, 1, 0, 0));
                }
                return;
            }

            shader.SetInt("u_UseShadows", 1);
            shader.SetFloat("u_ShadowBias", Editor.State.EditorSettings.ShadowsSettings.ShadowBias);
            shader.SetFloat("u_ShadowMapSize", _shadowManager.ShadowMapSize);
            shader.SetFloat("u_ShadowStrength", Editor.State.EditorSettings.ShadowsSettings.ShadowStrength);
            shader.SetFloat("u_ShadowDistance", Editor.State.EditorSettings.ShadowsSettings.ShadowDistance);

            // Bind shadow map texture
            GL.ActiveTexture(TextureUnit.Texture17);
            GL.BindTexture(TextureTarget.Texture2D, _shadowManager.ShadowTexture);
            shader.SetInt("u_ShadowMap", 17);

            // === OLD CSM CODE (DEPRECATED - NEW SYSTEM HANDLES THIS DIFFERENTLY) ===
            // CSM uniforms - determine cascade count from settings
            int cascadeCount = 1; // Default to legacy mode - CSM disabled in new system
            shader.SetInt("u_CascadeCount", cascadeCount);

            // Send default cascade data (CSM methods removed from new ShadowManager)
            for (int i = 0; i < 4; i++)
            {
                Matrix4 identity = Matrix4.Identity;
                int locMat = GL.GetUniformLocation(shader.Handle, $"u_CascadeMatrices[{i}]");
                if (locMat >= 0) GL.UniformMatrix4(locMat, false, ref identity);

                shader.SetFloat($"u_CascadeSplits[{i}]", 1000.0f);
                shader.SetVec4($"u_AtlasTransforms[{i}]", new Vector4(1, 1, 0, 0));
            }

            // Legacy: single shadow matrix (use LightSpaceMatrix from new system)
            Matrix4 legacyMat = _shadowManager.LightSpaceMatrix;
            int locLegacy = GL.GetUniformLocation(shader.Handle, "u_ShadowMatrix");
            if (locLegacy >= 0) GL.UniformMatrix4(locLegacy, false, ref legacyMat);
        }

        // Render scene geometry for shadow mapping (shared between simple and cascaded)
        private void RenderSceneForShadows(Engine.Scene.LightingState lighting, Matrix4 lightSpace)
        {
            // Note: Shader program should already be bound by caller
            // Render all casters: entities with MeshRenderer and terrain
            var entitiesSpan = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_scene.Entities);
            for (int i = 0; i < entitiesSpan.Length; i++)
            {
                var e = entitiesSpan[i];
                var meshRenderer = e.GetComponent<MeshRendererComponent>();
                if (meshRenderer != null && meshRenderer.HasMeshToRender())
                {
                    e.GetModelAndNormalMatrix(out var model, out _);
                    int curProg = GL.GetInteger(GetPName.CurrentProgram);
                    int locModel = GL.GetUniformLocation(curProg, "u_Model");
                    if (locModel >= 0) GL.UniformMatrix4(locModel, false, ref model);

                    // Check if using custom mesh first
                    int vao, idxCount;
                    if (meshRenderer.IsUsingCustomMesh())
                    {
                        var customMesh = LoadCustomMesh(meshRenderer.CustomMeshGuid!.Value, meshRenderer.SubmeshIndex);
                        if (customMesh.HasValue)
                        {
                            vao = customMesh.Value.VAO;
                            idxCount = customMesh.Value.IndexCount;
                        }
                        else
                        {
                            // Fallback to cube if custom mesh fails to load
                            vao = _cubeVao;
                            idxCount = _cubeIdx.Length;
                        }
                    }
                    else
                    {
                        // Use primitive mesh
                        vao = _cubeVao;
                        idxCount = _cubeIdx.Length;
                        switch (meshRenderer.Mesh)
                        {
                            case MeshKind.Cube: vao = _cubeVao; idxCount = _cubeIdx.Length; break;
                            case MeshKind.Plane: vao = _planeVao; idxCount = _planeIndexCount; break;
                            case MeshKind.Quad: vao = _quadVao; idxCount = _quadIndexCount; break;
                            case MeshKind.Sphere: vao = _sphereVao; idxCount = _sphereIndexCount; break;
                            case MeshKind.Capsule: vao = _capsuleVao; idxCount = _capsuleIndexCount; break;
                        }
                    }

                    // Disable culling for double-sided meshes (plane only - quad is single-sided like Unity)
                    bool isDoubleSided = meshRenderer.Mesh == MeshKind.Plane;
                    if (isDoubleSided) GL.Disable(EnableCap.CullFace);

                    GL.BindVertexArray(vao);
                    GL.DrawElements(PrimitiveType.Triangles, idxCount, DrawElementsType.UnsignedInt, 0);

                    if (isDoubleSided) GL.Enable(EnableCap.CullFace);
                }

                // Terrain
                if (e.HasComponent<Engine.Components.Terrain>())
                {
                    var terrain = e.GetComponent<Engine.Components.Terrain>();
                    if (terrain != null)
                    {
                        e.GetModelAndNormalMatrix(out var model, out _);
                        int curProg2 = GL.GetInteger(GetPName.CurrentProgram);
                        int locModel2 = GL.GetUniformLocation(curProg2, "u_Model");
                        if (locModel2 >= 0) GL.UniformMatrix4(locModel2, false, ref model);
                        terrain.Render(new System.Numerics.Vector3(CameraPosition().X, CameraPosition().Y, CameraPosition().Z));
                    }
                }
            }
        }

        // Render cascaded shadow maps (CSM) - renders multiple shadow maps at different distances
        // for better shadow quality near the camera and wider coverage far from camera
        // === OLD CSM IMPLEMENTATION (DEPRECATED - NOT USED BY NEW SHADOW SYSTEM) ===
        private bool RenderCascadedShadowMaps(Editor.State.EditorSettings.ShadowsSettingsData shadowSettings, float near, float far)
        {
            // This function is deprecated and not used by the new modern shadow system
            // It's kept for reference only - new system uses RenderShadowPass() instead
            return false;

            /* OLD CSM CODE - COMMENTED OUT
            if (_shadowManager == null || _pbrShader == null || _shadowProg == 0) return false;

            // Get lighting info
            var lighting = Engine.Scene.Lighting.Build(_scene);

            int cascadeCount = Math.Clamp(shadowSettings.CascadeCountCSM, 2, 4);
            int shadowMapSize = _shadowManager.ShadowMapSize;

            // Calculate cascade splits using practical split scheme
            float[] splits = CalculateCascadeSplits(cascadeCount, near, far, shadowSettings.CascadeLambda);

            // Get light direction
            var lightDir = new Vector3(_globalUniforms.DirLightDirection.X, _globalUniforms.DirLightDirection.Y, _globalUniforms.DirLightDirection.Z);
            if (lightDir.LengthSquared > 0f) lightDir.Normalize();

            // Setup FBO for shadow rendering
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _shadowFbo);
            bool useColorDebug = shadowSettings.DebugShowShadowMap;
            var bufs = new DrawBuffersEnum[] { DrawBuffersEnum.ColorAttachment0 };
            GL.DrawBuffers(bufs.Length, bufs);
            GL.ReadBuffer(useColorDebug ? ReadBufferMode.ColorAttachment0 : ReadBufferMode.None);
            GL.Disable(EnableCap.Blend);
            GL.Enable(EnableCap.DepthTest);

            // Attach shadow map texture
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, 
                TextureTarget.Texture2D, _shadowManager.ShadowTexture, 0);

            // Create/attach color debug texture if needed
            if (_shadowDebugColorTex == 0 || _shadowDebugColorTexSize != shadowMapSize)
            {
                if (_shadowDebugColorTex != 0)
                    try { GL.DeleteTexture(_shadowDebugColorTex); } catch { }

                _shadowDebugColorTex = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2D, _shadowDebugColorTex);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, shadowMapSize, shadowMapSize, 
                    0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
                _shadowDebugColorTexSize = shadowMapSize;
            }
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, 
                TextureTarget.Texture2D, _shadowDebugColorTex, 0);

            // Validate FBO
            var fboStatus = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (fboStatus != FramebufferErrorCode.FramebufferComplete)
            {
                LogManager.LogError($"CSM: Shadow FBO incomplete: {fboStatus}", "ViewportRenderer");
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
                return false;
            }

            // Clear entire shadow atlas
            GL.Viewport(0, 0, shadowMapSize, shadowMapSize);
            GL.Clear(useColorDebug ? (ClearBufferMask.DepthBufferBit | ClearBufferMask.ColorBufferBit) : ClearBufferMask.DepthBufferBit);

            // Configure cascade layout (2x2 atlas for 4 cascades, 2x1 for 2-3 cascades)
            int tilesX = cascadeCount == 4 ? 2 : cascadeCount;
            int tilesY = cascadeCount == 4 ? 2 : 1;
            int tileSize = shadowMapSize / Math.Max(tilesX, tilesY);

            _shadowManager.SetCascadeCount(cascadeCount);

            // Render each cascade into its atlas tile
            for (int c = 0; c < cascadeCount; c++)
            {
                float cascadeNear = splits[c];
                float cascadeFar = splits[c + 1];

                // Calculate frustum corners for this cascade
                Vector3[] frustumCorners = CalculateFrustumCornersWorldSpace(cascadeNear, cascadeFar);

                // Calculate light space bounding box
                Vector3 frustumCenter = Vector3.Zero;
                foreach (var corner in frustumCorners) frustumCenter += corner;
                frustumCenter /= frustumCorners.Length;

                // CRITICAL STABILIZATION: Use FIXED light view matrix independent of frustum
                // This prevents shadow swimming when camera rotates
                // Position light far away from world origin, looking at world origin
                Vector3 worldCenter = Vector3.Zero; // Fixed point in world space
                float lightDistance = 500f;
                var lightPos = worldCenter - lightDir * lightDistance;
                var lightView = LookAtLH(lightPos, worldCenter, Vector3.UnitY) * ZFlip;

                // Transform corners to light space and compute bounding sphere
                Vector3 lsCenter = Vector3.Zero;
                int cornerCount = 0;
                foreach (var corner in frustumCorners)
                {
                    var lc = Vector4.TransformRow(new Vector4(corner, 1f), lightView);
                    lsCenter += new Vector3(lc.X, lc.Y, lc.Z);
                    cornerCount++;
                }
                if (cornerCount > 0) lsCenter /= cornerCount;

                // Compute radius as max distance to center in XY plane (we'll make a square around that)
                float radius = 0f;
                float minZ = float.MaxValue, maxZ = float.MinValue;
                foreach (var corner in frustumCorners)
                {
                    var lc = Vector4.TransformRow(new Vector4(corner, 1f), lightView);
                    var v = new Vector3(lc.X, lc.Y, lc.Z);
                    float dx = v.X - lsCenter.X;
                    float dy = v.Y - lsCenter.Y;
                    float d = MathF.Sqrt(dx * dx + dy * dy);
                    if (d > radius) radius = d;
                    if (v.Z < minZ) minZ = v.Z;
                    if (v.Z > maxZ) maxZ = v.Z;
                }

                // Square size that encloses the circle
                float halfSize = radius;
                float worldSize = halfSize * 2.0f;

                // Compute texel size from worldSize to snap center on texel grid
                float texelSize = worldSize / tileSize;
                if (texelSize <= 0f) texelSize = 1e-6f;

                // Snap center to texel grid to stabilize
                lsCenter.X = MathF.Round(lsCenter.X / texelSize) * texelSize;
                lsCenter.Y = MathF.Round(lsCenter.Y / texelSize) * texelSize;

                // Build ortho bounds as square centered on snapped center
                float minX = lsCenter.X - halfSize;
                float maxX = lsCenter.X + halfSize;
                float minY = lsCenter.Y - halfSize;
                float maxY = lsCenter.Y + halfSize;

                // Extend Z range a bit to cover receiver geometry
                minZ -= 200f;
                maxZ += 50f;
                float orthoNear = minZ - 10f;
                float orthoFar = maxZ + 10f;

                Matrix4 ortho = Matrix4.CreateOrthographicOffCenter(minX, maxX, minY, maxY, orthoNear, orthoFar);
                Matrix4 lightSpace = lightView * ortho;

                // Store cascade data in ShadowManager  
                _shadowManager.SetCascadeMatrix(c, lightSpace);
                _shadowManager.SetCascadeSplit(c, cascadeFar);

                // Calculate atlas transform (scale/offset to map to tile)
                int tileX = c % tilesX;
                int tileY = c / tilesX;
                // Flip tileY so tile (0,0) corresponds to top-left in screen coordinates vs texture coords if needed
                int tileYFlipped = (tilesY - 1) - tileY;
                float scaleX = 1.0f / tilesX;
                float scaleY = 1.0f / tilesY;
                float offsetX = tileX * scaleX;
                float offsetY = tileYFlipped * scaleY;
                _shadowManager.SetAtlasTransform(c, new Vector4(scaleX, scaleY, offsetX, offsetY));

                // DEBUG: Project a couple of world points using the cascade matrix to inspect atlas UV mapping
                if (useColorDebug)
                {
                    Vector4 originLS = Vector4.TransformRow(new Vector4(0f, 0f, 0f, 1f), lightSpace);
                    Vector3 originProj = new Vector3(originLS.X, originLS.Y, originLS.Z) / originLS.W;
                    originProj = originProj * 0.5f + new Vector3(0.5f);
                    Vector2 originUV = new Vector2(originProj.X * scaleX + offsetX, originProj.Y * scaleY + offsetY);

                    var camPos = CameraPosition();
                    Vector4 camLS = Vector4.TransformRow(new Vector4(camPos.X, camPos.Y, camPos.Z, 1f), lightSpace);
                    Vector3 camProj = new Vector3(camLS.X, camLS.Y, camLS.Z) / camLS.W;
                    camProj = camProj * 0.5f + new Vector3(0.5f);
                    Vector2 camUV = new Vector2(camProj.X * scaleX + offsetX, camProj.Y * scaleY + offsetY);

                    LogManager.LogVerbose($"CSM DEBUG Cascade {c} tile=({tileX},{tileY}) atlas=({scaleX:F3},{scaleY:F3},{offsetX:F3},{offsetY:F3}) originUV=({originUV.X:F3},{originUV.Y:F3}) camUV=({camUV.X:F3},{camUV.Y:F3}) radius={radius:F2}", "ViewportRenderer");
                }

                // Set viewport for this tile (use flipped Y to match atlas texture coordinates)
                GL.Viewport(tileX * tileSize, tileYFlipped * tileSize, tileSize, tileSize);
                
                // CRITICAL: Enable scissor test to restrict clear to this tile
                // GL.Clear() is NOT affected by viewport, only by scissor!
                GL.Enable(EnableCap.ScissorTest);
                GL.Scissor(tileX * tileSize, tileYFlipped * tileSize, tileSize, tileSize);
                
                // DEBUG: Clear each cascade with different color to see atlas layout
                if (useColorDebug)
                {
                    float[] debugColors = new float[] { 
                        1f, 0f, 0f, 1f,  // Red for cascade 0
                        0f, 1f, 0f, 1f,  // Green for cascade 1
                        0f, 0f, 1f, 1f,  // Blue for cascade 2
                        1f, 1f, 0f, 1f   // Yellow for cascade 3
                    };
                    GL.ClearColor(debugColors[c * 4], debugColors[c * 4 + 1], debugColors[c * 4 + 2], debugColors[c * 4 + 3]);
                    GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                    GL.ClearColor(0f, 0f, 0f, 1f); // Reset
                }
                else
                {
                    // Clear depth only for this tile
                    GL.Clear(ClearBufferMask.DepthBufferBit);
                }
                
                // Disable scissor for rendering (viewport is enough for rendering)
                GL.Disable(EnableCap.ScissorTest);

                // Render scene depth for this cascade
                if (_shadowProg != 0 || (_shadowColorProg != 0 && useColorDebug))
                {
                    int prog = (useColorDebug && _shadowColorProg != 0) ? _shadowColorProg : _shadowProg;
                    GL.UseProgram(prog);
                    int loc = GL.GetUniformLocation(prog, "u_LightMatrix");
                    if (loc >= 0) GL.UniformMatrix4(loc, false, ref lightSpace);

                    RenderSceneForShadows(lighting, lightSpace);
                }
            }

            // Restore state (use MSAA FBO if active)
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, GetTargetFBO());
            GL.Viewport(0, 0, _w, _h);
            GL.UseProgram(0);

            // Legacy: store first cascade as main matrix for compatibility
            // COMMENTED OUT - CSM methods removed from new ShadowManager
            // _shadowManager.SetLightMatrix(_shadowManager.GetCascadeMatrix(0));

            return true;
            */ // END OLD CSM CODE
        }

        // Calculate frustum corners in world space for a specific cascade (near/far distances from camera)
        private Vector3[] CalculateFrustumCornersWorldSpace(float nearDist, float farDist)
        {
            // Get camera position in world space (use existing method, more reliable)
            var cameraWorldPos = CameraPosition();
            
            // Extract camera orientation from INVERSE of view matrix
            // View matrix transforms World -> View, so inverse gives View -> World
            Matrix4 invView = _viewGL.Inverted();
            
            // Extract basis vectors from inverse view matrix (columns are world-space axes)
            Vector3 right = new Vector3(invView.M11, invView.M21, invView.M31);    // X-axis in world space
            Vector3 up = new Vector3(invView.M12, invView.M22, invView.M32);       // Y-axis in world space
            Vector3 forward = new Vector3(-invView.M13, -invView.M23, -invView.M33); // -Z-axis (OpenGL convention)
            
            right.Normalize();
            up.Normalize();
            forward.Normalize();
            
            // Calculate frustum dimensions at near and far planes using FOV
            // Extract FOV from projection matrix: tan(fov/2) = 1/P[1,1] for symmetric frustum
            float tanHalfFovY = 1.0f / _projGL.M22;
            float aspect = _projGL.M22 / _projGL.M11; // aspect = width/height
            
            float nearHeight = 2.0f * tanHalfFovY * nearDist;
            float nearWidth = nearHeight * aspect;
            float farHeight = 2.0f * tanHalfFovY * farDist;
            float farWidth = farHeight * aspect;
            
            // Calculate center points of near and far planes
            Vector3 nearCenter = cameraWorldPos + forward * nearDist;
            Vector3 farCenter = cameraWorldPos + forward * farDist;
            
            // Calculate 8 frustum corners
            Vector3[] corners = new Vector3[8];
            
            // Near plane (4 corners)
            corners[0] = nearCenter - right * (nearWidth * 0.5f) - up * (nearHeight * 0.5f); // Bottom-left
            corners[1] = nearCenter + right * (nearWidth * 0.5f) - up * (nearHeight * 0.5f); // Bottom-right
            corners[2] = nearCenter - right * (nearWidth * 0.5f) + up * (nearHeight * 0.5f); // Top-left
            corners[3] = nearCenter + right * (nearWidth * 0.5f) + up * (nearHeight * 0.5f); // Top-right
            
            // Far plane (4 corners)
            corners[4] = farCenter - right * (farWidth * 0.5f) - up * (farHeight * 0.5f); // Bottom-left
            corners[5] = farCenter + right * (farWidth * 0.5f) - up * (farHeight * 0.5f); // Bottom-right
            corners[6] = farCenter - right * (farWidth * 0.5f) + up * (farHeight * 0.5f); // Top-left
            corners[7] = farCenter + right * (farWidth * 0.5f) + up * (farHeight * 0.5f); // Top-right
            
            return corners;
        }

        // Render cascaded shadow maps into the shadow atlas and upload matrices to ShadowManager
        // === OLD SHADOW IMPLEMENTATION (DEPRECATED - NOT USED BY NEW SHADOW SYSTEM) ===
        // Returns true if the pass executed (or attempted to render). Returns false when skipped
        // due to settings or missing directional light.
        private bool RenderShadowMaps()
        {
            // This function is deprecated and not used by the new modern shadow system
            // It's kept for reference only - new system uses RenderShadowPass() instead
            return false;

            /* OLD CODE - COMMENTED OUT
            if (_shadowManager == null || _pbrShader == null) return false;

            var shadowSettings = Editor.State.EditorSettings.ShadowsSettings;
            if (!shadowSettings.Enabled) return false;

            // Determine whether the active directional light should cast shadows
            var lighting = Engine.Scene.Lighting.Build(_scene);
            // Debug: log lighting/shadow settings to help trace why shadow pass may be skipped
            // Shadow settings logged only on errors
            if (!lighting.HasDirectional)
            {
                try { LogManager.LogVerbose("Shadows: Skipping - no directional light present in scene.", "ViewportRenderer"); } catch { }
                return false;
            }
            if (!lighting.DirCastShadows)
            {
                try { LogManager.LogVerbose("Shadows: Skipping - directional light set to not cast shadows (DirCastShadows=false).", "ViewportRenderer"); } catch { }
                return false;
            }

            // Shadow map size from settings (support up to 8K for high quality shadows)
            int shadowMapSize = Math.Clamp(shadowSettings.ShadowMapSize, 512, 8192);

            // Only recreate shadow manager if shadow map size changed
            if (_shadowManager == null || _shadowManager.ShadowMapSize != shadowMapSize)
            {
                LogManager.LogInfo($"Recreating ShadowManager with size {shadowMapSize} (was {_shadowManager?.ShadowMapSize ?? 0})", "ViewportRenderer");
                _shadowManager?.Dispose();
                _shadowManager = new Engine.Rendering.Shadows.ShadowManager(shadowMapSize);
            }

            float near = _nearClip;
            float far = _farClip;

            // Choose between simple single shadow map or cascaded shadow maps
            // NOTE: CSM is currently disabled/removed. Force legacy single shadow map path to
            // ensure simple shadows remain functional and avoid running CSM-specific code.
            if (shadowSettings.UseCascadedShadows)
            {
                // Even if the setting is true, bypass the CSM path to avoid executing removed code.
                // Keep fallback to single map below.
                // return RenderCascadedShadowMaps(shadowSettings, near, far);
            }

            // Single shadow map covering the entire camera frustum (legacy mode)
            // Inverse of projection * view maps from NDC/clip space back to world
            var camInv = (_projGL * _viewGL).Inverted();

            // Bind shadow FBO and attach atlas texture as depth
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _shadowFbo);
            // TEMP FIX: Always use color attachment to avoid depth-only framebuffer issues
            // Some drivers don't handle depth-only framebuffers correctly
            bool useColorDebug = Editor.State.EditorSettings.ShadowsSettings.DebugShowShadowMap;
            // Force color attachment even when debug is off
            var bufs = new DrawBuffersEnum[] { DrawBuffersEnum.ColorAttachment0 };
            GL.DrawBuffers(bufs.Length, bufs);
            if (useColorDebug)
            {
                GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            }
            else
            {
                // Don't read from color when debug is off, but still attach it
                GL.ReadBuffer(ReadBufferMode.None);
            }
            GL.Viewport(0, 0, shadowMapSize, shadowMapSize);
            GL.Disable(EnableCap.Blend);
            GL.Enable(EnableCap.DepthTest);

            // Single shadow map covering the entire camera frustum
            {

                // Compute shadow map coverage based on the camera frustum up to the
                // configured ShadowDistance. This produces a tighter projection and
                // reduces edge artifacts compared to a fixed world-space box.
                float shadowMapWorldSize = shadowSettings.ShadowDistance; // User-configured distance

                // Build frustum corners in world space from camera near to near+shadowDistance
                float shadowNear = _nearClip;
                float shadowFar = Math.Min(_farClip, _nearClip + shadowMapWorldSize);
                Vector3[] frustumCorners = CalculateFrustumCornersWorldSpace(shadowNear, shadowFar);

                // Compute bounding box of frustum slice in light space
                var lightDir = new Vector3(_globalUniforms.DirLightDirection.X, _globalUniforms.DirLightDirection.Y, _globalUniforms.DirLightDirection.Z);
                if (lightDir.LengthSquared > 0f) lightDir.Normalize();
                
                // Use scene directional light as-is (no forced override)
                // Light direction logged only on startup

                // STABLE: Use frustum center but with REAL light direction
                Vector3 frustumCenter = Vector3.Zero; // World origin
                foreach (var corner in frustumCorners) frustumCenter += corner;
                frustumCenter /= frustumCorners.Length;

                // Use REAL light direction but fixed distance to maintain stability
                const float lightDistance = 500f; // Fixed distance from scene center
                var lightPos = frustumCenter - lightDir * lightDistance; // Position light away from scene
                var lightTarget = frustumCenter; // Look at scene center
                var lightView = LookAtLH(lightPos, lightTarget, Vector3.UnitY) * ZFlip;

                // Transform corners to light space
                Vector3 min = new Vector3(float.MaxValue), max = new Vector3(float.MinValue);
                foreach (var corner in frustumCorners)
                {
                    var lc = Vector4.TransformRow(new Vector4(corner, 1f), lightView);
                    min = Vector3.ComponentMin(min, new Vector3(lc.X, lc.Y, lc.Z));
                    max = Vector3.ComponentMax(max, new Vector3(lc.X, lc.Y, lc.Z));
                }

                // Debug log Z range
                // Z range logged only on startup

                // Frustum corners logged only on startup

                // Stabilize shadows: round to texel boundaries to prevent swimming
                float rangeX = max.X - min.X;
                float rangeY = max.Y - min.Y;
                // Guard against degenerate tiny ranges
                if (rangeX <= 1e-6f) rangeX = 1e-3f; // minimal width
                if (rangeY <= 1e-6f) rangeY = 1e-3f;

                // Use the larger range for texel size to maintain square texels
                float maxRange = Math.Max(rangeX, rangeY);
                float texelSize = maxRange / shadowMapSize;
                if (texelSize <= 0f) texelSize = 1e-6f;

                // IMPROVED: Snap center to texel grid to eliminate swimming
                Vector3 center = (min + max) * 0.5f;
                
                // Snap center to texel boundaries to prevent swimming
                center.X = MathF.Round(center.X / texelSize) * texelSize;
                center.Y = MathF.Round(center.Y / texelSize) * texelSize;
                
                float halfSizeX = MathF.Ceiling(rangeX * 0.5f / texelSize) * texelSize;
                float halfSizeY = MathF.Ceiling(rangeY * 0.5f / texelSize) * texelSize;

                min.X = center.X - halfSizeX;
                max.X = center.X + halfSizeX;
                min.Y = center.Y - halfSizeY;
                max.Y = center.Y + halfSizeY;

                // Extend Z range to capture more geometry
                // IMPROVED: Extend much more to avoid clipping large terrain features
                min.Z -= 2000f; // Extend backwards significantly for large scenes
                max.Z += 500f;  // Front extension for overhangs and elevated features

                // Compute orthographic near/far explicitly from light-space Z bounds.
                float minZ = min.Z;
                float maxZ = max.Z;
                if (minZ > maxZ)
                {
                    var t = minZ; minZ = maxZ; maxZ = t;
                }
                const float zPad = 10.0f;
                float orthoNear = minZ - zPad;
                float orthoFar = maxZ + zPad;

                // Ortho params logged only on startup

                // Create orthographic projection covering the frustum slice
                // FIXED: Always use calculated bounds, no more debug override that masks issues
                Matrix4 ortho = Matrix4.CreateOrthographicOffCenter(min.X, max.X, min.Y, max.Y, orthoNear, orthoFar);
                // FIXED: Use view * projection order (the alternative gave better results)
                var lightSpace = lightView * ortho;
                
                // Debug: Log the corrected transformation
                var testPoint = new Vector4(29.172f, 128.543f, -26.804f, 1.0f);
                var transformed = Vector4.TransformRow(testPoint, lightSpace);
                var ndc = new Vector3(transformed.X, transformed.Y, transformed.Z) / transformed.W;
                // NDC logged only on startup

                // Store matrix into ShadowManager
                // We currently render a single (non-CSM) shadow map here - make sure
                // the ShadowManager receives a compatible CSM fallback so shaders
                // that expect cascade arrays will have valid data.
                _shadowManager.SetCascadeCount(1);
                _shadowManager.SetCascadeMatrix(0, lightSpace);
                // Use shadowDistance as a reasonable split value (not used when cascadeCount==1)
                _shadowManager.SetCascadeSplit(0, shadowMapWorldSize);
                // Atlas transform for single full-map: scale=1, offset=0
                _shadowManager.SetAtlasTransform(0, new Vector4(1.0f, 1.0f, 0.0f, 0.0f));
                // Keep legacy single matrix for compatibility
                _shadowManager.SetLightMatrix(lightSpace);

                // Attach shadow map as depth target
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, _shadowManager.ShadowTexture, 0);
                // ALWAYS create and attach color texture (required for some drivers, even if not used)
                bool useColorDebugTile = Editor.State.EditorSettings.ShadowsSettings.DebugShowShadowMap;

                // Create or resize color texture if needed
                if (_shadowDebugColorTex == 0 || _shadowDebugColorTexSize != shadowMapSize)
                {
                    // Delete old texture if it exists
                    if (_shadowDebugColorTex != 0)
                    {
                        try { GL.DeleteTexture(_shadowDebugColorTex); } catch { }
                    }

                    // Create new texture at the correct size
                    _shadowDebugColorTex = GL.GenTexture();
                    GL.BindTexture(TextureTarget.Texture2D, _shadowDebugColorTex);
                    GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, shadowMapSize, shadowMapSize, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
                    _shadowDebugColorTexSize = shadowMapSize;
                }
                // Always attach color texture to make framebuffer complete
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _shadowDebugColorTex, 0);

                // Validate FBO completeness (helps detect if driver rejects depth-only framebuffer)
                var fboStatus = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
                if (fboStatus != FramebufferErrorCode.FramebufferComplete)
                {
                    LogManager.LogError($"Shadows: Shadow FBO incomplete: {fboStatus}", "ViewportRenderer");
                    // Unbind and abort shadow rendering to avoid undefined behavior
                    GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
                    GL.UseProgram(0);
                    return false;
                }

                // Clear depth buffer for entire shadow map
                if (useColorDebugTile)
                    GL.Clear(ClearBufferMask.DepthBufferBit | ClearBufferMask.ColorBufferBit);
                else
                    GL.Clear(ClearBufferMask.DepthBufferBit);


                // Render scene depth using simple depth-only shader (_shadowProg)
                if (_shadowProg != 0)
                {
                    // Rendering shadow map logged only on startup
                    // Choose depth-only or color debug shader and set the uniform on
                    // the program that is actually bound (avoid using locations from
                    // other programs on the current program - that's invalid).
                    if (useColorDebugTile && _shadowColorProg != 0)
                    {
                        GL.UseProgram(_shadowColorProg);
                        int locColor = GL.GetUniformLocation(_shadowColorProg, "u_LightMatrix");
                        if (locColor >= 0) GL.UniformMatrix4(locColor, false, ref lightSpace);
                    }
                    else
                    {
                        GL.UseProgram(_shadowProg);
                        int locDepth = GL.GetUniformLocation(_shadowProg, "u_LightMatrix");
                        if (locDepth >= 0) GL.UniformMatrix4(locDepth, false, ref lightSpace);
                    }

                    // Use polygon offset to reduce shadow acne while keeping bias small
                    GL.Enable(EnableCap.PolygonOffsetFill);
                    // Use editor-controlled polygon offset values
                    GL.PolygonOffset(Editor.State.EditorSettings.ShadowsSettings.PolygonOffsetFactor,
                                     Editor.State.EditorSettings.ShadowsSettings.PolygonOffsetUnits);

                    // Render all casters: entities with MeshRenderer and terrain
                    var entitiesSpan = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_scene.Entities);
                    for (int i = 0; i < entitiesSpan.Length; i++)
                    {
                        var e = entitiesSpan[i];
                        var meshRenderer = e.GetComponent<MeshRendererComponent>();
                        if (meshRenderer != null && meshRenderer.HasMeshToRender())
                        {
                            e.GetModelAndNormalMatrix(out var model, out _);
                            // Set u_Model on the program that is currently bound (could be _shadowProg or _shadowColorProg)
                            int curProg = GL.GetInteger(GetPName.CurrentProgram);
                            int locModel = GL.GetUniformLocation(curProg, "u_Model");
                            if (locModel >= 0) GL.UniformMatrix4(locModel, false, ref model);

                            // Draw using VAO bound earlier in BuildRenderList mapping
                            int vao = _cubeVao, idxCount = _cubeIdx.Length;
                            switch (meshRenderer.Mesh)
                            {
                                case MeshKind.Cube: vao = _cubeVao; idxCount = _cubeIdx.Length; break;
                                case MeshKind.Plane: vao = _planeVao; idxCount = _planeIndexCount; break;
                                case MeshKind.Quad: vao = _quadVao; idxCount = _quadIndexCount; break;
                                case MeshKind.Sphere: vao = _sphereVao; idxCount = _sphereIndexCount; break;
                                case MeshKind.Capsule: vao = _capsuleVao; idxCount = _capsuleIndexCount; break;
                            }
                            // Disable culling for double-sided meshes (plane, quad)
                            bool isDoubleSided = meshRenderer.Mesh == MeshKind.Plane || meshRenderer.Mesh == MeshKind.Quad;
                            if (isDoubleSided) GL.Disable(EnableCap.CullFace);

                            GL.BindVertexArray(vao);
                            GL.DrawElements(PrimitiveType.Triangles, idxCount, DrawElementsType.UnsignedInt, 0);

                            if (isDoubleSided) GL.Enable(EnableCap.CullFace);
                        }

                        // Terrain
                        if (e.HasComponent<Engine.Components.Terrain>())
                        {
                            var terrain = e.GetComponent<Engine.Components.Terrain>();
                            if (terrain != null)
                            {
                                e.GetModelAndNormalMatrix(out var model, out _);
                                int curProg2 = GL.GetInteger(GetPName.CurrentProgram);
                                int locModel2 = GL.GetUniformLocation(curProg2, "u_Model");
                                if (locModel2 >= 0) GL.UniformMatrix4(locModel2, false, ref model);
                                terrain.Render(new System.Numerics.Vector3(CameraPosition().X, CameraPosition().Y, CameraPosition().Z));
                            }
                        }
                    }
                    GL.UseProgram(0);

                    // Restore polygon offset state
                    GL.Disable(EnableCap.PolygonOffsetFill);
                }
            }

            // CRITICAL: Restore OpenGL state after shadow pass
            // The shadow pass changes viewport and framebuffer, which MUST be restored
            // even when debug mode is disabled, otherwise the main render will be corrupted (use MSAA FBO if active)
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, GetTargetFBO());
            GL.Viewport(0, 0, _w, _h);

            // ALWAYS do readback to flush framebuffer (some drivers need this)
            // but only log when debug is enabled
            try
            {
                if (_shadowDebugColorTex != 0)
                {
                    // Read a small 4x4 block from the center of the color attachment
                    int sampleW = 4, sampleH = 4;
                    int cx = shadowMapSize / 2, cy = shadowMapSize / 2;
                    int rx = Math.Max(0, cx - sampleW / 2);
                    int ry = Math.Max(0, cy - sampleH / 2);
                    byte[] pixels = new byte[sampleW * sampleH * 4];
                    // Ensure we read from color attachment 0
                    GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
                    GL.ReadPixels(rx, ry, sampleW, sampleH, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
                    
                    // Only log when debug is enabled
                    if (Editor.State.EditorSettings.ShadowsSettings.DebugShowShadowMap)
                    {
                        long sum = 0; int nonzero = 0;
                        for (int i = 0; i < pixels.Length; i++) { sum += pixels[i]; if (pixels[i] != 0) nonzero++; }
                        try { LogManager.LogVerbose($"Shadows: DebugColorRead: center({cx},{cy}) {sampleW}x{sampleH} sum={sum} nonzero={nonzero} texId={_shadowDebugColorTex}", "ViewportRenderer"); } catch { }
                    }
                }
            }
            catch (Exception ex) { try { LogManager.LogWarning("Shadows: DebugColorRead failed: " + ex.Message, "ViewportRenderer"); } catch { } }

                // Force-blit debug color tile into the renderer color target so ImGui displays it
                try
                {
                    if (Editor.State.EditorSettings.ShadowsSettings.DebugShowShadowMap && _shadowDebugColorTex != 0)
                    {
                        int targetFbo = (_postTexHealthy && _postFbo != 0) ? _postFbo : _fbo;
                        // Compute overlay rectangle in target (match DrawShadowAtlasOverlay)
                        int overlayW = Math.Max(32, _w / 4);
                        int overlayH = Math.Max(32, _h / 4);
                        int ox = _w - overlayW - 8; // margin
                        int oy = _h - overlayH - 8;

                        // Bind read framebuffer (shadow FBO) and draw framebuffer (target)
                        GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _shadowFbo);
                        GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
                        GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, targetFbo);
                        // Ensure draw buffer targets color attachment 0
                        try { var bufs2 = new DrawBuffersEnum[] { DrawBuffersEnum.ColorAttachment0 }; GL.DrawBuffers(bufs2.Length, bufs2); } catch { }

                        // Perform blit from full shadow map into overlay region of target
                        GL.BlitFramebuffer(0, 0, _shadowManager.ShadowMapSize, _shadowManager.ShadowMapSize,
                                           ox, oy, ox + overlayW, oy + overlayH,
                                           ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);

                        // Restore default framebuffer bindings
                        GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0);
                        GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
                        try { LogManager.LogVerbose($"Shadows: Blitted debug color tex into targetFbo={targetFbo} rect=({ox},{oy}) {overlayW}x{overlayH}", "ViewportRenderer"); } catch { }
                    }
                }
                catch (Exception ex) { try { LogManager.LogWarning("Shadows: Debug blit failed: " + ex.Message, "ViewportRenderer"); } catch { } }

            // Render UI on top of the scene (mirror GameRenderer)
            try
            {
                if (_inputModule != null) _inputModule.Update();
                if (_uiRenderer != null)
                {
                    // Update demo canvas size if any canvas was created similarly to GameRenderer
                    // We don't force a demo canvas here; canvases created via components will be registered with EventSystem
                    _uiRenderer.RenderAllCanvases();
                }
            }
            catch (Exception ex)
            {
                LogManager.LogWarning($"UI render error: {ex.Message}", "ViewportRenderer");
            }

            // Unbind
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.UseProgram(0);

            return true;
            */ // END OLD CODE
        }

        private void UpdateCameraMatrices()
        {
            // Ne mettre à jour que si on n'utilise pas des matrices personnalisées
            if (!_useCustomMatrices)
            {
                float aspect = _w / Math.Max(1.0f, (float)_h);
                _projGL = CreateProjectionMatrix(aspect);
                var camPos = CameraPosition();
                var viewLH = LookAtLH(camPos, _target, Vector3.UnitY);
                _viewGL = viewLH * ZFlip;
            }
            
            var mvp = _viewGL * _projGL;
            _frustum.ExtractFromMatrix(mvp);
        }
        
        /* private void PerformFrustumCulling()
        {
            _visibleEntities.Clear();
            
            foreach (var entity in _scene.Entities)
            {
                if (!_entityCache.TryGetValue(entity.Id, out var cached))
                {
                    cached = CreateCachedData(entity);
                    _entityCache[entity.Id] = cached;
                }
                
                // Update bounds
                entity.GetWorldTRS(out var pos, out _, out var scale);
                cached.Bounds.Center = pos;
                cached.Bounds.Radius = CalculateEntityBounds(entity, scale);
                
                // Frustum test
                cached.IsVisible = _frustum.IsVisible(cached.Bounds);
                
                if (cached.IsVisible)
                {
                    _visibleEntities.Add(entity);
                }
                
                _entityCache[entity.Id] = cached;
            }
        } */
        
        private float CalculateEntityBounds(Entity entity, Vector3 scale)
        {
            // Unit cube diagonal * max scale component
            float maxScale = MathF.Max(MathF.Max(MathF.Abs(scale.X), MathF.Abs(scale.Y)), MathF.Abs(scale.Z));
            return 0.866f * maxScale; // sqrt(3)/2
        }
        
        /* private void RebuildRenderBatches()
        {
            // Clear existing batches
            foreach (var batch in _batches)
            {
                batch.Transforms?.Clear();
                batch.EntityIds?.Clear();
            }
            _batches.Clear();
            
            // Group visible entities by material
            var materialGroups = new Dictionary<Guid, List<Entity>>();
            
            foreach (var entity in _visibleEntities)
            {
                // Check if entity has a MeshRenderer component
                var meshRenderer = entity.GetComponent<Engine.Components.MeshRendererComponent>();
                if (meshRenderer == null || !meshRenderer.HasMeshToRender())
                    continue;

                var matGuid = meshRenderer.GetMaterialGuid();
                if (matGuid == Guid.Empty)
                    matGuid = Engine.Assets.AssetDatabase.EnsureDefaultWhiteMaterial();
                    
                if (!materialGroups.TryGetValue(matGuid, out var group))
                {
                    group = new List<Entity>();
                    materialGroups[matGuid] = group;
                }
                group.Add(entity);
            }
            
            // Create batches
                    batch.Transforms.Add(entity.WorldMatrix);
                    Guid lastGuid = Guid.Empty;
                    foreach (var item in items)
                    {
                        if (item.MaterialGuid != lastGuid)
                        {
                            // Debug: log material being bound and resolved albedo path
                            try
                            {
                                if (Engine.Assets.AssetDatabase.TryGet(item.MaterialGuid, out var rec))
                                {
                    batch.EntityIds.Add(entity.Id);
                }
                
                _batches.Add(batch);
            }
        } */
        
        private void UpdateGlobalUniforms()
        {
            _globalUniforms.ViewMatrix = _viewGL;
            _globalUniforms.ProjectionMatrix = _projGL;
            _globalUniforms.ViewProjectionMatrix = _viewGL * _projGL;
            _globalUniforms.CameraPosition = CameraPosition();
            
            // Get lighting from scene
            var lighting = Engine.Scene.Lighting.Build(_scene);

            // Set lighting state for skybox renderer
            Engine.Rendering.SkyboxRenderer.CurrentLightingState = lighting;

            // Directional Light
            if (lighting.HasDirectional)
            {
                _globalUniforms.DirLightDirection = lighting.DirDirection;
                // Send direction TO the light (vector from surface to light) to the shader.
                // The lighting.DirDirection is the light entity forward; negate it so the GPU
                // receives a consistent "towards-light" direction.
                //var toLight = -lighting.DirDirection;
                //if (toLight.LengthSquared > 0f) toLight = toLight.Normalized();
                //_globalUniforms.DirLightDirection = toLight;
                _globalUniforms.DirLightColor = lighting.DirColor;
                _globalUniforms.DirLightIntensity = lighting.DirIntensity;
            }
            else
            {
                _globalUniforms.DirLightIntensity = 0.0f;
            }

            // Ambient / skybox
            _globalUniforms.AmbientColor = lighting.AmbientColor;
            _globalUniforms.AmbientIntensity = lighting.AmbientIntensity;
            _globalUniforms.SkyboxTint = lighting.SkyboxTint;
            _globalUniforms.SkyboxExposure = lighting.SkyboxExposure;

            // Fog
            _globalUniforms.FogEnabled = lighting.FogEnabled ? 1 : 0;
            _globalUniforms.FogColor = lighting.FogColor;
            _globalUniforms.FogStart = lighting.FogStart;
            _globalUniforms.FogEnd = lighting.FogEnd;
            
            // Debug logging for UBO values (also persist to astrild_debug.log)
            try
            {
                // UBO lighting logged only on startup
            }
            catch { }

            // Point Lights (max 4) - fill shader slots
            _globalUniforms.PointLightCount = Math.Min(lighting.Points.Count, 4);
            for (int i = 0; i < 4; i++)
            {
                if (i < lighting.Points.Count)
                {
                    var point = lighting.Points[i];
                    switch (i)
                    {
                        case 0:
                            _globalUniforms.PointLightPos0 = new Vector4(point.pos, point.range);
                            _globalUniforms.PointLightColor0 = new Vector4(point.color, point.intensity);
                            break;
                        case 1:
                            _globalUniforms.PointLightPos1 = new Vector4(point.pos, point.range);
                            _globalUniforms.PointLightColor1 = new Vector4(point.color, point.intensity);
                            break;
                        case 2:
                            _globalUniforms.PointLightPos2 = new Vector4(point.pos, point.range);
                            _globalUniforms.PointLightColor2 = new Vector4(point.color, point.intensity);
                            break;
                        case 3:
                            _globalUniforms.PointLightPos3 = new Vector4(point.pos, point.range);
                            _globalUniforms.PointLightColor3 = new Vector4(point.color, point.intensity);
                            break;
                    }
                }
                else
                {
                    // Clear unused slots
                    switch (i)
                    {
                        case 0:
                            _globalUniforms.PointLightPos0 = Vector4.Zero;
                            _globalUniforms.PointLightColor0 = Vector4.Zero;
                            break;
                        case 1:
                            _globalUniforms.PointLightPos1 = Vector4.Zero;
                            _globalUniforms.PointLightColor1 = Vector4.Zero;
                            break;
                        case 2:
                            _globalUniforms.PointLightPos2 = Vector4.Zero;
                            _globalUniforms.PointLightColor2 = Vector4.Zero;
                            break;
                        case 3:
                            _globalUniforms.PointLightPos3 = Vector4.Zero;
                            _globalUniforms.PointLightColor3 = Vector4.Zero;
                            break;
                    }
                }
            }

            // Spot Lights (max 2)
            _globalUniforms.SpotLightCount = Math.Min(lighting.Spots.Count, 2);
            for (int i = 0; i < 2; i++)
            {
                if (i < lighting.Spots.Count)
                {
                    var spot = lighting.Spots[i];
                    switch (i)
                    {
                        case 0:
                            _globalUniforms.SpotLightPos0 = new Vector4(spot.pos, spot.range);
                            _globalUniforms.SpotLightDir0 = new Vector4(spot.dir, 0.0f);
                            _globalUniforms.SpotLightColor0 = new Vector4(spot.color, spot.intensity);
                            _globalUniforms.SpotLightAngle0 = spot.angle;
                            _globalUniforms.SpotLightInnerAngle0 = spot.innerAngle;
                            break;
                        case 1:
                            _globalUniforms.SpotLightPos1 = new Vector4(spot.pos, spot.range);
                            _globalUniforms.SpotLightDir1 = new Vector4(spot.dir, 0.0f);
                            _globalUniforms.SpotLightColor1 = new Vector4(spot.color, spot.intensity);
                            _globalUniforms.SpotLightAngle1 = spot.angle;
                            _globalUniforms.SpotLightInnerAngle1 = spot.innerAngle;
                            break;
                    }
                }
                else
                {
                    // Clear unused slots
                    switch (i)
                    {
                        case 0:
                            _globalUniforms.SpotLightPos0 = Vector4.Zero;
                            _globalUniforms.SpotLightDir0 = Vector4.Zero;
                            _globalUniforms.SpotLightColor0 = Vector4.Zero;
                            _globalUniforms.SpotLightAngle0 = 0.0f;
                            _globalUniforms.SpotLightInnerAngle0 = 0.0f;
                            break;
                        case 1:
                            _globalUniforms.SpotLightPos1 = Vector4.Zero;
                            _globalUniforms.SpotLightDir1 = Vector4.Zero;
                            _globalUniforms.SpotLightColor1 = Vector4.Zero;
                            _globalUniforms.SpotLightAngle1 = 0.0f;
                            _globalUniforms.SpotLightInnerAngle1 = 0.0f;
                            break;
                    }
                }
            }
            
            // ✅ Rebind notre UBO sur le binding 0 AVANT d’écrire/puis dessiner
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, _globalUBO);
            GL.BindBuffer(BufferTarget.UniformBuffer, _globalUBO);
            GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero,
                System.Runtime.InteropServices.Marshal.SizeOf<GlobalUniforms>(),
                ref _globalUniforms);
        }
        
        /* private void RenderBatches()
        {
            GL.UseProgram(_gfxShader);
            GL.BindVertexArray(_cubeVao);
            
            foreach (var batch in _batches)
            {
                if (batch.Transforms.Count == 0) continue;
                
                // Bind material
                BindMaterial(batch.MaterialGuid);
                
                // Render instances
                for (int i = 0; i < batch.Transforms.Count; i++)
                {
                    var mvp = batch.Transforms[i] * _viewGL * _projGL;
                    GL.UniformMatrix4(_locMvp, false, ref mvp);
                    GL.Uniform1(_locId, (int)batch.EntityIds[i]);
                    
                    GL.DrawElements(PrimitiveType.Triangles, _cubeIdx.Length, 
                                  DrawElementsType.UnsignedInt, 0);
                }
            }
        } */

        private void BindMaterial(Guid materialGuid)
        {
            try
            {
                var mat = Engine.Assets.AssetDatabase.LoadMaterial(materialGuid);
                if (mat?.AlbedoColor != null && mat.AlbedoColor.Length >= 4)
                {
                    GL.Uniform4(_locAlbColor, mat.AlbedoColor[0], mat.AlbedoColor[1], 
                              mat.AlbedoColor[2], mat.AlbedoColor[3]);
                }
                
                if (mat?.AlbedoTexture.HasValue == true && mat.AlbedoTexture.Value != Guid.Empty)
                {
                    int texHandle = TextureCache.GetOrLoad(mat.AlbedoTexture.Value, 
                        guid => Engine.Assets.AssetDatabase.TryGet(guid, out var rec) ? rec.Path : null);
                    
                    GL.ActiveTexture(TextureUnit.Texture0);
                    GL.BindTexture(TextureTarget.Texture2D, texHandle);
                    GL.Uniform1(_locUseTex, 1);
                }
                else
                {
                    GL.Uniform1(_locUseTex, 0);
                }
            }
            catch
            {
                // Fallback to default material
                GL.Uniform4(_locAlbColor, 1f, 1f, 1f, 1f);
                GL.Uniform1(_locUseTex, 0);
            }
        }

        private List<RenderItem> BuildRenderList()
        {
            // Simplified: no chunk uploading needed for single-mesh terrain

            var items = new List<RenderItem>(_scene.Entities.Count);
            var entitiesSpan = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_scene.Entities);

            for (int i = 0; i < entitiesSpan.Length; i++)
            {
                var entity = entitiesSpan[i];

                // Skip terrain entities - they are rendered separately in RenderTerrain()
                if (entity.HasComponent<Engine.Components.Terrain>()) continue;

                // Skip water entities - they are rendered separately in RenderWater()
                if (entity.HasComponent<Engine.Components.WaterComponent>()) continue;

                // Frustum culling - Quick Win #1
                entity.GetWorldTRS(out var worldPos, out var worldRot, out var worldScale);

                // Calculate bounding sphere radius based on mesh type and scale
                float boundsRadius = CalculateEntityBoundsRadius(entity, worldScale);
                var boundingSphere = new BoundingSphere
                {
                    Center = worldPos,
                    Radius = boundsRadius
                };

                // Skip entities outside frustum
                if (!_frustum.IsVisible(boundingSphere))
                    continue;

                // Standard mesh renderers
                if (entity.HasComponent<MeshRendererComponent>())
                {
                    var meshRenderer = entity.GetComponent<MeshRendererComponent>();
                    if (meshRenderer?.MaterialGuid == null || meshRenderer.MaterialGuid == Guid.Empty) continue;

                    entity.GetModelAndNormalMatrix(out var model, out var normalMat3);

                    // Quick Win #2: Use material cache to avoid loading from disk every frame
                    var materialGuid = meshRenderer.MaterialGuid.Value;
                    if (!_materialCache.TryGetValue(materialGuid, out var materialRuntime))
                    {
                        // Material not in cache, load it and cache it
                        try
                        {
                            var asset = Engine.Assets.AssetDatabase.LoadMaterial(materialGuid);
                            Func<Guid, string?> resolver = guid => Engine.Assets.AssetDatabase.TryGet(guid, out var rec) ? rec.Path : null;
                            materialRuntime = Engine.Rendering.MaterialRuntime.FromAsset(asset, resolver);
                            _materialCache[materialGuid] = materialRuntime; // Cache it!
                        }
                        catch
                        {
                            materialRuntime = new Engine.Rendering.MaterialRuntime { AlbedoTex = TextureCache.White1x1 };
                            _materialCache[materialGuid] = materialRuntime; // Cache even fallback materials
                        }
                    }

                    int vao, ebo, idxCount;

                    // Check if using custom mesh first
                    if (meshRenderer.IsUsingCustomMesh())
                    {
                        var customMesh = LoadCustomMesh(meshRenderer.CustomMeshGuid!.Value, meshRenderer.SubmeshIndex);
                        if (customMesh.HasValue)
                        {
                            vao = customMesh.Value.VAO;
                            ebo = customMesh.Value.EBO;
                            idxCount = customMesh.Value.IndexCount;
                        }
                        else
                        {
                            // Fallback to cube if custom mesh fails to load
                            vao = _cubeVao;
                            ebo = _cubeEbo;
                            idxCount = _cubeIdx.Length;
                        }
                    }
                    else
                    {
                        // Use primitive mesh
                        vao = _cubeVao;
                        ebo = _cubeEbo;
                        idxCount = _cubeIdx.Length;

                        switch (meshRenderer.Mesh)
                        {
                            case MeshKind.Cube:
                                vao = _cubeVao; ebo = _cubeEbo; idxCount = _cubeIdx.Length; break;
                            case MeshKind.Plane:
                                vao = _planeVao; ebo = _planeEbo; idxCount = _planeIndexCount; break;
                            case MeshKind.Quad:
                                vao = _quadVao; ebo = _quadEbo; idxCount = _quadIndexCount; break;
                            case MeshKind.Sphere:
                                vao = _sphereVao; ebo = _sphereEbo; idxCount = _sphereIndexCount; break;
                            case MeshKind.Capsule:
                                vao = _capsuleVao; ebo = _capsuleEbo; idxCount = _capsuleIndexCount; break;
                        }
                    }

                    items.Add(new RenderItem
                    {
                        Vao = vao,
                        Ebo = ebo,
                        IndexCount = idxCount,
                        Model = model,
                        NormalMat3 = normalMat3,
                        MaterialGuid = meshRenderer.MaterialGuid.Value,
                        MeshType = meshRenderer.Mesh,
                        MaterialRuntime = materialRuntime,
                        ObjectId = entity.Id
                    });

                    continue;
                }

                // Skip terrain entities - handled separately in RenderTerrain()
                // Removed old chunk-based terrain system
            }

            return items;
        }

        /// <summary>
        /// Render terrain using PBR shader (fallback to ensure visibility).
        /// </summary>
        private void RenderTerrain()
        {
            if (_scene?.Entities == null) return;

            // Initialize terrain renderer if needed
            if (_terrainRenderer == null)
            {
                try
                {
                    _terrainRenderer = new Engine.Rendering.Terrain.TerrainRenderer();
                }
                catch (Exception ex)
                {
                    LogManager.LogWarning($"Failed to create TerrainRenderer: {ex.Message}", "ViewportRenderer");
                    return;
                }
            }

            // Find terrain entities and render them using the dedicated TerrainRenderer
            int terrainCount = 0;
            foreach (var entity in _scene.Entities)
            {
                if (entity.HasComponent<Engine.Components.Terrain>())
                {
                    terrainCount++;
                    var terrain = entity.GetComponent<Engine.Components.Terrain>();
                    if (terrain != null)
                    {
                        // Draw a visual reference cube at the terrain position (debug only)
                        // Removed to reduce console spam

                        try
                        {
                            // Get the terrain entity's transform matrix
                            entity.GetModelAndNormalMatrix(out var terrainModel, out var terrainNormalMat);

                            // Use the dedicated TerrainRenderer with tessellation support
                            var viewPos = CameraPosition();

                            // Get directional light from global uniforms
                            var lightDir = new OpenTK.Mathematics.Vector3(
                                _globalUniforms.DirLightDirection.X,
                                _globalUniforms.DirLightDirection.Y,
                                _globalUniforms.DirLightDirection.Z);
                            if (lightDir.LengthSquared > 0f) lightDir.Normalize();

                            var lightColor = new OpenTK.Mathematics.Vector3(
                                _globalUniforms.DirLightColor.X,
                                _globalUniforms.DirLightColor.Y,
                                _globalUniforms.DirLightColor.Z);

                            // Get shadow settings
                            var shadowSettings = Editor.State.EditorSettings.ShadowsSettings;
                            bool shadowsEnabled = false;
                            int shadowTexture = 0;
                            OpenTK.Mathematics.Matrix4 shadowMatrix = OpenTK.Mathematics.Matrix4.Identity;
                            float shadowBias = 0.1f;
                            float shadowMapSize = 1024f;
                            float shadowStrength = shadowSettings.ShadowStrength;

                            if (_shadowManager != null)
                            {
                                if (shadowSettings.Enabled)
                                {
                                    shadowsEnabled = true;
                                    shadowTexture = _shadowManager.ShadowTexture;
                                    shadowMatrix = _shadowManager.LightSpaceMatrix;
                                    shadowBias = shadowSettings.ShadowBias;
                                    shadowMapSize = (float)_shadowManager.ShadowMapSize;

                                    // Console.WriteLine($"[ViewportRenderer] Terrain shadows: enabled={shadowsEnabled}, texture={shadowTexture}, bias={shadowBias}");
                                }
                            }

                            // SSAO is now handled as a post-effect, not in material shaders
                            bool ssaoEnabled = false;
                            int ssaoTexture = 0;
                            float ssaoStrength = 1.0f;
                            var screenSize = new OpenTK.Mathematics.Vector2(_w, _h);

                            // IMPORTANT: Bind the Global UBO before rendering terrain
                            // The terrain shader expects the Global UBO to be bound at binding point 0
                            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, _globalUBO);

                            // Render terrain with all features
                            _terrainRenderer.RenderTerrain(
                                terrain,
                                _viewGL,
                                _projGL,
                                viewPos,
                                lightDir,
                                lightColor,
                                ssaoEnabled,
                                ssaoTexture,
                                ssaoStrength,
                                screenSize,
                                shadowsEnabled,
                                shadowTexture,
                                shadowMatrix,
                                shadowBias,
                                shadowMapSize,
                                shadowStrength,  // Pass shadow strength
                                terrainModel,  // Pass the terrain's actual transform matrix
                                shadowSettings.ShadowBias,  // Use new bias
                                shadowSettings.ShadowDistance,  // Use shadow distance
                                0,  // globalUBO (not used)
                                entity.Id  // Pass entity ID for selection outline
                            );
                        }
                        catch (Exception ex)
                        {
                            LogManager.LogWarning($"Terrain rendering error: {ex.Message}", "ViewportRenderer");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Render water components using the Water shader with tessellation.
        /// </summary>
        private void RenderWater()
        {
            if (_scene?.Entities == null) return;

            // Find water entities and render them
            foreach (var entity in _scene.Entities)
            {
                if (entity.HasComponent<Engine.Components.WaterComponent>())
                {
                    var waterComponent = entity.GetComponent<Engine.Components.WaterComponent>();
                    if (waterComponent != null && waterComponent.WaterMaterialGuid.HasValue)
                    {
                        try
                        {
                            // Get the water entity's transform matrix
                            entity.GetModelAndNormalMatrix(out var waterModel, out var waterNormalMat);

                            // PERFORMANCE: Use material cache instead of reloading every frame!
                            var materialGuid = waterComponent.WaterMaterialGuid.Value;
                            Engine.Rendering.MaterialRuntime? waterMaterialRuntime = null;

                            if (!_materialCache.TryGetValue(materialGuid, out waterMaterialRuntime))
                            {
                                // Load water material (only once, then cached)
                                var waterMaterial = Engine.Assets.AssetDatabase.LoadMaterial(materialGuid);
                                if (waterMaterial == null) continue;

                                // Create MaterialRuntime from asset
                                Func<Guid, string?> waterResolver = guid => Engine.Assets.AssetDatabase.TryGet(guid, out var r) ? r.Path : null;
                                waterMaterialRuntime = Engine.Rendering.MaterialRuntime.FromAsset(waterMaterial, waterResolver);
                                _materialCache[materialGuid] = waterMaterialRuntime; // Cache it!
                            }

                            if (waterMaterialRuntime == null) continue;

                            // Get the Water shader
                            Engine.Rendering.ShaderProgram? waterShader = null;
                            if (!string.IsNullOrEmpty(waterMaterialRuntime.ShaderName))
                            {
                                waterShader = Engine.Rendering.ShaderLibrary.GetShaderByName(waterMaterialRuntime.ShaderName);
                            }

                            if (waterShader == null)
                            {
                                waterShader = Engine.Rendering.ShaderLibrary.GetShaderByName("Water");
                            }

                            if (waterShader == null)
                            {
                                LogManager.LogWarning("Failed to load Water shader", "ViewportRenderer");
                                continue;
                            }

                            waterShader.Use();

                            // Set matrices
                            waterShader.SetMat4("u_Model", waterModel);
                            waterShader.SetMat4("u_View", _viewGL);
                            waterShader.SetMat4("u_Projection", _projGL);
                            waterShader.SetMat3("u_NormalMat", new OpenTK.Mathematics.Matrix3(waterModel));

                            // Set view position
                            var viewPos = CameraPosition();
                            waterShader.SetVec3("u_ViewPos", viewPos);
                            waterShader.SetVec3("uCameraPos", viewPos);

                            // Set light direction and color
                            var lightDir = new OpenTK.Mathematics.Vector3(
                                _globalUniforms.DirLightDirection.X,
                                _globalUniforms.DirLightDirection.Y,
                                _globalUniforms.DirLightDirection.Z);
                            if (lightDir.LengthSquared > 0f) lightDir.Normalize();

                            var lightColor = new OpenTK.Mathematics.Vector3(
                                _globalUniforms.DirLightColor.X,
                                _globalUniforms.DirLightColor.Y,
                                _globalUniforms.DirLightColor.Z);

                            waterShader.SetVec3("u_LightDir", lightDir);
                            waterShader.SetVec3("u_LightColor", lightColor);

                            // Material is already loaded with all textures from cache
                            // Just assign the dynamic reflection texture
                            if (waterMaterialRuntime.EnableReflection && _reflectionTex != 0)
                            {
                                waterMaterialRuntime.ReflectionTexture = _reflectionTex;
                            }

                            // Bind material to shader WITH time for animation
                            // MaterialRuntime.Bind() will pass all water uniforms including u_Time
                            float time = (float)_timeStopwatch.Elapsed.TotalSeconds;
                            waterMaterialRuntime.Bind(waterShader, time);

                            // SSAO is now handled as a post-effect, not in water shader
                            waterShader.SetInt("u_SSAOEnabled", 0);
                            waterShader.SetFloat("u_SSAOStrength", 0.0f);
                            waterShader.SetVec2("u_ScreenSize", new OpenTK.Mathematics.Vector2(_w, _h));
                            GL.ActiveTexture(TextureUnit.Texture3);
                            GL.BindTexture(TextureTarget.Texture2D, 0);
                            waterShader.SetInt("u_SSAOTexture", 3);

                            // Set shadows
                            bool shadowsEnabled = false;
                            int shadowTexture = 0;
                            OpenTK.Mathematics.Matrix4 shadowMatrix = OpenTK.Mathematics.Matrix4.Identity;
                            float shadowBias = 0.005f;
                            float shadowMapSize = 1024f;

                            if (_shadowManager != null)
                            {
                                var shadowSettings = Editor.State.EditorSettings.ShadowsSettings;
                                if (shadowSettings.Enabled)
                                {
                                    shadowsEnabled = true;
                                    shadowTexture = _shadowManager.ShadowTexture;
                                    shadowMatrix = _shadowManager.LightSpaceMatrix;
                                    shadowBias = shadowSettings.ShadowBias;
                                    shadowMapSize = (float)_shadowManager.ShadowMapSize;
                                }
                            }

                            // Set shadow uniforms (CSM-aware)
                            SetShadowUniforms(waterShader, shadowsEnabled && shadowTexture > 0);

                            // Bind Global UBO
                            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, _globalUBO);

                            // Render the water mesh
                            waterComponent.Render();
                        }
                        catch (Exception ex)
                        {
                            LogManager.LogWarning($"Water rendering error: {ex.Message}", "ViewportRenderer");
                        }
                    }
                }
            }
        }

        private void DrawForwardOpaque()
        {

            if (_pbrShader == null)
            {
                if (_gfxShader == 0)
                    InitResources();

                GL.UseProgram(_gfxShader);
                GL.Uniform1(_locUseTex, 0);

                var entitiesSpan = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_scene.Entities);
                for (int i = 0; i < entitiesSpan.Length; i++)
                {
                    var e = entitiesSpan[i];
                    var meshRenderer = e.GetComponent<Engine.Components.MeshRendererComponent>();
                    if (meshRenderer == null || !meshRenderer.HasMeshToRender())
                        continue;

                    var model = e.WorldMatrix;
                    var mvp = model * _viewGL * _projGL;

                    GL.UniformMatrix4(_locMvp, false, ref mvp);
                    GL.Uniform1(_locId, e.Id);
                    GL.Uniform4(_locAlbColor, 1f, 1f, 1f, 1f);
                    GL.Uniform1(_locUseTex, 0);

                    // Check if using custom mesh first
                    if (meshRenderer.IsUsingCustomMesh())
                    {
                        var customMesh = LoadCustomMesh(meshRenderer.CustomMeshGuid!.Value, meshRenderer.SubmeshIndex);
                        if (customMesh.HasValue)
                        {
                            GL.BindVertexArray(customMesh.Value.VAO);
                            GL.DrawElements(PrimitiveType.Triangles, customMesh.Value.IndexCount, DrawElementsType.UnsignedInt, 0);
                        }
                        else
                        {
                            // Fallback to cube if custom mesh fails to load
                            GL.BindVertexArray(_legacyCubeVao);
                            GL.DrawElements(PrimitiveType.Triangles, _cubeIdx.Length, DrawElementsType.UnsignedInt, 0);
                        }
                    }
                    else
                    {
                        // Use primitive mesh
                        // Disable culling for double-sided meshes (plane only - quad is single-sided like Unity)
                        bool isDoubleSided = meshRenderer.Mesh == MeshKind.Plane;
                        if (isDoubleSided) GL.Disable(EnableCap.CullFace);

                        switch (meshRenderer.Mesh)
                        {
                            case MeshKind.Cube:
                                GL.BindVertexArray(_legacyCubeVao);
                                GL.DrawElements(PrimitiveType.Triangles, _cubeIdx.Length, DrawElementsType.UnsignedInt, 0);
                                break;
                            case MeshKind.Plane:
                                GL.BindVertexArray(_legacyPlaneVao);
                                GL.DrawElements(PrimitiveType.Triangles, _planeIndexCount, DrawElementsType.UnsignedInt, 0);
                                break;
                            case MeshKind.Quad:
                                GL.BindVertexArray(_legacyQuadVao);
                                GL.DrawElements(PrimitiveType.Triangles, _quadIndexCount, DrawElementsType.UnsignedInt, 0);
                                break;
                            case MeshKind.Sphere:
                                GL.BindVertexArray(_legacySphereVao);
                                GL.DrawElements(PrimitiveType.Triangles, _sphereIndexCount, DrawElementsType.UnsignedInt, 0);
                                break;
                            case MeshKind.Capsule:
                                GL.BindVertexArray(_legacyCapsuleVao);
                                GL.DrawElements(PrimitiveType.Triangles, _capsuleIndexCount, DrawElementsType.UnsignedInt, 0);
                                break;
                            default:
                                GL.BindVertexArray(_legacyCubeVao);
                                GL.DrawElements(PrimitiveType.Triangles, _cubeIdx.Length, DrawElementsType.UnsignedInt, 0);
                                break;
                        }

                        if (isDoubleSided) GL.Enable(EnableCap.CullFace);
                    }
                }
                // Additionally: if PBR shader is unavailable, draw the modern render items (water, models using PBR materials)
                // using the simple fallback shader so previews (like water) are still visible.
                try
                {
                    var fbItems = BuildRenderList();

                    // OPAQUE items first
                    foreach (var item in fbItems)
                    {
                        bool isTransparent = item.MaterialRuntime != null && item.MaterialRuntime.TransparencyMode != 0;
                        if (isTransparent) continue;

                        // Bind albedo texture to unit 0
                        GL.ActiveTexture(TextureUnit.Texture0);
                        int albedo = item.MaterialRuntime?.AlbedoTex ?? Engine.Rendering.TextureCache.White1x1;
                        GL.BindTexture(TextureTarget.Texture2D, albedo);
                        GL.Uniform1(_locAlbTex, 0);

                        var mvp = item.Model * _viewGL * _projGL;
                        GL.UniformMatrix4(_locMvp, false, ref mvp);
                        GL.Uniform1(_locId, (int)item.ObjectId);
                        var col = item.MaterialRuntime?.AlbedoColor ?? new float[] { 1, 1, 1, 1 };
                        GL.Uniform4(_locAlbColor, col[0], col[1], col[2], col[3]);
                        GL.Uniform1(_locUseTex, albedo != Engine.Rendering.TextureCache.White1x1 ? 1 : 0);

                        // Disable culling for plane (double-sided) - Quad is single-sided like Unity
                        bool isDoubleSided = item.MeshType == MeshKind.Plane;
                        if (isDoubleSided)
                        {
                            GL.Disable(EnableCap.CullFace);
                            LogManager.LogInfo($"PLANE RENDERING: Culling DISABLED (opaque pass)", "ViewportRenderer");
                        }

                        GL.BindVertexArray(item.Vao);
                        GL.BindBuffer(BufferTarget.ElementArrayBuffer, item.Ebo);
                            RecordDraw(PrimitiveType.Triangles, item.IndexCount);
                            GL.DrawElements(PrimitiveType.Triangles, item.IndexCount, DrawElementsType.UnsignedInt, 0);

                        if (isDoubleSided) GL.Enable(EnableCap.CullFace);
                    }

                    // TRANSPARENT items (sorted back-to-front)
                    GL.Enable(EnableCap.Blend);
                    GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                    GL.DepthMask(false);

                    _fbTransparentItems.Clear();
                    foreach (var it in fbItems) if (it.MaterialRuntime != null && it.MaterialRuntime.TransparencyMode != 0) _fbTransparentItems.Add(it);
                    var fbCamPosVec = new OpenTK.Mathematics.Vector3(CameraPosition());
                    _fbTransparentItems.Sort((a, b) =>
                    {
                        // Avoid allocating Vector3 per element during sort by computing squared distance directly
                        float camX = fbCamPosVec.X, camY = fbCamPosVec.Y, camZ = fbCamPosVec.Z;
                        float ax = a.Model.M41 - camX, ay = a.Model.M42 - camY, az = a.Model.M43 - camZ;
                        float bx = b.Model.M41 - camX, by = b.Model.M42 - camY, bz = b.Model.M43 - camZ;
                        float da = ax * ax + ay * ay + az * az;
                        float db = bx * bx + by * by + bz * bz;
                        return db.CompareTo(da);
                    });

                    int lastVaoFb = -1;
                    int lastEboFb = -1;
                    foreach (var item in _fbTransparentItems)
                    {
                        GL.ActiveTexture(TextureUnit.Texture0);
                        int albedo = item.MaterialRuntime?.AlbedoTex ?? Engine.Rendering.TextureCache.White1x1;
                        GL.BindTexture(TextureTarget.Texture2D, albedo);
                        GL.Uniform1(_locAlbTex, 0);

                        var mvp = item.Model * _viewGL * _projGL;
                        GL.UniformMatrix4(_locMvp, false, ref mvp);
                        GL.Uniform1(_locId, (int)item.ObjectId);
                        var col = item.MaterialRuntime?.AlbedoColor ?? new float[] { 1, 1, 1, 1 };
                        GL.Uniform4(_locAlbColor, col[0], col[1], col[2], col[3]);
                        GL.Uniform1(_locUseTex, albedo != Engine.Rendering.TextureCache.White1x1 ? 1 : 0);

                        // Disable culling for double-sided meshes (plane only - quad is single-sided like Unity)
                        bool isDoubleSided = item.MeshType == MeshKind.Plane;
                        if (isDoubleSided)
                        {
                            GL.Disable(EnableCap.CullFace);
                            LogManager.LogInfo($"PLANE RENDERING: Culling DISABLED (transparent pass)", "ViewportRenderer");
                        }

                        if (item.Vao != lastVaoFb)
                        {
                            GL.BindVertexArray(item.Vao);
                            lastVaoFb = item.Vao;
                        }
                        if (item.Ebo != lastEboFb)
                        {
                            GL.BindBuffer(BufferTarget.ElementArrayBuffer, item.Ebo);
                            lastEboFb = item.Ebo;
                        }

                        RecordDraw(PrimitiveType.Triangles, item.IndexCount);
                        GL.DrawElements(PrimitiveType.Triangles, item.IndexCount, DrawElementsType.UnsignedInt, 0);

                        if (isDoubleSided) GL.Enable(EnableCap.CullFace);
                    }

                    GL.DepthMask(true);
                    GL.Disable(EnableCap.Blend);
                }
                catch (Exception)
                {
                }

                return;
            }

            GL.Enable(EnableCap.DepthTest);
            GL.DepthMask(true);
            GL.Disable(EnableCap.Blend);

            // Face culling: CW because Z-flip in view matrix inverts CCW vertices to CW on screen
            // Cube vertices are CCW, but after LookAtLH * ZFlip transformation, they appear CW
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(TriangleFace.Back);
            GL.FrontFace(FrontFaceDirection.Cw);
            
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Less);

            var items = BuildRenderList();
            items.Sort((a, b) => a.MaterialGuid.CompareTo(b.MaterialGuid));

            // NOTE: Terrain rendering moved after SSAO compute so terrain can sample SSAO during forward shading.
            // RenderTerrain();

            var pbr = _pbrShader;
            if (pbr == null) return;
            pbr.Use();

            // Shadow uniforms for objects (CSM-aware)
            var shadowSettings = Editor.State.EditorSettings.ShadowsSettings;
            SetShadowUniforms(pbr, shadowSettings.Enabled);
            pbr.SetInt("u_DebugShowShadows", shadowSettings.DebugShowShadowMap ? 1 : 0);

            // SSAO is now handled as a post-effect, not in PBR shader
            // Set disabled defaults to avoid shader errors
            pbr.SetInt("u_SSAOEnabled", 0);
            pbr.SetFloat("u_SSAOStrength", 0.0f);
            _pbrShader.SetVec2("u_ScreenSize", new Vector2(_w, _h));
            GL.ActiveTexture(TextureUnit.Texture3);
            GL.BindTexture(TextureTarget.Texture2D, Engine.Rendering.TextureCache.White1x1);
            pbr.SetInt("u_SSAOTexture", 3);

            // Two-phase rendering: opaque then transparent
            // Opaque pass will be executed after SSAO compute so objects (including terrain) can sample the SSAO texture.
            Guid lastBound = Guid.Empty;

            // IMPORTANT: Render terrain FIRST to populate depth buffer for SSAO, but WITHOUT sampling SSAO
            // This is just for depth/geometry - the final terrain with SSAO will be rendered after SSAO compute
            // Skip this pre-render for now to avoid double-rendering
            // try
            // {
            //     if (_pbrShader != null)
            //     {
            //         RenderTerrain();
            //     }
            // }
            // catch (Exception ex)
            // {
            //     Console.WriteLine($"[ViewportRenderer] Error rendering terrain: {ex.Message}");
            // }

            // === QUEUE 2500: SHADOW MAP GENERATION (NEW) ===
            // Render shadow pass BEFORE main rendering so shadow map is ready
            RenderShadowPass();

            // Restore main framebuffer and viewport after shadow pass (use MSAA FBO if active)
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, GetTargetFBO());
            GL.Viewport(0, 0, _w, _h);

            // === WATER REFLECTION PASS (DISABLED - TO BE REIMPLEMENTED LATER) ===
            // float waterLevel = 0f;
            // var waterEntity = _scene.Entities.FirstOrDefault(e => e.HasComponent<Engine.Components.WaterComponent>());
            // if (waterEntity != null)
            // {
            //     waterLevel = waterEntity.WorldMatrix.M42;
            //     _reflectionFrameCounter++;
            //     if (_reflectionFrameCounter >= _reflectionUpdateInterval)
            //     {
            //         RenderReflectionPass(waterLevel);
            //         _reflectionFrameCounter = 0;
            //     }
            // }

            // === RENDER TERRAIN (before SSAO so SSAO can process terrain depth) ===
            RenderTerrain();

            // === QUEUE 2600: SSAO POST-PROCESSING ===
            // SSAO is now handled as a post-effect, not during main rendering pipeline

            // === Configure Simple Shadow Uniforms ===
                if (_pbrShader != null && shadowSettings.Enabled && _shadowManager != null)
                {
                    // Bind shadow texture to TextureUnit.Texture5
                    _shadowManager.BindShadowTexture(TextureUnit.Texture5);

                    // Set shadow uniforms for simple system
                    _pbrShader.SetInt("u_ShadowMap", 5);
                    _pbrShader.SetMat4("u_ShadowMatrix", _shadowManager.LightSpaceMatrix);
                    _pbrShader.SetInt("u_UseShadows", 1);
                    _pbrShader.SetFloat("u_ShadowMapSize", (float)_shadowManager.ShadowMapSize);
                    _pbrShader.SetFloat("u_ShadowBias", shadowSettings.ShadowBias);
                    _pbrShader.SetFloat("u_ShadowStrength", shadowSettings.ShadowStrength);
                    _pbrShader.SetFloat("u_ShadowDistance", shadowSettings.ShadowDistance);

                    // Shadow settings applied to shader (debug logging removed to avoid spam)
                }
                else if (_pbrShader != null)
                {
                    // Shadows disabled
                    _pbrShader.SetInt("u_UseShadows", 0);
                }

                // Terrain rendering moved to line ~4412 (before SSAO branches)

                // CRITICAL: Re-activate PBR shader (terrain renderer may have changed it)
                if (_pbrShader != null)
                {
                    _pbrShader.Use();

                    // Re-bind shadow uniforms for subsequent objects (using CSM-aware helper)
                    SetShadowUniforms(_pbrShader, shadowSettings.Enabled);
                    _pbrShader.SetInt("u_DebugShowShadows", shadowSettings.DebugShowShadowMap ? 1 : 0);

                    // SSAO is now handled as a post-effect
                    // Set disabled defaults for legacy uniforms
                    GL.ActiveTexture(TextureUnit.Texture3);
                    GL.BindTexture(TextureTarget.Texture2D, Engine.Rendering.TextureCache.White1x1);
                    _pbrShader.SetInt("u_SSAOTexture", 3);
                    _pbrShader.SetInt("u_SSAOEnabled", 0);
                    _pbrShader.SetFloat("u_SSAOStrength", 0.0f);
                    _pbrShader.SetVec2("u_ScreenSize", new Vector2(_w, _h));
                }

                // Also render opaque objects when SSAO is disabled
                lastBound = Guid.Empty;
                Engine.Rendering.ShaderProgram? lastShader2 = null;
                for (int idx = 0; idx < items.Count; )
                {
                    var item = items[idx];
                    bool isTransparent = item.MaterialRuntime != null && item.MaterialRuntime.TransparencyMode != 0;
                    if (isTransparent) { idx++; continue; }

                    var matGuid = item.MaterialGuid;
                    // Load/bind material once for this material's group
                    Engine.Rendering.MaterialRuntime? mr = null;
                    if (!_materialCache.TryGetValue(matGuid, out var cached))
                    {
                        try
                        {
                            var asset = Engine.Assets.AssetDatabase.LoadMaterial(matGuid);
                            Func<Guid, string?> resolver = guid => Engine.Assets.AssetDatabase.TryGet(guid, out var rec) ? rec.Path : null;
                            mr = Engine.Rendering.MaterialRuntime.FromAsset(asset, resolver);
                            _materialCache[matGuid] = mr;
                        }
                        catch
                        {
                            mr = item.MaterialRuntime ?? new Engine.Rendering.MaterialRuntime { AlbedoTex = Engine.Rendering.TextureCache.White1x1 };
                        }
                    }
                    else mr = cached;

                    Engine.Rendering.ShaderProgram? shaderToUse = pbr;
                    try
                    {
                        if (!string.IsNullOrEmpty(mr.ShaderName))
                        {
                            var alt = Engine.Rendering.ShaderLibrary.GetShaderByName(mr.ShaderName);
                            if (alt != null) shaderToUse = alt;
                        }
                    }
                    catch { }
                    if (shaderToUse != null)
                    {
                        shaderToUse.Use();
                        float time = (float)_timeStopwatch.Elapsed.TotalSeconds;
                        mr.Bind(shaderToUse, time);
                        if (string.Equals(mr.ShaderName, "Water", StringComparison.OrdinalIgnoreCase))
                        {
                            GL.PatchParameter(PatchParameterInt.PatchVertices, 4);
                        }
                        SetShadowUniforms(shaderToUse, shadowSettings.Enabled);
                        // SSAO is now handled as a post-effect, not during main rendering
                        // Set disabled defaults for legacy shaders that still have SSAO uniforms
                        try
                        {
                            shaderToUse.SetInt("u_SSAOEnabled", 0);
                            shaderToUse.SetFloat("u_SSAOStrength", 0.0f);
                            shaderToUse.SetVec2("u_ScreenSize", new OpenTK.Mathematics.Vector2(_w, _h));
                            GL.ActiveTexture(TextureUnit.Texture3);
                            GL.BindTexture(TextureTarget.Texture2D, Engine.Rendering.TextureCache.White1x1);
                            shaderToUse.SetInt("u_SSAOTexture", 3);
                        }
                        catch { }
                    }
                    lastBound = matGuid;
                    lastShader2 = shaderToUse;

                    // Group items with this material by VAO
                    var groups = new Dictionary<int, List<RenderItem>>();
                    for (int k = 0; k < items.Count; k++)
                    {
                        var it = items[k];
                        if (it.MaterialGuid != matGuid) continue;
                        if (!groups.TryGetValue(it.Vao, out var list)) { list = new List<RenderItem>(); groups[it.Vao] = list; }
                        list.Add(it);
                    }

                    foreach (var kv in groups)
                    {
                        var group = kv.Value;
                        if (group.Count == 0) continue;
                        var first = group[0];
                        GL.BindVertexArray(first.Vao);
                        GL.BindBuffer(BufferTarget.ElementArrayBuffer, first.Ebo);

                        // Check if this is a double-sided mesh (Plane only - Quad is single-sided like Unity)
                        bool isDoubleSided = first.MeshType == MeshKind.Plane;
                        if (isDoubleSided) GL.Disable(EnableCap.CullFace);

                        foreach (var it in group)
                        {
                            if (lastShader2 != null)
                            {
                                lastShader2.SetMat4("u_Model", it.Model);
                                lastShader2.SetMat3("u_NormalMat", it.NormalMat3);
                                lastShader2.SetUInt("u_ObjectId", it.ObjectId);
                            }

                            var primitiveType = (lastShader2 != null && it.MaterialRuntime != null &&
                                                string.Equals(it.MaterialRuntime.ShaderName, "Water", StringComparison.OrdinalIgnoreCase))
                                ? PrimitiveType.Patches
                                : PrimitiveType.Triangles;

                            RecordDraw(primitiveType, it.IndexCount);
                            GL.DrawElements(primitiveType, it.IndexCount, DrawElementsType.UnsignedInt, 0);
                        }

                        if (isDoubleSided) GL.Enable(EnableCap.CullFace);
                    }

                    // Advance idx past all items with this material
                    int nextIdx = idx;
                    while (nextIdx < items.Count && items[nextIdx].MaterialGuid == matGuid) nextIdx++;
                    idx = nextIdx;
                }
            

            // === RENDER WATER (DISABLED - TO BE REIMPLEMENTED LATER) ===
            // RenderWater();

            // === QUEUE 3000: TRANSPARENT ===
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.DepthMask(false); // disable depth writes for proper blending

            lastBound = Guid.Empty;
            Engine.Rendering.ShaderProgram? lastShader3 = null;
            Engine.Rendering.MaterialRuntime? mr3 = null;
            int lastVao3 = -1;
            int lastEbo3 = -1;
            // collect transparent items and sort them back-to-front relative to camera
            _transparentItems.Clear();
            foreach (var it in items)
            {
                bool isTransparent = it.MaterialRuntime != null && it.MaterialRuntime.TransparencyMode != 0;
                if (isTransparent) _transparentItems.Add(it);
            }

            // Debug log for transparent items (once per frame when present)
            if (_transparentItems.Count > 0)
            {
                try
                {
                    var first = _transparentItems[0];
                    int tmode = first.MaterialRuntime?.TransparencyMode ?? -1;
                }
                catch { }
            }

            var camPosVec = new OpenTK.Mathematics.Vector3(CameraPosition());
            _transparentItems.Sort((a, b) =>
            {
                // Compute squared distances without allocating vectors
                float camX = camPosVec.X, camY = camPosVec.Y, camZ = camPosVec.Z;
                float ax = a.Model.M41 - camX, ay = a.Model.M42 - camY, az = a.Model.M43 - camZ;
                float bx = b.Model.M41 - camX, by = b.Model.M42 - camY, bz = b.Model.M43 - camZ;
                float da = ax * ax + ay * ay + az * az;
                float db = bx * bx + by * by + bz * bz;
                return db.CompareTo(da); // farthest first
            });

            foreach (var item in _transparentItems)
            {
                if (item.MaterialGuid != lastBound || _forceMaterialRebind)
                {
                    try { if (Engine.Assets.AssetDatabase.TryGet(item.MaterialGuid, out var rec)) { } } catch { }

                    // Reload MaterialRuntime from cache in case it was updated
                    mr3 = null;
                    if (!_materialCache.TryGetValue(item.MaterialGuid, out var cached))
                    {
                        // Material not in cache, reload it
                        try
                        {
                            var asset = Engine.Assets.AssetDatabase.LoadMaterial(item.MaterialGuid);
                            Func<Guid, string?> resolver = guid => Engine.Assets.AssetDatabase.TryGet(guid, out var rec) ? rec.Path : null;
                            mr3 = Engine.Rendering.MaterialRuntime.FromAsset(asset, resolver);
                            _materialCache[item.MaterialGuid] = mr3;
                        }
                        catch
                        {
                            mr3 = item.MaterialRuntime ?? new Engine.Rendering.MaterialRuntime { AlbedoTex = Engine.Rendering.TextureCache.White1x1 };
                        }
                    }
                    else
                    {
                        mr3 = cached;
                    }

                    Engine.Rendering.ShaderProgram? shaderToUse = pbr;
                    try
                    {
                        if (!string.IsNullOrEmpty(mr3.ShaderName))
                        {
                            var alt = Engine.Rendering.ShaderLibrary.GetShaderByName(mr3.ShaderName);
                            if (alt != null) shaderToUse = alt;
                        }
                    }
                    catch { }
                    if (shaderToUse != null)
                    {
                        shaderToUse.Use();
                        float time = (float)_timeStopwatch.Elapsed.TotalSeconds;
                        mr3.Bind(shaderToUse, time);
                        lastShader3 = shaderToUse;

                        // Enable tessellation for Water shader
                        if (string.Equals(mr3.ShaderName, "Water", StringComparison.OrdinalIgnoreCase))
                        {
                            GL.PatchParameter(PatchParameterInt.PatchVertices, 3); // 3 vertices per patch (triangle)
                        }
                    }
                    lastBound = item.MaterialGuid;
                    if (_forceMaterialRebind)
                    {
                        // Clear the flag once we've forced a rebind for this material
                        _forceMaterialRebind = false;
                    }
                }

                if (lastShader3 != null)
                {
                    lastShader3.SetMat4("u_Model", item.Model);
                    lastShader3.SetMat3("u_NormalMat", item.NormalMat3);
                    lastShader3.SetUInt("u_ObjectId", item.ObjectId);
                }

                // Check if this is a double-sided mesh (Plane only - Quad is single-sided like Unity)
                bool isDoubleSided = item.MeshType == MeshKind.Plane;
                if (isDoubleSided) GL.Disable(EnableCap.CullFace);

                // Avoid redundant VAO / EBO binds when consecutive items share them
                if (item.Vao != lastVao3)
                {
                    GL.BindVertexArray(item.Vao);
                    lastVao3 = item.Vao;
                }
                if (item.Ebo != lastEbo3)
                {
                    GL.BindBuffer(BufferTarget.ElementArrayBuffer, item.Ebo);
                    lastEbo3 = item.Ebo;
                }

                // Use patches for tessellation shaders, triangles for others
                var primitiveType = (lastShader3 != null && mr3 != null && string.Equals(mr3.ShaderName, "Water", StringComparison.OrdinalIgnoreCase))
                    ? PrimitiveType.Patches
                    : PrimitiveType.Triangles;

                // Record draw for overlay/stats counters
                RecordDraw(primitiveType, item.IndexCount);
                GL.DrawElements(primitiveType, item.IndexCount, DrawElementsType.UnsignedInt, 0);

                if (isDoubleSided) GL.Enable(EnableCap.CullFace);
            }

            // Draw water preview planes for any terrain entities that requested it.
            // We're inside the transparent pass: blending is enabled and depth writes are disabled.
            try
            {
                if (_scene?.Entities != null)
                {
                    foreach (var entity in _scene.Entities)
                    {
                        // Water preview disabled in new terrain system
                        continue;
                        /*
                        if (!entity.HasComponent<Engine.Components.Terrain>()) continue;
                        var terrain = entity.GetComponent<Engine.Components.Terrain>();

                        if (_planeVao == 0) continue;

                        // Build model matrix for the water plane
                        var modelWater = Matrix4.CreateScale(terrain.TerrainWidth, 1.0f, terrain.TerrainLength) * Matrix4.CreateTranslation(0f, 0f, 0f);
                        if (pbr != null)
                        {
                            pbr.SetMat4("u_Model", modelWater);
                            pbr.SetMat3("u_NormalMat", new OpenTK.Mathematics.Matrix3(modelWater));
                            pbr.SetUInt("u_ObjectId", entity.Id);
                        }

                        */
                    }
                }
            }
            catch (Exception)
            {
                LogManager.LogWarning("Water preview render error (transparent pass)", "ViewportRenderer");
            }

            // Restore state
            GL.DepthMask(true);
            GL.Disable(EnableCap.Blend);

            // Debug: SSAO overlay removed - SSAO is now a post-effect
            // View SSAO by adding it to GlobalEffects component
        }

        /// <summary>
        /// Lightweight pass that renders only object IDs into ColorAttachment1 of the single-sample FBO.
        /// This is needed when MSAA is enabled because integer ID attachments cannot be blitted from
        /// a multisampled framebuffer.
        /// </summary>
        private void RenderIdOnlyPass()
        {
            // ID-only pass: kept silent in normal operation to avoid noisy per-frame logs
            if (_fbo == 0) return;
            // Bind the regular single-sample FBO and set draw buffers so
            // fragment outputs map correctly for an ID-only pass. We don't
            // want/write the floating-point color attachment here in normal
            // operation — map location 0 -> NONE and location 1 -> ColorAttachment1 (the ID buffer).
            // For debugging (temporary), also support packing the uint ID into
            // the color buffer so we can visually / readback confirm rendered IDs.
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
            // Normal ID-only mapping: location0 disabled, location1 -> ColorAttachment1
            var db = new DrawBuffersEnum[] { DrawBuffersEnum.None, DrawBuffersEnum.ColorAttachment1 };
            GL.DrawBuffers(db.Length, db);

            // Preserve depth test and viewport
            GL.Viewport(0, 0, _w, _h);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);

            // Clear ID attachment
            uint zero = 0;
            GL.ClearBuffer(ClearBuffer.Color, 1, ref zero);
            // Ensure depth is cleared so the ID-only pass isn't rejected by stale depth
            // Enable depth write, set clear depth to far (1.0) and clear the depth buffer
            GL.DepthMask(true);
            GL.ClearDepth(1.0);
            GL.Clear(ClearBufferMask.DepthBufferBit);

            // Use the simple gfx shader which supports u_ObjectId uniform
            if (_gfxShader == 0) InitResources();
            GL.UseProgram(_gfxShader);

            // Iterate entities and emit their IDs by drawing their geometry.
            // drawCount removed: no per-pass debug counting in release flow
            var entitiesSpan = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_scene.Entities);
            for (int i = 0; i < entitiesSpan.Length; i++)
            {
                var e = entitiesSpan[i];
                var meshRenderer = e.GetComponent<Engine.Components.MeshRendererComponent>();
                if (meshRenderer == null || !meshRenderer.HasMeshToRender())
                    continue;

                var model = e.WorldMatrix;
                var mvp = model * _viewGL * _projGL;
                GL.UniformMatrix4(_locMvp, false, ref mvp);
                // Write entity id to the integer output location
                GL.Uniform1(_locId, e.Id);

                // Draw without packing color; keep ID output in the integer attachment only

                // Draw mesh (use same VAO paths as DrawForwardOpaque's simple fallback)
                if (meshRenderer.IsUsingCustomMesh())
                {
                    var customMesh = LoadCustomMesh(meshRenderer.CustomMeshGuid!.Value, meshRenderer.SubmeshIndex);
                    if (customMesh.HasValue)
                    {
                        GL.BindVertexArray(customMesh.Value.VAO);
                        GL.DrawElements(PrimitiveType.Triangles, customMesh.Value.IndexCount, DrawElementsType.UnsignedInt, 0);
                    }
                    else
                    {
                        GL.BindVertexArray(_legacyCubeVao);
                        GL.DrawElements(PrimitiveType.Triangles, _cubeIdx.Length, DrawElementsType.UnsignedInt, 0);
                    }
                }
                else
                {
                    // Disable culling for double-sided meshes (plane only - quad is single-sided like Unity)
                    bool isDoubleSided = meshRenderer.Mesh == MeshKind.Plane;
                    if (isDoubleSided) GL.Disable(EnableCap.CullFace);

                    switch (meshRenderer.Mesh)
                    {
                        case MeshKind.Cube:
                            GL.BindVertexArray(_legacyCubeVao);
                            GL.DrawElements(PrimitiveType.Triangles, _cubeIdx.Length, DrawElementsType.UnsignedInt, 0);
                            break;
                        case MeshKind.Plane:
                            GL.BindVertexArray(_legacyPlaneVao);
                            GL.DrawElements(PrimitiveType.Triangles, _planeIndexCount, DrawElementsType.UnsignedInt, 0);
                            break;
                        case MeshKind.Quad:
                            GL.BindVertexArray(_legacyQuadVao);
                            GL.DrawElements(PrimitiveType.Triangles, _quadIndexCount, DrawElementsType.UnsignedInt, 0);
                            break;
                        case MeshKind.Sphere:
                            GL.BindVertexArray(_legacySphereVao);
                            GL.DrawElements(PrimitiveType.Triangles, _sphereIndexCount, DrawElementsType.UnsignedInt, 0);
                            break;
                        case MeshKind.Capsule:
                            GL.BindVertexArray(_legacyCapsuleVao);
                            GL.DrawElements(PrimitiveType.Triangles, _capsuleIndexCount, DrawElementsType.UnsignedInt, 0);
                            break;
                        default:
                            GL.BindVertexArray(_legacyCubeVao);
                            GL.DrawElements(PrimitiveType.Triangles, _cubeIdx.Length, DrawElementsType.UnsignedInt, 0);
                            break;
                    }

                    if (isDoubleSided) GL.Enable(EnableCap.CullFace);
                }
            }

            // Restore main FBO and draw buffers
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, GetTargetFBO());
            var mainBufs = new DrawBuffersEnum[] { DrawBuffersEnum.ColorAttachment0, DrawBuffersEnum.ColorAttachment1 };
            GL.DrawBuffers(mainBufs.Length, mainBufs);
            GL.UseProgram(0);
            // Exit silently (no per-frame debug output)
        }

        private CachedEntityData CreateCachedData(Entity entity)
        {
            return new CachedEntityData
            {
                LastWorldMatrix = entity.WorldMatrix,
                LastMaterialGuid = entity.MaterialGuid ?? Guid.Empty,
                IsVisible = true,
                Bounds = new BoundingSphere() { Center = new Vector3(0f,0f,0f), Radius = 1f }
            };
        }

        private void RenderGizmosOnTop()
        {
            // Sauvegarder l'état du depth buffer
            GL.GetInteger(GetPName.DepthFunc, out int oldDepthFunc);

            // Option 1: Depth test mais toujours passer (gizmo visible même derrière)
            GL.DepthFunc(DepthFunction.Always);

            // Option 2: Ou désactiver complètement le depth test
            // GL.Disable(EnableCap.DepthTest);

            // Option 3: Ou utiliser depth bias pour pousser vers l'avant
            // GL.Enable(EnableCap.PolygonOffsetFill);
            // GL.PolygonOffset(-1.0f, -1.0f);

            GL.UseProgram(_gfxShader);
            GL.Uniform1(_locId, (int)Engine.Scene.EntityIdRange.GizmoReservedId);

            switch (_mode)
            {
                case GizmoMode.Translate: DrawTranslateGizmo(); break;
                case GizmoMode.Rotate: DrawRotateGizmo(); break;
                case GizmoMode.Scale: DrawScaleGizmo(); break;
            }

            // Restaurer l'état
            GL.DepthFunc((DepthFunction)oldDepthFunc);
            // GL.Enable(EnableCap.DepthTest);  // si vous aviez désactivé
            // GL.Disable(EnableCap.PolygonOffsetFill);  // si vous aviez utilisé l'offset
        }

        private void RenderLightIcons()
        {
            // Utilisation de CollectionsMarshal.AsSpan pour éviter l'allocation d'une liste
            var entitiesSpan = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_scene.Entities);
            GL.UseProgram(_gfxShader);
            GL.Disable(EnableCap.DepthTest);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.Uniform1(_locUseTex, 0);

            for (int i = 0; i < entitiesSpan.Length; i++)
            {
                var entity = entitiesSpan[i];
                if (!entity.HasComponent<Engine.Components.LightComponent>()) continue;
                var lightComponent = entity.GetComponent<Engine.Components.LightComponent>();
                if (lightComponent == null || !lightComponent.Enabled) continue;

                entity.GetWorldTRS(out var position, out var rotation, out var scale);
                float iconSize = 0.3f;
                var iconScale = Vector3.One * iconSize;
                var iconMatrix = Matrix4.CreateScale(iconScale) * Matrix4.CreateTranslation(position);
                var mvp = iconMatrix * _viewGL * _projGL;

                GL.UniformMatrix4(_locMvp, false, ref mvp);
                GL.Uniform1(_locId, entity.Id);
                Vector4 iconColor = GetLightIconColor(lightComponent);
                GL.Uniform4(_locAlbColor, iconColor.X, iconColor.Y, iconColor.Z, iconColor.W);
                GL.BindVertexArray(_lightIconVao);
                RecordDraw(PrimitiveType.Triangles, 24);
                GL.DrawElements(PrimitiveType.Triangles, 24, DrawElementsType.UnsignedInt, 0);
            }

            GL.Enable(EnableCap.DepthTest);
            GL.Disable(EnableCap.Blend);
        }

        // Draw a small overlay showing the shadow atlas for debugging
        private void DrawShadowAtlasOverlay()
        {
            try
            {
                var shadowSettings = Editor.State.EditorSettings.ShadowsSettings;
                if (!shadowSettings.DebugShowShadowMap) return;
                if (_shadowManager == null) return;
                if (_gfxShader == 0) return;

                // We'll render the overlay into the renderer's color target so ImGui.Image shows it.
                int prevFb = GL.GetInteger(GetPName.FramebufferBinding);
                int targetFbo = (_postTexHealthy && _postFbo != 0) ? _postFbo : _fbo;
                try { Console.WriteLine($"[Shadows] Overlay: prevFb={prevFb} targetFbo={targetFbo} postHealthy={_postTexHealthy}"); } catch { }

                // Bind the target framebuffer (post or main) so our overlay becomes part of Renderer.ColorTexture
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, targetFbo);
                // Ensure the draw buffer targets the color attachment 0 of that FBO
                var db = new DrawBuffersEnum[] { DrawBuffersEnum.ColorAttachment0 };
                GL.DrawBuffers(db.Length, db);

                // Disable depth test so overlay is always visible on top of the scene in that target
                bool wasDepthEnabled = GL.IsEnabled(EnableCap.DepthTest);
                if (wasDepthEnabled) GL.Disable(EnableCap.DepthTest);
                GL.Viewport(0, 0, _w, _h);

                // Diagnostic visual test: clear a small magenta rectangle in the overlay region of the target
                try
                {
                    GL.Disable(EnableCap.Blend);
                    GL.ColorMask(true, true, true, true);

                    int overlayW = Math.Max(32, _w / 4);
                    int overlayH = Math.Max(32, _h / 4);
                    int ox = _w - overlayW - 8; // margin
                    int oy = _h - overlayH - 8;
                    GL.Enable(EnableCap.ScissorTest);
                    GL.Scissor(ox, oy, overlayW, overlayH);
                    // Magenta clear color
                    GL.ClearColor(1.0f, 0.0f, 1.0f, 1.0f);
                    GL.Clear(ClearBufferMask.ColorBufferBit);
                    GL.Disable(EnableCap.ScissorTest);
                    try { Console.WriteLine($"[Shadows] Overlay diagnostic clear rect ox={ox} oy={oy} w={overlayW} h={overlayH} viewport={_w}x{_h}"); } catch { }
                }
                catch (Exception ex) { try { Console.WriteLine("[Shadows] Overlay diagnostic clear failed: " + ex.Message); } catch { } }

                // Use a small dedicated shader that reads the depth (R) and outputs grayscale for clarity
                if (_shadowOverlayProg == 0)
                {
                    const string vsSrc = "#version 330 core\nlayout(location=0) in vec3 aPos; layout(location=1) in vec2 aUV; uniform mat4 u_MVP; out vec2 vUV; void main(){ vUV = aUV; gl_Position = u_MVP * vec4(aPos,1.0); }";
                    const string fsSrc = "#version 330 core\nin vec2 vUV; out vec4 outColor; uniform sampler2D u_DepthTex; void main(){ vec4 c = texture(u_DepthTex, vUV); outColor = vec4(c.rgb, 1.0); }";
                    int vs = GL.CreateShader(ShaderType.VertexShader); GL.ShaderSource(vs, vsSrc); GL.CompileShader(vs);
                    int fs = GL.CreateShader(ShaderType.FragmentShader); GL.ShaderSource(fs, fsSrc); GL.CompileShader(fs);
                    _shadowOverlayProg = GL.CreateProgram(); GL.AttachShader(_shadowOverlayProg, vs); GL.AttachShader(_shadowOverlayProg, fs); GL.LinkProgram(_shadowOverlayProg);
                    GL.DeleteShader(vs); GL.DeleteShader(fs);
                }

                GL.UseProgram(_shadowOverlayProg);
                // Simple quad transform: place small quad in top-right
                var mvp = Matrix4.CreateScale(0.25f, 0.25f, 1f) * Matrix4.CreateTranslation(0.75f, 0.75f, 0f);
                int locM = GL.GetUniformLocation(_shadowOverlayProg, "u_MVP"); if (locM >= 0) GL.UniformMatrix4(locM, false, ref mvp);
                int locTex = GL.GetUniformLocation(_shadowOverlayProg, "u_DepthTex");
                GL.ActiveTexture(TextureUnit.Texture0);
                // If the debug color tile was created/attached during the shadow pass and
                // debug overlay is requested, prefer showing the color debug texture which
                // directly visualizes rasterization. Otherwise sample the depth texture.
                bool useColorDebug = shadowSettings.DebugShowShadowMap && _shadowDebugColorTex != 0;
                int texToBind = useColorDebug ? _shadowDebugColorTex : _shadowManager.ShadowTexture;
                if (texToBind != 0)
                {
                    GL.BindTexture(TextureTarget.Texture2D, texToBind);
                    if (locTex >= 0) GL.Uniform1(locTex, 0);
                    try { Console.WriteLine($"[Shadows] DrawShadowAtlasOverlay: bound tex id={texToBind} (useColorDebug={useColorDebug}) framebuffer={GL.GetInteger(GetPName.FramebufferBinding)}"); } catch { }
                }
                else
                {
                    try { Console.WriteLine("[Shadows] DrawShadowAtlasOverlay: no texture to display (shadow or debug color tex == 0)"); } catch { }
                    GL.BindTexture(TextureTarget.Texture2D, 0);
                }

                GL.BindVertexArray(_legacyQuadVao);
                RecordDraw(PrimitiveType.Triangles, _quadIndexCount);
                GL.DrawElements(PrimitiveType.Triangles, _quadIndexCount, DrawElementsType.UnsignedInt, 0);
                var err = GL.GetError();
                if (err != ErrorCode.NoError)
                {
                    try { Console.WriteLine($"[Shadows] DrawShadowAtlasOverlay GL error: {err}"); } catch { }
                }
                GL.BindVertexArray(0);
                GL.UseProgram(0);

                // Restore depth test state
                if (wasDepthEnabled) GL.Enable(EnableCap.DepthTest);

                // Restore previous framebuffer binding so normal presentation continues
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, prevFb);
                // Restore drawbuffers for the main FBO if needed
                if (prevFb == 0)
                {
                    try { GL.DrawBuffer(DrawBufferMode.Back); } catch { }
                }
                else
                {
                    var mainBufs = new DrawBuffersEnum[] { DrawBuffersEnum.ColorAttachment0, DrawBuffersEnum.ColorAttachment1 };
                    try { GL.DrawBuffers(mainBufs.Length, mainBufs); } catch { }
                }
            }
            catch { }
        }

        private Vector4 GetLightIconColor(Engine.Components.LightComponent light)
        {
            // Base color from light color with some alpha for visibility
            var baseColor = new Vector4(light.Color.X, light.Color.Y, light.Color.Z, 0.8f);
            
            // Adjust based on light type
            return light.Type switch
            {
                Engine.Components.LightType.Directional => new Vector4(1.0f, 1.0f, 0.5f, 0.8f), // Yellow for directional
                Engine.Components.LightType.Point => new Vector4(0.5f, 1.0f, 0.5f, 0.8f),      // Green for point
                Engine.Components.LightType.Spot => new Vector4(0.5f, 0.5f, 1.0f, 0.8f),       // Blue for spot
                _ => baseColor
            };
        }

        // === Collider gizmos (BoxCollider OBB wireframe) ===
        private void RenderColliderGizmosOnTop()
        {
            if (_scene == null) return;

            // Save depth func and set to always so lines are visible
            GL.GetInteger(GetPName.DepthFunc, out int oldDepthFunc);
            GL.DepthFunc(DepthFunction.Always);

            // Use line VAO/VBO and simple color shader
            GL.BindVertexArray(_lineVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _lineVbo);
            GL.UseProgram(_gfxShader);
            GL.Uniform1(_locUseTex, 0);
            // Use reserved ID so picking ignores these
            GL.Uniform1(_locId, (int)Engine.Scene.EntityIdRange.GizmoReservedId);

            var entitiesSpan = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_scene.Entities);
            for (int i = 0; i < entitiesSpan.Length; i++)
            {
                var e = entitiesSpan[i];
                // Draw gizmo only for Collider components of selected entities.
                // This avoids rendering collider wires for all objects and matches
                // the expected editor behavior where collider gizmos are shown
                // only when an object is selected.
                bool entitySelected = Editor.State.Selection.Selected.Contains(e.Id) || Editor.State.Selection.ActiveEntityId == e.Id;
                if (!entitySelected) continue;

                foreach (var comp in e.GetAllComponents())
                {
                    if (comp is not Engine.Components.Collider col) continue;
                    if (!col.Enabled) continue;

                    var colr = new Vector4(0.2f, 1.0f, 1.0f, 1.0f);
                    if (col.IsTrigger) colr.W = 0.55f;

                    if (comp is Engine.Components.MeshCollider mc)
                    {
                        // Dessiner les triangles du MeshCollider
                        DrawMeshColliderWire(mc, colr);
                    }
                    else if (comp is Engine.Components.SphereCollider sc)
                    {
                        ComputeSphereWorld(sc, out var c, out var r);
                        DrawSphereWire(c, r, colr, 32);
                    }
                    else if (comp is Engine.Components.CapsuleCollider cc)
                    {
                        ComputeCapsuleWorld(cc, out var c, out var axis, out var r, out var halfHeight);
                        DrawCapsuleWire(c, axis, r, halfHeight, colr, 28);
                    }
                    else
                    {
                        var obb = col.GetWorldOBB();
                        DrawObbWire(obb, colr);
                    }
                }

                // Also draw point light range gizmo for selected entities that have a Point light
                // This mirrors the collider gizmo behavior: only draw when entity is selected
                if (entitySelected && e.HasComponent<Engine.Components.LightComponent>())
                {
                    var light = e.GetComponent<Engine.Components.LightComponent>();
                    if (light != null && light.Enabled && light.Type == Engine.Components.LightType.Point)
                    {
                        // Compute world position and scale
                        e.GetWorldTRS(out var wpos, out var wrot, out var wscl);
                        // Visual radius: multiply Range by largest scale component to match world transform
                        float s = MathF.Max(MathF.Max(MathF.Abs(wscl.X), MathF.Abs(wscl.Y)), MathF.Abs(wscl.Z));
                        float radius = MathF.Max(1e-6f, light.Range * s);
                        var lightColor = new Vector4(1.0f, 0.84f, 0.2f, 0.9f); // warm yellow for lights
                        DrawSphereWire(wpos, radius, lightColor, 48);
                    }
                }
            }

            // Restore depth func
            GL.DepthFunc((DepthFunction)oldDepthFunc);
        }

        /// <summary>
        /// Render Canvas entities as 3D planes in the viewport (like Unity)
        /// </summary>
        private void RenderCanvasGizmos3D()
        {
            if (_scene == null || _quadVao == 0 || _quadIndexCount == 0) return;

            // Find all Canvas entities
            var canvasEntities = _scene.Entities.Where(e => e.HasComponent<Engine.Components.UI.CanvasComponent>()).ToList();
            if (canvasEntities.Count == 0) return;

            // Save current OpenGL state
            GL.GetInteger(GetPName.CurrentProgram, out int oldProgram);
            GL.GetFloat(GetPName.LineWidth, out float oldLineWidth);
            GL.GetInteger(GetPName.VertexArrayBinding, out int oldVao);
            GL.GetInteger(GetPName.DepthWritemask, out int oldDepthMask);
            bool blendWasEnabled = GL.IsEnabled(EnableCap.Blend);
            bool cullFaceWasEnabled = GL.IsEnabled(EnableCap.CullFace);

            // Setup rendering state for semi-transparent quads
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.DepthMask(false); // Don't write to depth buffer for transparent UI
            GL.Disable(EnableCap.CullFace); // Render both sides

            GL.UseProgram(_gfxShader);
            GL.Uniform1(_locUseTex, 0); // No texture
            GL.Uniform1(_locId, (int)Engine.Scene.EntityIdRange.GizmoReservedId); // Don't interfere with picking

            GL.BindVertexArray(_quadVao);

            foreach (var entity in canvasEntities)
            {
                var canvasComp = entity.GetComponent<Engine.Components.UI.CanvasComponent>();
                if (canvasComp == null || canvasComp.RuntimeCanvas == null) continue;

                // Get world transform
                entity.GetWorldTRS(out var worldPos, out var worldRot, out var worldScale);

                // Get Canvas size (in pixels, treat as world units)
                var canvasSize = canvasComp.RuntimeCanvas.Size;
                // Like Unity: nearly 1:1 scale (1 pixel ≈ 0.1 world units)
                // This makes a 1920x1080 canvas = 192x108 world units (very visible!)
                float canvasWidth = canvasSize.X * 0.1f;
                float canvasHeight = canvasSize.Y * 0.1f;

                // Create transformation matrix for the quad
                // Canvas is a 2D plane facing forward (Z+)
                var scaleMatrix = Matrix4.CreateScale(canvasWidth * worldScale.X, canvasHeight * worldScale.Y, 1f * worldScale.Z);
                var rotMatrix = Matrix4.CreateFromQuaternion(worldRot);
                var transMatrix = Matrix4.CreateTranslation(worldPos);
                var modelMatrix = scaleMatrix * rotMatrix * transMatrix;

                // Calculate MVP
                var mvp = modelMatrix * _viewGL * _projGL;
                GL.UniformMatrix4(_locMvp, false, ref mvp);

                // Determine color based on selection state
                bool isSelected = Editor.State.Selection.Selected.Contains(entity.Id) ||
                                  Editor.State.Selection.ActiveEntityId == entity.Id;

                Vector4 canvasColor;
                if (isSelected)
                {
                    // Bright cyan/orange for selected canvas
                    canvasColor = new Vector4(1.0f, 0.7f, 0.2f, 0.3f);
                }
                else
                {
                    // Subtle blue-gray for unselected canvas
                    canvasColor = new Vector4(0.5f, 0.6f, 0.8f, 0.15f);
                }

                GL.Uniform4(_locAlbColor, canvasColor.X, canvasColor.Y, canvasColor.Z, canvasColor.W);

                // Draw the filled quad
                RecordDraw(PrimitiveType.Triangles, _quadIndexCount);
                GL.DrawElements(PrimitiveType.Triangles, _quadIndexCount, DrawElementsType.UnsignedInt, 0);

                // Draw wireframe border
                GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Line);
                GL.LineWidth(2.0f);
                Vector4 borderColor = isSelected ? new Vector4(1.0f, 0.8f, 0.3f, 1.0f) : new Vector4(0.6f, 0.7f, 0.9f, 0.6f);
                GL.Uniform4(_locAlbColor, borderColor.X, borderColor.Y, borderColor.Z, borderColor.W);
                RecordDraw(PrimitiveType.Triangles, _quadIndexCount);
                GL.DrawElements(PrimitiveType.Triangles, _quadIndexCount, DrawElementsType.UnsignedInt, 0);
                GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);

                // Draw UI element rectangles on the canvas surface
                if (isSelected && canvasComp.RuntimeCanvas.Roots != null)
                {
                    RenderUIElementsOn3DCanvas(entity, canvasComp.RuntimeCanvas, modelMatrix, canvasWidth, canvasHeight);

                    // Restore state after UI elements rendering
                    GL.BindVertexArray(_quadVao);
                    GL.UseProgram(_gfxShader);
                    GL.Uniform1(_locUseTex, 0);
                    GL.Uniform1(_locId, (int)Engine.Scene.EntityIdRange.GizmoReservedId);
                }
            }

            // Restore rendering state completely
            if (blendWasEnabled) GL.Enable(EnableCap.Blend); else GL.Disable(EnableCap.Blend);
            GL.DepthMask(oldDepthMask != 0);
            if (cullFaceWasEnabled) GL.Enable(EnableCap.CullFace); else GL.Disable(EnableCap.CullFace);
            GL.LineWidth(oldLineWidth);
            GL.UseProgram(oldProgram);
            GL.BindVertexArray(oldVao);
        }

        /// <summary>
        /// Render UI element outlines on a 3D Canvas plane
        /// </summary>
        private void RenderUIElementsOn3DCanvas(Engine.Scene.Entity canvasEntity, Engine.UI.Canvas canvas, Matrix4 canvasTransform, float canvasWidth, float canvasHeight)
        {
            if (canvas.Roots == null) return;

            // Use line rendering for UI element boundaries
            GL.BindVertexArray(_lineVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _lineVbo);
            GL.UseProgram(_gfxShader);
            GL.Uniform1(_locUseTex, 0);
            GL.Uniform1(_locId, (int)Engine.Scene.EntityIdRange.GizmoReservedId);

            void DrawUIElement(Engine.UI.UIElement element, int depth = 0)
            {
                // Get element's local rect
                var rect = element.Rect.GetLocalRect(canvas.Size);

                // Convert rect from pixel coordinates to normalized canvas space [-0.5, 0.5]
                float left = (rect.Left / canvas.Size.X - 0.5f) * canvasWidth * 2f;
                float right = (rect.Right / canvas.Size.X - 0.5f) * canvasWidth * 2f;
                float top = (rect.Top / canvas.Size.Y - 0.5f) * canvasHeight * 2f;
                float bottom = (rect.Bottom / canvas.Size.Y - 0.5f) * canvasHeight * 2f;

                // Create corner points in canvas local space (XY plane)
                var corners = new Vector3[]
                {
                    new Vector3(left, top, 0.01f),      // Top-left
                    new Vector3(right, top, 0.01f),     // Top-right
                    new Vector3(right, bottom, 0.01f),  // Bottom-right
                    new Vector3(left, bottom, 0.01f)    // Bottom-left
                };

                // Transform corners to world space
                for (int i = 0; i < corners.Length; i++)
                {
                    corners[i] = Vector3.TransformPosition(corners[i], canvasTransform);
                }

                // Determine color based on element type
                Vector4 color;
                if (element is Engine.UI.UIText)
                    color = new Vector4(0.5f, 1.0f, 0.5f, 1.0f); // Green for text
                else if (element is Engine.UI.UIImage)
                    color = new Vector4(0.5f, 0.7f, 1.0f, 1.0f); // Blue for images
                else
                    color = new Vector4(1.0f, 1.0f, 1.0f, 0.8f); // White for generic

                // Draw 4 lines forming a rectangle
                GL.LineWidth(1.5f);
                DrawLineWorld(corners[0], corners[1], color); // Top
                DrawLineWorld(corners[1], corners[2], color); // Right
                DrawLineWorld(corners[2], corners[3], color); // Bottom
                DrawLineWorld(corners[3], corners[0], color); // Left

                // Recursively draw children
                if (element.Children != null)
                {
                    foreach (var child in element.Children)
                    {
                        DrawUIElement(child, depth + 1);
                    }
                }
            }

            // Draw all root elements
            foreach (var root in canvas.Roots)
            {
                DrawUIElement(root);
            }
        }

        private void DrawLineWorld(in Vector3 a, in Vector3 b, in Vector4 color)
        {
            Span<float> data = stackalloc float[6] { a.X, a.Y, a.Z, b.X, b.Y, b.Z };
            unsafe { fixed (float* p = data) { GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, sizeof(float) * 6, (IntPtr)p); } }
            // World space: identity model
            var mvp = _viewGL * _projGL;
            GL.UniformMatrix4(_locMvp, false, ref mvp);
            GL.Uniform4(_locAlbColor, color.X, color.Y, color.Z, color.W);
            GL.DrawArrays(PrimitiveType.Lines, 0, 2);
        }

        /// <summary>
        /// Draw MeshCollider wireframe by rendering all triangle edges
        /// </summary>
        private void DrawMeshColliderWire(Engine.Components.MeshCollider meshCollider, in Vector4 color)
        {
            // MeshCollider gizmo disabled - drawing 300k+ triangles or even bounding boxes
            // causes severe performance issues. Use the mesh renderer visualization instead.
            // The collision still works perfectly without the gizmo.
            return;
        }

        private void DrawObbWire(in Engine.Physics.OBB obb, in Vector4 color)
        {
            // Build 8 corners of the OBB
            var R = obb.Orientation;
            // Use row vectors as world axes; using columns would apply inverse (transpose) rotation
            var hx = new Vector3(R.M11, R.M12, R.M13) * obb.HalfSize.X;
            var hy = new Vector3(R.M21, R.M22, R.M23) * obb.HalfSize.Y;
            var hz = new Vector3(R.M31, R.M32, R.M33) * obb.HalfSize.Z;

            Vector3 c = obb.Center;
            Vector3 c000 = c - hx - hy - hz;
            Vector3 c100 = c + hx - hy - hz;
            Vector3 c010 = c - hx + hy - hz;
            Vector3 c110 = c + hx + hy - hz;
            Vector3 c001 = c - hx - hy + hz;
            Vector3 c101 = c + hx - hy + hz;
            Vector3 c011 = c - hx + hy + hz;
            Vector3 c111 = c + hx + hy + hz;

            // 12 edges
            GL.LineWidth(1.5f);
            // bottom face
            DrawLineWorld(c000, c100, color);
            DrawLineWorld(c100, c110, color);
            DrawLineWorld(c110, c010, color);
            DrawLineWorld(c010, c000, color);
            // top face
            DrawLineWorld(c001, c101, color);
            DrawLineWorld(c101, c111, color);
            DrawLineWorld(c111, c011, color);
            DrawLineWorld(c011, c001, color);
            // verticals
            DrawLineWorld(c000, c001, color);
            DrawLineWorld(c100, c101, color);
            DrawLineWorld(c110, c111, color);
            DrawLineWorld(c010, c011, color);
            GL.LineWidth(1f);
        }

        // Record a draw for overlay stats
        private void RecordDraw(PrimitiveType prim, int indexCount)
        {
            // One draw call
            _frameDrawCalls++;
            // Estimate triangles produced by this draw
            int tris = 0;
            switch (prim)
            {
                case PrimitiveType.Triangles: tris = indexCount / 3; break;
                case PrimitiveType.TriangleStrip: tris = Math.Max(0, indexCount - 2); break;
                case PrimitiveType.TriangleFan: tris = Math.Max(0, indexCount - 2); break;
                default: tris = 0; break;
            }
            _frameTriangles += tris;
            // We count rendered objects at a coarse granularity: increment when indexCount>0
            if (indexCount > 0) _frameRenderedObjects++;
        }

        private void ComputeSphereWorld(Engine.Components.SphereCollider sc, out Vector3 center, out float radius)
        {
            var e = sc.Entity!;
            e.GetWorldTRS(out var wpos, out var wrot, out var wscl);
            // Center in world
            center = wpos + Vector3.Transform(sc.Center * wscl, wrot);
            // Approx radius with max scale like runtime OBB
            float s = MathF.Max(MathF.Max(MathF.Abs(wscl.X), MathF.Abs(wscl.Y)), MathF.Abs(wscl.Z));
            radius = MathF.Max(1e-6f, sc.Radius * s);
        }

        private void ComputeCapsuleWorld(Engine.Components.CapsuleCollider cc, out Vector3 center, out Vector3 axisDir, out float radius, out float halfHeight)
        {
            var e = cc.Entity!;
            e.GetWorldTRS(out var wpos, out var wrot, out var wscl);
            // Center in world
            center = wpos + Vector3.Transform(cc.Center * wscl, wrot);

            // Axis dir from rotation only (unit)
            Vector3 localAxis = cc.Direction switch { 0 => Vector3.UnitX, 1 => Vector3.UnitY, 2 => Vector3.UnitZ, _ => Vector3.UnitY };
            var rotM = Matrix3.CreateFromQuaternion(wrot);
            axisDir = new Vector3(
                rotM.M11 * localAxis.X + rotM.M12 * localAxis.Y + rotM.M13 * localAxis.Z,
                rotM.M21 * localAxis.X + rotM.M22 * localAxis.Y + rotM.M23 * localAxis.Z,
                rotM.M31 * localAxis.X + rotM.M32 * localAxis.Y + rotM.M33 * localAxis.Z
            );
            if (axisDir.LengthSquared <= 1e-8f) axisDir = Vector3.UnitY; else axisDir.Normalize();

            // Radius: use max component scale to match broadphase approx
            float sMax = MathF.Max(MathF.Max(MathF.Abs(wscl.X), MathF.Abs(wscl.Y)), MathF.Abs(wscl.Z));
            radius = MathF.Max(1e-6f, cc.Radius * sMax);

            // Height along axis with axis scale; halfHeight for cylinder part (caps excluded)
            float axisScale = cc.Direction switch { 0 => MathF.Abs(wscl.X), 1 => MathF.Abs(wscl.Y), 2 => MathF.Abs(wscl.Z), _ => MathF.Abs(wscl.Y) };
            float fullH = MathF.Max(cc.Height * axisScale, 2f * radius);
            halfHeight = MathF.Max(0f, 0.5f * fullH - radius);
        }

        private void DrawSphereWire(in Vector3 center, float radius, in Vector4 color, int segments = 24)
        {
            segments = Math.Clamp(segments, 8, 128);
            // Three great circles: XY, XZ, YZ
            DrawCircle(center, radius, Vector3.UnitZ, color, segments); // XY plane (normal Z)
            DrawCircle(center, radius, Vector3.UnitY, color, segments); // XZ plane (normal Y)
            DrawCircle(center, radius, Vector3.UnitX, color, segments); // YZ plane (normal X)
        }

        private void DrawCapsuleWire(in Vector3 center, in Vector3 axisDir, float radius, float halfHeight, in Vector4 color, int segments = 24)
        {
            segments = Math.Clamp(segments, 8, 128);
            Vector3 a = axisDir.LengthSquared > 0 ? axisDir : Vector3.UnitY;
            a.Normalize();
            // Build orthonormal basis (u,v,a)
            Vector3 tmp = MathF.Abs(Vector3.Dot(a, Vector3.UnitY)) < 0.999f ? Vector3.UnitY : Vector3.UnitX;
            Vector3 u = Vector3.Normalize(Vector3.Cross(a, tmp));
            Vector3 v = Vector3.Normalize(Vector3.Cross(a, u));

            // End cap centers
            Vector3 c0 = center - a * halfHeight;
            Vector3 c1 = center + a * halfHeight;

            // Rings: at ends and middle, plane perpendicular to axis
            DrawCircle(c0, radius, a, color, segments);
            DrawCircle(c1, radius, a, color, segments);
            DrawCircle(center, radius, a, color, segments);

            // Longitudinal lines (approx silhouette): 4 or 8 around
            int rails = 8;
            for (int i = 0; i < rails; i++)
            {
                float t = (float)i / rails * MathF.Tau;
                Vector3 dir = MathF.Cos(t) * u + MathF.Sin(t) * v; // around circle
                // From bottom hemisphere to top hemisphere: offset by radius along dir
                Vector3 p0 = c0 + dir * radius;
                Vector3 p1 = c1 + dir * radius;
                DrawLineWorld(p0, p1, color);
            }
        }

        // Draw a circle centered at c, radius r, oriented with plane normal n (right-handed)
        private void DrawCircle(in Vector3 c, float r, in Vector3 n, in Vector4 color, int segments)
        {
            segments = Math.Clamp(segments, 8, 128);
            Vector3 nn = n.LengthSquared > 0 ? Vector3.Normalize(n) : Vector3.UnitZ;
            // Find tangent basis (u,v) in plane
            Vector3 refv = MathF.Abs(Vector3.Dot(nn, Vector3.UnitY)) < 0.999f ? Vector3.UnitY : Vector3.UnitX;
            Vector3 u = Vector3.Normalize(Vector3.Cross(nn, refv));
            Vector3 v = Vector3.Normalize(Vector3.Cross(nn, u));

            Vector3 prev = c + (u * r);
            int segs = segments;
            for (int i = 1; i <= segs; i++)
            {
                float ang = (float)i / segs * MathF.Tau;
                Vector3 p = c + (MathF.Cos(ang) * u + MathF.Sin(ang) * v) * r;
                DrawLineWorld(prev, p, color);
                prev = p;
            }
        }

        // Nouvelle méthode : picking géométrique des gizmos (sans buffer d'ID)
        public uint PickGizmoGeometric(int px, int py)
        {
            if (!_gizmoVisible || !HasValidSelection()) return ID_NONE;

            var ray = ScreenRay(px, py);
            float depth = DepthAtTarget(_cubePos);

            switch (_mode)
            {
                case GizmoMode.Translate:
                    return PickTranslateGizmo(ray, depth);
                case GizmoMode.Rotate:
                    return PickRotateGizmo(ray, depth);
                case GizmoMode.Scale:
                    return PickScaleGizmo(ray, depth);
                default:
                    return ID_NONE;
            }
        }

        private uint PickTranslateGizmo((Vector3 origin, Vector3 dir) ray, float depth)
        {
            var R = GizmoRotation();
            float axisLenPx = 90f, planePx = 60f, planeOffPx = 16f;
            float len = PixelsToWorld(axisLenPx, depth);
            float sPlan = PixelsToWorld(planePx, depth);
            float oPlan = PixelsToWorld(planeOffPx, depth);

            float threshold = PixelsToWorld(8f, depth); // Distance de tolérance

            // Test des plans d'abord (plus faciles à cliquer)
            var planes = new[]
            {
                (id: ID_GZ_PLANE_XY, normal: Vector3.Transform(Vector3.UnitZ, R),
                corner: _cubePos + Vector3.Transform(new Vector3(oPlan, oPlan, 0), R)),
                (id: ID_GZ_PLANE_XZ, normal: Vector3.Transform(Vector3.UnitY, R),
                corner: _cubePos + Vector3.Transform(new Vector3(oPlan, 0, oPlan), R)),
                (id: ID_GZ_PLANE_YZ, normal: Vector3.Transform(Vector3.UnitX, R),
                corner: _cubePos + Vector3.Transform(new Vector3(0, oPlan, oPlan), R))
            };

                    foreach (var plane in planes)
                    {
                        if (RayPlane(ray.origin, ray.dir, plane.corner, plane.normal, out float t) && t > 0)
                        {
                            var hit = ray.origin + ray.dir * t;
                            var local = Vector3.Transform(hit - _cubePos, Quaternion.Invert(R));

                            // Vérifier si dans le carré du plan
                            bool inPlane = plane.id switch
                            {
                                ID_GZ_PLANE_XY => local.X >= oPlan && local.X <= oPlan + sPlan &&
                                                local.Y >= oPlan && local.Y <= oPlan + sPlan && Math.Abs(local.Z) < threshold,
                                ID_GZ_PLANE_XZ => local.X >= oPlan && local.X <= oPlan + sPlan &&
                                                local.Z >= oPlan && local.Z <= oPlan + sPlan && Math.Abs(local.Y) < threshold,
                                ID_GZ_PLANE_YZ => local.Y >= oPlan && local.Y <= oPlan + sPlan &&
                                                local.Z >= oPlan && local.Z <= oPlan + sPlan && Math.Abs(local.X) < threshold,
                                _ => false
                            };

                            if (inPlane) return plane.id;
                        }
                    }

                    // Test des axes
                    var axes = new[]
                    {
                (id: ID_GZ_X, dir: Vector3.Transform(Vector3.UnitX, R)),
                (id: ID_GZ_Y, dir: Vector3.Transform(Vector3.UnitY, R)),
                (id: ID_GZ_Z, dir: Vector3.Transform(Vector3.UnitZ, R))
            };

            foreach (var axis in axes)
            {
                float t = ClosestParamOnAxisToRay(_cubePos, axis.dir, ray.origin, ray.dir);
                if (t >= 0 && t <= len)
                {
                    var closest = _cubePos + axis.dir * t;
                    float dist = (ray.origin + ray.dir * Vector3.Dot(closest - ray.origin, ray.dir) - closest).Length;
                    if (dist <= threshold) return axis.id;
                }
            }

            return ID_NONE;
        }

        private uint PickRotateGizmo((Vector3 origin, Vector3 dir) ray, float depth)
        {
            var R = GizmoRotation();
            float ringPx = 90f, thickPx = 8f;
            float Rw = PixelsToWorld(ringPx, depth);
            float T = PixelsToWorld(thickPx, depth);

            var rings = new[]
            {
        (id: ID_GZ_ROT_X, normal: Vector3.Transform(Vector3.UnitX, R)),
        (id: ID_GZ_ROT_Y, normal: Vector3.Transform(Vector3.UnitY, R)),
        (id: ID_GZ_ROT_Z, normal: Vector3.Transform(Vector3.UnitZ, R))
    };

            foreach (var ring in rings)
            {
                if (RayPlane(ray.origin, ray.dir, _cubePos, ring.normal, out float t) && t > 0)
                {
                    var hit = ray.origin + ray.dir * t;
                    float dist = (hit - _cubePos).Length;
                    if (dist >= Rw - T * 0.5f && dist <= Rw + T * 0.5f)
                        return ring.id;
                }
            }

            return ID_NONE;
        }

        private uint PickScaleGizmo((Vector3 origin, Vector3 dir) ray, float depth)
        {
            var R = GizmoRotation();
            float axisLenPx = 90f, uniPx = 18f;
            float len = PixelsToWorld(axisLenPx, depth);
            float u = PixelsToWorld(uniPx, depth);
            float threshold = PixelsToWorld(8f, depth);

            // Test uniform handle d'abord
            var fwd = Forward();
            if (RayPlane(ray.origin, ray.dir, _cubePos, fwd, out float t) && t > 0)
            {
                var hit = ray.origin + ray.dir * t;
                var d = hit - _cubePos;
                var right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, fwd));
                var up = Vector3.Normalize(Vector3.Cross(fwd, right));

                float projX = Math.Abs(Vector3.Dot(d, right));
                float projY = Math.Abs(Vector3.Dot(d, up));

                if (projX <= u * 0.5f && projY <= u * 0.5f) return ID_GZ_S_UNI;
            }

            // Test des axes
            var axes = new[]
            {
        (id: ID_GZ_S_X, dir: Vector3.Transform(Vector3.UnitX, R)),
        (id: ID_GZ_S_Y, dir: Vector3.Transform(Vector3.UnitY, R)),
        (id: ID_GZ_S_Z, dir: Vector3.Transform(Vector3.UnitZ, R))
    };

            foreach (var axis in axes)
            {
                float tAxis = ClosestParamOnAxisToRay(_cubePos, axis.dir, ray.origin, ray.dir);
                if (tAxis >= 0 && tAxis <= len)
                {
                    var closest = _cubePos + axis.dir * tAxis;
                    float dist = (ray.origin + ray.dir * Vector3.Dot(closest - ray.origin, ray.dir) - closest).Length;
                    if (dist <= threshold) return axis.id;
                }
            }

            return ID_NONE;
        }

        // ---------- Picking ----------
        public uint PickIdAt(int px, int py)
        {
            if (_fbo == 0) return ID_NONE;
            px = Math.Clamp(px, 0, _w - 1);
            py = Math.Clamp(py, 0, _h - 1);

            // PickIdAt: silent in normal operation
            // If MSAA is active, resolve multisampled color and populate ID attachment
            if (_msaaRenderer != null && _antiAliasingMode.IsMSAA())
            {
                _msaaRenderer.ResolveToFramebuffer((uint)_fbo);
                RenderIdOnlyPass();
            }

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _fbo);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment1);
            uint id = 0;
            unsafe { GL.ReadPixels(px, py, 1, 1, PixelFormat.RedInteger, PixelType.UnsignedInt, (IntPtr)(&id)); }
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0);
            // Read complete; return id (no debug log)
            return id;
        }

        public uint PickIdAtFat(int px, int py, int radius = 8)
        {
            if (_fbo == 0) return ID_NONE;

            // PickIdAtFat: silent in normal operation

            if (_gizmoVisible && HasValidSelection())
            {
                uint gizmoId = PickGizmoGeometric(px, py);
                if (gizmoId != ID_NONE) return gizmoId;
            }

            int x0 = Math.Clamp(px - radius, 0, _w - 1);
            int y0 = Math.Clamp(py - radius, 0, _h - 1);
            int x1 = Math.Clamp(px + radius, 0, _w - 1);
            int y1 = Math.Clamp(py + radius, 0, _h - 1);
            int rw = x1 - x0 + 1, rh = y1 - y0 + 1;
            int count = rw * rh;

            if (_pickBuffer.Length < count)
                _pickBuffer = new uint[count];

            _pickCount.Clear();

            // If MSAA is active, resolve multisampled color and populate ID attachment
            if (_msaaRenderer != null && _antiAliasingMode.IsMSAA())
            {
                _msaaRenderer.ResolveToFramebuffer((uint)_fbo);
                RenderIdOnlyPass();
            }

            // First try reading integer ID attachment (ColorAttachment1)
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _fbo);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment1);
            unsafe { fixed (uint* p = _pickBuffer) { GL.ReadPixels(x0, y0, rw, rh, PixelFormat.RedInteger, PixelType.UnsignedInt, (IntPtr)p); } }

            // Fallback packing removed: rely on integer ID attachment readback

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0);

            for (int i = 0; i < count; i++)
            {
                uint v = _pickBuffer[i];
                if (v == 0 || v == Engine.Scene.EntityIdRange.GizmoReservedId) continue;
                _pickCount[v] = _pickCount.TryGetValue(v, out int c) ? c + 1 : 1;
            }

            // Read complete; compute best candidate silently
            if (_pickCount.Count == 0) return ID_NONE;

            uint bestEntity = 0;
            int bestCount = 0;
            foreach (var kv in _pickCount)
            {
                if (kv.Value > bestCount)
                {
                    bestEntity = kv.Key;
                    bestCount = kv.Value;
                }
            }

            return bestEntity;
            
        }

        void UpdateLightingUniforms()
        {
            var L = Lighting.Build(_scene); // <— récupère les lights

            // Set lighting state for skybox renderer
            Engine.Rendering.SkyboxRenderer.CurrentLightingState = L;

            // Global dir light via UBO existant
            var globals = new GlobalUniforms
            {
                ViewMatrix = _viewGL,
                ProjectionMatrix = _projGL,
                ViewProjectionMatrix = _viewGL * _projGL,
                CameraPosition = CameraPosition(),
                DirLightDirection = L.HasDirectional ? L.DirDirection : new Vector3(0,1,0) // direction réelle (négation gérée dans le shader)
            };
            GL.BindBuffer(BufferTarget.UniformBuffer, _globalUBO);
            GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero,
                System.Runtime.InteropServices.Marshal.SizeOf<GlobalUniforms>(), ref globals);

            // Points (simple en uniforms classiques)
            GL.UseProgram(_gfxShader);
            int locCount = GL.GetUniformLocation(_gfxShader, "uPointCount");
            // Dir light
            GL.Uniform3(GL.GetUniformLocation(_gfxShader, "uDirColor"), L.HasDirectional ? L.DirColor : new Vector3(0,0,0));
            GL.Uniform1(GL.GetUniformLocation(_gfxShader, "uDirIntensity"), L.HasDirectional ? L.DirIntensity : 0f);

            // Ambient (from environment settings)
            GL.Uniform3(GL.GetUniformLocation(_gfxShader, "uAmbientColor"), L.AmbientColor);
            GL.Uniform1(GL.GetUniformLocation(_gfxShader, "uAmbientIntensity"), L.AmbientIntensity);

            // AO : si tu n’as pas de map, mets 0
            GL.Uniform1(GL.GetUniformLocation(_gfxShader, "uHasAOMap"), 0f);
            GL.Uniform1(locCount, Math.Min(L.Points.Count, 4));
            for (int i = 0; i < Math.Min(L.Points.Count, 4); i++)
            {
                var (p,c,inten,range) = L.Points[i];
                GL.Uniform3(GL.GetUniformLocation(_gfxShader, $"uPoints[{i}].pos"),   p);
                GL.Uniform3(GL.GetUniformLocation(_gfxShader, $"uPoints[{i}].color"), c);
                GL.Uniform1(GL.GetUniformLocation(_gfxShader, $"uPoints[{i}].intensity"), inten);
                GL.Uniform1(GL.GetUniformLocation(_gfxShader, $"uPoints[{i}].range"), range);
            }
        }

        public List<uint> PickIdsInRect(int x0, int y0, int x1, int y1)
        {
            var list = new List<uint>();
            if (_fbo == 0) return list;
            int minx = Math.Clamp(Math.Min(x0, x1), 0, _w - 1);
            int maxx = Math.Clamp(Math.Max(x0, x1), 0, _w - 1);
            int miny = Math.Clamp(Math.Min(y0, y1), 0, _h - 1);
            int maxy = Math.Clamp(Math.Max(y0, y1), 0, _h - 1);
            int rw = maxx - minx + 1, rh = maxy - miny + 1;

            // If MSAA is active, resolve multisampled color and populate ID attachment
            if (_msaaRenderer != null && _antiAliasingMode.IsMSAA())
            {
                _msaaRenderer.ResolveToFramebuffer((uint)_fbo);
                RenderIdOnlyPass();
            }

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _fbo);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment1);
            uint[] buf = new uint[rw * rh];
            unsafe { fixed (uint* p = buf) { GL.ReadPixels(minx, miny, rw, rh, PixelFormat.RedInteger, PixelType.UnsignedInt, (IntPtr)p); } }
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0);

            var seen = new HashSet<uint>();
            foreach (var v in buf)
                if (v != 0 && !IsGizmo(v) && seen.Add(v)) list.Add(v);
            return list;
        }

        // Pick world position at pixel coordinates using depth buffer
        public Vector3? PickWorldPositionAt(int px, int py)
        {
            if (_fbo == 0) return null;
            px = Math.Clamp(px, 0, _w - 1);
            py = Math.Clamp(py, 0, _h - 1);

            // Read depth value from depth texture
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _fbo);
            float depth = 0f;
            unsafe { GL.ReadPixels(px, py, 1, 1, PixelFormat.DepthComponent, PixelType.Float, (IntPtr)(&depth)); }
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0);

            if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[PickWorldPosition] px={px}, py={py}, depth={depth}");

            // If depth is 1.0, we hit the far plane (nothing there)
            if (depth >= 0.9999f)
            {
                if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log("[PickWorldPosition] Depth at far plane, no geometry");
                return null;
            }

            // Convert pixel coordinates to NDC (Normalized Device Coordinates)
            // Note: px,py are already in GL coordinates (bottom-left origin)
            float ndcX = (2.0f * px) / _w - 1.0f;
            float ndcY = (2.0f * py) / _h - 1.0f;
            float ndcZ = 2.0f * depth - 1.0f; // OpenGL depth is [0,1], NDC is [-1,1]

            if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[PickWorldPosition] NDC: x={ndcX}, y={ndcY}, z={ndcZ}");

            // Transform from clip space to world space
            var viewProj = _viewGL * _projGL;
            var invViewProj = viewProj.Inverted();
            var clipPos = new Vector4(ndcX, ndcY, ndcZ, 1.0f);
            var worldPos = clipPos * invViewProj;

            if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[PickWorldPosition] Clip: {clipPos}, World (before divide): {worldPos}");

            // Perspective divide
            if (Math.Abs(worldPos.W) < 0.0001f)
            {
                if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log("[PickWorldPosition] W component too small");
                return null;
            }
            worldPos /= worldPos.W;

            if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[PickWorldPosition] Final world position: {worldPos.Xyz}");
            return worldPos.Xyz;
        }

        // --- Focus / Frame ---
        private void UpdateCameraAnimation()
        {
            if (!_camAnimating) return;
            _target = Vector3.Lerp(_target, _targetGoal, 0.18f);
            _distance = Mathf.Lerp(_distance, _distanceGoal, 0.18f);
            if ((_target - _targetGoal).LengthSquared < 1e-5f && MathF.Abs(_distance - _distanceGoal) < 1e-3f)
            { _target = _targetGoal; _distance = _distanceGoal; _camAnimating = false; }
        }

        public void FrameSelection(bool smooth = true)
        {
            // Keep existing method for backward compatibility, delegate to parameterized version
            var selectedIds = Selection.Selected.ToList();
            if (selectedIds.Count == 0 && Selection.ActiveEntityId != 0)
                selectedIds.Add(Selection.ActiveEntityId);
            FrameSelection(selectedIds, smooth);
        }

        public void FrameSelection(List<uint> entityIds, bool smooth = true)
        {
            if (entityIds.Count == 0) return;
            var first = _scene.GetById(entityIds[0]);
            if (first == null) return;
            first.GetWorldTRS(out var pos, out _, out var scale);
            
            // Calculate optimal radius based on entity type and actual size
            float radius = CalculateOptimalFrameRadius(first, scale);
            SetFocus(pos, radius, smooth);
        }
        
        private float CalculateOptimalFrameRadius(Entity entity, Vector3 scale)
        {
            // Special handling for terrain - use a moderate view distance
            if (entity.HasComponent<Engine.Components.Terrain>())
            {
                var terrain = entity.GetComponent<Engine.Components.Terrain>();
                if (terrain != null)
                {
                    // For terrain, use a fraction of its size to get a nice overview
                    // Typical terrain is 2000x2000, so we want to be at ~300-500 units away
                    float terrainSize = MathF.Max(terrain.TerrainWidth, terrain.TerrainLength);
                    return terrainSize * 0.25f; // 25% of terrain size gives good overview
                }
            }
            
            // For mesh objects, calculate bounds based on mesh type and scale
            var meshRenderer = entity.GetComponent<MeshRendererComponent>();
            if (meshRenderer != null)
            {
                // Check if using custom mesh with actual bounds
                if (meshRenderer.IsUsingCustomMesh() && meshRenderer.CustomMeshGuid.HasValue)
                {
                    // Try to get actual mesh bounds from the loaded mesh
                    var customMesh = LoadCustomMesh(meshRenderer.CustomMeshGuid.Value, meshRenderer.SubmeshIndex);
                    if (customMesh.HasValue)
                    {
                        // Use a conservative estimate based on scale
                        // Most imported meshes are normalized to roughly 1-2 units
                        float meshMaxScale = MathF.Max(MathF.Max(MathF.Abs(scale.X), MathF.Abs(scale.Y)), MathF.Abs(scale.Z));
                        return MathF.Max(1.0f, meshMaxScale * 1.5f);
                    }
                }
                
                // For primitive meshes, use accurate bounds
                float baseRadius = meshRenderer.Mesh switch
                {
                    MeshKind.Cube => 0.866f,      // sqrt(3)/2 for unit cube diagonal
                    MeshKind.Sphere => 0.5f,      // Radius of unit sphere
                    MeshKind.Capsule => 1.0f,     // Capsule height/2
                    MeshKind.Plane => 0.707f,     // sqrt(2)/2 for unit plane diagonal
                    MeshKind.Quad => 0.707f,      // sqrt(2)/2 for unit quad diagonal
                    _ => 1.0f
                };
                
                float primitiveMaxScale = MathF.Max(MathF.Max(MathF.Abs(scale.X), MathF.Abs(scale.Y)), MathF.Abs(scale.Z));
                return MathF.Max(0.5f, baseRadius * primitiveMaxScale * 1.2f); // 1.2x for comfortable framing
            }
            
            // Fallback: use entity's BoundsRadius with minimum
            return MathF.Max(1.0f, entity.BoundsRadius);
        }

        public void FocusUnderCursor(int px, int py, bool smooth = true)
        {
            uint id = PickIdAtFat(px, py, 4);
            if (id != 0 && !IsGizmo(id))
            {
                var e = _scene.GetById(id);
                if (e != null) 
                { 
                    e.GetWorldTRS(out var pos, out _, out var scale);
                    float radius = CalculateOptimalFrameRadius(e, scale);
                    SetFocus(pos, radius, smooth); 
                    return; 
                }
            }
            var ray = ScreenRay(px, py);
            if (RayPlane(ray.origin, ray.dir, Vector3.Zero, Vector3.UnitY, out float t))
            {
                var hit = ray.origin + ray.dir * t;
                SetFocus(hit, 1.0f, smooth);
            }
        }

        private void SetFocus(Vector3 center, float radius, bool smooth)
        {
            _targetGoal = center;
            
            // Calculate optimal distance to frame the object comfortably
            // Formula: distance = radius / tan(fovY/2) gives the distance for object to fill screen height
            // We add extra margin (50% of radius) for comfortable viewing with some breathing room
            float baseDistance = radius / MathF.Tan(_fovY * 0.5f);
            float margin = radius * 0.5f; // 50% margin for comfortable framing
            _distanceGoal = MathF.Max(0.5f, baseDistance + margin); // Minimum 0.5 units away
            
            if (smooth) 
                _camAnimating = true; 
            else 
            { 
                _target = _targetGoal; 
                _distance = _distanceGoal; 
                _camAnimating = false; 
            }
        }

        // ===================== DRAG + SNAPPING =====================
        private static float Snap(float v, float step) => MathF.Round(v / step) * step;
        private Quaternion GizmoRotation() => _localSpace ? _cubeRot : Quaternion.Identity;

        // Helpers snapshots multi
        private void SnapshotSelectionForDrag()
        {
            if (Scene == null) return;

            // ✅ Utiliser Selection.Selected au lieu de Selection.Set
            var selectedIds = Selection.Selected.ToList();
            if (selectedIds.Count == 0 && Selection.ActiveEntityId != 0)
                selectedIds.Add(Selection.ActiveEntityId);

            if (selectedIds.Count == 0)
            {
                _preDragPrimaryId = 0;
                return;
            }

            _preDragLocal.Clear();
            _preDragWorld.Clear();
            _preDragPrimaryId = Selection.ActiveEntityId;

            foreach (var id in selectedIds)
            {
                var e = _scene.GetById(id);
                if (e == null) continue;

                _preDragLocal[id] = new TRS { P = e.Transform.Position, R = e.Transform.Rotation, S = e.Transform.Scale };
                e.GetWorldTRS(out var wp, out var wr, out var ws);
                _preDragWorld[id] = new TRS { P = wp, R = wr, S = ws };
            }

            // S'assurer que le primaryId est valide
            if (_preDragPrimaryId == 0 && selectedIds.Count > 0)
                _preDragPrimaryId = selectedIds[0];

            if (_preDragPrimaryId != 0)
            {
                var e = _scene.GetById(_preDragPrimaryId);
                if (e != null)
                    _preDragPrimaryLocal = new Xform { Pos = e.Transform.Position, Rot = e.Transform.Rotation, Scl = e.Transform.Scale };
            }
        }

        private void PushCompositeEndDrag(string label)
        {
            if (_preDragLocal.Count == 0) return;
            var comp = new CompositeAction(label);
            foreach (var kv in _preDragLocal)
            {
                var id = kv.Key;
                var before = kv.Value; // local at begin
                var e = _scene.GetById(id);
                if (e == null) continue;
                var after = new Xform { Pos = e.Transform.Position, Rot = e.Transform.Rotation, Scl = e.Transform.Scale };
                if (before.P == after.Pos && before.R == after.Rot && before.S == after.Scl) continue;
                comp.Add(new TransformAction(label, id, new Xform { Pos = before.P, Rot = before.R, Scl = before.S }, after));
            }
            if (comp.Count > 0) 
            {
                // Emit event instead of directly pushing to UndoRedo
                GizmoDragEnded?.Invoke(comp);
            }

            _preDragLocal.Clear();
            _preDragWorld.Clear();
            _preDragPrimaryId = 0;
        }

        // --- Translate ---
        public void BeginDragTranslate(uint gizmoId, int px, int py)
        {
            EditingTouched?.Invoke();
            if (_preDragLocal.Count == 0) SnapshotSelectionForDrag();
            if (_preDragLocal.Count == 0) { _dragKind = DragKind.None; _activeId = 0; return; }

            bool isAxis = gizmoId == ID_GZ_X || gizmoId == ID_GZ_Y || gizmoId == ID_GZ_Z;
            bool isPlane = gizmoId == ID_GZ_PLANE_XY || gizmoId == ID_GZ_PLANE_XZ || gizmoId == ID_GZ_PLANE_YZ;
            if (!isAxis && !isPlane) { _dragKind = DragKind.None; _activeId = ID_NONE; return; }

            _activeId = gizmoId;
            _drag_startObjPos = _cubePos;
            var R = GizmoRotation();

            if (isAxis)
            {
                _dragKind = DragKind.TranslateAxis;
                var axis = gizmoId == ID_GZ_X ? Vector3.UnitX : gizmoId == ID_GZ_Y ? Vector3.UnitY : Vector3.UnitZ;
                _drag_axisDir = Vector3.Normalize(Vector3.Transform(axis, R));
                var ray = ScreenRay(px, py);
                _drag_t0 = ClosestParamOnAxisToRay(_drag_startObjPos, _drag_axisDir, ray.origin, ray.dir);
            }
            else
            {
                _dragKind = DragKind.TranslatePlane;
                var n = gizmoId == ID_GZ_PLANE_XY ? Vector3.UnitZ :
                        gizmoId == ID_GZ_PLANE_XZ ? Vector3.UnitY : Vector3.UnitX;
                _drag_planeNormal = Vector3.Normalize(Vector3.Transform(n, R));
                var ray = ScreenRay(px, py);
                if (RayPlane(ray.origin, ray.dir, _drag_startObjPos, _drag_planeNormal, out var t))
                    _drag_planeHit0 = ray.origin + ray.dir * t;
                else
                    _drag_planeHit0 = _drag_startObjPos;
            }
        }

        public void UpdateDragTranslate(int px, int py)
        {
            var ray = ScreenRay(px, py);
            if (_dragKind == DragKind.TranslateAxis)
            {
                float t1 = ClosestParamOnAxisToRay(_drag_startObjPos, _drag_axisDir, ray.origin, ray.dir);
                float dt = t1 - _drag_t0;
                if (_snapOn) dt = Snap(dt, _snapMove);
                _cubePos = _drag_startObjPos + _drag_axisDir * dt;
                if (_gizmoVisible) _gizmoPos = _cubePos;

            }
            else if (_dragKind == DragKind.TranslatePlane)
            {
                if (RayPlane(ray.origin, ray.dir, _drag_startObjPos, _drag_planeNormal, out var t1))
                {
                    var hit = ray.origin + ray.dir * t1;
                    var delta = hit - _drag_planeHit0;
                    if (_snapOn)
                        delta = new Vector3(Snap(delta.X, _snapMove), Snap(delta.Y, _snapMove), Snap(delta.Z, _snapMove));
                    _cubePos = _drag_startObjPos + delta;
                    if (_gizmoVisible) _gizmoPos = _cubePos;

                }
            }

            // applique aux N objets: translation monde par delta
            var deltaW = _cubePos - _drag_startObjPos;
            foreach (var id in _preDragLocal.Keys)
            {
                var e = _scene.GetById(id);
                if (e == null) continue;
                var startW = _preDragWorld[id];
                e.SetWorldTRS(startW.P + deltaW, startW.R, startW.S);
            }
        }

        // --- Rotate ---
        public void BeginDragRotate(uint gizmoId, int px, int py)
        {
            EditingTouched?.Invoke();
            if (_preDragLocal.Count == 0) SnapshotSelectionForDrag();
            if (_preDragLocal.Count == 0) { _dragKind = DragKind.None; _activeId = 0; return; }

            _activeId = gizmoId;
            _dragKind = DragKind.Rotate;
            _drag_startObjPos = _cubePos;
            var axis = gizmoId == ID_GZ_ROT_X ? Vector3.UnitX : gizmoId == ID_GZ_ROT_Y ? Vector3.UnitY : Vector3.UnitZ;
            _rot_axis = Vector3.Normalize(Vector3.Transform(axis, GizmoRotation()));
            _rot_start = _cubeRot;

            var any = MathF.Abs(Vector3.Dot(_rot_axis, Vector3.UnitY)) > 0.9f ? Vector3.UnitX : Vector3.UnitY;
            _rot_u = Vector3.Normalize(any - Vector3.Dot(any, _rot_axis) * _rot_axis);
            _rot_v = Vector3.Normalize(Vector3.Cross(_rot_axis, _rot_u));

            var ray = ScreenRay(px, py);
            if (!RayPlane(ray.origin, ray.dir, _cubePos, _rot_axis, out float t)) { _rot_angle0 = 0; return; }
            var hit = ray.origin + ray.dir * t;
            var r = hit - _cubePos;
            _rot_angle0 = MathF.Atan2(Vector3.Dot(r, _rot_v), Vector3.Dot(r, _rot_u));
        }

        public void UpdateDragRotate(int px, int py)
        {
            var ray = ScreenRay(px, py);
            if (!RayPlane(ray.origin, ray.dir, _cubePos, _rot_axis, out float t1)) return;
            var hit = ray.origin + ray.dir * t1;
            var r = hit - _cubePos;
            float a1 = MathF.Atan2(Vector3.Dot(r, _rot_v), Vector3.Dot(r, _rot_u));
            float da = a1 - _rot_angle0;
            if (da > MathF.PI) da -= MathF.Tau;
            if (da < -MathF.PI) da += MathF.Tau;
            if (_snapOn) da = Snap(da, _snapAngleRad);
            var qDelta = Quaternion.FromAxisAngle(_rot_axis, da);
            _cubeRot = qDelta * _rot_start;

            foreach (var id in _preDragLocal.Keys)
            {
                var e = _scene.GetById(id);
                if (e == null) continue;
                var sw = _preDragWorld[id];
                var rel = sw.P - _drag_startObjPos;
                var newPos = _drag_startObjPos + Vector3.Transform(rel, qDelta);
                var newRot = qDelta * sw.R;
                e.SetWorldTRS(newPos, newRot, sw.S);
            }
        }

        // --- Scale ---
        public void BeginDragScale(uint gizmoId, int px, int py)
        {
            EditingTouched?.Invoke();
            if (_preDragLocal.Count == 0) SnapshotSelectionForDrag();
            if (_preDragLocal.Count == 0) { _dragKind = DragKind.None; _activeId = 0; return; }

            _activeId = gizmoId;
            _scale_start = _cubeScale;

            if (gizmoId == ID_GZ_S_UNI)
            {
                _dragKind = DragKind.ScaleUniform;
                var fwd = Forward();
                var right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, fwd));
                var up = Vector3.Normalize(Vector3.Cross(fwd, right));
                _scale_camU = right; _scale_camV = up;

                var ray = ScreenRay(px, py);
                if (!RayPlane(ray.origin, ray.dir, _cubePos, fwd, out float t)) { _scale_r0 = 1f; return; }
                var hit = ray.origin + ray.dir * t;
                var d = hit - _cubePos;
                _scale_r0 = MathF.Max(0.001f, MathF.Sqrt(MathF.Pow(Vector3.Dot(d, _scale_camU), 2) + MathF.Pow(Vector3.Dot(d, _scale_camV), 2)));
            }
            else
            {
                _dragKind = DragKind.ScaleAxis;
                var axis = gizmoId == ID_GZ_S_X ? Vector3.UnitX : gizmoId == ID_GZ_S_Y ? Vector3.UnitY : Vector3.UnitZ;
                _scale_axis = Vector3.Normalize(Vector3.Transform(axis, GizmoRotation()));
                var ray = ScreenRay(px, py);
                _scale_t0 = ClosestParamOnAxisToRay(_cubePos, _scale_axis, ray.origin, ray.dir);
            }
        }

        public void UpdateDragScale(int px, int py)
        {
            var ray = ScreenRay(px, py);

            if (_dragKind == DragKind.ScaleAxis)
            {
                float t1 = ClosestParamOnAxisToRay(_cubePos, _scale_axis, ray.origin, ray.dir);
                float dt = t1 - _scale_t0;
                var factor = (_snapOn ? Snap(dt, _snapScale) : dt);

                foreach (var id in _preDragLocal.Keys)
                {
                    var e = _scene.GetById(id);
                    if (e == null) continue;
                    var ls = _preDragLocal[id].S;
                    if (_activeId == ID_GZ_S_X) ls.X = MathF.Max(0.05f, ls.X + factor);
                    if (_activeId == ID_GZ_S_Y) ls.Y = MathF.Max(0.05f, ls.Y + factor);
                    if (_activeId == ID_GZ_S_Z) ls.Z = MathF.Max(0.05f, ls.Z + factor);
                    e.Transform.Scale = ls;
                }
            }
            else if (_dragKind == DragKind.ScaleUniform)
            {
                var fwd = Forward();
                if (!RayPlane(ray.origin, ray.dir, _cubePos, fwd, out float t)) return;
                var hit = ray.origin + ray.dir * t;
                var d = hit - _cubePos;
                float r1 = MathF.Sqrt(MathF.Pow(Vector3.Dot(d, _scale_camU), 2) + MathF.Pow(Vector3.Dot(d, _scale_camV), 2));
                float k = (r1 <= 1e-5f) ? 1f : r1 / _scale_r0;
                if (_snapOn) k = MathF.Max(0.05f, Snap(k, _snapScale));

                foreach (var id in _preDragLocal.Keys)
                {
                    var e = _scene.GetById(id);
                    if (e == null) continue;
                    var sw = _preDragWorld[id];
                    var newS = sw.S * k;
                    var rel = sw.P - _cubePos;
                    var newPos = _cubePos + rel * k;
                    e.SetWorldTRS(newPos, sw.R, newS);
                }
            }
        }

        public void EndDrag()
        {
            InspectorPanel.InvalidateEulerCache();
            InspectorPanel.ForceUIRefresh();
            PushCompositeEndDrag("Transform (Gizmo)");
            _dragKind = DragKind.None; _activeId = ID_NONE;
        }

        // ===================== Prims & Maths =====================
        private float PixelsToWorld(float pixels, float depth)
        {
            depth = MathF.Max(0.001f, depth);
            return (pixels / Math.Max(1f, _h)) * 2f * depth * MathF.Tan(_fovY * 0.5f);
        }
        private float DepthAtTarget(Vector3 worldPos)
        {
            var cam = CameraPosition();
            var fwd = Forward();
            return Vector3.Dot(worldPos - cam, fwd);
        }

        private void DrawTranslateGizmo()
        {
            var R = GizmoRotation();
            var model = Matrix4.CreateFromQuaternion(R) * Matrix4.CreateTranslation(_cubePos);
            var mvp = model * _viewGL * _projGL;

            //bool depthWas = GL.IsEnabled(EnableCap.DepthTest);
            //GL.Disable(EnableCap.DepthTest);

            float depth = DepthAtTarget(_cubePos);
            float axisLenPx = 90f, arrowPx = 14f, planePx = 60f, planeOffPx = 16f;
            float len = PixelsToWorld(axisLenPx, depth);
            float sHead = PixelsToWorld(arrowPx, depth);
            float sPlan = PixelsToWorld(planePx, depth);
            float oPlan = PixelsToWorld(planeOffPx, depth);

            GL.Enable(EnableCap.Blend); GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.UseProgram(_gfxShader);
            GL.UniformMatrix4(_locMvp, false, ref mvp);
            GL.Uniform1(_locUseTex, 0); // gizmo = couleur unie

            GL.BindVertexArray(_triVao); GL.BindBuffer(BufferTarget.ArrayBuffer, _triVbo);
            DrawPlaneQuad(new Vector3(oPlan, oPlan, 0), new Vector3(sPlan, 0, 0), new Vector3(0, sPlan, 0),
                (_hoverId == ID_GZ_PLANE_XY || _activeId == ID_GZ_PLANE_XY) ? new Vector4(1.0f, 0.95f, 0.30f, 0.45f) : new Vector4(1.0f, 0.95f, 0.30f, 0.18f),
                ID_GZ_PLANE_XY);
            DrawPlaneQuad(new Vector3(oPlan, 0, oPlan), new Vector3(sPlan, 0, 0), new Vector3(0, 0, sPlan),
                (_hoverId == ID_GZ_PLANE_XZ || _activeId == ID_GZ_PLANE_XZ) ? new Vector4(1.0f, 0.50f, 0.85f, 0.45f) : new Vector4(1.0f, 0.50f, 0.85f, 0.18f),
                ID_GZ_PLANE_XZ);
            DrawPlaneQuad(new Vector3(0, oPlan, oPlan), new Vector3(0, sPlan, 0), new Vector3(0, 0, sPlan),
                (_hoverId == ID_GZ_PLANE_YZ || _activeId == ID_GZ_PLANE_YZ) ? new Vector4(0.50f, 0.95f, 1.00f, 0.45f) : new Vector4(0.50f, 0.95f, 1.00f, 0.18f),
                ID_GZ_PLANE_YZ);
            GL.Disable(EnableCap.Blend);

            GL.BindVertexArray(_lineVao); GL.BindBuffer(BufferTarget.ArrayBuffer, _lineVbo);
            float lwDefault = 3f, lwHover = 7f;

            GL.LineWidth((_hoverId == ID_GZ_X || _activeId == ID_GZ_X) ? lwHover : lwDefault);
            DrawLine(Vector3.Zero, new Vector3(+len, 0, 0),
                (_hoverId == ID_GZ_X || _activeId == ID_GZ_X) ? new Vector4(1f, 0.4f, 0.4f, 1) : new Vector4(1, 0, 0, 1), ID_GZ_X);
            DrawArrowHeadX(len, sHead, (_hoverId == ID_GZ_X || _activeId == ID_GZ_X) ? new Vector4(1f, 0.4f, 0.4f, 1) : new Vector4(1, 0, 0, 1), ID_GZ_X);

            GL.LineWidth((_hoverId == ID_GZ_Y || _activeId == ID_GZ_Y) ? lwHover : lwDefault);
            DrawLine(Vector3.Zero, new Vector3(0, +len, 0),
                (_hoverId == ID_GZ_Y || _activeId == ID_GZ_Y) ? new Vector4(0.6f, 1f, 0.6f, 1) : new Vector4(0, 1, 0, 1), ID_GZ_Y);
            DrawArrowHeadY(len, sHead, (_hoverId == ID_GZ_Y || _activeId == ID_GZ_Y) ? new Vector4(0.6f, 1f, 0.6f, 1) : new Vector4(0, 1, 0, 1), ID_GZ_Y);

            GL.LineWidth((_hoverId == ID_GZ_Z || _activeId == ID_GZ_Z) ? lwHover : lwDefault);
            DrawLine(Vector3.Zero, new Vector3(0, 0, +len),
                (_hoverId == ID_GZ_Z || _activeId == ID_GZ_Z) ? new Vector4(0.5f, 0.7f, 1f, 1) : new Vector4(0, 0.6f, 1, 1), ID_GZ_Z);
            DrawArrowHeadZ(len, sHead, (_hoverId == ID_GZ_Z || _activeId == ID_GZ_Z) ? new Vector4(0.5f, 0.7f, 1f, 1) : new Vector4(0, 0.6f, 1, 1), ID_GZ_Z);

            GL.LineWidth(1f);
            //if (depthWas) GL.Enable(EnableCap.DepthTest);
        }

        private void DrawRotateGizmo()
        {
            var R = GizmoRotation();
            var model = Matrix4.CreateFromQuaternion(R) * Matrix4.CreateTranslation(_cubePos);
            var mvp = model * _viewGL * _projGL;

            //bool depthWas = GL.IsEnabled(EnableCap.DepthTest);
            //GL.Disable(EnableCap.DepthTest);

            float depth = DepthAtTarget(_cubePos);
            float ringPx = 90f, thickPx = 8f; int N = 64;
            float Rw = PixelsToWorld(ringPx, depth);
            float T = PixelsToWorld(thickPx, depth);

            GL.UseProgram(_gfxShader);
            GL.UniformMatrix4(_locMvp, false, ref mvp);
            GL.Uniform1(_locUseTex, 0);

            GL.BindVertexArray(_triVao); GL.BindBuffer(BufferTarget.ArrayBuffer, _triVbo);
            DrawRingAnnulus(Vector3.UnitY, Vector3.UnitZ, Rw, T, N,
                (_hoverId == ID_GZ_ROT_X || _activeId == ID_GZ_ROT_X) ? new Vector4(1f, 0.4f, 0.4f, 1) : new Vector4(1, 0, 0, 1), ID_GZ_ROT_X);
            DrawRingAnnulus(Vector3.UnitZ, Vector3.UnitX, Rw, T, N,
                (_hoverId == ID_GZ_ROT_Y || _activeId == ID_GZ_ROT_Y) ? new Vector4(0.6f, 1f, 0.6f, 1) : new Vector4(0, 1, 0, 1), ID_GZ_ROT_Y);
            DrawRingAnnulus(Vector3.UnitX, Vector3.UnitY, Rw, T, N,
                (_hoverId == ID_GZ_ROT_Z || _activeId == ID_GZ_ROT_Z) ? new Vector4(0.5f, 0.7f, 1f, 1) : new Vector4(0, 0.6f, 1, 1), ID_GZ_ROT_Z);

            //if (depthWas) GL.Enable(EnableCap.DepthTest);
        }

        private void DrawScaleGizmo()
        {
            var R = GizmoRotation();
            var model = Matrix4.CreateFromQuaternion(R) * Matrix4.CreateTranslation(_cubePos);
            var mvp = model * _viewGL * _projGL;

            //bool depthWas = GL.IsEnabled(EnableCap.DepthTest);
            //GL.Disable(EnableCap.DepthTest);

            float depth = DepthAtTarget(_cubePos);
            float axisLenPx = 90f, headPx = 12f, uniPx = 18f;
            float len = PixelsToWorld(axisLenPx, depth);
            float sHead = PixelsToWorld(headPx, depth);
            float u = PixelsToWorld(uniPx, depth);

            GL.BindVertexArray(_lineVao); GL.BindBuffer(BufferTarget.ArrayBuffer, _lineVbo);
            float lwDefault = 3f, lwHover = 7f;

            GL.LineWidth((_hoverId == ID_GZ_S_X || _activeId == ID_GZ_S_X) ? lwHover : lwDefault);
            DrawLine(Vector3.Zero, new Vector3(+len, 0, 0),
                (_hoverId == ID_GZ_S_X || _activeId == ID_GZ_S_X) ? new Vector4(1f, 0.6f, 0.6f, 1) : new Vector4(1, 0.2f, 0.2f, 1), ID_GZ_S_X);
            DrawArrowHeadX(len, sHead, (_hoverId == ID_GZ_S_X || _activeId == ID_GZ_S_X) ? new Vector4(1f, 0.6f, 0.6f, 1) : new Vector4(1, 0.2f, 0.2f, 1), ID_GZ_S_X);

            GL.LineWidth((_hoverId == ID_GZ_S_Y || _activeId == ID_GZ_S_Y) ? lwHover : lwDefault);
            DrawLine(Vector3.Zero, new Vector3(0, +len, 0),
                (_hoverId == ID_GZ_S_Y || _activeId == ID_GZ_S_Y) ? new Vector4(0.7f, 1f, 0.7f, 1) : new Vector4(0.2f, 1, 0.2f, 1), ID_GZ_S_Y);
            DrawArrowHeadY(len, sHead, (_hoverId == ID_GZ_S_Y || _activeId == ID_GZ_S_Y) ? new Vector4(0.7f, 1f, 0.7f, 1) : new Vector4(0.2f, 1, 0.2f, 1), ID_GZ_S_Y);

            GL.LineWidth((_hoverId == ID_GZ_S_Z || _activeId == ID_GZ_S_Z) ? lwHover : lwDefault);
            DrawLine(Vector3.Zero, new Vector3(0, 0, +len),
                (_hoverId == ID_GZ_S_Z || _activeId == ID_GZ_S_Z) ? new Vector4(0.7f, 0.85f, 1f, 1) : new Vector4(0.2f, 0.6f, 1, 1), ID_GZ_S_Z);
            DrawArrowHeadZ(len, sHead, (_hoverId == ID_GZ_S_Z || _activeId == ID_GZ_S_Z) ? new Vector4(0.7f, 0.85f, 1f, 1) : new Vector4(0.2f, 0.6f, 1, 1), ID_GZ_S_Z);

            // Uniform handle (square facing camera)
            var fwd = Forward();
            var right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, fwd));
            var up = Vector3.Normalize(Vector3.Cross(fwd, right));
            GL.UseProgram(_gfxShader);
            GL.UniformMatrix4(_locMvp, false, ref mvp);
            GL.Uniform1(_locUseTex, 0);

            GL.BindVertexArray(_triVao); GL.BindBuffer(BufferTarget.ArrayBuffer, _triVbo);
            DrawPlaneQuad(new Vector3(0, 0, 0) - right * u * 0.5f - up * u * 0.5f, right * u, up * u,
                (_hoverId == ID_GZ_S_UNI || _activeId == ID_GZ_S_UNI) ? new Vector4(1, 1, 1, 1) : new Vector4(0.95f, 0.95f, 0.95f, 1),
                ID_GZ_S_UNI);

            GL.LineWidth(1f);
            //if (depthWas) GL.Enable(EnableCap.DepthTest);
        }

        private void DrawLine(in Vector3 a, in Vector3 b, in Vector4 color, uint id)
        {
            Span<float> data = stackalloc float[6] { a.X, a.Y, a.Z, b.X, b.Y, b.Z };
            unsafe { fixed (float* p = data) { GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, sizeof(float) * 6, (IntPtr)p); } }
            GL.UseProgram(_gfxShader);
            var mvp = Matrix4.CreateFromQuaternion(GizmoRotation()) * Matrix4.CreateTranslation(_cubePos) * _viewGL * _projGL;
            GL.UniformMatrix4(_locMvp, false, ref mvp);
            GL.Uniform1(_locUseTex, 0);
            GL.Uniform4(_locAlbColor, color.X, color.Y, color.Z, color.W);
            GL.Uniform1(_locId, (int)Engine.Scene.EntityIdRange.GizmoReservedId); // ✅ ID réservé au lieu de l'ID spécifique
            GL.DrawArrays(PrimitiveType.Lines, 0, 2);
        }
        private void DrawArrowHeadX(float len, float s, Vector4 color, uint id)
        {
            var T = new Vector3(len, 0, 0);
            var B1 = new Vector3(len - s, 0, s);
            var B2 = new Vector3(len - s, s, 0);
            var B3 = new Vector3(len - s, 0, -s);
            var B4 = new Vector3(len - s, -s, 0);
            DrawPyramid(T, B1, B2, B3, B4, color, id);
        }
        private void DrawArrowHeadY(float len, float s, Vector4 color, uint id)
        {
            var T = new Vector3(0, len, 0);
            var B1 = new Vector3(0, len - s, s);
            var B2 = new Vector3(s, len - s, 0);
            var B3 = new Vector3(0, len - s, -s);
            var B4 = new Vector3(-s, len - s, 0);
            DrawPyramid(T, B1, B2, B3, B4, color, id);
        }
        private void DrawArrowHeadZ(float len, float s, Vector4 color, uint id)
        {
            var T = new Vector3(0, 0, len);
            var B1 = new Vector3(0, s, len - s);
            var B2 = new Vector3(s, 0, len - s);
            var B3 = new Vector3(0, -s, len - s);
            var B4 = new Vector3(-s, 0, len - s);
            DrawPyramid(T, B1, B2, B3, B4, color, id);
        }
        private void DrawPyramid(in Vector3 T, in Vector3 B1, in Vector3 B2, in Vector3 B3, in Vector3 B4, in Vector4 color, uint id)
        {
            Span<float> data = stackalloc float[4 * 9] {
        T.X,T.Y,T.Z,  B1.X,B1.Y,B1.Z,  B2.X,B2.Y,B2.Z,
        T.X,T.Y,T.Z,  B2.X,B2.Y,B2.Z,  B3.X,B3.Y,B3.Z,
        T.X,T.Y,T.Z,  B3.X,B3.Y,B3.Z,  B4.X,B4.Y,B4.Z,
        T.X,T.Y,T.Z,  B4.X,B4.Y,B4.Z,  B1.X,B1.Y,B1.Z,
    };
            GL.BindVertexArray(_triVao); GL.BindBuffer(BufferTarget.ArrayBuffer, _triVbo);
            unsafe { fixed (float* p = data) { GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, sizeof(float) * data.Length, (IntPtr)p); } }
            GL.UseProgram(_gfxShader);
            var mvp = Matrix4.CreateFromQuaternion(GizmoRotation()) * Matrix4.CreateTranslation(_cubePos) * _viewGL * _projGL;
            GL.UniformMatrix4(_locMvp, false, ref mvp);
            GL.Uniform1(_locUseTex, 0);
            GL.Uniform4(_locAlbColor, color.X, color.Y, color.Z, color.W);
            GL.Uniform1(_locId, (int)Engine.Scene.EntityIdRange.GizmoReservedId); // ✅ ID réservé
            GL.DrawArrays(PrimitiveType.Triangles, 0, 12);
        }

        private void DrawPlaneQuad(in Vector3 p0, in Vector3 u, in Vector3 v, in Vector4 color, uint id)
        {
            var p1 = p0 + u; var p2 = p0 + u + v; var p3 = p0 + v;
            Span<float> data = stackalloc float[18] {
        p0.X,p0.Y,p0.Z,  p1.X,p1.Y,p1.Z,  p2.X,p2.Y,p2.Z,
        p0.X,p0.Y,p0.Z,  p2.X,p2.Y,p2.Z,  p3.X,p3.Y,p3.Z,
    };
            GL.BindVertexArray(_triVao); GL.BindBuffer(BufferTarget.ArrayBuffer, _triVbo);
            unsafe { fixed (float* p = data) { GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, sizeof(float) * data.Length, (IntPtr)p); } }
            GL.UseProgram(_gfxShader);
            var mvp = Matrix4.CreateFromQuaternion(GizmoRotation()) * Matrix4.CreateTranslation(_cubePos) * _viewGL * _projGL;
            GL.UniformMatrix4(_locMvp, false, ref mvp);
            GL.Uniform1(_locUseTex, 0);
            GL.Uniform4(_locAlbColor, color.X, color.Y, color.Z, color.W);
            GL.Uniform1(_locId, (int)Engine.Scene.EntityIdRange.GizmoReservedId); // ✅ ID réservé
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
        }

        private void DrawRingAnnulus(Vector3 axisU, Vector3 axisV, float radius, float thickness, int segments, Vector4 color, uint id)
        {
            // Construction de la géométrie...
            Span<float> data = stackalloc float[segments * 2 * 3 * 3];
            int idx = 0;

            float r0 = radius - thickness * 0.5f;
            float r1 = radius + thickness * 0.5f;

            for (int i = 0; i < segments; i++)
            {
                float a0 = (i / (float)segments) * MathF.Tau;
                float a1 = ((i + 1) / (float)segments) * MathF.Tau;

                var p0 = axisU * (MathF.Cos(a0) * r0) + axisV * (MathF.Sin(a0) * r0);
                var p1 = axisU * (MathF.Cos(a1) * r0) + axisV * (MathF.Sin(a1) * r0);
                var q0 = axisU * (MathF.Cos(a0) * r1) + axisV * (MathF.Sin(a0) * r1);
                var q1 = axisU * (MathF.Cos(a1) * r1) + axisV * (MathF.Sin(a1) * r1);

                data[idx++] = p0.X; data[idx++] = p0.Y; data[idx++] = p0.Z;
                data[idx++] = p1.X; data[idx++] = p1.Y; data[idx++] = p1.Z;
                data[idx++] = q1.X; data[idx++] = q1.Y; data[idx++] = q1.Z;

                data[idx++] = p0.X; data[idx++] = p0.Y; data[idx++] = p0.Z;
                data[idx++] = q1.X; data[idx++] = q1.Y; data[idx++] = q1.Z;
                data[idx++] = q0.X; data[idx++] = q0.Y; data[idx++] = q0.Z;
            }

            GL.BindVertexArray(_triVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _triVbo);
            unsafe { fixed (float* p = data) { GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, sizeof(float) * data.Length, (IntPtr)p); } }

            GL.UseProgram(_gfxShader);
            var mvp = Matrix4.CreateFromQuaternion(GizmoRotation()) * Matrix4.CreateTranslation(_cubePos) * _viewGL * _projGL;
            GL.UniformMatrix4(_locMvp, false, ref mvp);
            GL.Uniform1(_locUseTex, 0);
            GL.Uniform4(_locAlbColor, color.X, color.Y, color.Z, color.W);
            GL.Uniform1(_locId, (int)Engine.Scene.EntityIdRange.GizmoReservedId); // ✅ ID réservé

            GL.DrawArrays(PrimitiveType.Triangles, 0, segments * 6);
        }

        // ===================== Maths =====================
        private Vector3 Forward()
            => new Vector3(MathF.Cos(_pitch) * MathF.Sin(_yaw), MathF.Sin(_pitch), MathF.Cos(_pitch) * MathF.Cos(_yaw));
        private Vector3 CameraPosition()
        {
            // Si on utilise des matrices personnalisées (mode Play), extraire la position depuis la matrice de vue
            if (_useCustomMatrices)
            {
                // Extraire la position de la caméra depuis la matrice de vue
                // La matrice de vue est l'inverse de la matrice de transformation de la caméra
                var invView = _viewGL.Inverted();
                return new Vector3(invView.M41, invView.M42, invView.M43);
            }

            // Mode éditeur : utiliser la caméra orbitale de l'éditeur
            return _target - Forward() * _distance;
        }

        private (Vector3 origin, Vector3 dir) ScreenRay(int px, int py)
        {
            float nx = (2f * px) / _w - 1f;
            float ny = (2f * py) / _h - 1f;
            var inv = (_viewGL * _projGL).Inverted();
            var pNear = UnProject(nx, ny, -1f, inv);
            var pFar = UnProject(nx, ny, 1f, inv);
            var dir = Vector3.Normalize(pFar - pNear);
            return (pNear, dir);
        }
        private static Vector3 UnProject(float nx, float ny, float nz, Matrix4 invVP)
        {
            var v4 = new Vector4(nx, ny, nz, 1f);
            var w = Vector4.TransformRow(v4, invVP);
            if (w.W != 0f) w /= w.W;
            return new Vector3(w.X, w.Y, w.Z);
        }
        private static float ClosestParamOnAxisToRay(in Vector3 p0, in Vector3 u, in Vector3 r0, in Vector3 v)
        {
            var w0 = p0 - r0;
            float a = Vector3.Dot(u, u);
            float b = Vector3.Dot(u, v);
            float c = Vector3.Dot(v, v);
            float d = Vector3.Dot(u, w0);
            float e = Vector3.Dot(v, w0);
            float denom = a * c - b * b;
            if (MathF.Abs(denom) < 1e-6f) return 0f;
            return (b * e - c * d) / denom;
        }
        private static bool RayPlane(in Vector3 r0, in Vector3 rd, in Vector3 p0, in Vector3 n, out float t)
        {
            float denom = Vector3.Dot(n, rd);
            if (MathF.Abs(denom) < 1e-6f) { t = 0; return false; }
            t = Vector3.Dot(p0 - r0, n) / denom;
            return t >= 0;
        }
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

        /// <summary>
        /// Right-handed LookAt for OpenGL (preserves winding order).
        /// </summary>
        private static Matrix4 LookAtRH(in Vector3 eye, in Vector3 target, in Vector3 up)
        {
            var f = Vector3.Normalize(target - eye);
            var s = Vector3.Normalize(Vector3.Cross(f, up));  // RH
            var u = Vector3.Cross(s, f);
            return new Matrix4(
                new Vector4(s.X, u.X, -f.X, 0f),  // -f for RH
                new Vector4(s.Y, u.Y, -f.Y, 0f),
                new Vector4(s.Z, u.Z, -f.Z, 0f),
                new Vector4(-Vector3.Dot(s, eye), -Vector3.Dot(u, eye), Vector3.Dot(f, eye), 1f)
            );
        }

        public void ClearMaterialCache()
        {
            _materialCache.Clear();
        }

        /// <summary>
        /// Apply live updates coming from the inspector to an existing cached MaterialRuntime.
        /// This updates only dynamic uniform-like fields (albedo color, metallic, smoothness,
        /// tiling/offset, normal strength, transparency) and optionally replaces texture handles
        /// if texture GUIDs have changed. This allows ultra-smooth interactive editing without
        /// writing to disk or rebuilding the entire runtime.
        /// </summary>
        public void ApplyLiveMaterialUpdate(Guid materialGuid, Engine.Assets.MaterialAsset mat)
        {
            try
            {
                if (!_materialCache.TryGetValue(materialGuid, out var mr)) return;

                // Update scalar / color fields
                if (mat.AlbedoColor != null && mat.AlbedoColor.Length >= 4)
                    mr.AlbedoColor = new float[] { mat.AlbedoColor[0], mat.AlbedoColor[1], mat.AlbedoColor[2], mat.AlbedoColor[3] };

                mr.Metallic = mat.Metallic;
                mr.Smoothness = 1.0f - mat.Roughness;
                mr.OcclusionStrength = mat.OcclusionStrength;
                if (mat.EmissiveColor != null && mat.EmissiveColor.Length >= 3)
                    mr.EmissiveColor = new float[] { mat.EmissiveColor[0], mat.EmissiveColor[1], mat.EmissiveColor[2] };
                mr.HeightScale = mat.HeightScale;
                mr.TextureTiling = mat.TextureTiling ?? new float[] { 1f, 1f };
                mr.TextureOffset = mat.TextureOffset ?? new float[] { 0f, 0f };
                mr.NormalStrength = mat.NormalStrength;
                // DO NOT overwrite TransparencyMode here - it should only change when user explicitly
                // changes "Render Mode" dropdown, not when editing other properties like Smoothness.
                // mr.TransparencyMode = mat.TransparencyMode;
                
                // Update stylization parameters
                mr.Saturation = mat.Saturation;
                mr.Brightness = mat.Brightness;
                mr.Contrast = mat.Contrast;
                mr.Hue = mat.Hue;
                mr.Emission = mat.Emission;

                // If texture GUIDs changed, attempt to update texture handles.
                var resolver = new Func<Guid, string?>(g => Engine.Assets.AssetDatabase.TryGet(g, out var r) ? r.Path : null);
                try
                {
                    // Base textures
                    if (mat.AlbedoTexture.HasValue)
                        mr.AlbedoTex = Engine.Rendering.TextureCache.GetOrLoad(mat.AlbedoTexture.Value, resolver);
                    if (mat.NormalTexture.HasValue)
                        mr.NormalTex = Engine.Rendering.TextureCache.GetOrLoad(mat.NormalTexture.Value, resolver);
                    
                    // PBR textures
                    if (mat.MetallicTexture.HasValue)
                        mr.MetallicTex = Engine.Rendering.TextureCache.GetOrLoad(mat.MetallicTexture.Value, resolver);
                    if (mat.RoughnessTexture.HasValue)
                        mr.RoughnessTex = Engine.Rendering.TextureCache.GetOrLoad(mat.RoughnessTexture.Value, resolver);
                    if (mat.MetallicRoughnessTexture.HasValue)
                        mr.MetallicRoughnessTex = Engine.Rendering.TextureCache.GetOrLoad(mat.MetallicRoughnessTexture.Value, resolver);
                    if (mat.OcclusionTexture.HasValue)
                        mr.OcclusionTex = Engine.Rendering.TextureCache.GetOrLoad(mat.OcclusionTexture.Value, resolver);
                    if (mat.EmissiveTexture.HasValue)
                        mr.EmissiveTex = Engine.Rendering.TextureCache.GetOrLoad(mat.EmissiveTexture.Value, resolver);
                    if (mat.HeightTexture.HasValue)
                        mr.HeightTex = Engine.Rendering.TextureCache.GetOrLoad(mat.HeightTexture.Value, resolver);
                    
                    // Detail textures
                    if (mat.DetailMaskTexture.HasValue)
                        mr.DetailMaskTex = Engine.Rendering.TextureCache.GetOrLoad(mat.DetailMaskTexture.Value, resolver);
                    if (mat.DetailAlbedoTexture.HasValue)
                        mr.DetailAlbedoTex = Engine.Rendering.TextureCache.GetOrLoad(mat.DetailAlbedoTexture.Value, resolver);
                    if (mat.DetailNormalTexture.HasValue)
                        mr.DetailNormalTex = Engine.Rendering.TextureCache.GetOrLoad(mat.DetailNormalTexture.Value, resolver);
                }
                catch { }

                // Ensure next draw will rebind uniforms for this material
                _forceMaterialRebind = true;
            }
            catch (Exception ex)
            {
                try { Console.WriteLine($"[ViewportRenderer] ApplyLiveMaterialUpdate failed for {materialGuid}: {ex.Message}"); } catch { }
            }
        }

        /// <summary>
        /// Update ONLY the TransparencyMode for a cached material. Use this when user explicitly
        /// changes Render Mode dropdown to avoid unwanted side effects during other property edits.
        /// </summary>
        public void UpdateMaterialTransparency(Guid materialGuid, int transparencyMode)
        {
            try
            {
                if (!_materialCache.TryGetValue(materialGuid, out var mr)) return;
                mr.TransparencyMode = transparencyMode;
                _forceMaterialRebind = true;
            }
            catch (Exception ex)
            {
                try { Console.WriteLine($"[ViewportRenderer] UpdateMaterialTransparency failed for {materialGuid}: {ex.Message}"); } catch { }
            }
        }

        public void OnMaterialSaved(System.Guid materialGuid)
        {
            // Replace or remove the specific material from cache to force reload.
            // Previously we only removed the entry; replace it with a freshly-created
            // MaterialRuntime when possible so any subsequent bind in the same frame
            // (or very shortly after) will pick up updated uniforms (albedo color,
            // metallic, smoothness, tiling, etc.) without requiring an explicit
            // manual refresh by the user.
            try { Console.WriteLine($"[ViewportRenderer] Material saved: {materialGuid}, refreshing cache entry"); } catch { }
            try
            {
                // Remove cache entry first to ensure a clean reload
                if (_materialCache.Remove(materialGuid))
                {
                    try { Console.WriteLine($"[ViewportRenderer] Material {materialGuid} removed from cache (will reload)"); } catch { }
                }
                // Also set a force-rebind flag so any already-bound shader state
                // will be refreshed on the next draw pass (matches Terrain behaviour)
                _forceMaterialRebind = true;
            }
            catch
            {
                try { _materialCache.Remove(materialGuid); } catch { }
                _forceMaterialRebind = true;
            }
        }

        public void Dispose()
        {
            // Scene.EntityTransformChanged -= OnEntityTransformChanged;

            // Unsubscribe from material changes
            if (_subscribedToMaterialChanges)
            {
                Engine.Assets.AssetDatabase.MaterialSaved -= OnMaterialSaved;
                _subscribedToMaterialChanges = false;
            }

            // Clear material cache
            _materialCache.Clear();

            // Dispose terrain renderer
            // Terrain renderer removed - using direct terrain rendering

            // Dispose UBOs
            if (_globalUBO != 0)
            {
                GL.DeleteBuffer(_globalUBO);
                _globalUBO = 0;
            }
            
            foreach (var ubo in _materialUniformBuffers.Values)
            {
                GL.DeleteBuffer(ubo);
            }

            _materialUniformBuffers.Clear();
            _grid?.Dispose();
            _skyboxRenderer?.Dispose();
            _terrainRenderer?.Dispose();

            // === NEW: Dispose Modern Shadow System ===
            _shadowManager?.Dispose();
            _shadowDepthShader?.Dispose();
            if (_cubeVao != 0) GL.DeleteVertexArray(_cubeVao);
            if (_cubeVbo != 0) GL.DeleteBuffer(_cubeVbo);
            if (_cubeEbo != 0) GL.DeleteBuffer(_cubeEbo);
            if (_legacyCubeVao != 0) GL.DeleteVertexArray(_legacyCubeVao);
            if (_legacyCubeVbo != 0) GL.DeleteBuffer(_legacyCubeVbo);
            if (_lightIconVao != 0) GL.DeleteVertexArray(_lightIconVao);
            if (_lightIconVbo != 0) GL.DeleteBuffer(_lightIconVbo);
            if (_lightIconEbo != 0) GL.DeleteBuffer(_lightIconEbo);
            if (_gfxShader != 0) GL.DeleteProgram(_gfxShader);
            if (_lineVao != 0) GL.DeleteVertexArray(_lineVao);
            if (_lineVbo != 0) GL.DeleteBuffer(_lineVbo);
            if (_triVao != 0) GL.DeleteVertexArray(_triVao);
            if (_triVbo != 0) GL.DeleteBuffer(_triVbo);
            if (_colorTex != 0) GL.DeleteTexture(_colorTex);
            if (_idTex != 0) GL.DeleteTexture(_idTex);
            if (_depthTex != 0) GL.DeleteTexture(_depthTex);
            if (_fbo != 0) GL.DeleteFramebuffer(_fbo);
            if (_postTex != 0) GL.DeleteTexture(_postTex);
            if (_postFbo != 0) GL.DeleteFramebuffer(_postFbo);
            if (_postTex2 != 0) GL.DeleteTexture(_postTex2);
            if (_postFbo2 != 0) GL.DeleteFramebuffer(_postFbo2);
            if (_velocityTex != 0) GL.DeleteTexture(_velocityTex);
            if (_velocityFbo != 0) GL.DeleteFramebuffer(_velocityFbo);
            _velocityShader?.Dispose();
            _pbrShader?.Dispose();

            // Decrement instance count (thread-safe)
            Interlocked.Decrement(ref _instanceCount);
            try { Console.WriteLine($"[ViewportRenderer] Dispose: instances={_instanceCount}, this={this.GetHashCode()}"); } catch { }
        }

        // ======= POST-PROCESS EFFECTS =======
        private void ApplyPostProcessEffects()
        {
            if (_scene == null) return;

            try
            {
                // Console.WriteLine($"[ViewportRenderer] ApplyPostProcessEffects called - colorTex: {_colorTex}, fbo: {_fbo}, size: {_w}x{_h}");

                // Vérifier que nous avons des textures valides
                if (_colorTex <= 0 || _w <= 0 || _h <= 0)
                {
                    // Console.WriteLine($"[ViewportRenderer] Invalid render state, skipping post-process");
                    return;
                }

                // Mark post texture as healthy if FBOs exist
                if (_postFbo != 0 && _fbo != 0)
                {
                    _postTexHealthy = true;
                }

                // Post-process effects with ping-pong between two buffers to avoid read/write conflicts
                // Get all active post-process effects from the scene
                var allEffects = new List<(Engine.Components.PostProcessEffect effect, Engine.Components.IPostProcessRenderer renderer)>();
                
                foreach (var entity in _scene.Entities)
                {
                    if (!entity.Active) continue;
                    var globalEffects = entity.GetComponent<Engine.Components.GlobalEffects>();
                    if (globalEffects == null || !globalEffects.Enabled) continue;
                    
                    foreach (var effect in globalEffects.Effects.Where(e => e?.Enabled == true).OrderBy(e => e?.Priority ?? 0))
                    {
                        if (effect == null) continue;
                        // Get renderer for this effect type
                        if (Engine.Rendering.PostProcessManager.TryGetRenderer(effect.GetType(), out var renderer))
                        {
                            allEffects.Add((effect, renderer));
                        }
                    }
                }

                // When no effects are active, copy _colorTex to _postFbo using BlitFramebuffer
                // (faster than using a shader for a simple copy)
                if (allEffects.Count == 0)
                {
                    if (_postFbo != 0 && _colorTex != 0)
                    {
                        try
                        {
                            // Use glCopyImageSubData for a fast, artifact-free copy
                            // This is faster than BlitFramebuffer and avoids edge artifacts
                            GL.CopyImageSubData(
                                _colorTex, ImageTarget.Texture2D, 0, 0, 0, 0,
                                _postTex, ImageTarget.Texture2D, 0, 0, 0, 0,
                                _w, _h, 1
                            );
                            _postTexHealthy = true;
                        }
                        catch (Exception)
                        {
                            // Fallback to blit if CopyImageSubData is not supported
                            try
                            {
                                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _fbo);
                                GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
                                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, _postFbo);
                                GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
                                GL.BlitFramebuffer(0, 0, _w, _h, 0, 0, _w, _h, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
                                _postTexHealthy = true;
                            }
                            catch (Exception)
                            {
                                _postTexHealthy = false;
                            }
                        }
                    }
                }
                // Apply effects with ping-pong: read from source, write to target, then swap
                else if (allEffects.Count > 0)
                {
                    // Start by reading from _colorTex (the rendered scene)
                    int srcTex = _colorTex;

                    // Choose starting buffer based on effect count to ensure final result lands in _postFbo
                    // With odd number of effects: start with _postFbo (1st->_postFbo, 2nd->_postFbo2, 3rd->_postFbo)
                    // With even number of effects: start with _postFbo2 (1st->_postFbo2, 2nd->_postFbo)
                    int dstFbo = (allEffects.Count % 2 == 1) ? _postFbo : _postFbo2;
                    int dstTex = (allEffects.Count % 2 == 1) ? _postTex : _postTex2;
                    
                    // Log only on first frame or when effect count changes
                    
                    for (int i = 0; i < allEffects.Count; i++)
                    {
                        var (effect, renderer) = allEffects[i];

                        // Create context for this effect
                        var context = new Engine.Components.PostProcessContext(
                            (uint)srcTex,
                            (uint)dstFbo,
                            _w, _h,
                            0.016f,
                            _scene
                        );
                        
                        // Add depth texture and matrices for effects that need them (SSAO, GTAO, TAA, etc.)
                        context.DepthTexture = (uint)_depthTex;
                        context.ProjectionMatrix = _projGL;
                        context.ViewMatrix = _viewGL;
                        
                        // Bind target and render effect
                        try
                        {
                            GL.BindFramebuffer(FramebufferTarget.Framebuffer, dstFbo);
                            GL.Viewport(0, 0, _w, _h);

                            // Clear the target to ensure no artifacts from previous content
                            GL.ClearColor(0, 0, 0, 1);
                            GL.Clear(ClearBufferMask.ColorBufferBit);

                            // Ensure clean state for post-processing
                            GL.Disable(EnableCap.DepthTest);
                            GL.Disable(EnableCap.Blend);
                            GL.Disable(EnableCap.CullFace);
                            GL.Disable(EnableCap.ScissorTest);

                            renderer.Render(effect, context);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[ViewportRenderer] Post-effect error: {ex.Message}");
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

                    // Final result is now guaranteed to be in _postFbo/_postTex (no need for final blit)
                }

                // Console.WriteLine($"[ViewportRenderer] Created PostProcessContext, calling PostProcessManager.ApplyEffects");

                // NOTE: We no longer call PostProcessManager.ApplyEffects() here as we handle ping-pong manually above

                // Console.WriteLine($"[ViewportRenderer] Post-process effects applied successfully");

                // === SELECTION OUTLINE POST-PROCESS ===
                // Render outline AFTER other post-effects so it appears on top and isn't overwritten
                // IMPORTANT: We check if selection outline should be rendered
                bool shouldRenderOutline = _postFbo != 0 && _postTex != 0 && _postFbo2 != 0 && 
                                          _outlineRenderer != null && Scene != null &&
                                          Editor.State.EditorSettings.Outline.Enabled &&
                                          Editor.State.Selection.ActiveEntityId != 0;
                
                if (shouldRenderOutline)
                {
                    // Render outline into _postFbo2 (reading from _postTex, writing to _postTex2)
                    RenderSelectionOutline();
                    
                    // Copy result back from _postFbo2 to _postFbo (final output)
                    // This ensures the outline is in the correct buffer for display
                    if (_postFbo2 != 0 && _postTex2 != 0)
                    {
                        try
                        {
                            GL.CopyImageSubData(
                                _postTex2, ImageTarget.Texture2D, 0, 0, 0, 0,
                                _postTex, ImageTarget.Texture2D, 0, 0, 0, 0,
                                _w, _h, 1
                            );
                        }
                        catch
                        {
                            // Fallback to blit
                            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _postFbo2);
                            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
                            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, _postFbo);
                            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
                            GL.BlitFramebuffer(0, 0, _w, _h, 0, 0, _w, _h, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
                        }
                    }
                }
                // Note: When no outline is needed, _postTex already contains the final image from post-processing

                // Verify that the post texture actually contains a valid image; some drivers may
                // leave the texture uninitialized or the FBO incomplete even when creation succeeded.
                if (_postTex != 0 && !_postTexHealthy)
                {
                    try
                    {
                        GL.BindTexture(TextureTarget.Texture2D, _postTex);
                        GL.GetTexLevelParameter(TextureTarget.Texture2D, 0, GetTextureParameter.TextureWidth, out int tw);
                        GL.GetTexLevelParameter(TextureTarget.Texture2D, 0, GetTextureParameter.TextureHeight, out int th);
                        GL.BindTexture(TextureTarget.Texture2D, 0);

                        if (tw == _w && th == _h && tw > 0 && th > 0)
                        {
                            _postTexHealthy = true;
                        }
                        else
                        {
                            // Mark unhealthy — do NOT delete GL objects mid-frame. They will be recreated on next Resize.
                            _postTexHealthy = false;
                        }
                    }
                    catch (Exception)
                    {
                        try { if (_postTex != 0) { GL.DeleteTexture(_postTex); } } catch { }
                        try { if (_postFbo != 0) { GL.DeleteFramebuffer(_postFbo); } } catch { }
                        _postTex = 0; _postFbo = 0; _postTexHealthy = false;
                    }
                    finally
                    {
                        // Ensure default texture binding state
                        GL.BindTexture(TextureTarget.Texture2D, 0);
                    }
                }

                // ========== TAA (Temporal Anti-Aliasing) - FINAL STEP ==========
                // PERFORMANCE-OPTIMIZED: Apply TAA after all other post-processing
                if (_taaRenderer != null && TAASettings.Enabled && _postTexHealthy && _postTex != 0)
                {
                    try
                    {
                        // Calculate view-projection matrices (minimal CPU overhead)
                        // NOTE: Use the JITTERED projection matrices for reprojection so the
                        // reprojection math matches the pixels in the input (which were
                        // rendered with jitter). Passing the same matrices to the TAA
                        // shader avoids subtle UV offsets that create rings / ghosting.
                        Matrix4 viewProjJittered = _viewGL * _projGL; // _projGL includes jitter
                        Matrix4 invViewProjJittered = viewProjJittered.Inverted();

                        // First: render a camera-motion velocity texture by reconstructing world position
                        // from depth and reprojection (this is camera-only velocity. Per-object velocities
                        // can be added later by rendering object motion into a velocity buffer).
                        if (_velocityFbo != 0 && _velocityTex != 0 && _depthTex != 0)
                        {
                            try
                            {
                                if (_velocityShader == null)
                                {
                                    _velocityShader = Engine.Rendering.ShaderProgram.FromFiles(
                                        "Engine/Rendering/Shaders/PostProcess/Blit.vert",
                                        "Engine/Rendering/Shaders/PostProcess/Velocity.frag"
                                    );
                                }

                                // Render fullscreen velocity into _velocityFbo
                                GL.BindFramebuffer(FramebufferTarget.Framebuffer, _velocityFbo);
                                GL.Viewport(0, 0, _w, _h);
                                GL.Disable(EnableCap.DepthTest);
                                GL.Disable(EnableCap.Blend);

                                _velocityShader!.Use();
                                // Bind depth texture (from main FBO)
                                GL.ActiveTexture(TextureUnit.Texture0);
                                GL.BindTexture(TextureTarget.Texture2D, _depthTex);
                                int loc = GL.GetUniformLocation(_velocityShader.Handle, "u_Depth"); if (loc >= 0) GL.Uniform1(loc, 0);

                                var locInv = GL.GetUniformLocation(_velocityShader.Handle, "u_InvViewProj"); if (locInv >= 0) GL.UniformMatrix4(locInv, false, ref invViewProjJittered);
                                var locPrev = GL.GetUniformLocation(_velocityShader.Handle, "u_PrevViewProj"); if (locPrev >= 0) GL.UniformMatrix4(locPrev, false, ref _prevViewProj);

                                // Draw fullscreen quad
                                GL.BindVertexArray(_quadVao);
                                GL.BindBuffer(BufferTarget.ElementArrayBuffer, _quadEbo);
                                GL.DrawElements(PrimitiveType.Triangles, _quadIndexCount, DrawElementsType.UnsignedInt, 0);

                                // Restore
                                GL.BindVertexArray(0);
                                GL.BindFramebuffer(FramebufferTarget.Framebuffer, GetTargetFBO());
                                GL.Viewport(0, 0, _w, _h);
                                GL.Enable(EnableCap.DepthTest);
                            }
                            catch (Exception exVel)
                            {
                                Console.WriteLine($"[ViewportRenderer] Velocity pass error: {exVel.Message}");
                                // Continue without velocity
                            }
                        }

                        // Apply TAA: reads from _postTex, writes to TAA history buffer
                        // Returns history texture ID
                        int taaResultTex = _taaRenderer.RenderTAA(
                            _postTex,               // Current frame (already post-processed)
                            _depthTex,              // Depth buffer for reprojection
                            invViewProjJittered,    // Inverse view-proj (JITTERED) for world position reconstruction
                            viewProjJittered,       // Current view-proj (JITTERED) for storing history
                            _velocityTex            // Optional velocity texture (camera motion)
                        );

                        // PERFORMANCE: Ultra-fast blit TAA result back to _postFbo
                        // We need to copy the TAA result (history texture) into _postFbo
                        _taaRenderer.BlitToTarget(_postFbo, _w, _h);

                        // Post-process texture (_postTex) now contains TAA'd result

                        // Store current viewProj for next-frame velocity reprojection
                        _prevViewProj = viewProjJittered;
                    }
                    catch (Exception exTaa)
                    {
                        Console.WriteLine($"[ViewportRenderer] TAA error: {exTaa.Message}");
                        Console.WriteLine($"[ViewportRenderer] TAA stack: {exTaa.StackTrace}");
                        // Continue without TAA
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ViewportRenderer] Error applying post-process effects: {ex.Message}");
                // Continue without post-processing to avoid breaking the render
            }
        }

        // ======= (optionnel) PBR helpers gardés au cas où =======
        private MaterialRuntime ResolveMaterialForEntity(Engine.Scene.Entity e)
        {
            if (e.MaterialGuid == null || e.MaterialGuid == Guid.Empty)
                return new MaterialRuntime { AlbedoTex = TextureCache.White1x1 };

            var matAsset = AssetDatabase.LoadMaterial(e.MaterialGuid.Value);
            string? ResolvePath(Guid guid)
                => AssetDatabase.TryGet(guid, out var rec) ? rec.Path : null;

            return MaterialRuntime.FromAsset(matAsset, ResolvePath);
        }

        private void DrawEntityPbr(Engine.Scene.Entity e, Matrix4 view, Matrix4 proj)
        {
            if (_pbrShader == null) return;

            e.GetModelAndNormalMatrix(out var model, out var normal);

            var mvp = model * view * proj;
            var mr = ResolveMaterialForEntity(e);

            _pbrShader.Use();
            _pbrShader.SetMat4("u_Model", model);
            _pbrShader.SetMat4("u_MVP", mvp);
            _pbrShader.SetMat3("u_NormalMat", normal);

            mr.Bind(_pbrShader);

            // If the entity uses a custom imported mesh, try to load and draw it
            var meshRenderer = e.GetComponent<Engine.Components.MeshRendererComponent>();
            if (meshRenderer != null && meshRenderer.IsUsingCustomMesh() && meshRenderer.CustomMeshGuid.HasValue)
            {
                var customMesh = LoadCustomMesh(meshRenderer.CustomMeshGuid.Value, meshRenderer.SubmeshIndex);
                if (customMesh.HasValue)
                {
                    GL.BindVertexArray(customMesh.Value.VAO);
                    GL.DrawElements(PrimitiveType.Triangles, customMesh.Value.IndexCount, DrawElementsType.UnsignedInt, 0);
                }
                else
                {
                    // Fallback to primitive cube if custom mesh failed to load
                    GL.BindVertexArray(_cubeVao);
                    GL.DrawElements(PrimitiveType.Triangles, _cubeIdx.Length, DrawElementsType.UnsignedInt, 0);
                }
            }
            else
            {
                // Use primitive mesh
                GL.BindVertexArray(_cubeVao);
                GL.DrawElements(PrimitiveType.Triangles, _cubeIdx.Length, DrawElementsType.UnsignedInt, 0);
            }
        }

        // RenderTerrainFallback method removed - using direct terrain rendering instead

        // === Custom Mesh Loading ===

        private struct CustomMeshData
        {
            public int VAO;
            public int VBO;
            public int EBO;
            public int IndexCount;
        }

        private CustomMeshData? LoadCustomMesh(Guid meshGuid, int submeshIndex = 0)
        {
            // Create a cache key combining GUID and submesh index
            var cacheKey = (meshGuid, submeshIndex);

            // Check if already cached
            if (_customMeshCache.TryGetValue(cacheKey, out var cachedMesh))
                return cachedMesh;

            try
            {
                // Load mesh asset from AssetDatabase
                var meshAsset = Engine.Assets.AssetDatabase.LoadMeshAsset(meshGuid);

                if (meshAsset == null || meshAsset.SubMeshes.Count == 0)
                {
                    Engine.Utils.DebugLogger.Log($"[ViewportRenderer] Failed to load mesh asset {meshGuid} or no submeshes");
                    return null;
                }

                // Validate submesh index
                if (submeshIndex < 0 || submeshIndex >= meshAsset.SubMeshes.Count)
                {
                    Engine.Utils.DebugLogger.Log($"[ViewportRenderer] Invalid submesh index {submeshIndex}, using 0");
                    submeshIndex = 0;
                }

                // Load the requested submesh
                var subMesh = meshAsset.SubMeshes[submeshIndex];
                var meshData = UploadCustomMeshToGPU(cacheKey, subMesh.Vertices, subMesh.Indices);

                return meshData;
            }
            catch (Exception ex)
            {
                Engine.Utils.DebugLogger.Log($"[ViewportRenderer] Error loading custom mesh {meshGuid}: {ex.Message}");
                return null;
            }
        }

        private CustomMeshData UploadCustomMeshToGPU((Guid, int) cacheKey, float[] vertices, uint[] indices)
        {
            var meshData = new CustomMeshData();
            meshData.VAO = GL.GenVertexArray();
            meshData.VBO = GL.GenBuffer();
            meshData.EBO = GL.GenBuffer();
            meshData.IndexCount = indices.Length;

            GL.BindVertexArray(meshData.VAO);

            // Upload vertices (interleaved: pos(3) + normal(3) + uv(2) = 8 floats)
            GL.BindBuffer(BufferTarget.ArrayBuffer, meshData.VBO);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

            // Upload indices
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, meshData.EBO);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

            // Setup vertex attributes (same layout as primitives)
            // Position (location = 0)
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            // Normal (location = 1)
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);

            // UV (location = 2)
            GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, 8 * sizeof(float), 6 * sizeof(float));
            GL.EnableVertexAttribArray(2);

            GL.BindVertexArray(0);

            // Cache it
            _customMeshCache[cacheKey] = meshData;

            Engine.Utils.DebugLogger.Log($"[ViewportRenderer] Uploaded custom mesh {cacheKey.Item1} submesh {cacheKey.Item2} to GPU ({vertices.Length / 8} vertices, {indices.Length / 3} triangles)");

            return meshData;
        }
    }
}

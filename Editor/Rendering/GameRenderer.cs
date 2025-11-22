using System;
using System.Collections.Generic;
using System.Linq;
using Engine.Components;
using Engine.Scene;
using Engine.Rendering;
using Engine.Assets;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace Editor.Rendering
{
    /// <summary>
    /// GameRenderer complètement indépendant - AUCUNE connexion avec ViewportRenderer
    /// </summary>
    public class GameRenderer : IDisposable
    {
        // Optional override: use explicit view/proj matrices instead of reading from a CameraComponent
        private bool _useCustomMatrices = false;
        private Matrix4 _customView = Matrix4.Identity;
        private Matrix4 _customProj = Matrix4.Identity;
        private int _framebuffer = 0;
        private int _colorTexture = 0;
        private int _depthTexture = 0;
        private int _width = 1;
        private int _height = 1;
        private bool _disposed = false;
        private Scene? _scene;
        private CameraComponent? _camera;

        // Renderer basique pour meshes
        private int _basicShader = 0;
        private readonly Dictionary<MeshKind, MeshData> _meshCache = new();
        private readonly Dictionary<Guid, MeshData> _customMeshCache = new(); // Cache for imported meshes
        private readonly Dictionary<Guid, TextureData> _textureCache = new();

        // Material cache - prevents reloading materials every frame
        private readonly Dictionary<Guid, Engine.Rendering.MaterialRuntime> _materialCache = new();

        // Renderer pour les terrains
        private Engine.Rendering.Terrain.TerrainRenderer? _terrainRenderer;

        public GameRenderer()
        {
            InitializeOpenGL();

            // Subscribe to material changes for hot-reload
            try
            {
                AssetDatabase.MaterialSaved += OnMaterialSaved;
            }
            catch (Exception ex)
            {
                Engine.Utils.DebugLogger.Log($"[GameRenderer] Failed to subscribe to MaterialSaved: {ex.Message}");
            }

            // PERFORMANCE: Clear material caches on init to ensure fresh textures
            // This is important when GameRenderer is created after textures have already been uploaded
            try
            {
                Engine.Rendering.MaterialRuntime.ClearGlobalCache();
                _materialCache.Clear();
                Console.WriteLine("[GameRenderer] Cleared material caches on initialization");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameRenderer] Failed to clear material caches: {ex.Message}");
            }
        }

        private void OnMaterialSaved(Guid materialGuid)
        {
            // Invalidate local material cache when a material is saved (silently)
            // The global MaterialRuntime cache is automatically invalidated by MaterialRuntime.OnMaterialSaved
            _materialCache.Remove(materialGuid);
        }

        public int ColorTexture => _colorTexture;
        public int Width => _width;
        public int Height => _height;

        private void InitializeOpenGL()
        {
            // Initialize texture cache early to avoid delays on first render
            Engine.Rendering.TextureCache.Initialize();

            CreateFramebuffer();
            CreateBasicShader();
            LoadBasicMeshes();

            // Initialiser le renderer de terrain
            try
            {
                Console.WriteLine("[GameRenderer] Initializing TerrainRenderer...");
                _terrainRenderer = new Engine.Rendering.Terrain.TerrainRenderer();
                if (_terrainRenderer != null)
                {
                    Console.WriteLine("[GameRenderer] ✓ TerrainRenderer initialized successfully");
                }
                else
                {
                    Console.WriteLine("[GameRenderer] ✗ TerrainRenderer is null after initialization!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameRenderer] ✗ Impossible d'initialiser TerrainRenderer: {ex.Message}");
                Console.WriteLine($"[GameRenderer] Stack trace: {ex.StackTrace}");
            }

            // DISABLED: Initialize UIRenderer and a simple demo canvas
            // This was causing GL errors and the dark square in the viewport
            /*
            try
            {
                _uiRenderer = new Engine.UI.UIRenderer();
                _demoCanvas = new Engine.UI.Canvas { Size = new System.Numerics.Vector2(_width, _height) };
                // Create a demo UIImage
                var img = new Engine.UI.UIImage();
                img.Name = "DemoPanel";
                img.Rect.SizeDelta = new System.Numerics.Vector2(200, 80);
                img.Rect.AnchoredPosition = new System.Numerics.Vector2(10, 10);
                img.Color = 0xFF336699; // semi-blue
                _demoCanvas.AddRoot(img);
                Engine.UI.EventSystem.Instance.RegisterCanvas(_demoCanvas);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameRenderer] Failed to initialize UIRenderer demo: {ex.Message}");
            }
            */
        }

        private void CreateFramebuffer()
        {
            _framebuffer = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);

            // Texture couleur
            _colorTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _colorTexture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, _width, _height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _colorTexture, 0);

            // Texture profondeur
            _depthTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _depthTexture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent24, _width, _height, 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, _depthTexture, 0);

            var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != FramebufferErrorCode.FramebufferComplete)
            {
                throw new Exception($"GameRenderer Framebuffer incomplete: {status}");
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        private void CreateBasicShader()
        {
            string vertexShaderSource = @"
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

            string fragmentShaderSource = @"
#version 330 core
out vec4 FragColor;

in vec3 FragPos;
in vec3 Normal;
in vec2 TexCoord;

uniform vec3 uColor;
uniform bool uHasTexture;
uniform sampler2D uTexture;

void main()
{
    vec3 color = uColor;
    if (uHasTexture) {
        color = texture(uTexture, TexCoord).rgb * uColor;
    }

    // Éclairage simple
    vec3 lightDir = normalize(vec3(0.5, 1.0, 0.3));
    float diff = max(dot(normalize(Normal), lightDir), 0.2);

    FragColor = vec4(color * diff, 1.0);
}";

            int vertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vertexShader, vertexShaderSource);
            GL.CompileShader(vertexShader);

            int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fragmentShader, fragmentShaderSource);
            GL.CompileShader(fragmentShader);

            _basicShader = GL.CreateProgram();
            GL.AttachShader(_basicShader, vertexShader);
            GL.AttachShader(_basicShader, fragmentShader);
            GL.LinkProgram(_basicShader);

            GL.DeleteShader(vertexShader);
            GL.DeleteShader(fragmentShader);
        }

        private void LoadBasicMeshes()
        {
            // Charger les meshes de base (cube, sphere, etc.)
            LoadCubeMesh();
            LoadPlaneMesh();
        }

        private void LoadCubeMesh()
        {
            float[] vertices = {
                // Positions         // Normales          // UV
                -0.5f, -0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  0.0f, 0.0f,
                 0.5f, -0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  1.0f, 0.0f,
                 0.5f,  0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  1.0f, 1.0f,
                 0.5f,  0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  1.0f, 1.0f,
                -0.5f,  0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  0.0f, 1.0f,
                -0.5f, -0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  0.0f, 0.0f,

                -0.5f, -0.5f,  0.5f,  0.0f,  0.0f,  1.0f,  0.0f, 0.0f,
                 0.5f, -0.5f,  0.5f,  0.0f,  0.0f,  1.0f,  1.0f, 0.0f,
                 0.5f,  0.5f,  0.5f,  0.0f,  0.0f,  1.0f,  1.0f, 1.0f,
                 0.5f,  0.5f,  0.5f,  0.0f,  0.0f,  1.0f,  1.0f, 1.0f,
                -0.5f,  0.5f,  0.5f,  0.0f,  0.0f,  1.0f,  0.0f, 1.0f,
                -0.5f, -0.5f,  0.5f,  0.0f,  0.0f,  1.0f,  0.0f, 0.0f,

                -0.5f,  0.5f,  0.5f, -1.0f,  0.0f,  0.0f,  1.0f, 0.0f,
                -0.5f,  0.5f, -0.5f, -1.0f,  0.0f,  0.0f,  1.0f, 1.0f,
                -0.5f, -0.5f, -0.5f, -1.0f,  0.0f,  0.0f,  0.0f, 1.0f,
                -0.5f, -0.5f, -0.5f, -1.0f,  0.0f,  0.0f,  0.0f, 1.0f,
                -0.5f, -0.5f,  0.5f, -1.0f,  0.0f,  0.0f,  0.0f, 0.0f,
                -0.5f,  0.5f,  0.5f, -1.0f,  0.0f,  0.0f,  1.0f, 0.0f,

                 0.5f,  0.5f,  0.5f,  1.0f,  0.0f,  0.0f,  1.0f, 0.0f,
                 0.5f,  0.5f, -0.5f,  1.0f,  0.0f,  0.0f,  1.0f, 1.0f,
                 0.5f, -0.5f, -0.5f,  1.0f,  0.0f,  0.0f,  0.0f, 1.0f,
                 0.5f, -0.5f, -0.5f,  1.0f,  0.0f,  0.0f,  0.0f, 1.0f,
                 0.5f, -0.5f,  0.5f,  1.0f,  0.0f,  0.0f,  0.0f, 0.0f,
                 0.5f,  0.5f,  0.5f,  1.0f,  0.0f,  0.0f,  1.0f, 0.0f,

                -0.5f, -0.5f, -0.5f,  0.0f, -1.0f,  0.0f,  0.0f, 1.0f,
                 0.5f, -0.5f, -0.5f,  0.0f, -1.0f,  0.0f,  1.0f, 1.0f,
                 0.5f, -0.5f,  0.5f,  0.0f, -1.0f,  0.0f,  1.0f, 0.0f,
                 0.5f, -0.5f,  0.5f,  0.0f, -1.0f,  0.0f,  1.0f, 0.0f,
                -0.5f, -0.5f,  0.5f,  0.0f, -1.0f,  0.0f,  0.0f, 0.0f,
                -0.5f, -0.5f, -0.5f,  0.0f, -1.0f,  0.0f,  0.0f, 1.0f,

                -0.5f,  0.5f, -0.5f,  0.0f,  1.0f,  0.0f,  0.0f, 1.0f,
                 0.5f,  0.5f, -0.5f,  0.0f,  1.0f,  0.0f,  1.0f, 1.0f,
                 0.5f,  0.5f,  0.5f,  0.0f,  1.0f,  0.0f,  1.0f, 0.0f,
                 0.5f,  0.5f,  0.5f,  0.0f,  1.0f,  0.0f,  1.0f, 0.0f,
                -0.5f,  0.5f,  0.5f,  0.0f,  1.0f,  0.0f,  0.0f, 0.0f,
                -0.5f,  0.5f, -0.5f,  0.0f,  1.0f,  0.0f,  0.0f, 1.0f
            };

            var meshData = new MeshData();
            meshData.VAO = GL.GenVertexArray();
            meshData.VBO = GL.GenBuffer();
            meshData.VertexCount = 36;

            GL.BindVertexArray(meshData.VAO);
            GL.BindBuffer(BufferTarget.ArrayBuffer, meshData.VBO);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

            // Position
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);
            // Normal
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);
            // UV
            GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, 8 * sizeof(float), 6 * sizeof(float));
            GL.EnableVertexAttribArray(2);

            GL.BindVertexArray(0);

            _meshCache[MeshKind.Cube] = meshData;
        }

        private void LoadPlaneMesh()
        {
            float[] vertices = {
                // Positions         // Normales          // UV
                -5.0f, 0.0f, -5.0f,  0.0f,  1.0f,  0.0f,  0.0f, 0.0f,
                 5.0f, 0.0f, -5.0f,  0.0f,  1.0f,  0.0f,  10.0f, 0.0f,
                 5.0f, 0.0f,  5.0f,  0.0f,  1.0f,  0.0f,  10.0f, 10.0f,
                 5.0f, 0.0f,  5.0f,  0.0f,  1.0f,  0.0f,  10.0f, 10.0f,
                -5.0f, 0.0f,  5.0f,  0.0f,  1.0f,  0.0f,  0.0f, 10.0f,
                -5.0f, 0.0f, -5.0f,  0.0f,  1.0f,  0.0f,  0.0f, 0.0f
            };

            var meshData = new MeshData();
            meshData.VAO = GL.GenVertexArray();
            meshData.VBO = GL.GenBuffer();
            meshData.VertexCount = 6;

            GL.BindVertexArray(meshData.VAO);
            GL.BindBuffer(BufferTarget.ArrayBuffer, meshData.VBO);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, 8 * sizeof(float), 6 * sizeof(float));
            GL.EnableVertexAttribArray(2);

            GL.BindVertexArray(0);

            _meshCache[MeshKind.Plane] = meshData;
        }

        /// <summary>
        /// Load a custom mesh from asset database and upload to GPU
        /// </summary>
        private MeshData? LoadCustomMesh(Guid meshGuid)
        {
            // Check if already cached
            if (_customMeshCache.TryGetValue(meshGuid, out var cachedMesh))
                return cachedMesh;

            try
            {
                // Load mesh asset from AssetDatabase
                var meshAsset = AssetDatabase.LoadMeshAsset(meshGuid);

                if (meshAsset == null || meshAsset.SubMeshes.Count == 0)
                {
                    Engine.Utils.DebugLogger.Log($"[GameRenderer] Failed to load mesh asset {meshGuid} or no submeshes");
                    return null;
                }

                // For now, only render the first submesh
                // TODO: Support multiple submeshes
                var subMesh = meshAsset.SubMeshes[0];
                return UploadMeshToGPU(meshGuid, subMesh.Vertices, subMesh.Indices);
            }
            catch (Exception ex)
            {
                Engine.Utils.DebugLogger.Log($"[GameRenderer] Error loading custom mesh {meshGuid}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Upload mesh data to GPU and cache it
        /// </summary>
        private MeshData UploadMeshToGPU(Guid meshGuid, float[] vertices, uint[] indices)
        {
            var meshData = new MeshData();
            meshData.VAO = GL.GenVertexArray();
            meshData.VBO = GL.GenBuffer();
            meshData.EBO = GL.GenBuffer();
            meshData.VertexCount = indices.Length;

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
            _customMeshCache[meshGuid] = meshData;

            Engine.Utils.DebugLogger.Log($"[GameRenderer] Uploaded custom mesh {meshGuid} to GPU ({vertices.Length / 8} vertices, {indices.Length / 3} triangles)");

            return meshData;
        }

        public void Resize(int width, int height)
        {
            if (_width == width && _height == height) return;

            _width = Math.Max(1, width);
            _height = Math.Max(1, height);

            GL.BindTexture(TextureTarget.Texture2D, _colorTexture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, _width, _height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);

            GL.BindTexture(TextureTarget.Texture2D, _depthTexture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent24, _width, _height, 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);

            GL.BindTexture(TextureTarget.Texture2D, 0);
        }

        public void SetSourceScene(Scene sourceScene)
        {
            _scene = sourceScene;

            // CRITICAL: Clear material cache when changing scenes
            // This ensures materials reload with fresh texture references
            _materialCache.Clear();
            Console.WriteLine("[GameRenderer] Cleared material cache for new scene");
        }

        public void SetCameraFromComponent(CameraComponent camera)
        {
            _camera = camera;
            // When a camera component is explicitly set we do not automatically enable custom matrices.
            // The panel may call SetCameraMatrices(view, proj) to force exact matrices for the current viewport size.
            _useCustomMatrices = false;
        }

        /// <summary>
        /// Force the renderer to use the provided view and projection matrices.
        /// Useful to guarantee the GameRenderer renders exactly the CameraComponent's matrices
        /// (avoids accidental coupling with the editor viewport camera state).
        /// </summary>
        public void SetCameraMatrices(Matrix4 view, Matrix4 proj)
        {
            _customView = view;
            _customProj = proj;
            _useCustomMatrices = true;
        }

        public void ClearCustomMatrices()
        {
            _useCustomMatrices = false;
        }

        public void RenderScene()
        {
            if (_scene == null || _camera == null) return;

            // Process pending texture uploads from background loading
            try
            {
                var uploads = Engine.Rendering.TextureCache.ProcessPendingUploads(10); // Increased from 1 to 10 for faster loading
                if (uploads > 0)
                {
                    // CRITICAL: Clear BOTH global AND local material caches when textures are uploaded
                    // Otherwise materials keep references to placeholder White1x1 textures
                    Engine.Rendering.MaterialRuntime.ClearGlobalCache();
                    _materialCache.Clear(); // Also clear local cache!
                    Console.WriteLine($"[GameRenderer] ⚡ Cleared material caches after uploading {uploads} texture(s)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameRenderer] TextureCache.ProcessPendingUploads failed: {ex.Message}");
            }

            // Bind framebuffer
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
            GL.Viewport(0, 0, _width, _height);

            // Clear avec couleur distinctive du GamePanel
            GL.ClearColor(0.3f, 0.5f, 0.9f, 1.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Less);

            // Matrices de caméra - TOUJOURS utiliser les custom matrices si disponibles
            Matrix4 view;
            Matrix4 proj;
            if (_useCustomMatrices)
            {
                view = _customView;
                proj = _customProj;
            }
            else
            {
                // Fallback uniquement si pas de custom matrices
                float aspect = (float)_width / _height;
                view = _camera.ViewMatrix;
                proj = _camera.ProjectionMatrix(aspect);
            }

            // Debug position de caméra (optionnel, commenté pour réduire le spam)
            // try
            // {
            //     var invView = view.Inverted();
            //     var camPos = new OpenTK.Mathematics.Vector3(invView.M41, invView.M42, invView.M43);
            //     Console.WriteLine($"[GameRenderer] Camera world pos: {camPos}");
            // }
            // catch { }

            GL.UseProgram(_basicShader);

            // Uniformes globaux
            int viewLoc = GL.GetUniformLocation(_basicShader, "uView");
            int projLoc = GL.GetUniformLocation(_basicShader, "uProjection");
            GL.UniformMatrix4(viewLoc, false, ref view);
            GL.UniformMatrix4(projLoc, false, ref proj);

            // Rendu des terrains d'abord
            RenderTerrains(view, proj);

            // Rendu des entités normales
            foreach (var entity in _scene.Entities.Where(e => e.Active))
            {
                // Skip les terrains - rendus séparément
                if (entity.HasComponent<Engine.Components.Terrain>()) continue;

                var meshRenderer = entity.GetComponent<MeshRendererComponent>();
                if (meshRenderer == null || !meshRenderer.HasMeshToRender()) continue;

                MeshData? meshData = null;

                // Check if using custom mesh first
                if (meshRenderer.IsUsingCustomMesh())
                {
                    Engine.Utils.DebugLogger.Log($"[GameRenderer] Entity '{entity.Name}' using custom mesh: {meshRenderer.CustomMeshGuid!.Value}");
                    meshData = LoadCustomMesh(meshRenderer.CustomMeshGuid!.Value);
                    if (meshData == null)
                    {
                        // Fall back to primitive if custom mesh failed to load
                        Engine.Utils.DebugLogger.Log($"[GameRenderer] Failed to load custom mesh {meshRenderer.CustomMeshGuid!.Value}, falling back to primitive");
                        _meshCache.TryGetValue(meshRenderer.Mesh, out meshData);
                    }
                    else
                    {
                        Engine.Utils.DebugLogger.Log($"[GameRenderer] Custom mesh loaded successfully: {meshData.VertexCount} vertices");
                    }
                }
                else
                {
                    // Use primitive mesh
                    if (!_meshCache.TryGetValue(meshRenderer.Mesh, out meshData))
                        continue;
                }

                if (meshData == null) continue;

                // Matrice modèle
                entity.GetWorldTRS(out var pos, out var rot, out var scale);
                var model = Matrix4.CreateScale(scale) * Matrix4.CreateFromQuaternion(rot) * Matrix4.CreateTranslation(pos);

                int modelLoc = GL.GetUniformLocation(_basicShader, "uModel");
                GL.UniformMatrix4(modelLoc, false, ref model);

                // Load and bind material
                Guid materialGuid;
                // Prefer explicit material on MeshRendererComponent
                if (meshRenderer.MaterialGuid.HasValue && meshRenderer.MaterialGuid.Value != Guid.Empty)
                {
                    materialGuid = meshRenderer.MaterialGuid.Value;
                }
                else if (meshRenderer.CustomMeshGuid.HasValue && meshRenderer.CustomMeshGuid.Value != Guid.Empty)
                {
                    // If no material set on the renderer, try the imported mesh's per-submesh material
                    try
                    {
                        Engine.Assets.MeshAsset? meshAsset = null;
                        if (AssetDatabase.TryGet(meshRenderer.CustomMeshGuid.Value, out var rec))
                        {
                            try { meshAsset = Engine.Assets.MeshAsset.Load(rec.Path); } catch { meshAsset = null; }
                        }

                        if (meshAsset != null && meshAsset.MaterialGuids != null && meshAsset.MaterialGuids.Count > meshRenderer.SubmeshIndex && meshAsset.MaterialGuids[meshRenderer.SubmeshIndex].HasValue)
                        {
                            materialGuid = meshAsset.MaterialGuids[meshRenderer.SubmeshIndex]!.Value;
                        }
                        else
                        {
                            materialGuid = AssetDatabase.EnsureDefaultWhiteMaterial();
                        }
                    }
                    catch
                    {
                        materialGuid = AssetDatabase.EnsureDefaultWhiteMaterial();
                    }
                }
                else
                {
                    materialGuid = AssetDatabase.EnsureDefaultWhiteMaterial();
                }

                // Check cache first
                if (!_materialCache.TryGetValue(materialGuid, out var matRuntime))
                {
                    var materialAsset = AssetDatabase.LoadMaterial(materialGuid);
                    if (materialAsset != null)
                    {
                        matRuntime = Engine.Rendering.MaterialRuntime.FromAsset(materialAsset, (guid) =>
                        {
                            if (AssetDatabase.TryGet(guid, out var rec))
                                return rec.Path;
                            return null;
                        });
                        _materialCache[materialGuid] = matRuntime;
                    }
                }

                if (matRuntime != null)
                {

                    // Bind albedo texture (basic shader uses "uTexture" uniform)
                    if (matRuntime.AlbedoTex != 0 && matRuntime.AlbedoTex != Engine.Rendering.TextureCache.White1x1)
                    {
                        GL.ActiveTexture(TextureUnit.Texture0);
                        GL.BindTexture(TextureTarget.Texture2D, matRuntime.AlbedoTex);
                        GL.Uniform1(GL.GetUniformLocation(_basicShader, "uTexture"), 0); // ✅ Fixed: uTexture not uAlbedoTex
                        GL.Uniform1(GL.GetUniformLocation(_basicShader, "uHasTexture"), 1);
                    }
                    else
                    {
                        GL.Uniform1(GL.GetUniformLocation(_basicShader, "uHasTexture"), 0);
                    }

                    // Set albedo color (modulates texture)
                    GL.Uniform3(GL.GetUniformLocation(_basicShader, "uColor"),
                        matRuntime.AlbedoColor[0], matRuntime.AlbedoColor[1], matRuntime.AlbedoColor[2]);

                    // Note: Basic shader doesn't support normal maps, metallic, roughness yet
                    // These would need to be added to the shader for full PBR support
                }
                else
                {
                    // Fallback: white color, no texture
                    GL.Uniform1(GL.GetUniformLocation(_basicShader, "uHasTexture"), 0);
                    GL.Uniform3(GL.GetUniformLocation(_basicShader, "uColor"), 1f, 1f, 1f);
                }

                // Rendu - use DrawElements if we have indices (custom mesh), otherwise DrawArrays
                GL.BindVertexArray(meshData.VAO);
                if (meshData.EBO != 0)
                {
                    // Custom mesh with indices
                    GL.DrawElements(PrimitiveType.Triangles, meshData.VertexCount, DrawElementsType.UnsignedInt, IntPtr.Zero);
                }
                else
                {
                    // Primitive mesh without indices
                    GL.DrawArrays(PrimitiveType.Triangles, 0, meshData.VertexCount);
                }
            }

            GL.BindVertexArray(0);
            GL.UseProgram(0);

            // DISABLED: Render UI on top of the scene
            // This was causing GL errors (UIRenderer doesn't save/restore GL state properly)
            /*
            try
            {
                if (_uiRenderer != null)
                {
                    // Update demo canvas size in case of resize
                    if (_demoCanvas != null) _demoCanvas.Size = new System.Numerics.Vector2(_width, _height);
                    _uiRenderer.RenderAllCanvases();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameRenderer] UI render error: {ex.Message}");
            }
            */

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        private void RenderTerrains(Matrix4 view, Matrix4 proj)
        {
            if (_scene?.Entities == null || _terrainRenderer == null) return;

            foreach (var entity in _scene.Entities.Where(e => e.Active))
            {
                var terrain = entity.GetComponent<Engine.Components.Terrain>();
                if (terrain == null) continue;

                // Position de la caméra
                var viewPos = Vector3.Zero;
                if (_camera != null)
                {
                    // Extraire la position de la caméra depuis la matrice de vue
                    var invView = view.Inverted();
                    viewPos = new Vector3(invView.M41, invView.M42, invView.M43);
                }

                // Direction de la lumière et couleur
                var lightDir = new Vector3(0.5f, 1.0f, 0.3f);
                var lightColor = new Vector3(1.0f, 1.0f, 0.9f);

                try
                {
                    // Get terrain entity transform matrix
                    entity.GetModelAndNormalMatrix(out var terrainModel, out var terrainNormalMat);

                    _terrainRenderer.RenderTerrain(
                        terrain,
                        view,
                        proj,
                        viewPos,
                        lightDir,
                        lightColor,
                        false, // pas de SSAO
                        0,
                        1.0f,
                        new Vector2(_width, _height),
                        false, // pas d'ombres
                        0,
                        Matrix4.Identity,
                        0.005f,
                        1024f,
                        1.0f,  // PCF radius
                        terrainModel  // Pass terrain transform matrix
                    );
                }
                catch (Exception ex)
                {
                    // En cas d'erreur, continuer le rendu des autres terrains
                    Console.WriteLine($"[GameRenderer] Erreur terrain: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                // Unsubscribe from events
                try
                {
                    AssetDatabase.MaterialSaved -= OnMaterialSaved;
                }
                catch { }

                _terrainRenderer?.Dispose();

                // Clean up custom meshes
                foreach (var mesh in _customMeshCache.Values)
                {
                    GL.DeleteVertexArray(mesh.VAO);
                    GL.DeleteBuffer(mesh.VBO);
                    if (mesh.EBO != 0) GL.DeleteBuffer(mesh.EBO);
                }
                _customMeshCache.Clear();

                // Clear material cache
                _materialCache.Clear();

                foreach (var mesh in _meshCache.Values)
                {
                    GL.DeleteVertexArray(mesh.VAO);
                    GL.DeleteBuffer(mesh.VBO);
                    if (mesh.EBO != 0) GL.DeleteBuffer(mesh.EBO);
                }
                _meshCache.Clear();

                foreach (var texture in _textureCache.Values)
                {
                    GL.DeleteTexture(texture.TextureId);
                }
                _textureCache.Clear();

                if (_basicShader != 0)
                {
                    GL.DeleteProgram(_basicShader);
                    _basicShader = 0;
                }

                if (_framebuffer != 0)
                {
                    GL.DeleteFramebuffer(_framebuffer);
                    _framebuffer = 0;
                }

                if (_colorTexture != 0)
                {
                    GL.DeleteTexture(_colorTexture);
                    _colorTexture = 0;
                }

                if (_depthTexture != 0)
                {
                    GL.DeleteTexture(_depthTexture);
                    _depthTexture = 0;
                }

                _disposed = true;
            }
        }

        private class MeshData
        {
            public int VAO { get; set; }
            public int VBO { get; set; }
            public int EBO { get; set; }
            public int VertexCount { get; set; }
        }

        private class TextureData
        {
            public int TextureId { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
        }
    }
}

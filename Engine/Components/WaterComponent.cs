using System;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace Engine.Components
{
    /// <summary>
    /// Water component that generates a tessellated plane mesh.
    /// Uses GPU tessellation shaders for dynamic wave displacement.
    /// </summary>
    public class WaterComponent : Component
    {
        // Serialized properties
        [Engine.Serialization.SerializableAttribute("waterMaterialGuid")]
        public Guid? WaterMaterialGuid { get; set; } = null;

        [Engine.Serialization.SerializableAttribute("resolution")]
        public int Resolution { get; set; } = 32; // Number of patches per side (will create Resolution x Resolution patches)

        [Engine.Serialization.SerializableAttribute("waterWidth")]
        public float WaterWidth { get; set; } = 100f;

        [Engine.Serialization.SerializableAttribute("waterLength")]
        public float WaterLength { get; set; } = 100f;

        // Runtime fields
        private int _vao = 0, _vbo = 0, _ebo = 0;
        private int _patchCount = 0;
        private int _vertexCount = 0;
        private bool _meshGenerated = false;

        // Cached values to detect changes
        private int _lastResolution = -1;
        private float _lastWidth = -1f;
        private float _lastLength = -1f;

        // Public getters for stats
        public int PatchCount => _patchCount;
        public int VertexCount => _vertexCount;
        public bool IsMeshGenerated => _meshGenerated;

        /// <summary>
        /// Calculate approximate memory usage in bytes
        /// </summary>
        public int EstimatedMemoryUsage
        {
            get
            {
                if (!_meshGenerated) return 0;
                int vertexBytes = _vertexCount * 8 * sizeof(float); // 8 floats per vertex
                int indexBytes = _patchCount * 4 * sizeof(uint); // 4 indices per patch
                return vertexBytes + indexBytes;
            }
        }

        public WaterComponent()
        {
        }

        /// <summary>
        /// Called when component is attached to an entity
        /// </summary>
        public override void OnAttached()
        {
            base.OnAttached();

            // Assign default water material if none is set
            if (!WaterMaterialGuid.HasValue || WaterMaterialGuid == Guid.Empty)
            {
                WaterMaterialGuid = Engine.Assets.AssetDatabase.EnsureDefaultWaterMaterial();
            }

            // Generate water mesh
            GenerateWaterMesh();
        }

        /// <summary>
        /// Generate water plane mesh with tessellation patches.
        /// Each patch is a quad (4 vertices) that will be subdivided by the tessellation shader.
        /// </summary>
        public void GenerateWaterMesh()
        {
            try
            {
                // Clear old mesh data first
                ClearWaterMesh();

                int res = Math.Max(1, Resolution);

                // We create a grid of patches (quads)
                // Each patch has 4 vertices (vertices are shared between adjacent patches)
                int verticesPerSide = res + 1;
                int vertexCount = verticesPerSide * verticesPerSide;

                // Vertex format: Position(3) + Normal(3) + TexCoord(2) = 8 floats
                float[] vertices = new float[vertexCount * 8];

                float stepX = WaterWidth / res;
                float stepZ = WaterLength / res;
                float startX = -WaterWidth * 0.5f;
                float startZ = -WaterLength * 0.5f;

                // Generate vertices
                for (int z = 0; z < verticesPerSide; z++)
                {
                    for (int x = 0; x < verticesPerSide; x++)
                    {
                        int idx = (z * verticesPerSide + x) * 8;

                        // UV coordinates [0,1]
                        float u = x / (float)res;
                        float v = z / (float)res;

                        // World position (Y = 0, will be displaced by tessellation shader)
                        float posX = startX + x * stepX;
                        float posZ = startZ + z * stepZ;

                        // Position
                        vertices[idx + 0] = posX;
                        vertices[idx + 1] = 0f; // Y = 0 (flat plane, tessellation will displace)
                        vertices[idx + 2] = posZ;

                        // Normal (up)
                        vertices[idx + 3] = 0f;
                        vertices[idx + 4] = 1f;
                        vertices[idx + 5] = 0f;

                        // TexCoord
                        vertices[idx + 6] = u;
                        vertices[idx + 7] = v;
                    }
                }

                // Generate triangle indices (2 triangles per quad)
                int patchesPerSide = res;
                int totalPatches = patchesPerSide * patchesPerSide;
                uint[] indices = new uint[totalPatches * 6]; // 6 indices per quad (2 triangles)

                int iIdx = 0;
                for (int z = 0; z < patchesPerSide; z++)
                {
                    for (int x = 0; x < patchesPerSide; x++)
                    {
                        uint topLeft = (uint)(z * verticesPerSide + x);
                        uint topRight = (uint)(z * verticesPerSide + x + 1);
                        uint bottomLeft = (uint)((z + 1) * verticesPerSide + x);
                        uint bottomRight = (uint)((z + 1) * verticesPerSide + x + 1);

                        // First triangle (clockwise when viewed from above for correct culling)
                        indices[iIdx++] = topLeft;
                        indices[iIdx++] = topRight;
                        indices[iIdx++] = bottomLeft;

                        // Second triangle (clockwise when viewed from above for correct culling)
                        indices[iIdx++] = topRight;
                        indices[iIdx++] = bottomRight;
                        indices[iIdx++] = bottomLeft;
                    }
                }

                // Upload to GPU
                UploadMeshToGPU(vertices, indices);
                _patchCount = totalPatches;
                _vertexCount = vertexCount;
                _meshGenerated = true;

                // Update cached values
                _lastResolution = Resolution;
                _lastWidth = WaterWidth;
                _lastLength = WaterLength;

                Console.WriteLine($"[WaterComponent] Water mesh generated: {vertexCount} vertices, {totalPatches} patches ({res}x{res} resolution)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WaterComponent] Failed to generate water mesh: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        /// <summary>
        /// Upload mesh data to GPU buffers.
        /// </summary>
        private void UploadMeshToGPU(float[] vertices, uint[] indices)
        {
            // Clean up old buffers
            if (_vao != 0) GL.DeleteVertexArray(_vao);
            if (_vbo != 0) GL.DeleteBuffer(_vbo);
            if (_ebo != 0) GL.DeleteBuffer(_ebo);

            // Generate new buffers
            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();
            _ebo = GL.GenBuffer();

            GL.BindVertexArray(_vao);

            // Upload vertex data
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

            // Upload index data
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

            // Position attribute (location 0)
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            // Normal attribute (location 1)
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);

            // TexCoord attribute (location 2)
            GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, 8 * sizeof(float), 6 * sizeof(float));
            GL.EnableVertexAttribArray(2);

            GL.BindVertexArray(0);

            Console.WriteLine($"[WaterComponent] Uploaded mesh to GPU: VAO={_vao}, VBO={_vbo}, EBO={_ebo}");
        }

        /// <summary>
        /// Render the water mesh as regular triangles (no tessellation for now).
        /// </summary>
        public void Render()
        {
            // Don't render if mesh hasn't been generated
            if (!_meshGenerated || _vao == 0 || _patchCount == 0)
            {
                return;
            }

            // Enable blending for water transparency
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            // Enable face culling to only render top face (prevents seeing reflections from below)
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(TriangleFace.Back);

            GL.BindVertexArray(_vao);

            // Draw as triangles (we have quads, so 2 triangles per patch = 6 indices)
            // Each patch has 4 vertices arranged as a quad, we need to draw them as 2 triangles
            GL.DrawElements(PrimitiveType.Triangles, _patchCount * 6, DrawElementsType.UnsignedInt, 0);

            // Check for OpenGL errors
            var errorCode = GL.GetError();
            if (errorCode != ErrorCode.NoError)
            {
                Console.WriteLine($"[WaterComponent] GL error during rendering: {errorCode}");
            }

            GL.BindVertexArray(0);

            // Restore state
            GL.Disable(EnableCap.Blend);
        }

        /// <summary>
        /// Clear water mesh and release GPU resources.
        /// </summary>
        public void ClearWaterMesh()
        {
            if (_vao != 0) { GL.DeleteVertexArray(_vao); _vao = 0; }
            if (_vbo != 0) { GL.DeleteBuffer(_vbo); _vbo = 0; }
            if (_ebo != 0) { GL.DeleteBuffer(_ebo); _ebo = 0; }
            _meshGenerated = false;
            _patchCount = 0;
            Console.WriteLine("[WaterComponent] Water mesh cleared");
        }

        /// <summary>
        /// Regenerate mesh when resolution or size changes.
        /// </summary>
        public void UpdateMesh()
        {
            GenerateWaterMesh();
        }

        /// <summary>
        /// Check if mesh needs regeneration based on property changes
        /// </summary>
        public bool NeedsMeshRegeneration()
        {
            return _lastResolution != Resolution ||
                   Math.Abs(_lastWidth - WaterWidth) > 0.001f ||
                   Math.Abs(_lastLength - WaterLength) > 0.001f;
        }

        /// <summary>
        /// Update method - automatically regenerates mesh if properties changed
        /// </summary>
        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            // Auto-regenerate mesh if properties changed (for editor real-time updates)
            if (_meshGenerated && NeedsMeshRegeneration())
            {
                GenerateWaterMesh();
            }
        }

        public override void OnDetached()
        {
            base.OnDetached();
            ClearWaterMesh();
        }
    }
}

using System;
using System.Collections.Generic;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Engine.Components;
using Engine.Scene;

namespace Engine.Rendering
{
    /// <summary>
    /// High-performance particle renderer with GPU instancing and billboard rendering
    /// Inspired by Unity, Unreal, and Godot particle rendering best practices
    /// </summary>
    public sealed class ParticleRenderer : IDisposable
    {
        private ShaderProgram? _particleShader;
        private int _vao;
        private int _vbo;
        private int _instanceVbo;
        
        // Billboard quad vertices (position + UV)
        private static readonly float[] QuadVertices = new float[]
        {
            // Pos (XY)     UV
            -0.5f, -0.5f,   0.0f, 0.0f,
             0.5f, -0.5f,   1.0f, 0.0f,
             0.5f,  0.5f,   1.0f, 1.0f,
            -0.5f, -0.5f,   0.0f, 0.0f,
             0.5f,  0.5f,   1.0f, 1.0f,
            -0.5f,  0.5f,   0.0f, 1.0f
        };

        // Instance data structure for GPU
        private struct ParticleInstanceData
        {
            public Vector3 Position;
            public float Size;
            public Color4 Color;
            public float Rotation;
        }

        private ParticleInstanceData[] _instanceData = Array.Empty<ParticleInstanceData>();
        private const int MaxInstancesPerBatch = 10000;

        public ParticleRenderer()
        {
            InitializeBuffers();
            LoadShaders();
        }

        private void InitializeBuffers()
        {
            // Create VAO
            _vao = GL.GenVertexArray();
            GL.BindVertexArray(_vao);

            // Create VBO for quad vertices
            _vbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, QuadVertices.Length * sizeof(float), QuadVertices, BufferUsageHint.StaticDraw);

            // Layout 0: Position (vec2)
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);

            // Layout 1: UV (vec2)
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));

            // Create instance VBO (will be filled per-frame)
            _instanceVbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _instanceVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, MaxInstancesPerBatch * System.Runtime.InteropServices.Marshal.SizeOf<ParticleInstanceData>(), 
                IntPtr.Zero, BufferUsageHint.DynamicDraw);

            // Instance attributes (divisor = 1)
            int stride = System.Runtime.InteropServices.Marshal.SizeOf<ParticleInstanceData>();

            // Layout 2: Position (vec3)
            GL.EnableVertexAttribArray(2);
            GL.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, stride, 0);
            GL.VertexAttribDivisor(2, 1);

            // Layout 3: Size (float)
            GL.EnableVertexAttribArray(3);
            GL.VertexAttribPointer(3, 1, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
            GL.VertexAttribDivisor(3, 1);

            // Layout 4: Color (vec4)
            GL.EnableVertexAttribArray(4);
            GL.VertexAttribPointer(4, 4, VertexAttribPointerType.Float, false, stride, 4 * sizeof(float));
            GL.VertexAttribDivisor(4, 1);

            // Layout 5: Rotation (float)
            GL.EnableVertexAttribArray(5);
            GL.VertexAttribPointer(5, 1, VertexAttribPointerType.Float, false, stride, 8 * sizeof(float));
            GL.VertexAttribDivisor(5, 1);

            GL.BindVertexArray(0);
        }

        private void LoadShaders()
        {
            try
            {
                // Get the base directory and navigate to the correct shader location
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string vertPath = System.IO.Path.Combine(baseDir, "..", "..", "..", "..", "Engine", "Rendering", "Shaders", "Effects", "particle.vert");
                string fragPath = System.IO.Path.Combine(baseDir, "..", "..", "..", "..", "Engine", "Rendering", "Shaders", "Effects", "particle.frag");
                
                vertPath = System.IO.Path.GetFullPath(vertPath);
                fragPath = System.IO.Path.GetFullPath(fragPath);
                
                if (!System.IO.File.Exists(vertPath))
                {
                    throw new System.IO.FileNotFoundException($"Shader file not found: {vertPath}");
                }
                if (!System.IO.File.Exists(fragPath))
                {
                    throw new System.IO.FileNotFoundException($"Shader file not found: {fragPath}");
                }
                
                _particleShader = ShaderProgram.FromFiles(vertPath, fragPath);
            }
            catch (Exception ex)
            {
                try { Utils.DebugLogger.Log($"[ParticleRenderer] Failed to load particle shaders: {ex.Message}"); } catch { }
            }
        }

        /// <summary>
        /// Render all particle systems in the scene
        /// </summary>
        public void RenderParticleSystems(Scene.Scene scene, Matrix4 viewMatrix, Matrix4 projectionMatrix, Vector3 cameraPosition)
        {
            if (_particleShader == null || scene == null) return;

            // Gather all active particle systems
            var particleSystems = new List<ParticleSystem>();
            foreach (var entity in scene.Entities)
            {
                if (!entity.Active) continue;

                var ps = entity.GetComponent<ParticleSystem>();
                if (ps != null && ps.Enabled)
                {
                    if (ps.ParticleCount > 0)
                    {
                        particleSystems.Add(ps);
                    }
                }
            }

            if (particleSystems.Count == 0) return;

            // Save current GL state
            GL.GetInteger(GetPName.CurrentProgram, out int oldProgram);
            GL.GetInteger(GetPName.VertexArrayBinding, out int oldVao);
            bool blendWasEnabled = GL.IsEnabled(EnableCap.Blend);
            bool cullWasEnabled = GL.IsEnabled(EnableCap.CullFace);
            GL.GetBoolean(GetPName.DepthWritemask, out bool depthWriteWasEnabled);

            // Setup GL state for particle rendering
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.DepthMask(false); // Don't write to depth buffer
            GL.Disable(EnableCap.CullFace);

            // Bind shader and set uniforms
            _particleShader.Use();
            _particleShader.SetMat4("uView", viewMatrix);
            _particleShader.SetMat4("uProjection", projectionMatrix);
            _particleShader.SetVec3("uCameraPos", cameraPosition);
            _particleShader.SetVec3("uCameraRight", new Vector3(viewMatrix.M11, viewMatrix.M21, viewMatrix.M31));
            _particleShader.SetVec3("uCameraUp", new Vector3(viewMatrix.M12, viewMatrix.M22, viewMatrix.M32));
            _particleShader.SetInt("uHasTexture", 0);

            GL.BindVertexArray(_vao);

            // Render each particle system
            int totalRendered = 0;
            foreach (var ps in particleSystems)
            {
                RenderParticleSystem(ps);
                totalRendered += ps.ParticleCount;
            }

            GL.BindVertexArray(oldVao);
            GL.UseProgram(oldProgram);

            // Restore GL state
            if (!cullWasEnabled) GL.Disable(EnableCap.CullFace);
            else GL.Enable(EnableCap.CullFace);
            
            GL.DepthMask(depthWriteWasEnabled);
            
            if (!blendWasEnabled) GL.Disable(EnableCap.Blend);
        }

        private void RenderParticleSystem(ParticleSystem ps)
        {
            int count = ps.ParticleCount;
            if (count == 0) return;

            // Get particle data
            ps.GetRenderData(out var positions, out var sizes, out var colors, out var rotations);

            // Prepare instance data
            if (_instanceData.Length < count)
            {
                _instanceData = new ParticleInstanceData[count];
            }

            for (int i = 0; i < count; i++)
            {
                _instanceData[i] = new ParticleInstanceData
                {
                    Position = positions[i],
                    Size = sizes[i],
                    Color = colors[i],
                    Rotation = rotations[i]
                };
            }

            // Upload instance data to GPU
            GL.BindBuffer(BufferTarget.ArrayBuffer, _instanceVbo);
            int dataSize = count * System.Runtime.InteropServices.Marshal.SizeOf<ParticleInstanceData>();
            
            unsafe
            {
                fixed (ParticleInstanceData* ptr = _instanceData)
                {
                    GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, dataSize, (IntPtr)ptr);
                }
            }

            // Draw instanced
            GL.DrawArraysInstanced(PrimitiveType.Triangles, 0, 6, count);
        }

        public void Dispose()
        {
            if (_vao != 0)
            {
                GL.DeleteVertexArray(_vao);
                _vao = 0;
            }

            if (_vbo != 0)
            {
                GL.DeleteBuffer(_vbo);
                _vbo = 0;
            }

            if (_instanceVbo != 0)
            {
                GL.DeleteBuffer(_instanceVbo);
                _instanceVbo = 0;
            }

            _particleShader?.Dispose();
        }
    }
}

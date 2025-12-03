// Planar reflection renderer removed. Provide a minimal stub so remaining references compile.
using OpenTK.Mathematics;
using System;

namespace Engine.Rendering
{
    [Obsolete("Planar reflections removed - this stub exists only for compatibility.")]
    public sealed class PlanarReflectionRenderer : IDisposable
    {
        public Vector4 ReflectionPlane { get; set; } = new Vector4(0, 1, 0, 0);

        public PlanarReflectionRenderer(int width, int height) { }

        public (Matrix4 viewMatrix, Matrix4 projMatrix) BeginRender(OpenTK.Mathematics.Vector3 cameraPos, Matrix4 cameraView, Matrix4 cameraProj)
        {
            return (cameraView, cameraProj);
        }

        public void EndRender() { }

        public int GetReflectionTexture() => 0;

        public void Resize(int width, int height) { }

        public void Dispose() { }
    }
}

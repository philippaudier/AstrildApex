using System;
using System.Threading;
using System.Numerics;
using System.Collections.Generic;
using OpenTK.Mathematics;

namespace Engine.Rendering.Terrain.Tile
{
    /// <summary>
    /// Represents a single terrain tile for infinite streaming.
    /// </summary>
    public class TerrainTile
    {
        public enum TileState { Unloaded, Loading, ReadyCpu, ReadyGpu, Renderable, Evicted }

        public readonly int X;  // Tile grid coordinates
        public readonly int Y;
        public readonly int Lod;  // Level of detail (0 = highest)

        // State management (thread-safe)
        private TileState _state = TileState.Unloaded;
        private readonly object _stateLock = new object();

        // CPU-side buffers (owned by worker threads until uploaded)
        public float[]? VerticesCpu { get; private set; }
        public uint[]? IndicesCpu { get; private set; }

        // GPU handles (created/uploaded on GL thread)
        public int Vao { get; private set; }
        public int Vbo { get; private set; }
        public int Ebo { get; private set; }
        public int IndexCount { get; private set; }

        // Vegetation instances per layer
        public Dictionary<int, List<Matrix4>>? VegetationInstances { get; private set; }

        // Neighbor LODs captured at request time (key = (tx,ty) of neighbor)
        // Used by CPU generator to stitch borders when neighboring tiles have different LODs
        public System.Collections.Generic.Dictionary<(int x, int y), int>? NeighborLods { get; set; }
        // World bounds for frustum culling
        public System.Numerics.Vector3 WorldMin { get; private set; }
        public System.Numerics.Vector3 WorldMax { get; private set; }

        // LOD Transition for dithered fade (Unreal-style cross-fade)
        // For fade-in: 0.0 → 1.0 (new tile appearing)
        // For fade-out: 1.0 → 0.0 (old tile disappearing)
        public float TransitionFactor { get; private set; } = 0f;
        public bool IsFadingOut { get; private set; } = false;
        private const float _transitionSpeed = 1.0f;  // Slower fade for visible dithering (1 second)

        public TileState State { get { lock(_stateLock) { return _state; } } }

        public TerrainTile(int x, int y, int lod)
        {
            X = x; Y = y; Lod = lod;
            Vao = Vbo = Ebo = 0;
        }

        public void RequestLoad()
        {
            lock (_stateLock)
            {
                if (_state == TileState.Unloaded || _state == TileState.Evicted)
                {
                    _state = TileState.Loading;
                    // CRITICAL: Reset transition for dithering effect on reload
                    TransitionFactor = 0f;
                }
            }
        }

        public void CancelLoad()
        {
            lock (_stateLock)
            {
                if (_state == TileState.Loading) _state = TileState.Unloaded;
            }
        }

        public void OnCpuReady(float[] vertices, uint[] indices)
        {
            VerticesCpu = vertices;
            IndicesCpu = indices;
            IndexCount = indices?.Length ?? 0;

            // Calculate world bounds from vertices
            if (vertices != null && vertices.Length >= 8)
            {
                float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
                float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;

                for (int i = 0; i < vertices.Length; i += 8)  // 8 floats per vertex (pos, normal, uv)
                {
                    float x = vertices[i];
                    float y = vertices[i + 1];
                    float z = vertices[i + 2];

                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    minZ = Math.Min(minZ, z);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                    maxZ = Math.Max(maxZ, z);
                }

                WorldMin = new System.Numerics.Vector3(minX, minY, minZ);
                WorldMax = new System.Numerics.Vector3(maxX, maxY, maxZ);
            }

            lock (_stateLock)
            {
                _state = TileState.ReadyCpu;
            }
        }

        public void OnUploadedToGpu(int vao, int vbo, int ebo)
        {
            Vao = vao; Vbo = vbo; Ebo = ebo;
            lock (_stateLock)
            {
                _state = TileState.Renderable;
            }
            // Start fade-in transition
            TransitionFactor = 0f;
            // Return CPU buffers to pool to reduce GC pressure
            if (VerticesCpu != null)
            {
                Engine.Rendering.Terrain.MeshBufferPool.ReturnFloat(VerticesCpu);
                VerticesCpu = null;
            }
            IndicesCpu = null;  // TODO: Return to pool when uint[] support is added
        }

        public void AttachVegetation(Dictionary<int, List<Matrix4>> instances)
        {
            VegetationInstances = instances;
        }

        public void Evict()
        {
            lock (_stateLock)
            {
                _state = TileState.Evicted;
            }
            // Return CPU buffers to pool if still present
            if (VerticesCpu != null)
            {
                Engine.Rendering.Terrain.MeshBufferPool.ReturnFloat(VerticesCpu);
                VerticesCpu = null;
            }
            IndicesCpu = null;  // TODO: Return to pool when uint[] support is added

            // Note: GPU resource deletion handled by manager on GL thread
            VegetationInstances?.Clear();
            VegetationInstances = null;
        }

        public System.Numerics.Vector3 GetCenter()
        {
            return (WorldMin + WorldMax) * 0.5f;
        }

        /// <summary>
        /// Update LOD transition animation (call every frame for smooth dithered fade)
        /// Returns true if tile should still be rendered, false if fade-out complete
        /// </summary>
        public bool UpdateTransition(float deltaTime)
        {
            if (IsFadingOut)
            {
                // Fade out: 1.0 → 0.0
                TransitionFactor = Math.Max(0f, TransitionFactor - _transitionSpeed * deltaTime);
                return TransitionFactor > 0.01f;  // Keep rendering until nearly invisible
            }
            else if (TransitionFactor < 1f && State == TileState.Renderable)
            {
                // Fade in: 0.0 → 1.0
                TransitionFactor = Math.Min(1f, TransitionFactor + _transitionSpeed * deltaTime);
            }
            return true;
        }

        /// <summary>
        /// Start fade-out transition (for tiles being replaced by different LOD)
        /// </summary>
        public void StartFadeOut()
        {
            IsFadingOut = true;
            // Keep current TransitionFactor (usually 1.0) and fade down
        }

        /// <summary>
        /// Check if fade-out is complete
        /// </summary>
        public bool IsFadeOutComplete => IsFadingOut && TransitionFactor <= 0.01f;

        /// <summary>
        /// Reset transition factor to 0 (for re-appearing tiles)
        /// </summary>
        public void ResetTransition()
        {
            TransitionFactor = 0f;
            IsFadingOut = false;
        }

        public float GetRadius()
        {
            return System.Numerics.Vector3.Distance(WorldMin, WorldMax) * 0.5f;
        }
    }
}

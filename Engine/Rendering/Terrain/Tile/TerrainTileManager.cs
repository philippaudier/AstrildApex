using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Numerics;
using System.Linq;

namespace Engine.Rendering.Terrain.Tile
{
    /// <summary>
    /// Manages terrain tiles for infinite streaming.
    /// Handles async generation, GPU uploads, and eviction.
    /// </summary>
    public class TerrainTileManager : IDisposable
    {
        // Concurrent queue for tiles ready to be uploaded to GPU
        private readonly ConcurrentQueue<TerrainTile> _uploadQueue = new();

        // Queue for tiles that need CPU generation
        private readonly ConcurrentQueue<TerrainTile> _generationQueue = new();

        // Active tiles keyed by (x,y,lod) tuple
        private readonly Dictionary<(int x,int y,int lod), TerrainTile> _tiles = new();
        private readonly object _tilesLock = new object();

        // Worker thread for CPU generation
        private Thread? _workerThread = null;
        private readonly AutoResetEvent _workEvent = new(false);
        private volatile bool _running = false;

        // Tile generator delegate
        public Func<int, int, int, (float[] vertices, uint[] indices)>? TileGenerator;

        // Vegetation generator delegate (optional)
        public Func<Engine.Components.Terrain, int, int, Dictionary<int, List<OpenTK.Mathematics.Matrix4>>>? VegetationGenerator;

        // Stats for debugging/inspector
        public int LoadedTiles => _tiles.Count;
        public int RenderableTiles { get; private set; }
        public int LoadingTiles { get; private set; }

        public TerrainTileManager()
        {
        }

        /// <summary>
        /// Request tiles around camera position with intelligent LOD selection.
        /// </summary>
        public void RequestTilesAround(Engine.Components.Terrain terrain, float camWorldX, float camWorldZ, int radiusTiles = 2)
        {
            if (terrain == null) return;

            // Fixed tile size in world units
            float tileWorldSize = terrain.StreamingTileSize;
            if (tileWorldSize <= 0) tileWorldSize = 100f;

            // Convert camera world position to tile coordinates
            int centerTileX = (int)Math.Floor(camWorldX / tileWorldSize);
            int centerTileY = (int)Math.Floor(camWorldZ / tileWorldSize);

            // Request tiles in expanding rings based on distance
            int maxRadius = radiusTiles * 4;  // Expand search radius for distant LODs

            for (int dy = -maxRadius; dy <= maxRadius; dy++)
            {
                for (int dx = -maxRadius; dx <= maxRadius; dx++)
                {
                    int tx = centerTileX + dx;
                    int ty = centerTileY + dy;

                    // Distance from camera tile (in tile units)
                    float dist = (float)Math.Sqrt(dx * dx + dy * dy);

                    // Skip tiles beyond render distance
                    if (dist > maxRadius) continue;

                    // Determine LOD based on distance
                    int lod = 0;
                    if (dist > 8f) lod = 3;
                    else if (dist > 4f) lod = 2;
                    else if (dist > 2f) lod = 1;
                    else lod = 0;

                    // Clamp LOD to max allowed
                    lod = Math.Min(lod, terrain.StreamingMaxLOD);

                    RequestTile(tx, ty, lod);
                }
            }

            // Evict distant tiles to free memory
            EvictDistantTiles(centerTileX, centerTileY, maxRadius + 2);

            // Update stats
            UpdateStats();
        }

        /// <summary>
        /// Request generation of a single tile.
        /// CRITICAL: Only one LOD can be active for a given (x,y) position at a time.
        /// </summary>
        public TerrainTile RequestTile(int x, int y, int lod)
        {
            lock (_tilesLock)
            {
                var key = (x, y, lod);

                // CRITICAL FIX: Evict all other LODs for this position (x,y) first!
                // This prevents multiple LOD meshes from rendering on top of each other
                EvictOtherLODs(x, y, lod);

                if (_tiles.TryGetValue(key, out var existing))
                {
                    // If tile was evicted, re-request generation
                    if (existing.State == TerrainTile.TileState.Evicted ||
                        existing.State == TerrainTile.TileState.Unloaded)
                    {
                        existing.RequestLoad();
                        _generationQueue.Enqueue(existing);
                        _workEvent.Set();
                    }
                    return existing;
                }

                var t = new TerrainTile(x, y, lod);
                _tiles[key] = t;

                // Enqueue for generation
                t.RequestLoad();
                _generationQueue.Enqueue(t);
                _workEvent.Set();

                return t;
            }
        }

        /// <summary>
        /// Start background worker thread for tile generation.
        /// </summary>
        public void StartBackgroundWorker()
        {
            if (_running) return;
            _running = true;
            _workerThread = new Thread(WorkerLoop) { IsBackground = true, Name = "TerrainTileWorker" };
            _workerThread.Start();
        }

        /// <summary>
        /// Stop background worker thread.
        /// </summary>
        public void StopBackgroundWorker()
        {
            _running = false;
            _workEvent.Set();
            _workerThread?.Join(500);
            _workerThread = null;
        }

        /// <summary>
        /// Reset the tile manager - clears all tiles and queues.
        /// </summary>
        public void Reset()
        {
            // Stop worker thread
            StopBackgroundWorker();

            // Clear all tiles and queues
            lock (_tilesLock)
            {
                _tiles.Clear();
                while (_generationQueue.TryDequeue(out _)) { }
                while (_uploadQueue.TryDequeue(out _)) { }
            }

            // Restart worker thread
            StartBackgroundWorker();

            Engine.Utils.DebugLogger.Log("[TileManager] Reset complete - all tiles cleared");
        }

        /// <summary>
        /// Worker thread loop - processes tile generation queue.
        /// </summary>
        private void WorkerLoop()
        {
            while (_running)
            {
                _workEvent.WaitOne(100);
                if (!_running) break;

                while (_running && _generationQueue.TryDequeue(out var tile))
                {
                    try
                    {
                        if (TileGenerator == null) continue;

                        // Generate CPU buffers
                        var result = TileGenerator.Invoke(tile.X, tile.Y, tile.Lod);
                        if (result.vertices != null && result.indices != null)
                        {
                            tile.OnCpuReady(result.vertices, result.indices);
                            _uploadQueue.Enqueue(tile);
                        }
                    }
                    catch (Exception ex)
                    {
                        Engine.Utils.DebugLogger.Log($"[TerrainTileManager] ERROR generating tile ({tile.X},{tile.Y},lod={tile.Lod}): {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Iterate over renderable tiles visible in frustum.
        /// </summary>
        public void ForEachVisible(Func<Vector3, Vector3, bool> frustumTest, Action<TerrainTile> action)
        {
            lock (_tilesLock)
            {
                foreach (var tile in _tiles.Values)
                {
                    if (tile.State != TerrainTile.TileState.Renderable) continue;

                    // Frustum culling check
                    if (!frustumTest(tile.WorldMin, tile.WorldMax)) continue;

                    action(tile);
                }
            }
        }

        /// <summary>
        /// Iterate over all renderable tiles (no frustum culling).
        /// </summary>
        public void ForEachRenderable(Action<TerrainTile> action)
        {
            lock (_tilesLock)
            {
                foreach (var tile in _tiles.Values)
                {
                    if (tile.State == TerrainTile.TileState.Renderable)
                    {
                        try { action(tile); } catch { }
                    }
                }
            }
        }

        /// <summary>
        /// Process multiple tile GPU uploads per frame (budgeted uploads for responsiveness).
        /// </summary>
        public int TryProcessUploads(Action<TerrainTile> uploadAction, int maxUploadsPerFrame = 5)
        {
            int uploaded = 0;
            while (uploaded < maxUploadsPerFrame && _uploadQueue.TryDequeue(out var tile))
            {
                try
                {
                    uploadAction?.Invoke(tile);
                    uploaded++;
                }
                catch (Exception ex)
                {
                    Engine.Utils.DebugLogger.Log($"[TerrainTileManager] ERROR uploading tile: {ex.Message}");
                }
            }
            return uploaded;
        }

        /// <summary>
        /// Process one tile GPU upload per frame (legacy single upload).
        /// </summary>
        public bool TryProcessOneUpload(Action<TerrainTile> uploadAction)
        {
            return TryProcessUploads(uploadAction, 1) > 0;
        }

        /// <summary>
        /// Evict all other LODs for a given tile position (x,y), keeping only the specified LOD.
        /// This ensures only one LOD is active per position, preventing z-fighting/overlapping meshes.
        /// </summary>
        private void EvictOtherLODs(int x, int y, int keepLod)
        {
            // Note: Called from within RequestTile which already holds _tilesLock
            var toEvict = new List<(int, int, int)>();

            // Find all tiles at position (x,y) with different LOD
            foreach (var kv in _tiles)
            {
                var tile = kv.Value;
                if (tile.X == x && tile.Y == y && tile.Lod != keepLod)
                {
                    toEvict.Add(kv.Key);
                }
            }

            // Evict and free GPU resources
            foreach (var key in toEvict)
            {
                if (_tiles.TryGetValue(key, out var tile))
                {
                    try
                    {
                        // Free GPU resources (must be called on GL thread!)
                        if (tile.Vao != 0) OpenTK.Graphics.OpenGL4.GL.DeleteVertexArray(tile.Vao);
                        if (tile.Vbo != 0) OpenTK.Graphics.OpenGL4.GL.DeleteBuffer(tile.Vbo);
                        if (tile.Ebo != 0) OpenTK.Graphics.OpenGL4.GL.DeleteBuffer(tile.Ebo);
                        tile.Evict();
                    }
                    catch { }

                    _tiles.Remove(key);
                }
            }
        }

        /// <summary>
        /// Evict tiles beyond a certain distance from camera.
        /// </summary>
        private void EvictDistantTiles(int centerX, int centerY, int maxRadius)
        {
            lock (_tilesLock)
            {
                var toEvict = new List<(int, int, int)>();

                foreach (var kv in _tiles)
                {
                    var tile = kv.Value;
                    int dx = tile.X - centerX;
                    int dy = tile.Y - centerY;
                    float dist = (float)Math.Sqrt(dx * dx + dy * dy);

                    if (dist > maxRadius)
                    {
                        toEvict.Add(kv.Key);
                    }
                }

                // Evict and free GPU resources
                foreach (var key in toEvict)
                {
                    if (_tiles.TryGetValue(key, out var tile))
                    {
                        try
                        {
                            // Free GPU resources (must be called on GL thread!)
                            if (tile.Vao != 0) OpenTK.Graphics.OpenGL4.GL.DeleteVertexArray(tile.Vao);
                            if (tile.Vbo != 0) OpenTK.Graphics.OpenGL4.GL.DeleteBuffer(tile.Vbo);
                            if (tile.Ebo != 0) OpenTK.Graphics.OpenGL4.GL.DeleteBuffer(tile.Ebo);
                            tile.Evict();
                        }
                        catch { }

                        _tiles.Remove(key);
                    }
                }

                if (toEvict.Count > 0)
                {
                    Engine.Utils.DebugLogger.Log($"[TerrainTileManager] Evicted {toEvict.Count} distant tiles");
                }
            }
        }

        /// <summary>
        /// Update statistics for inspector/debugging.
        /// </summary>
        private void UpdateStats()
        {
            lock (_tilesLock)
            {
                RenderableTiles = _tiles.Values.Count(t => t.State == TerrainTile.TileState.Renderable);
                LoadingTiles = _tiles.Values.Count(t => t.State == TerrainTile.TileState.Loading || t.State == TerrainTile.TileState.ReadyCpu);
            }
        }

        /// <summary>
        /// Get memory usage estimate in MB.
        /// </summary>
        public float GetMemoryUsageMB()
        {
            lock (_tilesLock)
            {
                long bytes = 0;
                foreach (var tile in _tiles.Values)
                {
                    if (tile.VerticesCpu != null) bytes += tile.VerticesCpu.Length * sizeof(float);
                    if (tile.IndicesCpu != null) bytes += tile.IndicesCpu.Length * sizeof(uint);
                    // Estimate GPU memory (VAO/VBO/EBO)
                    if (tile.Vao != 0) bytes += (tile.IndexCount * sizeof(uint)) + (tile.IndexCount * 8 * sizeof(float));
                }
                return bytes / (1024f * 1024f);
            }
        }

        public void Dispose()
        {
            StopBackgroundWorker();
            _workEvent.Dispose();

            // Clean up all tiles
            lock (_tilesLock)
            {
                foreach (var tile in _tiles.Values)
                {
                    try
                    {
                        if (tile.Vao != 0) OpenTK.Graphics.OpenGL4.GL.DeleteVertexArray(tile.Vao);
                        if (tile.Vbo != 0) OpenTK.Graphics.OpenGL4.GL.DeleteBuffer(tile.Vbo);
                        if (tile.Ebo != 0) OpenTK.Graphics.OpenGL4.GL.DeleteBuffer(tile.Ebo);
                    }
                    catch { }
                }
                _tiles.Clear();
            }
        }
    }
}

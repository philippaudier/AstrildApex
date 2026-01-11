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
    /// Handles async generation, GPU uploads, and eviction with cross-fade transitions.
    /// </summary>
    public class TerrainTileManager : IDisposable
    {
        // Concurrent queue for tiles ready to be uploaded to GPU
        private readonly ConcurrentQueue<TerrainTile> _uploadQueue = new();

        // Queue for tiles that need CPU generation
        private readonly ConcurrentQueue<TerrainTile> _generationQueue = new();

        // Queue for GPU resources to delete (must be processed on GL thread)
        private readonly ConcurrentQueue<(int vao, int vbo, int ebo)> _gpuDeletionQueue = new();

        // Active tiles keyed by (x,y,lod) tuple
        private readonly Dictionary<(int x,int y,int lod), TerrainTile> _tiles = new();
        private readonly object _tilesLock = new object();

        // OPTIMIZATION: Secondary index for O(1) LOD eviction instead of O(n) full scan
        // Maps (x,y) → List of tiles at that position (different LODs)
        private readonly Dictionary<(int x, int y), List<TerrainTile>> _tilesByCoord = new();

        // Tiles that are fading out (cross-fade transition)
        private readonly List<TerrainTile> _fadingOutTiles = new();

        // Worker thread for CPU generation
        private Thread? _workerThread = null;
        private readonly AutoResetEvent _workEvent = new(false);
        private volatile bool _running = false;

        // Tile generator delegate - now accepts the TerrainTile so generator can access neighbor info
        public Func<TerrainTile, (float[] vertices, uint[] indices)>? TileGenerator;

        // Vegetation generator delegate (optional)
        public Func<Engine.Components.Terrain, int, int, Dictionary<int, List<OpenTK.Mathematics.Matrix4>>>? VegetationGenerator;

        // Stats for debugging/inspector
        public int LoadedTiles => _tiles.Count;
        public int RenderableTiles { get; private set; }
        public int LoadingTiles { get; private set; }

        // VRAM budget for Unreal-style caching (in MB)
        // Default: 512MB allows ~3000-4000 tiles cached (ultra responsive streaming)
        // Reduce if low VRAM GPU, increase for high-end (up to 2048MB)
        public float VramBudgetMB { get; set; } = 512f;

        public TerrainTileManager()
        {
        }

        /// <summary>
        /// Request tiles around camera position with intelligent LOD selection.
        /// PHASE 1: Calculate desired LODs based on distance
        /// PHASE 2: Adjust LODs to prevent seams (max 1 LOD difference between neighbors)
        /// PHASE 3: Request tiles with final adjusted LODs
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
            // CRITICAL: Use concentric circles, not squares!
            // Calculate max distance in meters for rendering (not tile units)
            float maxRenderDistance = radiusTiles * tileWorldSize * 4f;  // e.g., 3 tiles * 100m * 4 = 1200m
            int maxTileRadius = (int)Math.Ceiling(maxRenderDistance / tileWorldSize) + 1;  // +1 for safety

            // PHASE 1: Calculate desired LOD for each tile based on REAL distance from camera
            var desiredLods = new System.Collections.Generic.Dictionary<(int, int), int>();
            var tileDistances = new System.Collections.Generic.Dictionary<(int, int), float>(); // Store distances for priority sorting

            for (int dy = -maxTileRadius; dy <= maxTileRadius; dy++)
            {
                for (int dx = -maxTileRadius; dx <= maxTileRadius; dx++)
                {
                    int tx = centerTileX + dx;
                    int ty = centerTileY + dy;

                    // CRITICAL: Calculate REAL distance from camera to CLOSEST point on tile
                    // This ensures circular LOD rings, not square patterns
                    float tileMinX = tx * tileWorldSize;
                    float tileMaxX = (tx + 1) * tileWorldSize;
                    float tileMinZ = ty * tileWorldSize;
                    float tileMaxZ = (ty + 1) * tileWorldSize;

                    // Closest point on tile to camera (clamped to tile bounds)
                    float closestX = Math.Max(tileMinX, Math.Min(camWorldX, tileMaxX));
                    float closestZ = Math.Max(tileMinZ, Math.Min(camWorldZ, tileMaxZ));
                    
                    // Real distance from camera to closest tile point (in meters)
                    float realDistX = closestX - camWorldX;
                    float realDistZ = closestZ - camWorldZ;
                    float realDistMeters = (float)Math.Sqrt(realDistX * realDistX + realDistZ * realDistZ);

                    // Skip tiles beyond max render distance (TRUE CIRCLE, not square)
                    if (realDistMeters > maxRenderDistance) continue;

                    // Store distance for priority sorting
                    tileDistances[(tx, ty)] = realDistMeters;

                    // Check if tile already exists at this position with any LOD
                    var coordKey = (tx, ty);
                    TerrainTile? existingTile = null;
                    int currentLod = -1;
                    
                    lock (_tilesLock)
                    {
                        if (_tilesByCoord.TryGetValue(coordKey, out var list) && list.Count > 0)
                        {
                            existingTile = list[0]; // Get first (should be only one after EvictOtherLODs)
                            currentLod = existingTile.Lod;
                        }
                    }

                    // Determine target LOD based on REAL distance in meters with HYSTERESIS
                    // Concentric circles: LOD0: 0-200m, LOD1: 200-500m, LOD2: 500-1000m, LOD3: 1000m+
                    // Hysteresis prevents ping-pong: add 15% buffer zone when switching LODs
                    int targetLod = 0;
                    
                    if (currentLod == -1)
                    {
                        // No existing tile, use standard thresholds
                        if (realDistMeters > 1000f) targetLod = 3;
                        else if (realDistMeters > 500f) targetLod = 2;
                        else if (realDistMeters > 200f) targetLod = 1;
                        else targetLod = 0;
                    }
                    else
                    {
                        // Tile exists, apply hysteresis to prevent oscillation
                        // When moving closer: switch at threshold - 15%
                        // When moving away: switch at threshold + 15%
                        
                        if (currentLod == 0)
                        {
                            // Currently LOD0, only switch to LOD1 when significantly beyond threshold
                            if (realDistMeters > 230f) targetLod = 1;  // 200 + 15%
                            else targetLod = 0;
                        }
                        else if (currentLod == 1)
                        {
                            if (realDistMeters > 575f) targetLod = 2;  // 500 + 15%
                            else if (realDistMeters < 170f) targetLod = 0;  // 200 - 15%
                            else targetLod = 1;  // Stay at LOD1
                        }
                        else if (currentLod == 2)
                        {
                            if (realDistMeters > 1150f) targetLod = 3;  // 1000 + 15%
                            else if (realDistMeters < 425f) targetLod = 1;  // 500 - 15%
                            else targetLod = 2;  // Stay at LOD2
                        }
                        else if (currentLod == 3)
                        {
                            if (realDistMeters < 850f) targetLod = 2;  // 1000 - 15%
                            else targetLod = 3;  // Stay at LOD3
                        }
                    }

                    // Clamp LOD to max allowed
                    targetLod = Math.Min(targetLod, terrain.StreamingMaxLOD);

                    desiredLods[coordKey] = targetLod;
                }
            }

            // PHASE 2: Adjust LODs to prevent seams (iterative smoothing)
            // Rule: A tile's LOD can be at most 1 higher than its neighbors' LODs
            // This prevents T-junctions and ensures seamless borders
            // OPTIMIZED: Use in-place updates with change tracking to avoid dictionary copies

            var changedTiles = new System.Collections.Generic.HashSet<(int, int)>();
            int maxIterations = 10;  // Prevent infinite loops
            int iteration = 0;

            // Pre-fetch neighbor tiles once (batch lock acquisition)
            var neighborTiles = new System.Collections.Generic.Dictionary<(int, int), int>();
            lock (_tilesLock)
            {
                foreach (var kvp in desiredLods)
                {
                    var (tx, ty) = kvp.Key;
                    var neighbors = new[] {
                        (tx, ty - 1), (tx, ty + 1), (tx - 1, ty), (tx + 1, ty)
                    };

                    foreach (var neighbor in neighbors)
                    {
                        if (!desiredLods.ContainsKey(neighbor) &&
                            _tilesByCoord.TryGetValue(neighbor, out var list) && list.Count > 0)
                        {
                            neighborTiles.TryAdd(neighbor, list[0].Lod);
                        }
                    }
                }
            }

            // Mark all tiles as potentially changed for first iteration
            foreach (var key in desiredLods.Keys)
                changedTiles.Add(key);

            while (changedTiles.Count > 0 && iteration < maxIterations)
            {
                iteration++;
                var tilesToCheck = new System.Collections.Generic.List<(int, int)>(changedTiles);
                changedTiles.Clear();

                foreach (var (tx, ty) in tilesToCheck)
                {
                    if (!desiredLods.TryGetValue((tx, ty), out int myLod))
                        continue;

                    // Check 4 neighbors (N, S, E, W)
                    var neighbors = new[] {
                        (tx, ty - 1),  // North
                        (tx, ty + 1),  // South
                        (tx - 1, ty),  // West
                        (tx + 1, ty)   // East
                    };

                    int minNeighborLod = int.MaxValue;
                    bool hasNeighbor = false;

                    foreach (var neighbor in neighbors)
                    {
                        int neighborLod = int.MaxValue;

                        // Check desiredLods first (no lock needed)
                        if (desiredLods.TryGetValue(neighbor, out int desiredNeighborLod))
                        {
                            neighborLod = desiredNeighborLod;
                            hasNeighbor = true;
                        }
                        // Check pre-fetched neighbor tiles
                        else if (neighborTiles.TryGetValue(neighbor, out int renderedLod))
                        {
                            neighborLod = renderedLod;
                            hasNeighbor = true;
                        }

                        if (neighborLod < minNeighborLod)
                        {
                            minNeighborLod = neighborLod;
                        }
                    }

                    // If we have at least one neighbor and our LOD is > neighbor + 1, reduce it
                    if (hasNeighbor && minNeighborLod != int.MaxValue && myLod > minNeighborLod + 1)
                    {
                        int newLod = minNeighborLod + 1;
                        desiredLods[(tx, ty)] = newLod;

                        // Mark this tile and its neighbors as changed for next iteration
                        changedTiles.Add((tx, ty));
                        foreach (var neighbor in neighbors)
                        {
                            if (desiredLods.ContainsKey(neighbor))
                                changedTiles.Add(neighbor);
                        }
                    }
                }
            }

            // PHASE 3: Request tiles with adjusted LODs
            foreach (var kvp in desiredLods)
            {
                var (tx, ty) = kvp.Key;
                int finalLod = kvp.Value;

                // Check if LOD changed from current
                int currentLod = -1;
                lock (_tilesLock)
                {
                    if (_tilesByCoord.TryGetValue((tx, ty), out var list) && list.Count > 0)
                    {
                        currentLod = list[0].Lod;
                    }
                }

                // Build neighbor LOD dictionary (N,S,W,E) to pass to generator for stitching
                var neighborOffsets = new[] { (tx, ty - 1), (tx, ty + 1), (tx - 1, ty), (tx + 1, ty) };
                var neighborDict = new Dictionary<(int, int), int>();
                foreach (var nb in neighborOffsets)
                {
                    if (desiredLods.TryGetValue(nb, out int nbLod))
                    {
                        neighborDict[nb] = nbLod;
                    }
                    else
                    {
                        lock (_tilesLock)
                        {
                            if (_tilesByCoord.TryGetValue(nb, out var nlist) && nlist.Count > 0)
                            {
                                neighborDict[nb] = nlist[0].Lod;
                            }
                        }
                    }
                }

                // Request tile if: LOD changed or tile doesn't exist
                // Note: Skirts hide LOD seams, so we don't need to regenerate when neighbors change
                if (currentLod == -1 || currentLod != finalLod)
                {
                    float distance = tileDistances.TryGetValue((tx, ty), out float dist) ? dist : float.MaxValue;
                    RequestTile(tx, ty, finalLod, neighborDict, distance);
                }
            }

            // UNREAL-STYLE EVICTION: Keep tiles in VRAM for instant reappearance on camera rotation
            // Evict only when VERY far (2× max render distance with large margin for hysteresis)
            // CRITICAL: Large margin prevents thrashing (tiles flickering when near eviction boundary)
            float evictionDistance = maxRenderDistance * 2.5f;  // 150% beyond render distance
            int evictionTileRadius = (int)Math.Ceiling(evictionDistance / tileWorldSize);
            EvictDistantTiles(centerTileX, centerTileY, evictionTileRadius);

            // Update stats
            UpdateStats();
        }

        /// <summary>
        /// Request generation of a single tile.
        /// CRITICAL: Only one LOD can be active for a given (x,y) position at a time.
        /// </summary>
        public TerrainTile RequestTile(int x, int y, int lod, System.Collections.Generic.Dictionary<(int, int), int>? neighborLods = null, float distanceToCamera = float.MaxValue)
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
                        existing.RequestDistance = distanceToCamera; // Update distance for priority
                        existing.RequestLoad();
                        _generationQueue.Enqueue(existing);
                        _workEvent.Set();
                    }
                    // NOTE: Removed ResetTransition() call that was causing flickering
                    // RequestTile should NOT be called on already-renderable tiles with same LOD
                    // If this happens, it's a bug in the streaming logic
                    else if (existing.State == TerrainTile.TileState.Renderable)
                    {
                        // Tile already renderable, just return it (no transition reset)
                        // This should be rare - only when LOD didn't actually change
                    }
                    return existing;
                }

                var t = new TerrainTile(x, y, lod);
                // Attach neighbor info for CPU generator to use for stitching
                t.NeighborLods = neighborLods;
                t.RequestDistance = distanceToCamera; // Store distance for priority sorting
                _tiles[key] = t;

                // Add to secondary index for O(1) LOD lookup
                var coordKey = (x, y);
                if (!_tilesByCoord.TryGetValue(coordKey, out var list))
                {
                    list = new List<TerrainTile>();
                    _tilesByCoord[coordKey] = list;
                }
                list.Add(t);

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
                _tilesByCoord.Clear();  // Clear secondary index
                _fadingOutTiles.Clear();  // Clear fading tiles
                while (_generationQueue.TryDequeue(out _)) { }
                while (_uploadQueue.TryDequeue(out _)) { }
            }

            // Restart worker thread
            StartBackgroundWorker();

            Engine.Utils.DebugLogger.Log("[TileManager] Reset complete - all tiles cleared");
        }

        /// <summary>
        /// Worker thread loop - processes tile generation queue.
        /// OPTIMIZATION: Tiles are processed by distance priority (closest first)
        /// </summary>
        private void WorkerLoop()
        {
            while (_running)
            {
                _workEvent.WaitOne(100);
                if (!_running) break;

                // Dequeue all pending tiles into a list
                var pendingTiles = new List<TerrainTile>();
                while (_generationQueue.TryDequeue(out var tile))
                {
                    pendingTiles.Add(tile);
                }

                // Sort by distance to camera (closest first) for optimal streaming
                pendingTiles.Sort((a, b) => a.RequestDistance.CompareTo(b.RequestDistance));

                // DEBUG: Log first 3 tiles to verify priority sorting
                if (pendingTiles.Count > 0)
                {
                    int logCount = Math.Min(3, pendingTiles.Count);
                    for (int i = 0; i < logCount; i++)
                    {
                        var t = pendingTiles[i];
                        Engine.Utils.DebugLogger.Log($"[TileManager] Processing tile ({t.X},{t.Y}) LOD{t.Lod} at {t.RequestDistance:F1}m (priority #{i+1}/{pendingTiles.Count})");
                    }
                }

                // Process tiles in priority order
                foreach (var tile in pendingTiles)
                {
                    if (!_running) break;

                    try
                    {
                        if (TileGenerator == null) continue;

                        // Generate CPU buffers (generator now receives the TerrainTile so it can access NeighborLods)
                        var result = TileGenerator.Invoke(tile);
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
                
                // Also render fading-out tiles
                foreach (var tile in _fadingOutTiles)
                {
                    if (!frustumTest(tile.WorldMin, tile.WorldMax)) continue;
                    action(tile);
                }
            }
        }

        /// <summary>
        /// Iterate over all renderable tiles including those fading out (no frustum culling).
        /// </summary>
        public void ForEachRenderable(Action<TerrainTile> action)
        {
            lock (_tilesLock)
            {
                // Render active tiles
                foreach (var tile in _tiles.Values)
                {
                    if (tile.State == TerrainTile.TileState.Renderable)
                    {
                        try { action(tile); } catch { }
                    }
                }
                
                // Also render fading-out tiles (cross-fade effect)
                foreach (var tile in _fadingOutTiles)
                {
                    try { action(tile); } catch { }
                }
            }
        }

        /// <summary>
        /// Clean up completed fade-out tiles.
        /// Call this once per frame AFTER rendering (UpdateTransition is called during render).
        /// </summary>
        public void UpdateFadingTiles(float deltaTime)
        {
            lock (_tilesLock)
            {
                for (int i = _fadingOutTiles.Count - 1; i >= 0; i--)
                {
                    var tile = _fadingOutTiles[i];

                    // NOTE: UpdateTransition is called in TerrainRenderer for all tiles
                    // We just check if fade is complete here

                    // If fade complete, clean up GPU resources and remove
                    if (tile.IsFadeOutComplete)
                    {
                        if (tile.Vao != 0 || tile.Vbo != 0 || tile.Ebo != 0)
                        {
                            _gpuDeletionQueue.Enqueue((tile.Vao, tile.Vbo, tile.Ebo));
                        }
                        tile.Evict();
                        _fadingOutTiles.RemoveAt(i);
                    }
                }
            }
        }

        /// <summary>
        /// Process multiple tile GPU uploads per frame (budgeted uploads for responsiveness).
        /// OPTIMIZED: Increased from 5 to 20 for faster streaming when camera moves.
        /// </summary>
        public int TryProcessUploads(Action<TerrainTile> uploadAction, int maxUploadsPerFrame = 20)
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
        /// Uses cross-fade: old tiles fade out while new tile fades in.
        /// OPTIMIZED: Uses secondary index for O(1) lookup instead of O(n) full dictionary scan.
        /// </summary>
        private void EvictOtherLODs(int x, int y, int keepLod)
        {
            // Note: Called from within RequestTile which already holds _tilesLock
            var coordKey = (x, y);

            // OPTIMIZATION: Use secondary index for O(1) lookup (was O(n) full scan)
            if (!_tilesByCoord.TryGetValue(coordKey, out var tilesAtCoord))
                return; // No tiles at this position

            // Separate tiles into fade-out (renderable) and immediate evict (non-renderable)
            var toFadeOut = new List<TerrainTile>();
            var toEvictNow = new List<TerrainTile>();
            
            foreach (var tile in tilesAtCoord)
            {
                if (tile.Lod != keepLod && tile.State == TerrainTile.TileState.Renderable && !tile.IsFadingOut)
                {
                    toFadeOut.Add(tile);
                }
                else if (tile.Lod != keepLod && tile.State != TerrainTile.TileState.Renderable)
                {
                    // Non-renderable tiles can be evicted immediately
                    toEvictNow.Add(tile);
                }
            }

            // Start fade-out for renderable tiles (they'll be cleaned up when fade completes)
            foreach (var tile in toFadeOut)
            {
                tile.StartFadeOut();
                _fadingOutTiles.Add(tile);
                
                // Remove from main index but keep in _fadingOutTiles for rendering
                var key = (tile.X, tile.Y, tile.Lod);
                _tiles.Remove(key);
                tilesAtCoord.Remove(tile);
            }

            // Immediately evict non-renderable tiles
            foreach (var tile in toEvictNow)
            {
                var key = (tile.X, tile.Y, tile.Lod);

                // Queue GPU resource deletion for main thread (thread-safe)
                if (tile.Vao != 0 || tile.Vbo != 0 || tile.Ebo != 0)
                {
                    _gpuDeletionQueue.Enqueue((tile.Vao, tile.Vbo, tile.Ebo));
                }

                tile.Evict();
                _tiles.Remove(key);
                tilesAtCoord.Remove(tile);
            }

            // Clean up empty coord entry
            if (tilesAtCoord.Count == 0)
            {
                _tilesByCoord.Remove(coordKey);
            }
        }

        /// <summary>
        /// Evict tiles beyond a certain distance from camera.
        /// UNREAL-STYLE: Only evict if over VRAM budget OR very far.
        /// </summary>
        private void EvictDistantTiles(int centerX, int centerY, int maxRadius)
        {
            lock (_tilesLock)
            {
                // Check current VRAM usage
                float currentVramMB = GetMemoryUsageMB();

                // HYSTERESIS: Use different thresholds to prevent thrashing
                // Start evicting at 95% budget, stop when below 85%
                bool shouldEvict = currentVramMB > VramBudgetMB * 0.95f;

                var toEvict = new List<(int, int, int)>();

                foreach (var kv in _tiles)
                {
                    var tile = kv.Value;
                    int dx = tile.X - centerX;
                    int dy = tile.Y - centerY;
                    float distSq = dx * dx + dy * dy;

                    // OPTIMIZATION: Only evict if BOTH conditions met:
                    // 1. Tile is beyond maxRadius (very far from camera)
                    // 2. We're over VRAM budget (need to free memory)
                    // HYSTERESIS: Add 10% margin to maxRadius to prevent flickering
                    float hysteresisMargin = maxRadius * 0.1f;
                    bool tooFar = distSq > (maxRadius + hysteresisMargin) * (maxRadius + hysteresisMargin);

                    if (tooFar && shouldEvict)
                    {
                        toEvict.Add(kv.Key);
                    }
                }

                // Evict and free GPU resources
                foreach (var key in toEvict)
                {
                    if (_tiles.TryGetValue(key, out var tile))
                    {
                        // Queue GPU resource deletion for main thread (thread-safe)
                        if (tile.Vao != 0 || tile.Vbo != 0 || tile.Ebo != 0)
                        {
                            _gpuDeletionQueue.Enqueue((tile.Vao, tile.Vbo, tile.Ebo));
                        }

                        tile.Evict();
                        _tiles.Remove(key);

                        // Remove from secondary index
                        var coordKey = (tile.X, tile.Y);
                        if (_tilesByCoord.TryGetValue(coordKey, out var list))
                        {
                            list.Remove(tile);
                            if (list.Count == 0)
                            {
                                _tilesByCoord.Remove(coordKey);
                            }
                        }
                    }
                }

                if (toEvict.Count > 0)
                {
                    Engine.Utils.DebugLogger.Log($"[TerrainTileManager] Evicted {toEvict.Count} distant tiles (VRAM: {currentVramMB:F1}/{VramBudgetMB}MB)");
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
        /// Process queued GPU resource deletions. MUST be called on the main GL thread.
        /// Call this once per frame from your rendering loop.
        /// </summary>
        public void ProcessGpuDeletions()
        {
            int processed = 0;
            const int maxPerFrame = 50; // Limit deletions per frame to avoid stuttering

            while (processed < maxPerFrame && _gpuDeletionQueue.TryDequeue(out var handles))
            {
                try
                {
                    if (handles.vao != 0) OpenTK.Graphics.OpenGL4.GL.DeleteVertexArray(handles.vao);
                    if (handles.vbo != 0) OpenTK.Graphics.OpenGL4.GL.DeleteBuffer(handles.vbo);
                    if (handles.ebo != 0) OpenTK.Graphics.OpenGL4.GL.DeleteBuffer(handles.ebo);
                    processed++;
                }
                catch (Exception ex)
                {
                    Engine.Utils.DebugLogger.Log($"[TerrainTileManager] Failed to delete GPU resources: {ex.Message}");
                }
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
                
                // Also clean up fading-out tiles
                foreach (var tile in _fadingOutTiles)
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
                _tilesByCoord.Clear();
                _fadingOutTiles.Clear();
            }
        }
    }
}

using System;
using System.Collections.Generic;
using OpenTK.Mathematics;
using Engine.Components;
using Engine.Assets;

namespace Engine.Rendering
{
    /// <summary>
    /// Generates vegetation instances for terrain tiles.
    /// Uses deterministic seeding per tile for consistent, seamless vegetation across tiles.
    /// </summary>
    public static class VegetationGenerator
    {
        /// <summary>
        /// Generate vegetation instances for a specific terrain tile.
        /// Returns a dictionary of instances per vegetation layer.
        /// </summary>
        public static Dictionary<int, List<Matrix4>> GenerateVegetationForTile(
            Engine.Components.Terrain terrain,
            int tileX, int tileY,
            float tileWorldSize)
        {
            var result = new Dictionary<int, List<Matrix4>>();

            if (terrain.VegetationLayers == null || terrain.VegetationLayers.Length == 0)
                return result;

            float startX = tileX * tileWorldSize;
            float startZ = tileY * tileWorldSize;

            // Generate instances for each vegetation layer
            for (int layerIndex = 0; layerIndex < terrain.VegetationLayers.Length; layerIndex++)
            {
                var layer = terrain.VegetationLayers[layerIndex];
                if (layer == null || layer.Density <= 0) continue;

                var instances = GenerateLayerInstances(
                    terrain, layer, layerIndex,
                    tileX, tileY,
                    startX, startZ, tileWorldSize);

                if (instances.Count > 0)
                {
                    result[layerIndex] = instances;
                }
            }

            return result;
        }

        /// <summary>
        /// Generate instances for a single vegetation layer within a tile.
        /// </summary>
        private static List<Matrix4> GenerateLayerInstances(
            Engine.Components.Terrain terrain,
            VegetationLayer layer,
            int layerIndex,
            int tileX, int tileY,
            float startX, float startZ,
            float tileWorldSize)
        {
            var instances = new List<Matrix4>();

            // Deterministic seed per tile and layer
            int tileSeed = HashCode.Combine(
                terrain.ProceduralSeed,
                tileX, tileY,
                layerIndex
            );
            Random rng = new Random(tileSeed);

            // === REGION-BASED DISTRIBUTION (consistent with Single Terrain) ===
            // Group tiles into regions to maintain density consistency
            // Target region size: 1000x1000m (reference size for Single Terrain)
            const float targetRegionWorldSize = 1000f; // meters

            // Calculate how many tiles fit in 1000m (adapts to any tile size)
            int regionSize = Math.Max(1, (int)Math.Round(targetRegionWorldSize / tileWorldSize));

            // Example results:
            // - Tiles 100m → regionSize = 10 → Region = 1000x1000m
            // - Tiles 256m → regionSize = 4  → Region = 1024x1024m
            // - Tiles 50m  → regionSize = 20 → Region = 1000x1000m

            // Calculate which region this tile belongs to
            int regionX = (int)Math.Floor((double)tileX / regionSize);
            int regionY = (int)Math.Floor((double)tileY / regionSize);

            // Position of this tile within its region (0 to regionSize-1)
            int localTileX = ((tileX % regionSize) + regionSize) % regionSize;
            int localTileY = ((tileY % regionSize) + regionSize) % regionSize;

            // === REGION SPAWN CHANCE ===
            // Use region seed to determine if this layer spawns in this region
            int regionSeed = HashCode.Combine(terrain.ProceduralSeed, regionX, regionY, layerIndex, "region");
            Random regionRng = new Random(regionSeed);

            float regionSpawnRoll = (float)regionRng.NextDouble();
            if (regionSpawnRoll > layer.TileSpawnChance)
            {
                // This entire region doesn't have this vegetation type
                return instances; // Empty list
            }

            // === CALCULATE DENSITY FOR THIS TILE ===
            // Total area of the region
            float regionWorldSize = tileWorldSize * regionSize;
            float regionArea = regionWorldSize * regionWorldSize;

            // Expected total instances in the entire region (SAME as Single Terrain)
            // Density is "instances per 100x100m area" = per 10,000 m²
            float referenceArea = 100f * 100f; // 10,000 m²
            int targetRegionInstances = (int)((regionArea / referenceArea) * layer.Density);

            // Distribute uniformly across all tiles in the region
            int tilesPerRegion = regionSize * regionSize;
            float targetPerTile = (float)targetRegionInstances / tilesPerRegion;

            // Apply natural variation using continuous noise instead of per-tile random
            // This creates smooth variation across tiles (like Single Terrain) instead of abrupt changes
            float densityMultiplier = 1.0f;
            if (layer.DensityVariation > 0f)
            {
                // Use region + tile position for smooth variation
                float noiseX = (regionX * regionSize + localTileX) * 0.1f;
                float noiseY = (regionY * regionSize + localTileY) * 0.1f;

                // Simple 2D hash-based noise for smooth variation
                float noiseValue = SmoothNoise(noiseX, noiseY, terrain.ProceduralSeed + layerIndex);

                // Map noise [-1, 1] to density multiplier [1-variation, 1+variation]
                densityMultiplier = 1.0f + (noiseValue * layer.DensityVariation);
                densityMultiplier = Math.Max(0.1f, densityMultiplier);
            }

            int targetInstances = (int)(targetPerTile * densityMultiplier);

            // Use 3x attempts for rejection sampling (height/slope/distance filters)
            // Same as Single Terrain to account for rejected placements
            int attempts = targetInstances * 3;
            attempts = Math.Max(0, attempts);

            // === SIZE VARIATION PER TILE ===
            // Apply per-tile size offset (some tiles have larger/smaller trees)
            float tileSizeOffset = 0f;
            if (layer.SizeVariation > 0f)
            {
                float sizeVariationAmount = (float)rng.NextDouble() * 2.0f - 1.0f; // -1 to +1
                tileSizeOffset = sizeVariationAmount * layer.SizeVariation;
            }

            // OPTIMIZATION: Create spatial grid for O(1) neighbor queries (replaces O(n²) IsTooCloseToOthers)
            // Cell size = minDistance ensures we only check relevant neighbors
            // Reduces 1,000,000 comparisons to ~20 for typical density
            float cellSize = Math.Max(layer.MinDistance, tileWorldSize / 10f); // Min 10x10 grid
            var spatialGrid = new SpatialGrid(cellSize, startX, startZ);

            // Generate instances
            for (int i = 0; i < attempts; i++)
            {
                // Random position within tile
                float localX = (float)rng.NextDouble() * tileWorldSize;
                float localZ = (float)rng.NextDouble() * tileWorldSize;

                float worldX = startX + localX;
                float worldZ = startZ + localZ;

                // Sample height from infinite terrain
                float worldY = Engine.Rendering.Terrain.Tile.TileCpuGenerator.SampleHeightInfinite(terrain, worldX, worldZ);

                // Check placement constraints
                if (!IsValidPlacement(terrain, worldX, worldY, worldZ, layer, rng))
                    continue;

                // OPTIMIZED: Use spatial grid for O(1) distance check (was O(n) linear scan)
                // Checks only ~20 nearby instances instead of all 1000+
                if (layer.MinDistance > 0 && spatialGrid.IsTooClose(worldX, worldY, worldZ, layer.MinDistance))
                    continue;

                // Create instance matrix (with per-tile size variation)
                Matrix4 matrix = CreateVegetationMatrix(terrain, worldX, worldY, worldZ, layer, rng, tileSizeOffset);
                instances.Add(matrix);

                // Add to spatial grid for future distance checks
                spatialGrid.Add(matrix);
            }

            // === POST-PROCESSING (same as Single Terrain) ===
            // Apply prefab scale and mesh pivot correction
            ApplyPostProcessing(layer, instances);

            return instances;
        }

        /// <summary>
        /// Apply post-processing to vegetation instances:
        /// 1. Apply prefab root scale to matrix columns
        /// 2. Compute mesh minY and adjust Y translation for proper ground placement
        /// This matches Single Terrain's post-processing exactly.
        /// </summary>
        private static void ApplyPostProcessing(VegetationLayer layer, List<Matrix4> instances)
        {
            if (instances == null || instances.Count == 0) return;

            try
            {
                // 1) Apply prefab root scale if layer references a prefab
                if (layer.PrefabGuid.HasValue)
                {
                    try
                    {
                        var prefab = Engine.Assets.AssetDatabase.LoadPrefab(layer.PrefabGuid.Value);
                        if (prefab?.RootEntity != null)
                        {
                            var ls = prefab.RootEntity.LocalScale;
                            if (ls != null && ls.Length == 3 &&
                                !(Math.Abs(ls[0] - 1f) < 1e-6 && Math.Abs(ls[1] - 1f) < 1e-6 && Math.Abs(ls[2] - 1f) < 1e-6))
                            {
                                // Apply prefab base scale to the rotation/scale columns only.
                                // Multiplying the full 4x4 matrix by a scale matrix can incorrectly scale the translation.
                                float sx = ls[0];
                                float sy = ls[1];
                                float sz = ls[2];
                                for (int i = 0; i < instances.Count; i++)
                                {
                                    var m = instances[i];
                                    // Scale column 0 (basis X)
                                    m.M11 *= sx; m.M12 *= sx; m.M13 *= sx;
                                    // Scale column 1 (basis Y)
                                    m.M21 *= sy; m.M22 *= sy; m.M23 *= sy;
                                    // Scale column 2 (basis Z)
                                    m.M31 *= sz; m.M32 *= sz; m.M33 *= sz;
                                    // Preserve translation (M41..M43)
                                    instances[i] = m;
                                }
                            }
                        }
                    }
                    catch { }
                }

                // 2) Pivot correction: shift instances so mesh base (minY) sits on terrain
                // Determine model GUID: prefer explicit ModelGuid, otherwise try to extract from prefab
                Guid? modelGuid = layer.ModelGuid;
                if ((!modelGuid.HasValue || modelGuid == Guid.Empty) && layer.PrefabGuid.HasValue)
                {
                    try
                    {
                        if (Engine.Assets.AssetDatabase.TryGet(layer.PrefabGuid.Value, out var rec) &&
                            System.IO.File.Exists(rec.Path))
                        {
                            var prefab = Engine.Assets.PrefabAsset.Load(rec.Path);
                            if (prefab?.RootEntity != null)
                            {
                                // Look for MeshRendererComponent in root or children JSON
                                System.Text.Json.JsonElement? meshRendererJson = null;
                                if (prefab.RootEntity.Components != null &&
                                    prefab.RootEntity.Components.TryGetValue("MeshRendererComponent", out var rm))
                                    meshRendererJson = rm;

                                if (!meshRendererJson.HasValue)
                                {
                                    foreach (var child in prefab.RootEntity.Children)
                                    {
                                        if (child.Components != null &&
                                            child.Components.TryGetValue("MeshRendererComponent", out var cm))
                                        {
                                            meshRendererJson = cm;
                                            break;
                                        }
                                    }
                                }

                                if (meshRendererJson.HasValue &&
                                    meshRendererJson.Value.TryGetProperty("customMeshGuid", out var guidElem))
                                {
                                    var guidStr = guidElem.GetString();
                                    if (!string.IsNullOrEmpty(guidStr) && Guid.TryParse(guidStr, out var parsed))
                                        modelGuid = parsed;
                                }
                            }
                        }
                    }
                    catch { }
                }

                // If we have a mesh asset, compute minY for relevant submesh(es) and adjust instance translations
                if (modelGuid.HasValue && modelGuid != Guid.Empty)
                {
                    try
                    {
                        var meshAsset = Engine.Assets.AssetDatabase.LoadMeshAsset(modelGuid.Value);
                        if (meshAsset != null)
                        {
                            // Determine which submeshes to consider
                            var submeshIndices = new List<int>();
                            if (layer.SubmeshIndex == -1)
                            {
                                for (int s = 0; s < meshAsset.SubMeshes.Count; s++)
                                    submeshIndices.Add(s);
                                if (submeshIndices.Count == 0)
                                    submeshIndices.Add(0);
                            }
                            else
                            {
                                int sidx = Math.Max(0, Math.Min(layer.SubmeshIndex, meshAsset.SubMeshes.Count - 1));
                                submeshIndices.Add(sidx);
                            }

                            // Compute global minY across chosen submeshes (mesh-local space)
                            float globalMinY = float.MaxValue;
                            foreach (var s in submeshIndices)
                            {
                                var sub = meshAsset.SubMeshes[s];
                                for (int vi = 0; vi < sub.Vertices.Length; vi += 8)
                                {
                                    float vy = sub.Vertices[vi + 1];
                                    if (vy < globalMinY) globalMinY = vy;
                                }
                            }

                            if (globalMinY != float.MaxValue && Math.Abs(globalMinY) > 1e-6)
                            {
                                // Adjust each instance's translation by -minY * instanceScaleY
                                for (int i = 0; i < instances.Count; i++)
                                {
                                    var m = instances[i];
                                    // Extract Y-scale from matrix columns (length of second column)
                                    float scaleY = new OpenTK.Mathematics.Vector3(m.M21, m.M22, m.M23).Length;
                                    // Adjust translation Y (M42) so mesh base sits on terrain
                                    m.M42 -= globalMinY * scaleY;
                                    instances[i] = m;
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        /// <summary>
        /// Check if placement position is valid according to layer constraints.
        /// </summary>
        private static bool IsValidPlacement(
            Engine.Components.Terrain terrain,
            float worldX, float worldY, float worldZ,
            VegetationLayer layer,
            Random rng)
        {
            // Height constraints (normalized [0,1] against terrain height)
            float normalizedHeight = worldY / terrain.TerrainHeight;
            if (normalizedHeight < layer.MinHeight || normalizedHeight > layer.MaxHeight)
                return false;

            // Slope constraints
            if (layer.MinSlope > 0 || layer.MaxSlope < 90)
            {
                float slope = CalculateSlope(terrain, worldX, worldZ);
                if (slope < layer.MinSlope || slope > layer.MaxSlope)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Calculate terrain slope at world position (in degrees).
        /// </summary>
        private static float CalculateSlope(Engine.Components.Terrain terrain, float worldX, float worldZ)
        {
            float delta = 0.5f;

            float h0 = Engine.Rendering.Terrain.Tile.TileCpuGenerator.SampleHeightInfinite(terrain, worldX, worldZ);
            float hX = Engine.Rendering.Terrain.Tile.TileCpuGenerator.SampleHeightInfinite(terrain, worldX + delta, worldZ);
            float hZ = Engine.Rendering.Terrain.Tile.TileCpuGenerator.SampleHeightInfinite(terrain, worldX, worldZ + delta);

            float dx = hX - h0;
            float dz = hZ - h0;

            float slopeRad = (float)Math.Atan2(Math.Sqrt(dx * dx + dz * dz), delta);
            return slopeRad * 57.29577951f; // radians to degrees
        }

        /// <summary>
        /// Check if position is too close to existing instances.
        /// </summary>
        private static bool IsTooCloseToOthers(
            List<Matrix4> existingInstances,
            float worldX, float worldY, float worldZ,
            float minDistance)
        {
            float minDistSq = minDistance * minDistance;

            foreach (var matrix in existingInstances)
            {
                float dx = matrix.M41 - worldX;
                float dy = matrix.M42 - worldY;
                float dz = matrix.M43 - worldZ;

                float distSq = dx * dx + dy * dy + dz * dz;
                if (distSq < minDistSq)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Create transformation matrix for vegetation instance.
        /// Matches Single Terrain's matrix creation approach exactly.
        /// </summary>
        private static Matrix4 CreateVegetationMatrix(
            Engine.Components.Terrain terrain,
            float worldX, float worldY, float worldZ,
            VegetationLayer layer,
            Random rng,
            float tileSizeOffset = 0f)
        {
            // Uniform scale variation (per-instance)
            float scale = layer.MinScale + (float)rng.NextDouble() * (layer.MaxScale - layer.MinScale);

            // Apply per-tile size variation offset
            // This makes entire tiles have slightly larger/smaller vegetation on average
            if (tileSizeOffset != 0f)
            {
                float scaleRange = layer.MaxScale - layer.MinScale;
                scale += tileSizeOffset * scaleRange;
                // Clamp to prevent negative or extreme scales
                scale = Math.Max(0.1f, Math.Min(scale, layer.MaxScale * 1.5f));
            }

            // Random rotation around Y axis (if enabled)
            float rotationY = layer.RandomRotation ? (float)rng.NextDouble() * MathHelper.TwoPi : 0f;
            Quaternion rotation = Quaternion.FromAxisAngle(Vector3.UnitY, rotationY);

            // Calculate alignment with terrain normal using quaternions (same as Single Terrain mode)
            // This ensures determinant = +1 (pure rotation, not reflection) to preserve winding order
            if (layer.AlignToNormal && layer.AlignmentStrength > 0f)
            {
                // Get terrain normal at this position
                Vector3 terrainNormal = terrain.GetNormalAtPosition(worldX, worldZ);

                // Calculate alignment rotation from up vector to terrain normal
                var up = Vector3.UnitY;
                var alignmentRotation = CalculateAlignmentRotation(up, terrainNormal);

                // Apply alignment strength (0-100%)
                float strength = Math.Clamp(layer.AlignmentStrength / 100f, 0f, 1f);
                if (strength < 1f)
                {
                    // Lerp between identity (no alignment) and full alignment
                    alignmentRotation = Quaternion.Slerp(
                        Quaternion.Identity,
                        alignmentRotation,
                        strength);
                }

                // Combine alignment rotation with Y-axis rotation
                rotation = alignmentRotation * rotation;
            }

            // Build transform matrix: first rotation + scale, then set translation manually
            // This matches Single Terrain mode exactly for consistent winding order
            Matrix4 scaleMatrix = Matrix4.CreateScale(scale);
            Matrix4 rotationMatrix = Matrix4.CreateFromQuaternion(rotation);

            // Combine rotation and scale first (same order as Single Terrain)
            Matrix4 matrix = rotationMatrix * scaleMatrix;

            // Set translation in M41/M42/M43 (OpenTK Matrix4 stores translation in last row)
            // This ensures translation is not affected by rotation/scale
            matrix.M41 = worldX;
            matrix.M42 = worldY;
            matrix.M43 = worldZ;

            return matrix;
        }

        /// <summary>
        /// Calculate a rotation quaternion that aligns 'from' vector to 'to' vector.
        /// This ensures a pure rotation (determinant = +1) without reflection.
        /// Same implementation as Terrain.CalculateAlignmentRotation for consistency.
        /// </summary>
        private static Quaternion CalculateAlignmentRotation(Vector3 from, Vector3 to)
        {
            from = Vector3.Normalize(from);
            to = Vector3.Normalize(to);

            float dot = Vector3.Dot(from, to);

            // Vectors are parallel
            if (dot >= 0.999999f)
            {
                return Quaternion.Identity;
            }

            // Vectors are opposite
            if (dot <= -0.999999f)
            {
                var axis = Vector3.Cross(Vector3.UnitX, from);
                if (axis.LengthSquared < 0.000001f)
                {
                    axis = Vector3.Cross(Vector3.UnitZ, from);
                }
                axis = Vector3.Normalize(axis);
                return Quaternion.FromAxisAngle(axis, (float)Math.PI);
            }

            // General case
            var cross = Vector3.Cross(from, to);
            float s = (float)Math.Sqrt((1 + dot) * 2);
            float invS = 1f / s;

            return new Quaternion(
                cross.X * invS,
                cross.Y * invS,
                cross.Z * invS,
                s * 0.5f
            );
        }

        /// <summary>
        /// Simple 2D smooth noise for natural density variation.
        /// Returns value in range [-1, 1].
        /// </summary>
        private static float SmoothNoise(float x, float y, int seed)
        {
            // Hash the position with seed
            int ix = (int)Math.Floor(x);
            int iy = (int)Math.Floor(y);

            float fx = x - ix;
            float fy = y - iy;

            // Smooth interpolation
            float u = fx * fx * (3f - 2f * fx);
            float v = fy * fy * (3f - 2f * fy);

            // Hash corners
            float n00 = HashNoise2D(ix, iy, seed);
            float n10 = HashNoise2D(ix + 1, iy, seed);
            float n01 = HashNoise2D(ix, iy + 1, seed);
            float n11 = HashNoise2D(ix + 1, iy + 1, seed);

            // Bilinear interpolation
            float nx0 = n00 * (1f - u) + n10 * u;
            float nx1 = n01 * (1f - u) + n11 * u;
            return nx0 * (1f - v) + nx1 * v;
        }

        /// <summary>
        /// Hash 2D position to deterministic value in range [-1, 1].
        /// </summary>
        private static float HashNoise2D(int x, int y, int seed)
        {
            int hash = seed;
            hash = hash * 374761393 + x;
            hash = (hash ^ 61) ^ (hash >> 16);
            hash = hash + (hash << 3);
            hash = hash ^ y;
            hash = hash * 668265263;
            hash = hash + (hash << 10);
            hash = hash ^ (hash >> 6);
            hash = hash + (hash << 15);
            hash = hash ^ (hash >> 11);

            // Normalize to [-1, 1]
            return ((hash & 0x7FFFFFFF) / (float)0x7FFFFFFF) * 2f - 1f;
        }
    }

    /// <summary>
    /// Spatial grid for O(1) neighbor queries in vegetation placement.
    /// Replaces O(n²) distance checks with O(1) cell lookups.
    /// Reduces 1,000,000 comparisons to ~20 for typical vegetation density.
    /// </summary>
    internal class SpatialGrid
    {
        private readonly Dictionary<(int, int), List<Matrix4>> _cells = new();
        private readonly float _cellSize;
        private readonly float _originX;
        private readonly float _originZ;

        public SpatialGrid(float cellSize, float originX, float originZ)
        {
            _cellSize = cellSize;
            _originX = originX;
            _originZ = originZ;
        }

        private (int, int) GetCellKey(float worldX, float worldZ)
        {
            int cx = (int)Math.Floor((worldX - _originX) / _cellSize);
            int cz = (int)Math.Floor((worldZ - _originZ) / _cellSize);
            return (cx, cz);
        }

        public void Add(Matrix4 matrix)
        {
            float worldX = matrix.M41;
            float worldZ = matrix.M43;
            var key = GetCellKey(worldX, worldZ);

            if (!_cells.TryGetValue(key, out var list))
            {
                list = new List<Matrix4>();
                _cells[key] = list;
            }
            list.Add(matrix);
        }

        public bool IsTooClose(float worldX, float worldY, float worldZ, float minDistance)
        {
            float minDistSq = minDistance * minDistance;
            var key = GetCellKey(worldX, worldZ);

            // Check 3x3 grid of cells (current + 8 neighbors)
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    var neighborKey = (key.Item1 + dx, key.Item2 + dz);
                    if (!_cells.TryGetValue(neighborKey, out var list))
                        continue;

                    // Check distance to all instances in this cell
                    foreach (var matrix in list)
                    {
                        float dx2 = matrix.M41 - worldX;
                        float dy2 = matrix.M42 - worldY;
                        float dz2 = matrix.M43 - worldZ;

                        float distSq = dx2 * dx2 + dy2 * dy2 + dz2 * dz2;
                        if (distSq < minDistSq)
                            return true;
                    }
                }
            }

            return false;
        }
    }
}

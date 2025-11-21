using System;
using System.Collections.Generic;
using Engine.Components;
using OpenTK.Mathematics;

namespace Engine.Physics
{
    /// <summary>
    /// Spatial hash grid for efficient broadphase collision detection.
    /// Replaces O(N²) with O(N) average case by partitioning space into cells.
    /// </summary>
    public class SpatialHash
    {
        private readonly float _cellSize;
        private readonly Dictionary<Vector3i, HashSet<Collider>> _grid;
        private readonly Dictionary<Collider, HashSet<Vector3i>> _colliderCells;

        public SpatialHash(float cellSize = 10f)
        {
            _cellSize = cellSize;
            _grid = new Dictionary<Vector3i, HashSet<Collider>>();
            _colliderCells = new Dictionary<Collider, HashSet<Vector3i>>();
        }

        /// <summary>
        /// Insert a collider into the spatial hash based on its AABB.
        /// </summary>
        public void Insert(Collider collider)
        {
            if (!_colliderCells.ContainsKey(collider))
            {
                _colliderCells[collider] = new HashSet<Vector3i>();
            }

            var bounds = collider.WorldAABB;
            var minCell = WorldToCell(bounds.Min);
            var maxCell = WorldToCell(bounds.Max);

            // Safety check: limit the number of cells a collider can occupy
            int cellCountX = Math.Abs(maxCell.X - minCell.X) + 1;
            int cellCountY = Math.Abs(maxCell.Y - minCell.Y) + 1;
            int cellCountZ = Math.Abs(maxCell.Z - minCell.Z) + 1;
            
            const int MAX_CELLS_PER_DIMENSION = 100; // Safety limit
            if (cellCountX > MAX_CELLS_PER_DIMENSION || 
                cellCountY > MAX_CELLS_PER_DIMENSION || 
                cellCountZ > MAX_CELLS_PER_DIMENSION)
            {
                Console.WriteLine($"[SpatialHash] WARNING: Collider '{collider.Entity?.Name ?? "Unknown"}' has huge bounds ({cellCountX}x{cellCountY}x{cellCountZ} cells). Skipping spatial hash insertion.");
                return;
            }

            // Insert into all cells covered by AABB
            for (int x = minCell.X; x <= maxCell.X; x++)
            {
                for (int y = minCell.Y; y <= maxCell.Y; y++)
                {
                    for (int z = minCell.Z; z <= maxCell.Z; z++)
                    {
                        var cell = new Vector3i(x, y, z);
                        
                        if (!_grid.ContainsKey(cell))
                            _grid[cell] = new HashSet<Collider>();
                        
                        _grid[cell].Add(collider);
                        _colliderCells[collider].Add(cell);
                    }
                }
            }
        }

        /// <summary>
        /// Remove a collider from the spatial hash.
        /// </summary>
        public void Remove(Collider collider)
        {
            if (!_colliderCells.TryGetValue(collider, out var cells))
                return;

            foreach (var cell in cells)
            {
                if (_grid.TryGetValue(cell, out var cellSet))
                {
                    cellSet.Remove(collider);
                    if (cellSet.Count == 0)
                        _grid.Remove(cell);
                }
            }

            _colliderCells.Remove(collider);
        }

        /// <summary>
        /// Update a collider's position in the spatial hash.
        /// More efficient than Remove + Insert when AABB changes.
        /// </summary>
        public void Update(Collider collider)
        {
            Remove(collider);
            Insert(collider);
        }

        /// <summary>
        /// Query all potential colliding pairs in the hash.
        /// Returns unique pairs (A, B) where A and B share at least one cell.
        /// </summary>
        public void QueryPairs(HashSet<(Collider, Collider)> outPairs)
        {
            outPairs.Clear();

            foreach (var cell in _grid.Values)
            {
                if (cell.Count < 2) continue;

                var cellList = new List<Collider>(cell);
                for (int i = 0; i < cellList.Count; i++)
                {
                    var a = cellList[i];
                    for (int j = i + 1; j < cellList.Count; j++)
                    {
                        var b = cellList[j];
                        var pair = OrderPair(a, b);
                        // HashSet automatically prevents duplicates
                        outPairs.Add(pair);
                    }
                }
            }
        }

        /// <summary>
        /// Query colliders that overlap with an AABB.
        /// </summary>
        public void QueryAABB(in Vector3 min, in Vector3 max, HashSet<Collider> outColliders)
        {
            outColliders.Clear();
            var minCell = WorldToCell(min);
            var maxCell = WorldToCell(max);

            for (int x = minCell.X; x <= maxCell.X; x++)
            {
                for (int y = minCell.Y; y <= maxCell.Y; y++)
                {
                    for (int z = minCell.Z; z <= maxCell.Z; z++)
                    {
                        var cell = new Vector3i(x, y, z);
                        if (_grid.TryGetValue(cell, out var cellSet))
                        {
                            foreach (var collider in cellSet)
                                outColliders.Add(collider);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Clear all data from the spatial hash.
        /// </summary>
        public void Clear()
        {
            _grid.Clear();
            _colliderCells.Clear();
        }

        private Vector3i WorldToCell(Vector3 worldPos)
        {
            return new Vector3i(
                (int)MathF.Floor(worldPos.X / _cellSize),
                (int)MathF.Floor(worldPos.Y / _cellSize),
                (int)MathF.Floor(worldPos.Z / _cellSize)
            );
        }

        private static (Collider, Collider) OrderPair(Collider a, Collider b)
        {
            return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(a) < 
                   System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(b) 
                ? (a, b) : (b, a);
        }
    }

    /// <summary>
    /// Integer 3D vector for grid cell coordinates.
    /// </summary>
    public struct Vector3i : IEquatable<Vector3i>
    {
        public int X, Y, Z;

        public Vector3i(int x, int y, int z)
        {
            X = x; Y = y; Z = z;
        }

        public bool Equals(Vector3i other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object? obj) => obj is Vector3i other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z);
        public static bool operator ==(Vector3i a, Vector3i b) => a.Equals(b);
        public static bool operator !=(Vector3i a, Vector3i b) => !a.Equals(b);
    }
}

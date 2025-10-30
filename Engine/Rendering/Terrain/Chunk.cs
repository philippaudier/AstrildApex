using System;
using System.Numerics;

namespace Engine.Rendering.Terrain
{
    public enum ChunkState { Pending, Generating, Ready, PendingUpload, Uploaded, Unloaded }

    public class ChunkKey
    {
        public int X { get; }
        public int Z { get; }
        public ChunkKey(int x, int z) { X = x; Z = z; }
        public override int GetHashCode() => (X * 73856093) ^ (Z * 19349663);
        public override bool Equals(object? obj) => obj is ChunkKey k && k.X == X && k.Z == Z;
    }

    public class Chunk
    {
        public ChunkKey Key { get; }
        public MeshData? Mesh { get; set; }
        public ChunkState State { get; set; } = ChunkState.Pending;
        public Vector3 WorldPosition { get; set; }
        public DateTime LastAccess { get; set; } = DateTime.UtcNow;

        // GPU handles (0 = not uploaded)
        public int Vao { get; set; } = 0;
        public int Vbo { get; set; } = 0;
        public int Ebo { get; set; } = 0;
        public int IndexCount { get; set; } = 0;

        public Chunk(ChunkKey key)
        {
            Key = key;
        }
    }
}

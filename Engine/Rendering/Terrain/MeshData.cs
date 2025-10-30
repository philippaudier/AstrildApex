using System;

namespace Engine.Rendering.Terrain
{
    /// <summary>
    /// Mesh data produced by chunk generator. Arrays are kept public for fast access and pooling.
    /// </summary>
    public class MeshData
    {
        public float[] Vertices; // x,y,z interleaved
        public float[] Normals;  // x,y,z
        public float[] UVs;      // u,v
        public int[] Indices;

        // Slope-based splatting data (up to 4 textures per vertex)
        public float[]? SplatWeights; // r,g,b,a weights for up to 4 slope textures per vertex
        public int[]? SplatIndices;   // texture indices for each vertex (packed as 4 bytes per int)

        public int VertexCount;
        public int IndexCount;
        public bool HasSplatData => SplatWeights != null && SplatIndices != null;

        public MeshData(int vertexCount, int indexCount, bool enableSplatting = false)
        {
            // Rent arrays from pool to reduce GC pressure when generating many meshes
            Vertices = MeshBufferPool.RentFloat(vertexCount * 3);
            Normals = MeshBufferPool.RentFloat(vertexCount * 3);
            UVs = MeshBufferPool.RentFloat(vertexCount * 2);
            Indices = MeshBufferPool.RentInt(indexCount);

            // Allocate splatting data if enabled
            if (enableSplatting)
            {
                SplatWeights = MeshBufferPool.RentFloat(vertexCount * 4); // RGBA weights
                SplatIndices = MeshBufferPool.RentInt(vertexCount); // 4 texture indices packed per int
            }

            VertexCount = vertexCount;
            IndexCount = indexCount;
        }

        public MeshData()
        {
            // Ensure non-null arrays to satisfy nullable warnings and allow safe usage before proper sizing
            Vertices = Array.Empty<float>();
            Normals = Array.Empty<float>();
            UVs = Array.Empty<float>();
            Indices = Array.Empty<int>();
            SplatWeights = null;
            SplatIndices = null;
        }
    }
}

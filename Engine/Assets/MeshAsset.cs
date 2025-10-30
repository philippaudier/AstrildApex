using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;

namespace Engine.Assets
{
    /// <summary>
    /// Represents a 3D mesh asset loaded from external formats (FBX, OBJ, GLTF, etc.)
    /// </summary>
    public sealed class MeshAsset
    {
        public Guid Guid { get; set; }
        public string? Name { get; set; }

        /// <summary>
        /// Path to the source file relative to Assets folder
        /// </summary>
        public string? SourcePath { get; set; }

        /// <summary>
        /// Submeshes contained in this model
        /// </summary>
        public List<SubMesh> SubMeshes { get; set; } = new();

        /// <summary>
        /// Bounding box for the entire mesh
        /// </summary>
        public BoundingBox Bounds { get; set; }

        /// <summary>
        /// Total vertex count across all submeshes
        /// </summary>
        public int TotalVertexCount { get; set; }

        /// <summary>
        /// Total triangle count across all submeshes
        /// </summary>
        public int TotalTriangleCount { get; set; }

        /// <summary>
        /// Material GUIDs associated with submeshes (index matches SubMeshes)
        /// </summary>
        public List<Guid?> MaterialGuids { get; set; } = new();

        public static MeshAsset Load(string file)
            => JsonSerializer.Deserialize<MeshAsset>(File.ReadAllText(file))!;

        public static void Save(string file, MeshAsset mesh)
            => File.WriteAllText(file, JsonSerializer.Serialize(mesh, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>
    /// Represents a single submesh with its own geometry and material
    /// </summary>
    public sealed class SubMesh
    {
        public string? Name { get; set; }

        /// <summary>
        /// Interleaved vertex data: Position(3) + Normal(3) + TexCoord(2) = 8 floats per vertex
        /// </summary>
        public float[] Vertices { get; set; } = Array.Empty<float>();

        /// <summary>
        /// Triangle indices (3 per triangle)
        /// </summary>
        public uint[] Indices { get; set; } = Array.Empty<uint>();

        /// <summary>
        /// Material index for this submesh (index into MeshAsset.MaterialGuids)
        /// </summary>
        public int MaterialIndex { get; set; } = 0;

        /// <summary>
        /// Number of vertices in this submesh
        /// </summary>
        public int VertexCount => Vertices.Length / 8;

        /// <summary>
        /// Number of triangles in this submesh
        /// </summary>
        public int TriangleCount => Indices.Length / 3;
    }

    /// <summary>
    /// Axis-aligned bounding box
    /// </summary>
    public struct BoundingBox
    {
        public Vector3 Min { get; set; }
        public Vector3 Max { get; set; }

        public Vector3 Center => (Min + Max) * 0.5f;
        public Vector3 Size => Max - Min;

        public static BoundingBox Empty => new BoundingBox
        {
            Min = new Vector3(float.MaxValue),
            Max = new Vector3(float.MinValue)
        };

        public void Encapsulate(Vector3 point)
        {
            Min = Vector3.Min(Min, point);
            Max = Vector3.Max(Max, point);
        }

        public void Encapsulate(BoundingBox other)
        {
            Min = Vector3.Min(Min, other.Min);
            Max = Vector3.Max(Max, other.Max);
        }
    }

    /// <summary>
    /// Raw mesh data before GPU upload (used during import)
    /// </summary>
    public sealed class MeshData
    {
        public List<Vector3> Positions { get; set; } = new();
        public List<Vector3> Normals { get; set; } = new();
        public List<Vector2> TexCoords { get; set; } = new();
        public List<uint> Indices { get; set; } = new();

        /// <summary>
        /// Converts to interleaved vertex format (8 floats per vertex)
        /// </summary>
        public float[] ToInterleavedVertices()
        {
            int vertexCount = Positions.Count;
            var result = new float[vertexCount * 8];

            for (int i = 0; i < vertexCount; i++)
            {
                int offset = i * 8;
                var pos = Positions[i];
                var normal = i < Normals.Count ? Normals[i] : Vector3.UnitY;
                var uv = i < TexCoords.Count ? TexCoords[i] : Vector2.Zero;

                // Position (3 floats)
                result[offset + 0] = pos.X;
                result[offset + 1] = pos.Y;
                result[offset + 2] = pos.Z;

                // Normal (3 floats)
                result[offset + 3] = normal.X;
                result[offset + 4] = normal.Y;
                result[offset + 5] = normal.Z;

                // TexCoord (2 floats)
                result[offset + 6] = uv.X;
                result[offset + 7] = uv.Y;
            }

            return result;
        }

        /// <summary>
        /// Calculate bounding box from positions
        /// </summary>
        public BoundingBox CalculateBounds()
        {
            var bounds = BoundingBox.Empty;
            foreach (var pos in Positions)
                bounds.Encapsulate(pos);
            return bounds;
        }

        /// <summary>
        /// Generate flat normals if missing
        /// </summary>
        public void GenerateNormals()
        {
            if (Normals.Count >= Positions.Count) return;

            Normals.Clear();
            Normals.AddRange(new Vector3[Positions.Count]);

            // Calculate face normals and accumulate
            for (int i = 0; i < Indices.Count; i += 3)
            {
                var i0 = (int)Indices[i];
                var i1 = (int)Indices[i + 1];
                var i2 = (int)Indices[i + 2];

                var v0 = Positions[i0];
                var v1 = Positions[i1];
                var v2 = Positions[i2];

                var edge1 = v1 - v0;
                var edge2 = v2 - v0;
                var normal = Vector3.Normalize(Vector3.Cross(edge1, edge2));

                Normals[i0] += normal;
                Normals[i1] += normal;
                Normals[i2] += normal;
            }

            // Normalize all normals
            for (int i = 0; i < Normals.Count; i++)
            {
                var n = Normals[i];
                Normals[i] = n.Length() > 0.0001f ? Vector3.Normalize(n) : Vector3.UnitY;
            }
        }
    }
}

using System;
using OpenTK.Graphics.OpenGL4;

namespace Engine.Rendering.Terrain
{
    /// <summary>
    /// Small helper to upload MeshData to GPU (VAO/VBO/EBO) and return handles.
    /// Must be called from the GL main thread/context.
    /// </summary>
    public static class MeshUploader
    {
        public static void Upload(MeshData mesh, out int vao, out int vbo, out int ebo, out int indexCount)
        {
            vao = GL.GenVertexArray();
            vbo = GL.GenBuffer();
            ebo = GL.GenBuffer();

            GL.BindVertexArray(vao);

            // Determine if we need splatting data
            bool hasSplatData = mesh.HasSplatData;
            int vertexCount = mesh.VertexCount;
            int idxCountLocal = mesh.IndexCount;

            // Calculate buffer size: pos(3) + normal(3) + uv(2) + [splatWeights(4) + splatIndices(1)]
            // 8 base floats + 4 splat weight floats + 1 splat index (stored as uint via float slot) = 13
            int floatsPerVertex = hasSplatData ? 13 : 8; // corrected to include the splat index slot
            int floatCount = vertexCount * floatsPerVertex;
            var interleaved = MeshBufferPool.RentFloat(floatCount);

            for (int i = 0; i < vertexCount; i++)
            {
                int baseIdx = i * floatsPerVertex;
                // Position
                interleaved[baseIdx + 0] = mesh.Vertices[i * 3 + 0];
                interleaved[baseIdx + 1] = mesh.Vertices[i * 3 + 1];
                interleaved[baseIdx + 2] = mesh.Vertices[i * 3 + 2];
                // Normal
                interleaved[baseIdx + 3] = mesh.Normals[i * 3 + 0];
                interleaved[baseIdx + 4] = mesh.Normals[i * 3 + 1];
                interleaved[baseIdx + 5] = mesh.Normals[i * 3 + 2];
                // UV
                interleaved[baseIdx + 6] = mesh.UVs[i * 2 + 0];
                interleaved[baseIdx + 7] = mesh.UVs[i * 2 + 1];

                if (hasSplatData)
                {
                    // Splat weights (RGBA)
                    interleaved[baseIdx + 8] = mesh.SplatWeights![i * 4 + 0];
                    interleaved[baseIdx + 9] = mesh.SplatWeights[i * 4 + 1];
                    interleaved[baseIdx + 10] = mesh.SplatWeights[i * 4 + 2];
                    interleaved[baseIdx + 11] = mesh.SplatWeights[i * 4 + 3];
                    // Splat indices (packed as float for simplicity)
                    interleaved[baseIdx + 12] = BitConverter.UInt32BitsToSingle((uint)mesh.SplatIndices![i]);
                }
            }

            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(floatCount * sizeof(float)), interleaved, BufferUsageHint.StaticDraw);

            // Setup attributes: 0=pos(3), 1=normal(3), 2=uv(2), [3=splatWeights(4), 4=splatIndices(1)]
            int stride = floatsPerVertex * sizeof(float);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
            GL.EnableVertexAttribArray(2);
            GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, 6 * sizeof(float));

            if (hasSplatData)
            {
                // Splat weights at location 3
                GL.EnableVertexAttribArray(3);
                GL.VertexAttribPointer(3, 4, VertexAttribPointerType.Float, false, stride, 8 * sizeof(float));
                // Splat indices at location 4 (as uint)
                GL.EnableVertexAttribArray(4);
                GL.VertexAttribIPointer(4, 1, VertexAttribIntegerType.UnsignedInt, stride, (IntPtr)(12 * sizeof(float)));
            }

            // EBO - upload indices directly from mesh.Indices (ownership should be transferred by the producer)
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(idxCountLocal * sizeof(int)), mesh.Indices, BufferUsageHint.StaticDraw);

            // Unbind VAO to avoid accidental state leakage
            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);

            indexCount = idxCountLocal;
            try { Console.WriteLine($"[MeshUploader] Upload complete vao={vao} indexCount={indexCount} vertices={vertexCount} indices={idxCountLocal}"); } catch { }

            // Return pooled buffers
            MeshBufferPool.ReturnFloat(interleaved);
        }

        public static void Delete(int vao, int vbo, int ebo)
        {
            try { if (vao != 0) GL.DeleteVertexArray(vao); } catch { }
            try { if (vbo != 0) GL.DeleteBuffer(vbo); } catch { }
            try { if (ebo != 0) GL.DeleteBuffer(ebo); } catch { }
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using OpenTK.Graphics.OpenGL4;

namespace Engine.Rendering.Terrain
{
    public sealed class MeshUploadRequest
    {
        public Chunk TargetChunk { get; set; } = null!; // set by producer
        public MeshData Mesh { get; set; } = null!; // copy or reference owned by producer
    }

    /// <summary>
    /// Thread-safe queue for mesh uploads. Background threads can enqueue MeshData for chunks,
    /// and the GL/main thread must call FlushUploads to perform actual GL uploads.
    /// </summary>
    public static class MeshUploadQueue
    {
        private static readonly ConcurrentQueue<MeshUploadRequest> _queue = new();
        // Queue of deletion requests processed on the GL thread
        private static readonly ConcurrentQueue<(object generator, ChunkKey key)> _deletionQueue = new();

        public static void Enqueue(MeshUploadRequest req)
        {
            if (req == null) throw new ArgumentNullException(nameof(req));
            _queue.Enqueue(req);
        }

        public static bool TryDequeue(out MeshUploadRequest? req)
        {
            return _queue.TryDequeue(out req);
        }

        public static int Count => _queue.Count;

        /// <summary>
        /// Flush uploads by performing GL uploads for all pending requests using MeshUploader.Upload.
        /// Must be called on the GL thread.
        /// </summary>
        public static void FlushUploads()
        {
            // Limit uploads per-frame to avoid long GL-thread stalls when many chunks are enqueued.
            int uploadsThisFrame = 0;
            const int MaxUploadsPerFrame = 8; // tunable: increased for smoother streaming
            while (uploadsThisFrame < MaxUploadsPerFrame && _queue.TryDequeue(out var req))
            {
                try
                {
                    if (req.TargetChunk == null || req.Mesh == null) continue;
                    try { Console.WriteLine($"[MeshUploadQueue] Uploading chunk at {req.TargetChunk.WorldPosition.X},{req.TargetChunk.WorldPosition.Z}"); } catch { }
                    // If the chunk already has GPU handles, delete them first to avoid leaks
                    try {
                        if (req.TargetChunk.Vao != 0 || req.TargetChunk.Vbo != 0 || req.TargetChunk.Ebo != 0)
                        {
                            MeshUploader.Delete(req.TargetChunk.Vao, req.TargetChunk.Vbo, req.TargetChunk.Ebo);
                            req.TargetChunk.Vao = 0; req.TargetChunk.Vbo = 0; req.TargetChunk.Ebo = 0;
                        }
                    } catch { }

                    // upload and assign new handles
                    MeshUploader.Upload(req.Mesh, out int vao, out int vbo, out int ebo, out int idxCount);
                    req.TargetChunk.Vao = vao;
                    req.TargetChunk.Vbo = vbo;
                    req.TargetChunk.Ebo = ebo;
                    req.TargetChunk.IndexCount = idxCount;
                    req.TargetChunk.State = ChunkState.Uploaded;

                    // Return arrays to pool to avoid GC churn, but keep mesh reference until chunk is disposed
                    try {
                        if (req.Mesh != null) {
                            MeshBufferPool.ReturnFloat(req.Mesh.Vertices);
                            MeshBufferPool.ReturnFloat(req.Mesh.Normals);
                            MeshBufferPool.ReturnFloat(req.Mesh.UVs);
                            MeshBufferPool.ReturnInt(req.Mesh.Indices);

                            // Return splatting arrays if present
                            if (req.Mesh.SplatWeights != null)
                                MeshBufferPool.ReturnFloat(req.Mesh.SplatWeights);
                            if (req.Mesh.SplatIndices != null)
                                MeshBufferPool.ReturnInt(req.Mesh.SplatIndices);

                            // Mark arrays as returned but keep mesh structure
                            req.Mesh.Vertices = null!;
                            req.Mesh.Normals = null!;
                            req.Mesh.UVs = null!;
                            req.Mesh.Indices = null!;
                            req.Mesh.SplatWeights = null;
                            req.Mesh.SplatIndices = null;
                        }
                    } catch { }
                    // Keep chunk.Mesh reference with null arrays for state tracking
                    uploadsThisFrame++;
                }
                catch
                {
                    try { req.TargetChunk.State = ChunkState.Ready; } catch { }
                }
            }

            // Process pending deletion requests on GL thread.
            // Legacy chunk-based deletion has been removed along with the streaming system.
            // To keep the queue safe for any stray requests, just drain the queue and ignore entries.
            while (_deletionQueue.TryDequeue(out var _))
            {
                // no-op: legacy deletion handled by removed streaming system
            }
        }

        /// <summary>
        /// Request deletion of a chunk. This enqueues a request to be processed on the GL thread in FlushUploads.
        /// </summary>
        public static void RequestDeletion(object generator, ChunkKey key)
        {
            if (generator == null || key == null) return;
            _deletionQueue.Enqueue((generator, key));
        }
    }
}

using System;
using System.Collections.Generic;
using OpenTK.Graphics.OpenGL4;

namespace Engine.Profiling
{
    /// <summary>
    /// GPU timing system using OpenGL query objects.
    /// Measures actual GPU execution time with minimal CPU-GPU synchronization overhead.
    /// Uses query pooling with multiple frames of latency to avoid stalls.
    /// </summary>
    public static class GPUTimer
    {
        private const int QueryLatency = 3; // Number of frames to wait before reading query results
        private const int MaxActiveQueries = 256; // Maximum concurrent queries

        private class QueryScope
        {
            public int QueryId;
            public string Name = "";
            public int FrameIndex;
            public bool Active;
            public long ResultNanoseconds;
            public bool ResultAvailable;
        }

        private static readonly Queue<QueryScope> _queryPool = new();
        private static readonly List<QueryScope> _activeQueries = new();
        private static readonly Stack<QueryScope?> _scopeStack = new();
        private static readonly Dictionary<string, List<float>> _queryHistory = new(); // in milliseconds

        private static int _currentFrameIndex = 0;
        private static bool _isSupported = false;
        private static bool _supportChecked = false;

        private const int HistoryLength = 600; // Keep 10 seconds at 60fps

        /// <summary>
        /// Check if GPU timing is supported on this platform
        /// </summary>
        public static bool IsSupported
        {
            get
            {
                if (!_supportChecked)
                {
                    CheckSupport();
                }
                return _isSupported;
            }
        }

        /// <summary>
        /// Check GPU timer support by trying to create a query object
        /// </summary>
        private static void CheckSupport()
        {
            _supportChecked = true;

            try
            {
                // Try to create a test query to verify support
                int testQuery = GL.GenQuery();
                if (testQuery > 0)
                {
                    GL.DeleteQuery(testQuery);
                    _isSupported = true;
                    Console.WriteLine("[GPUTimer] GPU timing supported");
                }
                else
                {
                    _isSupported = false;
                    Console.WriteLine("[GPUTimer] GPU timestamp queries not supported on this platform");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GPUTimer] Error checking GPU timer support: {ex.Message}");
                _isSupported = false;
            }
        }

        /// <summary>
        /// Begin a new GPU timing scope
        /// </summary>
        public static void BeginScope(string name)
        {
            if (!IsSupported) return;

            try
            {
                // Check if we have too many active queries
                if (_activeQueries.Count >= MaxActiveQueries)
                {
                    // Too many active queries, push a null marker so EndScope can skip it
                    _scopeStack.Push(null);
                    return;
                }

                // Get or create query from pool
                QueryScope scope;
                if (_queryPool.Count > 0)
                {
                    scope = _queryPool.Dequeue();
                    scope.Name = name;
                    scope.FrameIndex = _currentFrameIndex;
                    scope.Active = true;
                    scope.ResultAvailable = false;
                }
                else
                {
                    // Create new query
                    int queryId = GL.GenQuery();
                    scope = new QueryScope
                    {
                        QueryId = queryId,
                        Name = name,
                        FrameIndex = _currentFrameIndex,
                        Active = true,
                        ResultAvailable = false
                    };
                }

                // Begin GPU query
                GL.BeginQuery(QueryTarget.TimeElapsed, scope.QueryId);

                _scopeStack.Push(scope);
                _activeQueries.Add(scope);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GPUTimer] Error beginning scope '{name}': {ex.Message}");
                // Push null to keep stack balanced
                _scopeStack.Push(null);
            }
        }

        /// <summary>
        /// End the current GPU timing scope
        /// </summary>
        public static void EndScope()
        {
            if (!IsSupported) return;

            try
            {
                if (_scopeStack.Count == 0)
                {
                    Console.WriteLine("[GPUTimer] EndScope called without matching BeginScope");
                    return;
                }

                QueryScope? scope = _scopeStack.Pop();

                // If scope is null, it means BeginScope was skipped (too many queries or error)
                if (scope == null)
                {
                    return;
                }

                // End GPU query
                GL.EndQuery(QueryTarget.TimeElapsed);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GPUTimer] Error ending scope: {ex.Message}");
            }
        }

        /// <summary>
        /// Call once per frame to collect results and advance frame index.
        /// This should be called at the end of the frame, after all rendering.
        /// </summary>
        public static void CollectResults()
        {
            if (!IsSupported) return;

            try
            {
                // Process queries from old frames (with latency)
                for (int i = _activeQueries.Count - 1; i >= 0; i--)
                {
                    QueryScope query = _activeQueries[i];

                    // Only check queries that are old enough (QueryLatency frames ago)
                    int frameAge = _currentFrameIndex - query.FrameIndex;
                    if (frameAge < QueryLatency)
                        continue;

                    // Check if result is available (non-blocking)
                    GL.GetQueryObject(query.QueryId, GetQueryObjectParam.QueryResultAvailable, out int available);

                    if (available != 0)
                    {
                        // Result is ready, read it
                        GL.GetQueryObject(query.QueryId, GetQueryObjectParam.QueryResult, out long result);
                        query.ResultNanoseconds = result;
                        query.ResultAvailable = true;

                        // Convert to milliseconds and store in history
                        float ms = result / 1_000_000.0f;
                        RecordResult(query.Name, ms);

                        // Return query to pool
                        query.Active = false;
                        _queryPool.Enqueue(query);
                        _activeQueries.RemoveAt(i);
                    }
                    else if (frameAge > QueryLatency + 10)
                    {
                        // Query is too old and still not ready - something went wrong
                        // Return it to pool anyway to avoid memory leak
                        Console.WriteLine($"[GPUTimer] Query '{query.Name}' timed out after {frameAge} frames");
                        query.Active = false;
                        _queryPool.Enqueue(query);
                        _activeQueries.RemoveAt(i);
                    }
                }

                _currentFrameIndex++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GPUTimer] Error collecting results: {ex.Message}");
            }
        }

        /// <summary>
        /// Record a GPU timing result in history
        /// </summary>
        private static void RecordResult(string name, float ms)
        {
            if (!_queryHistory.TryGetValue(name, out var history))
            {
                history = new List<float>(HistoryLength);
                _queryHistory[name] = history;
            }

            history.Add(ms);
            if (history.Count > HistoryLength)
            {
                history.RemoveAt(0);
            }
        }

        /// <summary>
        /// Get GPU timing history for a specific scope
        /// </summary>
        public static float[] GetHistory(string name)
        {
            if (_queryHistory.TryGetValue(name, out var history))
            {
                return history.ToArray();
            }
            return Array.Empty<float>();
        }

        /// <summary>
        /// Get the latest GPU timing for a scope (in milliseconds)
        /// </summary>
        public static float GetLatest(string name)
        {
            if (_queryHistory.TryGetValue(name, out var history) && history.Count > 0)
            {
                return history[history.Count - 1];
            }
            return 0f;
        }

        /// <summary>
        /// Get average GPU timing for a scope (in milliseconds)
        /// </summary>
        public static float GetAverage(string name)
        {
            if (_queryHistory.TryGetValue(name, out var history) && history.Count > 0)
            {
                float sum = 0;
                foreach (float v in history)
                    sum += v;
                return sum / history.Count;
            }
            return 0f;
        }

        /// <summary>
        /// Get maximum GPU timing for a scope (in milliseconds)
        /// </summary>
        public static float GetMax(string name)
        {
            if (_queryHistory.TryGetValue(name, out var history) && history.Count > 0)
            {
                float max = 0;
                foreach (float v in history)
                    if (v > max) max = v;
                return max;
            }
            return 0f;
        }

        /// <summary>
        /// Get all tracked scope names
        /// </summary>
        public static string[] GetScopeNames()
        {
            var names = new string[_queryHistory.Count];
            _queryHistory.Keys.CopyTo(names, 0);
            return names;
        }

        /// <summary>
        /// Clear all history and reset
        /// </summary>
        public static void Clear()
        {
            _queryHistory.Clear();
            _currentFrameIndex = 0;
        }

        /// <summary>
        /// Dispose of all GPU queries (call on shutdown)
        /// </summary>
        public static void Shutdown()
        {
            try
            {
                // Delete all queries
                foreach (var scope in _activeQueries)
                {
                    GL.DeleteQuery(scope.QueryId);
                }

                foreach (var scope in _queryPool)
                {
                    GL.DeleteQuery(scope.QueryId);
                }

                _activeQueries.Clear();
                _queryPool.Clear();
                _scopeStack.Clear();
                _queryHistory.Clear();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GPUTimer] Error during shutdown: {ex.Message}");
            }
        }
    }
}

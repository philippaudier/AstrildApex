using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Engine.Profiling
{
    /// <summary>
    /// Represents a hierarchical profiling scope with CPU/GPU timing and custom counters.
    /// Supports nested scopes for detailed performance analysis.
    /// </summary>
    public class ProfileScope : IDisposable
    {
        /// <summary>Name of this scope</summary>
        public string Name { get; set; } = "";

        /// <summary>Parent scope (null for root)</summary>
        public ProfileScope? Parent { get; set; }

        /// <summary>Child scopes</summary>
        public List<ProfileScope> Children { get; } = new();

        /// <summary>CPU time spent in this scope (milliseconds)</summary>
        public float CPUTimeMs { get; set; }

        /// <summary>GPU time spent in this scope (milliseconds)</summary>
        public float GPUTimeMs { get; set; }

        /// <summary>CPU time spent in this scope excluding children (self time)</summary>
        public float CPUSelfTimeMs { get; set; }

        /// <summary>Custom counters (e.g., "DrawCalls", "Instances", "Triangles")</summary>
        public Dictionary<string, long> Counters { get; } = new();

        /// <summary>Start timestamp for CPU timing</summary>
        private long _startTimestamp;

        /// <summary>Stopwatch for precise timing</summary>
        private static readonly double TimestampToMs = 1000.0 / Stopwatch.Frequency;

        /// <summary>Whether this scope has been closed</summary>
        private bool _closed = false;

        /// <summary>
        /// Create a new profile scope
        /// </summary>
        public ProfileScope(string name, ProfileScope? parent = null)
        {
            Name = name;
            Parent = parent;
            _startTimestamp = Stopwatch.GetTimestamp();

            if (parent != null)
            {
                parent.Children.Add(this);
            }
        }

        /// <summary>
        /// Close this scope and record timing
        /// </summary>
        public void Close()
        {
            if (_closed) return;

            long endTimestamp = Stopwatch.GetTimestamp();
            CPUTimeMs = (float)((endTimestamp - _startTimestamp) * TimestampToMs);

            // Calculate self time (excluding children)
            float childrenTime = 0;
            foreach (var child in Children)
            {
                childrenTime += child.CPUTimeMs;
            }
            CPUSelfTimeMs = Math.Max(0, CPUTimeMs - childrenTime);

            _closed = true;
        }

        /// <summary>
        /// Set a custom counter value
        /// </summary>
        public void SetCounter(string name, long value)
        {
            Counters[name] = value;
        }

        /// <summary>
        /// Increment a custom counter
        /// </summary>
        public void IncrementCounter(string name, long delta = 1)
        {
            if (Counters.TryGetValue(name, out long current))
            {
                Counters[name] = current + delta;
            }
            else
            {
                Counters[name] = delta;
            }
        }

        /// <summary>
        /// Get a counter value (returns 0 if not found)
        /// </summary>
        public long GetCounter(string name)
        {
            return Counters.TryGetValue(name, out long value) ? value : 0;
        }

        /// <summary>
        /// Find a child scope by name
        /// </summary>
        public ProfileScope? FindChild(string name)
        {
            foreach (var child in Children)
            {
                if (child.Name == name)
                    return child;
            }
            return null;
        }

        /// <summary>
        /// Get total CPU time including all children
        /// </summary>
        public float GetTotalCPUTime()
        {
            return CPUTimeMs;
        }

        /// <summary>
        /// Get depth in the tree (0 = root)
        /// </summary>
        public int GetDepth()
        {
            int depth = 0;
            ProfileScope? current = Parent;
            while (current != null)
            {
                depth++;
                current = current.Parent;
            }
            return depth;
        }

        /// <summary>
        /// Get full path from root (e.g., "Frame/Render/Vegetation")
        /// </summary>
        public string GetFullPath()
        {
            if (Parent == null)
                return Name;

            var parts = new List<string>();
            ProfileScope? current = this;
            while (current != null)
            {
                parts.Insert(0, current.Name);
                current = current.Parent;
            }
            return string.Join("/", parts);
        }

        /// <summary>
        /// Collect all scopes in this tree (depth-first)
        /// </summary>
        public List<ProfileScope> CollectAll()
        {
            var result = new List<ProfileScope> { this };
            foreach (var child in Children)
            {
                result.AddRange(child.CollectAll());
            }
            return result;
        }

        /// <summary>
        /// Clone this scope tree (deep copy)
        /// </summary>
        public ProfileScope Clone()
        {
            var clone = new ProfileScope(Name, null)
            {
                CPUTimeMs = this.CPUTimeMs,
                GPUTimeMs = this.GPUTimeMs,
                CPUSelfTimeMs = this.CPUSelfTimeMs,
                _closed = this._closed
            };

            foreach (var kvp in Counters)
            {
                clone.Counters[kvp.Key] = kvp.Value;
            }

            foreach (var child in Children)
            {
                var childClone = child.Clone();
                childClone.Parent = clone;
                clone.Children.Add(childClone);
            }

            return clone;
        }

        /// <summary>
        /// Dispose pattern - closes the scope
        /// </summary>
        public void Dispose()
        {
            Close();
        }

        /// <summary>
        /// Get a string representation for debugging
        /// </summary>
        public override string ToString()
        {
            return $"{Name} (CPU: {CPUTimeMs:F2}ms, GPU: {GPUTimeMs:F2}ms, Children: {Children.Count})";
        }
    }

    /// <summary>
    /// Helper class to manage a stack of active profile scopes
    /// </summary>
    public class ProfileScopeStack
    {
        private readonly Stack<ProfileScope> _stack = new();
        private ProfileScope? _rootScope;

        /// <summary>Current active scope (top of stack)</summary>
        public ProfileScope? Current => _stack.Count > 0 ? _stack.Peek() : null;

        /// <summary>Root scope of the current frame</summary>
        public ProfileScope? Root => _rootScope;

        /// <summary>Push a new scope onto the stack</summary>
        public ProfileScope Push(string name)
        {
            var parent = Current;
            var scope = new ProfileScope(name, parent);
            _stack.Push(scope);

            // Track root scope
            if (_rootScope == null && parent == null)
            {
                _rootScope = scope;
            }

            return scope;
        }

        /// <summary>Pop the current scope from the stack</summary>
        public void Pop()
        {
            if (_stack.Count == 0)
            {
                Console.WriteLine("[ProfileScopeStack] Warning: Pop called on empty stack");
                return;
            }

            var scope = _stack.Pop();
            scope.Close();
        }

        /// <summary>Clear the stack and reset for a new frame</summary>
        public void Clear()
        {
            // Close any remaining scopes
            while (_stack.Count > 0)
            {
                Pop();
            }

            _rootScope = null;
        }

        /// <summary>Get the root scope and prepare for a new frame</summary>
        public ProfileScope? TakeRoot()
        {
            var root = _rootScope;
            Clear();
            return root;
        }
    }
}

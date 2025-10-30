using System;
using System.Collections.Generic;
using System.Diagnostics;
using OpenTK.Mathematics;
using Engine.Scene;

namespace Editor.State
{
    public interface IEditorAction
    {
        string Label { get; }
        DateTime Timestamp { get; }
        long MemoryFootprint { get; }
        void Undo(Scene scene);
        void Redo(Scene scene);
        bool CanMergeWith(IEditorAction other);
        IEditorAction? TryMergeWith(IEditorAction other);
    }
    
    public static class UndoRedo
    {
        private static readonly CircularBuffer<IEditorAction> _undoStack = new(1000);
        private static readonly CircularBuffer<IEditorAction> _redoStack = new(1000);
        
        // Performance monitoring
        private static readonly Stopwatch _operationTimer = new();
        private static long _totalMemoryUsed = 0;
        private static readonly object _lock = new();
        
        // Composite state
        private static CompositeAction? _openComposite;
        private static int _compositeDepth = 0;
        private static readonly Stack<string> _compositeLabels = new();
        
        // Settings
        public static int MaxUndoLevels { get; set; } = 1000;
        public static long MaxMemoryUsage { get; set; } = 50 * 1024 * 1024; // 50MB
        public static TimeSpan MaxActionAge { get; set; } = TimeSpan.FromMinutes(30);
        public static bool EnableActionMerging { get; set; } = true;
        
        // Events
        public static event Action? AfterChange;
        public static event Action<UndoRedoStats>? StatsChanged;
        
        public static bool CanUndo => _undoStack.Count > 0;
        public static bool CanRedo => _redoStack.Count > 0;
        public static bool IsCompositeOpen => _openComposite != null;
        
        public static void BeginComposite(string label)
        {
            lock (_lock)
            {
                _compositeDepth++;
                _compositeLabels.Push(label);
                
                if (_openComposite == null)
                {
                    _openComposite = new CompositeAction(label, DateTime.UtcNow);
                    _redoStack.Clear();
                }
            }
        }
        
        public static void EndComposite()
        {
            lock (_lock)
            {
                if (_compositeDepth <= 0) return;
                
                _compositeDepth--;
                if (_compositeLabels.Count > 0)
                    _compositeLabels.Pop();
                
                if (_compositeDepth == 0 && _openComposite != null)
                {
                    var composite = _openComposite;
                    _openComposite = null;
                    
                    if (composite.Count > 0)
                    {
                        // Try to merge with last action if enabled
                        var merged = false;
                        if (EnableActionMerging && _undoStack.Count > 0)
                        {
                            var lastAction = _undoStack.PeekLast();
                            if (lastAction.CanMergeWith(composite))
                            {
                                var mergedAction = lastAction.TryMergeWith(composite);
                                if (mergedAction != null)
                                {
                                    _undoStack.RemoveLast();
                                    _undoStack.Add(mergedAction);
                                    merged = true;
                                }
                            }
                        }
                        
                        if (!merged)
                        {
                            PushInternal(composite);
                        }
                        
                        RaiseAfterChange();
                    }
                }
            }
        }
        
        public static void Push(IEditorAction action)
        {
            lock (_lock)
            {
                if (_openComposite != null)
                {
                    _openComposite.Add(action);
                }
                else
                {
                    // Try merging with last action
                    var merged = false;
                    if (EnableActionMerging && _undoStack.Count > 0)
                    {
                        var lastAction = _undoStack.PeekLast();
                        if (lastAction.CanMergeWith(action))
                        {
                            var mergedAction = lastAction.TryMergeWith(action);
                            if (mergedAction != null)
                            {
                                _undoStack.RemoveLast();
                                _undoStack.Add(mergedAction);
                                merged = true;
                            }
                        }
                    }
                    
                    if (!merged)
                    {
                        PushInternal(action);
                    }
                    
                    RaiseAfterChange();
                }
            }
        }
        
        private static void PushInternal(IEditorAction action)
        {
            _redoStack.Clear();
            _undoStack.Add(action);
            _totalMemoryUsed += action.MemoryFootprint;
            
            CleanupOldActions();
            UpdateStats();
        }
        
        public static void Undo(Scene scene)
        {
            lock (_lock)
            {
                if (_undoStack.Count == 0) return;
                
                _operationTimer.Restart();
                
                try
                {
                    var action = _undoStack.RemoveLast();
                    action.Undo(scene);
                    _redoStack.Add(action);
                    
                    _operationTimer.Stop();
                    RaiseAfterChange();
                    UpdateStats();
                }
                catch (Exception e)
                {
                    _operationTimer.Stop();
                    // Log error but don't crash
                    Debug.WriteLine($"Undo failed: {e.Message}");
                    throw;
                }
            }
        }
        
        public static void Redo(Scene scene)
        {
            lock (_lock)
            {
                if (_redoStack.Count == 0) return;
                
                _operationTimer.Restart();
                
                try
                {
                    var action = _redoStack.RemoveLast();
                    action.Redo(scene);
                    _undoStack.Add(action);
                    
                    _operationTimer.Stop();
                    RaiseAfterChange();
                    UpdateStats();
                }
                catch (Exception e)
                {
                    _operationTimer.Stop();
                    Debug.WriteLine($"Redo failed: {e.Message}");
                    throw;
                }
            }
        }
        
        public static void Clear()
        {
            lock (_lock)
            {
                _undoStack.Clear();
                _redoStack.Clear();
                _openComposite = null;
                _compositeDepth = 0;
                _compositeLabels.Clear();
                _totalMemoryUsed = 0;
                
                UpdateStats();
                RaiseAfterChange();
            }
        }
        
        public static void TouchEdit()
        {
            lock (_lock)
            {
                _redoStack.Clear();
                UpdateStats();
            }
        }
        
        private static void CleanupOldActions()
        {
            var cutoffTime = DateTime.UtcNow - MaxActionAge;
            
            // Remove old actions from undo stack
            while (_undoStack.Count > 0 && 
                   (_undoStack.Count > MaxUndoLevels || 
                    _totalMemoryUsed > MaxMemoryUsage ||
                    _undoStack.PeekFirst().Timestamp < cutoffTime))
            {
                var oldAction = _undoStack.RemoveFirst();
                _totalMemoryUsed -= oldAction.MemoryFootprint;
            }
            
            // Remove old actions from redo stack if memory constrained
            while (_redoStack.Count > 0 && _totalMemoryUsed > MaxMemoryUsage)
            {
                var oldAction = _redoStack.RemoveFirst();
                _totalMemoryUsed -= oldAction.MemoryFootprint;
            }
        }
        
        public static void RaiseAfterChange()
        {
            try 
            { 
                AfterChange?.Invoke(); 
                // Mark scene as modified when any change occurs
                Editor.SceneManagement.SceneManager.MarkSceneAsModified();
            } 
            catch { /* Prevent handlers from breaking undo system */ }
        }
        
        private static void UpdateStats()
        {
            var stats = new UndoRedoStats
            {
                UndoCount = _undoStack.Count,
                RedoCount = _redoStack.Count,
                MemoryUsed = _totalMemoryUsed,
                LastOperationTime = _operationTimer.Elapsed,
                CompositeDepth = _compositeDepth
            };
            
            try { StatsChanged?.Invoke(stats); }
            catch { /* Prevent handlers from breaking undo system */ }
        }
        
        public static UndoRedoStats GetStats()
        {
            lock (_lock)
            {
                return new UndoRedoStats
                {
                    UndoCount = _undoStack.Count,
                    RedoCount = _redoStack.Count,
                    MemoryUsed = _totalMemoryUsed,
                    LastOperationTime = _operationTimer.Elapsed,
                    CompositeDepth = _compositeDepth
                };
            }
        }
        
        public static List<string> GetUndoHistory(int maxCount = 10)
        {
            lock (_lock)
            {
                var history = new List<string>();
                int count = Math.Min(maxCount, _undoStack.Count);
                
                for (int i = _undoStack.Count - count; i < _undoStack.Count; i++)
                {
                    history.Add(_undoStack[i].Label);
                }
                
                return history;
            }
        }
    }
    
    // Optimized transform action with merging
    public sealed class TransformAction : IEditorAction
    {
        public string Label { get; }
        public DateTime Timestamp { get; }
        public long MemoryFootprint => 200; // Approximate bytes
        
        private readonly uint _entityId;
        private readonly Xform _before, _after;
        private readonly TimeSpan _maxMergeAge = TimeSpan.FromSeconds(2);
        
        public TransformAction(string label, uint entityId, Xform before, Xform after)
        {
            Label = label;
            Timestamp = DateTime.UtcNow;
            _entityId = entityId;
            _before = before;
            _after = after;
        }
        
        public void Undo(Scene scene) => Apply(scene, _before);
        public void Redo(Scene scene) => Apply(scene, _after);
        
        private void Apply(Scene scene, Xform xf)
        {
            var entity = scene.GetById(_entityId);
            if (entity == null) return;
            
            entity.Transform.Position = xf.Pos;
            entity.Transform.Rotation = xf.Rot;
            entity.Transform.Scale = xf.Scl;
        }
        
        public bool CanMergeWith(IEditorAction other)
        {
            if (other is not TransformAction otherTransform) return false;
            if (_entityId != otherTransform._entityId) return false;
            if ((other.Timestamp - Timestamp).Duration() > _maxMergeAge) return false;
            
            return Label.Contains("Transform") && otherTransform.Label.Contains("Transform");
        }
        
        public IEditorAction? TryMergeWith(IEditorAction other)
        {
            if (!CanMergeWith(other) || other is not TransformAction otherTransform)
                return null;
                
            // Create merged action that goes from our "before" to other's "after"
            return new TransformAction(
                $"Transform Merged ({Label})",
                _entityId,
                _before, // Keep original starting state
                otherTransform._after // Use final ending state
            );
        }
    }
    
    
    // Circular buffer for efficient memory management
    public class CircularBuffer<T> : IEnumerable<T>
    {
        private readonly T[] _buffer;
        private int _head = 0;
        private int _tail = 0;
        private int _count = 0;
        private readonly int _capacity;
        
        public int Count => _count;
        public int Capacity => _capacity;
        
        public CircularBuffer(int capacity)
        {
            _capacity = capacity;
            _buffer = new T[capacity];
        }
        
        public void Add(T item)
        {
            _buffer[_tail] = item;
            _tail = (_tail + 1) % _capacity;
            
            if (_count < _capacity)
            {
                _count++;
            }
            else
            {
                _head = (_head + 1) % _capacity;
            }
        }
        
        public T RemoveLast()
        {
            if (_count == 0)
                throw new InvalidOperationException("Buffer is empty");
                
            _tail = (_tail - 1 + _capacity) % _capacity;
            var item = _buffer[_tail];
            _buffer[_tail] = default!;
            _count--;
            
            return item;
        }
        
        public T RemoveFirst()
        {
            if (_count == 0)
                throw new InvalidOperationException("Buffer is empty");
                
            var item = _buffer[_head];
            _buffer[_head] = default!;
            _head = (_head + 1) % _capacity;
            _count--;
            
            return item;
        }
        
        public T PeekLast()
        {
            if (_count == 0)
                throw new InvalidOperationException("Buffer is empty");
                
            int lastIndex = (_tail - 1 + _capacity) % _capacity;
            return _buffer[lastIndex];
        }
        
        public T PeekFirst()
        {
            if (_count == 0)
                throw new InvalidOperationException("Buffer is empty");
                
            return _buffer[_head];
        }
        
        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= _count)
                    throw new IndexOutOfRangeException();
                    
                int actualIndex = (_head + index) % _capacity;
                return _buffer[actualIndex];
            }
        }
        
        public void Clear()
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _head = _tail = _count = 0;
        }
        
        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < _count; i++)
            {
                yield return this[i];
            }
        }
        
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            => GetEnumerator();
    }
    
    public struct Xform
    {
        public Vector3 Pos;
        public Quaternion Rot;
        public Vector3 Scl;
    }
    
    public struct UndoRedoStats
    {
        public int UndoCount;
        public int RedoCount;
        public long MemoryUsed;
        public TimeSpan LastOperationTime;
        public int CompositeDepth;
    }
}
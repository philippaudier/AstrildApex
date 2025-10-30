using System;
using System.Collections.Generic;
using Engine.Scene;

namespace Editor.State
{
    public sealed class CompositeAction : IEditorAction
    {
        public string Label { get; }
        public DateTime Timestamp { get; }
        public long MemoryFootprint { get; private set; }
        
        private readonly List<IEditorAction> _actions = [];
        
        public int Count => _actions.Count;
        
        public CompositeAction(string label) : this(label, DateTime.UtcNow) { }
        
        public CompositeAction(string label, DateTime timestamp)
        {
            Label = label;
            Timestamp = timestamp;
        }
        
        public void Add(IEditorAction action)
        {
            _actions.Add(action);
            MemoryFootprint += action.MemoryFootprint + 8; // 8 bytes for reference
        }
        
        public void Undo(Scene scene)
        {
            // Undo in reverse order
            for (var i = _actions.Count - 1; i >= 0; i--)
            {
                try
                {
                    _actions[i].Undo(scene);
                }
                catch (Exception)
                {
                    // Continue with other actions
                }
            }
        }
        
        public void Redo(Scene scene)
        {
            // Redo in forward order
            for (var i = 0; i < _actions.Count; i++)
            {
                try
                {
                    _actions[i].Redo(scene);
                }
                catch (Exception)
                {
                    // Continue with other actions
                }
            }
        }
        
        public bool CanMergeWith(IEditorAction other)
        {
            // Only merge with other composites of same type within time window
            if (other is not CompositeAction otherComposite) return false;
            if ((other.Timestamp - Timestamp).Duration() > TimeSpan.FromSeconds(1)) return false;
            
            return Label.Contains("Transform") && otherComposite.Label.Contains("Transform");
        }
        
        public IEditorAction? TryMergeWith(IEditorAction other)
        {
            if (!CanMergeWith(other) || other is not CompositeAction otherComposite)
                return null;
                
            var merged = new CompositeAction($"Merged {Label}", Timestamp);
            
            // Add all actions from both composites
            foreach (var action in _actions)
                merged.Add(action);
            foreach (var action in otherComposite._actions)
                merged.Add(action);
                
            return merged;
        }
    }
}

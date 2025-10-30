using System;
using System.Collections.Generic;

namespace Engine.Input
{
    /// <summary>
    /// Action d'entrée configurable (à la Unity)
    /// Support des catégories et détection de conflits
    /// </summary>
    public sealed class InputAction
    {
        public string Name { get; }
        public ActionType Type { get; }
        public string Category { get; }
        public string Description { get; }

        private readonly List<InputBinding> _bindings = new();
        private bool _wasPressed;
        private bool _isPressed;
        private float _value;

        public event Action<InputAction>? Started;
        public event Action<InputAction>? Performed;
        public event Action<InputAction>? Cancelled;

        public InputAction(string name, ActionType type = ActionType.Button, string category = "General", string description = "")
        {
            Name = name;
            Type = type;
            Category = category;
            Description = string.IsNullOrEmpty(description) ? name : description;
        }

        public void AddBinding(InputBinding binding) => _bindings.Add(binding);
        public void RemoveBinding(InputBinding binding) => _bindings.Remove(binding);
        public void ClearBindings() => _bindings.Clear();
        public IReadOnlyList<InputBinding> Bindings => _bindings;

        /// <summary>
        /// Trouve tous les conflits de cette action avec les autres actions
        /// </summary>
        public List<InputConflict> FindConflicts(IEnumerable<InputAction> allActions)
        {
            var conflicts = new List<InputConflict>();
            
            foreach (var action in allActions)
            {
                if (action == this) continue;
                
                foreach (var myBinding in _bindings)
                {
                    foreach (var otherBinding in action.Bindings)
                    {
                        if (myBinding.ConflictsWith(otherBinding))
                        {
                            conflicts.Add(new InputConflict(this, action, myBinding, otherBinding));
                        }
                    }
                }
            }
            
            return conflicts;
        }

        public bool WasPressedThisFrame => _isPressed && !_wasPressed;
        public bool WasReleasedThisFrame => !_isPressed && _wasPressed;
        public bool IsPressed => _isPressed;
        public float Value => _value;

    // Unity-like convenience APIs
    public bool GetKeyDown() => WasPressedThisFrame;
    public bool GetKeyUp() => WasReleasedThisFrame;
    public bool GetKey() => IsPressed;
    public float GetValue() => Value;

        internal void Update()
        {
            _wasPressed = _isPressed;

            float maxAbs = 0f;
            float maxVal = 0f;
            bool anyActive = false;

            foreach (var binding in _bindings)
            {
                float v = binding.GetValue();
                float a = MathF.Abs(v);
                if (a > maxAbs) { maxAbs = a; maxVal = v; }
                if (a > 0.1f) anyActive = true; // IMPORTANT: valeur absolue (axes négatifs)
            }

            _value = maxVal;
            _isPressed = anyActive;

            // Émissions d'événements cohérentes:
            if (WasPressedThisFrame) Started?.Invoke(this);

            if (_isPressed)
            {
                // Pour Value: Performed tant que l’entrée est active;
                // Pour Button: Performed à l’activation (déjà couvert par WasPressedThisFrame ci-dessus)
                if (Type == ActionType.Value || WasPressedThisFrame)
                    Performed?.Invoke(this);
            }

            if (WasReleasedThisFrame) Cancelled?.Invoke(this);
        }
    }

    public enum ActionType { Button, Value }
    
    /// <summary>
    /// Représente un conflit entre deux bindings
    /// </summary>
    public class InputConflict
    {
        public InputAction Action1 { get; }
        public InputAction Action2 { get; }
        public InputBinding Binding1 { get; }
        public InputBinding Binding2 { get; }
        
        public InputConflict(InputAction action1, InputAction action2, InputBinding binding1, InputBinding binding2)
        {
            Action1 = action1;
            Action2 = action2;
            Binding1 = binding1;
            Binding2 = binding2;
        }
        
        public string GetDescription()
        {
            return $"'{Action1.Name}' and '{Action2.Name}' both use '{Binding1.GetDisplayName()}'";
        }
    }
}

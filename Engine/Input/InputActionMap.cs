using System;
using System.Collections.Generic;
using System.Linq;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Engine.Input
{
    /// <summary>
    /// Groupe d'actions d'entrée qui peuvent être activées/désactivées ensemble
    /// </summary>
    public sealed class InputActionMap
    {
        public string Name { get; }
        public bool IsEnabled { get; private set; }
        
        private readonly Dictionary<string, InputAction> _actions = new();

        public InputActionMap(string name)
        {
            Name = name;
            IsEnabled = true;
        }

        public InputAction CreateAction(string name, ActionType type = ActionType.Button)
        {
            if (_actions.ContainsKey(name))
            {
                throw new InvalidOperationException($"Action '{name}' already exists in map '{Name}'");
            }

            var action = new InputAction(name, type);
            _actions[name] = action;
            return action;
        }

        public InputAction? FindAction(string name)
        {
            return _actions.TryGetValue(name, out var action) ? action : null;
        }

        public InputAction this[string name] => _actions[name];

        public IEnumerable<InputAction> Actions => _actions.Values;

        public void Enable()
        {
            IsEnabled = true;
        }

        public void Disable()
        {
            IsEnabled = false;
        }

        internal void Update()
        {
            if (!IsEnabled) return;

            foreach (var action in _actions.Values)
            {
                action.Update();
            }
        }

        // Unity-like query helpers so code can poll action states
        public bool GetKeyDown(string actionName)
        {
            var action = FindAction(actionName);
            return action != null && action.WasPressedThisFrame;
        }

        public bool GetKeyUp(string actionName)
        {
            var action = FindAction(actionName);
            return action != null && action.WasReleasedThisFrame;
        }

        public bool GetKey(string actionName)
        {
            var action = FindAction(actionName);
            return action != null && action.IsPressed;
        }

        /// <summary>
        /// Configure une action avec des liaisons par défaut
        /// </summary>
        public void ConfigureAction(string actionName, params InputBinding[] bindings)
        {
            var action = FindAction(actionName);
            if (action == null)
            {
                action = CreateAction(actionName);
            }

            action.ClearBindings();
            foreach (var binding in bindings)
            {
                action.AddBinding(binding);
            }
        }

        /// <summary>
        /// Traite un input clavier et retourne true si consommé
        /// </summary>
        public bool ProcessKeyInput(Keys key)
        {
            if (!IsEnabled) return false;

            foreach (var action in _actions.Values)
            {
                foreach (var binding in action.Bindings)
                {
                    if (binding.Type == BindingType.Key && binding.Key == key)
                    {
                        // Vérifier les modificateurs si c'est un binding composé
                        if (binding.IsComposite)
                        {
                            var inputManager = InputManager.Instance;
                            if (inputManager != null && binding.Modifiers.All(mod => inputManager.IsKeyDown(mod)))
                            {
                                // Input consommé par cette action
                                return true;
                            }
                        }
                        else
                        {
                            // Binding simple consommé
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Traite un input souris et retourne true si consommé
        /// </summary>
        public bool ProcessMouseInput(MouseButton button)
        {
            if (!IsEnabled) return false;

            foreach (var action in _actions.Values)
            {
                foreach (var binding in action.Bindings)
                {
                    if (binding.Type == BindingType.MouseButton && binding.MouseButton == button)
                    {
                        // Vérifier les modificateurs si nécessaire
                        if (binding.IsComposite)
                        {
                            var inputManager = InputManager.Instance;
                            if (inputManager != null && binding.Modifiers.All(mod => inputManager.IsKeyDown(mod)))
                            {
                                return true;
                            }
                        }
                        else
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
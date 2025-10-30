using System;
using System.Collections.Generic;
using System.Linq;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Engine.Input
{
    /// <summary>
    /// Liaison entre une action et une entrée physique (touche, bouton souris, axe)
    /// Support des bindings composés (ex: Ctrl+S)
    /// </summary>
    public sealed class InputBinding
    {
        public string Path { get; }
        public BindingType Type { get; }
        
        // Pour les touches clavier
        public Keys Key { get; }
        
        // Pour les boutons souris
        public MouseButton MouseButton { get; }
        
        // Pour les axes souris
        public MouseAxis MouseAxis { get; }
        public float Scale { get; }
        
        // Pour les bindings composés (modificateurs)
        public List<Keys> Modifiers { get; }
        public bool IsComposite => Modifiers.Any();

        private InputBinding(string path, BindingType type, Keys key = Keys.Unknown, 
            MouseButton mouseButton = MouseButton.Left, MouseAxis mouseAxis = MouseAxis.X, 
            float scale = 1f, List<Keys>? modifiers = null)
        {
            Path = path;
            Type = type;
            Key = key;
            MouseButton = mouseButton;
            MouseAxis = mouseAxis;
            Scale = scale;
            Modifiers = modifiers ?? new List<Keys>();
        }

        public static InputBinding FromKey(Keys key, List<Keys>? modifiers = null)
        {
            string path;
            if (modifiers?.Any() == true)
            {
                var modifierPart = string.Join("+", modifiers.Select(m => m.ToString()));
                path = $"<Keyboard>/{modifierPart}+{key}";
            }
            else
            {
                path = $"<Keyboard>/{key}";
            }
            return new InputBinding(path, BindingType.Key, key: key, modifiers: modifiers);
        }

        public static InputBinding FromKey(KeyCode keyCode, List<Keys>? modifiers = null)
        {
            var key = keyCode.ToOpenTK();
            return FromKey(key, modifiers);
        }

        public static InputBinding FromMouseButton(MouseButton button, List<Keys>? modifiers = null)
        {
            string path;
            if (modifiers?.Any() == true)
            {
                var modifierPart = string.Join("+", modifiers.Select(m => m.ToString()));
                path = $"<Mouse>/{modifierPart}+{button}";
            }
            else
            {
                path = $"<Mouse>/{button}";
            }
            return new InputBinding(path, BindingType.MouseButton, mouseButton: button, modifiers: modifiers);
        }

        public static InputBinding FromMouseAxis(MouseAxis axis, float scale = 1f)
        {
            return new InputBinding($"<Mouse>/{axis}", BindingType.MouseAxis, mouseAxis: axis, scale: scale);
        }

        public float GetValue()
        {
            var inputManager = InputManager.Instance;
            if (inputManager == null) return 0f;

            // Vérifier les modificateurs si c'est un binding composé
            if (IsComposite && !AreModifiersPressed(inputManager))
                return 0f;

            return Type switch
            {
                BindingType.Key => inputManager.IsKeyDown(Key) ? 1f : 0f,
                BindingType.MouseButton => inputManager.IsMouseButtonDown(MouseButton) ? 1f : 0f,
                BindingType.MouseAxis => inputManager.GetMouseAxisDelta(MouseAxis) * Scale,
                _ => 0f
            };
        }

        private bool AreModifiersPressed(InputManager inputManager)
        {
            return Modifiers.All(modifier => inputManager.IsKeyDown(modifier));
        }

        /// <summary>
        /// Vérifie si ce binding entre en conflit avec un autre
        /// </summary>
        public bool ConflictsWith(InputBinding other)
        {
            if (Type != other.Type) return false;

            return Type switch
            {
                BindingType.Key => Key == other.Key && ModifiersEqual(other.Modifiers),
                BindingType.MouseButton => MouseButton == other.MouseButton && ModifiersEqual(other.Modifiers),
                BindingType.MouseAxis => MouseAxis == other.MouseAxis,
                _ => false
            };
        }

        private bool ModifiersEqual(List<Keys> otherModifiers)
        {
            return Modifiers.Count == otherModifiers.Count && 
                   Modifiers.All(otherModifiers.Contains);
        }

        /// <summary>
        /// Retourne une description lisible du binding pour l'UI
        /// </summary>
        public string GetDisplayName()
        {
            var name = Type switch
            {
                BindingType.Key => GetKeyDisplayName(Key),
                BindingType.MouseButton => GetMouseButtonDisplayName(MouseButton),
                BindingType.MouseAxis => GetMouseAxisDisplayName(MouseAxis),
                _ => Path
            };

            if (IsComposite)
            {
                var modifierNames = Modifiers.Select(GetKeyDisplayName);
                return $"{string.Join(" + ", modifierNames)} + {name}";
            }

            return name;
        }

        private static string GetKeyDisplayName(Keys key)
        {
            return key switch
            {
                Keys.LeftControl or Keys.RightControl => "Ctrl",
                Keys.LeftShift or Keys.RightShift => "Shift",
                Keys.LeftAlt or Keys.RightAlt => "Alt",
                Keys.Space => "Space",
                Keys.Enter => "Enter",
                Keys.Escape => "Esc",
                Keys.Tab => "Tab",
                Keys.Backspace => "Backspace",
                Keys.Delete => "Delete",
                Keys.Home => "Home",
                Keys.End => "End",
                Keys.PageUp => "Page Up",
                Keys.PageDown => "Page Down",
                Keys.Up => "↑",
                Keys.Down => "↓",
                Keys.Left => "←",
                Keys.Right => "→",
                _ => key.ToString()
            };
        }

        private static string GetMouseButtonDisplayName(MouseButton button)
        {
            return button switch
            {
                MouseButton.Left => "Left Click",
                MouseButton.Right => "Right Click",
                MouseButton.Middle => "Middle Click",
                _ => button.ToString()
            };
        }

        private static string GetMouseAxisDisplayName(MouseAxis axis)
        {
            return axis switch
            {
                MouseAxis.X => "Mouse X",
                MouseAxis.Y => "Mouse Y",
                MouseAxis.ScrollX => "Scroll X",
                MouseAxis.ScrollY => "Scroll Y",
                _ => axis.ToString()
            };
        }

        public override string ToString() => Path;

        
    }

    public enum BindingType
    {
        Key,
        MouseButton,
        MouseAxis
    }

    public enum MouseAxis
    {
        X,
        Y,
        ScrollX,
        ScrollY
    }
}
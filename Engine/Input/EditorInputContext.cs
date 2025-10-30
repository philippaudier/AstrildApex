using System;
using System.Collections.Generic;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Engine.Input
{
    /// <summary>
    /// Contexte d'input pour l'éditeur
    /// Priority: 100 (plus haute que PlayMode - prend priorité sur le gameplay)
    /// </summary>
    public class EditorInputContext : InputContext
    {
        private readonly Dictionary<string, Action> _shortcuts = new();

        public EditorInputContext() : base("Editor", (int)InputContextType.EditorBase)
        {
            InitializeDefaultShortcuts();
        }

        private void InitializeDefaultShortcuts()
        {
            // Shortcuts d'éditeur classiques (actifs seulement hors du mode jeu)
            RegisterShortcut("LeftControl+S", () => {});
            RegisterShortcut("LeftControl+O", () => {});
            RegisterShortcut("LeftControl+N", () => {});
            RegisterShortcut("LeftControl+Z", () => {});
            RegisterShortcut("LeftControl+Y", () => {});
            
            // Transform tools
            RegisterShortcut("Q", () => {});
            RegisterShortcut("W", () => {});
            RegisterShortcut("E", () => {});
            RegisterShortcut("R", () => {});
            
            // Viewport
            RegisterShortcut("F", () => {});
            RegisterShortcut("Alt+LeftMouse", () => {});
            
            // Debug
            RegisterShortcut("F9", () => {
                Console.WriteLine("[DEBUG] F9 pressed - Reinitializing PostProcessManager");
                Engine.Rendering.PostProcessManager.Reinitialize();
            });
        }

        private void RegisterShortcut(string keyCombo, Action action)
        {
            _shortcuts[keyCombo.ToLowerInvariant()] = action;
        }

        public override bool HandleKeyDown(Keys key, bool isRepeat)
        {
            if (!CanProcessInput() || isRepeat) return false;

            // Vérifier les modificateurs actifs
            var inputManager = InputManager.Instance;
            if (inputManager == null) return false;

            string keyCombo = BuildKeyCombo(key, inputManager);
            
            if (_shortcuts.TryGetValue(keyCombo.ToLowerInvariant(), out var action))
            {
                // Editor shortcut triggered: {keyCombo}
                action.Invoke();
                return true; // Input consommé
            }

            return false;
        }

        public override bool HandleMouseDown(MouseButton button)
        {
            if (!CanProcessInput()) return false;

            // Gérer les inputs souris de l'éditeur
            // Pour l'instant, ne consomme rien
            return false;
        }

        public override bool CanProcessInput()
        {
            // Editor input n'est actif que hors du mode jeu
            return IsEnabled; // IsEnabled sera configuré par SetPlayModeActive
        }

        private bool IsInPlayMode()
        {
            // Cette méthode n'est plus utilisée - la logique est maintenant dans IsEnabled
            return false;
        }

        private string BuildKeyCombo(Keys key, InputManager inputManager)
        {
            var combo = new List<string>();

            // Ajouter les modificateurs
            if (inputManager.IsKeyDown(Keys.LeftControl) || inputManager.IsKeyDown(Keys.RightControl))
                combo.Add("LeftControl");
            if (inputManager.IsKeyDown(Keys.LeftShift) || inputManager.IsKeyDown(Keys.RightShift))
                combo.Add("LeftShift");
            if (inputManager.IsKeyDown(Keys.LeftAlt) || inputManager.IsKeyDown(Keys.RightAlt))
                combo.Add("LeftAlt");

            // Ajouter la touche principale
            combo.Add(key.ToString());

            return string.Join("+", combo);
        }
    }
}
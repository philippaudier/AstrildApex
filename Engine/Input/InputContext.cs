using System;
using System.Collections.Generic;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Engine.Input
{
    /// <summary>
    /// Contexte d'input avec priorité (inspiré d'Unreal Engine et Unity)
    /// Les contextes à plus haute priorité consomment l'input en premier
    /// </summary>
    public abstract class InputContext
    {
        public string Name { get; }
        public int Priority { get; set; }
        public bool IsEnabled { get; set; } = true;

        protected InputContext(string name, int priority = 0)
        {
            Name = name;
            Priority = priority;
        }

        /// <summary>
        /// Traite l'input pour ce contexte. Retourne true si l'input est consommé.
        /// </summary>
        public abstract bool HandleKeyDown(Keys key, bool isRepeat);
        
        /// <summary>
        /// Traite l'input souris pour ce contexte. Retourne true si l'input est consommé.
        /// </summary>
        public abstract bool HandleMouseDown(MouseButton button);

        /// <summary>
        /// Appelé chaque frame pour mettre à jour le contexte
        /// </summary>
        public virtual void Update(float deltaTime) { }

        /// <summary>
        /// Vérifie si ce contexte peut traiter l'input actuellement
        /// </summary>
        public virtual bool CanProcessInput() => IsEnabled;
    }

    /// <summary>
    /// Types de contextes d'input par priorité (plus haut = plus prioritaire)
    /// </summary>
    public enum InputContextType
    {
        PlayMode = 0,     // Gameplay normal
        EditorBase = 100, // Éditeur de base
        EditorUI = 200,   // UI de l'éditeur (panels, fenêtres)
        InputCapture = 300, // Capture d'input pour réassignation
        Debug = 400       // Console de debug, overlays
    }
}
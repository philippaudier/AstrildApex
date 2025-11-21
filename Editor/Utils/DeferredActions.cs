using System;
using System.Collections.Generic;
using Editor.Logging;

namespace Editor.Utils
{
    /// <summary>
    /// Queue d'actions à exécuter sur le thread UI après la fin du frame ImGui.
    /// Permet de déférer les opérations bloquantes déclenchées depuis des callbacks ImGui
    /// (menu items, boutons) afin d'éviter d'appeler ForceRender() au milieu d'un NewFrame().
    /// </summary>
    public static class DeferredActions
    {
        private static readonly Queue<Action> _queue = new Queue<Action>();
        private static readonly object _lock = new object();

        public static void Enqueue(Action a)
        {
            if (a == null) return;
            lock (_lock) _queue.Enqueue(a);
        }

        /// <summary>
        /// Exécute toutes les actions en file. Appeler depuis la boucle principale (après ImGui frame).
        /// </summary>
        public static void ProcessAll()
        {
            while (true)
            {
                Action? act = null;
                lock (_lock)
                {
                    if (_queue.Count == 0) break;
                    act = _queue.Dequeue();
                }

                try { act?.Invoke(); } catch (Exception ex) { LogManager.LogError($"Action failed: {ex.Message}", "DeferredActions"); }
            }
        }
    }
}

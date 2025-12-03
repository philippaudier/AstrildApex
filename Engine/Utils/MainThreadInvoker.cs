using System;
using System.Collections.Concurrent;

namespace Engine.Utils
{
    /// <summary>
    /// Lightweight main-thread invoker for cross-thread notifications.
    /// Background threads can Enqueue actions which must be executed on the main thread.
    /// The application (Editor/Program) should call ProcessPending() once per frame.
    /// </summary>
    public static class MainThreadInvoker
    {
        private static readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();

        public static void Enqueue(Action a)
        {
            if (a == null) return;
            _queue.Enqueue(a);
        }

        public static void ProcessPending()
        {
            while (_queue.TryDequeue(out var act))
            {
                try { act?.Invoke(); } catch { }
            }
        }
    }
}

using System;

namespace Engine.Core
{
    /// <summary>
    /// Small runtime environment flags that can be set by the host (Editor vs Player).
    /// Editor sets `IsEditor = true` so engine components can opt-out of editor-only behavior.
    /// </summary>
    public static class RuntimeEnvironment
    {
        /// <summary>
        /// True when running inside the Editor process. Default: false.
        /// Editor should set this flag early at startup.
        /// </summary>
        public static bool IsEditor { get; set; } = false;
        
        /// <summary>
        /// True when the host is currently running game Play Mode (editor Play or player).
        /// Editor will set this to true when entering Play Mode so components can
        /// enable Play-only behaviors like PlayOnAwake.
        /// </summary>
        public static bool IsPlayMode { get; set; } = false;
    }
}

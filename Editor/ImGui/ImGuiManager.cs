using Editor.ImGuiBackend;

namespace Editor.Utils
{
    /// <summary>
    /// Global manager for accessing ImGuiController instance
    /// </summary>
    public static class ImGuiControllerManager
    {
        private static ImGuiController? _instance;
        
        /// <summary>
        /// Initialize the ImGui manager with the controller instance
        /// </summary>
        public static void Initialize(ImGuiController controller)
        {
            _instance = controller;
        }
        
        /// <summary>
        /// Get the ImGuiController instance
        /// </summary>
        public static ImGuiController? Instance => _instance;
        
        /// <summary>
        /// Check if ImGuiController is initialized
        /// </summary>
        public static bool IsInitialized => _instance != null;
    }
}

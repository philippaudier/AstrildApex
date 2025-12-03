using Engine.Input;
using Serilog;

namespace Engine.Core.Systems
{
    /// <summary>
    /// System wrapper for InputManager - handles input polling and state updates
    /// </summary>
    public sealed class InputUpdateSystem : IUpdateSystem
    {
        public string Name => "Input";
        public bool Enabled { get; set; } = true;
        public UpdatePhase Phase => UpdatePhase.PreUpdate;
        public int Priority => 0; // First thing to execute

        public void Update(float deltaTime)
        {
            try
            {
                InputManager.Instance?.Update();
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "[InputUpdateSystem] Failed to update input");
            }
        }
    }
}

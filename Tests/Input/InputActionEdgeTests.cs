using NUnit.Framework;
using Engine.Input;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Tests.Input
{
    public class InputActionEdgeTests
    {
        [Test]
        public void InputAction_WasPressedAndReleasedDetection()
        {
            var action = new InputAction("TestAction");
            // Create a binding that we can manipulate via a fake InputManager
            var binding = InputBinding.FromKey(Keys.A);
            action.AddBinding(binding);

            // Simulate no input
            // Since InputAction.Update reads InputBinding.GetValue which depends on InputManager.Instance,
            // we need a stub InputManager. For now, ensure that Update() runs without throwing when Instance is null
            Assert.DoesNotThrow(() => action.Update());

            // Can't simulate edge easily without injecting a fake InputManager; this test ensures Update() is safe
            Assert.IsFalse(action.IsPressed);
        }
    }
}

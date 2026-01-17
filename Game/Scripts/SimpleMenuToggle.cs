using System;
using Engine.Scripting;
using Engine.Input;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Game
{
    /// <summary>
    /// Simple ESC menu toggle - just logs for now
    /// Shows how to integrate with your Input system
    /// </summary>
    public class SimpleMenuToggle : MonoBehaviour
    {
        private bool _menuVisible = false;
        private bool _escWasPressed = false;

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            var inputManager = InputManager.Instance;
            if (inputManager == null) return;

            // ESC key toggle
            bool escPressed = inputManager.GetKey(Keys.Escape);

            // Edge detection: only toggle on key press (not hold)
            if (escPressed && !_escWasPressed)
            {
                ToggleMenu();
            }

            _escWasPressed = escPressed;
        }

        private void ToggleMenu()
        {
            _menuVisible = !_menuVisible;

            if (_menuVisible)
            {
                Console.WriteLine("========================================");
                Console.WriteLine("       📖 IN-GAME MENU (OPENED)");
                Console.WriteLine("========================================");
                Console.WriteLine(" [1] Inventory");
                Console.WriteLine(" [2] Character");
                Console.WriteLine(" [3] Map");
                Console.WriteLine(" [4] Settings");
                Console.WriteLine("========================================");
                Console.WriteLine(" Press ESC again to close");
                Console.WriteLine("========================================");

                // Unlock cursor when menu opens
                InputManager.Instance?.UnlockCursor();
            }
            else
            {
                Console.WriteLine("[Menu] Menu closed");
            }
        }
    }
}

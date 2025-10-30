using System.Numerics;
using System;

namespace Engine.UI
{
    public class StandaloneInputModule
    {
        private bool _prevLeftDown = false;
        public void Update()
        {
            // Very small shim: try to read from Engine.Input if available, else use Console mouse (not ideal)
            try
            {
                // Prefer the Engine.Input.InputManager if initialized
                var im = Engine.Input.InputManager.Instance;
                if (im == null) return;

                var pos = im.MousePosition;
                bool curDown = im.GetMouseButton(OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Left);
                bool pressed = curDown && !_prevLeftDown;
                bool released = !curDown && _prevLeftDown;
                _prevLeftDown = curDown;

                var pd = new PointerEventData { Position = pos, Pressed = pressed, Released = released };
                EventSystem.Instance.ProcessPointer(pd);
            }
            catch { }
        }

        // Note: StandaloneInputModule intentionally uses Engine.Input.InputManager APIs.
    }
}

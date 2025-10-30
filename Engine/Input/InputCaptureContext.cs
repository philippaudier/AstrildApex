using System;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Engine.Input
{
    /// <summary>
    /// Contexte d'input pour la capture/réassignation de touches
    /// Priority: 300 (très haute - consomme TOUT pendant la capture)
    /// </summary>
    public class InputCaptureContext : InputContext
    {
        private bool _isCapturing = false;
        private Action<Keys>? _onKeyCaptured;
        private Action<MouseButton>? _onMouseCaptured;
        private Action? _onCaptureCancelled;

        public InputCaptureContext() : base("InputCapture", (int)InputContextType.InputCapture)
        {
            // Ce contexte n'est enabled que pendant une capture active
            IsEnabled = false;
        }

        public void BeginCapture(Action<Keys> onKeyCaptured, Action<MouseButton> onMouseCaptured, Action onCaptureCancelled)
        {
            _isCapturing = true;
            IsEnabled = true;
            _onKeyCaptured = onKeyCaptured;
            _onMouseCaptured = onMouseCaptured;
            _onCaptureCancelled = onCaptureCancelled;
            
        }

        public void EndCapture()
        {
            _isCapturing = false;
            IsEnabled = false;
            _onKeyCaptured = null;
            _onMouseCaptured = null;
            _onCaptureCancelled = null;
            
        }

        public override bool HandleKeyDown(Keys key, bool isRepeat)
        {
            if (!_isCapturing || isRepeat) return false;


            if (key == Keys.Escape)
            {
                // Annuler la capture
                _onCaptureCancelled?.Invoke();
                EndCapture();
                return true; // Consomme l'escape
            }

            // Capturer la touche
            _onKeyCaptured?.Invoke(key);
            EndCapture();
            return true; // Consomme toujours l'input pendant la capture
        }

        public override bool HandleMouseDown(MouseButton button)
        {
            if (!_isCapturing) return false;


            _onMouseCaptured?.Invoke(button);
            EndCapture();
            return true; // Consomme toujours l'input pendant la capture
        }

        public override bool CanProcessInput()
        {
            return _isCapturing && IsEnabled;
        }
    }
}
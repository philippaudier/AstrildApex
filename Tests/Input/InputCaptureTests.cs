using System;
using NUnit.Framework;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Engine.Input;

namespace Tests.Input
{
    public class InputCaptureTests
    {
        [Test]
        public void InputCapture_CapturesKeyAndCancels()
        {
            var ctx = new InputCaptureContext();
            bool captured = false;
            Keys capturedKey = Keys.Unknown;
            ctx.BeginCapture(k => { captured = true; capturedKey = k; }, b => { }, () => { captured = false; });

            // Simulate a key press handler
            bool handled = ctx.HandleKeyDown(Keys.A, isRepeat: false);
            Assert.IsTrue(handled);
            Assert.IsTrue(captured);
            Assert.AreEqual(Keys.A, capturedKey);
        }

        [Test]
        public void InputCapture_EscapeCancels()
        {
            var ctx = new InputCaptureContext();
            bool cancelled = false;
            ctx.BeginCapture(k => { }, b => { }, () => { cancelled = true; });

            bool handled = ctx.HandleKeyDown(Keys.Escape, isRepeat: false);
            Assert.IsTrue(handled);
            Assert.IsTrue(cancelled);
        }
    }
}

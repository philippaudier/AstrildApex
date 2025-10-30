using System;
using OpenTK.Windowing.Desktop;
using OpenTK.Graphics.OpenGL4;
using Editor.UI;
using Editor.ImGuiBackend;

namespace Editor.Utils;

/// <summary>
/// Manages the progressive loading of the engine at startup.
/// Shows a progress popup and forces rendering updates during loading.
/// </summary>
public class LoadingManager
{
    private readonly ProgressPopup _progressPopup = new ProgressPopup();
    private readonly GameWindow _window;
    private readonly ImGuiController _imgui;
    private int _totalSteps = 10;
    private int _currentStepIndex = 0;

    public LoadingManager(GameWindow window, ImGuiController imgui)
    {
        _window = window;
        _imgui = imgui;
    }

    /// <summary>
    /// Shows the loading popup and initializes the progress.
    /// </summary>
    public void Start()
    {
        _currentStepIndex = 0;
        _progressPopup.Show("Loading AstrildApex Engine", "Starting...");
        RenderFrame();
    }

    /// <summary>
    /// Updates progress and displays the next loading step description.
    /// Forces a render to show the update immediately.
    /// </summary>
    public void UpdateStep(string description)
    {
        _currentStepIndex++;
        float progress = (float)_currentStepIndex / _totalSteps;
        _progressPopup.Update(progress, description);
        RenderFrame();
    }

    /// <summary>
    /// Completes the loading process and hides the popup.
    /// </summary>
    public void Complete()
    {
        _progressPopup.Update(1.0f, "Ready!");
        RenderFrame();
        System.Threading.Thread.Sleep(200); // Brief pause to show "Ready!"
        _progressPopup.Hide();
    }

    /// <summary>
    /// Forces an immediate render of the loading screen.
    /// This allows us to show progress updates during the blocking Load callback.
    /// </summary>
    private void RenderFrame()
    {
        try
        {
            // Process window events
            _window.ProcessEvents(0.0);

            // Clear and render
            GL.Viewport(0, 0, _window.ClientSize.X, _window.ClientSize.Y);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            // Start ImGui frame
            _imgui.NewFrame(0.016f); // ~60fps frame time

            // Render the progress popup
            _progressPopup.Render();

            // Render ImGui
            _imgui.Render();

            // Swap buffers to display
            _window.SwapBuffers();
        }
        catch
        {
            // Silently ignore rendering errors during startup
        }
    }
}

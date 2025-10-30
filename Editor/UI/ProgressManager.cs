using Editor.UI;
using OpenTK.Graphics.OpenGL4;
using Editor.ImGuiBackend;

namespace Editor.UI;

/// <summary>
/// Global singleton manager for progress popups.
/// Use this to show progress for any long-running operation from anywhere in the editor.
/// </summary>
public static class ProgressManager
{
    private static ProgressPopup? _currentPopup = null;
    private static readonly object _lock = new object();
    private static ImGuiController? _imguiController = null;

    /// <summary>
    /// Initializes the ProgressManager with required dependencies.
    /// Call this once during editor startup.
    /// </summary>
    public static void Initialize(ImGuiController imguiController)
    {
        _imguiController = imguiController;
    }

    /// <summary>
    /// Shows a progress popup with the given title and description.
    /// If a popup is already showing, it will be replaced.
    /// </summary>
    public static void Show(string title, string description = "")
    {
        lock (_lock)
        {
            if (_currentPopup == null)
            {
                _currentPopup = new ProgressPopup();
            }
            _currentPopup.Show(title, description);
        }
    }

    /// <summary>
    /// Updates the current progress (0.0 to 1.0) and optionally the description.
    /// </summary>
    public static void Update(float progress, string? description = null)
    {
        lock (_lock)
        {
            _currentPopup?.Update(progress, description);
        }
    }

    /// <summary>
    /// Hides the current progress popup.
    /// </summary>
    public static void Hide()
    {
        lock (_lock)
        {
            _currentPopup?.Hide();
        }
    }

    /// <summary>
    /// Returns true if a progress popup is currently visible.
    /// </summary>
    public static bool IsVisible
    {
        get
        {
            lock (_lock)
            {
                return _currentPopup?.IsVisible ?? false;
            }
        }
    }

    /// <summary>
    /// Renders the current progress popup. Call this from your main render loop.
    /// </summary>
    public static void Render()
    {
        lock (_lock)
        {
            _currentPopup?.Render();
        }
    }

    /// <summary>
    /// Forces an immediate render of the progress popup.
    /// This is useful for blocking operations that prevent the main render loop from running.
    /// </summary>
    public static void ForceRender()
    {
        var window = Program.GameWindow;
        if (window == null || _imguiController == null || !IsVisible) return;

        try
        {
            // Process window events to pump messages
            window.ProcessEvents(0.0);

            // Clear the screen
            GL.Viewport(0, 0, window.ClientSize.X, window.ClientSize.Y);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            // Start a fresh ImGui frame
            _imguiController.NewFrame(0.016f);

            // Render ONLY the progress popup (minimal ImGui frame)
            Render();

            // Finalize ImGui rendering
            _imguiController.Render();

            // Display the result
            window.SwapBuffers();
        }
        catch (Exception ex)
        {
            // Log but don't crash
            Console.WriteLine($"[ProgressManager] ForceRender failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Helper class for tracking multi-step operations with automatic progress calculation.
    /// </summary>
    public class StepTracker
    {
        private readonly string _title;
        private readonly int _totalSteps;
        private int _currentStep = 0;

        public StepTracker(string title, int totalSteps)
        {
            _title = title;
            _totalSteps = totalSteps;
            ProgressManager.Show(_title, "Starting...");
            ProgressManager.ForceRender(); // Show immediately
            System.Threading.Thread.Sleep(100); // Give time to see the popup
        }

        /// <summary>
        /// Advances to the next step with the given description.
        /// </summary>
        public void NextStep(string description)
        {
            _currentStep++;
            float progress = (float)_currentStep / _totalSteps;
            ProgressManager.Update(progress, description);
            ProgressManager.ForceRender(); // Update immediately
        }

        /// <summary>
        /// Completes the operation and hides the popup.
        /// </summary>
        public void Complete(string finalMessage = "Complete!")
        {
            ProgressManager.Update(1.0f, finalMessage);
            ProgressManager.ForceRender(); // Show completion
            System.Threading.Thread.Sleep(400); // Longer pause to ensure user sees completion
            ProgressManager.Hide();
        }
    }
}

using System;
using System.Threading.Tasks;
using Editor.UI;
using Editor.Logging;

namespace Editor.UI;

/// <summary>
/// Examples of how to use the ProgressManager for various operations.
/// </summary>
public static class ProgressManagerExample
{
    /// <summary>
    /// Example 1: Simple progress tracking with manual updates
    /// </summary>
    public static void ExampleSimpleProgress()
    {
        // Show the popup
        ProgressManager.Show("Loading Assets", "Preparing...");

        // Do some work
        System.Threading.Thread.Sleep(500);

        // Update progress
        ProgressManager.Update(0.25f, "Loading textures...");
        System.Threading.Thread.Sleep(500);

        ProgressManager.Update(0.50f, "Loading models...");
        System.Threading.Thread.Sleep(500);

        ProgressManager.Update(0.75f, "Loading shaders...");
        System.Threading.Thread.Sleep(500);

        ProgressManager.Update(1.0f, "Complete!");
        System.Threading.Thread.Sleep(300);

        // Hide the popup
        ProgressManager.Hide();
    }

    /// <summary>
    /// Example 2: Using StepTracker for automatic progress calculation
    /// </summary>
    public static void ExampleStepTracker()
    {
        // Create a step tracker with 5 steps
        var tracker = new ProgressManager.StepTracker("Compiling Shaders", 5);

        // Perform each step
        tracker.NextStep("Compiling vertex shader...");
        System.Threading.Thread.Sleep(400);

        tracker.NextStep("Compiling fragment shader...");
        System.Threading.Thread.Sleep(400);

        tracker.NextStep("Linking program...");
        System.Threading.Thread.Sleep(400);

        tracker.NextStep("Validating shader...");
        System.Threading.Thread.Sleep(400);

        tracker.NextStep("Caching compiled shader...");
        System.Threading.Thread.Sleep(400);

        // Complete
        tracker.Complete("Shaders compiled successfully!");
    }

    /// <summary>
    /// Example 3: Using progress during file import
    /// </summary>
    public static void ExampleFileImport(string[] files)
    {
        var tracker = new ProgressManager.StepTracker("Importing Files", files.Length);

        foreach (var file in files)
        {
            var fileName = System.IO.Path.GetFileName(file);
            tracker.NextStep($"Importing {fileName}...");

            // Simulate import work
            System.Threading.Thread.Sleep(300);
        }

        tracker.Complete($"Imported {files.Length} file(s)!");
    }

    /// <summary>
    /// Example 4: Integration in existing code (like Model Import)
    /// </summary>
    public static void ExampleModelImport(string sourcePath)
    {
        try
        {
            ProgressManager.Show("Importing Model", "Reading file...");

            // Step 1: Read file
            System.Threading.Thread.Sleep(200);
            ProgressManager.Update(0.2f, "Parsing geometry...");

            // Step 2: Parse geometry
            System.Threading.Thread.Sleep(300);
            ProgressManager.Update(0.4f, "Loading materials...");

            // Step 3: Load materials
            System.Threading.Thread.Sleep(200);
            ProgressManager.Update(0.6f, "Creating GPU buffers...");

            // Step 4: Create GPU buffers
            System.Threading.Thread.Sleep(300);
            ProgressManager.Update(0.8f, "Updating asset database...");

            // Step 5: Update database
            System.Threading.Thread.Sleep(200);
            ProgressManager.Update(1.0f, "Import complete!");

            System.Threading.Thread.Sleep(300);
            ProgressManager.Hide();

            LogManager.LogInfo($"✓ Model imported successfully from {sourcePath}", "ProgressManagerExample");
        }
        catch (Exception ex)
        {
            ProgressManager.Hide();
            LogManager.LogError($"✗ Failed to import model: {ex.Message}", "ProgressManagerExample");
        }
    }

    /// <summary>
    /// Example 5: Async/await pattern with progress
    /// </summary>
    public static async Task ExampleAsyncOperation()
    {
        var tracker = new ProgressManager.StepTracker("Processing Scene", 4);

        tracker.NextStep("Analyzing entities...");
        await Task.Delay(500);

        tracker.NextStep("Optimizing meshes...");
        await Task.Delay(500);

        tracker.NextStep("Building octree...");
        await Task.Delay(500);

        tracker.NextStep("Finalizing...");
        await Task.Delay(500);

        tracker.Complete("Scene processing complete!");
    }
}

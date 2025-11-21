using System;
using System.Threading;
using System.Threading.Tasks;
using Editor.UI;
using Editor.Utils;
using Editor.Logging;

namespace Editor.Tasks
{
    /// <summary>
    /// Runs the 3D model import pipeline on a background thread while keeping the UI responsive.
    /// Handles progress reporting and finalization on the main thread via DeferredActions.
    /// </summary>
    public static class ModelImportJob
    {
        /// <summary>
        /// Launch a background import job. Must be called on the main thread (typically through DeferredActions).
        /// </summary>
        /// <param name="sourceFile">Full path to the source model.</param>
        /// <param name="assetsRoot">Root directory of the project Assets folder.</param>
        /// <param name="targetFolder">Optional sub-folder where the model should be stored.</param>
        /// <param name="displayName">Friendly name used in progress UI.</param>
        /// <param name="onCompleted">Optional callback executed on the main thread when import succeeds.</param>
        public static void Run(string sourceFile, string assetsRoot, string? targetFolder, string displayName, Action<Guid>? onCompleted = null)
        {
            if (string.IsNullOrWhiteSpace(sourceFile))
                throw new ArgumentException("Source file is required", nameof(sourceFile));
            if (string.IsNullOrWhiteSpace(assetsRoot))
                throw new ArgumentException("Assets root is required", nameof(assetsRoot));

            ProgressManager.Show("Importing 3D Model", $"Preparing {displayName}...");
            ProgressManager.Update(0.05f, "Scheduling background import...");
            ProgressManager.ForceRender();

            Task.Run(() => ExecuteImport(sourceFile, assetsRoot, targetFolder, displayName, onCompleted));
        }

        private static void ExecuteImport(
            string sourceFile,
            string assetsRoot,
            string? targetFolder,
            string displayName,
            Action<Guid>? onCompleted)
        {
            Guid importedGuid = Guid.Empty;

            try
            {
                ProgressManager.Update(0.20f, $"Importing {displayName} assets...");
                importedGuid = Engine.Assets.ModelImporter.ImportModel(sourceFile, assetsRoot, targetFolder);
                ProgressManager.Update(0.80f, "Model imported, preparing asset database...");
            }
            catch (Exception ex)
            {
                var captured = ex;
                DeferredActions.Enqueue(() =>
                {
                    ProgressManager.Update(1f, $"Import failed: {captured.Message}");
                    ProgressManager.ForceRender();
                    Task.Run(() =>
                    {
                        Thread.Sleep(600);
                        DeferredActions.Enqueue(ProgressManager.Hide);
                    });
                    LogManager.LogError($"Import failed: {captured.Message}\n{captured.StackTrace}", "ModelImportJob");
                });
                return;
            }

            DeferredActions.Enqueue(() =>
            {
                try
                {
                    ProgressManager.Update(0.92f, "Refreshing asset database...");
                    ProgressManager.ForceRender();
                    Engine.Assets.AssetDatabase.Refresh();

                    ProgressManager.Update(1.0f, "Model imported successfully!");
                    ProgressManager.ForceRender();
                    onCompleted?.Invoke(importedGuid);
                }
                catch (Exception refreshEx)
                {
                    ProgressManager.Update(1.0f, $"Refresh failed: {refreshEx.Message}");
                    ProgressManager.ForceRender();
                    LogManager.LogError($"AssetDatabase.Refresh failed: {refreshEx.Message}\n{refreshEx.StackTrace}", "ModelImportJob");
                }
                finally
                {
                    Task.Run(() =>
                    {
                        Thread.Sleep(450);
                        DeferredActions.Enqueue(ProgressManager.Hide);
                    });
                }
            });
        }
    }
}

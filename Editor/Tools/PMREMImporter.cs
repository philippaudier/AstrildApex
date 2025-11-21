using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Engine.Assets;

namespace Editor.Tools
{
    public static class PMREMImporter
    {
        // Runs cmgen to generate PMREM assets and imports them into the AssetDatabase.
        // args: expect keys --cmgen <path> --input <path> --out <relativeAssetsPath> [--size <poweroftwo>] [--samples <n>]
        public static int RunFromArgs(string[] args)
        {
            string? cmgen = null;
            string? input = null;
            string outRel = "Assets/Generated/Env";
            int size = 512;
            int samples = 1024;

            for (int i = 0; i < args.Length; i++)
            {
                var a = args[i];
                if (string.Equals(a, "--cmgen", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) cmgen = args[++i];
                else if (string.Equals(a, "--input", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) input = args[++i];
                else if (string.Equals(a, "--out", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) outRel = args[++i];
                else if (string.Equals(a, "--size", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) int.TryParse(args[++i], out size);
                else if (string.Equals(a, "--samples", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) int.TryParse(args[++i], out samples);
            }

            if (string.IsNullOrEmpty(input))
            {
                Console.WriteLine("PMREM Importer: --input <path> is required");
                return 2;
            }

            if (string.IsNullOrEmpty(cmgen))
            {
                // try to find cmgen on PATH
                cmgen = "cmgen"; // rely on PATH
            }

            var appDir = AppContext.BaseDirectory;
            var deployDirTemp = Path.Combine(Path.GetTempPath(), "astrild_pmrem_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(deployDirTemp);

            try
            {
                // Build cmgen args: generate kubemap + DFG + prefiltered chain into deploy dir
                // Use --deploy which tells cmgen to produce everything needed for deployment
                var startInfo = new ProcessStartInfo
                {
                    FileName = cmgen,
                    Arguments = $"--deploy \"{deployDirTemp}\" -t cubemap -f ktx -s {size} --ibl-samples={samples} \"{input}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                Console.WriteLine($"Running: {startInfo.FileName} {startInfo.Arguments}");
                using var proc = Process.Start(startInfo);
                if (proc == null)
                {
                    Console.WriteLine("Failed to start cmgen process");
                    return 3;
                }
                proc.OutputDataReceived += (s, e) => { if (e.Data != null) Console.WriteLine(e.Data); };
                proc.ErrorDataReceived += (s, e) => { if (e.Data != null) Console.Error.WriteLine(e.Data); };
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                proc.WaitForExit();

                if (proc.ExitCode != 0)
                {
                    Console.WriteLine($"cmgen failed with exit code {proc.ExitCode}");
                    return proc.ExitCode;
                }

                // Also generate PNG face outputs for editor preview (fallback if engine can't load KTX)
                var facesTemp = Path.Combine(deployDirTemp, "faces_png");
                Directory.CreateDirectory(facesTemp);
                try
                {
                    var startInfo2 = new ProcessStartInfo
                    {
                        FileName = cmgen,
                        Arguments = $"--deploy \"{facesTemp}\" -t cubemap -f png -s {size} --ibl-samples={samples} \"{input}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var proc2 = Process.Start(startInfo2);
                    if (proc2 != null)
                    {
                        proc2.OutputDataReceived += (s, e) => { if (e.Data != null) Console.WriteLine(e.Data); };
                        proc2.ErrorDataReceived += (s, e) => { if (e.Data != null) Console.Error.WriteLine(e.Data); };
                        proc2.BeginOutputReadLine(); proc2.BeginErrorReadLine(); proc2.WaitForExit();
                        if (proc2.ExitCode != 0)
                        {
                            Console.WriteLine($"cmgen (png) failed with exit code {proc2.ExitCode}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"cmgen (png) run failed: {ex.Message}");
                }

                // Copy generated files into Assets
                var assetsRoot = AssetDatabase.AssetsRoot;
                if (string.IsNullOrWhiteSpace(assetsRoot) || !Directory.Exists(assetsRoot))
                {
                    Console.WriteLine("AssetDatabase not initialized or assets root missing");
                    return 4;
                }

                var destRoot = Path.Combine(assetsRoot, outRel.TrimStart('\u005C', '/').Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(destRoot);

                // Copy all files generated into destRoot
                foreach (var file in Directory.EnumerateFiles(deployDirTemp, "*.*", SearchOption.AllDirectories))
                {
                    var rel = Path.GetRelativePath(deployDirTemp, file);
                    var dest = Path.Combine(destRoot, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(dest) ?? destRoot);
                    File.Copy(file, dest, true);
                }

                // Refresh asset db index
                AssetDatabase.Refresh();
                Console.WriteLine($"PMREM import complete. Assets copied to: {destRoot}");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PMREM Importer failed: {ex.Message}\n{ex.StackTrace}");
                return 5;
            }
            finally
            {
                try { Directory.Delete(deployDirTemp, true); } catch { }
            }
        }
    }
}

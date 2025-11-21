using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using Editor.Logging;

namespace Editor.Icons
{
    /// <summary>
    /// Small utility to import an external SVG icon package into the project's icon folders.
    /// Copies SVGs into canonical locations and (optionally) calls Inkscape to produce PNGs.
    /// This is intentionally conservative: if Inkscape is missing we still copy SVGs so
    /// the existing runtime rasterizer (IconManager) can fallback.
    /// </summary>
    public static class IconImporter
    {
        /// <summary>
        /// Import all .svg files from sourceFolder into the project's icon folders.
        /// copySvgs: copy the .svg files to target folders.
        /// convertPng: if true and Inkscape is available, create PNGs at common sizes (24,48,72)
        /// Returns true if at least one icon was processed (copied or converted).
        /// </summary>
        public static bool ImportIcons(string sourceFolder, bool copySvgs = true, bool convertPng = true, bool clearExportIcons = true)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sourceFolder))
                {
                    LogManager.LogWarning("IconImporter: sourceFolder is empty");
                    return false;
                }

                if (!Directory.Exists(sourceFolder))
                {
                    LogManager.LogWarning($"IconImporter: source folder not found: {sourceFolder}");
                    return false;
                }

                var svgs = Directory.EnumerateFiles(sourceFolder, "*.svg", SearchOption.TopDirectoryOnly).ToList();
                if (svgs.Count == 0)
                {
                    LogManager.LogWarning($"IconImporter: no .svg files found in {sourceFolder}");
                    return false;
                }

                // Canonical target folders (IconManager already searches these)
                var cwd = Directory.GetCurrentDirectory();
                var targets = new List<string>
                {
                    Path.Combine(cwd, "export", "icons"),
                    Path.Combine(cwd, "Assets", "Icons"),
                    Path.Combine(cwd, "Editor", "Assets", "Icons")
                };

                foreach (var t in targets) Directory.CreateDirectory(t);

                // Allow explicit INKSCAPE_PATH environment variable to locate a non-PATH inkscape executable
                var envPath = Environment.GetEnvironmentVariable("INKSCAPE_PATH");
                var inkscapeExe = string.IsNullOrEmpty(envPath) ? "inkscape" : envPath;
                var inkscapeAvailable = convertPng && IsInkscapeAvailable(inkscapeExe);
                if (convertPng && !inkscapeAvailable)
                {
                    LogManager.LogInfo("IconImporter: Inkscape not found (on PATH or INKSCAPE_PATH) — skipping PNG conversion, SVGs will still be copied.");
                }

                // Optionally clear the export/icons folder to remove stale icons
                if (clearExportIcons)
                {
                    try
                    {
                        var exportDir = targets[0];
                        if (Directory.Exists(exportDir))
                        {
                            foreach (var f in Directory.EnumerateFiles(exportDir))
                            {
                                try { File.Delete(f); } catch { }
                            }
                            LogManager.LogInfo($"IconImporter: cleared {exportDir}");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogManager.LogWarning($"IconImporter: failed to clear export folder: {ex.Message}");
                    }
                }

                int processed = 0;

                foreach (var svgPath in svgs)
                {
                    var fileName = Path.GetFileName(svgPath);
                    foreach (var target in targets)
                    {
                        try
                        {
                            var dest = Path.Combine(target, fileName);
                            if (copySvgs)
                            {
                                File.Copy(svgPath, dest, overwrite: true);
                            }
                            processed++;
                        }
                        catch (Exception ex)
                        {
                            LogManager.LogWarning($"IconImporter: failed to copy {fileName} to {target}: {ex.Message}");
                        }
                    }

                    if (inkscapeAvailable)
                    {
                        // Produce PNG variants for the export/icons folder (first target)
                        var pngTarget = targets[0];
                        var baseName = Path.GetFileNameWithoutExtension(svgPath);
                        foreach (var size in new[] { 24, 48, 72 })
                        {
                            var outFile = Path.Combine(pngTarget, baseName + ".png");
                            try
                            {
                                var args = $"\"{svgPath}\" --export-filename=\"{outFile}\" --export-width={size} --export-height={size}";
                                var psi = new ProcessStartInfo(inkscapeExe, args)
                                {
                                    CreateNoWindow = true,
                                    UseShellExecute = false,
                                    RedirectStandardOutput = true,
                                    RedirectStandardError = true
                                };

                                using var p = Process.Start(psi);
                                if (p == null)
                                {
                                    LogManager.LogWarning($"IconImporter: could not start Inkscape for {fileName}");
                                    continue;
                                }

                                // Wait a short while for conversion to complete
                                if (!p.WaitForExit(15000))
                                {
                                    try { p.Kill(); } catch { }
                                    LogManager.LogWarning($"IconImporter: Inkscape timed out for {fileName}");
                                    continue;
                                }

                                // Read stderr for diagnostics
                                var stderr = p.StandardError.ReadToEnd();
                                // Treat the conversion as successful if the output PNG exists even when exit code != 0
                                if (!File.Exists(outFile))
                                {
                                    LogManager.LogWarning($"IconImporter: Inkscape failed to produce PNG for {fileName}. ExitCode={p.ExitCode}. Stderr={stderr}");
                                }
                                else
                                {
                                    if (p.ExitCode != 0)
                                    {
                                        LogManager.LogInfo($"IconImporter: Inkscape produced PNG for {fileName} with warnings. Stderr={stderr}");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                LogManager.LogWarning($"IconImporter: conversion exception for {fileName}: {ex.Message}");
                            }
                        }
                    }
                }

                LogManager.LogInfo($"IconImporter: processed {svgs.Count} svg(s) from {sourceFolder}");
                return processed > 0;
            }
            catch (Exception ex)
            {
                LogManager.LogError($"IconImporter error: {ex.Message}");
                return false;
            }
        }

        private static bool IsInkscapeAvailable(string executable = "inkscape")
        {
            try
            {
                var psi = new ProcessStartInfo(executable, "--version")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var p = Process.Start(psi);
                if (p == null) return false;
                if (!p.WaitForExit(3000)) try { p.Kill(); } catch { }
                return p.ExitCode == 0;
            }
            catch { return false; }
        }
    }
}

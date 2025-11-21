using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Editor.Utils
{
    /// <summary>
    /// Manages system fonts and provides font discovery capabilities
    /// </summary>
    public static class FontManager
    {
        private static List<FontInfo> _availableFonts = new();
        private static bool _initialized = false;

        public class FontInfo
        {
            public string Name { get; set; } = "";
            public string FilePath { get; set; } = "";
            public string Family { get; set; } = "";
            public bool IsBold { get; set; }
            public bool IsItalic { get; set; }

            public string DisplayName
            {
                get
                {
                    var style = "";
                    if (IsBold && IsItalic) style = " (Bold Italic)";
                    else if (IsBold) style = " (Bold)";
                    else if (IsItalic) style = " (Italic)";
                    return $"{Family}{style}";
                }
            }
        }

        /// <summary>
        /// Get all available fonts (scans on first call)
        /// </summary>
        public static List<FontInfo> GetAvailableFonts()
        {
            if (!_initialized)
            {
                ScanSystemFonts();
                _initialized = true;
            }
            return _availableFonts;
        }

        /// <summary>
        /// Scan Windows system fonts directory
        /// </summary>
        private static void ScanSystemFonts()
        {
            _availableFonts.Clear();

            // Add default ImGui font
            _availableFonts.Add(new FontInfo
            {
                Name = "Default (Proggy Clean)",
                FilePath = "",
                Family = "Default",
                IsBold = false,
                IsItalic = false
            });

            try
            {
                // Windows Fonts directory
                string fontsDir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

                if (!Directory.Exists(fontsDir))
                {
                    try { Engine.Utils.DebugLogger.Log($"[FontManager] Fonts directory not found: {fontsDir}"); } catch { }
                    return;
                }

                // Scan all .ttf and .otf files
                var fontFiles = new List<string>();
                fontFiles.AddRange(Directory.GetFiles(fontsDir, "*.ttf", SearchOption.TopDirectoryOnly));
                fontFiles.AddRange(Directory.GetFiles(fontsDir, "*.otf", SearchOption.TopDirectoryOnly));

                try { Engine.Utils.DebugLogger.Log($"[FontManager] Found {fontFiles.Count} font files in {fontsDir}"); } catch { }

                foreach (var filePath in fontFiles)
                {
                    try
                    {
                        var fileName = Path.GetFileNameWithoutExtension(filePath);
                        var fontInfo = ParseFontFileName(fileName, filePath);

                        if (fontInfo != null)
                        {
                            _availableFonts.Add(fontInfo);
                        }
                    }
                    catch (Exception ex)
                    {
                        try { Engine.Utils.DebugLogger.Log($"[FontManager] Error processing font {filePath}: {ex.Message}"); } catch { }
                    }
                }

                // Sort alphabetically by display name
                _availableFonts = _availableFonts.OrderBy(f => f.DisplayName).ToList();

                try { Engine.Utils.DebugLogger.Log($"[FontManager] Successfully loaded {_availableFonts.Count} fonts"); } catch { }
            }
            catch (Exception ex)
            {
                try { Engine.Utils.DebugLogger.Log($"[FontManager] Error scanning fonts: {ex.Message}"); } catch { }
            }
        }

        /// <summary>
        /// Parse font file name to extract family, bold, italic info
        /// </summary>
        private static FontInfo? ParseFontFileName(string fileName, string filePath)
        {
            // Common patterns in Windows font files:
            // Arial.ttf, ArialBold.ttf, Arial-Bold.ttf, Arial Bold.ttf
            // Roboto-Regular.ttf, Roboto-Bold.ttf, RobotoCondensed-Italic.ttf

            var lowerName = fileName.ToLower();

            // Check for bold/italic
            bool isBold = lowerName.Contains("bold") || lowerName.Contains("bd");
            bool isItalic = lowerName.Contains("italic") || lowerName.Contains("oblique") ||
                           lowerName.Contains("it") && !lowerName.Contains("digit");

            // Extract family name by removing style suffixes
            var family = fileName;

            // Remove common suffixes
            var suffixesToRemove = new[]
            {
                "-Regular", "-Bold", "-Italic", "-BoldItalic", "-Light", "-Medium", "-SemiBold", "-Black",
                "Regular", "Bold", "Italic", "BoldItalic", "Light", "Medium", "SemiBold", "Black",
                "-Bd", "Bd", "-It", "-BI",
                " Regular", " Bold", " Italic", " Light", " Medium"
            };

            foreach (var suffix in suffixesToRemove)
            {
                if (family.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    family = family.Substring(0, family.Length - suffix.Length);
                    break;
                }
            }

            // Clean up separators
            family = family.Replace("-", " ").Replace("_", " ");

            // Remove trailing spaces
            family = family.Trim();

            // Skip some system/symbol fonts that don't render well
            var skipFamilies = new[] { "wingding", "webdings", "symbol", "marlett", "holomdl2" };
            if (skipFamilies.Any(skip => family.ToLower().Contains(skip)))
            {
                return null;
            }

            return new FontInfo
            {
                Name = fileName,
                FilePath = filePath,
                Family = family,
                IsBold = isBold,
                IsItalic = isItalic
            };
        }

        /// <summary>
        /// Get font info by display name
        /// </summary>
        public static FontInfo? GetFontByDisplayName(string displayName)
        {
            var fonts = GetAvailableFonts();
            return fonts.FirstOrDefault(f => f.DisplayName == displayName);
        }

        /// <summary>
        /// Get font info by family name (returns regular variant if available)
        /// </summary>
        public static FontInfo? GetFontByFamily(string familyName)
        {
            var fonts = GetAvailableFonts();

            // Try to find regular variant first
            var regular = fonts.FirstOrDefault(f =>
                f.Family.Equals(familyName, StringComparison.OrdinalIgnoreCase) &&
                !f.IsBold && !f.IsItalic);

            if (regular != null)
                return regular;

            // Return any variant of this family
            return fonts.FirstOrDefault(f =>
                f.Family.Equals(familyName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Refresh the font list (rescan)
        /// </summary>
        public static void Refresh()
        {
            _initialized = false;
            GetAvailableFonts();
        }
    }
}

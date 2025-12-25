using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Editor.Logging;

namespace Editor.Utils
{
    /// <summary>
    /// Manages Google Fonts integration - downloads, caches, and provides access to Google Fonts
    /// </summary>
    public static class GoogleFontsManager
    {
        private static readonly string CacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AstrildApex", "GoogleFonts"
        );
        
        private static readonly string MetadataFile = Path.Combine(CacheDirectory, "fonts_metadata.json");
        private static readonly string ApiKey = "AIzaSyACfWe_ltuaCEIgoC1NZOfrYZ9q1gZBVJ0"; // Replace with your actual API key
        private static readonly string GoogleFontsApiUrl = "https://www.googleapis.com/webfonts/v1/webfonts";
        
        private static List<GoogleFontInfo> _availableFonts = new();
        private static Dictionary<string, string> _downloadedFonts = new(); // family -> file path
        private static bool _initialized = false;
        private static bool _isDownloading = false;
        
        public class GoogleFontInfo
        {
            public string Family { get; set; } = "";
            public string Category { get; set; } = "";
            public List<string> Variants { get; set; } = new();
            public Dictionary<string, string> Files { get; set; } = new(); // variant -> download URL
            public int Popularity { get; set; }
            public bool IsDownloaded { get; set; }
            
            public string DisplayName => Family;
            
            public override string ToString() => Family;
        }
        
        public class GoogleFontsMetadata
        {
            public List<GoogleFontInfo> Fonts { get; set; } = new();
            public DateTime LastUpdated { get; set; }
        }
        
        /// <summary>
        /// Initialize Google Fonts manager
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            
            try
            {
                Directory.CreateDirectory(CacheDirectory);
                LoadCachedMetadata();
                LoadDownloadedFonts();
                _initialized = true;
                
                LogManager.LogInfo($"Google Fonts initialized. Cache: {CacheDirectory}", "GoogleFonts");
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to initialize Google Fonts: {ex.Message}", "GoogleFonts");
            }
        }
        
        /// <summary>
        /// Get all available Google Fonts (from cache or API)
        /// </summary>
        public static List<GoogleFontInfo> GetAvailableFonts()
        {
            if (!_initialized) Initialize();
            return _availableFonts;
        }
        
        /// <summary>
        /// Get popular Google Fonts (top 100)
        /// </summary>
        public static List<GoogleFontInfo> GetPopularFonts(int count = 100)
        {
            return GetAvailableFonts()
                .OrderByDescending(f => f.Popularity)
                .Take(count)
                .ToList();
        }
        
        /// <summary>
        /// Check if a font is downloaded
        /// </summary>
        public static bool IsFontDownloaded(string family)
        {
            return _downloadedFonts.ContainsKey(family);
        }
        
        /// <summary>
        /// Get local file path for downloaded font
        /// </summary>
        public static string? GetFontPath(string family)
        {
            return _downloadedFonts.TryGetValue(family, out var path) ? path : null;
        }
        
        /// <summary>
        /// Download a Google Font
        /// </summary>
        public static async Task<string?> DownloadFontAsync(string family, string variant = "regular")
        {
            var font = _availableFonts.FirstOrDefault(f => f.Family == family);
            if (font == null)
            {
                LogManager.LogWarning($"Font '{family}' not found in Google Fonts", "GoogleFonts");
                return null;
            }
            
            if (!font.Files.TryGetValue(variant, out var downloadUrl))
            {
                LogManager.LogWarning($"Variant '{variant}' not found for '{family}'", "GoogleFonts");
                variant = "regular";
                if (!font.Files.TryGetValue(variant, out downloadUrl))
                {
                    LogManager.LogWarning($"No downloadable variant found for '{family}'", "GoogleFonts");
                    return null;
                }
            }
            
            try
            {
                var fileName = $"{family.Replace(" ", "")}-{variant}.ttf";
                var filePath = Path.Combine(CacheDirectory, fileName);
                
                if (File.Exists(filePath))
                {
                    _downloadedFonts[family] = filePath;
                    font.IsDownloaded = true;
                    LogManager.LogInfo($"Font '{family}' already downloaded", "GoogleFonts");
                    return filePath;
                }
                
                LogManager.LogInfo($"Downloading '{family}' from {downloadUrl}...", "GoogleFonts");
                
                using var client = new HttpClient();
                var response = await client.GetAsync(downloadUrl);
                response.EnsureSuccessStatusCode();
                
                var fontData = await response.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync(filePath, fontData);
                
                _downloadedFonts[family] = filePath;
                font.IsDownloaded = true;
                
                LogManager.LogInfo($"Font '{family}' downloaded to {filePath}", "GoogleFonts");
                return filePath;
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to download '{family}': {ex.Message}", "GoogleFonts");
                return null;
            }
        }
        
        /// <summary>
        /// Fetch Google Fonts list from API
        /// </summary>
        public static async Task<bool> FetchFontsListAsync()
        {
            if (_isDownloading) return false;
            _isDownloading = true;
            
            try
            {
                LogManager.LogInfo("Fetching Google Fonts list from API...", "GoogleFonts");
                
                using var client = new HttpClient();
                var url = $"{GoogleFontsApiUrl}?sort=popularity&key={ApiKey}";
                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<GoogleFontsApiResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (apiResponse?.Items == null || apiResponse.Items.Count == 0)
                {
                    LogManager.LogWarning("No fonts received from API", "GoogleFonts");
                    return false;
                }
                
                _availableFonts = apiResponse.Items.Select((item, index) => new GoogleFontInfo
                {
                    Family = item.Family,
                    Category = item.Category,
                    Variants = item.Variants,
                    Files = item.Files,
                    Popularity = apiResponse.Items.Count - index, // Higher = more popular
                    IsDownloaded = _downloadedFonts.ContainsKey(item.Family)
                }).ToList();
                
                // Save to cache
                var metadata = new GoogleFontsMetadata
                {
                    Fonts = _availableFonts,
                    LastUpdated = DateTime.Now
                };
                
                var metadataJson = JsonSerializer.Serialize(metadata, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                
                await File.WriteAllTextAsync(MetadataFile, metadataJson);
                
                LogManager.LogInfo($"Fetched {_availableFonts.Count} Google Fonts", "GoogleFonts");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to fetch Google Fonts: {ex.Message}", "GoogleFonts");
                return false;
            }
            finally
            {
                _isDownloading = false;
            }
        }
        
        /// <summary>
        /// Load cached fonts metadata
        /// </summary>
        private static void LoadCachedMetadata()
        {
            if (!File.Exists(MetadataFile))
            {
                LogManager.LogInfo("No cached Google Fonts metadata found", "GoogleFonts");
                return;
            }
            
            try
            {
                var json = File.ReadAllText(MetadataFile);
                var metadata = JsonSerializer.Deserialize<GoogleFontsMetadata>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (metadata?.Fonts != null)
                {
                    _availableFonts = metadata.Fonts;
                    LogManager.LogInfo($"Loaded {_availableFonts.Count} fonts from cache (updated {metadata.LastUpdated:g})", "GoogleFonts");
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to load cached metadata: {ex.Message}", "GoogleFonts");
            }
        }
        
        /// <summary>
        /// Scan cache directory for downloaded fonts
        /// </summary>
        private static void LoadDownloadedFonts()
        {
            try
            {
                var ttfFiles = Directory.GetFiles(CacheDirectory, "*.ttf", SearchOption.TopDirectoryOnly);
                
                foreach (var file in ttfFiles)
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    // Extract family name (before the last hyphen)
                    var lastHyphen = fileName.LastIndexOf('-');
                    if (lastHyphen > 0)
                    {
                        var family = fileName.Substring(0, lastHyphen);
                        // Add spaces back to family name (e.g., "RobotoMono" -> "Roboto Mono")
                        family = System.Text.RegularExpressions.Regex.Replace(family, "([a-z])([A-Z])", "$1 $2");
                        _downloadedFonts[family] = file;
                        
                        // Update IsDownloaded flag
                        var font = _availableFonts.FirstOrDefault(f => f.Family == family);
                        if (font != null)
                            font.IsDownloaded = true;
                    }
                }
                
                LogManager.LogInfo($"Found {_downloadedFonts.Count} downloaded Google Fonts", "GoogleFonts");
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to scan downloaded fonts: {ex.Message}", "GoogleFonts");
            }
        }
        
        /// <summary>
        /// Is currently downloading fonts list?
        /// </summary>
        public static bool IsDownloading => _isDownloading;
        
        // API response models
        private class GoogleFontsApiResponse
        {
            public List<GoogleFontApiItem> Items { get; set; } = new();
        }
        
        private class GoogleFontApiItem
        {
            public string Family { get; set; } = "";
            public string Category { get; set; } = "";
            public List<string> Variants { get; set; } = new();
            public Dictionary<string, string> Files { get; set; } = new();
        }
    }
}

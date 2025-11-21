using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using System.Text.Json;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Drawing;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;
using OpenGLPixelFormat = OpenTK.Graphics.OpenGL4.PixelFormat;
using System.Drawing.Imaging;
using System.Diagnostics;
using Editor.Logging;

namespace Editor.Icons
{
    /// <summary>
    /// Gestionnaire d'icônes SVG pour AstrildApex Editor
    /// Convertit les SVG en textures OpenGL et les cache pour utilisation dans ImGui
    /// </summary>
    public static class IconManager
    {
        #region Data Structures
        
        public class IconData
        {
            public string Name { get; set; } = "";
            public string Svg { get; set; } = "";
            public string ViewBox { get; set; } = "0 0 24 24";
        }
        
        public class IconSet
        {
            public string Name { get; set; } = "";
            public string Version { get; set; } = "";
            public string Description { get; set; } = "";
            public int Count { get; set; }
            public Dictionary<string, IconData> Icons { get; set; } = [];
        }
        
        public class IconTexture
        {
            public uint TextureId { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public string Name { get; set; } = "";
            public Vector2 Size => new(Width, Height);
        }
        
        #endregion
        
        #region Fields
        
    private static IconSet? _iconSet;
    // Optional alias map to force a specific filename for a given icon key
    private static readonly Dictionary<string, string> _aliasMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, IconTexture> _textureCache = [];
        private static readonly Dictionary<string, nint> _imguiTextureCache = [];
        private static bool _isInitialized = false;
        
        // Configuration par défaut
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };
        
        public static int DefaultIconSize { get; set; } = 16;
        public static Vector4 DefaultIconColor { get; set; } = new Vector4(0.8f, 0.8f, 0.8f, 1.0f);
    // Path used by the Import UI (editable at runtime)
    private static string _iconImportPath = "icons";
    // Preview support
    private static readonly List<uint> _previewTextureIds = new();
    private static readonly List<string> _previewNames = new();
    private static string _selectedFolderPath = string.Empty;
    private static int _previewLimit = 48;
        
        #endregion
        
        #region Initialization
        
        /// <summary>
        /// Initialise le gestionnaire d'icônes avec le fichier JSON
        /// </summary>
        /// <param name="iconsFilePath">Chemin vers le fichier astrild-apex-icons.json</param>
        public static bool Initialize(string iconsFilePath)
        {
            try
            {
                if (!File.Exists(iconsFilePath))
                {
                    return false;
                }
                
                var jsonContent = File.ReadAllText(iconsFilePath);
                _iconSet = JsonSerializer.Deserialize<IconSet>(jsonContent, _jsonOptions);
                
                if (_iconSet?.Icons == null)
                {
                    return false;
                }
                
                _isInitialized = true;

                // Try to load optional alias mapping (icon_aliases.json) next to the icons JSON or in common icons folders
                try
                {
                    var aliasesPaths = new[] {
                        Path.Combine(Path.GetDirectoryName(iconsFilePath) ?? string.Empty, "icon_aliases.json"),
                        Path.Combine(Directory.GetCurrentDirectory(), "export", "icons", "icon_aliases.json"),
                        Path.Combine(Directory.GetCurrentDirectory(), "Editor", "Assets", "Icons", "icon_aliases.json")
                    };
                    foreach (var ap in aliasesPaths)
                    {
                        if (File.Exists(ap))
                        {
                            var t = File.ReadAllText(ap);
                            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(t, _jsonOptions);
                            if (map != null)
                            {
                                foreach (var kv in map) _aliasMap[kv.Key] = kv.Value;
                            }
                            break;
                        }
                    }
                }
                catch { }
                
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        
        /// <summary>
        /// Libère toutes les ressources
        /// </summary>
        public static void Shutdown()
        {
            foreach (var texture in _textureCache.Values)
            {
                GL.DeleteTexture(texture.TextureId);
            }
            
            _textureCache.Clear();
            _imguiTextureCache.Clear();
            // also clear any preview textures
            try { ClearPreviewTextures(); } catch { }
            _isInitialized = false;
            
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Vérifie si une icône existe
        /// </summary>
        public static bool HasIcon(string iconKey)
        {
            return _isInitialized && _iconSet?.Icons.ContainsKey(iconKey) == true;
        }
        
        /// <summary>
        /// Obtient la liste de toutes les icônes disponibles
        /// </summary>
        public static IEnumerable<string> GetAvailableIcons()
        {
            return _isInitialized && _iconSet?.Icons != null 
                ? _iconSet.Icons.Keys 
                : Array.Empty<string>();
        }

        /// <summary>
        /// Récupère l'IntPtr d'une texture d'icône pour utilisation dans ImGui.Image()
        /// </summary>
        /// <param name="iconKey">Clé de l'icône (ex: "save", "load", "move")</param>
        /// <param name="size">Taille de rendu (défaut: DefaultIconSize)</param>
        /// <param name="color">Couleur de l'icône (défaut: DefaultIconColor)</param>
        /// <returns>IntPtr de la texture ou IntPtr.Zero si erreur</returns>
        public static nint GetIconTexture(string iconKey, int size = 0, Vector4? color = null)
        {
            if (!_isInitialized || _iconSet?.Icons == null) return nint.Zero;
            if (size == 0) size = DefaultIconSize;

            var cacheKey = $"{iconKey}_{size}";
            if (_imguiTextureCache.TryGetValue(cacheKey, out var ptr)) return ptr;
            // Prepare search directories (project-relative and common locations)
            string[] searchDirs = { @"export\icons", @"..\export\icons", @"Assets\Icons", @"Editor\Assets\Icons" };
            // Try to resolve a file path for the requested icon key (supports variants and normalization)
            string? resolved = FindIconFile(iconKey, searchDirs);
            if (!string.IsNullOrEmpty(resolved) && File.Exists(resolved))
            {
                if (OperatingSystem.IsWindowsVersionAtLeast(6, 1))
                {
                    using var bmp = new Bitmap(resolved);
                    var tex = new IconTexture { TextureId = CreateOpenGLTexture(bmp), Width = bmp.Width, Height = bmp.Height, Name = Path.GetFileNameWithoutExtension(resolved) };
                    var handle = new nint(tex.TextureId);
                    _textureCache[cacheKey] = tex;
                    _imguiTextureCache[cacheKey] = handle;
                    return handle;
                }
            }

            // 2) Fallback: rasterizer SVG (si tu veux le garder)
            if (_iconSet.Icons.TryGetValue(iconKey, out var icon) && OperatingSystem.IsWindowsVersionAtLeast(6, 1))
            {
                var tex = CreateTextureFromSvg(icon.Svg, size, color ?? DefaultIconColor);
                if (tex != null)
                {
                    var handle = new nint(tex.TextureId);
                    _textureCache[cacheKey] = tex;
                    _imguiTextureCache[cacheKey] = handle;
                    return handle;
                }
            }
            return nint.Zero;
        }

        // Try several heuristics to find an existing icon file (.png or .svg) for the given key
        private static string? FindIconFile(string iconKey, string[] searchDirs)
        {
            // If an alias mapping exists, try it first
            if (!string.IsNullOrWhiteSpace(iconKey) && _aliasMap.TryGetValue(iconKey, out var alias))
            {
                // If alias is absolute path and exists, return it
                try
                {
                    if (Path.IsPathRooted(alias) && File.Exists(alias)) return alias;
                }
                catch { }

                // Otherwise try the alias name in each search dir (with common extensions)
                foreach (var dir in searchDirs)
                {
                    if (string.IsNullOrEmpty(dir)) continue;
                    var aliasCandidates = new[] { alias, alias + ".png", alias + ".svg" };
                    foreach (var c in aliasCandidates)
                    {
                        var p = Path.Combine(dir, c);
                        if (File.Exists(p)) return p;
                    }
                }
            }
            // Candidate filename patterns to try, in order
            var candidates = new List<string>
            {
                "{0}.png",
                "{0}_active.png",
                "{0}_disabled.png",
                "{0}.svg",
                "{0}_active.svg",
                "{0}_disabled.svg"
            };

            // Normal forms to try
            var forms = new List<string> { iconKey, iconKey.ToLowerInvariant(), iconKey.Replace('-', '_'), iconKey.Replace('_', '-') };

            foreach (var dir in searchDirs)
            {
                foreach (var form in forms)
                {
                    foreach (var pattern in candidates)
                    {
                        var fname = string.Format(pattern, form);
                        var p = Path.Combine(dir, fname);
                        if (File.Exists(p)) return p;
                    }
                }

                // If still not found, attempt substring matching against filenames in dir
                try
                {
                    if (Directory.Exists(dir))
                    {
                        var files = Directory.EnumerateFiles(dir).ToList();
                        // exact token match
                        foreach (var f in files)
                        {
                            var n = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                            if (n == iconKey.ToLowerInvariant() || n.Contains(iconKey.ToLowerInvariant())) return f;
                        }
                        // tokened partial match
                        var tokens = iconKey.ToLowerInvariant().Split(new[] { '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var f in files)
                        {
                            var n = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                            bool all = true;
                            foreach (var t in tokens) if (!n.Contains(t)) { all = false; break; }
                            if (all) return f;
                        }
                    }
                }
                catch { }
            }

            return null;
        }
        
        /// <summary>
        /// Rendu d'une icône dans ImGui (méthode simplifiée)
        /// </summary>
        /// <param name="iconKey">Clé de l'icône</param>
        /// <param name="size">Taille en pixels (optionnel)</param>
        /// <param name="color">Couleur (optionnel)</param>
        public static void RenderIcon(string iconKey, int size = 0, Vector4? color = null)
        {
            var texturePtr = GetIconTexture(iconKey, size, color);
            if (texturePtr != nint.Zero)
            {
                var sizeVec = new Vector2(size == 0 ? DefaultIconSize : size);
                ImGui.Image(texturePtr, sizeVec);
            }
            else
            {
                // Fallback - afficher le nom de l'icône
                ImGui.Text($"[{iconKey}]");
            }
        }
        
        /// <summary>
        /// Rendu d'un bouton avec icône
        /// </summary>
        /// <param name="iconKey">Clé de l'icône</param>
        /// <param name="tooltip">Tooltip optionnel</param>
        /// <param name="size">Taille de l'icône (optionnel)</param>
        /// <returns>True si le bouton a été cliqué</returns>
        public static bool IconButton(string iconKey, string? tooltip = null, int size = 0)
        {
            var texturePtr = GetIconTexture(iconKey, size);
            var sizeVec = new Vector2(size == 0 ? DefaultIconSize : size);
            
            bool clicked;
            if (texturePtr != nint.Zero)
            {
                clicked = ImGui.ImageButton($"##{iconKey}", texturePtr, sizeVec);
            }
            else
            {
                // Fallback - bouton texte
                clicked = ImGui.Button($"[{iconKey}]");
            }
            
            if (!string.IsNullOrEmpty(tooltip) && ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(tooltip);
            }
            
            return clicked;
        }
        
        /// <summary>
        /// Obtient les données SVG brutes d'une icône
        /// </summary>
        public static string? GetIconSvg(string iconKey)
        {
            return _isInitialized && _iconSet?.Icons.TryGetValue(iconKey, out var icon) == true 
                ? icon.Svg 
                : null;
        }
        
        /// <summary>
        /// Obtient les informations d'une icône
        /// </summary>
        public static IconData? GetIconData(string iconKey)
        {
            return _isInitialized && _iconSet?.Icons.TryGetValue(iconKey, out var icon) == true 
                ? icon 
                : null;
        }

        /// <summary>
        /// Write a simple report listing icon keys and whether a file was found for them.
        /// Useful to generate alias mappings for missing keys.
        /// </summary>
        public static void GenerateMissingIconReport(string outPath)
        {
            try
            {
                using var sw = new StreamWriter(outPath, false);
                sw.WriteLine("iconKey,found,path");
                if (_iconSet?.Icons != null)
                {
                    string[] searchDirs = { Path.Combine(Directory.GetCurrentDirectory(), "export", "icons"), Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Icons"), Path.Combine(Directory.GetCurrentDirectory(), "Editor", "Assets", "Icons") };
                    foreach (var key in _iconSet.Icons.Keys)
                    {
                        var p = FindIconFile(key, searchDirs);
                        if (!string.IsNullOrEmpty(p) && File.Exists(p)) sw.WriteLine($"{key},true,{p}");
                        else sw.WriteLine($"{key},false,");
                    }
                }
                else
                {
                    sw.WriteLine("(no icons loaded)");
                }
            }
            catch { }
        }
        
        #endregion
        
        #region Private Implementation
        

        /// <summary>
        /// Crée une texture OpenGL à partir de données SVG
        /// </summary>
        [SupportedOSPlatform("windows6.1")]
        private static IconTexture? CreateTextureFromSvg(string svgContent, int size, Vector4 color)
        {
            try
            {
                using var bmp = new Bitmap(size, size, DrawingPixelFormat.Format32bppArgb);
                using var g = Graphics.FromImage(bmp);
                g.Clear(Color.Transparent);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                var drawColor = Color.FromArgb(
                    (int)(color.W * 255),
                    (int)(color.X * 255),
                    (int)(color.Y * 255),
                    (int)(color.Z * 255));

                RenderSvgToGraphics(g, svgContent, size, drawColor);

                var texId = CreateOpenGLTexture(bmp);
                return new IconTexture { TextureId = texId, Width = size, Height = size, Name = $"svg_{texId}" };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Rendu simple d'SVG vers Graphics (version simplifiée)
        /// </summary>
        [SupportedOSPlatform("windows6.1")]
        private static void RenderSvgToGraphics(Graphics g, string svg, int size, Color color)
{
    // 1) viewBox pour connaître l’échelle source
    //   <svg viewBox="0 0 24 24">  ou  '0 0 48 48'
    var vbMatch = Regex.Match(svg, @"viewBox\s*=\s*['""]\s*([0-9.\-]+)\s+([0-9.\-]+)\s+([0-9.\-]+)\s+([0-9.\-]+)\s*['""]",
                              RegexOptions.IgnoreCase);
    float vbW = 24f, vbH = 24f;
    if (vbMatch.Success)
    {
        float.TryParse(vbMatch.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture, out vbW);
        float.TryParse(vbMatch.Groups[4].Value, System.Globalization.CultureInfo.InvariantCulture, out vbH);
        if (vbW <= 0) vbW = 24f; if (vbH <= 0) vbH = 24f;
    }
    float sx = size / vbW, sy = size / vbH;

    // 2) itérer sur chaque <path ...>
    var pathTags = Regex.Matches(svg, @"<path[^>]*>", RegexOptions.IgnoreCase);
    if (pathTags.Count == 0)
    {
        // Fallback : carré plein centré
        var m = Math.Max(1, size / 6);
        g.FillRectangle(new SolidBrush(color), m, m, size - 2 * m, size - 2 * m);
        return;
    }

    foreach (Match tag in pathTags)
    {
        var pathTag = tag.Value;

        // d="..."  ou  d='...'
        var dMatch = Regex.Match(pathTag, @"\sd\s*=\s*['""]([^'""]+)['""]", RegexOptions.IgnoreCase);
        if (!dMatch.Success) continue;
        var d = dMatch.Groups[1].Value;

        // fill="none" ?
        bool fillNone = Regex.IsMatch(pathTag, @"fill\s*=\s*['""]\s*none\s*['""]", RegexOptions.IgnoreCase);

        // stroke-width
        var penPx = Math.Max(1f, size / 12f);
        var sw = Regex.Match(pathTag, @"stroke\-width\s*=\s*['""]([0-9.\-]+)['""]", RegexOptions.IgnoreCase);
        if (sw.Success && float.TryParse(sw.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture, out var swVal))
            penPx = Math.Max(1f, swVal * (sx + sy) * 0.5f);

        using var gp = BuildGraphicsPathFromSvgD(d, sx, sy);
        if (gp == null) continue;

        if (!fillNone)
        {
            using var brush = new SolidBrush(color);
            g.FillPath(brush, gp);
        }

        using var pen = new Pen(color, penPx)
        {
            LineJoin = System.Drawing.Drawing2D.LineJoin.Round,
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap   = System.Drawing.Drawing2D.LineCap.Round
        };
        g.DrawPath(pen, gp);
    }
}

        // --- Parser minimal : M/L/H/V/C/Q/Z + relatifs ---
        [SupportedOSPlatform("windows6.1")]
        private static System.Drawing.Drawing2D.GraphicsPath? BuildGraphicsPathFromSvgD(string d, float sx, float sy)
        {
            var toks = Regex.Matches(d, @"[A-Za-z]|-?\d*\.?\d+(?:e[-+]?\d+)?", RegexOptions.CultureInvariant);
            if (toks.Count == 0) return null;

            var gp = new System.Drawing.Drawing2D.GraphicsPath();
            int i = 0;
            bool IsCmd(string s) => s.Length == 1 && char.IsLetter(s[0]);
            float Next() => float.Parse(toks[i++].Value, System.Globalization.CultureInfo.InvariantCulture);

            var cur = new PointF(0, 0);
            var start = cur;

            while (i < toks.Count)
            {
                if (!IsCmd(toks[i].Value)) { i++; continue; }
                char cmd = toks[i++].Value[0];

                switch (cmd)
                {
                    case 'M':
                    case 'm':
                        {
                            float x = Next(), y = Next();
                            if (cmd == 'm') { x += cur.X; y += cur.Y; }
                            cur = new PointF(x, y); start = cur;
                            while (i < toks.Count && !IsCmd(toks[i].Value))
                            {
                                float x2 = Next(), y2 = Next();
                                if (cmd == 'm') { x2 += cur.X; y2 += cur.Y; }
                                gp.AddLine(S(cur, sx, sy), S(new PointF(x2, y2), sx, sy));
                                cur = new PointF(x2, y2);
                            }
                            break;
                        }
                    case 'L':
                    case 'l':
                        {
                            float x = Next(), y = Next();
                            if (cmd == 'l') { x += cur.X; y += cur.Y; }
                            gp.AddLine(S(cur, sx, sy), S(new PointF(x, y), sx, sy));
                            cur = new PointF(x, y); break;
                        }
                    case 'H':
                    case 'h':
                        {
                            float x = Next(); if (cmd == 'h') x += cur.X;
                            gp.AddLine(S(cur, sx, sy), S(new PointF(x, cur.Y), sx, sy));
                            cur = new PointF(x, cur.Y); break;
                        }
                    case 'V':
                    case 'v':
                        {
                            float y = Next(); if (cmd == 'v') y += cur.Y;
                            gp.AddLine(S(cur, sx, sy), S(new PointF(cur.X, y), sx, sy));
                            cur = new PointF(cur.X, y); break;
                        }
                    case 'C':
                    case 'c':
                        {
                            float x1 = Next(), y1 = Next();
                            float x2 = Next(), y2 = Next();
                            float x = Next(), y = Next();
                            if (cmd == 'c') { x1 += cur.X; y1 += cur.Y; x2 += cur.X; y2 += cur.Y; x += cur.X; y += cur.Y; }
                            gp.AddBezier(S(cur, sx, sy), S(new PointF(x1, y1), sx, sy), S(new PointF(x2, y2), sx, sy), S(new PointF(x, y), sx, sy));
                            cur = new PointF(x, y); break;
                        }
                    case 'Q':
                    case 'q':
                        {
                            float x1 = Next(), y1 = Next();
                            float x = Next(), y = Next();
                            if (cmd == 'q') { x1 += cur.X; y1 += cur.Y; x += cur.X; y += cur.Y; }
                            var c1 = new PointF(cur.X + 2f / 3f * (x1 - cur.X), cur.Y + 2f / 3f * (y1 - cur.Y));
                            var c2 = new PointF(x + 2f / 3f * (x1 - x), y + 2f / 3f * (y1 - y));
                            gp.AddBezier(S(cur, sx, sy), S(c1, sx, sy), S(c2, sx, sy), S(new PointF(x, y), sx, sy));
                            cur = new PointF(x, y); break;
                        }
                    case 'Z':
                    case 'z':
                        gp.CloseFigure(); cur = start; break;

                    // A/S/T non gérés : la plupart de tes icônes n’en ont pas — on peut étendre si besoin.
                    default: break;
                }
            }
            return gp;

            static PointF S(PointF p, float sx2, float sy2) => new PointF(p.X * sx2, p.Y * sy2);
        }

        
        
        /// <summary>
        /// Crée une texture OpenGL à partir d'une bitmap
        /// </summary>
        [SupportedOSPlatform("windows6.1")]
        private static uint CreateOpenGLTexture(Bitmap bitmap)
        {
            var textureId = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, textureId);
            
            // Convertir bitmap en données de texture
            var bmpData = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly,
                DrawingPixelFormat.Format32bppArgb);
            
            try
            {
                // System.Drawing bitmap avec Format32bppArgb est en fait BGRA dans l'ordre des octets
                // Mais on spécifie RGBA comme format interne pour OpenGL
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8,
                    bitmap.Width, bitmap.Height, 0,
                    OpenGLPixelFormat.Bgra, PixelType.UnsignedByte, bmpData.Scan0);
                
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            }
            finally
            {
                bitmap.UnlockBits(bmpData);
            }
            
            GL.BindTexture(TextureTarget.Texture2D, 0);
            return (uint)textureId;
        }
        
        #endregion
        
        #region Debug and Testing
        
        /// <summary>
        /// Fenêtre de test pour toutes les icônes
        /// </summary>
        public static void RenderIconsTestWindow()
        {
            if (!_isInitialized || _iconSet?.Icons == null)
            {
                if (ImGui.Begin("⚠ Icon Manager - Not Initialized"))
                {
                    ImGui.Text("IconManager is not initialized.");
                    ImGui.Text("Call IconManager.Initialize(path) first.");
                }
                ImGui.End();
                return;
            }
            
            if (ImGui.Begin($"🎨 Icon Manager - {_iconSet.Name}"))
            {
                // Small importer UI: allow importing an external icons folder into the project's icon folders
                ImGui.Text("Import icons from a folder (SVG package)");
                ImGui.InputText("Source folder##icon_import_path", ref _iconImportPath, 260);
                ImGui.SameLine();
                if (ImGui.Button("Load Preview"))
                {
                    // Load a preview of SVGs from the path in the input field
                    try
                    {
                        if (Directory.Exists(_iconImportPath))
                        {
                            _selectedFolderPath = _iconImportPath;
                            ClearPreviewTextures();
                            LoadPreviewFromFolder(_selectedFolderPath, Math.Min(_previewLimit, 48));
                        }
                        else
                        {
                            LogManager.LogWarning($"IconManager: preview path not found: {_iconImportPath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogManager.LogError($"Icon preview failed: {ex.Message}");
                    }
                }
                ImGui.SameLine();
                if (ImGui.Button("Open Folder"))
                {
                    try { Process.Start(new ProcessStartInfo("explorer", _iconImportPath) { UseShellExecute = true }); } catch { }
                }
                ImGui.SameLine();
                if (ImGui.Button("Import icons"))
                {
                    // Run import and report using the editor log manager
                    try
                    {
                        var ok = IconImporter.ImportIcons(_iconImportPath, copySvgs: true, convertPng: true, clearExportIcons: true);
                        if (ok) LogManager.LogInfo($"Icons imported from '{_iconImportPath}'");
                        else LogManager.LogWarning($"Icon import completed with no icons or errors. See detailed logs.");
                    }
                    catch (Exception ex)
                    {
                        LogManager.LogError($"Icon import failed: {ex.Message}");
                    }
                }
                ImGui.SameLine();
                if (ImGui.Button("Clear Preview")) { ClearPreviewTextures(); }

                ImGui.Text($"Version: {_iconSet.Version}");
                ImGui.Text($"Description: {_iconSet.Description}");
                ImGui.Text($"Icons loaded: {_iconSet.Count}");
                ImGui.Separator();
                
                ImGui.Text("Available Icons:");
                
                var iconsPerRow = 6;
                var iconIndex = 0;
                
                foreach (var (key, iconData) in _iconSet.Icons)
                {
                    if (iconIndex > 0 && iconIndex % iconsPerRow != 0)
                    {
                        ImGui.SameLine();
                    }
                    
                    if (ImGui.BeginChild($"icon_{key}", new Vector2(80, 60), ImGuiChildFlags.Borders))
                    {
                        // Centre l'icône
                        ImGui.SetCursorPosX((80 - DefaultIconSize) / 2);
                        ImGui.SetCursorPosY(8);
                        
                        RenderIcon(key);
                        
                        // Nom centré
                        var textWidth = ImGui.CalcTextSize(key).X;
                        ImGui.SetCursorPosX(Math.Max(0, (80 - textWidth) / 2));
                        ImGui.Text(key);
                    }
                    ImGui.EndChild();
                    
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip($"{iconData.Name}\nKey: {key}");
                    }
                    
                    iconIndex++;
                }

                // Preview area for selected folder (before import)
                if (!string.IsNullOrEmpty(_selectedFolderPath))
                {
                    ImGui.Separator();
                    ImGui.Text($"Preview (from: {_selectedFolderPath})");
                    var cols = 6;
                    for (int i = 0; i < _previewTextureIds.Count; i++)
                    {
                        if (i > 0 && i % cols != 0) ImGui.SameLine();
                        var texId = _previewTextureIds[i];
                        var name = _previewNames[i];
                        var handle = new nint(texId);
                        ImGui.BeginChild($"pv_{i}", new Vector2(80, 60), ImGuiNET.ImGuiChildFlags.None);
                        ImGui.SetCursorPosX((80 - DefaultIconSize) / 2);
                        ImGui.SetCursorPosY(8);
                        ImGui.Image(handle, new Vector2(DefaultIconSize, DefaultIconSize));
                        var txtWidth = ImGui.CalcTextSize(name).X;
                        ImGui.SetCursorPosX(Math.Max(0, (80 - txtWidth) / 2));
                        ImGui.Text(name);
                        ImGui.EndChild();
                    }
                }
            }
            ImGui.End();
        }

        /// <summary>
        /// Dispose preview textures and clear preview lists
        /// </summary>
        private static void ClearPreviewTextures()
        {
            try
            {
                foreach (var id in _previewTextureIds)
                {
                    try { GL.DeleteTexture((int)id); } catch { }
                }
            }
            catch { }
            _previewTextureIds.Clear();
            _previewNames.Clear();
        }

        /// <summary>
        /// Load small preview textures from a folder (png/svg). Will generate temporary GL textures.
        /// </summary>
        private static void LoadPreviewFromFolder(string folder, int limit)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;
            try
            {
                var files = Directory.EnumerateFiles(folder)
                    .Where(f => f.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                    .Take(limit)
                    .ToList();

                foreach (var f in files)
                {
                    try
                    {
                        if (f.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                        {
                            var svg = File.ReadAllText(f);
                            var tex = CreateTextureFromSvg(svg, DefaultIconSize, DefaultIconColor);
                            if (tex != null)
                            {
                                _previewTextureIds.Add(tex.TextureId);
                                _previewNames.Add(Path.GetFileNameWithoutExtension(f));
                            }
                        }
                        else
                        {
                            using var bmp = new Bitmap(f);
                            var texId = CreateOpenGLTexture(bmp);
                            _previewTextureIds.Add(texId);
                            _previewNames.Add(Path.GetFileNameWithoutExtension(f));
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }
        
        #endregion
    }
}
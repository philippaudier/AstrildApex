using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SkiaSharp;
using Svg.Skia;

class Program
{
    const string JsonPath  = @"Editor/Icons/astrild-apex-icons.json";
    const string OutputDir = @"export/icons";
    const int    PNG_SIZE  = 32;

    // Mets false si tu veux garder le style d'origine des SVG
    const bool   FORCE_OUTLINE_WHITE = true;
    const float  OUTLINE_STROKE_PX   = 2f;

    static readonly Regex ViewBoxRx = new(
        @"viewBox\s*=\s*['""]\s*([0-9.\-]+)\s+([0-9.\-]+)\s+([0-9.\-]+)\s+([0-9.\-]+)\s*['""]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    static int Main()
    {
        if (!File.Exists(JsonPath))
        {
            Console.Error.WriteLine($"ERROR: JSON not found: {JsonPath}");
            return 1;
        }

        Directory.CreateDirectory(OutputDir);

        var json = File.ReadAllText(JsonPath, Encoding.UTF8);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("icons", out var icons))
        {
            Console.Error.WriteLine("ERROR: no 'icons' field in JSON");
            return 1;
        }

        foreach (var prop in icons.EnumerateObject())
        {
            string key = prop.Name;
            if (!prop.Value.TryGetProperty("svg", out var svgEl))
            {
                Console.WriteLine($"[SKIP] {key}: no svg");
                continue;
            }

            string svg = svgEl.GetString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(svg))
            {
                Console.WriteLine($"[SKIP] {key}: empty svg");
                continue;
            }

            if (FORCE_OUTLINE_WHITE)
                svg = InjectOutlineStyle(svg, OUTLINE_STROKE_PX);

            string outPath = Path.Combine(OutputDir, $"{key}.png");
            try
            {
                RenderSvgToPng(svg, PNG_SIZE, outPath);
                Console.WriteLine($"[OK] {key} -> {outPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FAIL] {key}: {ex.Message}");
            }
        }

        return 0;
    }

    // Ajoute un <style> global non destructif : fill:none, stroke blanc, arrondis
    static string InjectOutlineStyle(string svg, float strokePx)
    {
        if (!svg.Contains("<svg", StringComparison.OrdinalIgnoreCase))
            return svg;

        if (!ViewBoxRx.IsMatch(svg))
            svg = Regex.Replace(svg, "<svg", "<svg viewBox=\"0 0 24 24\"", RegexOptions.IgnoreCase);

        var css = $"<style type=\"text/css\">*{{fill:none;stroke:#ffffff;stroke-width:{strokePx};stroke-linecap:round;stroke-linejoin:round}}</style>";
        svg = Regex.Replace(svg, "(<svg[^>]*>)", $"$1{css}", RegexOptions.IgnoreCase);

        // Quelques normalisations utiles
        svg = Regex.Replace(svg, "stroke\\s*=\\s*\"currentColor\"", "stroke=\"#ffffff\"", RegexOptions.IgnoreCase);
        svg = Regex.Replace(svg, "fill\\s*=\\s*\"(?!none)[^\"]*\"",  "fill=\"none\"",    RegexOptions.IgnoreCase);

        return svg;
    }

    static void RenderSvgToPng(string svgContent, int size, string outPath)
    {
        var sksvg = new SKSvg();
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(svgContent));

        var picture = sksvg.Load(ms);
        if (picture is null)
            throw new InvalidOperationException("Invalid SVG (picture null)");

        // ViewBox si dispo, sinon cull rect
        var src = picture.CullRect;
        if (src.Width <= 0 || src.Height <= 0)
            throw new InvalidOperationException("Invalid SVG bounds");

        // Calcul du scale et centrage pour sortir un carré size×size
        float sx = size / src.Width;
        float sy = size / src.Height;
        float scale = Math.Min(sx, sy);
        float tx = (size - src.Width  * scale) * 0.5f;
        float ty = (size - src.Height * scale) * 0.5f;

        using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        // Appliquer transforms : centre + scale + correction viewBox origin
        canvas.Translate(tx, ty);
        canvas.Scale(scale, scale);
        canvas.Translate(-src.Left, -src.Top);

        canvas.DrawPicture(picture);
        canvas.Flush();

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var fs = File.Open(outPath, FileMode.Create, FileAccess.Write, FileShare.None);
        data.SaveTo(fs);
    }
}

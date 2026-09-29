using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;
using SkiaSharp;

namespace Dcs.Ipia.Rendering
{
    /// <summary>Renders DCS-Common's authored two-column summary cards from a saved manifest.</summary>
    public static class SnapshotSummaryKneeboardRenderer
    {
        #region BEGIN Public
        /// <summary>Generates every overview PNG from the pinned workspace rather than imported images.</summary>
        public static IReadOnlyDictionary<string, byte[]> Generate(EditorWorkspaceSnapshot workspace)
        {
            ArgumentNullException.ThrowIfNull(workspace);
            if (workspace.Sources?.TryGetValue("config/kneeboard.json", out var source) != true || source is null)
                throw new InvalidDataException("A pinned authored kneeboard manifest is required.");
            using var document = JsonDocument.Parse(source);
            if (!document.RootElement.TryGetProperty("summaryPages", out var summaries) ||
                summaries.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("The authored summary page mapping is missing.");
            var pages = workspace.Pages.Where(x => x.Kind == KneeboardPageKind.Summary).ToArray();
            if (summaries.GetArrayLength() != pages.Length)
                throw new InvalidDataException("Summary pages differ from the pinned manifest.");
            var ordered = workspace.Pages.OrderBy(x => x.Order).ThenBy(x => x.StableKey, StringComparer.Ordinal).ToArray();
            var results = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var summary in summaries.EnumerateArray())
            {
                var key = summary.GetProperty("file").GetString() ?? "";
                if (key.Length == 0 || !key.All(x => char.IsAsciiLetterOrDigit(x) || x is '-' or '_') ||
                    pages.Count(x => x.StableKey == key && x.ModuleId == workspace.Configuration.Module.Id) != 1)
                    throw new InvalidDataException("Missing or unsafe authored summary page: " + key);
                if (!summary.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array ||
                    items.GetArrayLength() is < 1 or > 14)
                    throw new InvalidDataException("The summary requires one to fourteen authored device cards.");
                var index = Array.FindIndex(ordered, x => x.Kind == KneeboardPageKind.Summary && x.StableKey == key);
                var path = $"KNEEBOARD/{workspace.Configuration.Module.DcsId}/{key}.png";
                if (!results.TryAdd(path, Render(summary, items, index + 1, ordered.Length)))
                    throw new InvalidDataException("Duplicate summary page: " + key);
            }
            return results;
        }
        #endregion END Public
        #region BEGIN private helpers
        private static byte[] Render(JsonElement summary, JsonElement items, int page, int total)
        {
            using var stream = typeof(CompositeKneeboardRenderer).Assembly
                .GetManifestResourceStream("Dcs.Ipia.Rendering.Fonts.DejaVuSans-IPIA.ttf") ??
                throw new InvalidDataException("The pinned kneeboard font is missing.");
            using var typeface = SKTypeface.FromStream(stream) ??
                                 throw new InvalidDataException("The pinned kneeboard font is invalid.");
            using var surface = SKSurface.Create(new SKImageInfo(1200, 1600)) ??
                                throw new InvalidDataException("Could not allocate kneeboard surface.");
            var canvas = surface.Canvas;
            canvas.Clear(SKColor.Parse("#07111d"));
            Box(new SKRect(48, 48, 1152, 1552), 28, "#0a1629", "#2c4566", 3);
            Box(new SKRect(48, 48, 1152, 168), 28, "#12253d");
            Write(summary.GetProperty("title").GetString() ?? "", 96, 118, 38, "#f4f8fb");
            Write(summary.GetProperty("kicker").GetString() ?? "", 96, 154, 18, "#63d7ff");
            var index = 0;
            foreach (var item in items.EnumerateArray())
            {
                var x = 96 + index % 2 * 480;
                var y = 220 + index / 2 * 170;
                var (fill, stroke) = Accent(item.GetProperty("accent").GetString());
                Box(new SKRect(x, y, x + 420, y + 130), 16, fill, stroke, 2);
                Box(new SKRect(x + 16, y + 14, x + 102, y + 116), 12, "#07111d", stroke, 2);
                Write(item.GetProperty("key").GetString() ?? "", x + 59, y + 69, 22, stroke, SKTextAlign.Center);
                var lines = Wrap(item.GetProperty("text").GetString() ?? "", typeface);
                Write(lines[0], x + 122, y + 42, 18, "#f5f9ff", maxWidth: 290);
                if (lines.Length > 1)
                    Write(lines[1], x + 122, y + 70, 16, "#dbe7f4", maxWidth: 290);
                index++;
            }
            Write("DCS-Common shared kneeboard renderer", 96, 1520, 18, "#88a3ba");
            Write($"{page} / {total}", 1104, 1520, 18, "#88a3ba", SKTextAlign.Right);
            using var image = surface.Snapshot();
            using var png = image.Encode(SKEncodedImageFormat.Png, 100);
            return png.ToArray();

            void Box(SKRect bounds, float radius, string fill, string? stroke = null, float strokeWidth = 0)
            {
                using var background = new SKPaint { IsAntialias = true, Color = SKColor.Parse(fill) };
                canvas.DrawRoundRect(bounds, radius, radius, background);
                if (stroke is null)
                    return;
                using var border = new SKPaint { IsAntialias = true, Color = SKColor.Parse(stroke),
                                                 Style = SKPaintStyle.Stroke, StrokeWidth = strokeWidth };
                canvas.DrawRoundRect(bounds, radius, radius, border);
            }
            void Write(string value, float x, float y, float size, string color,
                       SKTextAlign alignment = SKTextAlign.Left, float maxWidth = 1040)
            {
                using var font = new SKFont(typeface, size);
                if (value.Length == 0 || font.GetGlyphs(value).Any(glyph => glyph == 0) ||
                    font.MeasureText(value) > maxWidth)
                    throw new InvalidDataException("Summary text is empty, unsupported, or too wide: " + value);
                using var paint = new SKPaint { IsAntialias = true, Color = SKColor.Parse(color) };
                canvas.DrawText(value, x, y, alignment, font, paint);
            }
        }
        private static (string Fill, string Stroke) Accent(string? name) => name switch
        {
            "cyan" => ("#15314e", "#46d8ff"),
            "gold" => ("#3e2d12", "#ffc95c"),
            "red" => ("#38131c", "#ff6b76"),
            "blue" => ("#142b52", "#579dff"),
            "orange" => ("#412511", "#ff9d45"),
            "purple" => ("#301b46", "#bd7cff"),
            "green" => ("#153526", "#5fda91"),
            _ => ("#15314e", "#46d8ff")
        };
        private static string[] Wrap(string value, SKTypeface typeface)
        {
            const float maxWidth = 290;
            const int maxCharacters = 28;
            using var firstFont = new SKFont(typeface, 18);
            using var secondFont = new SKFont(typeface, 16);
            var words = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            var lines = new List<string>();
            foreach (var word in words)
            {
                var font = lines.Count == 0 ? firstFont : secondFont;
                if (font.MeasureText(word) > maxWidth)
                    throw new InvalidDataException("Summary word is too wide for a card: " + word);
                if (lines.Count == 0)
                {
                    lines.Add(word);
                    continue;
                }
                font = lines.Count == 1 ? firstFont : secondFont;
                var candidate = lines[^1] + " " + word;
                if (candidate.Length <= maxCharacters && font.MeasureText(candidate) <= maxWidth)
                    lines[^1] = candidate;
                else
                    lines.Add(word);
            }
            if (lines.Count == 0)
                return [""];
            if (lines.Count <= 2)
                return lines.ToArray();
            var second = lines[1];
            while (second.Length + 1 > maxCharacters || secondFont.MeasureText(second + "…") > maxWidth)
            {
                var space = second.LastIndexOf(' ');
                if (space < 0)
                    throw new InvalidDataException("Summary text is too wide for a card: " + value);
                second = second[..space];
            }
            return [lines[0], second.TrimEnd('.', ',', ';', ':') + "…"];
        }
        #endregion END private helpers
    }
}

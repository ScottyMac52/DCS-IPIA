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
        public static IReadOnlyDictionary<string, byte[]> Generate(EditorWorkspaceSnapshot workspace, bool transparent = false)
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
                var rendered = Render(summary, items, index + 1, ordered.Length, transparent);
                for (var part = 0; part < rendered.Count; part++)
                {
                    var suffix = part == 0 ? "" : $"-{part + 1:D3}";
                    var path = $"KNEEBOARD/{workspace.Configuration.Module.DcsId}/{key}{suffix}.png";
                    if (!results.TryAdd(path, rendered[part]))
                        throw new InvalidDataException("Duplicate summary page: " + path);
                }
            }
            return results;
        }
        #endregion END Public
        #region BEGIN private helpers

        // Each row takes the height of its tallest card. Oversized rows continue on new
        // kneeboard pages, so even a single very long description cannot be clipped.


        private static List<byte[]> Render(JsonElement summary, JsonElement items, int page, int total, bool transparent)
        {
            using var stream = typeof(CompositeKneeboardRenderer).Assembly
                .GetManifestResourceStream("Dcs.Ipia.Rendering.Fonts.DejaVuSans-IPIA.ttf") ??
                throw new InvalidDataException("The pinned kneeboard font is missing.");
            using var typeface = SKTypeface.FromStream(stream) ??
                                 throw new InvalidDataException("The pinned kneeboard font is invalid.");

            var cards = items.EnumerateArray().Select(item => (
                Key: item.GetProperty("key").GetString() ?? "",
                Accent: item.GetProperty("accent").GetString() ?? "",
                Lines: Wrap(item.GetProperty("text").GetString() ?? "", typeface))).ToArray();
            var pages = LayoutCards(cards);
            return pages.Select((slices, part) =>
                RenderPage(summary, typeface, slices, page, total, part, pages.Count, transparent)).ToList();
        }

        private static List<List<(string Key, string Accent, string[] Lines, int X, int Y, int Height)>> LayoutCards(
            (string Key, string Accent, string[] Lines)[] cards)
        {
            var pages = new List<List<(string Key, string Accent, string[] Lines, int X, int Y, int Height)>> { new() };
            var y = 220;
            for (var index = 0; index < cards.Length; index += 2)
            {
                var hasRight = index + 1 < cards.Length;
                AddCardRow(cards[index], hasRight ? cards[index + 1] : default, hasRight, pages, ref y);
            }
            return pages;
        }

        private static void AddCardRow(
            (string Key, string Accent, string[] Lines) left,
            (string Key, string Accent, string[] Lines) right,
            bool hasRight,
            List<List<(string Key, string Accent, string[] Lines, int X, int Y, int Height)>> pages,
            ref int y)
        {
            const int top = 220, bottom = 1460, minHeight = 130, lineHeight = 28, rowGap = 40;
            var leftOffset = 0;
            var rightOffset = 0;
            var firstSegment = true;
            do
            {
                if (y + minHeight > bottom)
                {
                    pages.Add([]);
                    y = top;
                }
                var capacity = 2 + (bottom - y - minHeight) / lineHeight;
                var drawLeft = firstSegment || leftOffset < left.Lines.Length;
                var drawRight = hasRight && (firstSegment || rightOffset < right.Lines.Length);
                var leftCount = drawLeft ? Math.Min(capacity, left.Lines.Length - leftOffset) : 0;
                var rightCount = drawRight ? Math.Min(capacity, right.Lines.Length - rightOffset) : 0;
                var height = minHeight + Math.Max(0, Math.Max(leftCount, rightCount) - 2) * lineHeight;
                if (drawLeft)
                    pages[^1].Add((left.Key, left.Accent, left.Lines.Skip(leftOffset).Take(leftCount).ToArray(), 96, y, height));
                if (drawRight)
                    pages[^1].Add((right.Key, right.Accent, right.Lines.Skip(rightOffset).Take(rightCount).ToArray(), 576, y, height));
                leftOffset += leftCount;
                rightOffset += rightCount;
                y += height + rowGap;
                firstSegment = false;
            }
            while (leftOffset < left.Lines.Length || (hasRight && rightOffset < right.Lines.Length));
        }

        private static byte[] RenderPage(
            JsonElement summary, SKTypeface typeface,
            List<(string Key, string Accent, string[] Lines, int X, int Y, int Height)> slices,
            int page, int total, int part, int pageCount, bool transparent)
        {
            using var surface = SKSurface.Create(new SKImageInfo(1200, 1600)) ??
                                throw new InvalidDataException("Could not allocate kneeboard surface.");
            var canvas = surface.Canvas;
            canvas.Clear(transparent ? SKColors.Transparent : SKColor.Parse("#07111d"));
            DrawBox(canvas, new SKRect(48, 48, 1152, 1552), 28, transparent ? "#990a1629" : "#0a1629", "#2c4566", 3);
            DrawBox(canvas, new SKRect(48, 48, 1152, 168), 28, "#12253d");
            Write(canvas, typeface, summary.GetProperty("title").GetString() ?? "", 96, 118, 38, "#f4f8fb");
            Write(canvas, typeface, summary.GetProperty("kicker").GetString() ?? "", 96, 154, 18, "#63d7ff");
            foreach (var slice in slices)
                DrawCard(canvas, typeface, slice);
            Write(canvas, typeface, "DCS-Common shared kneeboard renderer", 96, 1520, 18, "#88a3ba");
            var footer = pageCount == 1 ? $"{page} / {total}" : $"{page} / {total} (part {part + 1} / {pageCount})";
            Write(canvas, typeface, footer, 1104, 1520, 18, "#88a3ba", SKTextAlign.Right);
            using var image = surface.Snapshot();
            using var png = image.Encode(SKEncodedImageFormat.Png, 100);
            return png.ToArray();
        }

        private static void DrawCard(SKCanvas canvas, SKTypeface typeface,
            (string Key, string Accent, string[] Lines, int X, int Y, int Height) slice)
        {
            const int lineHeight = 28;
            var (x, topY, height) = (slice.X, slice.Y, slice.Height);
            var (fill, stroke) = Accent(slice.Accent);
            DrawBox(canvas, new SKRect(x, topY, x + 420, topY + height), 16, fill, stroke, 2);
            DrawBox(canvas, new SKRect(x + 16, topY + 14, x + 102, topY + height - 14),
                12, "#07111d", stroke, 2);
            Write(canvas, typeface, slice.Key, x + 59, topY + 69, 22, stroke, SKTextAlign.Center);
            for (var line = 0; line < slice.Lines.Length; line++)
                Write(canvas, typeface, slice.Lines[line], x + 122, topY + 42 + line * lineHeight,
                    line == 0 ? 18 : 16, line == 0 ? "#f5f9ff" : "#dbe7f4", maxWidth: 290);
        }

        private static void DrawBox(SKCanvas canvas, SKRect bounds, float radius,
                                    string fill, string? stroke = null, float strokeWidth = 0)
        {
            using var background = new SKPaint { IsAntialias = true, Color = SKColor.Parse(fill) };
            canvas.DrawRoundRect(bounds, radius, radius, background);
            if (stroke is null) return;
            using var border = new SKPaint { IsAntialias = true, Color = SKColor.Parse(stroke),
                                             Style = SKPaintStyle.Stroke, StrokeWidth = strokeWidth };
            canvas.DrawRoundRect(bounds, radius, radius, border);
        }

        private static void Write(SKCanvas canvas, SKTypeface typeface, string value,
                                  float x, float textY, float size, string color,
                                  SKTextAlign alignment = SKTextAlign.Left, float maxWidth = 1040)
        {
            using var font = new SKFont(typeface, size);
            if (value.Length == 0 || font.GetGlyphs(value).Any(glyph => glyph == 0) ||
                font.MeasureText(value) > maxWidth)
                throw new InvalidDataException("Summary text is empty, unsupported, or too wide: " + value);
            using var paint = new SKPaint { IsAntialias = true, Color = SKColor.Parse(color) };
            canvas.DrawText(value, x, textY, alignment, font, paint);
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
            var lines = new List<string>();
            foreach (var word in value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            {
                // Split even unspaced tokens, instead of dropping them or letting them
                // spill beyond the right edge of the card.
                var fragments = new List<string>();
                var fragment = "";
                foreach (var rune in word.EnumerateRunes())
                {
                    var next = fragment + rune;
                    if (fragment.Length > 0 && (next.Length > maxCharacters ||
                                                firstFont.MeasureText(next) > maxWidth))
                    {
                        fragments.Add(fragment);
                        fragment = rune.ToString();
                    }
                    else fragment = next;
                }
                if (fragment.Length > 0) fragments.Add(fragment);
                for (var index = 0; index < fragments.Count; index++)
                {
                    var part = fragments[index];
                    if (firstFont.MeasureText(part) > maxWidth)
                        throw new InvalidDataException("Summary glyph is too wide for a card: " + part);
                    if (lines.Count == 0) { lines.Add(part); continue; }
                    if (index > 0) { lines.Add(part); continue; }
                    var font = lines.Count == 1 ? firstFont : secondFont;
                    var candidate = lines[^1] + " " + part;
                    if (candidate.Length <= maxCharacters && font.MeasureText(candidate) <= maxWidth)
                        lines[^1] = candidate;
                    else lines.Add(part);
                }
            }
            return lines.ToArray();
        }
        #endregion END private helpers
    }
}

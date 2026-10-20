using Heimdall.Application;
using SkiaSharp;

namespace Heimdall.Rendering
{
    /// <summary>Uses one physical-page drawing pipeline for paginated previews, individual PNGs and a complete PDF.</summary>
    public static class ReferenceSheetRenderer
    {
        #region BEGIN Public
        /// <summary>Renders one controller's complete reference, preserving overflow as additional pages.</summary>
        public static ReferenceSheetDocument Render(ReferenceSheetContent content, ReferenceSheetOptions options, byte[]? artwork = null)
        {
            Validate(content);
            var size = options.Paper switch
            {
                "A4" => (Width: 595.28f, Height: 841.89f),
                "Letter" => (Width: 612f, Height: 792f),
                _ => throw new InvalidDataException("Choose A4 or Letter paper.")
            };
            if (options.Landscape) size = (size.Height, size.Width);
            using var stream = typeof(ReferenceSheetRenderer).Assembly.GetManifestResourceStream("Heimdall.Rendering.Fonts.DejaVuSans-HEIMDALL-Regular.ttf")
                ?? throw new InvalidDataException("The reference-sheet font is missing.");
            using var typeface = SKTypeface.FromStream(stream) ?? throw new InvalidDataException("The reference-sheet font is invalid.");
            using var font = new SKFont(typeface, 10);
            using var heading = new SKFont(typeface, 12);
            var headers = Wrap("Profile: " + content.Title + "\nController: " + content.Controller + "\nSection: " + content.Section, heading, size.Width - 72);
            var top = 36 + headers.Length * 16 + 34;
            var capacity = (int)((size.Height - 40 - top) / 14);
            if (capacity < 8) throw new InvalidDataException("The profile or controller name is too long for this paper size.");
            var lines = TableLines(content, font, size.Width - 72);
            var tables = lines.Chunk(capacity).ToArray();
            if (tables.Length == 0) tables = [[("No assignments in this section.", "")]];
            var total = tables.Length + (options.Artwork ? 1 : 0);
            if (total > 64) throw new InvalidDataException("This reference exceeds 64 pages. Select a section or only assigned commands.");
            using var picture = options.Artwork ? DecodeArtwork(artwork) : null;
            using var output = new MemoryStream();
            using var document = SKDocument.CreatePdf(output) ?? throw new InvalidDataException("Could not create the PDF.");
            var pngs = new List<byte[]>();
            for (var page = 0; page < total; page++)
            {
                var imagePage = options.Artwork && page == 0;
                var rows = imagePage ? Array.Empty<(string Left, string Right)>() : tables[page - (options.Artwork ? 1 : 0)];
                var canvas = document.BeginPage(size.Width, size.Height);
                Draw(canvas, headers, rows, imagePage ? picture : null, size.Width, size.Height, top, page, total, font, heading);
                document.EndPage();
                pngs.Add(Png(headers, rows, imagePage ? picture : null, size.Width, size.Height, top, page, total, font, heading));
            }
            document.Close();
            return new(pngs, output.ToArray());
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void Validate(ReferenceSheetContent content)
        {
            ArgumentNullException.ThrowIfNull(content);
            if (string.IsNullOrWhiteSpace(content.Title) || string.IsNullOrWhiteSpace(content.Controller) || string.IsNullOrWhiteSpace(content.Section)
                || content.Title.Length > 4096 || content.Controller.Length > 4096 || content.Section.Length > 200
                || content.Rows is null || content.Rows.Count > 2000 || content.Rows.Any(x => x is null || Invalid(x.Command) || Invalid(x.Input) || Invalid(x.Details)))
                throw new InvalidDataException("Invalid or excessive reference-sheet content.");
        }
        private static bool Invalid(string text) => string.IsNullOrWhiteSpace(text) || text.Length > 4096;
        private static SKImage DecodeArtwork(byte[]? artwork) => artwork is null ? throw new InvalidDataException("Choose a visible drawing or use the assignment-table layout.")
            : SKImage.FromEncodedData(artwork) ?? throw new InvalidDataException("The reference artwork could not be decoded.");
        private static (string Left, string Right)[] TableLines(ReferenceSheetContent content, SKFont font, float width)
        {
            var lines = new List<(string Left, string Right)>();
            foreach (var row in content.Rows)
            {
                var left = Wrap(row.Command, font, width * .44f - 12);
                var right = Wrap(row.Input + "\n" + row.Details, font, width * .56f - 12);
                for (var index = 0; index < Math.Max(left.Length, right.Length); index++)
                    lines.Add((index < left.Length ? left[index] : "", index < right.Length ? right[index] : ""));
                lines.Add(("", ""));
            }
            return lines.ToArray();
        }
        private static string[] Wrap(string value, SKFont font, float width)
        {
            if (font.GetGlyphs(value.Replace("\n", "", StringComparison.Ordinal)).Any(x => x == 0))
                throw new InvalidDataException("Reference text contains characters unsupported by the pinned font.");
            var lines = new List<string>(); var current = "";
            foreach (var rune in value.EnumerateRunes())
            {
                if (rune.Value == '\n') { lines.Add(current); current = ""; continue; }
                var next = current + rune;
                if (current.Length > 0 && font.MeasureText(next) > width)
                { lines.Add(current); current = rune.ToString(); }
                else current = next;
            }
            if (current.Length > 0) lines.Add(current);
            return lines.ToArray();
        }
        private static byte[] Png(string[] headers, (string Left, string Right)[] rows, SKImage? artwork,
            float width, float height, int top, int page, int total, SKFont font, SKFont heading)
        {
            using var surface = SKSurface.Create(new SKImageInfo((int)Math.Round(width * 2), (int)Math.Round(height * 2)))
                ?? throw new InvalidDataException("Could not allocate a reference preview.");
            surface.Canvas.Scale(2);
            Draw(surface.Canvas, headers, rows, artwork, width, height, top, page, total, font, heading);
            using var image = surface.Snapshot();
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
            return encoded.ToArray();
        }
        private static void Draw(SKCanvas canvas, string[] headers, (string Left, string Right)[] rows, SKImage? artwork,
            float width, float height, int top, int page, int total, SKFont font, SKFont heading)
        {
            canvas.Clear(SKColors.White);
            using var ink = new SKPaint { Color = SKColor.Parse("#142337"), IsAntialias = true };
            using var accent = new SKPaint { Color = SKColor.Parse("#245c84"), IsAntialias = true };
            canvas.DrawRect(0, 0, width, 6, accent);
            for (var line = 0; line < headers.Length; line++)
                canvas.DrawText(headers[line], 36, 36 + line * 16, SKTextAlign.Left, heading, ink);
            if (artwork is not null)
            {
                var scale = Math.Min((width - 72) / artwork.Width, (height - 44 - top) / artwork.Height);
                var x = (width - artwork.Width * scale) / 2;
                canvas.DrawImage(artwork, new SKRect(x, top, x + artwork.Width * scale, top + artwork.Height * scale), new SKSamplingOptions(SKFilterMode.Linear));
            }
            else
            {
                var right = 36 + (width - 72) * .44f;
                canvas.DrawText("COMMAND", 36, top - 12, SKTextAlign.Left, font, accent);
                canvas.DrawText("INPUT / BEHAVIOUR", right, top - 12, SKTextAlign.Left, font, accent);
                canvas.DrawLine(36, top - 6, width - 36, top - 6, accent);
                for (var line = 0; line < rows.Length; line++)
                {
                    canvas.DrawText(rows[line].Left, 36, top + 10 + line * 14, SKTextAlign.Left, font, ink);
                    canvas.DrawText(rows[line].Right, right, top + 10 + line * 14, SKTextAlign.Left, font, ink);
                }
            }
            canvas.DrawText("HEIMDALL reference sheet" + (artwork is not null ? " · full assignments follow" : ""), 36, height - 18, SKTextAlign.Left, font, ink);
            canvas.DrawText((page + 1) + " / " + total, width - 36, height - 18, SKTextAlign.Right, font, ink);
        }
        #endregion END private helpers
    }
}

using System.Globalization;
using System.Xml.Linq;
using Dcs.Ipia.Application;
using SkiaSharp;

namespace Dcs.Ipia.Rendering
{
    /// <summary>Renders readable paginated tables without controller images or machine-local fonts.</summary>
    public static class GenericKneeboardRenderer
    {
        #region BEGIN Public
        /// <summary>Builds deterministic vector pages with explicit scope, chord and control identity.</summary>
        /// <param name="diagram">Verified generic controls and editable names.</param>
        /// <param name="page">Effective saved or staged labels and modifier descriptions.</param>
        /// <param name="footer">Optional preview attribution.</param>
        /// <param name="inputKeys">Optional subset for custom-mapping continuation pages.</param>
        /// <returns>Ordered 1200 by 1600 image-free page sources.</returns>
        public static IReadOnlyList<string> BuildPages(UiLayerDiagram diagram, KneeboardComposite page, string footer = "", IReadOnlySet<string>? inputKeys = null)
        {
            GenericHardwareLayout.Validate(diagram);
            if (diagram.GenericCapabilities is null) throw new InvalidDataException("A generic layout is required.");
            using var typeface = Typeface();
            using var font = new SKFont(typeface, 22);
            var groups = new List<(string Text, string Color)[]>();
            var lines = new List<(string Text, string Color)>();
            foreach (var control in diagram.Controls.Where(x => inputKeys is null || inputKeys.Contains(x.InputKey)).OrderBy(x => x.X).ThenBy(x => x.Y))
            {
                // Native order is preserved even after canonical snapshot serialization sorts control IDs.
                lines.AddRange(Wrap(control.InputKey + " · " + control.Label, font, 1070).Select(x => (x, "#ffc95c")));
                var variants = page.Labels.GetValueOrDefault(control.Control) ?? [];
                if (variants.Count == 0) lines.Add(("    Unassigned", "#94a3b8"));
                foreach (var variant in variants)
                {
                    var scope = variant.Scope == AssignmentLayer.Module ? "Aircraft" : "UI Layer";
                    var chord = variant.ModifierChord.Length == 0 ? "Base" : variant.ModifierChord.Replace("+", " + ", StringComparison.Ordinal);
                    lines.AddRange(Wrap(scope + " / " + chord + ": " + variant.Text, font, 1030).Select(x => ("    " + x, variant.Color)));
                }
                lines.Add(("", "#ffffff"));
                groups.Add(lines.ToArray());
                lines.Clear();
            }
            if (page.Legend.Count > 0)
            {
                lines.Add(("MODIFIERS", "#ffc95c"));
                foreach (var entry in page.Legend)
                    lines.AddRange(Wrap(entry.Label, font, 1070).Select(x => (x, entry.Color)));
            }
            if (lines.Count > 0) groups.Add(lines.ToArray());
            var paginated = Paginate(groups);
            var count = paginated.Count;
            var result = new List<string>();
            XNamespace ns = "http://www.w3.org/2000/svg";
            for (var index = 0; index < count; index++)
            {
                var svg = new XElement(ns + "svg", new XAttribute("viewBox", "0 0 1200 1600"),
                    new XAttribute("width", 1200), new XAttribute("height", 1600),
                    new XElement(ns + "rect", new XAttribute("width", 1200), new XAttribute("height", 1600), new XAttribute("fill", "#06101d")));
                using var titleFont = new SKFont(typeface, 30);
                var titleLines = Wrap(page.Title, titleFont, 1090);
                Text(svg, titleLines[0], 54, 55, 30, "#ffffff", ns);
                if (titleLines.Count > 1) Text(svg, titleLines[1], 54, 90, 30, "#ffffff", ns);
                Text(svg, page.Kicker + " · " + (index + 1).ToString(CultureInfo.InvariantCulture) + " / " + count.ToString(CultureInfo.InvariantCulture), 54, 135, 22, "#ffc95c", ns);
                foreach (var (line, offset) in paginated[index].Select((value, offset) => (value, offset)))
                    Text(svg, line.Text, 54, 175 + offset * 32, 22, line.Color, ns);
                Text(svg, footer, 54, 1570, 18, "#94a3b8", ns);
                result.Add(svg.ToString(SaveOptions.DisableFormatting));
            }
            return result;
        }

        /// <summary>Rasterizes every page with the pinned HEIMDALL font and fixed readable text size.</summary>
        /// <param name="diagram">Generic layout.</param>
        /// <param name="page">Projected current labels.</param>
        /// <param name="footer">Optional preview attribution.</param>
        /// <param name="inputKeys">Optional subset for custom-mapping continuation pages.</param>
        /// <returns>All ordered PNG pages without any filesystem reads.</returns>
        public static IReadOnlyList<byte[]> RenderPages(UiLayerDiagram diagram, KneeboardComposite page, string footer = "", IReadOnlySet<string>? inputKeys = null, bool transparent = false)
        {
            using var typeface = Typeface();
            var result = new List<byte[]>();
            foreach (var source in BuildPages(diagram, page, footer, inputKeys))
            {
                using var surface = SKSurface.Create(new SKImageInfo(1200, 1600)) ?? throw new InvalidDataException("Could not allocate generic kneeboard.");
                surface.Canvas.Clear(transparent ? SKColors.Transparent : SKColor.Parse("#06101d"));
                foreach (var text in XDocument.Parse(source).Descendants().Where(x => x.Name.LocalName == "text"))
                {
                    using var font = new SKFont(typeface, float.Parse(text.Attribute("font-size")!.Value, CultureInfo.InvariantCulture));
                    using var paint = new SKPaint { Color = SKColor.Parse(text.Attribute("fill")!.Value), IsAntialias = true };
                    if (font.GetGlyphs(text.Value).Any(x => x == 0)) throw new InvalidDataException("Generic text contains a glyph absent from the pinned font.");
                    surface.Canvas.DrawText(text.Value, float.Parse(text.Attribute("x")!.Value, CultureInfo.InvariantCulture),
                        float.Parse(text.Attribute("y")!.Value, CultureInfo.InvariantCulture), SKTextAlign.Left, font, paint);
                }
                using var image = surface.Snapshot();
                using var png = image.Encode(SKEncodedImageFormat.Png, 100);
                result.Add(png.ToArray());
            }
            return result;
        }
        #endregion END Public
        #region BEGIN private helpers
        private static List<List<(string Text, string Color)>> Paginate(List<(string Text, string Color)[]> groups)
        {
            List<List<(string Text, string Color)>> pages = [[]];
            foreach (var group in groups)
            {
                if (group.Length <= 42)
                {
                    if (pages[^1].Count + group.Length > 42) pages.Add([]);
                    pages[^1].AddRange(group);
                    continue;
                }
                // Keep normal controls together. Oversized chord groups repeat their input heading on continuation pages.
                if (pages[^1].Count > 0) pages.Add([]);
                var chunks = group.Chunk(41).ToArray();
                for (var index = 0; index < chunks.Length; index++)
                {
                    if (index > 0) { pages.Add([]); pages[^1].Add(group[0]); }
                    pages[^1].AddRange(chunks[index]);
                }
            }
            return pages;
        }
        private static SKTypeface Typeface() => SKTypeface.FromStream(typeof(GenericKneeboardRenderer).Assembly
            .GetManifestResourceStream("Dcs.Ipia.Rendering.Fonts.DejaVuSans-IPIA.ttf")!) ?? throw new InvalidDataException("The pinned font is missing.");
        private static void Text(XElement svg, string value, int x, int y, int size, string color, XNamespace ns) =>
            svg.Add(new XElement(ns + "text", new XAttribute("x", x), new XAttribute("y", y), new XAttribute("font-size", size),
                new XAttribute("fill", color), value));
        private static List<string> Wrap(string text, SKFont font, float width)
        {
            var lines = new List<string>();
            var current = "";
            foreach (var rune in text.EnumerateRunes())
            {
                var next = current + rune;
                if (current.Length > 0 && font.MeasureText(next) > width)
                {
                    lines.Add(current);
                    current = rune.ToString();
                }
                else current = next;
            }
            lines.Add(current);
            return lines;
        }
        #endregion END private helpers
    }
}


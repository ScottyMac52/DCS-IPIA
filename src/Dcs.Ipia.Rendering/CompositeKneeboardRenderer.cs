using System.Globalization;
using System.Security;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Dcs.Ipia.Application;
using SkiaSharp;

namespace Dcs.Ipia.Rendering
{
    /// <summary>Builds one DCS-Common-style device PNG from staged Module and UI Layer assignments.</summary>
    public static class CompositeKneeboardRenderer
    {
        #region BEGIN Public
        /// <summary>Renders the combined device page to a 1200 by 1600 PNG without writing a repository.</summary>
        /// <param name="diagram">Stored draw.io-generated device SVG.</param>
        /// <param name="page">Current labels, authored page title and modifier legend.</param>
        /// <param name="footer">Preview footer, or empty when exporting an authored page.</param>
        /// <returns>The complete rendered PNG.</returns>
        public static byte[] Render(UiLayerDiagram diagram, KneeboardComposite page,
                                    string footer = "DCS-IPIA • current draft preview")
        {
            ArgumentNullException.ThrowIfNull(diagram);
            ArgumentNullException.ThrowIfNull(page);
            if (diagram.GenericCapabilities is not null && diagram.CustomMapping is null)
                return GenericKneeboardRenderer.RenderPages(diagram, page, footer)[0];
            ValidateGlyphs(page, footer);
            var device = CanonicalSvgRenderer.RenderCompleteScaled(
                GraphicWithoutDynamicText(diagram.Svg), diagram.Width, diagram.Height,
                Math.Min(2, (int)Math.Floor(4096 / Math.Max(diagram.Width, diagram.Height))));
            using var deviceImage = SKImage.FromEncodedData(device) ??
                                    throw new InvalidDataException("Could not decode the authored device image.");
            using var surface = SKSurface.Create(new SKImageInfo(1200, 1600)) ??
                                throw new InvalidDataException("Could not allocate kneeboard surface.");
            var canvas = surface.Canvas;
            canvas.Clear(SKColor.Parse("#06101d"));
            using (var stripe = new SKPaint { Color = SKColor.Parse("#46d8ff") })
                canvas.DrawRect(0, 0, 1200, 16, stripe);
            var height = page.Legend.Count > 0 ? 1170 : 1350;
            var scale = Math.Min(1130f / deviceImage.Width, height / (float)deviceImage.Height);
            var width = deviceImage.Width * scale;
            var fittedHeight = deviceImage.Height * scale;
            var bounds = new SKRect(35 + (1130 - width) / 2, 155 + (height - fittedHeight) / 2,
                                    35 + (1130 + width) / 2, 155 + (height + fittedHeight) / 2);
            canvas.DrawImage(deviceImage, bounds, new SKSamplingOptions(SKFilterMode.Linear));
            PaintLegend(canvas, page.Legend);
            PaintPageText(canvas, page, footer);
            canvas.Save();
            canvas.Translate(bounds.Left, bounds.Top);
            canvas.Scale(bounds.Width / (float)diagram.Width, bounds.Height / (float)diagram.Height);
            PaintLabels(canvas, BuildDeviceSvg(diagram, page), page);
            canvas.Restore();
            using var image = surface.Snapshot();
            using var png = image.Encode(SKEncodedImageFormat.Png, 100);
            return png.ToArray();
        }
        /// <summary>Builds the final-style page SVG for inspection and deterministic tests.</summary>
        /// <param name="diagram">Stored draw.io-generated device SVG.</param>
        /// <param name="page">Current combined labels and legend.</param>
        /// <param name="footer">Preview footer, or empty to retain only the authored SVG footer.</param>
        /// <returns>One complete SVG page with device image and modifier legend.</returns>
        public static string BuildPageSvg(UiLayerDiagram diagram, KneeboardComposite page,
                                          string footer = "DCS-IPIA • current draft preview")
        {
            ArgumentNullException.ThrowIfNull(diagram);
            ArgumentNullException.ThrowIfNull(page);
            if (diagram.GenericCapabilities is not null && diagram.CustomMapping is null)
                return GenericKneeboardRenderer.BuildPages(diagram, page, footer)[0];
            var device = BuildDeviceSvg(diagram, page);
            var png = CanonicalSvgRenderer.RenderCompleteScaled(
                device, diagram.Width, diagram.Height,
                Math.Min(2, (int)Math.Floor(4096 / Math.Max(diagram.Width, diagram.Height))));
            var encoded = Convert.ToBase64String(png);
            var legend = BuildLegend(page.Legend);
            var hardwareHeight = page.Legend.Count > 0 ? 1170 : 1350;
            return $"""
             <svg xmlns="http://www.w3.org/2000/svg" width="1200" height="1600" viewBox="0 0 1200 1600">
               <desc>Current DCS-IPIA device kneeboard: {Escape(diagram.Title)}</desc>
               <rect width="1200" height="1600" fill="#06101d"/>
               <rect width="1200" height="16" fill="#46d8ff"/>
               <text x="54" y="80" font-family="Arial,sans-serif" font-size="44" font-weight="700" fill="#f5f9ff">{Escape(page.Title)}</text>
               <text x="56" y="126" font-family="Arial,sans-serif" font-size="20" font-weight="700" fill="#ffc95c">{Escape(page.Kicker)}</text>
               <image href="data:image/png;base64,{encoded}" x="35" y="155" width="1130" height="{hardwareHeight}" preserveAspectRatio="xMidYMid meet"/>
             {legend}
              {(footer.Length == 0 ? "" : $"<text x=\"54\" y=\"1570\" font-family=\"Arial,sans-serif\" font-size=\"18\" fill=\"#8ea5bd\">{Escape(footer)}</text>")}
             </svg>
             """;
        }
        /// <summary>Substitutes every staged callout variant into the authored hardware SVG.</summary>
        /// <param name="diagram">Stored draw.io-generated device SVG.</param>
        /// <param name="page">Combined device labels.</param>
        /// <returns>Hardware SVG with current colored labels.</returns>
        public static string BuildDeviceSvg(UiLayerDiagram diagram, KneeboardComposite page)
        {
            ArgumentNullException.ThrowIfNull(diagram);
            ArgumentNullException.ThrowIfNull(page);
            using var reader = XmlReader.Create(new StringReader(diagram.Svg), new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = 4_000_000
            });
            var root = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
            foreach (var label in root.Descendants().Where(x => x.Name.LocalName == "text"))
            {
                var id = label.Attribute("id")?.Value;
                if (id?.StartsWith("lbl-", StringComparison.Ordinal) == true) ApplyControlLabel(label, id, page);
                else if (id?.StartsWith("meta-", StringComparison.Ordinal) == true) ApplyCategoryLabel(label, id, page);
            }
            foreach (var control in diagram.Controls)
            {
                var label = root.Descendants().FirstOrDefault(x => x.Attribute("id")?.Value == "lbl-" + control.Control);
                if (label is not null && label.Value.Length > 0)
                {
                    if (label.Attribute("data-ipia-text-layout")?.Value == "stacked")
                        FitStackedCallout(label, control, page.Labels[control.Control]);
                    else FitCallout(label, control, page.Labels[control.Control]);
                }
            }
            FitCategories(root, diagram, page);
            return root.ToString(SaveOptions.DisableFormatting);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void ApplyControlLabel(XElement label, string id, KneeboardComposite page)
        {
            var control = id["lbl-".Length..];
            page.Labels.TryGetValue(control, out var variants);
            if (label.Attribute("data-ipia-text-layout")?.Value == "stacked")
            {
                label.RemoveNodes();
                if (variants?.Count > 0) label.Value = "Pending layout";
                return;
            }
            label.RemoveNodes();
            if (label.Attribute("data-ipia-text-layout") is null)
                label.SetAttributeValue("font-size", variants?.Count > 1 ? "13" : "16");
            label.SetAttributeValue("font-weight", "700");
            if (variants is null || variants.Count == 0)
                return;
            var x = label.Attribute("x")?.Value ?? "0";
            if (variants.Count == 1)
            {
                var variant = variants[0];
                // DCS-Common retains the SVG's native fill for an unmodified base label.
                if (variant.Color != "#ffffff" || variant.Scope != AssignmentLayer.Module ||
                    variant.ModifierChord.Length > 0)
                    label.SetAttributeValue("fill", variant.Color);
                var lines = WrapLabel(variant.Text);
                if (lines.Count == 1)
                    label.Value = lines[0];
                else
                    for (var i = 0; i < lines.Count; i++)
                        label.Add(new XElement(label.Name.Namespace + "tspan", new XAttribute("x", x),
                                               new XAttribute("dy", i == 0 ? -8 : 16),
                                               new XAttribute("font-size", 16),
                                               new XAttribute("font-weight", "700"), lines[i]));
                return;
            }
            for (var i = 0; i < variants.Count; i++)
            {
                var variant = variants[i];
                var span = new XElement(label.Name.Namespace + "tspan", new XAttribute("x", x),
                                        new XAttribute("dy", i == 0 ? -(variants.Count - 1) * 8 : 16),
                                        new XAttribute("font-size", 13), new XAttribute("font-weight", "700"),
                                        new XAttribute("data-full-label", variant.Text), variant.Text);
                if (variant.Color != "#ffffff" || variant.Scope != AssignmentLayer.Module ||
                    variant.ModifierChord.Length > 0)
                    span.SetAttributeValue("fill", variant.Color);
                label.Add(span);
            }
        }
        private static void ApplyCategoryLabel(XElement label, string id, KneeboardComposite page)
        {
            if (label.Attribute("data-ipia-text-layout") is not null)
            {
                label.RemoveNodes();
                label.Value = page.CategoryLabels.GetValueOrDefault(id["meta-".Length..]) ?? "";
                return;
            }
            label.RemoveNodes();
            label.SetAttributeValue("font-size", "16");
            label.SetAttributeValue("font-weight", "700");
            var value = page.CategoryLabels.GetValueOrDefault(id["meta-".Length..]) ?? "";
            var lines = WrapLabel(value, 16);
            if (value.Length == 0)
                return;
            var x = label.Attribute("x")?.Value ?? "0";
            if (lines.Count == 1)
                label.Value = lines[0];
            else
                for (var i = 0; i < lines.Count; i++)
                    label.Add(new XElement(label.Name.Namespace + "tspan", new XAttribute("x", x),
                                           new XAttribute("dy", i == 0 ? -8 : 16),
                                           new XAttribute("font-size", 16),
                                           new XAttribute("font-weight", "700"), lines[i]));
        }
        private static void FitCategories(XDocument root, UiLayerDiagram diagram, KneeboardComposite page)
        {
            foreach (var (name, text) in page.CategoryLabels.Where(x => x.Value.Length > 0))
            {
                var label = root.Descendants().FirstOrDefault(x => x.Attribute("id")?.Value == "meta-" + name);
                var box = label?.ElementsBeforeSelf().LastOrDefault(x => x.Name.LocalName == "rect");
                if (label is null || box is null) continue;
                var bounds = new float[4];
                var keys = new[] { "x", "y", "width", "height" };
                if (keys.Where((key, i) => !float.TryParse(box.Attribute(key)?.Value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out bounds[i]) || !float.IsFinite(bounds[i])).Any()) continue;
                var area = SKRect.Create(bounds[0], bounds[1], bounds[2], bounds[3]);
                var choices = new List<SKRect> { area };
                var obstructed = false;
                foreach (var control in diagram.Controls.Where(x => page.Labels.TryGetValue(x.Control, out var values) && values.Count > 0))
                {
                    var obstacle = SKRect.Create((float)control.X - 2, (float)control.Y - 2,
                        (float)control.Width + 4, (float)control.Height + 4);
                    var next = new List<SKRect>();
                    foreach (var choice in choices)
                    {
                        if (!choice.IntersectsWith(obstacle)) { next.Add(choice); continue; }
                        obstructed = true;
                        next.Add(new(choice.Left, choice.Top, Math.Min(choice.Right, obstacle.Left), choice.Bottom));
                        next.Add(new(Math.Max(choice.Left, obstacle.Right), choice.Top, choice.Right, choice.Bottom));
                        next.Add(new(choice.Left, choice.Top, choice.Right, Math.Min(choice.Bottom, obstacle.Top)));
                        next.Add(new(choice.Left, Math.Max(choice.Top, obstacle.Bottom), choice.Right, choice.Bottom));
                    }
                    choices = next.Where(x => x.Width > 4 && x.Height > 2).ToList();
                }
                if (!obstructed && label.Attribute("data-ipia-text-layout") is null) continue;
                if (choices.Count == 0)
                    throw new InvalidDataException("Category has no space outside control labels: " + name);
                var fitted = choices.OrderByDescending(x => x.Width * x.Height).First();
                var categoryControl = new HardwareGraphicControl(name, name, "", fitted.Left, fitted.Top, fitted.Width, fitted.Height);
                IReadOnlyList<KneeboardLabelVariant> variants = [new(text, "#ffffff", AssignmentLayer.Module, "")];
                if (label.Attribute("data-ipia-text-layout")?.Value == "stacked")
                    FitStackedCallout(label, categoryControl, variants);
                else FitCallout(label, categoryControl, variants);
            }
        }
        private static void FitCallout(XElement label, HardwareGraphicControl control, IReadOnlyList<KneeboardLabelVariant> variants)
        {
            if (!double.IsFinite(control.Width) || !double.IsFinite(control.Height) || control.Width <= 4 || control.Height <= 2)
                throw new InvalidDataException("Authored callout has no usable text area: " + control.Control);
            using var typeface = LabelTypeface();
            using var font = new SKFont(typeface, AuthoredFontSize(label, variants.Count > 1 ? 13 : 16));
            var available = (float)control.Width - 4;
            var lines = new List<(string Text, KneeboardLabelVariant Variant)>();
            float spacing;
            while (true)
            {
                lines.Clear();
                foreach (var variant in variants)
                {
                    var current = "";
                    foreach (var word in variant.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var proposed = current.Length == 0 ? word : current + " " + word;
                        if (current.Length > 0 && font.MeasureText(proposed) > available)
                        {
                            lines.Add((current, variant));
                            current = word;
                        }
                        else current = proposed;
                    }
                    lines.Add((current, variant));
                }
                spacing = font.Metrics.Descent - font.Metrics.Ascent;
                if (lines.Count * spacing <= control.Height - 2 && lines.All(x => font.MeasureText(x.Text) <= available)) break;
                if (font.Size <= 0.5f)
                    throw new InvalidDataException("Control label is too long for its authored callout: " + control.Control);
                font.Size -= 0.25f;
            }
            var x = label.Attribute("text-anchor")?.Value switch
            {
                "middle" => control.X + control.Width / 2,
                "end" => control.X + control.Width - 2,
                _ => control.X + 2
            };
            var y = control.Y + control.Height / 2 - (font.Metrics.Ascent + font.Metrics.Descent) / 2;
            var originalFill = label.Attribute("fill")?.Value ?? "#ffffff";
            label.RemoveNodes();
            label.SetAttributeValue("x", x.ToString(CultureInfo.InvariantCulture));
            label.SetAttributeValue("y", y.ToString(CultureInfo.InvariantCulture));
            for (var i = 0; i < lines.Count; i++)
            {
                var (text, variant) = lines[i];
                var fill = variant.Color == "#ffffff" && variant.Scope == AssignmentLayer.Module && variant.ModifierChord.Length == 0
                    ? originalFill : variant.Color;
                label.Add(new XElement(label.Name.Namespace + "tspan", new XAttribute("x", x.ToString(CultureInfo.InvariantCulture)),
                    new XAttribute("dy", (i == 0 ? -(lines.Count - 1) * spacing / 2 : spacing).ToString(CultureInfo.InvariantCulture)),
                    new XAttribute("font-size", font.Size.ToString(CultureInfo.InvariantCulture)), new XAttribute("fill", fill),
                    new XAttribute("data-full-label", variant.Text), text));
            }
        }
        private static void FitStackedCallout(XElement label, HardwareGraphicControl control,
                                              IReadOnlyList<KneeboardLabelVariant> variants)
        {
            if (!double.IsFinite(control.Width) || !double.IsFinite(control.Height) || control.Width <= 4 || control.Height <= 2)
                throw new InvalidDataException("Authored stacked callout has no usable text area: " + control.Control);
            using var typeface = LabelTypeface();
            using var font = new SKFont(typeface, AuthoredFontSize(label, 16));
            var columns = variants.Select(v => v.Text.EnumerateRunes().Select(r => r.ToString()).ToArray()).ToArray();
            var columnWidth = (float)(control.Width - 4) / variants.Count;
            float spacing;
            while (true)
            {
                spacing = font.Metrics.Descent - font.Metrics.Ascent;
                if (columns.All(column => column.Length * spacing <= control.Height - 4 &&
                    column.All(character => font.MeasureText(character) <= columnWidth - 2))) break;
                if (font.Size <= 4)
                    throw new InvalidDataException("Stacked label needs a larger callout: " + control.Control);
                font.Size -= 0.25f;
            }
            label.SetAttributeValue(XNamespace.Xml + "space", "preserve");
            var native = label.Attribute("fill")?.Value ?? "#111111";
            label.RemoveNodes();
            label.SetAttributeValue("text-anchor", "middle");
            for (var column = 0; column < variants.Count; column++)
            {
                var variant = variants[column];
                var color = variant.Color == "#ffffff" && variant.Scope == AssignmentLayer.Module && variant.ModifierChord.Length == 0
                    ? native : variant.Color;
                var x = control.X + 2 + columnWidth * (column + 0.5);
                for (var row = 0; row < columns[column].Length; row++)
                    label.Add(new XElement(label.Name.Namespace + "tspan",
                        new XAttribute("x", x.ToString(CultureInfo.InvariantCulture)),
                        new XAttribute("y", (control.Y + 2 - font.Metrics.Ascent + row * spacing).ToString(CultureInfo.InvariantCulture)),
                        new XAttribute("font-size", font.Size.ToString(CultureInfo.InvariantCulture)),
                        new XAttribute("fill", color), new XAttribute("data-full-label", variant.Text),
                        columns[column][row]));
            }
        }
        private static float AuthoredFontSize(XElement label, float fallback) =>
            label.Attribute("data-ipia-text-layout") is not null &&
            float.TryParse(label.Attribute("font-size")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var size) &&
            float.IsFinite(size) && size >= 8 && size <= 72 ? size : fallback;
        private static SKTypeface LabelTypeface()
        {
            var source = typeof(CompositeKneeboardRenderer)
                             .Assembly.GetManifestResourceStream("Dcs.Ipia.Rendering.Fonts.DejaVuSans-IPIA.ttf") ??
                         throw new InvalidDataException("The pinned kneeboard font is missing.");
            return SKTypeface.FromStream(source) ??
                   throw new InvalidDataException("The pinned kneeboard font is invalid.");
        }
        private static void ValidateGlyphs(KneeboardComposite page, string footer)
        {
            using var typeface = LabelTypeface();
            using var font = new SKFont(typeface, 16);
            var values = new[] { page.Title, page.Kicker, footer }
                         .Concat(page.Legend.Select(x => x.Label))
                         .Concat(page.Labels.Values.SelectMany(x => x.Select(y => y.Text)))
                         .Concat(page.CategoryLabels.Values);
            foreach (var value in values)
                if (font.GetGlyphs(value).Any(x => x == 0))
                    throw new InvalidDataException("Kneeboard text contains a glyph absent from the pinned font: " +
                                                   value);
        }
        private static string GraphicWithoutDynamicText(string svg)
        {
            var source = XDocument.Parse(svg);
            foreach (var label in source.Descendants().Where(x => x.Name.LocalName == "text" &&
                        (x.Attribute("id")?.Value.StartsWith("lbl-", StringComparison.Ordinal) == true ||
                         x.Attribute("id")?.Value.StartsWith("meta-", StringComparison.Ordinal) == true)).ToArray())
                label.Remove();
            return source.ToString(SaveOptions.DisableFormatting);
        }
        private static void PaintLegend(SKCanvas canvas, IReadOnlyList<KneeboardModifierLegend> legend)
        {
            if (legend.Count == 0)
                return;
            var bounds = new SKRect(54, 1350, 1146, 1350 + 36 + legend.Count * 28);
            using (var background = new SKPaint { Color = SKColor.Parse("#0b1726") })
                canvas.DrawRoundRect(bounds, 8, 8, background);
            using (var border = new SKPaint
            {
                Color = SKColor.Parse("#334155"),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.5f
            })
                canvas.DrawRoundRect(bounds, 8, 8, border);
            for (var i = 0; i < legend.Count; i++)
                using (var swatch = new SKPaint { Color = SKColor.Parse(legend[i].Color) })
                    canvas.DrawRoundRect(new SKRect(66, 1382 + i * 28, 80, 1396 + i * 28), 2, 2, swatch);
        }
        private static void PaintPageText(SKCanvas canvas, KneeboardComposite page, string footer)
        {
            using var typeface = LabelTypeface();
            Write(page.Title, 54, 80, 44, SKColors.White);
            Write(page.Kicker, 56, 126, 20, SKColor.Parse("#ffc95c"));
            if (page.Legend.Count > 0)
            {
                Write("SHIFT / MODIFIER", 66, 1372, 14, SKColor.Parse("#94a3b8"));
                for (var i = 0; i < page.Legend.Count; i++)
                    Write(page.Legend[i].Label, 88, 1394 + i * 28, LegendFontSize(page.Legend[i].Label), SKColor.Parse("#e2e8f0"));
            }
            if (footer.Length > 0)
                Write(footer, 54, 1570, 18, SKColor.Parse("#8ea5bd"));
            void Write(string value, float x, float y, float size, SKColor color)
            {
                using var font = new SKFont(typeface, size);
                if (font.GetGlyphs(value).Any(x => x == 0))
                    throw new InvalidDataException("Kneeboard text contains a glyph absent from the pinned font: " +
                                                   value);
                using var paint = new SKPaint { IsAntialias = true, Color = color };
                canvas.DrawText(value, x, y, SKTextAlign.Left, font, paint);
            }
        }
        private static void PaintLabels(SKCanvas canvas, string svg, KneeboardComposite page)
        {
            var source = XDocument.Parse(svg);
            using var typeface = LabelTypeface();
            foreach (var control in page.Labels.Keys)
            {
                var label = source.Descendants().FirstOrDefault(x => x.Attribute("id")?.Value == "lbl-" + control);
                if (label is not null && label.Value.Length > 0)
                    PaintText(label, "callout");
            }
            foreach (var name in page.CategoryLabels.Keys)
            {
                var label = source.Descendants().FirstOrDefault(x => x.Attribute("id")?.Value == "meta-" + name);
                if (label is not null && label.Value.Length > 0)
                    PaintText(label, "category");
            }

            void PaintText(XElement label, string kind)
            {
                var x = Position(label, "x", kind);
                var y = Position(label, "y", kind);
                var alignment = label.Attribute("text-anchor")?.Value switch
                {
                    "middle" => SKTextAlign.Center,
                    "end" => SKTextAlign.Right,
                    _ => SKTextAlign.Left
                };
                var spans = label.Elements().Where(element => element.Name.LocalName == "tspan").ToArray();
                if (spans.Length == 0)
                {
                    Draw(label.Value, x, y, Size(label, 16),
                         label.Attribute("fill")?.Value ?? "#ffffff");
                    return;
                }
                foreach (var span in spans)
                {
                    x = span.Attribute("x") is null ? x : Position(span, "x", kind);
                    y = span.Attribute("y") is not null ? Position(span, "y", kind) : y + Position(span, "dy", kind);
                    Draw(span.Value, x, y, Size(span, 16),
                         span.Attribute("fill")?.Value ?? label.Attribute("fill")?.Value ?? "#ffffff");
                }

                void Draw(string value, float textX, float textY, float size, string color)
                {
                    using var font = new SKFont(typeface, size);
                    if (font.GetGlyphs(value).Any(glyph => glyph == 0))
                        throw new InvalidDataException("Kneeboard text contains a glyph absent from the pinned font: " +
                                                       value);
                    using var paint = new SKPaint
                    {
                        IsAntialias = true,
                        Color = SKColor.TryParse(color, out var parsed)
                                                                ? parsed : SKColors.White
                    };
                    canvas.DrawText(value, textX, textY, alignment, font, paint);
                }
            }
            static float Position(XElement element, string attribute, string kind) =>
                float.TryParse(element.Attribute(attribute)?.Value, NumberStyles.Float,
                               CultureInfo.InvariantCulture, out var position)
                    ? position
                    : throw new InvalidDataException("Authored " + kind + " has no usable " + attribute + " position.");
            static float Size(XElement element, float fallback) =>
                float.TryParse(element.Attribute("font-size")?.Value, NumberStyles.Float,
                               CultureInfo.InvariantCulture, out var size) && size > 0 ? size : fallback;
        }
        private static List<string> WrapLabel(string value, int max = 18)
        {
            var words = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            var lines = new List<string>();
            foreach (var word in words)
            {
                if (lines.Count == 0 || lines[^1].Length + 1 + word.Length > max)
                    lines.Add(word);
                else
                    lines[^1] += " " + word;
            }
            if (lines.Count <= 2)
                return lines.Count == 0 ? [""] : lines;
            var tail = string.Join(" ", lines.Skip(1));
            return [lines[0], tail[..Math.Min(max - 3, tail.Length)].TrimEnd() + "..."];
        }
        private static string BuildLegend(IReadOnlyList<KneeboardModifierLegend> legend)
        {
            if (legend.Count == 0)
                return "";
            var result = new StringBuilder();
            var height = 36 + legend.Count * 28;
            result.Append(
                CultureInfo.InvariantCulture,
                $"<g data-modifier-legend=\"true\"><rect x=\"54\" y=\"1350\" width=\"1092\" height=\"{height}\" rx=\"8\" fill=\"#0b1726\" stroke=\"#334155\" stroke-width=\"1.5\"/><text x=\"66\" y=\"1372\" font-family=\"Arial,sans-serif\" font-size=\"14\" font-weight=\"700\" fill=\"#94a3b8\">SHIFT / MODIFIER</text>");
            for (var i = 0; i < legend.Count; i++)
            {
                var y = 1382 + i * 28;
                result.Append(
                    CultureInfo.InvariantCulture,
                    $"<rect x=\"66\" y=\"{y}\" width=\"14\" height=\"14\" rx=\"2\" fill=\"{Escape(legend[i].Color)}\"/>");
                result.Append(
                    CultureInfo.InvariantCulture,
                    $"<text x=\"88\" y=\"{y + 12}\" font-family=\"Arial,sans-serif\" font-size=\"{LegendFontSize(legend[i].Label)}\" fill=\"#e2e8f0\">{Escape(legend[i].Label)}</text>");
            }
            return result.Append("</g>").ToString();
        }
        private static float LegendFontSize(string value)
        {
            using var typeface = LabelTypeface();
            using var font = new SKFont(typeface, 16);
            var width = font.MeasureText(value);
            return width > 1040 ? 16 * 1040 / width : 16;
        }
        private static string Escape(string value) => SecurityElement.Escape(value) ?? "";
        #endregion END private helpers
    }
}



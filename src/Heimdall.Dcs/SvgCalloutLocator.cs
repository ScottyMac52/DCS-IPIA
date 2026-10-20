using System.Globalization;
using System.Xml.Linq;

namespace Heimdall.Dcs
{
    /// <summary>Locates authored hardware callouts in draw.io-generated SVGs.</summary>
    public static class SvgCalloutLocator
    {
        #region BEGIN Public
        /// <summary>Maps rectangle callouts and text-only box markers to selection rectangles.</summary>
        /// <param name="root">The SVG root element.</param>
        /// <returns>Control identities and their authored rectangles.</returns>
        public static IReadOnlyDictionary<string, XElement> Find(XElement root)
        {
            ArgumentNullException.ThrowIfNull(root);
            var result = new Dictionary<string, XElement>(StringComparer.Ordinal);
            foreach (var comment in root.Nodes().OfType<XComment>())
            {
                var marker = comment.Value.Trim();
                var isBox = marker.StartsWith("box:", StringComparison.Ordinal);
                if (!isBox && !marker.StartsWith("callout:", StringComparison.Ordinal))
                    continue;
                var control = marker[(isBox ? "box:" : "callout:").Length..].Trim();
                XNode? next = comment.NextNode;
                while (next is XText whitespace && string.IsNullOrWhiteSpace(whitespace.Value))
                    next = next.NextNode;
                if (!isBox)
                {
                    while (next is XElement element && element.Name.LocalName is "line" or "polyline" or "circle")
                    {
                        next = next.NextNode;
                        while (next is XText whitespace && string.IsNullOrWhiteSpace(whitespace.Value))
                            next = next.NextNode;
                    }
                }
                if (isBox && next is XElement label && label.Name.LocalName == "text" &&
                    label.Attribute("id")?.Value == "lbl-" + control)
                    next = CreateTextSelection(label);
                if (next is not XElement rectangle || rectangle.Name.LocalName != "rect")
                    throw new InvalidDataException(
                        $"Graphic {(isBox?"box":"callout")} {control} has no adjacent rectangle or matching label.");
                result.Add(control, rectangle);
            }
            return result;
        }
        #endregion END Public
        #region BEGIN private helpers
        private static XElement CreateTextSelection(XElement label)
        {
            static double Coordinate(XElement text, string attribute)
            {
                var raw = text.Attribute(attribute)?.Value;
                if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
                    !double.IsFinite(value))
                    throw new InvalidDataException(
                        $"Graphic label {text.Attribute("id")?.Value} has no valid {attribute} coordinate.");
                return value;
            }
            var x = Coordinate(label, "x");
            var y = Coordinate(label, "y");
            const double width = 84;
            const double height = 24;
            var left = label.Attribute("text-anchor")
                           ?.Value switch { "middle" => x - width / 2, "end" => x - width,
                                            _ => x };
            // This rectangle exists only in the import's XML tree; the authored SVG stays unchanged.
            var rectangle =
                new XElement(label.Name.Namespace + "rect",
                             new XAttribute("x", Math.Max(1, left).ToString(CultureInfo.InvariantCulture)),
                             new XAttribute("y", Math.Max(1, y - 18).ToString(CultureInfo.InvariantCulture)),
                             new XAttribute("width", width.ToString(CultureInfo.InvariantCulture)),
                             new XAttribute("height", height.ToString(CultureInfo.InvariantCulture)));
            label.AddBeforeSelf(rectangle);
            return rectangle;
        }
        #endregion END private helpers
    }
}

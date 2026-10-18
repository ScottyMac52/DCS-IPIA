using System.Globalization;
using System.Xml.Linq;

namespace Dcs.Ipia.Rendering
{
    /// <summary>Softens graphical SVG leaves in a disposable preview while retaining opaque text and source data.</summary>
    public static class PreviewArtwork
    {
        #region BEGIN Public
        /// <summary>Returns preview-only SVG with artwork opacity; text, clipping and definitions remain intact.</summary>
        public static string Prepare(string source, double opacity)
        {
            if (!double.IsFinite(opacity) || opacity < 0 || opacity > 1) throw new ArgumentOutOfRangeException(nameof(opacity));
            if (opacity == 1) return source;
            var document = XDocument.Parse(source);
            foreach (var element in document.Descendants().Where(x => x.Name.LocalName is "image" or "path" or "rect" or "circle" or "ellipse" or "line" or "polygon" or "polyline")
                .Where(x => !x.Ancestors().Any(a => a.Name.LocalName is "defs" or "clipPath" or "mask" or "pattern" or "text")).ToArray())
                element.ReplaceWith(new XElement(element.Name.Namespace + "g", new XAttribute("opacity", opacity.ToString(CultureInfo.InvariantCulture)), new XElement(element)));
            return document.ToString(SaveOptions.DisableFormatting);
        }
        #endregion END Public
    }
}

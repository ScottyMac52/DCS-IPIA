using System.Xml.Linq;

namespace Dcs.Ipia.Application
{
    /// <summary>Exports deterministic editable artwork with explicit native control identities.</summary>
    public static class DrawioMappingStarter
    {
        #region BEGIN Public
        /// <summary>Creates a flat, image-free draw.io starter from reviewed generic capabilities.</summary>
        /// <param name="definition">Verified native buttons, axes and POV directions.</param>
        /// <returns>Editable source accepted by the bounded mapping importer.</returns>
        public static string Export(HardwareDefinition definition)
        {
            var controls = GenericHardwareLayout.Create(definition).Diagram.Controls;
            const int rows = 48;
            var width = Math.Max(600, 600 * ((controls.Count + rows - 1) / rows));
            var height = Math.Max(150, 70 + Math.Min(rows, controls.Count) * 38);
            if (width > 4096) throw new InvalidDataException("This definition exceeds the starter canvas limit. Export a supported partial drawing instead.");
            var root = new XElement("root", new XElement("mxCell", new XAttribute("id", "0")),
                new XElement("mxCell", new XAttribute("id", "1"), new XAttribute("parent", "0")));
            root.Add(Cell("canvas", "", "rounded=0;fillColor=#ffffff;strokeColor=none;", 0, 0, width, height));
            for (var i = 0; i < controls.Count; i++)
            {
                var control = controls[i];
                var cell = Cell("label-" + control.InputKey, control.Label,
                    "rounded=1;fillColor=#ffffff;strokeColor=#64748b;fontColor=#111111;fontSize=16;",
                    20 + i / rows * 600, 20 + i % rows * 38, 560, 32);
                cell.SetAttributeValue("inputKey", control.InputKey);
                root.Add(cell);
            }
            root.Add(Cell("footer", "HEIMDALL controller starter — " + controls.Count + " inputs",
                "text;fontColor=#475569;fontSize=12;", 0, height - 24, width, 24));
            var source = new XElement("mxfile", new XElement("diagram", new XAttribute("name", "Controller"),
                new XElement("mxGraphModel", new XAttribute("pageWidth", width), new XAttribute("pageHeight", height), root)))
                .ToString(SaveOptions.DisableFormatting);
            DrawioMappingImporter.Import(source, definition, "starter.drawio", "starter");
            return source;
        }
        #endregion END Public
        #region BEGIN private helpers
        private static XElement Cell(string id, string text, string style, int x, int y, int width, int height) =>
            new("mxCell", new XAttribute("id", id), new XAttribute("value", text), new XAttribute("style", style),
                new XAttribute("vertex", "1"), new XAttribute("parent", "1"),
                new XElement("mxGeometry", new XAttribute("x", x), new XAttribute("y", y),
                    new XAttribute("width", width), new XAttribute("height", height), new XAttribute("as", "geometry")));
        #endregion END private helpers
    }
}

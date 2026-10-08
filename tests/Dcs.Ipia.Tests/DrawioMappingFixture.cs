using System.IO.Compression;
using System.Text;
using Dcs.Ipia.Application;

namespace Dcs.Ipia.Tests
{
    /// <summary>Provides editable native and compressed diagrams with explicit stable mappings.</summary>
    internal static class DrawioMappingFixture
    {
        #region BEGIN Public
        /// <summary>Gets a complete tiny raster image.</summary>
        public static byte[] Png => Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
        /// <summary>Gets a complete tiny JPEG for actual native decode checks.</summary>
        public static byte[] Jpeg => Convert.FromBase64String("/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkSEw8UHRofHh0aHBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/2wBDAQkJCQwLDBgNDRgyIRwhMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjL/wAARCAABAAEDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwD3+iiigD//2Q==");
        /// <summary>Creates a partial graphic with actual button and axis mappings.</summary>
        /// <param name="image">Optional embedded or local raster reference.</param>
        /// <returns>Original editable source.</returns>
        public static string Source(string? image = null) => $"""
            <mxfile><diagram name="Custom">
            <mxGraphModel pageWidth="900" pageHeight="600"><root>
            <mxCell id="0"/><mxCell id="1" parent="0"/>
            <mxCell id="canvas" parent="1" vertex="1" style="fillColor=#ffffff;"><mxGeometry width="900" height="600"/></mxCell>
            {(image is null ? "" : $"<mxCell id=\"hardware-image-controller\" parent=\"1\" vertex=\"1\" style=\"image={image};\"><mxGeometry x=\"350\" y=\"100\" width=\"200\" height=\"200\"/></mxCell>")}
            <UserObject id="label-trigger" label="Trigger" inputKey="JOY_BTN1"><mxCell parent="1" vertex="1" style="fillColor=#ffffff;fontColor=#111111;"><mxGeometry x="10" y="100" width="250" height="45"/></mxCell></UserObject>
            <mxCell id="anchor-trigger" parent="1" vertex="1"><mxGeometry x="400" y="150" width="10" height="10"/></mxCell>
            <mxCell id="connector-trigger" edge="1" parent="1" source="label-trigger" target="anchor-trigger"><mxGeometry relative="1"><Array as="points"><mxPoint x="290" y="150"/></Array></mxGeometry></mxCell>
            <mxCell id="label-JOY_X" parent="1" vertex="1" value="Pitch"><mxGeometry x="10" y="200" width="250" height="45"/></mxCell>
            <mxCell id="presentation-title" parent="1" vertex="1" value="Custom controls"><mxGeometry x="10" y="10" width="300" height="40"/></mxCell>
            <mxCell id="footer" parent="1" vertex="1" value="User custom mapping"><mxGeometry x="0" y="575" width="900" height="25"/></mxCell>
            </root></mxGraphModel></diagram></mxfile>
            """;
        /// <summary>Uses draw.io raw deflate and URI encoding without changing the editable graph.</summary>
        /// <param name="source">Native diagram source.</param>
        /// <returns>Compressed draw.io source.</returns>
        public static string Compress(string source)
        {
            var graph = System.Xml.Linq.XDocument.Parse(source).Descendants("mxGraphModel").Single().ToString();
            return CompressGraph(graph);
        }
        /// <summary>Encodes arbitrary graph bytes for bounded-inflation rejection tests.</summary>
        /// <param name="graph">Uncompressed graph.</param>
        /// <returns>Compressed diagram envelope.</returns>
        public static string CompressGraph(string graph)
        {
            using var output = new MemoryStream();
            using (var deflate = new DeflateStream(output, CompressionLevel.SmallestSize, true))
                deflate.Write(Encoding.UTF8.GetBytes(Uri.EscapeDataString(graph)));
            return "<mxfile><diagram>" + Convert.ToBase64String(output.ToArray()) + "</diagram></mxfile>";
        }
        /// <summary>Creates a reviewed partial user revision.</summary>
        /// <returns>Portable revision independent from original paths.</returns>
        public static UserMappingRevision Revision() => DrawioMappingImporter.Import(Source(), GenericHardwareFixture.Definition(), "custom.drawio", "1.0");
        #endregion END Public
    }
}

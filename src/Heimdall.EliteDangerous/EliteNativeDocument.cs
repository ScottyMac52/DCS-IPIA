using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace Heimdall.EliteDangerous
{
    /// <summary>Validates bounded native XML and keeps all original sources for lossless output.</summary>
    internal sealed class EliteNativeDocument
    {
        #region BEGIN Public
        /// <summary>Gets the original captured sources.</summary>
        public required IReadOnlyDictionary<string, string> Sources { get; init; }
        /// <summary>Gets the bindings file path.</summary>
        public required string Path { get; init; }
        /// <summary>Gets the editable XML document.</summary>
        public required XDocument Xml { get; init; }
        /// <summary>Gets the validated root.</summary>
        public XElement Root => Xml.Root!;
        /// <summary>Gets native command elements, including unbound slots.</summary>
        public IEnumerable<XElement> Commands => Root.Elements().Where(x => x.Elements().Any(e => IsSlot(e.Name.LocalName)));

        /// <summary>Reads one bindings file with optional 4.x selector evidence.</summary>
        public static EliteNativeDocument Read(IReadOnlyDictionary<string, string> sources)
        {
            ArgumentNullException.ThrowIfNull(sources);
            if (sources.Count == 0 || sources.Count > 8
                || sources.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != sources.Count)
            {
                throw new InvalidOperationException("Capture one bindings file and optional .start selector files.");
            }
            foreach (var (path, text) in sources)
            {
                ValidateSource(path, text);
            }
            var files = sources.Keys.Where(x => x.EndsWith(".binds", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (files.Length != 1)
            {
                throw new InvalidOperationException("Exactly one .binds preset is required per profile.");
            }
            using var reader = XmlReader.Create(new StringReader(sources[files[0]]), new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4 * 1024 * 1024
            });
            var xml = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
            ValidateRoot(xml.Root);
            EliteNativeSettings.Validate(xml.Root!);
            var native = new EliteNativeDocument { Sources = sources, Path = files[0], Xml = xml };
            ValidateCommands(native.Commands);
            return native;
        }

        /// <summary>Identifies native assignment slot names.</summary>
        public static bool IsSlot(string name) => name is "Primary" or "Secondary" or "Binding";

        /// <summary>Reads a required attribute without inventing defaults.</summary>
        public static string Attribute(XElement node, string name) =>
            (string?)node.Attribute(name) ?? throw new InvalidOperationException($"Missing {name} on {node.Name}.");

        /// <summary>Reads a native 0/1 flag.</summary>
        public static bool Flag(XElement node) => Attribute(node, "Value") switch
        {
            "0" => false, "1" => true, _ => throw new InvalidOperationException($"Invalid flag on {node.Name}.")
        };

        /// <summary>Reads a finite native deadzone fraction.</summary>
        public static double Deadzone(XElement node)
        {
            if (!double.TryParse(Attribute(node, "Value"), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                || !double.IsFinite(value) || value < 0 || value > 1)
            {
                throw new InvalidOperationException("Elite deadzone must be between zero and one.");
            }
            return value;
        }
        #endregion END Public

        #region BEGIN private helpers
        private static void ValidateSource(string path, string text)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Contains('/') || path.Contains('\\') || path.Contains(':')
                || path.IndexOfAny(['"', '<', '>', '|', '?', '*']) >= 0
                || path.Contains("..", StringComparison.Ordinal) || text is null || text.Length > 4 * 1024 * 1024)
            {
                throw new InvalidOperationException("Elite sources must be bounded files directly inside Bindings.");
            }
            if (!path.EndsWith(".binds", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".start", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Only .binds and .start evidence is supported.");
            }
        }

        private static void ValidateRoot(XElement? root)
        {
            if (root?.Name != "Root" || Attribute(root, "MajorVersion") != "4"
                || !int.TryParse(Attribute(root, "MinorVersion"), NumberStyles.None, CultureInfo.InvariantCulture, out _)
                || string.IsNullOrWhiteSpace(Attribute(root, "PresetName")))
            {
                throw new InvalidOperationException("Expected an Elite 4.x Root with a preset name and numeric minor version.");
            }
            if (root.Descendants().Any(x => x.Name.Namespace != XNamespace.None))
            {
                throw new InvalidOperationException("Namespaced Elite commands are not supported.");
            }
        }

        private static void ValidateCommands(IEnumerable<XElement> commands)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var command in commands)
            {
                if (!names.Add(command.Name.LocalName) || command.Elements("Binding").Any() && command.Elements().Any(x => x.Name == "Primary" || x.Name == "Secondary"))
                {
                    throw new InvalidOperationException("Duplicate or mixed Elite command slots.");
                }
                foreach (var group in command.Elements().GroupBy(x => x.Name))
                {
                    if (group.Count() != 1)
                    {
                        throw new InvalidOperationException("Duplicate Elite command fields.");
                    }
                }
            }
        }
        #endregion END private helpers
    }
}

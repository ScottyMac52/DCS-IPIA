using System.Globalization;
using Heimdall.Application;
using Heimdall.Domain.MultiSim;

namespace Heimdall.WarThunder
{
    /// <summary>Interprets the captured controls-v5 envelope and occurrence-addressed native ranges.</summary>
    internal sealed class WarThunderNativeProfile
    {
        private readonly ILookup<string, WarThunderBlkScalar> _fields;
        private readonly ILookup<string, WarThunderBlkBlock> _children;
        internal WarThunderBlkDocument Document { get; }
        internal WarThunderBlkBlock Controls { get; }
        internal IReadOnlyList<WarThunderDevice> Devices { get; }
        internal IReadOnlyList<WarThunderBinding> Bindings { get; }
        internal WarThunderNativeProfile(string text)
        {
            Document = WarThunderBlkDocument.Parse(text);
            _fields = Document.Scalars.ToLookup(field => field.BlockAddress, StringComparer.Ordinal);
            _children = Document.Blocks.ToLookup(block => block.ParentAddress, StringComparer.Ordinal);
            var controls = Document.Blocks.Where(block => block.Path == "controls" || block.Path.EndsWith("/controls", StringComparison.Ordinal)).ToArray();
            if (controls.Length != 1) throw new InvalidOperationException("Capture exactly one native controls envelope.");
            Controls = controls[0];
            var version = Fields(Controls.Address).Where(field => Name(field.Path) == "version").ToArray();
            if (version.Length != 1 || version[0].Type != 'i' || version[0].Value != "5")
                throw new InvalidOperationException("Only captured War Thunder controls version 5 is supported.");
            Devices = ReadDevices();
            Bindings = ReadBindings();
        }
        #region BEGIN Public
        /// <summary>Gets direct native fields without flattening repeated same-name blocks.</summary>
        internal IReadOnlyList<WarThunderBlkScalar> Fields(string block) => _fields[block].ToArray();
        /// <summary>Gets direct children with one exact name.</summary>
        internal IReadOnlyList<WarThunderBlkBlock> Children(string block, string name) =>
            _children[block].Where(child => Name(child.Path) == name).ToArray();
        /// <summary>Returns all possible saved range owners; callers must reject zero or multiple matches for writes.</summary>
        internal IReadOnlyList<WarThunderDevice> Owners(CommandType type, int input) => Devices.Where(device => device.Contains(type, input)).ToArray();
        /// <summary>Describes conversion gaps without blocking exact unchanged export.</summary>
        internal IReadOnlyList<ImportDiagnostic> Diagnostics()
        {
            var diagnostics = new List<ImportDiagnostic>();
            if (Document.HasOpaqueSyntax) diagnostics.Add(new("war-thunder.syntax.opaque", "Unsupported syntax is preserved; native editing is blocked.", false));
            if (Devices.Any(device => !device.Valid)) diagnostics.Add(new("war-thunder.device.invalid", "A saved mapping has incomplete or invalid ranges. Native input writes are blocked.", false));
            if (Children(Controls.Address, "basePresetPaths").Count > 0)
                diagnostics.Add(new("war-thunder.catalog.inherited", "The export references a game preset. The catalog covers captured commands; inherited commands and defaults are not fabricated.", false));
            foreach (var binding in Bindings)
                foreach (var field in binding.Fields.Where(field => Name(field.Path) is "joyButton" or "axisId"))
                {
                    var type = Name(field.Path) == "joyButton" ? CommandType.Button : CommandType.Axis;
                    if (field.Type != 'i' || !int.TryParse(field.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var input) || Owners(type, input).Count != 1)
                        diagnostics.Add(new("war-thunder.input.unresolved", binding.Id + ": native input has no unique saved range owner; original data retained.", false));
                }
            return diagnostics;
        }
        /// <summary>Gets a native identifier from its captured path.</summary>
        internal static string Name(string path) => path[(path.LastIndexOf('/') + 1)..];
        /// <summary>Decodes only escaped quote/backslash spellings needed for captured device labels.</summary>
        internal static string Text(string literal) => literal.Length >= 2 && literal[0] == '"' && literal[^1] == '"'
            ? literal[1..^1].Replace("\\\"", "\"", StringComparison.Ordinal).Replace("\\\\", "\\", StringComparison.Ordinal) : literal;
        #endregion END Public
        #region BEGIN private helpers
        private List<WarThunderDevice> ReadDevices()
        {
            var mappings = Children(Controls.Address, "deviceMapping");
            var result = new List<WarThunderDevice>();
            foreach (var mapping in mappings)
                foreach (var block in Children(mapping.Address, "joystick"))
                {
                    var fields = Fields(block.Address);
                    string Label(string name)
                    {
                        var values = fields.Where(field => Name(field.Path) == name && field.Type == 't').ToArray();
                        return values.Length == 1 ? Text(values[0].Value) : "";
                    }
                    int Number(string name)
                    {
                        var values = fields.Where(field => Name(field.Path) == name && field.Type == 'i').ToArray();
                        return values.Length == 1 && int.TryParse(values[0].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : -1;
                    }
                    var axes = Number("axesOffset"); var axisCount = Number("axesCount");
                    var buttons = Number("buttonsOffset"); var buttonCount = Number("buttonsCount");
                    var valid = mappings.Count == 1 && axes >= 0 && axisCount >= 0 && buttons >= 0 && buttonCount >= 0
                        && (long)axes + axisCount <= (long)int.MaxValue + 1 && (long)buttons + buttonCount <= (long)int.MaxValue + 1;
                    result.Add(new("war-thunder.joystick." + result.Count.ToString(CultureInfo.InvariantCulture), block.Address,
                        Label("name"), Label("devId"), axes, axisCount, buttons, buttonCount, valid));
                }
            return result;
        }
        private List<WarThunderBinding> ReadBindings()
        {
            var result = new List<WarThunderBinding>();
            foreach (var section in _children[Controls.Address].Where(block => Name(block.Path) is "hotkeys" or "axes"))
                foreach (var block in _children[section.Address])
                    result.Add(new(block.Address, Name(section.Path), Name(block.Path), Fields(block.Address),
                        _children[block.Address].Any()));
            return result;
        }
        #endregion END private helpers
    }
}

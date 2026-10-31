using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Heimdall.Application;
using Heimdall.Application.Interfaces;
using Heimdall.Domain.MultiSim;

namespace Heimdall.WarThunder
{
    /// <summary>Imports portable native-backed controls, validates native edits and exports exact reviewed text without deployment.</summary>
    public sealed class WarThunderProfileBackend : ISimProfileBackend, ISimCapturedDeviceLinkValidator
    {
        private const string EvidenceKey = "war-thunder.v1";
        private static readonly HashSet<string> AxisParameters = new(StringComparer.Ordinal)
            { "inverse", "kMul", "innerDeadzone", "nonlinearity", "relative" };
        private static readonly JsonSerializerOptions EvidenceOptions = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        #region BEGIN Public
        /// <summary>Gets the owning adapter identifier.</summary>
        public string SimId => "war-thunder";
        /// <summary>Native activation codes remain opaque; portable modes, chord interpretation and portable tuning are not claimed.</summary>
        public SimBackendCapabilities Capabilities => new(1, [], false, false, false);
        /// <summary>Imports one explicitly supplied controls-v5 export under its original safe filename.</summary>
        public ProfileDocument Import(string contextId, IReadOnlyDictionary<string, string> sources, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentException.ThrowIfNullOrWhiteSpace(contextId); ArgumentNullException.ThrowIfNull(sources);
            if (sources.Count != 1) throw new InvalidOperationException("Capture exactly one exported controls BLK file.");
            var source = sources.Single(); ValidateFileName(source.Key);
            var evidence = new WarThunderNativeEvidence(contextId, source.Key, source.Value, source.Value);
            var profile = Create(evidence);
            cancellationToken.ThrowIfCancellationRequested();
            return profile;
        }
        /// <summary>Produces exact native text plus conversion warnings, or no output when the portable envelope was changed outside the adapter.</summary>
        public SimProfileExportPlan Export(ProfileDocument profile, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var evidence = Evidence(profile);
                var native = new WarThunderNativeProfile(evidence.Text);
                cancellationToken.ThrowIfCancellationRequested();
                return new(new Dictionary<string, string> { [evidence.FileName] = evidence.Text }, native.Diagnostics());
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or InvalidDataException or JsonException or FormatException)
            { return new(new Dictionary<string, string>(), [new("war-thunder.export.blocked", error.Message, true)]); }
        }
        /// <summary>Gets all captured commands, retaining original catalog evidence through clear operations.</summary>
        public ICommandCatalog GetCatalog(ProfileDocument profile)
        {
            var evidence = Evidence(profile);
            return new WarThunderCommandCatalog(evidence.ContextId, new(evidence.Text), new(evidence.CatalogText));
        }
        /// <summary>Resolves only explicit Windows instance evidence; saved product IDs, offsets and ordinal names never select a physical unit.</summary>
        public SimDeviceResolution ResolveDevice(PhysicalDeviceIdentity identity, IReadOnlyList<PhysicalDeviceIdentity> connected)
        {
            ArgumentNullException.ThrowIfNull(identity); ArgumentNullException.ThrowIfNull(connected);
            if (string.IsNullOrWhiteSpace(identity.InstanceId)) return new("unresolved", []);
            var matches = connected.Where(candidate => string.Equals(candidate.InstanceId, identity.InstanceId, StringComparison.OrdinalIgnoreCase)).ToArray();
            return new(matches.Length switch { 0 => "disconnected", 1 => "resolved", _ => "ambiguous" }, matches);
        }
        /// <summary>Validates a saved range before an explicit workspace-only physical association.</summary>
        public void ValidateCapturedDeviceLink(ProfileDocument profile, string logicalDeviceId)
        {
            if (profile.BackendId != SimId || !ReadDevices(profile).Any(device => device.Id == logicalDeviceId && device.Valid))
                throw new InvalidOperationException("Choose a valid saved War Thunder device mapping.");
        }
        /// <summary>Reads saved range declarations without claiming they are currently connected controllers.</summary>
        public static IReadOnlyList<WarThunderDevice> ReadDevices(ProfileDocument profile) => new WarThunderNativeProfile(Evidence(profile).Text).Devices;
        /// <summary>Reads every native command block, including empty, keyboard/mouse, mixed and unsupported records.</summary>
        public static IReadOnlyList<WarThunderBinding> ReadBindings(ProfileDocument profile) => new WarThunderNativeProfile(Evidence(profile).Text).Bindings;
        /// <summary>Reports all saved owners of a global native index. Zero or multiple matches remain unresolved.</summary>
        public static IReadOnlyList<WarThunderDevice> ResolveNativeInput(ProfileDocument profile, CommandType type, int globalIndex) =>
            new WarThunderNativeProfile(Evidence(profile).Text).Owners(type, globalIndex);
        /// <summary>Rebinds one explicit joystick scalar occurrence, preserving every other member of a chord or mixed record.</summary>
        public static ProfileDocument RebindInput(ProfileDocument profile, string recordId, string fieldAddress, string deviceId, CommandType type, int nativeLocalIndex)
        {
            var evidence = Evidence(profile); var native = new WarThunderNativeProfile(evidence.Text);
            var binding = Binding(native, recordId);
            var field = binding.Fields.SingleOrDefault(candidate => candidate.Address == fieldAddress)
                ?? throw new InvalidOperationException("Choose a field belonging to this captured record.");
            var name = type == CommandType.Button ? "joyButton" : type == CommandType.Axis ? "axisId" : "";
            if (name.Length == 0 || WarThunderNativeProfile.Name(field.Path) != name || field.Type != 'i'
                || binding.Section != (type == CommandType.Axis ? "axes" : "hotkeys")
                || (type == CommandType.Axis && binding.Fields.Count(other => WarThunderNativeProfile.Name(other.Path) == name) != 1))
                throw new InvalidOperationException("The selected native input kind does not match this record.");
            var input = Encode(native, deviceId, type, nativeLocalIndex);
            if (binding.Fields.Any(other => other.Address != field.Address && WarThunderNativeProfile.Name(other.Path) == name && other.Value == input))
                throw new InvalidOperationException("This native input already occurs in the same record.");
            return Edited(evidence, native.Document.ReplaceOccurrences(new Dictionary<string, string> { [field.Address] = input }));
        }
        /// <summary>Adds a basic joystick binding to a captured hotkey command, or an axis input to an existing unassigned axis block.</summary>
        public static ProfileDocument AddBinding(ProfileDocument profile, string commandId, string deviceId, CommandType type, int nativeLocalIndex)
        {
            var evidence = Evidence(profile); var native = new WarThunderNativeProfile(evidence.Text);
            var command = new WarThunderCommandCatalog(evidence.ContextId, native, new(evidence.CatalogText)).Find(commandId)
                ?? throw new InvalidOperationException("Choose a command present in the captured catalog.");
            if (command.Type != type || type is not (CommandType.Axis or CommandType.Button))
                throw new InvalidOperationException("Native command and input kinds do not match.");
            var input = Encode(native, deviceId, type, nativeLocalIndex);
            var section = command.Metadata["war-thunder.section"]; var name = command.Metadata["war-thunder.command"];
            var existing = native.Bindings.Where(binding => binding.Section == section && binding.Command == name).ToArray();
            if (existing.Any(binding => binding.HasChildren)) throw new InvalidOperationException("Nested native command content is not a creation template.");
            var empty = existing.FirstOrDefault(binding => binding.Empty);
            if (type == CommandType.Axis)
            {
                if (existing.Length != 1 || existing[0].Fields.Any(field => WarThunderNativeProfile.Name(field.Path) == "axisId"))
                    throw new InvalidOperationException("Choose one captured axis block without an axisId, or explicitly rebind an assigned axis.");
                return Edited(evidence, native.Document.AppendScalar(existing[0].Id, "axisId", 'i', input));
            }
            if (empty is not null) return Edited(evidence, native.Document.AppendScalar(empty.Id, "joyButton", 'i', input));
            var parent = native.Children(native.Controls.Address, "hotkeys");
            if (parent.Count != 1) throw new InvalidOperationException("The native hotkey table is missing or ambiguous.");
            return Edited(evidence, native.Document.AppendChild(parent[0].Address, name,
                new Dictionary<string, (char Type, string Value)> { ["joyButton"] = ('i', input) }));
        }
        /// <summary>Clears one joystick-only hotkey record to an explicit empty block, preserving occurrence identities and other alternatives.</summary>
        public static ProfileDocument ClearBinding(ProfileDocument profile, string recordId)
        {
            var evidence = Evidence(profile); var native = new WarThunderNativeProfile(evidence.Text);
            var binding = Binding(native, recordId);
            if (binding.Section != "hotkeys" || !binding.Fields.Any(field => WarThunderNativeProfile.Name(field.Path) == "joyButton")
                || binding.Fields.Any(field => WarThunderNativeProfile.Name(field.Path) is not ("joyButton" or "activationType")))
                throw new InvalidOperationException("Only a captured joystick-only hotkey alternative can be cleared. System, mixed and axis records remain preserved.");
            return Edited(evidence, native.Document.EmptyBlock(binding.Id));
        }
        /// <summary>Changes a captured axis parameter literal without guessing portable curve, range or unit conversions.</summary>
        public static ProfileDocument EditAxisParameter(ProfileDocument profile, string recordId, string fieldAddress, string nativeValue)
        {
            var evidence = Evidence(profile); var native = new WarThunderNativeProfile(evidence.Text);
            var binding = Binding(native, recordId);
            var field = binding.Fields.SingleOrDefault(candidate => candidate.Address == fieldAddress);
            if (binding.Section != "axes" || field is null || !AxisParameters.Contains(WarThunderNativeProfile.Name(field.Path))
                || binding.Fields.Count(other => other.Path == field.Path) != 1)
                throw new InvalidOperationException("Choose a captured axis parameter; use native rebind for axis ownership.");
            return Edited(evidence, native.Document.ReplaceOccurrences(new Dictionary<string, string> { [field.Address] = nativeValue }));
        }
        /// <summary>Reports only native operations valid for this record's current captured shape.</summary>
        public static WarThunderBindingCapabilities GetBindingCapabilities(ProfileDocument profile, string recordId)
        {
            var native = new WarThunderNativeProfile(Evidence(profile).Text);
            var binding = native.Bindings.SingleOrDefault(candidate => candidate.Id == recordId)
                ?? throw new InvalidOperationException("Unknown native binding occurrence.");
            if (native.Document.HasOpaqueSyntax || binding.HasChildren || (binding.Section == "axes"
                && native.Bindings.Count(other => other.Section == binding.Section && other.Command == binding.Command) != 1))
                return new(false, false, false, false, []);
            var axis = binding.Section == "axes";
            var inputName = axis ? "axisId" : "joyButton";
            var inputs = binding.Fields.Where(field => WarThunderNativeProfile.Name(field.Path) == inputName && field.Type == 'i').ToArray();
            string[] parameters = axis ? binding.Fields.Where(field => AxisParameters.Contains(WarThunderNativeProfile.Name(field.Path)))
                .GroupBy(field => field.Path, StringComparer.Ordinal).Where(group => group.Count() == 1)
                .Select(group => group.Single().Address).ToArray() : [];
            return new((axis ? inputs.Length == 1 : inputs.Length > 0) && native.Devices.Count > 0 && native.Devices.All(device => device.Valid),
                !axis && inputs.Length > 0 && binding.Fields.All(field => WarThunderNativeProfile.Name(field.Path) is "joyButton" or "activationType"),
                (!axis || !binding.Fields.Any(field => WarThunderNativeProfile.Name(field.Path) == "axisId")) && native.Devices.Count > 0 && native.Devices.All(device => device.Valid)
                    && !native.Bindings.Any(other => other.Section == binding.Section && other.Command == binding.Command && other.HasChildren),
                axis && inputs.Length == 1 && binding.Fields.Count(field => WarThunderNativeProfile.Name(field.Path) == "inverse") <= 1
                    && binding.Fields.Where(field => WarThunderNativeProfile.Name(field.Path) == "inverse").All(field => field.Type == 'b'),
                parameters);
        }
        /// <summary>Sets the known native inverse flag on an assigned axis; no polarity or portable curve inference is involved.</summary>
        public static ProfileDocument SetAxisInverse(ProfileDocument profile, string recordId, bool inverted)
        {
            var evidence = Evidence(profile); var native = new WarThunderNativeProfile(evidence.Text);
            var binding = Binding(native, recordId);
            if (binding.Section != "axes" || binding.Fields.Count(field => WarThunderNativeProfile.Name(field.Path) == "axisId" && field.Type == 'i') != 1)
                throw new InvalidOperationException("Choose an assigned native axis record.");
            var flags = binding.Fields.Where(field => WarThunderNativeProfile.Name(field.Path) == "inverse").ToArray();
            if (flags.Length > 1 || (flags.Length == 1 && flags[0].Type != 'b')) throw new InvalidOperationException("Native inverse flag is ambiguous or unsupported.");
            var value = flags.Length == 1 && (flags[0].Value is "0" or "1") ? (inverted ? "1" : "0") : (inverted ? "yes" : "no");
            return Edited(evidence, flags.Length == 0 ? native.Document.AppendScalar(binding.Id, "inverse", 'b', value)
                : native.Document.ReplaceOccurrences(new Dictionary<string, string> { [flags[0].Address] = value }));
        }
        /// <summary>Builds a typed inspection model; it is documentation of captured data, not a generative native file format.</summary>
        public static JsonElement Inspect(ProfileDocument profile)
        {
            var evidence = Evidence(profile); var native = new WarThunderNativeProfile(evidence.Text);
            return JsonSerializer.SerializeToElement(new
            {
                format = "war-thunder-native-inspection-v1", context = evidence.ContextId, fileName = evidence.FileName,
                controlsVersion = 5, opaqueSyntax = native.Document.HasOpaqueSyntax,
                devices = native.Devices, bindings = native.Bindings, scalars = native.Document.Scalars,
                blocks = native.Document.Blocks, diagnostics = native.Diagnostics()
            });
        }
        #endregion END Public
        #region BEGIN private helpers
        private static WarThunderBinding Binding(WarThunderNativeProfile native, string recordId)
        {
            var binding = native.Bindings.SingleOrDefault(candidate => candidate.Id == recordId)
                ?? throw new InvalidOperationException("Unknown native binding occurrence.");
            if (binding.HasChildren || (binding.Section == "axes"
                && native.Bindings.Count(other => other.Section == binding.Section && other.Command == binding.Command) != 1))
                throw new InvalidOperationException("Nested or repeated axis command content is preserved but is not editable.");
            return binding;
        }
        private static string Encode(WarThunderNativeProfile native, string deviceId, CommandType type, int index)
        {
            if (native.Devices.Any(device => !device.Valid)) throw new InvalidOperationException("Resolve invalid saved device ranges before changing native input ownership.");
            var device = native.Devices.SingleOrDefault(candidate => candidate.Id == deviceId)
                ?? throw new InvalidOperationException("Choose an existing saved device mapping.");
            var value = device.Encode(type, index);
            var owners = native.Owners(type, value);
            if (owners.Count != 1 || owners[0].Id != deviceId) throw new InvalidOperationException("This native range overlaps another saved device mapping.");
            return value.ToString(CultureInfo.InvariantCulture);
        }
        private static ProfileDocument Edited(WarThunderNativeEvidence evidence, string text) => Create(evidence with { Text = text });
        private static ProfileDocument Create(WarThunderNativeEvidence evidence)
        {
            var native = new WarThunderNativeProfile(evidence.Text);
            _ = new WarThunderNativeProfile(evidence.CatalogText);
            var profile = WarThunderProfileProjection.Create(evidence, native);
            profile.NativeData.Add(EvidenceKey, JsonSerializer.SerializeToElement(evidence));
            return profile;
        }
        private static WarThunderNativeEvidence Evidence(ProfileDocument profile)
        {
            ArgumentNullException.ThrowIfNull(profile);
            if (profile.SchemaVersion != 1 || profile.BackendId != "war-thunder" || profile.BackendVersion != 1
                || profile.NativeData is null || profile.NativeData.Count != 1 || !profile.NativeData.TryGetValue(EvidenceKey, out var data))
                throw new InvalidOperationException("Unsupported or foreign War Thunder profile envelope.");
            var evidence = data.Deserialize<WarThunderNativeEvidence>(EvidenceOptions) ?? throw new InvalidOperationException("Native capture is missing.");
            ArgumentException.ThrowIfNullOrWhiteSpace(evidence.ContextId); ValidateFileName(evidence.FileName);
            var expected = Create(evidence);
            if (JsonSerializer.Serialize(profile) != JsonSerializer.Serialize(expected))
                throw new InvalidOperationException("Portable mutations are not native edits. Use the adapter's occurrence-addressed operations.");
            return evidence;
        }
        private static void ValidateFileName(string name)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            if (name.Length > 255 || name.Any(char.IsControl) || name.IndexOfAny(['/', '\\', ':', '*', '?', '"', '<', '>', '|']) >= 0
                || !name.EndsWith(".blk", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(name)))
                throw new InvalidOperationException("Use the exported BLK file's safe leaf filename.");
        }
        #endregion END private helpers
    }
}

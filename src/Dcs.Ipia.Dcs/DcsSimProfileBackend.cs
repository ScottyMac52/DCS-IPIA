using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Domain.MultiSim;

namespace Dcs.Ipia.Dcs
{
    /// <summary>DCS compatibility adapter over existing parsers, renderers and verified workspace snapshots; never deploys output.</summary>
    public sealed class DcsSimProfileBackend : ISimProfileBackend
    {
        private const string NativeKey = "dcs.v1";
        private static readonly JsonSerializerOptions NativeOptions = new() { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };
        #region BEGIN Public
        /// <summary>Gets the DCS backend identifier.</summary>
        public string SimId => "dcs";
        /// <summary>Gets native Press/chord/filter support; runtime button modes and deployment are excluded.</summary>
        public SimBackendCapabilities Capabilities => new(1, [BindingMode.Press], true, true, false);
        /// <summary>Imports captured Config/Input Lua sources, retaining unknown native fields verbatim.</summary>
        /// <param name="contextId">Explicit module folder ID.</param>
        /// <param name="sources">Captured relative paths and native text.</param>
        /// <param name="cancellationToken">Cancellation before returning the independent document.</param>
        /// <returns>A source-preserving portable profile.</returns>
        public ProfileDocument Import(string contextId, IReadOnlyDictionary<string, string> sources, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(contextId);
            ArgumentNullException.ThrowIfNull(sources);
            cancellationToken.ThrowIfCancellationRequested();
            if (contextId is "UiLayer" or "." or ".." || !contextId.All(x => char.IsAsciiLetterOrDigit(x) || x is '-' or '_' or '.'))
                throw new InvalidDataException("Select an explicit safe module context distinct from UiLayer.");
            var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var source in sources)
            {
                SafePath(source.Key, contextId);
                if (source.Value is null || !files.TryAdd(source.Key, source.Value))
                    throw new InvalidDataException("Duplicate native path or missing source text: " + source.Key);
            }
            var profile = new ProfileDocument
            {
                BackendId = SimId,
                ContextId = contextId,
                Name = contextId,
                ProfileId = StableIdentity.Create("dcs-portable-profile", contextId).ToString("D")
            };
            var commands = new List<SimCommand>();
            foreach (var file in files.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (file.Key.EndsWith("/modifiers.lua", StringComparison.Ordinal)) { DcsLuaParser.ParseModifiers(file.Value); continue; }
                var context = file.Key.Split('/')[2];
                var device = DcsSimProfileProjection.Device(file.Key, context);
                var bindings = DcsLuaParser.ParseProfile(file.Value, NativeId(device));
                var modifiers = Modifiers(files, context);
                if (bindings.Where(x => x.Disposition != BindingDisposition.Removed).SelectMany(x => x.Reformers).Any(x => !modifiers.Any(m => m.Name == x)))
                    throw new InvalidDataException(file.Key + ": references an undeclared modifier.");
                foreach (var binding in bindings)
                {
                    DcsSimProfileProjection.ValidateFilter(binding.Filter);
                    commands.Add(DcsSimProfileProjection.Command(context, binding.CommandId, binding.CommandName, binding.Section == BindingSection.Axis));
                    if (binding.Disposition != BindingDisposition.Removed) DcsSimProfileProjection.Add(device, binding);
                }
                profile.Devices.Add(device);
            }
            if (profile.Devices.Count == 0) throw new InvalidDataException("No native DCS profiles were supplied.");
            Pin(profile, new(contextId, profile.ProfileId, files, profile.Devices, commands));
            return profile;
        }
        /// <summary>Projects a complete existing workspace while pinning artwork, source evidence, labels and recovery state.</summary>
        /// <param name="workspace">Existing DCS editor workspace.</param>
        /// <param name="cancellationToken">Cancellation before returning the independent document.</param>
        /// <returns>A portable profile whose native envelope can restore the complete workspace.</returns>
        public ProfileDocument Capture(EditorWorkspaceSnapshot workspace, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = EditorSnapshotSerializer.Serialize(workspace);
            workspace = EditorSnapshotSerializer.Deserialize(snapshot);
            var payload = SnapshotLuaPayloadRenderer.Generate(workspace).ToDictionary(x => x.Key, x => Encoding.UTF8.GetString(x.Value));
            var profile = Import(workspace.Configuration.Module.DcsId, payload, cancellationToken);
            var commands = Read(profile).Commands;
            profile.ProfileId = workspace.Configuration.Module.Id.ToString("D");
            profile.Name = workspace.Configuration.Module.DisplayName;
            foreach (var device in profile.Devices)
            {
                var candidates = workspace.Configuration.Profiles.Where(x => Path.GetFileName(device.LogicalId).Equals(x.SourceName, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (candidates.Length == 0 && device.LogicalId.Contains("/keyboard/", StringComparison.Ordinal)) continue;
                if (candidates.Length != 1) throw new InvalidDataException("Workspace profile does not uniquely resolve: " + device.LogicalId);
                var source = candidates[0];
                device.Identity!.SimSpecificIds["dcs.profileId"] = source.Id.ToString("D");
                device.Axes.Clear(); device.Buttons.Clear();
                var layer = Layer(device);
                foreach (var assignment in workspace.Assignments.Where(x => x.Slot.ProfileId == source.Id && x.Slot.Layer == layer))
                    DcsSimProfileProjection.Add(device, new(source.Id, assignment.Slot.Section, assignment.Command.Id,
                        assignment.Command.Name, BindingDisposition.Added, assignment.Slot.InputKey,
                        assignment.Slot.ModifierChord.Split('+', StringSplitOptions.RemoveEmptyEntries), assignment.Filter));
            }
            commands.AddRange(workspace.ModuleCommands.Select(x => DcsSimProfileProjection.Command(profile.ContextId, x.Id, x.Name, x.IsAxis, x.Category)));
            commands.AddRange(workspace.UiCommands.Select(x => DcsSimProfileProjection.Command("UiLayer", x.DcsCommandId, x.Name, x.IsAxis)));
            Pin(profile, new(profile.ContextId, profile.ProfileId, payload, profile.Devices, commands, snapshot));
            return profile;
        }
        /// <summary>Restores validated portable assignment edits through the existing DCS workspace validation path.</summary>
        /// <param name="profile">Captured portable workspace document.</param>
        /// <param name="cancellationToken">Cancellation before returning restored state.</param>
        /// <returns>Independent workspace, including untouched assets and native removal history.</returns>
        public static EditorWorkspaceSnapshot Restore(ProfileDocument profile, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var state = Read(profile);
            var workspace = EditorSnapshotSerializer.Deserialize(state.Workspace ?? throw new InvalidDataException("This profile has native files only; no editor workspace was captured."));
            var assignments = new List<DraftAssignment>();
            foreach (var device in profile.Devices)
            {
                if (!device.Identity!.SimSpecificIds.TryGetValue("dcs.profileId", out var profileId))
                {
                    if (JsonSerializer.Serialize(device) != JsonSerializer.Serialize(state.Baseline.Single(x => x.LogicalId == device.LogicalId)))
                        throw new InvalidDataException(device.LogicalId + ": this native keyboard profile is source-only; edit it through a supported authoring flow.");
                    continue;
                }
                var id = Guid.Parse(profileId);
                foreach (var binding in DcsSimProfileProjection.Bindings(device, state.Commands, Modifiers(state.Sources, device.ContextId), id))
                {
                    var chord = string.Join("+", binding.Reformers);
                    var original = workspace.Assignments.SingleOrDefault(x => x.Slot.ProfileId == id && x.Slot.Layer == Layer(device) &&
                        x.Slot.Section == binding.Section && x.Slot.InputKey == binding.InputKey && x.Slot.ModifierChord == chord && x.Command.Id == binding.CommandId);
                    assignments.Add(original is null
                        ? new(new(workspace.Configuration.Module.Id, id, Layer(device), binding.Section, binding.InputKey, chord),
                            new(binding.CommandId, binding.CommandName, binding.Section == BindingSection.Axis, Layer(device)))
                        { Filter = binding.Filter }
                        : original with { Filter = binding.Filter });
                }
            }
            var commands = state.Commands.Select(x => new DraftCommand(x.Metadata["nativeId"], x.DisplayName,
                x.Type == CommandType.Axis, x.Context == "UiLayer" ? AssignmentLayer.UiLayer : AssignmentLayer.Module)).DistinctBy(x => (x.Id, x.Layer));
            var draft = new DraftAssignmentService(workspace.Configuration, commands, assignments,
                Modifiers(state.Sources, "UiLayer").Select(x => x.Name));
            var restored = workspace with { Assignments = draft.Snapshot };
            SnapshotLuaPayloadRenderer.Generate(restored);
            cancellationToken.ThrowIfCancellationRequested();
            return restored;
        }
        /// <summary>Produces all native files or one blocking diagnostic, without writing to any destination.</summary>
        /// <param name="profile">Versioned portable profile.</param>
        /// <param name="cancellationToken">Cancellation before returning output.</param>
        /// <returns>A reviewable, atomic native export plan.</returns>
        public SimProfileExportPlan Export(ProfileDocument profile, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var state = Read(profile);
                Dictionary<string, string> files;
                if (state.Workspace is not null)
                {
                    var restored = Restore(profile, cancellationToken);
                    foreach (var source in restored.Sources ?? new Dictionary<string, string>())
                    {
                        if (source.Key.EndsWith(".diff.lua", StringComparison.Ordinal))
                            RejectUnknownFields(source.Value, DcsLuaParser.ParseProfile(source.Value, Guid.NewGuid()));
                        else if (source.Key.EndsWith("/modifiers.lua", StringComparison.Ordinal))
                        {
                            DcsLuaParser.ValidateModifiersRewrite(source.Value);
                            RejectNonstandardSyntax(source.Value);
                        }
                    }
                    files = SnapshotLuaPayloadRenderer.Generate(restored).ToDictionary(x => x.Key, x => Encoding.UTF8.GetString(x.Value));
                }
                else
                {
                    files = new(state.Sources, StringComparer.Ordinal);
                    foreach (var device in profile.Devices)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var baseline = state.Baseline.Single(x => x.LogicalId == device.LogicalId);
                        if (JsonSerializer.Serialize(device) == JsonSerializer.Serialize(baseline)) continue;
                        var bindings = DcsSimProfileProjection.Bindings(device, state.Commands, Modifiers(state.Sources, device.ContextId), NativeId(device));
                        var original = DcsLuaParser.ParseProfile(state.Sources[device.LogicalId], NativeId(device));
                        RejectUnknownFields(state.Sources[device.LogicalId], original);
                        var removals = original.Where(x => x.Disposition == BindingDisposition.Removed).ToList();
                        foreach (var old in original.Where(x => x.Section == BindingSection.Axis && x.Disposition != BindingDisposition.Removed))
                            if (!bindings.Any(x => x.Section == old.Section && x.InputKey == old.InputKey && x.CommandId == old.CommandId && x.Reformers.SequenceEqual(old.Reformers)))
                                removals.Add(new(old.ProfileId, old.Section, old.CommandId, old.CommandName, BindingDisposition.Removed, old.InputKey, old.Reformers, old.Filter));
                        var effective = bindings.Select(binding =>
                        {
                            var previous = original.FirstOrDefault(x => x.Disposition != BindingDisposition.Removed && x.Section == binding.Section &&
                                x.CommandId == binding.CommandId && x.InputKey == binding.InputKey && x.Reformers.SequenceEqual(binding.Reformers));
                            return new DcsBinding(binding.ProfileId, binding.Section, binding.CommandId, binding.CommandName,
                                previous?.Disposition ?? BindingDisposition.Added, binding.InputKey, binding.Reformers, binding.Filter);
                        });
                        files[device.LogicalId] = ModuleLuaProfileRenderer.Write([.. removals, .. effective]);
                    }
                }
                cancellationToken.ThrowIfCancellationRequested();
                return new(files, []);
            }
            catch (Exception ex) when (ex is InvalidDataException or ArgumentException or JsonException or FormatException or InvalidOperationException or KeyNotFoundException or NotSupportedException or RegexMatchTimeoutException)
            {
                return new(new Dictionary<string, string>(), [new("dcs-conversion-blocked", ex.Message, true)]);
            }
        }
        /// <summary>Reads the pinned DCS command catalog with explicit module and UI contexts.</summary>
        /// <param name="profile">Versioned profile owning the native catalog evidence.</param>
        /// <returns>Independent scoped commands.</returns>
        public ICommandCatalog GetCatalog(ProfileDocument profile) => new DcsSimCommandCatalog(Read(profile).Commands);
        /// <summary>Resolves only explicit nonempty DirectInput instance identities; ambiguous evidence blocks automatic adoption.</summary>
        /// <param name="identity">Captured instance identity.</param>
        /// <param name="connected">Observed attached devices.</param>
        /// <returns>Explicit resolution status and exact matches.</returns>
        public SimDeviceResolution ResolveDevice(PhysicalDeviceIdentity identity, IReadOnlyList<PhysicalDeviceIdentity> connected)
        {
            ArgumentNullException.ThrowIfNull(identity);
            ArgumentNullException.ThrowIfNull(connected);
            if (!Guid.TryParse(identity.InstanceId, out var id) || id == Guid.Empty) return new("unresolved", []);
            var matches = connected.Where(x => Guid.TryParse(x.InstanceId, out var candidate) && candidate == id).ToArray();
            return new(matches.Length switch { 0 => "disconnected", 1 => "resolved", _ => "ambiguous" }, matches);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static AssignmentLayer Layer(LogicalDevice device) => device.ContextId == "UiLayer" ? AssignmentLayer.UiLayer : AssignmentLayer.Module;
        private static Guid NativeId(LogicalDevice device) => StableIdentity.Create("dcs-portable-device", device.LogicalId);
        private static IReadOnlyList<DcsModifier> Modifiers(Dictionary<string, string> sources, string context) =>
            sources.TryGetValue("Config/Input/" + context + "/modifiers.lua", out var text) ? DcsLuaParser.ParseModifiers(text) : [];
        private static void Pin(ProfileDocument profile, DcsNativeProfileState state) => profile.NativeData[NativeKey] = JsonSerializer.SerializeToElement(state with { Commands = state.Commands.DistinctBy(x => x.Id).ToList() });
        private static DcsNativeProfileState Read(ProfileDocument profile)
        {
            ArgumentNullException.ThrowIfNull(profile);
            if (profile.SchemaVersion != 1 || profile.BackendId != "dcs" || profile.BackendVersion != 1)
                throw new InvalidDataException("Expected portable schema 1 and dcs backend version 1; migration or a different backend is required.");
            if (profile.NativeData is null || profile.NativeData.Count != 1 || !profile.NativeData.TryGetValue(NativeKey, out var extension) || extension.GetRawText().Length > 128 * 1024 * 1024)
                throw new InvalidDataException("A bounded dcs.v1 JSON source envelope is required.");
            var state = extension.Deserialize<DcsNativeProfileState>(NativeOptions) ?? throw new InvalidDataException("DCS source evidence is empty.");
            if (state.ContextId != profile.ContextId || state.ProfileId != profile.ProfileId || state.Sources is null || state.Commands is null || state.Baseline is null || profile.Devices is null ||
                profile.Devices.Count != state.Baseline.Count || profile.Devices.Any(x => x is null) || profile.Devices.Select(x => x.LogicalId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != profile.Devices.Count)
                throw new InvalidDataException("DCS context, profile identity or device membership differs from its captured source evidence; explicitly re-import instead of rebinding.");
            foreach (var source in state.Sources)
            {
                SafePath(source.Key, profile.ContextId);
                if (source.Value is null) throw new InvalidDataException("Missing native source text: " + source.Key);
            }
            if (state.Commands.Any(x => x is null || x.Metadata is null || !x.Metadata.ContainsKey("nativeId")) || state.Baseline.Any(x => x is null))
                throw new InvalidDataException("Malformed pinned catalog or device evidence.");
            foreach (var device in profile.Devices)
            {
                SafePath(device.LogicalId, profile.ContextId);
                var original = state.Baseline.SingleOrDefault(x => x.LogicalId == device.LogicalId);
                if (original is null || device.ContextId != original.ContextId || JsonSerializer.Serialize(device.Identity) != JsonSerializer.Serialize(original.Identity))
                    throw new InvalidDataException(device.LogicalId + ": physical instance or scope changed; explicitly resolve and re-import the device.");
            }
            return state;
        }
        private static void SafePath(string path, string context)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new InvalidDataException("A native DCS path is required.");
            var parts = path.Split('/');
            if (path.Any(char.IsControl) || path.IndexOfAny(['<', '>', '"', '|', '?', '*']) >= 0 || path.Contains('\\') || path.Contains(':') || parts.Any(x => x.Length == 0 || x is "." or "..") ||
                parts.Length is < 4 or > 5 || parts[0] != "Config" || parts[1] != "Input" || parts[2] != context && parts[2] != "UiLayer" ||
                !(parts.Length == 4 && parts[3] == "modifiers.lua" || parts.Length == 5 && parts[3] is "joystick" or "keyboard" && parts[4].EndsWith(".diff.lua", StringComparison.Ordinal)))
                throw new InvalidDataException("Unsupported or unsafe native DCS path: " + path);
        }
        private static void RejectUnknownFields(string text, IReadOnlyList<DcsBinding> bindings)
        {
            DcsLuaParser.ValidateProfileRewrite(text, bindings);
            RejectNonstandardSyntax(text);
        }
        private static void RejectNonstandardSyntax(string text)
        {
#pragma warning disable SYSLIB1045 // Conservative lexical guard complements the existing DCS parser; it never evaluates Lua.
            var unquoted = Regex.Replace(text, """
                "(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'|--[^\r\n]*
                """, " ", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            var words = Regex.Matches(unquoted, @"\b[A-Za-z_]\w*\b", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
#pragma warning restore SYSLIB1045
            if (words.Any(x => x.Value is not ("local" or "diff" or "modifiers" or "return" or "true" or "false")))
                throw new InvalidDataException("Editing nonstandard Lua syntax is unsupported; unedited source can be exported losslessly.");

        }
        #endregion END private helpers
    }
}

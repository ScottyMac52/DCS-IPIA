using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Heimdall.Application;
using Heimdall.Application.Interfaces;
using Heimdall.Domain;

namespace Heimdall.Dcs
{
    /// <summary>Imports pinned DCS-Common hardware definitions and canonical UI Layer Lua.</summary>
    public sealed class DcsCommonLuaImporter : ISharedLuaImporter
    {
        #region BEGIN Public
        /// <summary>Reads supplied source text without executing Lua or changing source files.</summary>
        /// <param name="request">The catalog and pinned Lua source texts.</param>
        /// <returns>Validated normalized data and exact source provenance.</returns>
        public SharedLuaImportResult Import(SharedLuaImportRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            var diagnostics = new List<ImportDiagnostic>();
            ValidateRequest(request, diagnostics);
            if (diagnostics.Any(x => x.IsError))
                return Failure(diagnostics);
            var sources = request.Sources!;
            ValidateSources(sources, diagnostics);
            if (diagnostics.Any(x => x.IsError))
                return Failure(diagnostics);
            var controls = new List<SharedHardwareControl>();
            var profiles = new List<DeviceProfile>();
            var bindings = new List<DcsBinding>();
            var modifiers = new List<DcsModifier>();
            var catalog = request.Catalog!;
            var known =
                catalog.Devices
                    .SelectMany(x => new[] { x.StableKey }.Concat(x.Aliases).Select(key => (Key: key, Device: x)))
                    .ToDictionary(x => x.Key, x => x.Device, StringComparer.OrdinalIgnoreCase);
            var modifierSource = ReadModifiers(sources, modifiers, diagnostics);
            foreach (var source in sources.OrderBy(x => x.Path, StringComparer.Ordinal))
            {
                try
                {
                    ImportSource(source, modifierSource, catalog, known, controls, profiles, bindings);
                }
                catch (Exception ex) when (ex is FormatException or ArgumentException or RegexMatchTimeoutException)
                {
                    diagnostics.Add(new("invalid-lua-source", $"{source.Path}: {ex.Message}", true));
                }
            }
            ValidateImportedReferences(controls, profiles, bindings, modifiers, catalog, diagnostics);
            if (diagnostics.Any(x => x.IsError))
                return Failure(diagnostics);
            var provenance = CreateProvenance(sources, request.Commit!);
            return new(controls.OrderBy(x => x.Control.StableKey, StringComparer.Ordinal).ToArray(),
                       profiles.OrderBy(x => x.SourceName, StringComparer.Ordinal).ToArray(),
                       bindings.OrderBy(x => x.ProfileId)
                           .ThenBy(x => x.CommandId, StringComparer.Ordinal)
                           .ThenBy(x => x.InputKey, StringComparer.Ordinal)
                           .ToArray(),
                       modifiers, provenance, diagnostics);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void ValidateHardwareHeader(bool started, bool ended, bool version, bool hasControls, string? id, string? name, int count, PhysicalDevice device)
        {
            if (!started || !ended || !version || !hasControls || id != device.StableKey ||
                string.IsNullOrWhiteSpace(name) || count == 0)
                throw new FormatException(
                    $"Invalid hardware definition for {device.StableKey}: expected schema v1, matching ID, name, and controls.");
        }
        private static SharedHardwareControl ParseHardwareControl(string value, PhysicalDevice device)
        {
            var match = ControlRow.Match(value);
            if (!match.Success)
                throw new FormatException($"Unsupported hardware control row: {value}");
            var key = match.Groups["id"].Value;
            var type = match.Groups["type"].Value;
            var kind =
                type switch
                {
                    "axis" => ControlKind.Axis,
                    "hat" or "hat-direction" => ControlKind.Hat,
                    "rocker-position" => ControlKind.Rocker,
                    "switch-position" => ControlKind.Switch,
                    "rotary-position" => ControlKind.Encoder,
                    "button" or "button-position" or "trigger-stage" or "paddle" => ControlKind.Button,
                    _ => throw new FormatException($"Unsupported hardware control type {type}.")
                };
            var control = new DeviceControl(StableIdentity.Create("control", $"{device.StableKey}/{key}"),
                                            device.Id, key, match.Groups["label"].Value, kind, null);
            return new(control, match.Groups["key"].Value, type,
                           match.Groups["group"].Success ? match.Groups["group"].Value : null,
                           match.Groups["position"].Success ? match.Groups["position"].Value : null);
        }
        /// <summary>Accumulates request errors before any Lua is parsed.</summary>
        private static void ValidateRequest(SharedLuaImportRequest request, List<ImportDiagnostic> diagnostics)
        {
            if (request.Commit is null || request.Commit.Length != 40 || !request.Commit.All(Uri.IsHexDigit))
                diagnostics.Add(new("invalid-commit", "A pinned 40-character DCS-Common commit is required.", true));
            if (request.Catalog is null || !request.Catalog.Succeeded)
                diagnostics.Add(new("invalid-catalog",
                                    "Import the shared catalogs successfully before importing their Lua sources.",
                                    true));
            if (request.Sources is null || request.Sources.Count == 0)
                diagnostics.Add(new("missing-lua", "Supply DCS-Common hardware and UI Layer Lua source texts.", true));
        }

        /// <summary>Validates every source path and text while preserving diagnostic order.</summary>
        private static void ValidateSources(IReadOnlyList<SharedLuaSource> sources, List<ImportDiagnostic> diagnostics)
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var source in sources)
            {
                if (!IsValidSource(source))
                    diagnostics.Add(
                        new("invalid-path", "Each Lua source needs a safe repository-relative .lua path.", true));
                else if (!paths.Add(source.Path))
                    diagnostics.Add(new("duplicate-path", $"Duplicate Lua source: {source.Path}", true));
                if (source is null || string.IsNullOrWhiteSpace(source.Content))
                    diagnostics.Add(new("missing-source", "Every Lua source needs its unmodified text.", true));
            }
        }

        /// <summary>Requires a safe repository-relative Lua path before accepting its source record.</summary>
        private static bool IsValidSource([NotNullWhen(true)] SharedLuaSource? source) =>
            source is not null && !string.IsNullOrWhiteSpace(source.Path) && !source.Path.StartsWith('/') &&
            !source.Path.Contains('\\') && !source.Path.Split('/').Any(x => x is "" or "." or "..") &&
            source.Path.EndsWith(".lua", StringComparison.Ordinal);

        /// <summary>Reads canonical modifiers and retains their dedicated diagnostics.</summary>
        private static SharedLuaSource? ReadModifiers(IReadOnlyList<SharedLuaSource> sources, List<DcsModifier> modifiers,
                                                      List<ImportDiagnostic> diagnostics)
        {
            var modifierSource =
                sources.SingleOrDefault(x => x.Path == "assets/shared/ui-layer/input/UiLayer/modifiers.lua");
            if (modifierSource is null)
                diagnostics.Add(new("missing-modifiers", "Canonical UI Layer modifiers.lua is required.", true));
            else
            {
                try
                {
                    modifiers.AddRange(DcsLuaParser.ParseModifiers(modifierSource.Content));
                }
                catch (Exception ex) when (ex is FormatException or ArgumentException)
                {
                    diagnostics.Add(new("invalid-modifiers", $"{modifierSource.Path}: {ex.Message}", true));
                }
            }
            return modifierSource;
        }

        /// <summary>Dispatches one source to its hardware or UI profile parser.</summary>
        private static void ImportSource(SharedLuaSource source, SharedLuaSource? modifierSource,
                                          SharedCatalogImportResult catalog, Dictionary<string, PhysicalDevice> known,
                                          List<SharedHardwareControl> controls, List<DeviceProfile> profiles, List<DcsBinding> bindings)
        {
            if (source.Path.StartsWith("assets/shared/hardware/lua/", StringComparison.Ordinal))
            {
                var key = Path.GetFileNameWithoutExtension(source.Path);
                if (!known.TryGetValue(key, out var device) || device.StableKey != key)
                    throw new FormatException(
                        $"Hardware definition {key} is absent from the shared hardware manifest.");
                controls.AddRange(ParseHardware(source.Content, device));
            }
            else if (source.Path.StartsWith("assets/shared/ui-layer/input/UiLayer/",
                                            StringComparison.Ordinal) &&
                     source.Path.EndsWith(".diff.lua", StringComparison.Ordinal))
            {
                ImportUiProfile(source, catalog, known, profiles, bindings);
            }
            else if (source.Path != modifierSource?.Path)
                throw new FormatException(
                    "Source is outside the DCS-Common hardware and canonical UI Layer Lua directories.");
        }

        /// <summary>Resolves a canonical profile identity before parsing its bindings.</summary>
        private static void ImportUiProfile(SharedLuaSource source, SharedCatalogImportResult catalog,
                                             Dictionary<string, PhysicalDevice> known, List<DeviceProfile> profiles,
                                             List<DcsBinding> bindings)
        {
            var filename = Path.GetFileName(source.Path);
            var name = GuidSuffix.Replace(filename[..^".diff.lua".Length], string.Empty).TrimEnd();
            var deviceKey = source.Path.Contains("/keyboard/", StringComparison.Ordinal)
                                ? "keyboard"
                                : ResolveDevice(name, catalog.DeviceMap!);
            if (deviceKey is null || deviceKey != "keyboard" && !known.ContainsKey(deviceKey))
                throw new FormatException(
                    $"No valid ordered device-map rule for {name}; resolve the profile identity in the shared map.");
            var instance = ResolveInstance(name, deviceKey, catalog.DeviceMap!);
            var id = StableIdentity.Create("ui-layer-profile", $"{deviceKey}/{name}");
            var profile = new DeviceProfile(id, deviceKey, instance, "ui-layer", filename);
            profiles.Add(profile);
            bindings.AddRange(DcsLuaParser.ParseProfile(source.Content, id));
        }

        /// <summary>Uses the first matching instance rule, in shared-map order.</summary>
        private static string? ResolveInstance(string name, string deviceKey, SharedDeviceMap map)
        {
            string? instance = null;
            foreach (var rule in map.InstancePatterns.Where(x => x.DeviceKey == deviceKey))
            {
                var match =
                    Regex.Match(name, rule.Pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                                TimeSpan.FromSeconds(1));
                if (match.Success)
                {
                    instance = match.Groups[rule.Group].Value;
                    break;
                }
            }
            return instance;
        }

        /// <summary>Checks unique identities and canonical modifier and command references.</summary>
        private static void ValidateImportedReferences(List<SharedHardwareControl> controls, List<DeviceProfile> profiles,
                                                        List<DcsBinding> bindings, List<DcsModifier> modifiers,
                                                        SharedCatalogImportResult catalog, List<ImportDiagnostic> diagnostics)
        {
            if (profiles.Select(x => x.Id).Distinct().Count() != profiles.Count)
                diagnostics.Add(new("duplicate-profile",
                                    "Canonical UI Layer profile identities must be unique without machine GUIDs.",
                                    true));
            if (controls.Select(x => x.Control.Id).Distinct().Count() != controls.Count)
                diagnostics.Add(new("duplicate-control", "Hardware control identities must be unique.", true));
            var defined = modifiers.Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
            foreach (var missing in bindings.SelectMany(x => x.Reformers)
                         .Distinct(StringComparer.Ordinal)
                         .Where(x => !defined.Contains(x)))
                diagnostics.Add(
                    new("unknown-modifier", $"UI Layer binding references undefined modifier {missing}.", true));
            var commands = catalog.Commands.Select(x => x.DcsCommandId).ToHashSet(StringComparer.Ordinal);
            foreach (var missing in bindings.Select(x => x.CommandId)
                         .Distinct(StringComparer.Ordinal)
                         .Where(x => !commands.Contains(x)))
                diagnostics.Add(
                    new("unknown-command",
                        $"UI Layer binding references command {missing} absent from the canonical functions catalog.",
                        true));
        }

        /// <summary>Hashes original source bytes and orders provenance by repository path.</summary>
        private static SourceProvenance[] CreateProvenance(IReadOnlyList<SharedLuaSource> sources, string commit)
        {
            var provenance =
                sources
                    .Select(x => new SourceProvenance(
                                "ScottyMac52/DCS-Common", commit, x.Path,
                                Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(x.Content)))))
                    .OrderBy(x => x.Path, StringComparer.Ordinal)
                    .ToArray();
            return provenance;
        }
        private static SharedLuaImportResult Failure(List<ImportDiagnostic> diagnostics) => new([], [], [], [], [],
                                                                                                diagnostics);
        private static string? ResolveDevice(string name, SharedDeviceMap map)
        {
            foreach (var rule in map.Rules)
                if (rule.Patterns.Any(x => name.Contains(x, StringComparison.OrdinalIgnoreCase)))
                    return rule.DeviceKey;
            return null;
        }
        private static List<SharedHardwareControl> ParseHardware(string text, PhysicalDevice device)
        {
            var output = new List<SharedHardwareControl>();
            string? id = null;
            string? name = null;
            var version = false;
            var hasControls = false;
            var started = false;
            var ended = false;
            foreach (var line in text.Split('\n'))
            {
                var value = line.Trim();
                if (value.Length == 0)
                    continue;
                if (value == "return {" && !started)
                {
                    started = true;
                    continue;
                }
                if (value == "schemaVersion = 1," && !version)
                {
                    version = true;
                    continue;
                }
                if (value.StartsWith("id = ", StringComparison.Ordinal) && id is null)
                {
                    id = ReadField(value, "id");
                    continue;
                }
                if (value.StartsWith("name = ", StringComparison.Ordinal) && name is null)
                {
                    name = ReadField(value, "name");
                    continue;
                }
                if (Variants.IsMatch(value))
                    continue;
                if (value == "controls = {" && !hasControls)
                {
                    hasControls = true;
                    continue;
                }
                if (value == "},")
                    continue;
                if (value == "}")
                {
                    ended = true;
                    continue;
                }
                output.Add(ParseHardwareControl(value, device));
            }
            ValidateHardwareHeader(started, ended, version, hasControls, id, name, output.Count, device);
            if (output.Select(x => x.Control.StableKey).Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
                output.Count)
                throw new FormatException($"Duplicate control ID in {device.StableKey}.");
            return output;
        }
        private static string ReadField(string line, string name)
        {
            var prefix = name + " = \"";
            if (!line.StartsWith(prefix, StringComparison.Ordinal) || !line.EndsWith("\",", StringComparison.Ordinal) ||
                line.Length <= prefix.Length + 2)
                throw new FormatException($"Invalid {name} field.");
            return line[prefix.Length..^2];
        }
        private static readonly Regex GuidSuffix =
            new(@" \{[0-9A-Fa-f-]{36}\}$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        private static readonly Regex Variants =
            new(@"^variants = \{(?: ""[^""]+"",?)+ \},$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        private static readonly Regex ControlRow = new(
            @"^\{ id = ""(?<id>[^""]+)"", key = ""(?<key>[^""]+)"", type = ""(?<type>[^""]+)"", hardwareLabel = ""(?<label>[^""]+)""(?:, group = ""(?<group>[^""]+)"", position = ""(?<position>[^""]+)"")? \},?$",
            RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        #endregion END private helpers
    }
}


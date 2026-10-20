using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Heimdall.Application;
using Heimdall.Application.Interfaces;
using Heimdall.Domain;

namespace Heimdall.Dcs
{
    /// <summary>Reads pinned DCS-Common shared manifests into stable domain identities.</summary>
    public sealed class DcsCommonCatalogImporter : ISharedCatalogImporter
    {
        #region BEGIN Public
        /// <summary>Imports authored DCS-Common catalogs without modifying their sources.</summary>
        /// <param name="request">The pinned source texts and paths.</param>
        /// <returns>Normalized data, provenance, and diagnostics.</returns>
        public SharedCatalogImportResult Import(SharedCatalogImportRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            var diagnostics = new List<ImportDiagnostic>();
            if (request.Commit.Length != 40 || !request.Commit.All(Uri.IsHexDigit))
                diagnostics.Add(new("invalid-commit", "A pinned 40-character commit SHA is required.", true));
            var sources =
                new[] { (request.HardwarePath, request.HardwareJson), (request.FunctionsPath, request.FunctionsJson),
                        (request.OverlaysPath, request.OverlaysJson), (request.DeviceMapPath, request.DeviceMapJson) };
            if (sources.Any(x => string.IsNullOrWhiteSpace(x.Item1) || x.Item1.StartsWith('/') ||
                                 x.Item1.Contains('\\') || x.Item1.Split('/').Any(part => part is "" or "." or "..")))
                diagnostics.Add(new("invalid-path", "Every source needs a safe repository-relative path.", true));
            if (sources.Select(x => x.Item1).Distinct(StringComparer.OrdinalIgnoreCase).Count() != sources.Length)
                diagnostics.Add(new("duplicate-path", "Source paths must be distinct.", true));
            if (sources.Any(x => string.IsNullOrWhiteSpace(x.Item2)))
                diagnostics.Add(new("missing-source", "All four authored source texts are required.", true));
            if (diagnostics.Count > 0)
                return Failure(diagnostics);
            try
            {
                using var hardware = JsonDocument.Parse(request.HardwareJson);
                using var functions = JsonDocument.Parse(request.FunctionsJson);
                using var overlays = JsonDocument.Parse(request.OverlaysJson);
                using var deviceMap = JsonDocument.Parse(request.DeviceMapJson);
                var devices = ParseDevices(hardware.RootElement);
                var catalog = new CommandCatalog(StableIdentity.Create("catalog", "dcs-common-ui-layer"),
                                                 "dcs-common-ui-layer", request.FunctionsPath, "1");
                var commands = ParseFunctions(functions.RootElement, catalog.Id);
                var templates = ParseTemplates(overlays.RootElement, devices, commands);
                var mapping = ParseDeviceMap(deviceMap.RootElement, devices, diagnostics);
                var provenance =
                    sources
                        .Select(x => new SourceProvenance(
                                    "ScottyMac52/DCS-Common", request.Commit, x.Item1,
                                    Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(x.Item2)))))
                        .OrderBy(x => x.Path, StringComparer.Ordinal)
                        .ToArray();
                return new(devices, catalog, commands, templates, mapping, provenance, diagnostics);
            }
            catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException or
                                           KeyNotFoundException or RegexParseException)
            {
                diagnostics.Add(new("invalid-shared-source", ex.Message, true));
            }
            return Failure(diagnostics);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static SharedCatalogImportResult Failure(List<ImportDiagnostic> diagnostics) => new([], null, [], [],
                                                                                                    null, [],
                                                                                                    diagnostics);
        private static PhysicalDevice[] ParseDevices(JsonElement root)
        {
            var devices = new List<PhysicalDevice>();
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in Items(root, "devices"))
            {
                var id = Required(entry, "id");
                var label = Required(entry, "label");
                if (!keys.Add(id))
                    throw new ArgumentException($"Duplicate device identity: {id}");
                var aliases = new List<string>();
                if (entry.TryGetProperty("aliases", out var source))
                {
                    if (source.ValueKind != JsonValueKind.Array)
                        throw new ArgumentException($"Invalid aliases for {id}.");
                    foreach (var alias in source.EnumerateArray())
                    {
                        var value = alias.GetString();
                        if (string.IsNullOrWhiteSpace(value) || !keys.Add(value))
                            throw new ArgumentException($"Ambiguous device alias: {value}");
                        aliases.Add(value);
                    }
                }
                devices.Add(new(StableIdentity.Create("device", id), id, label,
                                aliases.Order(StringComparer.Ordinal).ToArray()));
            }
            if (devices.Count == 0)
                throw new ArgumentException("No shared hardware devices.");
            return devices.OrderBy(x => x.StableKey, StringComparer.Ordinal).ToArray();
        }
        private static DcsCommandDefinition[] ParseFunctions(JsonElement root, Guid catalogId)
        {
            if (root.GetProperty("schemaVersion").GetInt32() != 1)
                throw new ArgumentException("Unsupported UI Layer functions schema.");
            var commands = new List<DcsCommandDefinition>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in Items(root, "functions"))
            {
                var id = Required(entry, "id");
                if (!ids.Add(id))
                    throw new ArgumentException($"Duplicate UI Layer function: {id}");
                commands.Add(new(StableIdentity.Create("ui-function", id), catalogId, Required(entry, "command"),
                                 Required(entry, "label"), Required(entry, "category"), false));
            }
            if (commands.Count == 0)
                throw new ArgumentException("No UI Layer functions.");
            return commands.OrderBy(x => x.Id).ToArray();
        }
        private static UiLayerTemplate[] ParseTemplates(JsonElement root, IReadOnlyList<PhysicalDevice> devices,
                                                        IReadOnlyList<DcsCommandDefinition> commands)
        {
            if (root.GetProperty("schemaVersion").GetInt32() != 1)
                throw new ArgumentException("Unsupported UI Layer overlays schema.");
            var knownDevices = devices.SelectMany(d => new[] { d.StableKey }.Concat(d.Aliases))
                                   .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var knownFunctions = commands.Select(c => c.Id).ToHashSet();
            var templates = new List<UiLayerTemplate>();
            var source = root.GetProperty("devices");
            if (source.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("Invalid UI Layer device overlays.");
            foreach (var device in source.EnumerateObject())
            {
                if (!knownDevices.Contains(device.Name))
                    throw new ArgumentException($"Unknown overlay device: {device.Name}");
                var entry = device.Value;
                var bindings = new SortedDictionary<string, string>(StringComparer.Ordinal);
                var mapping = entry.GetProperty("bindings");
                if (mapping.ValueKind != JsonValueKind.Object)
                    throw new ArgumentException($"Invalid bindings for {device.Name}.");
                foreach (var binding in mapping.EnumerateObject())
                {
                    if (!knownFunctions.Contains(StableIdentity.Create("ui-function", binding.Name)))
                        throw new ArgumentException($"Unknown UI Layer function: {binding.Name}");
                    var control = binding.Value.GetString();
                    if (string.IsNullOrWhiteSpace(control))
                        throw new ArgumentException($"Missing overlay control for {binding.Name}.");
                    bindings.Add(binding.Name, control);
                }
                var instances =
                    entry.TryGetProperty("appliesToInstances", out var selectors)
                        ? selectors.EnumerateArray()
                              .Select(x => x.GetString() ?? throw new ArgumentException("Missing instance selector."))
                              .Order(StringComparer.Ordinal)
                              .ToArray()
                        : [];
                templates.Add(
                    new(device.Name, Required(entry, "status"), Required(entry, "modifier"), instances, bindings));
            }
            return templates.OrderBy(x => x.DeviceKey, StringComparer.Ordinal).ToArray();
        }
        private static SharedDeviceMap ParseDeviceMap(JsonElement root, PhysicalDevice[] devices,
                                                      List<ImportDiagnostic> diagnostics)
        {
            if (root.GetProperty("schemaVersion").GetInt32() != 1)
                throw new ArgumentException("Unsupported device map schema.");
            var known = devices.SelectMany(d => new[] { d.StableKey }.Concat(d.Aliases))
                            .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var unresolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rules = new List<DeviceMappingRule>();
            foreach (var entry in Items(root, "mappings"))
            {
                var key = Required(entry, "deviceId");
                if (!known.Contains(key))
                    unresolved.Add(key);
                var patterns =
                    Items(entry, "patterns")
                        .Select(x => x.GetString() ?? throw new ArgumentException("Missing filename pattern."))
                        .ToArray();
                if (patterns.Length == 0 || patterns.Any(string.IsNullOrWhiteSpace))
                    throw new ArgumentException($"Empty filename pattern for {key}.");
                rules.Add(new(key, patterns, Optional(entry, "canonicalName"), Optional(entry, "source")));
            }
            if (rules.Count == 0)
                throw new ArgumentException("No device mapping rules.");
            var aliases = new SortedDictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
            var source = root.GetProperty("inputKeyAliases");
            if (source.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("Invalid input key aliases.");
            foreach (var device in source.EnumerateObject())
            {
                if (!known.Contains(device.Name))
                    unresolved.Add(device.Name);
                if (device.Value.ValueKind != JsonValueKind.Object)
                    throw new ArgumentException($"Invalid input key aliases for {device.Name}.");
                var values = new SortedDictionary<string, string>(StringComparer.Ordinal);
                foreach (var alias in device.Value.EnumerateObject())
                {
                    var target = alias.Value.GetString();
                    if (string.IsNullOrWhiteSpace(alias.Name) || string.IsNullOrWhiteSpace(target))
                        throw new ArgumentException($"Invalid input key alias for {device.Name}.");
                    values.Add(alias.Name, target);
                }
                aliases.Add(device.Name, values);
            }
            var instances = new List<DeviceInstancePattern>();
            foreach (var entry in Items(root, "instancePatterns"))
            {
                var key = Required(entry, "deviceId");
                if (!known.Contains(key))
                    unresolved.Add(key);
                var pattern = Required(entry, "regex");
                var group = entry.GetProperty("group").GetInt32();
                var compiled = new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
                if (group < 1 || group > compiled.GetGroupNumbers().Max())
                    throw new ArgumentException($"Invalid instance capture group for {key}.");
                instances.Add(new(key, pattern, group));
            }
            foreach (var key in unresolved.Order(StringComparer.Ordinal))
                diagnostics.Add(new(
                    "unresolved-device-map",
                    $"Device map refers to {key}, which is absent from the shared hardware manifest and aliases. Resolve before using this rule.",
                    false));
            return new(rules, aliases, instances);
        }
        private static string? Optional(JsonElement entry, string name) => entry.TryGetProperty(name, out var value)
                                                                               ? value.GetString()
                                                                               : null;
        private static JsonElement.ArrayEnumerator Items(JsonElement root, string name)
        {
            var value = root.GetProperty(name);
            if (value.ValueKind != JsonValueKind.Array)
                throw new ArgumentException($"Expected {name} array.");
            return value.EnumerateArray();
        }
        private static string Required(JsonElement entry, string name)
        {
            var value = entry.GetProperty(name).GetString();
            return string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"Missing {name}.") : value;
        }
        #endregion END private helpers
    }
}

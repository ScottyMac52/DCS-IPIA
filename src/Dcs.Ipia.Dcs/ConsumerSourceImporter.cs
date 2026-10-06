using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;
using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Dcs
{
    /// <summary>Reads a pinned module consumer checkout into a validated, read-only module view.</summary>
    public sealed class ConsumerSourceImporter : IConsumerSourceImporter
    {
        #region BEGIN Public
        /// <summary>Parses the supplied texts without accessing local source paths or writing consumer files.</summary>
        /// <param name="request">The pinned module files and imported DCS-Common authority.</param>
        /// <returns>Normalized identities, source-preserving metadata, provenance, and diagnostics.</returns>
        public ConsumerImportResult Import(ConsumerImportRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            var diagnostics = new List<ImportDiagnostic>();
            ValidateRequest(request, diagnostics);
            if (diagnostics.Count > 0)
                return Failure(diagnostics);
            var files = ReadSources(request.Sources!, diagnostics);
            if (diagnostics.Any(x => x.IsError))
                return Failure(diagnostics);
            var metadata = ReadMetadata(files, diagnostics);
            if (diagnostics.Any(x => x.IsError))
                return Failure(diagnostics);
            try
            {
                return ImportValidatedSources(request, files, metadata, diagnostics);
            }
            catch (Exception ex)
                when (ex is FormatException or ArgumentException or InvalidOperationException or KeyNotFoundException)
            {
                diagnostics.Add(new("invalid-huey-source", ex.Message, true));
                return Failure(diagnostics);
            }
        }

        #endregion END Public
        #region BEGIN private helpers
        /// <summary>Accumulates upfront request diagnostics in their established order.</summary>
        private static void ValidateRequest(ConsumerImportRequest request, List<ImportDiagnostic> diagnostics)
        {
            if (request.Commit is null || request.Commit.Length != 40 || !request.Commit.All(Uri.IsHexDigit))
                diagnostics.Add(new("invalid-commit", "A pinned 40-character module commit is required.", true));
            if (request.Catalog is null || !request.Catalog.Succeeded || request.SharedLua is null ||
                !request.SharedLua.Succeeded)
                diagnostics.Add(new("invalid-shared-input",
                                    "Import DCS-Common catalogs and canonical UI Layer Lua before the consumer.",
                                    true));
            if (request.Sources is null || request.Sources.Count == 0)
                diagnostics.Add(new("missing-sources", "Supply the module's config and Lua source files.", true));
        }

        /// <summary>Retains original text in path order and validates all source entries before parsing.</summary>
        private static SortedDictionary<string, string> ReadSources(IReadOnlyList<ConsumerSource> sources,
                                                                    List<ImportDiagnostic> diagnostics)
        {
            var files = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var source in sources)
            {
                if (!IsValidSource(source))
                    diagnostics.Add(
                        new("invalid-path", $"Invalid or unsupported module source path: {source?.Path}", true));
                else if (!files.TryAdd(source.Path, source.Content))
                    diagnostics.Add(new("duplicate-path", $"Duplicate module source: {source.Path}", true));
                if (source is null || string.IsNullOrWhiteSpace(source.Content))
                    diagnostics.Add(new("missing-source", $"Missing text for {source?.Path}", true));
            }
            if (!files.ContainsKey(Kneeboard))
                diagnostics.Add(new("missing-source", $"Required module source is missing: {Kneeboard}", true));
            return files;
        }

        /// <summary>Checks repository-relative path safety before accepting a supported source.</summary>
        private static bool IsValidSource([NotNullWhen(true)] ConsumerSource? source)
        {
            return source is not null && !string.IsNullOrWhiteSpace(source.Path) && !source.Path.StartsWith('/') &&
                   !source.Path.Contains('\\') && !source.Path.Split('/').Any(x => x is "" or "." or "..") &&
                   IsSupportedSourcePath(source.Path);
        }

        /// <summary>Clones JSON roots and records every JSON parse failure without leaking partial results.</summary>
        private static SortedDictionary<string, JsonElement> ReadMetadata(IReadOnlyDictionary<string, string> files,
                                                                          List<ImportDiagnostic> diagnostics)
        {
            var metadata = new SortedDictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var pair in files.Where(x => x.Key.EndsWith(".json", StringComparison.Ordinal)))
            {
                try
                {
                    using var json = JsonDocument.Parse(pair.Value);
                    metadata.Add(pair.Key, json.RootElement.Clone());
                }
                catch (JsonException ex)
                {
                    diagnostics.Add(new("invalid-json", $"{pair.Key}: {ex.Message}", true));
                }
            }
            return metadata;
        }

        /// <summary>Orchestrates validated import stages while keeping result construction atomic.</summary>
        private static ConsumerImportResult ImportValidatedSources(ConsumerImportRequest request,
                                                                   SortedDictionary<string, string> files,
                                                                   SortedDictionary<string, JsonElement> metadata,
                                                                   List<ImportDiagnostic> diagnostics)
        {
            var kneeboard = metadata[Kneeboard];
            var module = ReadModule(kneeboard, metadata);
            var modifiersText = ReadModifiersText(kneeboard, module.DcsId, files);
            ValidateUiMetadata(metadata, diagnostics);
            var devices = request.Catalog!.Devices
                .SelectMany(x => new[] { x.StableKey }.Concat(x.Aliases).Select(key => (Key: key, Device: x)))
                .ToDictionary(x => x.Key, x => x.Device, StringComparer.OrdinalIgnoreCase);
            var sourceProfiles = ReadProfiles(kneeboard);
            var pageRows = ReadPages(kneeboard);
            var pagesByProfile = pageRows.EnumerateArray()
                .ToDictionary(x => RequiredText(x, "profile"), StringComparer.Ordinal);
            metadata.TryGetValue(Overrides, out var overrides);
            metadata.TryGetValue(Roles, out var roles);
            ValidateDecisionShapes(overrides, roles);
            var modifiers = DcsLuaParser.ParseModifiers(modifiersText);
            var (profiles, bindings, instances, profilePaths) = ReadDeviceProfiles(module, sourceProfiles, pageRows,
                                                                                pagesByProfile, files, devices, overrides, roles);
            ValidateProfileCollection(profiles, pageRows, overrides, roles);
            ValidateActiveModifiers(bindings, modifiers);
            AddUnreferencedProfileDiagnostic(files, module.DcsId, profilePaths, diagnostics);
            var configuration = new ModuleConfiguration(module, profiles, modifiers, bindings, $"{module.DcsId}@{request.Commit}");
            AddMultipleBindingDiagnostic(bindings, diagnostics);
            var pageList = ReadKneeboardPages(kneeboard, pageRows, sourceProfiles, module, profiles, bindings, instances, diagnostics);
            var (usageList, functionKeys) = ReadUiUsage(kneeboard, request, module, profiles, devices);
            var provenance = CreateProvenance(request, module, files);
            return new(configuration, instances.OrderBy(x => x.StableKey, StringComparer.Ordinal).ToArray(),
                       pageList, usageList.OrderBy(x => x.CommandId).ToArray(),
                       functionKeys.Order(StringComparer.Ordinal).ToArray(), metadata, files, provenance, diagnostics);
        }

        /// <summary>Resolves the module identity from its own kneeboard and deterministic scaffold selection.</summary>
        private static DcsModule ReadModule(JsonElement kneeboard, IReadOnlyDictionary<string, JsonElement> metadata)
        {
            var dcsId = RequiredText(kneeboard, "aircraft");
            var scaffold = SelectConsumerScaffold(metadata, dcsId);
            if (scaffold.ValueKind == JsonValueKind.Undefined ||
                scaffold.GetProperty("schemaVersion").GetInt32() != 1)
                throw new FormatException($"No schema v1 consumer scaffold matches {dcsId}.");
            var config = scaffold.GetProperty("import");
            var module = new DcsModule(
                StableIdentity.Create("module", dcsId), dcsId,
                config.TryGetProperty("displayName", out var name) ? name.GetString() ?? dcsId : dcsId);
            return module;
        }

        /// <summary>Prefers the canonical consumer scaffold, then uses ordinal path precedence.</summary>
        private static JsonElement SelectConsumerScaffold(IReadOnlyDictionary<string, JsonElement> metadata, string dcsId)
        {
            var scaffold =
                metadata
                    .Where(x => x.Key.StartsWith("config/", StringComparison.Ordinal) &&
                                x.Key.EndsWith(".dcs-scaffold.json", StringComparison.Ordinal) &&
                                x.Value.TryGetProperty("import", out var import) &&
                                import.TryGetProperty("target", out var target) &&
                                target.GetString() == "consumer" &&
                                import.TryGetProperty("inputModuleId", out var id) && id.GetString() == dcsId)
                    // A checkout can contain both its canonical solution and a saved repo-input variant.
                    .OrderBy(x => x.Key == $"config/{dcsId}.dcs-scaffold.json" ? 0 : 1)
                    .ThenBy(x => x.Key, StringComparer.Ordinal)
                    .Select(x => x.Value)
                    .FirstOrDefault();
            return scaffold;
        }

        /// <summary>Validates the kneeboard schema and its module-local modifiers reference.</summary>
        private static string ReadModifiersText(JsonElement kneeboard, string dcsId, IReadOnlyDictionary<string, string> files)
        {
            var modifiersPath = RequiredText(kneeboard, "modifiersFile");
            if (kneeboard.GetProperty("schemaVersion").GetInt32() != 1 ||
                modifiersPath != $"src/Config/Input/{dcsId}/modifiers.lua" ||
                !files.TryGetValue(modifiersPath, out var modifiersText))
                throw new FormatException(
                    "Kneeboard schema, aircraft, or modifiers reference does not match supplied module sources.");
            return modifiersText;
        }

        /// <summary>Validates optional UI scaffolding and preserves authored overlay warnings.</summary>
        private static void ValidateUiMetadata(IReadOnlyDictionary<string, JsonElement> metadata,
                                               List<ImportDiagnostic> diagnostics)
        {
            ValidateUiScaffold(metadata);
            ValidateOverlaySchema(metadata);
            AddOverlayPreservedDiagnostic(metadata, diagnostics);
        }

        private static void ValidateUiScaffold(IReadOnlyDictionary<string, JsonElement> metadata)
        {
            if (metadata.TryGetValue(UiScaffold, out var uiScaffold) &&
                (uiScaffold.GetProperty("schemaVersion").GetInt32() != 1 ||
                 uiScaffold.GetProperty("import").GetProperty("target").GetString() != "ui-layer"))
                throw new FormatException("UI Layer scaffold must use schema v1 and ui-layer target.");
        }

        private static void ValidateOverlaySchema(IReadOnlyDictionary<string, JsonElement> metadata)
        {
            if (metadata.TryGetValue(Overlay, out var overlay) &&
                (overlay.ValueKind != JsonValueKind.Object ||
                 !overlay.TryGetProperty("schemaVersion", out var overlayVersion) ||
                 overlayVersion.GetInt32() is not(1 or 2)))
                throw new FormatException("Unsupported module-owned UI Layer overlay schema.");
        }

        private static void AddOverlayPreservedDiagnostic(IReadOnlyDictionary<string, JsonElement> metadata,
                                                          List<ImportDiagnostic> diagnostics)
        {
            if (metadata.TryGetValue(Overlay, out var overlay) &&
                overlay.TryGetProperty("assignments", out var overlayAssignments) &&
                overlayAssignments.ValueKind == JsonValueKind.Array && overlayAssignments.GetArrayLength() > 0)
                diagnostics.Add(new(
                    "module-overlay-preserved",
                    $"{overlayAssignments.GetArrayLength()} UI Layer overlay entries are retained as authored JSON and applied to matching canonical profiles in the module editor.",
                    false));
        }

        private static JsonElement ReadProfiles(JsonElement kneeboard)
        {
            var sourceProfiles = kneeboard.GetProperty("profiles");
            if (sourceProfiles.ValueKind != JsonValueKind.Object)
                throw new FormatException("Kneeboard profiles must be a keyed object.");
            return sourceProfiles;
        }

        private static JsonElement ReadPages(JsonElement kneeboard)
        {
            var pageRows = kneeboard.GetProperty("pages");
            if (pageRows.ValueKind != JsonValueKind.Array)
                throw new FormatException("Kneeboard pages must be an array.");
            return pageRows;
        }

        private static void ValidateDecisionShapes(JsonElement overrides, JsonElement roles)
        {
            if (overrides.ValueKind is not(JsonValueKind.Undefined or JsonValueKind.Object) ||
                roles.ValueKind is not(JsonValueKind.Undefined or JsonValueKind.Object))
                throw new FormatException("Device overrides and instance roles must be keyed objects.");
        }

        /// <summary>Imports each authored profile through independently validated identity and source steps.</summary>
        private static (List<DeviceProfile> Profiles, List<DcsBinding> Bindings, List<DeviceInstance> Instances, HashSet<string> SourcePaths) ReadDeviceProfiles(DcsModule module,
                                                JsonElement sourceProfiles, JsonElement pageRows,
                                                IReadOnlyDictionary<string, JsonElement> pagesByProfile,
                                                IReadOnlyDictionary<string, string> files,
                                                IReadOnlyDictionary<string, PhysicalDevice> devices, JsonElement overrides,
                                                JsonElement roles)
        {
            var profilePaths = new HashSet<string>(StringComparer.Ordinal);
            var profiles = new List<DeviceProfile>();
            var bindings = new List<DcsBinding>();
            var instances = new List<DeviceInstance>();
            foreach (var entry in sourceProfiles.EnumerateObject())
            {
                var (path, lua) = ReadProfileSource(entry, module.DcsId, files, profilePaths);
                var (page, key, device) = ReadProfilePage(entry.Name, pagesByProfile, devices);
                var authoredInstance = OptionalText(page, "deviceInstance");
                var instance = ResolveProfileInstance(pageRows, key, entry.Name, authoredInstance);
                var filename = Path.GetFileName(path);
                ValidateDeviceDecisions(overrides, roles, filename, key, authoredInstance);
                var id = StableIdentity.Create(module.DcsId, $"{key}\0{instance}");
                profiles.Add(new(id, key, instance, instance ?? key, filename));
                instances.Add(new(StableIdentity.Create("device-instance", $"{module.DcsId}/{key}/{instance}"),
                                  module.Id, device.Id, entry.Name, instance ?? key));
                bindings.AddRange(DcsLuaParser.ParseProfile(lua, id));
            }
            return (profiles, bindings, instances, profilePaths);
        }

        private static (string Path, string Lua) ReadProfileSource(JsonProperty entry, string dcsId,
                                                                   IReadOnlyDictionary<string, string> files,
                                                                   HashSet<string> profilePaths)
        {
            var profileRoot = $"src/Config/Input/{dcsId}/joystick/";
            var path =
                entry.Value.GetString() ?? throw new FormatException($"Missing profile path for {entry.Name}.");
            if (!files.TryGetValue(path, out var lua) ||
                !path.StartsWith(profileRoot, StringComparison.Ordinal))
                throw new FormatException($"Kneeboard profile {entry.Name} has no supplied Lua source: {path}");
            if (!profilePaths.Add(path))
                throw new FormatException($"Duplicate kneeboard profile path: {path}");
            return (path, lua);
        }

        private static (JsonElement Page, string Key, PhysicalDevice Device) ReadProfilePage(string profileName,
                                                                                             IReadOnlyDictionary<string, JsonElement> pagesByProfile,
                                                                                             IReadOnlyDictionary<string, PhysicalDevice> devices)
        {
            if (!pagesByProfile.TryGetValue(profileName, out var page))
                throw new FormatException($"No kneeboard page for profile {profileName}.");
            var key = RequiredText(page, "deviceId");
            if (!devices.TryGetValue(key, out var device))
                throw new FormatException($"Unknown shared device identity: {key}");
            return (page, key, device);
        }

        private static string? ResolveProfileInstance(JsonElement pageRows, string key, string profileName,
                                                      string? authoredInstance)
        {
            // A page may omit its instance even when several profiles use the same device.
            // The kneeboard profile key is the module's authored identity for those profiles.
            var repeatedDevice = pageRows.EnumerateArray().Count(x => RequiredText(x, "deviceId") == key) > 1;
            var instance = authoredInstance ?? (repeatedDevice ? profileName : null);
            return instance;
        }

        private static void ValidateDeviceDecisions(JsonElement overrides, JsonElement roles, string filename, string key,
                                                    string? authoredInstance)
        {
            if (overrides.ValueKind == JsonValueKind.Object &&
                overrides.TryGetProperty(filename, out var forced) && forced.GetString() != key)
                throw new FormatException($"Device override for {filename} conflicts with {key}.");
            if (roles.ValueKind == JsonValueKind.Object && roles.TryGetProperty(filename, out var assigned) &&
                assigned.GetString() != authoredInstance)
                throw new FormatException($"Instance role for {filename} conflicts with {authoredInstance}.");
        }

        private static void ValidateProfileCollection(IReadOnlyList<DeviceProfile> profiles, JsonElement pageRows,
                                                      JsonElement overrides, JsonElement roles)
        {
            ValidateDecisionReferences(overrides, profiles, "Device override");
            ValidateDecisionReferences(roles, profiles, "Instance role");
            if (profiles.Count != pageRows.GetArrayLength())
                throw new FormatException("Every kneeboard profile requires exactly one page.");
            if (profiles.Select(x => x.Id).Distinct().Count() != profiles.Count)
                throw new FormatException(
                    "Two kneeboard profiles resolve to the same device instance; assign distinct deviceInstance values.");
        }

        private static void ValidateDecisionReferences(JsonElement decisions, IReadOnlyList<DeviceProfile> profiles, string kind)
        {
            foreach (var decision in decisions.ValueKind == JsonValueKind.Object
                         ? decisions.EnumerateObject().ToArray()
                         : Array.Empty<JsonProperty>())
                if (!profiles.Any(x => x.SourceName == decision.Name))
                    throw new FormatException($"{kind} references unknown profile {decision.Name}.");
        }

        private static void ValidateActiveModifiers(IReadOnlyList<DcsBinding> bindings, IReadOnlyList<DcsModifier> modifiers)
        {
            // Removed defaults may retain historical chords absent from the current modifiers file.
            // Active assignments, however, must be installable with this module's definitions.
            var definedModifiers = modifiers.Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
            var missingModifiers = bindings.Where(x => x.Disposition != BindingDisposition.Removed)
                                       .SelectMany(x => x.Reformers)
                                       .Distinct(StringComparer.Ordinal)
                                       .Where(x => !definedModifiers.Contains(x))
                                       .Order(StringComparer.Ordinal)
                                       .ToArray();
            if (missingModifiers.Length > 0)
                throw new FormatException(
                    $"Module bindings reference undefined modifiers: {string.Join(", ",missingModifiers)}.");
        }

        private static void AddUnreferencedProfileDiagnostic(IReadOnlyDictionary<string, string> files, string dcsId,
                                                             HashSet<string> profilePaths, List<ImportDiagnostic> diagnostics)
        {
            var profileRoot = $"src/Config/Input/{dcsId}/joystick/";
            var unused =
                files.Keys
                    .Where(x => x.StartsWith(profileRoot, StringComparison.Ordinal) &&
                                x.EndsWith(".diff.lua", StringComparison.Ordinal) && !profilePaths.Contains(x))
                    .ToArray();
            if (unused.Length > 0)
                diagnostics.Add(new(
                    "unreferenced-module-profiles",
                    $"{unused.Length} Lua profile(s) under {profileRoot} are not referenced by kneeboard.json and were not imported.",
                    false));
        }

        private static void AddMultipleBindingDiagnostic(IReadOnlyList<DcsBinding> bindings, List<ImportDiagnostic> diagnostics)
        {
            var multi =
                bindings.Where(x => x.Disposition != BindingDisposition.Removed)
                    .GroupBy(x => new { x.ProfileId, x.Section, x.InputKey, Chord = string.Join("+", x.Reformers) })
                    .Count(x => x.Count() > 1);
            if (multi > 0)
                diagnostics.Add(new(
                    "multiple-bindings-per-slot",
                    $"{multi} controls have multiple DCS bindings. All bindings are stored; the assignment editor can show only one command per slot.",
                    false));
        }

        /// <summary>Builds ordered summary and active device pages, then validates authored labels.</summary>
        private static List<KneeboardPage> ReadKneeboardPages(JsonElement kneeboard, JsonElement pageRows,
                                                              JsonElement sourceProfiles, DcsModule module,
                                                              IReadOnlyList<DeviceProfile> profiles,
                                                              IReadOnlyList<DcsBinding> bindings,
                                                              IReadOnlyList<DeviceInstance> instances,
                                                              List<ImportDiagnostic> diagnostics)
        {
            var pageList = new List<KneeboardPage>();
            var order = 0;
            foreach (var item in kneeboard.GetProperty("summaryPages").EnumerateArray())
                pageList.Add(
                    new(StableIdentity.Create("kneeboard-page", $"{module.DcsId}/{RequiredText(item,"file")}"),
                        module.Id, null, KneeboardPageKind.Summary, RequiredText(item, "file"),
                        RequiredText(item, "title"), [], order++));
            AddDevicePages(pageList, ref order, pageRows, sourceProfiles, module, profiles, bindings, instances, diagnostics);
            if (pageList.Select(x => x.Id).Distinct().Count() != pageList.Count)
                throw new FormatException("Duplicate kneeboard page file.");
            var labels = kneeboard.GetProperty("labels");
            if (labels.ValueKind != JsonValueKind.Object)
                throw new FormatException("Binding labels must be a keyed object.");
            ValidateLabels(pageRows, labels);
            return pageList;
        }

        private static void AddDevicePages(List<KneeboardPage> pageList, ref int order, JsonElement pageRows,
                                           JsonElement sourceProfiles, DcsModule module, IReadOnlyList<DeviceProfile> profiles,
                                           IReadOnlyList<DcsBinding> bindings, IReadOnlyList<DeviceInstance> instances,
                                           List<ImportDiagnostic> diagnostics)
        {
            foreach (var item in pageRows.EnumerateArray())
            {
                var profileName = RequiredText(item, "profile");
                var physical = instances.Single(x => x.StableKey == profileName);
                var sourceName = Path.GetFileName(sourceProfiles.GetProperty(profileName).GetString()!);
                var profile = profiles.Single(x => x.SourceName == sourceName);
                // DCS-Common renders a device page only when its profile has effective added inputs.
                if (!bindings.Any(x => x.ProfileId == profile.Id && x.Disposition == BindingDisposition.Added))
                {
                    diagnostics.Add(new(
                        "inactive-kneeboard-page",
                        $"{RequiredText(item,"file")} is not rendered: {profileName} has no effective key or axis additions.",
                        false));
                    continue;
                }
                pageList.Add(
                    new(StableIdentity.Create("kneeboard-page", $"{module.DcsId}/{RequiredText(item,"file")}"),
                        module.Id, physical.Id, KneeboardPageKind.Device, RequiredText(item, "file"),
                        RequiredText(item, "title"), [], order++));
            }
        }

        /// <summary>Validates explicit UI usage and deduplicates functions without dropping authored metadata.</summary>
        private static (List<UiLayerModuleUsage> Usage, HashSet<string> FunctionKeys) ReadUiUsage(JsonElement kneeboard,
                                                ConsumerImportRequest request, DcsModule module,
                                                IReadOnlyList<DeviceProfile> profiles,
                                                IReadOnlyDictionary<string, PhysicalDevice> devices)
        {
            kneeboard.TryGetProperty("uiLayerUtilization", out var usage);
            if (usage.ValueKind != JsonValueKind.Undefined && RequiredText(usage, "mode") != "explicit")
                throw new FormatException("Only explicit UI Layer utilization is supported.");
            var validIds = request.Catalog.Commands.Select(x => x.Id).ToHashSet();
            var uiModifiers = request.SharedLua!.Modifiers.Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
            var used = new HashSet<Guid>();
            var usageList = new List<UiLayerModuleUsage>();
            var functionKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in usage.ValueKind == JsonValueKind.Undefined
                         ? Array.Empty<JsonElement>()
                         : usage.GetProperty("bindings").EnumerateArray().ToArray())
            {
                var (function, id) = ValidateUsageBinding(item, validIds, uiModifiers, devices, profiles);
                if (used.Add(id))
                {
                    usageList.Add(new(module.Id, id, true));
                    functionKeys.Add(function);
                }
            }
            return (usageList, functionKeys);
        }

        private static (string Function, Guid Id) ValidateUsageBinding(JsonElement item, HashSet<Guid> validIds,
                                                                       HashSet<string> uiModifiers,
                                                                       IReadOnlyDictionary<string, PhysicalDevice> devices,
                                                                       IReadOnlyList<DeviceProfile> profiles)
        {
            var function = RequiredText(item, "functionId");
            var id = StableIdentity.Create("ui-function", function);
            if (!validIds.Contains(id))
                throw new FormatException($"Unknown canonical UI Layer function {function}.");
            var deviceKey = RequiredText(item, "deviceId");
            if (!devices.ContainsKey(deviceKey) ||
                !profiles.Any(x => x.DeviceId == deviceKey &&
                                   x.Instance == OptionalText(item, "deviceInstance")))
                throw new FormatException(
                    $"UI Layer utilization has no matching module device instance: {deviceKey}.");
            foreach (var modifier in item.GetProperty("modifiers").EnumerateArray())
                if (!uiModifiers.Contains(modifier.GetString() ?? string.Empty))
                    throw new FormatException($"Unknown canonical UI Layer modifier {modifier.GetString()}.");
            return (function, id);
        }

        /// <summary>Hashes the exact retained source bytes using the pinned repository and commit.</summary>
        private static SourceProvenance[] CreateProvenance(ConsumerImportRequest request, DcsModule module,
                                                           IReadOnlyDictionary<string, string> files)
        {
            var provenance =
                files
                    .Select(x => new SourceProvenance(
                                request.Repository ?? module.DcsId, request.Commit!, x.Key,
                                Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(x.Value)))))
                    .ToArray();
            return provenance;
        }

        /// <summary>Recognizes committed module inputs that can be retained as pinned text.</summary>
        internal static bool IsSupportedSourcePath(string path) =>
            IsConfigurationPath(path) || IsModuleInputPath(path) || IsDocumentationPath(path) || IsReleasePath(path);

        private static bool IsConfigurationPath(string path) =>
            path.StartsWith("config/", StringComparison.Ordinal) && path.EndsWith(".json", StringComparison.Ordinal);

        private static bool IsModuleInputPath(string path) =>
            path.StartsWith("src/Config/Input/", StringComparison.Ordinal) &&
                (path.EndsWith("/modifiers.lua", StringComparison.Ordinal) ||
                 path.Contains("/joystick/", StringComparison.Ordinal) &&
                     path.EndsWith(".diff.lua", StringComparison.Ordinal));

        private static bool IsDocumentationPath(string path) =>
            path == "README.md" ||
            path.StartsWith("docs/", StringComparison.Ordinal) && path.EndsWith(".md", StringComparison.Ordinal) ||
            path == "packaging/ovgme/README.TXT";

        private static bool IsReleasePath(string path) =>
            path is "scripts/Build-OvGME.ps1" or "scripts/Build-Release.ps1" or "scripts/Test-Package.ps1" or
                    "scripts/version.mjs" or ".github/workflows/release.yml";

        private static string RequiredText(JsonElement item, string property)
        {
            var value = item.GetProperty(property).GetString();
            return string.IsNullOrWhiteSpace(value) ? throw new FormatException($"Missing {property}.") : value;
        }
        private static string? OptionalText(JsonElement item, string property) =>
            item.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null ? value.GetString()
                                                                                                  : null;
        private static void ValidateLabels(JsonElement value, JsonElement labels)
        {
            if (value.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in value.EnumerateArray())
                    ValidateLabels(child, labels);
                return;
            }
            if (value.ValueKind != JsonValueKind.Object)
                return;
            if (value.TryGetProperty("labelId", out var id) && !labels.TryGetProperty(id.GetString() ?? "", out _))
                throw new FormatException($"Kneeboard callout references missing label {id.GetString()}.");
            foreach (var child in value.EnumerateObject())
                ValidateLabels(child.Value, labels);
        }
        private static ConsumerImportResult Failure(List<ImportDiagnostic> diagnostics) =>
            new(null, [], [], [], [], new Dictionary<string, JsonElement>(), new Dictionary<string, string>(), [],
                diagnostics);
        private const string UiScaffold = "config/scaffold.dcs-scaffold.json";
        private const string Kneeboard = "config/kneeboard.json";
        private const string Overlay = "config/ui-layer-overlay.json";
        private const string Overrides = "config/scaffold-device-overrides.json";
        private const string Roles = "config/scaffold-instance-roles.json";
        #endregion END private helpers
    }
}

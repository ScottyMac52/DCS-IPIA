using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Heimdall.Application;
using Heimdall.Application.Interfaces;
using Heimdall.Domain;

namespace Heimdall.Dcs
{
    /// <summary>Reads committed source files from selected local Git checkouts for a module preview.</summary>
    public sealed class ModuleWorkspaceLoader : IModuleWorkspaceLoader
    {
        #region BEGIN Public
        /// <summary>Imports the committed repository heads without changing either checkout.</summary>
        /// <param name="commonRoot">Shared bundle or legacy DCS-Common checkout root.</param>
        /// <param name="hueyRoot">module checkout root.</param>
        /// <param name="cancellationToken">Cancellation token for Git reads.</param>
        /// <returns>A validated module import including original committed source text.</returns>
        public async Task<ModuleWorkspacePreview> LoadAsync(string commonRoot, string hueyRoot,
                                                            CancellationToken cancellationToken = default)
        {
            var common = await RepositoryRootAsync(commonRoot, cancellationToken);
            var huey = await RepositoryRootAsync(hueyRoot, cancellationToken);
            var commonCommit = await SourceCommitAsync(common, cancellationToken);
            var hueyCommit = (await GitAsync(huey, cancellationToken, "rev-parse", "HEAD")).Trim();
            var catalogSources = await ReadSourcesAsync(common, SharedCatalogPaths, cancellationToken);
            var catalog = new DcsCommonCatalogImporter().Import(
                new(commonCommit, SharedCatalogPaths[0], catalogSources[SharedCatalogPaths[0]], SharedCatalogPaths[1],
                    catalogSources[SharedCatalogPaths[1]], SharedCatalogPaths[2], catalogSources[SharedCatalogPaths[2]],
                    SharedCatalogPaths[3], catalogSources[SharedCatalogPaths[3]]));
            if (!catalog.Succeeded)
                throw new InvalidDataException(
                    $"DCS-Common catalog: {string.Join("; ",catalog.Diagnostics.Where(x=>x.IsError).Select(x=>x.Message))}");
            var sharedPaths = (await TrackedPathsAsync(common, cancellationToken, "assets/shared/hardware/lua",
                                                       "assets/shared/ui-layer/input/UiLayer"))
                                  .Where(x => x.EndsWith(".lua", StringComparison.Ordinal))
                                  .ToArray();
            var sharedSources = await ReadSourcesAsync(common, sharedPaths, cancellationToken);
            var shared = new DcsCommonLuaImporter().Import(
                new(commonCommit, catalog, sharedSources.Select(x => new SharedLuaSource(x.Key, x.Value)).ToArray()));
            if (!shared.Succeeded)
                throw new InvalidDataException(
                    $"DCS-Common Lua: {string.Join("; ",shared.Diagnostics.Where(x=>x.IsError).Select(x=>x.Message))}");
            var hueyPaths =
                (await TrackedPathsAsync(huey, cancellationToken, "config", "src/Config/Input", "README.md", "docs",
                                         "packaging/ovgme", "scripts", ".github/workflows/release.yml"))
                    .Where(ConsumerSourceImporter.IsSupportedSourcePath)
                    .ToArray();
            var hueySources = await ReadSourcesAsync(huey, hueyPaths, cancellationToken);
            var imported = new ConsumerSourceImporter().Import(
                new(hueyCommit, catalog, shared, hueySources.Select(x => new ConsumerSource(x.Key, x.Value)).ToArray(),
                    Path.GetFileName(huey)));
            var uncommitted = await UncommittedSourcesAsync(huey, cancellationToken);
            if (uncommitted.Length > 0)
                imported = new ConsumerImportResult(imported.Configuration, imported.Instances, imported.Pages,
                    imported.Usage, imported.UiFunctionKeys, imported.Metadata, imported.Sources, imported.Provenance,
                    [..imported.Diagnostics, new("uncommitted-module-sources",
                    $"Local changes were not imported: {string.Join(", ", uncommitted)}. Commit the module changes, then use Import Module again.", false)]);
            var (moduleCommands, commandSource, commandDiagnostics) =
                LoadLinkedCommands(huey, hueySources, imported.Configuration?.Module.DcsId);
            if (!imported.Succeeded)
                return new(imported, [], []) { ModuleCommit = hueyCommit, UncommittedSourcePaths = uncommitted, UiCommands = catalog.Commands, ModuleCommands = moduleCommands,
                                               ModuleCommandSource = commandSource,
                                               CommandDiagnostics = commandDiagnostics,
                                               SharedLuaSources = UiLayerSources(sharedSources) };

            var kneeboardRoot = $"kneeboard/{imported.Configuration!.Module.DcsId}";
            var available =
                (await TrackedPathsAsync(huey, cancellationToken, kneeboardRoot)).ToHashSet(StringComparer.Ordinal);
            var images = new List<KneeboardImageAsset>();
            foreach (var page in imported.Pages)
            {
                var path = $"{kneeboardRoot}/{page.StableKey}.png";
                if (!available.Contains(path))
                    continue;
                var bytes = await GitBytesAsync(huey, cancellationToken, "show", $"HEAD:{path}");
                images.Add(new(page.StableKey, path, bytes, Convert.ToHexStringLower(SHA256.HashData(bytes))));
            }
            IReadOnlyList<UiLayerDiagram> diagrams = [];
            var graphicDiagnostics = new List<ImportDiagnostic>();
            try
            {
                var graphicPaths = imported.Configuration!.Profiles
                                       .Select(x => catalog.Devices.Single(
                                                   y => y.StableKey == x.DeviceId ||
                                                        y.Aliases.Contains(x.DeviceId, StringComparer.Ordinal)))
                                       .Select(x => $"assets/shared/hardware/svg/{x.StableKey}.svg")
                                       .Distinct(StringComparer.Ordinal)
                                       .ToArray();
                var graphics = new SortedDictionary<string, string>(StringComparer.Ordinal);
                foreach (var path in graphicPaths)
                {
                    try
                    {
                        graphics.Add(path, await SourceTextAsync(common, path, cancellationToken));
                    }
                    catch (InvalidDataException)
                    { /* The profile diagnostic below identifies the missing graphic. */
                    }
                }
                diagrams = UiLayerDiagramFactory.Create(
                    imported, catalog, graphics, shared,
                    (profile, error) => graphicDiagnostics.Add(
                        new("device-graphic-unavailable",
                            $"{profile.DeviceId} {profile.Instance??profile.SourceName}: {error.Message}", true)));
            }
            catch (Exception ex) when (ex is InvalidDataException or FormatException or System.Xml.XmlException or
                                           ArgumentException or InvalidOperationException or KeyNotFoundException)
            {
                graphicDiagnostics.Add(
                    new("device-graphic-unavailable", $"DCS-Common hardware graphic: {ex.Message}", true));
            }
            return new(imported, images, diagrams) {
                ModuleCommit = hueyCommit, UncommittedSourcePaths = uncommitted,
                GraphicDiagnostics = graphicDiagnostics, UiCommands = catalog.Commands,
                ModuleCommands = moduleCommands,         ModuleCommandSource = commandSource,
                CommandDiagnostics = commandDiagnostics,
                SharedLuaSources = await HardwareSourcesAsync(common, catalog, shared, sharedSources, cancellationToken)
            };
        }
        /// <summary>Pins the available shared hardware and default filenames for offline device authoring.</summary>
        /// <param name="commonRoot">Shared bundle or legacy DCS-Common checkout root.</param>
        /// <param name="cancellationToken">Cancellation token for Git reads.</param>
        /// <returns>Shared hardware catalog and canonical Lua sources.</returns>
        public static async Task<IReadOnlyDictionary<string, string>> LoadHardwareAsync(string commonRoot, CancellationToken cancellationToken = default)
        {
            var common = await RepositoryRootAsync(commonRoot, cancellationToken);
            var commit = await SourceCommitAsync(common, cancellationToken);
            var sources = await ReadSourcesAsync(common, SharedCatalogPaths, cancellationToken);
            var catalog = new DcsCommonCatalogImporter().Import(new(commit,
                SharedCatalogPaths[0], sources[SharedCatalogPaths[0]], SharedCatalogPaths[1], sources[SharedCatalogPaths[1]],
                SharedCatalogPaths[2], sources[SharedCatalogPaths[2]], SharedCatalogPaths[3], sources[SharedCatalogPaths[3]]));
            var paths = (await TrackedPathsAsync(common, cancellationToken, "assets/shared/hardware/lua", "assets/shared/ui-layer/input/UiLayer"))
                .Where(x => x.EndsWith(".lua", StringComparison.Ordinal)).ToArray();
            var lua = await ReadSourcesAsync(common, paths, cancellationToken);
            var shared = new DcsCommonLuaImporter().Import(new(commit, catalog, lua.Select(x => new SharedLuaSource(x.Key, x.Value)).ToArray()));
            if (!catalog.Succeeded || !shared.Succeeded)
                throw new InvalidDataException("The shared hardware catalog or Lua controls are invalid.");
            return await HardwareSourcesAsync(common, catalog, shared, lua, cancellationToken);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static async Task<IReadOnlyDictionary<string, string>> HardwareSourcesAsync(string common,
            SharedCatalogImportResult catalog, SharedLuaImportResult shared, IReadOnlyDictionary<string, string> lua,
            CancellationToken cancellationToken)
        {
            var graphics = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var path in await TrackedPathsAsync(common, cancellationToken, "assets/shared/hardware/svg"))
                if (path.EndsWith(".svg", StringComparison.Ordinal))
                    graphics[path] = await SourceTextAsync(common, path, cancellationToken);
            var module = new DcsModule(Guid.NewGuid(), "HEIMDALL", "Shared hardware");
            var choices = new List<WorkspaceHardware>();
            var errors = new List<string>();
            foreach (var device in catalog.Devices)
            {
                var path = $"assets/shared/hardware/svg/{device.StableKey}.svg";
                if (!graphics.ContainsKey(path))
                    continue;
                foreach (var key in new[] { device.StableKey }.Concat(device.Aliases))
                {
                    var defaults = shared.Profiles.FirstOrDefault(x => x.DeviceId == key);
                    var file = defaults?.SourceName ?? device.DisplayName + ".diff.lua";
                    var profile = new DeviceProfile(Guid.NewGuid(), key, defaults?.Instance, key, file);
                    using var metadata = JsonDocument.Parse("""{"includeUiLayer":true,"pages":[]}""");
                    var import = new ConsumerImportResult(new ModuleConfiguration(module, [profile], [], [], "shared hardware"),
                        [], [], [], [], new Dictionary<string, JsonElement> { ["config/kneeboard.json"] = metadata.RootElement.Clone() },
                        new Dictionary<string, string>(), [], []);
                    var diagram = UiLayerDiagramFactory.Create(import, catalog, graphics, shared,
                        (profile, error) => errors.Add(profile.DeviceId + ": " + error.Message)).SingleOrDefault();
                    if (diagram is not null)
                        choices.Add(new(key, device.DisplayName, file, diagram with { Callouts = [] }));
                }
            }
            var result = UiLayerSources(lua);
            result["config/ipia-hardware.json"] = JsonSerializer.Serialize(choices);
            result["config/ipia-device-map.json"] = JsonSerializer.Serialize(catalog.DeviceMap);
            result["config/ipia-hardware-diagnostics.json"] = JsonSerializer.Serialize(errors);
            if (BundledSharedCatalog.IsRoot(common))
                result["config/ipia-shared-assets-provenance.json"] = await File.ReadAllTextAsync(
                    Path.Combine(common, "provenance.json"), cancellationToken);
            return result;
        }
        private static SortedDictionary<string, string> UiLayerSources(IReadOnlyDictionary<string, string> sources) =>
            new SortedDictionary<string, string>(
                sources.Where(x => x.Key.StartsWith("assets/shared/ui-layer/input/UiLayer/", StringComparison.Ordinal))
                    .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal),
                StringComparer.Ordinal);
        private static (IReadOnlyList<CommandPaletteEntry> Commands, string Source,
                        IReadOnlyList<ImportDiagnostic> Diagnostics)
            LoadLinkedCommands(string hueyRoot, IReadOnlyDictionary<string, string> sources, string? moduleId)
        {
            try
            {
                var solution = sources
                                   .Where(x => x.Key.StartsWith("config/", StringComparison.Ordinal) &&
                                               x.Key.EndsWith(".dcs-scaffold.json", StringComparison.Ordinal))
                                   .OrderBy(x => x.Key == $"config/{moduleId}.dcs-scaffold.json" ? 0 : 1)
                                   .ThenBy(x => x.Key, StringComparer.Ordinal)
                                   .FirstOrDefault(x =>
                                                   {
                                                       using var candidate = JsonDocument.Parse(x.Value);
                                                       var root = candidate.RootElement;
                                                       return root.TryGetProperty("import", out var import) &&
                                                              import.TryGetProperty("target", out var target) &&
                                                              target.GetString() == "consumer" &&
                                                              import.TryGetProperty("inputModuleId", out var id) &&
                                                              id.GetString() == moduleId;
                                                   });
                if (string.IsNullOrEmpty(solution.Value))
                    return ([], "No consumer scaffold with a linked command source was found.", []);
                using var document = JsonDocument.Parse(solution.Value);
                var import = document.RootElement.GetProperty("import");
                if (!import.TryGetProperty("commandSource", out var source) || source.ValueKind == JsonValueKind.Null)
                    return ([], "No module command source is linked in the scaffold solution.", []);
                var type = source.GetProperty("type").GetString();
                var paths = source.GetProperty("paths")
                                .EnumerateArray()
                                .Select(x => x.GetString())
                                .Where(x => !string.IsNullOrWhiteSpace(x))
                                .Select(x => Path.IsPathRooted(x!) ? x! : Path.GetFullPath(Path.Combine(hueyRoot, x!)))
                                .ToArray();
                if (paths.Length == 0)
                    throw new InvalidDataException("The linked command source has no paths.");
                var missing = paths.Where(x => !File.Exists(x)).ToArray();
                if (missing.Length > 0)
                    throw new FileNotFoundException(
                        $"Linked command source is missing: {string.Join(", ",missing.Select(Path.GetFileName))}. Relink it in the scaffold solution or import a catalog manually.");
                if (string.IsNullOrWhiteSpace(moduleId))
                    throw new InvalidDataException("The imported module ID is unavailable.");
                var commands =
                    type switch { "catalog-json" when paths.Length ==
                                      1 => DcsCommandCatalogImporter.Parse(File.ReadAllText(paths[0]), moduleId),
                                  "dcs-html" => DcsHtmlCommandCatalogImporter.Parse(
                                      paths.Select(path => (Path.GetFileName(path), File.ReadAllText(path)))),
                                  _ => throw new InvalidDataException($"Unsupported linked command source '{type}'.") };
                return (
                    commands,
                    $"{commands.Count} module commands and named categories loaded from {paths.Length} linked {type} source file(s).",
                    []);
            }
            catch (Exception ex)
                when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or
                          FormatException or InvalidOperationException or KeyNotFoundException or JsonException)
            {
                return ([], "Linked command source could not be loaded.",
                        [new("module-command-source", ex.Message, true)]);
            }
        }
        private static async Task<string> RepositoryRootAsync(string root, CancellationToken ct)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(root);
            if (BundledSharedCatalog.IsRoot(root))
            {
                ct.ThrowIfCancellationRequested();
                BundledSharedCatalog.Validate(root);
                return root;
            }
            if (!Directory.Exists(root))
                throw new DirectoryNotFoundException($"Selected folder does not exist: {root}");
            var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            var gitRoot = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath((await GitAsync(full, ct, "rev-parse", "--show-toplevel")).Trim()));
            if (!string.Equals(full, gitRoot,
                               OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase
                                                           : StringComparison.Ordinal))
                throw new InvalidDataException($"Select the repository root, not a subfolder: {root}");
            return full;
        }
        private static async Task<string[]> UncommittedSourcesAsync(string root, CancellationToken ct)
        {
            var changed = await GitAsync(root, ct, "diff", "--name-only", "--no-renames", "-z", "HEAD", "--");
            var untracked = await GitAsync(root, ct, "ls-files", "--others", "--exclude-standard", "-z");
            return (changed + untracked).Split('\0', StringSplitOptions.RemoveEmptyEntries)
                .Where(path => ConsumerSourceImporter.IsSupportedSourcePath(path) ||
                    (path.StartsWith("kneeboard/", StringComparison.Ordinal) && path.EndsWith(".png", StringComparison.Ordinal)))
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        }
        private static async Task<string[]> TrackedPathsAsync(string root, CancellationToken ct, params string[] paths)
        {
            if (BundledSharedCatalog.IsRoot(root)) return BundledSharedCatalog.Paths(paths);
            var output = await GitAsync(root, ct, ["ls-tree", "-r", "-z", "--name-only", "HEAD", "--", ..paths]);
            return output.Split('\0', StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal).ToArray();
        }
        private static async Task<SortedDictionary<string, string>> ReadSourcesAsync(string root,
                                                                                     IEnumerable<string> paths,
                                                                                     CancellationToken ct)
        {
            var values = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var path in paths)
                values.Add(path, await SourceTextAsync(root, path, ct));
            return values;
        }
        private static async Task<string> SourceCommitAsync(string root, CancellationToken ct) =>
            BundledSharedCatalog.IsRoot(root) ? BundledSharedCatalog.SourceCommit() :
                (await GitAsync(root, ct, "rev-parse", "HEAD")).Trim();
        private static Task<string> SourceTextAsync(string root, string path, CancellationToken ct) =>
            BundledSharedCatalog.IsRoot(root) ? BundledSharedCatalog.ReadTextAsync(path, ct) : GitAsync(root, ct, "show", "HEAD:" + path);
        private static async Task<string> GitAsync(string root, CancellationToken ct, params string[] args)
        {
            using var process = new Process();
            process.StartInfo = new("git") { WorkingDirectory = root,
                                             RedirectStandardOutput = true,
                                             RedirectStandardError = true,
                                             UseShellExecute = false,
                                             CreateNoWindow = true,
                                             StandardOutputEncoding = Encoding.UTF8,
                                             StandardErrorEncoding = Encoding.UTF8 };
            foreach (var argument in args)
                process.StartInfo.ArgumentList.Add(argument);
            try
            {
                if (!process.Start())
                    throw new InvalidOperationException("Unable to start Git.");
                var output = process.StandardOutput.ReadToEndAsync(ct);
                var error = process.StandardError.ReadToEndAsync(ct);
                await process.WaitForExitAsync(ct);
                var stdout = await output;
                var stderr = await error;
                if (process.ExitCode != 0)
                    throw new InvalidDataException($"Git {args[0]} failed in {root}: {stderr.Trim()}");
                return stdout;
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                throw new InvalidOperationException("Git is required to read committed source files.", ex);
            }
        }
        private static async Task<byte[]> GitBytesAsync(string root, CancellationToken ct, params string[] args)
        {
            using var process = new Process();
            process.StartInfo =
                new("git") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true,
                             UseShellExecute = false, CreateNoWindow = true };
            foreach (var argument in args)
                process.StartInfo.ArgumentList.Add(argument);
            try
            {
                if (!process.Start())
                    throw new InvalidOperationException("Unable to start Git.");
                using var output = new MemoryStream();
                var copy = process.StandardOutput.BaseStream.CopyToAsync(output, ct);
                var error = process.StandardError.ReadToEndAsync(ct);
                await Task.WhenAll(copy, process.WaitForExitAsync(ct));
                if (process.ExitCode != 0)
                    throw new InvalidDataException($"Git {args[0]} failed in {root}: {(await error).Trim()}");
                return output.ToArray();
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                throw new InvalidOperationException("Git is required to read committed kneeboard images.", ex);
            }
        }
        private static readonly string[] SharedCatalogPaths = [
            "assets/shared/hardware/manifest.json", "assets/shared/ui-layer/functions.json",
            "assets/shared/ui-layer/hardware-overlays.json", "assets/shared/hardware/scaffold-device-map.json"
        ];
        #endregion END private helpers
    }
}

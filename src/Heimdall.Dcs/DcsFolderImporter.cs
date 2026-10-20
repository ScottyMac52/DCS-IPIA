using Heimdall.Application;
using Heimdall.Application.Interfaces;
using Heimdall.Domain;

namespace Heimdall.Dcs
{
    /// <summary>Reads DCS Saved Games joystick Lua without assuming consumer repository metadata.</summary>
    public sealed class DcsFolderImporter : IDcsFolderImporter
    {
        #region BEGIN Public
        /// <summary>Recognizes a joystick directory or its containing module input directory.</summary>
        /// <param name="folder">The user-selected directory.</param>
        /// <returns>Whether it contains at least one diff.lua profile.</returns>
        public bool HasProfiles(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                return false;
            var source = ProfileDirectory(folder);
            return Directory.Exists(source) &&
                   Directory.EnumerateFiles(source, "*.diff.lua", SearchOption.TopDirectoryOnly).Any();
        }
        /// <summary>Parses selected profile files and the adjacent modifiers file without writing to disk.</summary>
        /// <param name="folder">The selected joystick or module input directory.</param>
        /// <param name="cancellationToken">Cancellation token for file reads.</param>
        /// <returns>Read-only profile, binding, and modifier details.</returns>
        public async Task<DcsFolderImportResult> ImportAsync(string folder,
                                                             CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(folder);
            var diagnostics = new List<ImportDiagnostic>();
            var profiles = new List<DeviceProfile>();
            var bindings = new List<DcsBinding>();
            var modifiers = new List<DcsModifier>();
            var sources = new Dictionary<string, string>(StringComparer.Ordinal);
            var source = ProfileDirectory(folder);
            if (!Directory.Exists(source))
            {
                diagnostics.Add(new("missing-joystick-folder", $"No joystick folder exists at {source}.", true));
                return new(folder, profiles, bindings, modifiers, diagnostics);
            }
            var files = Directory.GetFiles(source, "*.diff.lua", SearchOption.TopDirectoryOnly)
                            .Order(StringComparer.Ordinal)
                            .ToArray();
            if (files.Length == 0)
                diagnostics.Add(new("missing-profiles", $"No .diff.lua profiles exist in {source}.", true));
            var modifierPath = Path.Combine(Directory.GetParent(source)!.FullName, "modifiers.lua");
            if (File.Exists(modifierPath))
            {
                try
                {
                    var text = await File.ReadAllTextAsync(modifierPath, cancellationToken);
                    modifiers.AddRange(DcsLuaParser.ParseModifiers(text));
                    sources["modifiers.lua"] = text;
                }
                catch (Exception ex) when (ex is IOException or FormatException or ArgumentException)
                {
                    diagnostics.Add(new("invalid-modifiers", $"{modifierPath}: {ex.Message}", true));
                }
            }
            else
                diagnostics.Add(new(
                    "missing-modifiers",
                    $"No adjacent modifiers.lua at {modifierPath}; unmodified bindings can still be reviewed.", false));
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var filename = Path.GetFileName(file);
                    var key = filename[..^ ".diff.lua".Length];
                    var guidStart = key.LastIndexOf(" {", StringComparison.Ordinal);
                    if (guidStart >= 0 && key.EndsWith('}'))
                        key = key[..guidStart];
                    var id = StableIdentity.Create("dcs-saved-games-profile", filename.ToUpperInvariant());
                    var profile = new DeviceProfile(id, key, null, "unresolved device", filename);
                    var text = await File.ReadAllTextAsync(file, cancellationToken);
                    var parsed = DcsLuaParser.ParseProfile(text, id);
                    profiles.Add(profile);
                    bindings.AddRange(parsed);
                    sources["joystick/" + filename] = text;
                }
                catch (Exception ex) when (ex is IOException or FormatException or ArgumentException)
                {
                    diagnostics.Add(new("invalid-profile", $"{file}: {ex.Message}", true));
                }
            }
            if (profiles.Select(x => x.Id).Distinct().Count() != profiles.Count)
                diagnostics.Add(new(
                    "ambiguous-profile",
                    "Two files resolve to the same native filename. Resolve the duplicate before importing.",
                    true));
            var keyboard = Path.Combine(Directory.GetParent(source)!.FullName, "keyboard");
            if (Directory.Exists(keyboard))
                foreach (var file in Directory.GetFiles(keyboard, "*.diff.lua").Order(StringComparer.Ordinal))
                {
                    try
                    {
                        var text = await File.ReadAllTextAsync(file, cancellationToken);
                        DcsLuaParser.ParseProfile(text, StableIdentity.Create("dcs-keyboard-profile", Path.GetFileName(file)));
                        sources["keyboard/" + Path.GetFileName(file)] = text;
                    }
                    catch (Exception ex) when (ex is IOException or FormatException or ArgumentException)
                    {
                        diagnostics.Add(new("invalid-keyboard-profile", $"{file}: {ex.Message}", true));
                    }
                }
            var names = modifiers.Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
            foreach (var missing in bindings.SelectMany(x => x.Reformers)
                         .Distinct(StringComparer.Ordinal)
                         .Where(x => !names.Contains(x)))
                diagnostics.Add(new("unknown-modifier",
                                    $"Binding references {missing}, which is not in the adjacent modifiers.lua.",
                                    true));
            return new(folder, profiles.OrderBy(x => x.DeviceId, StringComparer.Ordinal).ToArray(),
                       bindings.OrderBy(x => x.ProfileId).ThenBy(x => x.CommandId, StringComparer.Ordinal).ToArray(),
                       modifiers, diagnostics) { Sources = sources };
        }
        #endregion END Public
        #region BEGIN private helpers
        private static string ProfileDirectory(string folder) =>
            string.Equals(Path.GetFileName(Path.TrimEndingDirectorySeparator(folder)), "joystick",
                          StringComparison.OrdinalIgnoreCase)
                ? folder
                : Path.Combine(folder, "joystick");
        #endregion END private helpers
    }
}

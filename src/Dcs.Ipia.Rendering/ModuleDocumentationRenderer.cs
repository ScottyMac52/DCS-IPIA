using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Rendering
{
    /// <summary>Renders complete module guides from frozen state and parsed deployment output.</summary>
    public static class ModuleDocumentationRenderer
    {
        #region BEGIN Public
        /// <summary>Renders installation, mappings, provenance and explicitly declared integration instructions.</summary>
        /// <param name="workspace">Frozen editor state.</param>
        /// <param name="version">Validated package version.</param>
        /// <param name="sha256">Canonical workspace digest.</param>
        /// <param name="profiles">Parsed generated Lua, including removal history.</param>
        /// <param name="defaults">Captured shared default profile baseline.</param>
        /// <returns>Ordered fixed Markdown filenames with LF endings.</returns>
        public static IReadOnlyDictionary<string, string> Render(EditorWorkspaceSnapshot workspace, string version,
            string sha256, IReadOnlyList<ModuleDocumentationProfile> profiles, IReadOnlyList<ModuleDocumentationProfile> defaults)
        {
            ArgumentNullException.ThrowIfNull(workspace);
            ArgumentNullException.ThrowIfNull(profiles);
            ArgumentNullException.ThrowIfNull(defaults);
            var module = workspace.Configuration.Module;
            var identity = $"Module: {Escape(module.DisplayName)} ({Escape(module.DcsId)}) · Version: {Escape(version)}\n\nWorkspace SHA-256: {Escape(sha256)}\n\n";
            var documents = new SortedDictionary<string, string>(StringComparer.Ordinal) {
                ["INSTALLATION.md"] = Installation(workspace, version),
                ["CONTROL-MAPPINGS.md"] = Mappings(workspace, profiles, defaults),
                ["THIRD-PARTY-ASSETS.md"] = Provenance(workspace)
            };
            if (workspace.Sources?.TryGetValue("config/integrations.json", out var integrations) == true)
                documents["INTEGRATIONS.md"] = Integrations(integrations, module.DcsId);
            documents["README.md"] = "# Module documentation\n\n" + identity +
                "Generated from the same frozen inputs as the OvGME package. No live repository or database is consulted.\n\n" +
                string.Join('\n', documents.Keys.Select(x => $"- [{x}]({x})")) +
                "\n\n## Reproduce or return to a previous version\n\nKeep the exported .ipia-module.json snapshot alongside these guides and the ZIP/checksum. " +
                "Restore it into an HEIMDALL database where that module identity does not already exist. Set the recorded version, then export documentation and build OvGME. " +
                "Restore preserves module identity and pinned UI defaults. Disable the installed OvGME version before enabling an earlier package.\n";
            foreach (var name in documents.Keys.Where(x => x != "README.md").ToArray())
                documents[name] = identity + documents[name];
            return documents;
        }
        #endregion END Public
        #region BEGIN private helpers
        private static string Installation(EditorWorkspaceSnapshot workspace, string version)
        {
            var id = Escape(workspace.Configuration.Module.DcsId);
            return "# Installation and removal\n\n" +
                "1. Close DCS. Back up the following folders outside Saved Games before enabling this package.\n" +
                $"2. Configure OvGME with your active DCS Saved Games folder as its root. Verify the checksum of DCS-{id}-Components-{Escape(version)}-OVGME.zip.\n" +
                "3. Disable any previous or conflicting package, add the ZIP to OvGME and enable it.\n" +
                "4. Open DCS Controls for this aircraft and UI Layer. Verify device columns, modifier switches and axes; test the kneeboard pages in game.\n\n" +
                $"| Destination under Saved Games | Purpose |\n| --- | --- |\n| Config/Input/{id} | Aircraft profiles and modifiers |\n" +
                $"| Config/Input/UiLayer | Module-owned effective UI profiles and modifiers |\n| KNEEBOARD/{id} | Generated reference pages |\n\n" +
                "## Device GUIDs and shared UI files\n\nExact native filenames appear in CONTROL-MAPPINGS.md. If your device GUID differs, load the matching profile into the correct device column in DCS, " +
                "or bind the physical device in HEIMDALL and save a new snapshot. Do not copy one device's GUID to another. " +
                "DCS has one UI Layer directory: enabling another aircraft package can replace overlapping UI files. Verify the effective UI bindings after switching packages.\n\n" +
                "## Remove or restore\n\nClose DCS and disable this package in OvGME before enabling another version. Restore your backed-up folders to return to the pre-package state. " +
                "Documentation export creates no files in Saved Games.\n\n## Kneeboard pages\n\n" +
                Table(["File", "Title", "Kind"], DevicePages(workspace));
        }
        private static IEnumerable<string[]> DevicePages(EditorWorkspaceSnapshot workspace)
        {
            foreach (var page in workspace.Pages.OrderBy(x => x.Order).ThenBy(x => x.StableKey, StringComparer.Ordinal))
            {
                var diagram = workspace.Diagrams.SingleOrDefault(x => x.GenericCapabilities is not null &&
                    KneeboardPageLocator.PageKeyForProfile(workspace, x.ProfileId) == page.StableKey);
                var count = diagram is null ? 1 : GenericKneeboardRenderer.BuildPages(diagram,
                    KneeboardDraftProjection.CreateCombined(workspace, diagram, workspace.Assignments, workspace.Labels)).Count;
                for (var index = 0; index < count; index++)
                {
                    var suffix = index == 0 ? "" : "-" + (index + 1).ToString("D3", System.Globalization.CultureInfo.InvariantCulture);
                    yield return [$"KNEEBOARD/{workspace.Configuration.Module.DcsId}/{page.StableKey}{suffix}.png", page.Title, page.Kind.ToString()];
                }
            }
        }
        private static string Mappings(EditorWorkspaceSnapshot workspace, IReadOnlyList<ModuleDocumentationProfile> profiles,
            IReadOnlyList<ModuleDocumentationProfile> defaults)
        {
            var text = new StringBuilder("# Control mappings\n\nAdded/Changed are active diff entries; Removed records DCS default removals and is not an active assignment. " +
                "These tables describe the generated Lua. An input with no active diff may still have a built-in DCS default; the snapshot cannot enumerate the installed game's defaults.\n\n");
            foreach (var profile in profiles.OrderBy(x => x.Path, StringComparer.Ordinal))
            {
                var layer = profile.Path.StartsWith("Config/Input/UiLayer/", StringComparison.Ordinal) ? AssignmentLayer.UiLayer : AssignmentLayer.Module;
                text.Append("## ").Append(Escape(profile.Path)).Append("\n\nScope: ").Append(layer).Append("\n\n");
                text.Append(Table(["Input", "Chord", "Section", "Command", "Command ID", "Disposition", "Label", "Axis filter"],
                    profile.Bindings.OrderBy(x => x.Section).ThenBy(x => x.InputKey, StringComparer.Ordinal)
                        .ThenBy(x => string.Join(";", x.Reformers), StringComparer.Ordinal).ThenBy(x => x.CommandId, StringComparer.Ordinal)
                        .ThenBy(x => x.Disposition).Select(x => new[] { x.InputKey, Chord(x.Reformers), x.Section.ToString(),
                            x.CommandName, x.CommandId, x.Disposition.ToString(), Label(workspace, profile, layer, x),
                            x.Filter is null ? "Not set" : JsonSerializer.Serialize(x.Filter, FilterOptions) })));
                text.Append(Table(["Modifier", "Native device", "Input", "Mode"], profile.Modifiers.OrderBy(x => x.Name, StringComparer.Ordinal)
                    .Select(x => new[] { x.Name, x.Device, x.Key, x.Mode.ToString() })));
                var diagram = workspace.Diagrams.SingleOrDefault(x => x.ProfileId == profile.ProfileId);
                if (diagram is null) continue;
                text.Append("### Authored controls without an active diff\n\n");
                text.Append(Table(["Input", "Control", "Section", "State"], diagram.Controls
                    .Where(x => !profile.Bindings.Any(b => b.InputKey == x.InputKey && b.Disposition != BindingDisposition.Removed))
                    .OrderBy(x => x.InputKey, StringComparer.Ordinal)
                    .Select(x => new[] { x.InputKey, x.Label, x.IsAxis ? "Axis" : "Key",
                        "No active packaged diff; check DCS defaults/modifier activators" })));
            }
            text.Append("## Pinned shared UI default baseline\n\n");
            if (workspace.SharedDefault is { } shared)
                text.Append("Revision: ").Append(shared.Revision).Append(" · Source commit: ").Append(Escape(shared.SourceCommit))
                    .Append(" · Content SHA-256: ").Append(Escape(shared.ContentSha256 ?? "Not recorded")).Append("\n\n");
            else if (workspace.Sources?.TryGetValue("config/ipia-default-choice.json", out var choice) == true &&
                JsonDocument.Parse(choice).RootElement.GetProperty("choice").GetString() == "empty")
                text.Append("Explicit empty default baseline: no inherited controller profiles, assignments or modifiers.\n\n");
            else text.Append("No separately captured shared default revision. Effective UI output is listed above.\n\n");
            text.Append("The baseline is provenance, not a second installed profile. Module overrides and removals are reflected in the effective UI tables above.\n\n");
            text.Append(Table(["Captured source", "Input", "Chord", "Command", "Disposition"], defaults.OrderBy(x => x.Path, StringComparer.Ordinal)
                .SelectMany(p => p.Bindings.OrderBy(x => x.InputKey, StringComparer.Ordinal).ThenBy(x => x.CommandId, StringComparer.Ordinal)
                    .Select(x => new[] { p.Path, x.InputKey, Chord(x.Reformers), x.CommandName, x.Disposition.ToString() }))));
            return text.ToString();
        }
        private static string Label(EditorWorkspaceSnapshot workspace, ModuleDocumentationProfile profile, AssignmentLayer layer, DcsBinding binding)
        {
            if (profile.ProfileId is null || binding.Disposition == BindingDisposition.Removed) return "";
            var slot = new DraftAssignmentSlot(workspace.Configuration.Module.Id, profile.ProfileId.Value, layer,
                binding.Section, binding.InputKey, string.Join("+", binding.Reformers));
            return workspace.Labels.GetValueOrDefault(slot.Key) ?? binding.CommandName;
        }
        private static string Provenance(EditorWorkspaceSnapshot workspace)
        {
            var sources = workspace.Sources ?? new Dictionary<string, string>();
            var text = new StringBuilder("# Third-party assets and provenance\n\nModule provenance: ").Append(Escape(workspace.Configuration.Provenance))
                .Append("\n\nCaptured source hashes identify the exact frozen inputs; they do not grant redistribution rights. " +
                    "Retain the captured license/attribution statements below when redistributing. Missing statements require review before publishing.\n\n");
            text.Append(Table(["Captured source", "UTF-8 SHA-256"], sources.OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => new[] { x.Key, Hash(Encoding.UTF8.GetBytes(x.Value)) })));
            text.Append("## Images and authored diagrams\n\n");
            text.Append(Table(["Asset", "SHA-256"], workspace.Images.OrderBy(x => x.Path, StringComparer.Ordinal).Select(x => new[] { x.Path, x.Sha256 })
                .Concat(workspace.Diagrams.OrderBy(x => x.ProfileId).Select(x => new[] { x.Title + " (SVG)", Hash(Encoding.UTF8.GetBytes(x.Svg)) }))));
            var statements = sources.Where(x => x.Key.Contains("license", StringComparison.OrdinalIgnoreCase) ||
                x.Key.Contains("attribution", StringComparison.OrdinalIgnoreCase) || x.Key.Contains("third-party", StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Key, StringComparer.Ordinal).ToArray();
            if (statements.Length == 0) text.Append("\nNo license or attribution statement was captured in this snapshot.\n");
            foreach (var statement in statements)
                text.Append("\n## ").Append(Escape(statement.Key)).Append("\n\n").Append(Quoted(statement.Value));
            using var stream = typeof(ModuleDocumentationRenderer).Assembly.GetManifestResourceStream("Dcs.Ipia.Rendering.Fonts.DejaVuSans-IPIA.LICENSE.txt")!;
            using var reader = new StreamReader(stream);
            text.Append("\n## HEIMDALL rendering font license\n\n").Append(Quoted(reader.ReadToEnd()));
            return text.ToString();
        }
        private static string Integrations(string source, string moduleDcsId)
        {
            var entries = ModuleIntegrationMetadata.Parse(source, moduleDcsId);
            return "# Explicit module integrations\n\nThese instructions are captured module metadata. They do not install scripts or enable integrations. " +
                "Imported consumer guides may describe an earlier layout; check their paths and commands against this release's generated installation and control mappings.\n\n" +
                string.Join('\n', entries.Select(x => "## " + Escape(x.Name) + "\n\n" + Quoted(x.Instructions)));
        }
        private static string Table(string[] headers, IEnumerable<string[]> values)
        {
            var rows = values.Select(x => "| " + string.Join(" | ", x.Select(Escape)) + " |").ToArray();
            return "| " + string.Join(" | ", headers) + " |\n| " + string.Join(" | ", headers.Select(_ => "---")) + " |\n" +
                (rows.Length == 0 ? "| " + string.Join(" | ", headers.Select(_ => "—")) + " |" : string.Join('\n', rows)) + "\n\n";
        }
        private static string Chord(IReadOnlyList<string> values) => values.Count == 0 ? "Base" : string.Join(" + ", values);
        private static string Escape(string value) => System.Net.WebUtility.HtmlEncode(value).Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("|", "\\|", StringComparison.Ordinal).Replace("\r", "", StringComparison.Ordinal).Replace("\n", "<br>", StringComparison.Ordinal)
            .Replace("`", "\\`", StringComparison.Ordinal).Replace("*", "\\*", StringComparison.Ordinal).Replace("[", "\\[", StringComparison.Ordinal)
            .Replace("]", "\\]", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal).Replace("#", "\\#", StringComparison.Ordinal);
        private static string Quoted(string text) => string.Join('\n', text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n')
            .Split('\n').Select(x => "    " + x)) + "\n";
        private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
        private static readonly JsonSerializerOptions FilterOptions = new() { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };
        #endregion END private helpers
    }
}

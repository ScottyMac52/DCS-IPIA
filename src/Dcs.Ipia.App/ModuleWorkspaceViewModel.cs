using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Dcs;

namespace DcsIpia.Presentation
{
    /// <summary>Formats validated repository or Saved Games inputs for the read-only preview.</summary>
    public sealed class ModuleWorkspaceViewModel
    {
        /// <summary>Builds a committed Huey preview with existing rendered PNG assets.</summary>
        /// <param name="preview">The completed repository import and committed kneeboard images.</param>
        public ModuleWorkspaceViewModel(ModuleWorkspacePreview preview)
        {
            ArgumentNullException.ThrowIfNull(preview);
            var result = preview.Import;
            if (!result.Succeeded)
                throw new ArgumentException("Only validated imports can be previewed.", nameof(preview));
            var configuration = result.Configuration!;
            ModuleId = configuration.Module.Id;
            ModuleDcsId = configuration.Module.DcsId;
            ImportedBindings = configuration.Bindings;
            WindowTitle = configuration.Module.DisplayName + " · assignment editor · " + ApplicationVersion.Label;
            Summary =
                $"{configuration.Module.DisplayName} · {configuration.Profiles.Count} profiles · {configuration.Bindings.Count} bindings · {result.Pages.Count} pages";
            Commit = $"{configuration.Module.DcsId} commit: {result.Provenance[0].Commit}";
            SourceNote =
                $"{result.Provenance.Count} committed source files · {preview.Images.Count} existing kneeboard PNGs · uncommitted changes excluded";
            Profiles =
                configuration.Profiles
                    .Select(
                        x =>
                            $"{x.DeviceId}  ·  {x.Instance??"single instance"}  ·  {configuration.Bindings.Count(y=>y.ProfileId==x.Id)} bindings  ·  {x.SourceName}")
                    .ToArray();
            Bindings =
                configuration.Bindings
                    .Select(
                        x =>
                        {
                            var profile = configuration.Profiles.Single(y => y.Id == x.ProfileId);
                            return $"{profile.DeviceId} {profile.Instance}  ·  {x.InputKey}  ·  {x.CommandName}  ·  {x.Disposition}{(x.Reformers.Count==0?"":$"  ·  {string.Join(" + ",x.Reformers)}")}";
                        })
                    .ToArray();
            Pages = result.Pages.Select(x => $"{x.Order:00}  ·  {x.Title}").ToArray();
            PageKeys = result.Pages.Select(x => x.StableKey).ToArray();
            Images = preview.Images;
            UiDiagrams = preview.Diagrams;
            Labels = KneeboardLabelImporter.Import(result.Metadata, configuration);
            ModuleCommands = CommandPalette.FromBindings(configuration.Bindings, preview.ModuleCommands);
            (Draft, DraftNote) = CreateDraft(preview, ModuleCommands);
            ModuleModifierChoices = [
                "Base", ..configuration.Modifiers.Select(x => x.Name)
                            .Concat(configuration.Bindings.Where(x => x.Reformers.Count > 1)
                                        .Select(x => string.Join(" + ", x.Reformers)))
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .Distinct(StringComparer.Ordinal)
            ];
            UiModifierChoices = [
                "Base", ..preview.Diagrams.SelectMany(x => x.Callouts)
                            .Select(x => x.Modifier)
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .Distinct(StringComparer.Ordinal)
            ];
            ModifierChoices = ModuleModifierChoices;
            UiCommands = CommandPalette.FromUiLayer(preview.UiCommands);
            PaletteSourceNote =
                preview.ModuleCommands.Count > 0
                    ? preview.ModuleCommandSource
                    : $"{preview.ModuleCommandSource} Import the module command JSON or DCS Controls HTML to show all named categories; bindings without a catalog remain Uncategorized.";
            UiUsage =
                preview.Diagrams.Count == 0
                ?[preview.GraphicDiagnostics.Count > 0
                      ? "UI Layer graphic unavailable. See Diagnostics; imported kneeboards and profiles remain available."
                      : "No graphical UI Layer assignments found in this committed source."] : [];
            var issues =
                result.Diagnostics.Concat(preview.GraphicDiagnostics).Concat(preview.CommandDiagnostics).ToArray();
            HasErrors = issues.Any(x => x.IsError);
            Diagnostics = issues.Length == 0 ?["No import diagnostics."]
                : issues.Select(x => $"{(x.IsError?"Error":"Note")}  ·  {x.Code}: {x.Message}")
                      .ToArray();
            Provenance = result.Provenance.Select(x => $"{x.Path}  ·  SHA-256 {x.Sha256}").ToArray();
        }
        /// <summary>Reopens a persisted workspace using only local SQLite data.</summary>
        /// <param name="saved">Stored module, SVGs, catalogs, and assignments.</param>
        public ModuleWorkspaceViewModel(EditorWorkspaceSnapshot saved)
            : this(new ModuleWorkspacePreview(
                  new ConsumerImportResult(saved.Configuration, [], saved.Pages, [], [],
                                           new Dictionary<string, System.Text.Json.JsonElement>(),
                                           new Dictionary<string, string>(),
                                           [new SourceProvenance("SQLite", "local", "editor_workspaces", "local")], []),
                  saved.Images,
                  saved.Diagrams) { ModuleCommands = saved.ModuleCommands, UiCommands = saved.UiCommands })
        {
            saved = PinnedUiLayerProfileRecovery.Restore(saved);
            UiCommands = CommandPalette.FromUiLayer(saved.UiCommands);
            UiDiagrams = saved.Diagrams;
            ModuleCommands = saved.ModuleCommands;
            var moduleCommands =
                saved.ModuleCommands.Select(x => new DraftCommand(x.Id, x.Name, x.IsAxis, AssignmentLayer.Module));
            var uiCommands = saved.UiCommands.Select(
                x => new DraftCommand(x.DcsCommandId, x.Name, x.IsAxis, AssignmentLayer.UiLayer));
            Draft = new DraftAssignmentService(
                saved.Configuration, moduleCommands.Concat(uiCommands), saved.Assignments,
                WorkspaceStructureEditor.UiModifierNames(saved));
            DraftNote = "Changes remain pending until Save to database.";
            WindowTitle = saved.Configuration.Module.DisplayName + " · database assignment editor · " + ApplicationVersion.Label;
            Commit = "Saved in local SQLite database";
            SourceNote = "Editor graphics, command catalogs, kneeboards, labels and assignments loaded from SQLite.";
            Labels = new Dictionary<string, string>(saved.Labels, StringComparer.Ordinal);
            Workspace = saved;
        }
        /// <summary>Builds a live Saved Games profile preview without inventing device IDs or kneeboard output.</summary>
        /// <param name="folder">The parsed local joystick folder.</param>
        public ModuleWorkspaceViewModel(DcsFolderImportResult folder)
        {
            ArgumentNullException.ThrowIfNull(folder);
            if (folder.Profiles.Count == 0)
                throw new ArgumentException("No readable profiles are available for preview.", nameof(folder));
            ModuleId = Guid.Empty;
            SourceFolder = folder.Folder;
            ModuleDcsId = null;
            ImportedBindings = folder.Bindings;
            WindowTitle = "DCS Saved Games · joystick source preview · " + ApplicationVersion.Label;
            Summary =
                $"{folder.Profiles.Count} local profiles · {folder.Bindings.Count} bindings · {folder.Modifiers.Count} modifiers";
            Commit = $"Source folder: {folder.Folder}";
            SourceNote = "Reading live Saved Games files · device identities are not inferred from filenames";
            Profiles =
                folder.Profiles
                    .Select(
                        x =>
                            $"{x.SourceName}  ·  {folder.Bindings.Count(y=>y.ProfileId==x.Id)} bindings  ·  device not resolved")
                    .ToArray();
            Bindings =
                folder.Bindings
                    .Select(
                        x =>
                        {
                            var profile = folder.Profiles.Single(y => y.Id == x.ProfileId);
                            return $"{profile.SourceName}  ·  {x.InputKey}  ·  {x.CommandName}  ·  {x.Disposition}{(x.Reformers.Count==0?"":$"  ·  {string.Join(" + ",x.Reformers)}")}";
                        })
                    .ToArray();
            Pages = [
                "Saved Games joystick files contain no kneeboard images. Open a module repository to preview its committed PNGs."
            ];
            PageKeys = [];
            Images = [];
            UiDiagrams = [];
            Draft = null;
            ModuleModifierChoices = ["Base"];
            UiModifierChoices = ["Base"];
            ModifierChoices = ModuleModifierChoices;
            DraftNote = "Import both committed repositories to edit an identified device.";
            ModuleCommands = CommandPalette.FromBindings(folder.Bindings);
            UiCommands = [];
            PaletteSourceNote =
                "Aircraft list: commands found in local bindings (partial catalog). Open both repositories for canonical UI Layer commands.";
            UiUsage = ["Canonical UI Layer graphics require a module repository and DCS-Common catalog."];
            HasErrors = folder.Diagnostics.Any(x => x.IsError);
            Diagnostics = folder.Diagnostics.Count == 0 ?["No import diagnostics."]
                : folder.Diagnostics.Select(x => $"{(x.IsError?"Error":"Note")}  ·  {x.Code}: {x.Message}")
                      .ToArray();
            Provenance = ["Live local files; no repository commit or source hash was claimed."];
        }
        #region BEGIN Public
        /// <summary>Gets the saved editor workspace when opened from SQLite.</summary>
        public EditorWorkspaceSnapshot? Workspace { get; }
        /// <summary>Gets the persisted callout label overrides.</summary>
        public IReadOnlyDictionary<string, string> Labels { get; } = new Dictionary<string, string>();
        /// <summary>Gets or sets the selected import source root.</summary>
        public string? CommonRoot { get; set; }
        /// <summary>Gets or sets the source folder for the one-time import.</summary>
        public string? SourceFolder { get; set; }
        /// <summary>Gets the actual DCS module identity for validating an imported command catalog.</summary>
        public string? ModuleDcsId { get; }
        /// <summary>Gets imported bindings used to include existing commands when a catalog is loaded.</summary>
        public IReadOnlyList<DcsBinding> ImportedBindings { get; }
        /// <summary>Gets the selected imported module identity.</summary>
        public Guid ModuleId { get; }
        /// <summary>Gets the preview window title.</summary>
        public string WindowTitle { get; }
        /// <summary>Gets the module and import counts.</summary>
        public string Summary { get; }
        /// <summary>Gets the source origin.</summary>
        public string Commit { get; }
        /// <summary>Gets a reminder about committed or live source behavior.</summary>
        public string SourceNote { get; }
        /// <summary>Gets device profile rows.</summary>
        public IReadOnlyList<string> Profiles { get; }
        /// <summary>Gets aircraft binding rows.</summary>
        public IReadOnlyList<string> Bindings { get; }
        /// <summary>Gets kneeboard page labels or a missing asset explanation.</summary>
        public IReadOnlyList<string> Pages { get; }
        /// <summary>Gets page identities aligned to the displayed rows.</summary>
        public IReadOnlyList<string> PageKeys { get; }
        /// <summary>Gets existing committed kneeboard PNGs.</summary>
        public IReadOnlyList<KneeboardImageAsset> Images { get; }
        /// <summary>Gets diagrams positioned on committed shared hardware images.</summary>
        public IReadOnlyList<UiLayerDiagram> UiDiagrams { get; }
        /// <summary>Gets imported aircraft commands; this is not a complete aircraft command catalog.</summary>
        public IReadOnlyList<CommandPaletteEntry> ModuleCommands { get; }
        /// <summary>Gets canonical UI Layer commands, when DCS-Common was imported.</summary>
        public IReadOnlyList<CommandPaletteEntry> UiCommands { get; }
        /// <summary>Gets command source and completeness information.</summary>
        public string PaletteSourceNote { get; }
        /// <summary>Gets the in-memory draft editor when canonical controls and source identities are available.</summary>
        public DraftAssignmentService? Draft { get; }
        /// <summary>Gets Base and native module modifier chords.</summary>
        public IReadOnlyList<string> ModuleModifierChoices { get; }
        /// <summary>Gets Base and canonical UI Layer modifier chords used by this module.</summary>
        public IReadOnlyList<string> UiModifierChoices { get; }
        /// <summary>Gets the module modifier choices for compatibility with existing previews.</summary>
        public IReadOnlyList<string> ModifierChoices { get; }
        /// <summary>Gets the editor availability or unmapped-binding diagnostic.</summary>
        public string DraftNote { get; }
        /// <summary>Gets an explanation when graphics are unavailable.</summary>
        public IReadOnlyList<string> UiUsage { get; }
        /// <summary>Gets whether any source or graphic diagnostic requires attention.</summary>
        public bool HasErrors { get; }
        /// <summary>Gets import diagnostics.</summary>
        public IReadOnlyList<string> Diagnostics { get; }
        /// <summary>Gets source hashes or their availability explanation.</summary>
        public IReadOnlyList<string> Provenance { get; }
        #endregion END Public
        #region BEGIN private helpers
        private static (DraftAssignmentService?, string)
            CreateDraft(ModuleWorkspacePreview preview, IReadOnlyList<CommandPaletteEntry> catalog)
        {
            var configuration = preview.Import.Configuration!;
            if (preview.Diagrams.Count == 0 || preview.Diagrams.All(x => x.Controls.Count == 0))
                return (null, "No mapped controls in a validated draw.io-generated SVG; editing is unavailable.");
            try
            {
                var moduleCommands =
                    catalog.Select(x => new DraftCommand(x.Id, x.Name, x.IsAxis, AssignmentLayer.Module));
                var moduleNames = catalog.ToDictionary(x => x.Id, x => x.Name, StringComparer.Ordinal);
                var uiCommands = preview.UiCommands.Select(
                    x => new DraftCommand(x.DcsCommandId, x.Name, x.IsAxis, AssignmentLayer.UiLayer));
                var assignments = new List<DraftAssignment>();
                var unmatched = 0;
                foreach (var binding in configuration.Bindings.Where(
                             x => x.Disposition != global::Dcs.Ipia.Domain.BindingDisposition.Removed))
                {
                    var diagram = preview.Diagrams.FirstOrDefault(x => x.ProfileId == binding.ProfileId);
                    var matches =
                        diagram?.Controls.Where(x => DcsInputKey.SamePhysicalInput(x.InputKey, binding.InputKey))
                            .ToArray() ??
                        [];
                    if (matches.Length == 0)
                    {
                        unmatched++;
                        continue;
                    }
                    var slot =
                        new DraftAssignmentSlot(configuration.Module.Id, binding.ProfileId, AssignmentLayer.Module,
                                                binding.Section, binding.InputKey, string.Join("+", binding.Reformers));
                    assignments.Add(new DraftAssignment(
                        slot,
                        new(binding.CommandId, moduleNames.GetValueOrDefault(binding.CommandId, binding.CommandName),
                            binding.Section == global::Dcs.Ipia.Domain.BindingSection.Axis,
                            AssignmentLayer.Module)) { Filter = binding.Filter });
                }
                foreach (var diagram in preview.Diagrams)
                    foreach (var callout in diagram.Callouts)
                    {
                        var control = diagram.Controls.SingleOrDefault(x => x.Control == callout.Control);
                        if (control is null)
                        {
                            unmatched++;
                            continue;
                        }
                        assignments.Add(
                            new(new(configuration.Module.Id, diagram.ProfileId, AssignmentLayer.UiLayer,
                                    global::Dcs.Ipia.Domain.BindingSection.Key,
                                    DcsInputKey.Resolve(configuration, diagram.ProfileId,
                                                        global::Dcs.Ipia.Domain.BindingSection.Key, control.InputKey),
                                    callout.Modifier.Replace(" + ", "+", StringComparison.Ordinal)),
                                new(callout.CommandId, callout.Function, false, AssignmentLayer.UiLayer)));
                    }
                var unique =
                    assignments.GroupBy(x => x.Slot.Key, StringComparer.Ordinal).Select(x => x.First()).ToArray();
                var editor = new DraftAssignmentService(
                    configuration, moduleCommands.Concat(uiCommands), unique,
                    preview.Diagrams.SelectMany(x => x.Callouts)
                        .SelectMany(x => x.Modifier.Split('+', StringSplitOptions.TrimEntries |
                                                                   StringSplitOptions.RemoveEmptyEntries)));
                return (
                    editor,
                    unmatched == 0
                        ? "Changes are staged in memory. Review, undo or discard them before leaving this window."
                        : $"{unmatched} imported bindings could not be mapped to authored controls and are excluded from this draft. No source file is changed.");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return (null, "Editing unavailable: " + ex.Message);
            }
        }
        #endregion END private helpers
    }
}

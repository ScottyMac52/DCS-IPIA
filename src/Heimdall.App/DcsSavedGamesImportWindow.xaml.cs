using System.IO;
using System.Windows;
using Heimdall.Application;
using Heimdall.Application.Interfaces;
using Heimdall.Dcs;

namespace Heimdall.App
{
    /// <summary>Reviews Saved Games hardware associations and creates a new editable SQLite module.</summary>
    public partial class DcsSavedGamesImportWindow : Window
    {
        private readonly DcsFolderImportResult _folder;
        private readonly DcsTrainingEnvironment? _training;
        private readonly Action? _completed;
        private readonly DcsSharedUiLayerWorkspace _defaults;
        private readonly IReadOnlyDictionary<string, string> _hardwareSources;
        private readonly IDcsEditorWorkspaceRepository _repository;
        private readonly IReadOnlyList<DcsSavedGamesProfileChoice> _choices;
        /// <summary>Opens a module and hardware review without writing source files or the database.</summary>
        /// <param name="folder">Frozen profiles read from the selected module folder.</param>
        /// <param name="defaults">Current shared UI Layer revision to pin.</param>
        /// <param name="hardwareSources">Internal hardware sources and recognition rules.</param>
        /// <param name="repository">Destination module storage.</param>
        /// <param name="completed">Optional completion callback for an embedded form.</param>
        /// <param name="training">Optional session-owned sample access; never changes the active edition.</param>
        public DcsSavedGamesImportWindow(DcsFolderImportResult folder, DcsSharedUiLayerWorkspace defaults,
            IReadOnlyDictionary<string, string> hardwareSources, IDcsEditorWorkspaceRepository repository, Action? completed = null, DcsTrainingEnvironment? training = null)
        {
            training?.RequireSources(repository, defaults, hardwareSources);
            _training = training;
            _completed = completed;
            _folder = folder;
            _defaults = defaults;
            _hardwareSources = hardwareSources;
            _repository = repository;
            var catalog = new DcsEditorWorkspaceSnapshot(new(new(Guid.NewGuid(), "catalog", "Hardware"), [], [], [], "internal catalog"),
                [], [], [], [], [], [], new Dictionary<string, string>(), hardwareSources);
            var hardware = EditionAccess.Hardware(DcsWorkspaceStructureEditor.Hardware(catalog), _training).ToArray();
            _choices = folder.Profiles.Select(profile =>
            {
                var physical = DcsPhysicalIdentity.FromFileName(profile.SourceName);
                var suggested = physical is { } guid
                    ? DcsDetectedDeviceHardwareMatcher.Match(catalog, new(guid, DcsPhysicalIdentity.NameFromFileName(profile.SourceName), false, profile.SourceName))
                    : null;
                return new DcsSavedGamesProfileChoice(profile, folder.Bindings.Where(x => x.ProfileId == profile.Id).ToArray(), hardware,
                    hardware.FirstOrDefault(x => x.DeviceId == suggested?.DeviceId));
            }).ToArray();
            InitializeComponent();
            ProfileChoices.ItemsSource = _choices;
            var selected = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder.Folder));
            var moduleFolder = Path.GetFileName(selected).Equals("joystick", StringComparison.OrdinalIgnoreCase)
                ? Directory.GetParent(selected)!.FullName : selected;
            DcsIdInput.Text = DisplayNameInput.Text = Path.GetFileName(moduleFolder);
            ImportSummary.Text = folder.Profiles.Count + " joystick profiles · " + folder.Bindings.Count + " bindings · " +
                folder.Modifiers.Count + " modifiers · " + folder.Sources.Keys.Count(x => x.StartsWith("keyboard/", StringComparison.Ordinal)) + " keyboard profiles. " +
                string.Join(" ", folder.Diagnostics.Select(x => x.Message));
            ImportSavedGamesButton.IsEnabled = folder.Succeeded;
        }
        #region BEGIN Public
        /// <summary>Gets the successfully persisted and reopened module, or null after cancellation.</summary>
        public DcsEditorWorkspaceSnapshot? ImportedWorkspace { get; private set; }
        #endregion END Public
        #region BEGIN private helpers
        private async void ImportClicked(object sender, RoutedEventArgs e)
        {
            ImportSavedGamesButton.IsEnabled = false;
            try
            {
                if (_choices.Any(x => x.Hardware is null))
                    throw new InvalidDataException("Choose hardware for every profile before importing.");
                DcsNewModuleWorkspaceFactory.ValidateIdentity(DcsIdInput.Text, DisplayNameInput.Text);
                foreach (var id in await _repository.ListAsync())
                    if ((await _repository.GetAsync(id))?.Configuration.Module.DcsId.Equals(DcsIdInput.Text, StringComparison.OrdinalIgnoreCase) == true)
                        throw new InvalidDataException("This DCS input folder already has a saved module. Open it, or delete it explicitly before importing a replacement.");
                var workspace = DcsSavedGamesWorkspaceFactory.Create(_folder, DcsIdInput.Text, DisplayNameInput.Text, _defaults,
                    _hardwareSources, _choices.Select(x => new DcsSavedGamesDeviceSelection(x.Profile.Id, x.Hardware!)).ToArray(), emptyDefaults: _training is null && !EditionAccess.Policy.HasPremium);
                EditionAccess.RequireWorkspace(workspace, training: _training);
                await _repository.SaveAsync(workspace);
                ImportedWorkspace = await _repository.GetAsync(workspace.Configuration.Module.Id)
                    ?? throw new InvalidDataException("The imported module could not be reopened from SQLite.");
                if (_completed is null) DialogResult = true;
                else _completed();
            }
            catch (Exception ex) { ImportError.Text = "Module was not imported: " + ex.Message; }
            finally { ImportSavedGamesButton.IsEnabled = _folder.Succeeded; }
        }
        #endregion END private helpers
    }
}

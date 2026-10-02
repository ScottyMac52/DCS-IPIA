using System.IO;
using System.Windows;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;
using Dcs.Ipia.Dcs;

namespace DcsIpia.Presentation
{
    /// <summary>Creates a local module with reviewed identity, hardware choices and a pinned default baseline.</summary>
    public partial class NewModuleWindow : Window
    {
        private readonly IEditorWorkspaceRepository _repository;
        private readonly SharedUiLayerWorkspace _defaults;
        private readonly IReadOnlyDictionary<string, string> _hardwareSources;
        /// <summary>Initializes a creation form from verified internal assets.</summary>
        /// <param name="repository">Local module storage.</param>
        /// <param name="defaults">Saved shared defaults to pin.</param>
        /// <param name="hardwareSources">Pinned internal hardware choices.</param>
        public NewModuleWindow(IEditorWorkspaceRepository repository, SharedUiLayerWorkspace defaults,
            IReadOnlyDictionary<string, string> hardwareSources)
        {
            _repository = repository;
            _defaults = defaults;
            _hardwareSources = hardwareSources;
            InitializeComponent();
            BaselineText.Text = $"Shared default UI Layer revision {defaults.Revision} will be copied into this module.";
            HardwareChoices.ItemsSource = WorkspaceStructureEditor.Hardware(new(
                new(new(Guid.NewGuid(), "catalog", "Hardware"), [], [], [], "internal catalog"),
                [], [], [], [], [], [], new Dictionary<string, string>(), hardwareSources));
        }
        #region BEGIN Public
        /// <summary>Gets the successfully saved and reopened module, or null when cancelled.</summary>
        public EditorWorkspaceSnapshot? CreatedWorkspace { get; private set; }
        #endregion END Public
        #region BEGIN private helpers
        private async void CreateClicked(object sender, RoutedEventArgs e)
        {
            CreateButton.IsEnabled = false;
            try
            {
                NewModuleWorkspaceFactory.ValidateIdentity(DcsIdInput.Text, DisplayNameInput.Text);
                foreach (var id in await _repository.ListAsync())
                    if ((await _repository.GetAsync(id))?.Configuration.Module.DcsId.Equals(DcsIdInput.Text, StringComparison.OrdinalIgnoreCase) == true)
                        throw new InvalidOperationException("That DCS input folder already has a saved module. Open it or choose another identity.");
                var workspace = NewModuleWorkspaceFactory.Create(DcsIdInput.Text, DisplayNameInput.Text, _defaults, _hardwareSources);
                foreach (var hardware in HardwareChoices.SelectedItems.Cast<WorkspaceHardware>())
                {
                    var fileName = Path.GetFileName(hardware.DefaultFileName.Replace('\\\\', '/'));
                    var inherited = workspace.Configuration.Profiles.FirstOrDefault(x => x.DeviceId == hardware.DeviceId &&
                        x.SourceName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
                    workspace = WorkspaceStructureEditor.AddDevice(workspace, hardware, AssignmentLayer.Module,
                        inherited?.SourceName ?? fileName, inherited?.Instance, null);
                }
                await _repository.SaveAsync(workspace);
                CreatedWorkspace = await _repository.GetAsync(workspace.Configuration.Module.Id)
                    ?? throw new InvalidDataException("The new module could not be reopened from SQLite.");
                DialogResult = true;
            }
            catch (Exception ex)
            {
                CreationError.Text = ex.Message;
            }
            finally
            {
                CreateButton.IsEnabled = true;
            }
        }
        #endregion END private helpers
    }
}

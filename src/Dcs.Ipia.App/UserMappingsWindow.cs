using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;
using Dcs.Ipia.Infrastructure;
using Dcs.Ipia.Rendering;
using Microsoft.Win32;

namespace DcsIpia.Presentation
{
    /// <summary>Previews validated imports and publishes immutable user-library revisions without mutating modules.</summary>
    public sealed class UserMappingsWindow : Window
    {
        private readonly IUserMappingLibrary _library;
        private readonly IReadOnlyList<UserMappingRevision> _modulePins;
        private readonly Func<string?>? _sourcePicker;
        private readonly Func<string?>? _exportPicker;
        private readonly ComboBox _definitions;
        private readonly ListBox _revisions;
        private readonly Image _preview;
        private readonly TextBlock _status;
        private readonly Button _import;
        private readonly Button _bundled;
        private readonly Button _discover;
        private readonly string? _bundledDrawingsDirectory;
        private readonly bool _canDiscoverHardware;
        private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _bundledInputs;
        private readonly Button _save;
        private readonly Button _export;
        private readonly Button _starter;
        private readonly Button _review;
        private readonly Button _delete;
        private HashSet<string> _libraryRevisionIds = new(StringComparer.Ordinal);
        private readonly IEditorWorkspaceRepository? _workspaces;
        private readonly Func<string?>? _starterPicker;
        private UserMappingRevision? _candidate;
        private bool _busy;
        /// <summary>Creates profile-scoped mapping tools using explicit native target definitions.</summary>
        /// <param name="library">Immutable profile library.</param>
        /// <param name="definitions">Verified discovered capability choices.</param>
        /// <param name="sourcePicker">Optional source browser adapter.</param>
        /// <param name="exportPicker">Optional editable bundle destination adapter.</param>
        /// <param name="targetNames">Physical labels for native definition selection.</param>
        /// <param name="bundledDrawingsDirectory">Folder containing the installed editable hardware drawings.</param>
        /// <param name="bundledInputs">Bundled filename-to-control mappings from the installed catalog.</param>
        /// <param name="canDiscoverHardware">Whether the owner can open discovery and refresh this window.</param>
        /// <param name="modulePins">Read-only revisions carried by saved/portable modules.</param>
        /// <param name="workspaces">Optional saved modules for explicit reviewed adoption.</param>
        /// <param name="starterPicker">Optional starter export destination adapter.</param>
        public UserMappingsWindow(IUserMappingLibrary library, IReadOnlyList<HardwareDefinition> definitions,
            Func<string?>? sourcePicker = null, Func<string?>? exportPicker = null, IReadOnlyDictionary<string, string>? targetNames = null, IReadOnlyList<UserMappingRevision>? modulePins = null,
            string? bundledDrawingsDirectory = null, bool canDiscoverHardware = false,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? bundledInputs = null,
            IEditorWorkspaceRepository? workspaces = null, Func<string?>? starterPicker = null)
        {
            _library = library;
            _workspaces = workspaces; _starterPicker = starterPicker;
            _bundledDrawingsDirectory = bundledDrawingsDirectory;
            _canDiscoverHardware = canDiscoverHardware;
            _bundledInputs = bundledInputs ?? new Dictionary<string, IReadOnlyDictionary<string, string>>();
            _modulePins = modulePins ?? [];
            _sourcePicker = sourcePicker; _exportPicker = exportPicker;
            Title = "Hardware drawings — import and saved drawings";
            Width = 1000; Height = 760; MinWidth = 700; MinHeight = 500;
            SetResourceReference(BackgroundProperty, "WindowBackgroundBrush");
            SetResourceReference(ForegroundProperty, "PrimaryTextBrush");
            NameScope.SetNameScope(this, new NameScope());
            var panel = new DockPanel { Margin = new Thickness(18) };
            var heading = new StackPanel();
            heading.Children.Add(new TextBlock { Text = "1. Choose a controller. 2. Open a draw.io drawing and check the preview. 3. Save the drawing.", TextWrapping = TextWrapping.Wrap });
            heading.Children.Add(new TextBlock { Text = "To use a saved drawing: Open module → Devices → select the device and saved drawing → Apply drawing to selected device → Save. Importing alone does not change a module.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 12) });
            heading.Children.Add(new TextBlock { Text = "Controller for this drawing" });
            _definitions = new ComboBox { ItemsSource = definitions.Select(x => new KeyValuePair<string, HardwareDefinition>(
                (targetNames?.GetValueOrDefault(x.Id) ?? "Controller") + " · " + x.Id[..12], x)).ToArray(), DisplayMemberPath = "Key", SelectedValuePath = "Value", SelectedIndex = definitions.Count > 0 ? 0 : -1, Margin = new Thickness(0, 4, 0, 10) };
            RegisterName("MappingDefinitions", _definitions); heading.Children.Add(_definitions);
            var buttons = new WrapPanel();
            _import = Button("Open draw.io file…", "PreviewMappingButton", PreviewClicked);
            _bundled = Button("Browse bundled drawings…", "BundledMappingButton", async (_, _) => await PreviewSourceAsync(true));
            _discover = Button("Discover / manage controllers…", "DiscoverMappingHardwareButton", (_, _) =>
            {
                DiscoverHardwareRequested = true;
                Close();
            });
            _save = Button("Save drawing", "SaveMappingButton", SaveClicked);
            _export = Button("Export editable bundle…", "ExportMappingButton", ExportClicked);
            _starter = Button("Export starter draw.io…", "ExportMappingStarterButton", StarterClicked);
            _review = Button("Review replacements / rollback…", "ReviewMappingRevisionButton", ReviewClicked);
            _delete = Button("Delete saved drawing…", "DeleteMappingRevisionButton", DeleteClicked);
            buttons.Children.Add(_starter); buttons.Children.Add(_import); buttons.Children.Add(_bundled); buttons.Children.Add(_save); buttons.Children.Add(_export); buttons.Children.Add(_delete);
            if (_workspaces is not null) buttons.Children.Add(_review);
            if (_canDiscoverHardware) buttons.Children.Add(_discover);
            heading.Children.Add(buttons);
            _status = new TextBlock { Text = "Start with Export starter draw.io, edit it externally, then open and save it here. Keep label IDs and inputKey metadata intact. Saved revisions are retained; select any version to preview, export or review adoption/rollback.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 10) };
            RegisterName("MappingStatus", _status); heading.Children.Add(_status);
            DockPanel.SetDock(heading, Dock.Top); panel.Children.Add(heading);
            _revisions = new ListBox { DisplayMemberPath = "Title", Width = 250, Margin = new Thickness(0, 0, 12, 0) };
            RegisterName("MappingRevisions", _revisions);
            DockPanel.SetDock(_revisions, Dock.Left); panel.Children.Add(_revisions);
            _preview = new Image { Stretch = System.Windows.Media.Stretch.Uniform };
            panel.Children.Add(new ScrollViewer { Content = _preview, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            Content = panel;
            _revisions.SelectionChanged += (_, _) =>
            {
                if (_busy || _revisions.SelectedItem is not UserMappingRevision revision) return;
                try { Preview(revision); _candidate = null; _status.Text = "Saved drawing: " + revision.Title + ". To use it, select it in your module’s Devices tab and apply it to a compatible device."; }
                catch (Exception ex) { _status.Text = "Unable to preview drawing: " + ex.Message; }
                UpdateButtons();
            };
            _definitions.SelectionChanged += (_, _) => { _candidate = null; _preview.Source = null; _status.Text = "Controller changed. Open a drawing to check that its control labels match before saving."; UpdateButtons(); };
            Loaded += async (_, _) => await ReloadAsync();
            Closing += (_, e) => { if (_busy) e.Cancel = true; };
            UpdateButtons();
        }
        #region BEGIN Public
        /// <summary>Gets whether the owner should open hardware discovery, then reload the drawing choices.</summary>
        public bool DiscoverHardwareRequested { get; private set; }
        #endregion END Public
        #region BEGIN private helpers
        private Button Button(string text, string name, RoutedEventHandler action)
        {
            var button = new Button { Content = text, Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(0, 0, 8, 0) };
            RegisterName(name, button); button.Click += action; return button;
        }
        private async Task ReloadAsync()
        {
            SetBusy(true);
            try { await RefreshRevisionsAsync(); }
            catch (Exception ex) { _status.Text = "Unable to read user mappings: " + ex.Message; }
            finally
            {
                SetBusy(false);
                if (_definitions.Items.Count == 0)
                    _status.Text += "\nNo controllers are available for import. Use Hardware → Discover devices… / Rescan, select your controllers, then Save reviewed inventory. This window reopens with the updated controller list after discovery. Existing module drawings do not always include the native control definitions needed here.";
            }
        }
        private async void PreviewClicked(object sender, RoutedEventArgs e) => await PreviewSourceAsync(false);
        private async Task PreviewSourceAsync(bool bundled)
        {
            if (_definitions.SelectedValue is not HardwareDefinition definition) return;
            var dialog = new OpenFileDialog
            {
                Title = bundled ? "Choose a bundled drawing for the selected controller" : "Choose an editable draw.io hardware drawing",
                Filter = "Draw.io source|*.drawio;*.xml",
                InitialDirectory = bundled ? _bundledDrawingsDirectory ?? "" : ""
            };
            var path = _sourcePicker is not null ? _sourcePicker() : dialog.ShowDialog(this) == true ? dialog.FileName : null;
            if (path is null) return;
            _candidate = null; SetBusy(true);
            try
            {
                IReadOnlyDictionary<string, string>? catalogInputs = null;
                _bundledInputs.TryGetValue(Path.GetFileName(path), out catalogInputs);
                var revision = await Task.Run(() => DrawioMappingFileImport.Load(path, definition, ApplicationVersion.Label, catalogInputs, _bundledInputs.Values));
                Preview(revision);
                _candidate = revision;
                _status.Text = "Verified preview: " + revision.MappedInputs.Count + " mapped / " + revision.Controls.Count +
                    " actual controls. Unmapped controls remain listed and get readable continuation kneeboards. Click Save drawing to keep it, then apply it in your module’s Devices tab.";
            }
            catch (Exception ex) { _preview.Source = null; _status.Text = "Drawing could not be imported: " + ex.Message; }
            finally { SetBusy(false); }
        }
        private async void SaveClicked(object sender, RoutedEventArgs e)
        {
            if (_candidate is not { } revision) return;
            SetBusy(true);
            try
            {
                await _library.SaveAsync(revision);
                _candidate = null;
                await RefreshRevisionsAsync();
                _status.Text = "Saved drawing: " + revision.Title + ". Upload only: no modules changed. Choose Review replacements / rollback to replace selected devices across modules, or apply it from a module’s Devices tab.";
            }
            catch (Exception ex) { _status.Text = "Unable to save drawing: " + ex.Message; }
            finally { SetBusy(false); }
        }
        private async void DeleteClicked(object sender, RoutedEventArgs e)
        {
            if (_revisions.SelectedItem is not UserMappingRevision revision ||
                !_libraryRevisionIds.Contains(revision.Id)) return;
            var answer = ThemeMessageBox.Show(this,
                "Permanently delete the saved drawing revision “" + revision.Title +
                "”? This removes it from your saved drawing library. Existing module pins and bundled drawings are not changed.",
                "Delete saved drawing", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (answer != MessageBoxResult.Yes) return;
            SetBusy(true);
            try
            {
                await _library.DeleteAsync(revision.Id);
                _candidate = null;
                _preview.Source = null;
                await RefreshRevisionsAsync();
                _status.Text = "Deleted saved drawing: " + revision.Title + ". Existing module pins were left unchanged.";
            }
            catch (Exception ex) { _status.Text = "Unable to delete saved drawing: " + ex.Message; }
            finally { SetBusy(false); }
        }
        private async Task RefreshRevisionsAsync()
        {
            var saved = await _library.ListAsync();
            _libraryRevisionIds = saved.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
            _revisions.ItemsSource = saved.Concat(_modulePins).DistinctBy(x => x.Id).OrderBy(x => x.Id, StringComparer.Ordinal).ToArray();
            UpdateButtons();
        }
        private void ReviewClicked(object sender, RoutedEventArgs e)
        {
            if (_workspaces is null || _revisions.SelectedItem is not UserMappingRevision revision) return;
            new MappingRevisionReviewWindow(revision, _workspaces) { Owner = this }.ShowDialog();
        }
        private void StarterClicked(object sender, RoutedEventArgs e)
        {
            if (_definitions.SelectedValue is not HardwareDefinition definition) return;
            var dialog = new SaveFileDialog { Title = "Export editable controller starter", Filter = "Draw.io source|*.drawio", FileName = "controller-starter.drawio" };
            var path = _starterPicker is not null ? _starterPicker() : dialog.ShowDialog(this) == true ? dialog.FileName : null;
            if (path is null) return;
            try
            {
                var source = DrawioMappingStarter.Export(definition);
                using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
                using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false));
                writer.Write(source);
                _status.Text = "Exported starter. Edit layout and add images in draw.io; retain label IDs and inputKey attributes. Open the edited file here to preview and save a revision.";
            }
            catch (Exception ex) { _status.Text = "Unable to export starter: " + ex.Message; }
        }
        private void ExportClicked(object sender, RoutedEventArgs e)
        {
            if (_revisions.SelectedItem is not UserMappingRevision revision) return;
            var dialog = new SaveFileDialog { Title = "Export editable mapping bundle", Filter = "Mapping bundle|*.ipia-mapping.zip", FileName = "mapping.ipia-mapping.zip" };
            var path = _exportPicker is not null ? _exportPicker() : dialog.ShowDialog(this) == true ? dialog.FileName : null;
            if (path is null) return;
            try
            {
                var bytes = UserMappingBundle.Export(revision);
                using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
                stream.Write(bytes);
                _status.Text = "Exported drawing and images. Extract the bundle, edit mapping.drawio, then open and save it here as a new drawing version.";
            }
            catch (Exception ex) { _status.Text = "Unable to export bundle: " + ex.Message; }
        }
        private void Preview(UserMappingRevision revision)
        {
            UserMappingImageVerifier.Validate(revision);
            var bytes = CanonicalSvgRenderer.RenderComplete(revision.Svg, revision.Width, revision.Height);
            using var stream = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze();
            _preview.Source = bitmap;
        }
        private void SetBusy(bool busy) { _busy = busy; UpdateButtons(); }
        private void UpdateButtons()
        {
            _definitions.IsEnabled = _revisions.IsEnabled = !_busy;
            _import.IsEnabled = !_busy && _definitions.SelectedValue is HardwareDefinition;
            _bundled.IsEnabled = _import.IsEnabled && Directory.Exists(_bundledDrawingsDirectory);
            _discover.IsEnabled = !_busy && _canDiscoverHardware;
            _save.IsEnabled = !_busy && _candidate is not null;
            _export.IsEnabled = !_busy && _revisions.SelectedItem is UserMappingRevision;
            _delete.IsEnabled = !_busy && _revisions.SelectedItem is UserMappingRevision selected &&
                _libraryRevisionIds.Contains(selected.Id);
            _starter.IsEnabled = !_busy && _definitions.SelectedValue is HardwareDefinition;
            _review.IsEnabled = _export.IsEnabled && _workspaces is IEditorWorkspaceUpdateRepository;
        }
        #endregion END private helpers
    }
}

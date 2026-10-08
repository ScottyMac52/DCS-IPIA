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
        public UserMappingsWindow(IUserMappingLibrary library, IReadOnlyList<HardwareDefinition> definitions,
            Func<string?>? sourcePicker = null, Func<string?>? exportPicker = null, IReadOnlyDictionary<string, string>? targetNames = null, IReadOnlyList<UserMappingRevision>? modulePins = null,
            string? bundledDrawingsDirectory = null, bool canDiscoverHardware = false,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? bundledInputs = null)
        {
            _library = library;
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
            buttons.Children.Add(_import); buttons.Children.Add(_bundled); buttons.Children.Add(_save); buttons.Children.Add(_export);
            if (_canDiscoverHardware) buttons.Children.Add(_discover);
            heading.Children.Add(buttons);
            _status = new TextBlock { Text = "Drawings must use IPIA control labels (label-* with a DCS input key such as JOY_BTN1). Use a compatible bundled drawing as a starting point; ordinary draw.io files may need preparation. Controls without drawing labels remain listed.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 10) };
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
            var button = new Button { Content = text, Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 0, 8, 0) };
            RegisterName(name, button); button.Click += action; return button;
        }
        private async Task ReloadAsync()
        {
            SetBusy(true);
            try { _revisions.ItemsSource = (await _library.ListAsync()).Concat(_modulePins).DistinctBy(x => x.Id).OrderBy(x => x.Id, StringComparer.Ordinal).ToArray(); }
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
                var revision = await Task.Run(() => DrawioMappingFileImport.Load(path, definition, ApplicationVersion.Label, catalogInputs));
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
                _revisions.ItemsSource = (await _library.ListAsync()).Concat(_modulePins).DistinctBy(x => x.Id).OrderBy(x => x.Id, StringComparer.Ordinal).ToArray();
                _status.Text = "Saved drawing: " + revision.Title + ". Next: Open module → Devices → select the device and this drawing → Apply drawing to selected device → Save.";
            }
            catch (Exception ex) { _status.Text = "Unable to save drawing: " + ex.Message; }
            finally { SetBusy(false); }
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
        }
        #endregion END private helpers
    }
}

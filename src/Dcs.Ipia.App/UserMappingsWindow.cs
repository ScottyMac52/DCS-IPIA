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
        private readonly Func<string?>? _sourcePicker;
        private readonly Func<string?>? _exportPicker;
        private readonly ComboBox _definitions;
        private readonly ListBox _revisions;
        private readonly Image _preview;
        private readonly TextBlock _status;
        private readonly Button _import;
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
        public UserMappingsWindow(IUserMappingLibrary library, IReadOnlyList<HardwareDefinition> definitions,
            Func<string?>? sourcePicker = null, Func<string?>? exportPicker = null, IReadOnlyDictionary<string, string>? targetNames = null)
        {
            _library = library;
            _sourcePicker = sourcePicker; _exportPicker = exportPicker;
            Title = "User draw.io mapping library";
            Width = 1000; Height = 760; MinWidth = 700; MinHeight = 500;
            SetResourceReference(BackgroundProperty, "WindowBackgroundBrush");
            SetResourceReference(ForegroundProperty, "PrimaryTextBrush");
            NameScope.SetNameScope(this, new NameScope());
            var panel = new DockPanel { Margin = new Thickness(18) };
            var heading = new StackPanel();
            heading.Children.Add(new TextBlock { Text = "Select a target definition, browse a draw.io file, review the preview, then save a new immutable revision.", TextWrapping = TextWrapping.Wrap });
            heading.Children.Add(new TextBlock { Text = "Library import changes no modules. In a module's Devices tab, explicitly stage a revision on a compatible profile, then Save or Discard.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 12) });
            heading.Children.Add(new TextBlock { Text = "Target definition (matching native controls)" });
            _definitions = new ComboBox { ItemsSource = definitions.Select(x => new KeyValuePair<string, HardwareDefinition>(
                (targetNames?.GetValueOrDefault(x.Id) ?? "Native definition") + " · " + x.Id[..12], x)).ToArray(), DisplayMemberPath = "Key", SelectedValuePath = "Value", SelectedIndex = definitions.Count > 0 ? 0 : -1, Margin = new Thickness(0, 4, 0, 10) };
            RegisterName("MappingDefinitions", _definitions); heading.Children.Add(_definitions);
            var buttons = new WrapPanel();
            _import = Button("Preview draw.io import…", "PreviewMappingButton", PreviewClicked);
            _save = Button("Save new revision", "SaveMappingButton", SaveClicked);
            _export = Button("Export editable bundle…", "ExportMappingButton", ExportClicked);
            buttons.Children.Add(_import); buttons.Children.Add(_save); buttons.Children.Add(_export);
            heading.Children.Add(buttons);
            _status = new TextBlock { Text = "Supported: one native/compressed diagram, stable label-* mappings, raster images and callout connectors. Unmapped controls retain a generic listing.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 10) };
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
                try { Preview(revision); _candidate = null; _status.Text = "Saved revision " + revision.Id + " · " + revision.ApplicationVersion; }
                catch (Exception ex) { _status.Text = "Unable to preview revision: " + ex.Message; }
                UpdateButtons();
            };
            _definitions.SelectionChanged += (_, _) => { _candidate = null; UpdateButtons(); };
            Loaded += async (_, _) => await ReloadAsync();
            Closing += (_, e) => { if (_busy) e.Cancel = true; };
            UpdateButtons();
        }
        #region BEGIN Public
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
            try { _revisions.ItemsSource = await _library.ListAsync(); }
            catch (Exception ex) { _status.Text = "Unable to read user mappings: " + ex.Message; }
            finally { SetBusy(false); }
        }
        private async void PreviewClicked(object sender, RoutedEventArgs e)
        {
            if (_definitions.SelectedValue is not HardwareDefinition definition) return;
            var dialog = new OpenFileDialog { Title = "Import editable draw.io mapping", Filter = "Draw.io source|*.drawio;*.xml" };
            var path = _sourcePicker is not null ? _sourcePicker() : dialog.ShowDialog(this) == true ? dialog.FileName : null;
            if (path is null) return;
            _candidate = null; SetBusy(true);
            try
            {
                var revision = await Task.Run(() => DrawioMappingFileImport.Load(path, definition, ApplicationVersion.Label));
                Preview(revision);
                _candidate = revision;
                _status.Text = "Verified preview: " + revision.MappedInputs.Count + " mapped / " + revision.Controls.Count +
                    " actual controls. Unmapped controls remain listed and get readable continuation kneeboards. Save to publish this revision.";
            }
            catch (Exception ex) { _preview.Source = null; _status.Text = "Import rejected before publication: " + ex.Message; }
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
                _revisions.ItemsSource = await _library.ListAsync();
                _status.Text = "Saved immutable revision " + revision.Id + ". No modules were changed.";
            }
            catch (Exception ex) { _status.Text = "Unable to save revision: " + ex.Message; }
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
                _status.Text = "Exported editable source and captured images. Extract, edit mapping.drawio, then import to create a new revision.";
            }
            catch (Exception ex) { _status.Text = "Unable to export bundle: " + ex.Message; }
        }
        private void Preview(UserMappingRevision revision)
        {
            DrawioMappingImporter.Validate(revision);
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
            _save.IsEnabled = !_busy && _candidate is not null;
            _export.IsEnabled = !_busy && _revisions.SelectedItem is UserMappingRevision;
        }
        #endregion END private helpers
    }
}

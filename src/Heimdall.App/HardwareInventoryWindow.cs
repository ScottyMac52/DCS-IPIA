using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Heimdall.Application;
using Heimdall.Dcs;

namespace Heimdall.App
{
    /// <summary>Reviews discovery and saved hardware without requiring or changing a module.</summary>
    public sealed class HardwareInventoryWindow : Window, IDisposable
    {
        private readonly HardwareInventoryManager _manager;
        private readonly SetupManager? _setup;
        private readonly TextBox _destination = new();
        private readonly CheckBox _offline = new() { Content = "Finish _offline without a destination or selected controller", Margin = new Thickness(0, 6, 0, 6) };
        private readonly CheckBox _neutral = new() { Content = "Use _neutral / empty defaults for new modules (existing modules and shared revisions are preserved)", IsChecked = true };
        private readonly Button _skip = new() { Content = "_Skip for now", Margin = new Thickness(8, 0, 0, 0), IsCancel = true };
        private readonly bool _scanOnOpen;
        private readonly Func<CancellationToken, Task<IReadOnlyDictionary<string, string>>> _catalog;
        private IReadOnlyDictionary<string, string> _sources = new Dictionary<string, string>();
        private HardwareInventoryRow[] _rows = [];
        private readonly DataGrid _grid = new() { AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false };
        private readonly TextBlock _details = new() { TextWrapping = TextWrapping.Wrap };
        private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) };
        private readonly Button _rescan = new() { Content = "Rescan", Margin = new Thickness(0, 0, 8, 0) };
        private readonly Button _save = new() { Content = "Save reviewed inventory", Margin = new Thickness(0, 0, 8, 0) };
        private readonly Button _cancel = new() { Content = "Cancel _operation", IsEnabled = false };
        private CancellationTokenSource? _scan;
        private bool _busy;
        private bool _dirty;
        /// <summary>Creates the independent hardware-management surface.</summary>
        /// <param name="manager">Profile-scoped inventory use cases.</param>
        /// <param name="scanOnOpen">Whether to begin discovery on opening.</param>
        /// <param name="catalog">Optional pinned catalog test adapter.</param>
        /// <param name="setup">Optional transactional onboarding use case.</param>
        /// <param name="destination">Current Saved Games preference for review.</param>
        public HardwareInventoryWindow(HardwareInventoryManager manager, bool scanOnOpen,
            Func<CancellationToken, Task<IReadOnlyDictionary<string, string>>>? catalog = null, SetupManager? setup = null, string destination = "")
        {
            _manager = manager;
            _setup = setup;
            _destination.Text = destination;
            _scanOnOpen = scanOnOpen;
            _catalog = catalog ?? (ct => Task.Run(() => ModuleWorkspaceLoader.LoadHardwareAsync(BundledSharedCatalog.Root, ct), ct));
            Title = setup is null ? "HEIMDALL — User hardware inventory" : "HEIMDALL — Hardware setup";
            if (setup is not null) { _save.Content = "_Confirm setup"; _save.IsDefault = true; }
            Width = 1200; Height = 740; MinWidth = 800; MinHeight = 500;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            SetResourceReference(BackgroundProperty, "WindowBackgroundBrush");
            SetResourceReference(ForegroundProperty, "PrimaryTextBrush");
            NameScope.SetNameScope(this, new NameScope());
            RegisterName("HardwareInventoryGrid", _grid);
            RegisterName("HardwareInventoryStatus", _status);
            RegisterName("HardwareInventorySave", _save);
            RegisterName("HardwareInventoryRescan", _rescan);
            RegisterName("SetupDestination", _destination);
            RegisterName("SetupOffline", _offline);
            RegisterName("SetupNeutral", _neutral);
            RegisterName("SetupSkip", _skip);
            var layout = new Grid { Margin = new Thickness(20) };
            foreach (var height in new[] { GridLength.Auto, GridLength.Auto, GridLength.Auto, new GridLength(3, GridUnitType.Star), new GridLength(2, GridUnitType.Star) })
                layout.RowDefinitions.Add(new RowDefinition { Height = height });
            var toolbar = new StackPanel { Orientation = Orientation.Horizontal };
            toolbar.Children.Add(_rescan); toolbar.Children.Add(_save); toolbar.Children.Add(_cancel);
            if (setup is not null)
            {
                layout.Children.Add(SetupHeader());
                toolbar.Children.Add(_skip);
                _skip.Click += (_, _) => { if (!_busy) { _dirty = false; Close(); } };
            }
            Grid.SetRow(toolbar, 1); layout.Children.Add(toolbar);
            Grid.SetRow(_status, 2); layout.Children.Add(_status);
            Grid.SetRow(_grid, 3); layout.Children.Add(_grid);
            var scroll = new ScrollViewer { Content = _details, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 12, 0, 0) };
            Grid.SetRow(scroll, 4); layout.Children.Add(scroll);
            _grid.Columns.Add(new DataGridCheckBoxColumn { Header = "Selected", Binding = new Binding(nameof(HardwareInventoryRow.Selected)) });
            Text("Instance label", nameof(HardwareInventoryRow.Label), false);
            Text("Role", nameof(HardwareInventoryRow.Role), false);
            Text("Native name", nameof(HardwareInventoryRow.NativeName));
            Text("Physical GUID", nameof(HardwareInventoryRow.Identity));
            Text("Status", nameof(HardwareInventoryRow.Status));
            Text("Controls", nameof(HardwareInventoryRow.Controls));
            Text("Mapping", nameof(HardwareInventoryRow.Mapping));
            if (setup is not null)
            {
                var choice = new FrameworkElementFactory(typeof(ComboBox));
                choice.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(HardwareInventoryRow.MappingChoices)));
                choice.SetBinding(System.Windows.Controls.Primitives.Selector.SelectedItemProperty,
                    new Binding(nameof(HardwareInventoryRow.MappingChoice)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged, NotifyOnSourceUpdated = true });
                _grid.Columns.Add(new DataGridTemplateColumn { Header = "Preferred mapping", CellTemplate = new DataTemplate { VisualTree = choice } });
            }
            _grid.Columns.Add(new DataGridCheckBoxColumn { Header = "Adopt changed controls", Binding = new Binding(nameof(HardwareInventoryRow.AdoptChangedCapabilities)) });
            _grid.SelectionChanged += (_, _) => _details.Text = (_grid.SelectedItem as HardwareInventoryRow)?.Details ?? "";
            _grid.CellEditEnding += (_, _) => _dirty = true;
            _grid.SourceUpdated += (_, _) => _dirty = true;
            _rescan.Click += async (_, _) => { if (DiscardEdits()) await ScanAsync(); };
            _save.Click += async (_, _) => await SaveAsync();
            _cancel.Click += (_, _) => _scan?.Cancel();
            Content = layout;
            Loaded += async (_, _) => await InitializeAsync();
            Closing += (_, e) =>
            {
                if (_busy) { _scan?.Cancel(); e.Cancel = true; return; }
                if (!DiscardEdits()) e.Cancel = true;
            };
            Closed += (_, _) => Dispose();
        }
        #region BEGIN Public
        /// <summary>Gets whether confirmed setup committed successfully.</summary>
        public bool SetupConfirmed { get; private set; }
        /// <summary>Releases the scan cancellation source after the window closes.</summary>
        public void Dispose()
        {
            _scan?.Dispose();
            _scan = null;
            GC.SuppressFinalize(this);
        }
        #endregion END Public
        #region BEGIN private helpers
        private StackPanel SetupHeader()
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
            panel.Children.Add(new TextBlock { Text = "Welcome. Review your destination, select usable controllers, and confirm their labels and mapping. Scanning writes nothing. Skip or cancel to continue with New, Import or Open Module; resume setup from Settings.", TextWrapping = TextWrapping.Wrap });
            var browse = new Button { Content = "_Browse…", Margin = new Thickness(8, 0, 0, 0) };
            browse.Click += (_, _) => { var picker = new ThemeBrowseDialog { SelectFolder = true, Title = "Select Saved Games DCS folder" }; if (picker.ShowDialog(this) == true) _destination.Text = picker.FolderName; };
            var folder = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
            folder.Children.Add(new Label { Content = "Saved _Games DCS folder", Target = _destination });
            DockPanel.SetDock(browse, Dock.Right); folder.Children.Add(browse); folder.Children.Add(_destination);
            panel.Children.Add(folder); panel.Children.Add(_offline); panel.Children.Add(_neutral);
            return panel;
        }
        private void Text(string title, string property, bool readOnly = true) =>
            _grid.Columns.Add(new DataGridTextColumn { Header = title, Binding = new Binding(property), IsReadOnly = readOnly });
        private bool DiscardEdits() => !_dirty || ThemeMessageBox.Show(this,
            "Discard the unsaved inventory selection and label edits?", "Hardware inventory",
            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
        private async Task InitializeAsync()
        {
            Busy(true);
            try { ShowRows(await _manager.LoadAsync()); _status.Text = "Saved units retained offline. Rescan to observe connections; scans do not adopt devices."; }
            catch (Exception ex) { _status.Text = "Unable to load inventory: " + ex.Message; }
            finally { Busy(false); }
            if (_scanOnOpen) await ScanAsync();
        }
        private async Task ScanAsync()
        {
            if (_busy) return;
            _scan?.Dispose();
            _scan = new CancellationTokenSource();
            Busy(true);
            _cancel.IsEnabled = true;
            try
            {
                _status.Text = "Enumerating attached DirectInput controls…";
                var rows = await _manager.ScanAsync(_scan.Token);
                var warning = "";
                try { _sources = await _catalog(_scan.Token); }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { _sources = new Dictionary<string, string>(); warning = " Bundled mapping unavailable: " + ex.Message; }
                _scan.Token.ThrowIfCancellationRequested();
                ShowRows(rows);
                _dirty = false;
                _status.Text = (rows.Count == 0 ? "No controllers found. Keyboard support remains available in module editing." :
                    "Review controls, select units and edit labels/roles, then save. Changed definitions remain pinned unless explicitly adopted.") + warning;
            }
            catch (OperationCanceledException) { _status.Text = "Scan cancelled. Previous review rows and saved inventory are unchanged."; }
            catch (Exception ex) { _status.Text = "Discovery failed; saved inventory was not changed: " + ex.Message; }
            finally { Busy(false); _cancel.IsEnabled = false; }
        }
        private async Task SaveAsync()
        {
            if (_busy) return;
            _grid.CommitEdit(DataGridEditingUnit.Cell, true);
            _grid.CommitEdit(DataGridEditingUnit.Row, true);
            _scan?.Dispose();
            _scan = new CancellationTokenSource();
            Busy(true);
            _cancel.IsEnabled = true;
            try
            {
                if (!EditionAccess.Policy.HasPremium)
                    EditionAccess.Policy.RequireInventory(await _manager.ReadSnapshotAsync(_scan.Token), _rows.Select(row => row.Edit()).ToArray());
                if (_setup is not null)
                {
                    if (_neutral.IsChecked != true) throw new InvalidOperationException("Review and confirm neutral defaults. Deliberate shared-revision choices remain available when creating modules.");
                    await _setup.ConfirmAsync(_rows.Select(x => x.Edit()).ToArray(), _destination.Text, _offline.IsChecked == true, _scan.Token);
                    SetupConfirmed = true;
                    _dirty = false;
                    Busy(false);
                    Close();
                    return;
                }
                var observations = _rows.Select(x => x.Candidate.Observed).Where(x => x is not null).Cast<ControllerObservation>().ToArray();
                var snapshot = await _manager.SaveAsync(_rows.Select(x => x.Edit()).ToArray(), _scan.Token);
                ShowRows(HardwareInventoryManager.Merge(snapshot, observations));
                _dirty = false;
                _status.Text = "Reviewed inventory saved. Module assignments and DCS files were not changed.";
            }
            catch (OperationCanceledException) { _status.Text = "Confirmation cancelled. Saved inventory and setup state are unchanged."; }
            catch (Exception ex) { _status.Text = (_setup is null ? "Inventory" : "Setup") + " was not saved: " + ex.Message; }
            finally { Busy(false); _cancel.IsEnabled = false; }
        }
        private void ShowRows(IReadOnlyList<HardwareInventoryCandidate> rows)
        {
            _rows = rows.Select(x => new HardwareInventoryRow(x, _sources, _setup is not null)).ToArray();
            _grid.ItemsSource = _rows;
            _grid.SelectedIndex = _rows.Length > 0 ? 0 : -1;
        }
        private void Busy(bool value)
        {
            _busy = value;
            _grid.IsEnabled = !value;
            _destination.IsEnabled = !value;
            _offline.IsEnabled = !value;
            _neutral.IsEnabled = !value;
            _skip.IsEnabled = !value;
            _rescan.IsEnabled = !value;
            _save.IsEnabled = !value;
        }
        #endregion END private helpers
    }
}

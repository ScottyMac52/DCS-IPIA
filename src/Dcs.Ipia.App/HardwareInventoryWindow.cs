using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;

namespace DcsIpia.Presentation
{
    /// <summary>Reviews discovery and saved hardware without requiring or changing a module.</summary>
    public sealed class HardwareInventoryWindow : Window, IDisposable
    {
        private readonly HardwareInventoryManager _manager;
        private readonly bool _scanOnOpen;
        private readonly Func<CancellationToken, Task<IReadOnlyDictionary<string, string>>> _catalog;
        private IReadOnlyDictionary<string, string> _sources = new Dictionary<string, string>();
        private HardwareInventoryRow[] _rows = [];
        private readonly DataGrid _grid = new() { AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false };
        private readonly TextBlock _details = new() { TextWrapping = TextWrapping.Wrap };
        private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) };
        private readonly Button _rescan = new() { Content = "Rescan", Margin = new Thickness(0, 0, 8, 0) };
        private readonly Button _save = new() { Content = "Save reviewed inventory", Margin = new Thickness(0, 0, 8, 0) };
        private readonly Button _cancel = new() { Content = "Cancel scan", IsEnabled = false };
        private CancellationTokenSource? _scan;
        private bool _busy;
        private bool _dirty;
        /// <summary>Creates the independent hardware-management surface.</summary>
        /// <param name="manager">Profile-scoped inventory use cases.</param>
        /// <param name="scanOnOpen">Whether to begin discovery on opening.</param>
        /// <param name="catalog">Optional pinned catalog test adapter.</param>
        public HardwareInventoryWindow(HardwareInventoryManager manager, bool scanOnOpen,
            Func<CancellationToken, Task<IReadOnlyDictionary<string, string>>>? catalog = null)
        {
            _manager = manager;
            _scanOnOpen = scanOnOpen;
            _catalog = catalog ?? (ct => Task.Run(() => ModuleWorkspaceLoader.LoadHardwareAsync(BundledSharedCatalog.Root, ct), ct));
            Title = "DCS-IPIA — User hardware inventory";
            Width = 1200; Height = 740; MinWidth = 800; MinHeight = 500;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            SetResourceReference(BackgroundProperty, "WindowBackgroundBrush");
            SetResourceReference(ForegroundProperty, "PrimaryTextBrush");
            NameScope.SetNameScope(this, new NameScope());
            RegisterName("HardwareInventoryGrid", _grid);
            RegisterName("HardwareInventoryStatus", _status);
            RegisterName("HardwareInventorySave", _save);
            RegisterName("HardwareInventoryRescan", _rescan);
            var layout = new Grid { Margin = new Thickness(20) };
            foreach (var height in new[] { GridLength.Auto, GridLength.Auto, new GridLength(3, GridUnitType.Star), new GridLength(2, GridUnitType.Star) })
                layout.RowDefinitions.Add(new RowDefinition { Height = height });
            var toolbar = new StackPanel { Orientation = Orientation.Horizontal };
            toolbar.Children.Add(_rescan); toolbar.Children.Add(_save); toolbar.Children.Add(_cancel);
            layout.Children.Add(toolbar);
            Grid.SetRow(_status, 1); layout.Children.Add(_status);
            Grid.SetRow(_grid, 2); layout.Children.Add(_grid);
            var scroll = new ScrollViewer { Content = _details, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 12, 0, 0) };
            Grid.SetRow(scroll, 3); layout.Children.Add(scroll);
            _grid.Columns.Add(new DataGridCheckBoxColumn { Header = "Selected", Binding = new Binding(nameof(HardwareInventoryRow.Selected)) });
            Text("Instance label", nameof(HardwareInventoryRow.Label), false);
            Text("Role", nameof(HardwareInventoryRow.Role), false);
            Text("Native name", nameof(HardwareInventoryRow.NativeName));
            Text("Physical GUID", nameof(HardwareInventoryRow.Identity));
            Text("Status", nameof(HardwareInventoryRow.Status));
            Text("Controls", nameof(HardwareInventoryRow.Controls));
            Text("Mapping", nameof(HardwareInventoryRow.Mapping));
            _grid.Columns.Add(new DataGridCheckBoxColumn { Header = "Adopt changed controls", Binding = new Binding(nameof(HardwareInventoryRow.AdoptChangedCapabilities)) });
            _grid.SelectionChanged += (_, _) => _details.Text = (_grid.SelectedItem as HardwareInventoryRow)?.Details ?? "";
            _grid.CellEditEnding += (_, _) => _dirty = true;
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
        /// <summary>Releases the scan cancellation source after the window closes.</summary>
        public void Dispose()
        {
            _scan?.Dispose();
            _scan = null;
            GC.SuppressFinalize(this);
        }
        #endregion END Public
        #region BEGIN private helpers
        private void Text(string title, string property, bool readOnly = true) =>
            _grid.Columns.Add(new DataGridTextColumn { Header = title, Binding = new Binding(property), IsReadOnly = readOnly });
        private bool DiscardEdits() => !_dirty || MessageBox.Show(this,
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
            Busy(true);
            try
            {
                var observations = _rows.Select(x => x.Candidate.Observed).Where(x => x is not null).Cast<ControllerObservation>().ToArray();
                var snapshot = await _manager.SaveAsync(_rows.Select(x => x.Edit()).ToArray());
                ShowRows(HardwareInventoryManager.Merge(snapshot, observations));
                _dirty = false;
                _status.Text = "Reviewed inventory saved. Module assignments and DCS files were not changed.";
            }
            catch (Exception ex) { _status.Text = "Inventory was not saved: " + ex.Message; }
            finally { Busy(false); }
        }
        private void ShowRows(IReadOnlyList<HardwareInventoryCandidate> rows)
        {
            _rows = rows.Select(x => new HardwareInventoryRow(x, _sources)).ToArray();
            _grid.ItemsSource = _rows;
            _grid.SelectedIndex = _rows.Length > 0 ? 0 : -1;
        }
        private void Busy(bool value)
        {
            _busy = value;
            _grid.IsEnabled = !value;
            _rescan.IsEnabled = !value;
            _save.IsEnabled = !value;
        }
        #endregion END private helpers
    }
}

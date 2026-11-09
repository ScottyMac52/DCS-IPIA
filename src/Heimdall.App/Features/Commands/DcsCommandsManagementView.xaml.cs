using Heimdall.App.Games.Dcs;
using Heimdall.Application;
using Heimdall.Application.Games;
using CommandType = Heimdall.Domain.MultiSim.CommandType;
using Heimdall.Application.Interfaces;
using Heimdall.Dcs;
using Heimdall.App;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

namespace Heimdall.App.Features.Commands
{
    /// <summary>Browses, imports and edits the module command catalog.</summary>
    public partial class DcsCommandsManagementView : UserControl, IDisposable
    {

        private UIElement? _previewHost;
        private IDcsModuleWorkspaceContext? _context;
        private DcsAssignmentBrowser? _browser;
        private IReadOnlyList<DcsCommandPaletteEntry>? _published;
        private DcsCommandPaletteEntry? _selected => _browser?.Commands.FirstOrDefault(x => x.Id == _context?.State.Selection.CommandId);
        private Guid? _assignmentDeviceId;
        private DcsCommandPaletteEntry? _displayedCommand;
        private bool _updatingFilters;
        private bool _refreshing;
        private bool _hostOwnsLifetime;
        private bool _featureActive;
        private AssignmentLayer? _assignmentScope;
        private static readonly string[] CatalogActions = ["Add", "Update", "Remove", "Import JSON…", "Import HTML…", "Clear all"];
        private Point? _dragStart;
        private TitleAdorner? _adorner;
        private AdornerLayer? _layer;

        #region BEGIN Public
        /// <summary>Gets the shared browser contract for cached feature lifetime ownership.</summary>
        internal IGameAssignmentBrowser Browser => _browser ?? throw new InvalidOperationException("Initialize the command browser.");
        /// <summary>Releases native browser state subscriptions when the workspace closes.</summary>
        public void Dispose() { _browser?.Dispose(); _browser = null; GC.SuppressFinalize(this); }

        /// <summary>Occurs when the command selection changes.</summary>
        public event EventHandler<DcsCommandPaletteEntry?>? SelectedCommandChanged;
        /// <summary>Gets the currently selected command, if any.</summary>
        public DcsCommandPaletteEntry? SelectedCommand => _selected;

        public DcsCommandsManagementView()
        {
            InitializeComponent();
            Loaded += (_, _) => { if (!_hostOwnsLifetime) _browser?.SetActive(true); };
            Unloaded += (_, _) => { if (!_hostOwnsLifetime) _browser?.SetActive(false); };
            // Replace the ordinary XAML subscription with one that also
            // receives feedback already handled by the DataGrid or its children.
            GiveFeedbackEventHandler feedback = CommandGiveFeedback;
            Commands.RemoveHandler(DragDrop.GiveFeedbackEvent, feedback);
            Commands.AddHandler(DragDrop.GiveFeedbackEvent, feedback, true);
        }

        /// <summary>Connects this view to the shared workspace and refreshes its displayed state.</summary>
        public void Initialize(IDcsModuleWorkspaceContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            if (ReferenceEquals(_context, context)) return;
            _context = context;
            _browser?.Dispose();
            _browser = new(context, () => context.Draft?.Snapshot ?? context.Working?.Assignments ?? []);
            _browser.SetActive(_hostOwnsLifetime ? _featureActive : IsLoaded);
            _browser.ProjectionChanged += (_, _) => PublishBrowser();
            Refresh();
        }
        /// <summary>Selects the assignment palette scope while keeping UI Layer catalog authoring read-only.</summary>
        public void UseAssignmentPalette(AssignmentLayer scope)
        {
            if (_assignmentScope == scope) return;
            _assignmentScope = scope;
            CatalogEditor.IsExpanded = false;
            CatalogEditor.Visibility = Visibility.Collapsed;
            CatalogActionsBar.IsFeatureActions = false;
            Commands.Columns[1].Visibility = Visibility.Collapsed;
            CatalogFields.IsEnabled = scope == AssignmentLayer.Module;
            foreach (var caption in CatalogActions)
                if (ActionBar.FindButton(this, caption) is { } button) button.IsEnabled = scope == AssignmentLayer.Module;
            Refresh();
        }
        /// <summary>Limits assignment ownership filters to the controller currently selected in the workspace rail.</summary>
        public void UseAssignmentController(Guid? profileId)
        {
            if (_assignmentDeviceId == profileId) return;
            _assignmentDeviceId = profileId;
            Refresh();
        }
        /// <summary>Accepts synchronous selection ownership from the feature host instead of delayed WPF load events.</summary>
        internal void SetFeatureActive(bool active)
        {
            _hostOwnsLifetime = true; _featureActive = active; _browser?.SetActive(active);
        }
        #endregion END Public

        #region BEGIN private helpers
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out NativeScreenPoint point);

        private void Refresh()
        {
            if (_refreshing) return;
            _refreshing = true;
            try { RefreshCommands(); }
            finally { _refreshing = false; }
        }


        private void RefreshCommands()
        {
            if (_browser is null || CommandSearch is null || CommandCategoryFilter is null || CommandTypeFilter is null || CommandStateFilter is null) return;
            _browser.Query(new(_assignmentScope ?? AssignmentLayer.Module, _assignmentDeviceId,
                CommandCategoryFilter.SelectedItem as string is { } category && category != "All" ? category : null,
                CommandTypeFilter.SelectedIndex switch { 1 => CommandType.Button, 2 => CommandType.Axis, _ => null },
                (DcsCommandAssignmentStatus)Math.Max(0, CommandStateFilter.SelectedIndex)), CommandSearch.Text);
            PublishBrowser();
            var normalized = CommandCategoryFilter.SelectedItem as string is { } current && current != "All" ? current : null;
            if (_browser.Filter.Category != normalized) _browser.SetFilter(_browser.Filter with { Category = normalized });
        }
        private void PublishBrowser()
        {
            if (_browser is null) return;
            var previous = _refreshing; _refreshing = true;
            try
            {
                var categories = _browser.Categories.Prepend("All").ToArray();
                var selectedCategory = CommandCategoryFilter.SelectedItem as string;
                _updatingFilters = true;
                if (!(CommandCategoryFilter.ItemsSource as IEnumerable<string> ?? []).SequenceEqual(categories))
                {
                    CommandCategoryFilter.ItemsSource = categories;
                    CommandCategoryFilter.SelectedItem = selectedCategory is not null && categories.Contains(selectedCategory) ? selectedCategory : "All";
                }
                _updatingFilters = false;
                if (!ReferenceEquals(_published, _browser.Commands))
                {
                    _published = _browser.Commands;
                    Commands.ItemsSource = _browser.Commands.Select(x => new DcsBrowserRow(x, _browser.BoundCount(x.Id))).ToArray();
                }
                Commands.SelectedItem = Commands.Items.Cast<DcsBrowserRow>().FirstOrDefault(x => x.Id == _context?.State.Selection.CommandId);
                StatusText.Text = $"{_browser.Commands.Count} of {_browser.PaletteCount} {(_assignmentScope == AssignmentLayer.UiLayer ? "UI Layer" : "module")} commands";
                if (_displayedCommand != _selected)
                {
                    DisplaySelectedCommand();
                }
            }
            finally { _updatingFilters = false; _refreshing = previous; }
        }

        private void FiltersChanged(object sender, EventArgs e) { if (!_updatingFilters) Refresh(); }

        private void CommandSelected(object sender, SelectionChangedEventArgs e)
        {
            if (_refreshing) return;
            var id = (Commands.SelectedItem as DcsBrowserRow)?.Id;
            _browser?.SelectCommand(id);
            DisplaySelectedCommand();
        }

        private void DisplaySelectedCommand()
        {
            if (_selected is null)
            {
                CommandId.Text = ""; CommandName.Text = ""; CommandCategory.Text = "";
                CommandIsAxis.IsChecked = false; CommandId.IsReadOnly = false;
            }
            else
            {
                CommandId.Text = _selected.Id; CommandName.Text = _selected.Name;
                CommandCategory.Text = _selected.Category; CommandIsAxis.IsChecked = _selected.IsAxis;
                CommandId.IsReadOnly = true;
            }
            _displayedCommand = _selected;
            SelectedCommandChanged?.Invoke(this, _selected);
        }

        private void AddClicked(object sender, RoutedEventArgs e)
        {
            if (_context is null) return;
            var id = CommandId.Text.Trim(); var name = CommandName.Text.Trim();
            if (id.Length == 0 || name.Length == 0) { StatusText.Text = "Enter an id and a name."; return; }
            try
            {
                if (!StageCatalogEdit(w => DcsModuleCommandCatalogEditor.Set(w, null, new(id, name, CommandCategory.Text.Trim(), CommandIsAxis.IsChecked == true)))) return;
                StatusText.Text = "Command added and staged."; CommandId.Text = ""; CommandName.Text = "";
            }
            catch (Exception ex) { StatusText.Text = ex.Message; }
        }

        private void UpdateClicked(object sender, RoutedEventArgs e)
        {
            if (_context is null || _selected is null) { StatusText.Text = "Select a command to update."; return; }
            try
            {
                if (!StageCatalogEdit(w => DcsModuleCommandCatalogEditor.Set(w, _selected, new(_selected.Id, CommandName.Text.Trim(), CommandCategory.Text.Trim(), CommandIsAxis.IsChecked == true)))) return;
                StatusText.Text = "Command updated and staged.";
            }
            catch (Exception ex) { StatusText.Text = ex.Message; }
        }

        private void RemoveClicked(object sender, RoutedEventArgs e)
        {
            if (_context is null || _selected is null) { StatusText.Text = "Select a command to remove."; return; }
            try
            {
                if (!StageCatalogEdit(w => DcsModuleCommandCatalogEditor.Remove(w, _selected))) return;
                StatusText.Text = "Command removed and staged."; _browser?.SelectCommand(null);
            }
            catch (Exception ex) { StatusText.Text = ex.Message; }
        }

        private void ImportJsonClicked(object sender, RoutedEventArgs e) => ImportCommands(false);
        private void ImportHtmlClicked(object sender, RoutedEventArgs e) => ImportCommands(true);

        private void ImportCommands(bool html)
        {
            if (_context?.Working is null) return;
            var picker = new ThemeBrowseDialog
            {
                Title = html ? "Select DCS Controls HTML" : "Select IPI command catalog JSON",
                Filter = html ? "DCS Controls HTML|*.html;*.htm" : "IPI command catalog JSON|*.json",
                Multiselect = html
            };
            var paths = _context.CommandFilePicker is null
                ? (picker.ShowDialog(Window.GetWindow(this)) == true ? picker.FileNames : null)
                : _context.CommandFilePicker(html);
            if (paths is null) return;
            try
            {
                var review = DcsModuleCommandCatalogImport.ReadFiles(_context.Working, paths, html);
                var dialog = new DcsCommandImportReviewWindow(_context.Working.Configuration.Module.DisplayName, review) { Owner = Window.GetWindow(this) };
                if (dialog.ShowDialog() != true) return;
                if (!StageCatalogEdit(w => DcsModuleCommandCatalogImport.Apply(w, review))) return;
                StatusText.Text = "Commands imported and staged.";
            }
            catch (Exception ex) { StatusText.Text = "Import failed: " + ex.Message; }
        }

        private void ClearAllClicked(object sender, RoutedEventArgs e)
        {
            if (_context?.Working is null) return;
            var count = _context.Working.ModuleCommands.Count;
            if (count == 0) { StatusText.Text = "No commands to clear."; return; }
            if (ThemeMessageBox.Show(Window.GetWindow(this), $"Clear all {count} module commands? Assignments that use them must be removed first.",
                "Clear all commands", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            try
            {
                if (!StageCatalogEdit(w =>
                {
                    var current = w;
                    foreach (var command in w.ModuleCommands.ToArray()) current = DcsModuleCommandCatalogEditor.Remove(current, command);
                    return current;
                })) return;
                StatusText.Text = "All commands cleared and staged.";
            }
            catch (Exception ex) { StatusText.Text = "Clear failed: " + ex.Message; }
        }

        private bool StageCatalogEdit(Func<DcsEditorWorkspaceSnapshot, DcsEditorWorkspaceSnapshot> edit)
        {
            var result = _context!.RunStructureEdit(edit);
            if (result.Outcome == WorkspaceOperationOutcome.Applied) return true;
            StatusText.Text = result.Message;
            return false;
        }

        private async void SaveClicked(object sender, RoutedEventArgs e)
        {
            if (_context is null) return;
            try
            {
                await _context.SaveAsync();
                StatusText.Text = "Saved to database.";
            }
            catch (Exception ex) { StatusText.Text = "Database save failed: " + ex.Message; }
        }

        private void DiscardClicked(object sender, RoutedEventArgs e)
        {
            _context?.Discard();
            StatusText.Text = "Pending edits discarded.";
        }

        private void CommandDoubleClicked(object sender, MouseButtonEventArgs e)
        {
            if (_context?.Working is null || _selected is null) return;
            var assignments = (_context.Draft?.Snapshot ?? _context.Working.Assignments).Where(x => x.Command.Id == _selected.Id).ToArray();
            new DcsCommandUsageWindow(_selected.Name, _selected.Category, assignments, _context.Working, _context.Labels) { Owner = Window.GetWindow(this) }.ShowDialog();
        }

        private void CommandDragStart(object sender, MouseButtonEventArgs e) => _dragStart = e.GetPosition(this);

        private void CommandDrag(object sender, MouseEventArgs e)
        {
            if (_dragStart is not Point start || _selected is null || e.LeftButton != MouseButtonState.Pressed) return;
            var pos = e.GetPosition(this);
            if (Math.Abs(pos.X - start.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(pos.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            _dragStart = null;
            var command = _selected;
            _previewHost = FindPreviewHost();
            _layer = AdornerLayer.GetAdornerLayer(_previewHost);
            _adorner = new TitleAdorner(_previewHost, command.Name)
            {
                IsHitTestVisible = false
            };
            UpdatePreviewPosition();
            try
            {
                _layer?.Add(_adorner);
                DragDrop.DoDragDrop(Commands,
                    new DataObject(typeof(DcsCommandPaletteEntry), command), DragDropEffects.Copy);
            }
            finally
            {
                if (_adorner is not null)
                    _layer?.Remove(_adorner);
                _adorner = null;
                _layer = null;
                _previewHost = null;
                _dragStart = null;
            }
        }

        private UIElement FindPreviewHost()
        {
            // Use the content of the nearest decorator, spanning both the
            // command list and sibling drop targets when embedded in AssignmentsView.
            DependencyObject? current = this;
            while (current is not null)
            {
                if (current is AdornerDecorator decorator && decorator.Child is UIElement child)
                    return child;
                current = VisualTreeHelper.GetParent(current);
            }
            return this;
        }

        private void UpdatePreviewPosition()
        {
            if (_adorner is null || _previewHost is null || !GetCursorPos(out var point))
                return;
            _adorner.Update(_previewHost.PointFromScreen(new Point(point.X, point.Y)));
        }

        private void CommandGiveFeedback(object sender, GiveFeedbackEventArgs e)
        {
            UpdatePreviewPosition();
            e.UseDefaultCursors = false;
            Mouse.SetCursor(e.Effects == DragDropEffects.None ? Cursors.No : Cursors.Arrow);
            e.Handled = true;
        }
        #endregion END private helpers
    }
}



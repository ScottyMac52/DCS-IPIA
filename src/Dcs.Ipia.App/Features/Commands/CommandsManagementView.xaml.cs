using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;
using Dcs.Ipia.Dcs;
using DcsIpia.Presentation;
using Microsoft.Win32;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

namespace DcsIpia.Features.Commands
{
    public partial class CommandsManagementView : UserControl
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT point);

        private UIElement? _previewHost;
        private IModuleWorkspaceContext? _context;
        private CommandPaletteEntry? _selected;
        private bool _updatingFilters;
        private Point? _dragStart;
        private TitleAdorner? _adorner;
        private AdornerLayer? _layer;

        public event EventHandler<CommandPaletteEntry?>? SelectedCommandChanged;
        public CommandPaletteEntry? SelectedCommand => _selected;

        public CommandsManagementView()
        {
            InitializeComponent();
            // Replace the ordinary XAML subscription with one that also
            // receives feedback already handled by the DataGrid or its children.
            GiveFeedbackEventHandler feedback = CommandGiveFeedback;
            Commands.RemoveHandler(DragDrop.GiveFeedbackEvent, feedback);
            Commands.AddHandler(DragDrop.GiveFeedbackEvent, feedback, true);
        }

        public void Initialize(IModuleWorkspaceContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            _context = context;
            _context.StateChanged += (_, _) => Refresh();
            Refresh();
        }

        private void Refresh()
        {
            if (_context?.Working is null || CommandSearch is null || CommandCategoryFilter is null ||
                CommandTypeFilter is null || CommandStateFilter is null)
            {
                StatusText.Text = "No working snapshot";
                if (Commands is not null) Commands.ItemsSource = null;
                return;
            }

            var all = _context.Working.ModuleCommands;
            var categories = Enumerable.Repeat("All", 1)
                .Concat(all.Select(x => x.Category).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
                .ToArray();

            _updatingFilters = true;
            var selectedCategory = CommandCategoryFilter.SelectedItem as string;
            CommandCategoryFilter.ItemsSource = categories;
            CommandCategoryFilter.SelectedItem = selectedCategory is not null && categories.Contains(selectedCategory)
                ? selectedCategory : "All";
            _updatingFilters = false;

            var search = CommandSearch.Text;
            var category = CommandCategoryFilter.SelectedItem as string;
            var filtered = all.Where(x =>
                (string.IsNullOrWhiteSpace(search) ||
                 x.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                 x.Id.Contains(search, StringComparison.OrdinalIgnoreCase)) &&
                (category is null || category == "All" || x.Category == category) &&
                (CommandTypeFilter.SelectedIndex == 0 || (CommandTypeFilter.SelectedIndex == 2) == x.IsAxis))
                .ToArray();

            var assignments = _context.Draft?.Snapshot ?? _context.Working.Assignments;
            var devices = WorkspaceStructureEditor.Describe(_context.Working).Devices;
            var deviceCount = devices.Select(d => d.ProfileId).Distinct().Count();
            var usage = assignments.GroupBy(a => a.Command.Id, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Select(a => a.Slot.ProfileId).Distinct().Count(), StringComparer.Ordinal);
            var bound = assignments.GroupBy(a => a.Command.Id, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

            var state = (CommandStateFilter.SelectedItem as ComboBoxItem)?.Content as string ?? "All";
            filtered = filtered.Where(x =>
            {
                var usedOn = usage.GetValueOrDefault(x.Id);
                return state switch
                {
                    "Assigned on this device/layer" => usedOn > 0,
                    "Unassigned on this device/layer" => usedOn == 0,
                    "Assigned on every device" => usedOn == deviceCount && deviceCount > 0,
                    "Unassigned on every device" => usedOn == 0,
                    _ => true
                };
            }).ToArray();

            Commands.ItemsSource = filtered.Select(x => new { x.Id, x.Name, x.Category, x.IsAxis, Bound = bound.GetValueOrDefault(x.Id) }).ToArray();
            StatusText.Text = $"{filtered.Length} of {all.Count} module commands";
        }

        private void FiltersChanged(object sender, EventArgs e) { if (!_updatingFilters) Refresh(); }

        private void CommandSelected(object sender, SelectionChangedEventArgs e)
        {
            var id = (Commands.SelectedItem as dynamic)?.Id as string;
            _selected = id is null ? null : _context?.Working?.ModuleCommands.FirstOrDefault(x => x.Id == id);
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
            SelectedCommandChanged?.Invoke(this, _selected);
        }

        private void AddClicked(object sender, RoutedEventArgs e)
        {
            if (_context is null) return;
            var id = CommandId.Text.Trim(); var name = CommandName.Text.Trim();
            if (id.Length == 0 || name.Length == 0) { StatusText.Text = "Enter an id and a name."; return; }
            try
            {
                _context.RunStructureEdit(w => ModuleCommandCatalogEditor.Set(w, null, new(id, name, CommandCategory.Text.Trim(), CommandIsAxis.IsChecked == true)));
                StatusText.Text = "Command added and staged."; CommandId.Text = ""; CommandName.Text = "";
            }
            catch (Exception ex) { StatusText.Text = ex.Message; }
        }

        private void UpdateClicked(object sender, RoutedEventArgs e)
        {
            if (_context is null || _selected is null) { StatusText.Text = "Select a command to update."; return; }
            try
            {
                _context.RunStructureEdit(w => ModuleCommandCatalogEditor.Set(w, _selected, new(_selected.Id, CommandName.Text.Trim(), CommandCategory.Text.Trim(), CommandIsAxis.IsChecked == true)));
                StatusText.Text = "Command updated and staged.";
            }
            catch (Exception ex) { StatusText.Text = ex.Message; }
        }

        private void RemoveClicked(object sender, RoutedEventArgs e)
        {
            if (_context is null || _selected is null) { StatusText.Text = "Select a command to remove."; return; }
            try
            {
                _context.RunStructureEdit(w => ModuleCommandCatalogEditor.Remove(w, _selected));
                StatusText.Text = "Command removed and staged."; _selected = null;
            }
            catch (Exception ex) { StatusText.Text = ex.Message; }
        }

        private void ImportJsonClicked(object sender, RoutedEventArgs e) => ImportCommands(false);
        private void ImportHtmlClicked(object sender, RoutedEventArgs e) => ImportCommands(true);

        private void ImportCommands(bool html)
        {
            if (_context?.Working is null) return;
            var picker = new OpenFileDialog
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
                var review = ModuleCommandCatalogImport.ReadFiles(_context.Working, paths, html);
                var dialog = new CommandImportReviewWindow(_context.Working.Configuration.Module.DisplayName, review) { Owner = Window.GetWindow(this) };
                if (dialog.ShowDialog() != true) return;
                _context.RunStructureEdit(w => ModuleCommandCatalogImport.Apply(w, review));
                StatusText.Text = "Commands imported and staged.";
            }
            catch (Exception ex) { StatusText.Text = "Import failed: " + ex.Message; }
        }

        private void ClearAllClicked(object sender, RoutedEventArgs e)
        {
            if (_context?.Working is null) return;
            var count = _context.Working.ModuleCommands.Count;
            if (count == 0) { StatusText.Text = "No commands to clear."; return; }
            if (MessageBox.Show(Window.GetWindow(this), $"Clear all {count} module commands? Assignments that use them must be removed first.",
                "Clear all commands", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            try
            {
                _context.RunStructureEdit(w =>
                {
                    var current = w;
                    foreach (var command in w.ModuleCommands.ToArray()) current = ModuleCommandCatalogEditor.Remove(current, command);
                    return current;
                });
                StatusText.Text = "All commands cleared and staged.";
            }
            catch (Exception ex) { StatusText.Text = "Clear failed: " + ex.Message; }
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
            new CommandUsageWindow(_selected.Name, _selected.Category, assignments, _context.Working, _context.Labels) { Owner = Window.GetWindow(this) }.ShowDialog();
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
                    new DataObject(typeof(CommandPaletteEntry), command), DragDropEffects.Copy);
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
    }
}


using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Dcs.Ipia.Rendering;
using System.Windows.Controls;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;

namespace DcsIpia.Presentation
{
    /// <summary>Reviews affected saved profiles and explicitly stages, saves or discards selected revision changes.</summary>
    public sealed class MappingRevisionReviewWindow : Window
    {
        private readonly UserMappingRevision _revision;
        private readonly DrawingReplacementSession _session;
        private readonly StackPanel _rows = new();
        private readonly Image _before = new() { Stretch = System.Windows.Media.Stretch.Uniform };
        private readonly Image _after = new() { Stretch = System.Windows.Media.Stretch.Uniform };
        private readonly TextBlock _comparison = new() { Text = "Choose Compare pictures beside a target to preview its current and proposed artwork.", TextWrapping = TextWrapping.Wrap };
        private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
        private readonly List<(CheckBox Choice, DrawingReplacementTarget Target)> _targets = [];
        private readonly List<Button> _restoreButtons = [];
        private readonly Button _selectAll;
        private readonly Button _clearSelection;
        private readonly Button _stage;
        private readonly Button _save;
        private readonly Button _discard;
        private bool _busy;
        #region BEGIN Public
        /// <summary>Creates a review boundary with no automatic module mutation.</summary>
        /// <param name="revision">Selected library or historical pin, including rollback targets.</param>
        /// <param name="repository">Saved workspaces supporting stale-state checked updates.</param>
        public MappingRevisionReviewWindow(UserMappingRevision revision, IEditorWorkspaceRepository repository)
        {
            _revision = revision; _session = new(revision, repository);
            Title = "Review drawing adoption / rollback — " + revision.Title;
            Width = 1100; Height = 850;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            SetResourceReference(BackgroundProperty, "WindowBackgroundBrush");
            SetResourceReference(ForegroundProperty, "PrimaryTextBrush");
            NameScope.SetNameScope(this, new NameScope());
            var panel = new DockPanel { Margin = new Thickness(20) };
            var heading = new StackPanel();
            heading.Children.Add(new TextBlock { Text = "Upload only saves a drawing. To replace artwork, select individual devices or all compatible devices across modules, then Stage selected and Save staged changes.", TextWrapping = TextWrapping.Wrap });
            heading.Children.Add(new TextBlock { Text = "Close module editors before using this review. Incompatible control IDs are blocked until the drawing is reconciled. Older pins remain available for rollback and export.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 12) });
            var actions = new WrapPanel();
            _selectAll = Action("Select all compatible", "SelectAllRevisionTargetsButton", (_, _) => SelectTargets(true));
            _clearSelection = Action("Clear selection", "ClearRevisionTargetsButton", (_, _) => SelectTargets(false));
            actions.Children.Add(_selectAll); actions.Children.Add(_clearSelection);
            _stage = Action("Stage selected", "StageRevisionTargetsButton", StageClicked);
            _save = Action("Save staged changes", "SaveRevisionTargetsButton", SaveClicked);
            _discard = Action("Discard staged changes", "DiscardRevisionTargetsButton", (_, _) => { _session.Discard(); SetBusy(false); _status.Text = "Discarded unsaved drawing changes. Reopen review after any save attempt."; });
            actions.Children.Add(_stage); actions.Children.Add(_save); actions.Children.Add(_discard);
            heading.Children.Add(actions); heading.Children.Add(_status);
            RegisterName("RevisionReviewStatus", _status);
            DockPanel.SetDock(heading, Dock.Top); panel.Children.Add(heading);
            var comparison = new DockPanel { Height = 290, Margin = new Thickness(0, 12, 0, 0) };
            DockPanel.SetDock(_comparison, Dock.Top); comparison.Children.Add(_comparison);
            var pictures = new Grid();
            pictures.ColumnDefinitions.Add(new ColumnDefinition()); pictures.ColumnDefinitions.Add(new ColumnDefinition());
            var beforePanel = new DockPanel(); var afterPanel = new DockPanel();
            var beforeTitle = new TextBlock { Text = "Current module artwork" }; var afterTitle = new TextBlock { Text = "Selected revision" };
            DockPanel.SetDock(beforeTitle, Dock.Top); DockPanel.SetDock(afterTitle, Dock.Top);
            beforePanel.Children.Add(beforeTitle); beforePanel.Children.Add(_before);
            afterPanel.Children.Add(afterTitle); afterPanel.Children.Add(_after);
            Grid.SetColumn(afterPanel, 1); pictures.Children.Add(beforePanel); pictures.Children.Add(afterPanel);
            RegisterName("RevisionBeforePreview", _before); RegisterName("RevisionAfterPreview", _after);
            comparison.Children.Add(pictures); DockPanel.SetDock(comparison, Dock.Bottom); panel.Children.Add(comparison);
            panel.Children.Add(new ScrollViewer { Content = _rows, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            Content = panel;
            Loaded += async (_, _) => await LoadAsync();
            Closing += (_, e) =>
            {
                if (_busy) { e.Cancel = true; return; }
                if (_session.StagedModuleCount > 0 && ThemeMessageBox.Show(this, "Discard staged drawing changes and close?", "Unsaved drawing changes",
                    MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) e.Cancel = true;
            };
            SetBusy(false);
        }
        #endregion END Public
        #region BEGIN private helpers
        private Button Action(string label, string name, RoutedEventHandler handler)
        {
            var button = new Button { Content = label, Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 8, 8, 8) };
            RegisterName(name, button); button.Click += handler; return button;
        }
        private async Task LoadAsync()
        {
            SetBusy(true);
            try
            {
                await _session.LoadAsync();
                foreach (var target in _session.Targets)
                {
                    var choice = new CheckBox { Content = target.Title + (target.AlreadyApplied ? " — already pinned" : ""),
                        IsEnabled = target.CanReplace, Margin = new Thickness(0, 12, 0, 4) };
                    _rows.Children.Add(Action("Compare pictures", "PreviewRevisionTarget_" + _targets.Count, (_, _) => ShowComparison(target.Diagram)));
                    RegisterName("RevisionTarget_" + _targets.Count, choice);
                    _targets.Add((choice, target));
                    _rows.Children.Add(choice);
                    _rows.Children.Add(new TextBlock { Text = string.Join("\n", target.Review.Changes.Concat(target.Review.Conflicts)),
                        TextWrapping = TextWrapping.Wrap, Margin = new Thickness(24, 0, 0, 8) });
                    if (target.CanRestoreOriginal)
                    {
                        var restore = Action("Stage original built-in drawing", "RestoreOriginalDrawing_" + (_targets.Count - 1), (_, _) => StageOriginal(target));
                        _restoreButtons.Add(restore); _rows.Children.Add(restore);
                    }
                }
                _status.Text = _targets.Count == 0 ? "No saved device drawings are available. Create a module first." :
                    "No devices selected. Saving a drawing alone changes no modules. Select all compatible affects only eligible devices.";
            }
            catch (Exception ex) { _status.Text = "Unable to review saved modules: " + ex.Message; }
            finally { SetBusy(false); }
        }
        private void ShowComparison(UiLayerDiagram diagram)
        {
            try
            {
                _before.Source = Bitmap(diagram.Svg, diagram.Width, diagram.Height);
                _after.Source = Bitmap(_revision.Svg, _revision.Width, _revision.Height);
                _comparison.Text = diagram.Title + " · " + (diagram.CustomMapping?.Title ?? "Current built-in/generic layout") + " → " + _revision.Title;
            }
            catch (Exception ex) { _before.Source = _after.Source = null; _comparison.Text = "Unable to compare artwork: " + ex.Message; }
        }
        private static BitmapImage Bitmap(string svg, double width, double height)
        {
            using var stream = new MemoryStream(CanonicalSvgRenderer.RenderComplete(svg, width, height));
            var bitmap = new BitmapImage();
            bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = stream;
            bitmap.EndInit(); bitmap.Freeze(); return bitmap;
        }
        private void SelectTargets(bool select)
        {
            var compatible = _session.CompatibleTargets;
            foreach (var target in _targets) target.Choice.IsChecked = select && compatible.Contains(target.Target);
        }
        private void StageOriginal(DrawingReplacementTarget target)
        {
            try
            {
                var original = target.Diagram.OriginalDrawing!;
                _before.Source = Bitmap(target.Diagram.Svg, target.Diagram.Width, target.Diagram.Height);
                _after.Source = Bitmap(original.Svg, original.Width, original.Height);
                _comparison.Text = target.Title + " → original built-in drawing";
                _session.Stage([target], restoreOriginal: true);
                _status.Text = "Original built-in drawing staged. Save to keep it or Discard.";
            }
            catch (Exception ex) { _status.Text = "Unable to stage original drawing: " + ex.Message; }
            SetBusy(false);
        }
        private void StageClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                _session.Stage(_targets.Where(x => x.Choice.IsChecked == true).Select(x => x.Target));
                _status.Text = "Staged changes in " + _session.StagedModuleCount + " modules. Saved data is unchanged until Save.";
            }
            catch (Exception ex) { _status.Text = "Unable to stage changes: " + ex.Message; }
            SetBusy(false);
        }
        private async void SaveClicked(object sender, RoutedEventArgs e)
        {
            SetBusy(true);
            try
            {
                await _session.SaveAsync();
                _status.Text = "Saved reviewed changes in " + _session.SavedModuleCount + " modules. Unselected devices are unchanged. Reopen review for another change.";
            }
            catch (Exception ex)
            {
                _status.Text = "Saved " + _session.SavedModuleCount + " modules; remaining changes were not saved. " + ex.Message + " Discard and reopen review before retrying.";
            }
            finally { SetBusy(false); }
        }
        private void SetBusy(bool busy)
        {
            _busy = busy;
            var canStage = !busy && _session.StagedModuleCount == 0 && !_session.RequiresReload;
            _stage.IsEnabled = _selectAll.IsEnabled = _clearSelection.IsEnabled = canStage;
            _save.IsEnabled = !busy && _session.StagedModuleCount > 0 && _session.CanSave && !_session.RequiresReload;
            _discard.IsEnabled = !busy && _session.StagedModuleCount > 0;
            foreach (var target in _targets) target.Choice.IsEnabled = canStage && target.Target.CanReplace;
            foreach (var restore in _restoreButtons) restore.IsEnabled = canStage;
        }
        #endregion END private helpers
    }
}

using System.Windows;
using System.Windows.Controls;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;

namespace DcsIpia.Presentation
{
    /// <summary>Reviews affected saved profiles and explicitly stages, saves or discards selected revision changes.</summary>
    public sealed class MappingRevisionReviewWindow : Window
    {
        private readonly UserMappingRevision _revision;
        private readonly IEditorWorkspaceRepository _repository;
        private readonly StackPanel _rows = new();
        private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
        private readonly List<(CheckBox Choice, EditorWorkspaceSnapshot Expected, Guid ProfileId)> _targets = [];
        private readonly Dictionary<Guid, (EditorWorkspaceSnapshot Expected, EditorWorkspaceSnapshot Updated)> _pending = [];
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
            _revision = revision; _repository = repository;
            Title = "Review drawing adoption / rollback — " + revision.Title;
            Width = 950; Height = 700;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            SetResourceReference(BackgroundProperty, "WindowBackgroundBrush");
            SetResourceReference(ForegroundProperty, "PrimaryTextBrush");
            NameScope.SetNameScope(this, new NameScope());
            var panel = new DockPanel { Margin = new Thickness(20) };
            var heading = new StackPanel();
            heading.Children.Add(new TextBlock { Text = "Review each module and physical device. Select only the targets to change, then Stage selected. Save commits the staged drawings; Discard leaves saved modules unchanged.", TextWrapping = TextWrapping.Wrap });
            heading.Children.Add(new TextBlock { Text = "Close module editors before using this review. Incompatible control IDs are blocked until the drawing is reconciled. Older pins remain available for rollback and export.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 12) });
            var actions = new WrapPanel();
            _stage = Action("Stage selected", "StageRevisionTargetsButton", StageClicked);
            _save = Action("Save staged changes", "SaveRevisionTargetsButton", SaveClicked);
            _discard = Action("Discard staged changes", "DiscardRevisionTargetsButton", (_, _) => { _pending.Clear(); SetBusy(false); _status.Text = "Discarded staged drawing changes. Saved modules are unchanged."; });
            actions.Children.Add(_stage); actions.Children.Add(_save); actions.Children.Add(_discard);
            heading.Children.Add(actions); heading.Children.Add(_status);
            RegisterName("RevisionReviewStatus", _status);
            DockPanel.SetDock(heading, Dock.Top); panel.Children.Add(heading);
            panel.Children.Add(new ScrollViewer { Content = _rows, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            Content = panel;
            Loaded += async (_, _) => await LoadAsync();
            Closing += (_, e) =>
            {
                if (_busy) { e.Cancel = true; return; }
                if (_pending.Count > 0 && MessageBox.Show(this, "Discard staged drawing changes and close?", "Unsaved drawing changes",
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
                foreach (var id in await _repository.ListAsync())
                {
                    var workspace = await _repository.GetAsync(id);
                    if (workspace is null) continue;
                    foreach (var diagram in workspace.Diagrams.Where(x => x.GenericCapabilities is not null || x.CustomMapping is not null))
                    {
                        var review = MappingRevisionReview.Create(diagram, _revision);
                        var already = diagram.CustomMapping?.Id == _revision.Id;
                        var choice = new CheckBox { Content = workspace.Configuration.Module.DisplayName + " / " + diagram.Title +
                            (already ? " — already pinned" : ""), IsEnabled = !already && review.Conflicts.Count == 0,
                            Margin = new Thickness(0, 12, 0, 4), Tag = review.Conflicts.Count == 0 && !already };
                        RegisterName("RevisionTarget_" + _targets.Count, choice);
                        _targets.Add((choice, workspace, diagram.ProfileId));
                        _rows.Children.Add(choice);
                        _rows.Children.Add(new TextBlock { Text = string.Join("\n", review.Changes.Concat(review.Conflicts)),
                            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(24, 0, 0, 8) });
                    }
                }
                _status.Text = _targets.Count == 0 ? "No saved generic/custom profiles are available. Create a module first." :
                    "No targets are selected. Reviewing and changing library artwork do not update modules.";
            }
            catch (Exception ex) { _status.Text = "Unable to review saved modules: " + ex.Message; }
            finally { SetBusy(false); }
        }
        private void StageClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                var staged = new Dictionary<Guid, (EditorWorkspaceSnapshot Expected, EditorWorkspaceSnapshot Updated)>();
                foreach (var group in _targets.Where(x => x.Choice.IsChecked == true && x.Choice.IsEnabled)
                    .GroupBy(x => x.Expected.Configuration.Module.Id))
                {
                    var expected = group.First().Expected;
                    var updated = expected;
                    foreach (var target in group) updated = UserMappingAdoption.Apply(updated, target.ProfileId, _revision);
                    staged.Add(group.Key, (expected, updated));
                }
                foreach (var entry in staged) _pending.Add(entry.Key, entry.Value);
                _status.Text = _pending.Count == 0 ? "Select at least one compatible target." :
                    "Staged changes in " + _pending.Count + " modules. Saved data is unchanged until Save.";
                SetBusy(false);
            }
            catch (Exception ex) { _pending.Clear(); _status.Text = "Unable to stage changes: " + ex.Message; SetBusy(false); }
        }
        private async void SaveClicked(object sender, RoutedEventArgs e)
        {
            if (_repository is not IEditorWorkspaceUpdateRepository updates) return;
            SetBusy(true);
            var saved = 0;
            try
            {
                foreach (var entry in _pending.ToArray())
                {
                    await updates.SaveIfUnchangedAsync(entry.Value.Expected, entry.Value.Updated);
                    _pending.Remove(entry.Key); saved++;
                    foreach (var target in _targets.Where(x => x.Expected.Configuration.Module.Id == entry.Key))
                    { target.Choice.IsChecked = false; target.Choice.Tag = false; }
                }
                _status.Text = "Saved reviewed changes in " + saved + " modules. Other modules and devices retain their pins. Close and reopen review for another change.";
            }
            catch (Exception ex) { _status.Text = "Saved " + saved + " modules; remaining changes were not saved. " + ex.Message + " Discard and reopen review before retrying."; }
            finally { SetBusy(false); }
        }
        private void SetBusy(bool busy)
        {
            _busy = busy;
            _stage.IsEnabled = !busy && _pending.Count == 0;
            _save.IsEnabled = !busy && _pending.Count > 0 && _repository is IEditorWorkspaceUpdateRepository;
            _discard.IsEnabled = !busy && _pending.Count > 0;
            foreach (var target in _targets) target.Choice.IsEnabled = !busy && _pending.Count == 0 && target.Choice.Tag is true;
        }
        #endregion END private helpers
    }
}

using System.Windows;
using System.Windows.Controls;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;

namespace DcsIpia.Presentation
{
    /// <summary>Reopens saved shared defaults independently of aircraft module editors.</summary>
    public partial class SharedUiLayerWorkspaceWindow : Window
    {
        private SharedUiLayerWorkspace _workspace;
        private SharedUiLayerWorkspace _draft;
        private readonly ISharedUiLayerWorkspaceRepository? _repository;
        private bool _dirty;
        private bool _saving;
        /// <summary>Shows a stored revision and its original catalog and Lua source texts.</summary>
        /// <param name="workspace">Saved shared defaults.</param>
        /// <param name="repository">Revision storage, or null for a read-only preview.</param>
        public SharedUiLayerWorkspaceWindow(SharedUiLayerWorkspace workspace, ISharedUiLayerWorkspaceRepository? repository = null)
        {
            _workspace = workspace;
            _draft = workspace;
            _repository = repository;
            InitializeComponent();
            RevisionText.Text = "Revision " + workspace.Revision;
            SourcePaths.ItemsSource = workspace.Sources.Keys.Order(StringComparer.Ordinal).ToArray();
            SourcePaths.SelectedIndex = 0;
            BindingFields.IsEnabled = repository is not null;
            ProfilePaths.ItemsSource = SharedUiLayerBindingEditor.Profiles(workspace);
            if (repository is not null)
            {
                CommandChoice.ItemsSource = SharedUiLayerBindingEditor.Commands(workspace);
                DeclaredModifiers.Text = "Declared modifiers: " + string.Join(", ", DcsLuaParser.ParseModifiers(
                    workspace.Sources["assets/shared/ui-layer/input/UiLayer/modifiers.lua"]).Select(x => x.Name));
            }
            ProfilePaths.SelectedIndex = 0;
            Buttons();
            Closing += (_, e) => {
                if (_saving || _dirty && MessageBox.Show(this, "Discard pending shared default binding changes?", "Pending shared defaults",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) e.Cancel = true;
            };
        }
        #region BEGIN Public
        #endregion END Public
        #region BEGIN private helpers
        private void SourceChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SourcePaths.SelectedItem is string path) SourceText.Text = _draft.Sources[path];
        }
        private void Buttons()
        {
            SaveDefaultsButton.IsEnabled = _repository is not null && _dirty && !_saving;
            DiscardDefaultsButton.IsEnabled = _dirty && !_saving;
            BindingFields.IsEnabled = _repository is not null && !_saving;
            ClearLayerButton.IsEnabled = _repository is not null && !_saving;
        }
        private void ProfileChanged(object sender, SelectionChangedEventArgs e)
        {
            if (BindingRows is null) return;
            BindingRows.ItemsSource = ProfilePaths.SelectedItem is string path ? SharedUiLayerBindingEditor.Bindings(_draft, path) : null;
            if (ClearLayerChoice is null) return;
            var selected = ClearLayerChoice.SelectedItem as string ?? "Base";
            ClearLayerChoice.ItemsSource = Enumerable.Repeat("Base", 1).Concat(
                DcsLuaParser.ParseModifiers(_draft.Sources["assets/shared/ui-layer/input/UiLayer/modifiers.lua"]).Select(x => x.Name))
                .Concat((BindingRows.ItemsSource as IEnumerable<DcsBinding> ?? []).Where(x => x.Reformers.Count > 0)
                    .Select(x => string.Join(" + ", x.Reformers.Order(StringComparer.Ordinal))))
                .Distinct(StringComparer.Ordinal).ToArray();
            ClearLayerChoice.SelectedItem = (ClearLayerChoice.ItemsSource as IEnumerable<string>)!.Contains(selected) ? selected : "Base";
        }
        private void BindingSelected(object sender, SelectionChangedEventArgs e)
        {
            if (BindingRows.SelectedItem is not DcsBinding binding || InputKey is null) return;
            InputKey.Text = binding.InputKey;
            ModifierChord.Text = string.Join(" + ", binding.Reformers);
            CommandChoice.SelectedItem = (CommandChoice.ItemsSource as IEnumerable<DraftCommand>)?.FirstOrDefault(x => x.Id == binding.CommandId);
        }
        private void NewBindingClicked(object sender, RoutedEventArgs e)
        {
            BindingRows.SelectedItem = null;
            InputKey.Text = ModifierChord.Text = "";
            CommandChoice.SelectedItem = null;
        }
        private string ProfilePath() => ProfilePaths.SelectedItem as string ?? throw new InvalidOperationException("Select a device profile.");
        private DcsBinding SelectedBinding() => BindingRows.SelectedItem as DcsBinding ?? throw new InvalidOperationException("Select a binding.");
        private void AddBindingClicked(object sender, RoutedEventArgs e) => SetBinding(null);
        private void UpdateBindingClicked(object sender, RoutedEventArgs e) => Edit(() => Set(SelectedBinding()));
        private void SetBinding(DcsBinding? current) => Edit(() => Set(current));
        private SharedUiLayerWorkspace Set(DcsBinding? current) => SharedUiLayerBindingEditor.Set(_draft, ProfilePath(), current,
            (CommandChoice.SelectedItem as DraftCommand)?.Id ?? throw new InvalidOperationException("Select a command."),
            InputKey.Text, ModifierChord.Text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        private void RemoveBindingClicked(object sender, RoutedEventArgs e) => Edit(() => SharedUiLayerBindingEditor.Remove(_draft, ProfilePath(), SelectedBinding()));
        private void ClearLayerClicked(object sender, RoutedEventArgs e) => Edit(() =>
            SharedUiLayerBindingEditor.ClearLayerAssignments(_draft, ProfilePath(),
                ClearLayerChoice.SelectedItem is string layer && layer != "Base" ? layer : ""));
        private void Edit(Func<SharedUiLayerWorkspace> edit)
        {
            try
            {
                _draft = edit();
                _dirty = true;
                BindingRows.ItemsSource = SharedUiLayerBindingEditor.Bindings(_draft, ProfilePath());
                if (SourcePaths.SelectedItem is string path) SourceText.Text = _draft.Sources[path];
                EditorStatus.Text = "Change staged. Save a new revision to keep it, or discard the draft.";
                Buttons();
            }
            catch (Exception ex) { EditorStatus.Text = ex.Message; }
        }
        private async void SaveClicked(object sender, RoutedEventArgs e)
        {
            if (_repository is null || !_dirty || _saving) return;
            _saving = true;
            Buttons();
            try
            {
                SharedUiLayerBindingEditor.Validate(_draft);
                _workspace = await _repository.SaveAsync(_draft);
                _draft = _workspace;
                _dirty = false;
                RevisionText.Text = "Revision " + _workspace.Revision;
                EditorStatus.Text = "Shared default revision saved. Existing module workspaces were not changed.";
            }
            catch (Exception ex) { EditorStatus.Text = "Defaults were not saved: " + ex.Message; }
            finally { _saving = false; Buttons(); }
        }
        private void DiscardClicked(object sender, RoutedEventArgs e)
        {
            _draft = _workspace;
            _dirty = false;
            BindingRows.ItemsSource = ProfilePaths.SelectedItem is string path ? SharedUiLayerBindingEditor.Bindings(_draft, path) : null;
            if (SourcePaths.SelectedItem is string source) SourceText.Text = _draft.Sources[source];
            EditorStatus.Text = "Draft discarded.";
            Buttons();
        }
        #endregion END private helpers
    }
}

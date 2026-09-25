using System.Windows;
using DcsConsumerScaffold.Models;
using DcsConsumerScaffold.Services;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace DcsConsumerScaffold;

/// <summary>Hosts DCS-Common's authoritative UI Layer device and command editor.</summary>
public partial class UiLayerEditorWindow : Window
{
    private UiLayerCatalogComparison? _comparison;
    private readonly List<IpiUiFunctionChoice> _functions = [];
    private UiLayerCatalogBinding? _selectedBinding;
    private UiLayerCatalogDocument? _document;
    private string _fingerprint = string.Empty;
    #region BEGIN Public
    /// <summary>Gets pending authoritative binding edits.</summary>
    public ObservableCollection<UiLayerBindingEdit> StagedEdits { get; } = [];
    /// <summary>Gets editable authoritative modifier definitions.</summary>
    public ObservableCollection<IpiModifierDefinition> EditableModifiers { get; } = [];
    /// <summary>Gets reconciliation actions supported by DCS-Common.</summary>
    public IReadOnlyList<string> Actions { get; } = ["Keep", "Add", "Replace", "Remove"];

    public UiLayerEditorWindow(string? commonRoot = null, string? moduleContext = null)
    {
        InitializeComponent();
        DataContext = this;
        CommonRootBox.Text = commonRoot ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(moduleContext)) StatusText.Text = $"{moduleContext}. Pending destination: DCS-Common authoritative UI Layer.";
        StagedEditsGrid.ItemsSource = StagedEdits;
        ModifiersGrid.ItemsSource = EditableModifiers;
    }

    #endregion END Public
    #region BEGIN private helpers
    private void BrowseCommon_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Select DCS-Common repository root" };
        if (dialog.ShowDialog(this) == true) CommonRootBox.Text = dialog.FolderName;
    }

    private void BrowseSource_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Select Saved Games Config\\Input\\UiLayer folder" };
        if (dialog.ShowDialog(this) == true) SourceRootBox.Text = dialog.FolderName;
    }

    private async void Load_Click(object sender, RoutedEventArgs e) => await RunAsync(async () =>
        Display(await UiLayerCatalogService.InspectAsync(CommonRootBox.Text.Trim())));

    private async void Compare_Click(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        _comparison = await UiLayerCatalogService.CompareAsync(CommonRootBox.Text.Trim(), SourceRootBox.Text.Trim());
        Display(_comparison.Canonical);
        ChangesGrid.ItemsSource = _comparison.Changes;
        SaveButton.IsEnabled = _comparison.Source.Valid;
        StatusText.Text = _comparison.Source.Valid
            ? "Comparison loaded. Absence defaults to Keep; choose Remove explicitly when intended."
            : "Imported source is invalid and cannot be saved.";
    });

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_comparison is null) return;
        if (MessageBox.Show(this, "Validate and atomically apply the selected catalog actions?", "Save definitive UI Layer",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        await RunAsync(async () =>
        {
            var result = await UiLayerCatalogService.ApplyAsync(CommonRootBox.Text.Trim(), SourceRootBox.Text.Trim(), _comparison.Changes);
            Display(result);
            _comparison = null;
            ChangesGrid.ItemsSource = null;
            SaveButton.IsEnabled = false;
            StatusText.Text = $"Definitive UI Layer saved and validated. Fingerprint: {result.Fingerprint}";
        });
    }

    private void Display(UiLayerCatalogDocument document)
    {
        _document = document;
        RefreshProjectedViews();
        _fingerprint = document.Fingerprint;
        EditableModifiers.Clear();
        foreach (var modifier in document.Modifiers) EditableModifiers.Add(new IpiModifierDefinition
            { Name = modifier.Name, Device = modifier.Device, Key = modifier.Key, Mode = modifier.Mode });
        PossibilitiesGrid.ItemsSource = document.Possibilities;
        EditDeviceBox.ItemsSource = document.Possibilities;
        LayerBox.ItemsSource = document.Modifiers.Select(item => new IpiLayerChoice { Name = item.Name }).ToList();
        LayerBox.UnselectAll();
        ErrorsList.ItemsSource = document.Errors;
        SummaryText.Text = $"{document.Scope}: Possibilities={document.Summary.Possibilities}  Profiles={document.Summary.Profiles}  Bindings={document.Summary.Bindings}  " +
            $"Keys={document.Summary.Keys}  Axes={document.Summary.Axes}  Modifiers={document.Summary.Modifiers}  Errors={document.Summary.Errors}";
        StatusText.Text = document.Valid ? $"Catalog valid. Fingerprint: {document.Fingerprint}" : "Catalog validation failed.";
        AuthorSaveButton.IsEnabled = document.Valid;
        LoadFunctions();
    }

    private void LoadFunctions()
    {
        _functions.Clear();
        _functions.AddRange(IpiAuthoringService.UiFunctions(CommonRootBox.Text.Trim()));
        FilterFunctions();
    }

    private void CommandSearch_Changed(object sender, TextChangedEventArgs e) => FilterFunctions();

    private void FilterFunctions()
    {
        var query = CommandSearchBox?.Text?.Trim() ?? string.Empty;
        UiCommandsGrid.ItemsSource = _functions.Where(item => query.Length == 0 ||
            new[] { item.Id, item.Command, item.Label, item.Category }.Any(value => value.Contains(query, StringComparison.OrdinalIgnoreCase))).ToList();
    }

    private static string SelectedText(ComboBox box) => (box.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? string.Empty;

    private async void EditDevice_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (EditDeviceBox.SelectedItem is not UiLayerDevicePossibility device || _document is null) return;
        var profiles = UiLayerEditorSelection.ProfilesForDevice(_document, device);
        EditProfileBox.ItemsSource = profiles;
        EditProfileBox.SelectedIndex = profiles.Count > 0 ? 0 : -1;
        EditProfileBox.Text = profiles.Count > 0 ? profiles[0].Name : string.Empty;
        if (profiles.Count > 0 && profiles[0] is { } profile)
            EditCategoryBox.SelectedIndex = profile.Category switch { "keyboard" => 1, "mouse" => 2, _ => 0 };
        try
        {
            SharedControlBox.ItemsSource = await IpiAuthoringService.SharedControlsAsync(CommonRootBox.Text.Trim(), device.DeviceId);
            SharedControlBox.SelectedIndex = SharedControlBox.Items.Count > 0 ? 0 : -1;
        }
        catch (Exception ex) { StatusText.Text = ex.Message; }
    }

    private void UiCommandSelection_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (UiCommandsGrid.SelectedItem is IpiUiFunctionChoice command)
            EditLabelBox.Text = command.Label;
    }

    private void BindingSelection_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (BindingsGrid.SelectedItem is not UiLayerCatalogBinding binding) return;
        _selectedBinding = binding;
        EditDeviceBox.SelectedItem = ((IEnumerable<UiLayerDevicePossibility>)EditDeviceBox.ItemsSource)
            .FirstOrDefault(item => item.DeviceId.Equals(binding.DeviceId, StringComparison.OrdinalIgnoreCase));
        LayerBox.UnselectAll();
        foreach (var item in ((IEnumerable<IpiLayerChoice>)LayerBox.ItemsSource)
            .Where(item => binding.Modifiers.Contains(item.Name, StringComparer.Ordinal)))
            LayerBox.SelectedItems.Add(item);
        EditCategoryBox.SelectedIndex = binding.Category switch { "keyboard" => 1, "mouse" => 2, _ => 0 };
        var command = _functions.FirstOrDefault(item => item.Command == binding.Command);
        if (command is not null) UiCommandsGrid.SelectedItem = command;
        EditProfileBox.Text = binding.Profile;
        EditLabelBox.Text = binding.Name;
    }

    private void StageEdit_Click(object sender, RoutedEventArgs e)
    {
        if (UiCommandsGrid.SelectedItem is not IpiUiFunctionChoice command)
        {
            MessageBox.Show(this, "Select a validated UI Layer command.", "Stage UI Layer edit", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var action = SelectedText(EditActionBox);
        if (action == "restore")
        {
            RestoreSelectedBinding();
            return;
        }
        var edit = new UiLayerBindingEdit { Action = action, Command = command.Command, Name = string.IsNullOrWhiteSpace(EditLabelBox.Text) ? command.Label : EditLabelBox.Text.Trim() };
        if (action == "relabel") edit.Label = EditLabelBox.Text;
        else
        {
            if (EditDeviceBox.SelectedItem is not UiLayerDevicePossibility device || string.IsNullOrWhiteSpace(EditProfileBox.Text))
                throw new InvalidOperationException("Select a definitive device possibility and profile filename.");
            var control = SharedControlBox.SelectedItem as IpiSharedControlChoice;
            if (action != "clear" && control is null)
                throw new InvalidOperationException("Select a shared DCS-Common control.");
            var category = _selectedBinding?.Profile == EditProfileBox.Text.Trim() ? _selectedBinding.Category : SelectedText(EditCategoryBox);
            if (string.IsNullOrWhiteSpace(category)) throw new InvalidOperationException("Select joystick, keyboard, or mouse profile category.");
            edit.Profile = new UiLayerProfileTarget { Category = category, Filename = EditProfileBox.Text.Trim(), DeviceId = device.DeviceId };
            edit.Section = action == "clear" ? _selectedBinding?.Section ?? "keyDiffs" : control!.Section;
            var chord = LayerBox.SelectedItems.Cast<IpiLayerChoice>().Select(item => item.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToList();
            if (action == "move")
            {
                if (_selectedBinding is null) throw new InvalidOperationException("Select the existing binding to move.");
                edit.From = new UiLayerPhysicalTarget { Key = _selectedBinding.Key, Reformers = [.. _selectedBinding.Modifiers] };
                edit.To = new UiLayerPhysicalTarget { Key = control!.Key, Reformers = chord };
            }
            else
            {
                if (action == "clear" && _selectedBinding is null) throw new InvalidOperationException("Select the existing binding to clear.");
                edit.Key = action == "clear" ? _selectedBinding!.Key : control!.Key;
                edit.Reformers = action == "clear" ? [.. _selectedBinding!.Modifiers] : chord;
            }
        }
        StagedEdits.Add(edit);
        RefreshProjectedViews();
        StatusText.Text = $"Staged authoritative change: {edit.Summary}. Nothing has been written yet.";
    }

    private void RestoreSelectedBinding()
    {
        if (_selectedBinding is null) throw new InvalidOperationException("Select the authoritative binding to restore.");
        var removed = StagedEdits.Where(edit => edit.Command == _selectedBinding.Command
            && edit.Profile?.Filename == _selectedBinding.Profile
            && (edit.Key == _selectedBinding.Key || edit.From?.Key == _selectedBinding.Key || edit.To?.Key == _selectedBinding.Key))
            .ToList();
        foreach (var edit in removed) StagedEdits.Remove(edit);
        RefreshProjectedViews();
        StatusText.Text = removed.Count == 0
            ? "The selected authoritative binding has no staged changes to restore."
            : $"Restored the selected binding by unstaging {removed.Count} pending change(s). Nothing has been written.";
    }

    private void UnstageEdit_Click(object sender, RoutedEventArgs e)
    {
        if (StagedEditsGrid.SelectedItem is UiLayerBindingEdit edit) StagedEdits.Remove(edit);
        RefreshProjectedViews();
    }

    private void DeleteBinding_Click(object sender, RoutedEventArgs e)
    {
        if (BindingsGrid.SelectedItem is not UiLayerCatalogBinding binding) return;
        var edit = UiLayerEditorSelection.DeleteBinding(binding);
        if (!StagedEdits.Any(item => item.Action == edit.Action && item.Profile?.Filename == edit.Profile?.Filename
            && item.Command == edit.Command && item.Key == edit.Key
            && item.Reformers.SequenceEqual(edit.Reformers, StringComparer.Ordinal)))
            StagedEdits.Add(edit);
        RefreshProjectedViews();
        StatusText.Text = $"Staged binding deletion: {binding.Name}. Nothing has been written yet.";
    }

    private void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (ProfilesGrid.SelectedItem is not UiLayerCatalogProfile profile) return;
        if (MessageBox.Show(this, $"Delete {profile.Name} and all of its bindings from the definitive catalog?",
                "Stage profile deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        foreach (var edit in StagedEdits.Where(item => item.Profile?.Filename == profile.Name).ToList())
            StagedEdits.Remove(edit);
        StagedEdits.Add(UiLayerEditorSelection.DeleteProfile(profile));
        RefreshProjectedViews();
        StatusText.Text = $"Staged profile deletion: {profile.Name}. Nothing has been written yet.";
    }

    private void RefreshProjectedViews()
    {
        if (_document is null) return;
        ProfilesGrid.ItemsSource = UiLayerEditorSelection.ProjectProfiles(_document, StagedEdits);
        BindingsGrid.ItemsSource = UiLayerEditorSelection.ProjectBindings(_document, StagedEdits);
    }

    private async void SaveAuthored_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, $"Validate and atomically write {StagedEdits.Count} binding edit(s) and {EditableModifiers.Count} modifier definitions to DCS-Common?",
            "Save authoritative UI Layer", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await RunAsync(async () =>
        {
            var result = await UiLayerCatalogService.ApplyEditsAsync(CommonRootBox.Text.Trim(), _fingerprint, EditableModifiers.ToArray(), StagedEdits.ToArray());
            StagedEdits.Clear();
            Display(result);
            StatusText.Text = result.Impact is null
                ? $"Saved authoritative UI Layer: {string.Join(", ", result.ChangedFiles)}. Rebuild affected consumers."
                : $"Saved authoritative UI Layer: {string.Join(", ", result.ChangedFiles)}. {result.Impact.Summary}";
        });
    }

    private async Task RunAsync(Func<Task> action)
    {
        IsEnabled = false;
        try { await action(); }
        catch (Exception ex) { StatusText.Text = ex.Message; MessageBox.Show(this, ex.Message, "UI Layer Editor", MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { IsEnabled = true; }
    }
    #endregion END private helpers
}

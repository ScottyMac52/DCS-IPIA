using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Dcs;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace Heimdall.App.Features.Assignments
{
    /// <summary>Owns native callout labels, label search/import and generic control naming.</summary>
    internal sealed class DcsAssignmentLabels(
        DcsAssignmentViewState state,
        FrameworkElement owner,
        TextBox LabelEditor,
        TextBox GenericControlName,
        TextBlock SelectedCallout,
        Action refresh,
        Func<string, Func<GameAssignmentReview>, Action, DraftResult> editLabels)
    {
        #region BEGIN Public
        internal void ChangeCalloutLabel(HardwareGraphicControl control)
        {
            if (state.Context is not { Working: { } workspace } context || state.Diagram is not UiLayerDiagram diagram)
                return;

            var chord = state.Chord;
            var slot = DcsKneeboardDraftProjection.SlotForControl(
                workspace, diagram, control, state.Layer, chord);
            if (slot is null) return;

            var assignment = (state.Context.Draft?.Snapshot ?? state.Context.Working.Assignments)
                .FirstOrDefault(x => DcsInputKey.PhysicalSlotKey(x.Slot) == DcsInputKey.PhysicalSlotKey(slot));
            var editor = new TextBox
            {
                Text = state.Context.Labels.GetValueOrDefault(slot.Key) ?? assignment?.Command.Name ?? "",
                MinWidth = 320,
                Margin = new Thickness(0, 8, 0, 12)
            };
            var save = new Button { Content = "OK", IsDefault = true, MinWidth = 80, Margin = new Thickness(0, 0, 8, 0) };
            var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 80 };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            buttons.Children.Add(save);
            buttons.Children.Add(cancel);
            var content = new StackPanel { Margin = new Thickness(16) };
            content.Children.Add(new TextBlock { Text = "Callout label (leave blank to restore the default):" });
            content.Children.Add(editor);
            content.Children.Add(buttons);
            var dialog = new AppearanceDialog("ChangeLabelDialog")
            {
                Title = "Change label · " + control.Label,
                Owner = Window.GetWindow(owner),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.WidthAndHeight,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                Content = content
            };
            save.Click += (_, _) => dialog.DialogResult = true;
            dialog.Loaded += (_, _) =>
            {
                editor.Focus();
                editor.SelectAll();
            };
            var result = editLabels(slot.Key, () =>
            {
                if (state.ReservationError(diagram, control.InputKey) is { } blocked) return new(false, false, blocked);
                return dialog.ShowDialog() == true ? new(true, true, "") : new(false, false, "Label change canceled.");
            }, () => context.SetLabel(slot.Key, editor.Text));
            if (result.Outcome != DraftOutcome.Applied) return;
            state.UpdatingLabel = true;
            try
            {
                LabelEditor.Text = string.IsNullOrWhiteSpace(editor.Text)
                    ? assignment?.Command.Name ?? "" : editor.Text;
            }
            finally
            {
                state.UpdatingLabel = false;
            }
            refresh();
            SelectedCallout.Text = "Callout label changed and staged.";
        }


        internal void SearchReplaceLabelsClicked(object sender, RoutedEventArgs e)
        {
            if (state.Context is not { Working: { } workspace } context || state.Diagram is not UiLayerDiagram diagram)
            {
                SelectedCallout.Text = "Select a device first.";
                return;
            }

            var chord = state.Chord;
            var layer = state.Layer;
            var snapshot = context.Draft?.Snapshot ?? workspace.Assignments;

            var candidates = new List<(HardwareGraphicControl Control, DcsDraftAssignmentSlot Slot, string Current)>();
            foreach (var control in diagram.Controls)
            {
                var slot = DcsKneeboardDraftProjection.SlotForControl(workspace, diagram, control, layer, chord);
                if (slot is null) continue;
                var assignment = snapshot.FirstOrDefault(x =>
                    DcsInputKey.PhysicalSlotKey(x.Slot) == DcsInputKey.PhysicalSlotKey(slot));
                var current = state.Context.Labels.GetValueOrDefault(slot.Key) ?? assignment?.Command.Name ?? control.Label ?? "";
                if (string.IsNullOrWhiteSpace(current)) continue;
                candidates.Add((control, slot, current));
            }

            if (candidates.Count == 0)
            {
                SelectedCallout.Text = "No labels available to search on this device.";
                return;
            }

            var findBox = new TextBox { MinWidth = 280, Margin = new Thickness(0, 4, 0, 8) };
            var replaceBox = new TextBox { MinWidth = 280, Margin = new Thickness(0, 4, 0, 8) };
            var regexCheck = new CheckBox { Content = "Use regular expressions", Margin = new Thickness(0, 0, 0, 8) };
            var scopeCurrent = new RadioButton { Content = "Current control only", IsChecked = state.Selected is not null, Margin = new Thickness(0, 0, 12, 0) };
            var scopeAll = new RadioButton { Content = "All controls on this device", IsChecked = state.Selected is null };
            var scopePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            scopePanel.Children.Add(scopeCurrent);
            scopePanel.Children.Add(scopeAll);

            var previewList = new ListBox { MinHeight = 140, MaxHeight = 220, Margin = new Thickness(0, 4, 0, 8) };
            var status = new TextBlock { Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap };

            var apply = new Button { Content = "Apply", IsDefault = true, IsEnabled = false, MinWidth = 80, Margin = new Thickness(0, 0, 8, 0) };

            void RefreshPreview()
            {
                previewList.Tag = null;
                apply.IsEnabled = false;
                previewList.Items.Clear();
                status.Text = "";
                var find = findBox.Text ?? "";
                if (string.IsNullOrEmpty(find))
                {
                    status.Text = "Enter a search term.";
                    return;
                }

                var scopeControls = scopeCurrent.IsChecked == true && state.Selected is not null
                    ? candidates.Where(c => c.Control.InputKey == state.Selected.InputKey).ToList()
                    : candidates;

                var matches = new List<(DcsDraftAssignmentSlot Slot, string Old, string New)>();
                try
                {
                    var re = regexCheck.IsChecked == true
                        ? new Regex(find, RegexOptions.None, TimeSpan.FromSeconds(1)) : null;
                    foreach (var (control, slot, current) in scopeControls)
                    {
                        string next;
                        if (re is not null)
                        {
                            if (!re.IsMatch(current)) continue;
                            next = re.Replace(current, replaceBox.Text ?? "");
                        }
                        else
                        {
                            if (!current.Contains(find, StringComparison.OrdinalIgnoreCase)) continue;
                            next = current.Replace(find, replaceBox.Text ?? "", StringComparison.OrdinalIgnoreCase);
                        }
                        if (next == current) continue;
                        matches.Add((slot, current, next));
                        previewList.Items.Add($"{control.Label}  →  {next}");
                    }
                }
                catch (RegexMatchTimeoutException)
                {
                    previewList.Items.Clear();
                    status.Text = "Search timed out. Simplify the regular expression and try again.";
                    return;
                }
                catch (ArgumentException ex)
                {
                    previewList.Items.Clear();
                    status.Text = "Invalid regex or replacement: " + ex.Message;
                    return;
                }

                status.Text = matches.Count == 0
                    ? "No matches."
                    : $"{matches.Count} label{(matches.Count == 1 ? "" : "s")} will change.";
                previewList.Tag = matches;
                apply.IsEnabled = matches.Count > 0;
            }

            findBox.TextChanged += (_, _) => RefreshPreview();
            replaceBox.TextChanged += (_, _) => RefreshPreview();
            regexCheck.Checked += (_, _) => RefreshPreview();
            regexCheck.Unchecked += (_, _) => RefreshPreview();
            scopeCurrent.Checked += (_, _) => RefreshPreview();
            scopeAll.Checked += (_, _) => RefreshPreview();

            var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 80 };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            buttons.Children.Add(apply);
            buttons.Children.Add(cancel);

            var content = new StackPanel { Margin = new Thickness(16) };
            content.Children.Add(new TextBlock { Text = "Search for:" });
            content.Children.Add(findBox);
            content.Children.Add(new TextBlock { Text = "Replace with:" });
            content.Children.Add(replaceBox);
            content.Children.Add(regexCheck);
            content.Children.Add(new TextBlock { Text = "Scope:" });
            content.Children.Add(scopePanel);
            content.Children.Add(new TextBlock { Text = "Preview:" });
            content.Children.Add(previewList);
            content.Children.Add(status);
            content.Children.Add(buttons);

            var dialog = new AppearanceDialog("SearchReplaceDialog")
            {
                Title = "Search & replace labels",
                Owner = Window.GetWindow(owner),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.WidthAndHeight,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                Content = content,
                MinWidth = 420
            };

            KeyValuePair<string, string?>[]? updates = null;
            apply.Click += (_, _) =>
            {
                RefreshPreview();
                if (previewList.Tag is not List<(DcsDraftAssignmentSlot Slot, string Old, string New)> matches || matches.Count == 0)
                {
                    return;
                }
                updates = matches.Select(match => new KeyValuePair<string, string?>(match.Slot.Key, match.New)).ToArray();
                dialog.DialogResult = true;
            };

            dialog.Loaded += (_, _) => { findBox.Focus(); RefreshPreview(); };
            var result = editLabels("", () => dialog.ShowDialog() == true
                    ? new(true, updates is { Length: > 0 }, "") : new(false, false, "Label change canceled."),
                () => context.SetLabels(updates!));
            if (result.Outcome == DraftOutcome.Applied)
            {
                refresh();
                SelectedCallout.Text = "Labels updated.";
            }
        }

        internal void LabelChanged(object sender, TextChangedEventArgs e)
        {
            if (state.UpdatingLabel || state.Selected is null || state.Context?.Working is null) return;
            if (state.Diagram is not UiLayerDiagram diagram) return;

            var layer = state.Layer;
            var chord = state.Chord;
            var slot = DcsKneeboardDraftProjection.SlotForControl(state.Context.Working, diagram, state.Selected, layer, chord);
            if (slot is null) return;

            var context = state.Context;
            var text = LabelEditor.Text;
            var result = editLabels(slot.Key, () => state.ReservationError(diagram, slot.InputKey) is { } blocked
                ? new(false, false, blocked) : new(true, true, ""), () => context.SetLabel(slot.Key, text));
            if (result.Outcome == DraftOutcome.Applied) refresh();
        }
        internal void LoadAuthoredLabelsClicked(object sender, RoutedEventArgs e)
        {
            if (state.Context is not { Working: { } workspace } context) return;

            var picker = new ThemeBrowseDialog
            {
                Title = "Select a module config/kneeboard.json file",
                Filter = "kneeboard.json|kneeboard.json|JSON files|*.json"
            };
            KeyValuePair<string, string?>[]? updates = null;
            try
            {
                var result = editLabels("", () =>
                {
                    if (picker.ShowDialog(Window.GetWindow(owner)) != true) return new(false, false, "Label import canceled.");
                    using var document = JsonDocument.Parse(File.ReadAllText(picker.FileName));
                    var metadata = new Dictionary<string, JsonElement> { ["config/kneeboard.json"] = document.RootElement };
                    updates = DcsKneeboardLabelImporter.Import(metadata, workspace.Configuration)
                        .Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value)).ToArray();
                    return updates.Length > 0 ? new(true, true, "") : new(false, false, "No authored labels matched this module.");
                }, () => context.SetLabels(updates!));
                if (result.Outcome != DraftOutcome.Applied) return;
                refresh();
                SelectedCallout.Text = $"{updates!.Length} authored labels staged. Save to keep them, or Discard draft.";
            }
            catch (Exception ex)
            {
                SelectedCallout.Text = "Unable to load authored labels: " + ex.Message;
            }
        }

        internal void RenameGenericControlClicked(object sender, RoutedEventArgs e)
        {
            if (state.Context is null || state.Selected is null || state.Diagram is not UiLayerDiagram diagram) return;
            if (diagram.GenericCapabilities is null)
            {
                SelectedCallout.Text = "Rename is only available for generic inventory devices.";
                return;
            }

            var key = state.Selected.InputKey;
            var label = GenericControlName.Text;
            state.Context.RunStructureEdit(workspace =>
                DcsInventoryHardwareAuthoring.RenameControl(workspace, diagram.ProfileId, key, label));

            refresh();
            SelectedCallout.Text = "Generic control renamed and staged.";
        }
        #endregion END Public
    }
}

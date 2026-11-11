using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.App.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Dcs;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Heimdall.App.Features.Assignments
{
    /// <summary>Assembles the DCS assignment panels and supplies native scope/capability hooks to the shared view.</summary>
    public partial class DcsAssignmentsView : GameAssignmentsView
    {
        private readonly DcsAssignmentViewState _state;
        private readonly DcsAssignmentInspector _inspector;
        private readonly DcsAssignmentEditing _editing;
        private readonly DcsAssignmentLabels _labels;
        private readonly DcsAssignmentDragDrop _drag;
        private readonly DcsAssignmentDiagram _diagram;
        private readonly DcsAssignmentPreview _preview;
        private readonly DiagramViewport _viewport;
        private TextBlock SelectedCallout => SelectionEditor.AssignmentDraftStatus;
        private Button? TuneAxisButton => ActionBar.FindButton(this, "Tune axis…");
        private Button? AssignButton => ActionBar.FindButton(this, "Assign selected command");
        private const string BaseLayerName = "Base", LayerSeparator = " + ", LayerSeparatorTrim = "+", NoWorkingSnapshotText = "No working snapshot";
        public DcsAssignmentsView()
        {
            _state = new(() => DeviceChoice?.SelectedItem as UiLayerDiagram,
                () => Scope?.SelectedIndex == 1 ? AssignmentLayer.UiLayer : AssignmentLayer.Module,
                () => Layers?.SelectedItem as string);
            AppearanceManager.ObserveArtwork(this, RefreshDiagram);
            InitializeComponent();
            RegisterName("SelectedCallout", SelectedCallout);
            AssignmentWorkspace.Name = "AssignmentWorkspace";
            RegisterName("AssignmentWorkspace", AssignmentWorkspace);
            WorkspaceLayout.SetKey(AssignmentWorkspace, "assignments.split");
            ControllerHeader.SizeChanged += (_, _) => ControllerReserveHeight = ControllerHeader.ActualHeight + 160;
            AssignmentWorkspace.AllowDrop = true;
            _inspector = new(_state, InputPicker, SelectedCallout, InspectorDetails, LabelEditor, GenericControlName,
                RenameGenericControlButton, RefreshAssignmentActions);
            _labels = new(_state, this, LabelEditor, GenericControlName, SelectedCallout, RefreshDiagram);
            _editing = new(_state, this, SelectedCallout, () => CommandManager.SelectedCommand, RefreshDiagram,
                _inspector.ClearLabel, AssignmentContext, CaptureAssignmentSnapshot, ExecuteNativeAssignment);
            _drag = new(_state, this, AssignmentWorkspace, SelectedCallout, _editing, RefreshDiagram);
            _diagram = new(_state, this, Diagram, Zoom, DeviceTitle, InputPicker, _inspector, _drag, _labels,
                _editing, AssignmentContext, AssignmentAction);
            _preview = new(_state, this, SelectedCallout);
            _viewport = new DiagramViewport(DiagramScroll, Diagram, Zoom, "assignments.zoom");
            AssignmentWorkspace.PreviewDragOver += _drag.WorkspacePreviewDragOver;
            AssignmentWorkspace.DragOver += _drag.WorkspaceDragOver;
            AssignmentWorkspace.Drop += _drag.WorkspaceDrop;
            CommandManager.SelectedCommandChanged += (_, command) =>
            {
                SelectionEditor.ShowCommandLabel(command?.Name); RefreshAssignmentActions();
            };
        }

        #region BEGIN Public
        /// <summary>Connects the composed native panels and common refresh lifetime to this workspace.</summary>
        public void Initialize(IDcsModuleWorkspaceContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            if (ReferenceEquals(_state.Context, context)) return;
            _state.Context = context;
            BindAssignmentContext(context);
            Scope.SelectedIndex = 0;
            RefreshAssignments();
            CommandManager.Initialize(context);
            BindBrowser(CommandManager.Browser);
            CommandManager.UseAssignmentPalette(_state.Layer);
        }
        internal Action<AxisTuningWindow>? AxisTuningPresenter
        {
            get => _editing.AxisTuningPresenter;
            set => _editing.AxisTuningPresenter = value;
        }
        internal void DropPaletteCommand(HardwareGraphicControl control, DcsCommandPaletteEntry command) => _editing.DropPaletteCommand(control, command);
        #endregion END Public
        protected override WorkspaceAssignmentContext CaptureAssignmentContext()
        {
            if (_state.Context is not { } context) return new("", false, false, false, false);
            if (context.State.Operation is null)
                context.Select(context.State.Selection with
                {
                    ControllerId = _state.Diagram?.ProfileId.ToString("N"),
                    CommandId = CommandManager.SelectedCommand?.Id,
                    NativeScope = AssignmentScope()
                });
            return context.Context;
        }
        protected override string CaptureAssignmentSnapshot() => JsonSerializer.Serialize(new
        {
            Assignments = _state.Context?.Draft?.Snapshot,
            Labels = _state.Context?.Labels.OrderBy(item => item.Key, StringComparer.Ordinal).ToArray()
        });
        /// <inheritdoc/>
        protected override void OnFeatureActiveChanged(bool active) => CommandManager.SetFeatureActive(active);
        protected override Task SaveAssignmentsCoreAsync() => _state.Context?.SaveAsync()
            ?? throw new InvalidOperationException("Open a DCS workspace before saving.");
        protected override void DiscardAssignmentsCore()
        {
            if (_state.Context is null) throw new InvalidOperationException("Open a DCS workspace before discarding.");
            _state.Context.Discard();
        }
        protected override void RefreshAssignmentDrawing() => RefreshDiagram();
        protected override void RefreshAssignmentAvailability() => ShowAssignmentActions();
        protected override void ShowAssignmentStatus(string message) => SelectedCallout.Text = message;
        protected override string SaveSuccessMessage => "Saved to database.";
        protected override string SaveFailurePrefix => "Database save failed: ";
        #region BEGIN private helpers
        private WorkspaceAssignmentContext AssignmentContext() => CurrentAssignmentContext;
        private DraftResult ExecuteNativeAssignment(DcsDraftAssignmentSlot slot, string command, Func<DraftResult> apply) =>
            ExecuteAssignment(new(CaptureAssignmentContext().Scope, command, slot.InputKey, slot.Key), apply);
        private void RefreshDiagram() => _diagram?.RefreshDiagram();
        private void FitDiagramClicked(object sender, RoutedEventArgs e) => _viewport.Fit();
        private void InputPickerChanged(object sender, SelectionChangedEventArgs e) => _diagram?.InputPickerChanged(sender, e);
        private void AssignClicked(object sender, RoutedEventArgs e) => _editing.AssignClicked(sender, e);
        private void ClearClicked(object sender, RoutedEventArgs e) => _editing.ClearClicked(sender, e);
        private void ClearLayerClicked(object sender, RoutedEventArgs e) => _editing.ClearLayerClicked(sender, e);
        private void UndoClicked(object sender, RoutedEventArgs e) => _editing.UndoClicked(sender, e);
        private async void SaveClicked(object sender, RoutedEventArgs e) => await SaveAssignmentsAsync();
        private void DiscardClicked(object sender, RoutedEventArgs e) => DiscardAssignments();
        private void TuneAxisClicked(object sender, RoutedEventArgs e) => _editing.TuneAxisClicked(sender, e);
        private void RenderClicked(object sender, RoutedEventArgs e) => _preview.RenderClicked(sender, e);
        private void LabelChanged(object sender, TextChangedEventArgs e) => _labels?.LabelChanged(sender, e);
        private void SearchReplaceLabelsClicked(object sender, RoutedEventArgs e) => _labels.SearchReplaceLabelsClicked(sender, e);
        private void LoadAuthoredLabelsClicked(object sender, RoutedEventArgs e) => _labels.LoadAuthoredLabelsClicked(sender, e);
        private void RenameGenericControlClicked(object sender, RoutedEventArgs e) => _labels.RenameGenericControlClicked(sender, e);
        protected override void RefreshBrowserEditorCore()
        {
            SelectionEditor.ShowCommandLabel(CommandManager.SelectedCommand?.Name);
            RefreshAssignmentActions();
        }
        protected override void RefreshAssignmentsCore()
        {
            if (_state.Context?.Working is null)
            {
                StatusText.Text = NoWorkingSnapshotText;
                DeviceChoice.ItemsSource = null;
                Layers.ItemsSource = null;
                return;
            }

            var state = DcsWorkspaceStructureEditor.Describe(_state.Context.Working);
            var devices = _state.Context.Working.Diagrams
                .Where(x => state.Devices.Any(d => d.ProfileId == x.ProfileId && d.Layer == _state.Layer))
                .ToArray();

            if (DeviceChoice.ItemsSource is not UiLayerDiagram[] displayed ||
                !displayed.SequenceEqual(devices, ReferenceEqualityComparer.Instance))
            {
                DeviceChoice.ItemsSource = devices;
                DeviceChoice.DisplayMemberPath = "Title";
            }
            DeviceChoice.SelectedItem = devices.FirstOrDefault(x => x.ProfileId == _state.DeviceId)
                ?? (devices.Length > 0 ? devices[0] : null);
            if (_state.Diagram is UiLayerDiagram selected)
                _state.DeviceId = selected.ProfileId;

            var assignments = _state.Context.Draft?.Snapshot ?? _state.Context.Working.Assignments;
            var importedUiChords = _state.Layer == AssignmentLayer.UiLayer &&
                _state.Context.Working.Sources?.ContainsKey("assets/shared/ui-layer/input/UiLayer/modifiers.lua") != true
                ? _state.Context.Working.Diagrams.SelectMany(x => x.Callouts).Select(x => x.Modifier)
                : Enumerable.Empty<string>();
            var choices = Enumerable.Repeat(BaseLayerName, 1)
                .Concat(state.Modifiers.Where(x => x.Layer == _state.Layer).Select(x => x.Name))
                .Concat(assignments.Where(x => x.Slot.Layer == _state.Layer).Select(x => x.Slot.ModifierChord))
                .Concat(importedUiChords)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Replace(LayerSeparator, LayerSeparatorTrim, StringComparison.Ordinal)
                    .Replace(LayerSeparatorTrim, LayerSeparator, StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var selectedLayer = Layers.SelectedItem as string;
            if (!(Layers.ItemsSource as string[] ?? []).SequenceEqual(choices))
            {
                Layers.ItemsSource = choices;
                Layers.SelectedItem = choices.Contains(selectedLayer) ? selectedLayer : selectedLayer is not null && selectedLayer != "Base" && choices.Length > 1 ? choices[1] : choices[0];
            }

            CommandManager.UseAssignmentController(_state.DeviceId);
            StatusText.Text = $"{devices.Length} devices · layer {Layers.SelectedItem}";
            RefreshDiagram();
        }
        private void DeviceChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AssignmentRefreshRunning) return;
            if (_state.Diagram is UiLayerDiagram d)
            {
                if (_state.DeviceId != d.ProfileId) _inspector.Clear();
                _state.DeviceId = d.ProfileId;
            }
            RefreshAssignments();
        }
        private void ScopeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AssignmentRefreshRunning) return;
            _inspector.Clear();
            RefreshAssignments();
            if (_state.Context is not null) CommandManager.UseAssignmentPalette(_state.Layer);
        }
        private void LayerChanged(object sender, SelectionChangedEventArgs e) { if (AssignmentRefreshRunning) return; _inspector.Clear(); RefreshAssignments(); }

        private void ZoomChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ZoomPercent is null) return;
            ZoomPercent.Text = ((int)(Zoom.Value * 100)).ToString(CultureInfo.InvariantCulture);
            Diagram.LayoutTransform = new ScaleTransform(Zoom.Value, Zoom.Value);
        }

        private void ZoomPercentChanged(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(ZoomPercent.Text, out var percent))
                Zoom.Value = Math.Clamp(percent / 100.0, Zoom.Minimum, Zoom.Maximum);
        }
        private void Zoom_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (Zoom is null) return;
            Zoom.Value = 1.0;
        }
        // Both selection writers must use one scope; differing encodings reset selection on every refresh and
        // re-post context notifications indefinitely, starving lower-priority dispatcher work.
        private string AssignmentScope() =>
            (_state.Context?.Working?.Configuration.Module.Id.ToString("N") ?? "") + ":" + _state.Layer + ":" + Layers.SelectedItem;
        private void SynchronizeAssignmentSelection()
        {
            if (DeviceChoice is null || Layers is null || Scope is null || CommandManager is null) return;
            var commandId = CommandManager.SelectedCommand?.Id;
            AssignmentSelection.Update(() =>
            {
                AssignmentSelection.SelectScope(AssignmentScope());
                AssignmentSelection.SelectController(_state.Diagram?.ProfileId.ToString("N"));
                AssignmentSelection.SelectCommand(commandId);
                AssignmentSelection.SelectInput(_state.Selected?.InputKey);
            });
        }
        private WorkspaceAssignmentActionState AssignmentAction(HardwareGraphicControl? control, WorkspaceAssignmentAction action)
        {
            var diagram = _state.Diagram;
            var chord = _state.Chord;
            var slot = _state.Context?.Working is { } workspace && diagram is not null && control is not null
                ? DcsKneeboardDraftProjection.SlotForControl(workspace, diagram, control, _state.Layer, chord) : null;
            var assigned = slot is not null && _state.Context?.Draft?.Snapshot.Any(item =>
                DcsInputKey.PhysicalSlotKey(item.Slot) == DcsInputKey.PhysicalSlotKey(slot)) == true;
            var reserved = diagram is not null && control is not null ? _state.ReservationError(diagram, control.InputKey) : null;
            var ready = action switch
            {
                WorkspaceAssignmentAction.Apply => slot is not null && reserved is null && CommandManager.SelectedCommand is { } command && command.IsAxis == control!.IsAxis,
                WorkspaceAssignmentAction.Clear => assigned,
                WorkspaceAssignmentAction.TuneAxis => assigned && control?.IsAxis == true && reserved is null,
                WorkspaceAssignmentAction.Inspect => control is not null,
                _ => false
            };
            var supported = action is WorkspaceAssignmentAction.Apply or WorkspaceAssignmentAction.Clear or WorkspaceAssignmentAction.TuneAxis or WorkspaceAssignmentAction.Inspect;
            return WorkspaceAssignmentActionState.For(action, AssignmentContext(), new(supported, ready,
                action != WorkspaceAssignmentAction.Clear && reserved is not null ? reserved : "Select a compatible command and controller input; Clear and Tune require an existing assignment."));
        }
        private void RefreshAssignmentActions()
        {
            SynchronizeAssignmentSelection();
            ShowAssignmentActions();
        }
        private void ShowAssignmentActions()
        {
            if (AssignButton is { } assign) AssignmentActionPresentation.Show(assign, AssignmentAction(_state.Selected, WorkspaceAssignmentAction.Apply));
            if (ActionBar.FindButton(this, "Clear assignment") is { } clear) AssignmentActionPresentation.Show(clear, AssignmentAction(_state.Selected, WorkspaceAssignmentAction.Clear));
            if (TuneAxisButton is { } tune) AssignmentActionPresentation.Show(tune, AssignmentAction(_state.Selected, WorkspaceAssignmentAction.TuneAxis));
        }
        #endregion END private helpers
    }
}




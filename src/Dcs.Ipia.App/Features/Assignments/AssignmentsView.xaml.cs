using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Rendering;
using DcsIpia.Presentation;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace DcsIpia.Features.Assignments
{
    /// <summary>Edits module and UI Layer assignments on device drawings.</summary>
    public partial class AssignmentsView : UserControl
    {

        private IModuleWorkspaceContext? _context;
        private bool _refreshingChoices;
        private Point? _calloutDragStart;
        private HardwareGraphicControl? _calloutDragControl;
        private bool _updatingLabel;
        private string? _selectedInputKey;
        private Guid? _selectedDeviceId;
        private Button? TuneAxisButton => ActionBar.FindButton(this, "Tune axis…");
        private Button? AssignButton => ActionBar.FindButton(this, "Assign selected command");
        private const string BaseLayerName = "Base";
        private const string UiLayerName = "UI Layer";
        private const string ModuleLayerName = "Module Layer";
        private const string LayerSeparator = " + ";
        private const string LayerSeparatorTrim = "+";
        private const string NoLayerName = "No Layer";
        private const string NoDeviceName = "No Device";
        private const string NoAssignmentsText = "No assignments available";
        private const string NoDiagramText = "No diagram available";
        private const string NoCommandsText = "No commands available";
        private const string NoWorkingSnapshotText = "No working snapshot";
        private const string NoDraftSnapshotText = "No draft snapshot";
        private const string NoLayerSelectedText = "No layer selected";
        private const string NoDeviceSelectedText = "No device selected";
        private const string NoAssignmentsForLayerText = "No assignments for selected layer";
        private const string NoDiagramForDeviceText = "No diagram for selected device";
        private const string NoCommandsForLayerText = "No commands for selected layer";
        private const string NoAssignmentsForDeviceText = "No assignments for selected device";
        private const string NoDiagramForLayerText = "No diagram for selected layer";
        private const string NoCommandsForDeviceText = "No commands for selected device";
        private const string NoAssignmentsForLayerAndDeviceText = "No assignments for selected layer and device";
        private const string UnAssignedText = "Unassigned";
        private const string SelectDeviceText = "Select a device";
        private const string UnableToRenderMessage = "Unable to render: ";
        private HardwareGraphicControl? _selected;
        private readonly DiagramViewport _viewport;
        #region BEGIN Public
        public AssignmentsView()
        {
            AppearanceManager.ObserveArtwork(this, RefreshDiagram);
            InitializeComponent();
            _viewport = new DiagramViewport(DiagramScroll, Diagram, Zoom, "assignments.zoom");
        }


        /// <summary>Connects this view to the shared workspace and refreshes its displayed state.</summary>
        public void Initialize(IModuleWorkspaceContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            if (ReferenceEquals(_context, context)) return;
            if (_context is not null) _context.StateChanged -= WorkspaceStateChanged;
            _context = context;
            _context.StateChanged += WorkspaceStateChanged;
            Scope.SelectedIndex = 0;
            RefreshChoices();
            CommandManager.Initialize(context);
            CommandManager.UseAssignmentPalette(CurrentLayer);
        }

        private AssignmentLayer CurrentLayer =>
            Scope.SelectedIndex == 1 ? AssignmentLayer.UiLayer : AssignmentLayer.Module;
        #endregion END Public

        #region BEGIN private helpers
        private void FitDiagramClicked(object sender, RoutedEventArgs e) => _viewport.Fit();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out NativeScreenPoint point);

        private void RefreshChoices()
        {
            if (_refreshingChoices) return;
            _refreshingChoices = true;
            try { RefreshWorkspaceChoices(); }
            finally { _refreshingChoices = false; }
        }

        private void WorkspaceStateChanged(object? sender, EventArgs e) => RefreshChoices();

        private void RefreshWorkspaceChoices()
        {
            if (_context?.Working is null)
            {
                StatusText.Text = NoWorkingSnapshotText;
                DeviceChoice.ItemsSource = null;
                Layers.ItemsSource = null;
                return;
            }

            var state = WorkspaceStructureEditor.Describe(_context.Working);
            var devices = _context.Working.Diagrams
                .Where(x => state.Devices.Any(d => d.ProfileId == x.ProfileId && d.Layer == CurrentLayer))
                .ToArray();

            DeviceChoice.ItemsSource = devices;
            DeviceChoice.DisplayMemberPath = "Title";
            DeviceChoice.SelectedItem = devices.FirstOrDefault(x => x.ProfileId == _selectedDeviceId)
                ?? (devices.Length > 0 ? devices[0] : null);
            if (DeviceChoice.SelectedItem is UiLayerDiagram selected)
                _selectedDeviceId = selected.ProfileId;

            var assignments = _context.Draft?.Snapshot ?? _context.Working.Assignments;
            var importedUiChords = CurrentLayer == AssignmentLayer.UiLayer &&
                _context.Working.Sources?.ContainsKey("assets/shared/ui-layer/input/UiLayer/modifiers.lua") != true
                ? _context.Working.Diagrams.SelectMany(x => x.Callouts).Select(x => x.Modifier)
                : Enumerable.Empty<string>();
            var choices = Enumerable.Repeat(BaseLayerName, 1)
                .Concat(state.Modifiers.Where(x => x.Layer == CurrentLayer).Select(x => x.Name))
                .Concat(assignments.Where(x => x.Slot.Layer == CurrentLayer).Select(x => x.Slot.ModifierChord))
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

            StatusText.Text = $"{devices.Length} devices · layer {Layers.SelectedItem}";
            RefreshDiagram();
        }

        private void DeviceChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_refreshingChoices) return;
            if (DeviceChoice.SelectedItem is UiLayerDiagram d)
                _selectedDeviceId = d.ProfileId;
            RefreshChoices();
        }
        private void ScopeChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshChoices();
            if (_context is not null) CommandManager.UseAssignmentPalette(CurrentLayer);
        }
        private void LayerChanged(object sender, SelectionChangedEventArgs e) => RefreshChoices();

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
        private IReadOnlyList<WorkspaceModifier> Reserved(UiLayerDiagram diagram, string key) =>
            _context?.Working is { } workspace
                ? ModifierAssignmentReservation.Find(WorkspaceStructureEditor.Describe(workspace), diagram.ProfileId, key)
                : [];

        private string? ReservationError(UiLayerDiagram diagram, string key)
        {
            var held = Reserved(diagram, key);
            return held.Count == 0 ? null : ModifierAssignmentReservation.BlockReason(key, held);
        }

        private void RefreshDiagram()
        {
            Diagram.Children.Clear();
            if (_context?.Working is null || DeviceChoice.SelectedItem is not UiLayerDiagram diagram ||
                string.IsNullOrWhiteSpace(diagram.Svg))
            {
                DeviceTitle.Text = SelectDeviceText;
                return;
            }

            DeviceTitle.Text = diagram.Title;
            try
            {
                var snapshot = _context.Draft?.Snapshot ?? _context.Working.Assignments;
                var colorPage = KneeboardDraftProjection.CreateCombined(_context.Working, diagram, snapshot, _context.Labels);
                var previewSvg = diagram.Svg;
                if (diagram.Svg.Contains("meta-mfd-category-", StringComparison.Ordinal))
                {
                    // Apply saved MFD side headings; interactive controls supply assignment text below.
                    var categoryPage = colorPage with
                    {
                        Labels = new Dictionary<string, IReadOnlyList<KneeboardLabelVariant>>()
                    };
                    previewSvg = CompositeKneeboardRenderer.BuildDeviceSvg(diagram, categoryPage);
                }
                var png = CanonicalSvgRenderer.Render(PreviewArtwork.Prepare(previewSvg, AppearanceManager.ArtworkOpacityFor(this)), diagram.Width, diagram.Height);
                using var stream = new MemoryStream(png);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();

                Diagram.Width = diagram.Width;
                Diagram.Height = diagram.Height;
                Diagram.LayoutTransform = new ScaleTransform(Zoom.Value, Zoom.Value);
                var surface = new Canvas { Width = diagram.Width, Height = diagram.Height };
                surface.Children.Add(new Image { Source = bitmap, Width = diagram.Width, Height = diagram.Height });
                Diagram.Children.Add(surface);

                var layer = CurrentLayer;
                var chord = Layers.SelectedItem as string == BaseLayerName ? "" : (Layers.SelectedItem as string ?? "").Replace(LayerSeparator, LayerSeparatorTrim, StringComparison.Ordinal);
                var management = WorkspaceStructureEditor.Describe(_context.Working);

                foreach (var control in diagram.Controls)
                {
                    var slot = KneeboardDraftProjection.SlotForControl(_context.Working, diagram, control, layer, chord);
                    if (slot is null) continue;

                    var assignment = snapshot.FirstOrDefault(x =>
                        DcsInputKey.PhysicalSlotKey(x.Slot) == DcsInputKey.PhysicalSlotKey(slot));
                    var reserved = ModifierAssignmentReservation.Find(management, diagram.ProfileId, control.InputKey);
                    var protectedInput = reserved.Count != 0;
                    var modifierDescription = string.Join("\n", reserved.Select(ModifierAssignmentReservation.Description));
                    var caption = protectedInput ? "MODIFIER\n" + string.Join(", ", reserved.Select(x => x.Name))
                        : assignment is null ? UnAssignedText
                        : _context.Labels.GetValueOrDefault(slot.Key) ?? assignment.Command.Name;

                    var variantColor = colorPage.Labels.GetValueOrDefault(control.Control)?.FirstOrDefault(x =>
                        x.Scope == layer && x.ModifierChord == slot.ModifierChord)?.Color
                        ?? colorPage.Legend.FirstOrDefault(x => x.Layer == layer && x.Chord == slot.ModifierChord)?.Color
                        ?? (layer == AssignmentLayer.UiLayer && slot.ModifierChord.Length == 0
                            ? KneeboardStateColors.UiBase : KneeboardStateColors.UnusedModifier);
                    var ink = new SolidColorBrush((Color)ColorConverter.ConvertFromString(
                        KneeboardStateColors.Resolve(layer, slot.ModifierChord, variantColor)));
                    var text = new TextBlock
                    {
                        Foreground = ink,
                        // White glyph edges preserve the functional ink over opaque controls as well as artwork.
                        Effect = new DropShadowEffect { Color = ((SolidColorBrush)FindResource("InputOutlineLightBrush")).Color,
                            BlurRadius = 2, ShadowDepth = 0, Opacity = 1 },
                        Text = caption,
                        TextAlignment = TextAlignment.Center,
                        TextWrapping = TextWrapping.Wrap,
                        VerticalAlignment = VerticalAlignment.Center,
                        FontSize = 11,
                        FontWeight = protectedInput ? FontWeights.Bold : FontWeights.Normal
                    };
                    var border = new ContrastOutlineBorder
                    {
                        BorderBrush = ink,
                        Width = control.Width,
                        Height = control.Height,
                        BorderThickness = new Thickness(protectedInput ? 2 : 1),
                        ToolTip = protectedInput ? modifierDescription +
                            "\nChange or remove the modifier in Modifiers before assigning." : null,
                        Child = text,
                        Tag = control
                    };
                    border.SetResourceReference(Border.BackgroundProperty, protectedInput ? "BadgeBackgroundBrush" : "ControlBackgroundBrush");
                    Canvas.SetLeft(border, control.X);
                    Canvas.SetTop(border, control.Y);
                    surface.Children.Add(border);
                    border.Cursor = Cursors.Hand;
                    border.AllowDrop = !protectedInput;
                    border.DragOver += (_, args) =>
                    {
                        var ok = !protectedInput && (args.Data.GetDataPresent(typeof(CommandPaletteEntry)) ||
                                 args.Data.GetDataPresent(typeof(EditorControlDrag)));
                        args.Effects = ok ? DragDropEffects.Copy : DragDropEffects.None;
                        if (ok && args.Data.GetDataPresent(typeof(EditorControlDrag)))
                            args.Effects = (Keyboard.Modifiers & ModifierKeys.Control) != 0 ? DragDropEffects.Copy : DragDropEffects.Move;
                        args.Handled = true;
                    };
                    border.Drop += (_, args) => DropOnCallout(control, args);

                    void SelectCallout()
                    {
                        _selected = control;
                        _selectedInputKey = control.InputKey;
                        SelectedCallout.Text = protectedInput
                            ? $"{control.Label} · {control.InputKey} · MODIFIER (reserved)"
                            : $"{control.Label} · {control.InputKey} → {caption}";
                        var selectedSlot = KneeboardDraftProjection.SlotForControl(
                            _context!.Working!, diagram, control, CurrentLayer,
                            Layers.SelectedItem as string == "Base" ? "" : (Layers.SelectedItem as string ?? "").Replace(" + ", "+", StringComparison.Ordinal));

                        var pendingAssignment = selectedSlot is null ? null : _context.Draft?.Snapshot.FirstOrDefault(x =>
                            DcsInputKey.PhysicalSlotKey(x.Slot) == DcsInputKey.PhysicalSlotKey(selectedSlot));

                        var original = selectedSlot is null ? null : _context.Saved?.Assignments.FirstOrDefault(x =>
                            DcsInputKey.PhysicalSlotKey(x.Slot) == DcsInputKey.PhysicalSlotKey(selectedSlot));

                        InspectorDetails.Text = selectedSlot is null
                            ? ""
                            : $"Input: {control.InputKey}   Chord: {(selectedSlot.ModifierChord.Length == 0 ? "Base" : selectedSlot.ModifierChord)}\n" +
                              $"Saved: {original?.Command.Name ?? "Unassigned"}   Pending: {pendingAssignment?.Command.Name ?? "Unassigned"}\n" +
                              $"Default label: {control.Label}";
                        if (protectedInput)
                            InspectorDetails.Text += "\n" + modifierDescription +
                                "\nReserved: change or remove it in Modifiers before assigning a command." +
                                (pendingAssignment is null ? "" : "\nExisting assignment can still be cleared.");
                        if (AssignButton is { } assignButton) assignButton.IsEnabled = !protectedInput;
                        TuneAxisButton?.IsEnabled = !protectedInput && control.IsAxis && pendingAssignment is not null;
                        _updatingLabel = true;
                        LabelEditor.Text = protectedInput || caption == UnAssignedText ? "" : caption;
                        LabelEditor.IsEnabled = !protectedInput;
                        _updatingLabel = false;
                        var isGeneric = diagram.GenericCapabilities is not null;
                        GenericControlName.IsEnabled = isGeneric;
                        RenameGenericControlButton.IsEnabled = isGeneric;
                        GenericControlName.Text = isGeneric ? control.Label : "";
                    }

                    border.PreviewMouseLeftButtonDown += (_, args) =>
                    {
                        _calloutDragStart = args.GetPosition(border);
                        _calloutDragControl = control;
                        SelectCallout();
                    };

                    var changeLabel = new MenuItem { Header = "Change label…" };
                    changeLabel.Click += (_, _) => ChangeCalloutLabel(control);
                    var clearAssignment = new MenuItem { Header = "Clear assignment" };
                    clearAssignment.Click += (sender, args) => ClearClicked(sender, args);
                    var tuneAxis = new MenuItem { Header = "Tune axis…" };
                    tuneAxis.Click += (sender, args) => TuneAxisClicked(sender, args);

                    var menu = new ContextMenu();
                    menu.Items.Add(changeLabel);
                    menu.Items.Add(clearAssignment);
                    if (control.IsAxis)
                    {
                        menu.Items.Add(new Separator());
                        menu.Items.Add(tuneAxis);
                    }
                    border.ContextMenu = menu;
                    border.ContextMenuOpening += (_, _) =>
                    {
                        _calloutDragStart = null;
                        _calloutDragControl = null;
                        SelectCallout();
                        var currentAssignment = _context?.Draft?.Snapshot.FirstOrDefault(x =>
                            DcsInputKey.PhysicalSlotKey(x.Slot) == DcsInputKey.PhysicalSlotKey(slot));
                        changeLabel.IsEnabled = !protectedInput && _context?.Working is not null;
                        clearAssignment.IsEnabled = currentAssignment is not null;
                        tuneAxis.IsEnabled = !protectedInput && control.IsAxis && currentAssignment is not null;
                    };
                    border.PreviewMouseMove += (_, args) =>
                    {
                        if (_calloutDragStart is not Point start || _calloutDragControl != control || args.LeftButton != MouseButtonState.Pressed)
                            return;
                        var pos = args.GetPosition(border);
                        if (Math.Abs(pos.X - start.X) < SystemParameters.MinimumHorizontalDragDistance &&
                            Math.Abs(pos.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance)
                            return;
                        _calloutDragStart = null;
                        CalloutDrag(border, control, args);
                    };
                }
                if (_selectedInputKey is not null)
                {
                    var match = diagram.Controls.FirstOrDefault(x => x.InputKey == _selectedInputKey);
                    if (match is not null)
                    {
                        _selected = match;
                        // re-apply the same selection UI you already have
                        var currentReservation = Reserved(diagram, match.InputKey);
                        SelectedCallout.Text = currentReservation.Count > 0
                            ? $"{match.Label} · {match.InputKey} · MODIFIER (reserved)"
                            : $"{match.Label} · {match.InputKey}";
                        if (AssignButton is { } assignButton) assignButton.IsEnabled = currentReservation.Count == 0;
                        LabelEditor.IsEnabled = currentReservation.Count == 0;
                        GenericControlName.Text = diagram.GenericCapabilities is not null ? match.Label : "";
                    }
                }
            }
            catch (Exception ex)
            {
                DeviceTitle.Text = UnableToRenderMessage + ex.Message;
            }
        }

        private void AssignClicked(object sender, RoutedEventArgs e)
        {
            if (_context?.Working is { } workspace && _selected is { } selected &&
                DeviceChoice.SelectedItem is UiLayerDiagram target &&
                ReservationError(target, selected.InputKey) is { } reason)
            {
                SelectedCallout.Text = reason;
                return;
            }
            if (_context?.Draft is null || _selected is null || CommandManager.SelectedCommand is not { } command)
            {
                SelectedCallout.Text = "Select a callout and a command.";
                return;
            }
            if (DeviceChoice.SelectedItem is not UiLayerDiagram diagram) return;

            var layer = CurrentLayer;
            var chord = Layers.SelectedItem as string == "Base" ? "" : (Layers.SelectedItem as string ?? "").Replace(" + ", "+", StringComparison.Ordinal);
            var slot = KneeboardDraftProjection.SlotForControl(_context.Working!, diagram, _selected, layer, chord);
            if (slot is null)
            {
                SelectedCallout.Text = "No slot for the selected control.";
                return;
            }
            var result = AssignWithConfirmation(slot, new DraftCommand(command.Id, command.Name, command.IsAxis, layer));
            if (result.Outcome == DraftOutcome.Applied)
            {
                RefreshDiagram();
                SelectedCallout.Text = $"{_selected.Label} assigned to {command.Name} (staged).";

            }
            else
            {
                SelectedCallout.Text = result.Message;
            }
        }

        private void ClearClicked(object sender, RoutedEventArgs e)
        {
            if (_context?.Draft is null || _selected is null)
            {
                SelectedCallout.Text = "Select a callout to clear.";
                return;
            }

            if (DeviceChoice.SelectedItem is not UiLayerDiagram diagram) return;

            var layer = CurrentLayer;
            var chord = Layers.SelectedItem as string == "Base" ? "" : (Layers.SelectedItem as string ?? "").Replace(" + ", "+", StringComparison.Ordinal);
            var slot = KneeboardDraftProjection.SlotForControl(_context.Working!, diagram, _selected, layer, chord);
            if (slot is null) return;

            var result = _context.Draft.Remove(slot);
            if (result.Outcome == DraftOutcome.Applied)
            {
                _context.SetLabel(slot.Key, null);
                _updatingLabel = true;
                LabelEditor.Text = "";
                _updatingLabel = false;
                RefreshDiagram();
                SelectedCallout.Text = $"{slot.InputKey} cleared (staged).";
            }
            else
            {
                SelectedCallout.Text = result.Message;
            }
        }

        private void ClearLayerClicked(object sender, RoutedEventArgs e)
        {
            if (_context?.Working is null || DeviceChoice.SelectedItem is not UiLayerDiagram diagram) return;

            var device = WorkspaceStructureEditor.Describe(_context.Working).Devices
                .FirstOrDefault(x => x.ProfileId == diagram.ProfileId && x.Layer == CurrentLayer);
            if (device is null)
            {
                SelectedCallout.Text = "Select a device to clear.";
                return;
            }

            var layerName = Layers.SelectedItem as string == "Base" ? "" : Layers.SelectedItem as string ?? "";
            _context.RunStructureEdit(workspace =>
                WorkspaceStructureEditor.ClearLayerAssignments(workspace, device, layerName));

            RefreshDiagram();
            SelectedCallout.Text = "Layer assignments cleared and staged.";
        }

        private TitleAdorner? _calloutAdorner;
        private AdornerLayer? _calloutLayer;

        private void CalloutDrag(Border source, HardwareGraphicControl control, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _context?.Draft is null) return;
            if (DeviceChoice.SelectedItem is not UiLayerDiagram diagram) return;

            var layer = CurrentLayer;
            var chord = Layers.SelectedItem as string == "Base" ? "" : (Layers.SelectedItem as string ?? "").Replace(" + ", "+", StringComparison.Ordinal);
            var slot = KneeboardDraftProjection.SlotForControl(_context.Working!, diagram, control, layer, chord);
            if (slot is null) return;

            var assignment = _context.Draft.Snapshot.FirstOrDefault(x =>
                DcsInputKey.PhysicalSlotKey(x.Slot) == DcsInputKey.PhysicalSlotKey(slot));
            if (assignment is null) return;

            // Adorn the unscaled workspace, not the Window or scaled Diagram.
            // The local AdornerDecorator guarantees a layer inside this view.
            _calloutLayer = AdornerLayer.GetAdornerLayer(AssignmentWorkspace);
            _calloutAdorner = new TitleAdorner(AssignmentWorkspace, control.Label)
            {
                IsHitTestVisible = false
            };
            _calloutAdorner.Update(e.GetPosition(AssignmentWorkspace));

            GiveFeedbackEventHandler feedback = CalloutGiveFeedback;
            source.AddHandler(DragDrop.GiveFeedbackEvent, feedback, true);
            try
            {
                _calloutLayer?.Add(_calloutAdorner);
                DragDrop.DoDragDrop(source,
                    new DataObject(typeof(EditorControlDrag), new EditorControlDrag(slot)),
                    DragDropEffects.Move | DragDropEffects.Copy);
            }
            finally
            {
                source.RemoveHandler(DragDrop.GiveFeedbackEvent, feedback);
                if (_calloutAdorner is not null)
                    _calloutLayer?.Remove(_calloutAdorner);
                _calloutAdorner = null;
                _calloutLayer = null;
                _calloutDragStart = null;
                _calloutDragControl = null;
            }
        }

        private void WorkspacePreviewDragOver(object sender, DragEventArgs e)
        {
            // Observe the preview event before a callout handles bubbling DragOver.
            // Leave Effects and Handled to the actual drop target.
            if (_calloutAdorner is not null && e.Data.GetDataPresent(typeof(EditorControlDrag)))
                _calloutAdorner.Update(e.GetPosition(AssignmentWorkspace));
        }

        private void WorkspaceDragOver(object sender, DragEventArgs e)
        {
            // Only reached when a callout has not already handled the event.
            e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void WorkspaceDrop(object sender, DragEventArgs e)
        {
            // Blank workspace is not an assignment target.
            e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void CalloutGiveFeedback(object sender, GiveFeedbackEventArgs e)
        {
            // Feedback is raised on the drag source by the OLE drag loop.
            // Read the cursor here on the UI thread; WPF mouse positions can
            // be stale while DoDragDrop owns mouse input.
            if (_calloutAdorner is not null && GetCursorPos(out var point))
            {
                var position = AssignmentWorkspace.PointFromScreen(new Point(point.X, point.Y));
                _calloutAdorner.Update(position);
            }

            e.UseDefaultCursors = false;
            Mouse.SetCursor(e.Effects == DragDropEffects.None ? Cursors.No : Cursors.Arrow);
            e.Handled = true;
        }

        private void DropOnCallout(HardwareGraphicControl control, DragEventArgs e)
        {
            if (_context?.Draft is null || DeviceChoice.SelectedItem is not UiLayerDiagram diagram) return;
            if (ReservationError(diagram, control.InputKey) is { } blocked)
            {
                SelectedCallout.Text = blocked;
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            var layer = CurrentLayer;
            var chord = Layers.SelectedItem as string == "Base" ? "" : (Layers.SelectedItem as string ?? "").Replace(" + ", "+", StringComparison.Ordinal);
            var destination = KneeboardDraftProjection.SlotForControl(_context.Working!, diagram, control, layer, chord);
            if (destination is null) return;

            if (e.Data.GetData(typeof(EditorControlDrag)) is EditorControlDrag sourceDrag)
            {
                var copy = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
                var sourceReservation = ReservationError(diagram, sourceDrag.Slot.InputKey);
                if (copy && sourceReservation is not null)
                {
                    SelectedCallout.Text = "Copying an assignment from a reserved modifier is not permitted. " +
                        sourceReservation;
                    e.Effects = DragDropEffects.None;
                    e.Handled = true;
                    return;
                }
                var result = copy
                    ? _context.Draft.Copy(sourceDrag.Slot, destination, OccupiedDestinationBehavior.Ask, null)
                    : _context.Draft.Move(sourceDrag.Slot, destination, OccupiedDestinationBehavior.Ask, null);

                if (result.Outcome == DraftOutcome.ConfirmationRequired)
                {
                    var decision = ThemeMessageBox.Show(Window.GetWindow(this),
                        "Destination is occupied. Yes: replace; No: swap; Cancel: keep both.",
                        "Occupied control", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                    var choice = decision switch
                    {
                        MessageBoxResult.Yes => DraftConflictChoice.Replace,
                        MessageBoxResult.No => DraftConflictChoice.Swap,
                        _ => DraftConflictChoice.Cancel
                    };
                    if (choice == DraftConflictChoice.Swap && sourceReservation is not null)
                    {
                        SelectedCallout.Text = "Cannot swap an assignment into a reserved modifier. " + sourceReservation;
                        e.Effects = DragDropEffects.None;
                        e.Handled = true;
                        return;
                    }
                    result = copy
                        ? _context.Draft.Copy(sourceDrag.Slot, destination, OccupiedDestinationBehavior.Ask, choice)
                        : _context.Draft.Move(sourceDrag.Slot, destination, OccupiedDestinationBehavior.Ask, choice);
                }

                if (result.Outcome == DraftOutcome.Applied)
                {
                    if (result.Outcome == DraftOutcome.Applied)
                    {
                        var sourceKey = sourceDrag.Slot.Key;
                        var destKey = destination.Key;
                        var label = _context.Labels.GetValueOrDefault(sourceKey);

                        if (!copy)
                            _context.SetLabel(sourceKey, null);

                        if (label is not null)
                            _context.SetLabel(destKey, label);

                        RefreshDiagram();
                        SelectedCallout.Text = result.Message;
                    }
                }
                else
                {
                    SelectedCallout.Text = result.Message;
                }
            }
            else if (e.Data.GetData(typeof(CommandPaletteEntry)) is CommandPaletteEntry command)
            {
                DropPaletteCommand(control, command);
            }
            e.Handled = true;
        }

        internal void DropPaletteCommand(HardwareGraphicControl control, CommandPaletteEntry command)
        {
            if (_context?.Draft is null || DeviceChoice.SelectedItem is not UiLayerDiagram diagram) return;
            if (ReservationError(diagram, control.InputKey) is { } blocked)
            {
                SelectedCallout.Text = blocked;
                return;
            }
            var layer = CurrentLayer;
            var chord = Layers.SelectedItem as string == "Base" ? "" :
                (Layers.SelectedItem as string ?? "").Replace(" + ", "+", StringComparison.Ordinal);
            var destination = KneeboardDraftProjection.SlotForControl(_context.Working!, diagram, control, layer, chord);
            if (destination is null) return;
            var result = AssignWithConfirmation(destination, new DraftCommand(command.Id, command.Name, command.IsAxis, layer));
            if (result.Outcome == DraftOutcome.Applied)
            {
                _selected = control;
                RefreshDiagram();
                SelectedCallout.Text = $"{control.Label} assigned to {command.Name} (staged).";
            }
            else SelectedCallout.Text = result.Message;
        }

        private DraftResult AssignWithConfirmation(DraftAssignmentSlot slot, DraftCommand command)
        {
            if (_context?.Draft is null)
                return new(DraftOutcome.Blocked, "No draft.");
            if (_context.Working is { } workspace &&
                workspace.Diagrams.FirstOrDefault(x => x.ProfileId == slot.ProfileId) is { } diagram &&
                ReservationError(diagram, slot.InputKey) is { } blocked)
                return new(DraftOutcome.Blocked, blocked);

            var result = _context.Draft.Assign(slot, command, OccupiedDestinationBehavior.Ask, null);
            if (result.Outcome != DraftOutcome.ConfirmationRequired)
                return result;

            var decision = ThemeMessageBox.Show(
                Window.GetWindow(this),
                "Destination is occupied. Yes: replace; No: swap; Cancel: keep both.",
                "Occupied control",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            var choice = decision switch
            {
                MessageBoxResult.Yes => DraftConflictChoice.Replace,
                MessageBoxResult.No => DraftConflictChoice.Swap,
                _ => DraftConflictChoice.Cancel
            };
            return _context.Draft.Assign(slot, command, OccupiedDestinationBehavior.Ask, choice);
        }

        private void UndoClicked(object sender, RoutedEventArgs e)
        {
            if (_context?.Draft is null) return;
            var result = _context.Draft.Undo();
            RefreshDiagram();
            SelectedCallout.Text = result.Message;
        }

        private void Zoom_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (Zoom is null) return;
            Zoom.Value = 1.0;
        }

        private void ChangeCalloutLabel(HardwareGraphicControl control)
        {
            if (_context?.Working is null || DeviceChoice.SelectedItem is not UiLayerDiagram diagram)
                return;

            var chord = Layers.SelectedItem as string == BaseLayerName ? ""
                : (Layers.SelectedItem as string ?? "").Replace(LayerSeparator, LayerSeparatorTrim, StringComparison.Ordinal);
            var slot = KneeboardDraftProjection.SlotForControl(
                _context.Working, diagram, control, CurrentLayer, chord);
            if (slot is null) return;

            var assignment = (_context.Draft?.Snapshot ?? _context.Working.Assignments)
                .FirstOrDefault(x => DcsInputKey.PhysicalSlotKey(x.Slot) == DcsInputKey.PhysicalSlotKey(slot));
            var editor = new TextBox
            {
                Text = _context.Labels.GetValueOrDefault(slot.Key) ?? assignment?.Command.Name ?? "",
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
                Owner = Window.GetWindow(this),
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
            if (dialog.ShowDialog() != true) return;

            _context.SetLabel(slot.Key, string.IsNullOrWhiteSpace(editor.Text) ? null : editor.Text);
            _updatingLabel = true;
            try
            {
                LabelEditor.Text = string.IsNullOrWhiteSpace(editor.Text)
                    ? assignment?.Command.Name ?? "" : editor.Text;
            }
            finally
            {
                _updatingLabel = false;
            }
            RefreshDiagram();
            SelectedCallout.Text = "Callout label changed and staged.";
        }


        private void SearchReplaceLabelsClicked(object sender, RoutedEventArgs e)
        {
            if (_context?.Working is null || DeviceChoice.SelectedItem is not UiLayerDiagram diagram)
            {
                SelectedCallout.Text = "Select a device first.";
                return;
            }

            var chord = Layers.SelectedItem as string == BaseLayerName ? ""
                : (Layers.SelectedItem as string ?? "").Replace(LayerSeparator, LayerSeparatorTrim, StringComparison.Ordinal);
            var layer = CurrentLayer;
            var snapshot = _context.Draft?.Snapshot ?? _context.Working.Assignments;

            var candidates = new List<(HardwareGraphicControl Control, DraftAssignmentSlot Slot, string Current)>();
            foreach (var control in diagram.Controls)
            {
                var slot = KneeboardDraftProjection.SlotForControl(_context.Working, diagram, control, layer, chord);
                if (slot is null) continue;
                var assignment = snapshot.FirstOrDefault(x =>
                    DcsInputKey.PhysicalSlotKey(x.Slot) == DcsInputKey.PhysicalSlotKey(slot));
                var current = _context.Labels.GetValueOrDefault(slot.Key) ?? assignment?.Command.Name ?? control.Label ?? "";
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
            var scopeCurrent = new RadioButton { Content = "Current control only", IsChecked = _selected is not null, Margin = new Thickness(0, 0, 12, 0) };
            var scopeAll = new RadioButton { Content = "All controls on this device", IsChecked = _selected is null };
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

                var scopeControls = scopeCurrent.IsChecked == true && _selected is not null
                    ? candidates.Where(c => c.Control.InputKey == _selected.InputKey).ToList()
                    : candidates;

                var matches = new List<(DraftAssignmentSlot Slot, string Old, string New)>();
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
                Owner = Window.GetWindow(this),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.WidthAndHeight,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                Content = content,
                MinWidth = 420
            };

            apply.Click += (_, _) =>
            {
                RefreshPreview();
                if (previewList.Tag is not List<(DraftAssignmentSlot Slot, string Old, string New)> matches || matches.Count == 0)
                {
                    return;
                }
                foreach (var (slot, _, next) in matches)
                    _context.SetLabel(slot.Key, string.IsNullOrWhiteSpace(next) ? null : next);
                dialog.DialogResult = true;
            };

            dialog.Loaded += (_, _) => { findBox.Focus(); RefreshPreview(); };
            if (dialog.ShowDialog() != true) return;

            RefreshDiagram();
            SelectedCallout.Text = "Labels updated.";
        }

        private void LabelChanged(object sender, TextChangedEventArgs e)
        {
            if (_updatingLabel || _selected is null || _context?.Working is null) return;
            if (DeviceChoice.SelectedItem is not UiLayerDiagram diagram) return;

            var layer = CurrentLayer;
            var chord = Layers.SelectedItem as string == "Base" ? "" : (Layers.SelectedItem as string ?? "").Replace(LayerSeparator, LayerSeparatorTrim, StringComparison.Ordinal);
            var slot = KneeboardDraftProjection.SlotForControl(_context.Working, diagram, _selected, layer, chord);
            if (slot is null) return;

            _context.SetLabel(slot.Key, LabelEditor.Text);
            RefreshDiagram();
        }

        private async void SaveClicked(object sender, RoutedEventArgs e)
        {
            if (_context is null) return;
            try
            {
                await _context.SaveAsync();
                RefreshDiagram();
                SelectedCallout.Text = "Saved to database.";
            }
            catch (Exception ex) { SelectedCallout.Text = "Database save failed: " + ex.Message; }
        }

        private void DiscardClicked(object sender, RoutedEventArgs e)
        {
            _context?.Discard();
            RefreshDiagram();
            SelectedCallout.Text = "Pending edits discarded.";
        }

        internal Action<AxisTuningWindow>? AxisTuningPresenter { get; set; }

        private void TuneAxisClicked(object sender, RoutedEventArgs e)
        {
            if (_context?.Draft is null || _selected is null || !_selected.IsAxis) return;
            if (DeviceChoice.SelectedItem is not UiLayerDiagram diagram) return;

            var layer = CurrentLayer;
            var chord = Layers.SelectedItem as string == "Base" ? "" : (Layers.SelectedItem as string ?? "").Replace(" + ", "+", StringComparison.Ordinal);
            var slot = KneeboardDraftProjection.SlotForControl(_context.Working!, diagram, _selected, layer, chord);
            if (slot is null) return;

            var assignment = _context.Draft.Snapshot.FirstOrDefault(x =>
                DcsInputKey.PhysicalSlotKey(x.Slot) == DcsInputKey.PhysicalSlotKey(slot));
            if (assignment is null)
            {
                SelectedCallout.Text = "Assign a command before tuning.";
                return;
            }

            AxisTuningWindow? dialog = null;
            void Apply()
            {
                var result = _context.Draft.TuneAxis(slot, dialog!.Filter);
                SelectedCallout.Text = result.Message;
                if (result.Outcome == DraftOutcome.Applied) RefreshDiagram();
            }
            dialog = new AxisTuningWindow(assignment.Filter, AxisTuningPresenter is null ? null : Apply)
                { Owner = Window.GetWindow(this) };
            if (AxisTuningPresenter is { } presenter) presenter(dialog);
            else if (dialog.ShowDialog() == true) Apply();
        }

        private void RenderClicked(object sender, RoutedEventArgs e)
        {
            if (_context?.Working is null || _context.Draft is null ||
                DeviceChoice.SelectedItem is not UiLayerDiagram diagram ||
                string.IsNullOrWhiteSpace(diagram.Svg))
            {
                SelectedCallout.Text = "Select a device with an authored graphic to render.";
                return;
            }

            try
            {
                var page = KneeboardDraftProjection.CreateCombined(
                    _context.Working, diagram, _context.Draft.Snapshot, _context.Labels);
                var pages = new StackPanel();
                var view = new AppearanceDialog("CurrentKneeboardDialog")
                {
                    Title = "Current kneeboard · " + diagram.Title,
                    Owner = Window.GetWindow(this), Width = 900, Height = 900,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Content = new ScrollViewer { Content = pages }
                };
                void Redraw()
                {
                    pages.Children.Clear();
                    var images = DeviceKneeboardPages.Render(diagram, page, "DCS-IPIA · current draft preview",
                        AppearanceManager.HasBackground(view), AppearanceManager.ArtworkOpacityFor(view));
                    foreach (var png in images)
                    {
                        using var stream = new MemoryStream(png);
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = stream;
                        bitmap.EndInit(); bitmap.Freeze();
                        pages.Children.Add(new Image { Source = bitmap, Width = 800, Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 0, 12) });
                    }
                }
                AppearanceManager.ObserveArtwork(view, Redraw);
                Redraw();
                view.ShowDialog();
            }
            catch (Exception ex)
            {
                SelectedCallout.Text = "Unable to render kneeboard: " + ex.Message;
            }
        }

        private void LoadAuthoredLabelsClicked(object sender, RoutedEventArgs e)
        {
            if (_context?.Working is null) return;

            var picker = new ThemeBrowseDialog
            {
                Title = "Select a module config/kneeboard.json file",
                Filter = "kneeboard.json|kneeboard.json|JSON files|*.json"
            };
            if (picker.ShowDialog(Window.GetWindow(this)) != true) return;

            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(picker.FileName));
                var metadata = new Dictionary<string, JsonElement> { ["config/kneeboard.json"] = document.RootElement };
                var imported = KneeboardLabelImporter.Import(metadata, _context.Working.Configuration);
                if (imported.Count == 0)
                {
                    SelectedCallout.Text = "No authored labels matched this module.";
                    return;
                }

                foreach (var pair in imported)
                    _context.SetLabel(pair.Key, pair.Value);

                RefreshDiagram();
                SelectedCallout.Text = $"{imported.Count} authored labels staged. Save to keep them, or Discard draft.";
            }
            catch (Exception ex)
            {
                SelectedCallout.Text = "Unable to load authored labels: " + ex.Message;
            }
        }

        private void RenameGenericControlClicked(object sender, RoutedEventArgs e)
        {
            if (_context is null || _selected is null || DeviceChoice.SelectedItem is not UiLayerDiagram diagram) return;
            if (diagram.GenericCapabilities is null)
            {
                SelectedCallout.Text = "Rename is only available for generic inventory devices.";
                return;
            }

            var key = _selected.InputKey;
            var label = GenericControlName.Text;
            _context.RunStructureEdit(workspace =>
                InventoryHardwareAuthoring.RenameControl(workspace, diagram.ProfileId, key, label));

            RefreshDiagram();
            SelectedCallout.Text = "Generic control renamed and staged.";
        }
        #endregion END private helpers
    }
}

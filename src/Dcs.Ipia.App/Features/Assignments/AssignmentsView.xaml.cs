using System.Xml;
using System.Xml.Linq;

using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Rendering;
using DcsIpia.Presentation;
using Microsoft.Win32;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DcsIpia.Features.Assignments
{
    public partial class AssignmentsView : UserControl
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

        private IModuleWorkspaceContext? _context;
        private Point? _calloutDragStart;
        private HardwareGraphicControl? _calloutDragControl;
        private bool _updatingLabel;
        private string? _selectedInputKey;
        private Guid? _selectedDeviceId;
        private Button? TuneAxisButton => ActionBar.FindButton(this, "Tune axis…");
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
        public AssignmentsView() => InitializeComponent();

        public void Initialize(IModuleWorkspaceContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            _context = context;
            _context.StateChanged += (_, _) => RefreshChoices();
            Scope.SelectedIndex = 0;
            RefreshChoices();
            CommandManager.Initialize(context);
        }

        private AssignmentLayer CurrentLayer =>
            Scope.SelectedIndex == 1 ? AssignmentLayer.UiLayer : AssignmentLayer.Module;

        private void RefreshChoices()
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
            if (DeviceChoice.SelectedItem is UiLayerDiagram d)
                _selectedDeviceId = d.ProfileId;
            RefreshChoices();
        }
        private void ScopeChanged(object sender, SelectionChangedEventArgs e) => RefreshChoices();
        private void LayerChanged(object sender, SelectionChangedEventArgs e) => RefreshChoices();

        private void ZoomChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ZoomPercent is null) return;
            ZoomPercent.Text = ((int)(Zoom.Value * 100)).ToString(CultureInfo.InvariantCulture);
            RefreshDiagram();
        }

        private void ZoomPercentChanged(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(ZoomPercent.Text, out var percent))
                Zoom.Value = Math.Clamp(percent / 100.0, Zoom.Minimum, Zoom.Maximum);
        }
        private static void CoverAuthoredLabels(Canvas surface, string svg)
        {
            using var reader = XmlReader.Create(new StringReader(svg), new XmlReaderSettings {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4_000_000
            });
            var root = XDocument.Load(reader, LoadOptions.PreserveWhitespace).Root;
            if (root is null)
                return;
            foreach (var (control, box) in SvgCalloutLocator.Find(root))
            {
                var x = double.Parse(box.Attribute("x")?.Value ?? "0", CultureInfo.InvariantCulture);
                var y = double.Parse(box.Attribute("y")?.Value ?? "0", CultureInfo.InvariantCulture);
                var width = double.Parse(box.Attribute("width")?.Value ?? "0", CultureInfo.InvariantCulture);
                var height = double.Parse(box.Attribute("height")?.Value ?? "0", CultureInfo.InvariantCulture);
                if (width <= 0 || height <= 0)
                    continue;
                var mask =
                    new Border { Width = width,
                                 Height = height,
                                 Background = Brushes.White,
                                 BorderBrush = Brushes.SlateGray,
                                 BorderThickness = new Thickness(1),
                                 Tag = control,
                                 Child = new TextBlock { Text = "Unassigned", TextAlignment = TextAlignment.Center,
                                                         TextWrapping = TextWrapping.Wrap,
                                                         VerticalAlignment = VerticalAlignment.Center, FontSize = 11,
                                                         Foreground = Brushes.Black } };
                Canvas.SetLeft(mask, x);
                Canvas.SetTop(mask, y);
                surface.Children.Add(mask);
            }
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
                var previewSvg = diagram.Svg;
                if (diagram.Svg.Contains("meta-mfd-category-", StringComparison.Ordinal))
                {
                    // Apply saved MFD side headings; interactive controls supply assignment text below.
                    var page = KneeboardDraftProjection.CreateCombined(
                        _context.Working, diagram, _context.Draft?.Snapshot ?? _context.Working.Assignments,
                        _context.Labels);
                    var categoryPage = page with
                    {
                        Labels = new Dictionary<string, IReadOnlyList<KneeboardLabelVariant>>()
                    };
                    previewSvg = CompositeKneeboardRenderer.BuildDeviceSvg(diagram, categoryPage);
                }
                var png = CanonicalSvgRenderer.Render(previewSvg, diagram.Width, diagram.Height);
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
                CoverAuthoredLabels(surface, diagram.Svg);
                Diagram.Children.Add(surface);

                var layer = CurrentLayer;
                var chord = Layers.SelectedItem as string == BaseLayerName ? "" : (Layers.SelectedItem as string ?? "").Replace(LayerSeparator, LayerSeparatorTrim, StringComparison.Ordinal);
                var snapshot = _context.Draft?.Snapshot ?? _context.Working.Assignments;

                foreach (var control in diagram.Controls)
                {
                    var slot = KneeboardDraftProjection.SlotForControl(_context.Working, diagram, control, layer, chord);
                    if (slot is null) continue;

                    var assignment = snapshot.FirstOrDefault(x =>
                        DcsInputKey.PhysicalSlotKey(x.Slot) == DcsInputKey.PhysicalSlotKey(slot));
                    var caption = assignment is null ? UnAssignedText
                        : _context.Labels.GetValueOrDefault(slot.Key) ?? assignment.Command.Name;

                    var text = new TextBlock
                    {
                        Text = caption,
                        TextAlignment = TextAlignment.Center,
                        TextWrapping = TextWrapping.Wrap,
                        VerticalAlignment = VerticalAlignment.Center,
                        FontSize = 11,
                        Foreground = Brushes.Black
                    };
                    var border = new Border
                    {
                        Width = control.Width,
                        Height = control.Height,
                        Background = Brushes.White,
                        BorderBrush = assignment is null ? Brushes.SlateGray : Brushes.White,
                        BorderThickness = new Thickness(1),
                        Child = text,
                        Tag = control
                    };
                    Canvas.SetLeft(border, control.X);
                    Canvas.SetTop(border, control.Y);
                    surface.Children.Add(border);
                    border.Cursor = Cursors.Hand;
                    border.AllowDrop = true;
                    border.DragOver += (_, args) =>
                    {
                        var ok = args.Data.GetDataPresent(typeof(CommandPaletteEntry)) ||
                                 args.Data.GetDataPresent(typeof(EditorControlDrag));
                        args.Effects = ok ? DragDropEffects.Copy : DragDropEffects.None;
                        if (args.Data.GetDataPresent(typeof(EditorControlDrag)))
                            args.Effects = (Keyboard.Modifiers & ModifierKeys.Control) != 0 ? DragDropEffects.Copy : DragDropEffects.Move;
                        args.Handled = true;
                    };
                    border.Drop += (_, args) => DropOnCallout(control, args);

                    void SelectCallout()
                    {
                        _selected = control;
                        _selectedInputKey = control.InputKey;
                        SelectedCallout.Text = $"{control.Label} · {control.InputKey} → {caption}";
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
                        TuneAxisButton?.IsEnabled = control.IsAxis && pendingAssignment is not null;
                        _updatingLabel = true;
                        LabelEditor.Text = caption == UnAssignedText ? "" : caption;
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
                        changeLabel.IsEnabled = _context?.Working is not null;
                        clearAssignment.IsEnabled = currentAssignment is not null;
                        tuneAxis.IsEnabled = control.IsAxis && currentAssignment is not null;
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
                        SelectedCallout.Text = $"{match.Label} · {match.InputKey}";
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

            var layer = CurrentLayer;
            var chord = Layers.SelectedItem as string == "Base" ? "" : (Layers.SelectedItem as string ?? "").Replace(" + ", "+", StringComparison.Ordinal);
            var destination = KneeboardDraftProjection.SlotForControl(_context.Working!, diagram, control, layer, chord);
            if (destination is null) return;

            if (e.Data.GetData(typeof(EditorControlDrag)) is EditorControlDrag sourceDrag)
            {
                var copy = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
                var result = copy
                    ? _context.Draft.Copy(sourceDrag.Slot, destination, OccupiedDestinationBehavior.Ask, null)
                    : _context.Draft.Move(sourceDrag.Slot, destination, OccupiedDestinationBehavior.Ask, null);

                if (result.Outcome == DraftOutcome.ConfirmationRequired)
                {
                    var decision = MessageBox.Show(Window.GetWindow(this),
                        "Destination is occupied. Yes: replace; No: swap; Cancel: keep both.",
                        "Occupied control", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                    var choice = decision switch
                    {
                        MessageBoxResult.Yes => DraftConflictChoice.Replace,
                        MessageBoxResult.No => DraftConflictChoice.Swap,
                        _ => DraftConflictChoice.Cancel
                    };
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
                // existing command-drop path stays here
                var result = AssignWithConfirmation(destination, new DraftCommand(command.Id, command.Name, command.IsAxis, layer));
                if (result.Outcome == DraftOutcome.Applied)
                {
                    _selected = control;
                    RefreshDiagram();
                    SelectedCallout.Text = $"{control.Label} assigned to {command.Name} (staged).";
                }
                else
                {
                    SelectedCallout.Text = result.Message;
                }
            }
            e.Handled = true;
        }

        private DraftResult AssignWithConfirmation(DraftAssignmentSlot slot, DraftCommand command)
        {
            if (_context?.Draft is null)
                return new(DraftOutcome.Blocked, "No draft.");

            var result = _context.Draft.Assign(slot, command, OccupiedDestinationBehavior.Ask, null);
            if (result.Outcome != DraftOutcome.ConfirmationRequired)
                return result;

            var decision = MessageBox.Show(
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
            var dialog = new Window
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

            var dialog = new AxisTuningWindow(assignment.Filter) { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() != true) return;

            var result = _context.Draft.TuneAxis(slot, dialog.Filter);
            SelectedCallout.Text = result.Message;
            if (result.Outcome == DraftOutcome.Applied)
                RefreshDiagram();
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
                var images = DeviceKneeboardPages.Render(diagram, page, "DCS-IPIA · current draft preview");

                var pages = new StackPanel();
                foreach (var png in images)
                {
                    using var stream = new MemoryStream(png);
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    pages.Children.Add(new Image { Source = bitmap, Width = 800, Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 0, 12) });
                }

                var view = new Window
                {
                    Title = "Current kneeboard · " + diagram.Title,
                    Owner = Window.GetWindow(this),
                    Width = 900,
                    Height = 900,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Content = new ScrollViewer { Content = pages }
                };
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

            var picker = new OpenFileDialog
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
    }
}

using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.App.Games;
using Heimdall.Dcs;
using Heimdall.Rendering;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Heimdall.App.Features.Assignments
{
    /// <summary>Builds controller artwork and callouts, wiring native interactions to focused collaborators.</summary>
    internal sealed class DcsAssignmentDiagram(
        DcsAssignmentViewState state,
        FrameworkElement owner,
        Canvas Diagram,
        Slider Zoom,
        TextBlock DeviceTitle,
        AssignmentInputPicker InputPicker,
        DcsAssignmentInspector inspector,
        DcsAssignmentDragDrop drag,
        DcsAssignmentLabels labels,
        DcsAssignmentEditing editing,
        Func<WorkspaceAssignmentContext> AssignmentContext,
        Func<HardwareGraphicControl?, WorkspaceAssignmentAction, WorkspaceAssignmentActionState> actionState)
    {
        private readonly HardwareDiagramArtworkCache _artwork = new(capacity: 12, preserveLabels: false,
            pixelBudget: 64 * 1024 * 1024);
        private readonly Dictionary<string, Action> _inputSelections = new(StringComparer.Ordinal);
        private const string UnAssignedText = "Unassigned", SelectDeviceText = "Select a device", UnableToRenderMessage = "Unable to render: ";
        #region BEGIN Public
        internal void InputPickerChanged(object sender, SelectionChangedEventArgs e)
        {
            if (InputPicker.SelectedValue is string key && key != state.InputKey
                && _inputSelections.TryGetValue(key, out var select))
                select();
        }

        internal void RefreshDiagram()
        {
            Diagram.Children.Clear();
            _inputSelections.Clear();
            InputPicker.ItemsSource = null;
            InputPicker.IsEnabled = false;
            if (state.Context?.Working is null || state.Diagram is not UiLayerDiagram diagram ||
                (diagram.DrawingUnlinked || string.IsNullOrWhiteSpace(diagram.Svg)))
            {
                DeviceTitle.Text = SelectDeviceText;
                inspector.Clear();
                return;
            }

            DeviceTitle.Text = diagram.Title;
            if (state.InputKey is not null && !diagram.Controls.Any(x => x.InputKey == state.InputKey)) inspector.Clear();
            try
            {
                var workspace = state.Context.Working;
                var snapshot = state.Context.Draft?.Snapshot ?? workspace.Assignments;
                KneeboardComposite colorPage;
#if PERFORMANCE_TRACE
                using (PerformanceTrace.Measure("diagram.dcs-projection", GetType()))
#endif
                { colorPage = DcsKneeboardDraftProjection.CreateCombined(workspace, diagram, snapshot, state.Context.Labels); }
                var bitmap = _artwork.GetOrRender(PrepareArtwork(diagram, colorPage), diagram.Width, diagram.Height);

                Diagram.Width = diagram.Width;
                Diagram.Height = diagram.Height;
                Diagram.LayoutTransform = new ScaleTransform(Zoom.Value, Zoom.Value);
                var surface = new Canvas { Width = diagram.Width, Height = diagram.Height };
                surface.Children.Add(new Image { Source = bitmap, Width = diagram.Width, Height = diagram.Height });
                Diagram.Children.Add(surface);

                var layer = state.Layer;
                var chord = state.Chord;
                var management = DcsWorkspaceStructureEditor.Describe(workspace);
#if PERFORMANCE_TRACE
                using var regions = PerformanceTrace.Measure("diagram.hit-regions", GetType());
#endif
                var bySlot = new Dictionary<string, DcsDraftAssignment>(StringComparer.Ordinal);
                foreach (var assignment in snapshot)
                    if (assignment.Slot.ProfileId == diagram.ProfileId && assignment.Slot.Layer == layer)
                        bySlot.TryAdd(DcsInputKey.PhysicalSlotKey(assignment.Slot), assignment);

                foreach (var control in diagram.Controls)
                {
                    var slot = DcsKneeboardDraftProjection.SlotForControl(workspace, diagram, control, layer, chord);
                    if (slot is null) continue;

                    var assignment = bySlot.GetValueOrDefault(DcsInputKey.PhysicalSlotKey(slot));
                    var reserved = DcsModifierAssignmentReservation.Find(management, diagram.ProfileId, control.InputKey);
                    var protectedInput = reserved.Count != 0;
                    var modifierDescription = string.Join("\n", reserved.Select(DcsModifierAssignmentReservation.Description));
                    var caption = protectedInput ? "MODIFIER\n" + string.Join(", ", reserved.Select(x => x.Name))
                        : assignment is null ? UnAssignedText
                        : state.Context.Labels.GetValueOrDefault(slot.Key) ?? assignment.Command.Name;

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
                        Effect = new DropShadowEffect { Color = ((SolidColorBrush)owner.FindResource("InputOutlineLightBrush")).Color,
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
                        var compatible = args.Data.GetData(typeof(DcsCommandPaletteEntry)) is DcsCommandPaletteEntry incoming
                            ? incoming.IsAxis == control.IsAxis : args.Data.GetDataPresent(typeof(DcsEditorControlDrag));
                        var ok = WorkspaceAssignmentActionState.For(WorkspaceAssignmentAction.Apply, AssignmentContext(),
                            new(true, !protectedInput && compatible)).Enabled;
                        args.Effects = ok ? DragDropEffects.Copy : DragDropEffects.None;
                        if (ok && args.Data.GetDataPresent(typeof(DcsEditorControlDrag)))
                            args.Effects = (Keyboard.Modifiers & ModifierKeys.Control) != 0 ? DragDropEffects.Copy : DragDropEffects.Move;
                        args.Handled = true;
                    };
                    border.Drop += (_, args) => drag.DropOnCallout(control, args);

                    void SelectCallout() => inspector.Select(control, diagram, caption, protectedInput, modifierDescription);

                    border.PreviewMouseLeftButtonDown += (_, args) =>
                    {
                        drag.Start(control, args, border);
                        SelectCallout();
                    };

                    var changeLabel = new MenuItem { Header = "Change label…" };
                    changeLabel.Click += (_, _) => labels.ChangeCalloutLabel(control);
                    var clearAssignment = new MenuItem { Header = "Clear assignment" };
                    clearAssignment.Click += (sender, args) => editing.ClearClicked(sender, args);
                    var tuneAxis = new MenuItem { Header = "Tune axis…" };
                    tuneAxis.Click += (sender, args) => editing.TuneAxisClicked(sender, args);

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
                        drag.ClearStart();
                        SelectCallout();
                        changeLabel.IsEnabled = !protectedInput && state.Context?.Working is not null;
                        AssignmentActionPresentation.Show(clearAssignment, actionState(control, WorkspaceAssignmentAction.Clear));
                        AssignmentActionPresentation.Show(tuneAxis, actionState(control, WorkspaceAssignmentAction.TuneAxis));
                    };
                    _inputSelections.TryAdd(control.InputKey, SelectCallout);
                    if (state.InputKey == control.InputKey) SelectCallout();
                    border.PreviewMouseMove += (_, args) => drag.Move(border, control, args);
                }
            }
            catch (Exception ex)
            {
                _inputSelections.Clear();
                DeviceTitle.Text = UnableToRenderMessage + ex.Message;
            }

            InputPicker.ItemsSource = diagram.Controls.Where(control => _inputSelections.ContainsKey(control.InputKey))
                .DistinctBy(control => control.InputKey)
                .Select(control => new KeyValuePair<string, string>(control.InputKey,
                    AssignmentInputLabel.ForPhysicalKey(control.InputKey, control.Label))).ToArray();
            InputPicker.IsEnabled = _inputSelections.Count > 0;
            InputPicker.SelectedValue = state.InputKey;
        }
        #endregion END Public

        #region BEGIN private helpers
        private string PrepareArtwork(UiLayerDiagram diagram, KneeboardComposite page)
        {
#if PERFORMANCE_TRACE
            using var compose = PerformanceTrace.Measure("diagram.compose", GetType());
#endif
            var svg = diagram.Svg;
            if (svg.Contains("meta-mfd-category-", StringComparison.Ordinal))
            {
                // The artwork provides saved MFD headings. Interactive controls supply assignment text.
                var categoryPage = page with { Labels = new Dictionary<string, IReadOnlyList<KneeboardLabelVariant>>() };
                svg = CompositeKneeboardRenderer.BuildDeviceSvg(diagram, categoryPage);
            }
            return PreviewArtwork.Prepare(svg, AppearanceManager.ArtworkOpacityFor(owner));
        }
        #endregion END private helpers
    }
}

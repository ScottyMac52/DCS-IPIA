using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Heimdall.Domain.MultiSim;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Infrastructure;
using Heimdall.Squadrons;
using Microsoft.Win32;

namespace Heimdall.App.Games.Squadrons
{
    /// <summary>Renders native controller and system artwork using the shared diagram presenter.</summary>
    internal sealed class SquadronsAssignmentDrawing(
        SquadronsWorkspaceContext context,
        Func<SquadronsAssignmentEditing> editing,
        Action<bool> refresh,
        Action<Action> run,
        ListBox DevicePicker,
        TextBlock StatusText,
        SquadronsDevicesView _devicesView,
        SquadronsAssignmentsView _assignmentsView,
        Window owner,
        Func<SquadronsControllerPresenter> controllers)
    {
        private readonly HardwareDiagramPresenter _drawingPresenter = new(_assignmentsView.DrawingCanvas);
        private readonly DiagramViewport _drawingViewport = new(_assignmentsView.DrawingScroll, _assignmentsView.DrawingCanvas,
            _assignmentsView.DrawingZoom, "squadrons.diagram");
        #region BEGIN Public
        internal HardwareDiagramPresenter Presenter => _drawingPresenter;
        internal void Start()
        {
            _drawingViewport.EnablePanning();
            // Any drawing-area drop outside a verified callout must be explained.
            _assignmentsView.DrawingCanvas.AllowDrop = true;
            _assignmentsView.DrawingCanvas.DragOver += (_, e) =>
            {
                if (!e.Handled && e.Data.GetDataPresent(SquadronsWorkspaceContext.CommandDragFormat))
                {
                    e.Effects = DragDropEffects.None;
                    e.Handled = true;
                }
            };
            _assignmentsView.DrawingCanvas.Drop += (_, e) =>
            {
                if (!e.Handled && e.Data.GetDataPresent(SquadronsWorkspaceContext.CommandDragFormat))
                {
                    _assignmentsView.DrawingDropFeedback.Text = "Drop onto an outlined control callout, not the drawing background.";
                    StatusText.Text = _assignmentsView.DrawingDropFeedback.Text;
                    e.Handled = true;
                }
            };
        }
        #endregion END Public
        #region BEGIN private helpers
        internal void RenderDrawing()
        {
            controllers().Input?.Stop();
            _assignmentsView.FitDrawingButton.IsEnabled = false;
            _assignmentsView.DrawingCanvas.Children.Clear();
            _drawingPresenter.Regions.Clear();
            _assignmentsView.DrawingCanvas.Width = 600; _assignmentsView.DrawingCanvas.Height = 200;
            _assignmentsView.DrawingHint.Visibility = Visibility.Visible;
            _assignmentsView.InputActivity.Visibility = Visibility.Visible;
            _assignmentsView.DrawingScroll.Visibility = Visibility.Collapsed;
            var snapshot = context.Session?.Snapshot;
            var slot = context.SelectedController;
            var pin = slot is null ? null : snapshot?.Hardware.GetValueOrDefault(slot);
            // A native physical link does not imply an artwork catalog identity.
            var oldRefreshing = context.Refreshing;
            context.Refreshing = true;
            try
            {
                _devicesView.DrawingPicker.ItemsSource = context.Drawings.Where(x => !string.IsNullOrWhiteSpace(x.Diagram.Svg))
                    .OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase).ToArray();
                _devicesView.DrawingPicker.SelectedItem = context.Drawings.FirstOrDefault(x => x.DeviceId == (slot is null ? null : context.Session?.DrawingId(slot)));
            }
            finally { context.Refreshing = oldRefreshing; }
            var links = ControllerLinkState.For(pin is not null, slot is not null && context.Session?.DrawingId(slot) is not null,
                snapshot is not null && context.Session?.AccessMessage.Length == 0, controllers().Identification.Running || (controllers().Input?.Running ?? false) || context.ScanningInputs);
            _devicesView.DrawingPicker.IsEnabled = links.SelectDrawing && context.Drawings.Count > 0;
            _devicesView.ClearDrawingButton.IsEnabled = links.UnlinkDrawing;
            _devicesView.ChooseDrawingButton.IsEnabled = _devicesView.DrawingPicker.IsEnabled;
            FeatureActionBar.ShowAlternate(_devicesView.ChooseDrawingButton, _devicesView.ClearDrawingButton,
                slot is not null && context.Session?.DrawingId(slot) is not null);
            _devicesView.DrawingPicker.ToolTip = links.UnlinkDrawing ? "Unlink drawing before choosing another drawing." : "Choose an optional drawing.";
            if (snapshot is not null && slot is not null && _devicesView.DrawingPicker.SelectedItem is WorkspaceHardware preview)
                _devicesView.ControllerPreview.Show(preview.Diagram,
                    SquadronsDrawingProjection.Diagram(snapshot.Name, preview.Diagram, context.Rows, slot, "All"),
                    AppearanceManager.ArtworkOpacityFor(owner));
            else
                _devicesView.ControllerPreview.Clear();
            if (_assignmentsView.DrawingPanel.IsExpanded != true) return;
            if (snapshot is not null && DevicePicker.SelectedItem is GameDeviceChoice system &&
                SquadronsSystemInputDrawings.Create(system.Id, context.Rows) is { } systemDiagram)
            {
                ShowSystemDrawing(snapshot.Name, system.Id, systemDiagram);
                return;
            }
            if (slot is null || pin is null)
            {
                _assignmentsView.DrawingHint.Text = "Select a linked joystick controller before choosing its hardware drawing.";
                return;
            }
            if (pin.DrawingHidden)
            {
                _assignmentsView.DrawingHint.Text = "Drawing hidden. Select a hardware drawing to show it again.";
                return;
            }
            if (string.IsNullOrEmpty(pin.HardwareId))
            {
                _assignmentsView.DrawingHint.Text = "This controller is linked, but no hardware drawing is selected. Choose a matching shared drawing above.";
                return;
            }
            var hardware = context.Drawings.FirstOrDefault(x => x.DeviceId == pin.HardwareId);
            if (hardware is null || string.IsNullOrWhiteSpace(hardware.Diagram.Svg))
            {
                _assignmentsView.DrawingHint.Text = "The selected hardware drawing is not available in the current shared catalog.";
                return;
            }
            var diagram = hardware.Diagram;
            var section = context.DrawingSection;
            var page = SquadronsDrawingProjection.Diagram(snapshot!.Name, diagram, context.Rows, slot, section);
            // Native clearing reviews the complete pointed-control record set, including both axis directions.
            _drawingPresenter.Show(diagram, page,
                editing().DrawingInteractions(),
                AppearanceManager.ArtworkOpacityFor(owner),
                control => control.Label + " · " + control.InputKey + "\n" +
                    SquadronsDrawingProjection.AssignmentSummary(control, context.Rows, slot, section));
            _assignmentsView.DrawingHint.Visibility = _assignmentsView.InputActivity.Visibility = Visibility.Collapsed;
            _assignmentsView.DrawingScroll.Visibility = Visibility.Visible;
            _assignmentsView.FitDrawingButton.IsEnabled = true;
        }

        private void ShowSystemDrawing(string title, string deviceId, UiLayerDiagram diagram)
        {
            void Select(HardwareGraphicControl control)
            {
                var rows = SquadronsSystemInputDrawings.ForControl(control, context.Rows, deviceId, context.DrawingSection);
                var selected = rows.Count == 0 ? null : rows[0];
                var oldRefreshing = context.Refreshing;
                context.Refreshing = true;
                try
                {
                    _assignmentsView.BindingsGrid.SelectedItem = null;
                    _assignmentsView.BindingsGrid.ItemsSource = rows;
                    _assignmentsView.BindingsGrid.SelectedItem = selected;
                }
                finally { context.Refreshing = oldRefreshing; }
                _assignmentsView.CountText.Text = control.Label + " · " + rows.Count + " captured assignments · read-only.";
                _assignmentsView.EmptyState.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                _assignmentsView.EmptyState.Text = "No captured assignments on this input in the current context.";
                _assignmentsView.RefreshNativeAssignments();
            }
            _drawingPresenter.Show(diagram, SquadronsSystemInputDrawings.Diagram(title, diagram, context.Rows, deviceId, context.DrawingSection),
                new HardwareDiagramInteractions(Select, Inspect: Select), AppearanceManager.ArtworkOpacityFor(owner),
                control => control.Label + " · read-only\n" + string.Join("\n",
                    SquadronsSystemInputDrawings.ForControl(control, context.Rows, deviceId, context.DrawingSection)
                        .Select(row => row.CommandName + " · " + row.InputLabel + "\n" +
                            string.Join(", ", row.Native.Fields.OrderBy(field => field.Key, StringComparer.Ordinal)
                                .Select(field => field.Key + "=" + field.Value)))));
            _assignmentsView.DrawingHint.Visibility = _assignmentsView.InputActivity.Visibility = Visibility.Collapsed;
            _assignmentsView.DrawingScroll.Visibility = Visibility.Visible;
            _assignmentsView.FitDrawingButton.IsEnabled = true;
        }

        internal void DrawingChanged(object sender, SelectionChangedEventArgs e)
        {
            if (context.Refreshing || !_devicesView.DrawingPicker.IsEnabled || context.Session is null || context.SelectedController is not { } slot ||
                _devicesView.DrawingPicker.SelectedItem is not WorkspaceHardware drawing) return;
            run(() =>
            {
                context.Session.ChangeDrawing(slot, drawing.DeviceId);
                refresh(false);
                StatusText.Text = "Shared drawing selected. Save profile to retain this choice.";
            });
        }

        internal void ClearDrawingClicked(object sender, RoutedEventArgs e) => run(() =>
        {
            if (!_devicesView.ClearDrawingButton.IsEnabled || context.Session is null || context.SelectedController is not { } slot) return;
            context.Session.ChangeDrawing(slot, null);
            refresh(false);
            StatusText.Text = "Drawing unlinked. Choose another drawing or save this change.";
        });

        internal void FitDrawingClicked(object sender, RoutedEventArgs e) => _drawingViewport.Fit();

        internal void DrawingExpanded(object sender, RoutedEventArgs e)
        {
            if (!ReferenceEquals(e.OriginalSource, _assignmentsView.DrawingPanel)) return;

            if (!context.Refreshing && _drawingPresenter is not null) run(RenderDrawing);
        }
        #endregion END private helpers
    }
}

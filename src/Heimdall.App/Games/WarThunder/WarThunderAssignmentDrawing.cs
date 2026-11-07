using System.IO;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Domain.MultiSim;
using Heimdall.Infrastructure;
using Heimdall.WarThunder;
using Microsoft.Win32;

namespace Heimdall.App.Games.WarThunder
{
    /// <summary>Renders optional native controller artwork and delegates pointed-control actions.</summary>
    internal sealed class WarThunderAssignmentDrawing(
        WarThunderWorkspaceContext context,
        Func<WarThunderControllerPresenter> controllers,
        Func<WarThunderAssignmentEditing> editing,
        WarThunderDevicesView _devices,
        WarThunderAssignmentsView _assignments,
        Window owner)
    {
        private readonly HardwareDiagramPresenter _presenter = new(_assignments.DrawingCanvas);
        private readonly DiagramViewport _viewport = new(_assignments.DrawingScroll, _assignments.DrawingCanvas, _assignments.Zoom, "war-thunder.diagram");
        #region BEGIN Public
        internal HardwareDiagramPresenter Presenter => _presenter;
        internal DiagramViewport Viewport => _viewport;
        internal void Start() => _viewport.EnablePanning();
        #endregion END Public
        #region BEGIN private helpers
        internal void ShowDrawing()
        {
            _assignments.DrawingCanvas.Children.Clear(); _presenter.Regions.Clear();
            var drawing = context.SelectedDevice is { } id ? context.Session?.DrawingId(id) : null;
            var hardware = context.Drawings.FirstOrDefault(item => item.DeviceId == drawing);
            controllers().RefreshPhysicalMap();
            _devices.ControllerPreview.Clear();
            if (hardware is not null)
            {
                var page = WarThunderDrawingProjection.Diagram(hardware.Diagram, context.Session!.Snapshot.Profile, context.SelectedDevice!, context.PhysicalMap);
                _devices.ControllerPreview.Show(hardware.Diagram, page, AppearanceManager.ArtworkOpacityFor(owner));
                _presenter.Show(hardware.Diagram, page, Interactions(), AppearanceManager.ArtworkOpacityFor(owner));
                _assignments.Artwork.Feedback.Text = context.PhysicalMap.Message;
            }
            _assignments.Artwork.Viewport.Visibility = hardware is null ? Visibility.Collapsed : Visibility.Visible;
            _assignments.Artwork.Hint.Visibility = hardware is null ? Visibility.Visible : Visibility.Collapsed;
            _assignments.Activity.Visibility = hardware is null ? Visibility.Visible : Visibility.Collapsed;
            if (hardware is null) _assignments.Artwork.Hint.Text = context.PhysicalMap.Message + " Choose a controller drawing on Devices.";
            editing().RefreshInputState();
        }
        internal void RefreshActions() => _presenter.RefreshActions(Interactions());
        private HardwareDiagramInteractions Interactions() =>
            new HardwareDiagramInteractions(
                    editing().SelectPhysicalControl, context.PhysicalMap.Inputs.Count > 0 ? editing().DropPhysicalCommand : null,
                    CanClear: editing().CanClearPhysicalControl, Clear: editing().ClearPhysicalControl,
                    CanTune: editing().CanTunePhysicalControl, Tune: editing().TunePhysicalControl,
                    DragFormat: "heimdall.war-thunder.command", Inspect: editing().InspectPhysicalControl, CanDrop: editing().CanDropPhysicalCommand,
                    ClearChoices: editing().ClearPhysicalChoices, ClearUnavailableReason: editing().ClearUnavailableReason, ActionState: editing().ControlAction);
        #endregion END private helpers
    }
}

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Rendering;

namespace Heimdall.App.Games
{
    /// <summary>Shared visual-only renderer and hit-region presenter for hardware diagrams.
    /// Simulator adapters own native editing, never this class.</summary>
    internal sealed class HardwareDiagramPresenter(Canvas canvas)
    {
        private readonly Dictionary<(string Svg, double Width, double Height), BitmapImage> _artwork = [];
        #region BEGIN Public
        /// <summary>Gets current physical controls and their feedback outlines.</summary>
        internal List<(HardwareGraphicControl Control, Border Outline)> Regions { get; } = [];
        /// <summary>Displays the labeled shared drawing with click, drop and context actions.</summary>
        internal void Show(UiLayerDiagram diagram, KneeboardComposite page, HardwareDiagramInteractions actions,
            double artworkOpacity = 1, Func<HardwareGraphicControl, string>? tooltip = null)
        {
            canvas.Children.Clear(); Regions.Clear();
            canvas.Width = diagram.Width; canvas.Height = diagram.Height;
            canvas.Children.Add(new Image { Source = RenderArtwork(diagram, page, artworkOpacity), Width = diagram.Width, Height = diagram.Height, IsHitTestVisible = false });
#if PERFORMANCE_TRACE
            using var regions = PerformanceTrace.Measure("diagram.hit-regions", GetType());
#endif
            foreach (var control in diagram.Controls)
            {
                var border = new Border { Width = control.Width, Height = control.Height, Background = Brushes.Transparent,
                    BorderBrush = Brushes.Transparent, BorderThickness = new Thickness(3), AllowDrop = true,
                    ToolTip = tooltip?.Invoke(control) ?? control.Label + " · " + control.InputKey, Tag = control };
                Canvas.SetLeft(border, control.X); Canvas.SetTop(border, control.Y);
                var availability = HardwareDiagramActionAvailability.For(actions.Select is not null,
                    actions.Drop is not null, actions.Clear is not null, actions.CanClear?.Invoke(control) == true,
                    actions.Tune is not null, actions.CanTune?.Invoke(control) == true);
                if (availability.Select)
                    border.MouseLeftButtonDown += (_, e) => { actions.Select!(control); e.Handled = true; };
                if (availability.Drop)
                {
                    border.DragOver += (_, e) => { e.Effects = e.Data.GetData(actions.DragFormat) is string id && (State(actions, control, WorkspaceAssignmentAction.Apply, true).Enabled && (actions.CanDrop?.Invoke(control, id) ?? true)) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
                    border.Drop += (_, e) => { if (e.Data.GetData(actions.DragFormat) is string id && (State(actions, control, WorkspaceAssignmentAction.Apply, true).Enabled && (actions.CanDrop?.Invoke(control, id) ?? true))) actions.Drop!(control, id); else e.Effects = DragDropEffects.None; e.Handled = true; };
                }
                // A left click already selects the input. A right-click menu
                // advertises only actual native operations; no phantom Select/Tune.
                if (actions.Clear is not null || actions.Tune is not null || actions.Inspect is not null)
                {
                    var menu = new ContextMenu();
                    RefreshMenu(menu, control, actions);
                    border.ContextMenuOpening += (_, e) =>
                    {
                        RefreshMenu(menu, control, actions);
                        if (menu.Items.Count == 0) e.Handled = true;
                    };
                    // Place the popup by the mouse, not centered across a wide
                    // authored hit region; do not obscure the control artwork.
                    menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
                    border.ContextMenu = menu;
                }
                canvas.Children.Add(border); Regions.Add((control, border));
            }
        }
        /// <summary>Refreshes mounted callout actions when shared availability changes without re-rendering artwork.</summary>
        internal void RefreshActions(HardwareDiagramInteractions actions)
        {
            foreach (var (control, outline) in Regions)
                if (outline.ContextMenu is { } menu) RefreshMenu(menu, control, actions);
        }
        /// <summary>Refreshes real pointed-control actions at menu opening, independently of left-click selection.</summary>
        internal static void RefreshMenu(ContextMenu menu, HardwareGraphicControl control, HardwareDiagramInteractions actions)
        {
            menu.Items.Clear();
            if (actions.Inspect is not null)
            {
                var inspect = new MenuItem { Header = "View assignments" };
                AssignmentActionPresentation.Show(inspect, State(actions, control, WorkspaceAssignmentAction.Inspect, true));
                inspect.Click += (_, _) => actions.Inspect(control);
                menu.Items.Add(inspect);
            }
            if (actions.Clear is not null)
            {
                if (menu.Items.Count > 0) menu.Items.Add(new Separator());
                var state = State(actions, control, WorkspaceAssignmentAction.Clear, actions.CanClear?.Invoke(control) == true,
                    actions.ClearUnavailableReason?.Invoke(control) ?? "No editable assignment on this control.");
                var enabled = state.Enabled;
                var clear = new MenuItem { Header = actions.ClearLabel };
                AssignmentActionPresentation.Show(clear, state);
                if (!enabled)
                {
                    clear.ToolTip = state.Reason;
                    ToolTipService.SetShowOnDisabled(clear, true);
                }
                var choices = enabled ? actions.ClearChoices?.Invoke(control) : null;
                if (choices is { Count: > 1 })
                    foreach (var choice in choices)
                    {
                        var alternative = new MenuItem { Header = choice.Label };
                        alternative.Click += (_, _) => choice.Execute();
                        clear.Items.Add(alternative);
                    }
                else clear.Click += (_, _) => actions.Clear(control);
                menu.Items.Add(clear);
            }
            if (control.IsAxis && actions.Tune is not null)
            {
                if (menu.Items.Count > 0) menu.Items.Add(new Separator());
                var tune = new MenuItem { Header = "Tune axis…" };
                AssignmentActionPresentation.Show(tune, State(actions, control, WorkspaceAssignmentAction.TuneAxis,
                    actions.CanTune?.Invoke(control) == true, "Select an assigned axis that supports tuning."));
                tune.Click += (_, _) => actions.Tune(control);
                menu.Items.Add(tune);
            }
        }
        /// <summary>Decodes a frozen bitmap with no retained stream handle.</summary>
        internal static BitmapImage Image(byte[] bytes)
        {
            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream; image.EndInit(); image.Freeze(); return image;
        }
        #endregion END Public
        #region BEGIN private helpers
        // Cache only frozen pixels, keyed by complete composed output and dimensions. Hit regions and menus remain live.
        // Bound retained images across controller, scope, content and opacity changes.
        private BitmapImage RenderArtwork(UiLayerDiagram diagram, KneeboardComposite page, double opacity)
        {
            string svg;
#if PERFORMANCE_TRACE
            using (PerformanceTrace.Measure("diagram.compose", GetType()))
#endif
            { svg = PreviewArtwork.Prepare(CompositeKneeboardRenderer.BuildDeviceSvg(diagram, page), opacity); }
            var key = (svg, diagram.Width, diagram.Height);
            if (_artwork.TryGetValue(key, out var image)) return image;
#if PERFORMANCE_TRACE
            using var raster = PerformanceTrace.Measure("diagram.rasterize", GetType());
#endif
            image = Image(CanonicalSvgRenderer.RenderComplete(svg, diagram.Width, diagram.Height));
            if (_artwork.Count >= 8) _artwork.Remove(_artwork.Keys.First());
            _artwork.Add(key, image);
            return image;
        }
        private static WorkspaceAssignmentActionState State(HardwareDiagramInteractions actions, HardwareGraphicControl control,
            WorkspaceAssignmentAction action, bool ready, string reason = "") => actions.ActionState?.Invoke(control, action)
                ?? WorkspaceAssignmentActionState.For(action, new("", true, false, false, false), new(true, ready, reason));
        #endregion END private helpers
    }
}


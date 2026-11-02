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
        #region BEGIN Public
        /// <summary>Gets current physical controls and their feedback outlines.</summary>
        internal List<(HardwareGraphicControl Control, Border Outline)> Regions { get; } = [];
        /// <summary>Displays the labeled shared drawing with click, drop and context actions.</summary>
        internal void Show(UiLayerDiagram diagram, KneeboardComposite page, HardwareDiagramInteractions actions,
            double artworkOpacity = 1, Func<HardwareGraphicControl, string>? tooltip = null)
        {
            canvas.Children.Clear(); Regions.Clear();
            canvas.Width = diagram.Width; canvas.Height = diagram.Height;
            var png = CanonicalSvgRenderer.RenderComplete(PreviewArtwork.Prepare(CompositeKneeboardRenderer.BuildDeviceSvg(diagram, page), artworkOpacity), diagram.Width, diagram.Height);
            canvas.Children.Add(new Image { Source = Image(png), Width = diagram.Width, Height = diagram.Height, IsHitTestVisible = false });
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
                    border.DragOver += (_, e) => { e.Effects = e.Data.GetData(actions.DragFormat) is string id && (actions.CanDrop?.Invoke(control, id) ?? true) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
                    border.Drop += (_, e) => { if (e.Data.GetData(actions.DragFormat) is string id && (actions.CanDrop?.Invoke(control, id) ?? true)) actions.Drop!(control, id); else e.Effects = DragDropEffects.None; e.Handled = true; };
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
        /// <summary>Refreshes real pointed-control actions at menu opening, independently of left-click selection.</summary>
        internal static void RefreshMenu(ContextMenu menu, HardwareGraphicControl control, HardwareDiagramInteractions actions)
        {
            menu.Items.Clear();
            if (actions.Inspect is not null)
            {
                var inspect = new MenuItem { Header = "View assignments" };
                inspect.Click += (_, _) => actions.Inspect(control);
                menu.Items.Add(inspect);
            }
            if (actions.Clear is not null)
            {
                if (menu.Items.Count > 0) menu.Items.Add(new Separator());
                var enabled = actions.CanClear?.Invoke(control) == true;
                var clear = new MenuItem { Header = actions.ClearLabel, IsEnabled = enabled };
                if (!enabled)
                {
                    clear.ToolTip = actions.ClearUnavailableReason?.Invoke(control) ?? "No editable assignment on this control.";
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
            if (control.IsAxis && actions.CanTune?.Invoke(control) == true && actions.Tune is not null)
            {
                if (menu.Items.Count > 0) menu.Items.Add(new Separator());
                var tune = new MenuItem { Header = "Tune axis…" };
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
    }
}


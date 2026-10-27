using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Heimdall.Application;
using Heimdall.Rendering;

namespace Heimdall.App.Games
{
    /// <summary>Reuses the canonical SVG renderer and shared hit regions for native game assignments.</summary>
    internal sealed class EliteDiagramPresenter(Canvas canvas)
    {
        #region BEGIN Public
        /// <summary>Gets current physical controls and their feedback outlines.</summary>
        internal List<(HardwareGraphicControl Control, Border Outline)> Regions { get; } = [];
        /// <summary>Displays the labeled shared drawing with click, drop and context actions.</summary>
        internal void Show(UiLayerDiagram diagram, KneeboardComposite page, Action<HardwareGraphicControl> select,
            Action<HardwareGraphicControl, string> drop, Action? clear, double artworkOpacity = 1, Func<HardwareGraphicControl, string>? tooltip = null, string dragFormat = "heimdall.elite.command")
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
                border.MouseLeftButtonDown += (_, e) => { select(control); e.Handled = true; };
                border.DragOver += (_, e) => { e.Effects = e.Data.GetDataPresent(dragFormat) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
                border.Drop += (_, e) => { if (e.Data.GetData(dragFormat) is string id) drop(control, id); e.Handled = true; };
                var menu = new ContextMenu();
                // Selecting an axis is not tuning it; never advertise unsupported commands.
                var edit = new MenuItem { Header = control.IsAxis
                    ? "Select axis for assignment" : "Select input for assignment" };
                edit.Click += (_, _) => select(control);
                menu.Items.Add(edit);
                // Simulators without a real clear action must not show a phantom menu item.
                if (clear is not null)
                {
                    var remove = new MenuItem { Header = "Clear selected assignment" };
                    remove.Click += (_, _) => { select(control); clear(); };
                    menu.Items.Add(remove);
                }
                border.ContextMenu = menu;
                canvas.Children.Add(border); Regions.Add((control, border));
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

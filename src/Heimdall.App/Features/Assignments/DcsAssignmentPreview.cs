using Heimdall.Application;
using Heimdall.Dcs;
using Heimdall.Rendering;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Heimdall.App.Features.Assignments
{
    /// <summary>Renders a current-draft kneeboard without persisting or deploying it.</summary>
    internal sealed class DcsAssignmentPreview(DcsAssignmentViewState state, FrameworkElement owner, TextBlock SelectedCallout)
    {
        #region BEGIN Public
        internal void RenderClicked(object sender, RoutedEventArgs e)
        {
            if (state.Context?.Working is null || state.Context.Draft is null ||
                state.Diagram is not UiLayerDiagram diagram ||
                (diagram.DrawingUnlinked || string.IsNullOrWhiteSpace(diagram.Svg)))
            {
                SelectedCallout.Text = "Select a device with an authored graphic to render.";
                return;
            }

            try
            {
                var page = DcsKneeboardDraftProjection.CreateCombined(
                    state.Context.Working, diagram, state.Context.Draft.Snapshot, state.Context.Labels);
                var pages = new StackPanel();
                var view = new AppearanceDialog("CurrentKneeboardDialog")
                {
                    Title = "Current kneeboard · " + diagram.Title,
                    Owner = Window.GetWindow(owner), Width = 900, Height = 900,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Content = new ScrollViewer { Content = pages }
                };
                void Redraw()
                {
                    pages.Children.Clear();
                    var images = DeviceKneeboardPages.Render(diagram, page, "HEIMDALL · current draft preview",
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
        #endregion END Public
    }
}

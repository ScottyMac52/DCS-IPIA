using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Rendering;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DcsIpia.Features.Kneeboards
{
    public partial class KneeboardsView : UserControl
    {
        private IModuleWorkspaceContext? _context;
        private bool _updating;

        public KneeboardsView() => InitializeComponent();

        public void Initialize(IModuleWorkspaceContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            _context = context;
            _context.StateChanged += (_, _) => Refresh();
            Refresh();
        }

        private void Refresh()
        {
            if (_context?.Working is null && _context?.Saved is null)
            {
                KneeboardList.ItemsSource = null;
                KneeboardPreview.Source = null;
                KneeboardCaption.Text = "No kneeboard available.";
                return;
            }
            var pages = _context.Working?.Pages ?? _context.Saved?.Pages ?? [];
            KneeboardList.ItemsSource = pages.OrderBy(x => x.Order).Select(x => $"{x.Order:00}  ·  {x.Title}").ToArray();
            if (KneeboardList.SelectedIndex < 0 && KneeboardList.Items.Count > 0)
                KneeboardList.SelectedIndex = 0;
            RefreshPreview();
        }

        private void PageSelected(object sender, SelectionChangedEventArgs e)
        {
            if (GenericPageChoice is not null) GenericPageChoice.SelectedIndex = 0;
            RefreshPreview();
        }

        private void GenericPageChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_updating) RefreshPreview();
        }

        private void RefreshPreview()
        {
            if (KneeboardPreview is null || KneeboardCaption is null || GenericPageChoice is null) return;
            KneeboardPreview.Source = null;
            GenericPageChoice.IsEnabled = false;

            var index = KneeboardList.SelectedIndex;
            var workspace = _context?.Working ?? _context?.Saved;
            if (workspace is null || index < 0 || index >= workspace.Pages.Count)
            {
                KneeboardCaption.Text = "No rendered kneeboard image is available.";
                return;
            }

            try
            {
                var page = workspace.Pages.OrderBy(x => x.Order).ElementAt(index);
                var path = $"KNEEBOARD/{workspace.Configuration.Module.DcsId}/{page.StableKey}.png";
                byte[] bytes;
                if (page.Kind == KneeboardPageKind.Device)
                {
                    var diagram = workspace.Diagrams.Single(x =>
                        KneeboardPageLocator.PageKeyForProfile(workspace, x.ProfileId) == page.StableKey);
                    var combined = KneeboardDraftProjection.CreateCombined(workspace, diagram, workspace.Assignments, workspace.Labels);
                    if (diagram.GenericCapabilities is null && diagram.CustomMapping is null)
                        bytes = CompositeKneeboardRenderer.Render(diagram, combined, "");
                    else
                    {
                        var generated = DeviceKneeboardPages.Render(diagram, combined);
                        var choices = Enumerable.Range(1, generated.Count).Select(x => "Page " + x).ToArray();
                        if (!(GenericPageChoice.ItemsSource as string[] ?? []).SequenceEqual(choices, StringComparer.Ordinal))
                        {
                            _updating = true;
                            GenericPageChoice.ItemsSource = choices;
                            GenericPageChoice.SelectedIndex = 0;
                            _updating = false;
                        }
                        GenericPageChoice.IsEnabled = generated.Count > 1;
                        var selected = Math.Max(0, GenericPageChoice.SelectedIndex);
                        bytes = generated[selected];
                        path = path[..^4] + (selected == 0 ? "" : "-" + (selected + 1).ToString("D3", CultureInfo.InvariantCulture)) + ".png";
                    }
                }
                else if (page.Kind == KneeboardPageKind.Summary)
                    bytes = SnapshotSummaryKneeboardRenderer.Generate(workspace)[path];
                else
                    throw new InvalidDataException("Unsupported page: " + page.StableKey);

                using var stream = new MemoryStream(bytes);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                KneeboardPreview.Source = bitmap;
                KneeboardCaption.Text = "Generated: " + path;
            }
            catch (Exception ex)
            {
                KneeboardCaption.Text = "Unable to generate: " + ex.Message;
            }
        }

        private void ZoomChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ZoomPercent is null || ZoomBox is null) return;
            ZoomPercent.Text = ((int)(Zoom.Value * 100)).ToString(CultureInfo.InvariantCulture);
            ZoomBox.LayoutTransform = new ScaleTransform(Zoom.Value, Zoom.Value);
        }

        private void ZoomPercentChanged(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(ZoomPercent.Text, out var percent))
                Zoom.Value = Math.Clamp(percent / 100.0, Zoom.Minimum, Zoom.Maximum);
        }

        private void ZoomReset(object sender, MouseButtonEventArgs e) => Zoom.Value = 1.0;
    }
}
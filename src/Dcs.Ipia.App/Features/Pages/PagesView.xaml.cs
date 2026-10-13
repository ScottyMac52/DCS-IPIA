using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Rendering;
using DcsIpia.Presentation;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DcsIpia.Features.Pages
{
    public partial class PagesView : UserControl
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT point);
        private IModuleWorkspaceContext? _context;
        private KneeboardPage? _selected;
        private bool _refreshing;
        private readonly List<ModuleSummaryCard> _cards = [];
        private Point? _dragStart;
        private KneeboardPage? _dragPage;
        private Point? _cardDragStart;
        private ModuleSummaryCard? _cardDrag;
        private TitleAdorner? _adorner;
        private AdornerLayer? _layer;

        private TitleAdorner? _cardAdorner;
        private AdornerLayer? _cardLayer;

        public PagesView()
        {
            InitializeComponent();
            CardAccent.ItemsSource = new[] { "cyan", "gold", "red", "blue", "orange", "purple", "green" };
            CardAccent.SelectedIndex = 0;
        }

        public void Initialize(IModuleWorkspaceContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            _context = context;
            _context.StateChanged += (_, _) => Refresh();
            Refresh();
            PageKind.ItemsSource = Enum.GetValues<KneeboardPageKind>();
            PageKind.SelectedIndex = 0;
        }

        private void PageSelected(object sender, SelectionChangedEventArgs e)
        {
            var selected = Pages.SelectedItem as KneeboardPage;
            if (selected is null || _context?.Working is null)
            {
                _selected = null;
                ClearForm();
                return;
            }
            _selected = _context.Working.Pages.FirstOrDefault(x => x.Id == selected.Id);
            if (_selected is null)
            {
                ClearForm();
                return;
            }
            var settings = ModuleKneeboardPageEditor.Describe(_context.Working, _selected);
            _refreshing = true;
            PageKind.SelectedItem = settings.Kind;
            PageKind.IsEnabled = false;
            PageDevice.SelectedItem = _context.Working.Configuration.Profiles.FirstOrDefault(x => x.Id == settings.ProfileId);
            PageDevice.IsEnabled = false;
            PageFile.Text = settings.File;
            PageTitle.Text = settings.Title;
            PageSubtitle.Text = settings.Subtitle;
            PageOrder.Text = settings.Order.ToString(CultureInfo.InvariantCulture);
            _cards.Clear();
            _cards.AddRange(settings.Cards);
            SummaryCards.ItemsSource = _cards.ToArray();
            SummaryGroup.IsEnabled = settings.Kind == KneeboardPageKind.Summary;
            _refreshing = false;
        }

        private void NewClicked(object sender, RoutedEventArgs e)
        {
            Pages.SelectedItem = null;
            _selected = null;
            PageKind.IsEnabled = true;
            PageKind.SelectedIndex = 0;
            PageFile.Text = "overview";
            PageTitle.Text = "CONTROL OVERVIEW";
            PageSubtitle.Text = "CONTROLS";
            PageOrder.Text = (_context?.Working?.Pages.Count ?? 0).ToString(CultureInfo.InvariantCulture);
            StatusText.Text = "Enter page details, then Update to add.";
            _cards.Clear();
            SummaryCards.ItemsSource = _cards.ToArray();
        }

        private void UpdateClicked(object sender, RoutedEventArgs e)
        {
            if (_context is null) return;
            try
            {
                var settings = ReadSettings();
                _context.RunStructureEdit(workspace => ModuleKneeboardPageEditor.Set(workspace, _selected, settings));
                StatusText.Text = _selected is null ? "Page added and staged." : "Page updated and staged.";
            }
            catch (Exception ex) { StatusText.Text = ex.Message; }
        }

        private void RemoveClicked(object sender, RoutedEventArgs e)
        {
            if (_context is null || _selected is null) { StatusText.Text = "Select a page to remove."; return; }
            try
            {
                _context.RunStructureEdit(workspace => ModuleKneeboardPageEditor.Remove(workspace, _selected));
                StatusText.Text = "Page removed and staged.";
                _selected = null;
            }
            catch (Exception ex) { StatusText.Text = ex.Message; }
        }

        private ModuleKneeboardPageSettings ReadSettings()
        {
            if (!int.TryParse(PageOrder.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var order))
                throw new InvalidOperationException("Enter a non-negative page order.");
            var kind = PageKind.SelectedItem is KneeboardPageKind k ? k : KneeboardPageKind.Device;
            return new(kind,
                kind == KneeboardPageKind.Device ? (PageDevice.SelectedItem as DeviceProfile)?.Id : null,
                PageFile.Text, PageTitle.Text, PageSubtitle.Text, order,
                kind == KneeboardPageKind.Summary ? _cards.ToArray() : []);
        }

        private void ClearForm()
        {
            PageFile.Text = "";
            PageTitle.Text = "";
            PageSubtitle.Text = "";
            PageOrder.Text = "";
            PageKind.IsEnabled = true;
            PageDevice.IsEnabled = true;
            _cards.Clear();
            SummaryCards.ItemsSource = _cards.ToArray();
        }

        private async void SaveClicked(object sender, RoutedEventArgs e)
        {
            if (_context is null) return;
            await _context.SaveAsync();
            StatusText.Text = "Saved to database.";
        }

        private void DiscardClicked(object sender, RoutedEventArgs e)
        {
            _context?.Discard();
            _selected = null;
            Refresh();
            StatusText.Text = "Pending edits discarded.";
        }

        private void Refresh()
        {
            if (_context?.Working is null)
            {
                StatusText.Text = "No working snapshot";
                Pages.ItemsSource = null;
                return;
            }
            var pages = _context.Working.Pages.OrderBy(x => x.Order).ToArray();
            Pages.ItemsSource = pages;
            StatusText.Text = $"{pages.Length} pages";
            PageDevice.ItemsSource = _context.Working.Configuration.Profiles;
        }

        private void AddMissingClicked(object sender, RoutedEventArgs e)
        {
            if (_context is null) return;
            try
            {
                _context.RunStructureEdit(workspace => ModuleKneeboardPageEditor.AddMissingDevicePages(workspace));
                StatusText.Text = "Missing device pages added and staged.";
            }
            catch (Exception ex) { StatusText.Text = ex.Message; }
        }

        private void KindChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_refreshing || PageDevice is null || SummaryGroup is null) return;
            var summary = PageKind.SelectedItem is KneeboardPageKind.Summary;
            SummaryGroup.IsEnabled = summary;
            PageDevice.IsEnabled = !summary && _selected is null;
        }

        private void NewCardClicked(object sender, RoutedEventArgs e)
        {
            SummaryCards.SelectedItem = null;
            CardTitle.Text = "";
            CardBody.Text = "";
        }

        private void SetCardClicked(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(CardTitle.Text)) return;
            var card = new ModuleSummaryCard(CardTitle.Text.Trim(), CardBody.Text.Trim(), CardAccent.SelectedItem as string ?? "blue");
            var index = SummaryCards.SelectedIndex;
            if (index < 0) _cards.Add(card);
            else _cards[index] = card;
            SummaryCards.ItemsSource = _cards.ToArray();
            CardTitle.Text = "";
            CardBody.Text = "";
        }

        private void RemoveCardClicked(object sender, RoutedEventArgs e)
        {
            if (SummaryCards.SelectedItem is not ModuleSummaryCard card) return;
            _cards.Remove(card);
            SummaryCards.ItemsSource = _cards.ToArray();
        }

        private void CardSelected(object sender, SelectionChangedEventArgs e)
        {
            if (SummaryCards.SelectedItem is not ModuleSummaryCard card) return;
            CardTitle.Text = card.Key;
            CardBody.Text = card.Text;
            CardAccent.SelectedItem = card.Accent;
        }

        private void CardDragStart(object sender, MouseButtonEventArgs e)
        {
            _cardDragStart = e.GetPosition(SummaryCards);
            _cardDrag = SummaryCards.SelectedItem as ModuleSummaryCard;
        }

        private void CardDrag(object sender, MouseEventArgs e)
        {
            if (_cardDragStart is not Point start || _cardDrag is null || e.LeftButton != MouseButtonState.Pressed) return;
            var pos = e.GetPosition(SummaryCards);
            if (Math.Abs(pos.X - start.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(pos.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            _cardDragStart = null;

            var host = FindCardHost();
            _cardLayer = AdornerLayer.GetAdornerLayer(host);
            _cardAdorner = new TitleAdorner(host, _cardDrag.Key) { IsHitTestVisible = false };
            _cardLayer?.Add(_cardAdorner);

            GiveFeedbackEventHandler feedback = (_, args) =>
            {
                if (_cardAdorner is not null && GetCursorPos(out var p))
                    _cardAdorner.Update(host.PointFromScreen(new Point(p.X, p.Y)));
                args.UseDefaultCursors = false;
                Mouse.SetCursor(Cursors.Arrow);
                args.Handled = true;
            };
            SummaryCards.AddHandler(DragDrop.GiveFeedbackEvent, feedback, true);
            try
            {
                DragDrop.DoDragDrop(SummaryCards, new DataObject(typeof(ModuleSummaryCard), _cardDrag), DragDropEffects.Move);
            }
            finally
            {
                SummaryCards.RemoveHandler(DragDrop.GiveFeedbackEvent, feedback);
                if (_cardAdorner is not null) _cardLayer?.Remove(_cardAdorner);
                _cardAdorner = null;
            }
        }

        private UIElement FindCardHost()
        {
            DependencyObject? current = SummaryCards;
            while (current is not null)
            {
                if (current is AdornerDecorator decorator && decorator.Child is UIElement child)
                    return child;
                current = VisualTreeHelper.GetParent(current);
            }
            return SummaryCards;
        }

        private void CardDragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(typeof(ModuleSummaryCard)) ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        }

        private void CardDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(typeof(ModuleSummaryCard)) is not ModuleSummaryCard source) return;
            var point = e.GetPosition(SummaryCards);
            var insertion = _cards.Count;
            if (SummaryCards.InputHitTest(point) is DependencyObject element &&
                ItemsControl.ContainerFromElement(SummaryCards, element) is ListBoxItem row)
            {
                insertion = SummaryCards.ItemContainerGenerator.IndexFromContainer(row);
                if (e.GetPosition(row).Y >= row.ActualHeight / 2) insertion++;
            }
            var from = _cards.IndexOf(source);
            if (from < 0) return;
            if (insertion > from) insertion--;
            if (insertion == from) return;
            _cards.RemoveAt(from);
            _cards.Insert(Math.Clamp(insertion, 0, _cards.Count), source);
            SummaryCards.ItemsSource = _cards.ToArray();
            e.Handled = true;
        }

        private void GenerateSummaryClicked(object sender, RoutedEventArgs e)
        {
            if (_context is null) return;
            try
            {
                _context.RunStructureEdit(workspace => ModuleKneeboardPageEditor.GenerateSummary(workspace));
                StatusText.Text = "Summary pages generated and staged.";
            }
            catch (Exception ex) { StatusText.Text = ex.Message; }
        }

        private void PageDragStart(object sender, MouseButtonEventArgs e)
        {
            _dragStart = e.GetPosition(Pages);
            _dragPage = Pages.SelectedItem as KneeboardPage;
        }

        private void PageDrag(object sender, MouseEventArgs e)
        {
            if (_dragStart is not Point start || _dragPage is null || e.LeftButton != MouseButtonState.Pressed) return;
            var pos = e.GetPosition(Pages);
            if (Math.Abs(pos.X - start.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(pos.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            _dragStart = null;
            _layer = AdornerLayer.GetAdornerLayer(Pages);
            _adorner = new TitleAdorner(Pages, _dragPage.Title);
            _layer?.Add(_adorner);
            DragDrop.DoDragDrop(Pages, new DataObject(typeof(KneeboardPage), _dragPage), DragDropEffects.Move);
            if (_adorner is not null) _layer?.Remove(_adorner);
            _adorner = null;
        }

        private void PageDragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(KneeboardPage)))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
            _adorner?.Update(e.GetPosition(Pages));
        }

        private void PageDrop(object sender, DragEventArgs e)
        {
            if (_context is null || e.Data.GetData(typeof(KneeboardPage)) is not KneeboardPage source) return;
            var point = e.GetPosition(Pages);
            var row = Pages.InputHitTest(point) as DependencyObject;
            while (row is not null && row is not DataGridRow)
                row = VisualTreeHelper.GetParent(row);
            var target = (row as DataGridRow)?.Item as KneeboardPage;
            if (target is null || target.Id == source.Id) return;
            try
            {
                _context.RunStructureEdit(workspace => ModuleKneeboardPageEditor.Move(workspace, source, target.Order));
                StatusText.Text = "Page order staged.";
            }
            catch (Exception ex) { StatusText.Text = ex.Message; }
            e.Handled = true;
        }

        private void PageGiveFeedback(object sender, GiveFeedbackEventArgs e)
        {
            e.UseDefaultCursors = false;
            Mouse.SetCursor(Cursors.Arrow);
            e.Handled = true;
            _adorner?.Update(Mouse.GetPosition(Pages));
        }

        private void PreviewClicked(object sender, RoutedEventArgs e)
        {
            if (_context?.Working is null || _selected is null) return;
            try
            {
                var settings = ReadSettings();
                var updated = ModuleKneeboardPageEditor.Set(_context.Working, _selected, settings);
                byte[] png;
                if (settings.Kind == KneeboardPageKind.Summary)
                    png = SnapshotSummaryKneeboardRenderer.Generate(updated)[$"KNEEBOARD/{updated.Configuration.Module.DcsId}/{settings.File}.png"];
                else
                {
                    var diagram = updated.Diagrams.Single(x => x.ProfileId == settings.ProfileId);
                    png = CompositeKneeboardRenderer.Render(diagram,
                        KneeboardDraftProjection.CreateCombined(updated, diagram, updated.Assignments, updated.Labels), "");
                }
                using var stream = new MemoryStream(png);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                new Window
                {
                    Title = "Preview · " + settings.Title,
                    Owner = Window.GetWindow(this),
                    Width = 850,
                    Height = 900,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Content = new ScrollViewer { Content = new Image { Source = bitmap, Stretch = Stretch.Uniform } }
                }.ShowDialog();
            }
            catch (Exception ex) { StatusText.Text = "Preview failed: " + ex.Message; }
        }

        private void SummaryCardsResizeDragged(object sender, DragDeltaEventArgs e)
        {
            SummaryCards.Height = Math.Clamp(SummaryCards.Height + e.VerticalChange, 60, 400);
            e.Handled = true;
        }
    }
}
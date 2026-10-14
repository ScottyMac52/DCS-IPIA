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
    /// <summary>Edits kneeboard pages and their summary cards.</summary>
    public partial class PagesView : UserControl
    {
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

        #region BEGIN Public
        public PagesView()
        {
            InitializeComponent();
            CardAccent.ItemsSource = new[] { "cyan", "gold", "red", "blue", "orange", "purple", "green" };
            CardAccent.SelectedIndex = 0;
        }

        /// <summary>Connects this view to the shared workspace and refreshes its displayed state.</summary>
        public void Initialize(IModuleWorkspaceContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            _context = context;
            _context.StateChanged += (_, _) => Refresh();
            Refresh();
            PageKind.ItemsSource = Enum.GetValues<KneeboardPageKind>();
            PageKind.SelectedIndex = 0;
            PageOrder.Text = _context.Working?.Pages.Count.ToString(CultureInfo.InvariantCulture) ?? "0";
        }
        #endregion END Public

        #region BEGIN private helpers
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out NativeScreenPoint point);

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
            try
            {
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
            }
            catch (Exception ex)
            {
                ClearForm();
                StatusText.Text = "Unable to edit page: " + ex.Message + " You can still remove this page.";
            }
            finally { _refreshing = false; }
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
            try
            {
                await _context.SaveAsync();
                StatusText.Text = "Saved to database.";
            }
            catch (Exception ex) { StatusText.Text = "Database save failed: " + ex.Message; }
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
            var selectedId = (Pages.SelectedItem as KneeboardPage)?.Id;
            var pages = _context.Working.Pages.OrderBy(x => x.Order).ToArray();
            Pages.ItemsSource = pages;
            Pages.SelectedItem = pages.FirstOrDefault(x => x.Id == selectedId);
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
                DragDrop.DoDragDrop(SummaryCards, new DataObject(typeof(SummaryCardDrag), new SummaryCardDrag(SummaryCards, _cardDrag)), DragDropEffects.Move);
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
            e.Effects = CardSource(e) >= 0 ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        }

        private void CardDrop(object sender, DragEventArgs e)
        {
            var source = CardSource(e);
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            if (source < 0) return;
            var insertion = _cards.Count;
            if (SummaryCards.InputHitTest(e.GetPosition(SummaryCards)) is DependencyObject element &&
                ItemsControl.ContainerFromElement(SummaryCards, element) is ListBoxItem row)
            {
                insertion = SummaryCards.ItemContainerGenerator.IndexFromContainer(row);
                if (e.GetPosition(row).Y >= row.ActualHeight / 2) insertion++;
            }
            var moved = SummaryCardOrder.Move(_cards, source, insertion);
            if (ReferenceEquals(moved, _cards)) return;
            var destination = insertion > source ? insertion - 1 : insertion;
            _cards.Clear();
            _cards.AddRange(moved);
            SummaryCards.ItemsSource = _cards.ToArray();
            SummaryCards.SelectedIndex = destination;
            SummaryCards.ScrollIntoView(SummaryCards.SelectedItem);
            e.Effects = DragDropEffects.Move;
            StatusText.Text = "Cards reordered and renumbered in the form. Stage page, then Save to database to keep them.";
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

        private KneeboardPage? PageDragSource(DragEventArgs e)
        {
            if (_context!.Working is null || e.Data.GetData(typeof(KneeboardPageDrag)) is not KneeboardPageDrag drag ||
                !ReferenceEquals(drag.Source, Pages)) return null;
            return _context!.Working.Pages.FirstOrDefault(x => ReferenceEquals(x, drag.Page));
        }

        private int CardSource(DragEventArgs e)
        {
            if (_context!.Working is null || !SummaryGroup.IsEnabled ||
                (e.AllowedEffects & DragDropEffects.Move) == 0 ||
                e.Data.GetData(typeof(SummaryCardDrag)) is not SummaryCardDrag drag || !ReferenceEquals(drag.Source, SummaryCards)) return -1;
            return _cards.FindIndex(x => ReferenceEquals(x, drag.Card));
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
            DragDrop.DoDragDrop(Pages, new DataObject(typeof(KneeboardPageDrag), new KneeboardPageDrag(Pages, _dragPage)), DragDropEffects.Move);
            if (_adorner is not null) _layer?.Remove(_adorner);
            _adorner = null;
        }

        private void PageDragOver(object sender, DragEventArgs e)
        {
            e.Effects = PageDragSource(e) is null ? DragDropEffects.None : DragDropEffects.Move;
            e.Handled = true;
        }

        private void PageDrop(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            if (PageDragSource(e) is not { } page) return;
            var insertion = Pages.Items.Count;
            if (Pages.InputHitTest(e.GetPosition(Pages)) is DependencyObject element &&
                ItemsControl.ContainerFromElement(Pages, element) is DataGridRow row)
            {
                insertion = row.GetIndex();
                if (e.GetPosition(row).Y >= row.ActualHeight / 2) insertion++;
            }
            if (_context!.Working is null || ReferenceEquals(ModuleKneeboardPageEditor.Move(_context!.Working, page, insertion), _context!.Working)) return;
            _context!.RunStructureEdit(workspace => ModuleKneeboardPageEditor.Move(workspace, page, insertion));
            Pages.SelectedItem = _context!.Working.Pages.FirstOrDefault(x => x.Id == page.Id);
            if (Pages.SelectedItem is not null) Pages.ScrollIntoView(Pages.SelectedItem);
            e.Effects = DragDropEffects.Move;
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
                byte[][] images;
                if (settings.Kind == KneeboardPageKind.Summary)
                {
                    var prefix = $"KNEEBOARD/{updated.Configuration.Module.DcsId}/{settings.File}";
                    images = SnapshotSummaryKneeboardRenderer.Generate(updated)
                        .Where(x => x.Key == prefix + ".png" ||
                            (x.Key.StartsWith(prefix + "-", StringComparison.Ordinal) &&
                             x.Key.EndsWith(".png", StringComparison.Ordinal)))
                        .OrderBy(x => x.Key == prefix + ".png" ? 0 : 1)
                        .ThenBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Value).ToArray();
                }
                else
                {
                    var diagram = updated.Diagrams.Single(x => x.ProfileId == settings.ProfileId);
                    images = [CompositeKneeboardRenderer.Render(diagram,
                        KneeboardDraftProjection.CreateCombined(updated, diagram, updated.Assignments, updated.Labels), "")];
                }
                var stack = new StackPanel();
                foreach (var png in images)
                {
                    using var stream = new MemoryStream(png);
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    stack.Children.Add(new Image { Source = bitmap, Stretch = Stretch.Uniform, MaxWidth = 800, Margin = new Thickness(0, 0, 0, 12) });
                }
                new Window
                {
                    Title = "Preview · " + settings.Title,
                    Owner = Window.GetWindow(this),
                    Width = 850,
                    Height = 900,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Content = new ScrollViewer { Content = stack }
                }.ShowDialog();
            }
            catch (Exception ex) { StatusText.Text = "Preview failed: " + ex.Message; }
        }

        private void SummaryCardsResizeKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key is not (Key.Up or Key.Down)) return;
            SummaryCards.Height = Math.Clamp(SummaryCards.Height + (e.Key == Key.Down ? 20 : -20), SummaryCards.MinHeight, SummaryCards.MaxHeight);
            e.Handled = true;
        }

        private void SummaryCardsResizeDragged(object sender, DragDeltaEventArgs e)
        {
            SummaryCards.Height = Math.Clamp(SummaryCards.Height + e.VerticalChange, SummaryCards.MinHeight, SummaryCards.MaxHeight);
            e.Handled = true;
        }
        #endregion END private helpers
    }
}

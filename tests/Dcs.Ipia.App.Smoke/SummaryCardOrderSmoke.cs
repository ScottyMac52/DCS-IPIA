using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Infrastructure;
using DcsIpia.Presentation;

namespace Dcs.Ipia.App.Smoke
{
    /// <summary>Exercises routed summary-card drop events, selection and the real page save boundary.</summary>
    public static class SummaryCardOrderSmoke
    {
        private static readonly string[] NumberedKeys = ["01", "02", "03"];
        #region BEGIN Public
        /// <summary>Checks valid, foreign and unchanged drops and reopens the saved reordered summary.</summary>
        public static void Verify()
        {
            var path = Path.Combine(Path.GetTempPath(), "ipia-summary-drag-" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                var connection = "Data Source=" + path + ";Pooling=False";
                new SqliteMigrationRunner(connection).MigrateAsync().GetAwaiter().GetResult();
                var repository = new SqliteEditorWorkspaceRepository(connection);
                var module = new DcsModule(Guid.NewGuid(), "DRAG-TEST", "Drag test");
                var workspace = new EditorWorkspaceSnapshot(new(module, [], [], [], "smoke"), [], [], [], [], [], [],
                    new Dictionary<string, string>(), new Dictionary<string, string> { ["config/kneeboard.json"] = "{}" });
                workspace = ModuleKneeboardPageEditor.Set(workspace, null,
                    new(KneeboardPageKind.Summary, null, "summary", "Summary", "CONTROLS", 0,
                        [new("07", "First", "red"), new("08", "Second", "green"), new("09", "Third", "blue")]));
                repository.SaveAsync(workspace).GetAwaiter().GetResult();
                var window = new ModuleWorkspaceWindow(new ModuleWorkspaceViewModel(workspace), repository: repository,
                    deviceDiscovery: new EmptyDeviceDiscovery());
                window.Show();
                var tabs = (TabControl)window.FindName("ImportedTabs")!;
                tabs.SelectedItem = tabs.Items.Cast<TabItem>().Single(x => x.Header.ToString() == "Pages");
                ((DataGrid)window.FindName("AuthoredPages")!).SelectedIndex = 0;
                var cards = (ListBox)window.FindName("SummaryCards")!;
                cards.BringIntoView();
                window.UpdateLayout();
                if (!cards.AllowDrop || cards.ItemContainerGenerator.ContainerFromIndex(0) is not ListBoxItem)
                    throw new InvalidOperationException("Summary drag list was not realized or did not allow drops.");
                VerifyResizing(window, cards);
                var first = (ModuleSummaryCard)cards.Items[0];
                var point = new Point(10, cards.ActualHeight - 5);
                var foreign = new DataObject(typeof(SummaryCardDrag), new SummaryCardDrag(new ListBox(), first));
                cards.RaiseEvent(DragArgs(foreign, cards, point, DragDrop.PreviewDragOverEvent));
                var rejected = DragArgs(foreign, cards, point, DragDrop.PreviewDropEvent);
                cards.RaiseEvent(rejected);
                if (!rejected.Handled) throw new InvalidOperationException("The summary preview drop handler did not run.");
                if (rejected.Effects != DragDropEffects.None || !ReferenceEquals(cards.Items[0], first))
                    throw new InvalidOperationException("A foreign card drag changed the list.");
                var payload = new DataObject(typeof(SummaryCardDrag), new SummaryCardDrag(cards, first));
                var over = DragArgs(payload, cards, point, DragDrop.PreviewDragOverEvent);
                cards.RaiseEvent(over);
                if (over.Effects != DragDropEffects.Move) throw new InvalidOperationException("A same-list drag was rejected.");
                cards.RaiseEvent(DragArgs(payload, cards, point, DragDrop.PreviewDropEvent));
                Check(cards, "Second,Third,First");
                if (cards.SelectedIndex != 2 || ((TextBox)window.FindName("SummaryCardKey")!).Text != "03")
                    throw new InvalidOperationException("The moved card/editor selection was not refreshed.");
                // Move upward onto the upper half of the first row.
                window.UpdateLayout();
                var row = (ListBoxItem)cards.ItemContainerGenerator.ContainerFromIndex(0);
                point = row.TranslatePoint(new Point(10, row.ActualHeight / 4), cards);
                payload = new DataObject(typeof(SummaryCardDrag), new SummaryCardDrag(cards, (ModuleSummaryCard)cards.Items[2]));
                cards.RaiseEvent(DragArgs(payload, cards, point, DragDrop.PreviewDropEvent));
                Check(cards, "First,Second,Third");
                // Dropping before the same row must leave the same objects in place.
                var unchanged = (ModuleSummaryCard)cards.Items[0];
                payload = new DataObject(typeof(SummaryCardDrag), new SummaryCardDrag(cards, unchanged));
                cards.RaiseEvent(DragArgs(payload, cards, point, DragDrop.PreviewDropEvent));
                if (!ReferenceEquals(unchanged, cards.Items[0])) throw new InvalidOperationException("Same-position drop changed cards.");
                // Lower half of the last row inserts after it.
                window.UpdateLayout();
                row = (ListBoxItem)cards.ItemContainerGenerator.ContainerFromIndex(2);
                point = row.TranslatePoint(new Point(10, row.ActualHeight * 3 / 4), cards);
                cards.RaiseEvent(DragArgs(payload, cards, point, DragDrop.PreviewDropEvent));
                Check(cards, "Second,Third,First");
                var saved = repository.GetAsync(module.Id).GetAwaiter().GetResult()!;
                if (ModuleKneeboardPageEditor.Describe(saved, saved.Pages[0]).Cards[0].Key != "07")
                    throw new InvalidOperationException("A form drag bypassed Stage/Save.");
                ((Button)window.FindName("StageAuthoredPageButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var previous = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(window.Dispatcher));
                try { ((Button)window.FindName("SaveAuthoringButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
                finally { SynchronizationContext.SetSynchronizationContext(previous); }
                var deadline = DateTime.UtcNow.AddSeconds(15);
                while (true)
                {
                    saved = repository.GetAsync(module.Id).GetAwaiter().GetResult()!;
                    var savedCards = ModuleKneeboardPageEditor.Describe(saved, saved.Pages[0]).Cards;
                    if (savedCards[0].Text == "Second" && savedCards[0].Key == "01" &&
                        ((TextBlock)window.FindName("PageAuthoringStatus")!).Text.EndsWith("saved to database.", StringComparison.Ordinal)) break;
                    if (((TextBlock)window.FindName("EditorMessage")!).Text.StartsWith("Database save failed:", StringComparison.Ordinal))
                        throw new InvalidOperationException(((TextBlock)window.FindName("EditorMessage")!).Text);
                    if (DateTime.UtcNow > deadline) throw new InvalidOperationException("Summary card order did not save.");
                    window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                }
                window.Close();
            }
            finally { File.Delete(path); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void VerifyResizing(ModuleWorkspaceWindow window, ListBox cards)
        {
            var handle = window.FindName("SummaryCardsResizeHandle") as Thumb
                ?? throw new InvalidOperationException("Summary cards have no vertical resize handle.");
            if (!handle.Focusable || handle.ActualHeight < 8)
                throw new InvalidOperationException("Summary resize handle is not accessible or usable.");
            var original = cards.Items.Cast<ModuleSummaryCard>().ToArray();
            var height = cards.ActualHeight;
            handle.RaiseEvent(new DragDeltaEventArgs(0, 160) { RoutedEvent = Thumb.DragDeltaEvent });
            window.UpdateLayout();
            if (cards.ActualHeight < height + 150 || !original.SequenceEqual(cards.Items.Cast<ModuleSummaryCard>()))
                throw new InvalidOperationException("Resizing did not expand the list or changed its card order.");
            height = cards.Height;
            var keyboard = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window)!, Environment.TickCount, Key.Down)
                { RoutedEvent = UIElement.KeyDownEvent };
            handle.RaiseEvent(keyboard);
            if (!keyboard.Handled || cards.Height <= height)
                throw new InvalidOperationException("The summary list could not be expanded with the keyboard.");
            handle.RaiseEvent(new DragDeltaEventArgs(0, -10000) { RoutedEvent = Thumb.DragDeltaEvent });
            window.UpdateLayout();
            if (cards.Height != cards.MinHeight || cards.ActualHeight < 110)
                throw new InvalidOperationException("Summary resizing did not respect the minimum usable height.");
            handle.RaiseEvent(new DragDeltaEventArgs(0, 10000) { RoutedEvent = Thumb.DragDeltaEvent });
            window.UpdateLayout();
            if (cards.Height != cards.MaxHeight || cards.ActualHeight < 500)
                throw new InvalidOperationException("Summary cards could not expand enough to review all fourteen rows.");
            // Continue the existing routed drag/drop and Stage/Save checks with the expanded list.
        }
        private static DragEventArgs DragArgs(IDataObject data, ListBox target, Point point, RoutedEvent routedEvent)
        {
            var args = (DragEventArgs)Activator.CreateInstance(typeof(DragEventArgs), BindingFlags.Instance | BindingFlags.NonPublic,
                null, [data, DragDropKeyStates.LeftMouseButton, DragDropEffects.Move, target, point], null)!;
            args.RoutedEvent = routedEvent;
            return args;
        }
        private static void Check(ListBox cards, string expected)
        {
            if (!cards.Items.Cast<ModuleSummaryCard>().Select(x => x.Text).SequenceEqual(expected.Split(',')) ||
                !cards.Items.Cast<ModuleSummaryCard>().Select(x => x.Key).SequenceEqual(NumberedKeys))
                throw new InvalidOperationException("Summary drop did not reorder and renumber the rows.");
        }
        #endregion END private helpers
    }
}

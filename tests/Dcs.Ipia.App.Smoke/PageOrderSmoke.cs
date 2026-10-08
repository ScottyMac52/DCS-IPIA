using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Infrastructure;
using DcsIpia.Presentation;

namespace Dcs.Ipia.App.Smoke
{
    /// <summary>Verifies routed page-row drops, automatic order and the real draft save boundary.</summary>
    public static class PageOrderSmoke
    {
        #region BEGIN Public
        /// <summary>Moves rows in both directions and reopens their saved order from SQLite.</summary>
        public static void Verify()
        {
            var path = Path.Combine(Path.GetTempPath(), "ipia-page-drag-" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                var connection = "Data Source=" + path + ";Pooling=False";
                new SqliteMigrationRunner(connection).MigrateAsync().GetAwaiter().GetResult();
                var repository = new SqliteEditorWorkspaceRepository(connection);
                var module = new DcsModule(Guid.NewGuid(), "PAGE-DRAG", "Page drag");
                var workspace = new EditorWorkspaceSnapshot(new(module, [], [], [], "smoke"), [], [], [], [], [], [],
                    new Dictionary<string, string>(), new Dictionary<string, string> { ["config/kneeboard.json"] = "{}" });
                for (var i = 0; i < 3; i++)
                    workspace = ModuleKneeboardPageEditor.Set(workspace, null,
                        new(KneeboardPageKind.Summary, null, "summary-" + i, "Summary " + i, "CONTROLS", i + 10,
                            [new("01", "Card " + i, "cyan")]));
                repository.SaveAsync(workspace).GetAwaiter().GetResult();
                var original = workspace.Pages.ToArray();
                var window = new ModuleWorkspaceWindow(new ModuleWorkspaceViewModel(workspace), repository: repository,
                    deviceDiscovery: new EmptyDeviceDiscovery());
                window.Show();
                var tabs = (TabControl)window.FindName("ImportedTabs")!;
                tabs.SelectedItem = tabs.Items.Cast<TabItem>().Single(x => x.Header.ToString() == "Pages");
                var pages = (DataGrid)window.FindName("AuthoredPages")!;
                window.UpdateLayout();
                if (!pages.AllowDrop || pages.CanUserSortColumns)
                    throw new InvalidOperationException("Page rows do not support output-order drag/drop.");
                var first = (KneeboardPage)pages.Items[0];
                var point = new Point(10, pages.ActualHeight - 5);
                var foreign = new DataObject(typeof(KneeboardPageDrag), new KneeboardPageDrag(new DataGrid(), first));
                var rejected = DragArgs(foreign, pages, point, DragDrop.PreviewDropEvent);
                pages.RaiseEvent(rejected);
                if (!rejected.Handled || rejected.Effects != DragDropEffects.None || !ReferenceEquals(first, pages.Items[0]))
                    throw new InvalidOperationException("A foreign page drag changed the draft.");
                var payload = new DataObject(typeof(KneeboardPageDrag), new KneeboardPageDrag(pages, first));
                var over = DragArgs(payload, pages, point, DragDrop.PreviewDragOverEvent);
                pages.RaiseEvent(over);
                if (over.Effects != DragDropEffects.Move) throw new InvalidOperationException("A current page drag was rejected.");
                pages.RaiseEvent(DragArgs(payload, pages, point, DragDrop.PreviewDropEvent));
                Check(pages, [original[1].Id, original[2].Id, original[0].Id]);
                if ((pages.SelectedItem as KneeboardPage)?.Id != first.Id || ((TextBox)window.FindName("AuthoredPageOrder")!).Text != "2")
                    throw new InvalidOperationException("Moved page selection or order field was not refreshed.");
                window.UpdateLayout();
                var row = (DataGridRow)pages.ItemContainerGenerator.ContainerFromIndex(0);
                point = row.TranslatePoint(new Point(10, row.ActualHeight / 4), pages);
                payload = new DataObject(typeof(KneeboardPageDrag), new KneeboardPageDrag(pages, (KneeboardPage)pages.Items[2]));
                pages.RaiseEvent(DragArgs(payload, pages, point, DragDrop.PreviewDropEvent));
                Check(pages, original.Select(x => x.Id).ToArray());
                var unchanged = (KneeboardPage)pages.Items[0];
                payload = new DataObject(typeof(KneeboardPageDrag), new KneeboardPageDrag(pages, unchanged));
                pages.RaiseEvent(DragArgs(payload, pages, point, DragDrop.PreviewDropEvent));
                if (!ReferenceEquals(unchanged, pages.Items[0])) throw new InvalidOperationException("An unchanged drop rewrote the pages.");
                window.UpdateLayout();
                row = (DataGridRow)pages.ItemContainerGenerator.ContainerFromIndex(2);
                point = row.TranslatePoint(new Point(10, row.ActualHeight * 3 / 4), pages);
                pages.RaiseEvent(DragArgs(payload, pages, point, DragDrop.PreviewDropEvent));
                var expected = new[] { original[1].Id, original[2].Id, original[0].Id };
                Check(pages, expected);
                var saved = repository.GetAsync(module.Id).GetAwaiter().GetResult()!;
                if (!saved.Pages.Select(x => x.Id).SequenceEqual(original.Select(x => x.Id)))
                    throw new InvalidOperationException("Page drop bypassed Save to database.");
                var previous = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(window.Dispatcher));
                try { ((Button)window.FindName("SaveAuthoringButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
                finally { SynchronizationContext.SetSynchronizationContext(previous); }
                var deadline = DateTime.UtcNow.AddSeconds(15);
                while (true)
                {
                    saved = repository.GetAsync(module.Id).GetAwaiter().GetResult()!;
                    if (saved.Pages.Select(x => x.Id).SequenceEqual(expected) && saved.Pages.Select(x => x.Order).SequenceEqual(Enumerable.Range(0, 3))) break;
                    if (DateTime.UtcNow > deadline) throw new InvalidOperationException("Reordered pages did not save.");
                    window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                }
                foreach (var page in original)
                    if (ModuleKneeboardPageEditor.Describe(saved, saved.Pages.Single(x => x.Id == page.Id)).Cards.Single().Text !=
                        ModuleKneeboardPageEditor.Describe(workspace, page).Cards.Single().Text)
                        throw new InvalidOperationException("Page reordering changed summary content.");
                window.Close();
            }
            finally { File.Delete(path); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static DragEventArgs DragArgs(IDataObject data, DataGrid target, Point point, RoutedEvent routedEvent)
        {
            var args = (DragEventArgs)Activator.CreateInstance(typeof(DragEventArgs), BindingFlags.Instance | BindingFlags.NonPublic,
                null, [data, DragDropKeyStates.LeftMouseButton, DragDropEffects.Move, target, point], null)!;
            args.RoutedEvent = routedEvent;
            return args;
        }
        private static void Check(DataGrid pages, IReadOnlyList<Guid> expected)
        {
            if (!pages.Items.Cast<KneeboardPage>().Select(x => x.Id).SequenceEqual(expected) ||
                !pages.Items.Cast<KneeboardPage>().Select(x => x.Order).SequenceEqual(Enumerable.Range(0, expected.Count)))
                throw new InvalidOperationException("Page rows did not reorder and renumber.");
        }
        #endregion END private helpers
    }
}

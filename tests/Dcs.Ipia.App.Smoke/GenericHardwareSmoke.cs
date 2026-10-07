using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Input;
using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Infrastructure;
using DcsIpia.Presentation;

namespace Dcs.Ipia.App.Smoke
{
    /// <summary>Exercises generic inventory selection, empty creation and saved pagination in real WPF.</summary>
    public static class GenericHardwareSmoke
    {
        #region BEGIN Public
        /// <summary>Creates repeated controllers through the dialog and edits their generic controls without artwork.</summary>
        public static void Verify()
        {
            var folder = Path.Combine(Path.GetTempPath(), "ipia-generic-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var connection = "Data Source=" + Path.Combine(folder, "ipia.db") + ";Pooling=False";
                new SqliteMigrationRunner(connection).MigrateAsync().GetAwaiter().GetResult();
                var capabilities = ControllerCapabilityMapper.Map(Enumerable.Range(0, 40).Select(x =>
                    new ControllerObject(Guid.Parse("a36d02f0-c9f3-11cf-bfc7-444553540000"), 12u | ((uint)x << 8), "Button " + (x + 1))));
                var definition = new HardwareDefinition(ControllerCapabilityMapper.Fingerprint(capabilities), capabilities);
                var inventory = new SqliteHardwareInventoryRepository(connection);
                inventory.SaveAsync(new(1, [definition], [new(Guid.NewGuid(), "Repeated generic panel", definition.Id, "Left", "Pilot", true, []),
                    new(Guid.NewGuid(), "Repeated generic panel", definition.Id, "Right", "Pilot", true, [])], [])).GetAwaiter().GetResult();
                var manager = new HardwareInventoryManager(inventory, new InventorySmokeDiscovery());
                var repository = new SqliteEditorWorkspaceRepository(connection);
                var sources = new Dictionary<string, string> { ["config/ipia-hardware.json"] = "[]" };
                var defaults = new SharedUiLayerWorkspace(0, new string('a', 40), new Dictionary<string, string>
                {
                    ["assets/shared/ui-layer/functions.json"] = """{"functions":[{"id":"pause","command":"d3001pnilu3001cdnilvd1vpnilvunil","label":"Pause","category":"General"}]}"""
                });
                var creation = new NewModuleWindow(repository, defaults, sources, inventory: manager);
                Exception? failure = null;
                var submitted = false;
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
                var deadline = DateTime.UtcNow.AddSeconds(30);
                timer.Tick += (_, _) =>
                {
                    var choices = (ComboBox)creation.FindName("InventoryChoices")!;
                    if (submitted)
                    {
                        if (creation.CreatedWorkspace is not null) { timer.Stop(); return; }
                        if (((Button)creation.FindName("CreateButton")!).IsEnabled || DateTime.UtcNow >= deadline)
                        {
                            failure = new InvalidOperationException("Generic creation failed: " + ((TextBlock)creation.FindName("CreationError")!).Text);
                            timer.Stop(); creation.Close();
                        }
                        return;
                    }
                    if (choices.Items.Count != 2 && DateTime.UtcNow < deadline) return;
                    try
                    {
                        if (choices.Items.Count != 2 || ((CheckBox)creation.FindName("EmptyDefaults")!).IsChecked != true)
                            throw new InvalidOperationException("Neutral creation or repeated inventory selection is missing.");
                        ((TextBox)creation.FindName("DcsIdInput")!).Text = "GENERIC-SMOKE";
                        ((TextBox)creation.FindName("DisplayNameInput")!).Text = "Generic smoke";
                        for (var index = 0; index < 2; index++)
                        {
                            choices.SelectedIndex = index;
                            Click((Button)creation.FindName("AddUnitButton")!);
                        }
                        if (((ListBox)creation.FindName("SelectedUnits")!).Items.Count != 2)
                            throw new InvalidOperationException("Generic units were not explicitly added: " + ((TextBlock)creation.FindName("CreationError")!).Text);
                        submitted = true;
                        Click((Button)creation.FindName("CreateButton")!);
                    }
                    catch (Exception ex) { failure = ex; timer.Stop(); creation.Close(); }
                };
                creation.Closed += (_, _) => timer.Stop();
                timer.Start();
                if (creation.ShowDialog() != true) throw failure ?? new InvalidOperationException("Generic module creation failed.");
                var workspace = creation.CreatedWorkspace!;
                if (workspace.Configuration.Profiles.Count != 2 || workspace.SharedDefault is not null || workspace.Assignments.Count != 0 ||
                    workspace.Diagrams.Any(x => x.GenericCapabilities is null))
                    throw new InvalidOperationException("Generic creation copied personal defaults or lost distinct physical units.");
                var editor = new ModuleWorkspaceWindow(new ModuleWorkspaceViewModel(workspace), repository: repository, inventory: manager);
                editor.Show();
                var units = (ComboBox)editor.FindName("InventoryUnits")!;
                Pump(editor, () => units.Items.Count == 2);
                ((ComboBox)editor.FindName("DevicesScope")!).SelectedIndex = 1;
                units.SelectedIndex = 0;
                Click((Button)editor.FindName("AddInventoryButton")!);
                var message = ((TextBlock)editor.FindName("DevicesMessage")!).Text;
                if (!message.StartsWith("Change staged", StringComparison.Ordinal)) throw new InvalidOperationException(message);
                var canvas = (Canvas)editor.FindName("Diagram")!;
                var target = canvas.Children.OfType<Canvas>().Single().Children.OfType<Border>()
                    .First(x => x.Tag is HardwareGraphicControl control && control.InputKey == "JOY_BTN1");
                target.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                    { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });
                ((TextBox)editor.FindName("GenericControlName")!).Text = "Master arm";
                Click((Button)editor.FindName("RenameGenericControlButton")!);
                Click((Button)editor.FindName("SaveAuthoringButton")!);
                Pump(editor, () => ((TextBlock)editor.FindName("PendingSummary")!).Text == "No pending assignment changes");
                var pages = (ComboBox)editor.FindName("GenericPageChoice")!;
                ((ListBox)editor.FindName("KneeboardList")!).SelectedIndex = 0;
                Pump(editor, () => pages.Items.Count > 1);
                pages.SelectedIndex = 1;
                if (!((TextBlock)editor.FindName("KneeboardCaption")!).Text.Contains("-002.png", StringComparison.Ordinal) ||
                    ((Image)editor.FindName("KneeboardPreview")!).Source is null)
                    throw new InvalidOperationException("Saved generic pagination did not select the packaged second page.");
                editor.Close();
                var reopened = repository.GetAsync(workspace.Configuration.Module.Id).GetAwaiter().GetResult()!;
                if (WorkspaceStructureEditor.Describe(reopened).Devices.Count != 3 ||
                    reopened.Diagrams.Count(x => x.Controls.Any(control => control.Label == "Master arm")) != 1)
                    throw new InvalidOperationException("Generic aircraft/UI scope memberships did not survive SQLite reopen.");
            }
            finally { Directory.Delete(folder, true); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void Click(Button button)
        {
            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(button.Dispatcher));
            try { button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }
        }
        private static void Pump(Window window, Func<bool> ready)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (!ready())
            {
                if (DateTime.UtcNow >= deadline) throw new InvalidOperationException("Generic hardware UI timed out.");
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
            }
        }
        #endregion END private helpers
    }
}

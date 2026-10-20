using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Dcs;
using DcsIpia.Features.Assignments;

namespace DcsIpia.Presentation
{
    /// <summary>Drives approved training actions through real feature controls and their normal application boundaries.</summary>
    public sealed class TrainingActions : IDisposable
    {
        private readonly TrainingEnvironment _environment;
        private readonly Action<Window> _mount;
        private readonly Action<FrameworkElement> _highlight;
        private readonly Func<FrameworkElement, FrameworkElement, Task> _animateDrag;
        private ContextMenu? _menu;
        private Window? _form;
        private ModuleWorkspaceWindow? _workspace;
        private TaskCompletionSource? _formCompletion;
        private const string Light = "d3001pnilu3001cd1vd1vpnilvu0";
        private const string Shifted = "d3002pnilu3002cd1vd1vpnilvu0";
        private const string Pitch = "a2001cdnil";
        /// <summary>Connects an isolated environment to the visual training shell.</summary>
        public TrainingActions(TrainingEnvironment environment, Action<Window> mount, Action<FrameworkElement> highlight,
            Func<FrameworkElement, FrameworkElement, Task> animateDrag)
        {
            _environment = environment;
            _mount = mount;
            _highlight = highlight;
            _animateDrag = animateDrag;
        }
        #region BEGIN Public
        /// <summary>Gets the only actions scenario data may request.</summary>
        public static IReadOnlySet<string> Supported { get; } = new HashSet<string>(StringComparer.Ordinal)
        {
            "drag-ready", "drag", "context-label", "context-clear", "clear", "context-axis", "axis-options", "axis-apply",
            "toggle", "toggled", "switches", "test-start", "test-base", "test-hold", "test-release", "test-toggle",
            "test-toggle-release", "test-toggle-off", "test-switches", "test-axes", "test-stop",
            "new", "identify", "add-device", "create", "import-review", "import", "devices", "catalog", "base", "modifier",
            "shifted", "label", "axis", "ui", "save", "discard", "reopen", "pages", "kneeboard", "validate", "snapshot", "package", "finish"
        };
        /// <summary>Executes one real UI action, verifies its result, and highlights the narrated target.</summary>
        public async Task ExecuteAsync(TrainingStep step)
        {
            switch (step.Action)
            {
                case "new": OpenNew(); break;
                case "identify": Identify(); break;
                case "add-device": AddDevice(); break;
                case "create": await CreateAsync(); break;
                case "import-review": await ReviewImportAsync(); break;
                case "import": await ImportAsync(); break;
                case "devices": SelectDevice(); break;
                case "catalog": ImportCatalog(); break;
                case "base": Assign("JOY_BTN1", Light, "Base", false); break;
                case "modifier": AddModifier("TRAIN_SHIFT", "JOY_BTN3", ModifierMode.Hold); break;
                case "shifted": Assign("JOY_BTN1", Shifted, "TRAIN_SHIFT", false); break;
                case "drag-ready": SelectPalette("JOY_BTN5", Light, "Base", false); break;
                case "drag": await DragAssignAsync("JOY_BTN5", Light); break;
                case "context-label": OpenContext("JOY_BTN1"); break;
                case "context-clear": OpenContext("JOY_BTN5"); break;
                case "clear": MenuClick("Clear assignment");
                    Require(!_workspace!._draft!.Snapshot.Any(x => x.Slot.InputKey == "JOY_BTN5" && x.Slot.ModifierChord.Length == 0 && x.Slot.Layer == AssignmentLayer.Module), "Context clear did not remove the assignment."); break;
                case "label": ContextLabel(); break;
                case "toggle": AddModifier("TRAIN_TOGGLE", "JOY_BTN4", ModifierMode.Toggle); break;
                case "toggled": Assign("JOY_BTN1", ButtonCommand(3), "TRAIN_TOGGLE", false); break;
                case "switches": MapSwitches(); break;
                case "context-axis": OpenContext("JOY_Y"); break;
                case "axis-options": AxisOptions(); break;
                case "axis-apply": ApplyAxis(); break;
                case "test-start": StartTest(); break;
                case "test-base": await SampleAsync(("JOY_BTN1", 1)); break;
                case "test-hold": await SampleAsync(("JOY_BTN3", 1)); break;
                case "test-release": await SampleAsync(("JOY_BTN3", 0), ("JOY_BTN1", 0)); break;
                case "test-toggle": await SampleAsync(("JOY_BTN4", 1)); break;
                case "test-toggle-release": await SampleAsync(("JOY_BTN4", 0), ("JOY_BTN1", 1)); break;
                case "test-toggle-off": await SampleAsync(("JOY_BTN4", 1), ("JOY_BTN1", 0));
                    await SampleAsync(("JOY_BTN4", 0)); break;
                case "test-switches": await TestSwitchesAsync(); break;
                case "test-axes": await SampleAsync(("JOY_X", 0.75), ("JOY_Y", 0.25)); break;
                case "test-stop": Click(Get<Button>(Navigate("InputTest"), "StopInputTestButton")); break;
                case "axis": TuneAxis(); break;
                case "ui": Assign("JOY_BTN2", "d52pnilunilcdnilvdnilvpnilvunil", "Base", true); break;
                case "save": await SaveAsync(); break;
                case "discard": DiscardExample(); break;
                case "reopen": await ReopenAsync(); break;
                case "pages": await PagesAsync(); break;
                case "kneeboard": Preview(); break;
                case "validate": await OutputAsync("ValidateModuleButton", "Validated "); break;
                case "snapshot": await OutputAsync("ExportModuleSnapshotButton", "Exported module snapshot:"); break;
                case "package": await OutputAsync("BuildButton", "Wrote "); break;
                case "finish": break;
                default: throw new InvalidDataException("Unsupported training action: " + step.Action);
            }
            await Dispatcher.Yield(DispatcherPriority.Loaded);
            var target = Resolve(step.Target);
            target.BringIntoView();
            target.UpdateLayout();
            _highlight(target);
            // ScrollViewer applies queued offsets during layout. Finish the step after that work settles.
            await Dispatcher.Yield(DispatcherPriority.Background);
        }
        /// <summary>Releases the isolated views and simulated input source.</summary>
        public void Dispose()
        {
            Close();
            GC.SuppressFinalize(this);
        }
        /// <summary>Detaches workspace subscriptions and discards only the isolated draft.</summary>
        public void Close()
        {
            if (_menu is not null) { _menu.IsOpen = false; _menu = null; }
            if (_workspace is not null)
            {
                _workspace.DiscardClicked(null!, null!);
                _workspace.Close();
                _workspace = null;
            }
            _form?.Close();
            _form = null;
        }
        #endregion END Public
        #region BEGIN private helpers
        private void OpenNew()
        {
            _formCompletion = new TaskCompletionSource();
            var form = new NewModuleWindow(_environment.Repository, _environment.Defaults, _environment.Hardware,
                new TrainingDiscovery(), completed: () => _formCompletion.SetResult(), savedGamesRoot: _environment.ImportFolder, training: _environment);
            _form = form;
            _mount(form);
            Get<CheckBox>(form, "EmptyDefaults").IsChecked = false;
            Get<ComboBox>(form, "PhysicalChoices").ItemsSource = new[] { TrainingDiscovery.Unit };
        }
        private void Identify()
        {
            Get<TextBox>(_form!, "DcsIdInput").Text = "TRAINING";
            Get<TextBox>(_form!, "DisplayNameInput").Text = "HEIMDALL Training Aircraft";
        }
        private void AddDevice()
        {
            var physical = Get<ComboBox>(_form!, "PhysicalChoices");
            physical.SelectedIndex = 0;
            var hardware = Get<ComboBox>(_form!, "HardwareChoices");
            hardware.SelectedItem = hardware.Items.Cast<WorkspaceHardware>().Single(x => x.DeviceId == "training-joystick");
            var baseline = Get<ComboBox>(_form!, "BaselineChoices");
            baseline.SelectedItem = baseline.Items.Cast<string>().Single(x => x == TrainingDiscovery.Unit.FileName);
            Click(Get<Button>(_form!, "AddUnitButton"));
            Require(Get<ListBox>(_form!, "SelectedUnits").Items.Count == 1, "Physical unit was not added.");
        }
        private async Task CreateAsync()
        {
            Click(Get<Button>(_form!, "CreateButton"));
            await WaitForFormAsync("CreationError");
            var created = ((NewModuleWindow)_form!).CreatedWorkspace ?? throw new InvalidDataException("Module creation did not persist.");
            OpenWorkspace(created);
        }
        private async Task ReviewImportAsync()
        {
            var folder = await new DcsFolderImporter().ImportAsync(_environment.ImportFolder);
            Require(folder.Succeeded, string.Join("; ", folder.Diagnostics.Select(x => x.Message)));
            _formCompletion = new TaskCompletionSource();
            _form = new SavedGamesImportWindow(folder, _environment.Defaults, _environment.Hardware, _environment.Repository,
                () => _formCompletion.SetResult(), training: _environment);
            _mount(_form);
            foreach (var choice in Get<ItemsControl>(_form, "ProfileChoices").Items.Cast<SavedGamesProfileChoice>())
                choice.Hardware = choice.HardwareChoices.Single(x => x.DeviceId == "training-joystick");
            Get<ItemsControl>(_form, "ProfileChoices").Items.Refresh();
        }
        private async Task ImportAsync()
        {
            Click(Get<Button>(_form!, "ImportSavedGamesButton"));
            await WaitForFormAsync("ImportError");
            var saved = ((SavedGamesImportWindow)_form!).ImportedWorkspace ?? throw new InvalidDataException("Import did not persist.");
            Require(saved.Assignments.Any(x => x.Filter is not null) && saved.Assignments.Any(x => x.Slot.ModifierChord == "TRAIN_SHIFT"),
                "Import lost tuning or modifier assignments.");
            Require(File.ReadAllText(Path.Combine(_environment.ImportFolder, "joystick", TrainingDiscovery.Unit.FileName)) == TrainingAssets.Read("profile.lua"),
                "Import changed its source fixture.");
            OpenWorkspace(saved);
        }
        private async Task WaitForFormAsync(string errorControl)
        {
            try { await _formCompletion!.Task.WaitAsync(TimeSpan.FromSeconds(30)); }
            catch (TimeoutException ex)
            {
                throw new InvalidDataException("Form did not complete: " + Get<TextBlock>(_form!, errorControl).Text, ex);
            }
        }
        private void OpenWorkspace(EditorWorkspaceSnapshot saved)
        {
            _workspace = new ModuleWorkspaceWindow(new ModuleWorkspaceViewModel(saved), persistLayout: false, repository: _environment.Repository,
                deviceDiscovery: new TrainingDiscovery(), commandFilePicker: _ => [Path.Combine(_environment.Root, "commands.json")],
                outputPathPicker: snapshot => snapshot ? Path.Combine(_environment.Outputs, "training.ipia-module.json") : _environment.Outputs,
                documentationFolderPicker: () => _environment.Outputs, completeReleaseFolderPicker: () => _environment.Outputs,
                integrationFilePicker: () => null, axisDefaultsFilePicker: () => null,
                training: _environment);
            _workspace._savedGamesRoot = _environment.ImportFolder;
            _mount(_workspace);
        }
        private UserControl Navigate(string feature)
        {
            var nav = Get<ListBox>(_workspace!, "FeatureNav");
            nav.SelectedItem = nav.Items.Cast<ListBoxItem>().Single(x => Equals(x.Tag, feature));
            var view = (UserControl)Get<ContentControl>(_workspace!, "FeatureContent").Content;
            view.UpdateLayout();
            return view;
        }
        private void SelectDevice()
        {
            var view = Navigate("Devices");
            var devices = Get<DataGrid>(view, "ManagedDevices");
            devices.SelectedIndex = 0;
            Require(devices.SelectedItem is not null, "No sample device in the workspace.");
        }
        private void ImportCatalog()
        {
            var view = Get<UserControl>(Navigate("Commands"), "Manager");
            WithDialog<CommandImportReviewWindow>(() => Click(Action(view, "Import JSON…")),
                dialog => Click(Get<Button>(dialog, "StageImportButton")));
            Require(_workspace!._working!.ModuleCommands.Count >= 3 &&
                _workspace._working.Sources!.Keys.Any(x => x.StartsWith("config/ipia-command-imports/", StringComparison.Ordinal)),
                "Command catalog import failed.");
        }
        private void AddModifier(string name, string key, ModifierMode mode)
        {
            var view = Navigate("Modifiers");
            var rows = Get<DataGrid>(view, "ManagedModifiers");
            if (!rows.Items.Cast<WorkspaceModifier>().Any(x => x.Name == name))
            {
                Click(Action(view, "New"));
                Get<TextBox>(view, "ModifierName").Text = name;
                Get<TextBox>(view, "ModifierSemantic").Text = name;
                Get<ComboBox>(view, "ModifierModeChoice").SelectedItem = mode;
                Get<ComboBox>(view, "ModifierDevice").SelectedIndex = 0;
                var control = Get<ComboBox>(view, "ModifierControl");
                control.SelectedItem = control.Items.Cast<HardwareGraphicControl>().First(x => x.InputKey == key);
                Click(Action(view, "Add"));
            }
            rows.SelectedItem = rows.Items.Cast<WorkspaceModifier>().FirstOrDefault(x => x.Name == name)
                ?? throw new InvalidDataException("Modifier was not staged: " + Get<TextBlock>(_workspace!, "EditorMessage").Text +
                    " " + Get<TextBlock>(view, "StatusMessage").Text);
        }
        private UserControl SelectAssignment(string key, string layer, bool ui)
        {
            var view = Navigate("Assignments");
            Get<ComboBox>(view, "Scope").SelectedIndex = ui ? 1 : 0;
            var device = Get<ComboBox>(view, "DeviceChoice");
            device.SelectedIndex = 0;
            Get<ComboBox>(view, "Layers").SelectedItem = layer;
            Require(Equals(Get<ComboBox>(view, "Layers").SelectedItem, layer), "Modifier layer is unavailable: " + layer);
            view.UpdateLayout();
            Click(Get<Button>(view, "FitDiagramButton"));
            view.UpdateLayout();
            var diagram = Get<Canvas>(view, "Diagram");
            var border = Descendants(diagram).OfType<Border>().FirstOrDefault(x => x.Tag is HardwareGraphicControl control &&
                control.InputKey == key && !control.Control.EndsWith("-shifted", StringComparison.Ordinal))
                ?? throw new InvalidDataException("Missing sample callout: " + key);
            border.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });
            return view;
        }
        private UserControl SelectPalette(string key, string command, string layer, bool ui)
        {
            var view = SelectAssignment(key, layer, ui);
            var manager = Get<UserControl>(view, "CommandManager");
            var grid = Get<DataGrid>(manager, "Commands");
            grid.SelectedItem = grid.Items.Cast<object>().Single(x => Equals(x.GetType().GetProperty("Id")!.GetValue(x), command));
            grid.ScrollIntoView(grid.SelectedItem);
            return view;
        }
        private void Assign(string key, string command, string layer, bool ui)
        {
            var view = SelectPalette(key, command, layer, ui);
            Click(Action(view, "Assign selected command"));
            Require(_workspace!._draft!.Snapshot.Any(x => x.Command.Id == command && x.Slot.InputKey == key &&
                x.Slot.ModifierChord == (layer == "Base" ? "" : layer) && x.Slot.Layer == (ui ? AssignmentLayer.UiLayer : AssignmentLayer.Module)),
                "Assignment did not reach the draft.");
        }
        private void SetLabel(string text)
        {
            var view = SelectAssignment("JOY_BTN1", "Base", false);
            Get<TextBox>(view, "LabelEditor").Text = text;
            Require(_workspace!._labels.Values.Contains(text, StringComparer.Ordinal), "Label was not staged.");
        }
        private static string ButtonCommand(int index) => $"d{3000 + index}pnilu{3000 + index}cd1vd1vpnilvu0";
        private static Border Callout(UserControl view, string key) => Descendants(Get<Canvas>(view, "Diagram")).OfType<Border>()
            .First(x => x.Tag is HardwareGraphicControl control && control.InputKey == key);
        private async Task DragAssignAsync(string key, string command)
        {
            var view = SelectPalette(key, command, "Base", false);
            var grid = Get<DataGrid>(Get<UserControl>(view, "CommandManager"), "Commands");
            view.UpdateLayout();
            var source = grid.ItemContainerGenerator.ContainerFromItem(grid.SelectedItem) as FrameworkElement ?? grid;
            var target = Callout(view, key);
            await _animateDrag(source, target);
            var definition = _workspace!._working!.ModuleCommands.Single(x => x.Id == command);
            ((AssignmentsView)view).DropPaletteCommand((HardwareGraphicControl)target.Tag,
                new(definition.Id, definition.Name, definition.Category, definition.IsAxis));
            Require(_workspace._draft!.Snapshot.Any(x => x.Slot.InputKey == key && x.Command.Id == command), "Palette drop was not applied.");
        }
        private void OpenContext(string key)
        {
            var view = SelectAssignment(key, "Base", false);
            var target = Callout(view, key);
            _highlight(target);
            _menu = target.ContextMenu ?? throw new InvalidDataException("Callout has no context menu.");
            _menu.PlacementTarget = target;
            _menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            _menu.StaysOpen = true;
            _menu.IsOpen = true;
        }
        private void MenuClick(string caption)
        {
            var item = _menu?.Items.OfType<MenuItem>().Single(x => Equals(x.Header, caption))
                ?? throw new InvalidDataException("Training context menu is not open.");
            _menu!.IsOpen = false;
            _menu = null;
            Require(item.IsEnabled, "Context action is disabled: " + caption);
            item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        }
        private void ContextLabel()
        {
            WithDialog<Window>(() => MenuClick("Change label…"), dialog =>
            {
                Descendants(dialog).OfType<TextBox>().Single().Text = "LIGHTS";
                Click(Action(dialog, "OK"));
            });
            Require(_workspace!._labels.Values.Contains("LIGHTS", StringComparer.Ordinal), "Context label was not staged.");
        }
        private void TuneAxis()
        {
            Assign("JOY_Y", Pitch, "Base", false);
            Assign("JOY_X", "a2002cdnil", "Base", false);
            SelectAssignment("JOY_Y", "Base", false);
        }
        private void AxisOptions()
        {
            var view = (AssignmentsView)Navigate("Assignments");
            view.AxisTuningPresenter = dialog => { _form = dialog; _mount(dialog); };
            try { MenuClick("Tune axis…"); }
            finally { view.AxisTuningPresenter = null; }
            Require(_form is AxisTuningWindow, "Axis editor was not opened from the context menu.");
            Get<TextBox>(_form!, "Deadzone").Text = "3";
            Get<TextBox>(_form!, "Curvature").Text = "15";
            Get<TextBox>(_form!, "SaturationX").Text = "100";
            Get<TextBox>(_form!, "SaturationY").Text = "100";
        }
        private void ApplyAxis()
        {
            Click(Get<Button>(_form!, "ApplyToDraftButton"));
            Require(_workspace!._draft!.Snapshot.Any(x => x.Slot.InputKey == "JOY_Y" && x.Filter?.Deadzone == 3), "Axis tuning was not staged.");
            _mount(_workspace);
            _form!.Close();
            _form = null;
            SelectAssignment("JOY_Y", "Base", false);
        }
        private void MapSwitches()
        {
            for (var index = 0; index < 5; index++)
                Assign("JOY_BTN" + (6 + index), ButtonCommand(4 + index), "Base", false);
        }
        private void StartTest()
        {
            var view = Navigate("InputTest");
            Get<ComboBox>(view, "InputTestSnapshotChoice").SelectedIndex = 1;
            Get<CheckBox>(view, "ShowAxis").IsChecked = true;
            Get<Slider>(view, "Zoom").Value = 0.4;
            Click(Get<Button>(view, "StartInputTestButton"));
            Require(Get<TextBlock>(view, "InputTestStatus").Text.StartsWith("Testing frozen saved profile", StringComparison.Ordinal),
                Get<TextBlock>(view, "InputTestStatus").Text);
        }
        private async Task SampleAsync(params (string Key, double Value)[] values)
        {
            foreach (var (key, value) in values) _environment.Input!.Set(key, value);
            // The actual input-test timer consumes the sample, resolves generated Lua and updates the legend/log.
            var log = Get<ListBox>(Navigate("InputTest"), "InputTestLog");
            var count = log.Items.Count;
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (log.Items.Count == count)
            {
                if (DateTime.UtcNow > deadline) throw new InvalidDataException("Simulated input did not reach the tester.");
                await Task.Delay(30);
            }
        }
        private async Task TestSwitchesAsync()
        {
            await SampleAsync(("JOY_BTN6", 1), ("JOY_BTN8", 1));
            await SampleAsync(("JOY_BTN6", 0), ("JOY_BTN7", 1), ("JOY_BTN8", 0), ("JOY_BTN9", 1));
            await SampleAsync(("JOY_BTN9", 0), ("JOY_BTN10", 1));
        }
        private async Task SaveAsync()
        {
            await _workspace!.SaveWorkspaceAsync();
            var reopened = await _environment.Repository.GetAsync(_workspace._saved!.Configuration.Module.Id);
            Require(reopened is not null && reopened.Assignments.Count == _workspace._draft!.Snapshot.Count, "Save did not persist assignments.");
        }
        private void DiscardExample()
        {
            SetLabel("TEMPORARY - discard this");
            var view = Navigate("Assignments");
            Click(Action(view, "Discard draft"));
            SelectAssignment("JOY_BTN1", "Base", false);
            Require(!_workspace!._labels.Values.Contains("TEMPORARY - discard this", StringComparer.Ordinal), "Discard did not restore saved labels.");
        }
        private async Task ReopenAsync()
        {
            var saved = await _environment.Repository.GetAsync(_workspace!._saved!.Configuration.Module.Id)
                ?? throw new InvalidDataException("Saved training module is missing.");
            _workspace.DiscardClicked(null!, null!);
            _workspace.Close();
            OpenWorkspace(saved);
        }
        private async Task PagesAsync()
        {
            var view = Navigate("Pages");
            Click(Action(view, "Add missing device pages"));
            Click(Action(view, "Generate Summary"));
            await SaveAsync();
            Require(_workspace!._saved!.Pages.Any(x => x.Kind == KneeboardPageKind.Summary), "Summary pages were not created.");
        }
        private void Preview()
        {
            var view = Navigate("Kneeboards");
            var source = Get<Image>(view, "KneeboardPreview").Source
                ?? throw new InvalidDataException("Kneeboard preview failed.");
            var box = Get<Viewbox>(view, "ZoomBox");
            var viewport = Descendants(view).OfType<ScrollViewer>().Single(x => ReferenceEquals(x.Content, box));
            var fit = Math.Min((viewport.ViewportWidth - 8) / source.Width, (viewport.ViewportHeight - 8) / source.Height);
            Get<Slider>(view, "Zoom").Value = Math.Clamp(Math.Floor(fit * 10) / 10, 0.1, 1);
            view.UpdateLayout();
        }
        private async Task OutputAsync(string button, string expected)
        {
            Get<TextBox>(_workspace!, "PackageVersion").Text = "1.0.0";
            Click(Get<Button>(_workspace!, button));
            var deadline = DateTime.UtcNow.AddSeconds(60);
            while (!Get<Button>(_workspace!, button).IsEnabled)
            {
                if (DateTime.UtcNow > deadline) throw new TimeoutException("Training output did not finish.");
                await Task.Delay(50);
            }
            var status = Get<TextBlock>(_workspace!, "BuildStatus").Text;
            Require(status.StartsWith(expected, StringComparison.Ordinal), status);
        }
        private FrameworkElement Resolve(string target)
        {
            var parts = target.Split(':', 2);
            if (parts.Length != 2) throw new InvalidDataException("Invalid training target: " + target);
            FrameworkElement root = parts[0] switch
            {
                "Form" => _form ?? throw new InvalidDataException("Training form is not open."),
                "Host" => _workspace ?? throw new InvalidDataException("Training workspace is not open."),
                _ => Navigate(parts[0])
            };
            if (parts[0] == "Assignments" && parts[1].StartsWith("JOY_", StringComparison.Ordinal))
                return Descendants(Get<Canvas>(root, "Diagram")).OfType<Border>().FirstOrDefault(x =>
                    x.Tag is HardwareGraphicControl control && control.InputKey == parts[1] &&
                    !control.Control.EndsWith("-shifted", StringComparison.Ordinal))
                    ?? throw new InvalidDataException("Missing training callout: " + parts[1]);
            var element = Get<FrameworkElement>(root, parts[1]);
            // Disclosed editors remain real training targets; reveal them before visual transforms/highlighting.
            for (DependencyObject? ancestor = element; ancestor is not null; ancestor = LogicalTreeHelper.GetParent(ancestor))
                if (ancestor is Expander expander) expander.IsExpanded = true;
            root.UpdateLayout();
            return element;
        }
        private static T Get<T>(FrameworkElement root, string name) where T : class => root.FindName(name) as T
            ?? throw new InvalidDataException("Missing training control: " + name);
        private static Button Action(DependencyObject root, string caption) => ActionBar.FindButton(root, caption)
            ?? throw new InvalidDataException("Missing training action: " + caption);
        private static void Click(Button button)
        {
            Require(button.IsEnabled, "Training action is disabled: " + button.Content);
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidDataException(message);
        }
        private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
                yield return child;
                foreach (var nested in Descendants(child)) yield return nested;
            }
        }
        private static void WithDialog<T>(Action open, Action<T> act) where T : Window
        {
            Exception? failure = null;
            var operated = false;
            var operation = System.Windows.Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                var dialog = System.Windows.Application.Current.Windows.OfType<T>().SingleOrDefault(x =>
                    typeof(T) != typeof(Window) || x.Title.StartsWith("Change label", StringComparison.Ordinal));
                try
                {
                    if (dialog is null) throw new InvalidDataException("Expected training review did not open.");
                    act(dialog);
                    operated = true;
                }
                catch (Exception ex) { failure = ex; dialog?.Close(); }
            }));
            try { open(); }
            finally { if (operation.Status == DispatcherOperationStatus.Pending) operation.Abort(); }
            if (failure is not null) throw new InvalidDataException("Training review failed.", failure);
            Require(operated, "Training review was not completed.");
        }
        #endregion END private helpers
    }
}

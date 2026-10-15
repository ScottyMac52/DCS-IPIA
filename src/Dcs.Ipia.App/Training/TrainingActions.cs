using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Dcs;

namespace DcsIpia.Presentation
{
    /// <summary>Drives approved training actions through real feature controls and their normal application boundaries.</summary>
    public sealed class TrainingActions
    {
        private readonly TrainingEnvironment _environment;
        private readonly Action<Window> _mount;
        private readonly Action<FrameworkElement> _highlight;
        private Window? _form;
        private ModuleWorkspaceWindow? _workspace;
        private TaskCompletionSource? _formCompletion;
        private const string Light = "d3001pnilu3001cd1vd1vpnilvu0";
        private const string Shifted = "d3002pnilu3002cd1vd1vpnilvu0";
        private const string Pitch = "a2001cdnil";
        /// <summary>Connects an isolated environment to the visual training shell.</summary>
        public TrainingActions(TrainingEnvironment environment, Action<Window> mount, Action<FrameworkElement> highlight)
        {
            _environment = environment;
            _mount = mount;
            _highlight = highlight;
        }
        #region BEGIN Public
        /// <summary>Gets the only actions scenario data may request.</summary>
        public static IReadOnlySet<string> Supported { get; } = new HashSet<string>(StringComparer.Ordinal)
        {
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
                case "modifier": AddModifier(); break;
                case "shifted": Assign("JOY_BTN1", Shifted, "TRAIN_SHIFT", false); break;
                case "label": SetLabel("LIGHTS"); break;
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
        }
        /// <summary>Detaches workspace subscriptions and discards only the isolated draft.</summary>
        public void Close()
        {
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
                new TrainingDiscovery(), completed: () => _formCompletion.SetResult(), savedGamesRoot: _environment.ImportFolder);
            _form = form;
            _mount(form);
            Get<CheckBox>(form, "EmptyDefaults").IsChecked = false;
            Get<ComboBox>(form, "PhysicalChoices").ItemsSource = new[] { TrainingDiscovery.Unit };
        }
        private void Identify()
        {
            Get<TextBox>(_form!, "DcsIdInput").Text = "TRAINING";
            Get<TextBox>(_form!, "DisplayNameInput").Text = "IPIA Training Aircraft";
        }
        private void AddDevice()
        {
            var physical = Get<ComboBox>(_form!, "PhysicalChoices");
            physical.SelectedIndex = 0;
            var hardware = Get<ComboBox>(_form!, "HardwareChoices");
            hardware.SelectedItem = hardware.Items.Cast<WorkspaceHardware>().Single(x => x.DeviceId == "logitech-throttle-quadrant");
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
                () => _formCompletion.SetResult());
            _mount(_form);
            foreach (var choice in Get<ItemsControl>(_form, "ProfileChoices").Items.Cast<SavedGamesProfileChoice>())
                choice.Hardware = choice.HardwareChoices.Single(x => x.DeviceId == "logitech-throttle-quadrant");
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
            _workspace = new ModuleWorkspaceWindow(new ModuleWorkspaceViewModel(saved), repository: _environment.Repository,
                deviceDiscovery: new TrainingDiscovery(), commandFilePicker: _ => [Path.Combine(_environment.Root, "commands.json")],
                outputPathPicker: snapshot => snapshot ? Path.Combine(_environment.Outputs, "training.ipia-module.json") : _environment.Outputs,
                documentationFolderPicker: () => _environment.Outputs, completeReleaseFolderPicker: () => _environment.Outputs,
                integrationFilePicker: () => null, axisDefaultsFilePicker: () => null);
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
        private void AddModifier()
        {
            var view = Navigate("Modifiers");
            var rows = Get<DataGrid>(view, "ManagedModifiers");
            if (!rows.Items.Cast<WorkspaceModifier>().Any(x => x.Name == "TRAIN_SHIFT"))
            {
                Click(Action(view, "New"));
                Get<TextBox>(view, "ModifierName").Text = "TRAIN_SHIFT";
                Get<ComboBox>(view, "ModifierModeChoice").SelectedItem = ModifierMode.Hold;
                Get<ComboBox>(view, "ModifierDevice").SelectedIndex = 0;
                var control = Get<ComboBox>(view, "ModifierControl");
                control.SelectedItem = control.Items.Cast<HardwareGraphicControl>().First(x => x.InputKey == "JOY_BTN3");
                Click(Action(view, "Add"));
            }
            rows.SelectedItem = rows.Items.Cast<WorkspaceModifier>().Single(x => x.Name == "TRAIN_SHIFT");
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
            var diagram = Get<Canvas>(view, "Diagram");
            var border = Descendants(diagram).OfType<Border>().FirstOrDefault(x => x.Tag is HardwareGraphicControl control &&
                control.InputKey == key && !control.Control.EndsWith("-shifted", StringComparison.Ordinal))
                ?? throw new InvalidDataException("Missing sample callout: " + key);
            border.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });
            return view;
        }
        private void Assign(string key, string command, string layer, bool ui)
        {
            var view = SelectAssignment(key, layer, ui);
            var manager = Get<UserControl>(view, "CommandManager");
            var grid = Get<DataGrid>(manager, "Commands");
            grid.SelectedItem = grid.Items.Cast<object>().Single(x => Equals(x.GetType().GetProperty("Id")!.GetValue(x), command));
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
        private void TuneAxis()
        {
            Assign("JOY_X", Pitch, "Base", false);
            var view = SelectAssignment("JOY_X", "Base", false);
            WithDialog<AxisTuningWindow>(() => Click(Action(view, "Tune axis…")), dialog =>
            {
                Get<TextBox>(dialog, "Deadzone").Text = "3";
                Get<TextBox>(dialog, "Curvature").Text = "15";
                Click(Action(dialog, "Apply to draft"));
            });
            Require(_workspace!._draft!.Snapshot.Any(x => x.Slot.InputKey == "JOY_X" && x.Filter?.Deadzone == 3), "Axis tuning was not staged.");
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
            Require(Get<Image>(view, "KneeboardPreview").Source is not null, "Kneeboard preview failed.");
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
            return Get<FrameworkElement>(root, parts[1]);
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
                var dialog = System.Windows.Application.Current.Windows.OfType<T>().SingleOrDefault();
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

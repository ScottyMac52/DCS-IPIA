using Dcs.Ipia.Application;
using System.Windows;
using System.Windows.Controls;
using DcsIpia.Presentation;

namespace Dcs.Ipia.App.Smoke
{
    /// <summary>Accesses the refactored workspace through its real navigation and view namescopes.</summary>
    public static class WorkspaceUi
    {
        private static readonly Dictionary<string, (string Feature, string Name, bool Button)> Controls = new()
        {
            ["StartInputTestButton"] = ("InputTest", "StartInputTestButton", false),
            ["StopInputTestButton"] = ("InputTest", "StopInputTestButton", false),
            ["ClearInputTestLogButton"] = ("InputTest", "ClearInputTestLogButton", false),
            ["CopyInputTestLogButton"] = ("InputTest", "CopyInputTestLogButton", false),
            ["InputTestSnapshotChoice"] = ("InputTest", "InputTestSnapshotChoice", false),
            ["InputTestLogMessage"] = ("InputTest", "InputTestLogMessage", false),
            ["InputTestSplitter"] = ("InputTest", "InputTestSplitter", false),
                ["AddInventoryButton"] = ("Devices", "Add inventory unit with generic controls", true),
                ["AdoptCustomMappingButton"] = ("Devices", "Apply drawing to selected device", true),
                ["AircraftCommandCategory"] = ("Commands", "CommandCategory", false),
                ["AircraftCommandCode"] = ("Commands", "CommandId", false),
                ["AircraftCommandName"] = ("Commands", "CommandName", false),
                ["AuthoredCommands"] = ("Commands", "Commands", false),
                ["AuthoredPageFile"] = ("Pages", "PageFile", false),
                ["AuthoredPageKind"] = ("Pages", "PageKind", false),
                ["AuthoredPageOrder"] = ("Pages", "PageOrder", false),
                ["AuthoredPageSubtitle"] = ("Pages", "PageSubtitle", false),
                ["AuthoredPageTitle"] = ("Pages", "PageTitle", false),
                ["AuthoredPages"] = ("Pages", "Pages", false),
                ["CommandCategory"] = ("Assignments", "CommandCategoryFilter", false),
                ["Commands"] = ("Assignments", "Commands", false),
                ["CustomMappingRevisions"] = ("Devices", "CustomMappingRevisions", false),
                ["DeviceChoice"] = ("Assignments", "DeviceChoice", false),
                ["DevicesMessage"] = ("Devices", "StatusMessage", false),
                ["DevicesScope"] = ("Devices", "DevicesScope", false),
                ["Diagram"] = ("Assignments", "Diagram", false),
                ["DiscardIntegrationInstructionsButton"] = ("Documentation", "Discard draft", true),
                ["GenerateSummaryButton"] = ("Pages", "Generate Summary", true),
                ["GenericControlName"] = ("Assignments", "GenericControlName", false),
                ["GenericPageChoice"] = ("Kneeboards", "GenericPageChoice", false),
                ["ImportAxisDefaultsButton"] = ("Devices", "Import DCS axis defaults…", true),
                ["InputTestLog"] = ("InputTest", "InputTestLog", false),
                ["InputTestStatus"] = ("InputTest", "InputTestStatus", false),
                ["IntegrationInstructionText"] = ("Documentation", "InstructionText", false),
                ["IntegrationInstructions"] = ("Documentation", "Instructions", false),
                ["IntegrationStatus"] = ("Documentation", "StatusText", false),
                ["InventoryUnits"] = ("Devices", "InventoryUnits", false),
                ["KneeboardCaption"] = ("Kneeboards", "KneeboardCaption", false),
                ["KneeboardList"] = ("Kneeboards", "KneeboardList", false),
                ["KneeboardPreview"] = ("Kneeboards", "KneeboardPreview", false),
                ["Layers"] = ("Assignments", "Layers", false),
                ["LoadCommandHtmlButton"] = ("Commands", "Import HTML…", true),
                ["LoadCommandJsonButton"] = ("Commands", "Import JSON…", true),
                ["LoadIntegrationInstructionsButton"] = ("Documentation", "Load integration instructions…", true),
                ["ManagedDevices"] = ("Devices", "ManagedDevices", false),
                ["ManagedFileName"] = ("Devices", "ManagedFileName", false),
                ["ManagedModifiers"] = ("Modifiers", "ManagedModifiers", false),
                ["MfdLabelFields"] = ("Devices", "MfdLabelFields", false),
                ["MissingDevicePagesButton"] = ("Pages", "Add missing device pages", true),
                ["ModifierDevice"] = ("Modifiers", "ModifierDevice", false),
                ["ModifierModeChoice"] = ("Modifiers", "ModifierModeChoice", false),
                ["ModifierName"] = ("Modifiers", "ModifierName", false),
                ["PageAuthoringStatus"] = ("Pages", "StatusText", false),
                ["RebindDeviceButton"] = ("Devices", "Rebind physical unit…", true),
                ["RemoveAuthoredPageButton"] = ("Pages", "Remove page", true),
                ["RemoveIntegrationInstructionButton"] = ("Documentation", "Remove selected", true),
                ["RemoveManagedDeviceButton"] = ("Devices", "Remove", true),
                ["RenameGenericControlButton"] = ("Assignments", "RenameGenericControlButton", false),
                ["SaveIntegrationInstructionsButton"] = ("Documentation", "Save to database", true),
                ["Scope"] = ("Assignments", "Scope", false),
                ["SetSummaryCardButton"] = ("Pages", "Set card", true),
                ["StageAuthoredPageButton"] = ("Pages", "Stage page", true),
                ["SummaryCardKey"] = ("Pages", "CardTitle", false),
                ["SummaryCardText"] = ("Pages", "CardBody", false),
                ["SummaryCards"] = ("Pages", "SummaryCards", false),
                ["SummaryCardsResizeHandle"] = ("Pages", "SummaryCardsResize", false),
        };

        #region BEGIN Public
        /// <summary>Selects a workspace feature and returns its active user control.</summary>
        public static UserControl Navigate(ModuleWorkspaceWindow window, string feature)
        {
            var nav = (ListBox)window.FindName("FeatureNav");
            nav.SelectedItem = nav.Items.Cast<ListBoxItem>().Single(x => Equals(x.Tag, feature));
            window.UpdateLayout();
            return (UserControl)((ContentControl)window.FindName("FeatureContent")).Content;
        }

        /// <summary>Finds a host or feature control using the smoke test control-name mapping.</summary>
        public static object Find(FrameworkElement root, string name)
        {
            if (root is not ModuleWorkspaceWindow window) return root.FindName(name)!;
            if (window.FindName(name) is { } hostControl) return hostControl;
            var current = (UserControl)((ContentControl)window.FindName("FeatureContent")).Content;
            if (name is "SaveAuthoringButton" or "DiscardAuthoringButton")
                return Button(current, name == "SaveAuthoringButton" ? "Save to database" : "Discard draft");
            if (name == "StageAircraftCommandButton")
            {
                var commands = Navigate(window, "Commands");
                var manager = (UserControl)commands.FindName("Manager");
                var grid = (DataGrid)manager.FindName("Commands");
                return Button(manager, grid.SelectedItem is null ? "Add" : "Update");
            }
            if (!Controls.TryGetValue(name, out var target))
                throw new InvalidOperationException("Unmapped workspace smoke control: " + name);
            var view = Navigate(window, target.Feature);
            if (target.Feature == "Commands") view = (UserControl)view.FindName("Manager");
            if (target.Feature == "Assignments" && target.Name.StartsWith("Command", StringComparison.Ordinal))
                view = (UserControl)view.FindName("CommandManager");
            return target.Button ? Button(view, target.Name) : view.FindName(target.Name)
                ?? throw new InvalidOperationException($"Missing {target.Feature} control: {target.Name}");
        }

        /// <summary>Reads the displayed command palette entries from a grid.</summary>
        public static IReadOnlyList<CommandPaletteEntry> Entries(DataGrid grid) => grid.Items.Cast<object>().Select(row =>
            new CommandPaletteEntry((string)row.GetType().GetProperty("Id")!.GetValue(row)!,
                (string)row.GetType().GetProperty("Name")!.GetValue(row)!,
                (string)row.GetType().GetProperty("Category")!.GetValue(row)!,
                (bool)row.GetType().GetProperty("IsAxis")!.GetValue(row)!)).ToArray();

        /// <summary>Selects a displayed command by its stable command identifier.</summary>
        public static void SelectCommand(DataGrid grid, string id) => grid.SelectedItem = grid.Items.Cast<object>()
            .Single(row => Equals(row.GetType().GetProperty("Id")!.GetValue(row), id));

        /// <summary>Finds an action button by caption or reports a missing smoke test control.</summary>
        public static Button Button(DependencyObject view, string caption) =>
            ActionBar.FindButton(view, caption) ?? throw new InvalidOperationException("Missing action: " + caption);
        #endregion END Public
    }
}

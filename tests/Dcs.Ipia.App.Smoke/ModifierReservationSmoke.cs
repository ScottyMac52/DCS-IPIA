using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Dcs;
using DcsIpia.Presentation;

namespace Dcs.Ipia.App.Smoke
{
    /// <summary>Exercises modifier-reserved physical callouts in the real Assignments UI.</summary>
    public static class ModifierReservationSmoke
    {
        #region BEGIN Public
        /// <summary>Checks callout protection, legacy clearing, staged release, and discard.</summary>
        public static void Verify()
        {
            var module = new DcsModule(Guid.NewGuid(), "RESERVED-MFD", "Reserved inputs");
            var profile = new DeviceProfile(Guid.NewGuid(), "tm-mfd", "MFD 2", "MFD", "MFD.diff.lua");
            var configuration = new ModuleConfiguration(module, [profile],
                [new DcsModifier("S3", "MFD", "JOY_BTN7", ModifierMode.Hold)], [], "smoke");
            var svg = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 130 100'>" +
                "<rect width='130' height='100' fill='white'/>" +
                "<!-- box:reserved --><rect x='10' y='10' width='48' height='34' fill='white'/>" +
                "<text id='lbl-reserved' x='12' y='28'>BTN7</text>" +
                "<!-- box:reserved-shifted --><rect x='64' y='10' width='50' height='34' fill='white'/>" +
                "<text id='lbl-reserved-shifted' x='65' y='28'>S3</text>" +
                "<!-- box:free --><rect x='10' y='52' width='48' height='34' fill='white'/>" +
                "<text id='lbl-free' x='12' y='68'>BTN8</text></svg>";
            var diagram = new UiLayerDiagram("TM MFD", "MFD 2", "mfd.svg", 130, 100, [], [])
            {
                ProfileId = profile.Id, Svg = svg, SupportsUiLayer = true,
                Controls = [
                    new("reserved", "BTN7", "JOY_BTN7", 10, 10, 48, 34),
                    new("reserved-shifted", "SHIFT BTN7", "JOY_BTN7", 64, 10, 50, 34),
                    new("free", "BTN8", "JOY_BTN8", 10, 52, 48, 34)
                ]
            };
            var management = new WorkspaceManagement([
                new(profile.Id, AssignmentLayer.Module, "MFD.diff.lua", null, null),
                new(profile.Id, AssignmentLayer.UiLayer, "MFD.diff.lua", null, null)
            ], [
                new(AssignmentLayer.Module, profile.Id, "S3", "JOY_BTN7", "SHIFT", ModifierMode.Hold, "MFD"),
                new(AssignmentLayer.UiLayer, profile.Id, "UI SHIFT", "JOY_BTN7", "UI", ModifierMode.Toggle, "MFD")
            ]);
            var slot = new DraftAssignmentSlot(module.Id, profile.Id, AssignmentLayer.Module,
                BindingSection.Key, "JOY_BTN7");
            var workspace = new EditorWorkspaceSnapshot(configuration, [], [], [diagram],
                [new CommandPaletteEntry("CMD", "Legacy", "General", false)], [],
                [new DraftAssignment(slot, new DraftCommand("CMD", "Legacy", false, AssignmentLayer.Module))],
                new Dictionary<string, string>(), new Dictionary<string, string> {
                    ["config/kneeboard.json"] = "{}",
                    [WorkspaceManagement.SourcePath] = JsonSerializer.Serialize(management)
                });
            var window = new ModuleWorkspaceWindow(new ModuleWorkspaceViewModel(workspace),
                deviceDiscovery: new EmptyDeviceDiscovery());
            try
            {
                window.Show();
                var view = WorkspaceUi.Navigate(window, "Assignments");
                Border Callout(string name) =>
                    ((Canvas)view.FindName("Diagram")!).Children.OfType<Canvas>().Single()
                        .Children.OfType<Border>().Single(x =>
                            x.Tag is HardwareGraphicControl c && c.Control == name);
                if (!((TextBlock)Callout("reserved").Child).Text.Contains("MODIFIER", StringComparison.Ordinal) ||
                    !((TextBlock)Callout("reserved-shifted").Child).Text.Contains("UI SHIFT", StringComparison.Ordinal) ||
                    Callout("reserved").AllowDrop || Callout("reserved-shifted").AllowDrop ||
                    !Callout("free").AllowDrop ||
                    Callout("reserved").ToolTip?.ToString()?.Contains("S3 (Hold)", StringComparison.Ordinal) != true ||
                    Callout("reserved").ToolTip?.ToString()?.Contains("UI SHIFT (Toggle)", StringComparison.Ordinal) != true)
                    throw new InvalidOperationException("Modifier callouts were not labeled or protected.");

                Callout("reserved").RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) {
                    RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent
                });
                if (ActionBar.FindButton(view, "Assign selected command")?.IsEnabled != false)
                    throw new InvalidOperationException("Reserved callout still allowed Assign.");
                var clear = ActionBar.FindButton(view, "Clear assignment")!;
                clear.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, clear));
                if (!((TextBlock)Callout("reserved").Child).Text.Contains("MODIFIER", StringComparison.Ordinal))
                    throw new InvalidOperationException("Clearing a legacy assignment removed a modifier.");

                var context = new WindowContext(window);
                context.RunStructureEdit(w => WorkspaceStructureEditor.RemoveModifier(w,
                    WorkspaceStructureEditor.Describe(w).Modifiers.Single(x => x.Layer == AssignmentLayer.Module)));
                if (Callout("reserved").AllowDrop)
                    throw new InvalidOperationException("The UI Layer modifier was not respected.");
                context.RunStructureEdit(w => WorkspaceStructureEditor.RemoveModifier(w,
                    WorkspaceStructureEditor.Describe(w).Modifiers.Single(x => x.Layer == AssignmentLayer.UiLayer)));
                if (!Callout("reserved").AllowDrop || !Callout("reserved-shifted").AllowDrop)
                    throw new InvalidOperationException("Removing modifiers did not release reserved inputs.");
                context.Discard();
                if (Callout("reserved").AllowDrop ||
                    !((TextBlock)Callout("reserved").Child).Text.Contains("MODIFIER", StringComparison.Ordinal))
                    throw new InvalidOperationException("Discard failed to restore the reservation.");
            }
            finally { window.Close(); }
        }
        #endregion END Public
    }
}

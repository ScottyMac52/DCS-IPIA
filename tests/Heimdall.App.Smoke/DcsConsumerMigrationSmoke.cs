using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Heimdall.App.Games;
using Heimdall.Application;
using Heimdall.Domain;
using Heimdall.TestSupport;

namespace Heimdall.App.Smoke
{
    /// <summary>Protects native DCS inspection and per-controller state while using shared consumer components.</summary>
    internal static class DcsConsumerMigrationSmoke
    {
        #region BEGIN Public
        /// <summary>Switches between identical physical input keys without retaining the old controller's inspector details.</summary>
        internal static void Verify()
        {
            var previous = EditionAccess.Policy;
            using var issuer = new OwnerLicenseTestIssuer();
            EditionAccess.SetForTests(issuer.PremiumPolicy);
            ModuleWorkspaceWindow? window = null;
            try
            {
                var module = new DcsModule(Guid.NewGuid(), "CONSUMERS", "Consumer parity");
                var left = new DeviceProfile(Guid.NewGuid(), "test", "left", "Left", "Left.diff.lua");
                var right = new DeviceProfile(Guid.NewGuid(), "test", "right", "Right", "Right.diff.lua");
                var workspace = new EditorWorkspaceSnapshot(new(module, [left, right], [], [], "Smoke"), [], [],
                    [Diagram(left), Diagram(right)],
                    [new("left", "Left action", "Buttons", false), new("right", "Right action", "Buttons", false)], [],
                    [Assignment(module.Id, left.Id, "left", "Left action"), Assignment(module.Id, right.Id, "right", "Right action")],
                    new Dictionary<string, string>(), new Dictionary<string, string>
                    {
                        ["config/kneeboard.json"] = """{"profiles":{},"pages":[],"summaryPages":[]}""",
                        ["assets/shared/ui-layer/input/UiLayer/modifiers.lua"] = "local modifiers = {} return modifiers"
                    });
                var frozen = JsonSerializer.Serialize(workspace);
                window = new(new ModuleWorkspaceViewModel(workspace), persistLayout: false);
                window.Show(); ThemeControlSmoke.Pump(window);
                if (window.FindName("WorkspaceRoot") is not GameWorkspaceShell { SeparateFeatureActions: true })
                    throw new InvalidOperationException("DCS is not using the shared workspace shell.");
                var view = WorkspaceUi.Navigate(window, "Assignments");
                if (view.FindName("SelectionEditor") is not GameAssignmentEditorView)
                    throw new InvalidOperationException("DCS is not using the composed shared editor.");
                var rail = (ListBox)window.FindName("ControllerPicker"); rail.SelectedValue = left.Id;
                SelectCallout(view); ThemeControlSmoke.Pump(window);
                if (!((TextBlock)view.FindName("InspectorDetails")).Text.Contains("Saved: Left action", StringComparison.Ordinal))
                    throw new InvalidOperationException("Left controller inspection lost its native saved binding.");
                rail.SelectedValue = right.Id; ThemeControlSmoke.Pump(window);
                if (((TextBlock)view.FindName("InspectorDetails")).Text.Length != 0)
                    throw new InvalidOperationException("Controller switching retained stale native inspector details.");
                SelectCallout(view); ThemeControlSmoke.Pump(window);
                if (!((TextBlock)view.FindName("InspectorDetails")).Text.Contains("Saved: Right action", StringComparison.Ordinal) ||
                    ((TextBox)view.FindName("LabelEditor")).Text != "Right action")
                    throw new InvalidOperationException("Right controller inspection did not reload its native binding and label.");
                if (JsonSerializer.Serialize(new WindowContext(window).Working) != frozen)
                    throw new InvalidOperationException("Shared consumer selection changed the native DCS workspace.");
                window.Close(); window = null;
            }
            finally
            {
                if (window is not null) { new WindowContext(window).Discard(); window.Close(); }
                EditionAccess.SetForTests(previous);
            }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static UiLayerDiagram Diagram(DeviceProfile profile) => new(profile.Role, null, "test.svg", 120, 100, [], [])
        {
            ProfileId = profile.Id,
            Svg = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 120 100'><rect width='120' height='100'/></svg>",
            Controls = [new("button", "Button", "JOY_BTN1", 10, 10, 40, 30)]
        };
        private static DraftAssignment Assignment(Guid module, Guid profile, string command, string name) =>
            new(new(module, profile, AssignmentLayer.Module, BindingSection.Key, "JOY_BTN1"), new(command, name, false, AssignmentLayer.Module));
        private static void SelectCallout(UserControl view)
        {
            var surface = ((Canvas)view.FindName("Diagram")).Children.OfType<Canvas>().Single();
            var border = surface.Children.OfType<Border>().Single();
            border.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });
        }
        #endregion END private helpers
    }
}

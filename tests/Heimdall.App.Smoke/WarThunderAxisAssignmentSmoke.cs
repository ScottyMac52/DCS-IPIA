using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Heimdall.App.Games;
using Heimdall.Application;
using Heimdall.Domain.MultiSim;
using Heimdall.Infrastructure;
using Heimdall.WarThunder;

namespace Heimdall.App.Smoke
{
    /// <summary>Exercises actual axis callouts and public Apply against a ten-axis capture with one nonstandard raw object.</summary>
    internal static class WarThunderAxisAssignmentSmoke
    {
        #region BEGIN Public
        internal static void Verify(string root, string original)
        {
            var device = new DetectedDcsDevice(Guid.NewGuid(), "Axis controller", true, ProductGuid: Guid.Parse("00020001-0000-0000-0000-504944564944"));
            var discovery = new GameSmokeDiscovery(device);
            string[] guids = ["a36d02e1", "a36d02e0", "a36d02e2", "a36d02f4", "a36d02f5", "a36d02e3", "a36d02e4", "a36d02e4"];
            var objects = guids.Select((guid, index) => new ControllerObject(Guid.Parse(guid + "-c9f3-11cf-bfc7-444553540000"), (uint)(2 + index * 256), guid)).ToList();
            objects.Insert(1, new(Guid.NewGuid(), 2306u, "Nonstandard axis")); objects.Add(new(Guid.NewGuid(), 16u, "POV"));
            discovery.Inputs[device.PhysicalGuid] = ControllerCapabilityMapper.Map(objects);
            var services = new GameWorkspaceServices(new FileGameWorkspaceStore(Path.Combine(root, "axis-store")), discovery);
            var path = Path.Combine(root, "Axis.blk");
            File.WriteAllText(path, original.Replace("pitch{axisId:i=1", "pitch{axisId:i=0", StringComparison.Ordinal).Replace("axesCount:i=2", "axesCount:i=10", StringComparison.Ordinal).Replace("axesOffset:i=2", "axesOffset:i=10", StringComparison.Ordinal));
            var diagram = new UiLayerDiagram("Axes", null, "", 600, 400, [], [])
            {
                Svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"600\" height=\"400\"><rect width=\"600\" height=\"400\"/></svg>",
                Controls = [new("ry", "RY", "JOY_RY", 20, 20, 100, 30, true), new("x", "X", "JOY_X", 140, 20, 100, 30, true)]
            };
            WorkspaceHardware[] drawings = [new("axis-drawing", "Axes", "axis.diff.lua", diagram)];
            using var window = new WarThunderWorkspaceWindow(services, pendingChoice: () => MessageBoxResult.No, reviewChange: _ => true, drawings: drawings);
            WorkspaceLayout.SetPersistenceEnabled(window, false); window.Show(); ThemeControlSmoke.Pump(window); window.ImportFile(path);
            ((ListBox)window.FindName("Controllers")).SelectedIndex = 1;
            window.DeviceView.Physical.SelectedItem = device; Click(window.DeviceView.Link); window.DeviceView.Drawing.SelectedItem = drawings[0];
            ((ListBox)window.FindName("FeaturePicker")).SelectedIndex = 1; ThemeControlSmoke.Pump(window);
            if (!window.SaveCurrent()) throw new InvalidOperationException("Axis baseline did not save.");
            var baseline = window.Session!.Export().Files.Single().Value;
            var view = window.AssignmentView;
            var pitch = window.Session.Catalog.Commands.Single(command => command.Metadata["war-thunder.command"] == "pitch");
            window.DropPhysicalCommand(diagram.Controls[0], pitch.Id);
            RequireAxis(window, 4);
            view.Browser.CommandsList.SelectedItem = view.Browser.CommandsList.Items.Cast<SimCommand>().Single(command => command.Id == pitch.Id);
            var control = view.DrawingCanvas.Children.OfType<Border>().Single(border => border.Tag is HardwareGraphicControl graphic && graphic.InputKey == "JOY_X");
            control.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent });
            if (!view.Editor.Apply.IsEnabled || view.Editor.Index.Text != "1") throw new InvalidOperationException("Axis callout did not select the verified original native ordinal.");
            var apply = ((FeatureActionBar)window.FindName("FeatureActions")).Actions.Single(action => action.Id == "Apply");
            if (!apply.Command.CanExecute(null)) throw new InvalidOperationException("Public Apply was disabled for the mapped axis.");
            apply.Command.Execute(null); RequireAxis(window, 1);
            Click((Button)window.FindName("Discard"));
            if (window.Session.Export().Files.Single().Value != baseline || window.Session.HasChanges) throw new InvalidOperationException("Axis discard did not restore exact saved text.");
            window.Close();
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        private static void RequireAxis(WarThunderWorkspaceWindow window, int index)
        {
            var text = window.Session!.Export().Files.Single().Value;
            if (!text.Contains("pitch{axisId:i=" + index + ";inverse:b=no;kMul:r=1.25;mouseAxisId:i=-1;futureParam:r=2;}", StringComparison.Ordinal)
                || !text.Contains("keyboardKey:i=30;keyboardKey:i=31", StringComparison.Ordinal)
                || !window.Session.HasChanges || !((Button)window.FindName("Save")).IsEnabled)
                throw new InvalidOperationException("Physical axis assignment did not update the draft while retaining native tuning and system alternatives.");
        }
        #endregion END private helpers
    }
}

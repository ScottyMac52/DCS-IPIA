using Heimdall.App.Games.WarThunder;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Heimdall.App.Games;
using Heimdall.Application;
using Heimdall.Domain.MultiSim;
using Heimdall.Infrastructure;

namespace Heimdall.App.Smoke
{
    /// <summary>Exercises a seven-axis offset-fallback layout through public Apply, saved-state restoration and preserved export.</summary>
    internal static class WarThunderNativeLayoutSmoke
    {
        #region BEGIN Public
        internal static void Verify(string root, string original)
        {
            var device = new DetectedDcsDevice(Guid.NewGuid(), "Fallback fixture", true, ProductGuid: Guid.Parse("00020001-0000-0000-0000-504944564944"));
            var discovery = new GameSmokeDiscovery(device);
            var objects = Enumerable.Range(0, 64).Select(index => new ControllerObject(Guid.Parse("a36d02f0-c9f3-11cf-bfc7-444553540000"),
                (uint)(4 + index * 256), "Button") { Details = new((uint)(48 + index), 0) }).ToList();
            string[] guids = ["a36d02e2", "a36d02e1", "a36d02e0", "a36d02f5", "a36d02f4", "a36d02e3", "a36d02f3"];
            uint[] offsets = [0, 4, 8, 12, 16, 24, 28];
            objects.AddRange(guids.Select((guid, index) => new ControllerObject(Guid.Parse(guid + "-c9f3-11cf-bfc7-444553540000"),
                (uint)(2 + index * 256), "Axis") { Details = new(offsets[index], 256) }));
            discovery.Inputs[device.PhysicalGuid] = ControllerCapabilityMapper.Map(objects);
            var services = new GameWorkspaceServices(new FileGameWorkspaceStore(Path.Combine(root, "layout-store")), discovery);
            var source = original.Replace("axesCount:i=2", "axesCount:i=7", StringComparison.Ordinal)
                .Replace("axesOffset:i=2", "axesOffset:i=7", StringComparison.Ordinal)
                .Replace("buttonsCount:i=4", "buttonsCount:i=64", StringComparison.Ordinal)
                .Replace("buttonsOffset:i=4", "buttonsOffset:i=64", StringComparison.Ordinal);
            var folder = Path.Combine(root, "layout-source"); Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "Fallback.blk"); File.WriteAllText(path, source);
            var diagram = new UiLayerDiagram("Fallback", null, "", 600, 400, [], [])
            {
                Svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"600\" height=\"400\"><rect width=\"600\" height=\"400\"/></svg>",
                Controls = [new("main", "Main axis", "JOY_Z", 20, 20, 120, 30, true)]
            };
            WorkspaceHardware[] drawings = [new("fallback-drawing", "Fallback", "fallback.diff.lua", diagram)];
            using var window = new WarThunderWorkspaceWindow(services, pendingChoice: () => MessageBoxResult.No, reviewChange: _ => true, drawings: drawings);
            WorkspaceLayout.SetPersistenceEnabled(window, false); window.Show(); ThemeControlSmoke.Pump(window); window.ImportFile(path);
            ((ListBox)window.FindName("Controllers")).SelectedIndex = 1;
            window.DeviceView.Physical.SelectedItem = device; Click(window.DeviceView.Link); window.DeviceView.Drawing.SelectedItem = drawings[0];
            if (!window.DeviceView.LayoutTrace.Text.Contains("raw-offset fallback (28)", StringComparison.Ordinal)
                || !window.DeviceView.LayoutTrace.Text.Contains("Native slot 6", StringComparison.Ordinal))
                throw new InvalidOperationException("Native layout trace did not explain the seventh axis.");
            ((ListBox)window.FindName("FeaturePicker")).SelectedIndex = 1; ThemeControlSmoke.Pump(window);
            if (!window.SaveCurrent()) throw new InvalidOperationException("Fallback layout baseline did not save.");
            var view = window.AssignmentView;
            view.Browser.CommandsList.SelectedItem = view.Browser.CommandsList.Items.Cast<SimCommand>().Single(command => command.Metadata["war-thunder.command"] == "throttle");
            var control = view.DrawingCanvas.Children.OfType<Border>().Single(border => border.Tag is HardwareGraphicControl graphic && graphic.InputKey == "JOY_Z");
            control.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent });
            var apply = ((FeatureActionBar)window.FindName("FeatureActions")).Actions.Single(action => action.Id == "Apply");
            if (!apply.Command.CanExecute(null) || !Equals(view.Editor.Index.SelectedValue, 0)) throw new InvalidOperationException("Fallback layout did not enable the ordinary main axis at native slot zero.");
            apply.Command.Execute(null);
            var saved = window.Session!.Export().Files.Single().Value;
            if (!saved.Contains("throttle{axisId:i=0;nonlinearity:i=1;}", StringComparison.Ordinal)
                || !window.Session.HasChanges || !((Button)window.FindName("Save")).IsEnabled || !window.SaveCurrent())
                throw new InvalidOperationException("Fallback layout assignment did not stage and save while preserving native tuning.");
            var id = window.Session.Snapshot.Id; window.LoadWorkspace(id);
            if (window.Session.Export().Files.Single().Value != saved) throw new InvalidOperationException("Saved fallback layout assignment did not reopen exactly.");
            var clear = ((FeatureActionBar)window.FindName("FeatureActions")).Actions.Single(action => action.Id == "Clear");
            if (!clear.Command.CanExecute(null)) throw new InvalidOperationException("Saved fallback axis did not expose Clear.");
            clear.Command.Execute(null); Click((Button)window.FindName("Discard"));
            if (window.Session.Export().Files.Single().Value != saved || window.Session.HasChanges) throw new InvalidOperationException("Fallback axis Discard did not restore the saved text.");
            var export = window.ExportReview(root);
            if (File.ReadAllText(Path.Combine(export, "Fallback.blk")) != saved || File.ReadAllText(path) != source)
                throw new InvalidOperationException("Fallback review export changed native text or the imported source file.");
            window.Close();
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        #endregion END private helpers
    }
}

using System.Windows;
using System.Windows.Controls;
using Dcs.Ipia.Application;
using DcsIpia.Presentation.Games;

namespace Dcs.Ipia.App.Smoke
{
    /// <summary>Checks real fallback visibility, exact-device activity and native handle cleanup.</summary>
    public static class EliteInputActivitySmoke
    {
        #region BEGIN Public
        /// <summary>Exercises illustrated, explicitly hidden and stopped activity on the actual ED window.</summary>
        public static void Verify(EliteWorkspaceWindow window, DetectedDcsDevice device, Func<FakeDeviceInputSource?> source)
        {
            var feature = Box<ListBox>(window, "FeaturePicker");
            var text = Box<TextBox>(window, "InputActivity");
            feature.SelectedIndex = 2;
            Click(window, "StartInputButton"); window.SampleInput();
            if (text.IsVisible) throw new InvalidOperationException("Activity text covered an available drawing.");
            Click(window, "StopInputButton");
            if (source() is not { Disposed: true }) throw new InvalidOperationException("Illustrated testing retained input handles.");
            var pin = window.Session!.Snapshot.Hardware.Single(x => x.Value.Device.PhysicalGuid == device.PhysicalGuid);
            window.Session.ChangeDrawing(pin.Key, null);
            if (!text.IsVisible || !text.IsReadOnly || Box<ScrollViewer>(window, "DiagramScroll").IsVisible)
                throw new InvalidOperationException("No drawing did not switch input testing to a read-only text area.");
            Click(window, "StartInputButton");
            var input = source()!;
            input.ButtonValue = 0; window.SampleInput();
            input.ButtonValue = 1; window.SampleInput();
            input.ButtonValue = 0; window.SampleInput();
            if (!text.Text.Contains(device.Name, StringComparison.Ordinal) || !text.Text.Contains(device.PhysicalGuid.ToString(), StringComparison.Ordinal)
                || !text.Text.Contains("JOY_BTN1 · Pressed", StringComparison.Ordinal) || !text.Text.Contains("JOY_BTN1 · Released", StringComparison.Ordinal)
                || text.Text.Contains("JOY_X", StringComparison.Ordinal))
                throw new InvalidOperationException("Fallback lost device transitions or displayed the other controller's noisy axes.");
            ThemeControlSmoke.SaveImage(window, "elite-input-text");
            feature.SelectedIndex = 1;
            if (text.IsVisible || text.Text.Length != 0 || !input.Disposed) throw new InvalidOperationException("Leaving Input Testing retained activity or native handles.");
            feature.SelectedIndex = 2; Click(window, "StartInputButton"); window.SampleInput(); input = source()!;
            window.Hide(); ThemeControlSmoke.Pump(window);
            if (!input.Disposed || text.Text.Length != 0) throw new InvalidOperationException("Focus loss retained live input activity.");
            window.Show(); window.Activate(); ThemeControlSmoke.Pump(window);
            Click(window, "StartInputButton"); window.SampleInput(); input = source()!;
            Click(window, "StopInputButton");
            if (!input.Disposed || text.Text.Length != 0) throw new InvalidOperationException("Stop retained raw activity or input handles.");
            window.Session.ChangeDrawing(pin.Key, pin.Value.HardwareId);
            if (text.IsVisible) throw new InvalidOperationException("Restoring the drawing left the fallback visible.");
        }
        #endregion END Public
        #region BEGIN private helpers
        private static T Box<T>(FrameworkElement window, string name) where T : FrameworkElement => (T)window.FindName(name);
        private static void Click(FrameworkElement window, string name) => Box<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        #endregion END private helpers
    }
}

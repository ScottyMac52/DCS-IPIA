using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Heimdall.Application;
using Heimdall.Domain;
using Heimdall.App;

namespace Heimdall.App.Smoke
{
    /// <summary>Checks that translucent assignment callouts contain only the current caption.</summary>
    public static class AssignmentsLabelSmoke
    {
        #region BEGIN Public
        /// <summary>Detects placeholder ghost text beneath assigned callouts at multiple control opacities.</summary>
        public static void Verify()
        {
            var module = new DcsModule(Guid.NewGuid(), "LABELS", "Assignment label regression");
            var profile = new DeviceProfile(Guid.NewGuid(), "tm-tpr", null, "Rudder", "Rudder.diff.lua");
            var slot = new DraftAssignmentSlot(module.Id, profile.Id, AssignmentLayer.Module, BindingSection.Axis, "JOY_X");
            var graphic = new UiLayerDiagram("Rudder", null, "rudder.svg", 240, 90, [], [])
            {
                ProfileId = profile.Id,
                Svg = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 240 90'>" +
                    "<rect width='240' height='90' fill='black'/><!-- box:axis -->" +
                    "<rect x='20' y='20' width='200' height='50' fill='black'/>" +
                    "<text id='lbl-axis' x='120' y='48' text-anchor='middle' fill='white'>Original label</text></svg>",
                Controls = [new("axis", "Axis", "JOY_X", 20, 20, 200, 50, true)]
            };
            var workspace = new EditorWorkspaceSnapshot(new(module, [profile], [], [], "smoke"), [], [], [graphic],
                [new("X", "X", "Axis", true)], [], [new(slot, new("X", "X", true, AssignmentLayer.Module))],
                new Dictionary<string, string>(), new Dictionary<string, string> { ["config/kneeboard.json"] = "{}" });
            var window = new ModuleWorkspaceWindow(new ModuleWorkspaceViewModel(workspace),
                deviceDiscovery: new EmptyDeviceDiscovery(), persistLayout: false);
            try
            {
                window.Show();
                var view = WorkspaceUi.Navigate(window, "Assignments");
                view.Resources["ControlForegroundBrush"] = Brushes.Gold;
                view.Resources["AccentTextBrush"] = Brushes.Gold;
                ThemeControlSmoke.Pump(window);
                var diagram = (Canvas)view.FindName("Diagram")!;
                var surface = diagram.Children.OfType<Canvas>().Single();
                var captions = surface.Children.OfType<Border>().ToArray();
                if (captions.Length != 1 || captions[0].Tag is not HardwareGraphicControl ||
                    captions[0].Child is not TextBlock { Text: "X" })
                    throw new InvalidOperationException("Assignments rendered an extra placeholder caption or lost its current assignment.");

                var blue = (Color)ColorConverter.ConvertFromString(KneeboardStateColors.Base);
                if (captions[0] is not ContrastOutlineBorder || (captions[0].BorderBrush as SolidColorBrush)?.Color != blue ||
                    (((TextBlock)captions[0].Child).Foreground as SolidColorBrush)?.Color != blue)
                    throw new InvalidOperationException("Assignments Base retained theme gold instead of shared blue state ink.");

                foreach (var opacity in new[] { 1.0, .35, 0.0 })
                {
                    view.Resources["ControlBackgroundBrush"] = new SolidColorBrush(Color.FromArgb((byte)(255 * opacity), 0, 0, 0));
                    ThemeControlSmoke.Pump(window);
                    var bitmap = new RenderTargetBitmap(240, 90, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(surface);
                    var pixels = new byte[240 * 90 * 4];
                    bitmap.CopyPixels(pixels, 240 * 4, 0);
                    // This area is inside the callout, left of the narrow current caption "X".
                    // The old "Unassigned" mask drew visible letters here through translucent backgrounds.
                    for (var y = 25; y < 65; y++)
                        for (var x = 75; x < 108; x++)
                        {
                            var offset = (y * 240 + x) * 4;
                            if (pixels[offset] > 8 || pixels[offset + 1] > 8 || pixels[offset + 2] > 8)
                                throw new InvalidOperationException($"Ghost callout text is visible at control opacity {opacity}.");
                        }
                }
                ThemeControlSmoke.SaveImage(window, "assignments-single-caption-transparent");
            }
            finally { window.Close(); }
        }
        #endregion END Public
    }
}

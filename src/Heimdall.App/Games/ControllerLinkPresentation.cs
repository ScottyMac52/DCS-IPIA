using System.Windows.Controls;
using Heimdall.Application;

namespace Heimdall.App.Games
{
    /// <summary>Keeps saved physical identity visible, including disconnected units, without choosing a replacement.</summary>
    internal static class ControllerLinkPresentation
    {
        internal static void ShowPhysical(ComboBox picker, DetectedDcsDevice? saved,
            IEnumerable<DetectedDcsDevice> observed)
        {
            var previous = picker.SelectedItem as DetectedDcsDevice;
            var choices = observed.ToList();
            if (saved is not null && !choices.Any(x => x.PhysicalGuid == saved.PhysicalGuid && x.ProductGuid == saved.ProductGuid))
                choices.Add(saved with { Connected = false });
            picker.ItemsSource = choices;
            var target = saved ?? previous;
            picker.SelectedItem = target is null ? null : choices.FirstOrDefault(x =>
                x.PhysicalGuid == target.PhysicalGuid && x.ProductGuid == target.ProductGuid);
        }
    }
}

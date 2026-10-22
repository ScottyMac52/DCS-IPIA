using Heimdall.Application;
using Heimdall.Domain.MultiSim;

namespace Heimdall.EliteDangerous
{
    /// <summary>Projects only the chosen native device's button/key choices without opening input handles or changing bindings.</summary>
    public static class EliteDeviceInputs
    {
        #region BEGIN Public
        /// <summary>Combines verified drawing controls with captured native keys for the requested input kind.</summary>
        public static IReadOnlyList<EliteInputChoice> ForDevice(ProfileDocument profile, string deviceId, UiLayerDiagram? diagram = null, bool axes = false)
        {
            var device = profile.Devices.Single(x => x.LogicalId == deviceId);
            diagram ??= EliteSystemInputDrawings.Create(profile, deviceId);
            var choices = new Dictionary<string, EliteInputChoice>(StringComparer.Ordinal);
            if (diagram is not null)
                foreach (var control in diagram.Controls.Where(x => x.IsAxis == axes))
                    if (EliteInputKeys.FromDiagram(diagram, control) is { } key)
                        choices.TryAdd(key, new(key, string.IsNullOrWhiteSpace(control.Label) ? key : control.Label));
            var captured = axes ? device.Axes.Select(x => x.SourceAxis) : device.Buttons.Select(x => x.SourceButton)
                .Concat(profile.Devices.SelectMany(x => x.Buttons).SelectMany(x => x.Modifiers)
                    .Where(x => x.Source == deviceId).Select(x => x.Value));
            foreach (var key in captured.OfType<string>().Where(x => !string.IsNullOrWhiteSpace(x))) choices.TryAdd(key, new(key, key));
            return choices.Values.OrderBy(x => x.ButtonNumber is null ? 1 : 0).ThenBy(x => x.ButtonNumber)
                .ThenBy(x => x.Key, StringComparer.Ordinal).ToArray();
        }
        #endregion END Public
    }
}

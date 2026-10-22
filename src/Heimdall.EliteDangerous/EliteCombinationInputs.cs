using Heimdall.Application;
using Heimdall.Domain.MultiSim;

namespace Heimdall.EliteDangerous
{
    /// <summary>Projects only the chosen native device's button/key choices without opening input handles or changing bindings.</summary>
    public static class EliteCombinationInputs
    {
        #region BEGIN Public
        /// <summary>Combines verified drawing buttons with captured inputs and cross-device combination keys; axes are excluded.</summary>
        public static IReadOnlyList<EliteCombinationInput> ForDevice(ProfileDocument profile, string deviceId, UiLayerDiagram? diagram = null)
        {
            var device = profile.Devices.Single(x => x.LogicalId == deviceId);
            diagram ??= EliteSystemInputDrawings.Create(profile, deviceId);
            var choices = new Dictionary<string, EliteCombinationInput>(StringComparer.Ordinal);
            if (diagram is not null)
                foreach (var control in diagram.Controls.Where(x => !x.IsAxis))
                    if (EliteInputKeys.FromDiagram(diagram, control) is { } key)
                        choices.TryAdd(key, new(key, string.IsNullOrWhiteSpace(control.Label) ? key : control.Label));
            var captured = device.Buttons.Select(x => x.SourceButton).Concat(profile.Devices.SelectMany(x => x.Buttons)
                .SelectMany(x => x.Modifiers).Where(x => x.Source == deviceId).Select(x => x.Value));
            foreach (var key in captured.Where(x => !string.IsNullOrWhiteSpace(x))) choices.TryAdd(key, new(key, key));
            return choices.Values.OrderBy(x => x.ButtonNumber is null ? 1 : 0).ThenBy(x => x.ButtonNumber)
                .ThenBy(x => x.Key, StringComparer.Ordinal).ToArray();
        }
        #endregion END Public
    }
}

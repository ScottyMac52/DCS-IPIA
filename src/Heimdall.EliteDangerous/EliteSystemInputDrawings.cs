using Heimdall.Application;
using Heimdall.Domain.MultiSim;

namespace Heimdall.EliteDangerous
{
    /// <summary>Projects Elite system inputs onto shared simulator-independent artwork.</summary>
    public static class EliteSystemInputDrawings
    {
        #region BEGIN Public
        /// <summary>Returns keyboard/mouse artwork with exact additional imported inputs.</summary>
        public static UiLayerDiagram? Create(ProfileDocument profile, string deviceId)
        {
            var device = profile.Devices.Single(x => x.LogicalId == deviceId);
            var token = device.Identity!.SimSpecificIds["ed.device"];
            if (token is not ("Keyboard" or "Mouse")) return null;
            var inputs = device.Buttons.Select(x => x.SourceButton).Concat(device.Axes.Select(x => x.SourceAxis))
                .Concat(profile.Devices.SelectMany(x => x.Buttons).SelectMany(x => x.Modifiers)
                    .Where(x => x.Source == deviceId).Select(x => x.Value)).OfType<string>();
            return SystemInputDrawings.Create(token, inputs, device.Axes.Select(x => x.SourceAxis).OfType<string>().ToArray()) with
            { Path = "elite-system/" + token + ".svg" };
        }
        #endregion END Public
    }
}

namespace Dcs.Ipia.Application.Games
{
    /// <summary>Suggests artwork only for an exact unique catalog name; names never grant entitlement or resolve game indices.</summary>
    public static class GameControllerDrawings
    {
        #region BEGIN Public
        /// <summary>Returns one unambiguous shared illustration, leaving unknown products and alternate views for optional user choice.</summary>
        public static string? Suggest(IEnumerable<WorkspaceHardware> hardware, DetectedDcsDevice device)
        {
            var matches = hardware.Where(x => DcsPhysicalIdentity.FromFileName(x.DefaultFileName) is not null
                && DcsPhysicalIdentity.NameFromFileName(x.DefaultFileName).Equals(device.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
            return matches.Length == 1 ? matches[0].DeviceId : null;
        }
        #endregion END Public
    }
}

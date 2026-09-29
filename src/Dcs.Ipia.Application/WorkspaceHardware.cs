namespace Dcs.Ipia.Application
{
    /// <summary>Pinned shared hardware used to create new physical device instances without a checkout.</summary>
    /// <param name="DeviceId">Shared hardware ID or alias.</param>
    /// <param name="Label">Shared hardware display label.</param>
    /// <param name="DefaultFileName">Default native DCS filename.</param>
    /// <param name="Diagram">Validated hardware graphic and shared controls.</param>
    public sealed record WorkspaceHardware(string DeviceId, string Label, string DefaultFileName,
                                           UiLayerDiagram Diagram)
    {
        #region BEGIN Public
        /// <summary>Gets a distinguishable hardware choice including its alias.</summary>
        public string Title => Label + " · " + DeviceId;
        #endregion END Public
    }
}

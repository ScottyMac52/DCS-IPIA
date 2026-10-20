namespace Heimdall.Application
{
    /// <summary>An explicitly selected physical unit and optional copied baseline to rebind.</summary>
    /// <param name="Hardware">Chosen capability template.</param>
    /// <param name="FileName">Actual GUID-qualified native DCS filename.</param>
    /// <param name="Instance">Human instance label.</param>
    /// <param name="Role">Optional semantic role.</param>
    /// <param name="BaselineFileName">Copied baseline profile to retain and rebind, or null for a new unit.</param>
    public sealed record NewModuleDeviceSelection(WorkspaceHardware Hardware, string FileName, string? Instance,
                                                  string? Role, string? BaselineFileName = null)
    {
        #region BEGIN Public
        /// <summary>Gets the readable selection label.</summary>
        public string Title => DcsDeviceDisplay.Label(FileName, Instance ?? Role);
        #endregion END Public
    }
}

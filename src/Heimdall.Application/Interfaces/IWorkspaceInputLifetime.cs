namespace Heimdall.Application.Interfaces
{
    /// <summary>Releases foreground input acquisition before persistence or replacement.</summary>
    public interface IWorkspaceInputLifetime
    {
        #region BEGIN Public
        /// <summary>Synchronously releases acquisition and handlers.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Stop is the documented PR3 role contract name in workspace-role-contracts.md.")]
        void Stop();
        #endregion END Public
    }
}

using Heimdall.Application.Games;

namespace Heimdall.Application.Interfaces
{
    /// <summary>Retains typed Elite backend and row projections behind the shared workspace role.</summary>
    public interface IEliteWorkspaceContext<TBackend, TRow> : IGameWorkspaceContext<GameWorkspace>
    {
        #region BEGIN Public
        /// <summary>Gets the existing native profile session.</summary>
        GameProfileSession? Session { get; }
        /// <summary>Gets the native backend without erasing its type.</summary>
        TBackend Backend { get; }
        /// <summary>Gets typed native record projections.</summary>
        IReadOnlyList<TRow> Rows { get; }
        #endregion END Public
    }
}

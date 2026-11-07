
namespace Heimdall.Application.Interfaces
{
    /// <summary>Adds native typed snapshots to the common orchestration contract.</summary>
    public interface IGameWorkspaceContext<TSnapshot> : IGameWorkspaceContext where TSnapshot : class
    {
        #region BEGIN Public
        /// <summary>Gets the native working snapshot without erasing its type.</summary>
        TSnapshot? Working { get; }
        /// <summary>Gets the native saved baseline without forcing a shared persistence model.</summary>
        TSnapshot? Saved { get; }
        #endregion END Public
    }
}

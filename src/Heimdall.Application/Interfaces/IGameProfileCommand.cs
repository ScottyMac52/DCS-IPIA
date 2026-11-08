using Heimdall.Application.Games;

namespace Heimdall.Application.Interfaces
{
    /// <summary>Typed optional native profile work, such as import or reviewed output, with explicit availability.</summary>
    public interface IGameProfileCommand<in TRequest, TResult>
    {
        #region BEGIN Public
        /// <summary>Gets whether the command can currently run, with a reason when it cannot.</summary>
        WorkspaceAvailability Availability { get; }
        /// <summary>Runs inside one admitted operation and returns native data with its committed/recovery outcome.</summary>
        Task<ProfileCommandExecution<TResult>> ExecuteAsync(TRequest request, CancellationToken cancellationToken);
        #endregion END Public
    }
}

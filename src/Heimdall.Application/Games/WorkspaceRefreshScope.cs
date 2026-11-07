namespace Heimdall.Application.Games
{
    /// <summary>Releases a nested refresh scope once without introducing a second workspace lock.</summary>
    internal sealed class WorkspaceRefreshScope(Action release) : IDisposable
    {
        private Action? _release = release;
        #region BEGIN Public
        /// <inheritdoc/>
        public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
        #endregion END Public
    }
}

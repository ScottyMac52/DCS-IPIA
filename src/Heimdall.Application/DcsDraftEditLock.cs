namespace Heimdall.Application
{
    /// <summary>Releases one persistence-owned draft suspension exactly once.</summary>
    internal sealed class DcsDraftEditLock(Action release) : IDisposable
    {
        private Action? _release = release;
        #region BEGIN Public
        /// <summary>Releases only this lease, including when disposal is repeated.</summary>
        public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
        #endregion END Public
    }
}

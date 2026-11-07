namespace Heimdall.App.Smoke
{
    /// <summary>Counts live state subscriptions for the shared assignment-view lifetime checks.</summary>
    internal sealed class AssignmentChangeSource
    {
        private EventHandler? _changed;
        #region BEGIN Public
        internal int Subscribers => _changed?.GetInvocationList().Length ?? 0;
        internal void Subscribe(EventHandler handler) => _changed += handler;
        internal void Unsubscribe(EventHandler handler) => _changed -= handler;
        internal void Raise() => _changed?.Invoke(this, EventArgs.Empty);
        #endregion END Public
    }
}

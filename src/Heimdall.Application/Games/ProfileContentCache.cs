namespace Heimdall.Application.Games
{
    /// <summary>Memoizes an immutable projection of a native profile document per session, content stamp and key, so
    /// repeated availability/presentation reads (for example on every selection) do not clone the whole document.
    /// The projection receives an isolated snapshot and must return immutable data (records, strings, read-only arrays).</summary>
    public sealed class ProfileContentCache<TKey, TValue>(Func<GameProfileSession?> session, Func<GameWorkspace, TKey, TValue> project)
    {
        private readonly Func<GameProfileSession?> _session = session ?? throw new ArgumentNullException(nameof(session));
        private readonly Func<GameWorkspace, TKey, TValue> _project = project ?? throw new ArgumentNullException(nameof(project));
        private GameProfileSession? _owner;
        private long _stamp;
        private TKey _key = default!;
        private TValue _value = default!;
        private bool _filled;
        #region BEGIN Public
        /// <summary>Gets how many projections were computed (a deterministic work counter for regression tests).</summary>
        public int Projections { get; private set; }
        /// <summary>Gets the projection for the current document, or <paramref name="none"/> without an open session.</summary>
        public TValue Get(TKey key, TValue none)
        {
            var session = _session();
            if (session is null) { _filled = false; _owner = null; return none; }
            if (!_filled || !ReferenceEquals(_owner, session) || _stamp != session.ContentStamp || !EqualityComparer<TKey>.Default.Equals(_key, key))
            {
                _value = _project(session.Snapshot, key);
                _owner = session; _stamp = session.ContentStamp; _key = key; _filled = true; Projections++;
            }
            return _value;
        }
        #endregion END Public
    }
}

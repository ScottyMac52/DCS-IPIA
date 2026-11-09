using Heimdall.Application.Interfaces;

namespace Heimdall.Application.Games
{
    /// <summary>Owns cached projection, search/filter refresh, stable selection, notification coalescing and browser lifetime.</summary>
    public abstract class GameAssignmentBrowserBase<TCommand, TRecord, TFilter> : IGameAssignmentBrowser<TCommand, TRecord, TFilter>
    {
        private readonly IGameWorkspaceContext _context;
        private WorkspaceIdentity? _identity;
        private long _version = -1, _nativeVersion = -1;
        private IReadOnlyList<TCommand> _allCommands = [];
        private IReadOnlyList<TRecord> _allRecords = [];
        private string _search = "";
        private string? _pendingPreferredCommandId;
        private IReadOnlyList<TCommand> _filteringCommands = [];
        private bool _active = true;
        private bool _initialSelectionPending;
        private bool _refreshing, _pending, _started, _disposed, _selecting;
        private int _builds, _publications;
        private WorkspaceSelection? _observed;
        /// <summary>Binds the browser to the one authoritative context and initial native filter.</summary>
        protected GameAssignmentBrowserBase(IGameWorkspaceContext context, TFilter initialFilter)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context)); Filter = initialFilter;
            _context.StateChanged += ContextChanged;
        }
        #region BEGIN Public
        /// <inheritdoc/>
        public IReadOnlyList<TCommand> Commands { get; private set; } = [];
        /// <inheritdoc/>
        public IReadOnlyList<TRecord> Records { get; private set; } = [];
        /// <inheritdoc/>
        public TFilter Filter { get; private set; }
        /// <inheritdoc/>
        public BrowserWorkCounts Metrics => new(_builds, _publications);
        /// <summary>Gets the cached complete command projection, built only when content changes.</summary>
        public IReadOnlyList<TCommand> AllCommands => _allCommands;
        /// <summary>Gets the cached complete native record projection.</summary>
        public IReadOnlyList<TRecord> AllRecords => _allRecords;
        /// <inheritdoc/>
        public event EventHandler? ProjectionChanged;
        /// <inheritdoc/>
        public void Search(string text)
        {
            ArgumentNullException.ThrowIfNull(text); var search = text.Trim();
            if (_started && search == _search) return;
            _search = search; Refresh();
        }
        /// <inheritdoc/>
        public void Query(TFilter filter, string text, string? preferredCommandId = null)
        {
            ArgumentNullException.ThrowIfNull(filter); ArgumentNullException.ThrowIfNull(text);
            Filter = filter; _search = text.Trim(); Refresh(preferredCommandId);
        }
        /// <inheritdoc/>
        public void SetFilter(TFilter filter)
        {
            ArgumentNullException.ThrowIfNull(filter);
            if (_started && EqualityComparer<TFilter>.Default.Equals(Filter, filter)) return;
            Filter = filter; Refresh();
        }
        /// <inheritdoc/>
        public void Refresh(string? preferredCommandId = null)
        {
            if (_disposed) return;
            if (_context.State.Operation is not null || _refreshing)
            {
                _pending = true;
                if (preferredCommandId is not null) _pendingPreferredCommandId = preferredCommandId;
                return;
            }
            // Native observers must see the published rows for the new selection, not the previous editor.
            using var notifications = _context.DeferRefresh();
            _started = true; _refreshing = true;
            try
            {
                do
                {
                    _pending = false;
                    preferredCommandId ??= _pendingPreferredCommandId;
                    _pendingPreferredCommandId = null;
                    RefreshProjection(preferredCommandId); preferredCommandId = null;
                } while (_pending && !_disposed && _context.State.Operation is null);
            }
            catch (Exception error)
            {
                _version = -1;
                ApplySelection(_context.State.Selection with { CommandId = null, AlternativeId = null, InputId = null });
                _context.ReportStatus("Browser refresh failed: " + error.Message);
            }
            finally { _refreshing = false; }
        }
        /// <inheritdoc/>
        public void SetActive(bool active) { if (_disposed || _active == active) return; _active = active; if (active) { Filter = FilterForSelection(Filter, _context.State.Selection); Refresh(); } }
        /// <inheritdoc/>
        public void SelectCommand(string? commandId)
        {
            if (_disposed || !_context.State.Open || _context.State.Operation is not null) return;
            using var notifications = _context.DeferRefresh();
            var command = Commands.FirstOrDefault(x => CommandId(x) == commandId);
            ApplySelection(command is null ? ScopeSelection with { CommandId = null, AlternativeId = null, InputId = null }
                : CommandSelection(command));
            Refresh();
        }
        /// <inheritdoc/>
        public void SelectRecord(string? recordId)
        {
            if (_disposed || !_context.State.Open || _context.State.Operation is not null) return;
            using var notifications = _context.DeferRefresh();
            var record = Records.FirstOrDefault(x => RecordId(x) == recordId);
            if (record is null) ApplySelection(_context.State.Selection with { AlternativeId = null, InputId = null });
            else SelectNativeRecord(record);
            Refresh();
        }
        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true; _context.StateChanged -= ContextChanged; ProjectionChanged = null; DisposeNative(); GC.SuppressFinalize(this);
        }
        #endregion END Public
        /// <summary>Gets commands being projected while native record predicates run.</summary>
        protected IReadOnlyList<TCommand> VisibleCommands => _filteringCommands;
        /// <summary>Gets the shared selection and state authority.</summary>
        protected IGameWorkspaceContext Context => _context;
        /// <summary>Gets native initial-open command selection policy; stale identities never fall back.</summary>
        protected virtual bool SelectInitialCommand => false;
        /// <summary>Gets native initial occurrence selection policy when a command is newly selected.</summary>
        protected virtual bool SelectInitialRecord => false;
        /// <summary>Gets additional native evidence invalidation, such as a newly completed hardware scan.</summary>
        protected virtual long NativeVersion => 0;
        /// <summary>Projects authoritative external controller selection into native filter facts without rereading a document.</summary>
        protected abstract TFilter FilterForSelection(TFilter filter, WorkspaceSelection selection);
        /// <summary>Gets the controller and scope selected by the native filter.</summary>
        protected abstract WorkspaceSelection ScopeSelection { get; }
        /// <summary>Reads one complete native command projection on content invalidation.</summary>
        protected abstract IReadOnlyList<TCommand> ReadCommands();
        /// <summary>Reads native records from the same prepared projection.</summary>
        protected abstract IReadOnlyList<TRecord> ReadRecords();
        /// <summary>Gets the command's stable native identity.</summary>
        protected abstract string CommandId(TCommand command);
        /// <summary>Gets the record's stable native address.</summary>
        protected abstract string RecordId(TRecord record);
        /// <summary>Prepares native query facts once per refresh before cached rows are tested.</summary>
        protected virtual void PrepareQuery(TFilter filter, string search) { }
        /// <summary>Applies native metadata/ownership policy to one cached command.</summary>
        protected abstract bool IncludeCommand(TCommand command, TFilter filter, string search);
        /// <summary>Applies native record policy to one cached record.</summary>
        protected abstract bool IncludeRecord(TRecord record, TFilter filter, string search);
        /// <summary>Maps a native command to shared selection addresses.</summary>
        protected abstract WorkspaceSelection SelectionForCommand(TCommand command);
        /// <summary>Maps a native record to shared selection addresses.</summary>
        protected abstract WorkspaceSelection SelectionForRecord(TRecord record);
        /// <summary>Updates a presentation edge after a complete projection has been published.</summary>
        protected virtual void OnProjectionChanged() { }
        /// <summary>Releases optional presentation resources owned by a native adapter.</summary>
        protected virtual void DisposeNative() { }
        #region BEGIN private helpers
        private void ContextChanged(object? sender, EventArgs e)
        {
            if (!_active || !_started || _selecting || _disposed || _context.State.Operation is not null) return;
            var selection = _context.State.Selection;
            if (_observed?.ControllerId != selection.ControllerId) Filter = FilterForSelection(Filter, selection);
            var relevant = _observed is null || _observed.ControllerId != selection.ControllerId || _observed.CommandId != selection.CommandId
                || _observed.AlternativeId != selection.AlternativeId || _observed.NativeScope != selection.NativeScope;
            if (_pending || _identity != _context.State.Identity || _version != _context.ContentVersion || relevant) Refresh();
            else _observed = selection;
        }
        private WorkspaceSelection CommandSelection(TCommand command)
        {
            var selection = SelectionForCommand(command);
            return selection.CommandId == _context.State.Selection.CommandId ? selection
                : selection with { AlternativeId = null, InputId = null };
        }
        private void ApplySelection(WorkspaceSelection selection)
        {
            if (!_active || _context.State.Selection == selection || _context.State.Operation is not null) return;
            _selecting = true;
            try { _context.Select(selection); }
            finally { _selecting = false; }
        }
        private void SelectNativeRecord(TRecord record)
        {
            if (!_active) return;
            var target = SelectionForRecord(record);
            if (_context.State.Selection == target) return;
            using var refresh = _context.DeferRefresh();
            if (_context.State.Selection.ControllerId != target.ControllerId || _context.State.Selection.NativeScope != target.NativeScope)
                ApplySelection(target with { AlternativeId = null, InputId = null });
            ApplySelection(target with { InputId = null });
            // A new native record supplies fresh input evidence after the context clears the previous address.
            if (target.InputId is not null && _context.State.Selection.InputId != target.InputId)
                ApplySelection(_context.State.Selection with { InputId = target.InputId });
        }
        private void RefreshProjection(string? preferred)
        {
            var content = _identity != _context.State.Identity || _version != _context.ContentVersion || _nativeVersion != NativeVersion;
            var commands = content ? Array.AsReadOnly(ReadCommands().ToArray()) : _allCommands;
            var records = content ? Array.AsReadOnly(ReadRecords().ToArray()) : _allRecords;
            PrepareQuery(Filter, _search);
            var visible = commands.Where(x => IncludeCommand(x, Filter, _search)).ToArray();
            if (_identity != _context.State.Identity) _initialSelectionPending = true;
            var initial = _initialSelectionPending;
            SelectVisibleCommand(visible, preferred, initial);
            _filteringCommands = visible;
            var matching = records.Where(x => IncludeRecord(x, Filter, _search)).ToArray();
            ReconcileRecordSelection(matching, content || initial);
            PublishProjection(commands, records, visible, matching, content);
        }
        private void SelectVisibleCommand(TCommand[] visible, string? preferred, bool initial)
        {
            var wanted = preferred ?? _context.State.Selection.CommandId;
            var selected = visible.FirstOrDefault(x => CommandId(x) == wanted);
            if (selected is null && wanted is null && _active && initial && SelectInitialCommand) selected = visible.FirstOrDefault();
            var scope = _context.State.Open ? ScopeSelection : new WorkspaceSelection(null, null, null, null, null, "");
            ApplySelection(selected is null ? scope with { CommandId = null, AlternativeId = null, InputId = null } : CommandSelection(selected));
        }
        private void ReconcileRecordSelection(TRecord[] matching, bool initial)
        {
            var selection = _context.State.Selection;
            if (selection.CommandId is not null && selection.AlternativeId is null && SelectInitialRecord && matching.Length > 0
                && (initial || _observed?.CommandId != selection.CommandId)) SelectNativeRecord(matching[0]);
            if (_context.State.Selection.AlternativeId is { } recordId && !matching.Any(x => RecordId(x) == recordId))
                ApplySelection(_context.State.Selection with { AlternativeId = null, InputId = null });
        }
        private void PublishProjection(IReadOnlyList<TCommand> commands, IReadOnlyList<TRecord> records, TCommand[] visible, TRecord[] matching, bool content)
        {
            var changed = content || !Commands.SequenceEqual(visible) || !Records.SequenceEqual(matching) || _observed != _context.State.Selection;
            if (content)
            {
                _allCommands = commands; _allRecords = records; _identity = _context.State.Identity;
                _version = _context.ContentVersion; _nativeVersion = NativeVersion; _builds++;
            }
            _observed = _context.State.Selection;
            if (_active) _initialSelectionPending = false;
            if (!changed) return;
            if (content || !Commands.SequenceEqual(visible)) Commands = Array.AsReadOnly(visible);
            if (content || !Records.SequenceEqual(matching)) Records = Array.AsReadOnly(matching);
            _publications++;
            OnProjectionChanged(); ProjectionChanged?.Invoke(this, EventArgs.Empty);
        }
        #endregion END private helpers
    }
}


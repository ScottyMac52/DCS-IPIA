using Heimdall.Domain.MultiSim;

namespace Heimdall.Application.Games
{
    /// <summary>One UI-independent source for controller, command, slot and pending input selection.
    /// Selection transitions never edit a profile or persist a workspace.</summary>
    public sealed class GameAssignmentSelection(string defaultSlot = "Primary")
    {
        private readonly string _defaultSlot = defaultSlot;
        private int _changing;
        #region BEGIN Public
        /// <summary>Reports one actual selection transition without editing native data.</summary>
        public event EventHandler? Changed;
        /// <summary>Gets the workspace and section scope of this selection.</summary>
        public string Scope { get; private set; } = "";
        /// <summary>Gets the selected native alternative; changing it clears pending input.</summary>
        public string? AlternativeId { get; private set; }
        /// <summary>Gets why a pointed physical input cannot be translated by its adapter.</summary>
        public string InputBlockedReason { get; private set; } = "";
        /// <summary>Gets the displayed controller identity, independent of any physical hardware link.</summary>
        public string? ControllerId { get; private set; }
        /// <summary>Gets the selected visible native command identity.</summary>
        public string? CommandId { get; private set; }
        /// <summary>Gets the adapter-selected native binding slot.</summary>
        public string Slot { get; private set; } = defaultSlot;
        /// <summary>Gets the pending native input key, cleared when its selection context changes.</summary>
        public string? InputKey { get; private set; }

        /// <summary>Preserves a visible command by identity, otherwise selects the first visible command.
        /// Controller changes invalidate pending input even when the command remains selected.</summary>
        public void Refresh(string? controllerId, IReadOnlyList<SimCommand> commands, string? preferredCommandId = null, bool selectFirst = true)
        {
            Change(() =>
            {
            SelectController(controllerId);
            var wanted = preferredCommandId ?? CommandId;
            SelectCommand(commands.FirstOrDefault(x => x.Id == wanted)?.Id ?? (selectFirst && commands.Count > 0 ? commands[0].Id : null));
                    });
        }
        /// <summary>Preserves native command IDs without requiring a multi-sim catalog row.</summary>
        public void RefreshIds(string? controllerId, IReadOnlyList<string> commandIds, string? preferredCommandId = null, bool selectFirst = true)
        {
            Change(() =>
            {
                SelectController(controllerId);
                var wanted = preferredCommandId ?? CommandId;
                SelectCommand(commandIds.Contains(wanted, StringComparer.Ordinal) ? wanted : selectFirst && commandIds.Count > 0 ? commandIds[0] : null);
            });
        }
        /// <summary>Selects a command; a different command cannot inherit stale pending input.</summary>
        public void SelectCommand(string? commandId)
        {
            Change(() =>
            {
            if (CommandId != commandId) { ClearInput(); AlternativeId = null; }
            CommandId = commandId;
                    });
        }
        /// <summary>Switches native binding slot and invalidates input from the previous slot.</summary>
        public void SelectSlot(string slot)
        {
            Change(() =>
            {
            if (Slot != slot) { ClearInput(); AlternativeId = null; }
            Slot = slot;
                    });
        }
        /// <summary>Tracks a native input supplied by the adapter; no input survives an empty command selection.</summary>
        public void SelectInput(string? inputKey)
        {
            Change(() =>
            {
            InputKey = CommandId is null || string.IsNullOrWhiteSpace(inputKey) ? null : inputKey;
            InputBlockedReason = "";
                    });
        }
        /// <summary>Replaces the workspace or section without carrying a command, record or input from the previous scope.</summary>
        public void SelectScope(string scope)
        {
            Change(() =>
            {
            if (Scope == scope) return;
            Reset(); Scope = scope;
                    });
        }
        /// <summary>Changes controller while retaining the selected command and invalidating its old native target.</summary>
        public void SelectController(string? controllerId)
        {
            Change(() =>
            {
            if (ControllerId != controllerId) { ClearInput(); AlternativeId = null; }
            ControllerId = controllerId;
                    });
        }
        /// <summary>Selects an occurrence or alternative without reusing the old occurrence's pending input.</summary>
        public void SelectAlternative(string? alternativeId)
        {
            Change(() =>
            {
            if (AlternativeId != alternativeId) ClearInput();
            AlternativeId = alternativeId;
                    });
        }
        /// <summary>Invalidates a physical target when its adapter cannot translate it; stale native input must not remain actionable.</summary>
        public void BlockInput(string reason)
        {
            Change(() =>
            {
            InputKey = null; InputBlockedReason = reason;
                    });
        }
        /// <summary>Applies several transitions as one; observers see only the final selection, never an intermediate reset.</summary>
        public void Update(Action change)
        {
            ArgumentNullException.ThrowIfNull(change);
            Change(change);
        }
        /// <summary>Clears selection when closing or replacing a workspace.</summary>
        public void Reset()
        {
            Change(() =>
            {
            Scope = ""; ControllerId = CommandId = AlternativeId = null;
            ClearInput(); Slot = _defaultSlot;
                    });
        }
        #endregion END Public
        #region BEGIN private helpers
        private void Change(Action change)
        {
            var before = (Scope, ControllerId, CommandId, Slot, AlternativeId, InputKey, InputBlockedReason);
            _changing++;
            try { change(); }
            finally { _changing--; }
            if (_changing == 0 && before != (Scope, ControllerId, CommandId, Slot, AlternativeId, InputKey, InputBlockedReason))
                Changed?.Invoke(this, EventArgs.Empty);
        }
        private void ClearInput() { InputKey = null; InputBlockedReason = ""; }
        #endregion END private helpers
    }
}

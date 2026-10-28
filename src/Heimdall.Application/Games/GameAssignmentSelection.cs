using Heimdall.Domain.MultiSim;

namespace Heimdall.Application.Games
{
    /// <summary>One UI-independent source for controller, command, slot and pending input selection.
    /// Selection transitions never edit a profile or persist a workspace.</summary>
    public sealed class GameAssignmentSelection
    {
        #region BEGIN Public
        /// <summary>Gets the displayed controller identity, independent of any physical hardware link.</summary>
        public string? ControllerId { get; private set; }
        /// <summary>Gets the selected visible native command identity.</summary>
        public string? CommandId { get; private set; }
        /// <summary>Gets the adapter-selected native binding slot.</summary>
        public string Slot { get; private set; } = "Primary";
        /// <summary>Gets the pending native input key, cleared when its selection context changes.</summary>
        public string? InputKey { get; private set; }

        /// <summary>Preserves a visible command by identity, otherwise selects the first visible command.
        /// Controller changes invalidate pending input even when the command remains selected.</summary>
        public void Refresh(string? controllerId, IReadOnlyList<SimCommand> commands, string? preferredCommandId = null, bool selectFirst = true)
        {
            if (ControllerId != controllerId) InputKey = null;
            ControllerId = controllerId;
            var wanted = preferredCommandId ?? CommandId;
            SelectCommand(commands.FirstOrDefault(x => x.Id == wanted)?.Id ?? (selectFirst && commands.Count > 0 ? commands[0].Id : null));
        }
        /// <summary>Selects a command; a different command cannot inherit stale pending input.</summary>
        public void SelectCommand(string? commandId)
        {
            if (CommandId != commandId) InputKey = null;
            CommandId = commandId;
        }
        /// <summary>Switches native binding slot and invalidates input from the previous slot.</summary>
        public void SelectSlot(string slot)
        {
            if (Slot != slot) InputKey = null;
            Slot = slot;
        }
        /// <summary>Tracks a native input supplied by the adapter; no input survives an empty command selection.</summary>
        public void SelectInput(string inputKey) => InputKey = CommandId is null ? null : inputKey;
        /// <summary>Clears selection when closing or replacing a workspace.</summary>
        public void Reset()
        {
            ControllerId = CommandId = InputKey = null;
            Slot = "Primary";
        }
        #endregion END Public
    }
}

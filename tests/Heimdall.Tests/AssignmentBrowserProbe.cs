using Heimdall.Application.Games;

namespace Heimdall.Tests
{
    /// <summary>Native predicate probe exercising the actual shared browser owner.</summary>
    public sealed class AssignmentBrowserProbe(WorkspaceContextProbe context) : GameAssignmentBrowserBase<string, string, string>(context, "all")
    {
        #region BEGIN Public
        /// <summary>Gets or sets a projection failure to verify retained rows and retry.</summary>
        public bool Fail { get; set; }
        /// <summary>Gets or sets work run during presentation publication.</summary>
        public Action? Published { get; set; }
        #endregion END Public
        protected override string FilterForSelection(string filter, WorkspaceSelection selection) => filter;
        protected override Heimdall.Application.Games.WorkspaceSelection ScopeSelection => Context.State.Selection;
        protected override IReadOnlyList<string> ReadCommands() => Fail ? throw new InvalidOperationException("native projection failed") : ["fire", "pitch"];
        protected override IReadOnlyList<string> ReadRecords() => ["fire", "pitch"];
        protected override string CommandId(string command) => command;
        protected override string RecordId(string record) => record;
        protected override bool IncludeCommand(string command, string filter, string search) => command.Contains(search, StringComparison.OrdinalIgnoreCase) && (filter == "all" || command == filter);
        protected override bool IncludeRecord(string record, string filter, string search) => record == Context.State.Selection.CommandId;
        protected override WorkspaceSelection SelectionForCommand(string command) => Context.State.Selection with { CommandId = command };
        protected override WorkspaceSelection SelectionForRecord(string record) => Context.State.Selection with { CommandId = record, AlternativeId = record };
        protected override void OnProjectionChanged() => Published?.Invoke();
    }
}

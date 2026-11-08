using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;

namespace Heimdall.Tests
{
    /// <summary>A typed profile command whose native review, run and success are controlled by a case.</summary>
    public sealed class ProfileCommandProbe(WorkspaceContextProbe context, ProfileTestInput input) : GameProfileCommandBase<string, string, bool>(context, input)
    {
        #region BEGIN Public
        /// <summary>Gets or sets the native availability.</summary>
        public WorkspaceAvailability Native { get; set; } = new(true, "");
        /// <summary>Gets or sets work run during review.</summary>
        public Action? DuringReview { get; set; }
        /// <summary>Gets or sets work run during native execution.</summary>
        public Func<Task>? DuringRun { get; set; }
        /// <summary>Gets or sets whether native work succeeds.</summary>
        public bool Succeeds { get; set; } = true;
        /// <summary>Gets or sets the durable effect produced by the native run.</summary>
        public ProfileCommandEffects Effects { get; set; }
        /// <summary>Gets or sets native acceptance work, including a fault after a write.</summary>
        public Action? DuringAccept { get; set; }
        /// <summary>Gets the hook call order.</summary>
        public List<string> Calls { get; } = [];
        /// <summary>Gets the operation active during prepare.</summary>
        public WorkspaceOperationKind? PreparedUnder { get; private set; }
        /// <summary>Gets the input stops observed at prepare.</summary>
        public int StopsAtPrepare { get; private set; } = -1;
        /// <summary>Gets the managed thread that accepted the native result.</summary>
        public int AcceptThreadId { get; private set; }
        #endregion END Public
        /// <inheritdoc/>
        protected override WorkspaceOperationKind Kind => WorkspaceOperationKind.Export;
        /// <inheritdoc/>
        protected override WorkspaceAvailability ReadAvailability() => Native;
        /// <inheritdoc/>
        protected override string Prepare(string request)
        {
            Calls.Add("prepare"); PreparedUnder = context.State.Operation; StopsAtPrepare = input.Stops;
            DuringReview?.Invoke(); return request + ":reviewed";
        }
        /// <inheritdoc/>
        protected override async Task<bool> RunNativeAsync(string prepared, IWorkspaceOperationLease lease)
        {
            Calls.Add("run:" + prepared);
            if (DuringRun is { } run) await run();
            lease.Cancellation.ThrowIfCancellationRequested();
            return Succeeds;
        }
        /// <inheritdoc/>
        protected override ProfileCommandEffects ReadEffects(bool result) => Effects;
        /// <inheritdoc/>
        protected override bool IsSuccessful(bool result) => result;
        /// <inheritdoc/>
        protected override void Accept(bool result, IWorkspaceOperationLease lease) { Calls.Add("accept"); AcceptThreadId = Environment.CurrentManagedThreadId; DuringAccept?.Invoke(); }
    }
}

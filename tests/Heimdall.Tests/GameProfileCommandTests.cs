using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Typed import/export/configuration commands share availability, stale-review and operation admission.</summary>
    public sealed class GameProfileCommandTests
    {
        private sealed class Command(WorkspaceContextProbe context, ProfileTestInput input) : GameProfileCommandBase<string, string, bool>(context, input)
        {
            public WorkspaceAvailability Native { get; set; } = new(true, "");
            public Action? DuringReview { get; set; }
            public Func<Task>? DuringRun { get; set; }
            public bool Succeeds { get; set; } = true;
            public List<string> Calls { get; } = [];
            protected override WorkspaceOperationKind Kind => WorkspaceOperationKind.Export;
            protected override WorkspaceAvailability ReadAvailability() => Native;
            protected override string Prepare(string request) { Calls.Add("prepare"); DuringReview?.Invoke(); return request + ":reviewed"; }
            protected override async Task<bool> RunNativeAsync(string prepared, IWorkspaceOperationLease lease)
            {
                Calls.Add("run:" + prepared);
                if (DuringRun is { } run) await run();
                lease.Cancellation.ThrowIfCancellationRequested();
                return Succeeds;
            }
            protected override bool IsSuccessful(bool result) => result;
            protected override void Accept(bool result, IWorkspaceOperationLease lease) => Calls.Add("accept");
        }
        #region BEGIN Public
        /// <summary>Success runs native work once inside an admitted operation and accepts once.</summary>
        [Fact]
        public async Task Available_command_runs_once_and_accepts()
        {
            using var context = new WorkspaceContextProbe(new()); context.Open(); var input = new ProfileTestInput();
            var command = new Command(context, input);
            IGameProfileCommand<string, bool> contract = command;
            Assert.True(contract.Availability.Enabled);
            Assert.True(await contract.ExecuteAsync("out", CancellationToken.None));
            Assert.Equal(["prepare", "run:out:reviewed", "accept"], command.Calls); Assert.Equal(1, input.Stops); Assert.Null(context.State.Operation);
        }
        /// <summary>Unavailable, stale-reviewed, unsuccessful, cancelled and reentrant commands never accept native output.</summary>
        [Fact]
        public async Task Unavailable_stale_unsuccessful_cancelled_and_reentrant_commands_do_not_accept()
        {
            using var context = new WorkspaceContextProbe(new()); context.Open();
            var command = new Command(context, new()) { Native = new(false, "Edition does not allow export.") };
            Assert.Equal("Edition does not allow export.", command.Availability.Reason);
            Assert.Equal("Edition does not allow export.", (await Assert.ThrowsAsync<InvalidOperationException>(() => command.ExecuteAsync("x", CancellationToken.None))).Message);
            Assert.Empty(command.Calls);
            command.Native = new(true, ""); command.DuringReview = () => context.Edit("changed during review");
            await Assert.ThrowsAsync<InvalidOperationException>(() => command.ExecuteAsync("x", CancellationToken.None));
            Assert.Equal(["prepare"], command.Calls);
            command.DuringReview = null; command.Succeeds = false; command.Calls.Clear();
            Assert.False(await command.ExecuteAsync("x", CancellationToken.None)); Assert.DoesNotContain("accept", command.Calls);
            command.Succeeds = true; command.Calls.Clear();
            using var cancel = new CancellationTokenSource(); command.DuringRun = () => { cancel.Cancel(); return Task.CompletedTask; };
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => command.ExecuteAsync("x", cancel.Token));
            Assert.DoesNotContain("accept", command.Calls); Assert.Null(context.State.Operation);
            Exception? nested = null; command.Calls.Clear();
            command.DuringRun = async () => { Assert.False(command.Availability.Enabled); nested = await Record.ExceptionAsync(() => command.ExecuteAsync("y", CancellationToken.None)); };
            Assert.True(await command.ExecuteAsync("x", CancellationToken.None));
            Assert.IsType<InvalidOperationException>(nested); Assert.Single(command.Calls, "accept");
        }
        #endregion END Public
    }
}

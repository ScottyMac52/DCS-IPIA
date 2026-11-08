using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Typed import/export/configuration commands share availability, stale-review and operation admission.</summary>
    public sealed class GameProfileCommandTests
    {
        #region BEGIN Public
        /// <summary>Success runs native work once inside an admitted operation and accepts once.</summary>
        [Fact]
        public async Task Available_command_runs_once_and_accepts()
        {
            using var context = new WorkspaceContextProbe(new()); context.Open(); var input = new ProfileTestInput();
            var command = new ProfileCommandProbe(context, input);
#pragma warning disable CA1859 // Deliberately exercised through the public command contract.
            IGameProfileCommand<string, bool> contract = command;
#pragma warning restore CA1859
            Assert.True(contract.Availability.Enabled);
            Assert.True(await contract.ExecuteAsync("out", CancellationToken.None));
            Assert.Equal(["prepare", "run:out:reviewed", "accept"], command.Calls); Assert.Equal(1, input.Stops); Assert.Null(context.State.Operation);
            // Spec order: admission, then stop input, then prepare/review inside the one operation.
            Assert.Equal(WorkspaceOperationKind.Export, command.PreparedUnder); Assert.Equal(1, command.StopsAtPrepare);
        }
        /// <summary>Unavailable, stale-reviewed, unsuccessful, cancelled and reentrant commands never accept native output.</summary>
        [Fact]
        public async Task Unavailable_stale_unsuccessful_cancelled_and_reentrant_commands_do_not_accept()
        {
            using var context = new WorkspaceContextProbe(new()); context.Open();
            var command = new ProfileCommandProbe(context, new()) { Native = new(false, "Edition does not allow export.") };
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

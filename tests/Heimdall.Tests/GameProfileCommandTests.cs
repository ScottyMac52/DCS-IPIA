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
            Assert.True((await contract.ExecuteAsync("out", CancellationToken.None)).Native);
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
            Assert.False((await command.ExecuteAsync("x", CancellationToken.None)).Native); Assert.DoesNotContain("accept", command.Calls);
            command.Succeeds = true; command.Calls.Clear();
            using var cancel = new CancellationTokenSource(); command.DuringRun = () => { cancel.Cancel(); return Task.CompletedTask; };
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => command.ExecuteAsync("x", cancel.Token));
            Assert.DoesNotContain("accept", command.Calls); Assert.Null(context.State.Operation);
            Exception? nested = null; command.Calls.Clear();
            command.DuringRun = async () => { Assert.False(command.Availability.Enabled); nested = await Record.ExceptionAsync(() => command.ExecuteAsync("y", CancellationToken.None)); };
            Assert.True((await command.ExecuteAsync("x", CancellationToken.None)).Native);
            Assert.IsType<InvalidOperationException>(nested); Assert.Single(command.Calls, "accept");
        }
        /// <summary>A durable write followed by a native install/status failure remains committed and is never silently retried.</summary>
        [Theory]
        [InlineData(ProfileCommandEffects.WorkspaceWrite, true)]
        [InlineData(ProfileCommandEffects.OutputWrite, false)]
        public async Task Postwrite_accept_failure_preserves_receipt_and_reports_completion(ProfileCommandEffects effects, bool recovery)
        {
            using var context = new WorkspaceContextProbe(new()); context.Open();
            var writes = 0;
            var command = new ProfileCommandProbe(context, new())
            {
                Effects = effects, DuringRun = () => { writes++; return Task.CompletedTask; },
                DuringAccept = () => throw new IOException("native completion failed")
            };
            var result = await command.ExecuteAsync("durable receipt", default);
            Assert.True(result.Native);
            Assert.Equal((WorkspaceOperationOutcome.Applied, true, recovery),
                (result.Completion.Outcome, result.Completion.Committed, result.Completion.RecoveryRequired));
            Assert.Contains("native completion failed", result.Completion.Message, StringComparison.Ordinal);
            Assert.Equal(result.Completion.Message, context.Status); Assert.Null(context.State.Operation); Assert.Equal(1, writes);
            Assert.Equal(recovery, context.State.Recovery.Length > 0);
            if (recovery)
            {
                Assert.False(command.Availability.Enabled);
                await Assert.ThrowsAsync<InvalidOperationException>(() => command.ExecuteAsync("retry", default));
                Assert.Equal(1, writes);
            }
        }
        /// <summary>An import/unsaved operation whose acceptance fails has no durable write to claim.</summary>
        [Fact]
        public async Task Non_durable_accept_failure_propagates_and_releases_operation()
        {
            using var context = new WorkspaceContextProbe(new()); context.Open();
            var command = new ProfileCommandProbe(context, new()) { DuringAccept = () => throw new IOException("install failed") };
            await Assert.ThrowsAsync<IOException>(() => command.ExecuteAsync("import", default));
            Assert.Null(context.State.Operation); Assert.Empty(context.State.Recovery);
        }
        #endregion END Public
    }
}

using Heimdall.Application.Games;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Protects asynchronous DCS save/discard transitions under the shared lifecycle guard.</summary>
    public sealed class AsyncWorkspaceLeaveTests
    {
        #region BEGIN Public
        /// <summary>A clean workspace never prompts, saves or discards.</summary>
        [Fact]
        public async Task Clean_workspace_bypasses_every_native_callback()
        {
            Assert.True(await GameWorkspaceLeaveGuard.ConfirmAsync(false,
                () => throw new InvalidOperationException("Unexpected prompt"),
                () => throw new InvalidOperationException("Unexpected save"),
                () => throw new InvalidOperationException("Unexpected discard")));
        }
        /// <summary>The decision remains pending until the native repository finishes its save.</summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Leave_waits_for_persistence_and_preserves_failed_save_drafts(bool saved)
        {
            var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var discarded = 0;
            var leave = GameWorkspaceLeaveGuard.ConfirmAsync(true, () => GameWorkspaceLeaveDecision.Save,
                () => completed.Task, () => discarded++);
            Assert.False(leave.IsCompleted);
            completed.SetResult(saved);
            Assert.Equal(saved, await leave);
            Assert.Equal(0, discarded);
        }
        /// <summary>Only explicit discard invokes the native discard command.</summary>
        [Theory]
        [InlineData(GameWorkspaceLeaveDecision.Cancel, false, 0)]
        [InlineData(GameWorkspaceLeaveDecision.Discard, true, 1)]
        public async Task Cancel_and_discard_have_separate_native_effects(GameWorkspaceLeaveDecision decision, bool allowed, int count)
        {
            var discarded = 0;
            Assert.Equal(allowed, await GameWorkspaceLeaveGuard.ConfirmAsync(true, () => decision,
                () => throw new InvalidOperationException("Unexpected save"), () => discarded++));
            Assert.Equal(count, discarded);
        }
        /// <summary>A repository exception does not silently discard the pending workspace.</summary>
        [Fact]
        public async Task Save_exceptions_propagate_without_discarding()
        {
            var discarded = 0;
            await Assert.ThrowsAsync<IOException>(() => GameWorkspaceLeaveGuard.ConfirmAsync(true,
                () => GameWorkspaceLeaveDecision.Save, () => Task.FromException<bool>(new IOException("Save failed")), () => discarded++));
            Assert.Equal(0, discarded);
        }
        #endregion END Public
    }
}

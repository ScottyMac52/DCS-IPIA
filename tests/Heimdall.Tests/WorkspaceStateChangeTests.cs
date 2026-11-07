using Heimdall.Application.Games;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Selection notifications must not be mistaken for native content changes that reload editors.</summary>
    public sealed class WorkspaceStateChangeTests
    {
        [Fact]
        public void PendingInputSelectionIsSelectionOnly()
        {
            using var context = new WorkspaceContextProbe(new WorkspaceTestScheduler());
            context.Open();
            context.Select(context.State.Selection with { ControllerId = "Keyboard", CommandId = "PrimaryFire" });
            var before = context.State; var snapshot = context.CaptureSnapshot();
            context.Select(context.State.Selection with { InputId = "Key_B" });
            Assert.Equal("Key_B", context.State.Selection.InputId);
            Assert.True(WorkspaceStateChange.IsSelectionOnly(before, snapshot, context.State, context.CaptureSnapshot()));
        }

        [Fact]
        public void NativeContentEditWhileDirtyIsNotSelectionOnly()
        {
            using var context = new WorkspaceContextProbe(new WorkspaceTestScheduler());
            context.Open(); context.Edit("first");
            var before = context.State; var snapshot = context.CaptureSnapshot();
            context.Edit("second");
            Assert.False(WorkspaceStateChange.IsSelectionOnly(before, snapshot, context.State, context.CaptureSnapshot()));
        }

        [Fact]
        public void SelectionWithOtherStateChangesIsNotSelectionOnly()
        {
            using var context = new WorkspaceContextProbe(new WorkspaceTestScheduler());
            context.Open();
            var before = context.State; var snapshot = context.CaptureSnapshot();
            context.SetEditedName("Renamed");
            context.Select(context.State.Selection with { ControllerId = "Keyboard" });
            Assert.False(WorkspaceStateChange.IsSelectionOnly(before, snapshot, context.State, context.CaptureSnapshot()));
            Assert.False(WorkspaceStateChange.IsSelectionOnly(null, null, context.State, context.CaptureSnapshot()));
            Assert.False(WorkspaceStateChange.IsSelectionOnly(context.State, context.CaptureSnapshot(), context.State, context.CaptureSnapshot()));
        }
    }
}

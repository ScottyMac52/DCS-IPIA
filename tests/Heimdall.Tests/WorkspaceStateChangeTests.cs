using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
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
    
        [Fact]
        public void InputTestStartAndStopArePresentationOnly()
        {
            using var context = new WorkspaceContextProbe(new WorkspaceTestScheduler());
            context.Open();
            var idle = context.State; var snapshot = context.CaptureSnapshot();
            Assert.True(context.TryStartActivity(WorkspaceActivityKind.Test, out var lease).Enabled);
            var testing = context.State;
            Assert.True(WorkspaceStateChange.IsPresentationOnly(idle, snapshot, testing, context.CaptureSnapshot()));
            lease!.Dispose();
            Assert.Null(context.State.Activity);
            Assert.True(WorkspaceStateChange.IsPresentationOnly(testing, snapshot, context.State, context.CaptureSnapshot()));
        }

        [Fact]
        public void NativeFingerprintChangesReloadButOperationGatesDoNot()
        {
            using var context = new WorkspaceContextProbe(new WorkspaceTestScheduler());
            context.Open();
            var before = context.State; var snapshot = context.CaptureSnapshot();
            context.Edit("staged");
            Assert.False(WorkspaceStateChange.IsPresentationOnly(before, snapshot, context.State, context.CaptureSnapshot()));
            before = context.State; snapshot = context.CaptureSnapshot();
            context.SetEditedName("Renamed");
            Assert.False(WorkspaceStateChange.IsPresentationOnly(before, snapshot, context.State, context.CaptureSnapshot()));
            before = context.State; snapshot = context.CaptureSnapshot();
            WorkspaceState? during = null;
            context.RunOperation(WorkspaceOperationKind.Export, _ => { during = context.State; return new(WorkspaceOperationOutcome.Applied, ""); });
            Assert.True(WorkspaceStateChange.IsPresentationOnly(before, snapshot, during!, context.CaptureSnapshot()));
            Assert.False(WorkspaceStateChange.IsPresentationOnly(null, null, context.State, context.CaptureSnapshot()));
        }

        [Fact]
        public void PendingNameAndAccessChangesPreserveEditorsWhenNativeDataIsUnchanged()
        {
            using var context = new WorkspaceContextProbe(new()); context.Open();
            var before = context.State; var native = context.Working!.ToString();
            context.SetEditedName("Pending name");
            Assert.True(context.State.Dirty); Assert.Equal(native, context.Working.ToString());
            Assert.True(WorkspaceStateChange.IsPresentationOnly(before, native, context.State, context.Working.ToString()));
            before = context.State;
            context.ReadOnly = true; context.RefreshNativeState();
            Assert.True(WorkspaceStateChange.IsPresentationOnly(before, native, context.State, context.Working.ToString()));
        }

        [Fact]
        public void SameContentWithANewMountOrOpenStateStillReloads()
        {
            using var context = new WorkspaceContextProbe(new()); context.Open();
            var before = context.State; var native = context.Working!.ToString();
            context.Open();
            Assert.Equal(native, context.Working!.ToString());
            Assert.False(WorkspaceStateChange.IsPresentationOnly(before, native, context.State, context.Working.ToString()));
            Assert.False(WorkspaceStateChange.IsPresentationOnly(context.State, native, context.State with { Open = false }, native));
            Assert.False(WorkspaceStateChange.IsPresentationOnly(context.State, native, context.State, native));
        }
    }
}

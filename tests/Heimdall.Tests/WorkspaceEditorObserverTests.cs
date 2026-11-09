using Heimdall.Application.Games;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Pins editor invalidation separately from document caching, native input drafts and transient action gates.</summary>
    public sealed class WorkspaceEditorObserverTests
    {
        #region BEGIN Public
        /// <summary>Only a completed render accepts a new command, scope or native projection.</summary>
        [Fact]
        public void Unrendered_selection_and_evidence_revisions_remain_pending()
        {
            using var context = new WorkspaceContextProbe(new()); context.Open();
            var observer = new WorkspaceEditorObserver(context);
            Assert.True(observer.NeedsRefresh()); observer.Accept(); Assert.False(observer.NeedsRefresh());
            context.Select(context.State.Selection with { CommandId = "fire", SlotId = "Primary" });
            Assert.True(observer.NeedsRefresh()); Assert.True(observer.NeedsRefresh());
            Assert.False(observer.NeedsContentRefresh());
            observer.Accept(); Assert.False(observer.NeedsRefresh());
            Assert.True(observer.NeedsRefresh(1)); Assert.True(observer.NeedsRefresh(1));
            Assert.False(observer.NeedsContentRefresh());
            observer.Accept(1); Assert.False(observer.NeedsRefresh(1));
            context.Select(context.State.Selection with { SlotId = "Secondary" });
            Assert.True(observer.NeedsRefresh(1));
        }
        /// <summary>Pending fields and failed-operation gates cannot reload native baseline over a user's draft.</summary>
        [Fact]
        public void Pending_input_name_and_action_gates_preserve_the_rendered_editor()
        {
            using var context = new WorkspaceContextProbe(new()); context.Open();
            context.Select(context.State.Selection with { CommandId = "fire" });
            var observer = new WorkspaceEditorObserver(context); observer.Accept();
            context.Select(context.State.Selection with { InputId = "JOY_BTN2" });
            context.SetEditedName("Pending name"); context.ReadOnly = true; context.RefreshNativeState();
            Assert.False(observer.NeedsRefresh());
        }
        /// <summary>Completing a failed scan invalidates the editor even when the document and projection count are unchanged.</summary>
        [Fact]
        public void Scan_completion_and_content_or_session_changes_require_rendering()
        {
            using var context = new WorkspaceContextProbe(new()); context.Open();
            var observer = new WorkspaceEditorObserver(context); observer.Accept(3);
            Assert.True(context.TryStartActivity(WorkspaceActivityKind.Scan, out var lease).Enabled);
            Assert.True(observer.NeedsRefresh(3)); observer.Accept(3);
            lease!.Dispose(); Assert.True(observer.NeedsRefresh(3)); observer.Accept(3);
            context.Edit("first"); Assert.True(observer.NeedsRefresh(3)); Assert.True(observer.NeedsContentRefresh()); observer.Accept(3);
            context.Edit("second"); Assert.True(observer.NeedsRefresh(3)); observer.Accept(3);
            context.Open(); Assert.True(observer.NeedsRefresh(3));
        }
        #endregion END Public
    }
}

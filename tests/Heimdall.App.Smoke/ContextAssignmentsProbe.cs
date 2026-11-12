using System.Windows.Controls;
using Heimdall.App.Games;
using Heimdall.Application.Games;

namespace Heimdall.App.Smoke
{
    /// <summary>One real shared-view consumer, bound in turn to each production context adapter.</summary>
    internal sealed class ContextAssignmentsProbe : GameAssignmentsView
    {
        private readonly WorkspaceContextCase _fixture;
        internal ContextAssignmentsProbe(WorkspaceContextCase fixture)
        {
            _fixture = fixture;
            EditorPanel = Pending;
        }
        internal void Bind() => BindAssignmentContext(_fixture.Context);
        internal TextBox Pending { get; } = new();
        internal int Reloads { get; private set; }
        internal int AvailabilityUpdates { get; private set; }
        internal bool FailReload { get; set; }
        internal void ReloadNative() => RefreshAssignments();
        protected override WorkspaceAssignmentContext CaptureAssignmentContext() => _fixture.Context.Context;
        protected override Task SaveAssignmentsCoreAsync() => Task.CompletedTask;
        protected override void DiscardAssignmentsCore() { }
        protected override void RefreshAssignmentsCore()
        {
            if (FailReload) throw new InvalidOperationException("Injected native editor render failure.");
            Reloads++; Pending.Text = "Loaded native record";
        }
        protected override void RefreshAssignmentAvailability() => AvailabilityUpdates++;
        protected override void RefreshAssignmentDrawing() { }
        protected override void ShowAssignmentStatus(string message) { }
    }
}


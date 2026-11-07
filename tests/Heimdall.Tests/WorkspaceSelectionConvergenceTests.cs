using Xunit;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;

namespace Heimdall.Tests
{
    /// <summary>Guards the DCS assignment refresh pattern that re-selects scope from a state notification.</summary>
    public sealed class WorkspaceSelectionConvergenceTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void RefreshThatReselectsScopeFromStateChangedSettles(bool consistentScope)
        {
            var scheduler = new BoundedScheduler();
            using var bound = new BoundedProbe(scheduler);
            bound.Open();
            var engine = ((IWorkspaceSelectionEngineOwner)bound).SelectionEngine;
            // Mirrors DcsAssignmentsView.RefreshAssignmentActions: synchronize the engine, then capture the context.
            bound.StateChanged += (_, _) =>
            {
                engine.SelectScope("module:Module:Base");
                engine.SelectController("device");
                bound.Select(bound.State.Selection with { ControllerId = "device", NativeScope = consistentScope ? "module:Module:Base" : "Module|Base" });
            };
            bound.Edit("staged");
            var drained = scheduler.Drain(1000);
            if (consistentScope) Assert.True(drained < 1000, "Consistent scope must settle.");
            else Assert.Equal(1000, drained);
        }

        private sealed class BoundedScheduler : IWorkspaceScheduler
        {
            private readonly Queue<Action> _queue = new();
            public bool CheckAccess() => true;
            public void VerifyAccess() { }
            public void Post(Action action) => _queue.Enqueue(action);
            public int Drain(int limit)
            {
                var count = 0;
                while (count < limit && _queue.TryDequeue(out var action)) { action(); count++; }
                return count;
            }
        }
        private sealed class BoundedProbe(IWorkspaceScheduler scheduler) : GameWorkspaceContextBase<WorkspaceTestSnapshot>(scheduler)
        {
            private WorkspaceTestSnapshot? _working, _saved;
            public void Open() => ReplaceNativeSession(() => _saved = _working = new("native:key", "Profile", "saved"));
            public void Edit(string data) { _working = _working! with { Data = data }; NativeStateChanged(); }
            protected override NativeWorkspaceFacts ReadNativeFacts()
            {
                var ready = new WorkspaceAvailability(true, "");
                return new(_working?.Key ?? "", _working is not null, false, "", _working != _saved, false, _saved?.Name ?? "", new(ready, ready, ready, ready, ready, ready, ready));
            }
            protected override WorkspaceTestSnapshot? ReadWorking() => _working;
            protected override WorkspaceTestSnapshot? ReadSaved() => _saved;
            protected override string CaptureNativeFingerprint() => _working?.ToString() ?? "";
            protected override bool NameEquals(string left, string right) => left == right;
            protected override void DisposeNative() { }
        }
    }
}

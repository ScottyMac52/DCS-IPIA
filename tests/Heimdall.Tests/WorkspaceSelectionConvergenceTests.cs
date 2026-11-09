using Xunit;
using Heimdall.Application.Interfaces;

namespace Heimdall.Tests
{
    /// <summary>Guards the DCS assignment refresh pattern that re-selects scope from a state notification.</summary>
    public sealed class WorkspaceSelectionConvergenceTests
    {
        #region BEGIN Public
        /// <summary>Distinguishes a convergent scope refresh from a bounded deliberately inconsistent scope loop.</summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void RefreshThatReselectsScopeFromStateChangedSettles(bool consistentScope)
        {
            var scheduler = new ConvergenceWorkspaceScheduler();
            using var bound = new ConvergenceWorkspaceContext(scheduler);
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

        #endregion END Public
    }
}

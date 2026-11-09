using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Section changes must not publish a transient selection without a controller (Elite reference preview regression).</summary>
    public sealed class GameAssignmentSelectionUpdateTests
    {
        #region BEGIN Public
        /// <summary>Batches scope and controller changes into one notification containing the final controller.</summary>
        [Fact]
        public void ScopeAndControllerUpdatePublishesOnceWithController()
        {
            var selection = new GameAssignmentSelection();
            selection.SelectScope("profile:All"); selection.SelectController("06A30BAC");
            var observed = new List<string?>();
            selection.Changed += (_, _) => observed.Add(selection.ControllerId);
            selection.Update(() => { selection.SelectScope("profile:Systems"); selection.SelectController("06A30BAC"); });
            Assert.Equal(["06A30BAC"], observed);
            Assert.Equal("profile:Systems", selection.Scope);
        }

        /// <summary>Prevents section-change observers from seeing an intermediate missing controller.</summary>
        [Fact]
        public void ContextListenersNeverSeeMissingControllerDuringSectionChange()
        {
            using var context = new WorkspaceContextProbe(new WorkspaceTestScheduler());
            context.Open();
            context.Select(context.State.Selection with { NativeScope = "profile:All", ControllerId = "06A30BAC" });
            var engine = ((IWorkspaceSelectionEngineOwner)context).SelectionEngine;
            var controllers = new List<string?>();
            context.StateChanged += (_, _) => controllers.Add(context.State.Selection.ControllerId);
            engine.Update(() => { engine.SelectScope("profile:Systems"); engine.SelectController("06A30BAC"); });
            Assert.NotEmpty(controllers);
            Assert.All(controllers, id => Assert.Equal("06A30BAC", id));
        }
        #endregion END Public
    }
}

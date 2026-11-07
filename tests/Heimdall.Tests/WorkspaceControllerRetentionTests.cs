using Heimdall.Application.Games;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>A save replaces the native session and resets selection; the displayed controller must survive the rebuild.</summary>
    public sealed class WorkspaceControllerRetentionTests
    {
        private static readonly string[] Ids = ["Keyboard", "06A30BAC"];

        [Fact]
        public void SessionReplacementKeepsDisplayedController()
        {
            using var context = new WorkspaceContextProbe(new WorkspaceTestScheduler());
            context.Open();
            context.Select(context.State.Selection with { ControllerId = "06A30BAC" });
            context.Open(name: "Saved");
            Assert.Null(context.State.Selection.ControllerId);
            Assert.Equal("06A30BAC", WorkspaceControllerRetention.Choose(Ids, context.State.Selection.ControllerId, "06A30BAC"));
        }

        [Fact]
        public void SelectionWinsThenFallsBackToFirst()
        {
            Assert.Equal("Keyboard", WorkspaceControllerRetention.Choose(Ids, "Keyboard", "06A30BAC"));
            Assert.Equal("Keyboard", WorkspaceControllerRetention.Choose(Ids, "Removed", "AlsoRemoved"));
            Assert.Null(WorkspaceControllerRetention.Choose([], null, "06A30BAC"));
        }
    }
}

using System.Text;
using Heimdall.Application.Games;
using Heimdall.EliteDangerous;
using Heimdall.TestSupport;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Deterministic work-count regressions for shared context hot paths (selection, refresh, edits) on a real native session.</summary>
    public sealed class WorkspaceHotPathTests
    {
        #region BEGIN Public
        /// <summary>Selection and no-op refreshes publish the contract notifications but never serialize or clone the workspace.</summary>
        [Theory]
        [InlineData(0)]
        [InlineData(600)]
        public void Selection_and_noop_refresh_do_no_whole_workspace_serialization(int extra)
        {
            using var issuer = new OwnerLicenseTestIssuer(); using var context = new ProfileWorkspaceContextProbe();
            var session = Open(issuer.Policy, extra); context.Session = session; context.Revision = "r";
            var commands = session.Catalog.Commands.Take(20).Select(command => command.Id).ToArray();
            context.NativeSelection.RefreshIds(null, commands);
            var events = 0; context.StateChanged += (_, _) => events++;
            var before = GameWorkspaceContract.ThreadSerializations; var content = context.ContentVersion;
            foreach (var id in commands.Skip(1)) context.NativeSelection.SelectCommand(id);
            for (var i = 0; i < 10; i++) context.RefreshNativeState();
            Assert.Equal(0, GameWorkspaceContract.ThreadSerializations - before);
            Assert.Equal(commands.Length - 1, events);
            Assert.Equal(commands[^1], context.State.Selection.CommandId);
            Assert.Equal(content, context.ContentVersion);
        }
        /// <summary>A genuine edit serializes the current document at most a bounded number of times, reloads content and keeps exact dirty semantics.</summary>
        [Fact]
        public void Native_edit_is_bounded_and_dirty_equality_is_exact()
        {
            using var issuer = new OwnerLicenseTestIssuer(); using var context = new ProfileWorkspaceContextProbe();
            var session = Open(issuer.Policy, 600); context.Session = session; context.Revision = "r";
            var original = session.Snapshot.Name; var content = context.ContentVersion;
            var before = GameWorkspaceContract.ThreadSerializations;
            session.Rename("Edited");
            Assert.InRange(GameWorkspaceContract.ThreadSerializations - before, 1, 3);
            Assert.True(context.State.Dirty); Assert.Equal("Edited", context.Working!.Name); Assert.NotEqual(content, context.ContentVersion);
            session.Rename(original);
            Assert.False(context.State.Dirty); Assert.False(session.HasChanges);
            session.Rename(original);
            Assert.False(context.State.Dirty);
            session.Rename("Again"); Assert.True(session.HasChanges);
            context.RunOperation(WorkspaceOperationKind.Discard, lease => { session.Discard(lease); return new(WorkspaceOperationOutcome.Applied, ""); });
            Assert.False(context.State.Dirty); Assert.Equal(original, context.Working!.Name);
        }
        /// <summary>Snapshots stay independent mutable copies even when content facts are cached.</summary>
        [Fact]
        public void Snapshots_remain_isolated_copies()
        {
            using var issuer = new OwnerLicenseTestIssuer(); using var context = new ProfileWorkspaceContextProbe();
            var session = Open(issuer.Policy, 0); context.Session = session; context.Revision = "r";
            _ = session.HasChanges; var first = session.Snapshot; var second = session.Snapshot;
            Assert.NotSame(first, second); Assert.NotSame(first.Hardware, second.Hardware);
            first.Hardware.Clear(); first.Profile.Devices.Clear(); first.Profile.Metadata["mutated"] = "yes";
            Assert.Equal(second.Profile.Devices.Count, session.Snapshot.Profile.Devices.Count); Assert.False(session.Snapshot.Profile.Metadata.ContainsKey("mutated"));
            Assert.False(session.HasChanges); Assert.False(context.State.Dirty);
        }
        /// <summary>The content stamp ignores selection, name overlay and activity, and moves for edits, discard and session replacement.</summary>
        [Fact]
        public void Content_version_moves_only_for_native_content_or_identity()
        {
            using var issuer = new OwnerLicenseTestIssuer(); using var context = new ProfileWorkspaceContextProbe();
            var session = Open(issuer.Policy, 0); context.Session = session; context.Revision = "r";
            var observer = new WorkspaceChangeObserver(context); observer.Accept();
            var commands = session.Catalog.Commands.Take(3).Select(command => command.Id).ToArray();
            context.NativeSelection.RefreshIds(null, commands); context.NativeSelection.SelectCommand(commands[1]);
            Assert.Equal(WorkspaceChangeKind.Selection, observer.Observe());
            Assert.Equal(WorkspaceChangeKind.None, observer.Observe());
            context.SetEditedName("Overlay");
            Assert.Equal(WorkspaceChangeKind.Presentation, observer.Observe());
            Assert.True(context.TryStartActivity(WorkspaceActivityKind.Test, out var activity).Enabled);
            Assert.Equal(WorkspaceChangeKind.Presentation, observer.Observe());
            activity!.Dispose(); observer.Observe();
            session.Rename("Edited");
            Assert.Equal(WorkspaceChangeKind.Content, observer.Observe());
            context.RunOperation(WorkspaceOperationKind.Discard, lease => { session.Discard(lease); return new(WorkspaceOperationOutcome.Applied, ""); });
            Assert.Equal(WorkspaceChangeKind.Content, observer.Observe());
            context.Session = Open(issuer.Policy, 0);
            Assert.Equal(WorkspaceChangeKind.Content, observer.Observe());
        }
        #endregion END Public

        #region BEGIN private helpers
        internal static GameProfileSession Open(Heimdall.Application.EditionPolicy policy, int extra)
        {
            var backend = new EliteDangerousProfileBackend();
            var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures/EliteDangerous/Synthetic.4.2.binds"));
            var bindings = new StringBuilder();
            for (var i = 0; i < extra; i++)
                bindings.Append(System.Globalization.CultureInfo.InvariantCulture, $"<Cmd{i}><Primary Device=\"ExampleStick\" Key=\"Joy_{i % 32 + 1}\" /><Secondary Device=\"Keyboard\" Key=\"Key_{(char)('A' + i % 26)}\" /><ToggleOn Value=\"0\" /></Cmd{i}>");
            var profile = backend.Import("controls", new Dictionary<string, string> { ["controls.binds"] = text.Replace("</Root>", bindings + "</Root>", StringComparison.Ordinal) });
            return new(backend, new(1, Guid.NewGuid(), "Native", profile, new()), policy, [], () => []);
        }
        #endregion END private helpers
    }
}

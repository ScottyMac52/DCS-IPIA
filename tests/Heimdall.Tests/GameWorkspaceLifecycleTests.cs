using Heimdall.Application.Games;
using Heimdall.EliteDangerous;
using Heimdall.TestSupport;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Protects the shared, game-agnostic profile chrome and leave decision protocol
    /// before any WPF header is replaced with a composed shell.</summary>
    public sealed class GameWorkspaceLifecycleTests
    {
        [Fact]
        public void Clean_workspace_never_prompts_or_invokes_commands()
        {
            var prompts = 0;
            var result = GameWorkspaceLeaveGuard.Confirm(false,
                () => { prompts++; return GameWorkspaceLeaveDecision.Cancel; },
                () => throw new InvalidOperationException("Save should not run"),
                () => throw new InvalidOperationException("Discard should not run"));
            Assert.True(result);
            Assert.Equal(0, prompts);
            Assert.Equal("No profile", GameWorkspaceChromeState.For(null, null, null).Status);
            Assert.False(GameWorkspaceChromeState.For(null, null, null).CanExport);
        }

        [Fact]
        public void Cancel_and_failed_save_never_discard_a_pending_draft()
        {
            var discarded = 0;
            Assert.False(GameWorkspaceLeaveGuard.Confirm(true,
                () => GameWorkspaceLeaveDecision.Cancel, () => true, () => discarded++));
            Assert.False(GameWorkspaceLeaveGuard.Confirm(true,
                () => GameWorkspaceLeaveDecision.Save, () => false, () => discarded++));
            Assert.Equal(0, discarded);
        }

        [Fact]
        public void Explicit_discard_and_successful_save_follow_independent_paths()
        {
            var saves = 0; var discards = 0;
            Assert.True(GameWorkspaceLeaveGuard.Confirm(true,
                () => GameWorkspaceLeaveDecision.Save, () => { saves++; return true; }, () => discards++));
            Assert.Equal(1, saves); Assert.Equal(0, discards);
            Assert.True(GameWorkspaceLeaveGuard.Confirm(true,
                () => GameWorkspaceLeaveDecision.Discard, () => { saves++; return true; }, () => discards++));
            Assert.Equal(1, saves); Assert.Equal(1, discards);
        }

        [Fact]
        public void Profile_chrome_reflects_session_draft_unpersisted_profile_and_busy_state()
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var backend = new EliteDangerousProfileBackend();
            var profile = backend.Import("controls", new Dictionary<string, string>
            {
                ["controls.binds"] = "<Root PresetName=\"Test\" MajorVersion=\"4\" MinorVersion=\"0\" />"
            });
            var workspace = new GameWorkspace(1, Guid.NewGuid(), "Test", profile, new());
            var session = new GameProfileSession(backend, workspace, issuer.Policy, [], () => []);
            var clean = GameWorkspaceChromeState.For(session, "saved-revision", "Test");
            Assert.False(clean.Pending);
            Assert.True(clean.CanExport);
            Assert.False(clean.CanSave);
            Assert.Equal("Saved", clean.Status);
            var renamedInEditor = GameWorkspaceChromeState.For(session, "saved-revision", "Edited");
            Assert.True(renamedInEditor.Pending);
            Assert.True(renamedInEditor.CanSave);
            Assert.False(renamedInEditor.CanExport);
            session.Rename("Stage");
            Assert.True(GameWorkspaceChromeState.For(session, "saved-revision", "Stage").Pending);
            var busy = GameWorkspaceChromeState.For(session, "saved-revision", "Stage", busy: true);
            Assert.False(busy.CanSave); Assert.False(busy.CanDiscard); Assert.False(busy.CanExport);
            session.Discard();
            Assert.False(GameWorkspaceChromeState.For(session, "saved-revision", "Test").Pending);
            Assert.True(GameWorkspaceChromeState.For(session, null, "Test").Pending);
        }
    }
}

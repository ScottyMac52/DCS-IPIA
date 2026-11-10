using System.IO;
using Heimdall.App.Games.Dcs;
using Heimdall.App.Games.Elite;
using Heimdall.App.Games.Squadrons;
using Heimdall.App.Games.WarThunder;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.TestSupport;

namespace Heimdall.App.Smoke
{
    /// <summary>Exercises deferred editor rendering through real shared views and all four production browser/context adapters.</summary>
    internal static class BrowserEditorConformanceSmoke
    {
        #region BEGIN Public
        internal static void Verify()
        {
            var previous = EditionAccess.Policy; using var issuer = new OwnerLicenseTestIssuer();
            var root = Path.Combine(Path.GetTempPath(), "heimdall-browser-editor-" + Guid.NewGuid().ToString("N"));
            IReadOnlyList<WorkspaceContextCase> cases = [];
            try
            {
                EditionAccess.SetForTests(issuer.PremiumPolicy); cases = WorkspaceContextCases.Create(root, issuer.PremiumPolicy);
                foreach (var fixture in cases) Verify(fixture);
            }
            finally
            {
                foreach (var fixture in cases) { fixture.Profiles.Dispose(); fixture.Context.Dispose(); }
                EditionAccess.SetForTests(previous); if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void Verify(WorkspaceContextCase fixture)
        {
            using var browser = CreateBrowser(fixture.Context);
            using var view = new ContextAssignmentsProbe(fixture);
            var window = new ContextWindowProbe(fixture, view); var blocked = false;
            view.BindBrowser(browser, () => blocked);
            try
            {
                window.Show(); ThemeControlSmoke.Pump(window); browser.Refresh(); view.SynchronizeNativeAssignments();
                if (fixture.Context is WarThunderWorkspaceContext thunder && browser is Heimdall.WarThunder.WarThunderAssignmentBrowser nativeBrowser)
                {
                    Require(nativeBrowser.Commands.Count > 1, fixture, "Physical-scope conformance needs two native commands.");
                    browser.SelectCommand(nativeBrowser.Commands[0].Id);
                    var input = new Heimdall.WarThunder.WarThunderPhysicalInput("JOY_X", Heimdall.Domain.MultiSim.CommandType.Axis, 0);
                    thunder.SelectedPhysicalInput = input; thunder.Selection.SelectInput(input.Key);
                    Require(thunder.SelectedPhysicalInput == input, fixture, "Pending input lost its current navigation scope.");
                    browser.SelectCommand(nativeBrowser.Commands[1].Id);
                    Require(thunder.SelectedPhysicalInput is null, fixture, "A physical input survived a different command address.");
                }
                var before = view.Reloads; view.Pending.Text = "pending editor input";
                fixture.Context.SetEditedName("Pending name");
                Require(view.Reloads == before && view.Pending.Text == "pending editor input", fixture, "A name-only notification reloaded the editor.");
                blocked = true; fixture.Edit("first content revision");
                Require(view.Reloads == before, fixture, "A native refresh guard was ignored.");
                blocked = false; view.SynchronizeNativeAssignments();
                Require(view.Reloads == before + 1, fixture, "The deferred native editor revision was consumed without rendering.");
                before = view.Reloads; view.SynchronizeNativeAssignments(); browser.Refresh();
                Require(view.Reloads == before, fixture, "An identical editor revision reloaded again.");
                blocked = true; fixture.Edit("second content revision"); blocked = false;
                var selection = fixture.Context.State.Selection; var native = fixture.NativeSnapshot();
                view.FailReload = true; view.SynchronizeNativeAssignments();
                Require(view.Reloads == before && fixture.Context.Status.Contains("Injected native editor render failure", StringComparison.Ordinal), fixture,
                    "A failed native render was accepted or its diagnostic was lost.");
                Require(fixture.Context.State.Selection == selection && fixture.NativeSnapshot() == native, fixture,
                    "An editor render failure altered valid selection or native data.");
                view.FailReload = false; view.SynchronizeNativeAssignments();
                Require(view.Reloads == before + 1, fixture, "A failed native render could not be retried.");
                Require(fixture.Context.TryStartActivity(WorkspaceActivityKind.Scan, out var scan).Enabled, fixture, "The fixture could not start its hardware scan.");
                before = view.Reloads; blocked = true; scan!.Dispose();
                Require(view.Reloads == before, fixture, "Scan completion bypassed the native refresh guard.");
                blocked = false; view.SynchronizeNativeAssignments();
                Require(view.Reloads == before + 1, fixture, "Scan completion did not invalidate the native editor independently of document content.");
                Require(fixture.Writes() == 0, fixture, "Browser/editor synchronization performed a persistence write.");
                Console.WriteLine("Browser/editor state conformance: " + fixture.Name + " passed.");
            }
            finally { window.Close(); ThemeControlSmoke.Pump(window); }
        }
        private static IGameAssignmentBrowser CreateBrowser(IGameWorkspaceContext context) => context switch
        {
            DcsWorkspaceContext dcs => new Heimdall.Dcs.DcsAssignmentBrowser(dcs),
            EliteWorkspaceContext elite => new Heimdall.EliteDangerous.EliteAssignmentBrowser(elite, elite.Backend),
            SquadronsWorkspaceContext squadrons => new Heimdall.Squadrons.SquadronsAssignmentBrowser(squadrons, squadrons.Backend, () => squadrons.ControllerInputs),
            WarThunderWorkspaceContext thunder => new Heimdall.WarThunder.WarThunderAssignmentBrowser(thunder, thunder.Backend),
            _ => throw new InvalidOperationException("Unsupported production browser context.")
        };
        private static void Require(bool condition, WorkspaceContextCase fixture, string message)
        {
            if (!condition) throw new InvalidOperationException(fixture.Name + ": " + message);
        }
        #endregion END private helpers
    }
}

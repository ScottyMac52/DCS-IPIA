using System.Diagnostics;
using Heimdall.Application.Games;
using Heimdall.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace Heimdall.Tests
{
    /// <summary>Runs the same identity, cache, search, no-mutation and subscription policies through all four production native browsers.</summary>
    public sealed class AssignmentBrowserConformanceTests(ITestOutputHelper output)
    {
        #region BEGIN Public
        /// <summary>Warm searches, invalid selections, controller changes and native dirty changes follow one policy in every game.</summary>
        [Theory, InlineData("DCS"), InlineData("Elite"), InlineData("Squadrons"), InlineData("War Thunder")]
        public void Native_browsers_share_cached_identity_and_invalidation_policy(string game)
        {
            using var issuer = new OwnerLicenseTestIssuer(); using var c = BrowserConformanceCase.Open(game, issuer.Policy);
            c.Browser.Refresh(); Assert.NotEmpty(c.Ids()); var first = c.Ids()[0]; var initial = c.Metrics(); var copies = c.Copies(); var native = c.Native();
            var notifications = 0; c.Subscribe((_, _) => notifications++);
            c.Browser.Search(" no-such-command-9f49 "); Assert.Empty(c.Ids()); Assert.Null(c.Context.State.Selection.CommandId);
            c.Browser.Search(""); c.Browser.SelectCommand(first); Assert.Equal(first, c.Context.State.Selection.CommandId);
            c.Browser.SelectCommand("missing"); Assert.Null(c.Context.State.Selection.CommandId); Assert.Null(c.Context.State.Selection.InputId);
            c.Browser.SelectRecord("missing"); Assert.Null(c.Context.State.Selection.AlternativeId);
            var controller = game == "DCS" ? Guid.NewGuid().ToString("N") : game == "Squadrons" ? "squadrons.joystick.1" : "browser-controller";
            c.Context.Select(c.Context.State.Selection with { ControllerId = controller, NativeScope = "new scope" });
            Assert.Equal(controller, c.Context.State.Selection.ControllerId);
            c.Controller(null); c.Assigned(); c.Reset();
            Assert.Equal(initial.ContentBuilds, c.Metrics().ContentBuilds);
            // The DCS Native() diagnostic itself reads the working snapshot once; profile fingerprints do not clone.
            Assert.Equal(copies + (game == "DCS" ? 1 : 0), c.Copies());
            Assert.Equal(native, c.Native());
            var publications = c.Metrics().Publications;
            c.Browser.Refresh(); c.Browser.Search(""); Assert.Equal(publications, c.Metrics().Publications);
            c.Edit("dirty one"); Assert.Equal(initial.ContentBuilds + 1, c.Metrics().ContentBuilds);
            c.Edit("dirty two"); Assert.Equal(initial.ContentBuilds + 2, c.Metrics().ContentBuilds);
            Assert.True(c.Context.State.Dirty);
            c.Browser.SetActive(false); var inactive = c.Metrics().ContentBuilds; c.Edit("unmounted"); Assert.Equal(inactive, c.Metrics().ContentBuilds);
            c.Browser.SetActive(true); Assert.Equal(inactive + 1, c.Metrics().ContentBuilds);
            c.Browser.Dispose(); var disposed = c.Metrics(); c.Edit("disposed"); Assert.Equal(disposed, c.Metrics()); Assert.True(notifications > 0);
        }
        /// <summary>Records repeatable before/after backend work, allocation and duration without making machine-speed assertions.</summary>
        [Theory, InlineData("DCS"), InlineData("Elite"), InlineData("Squadrons"), InlineData("War Thunder")]
        public void Warm_browser_work_is_measured_without_document_copies(string game)
        {
            using var issuer = new OwnerLicenseTestIssuer(); using var c = BrowserConformanceCase.Open(game, issuer.Policy);
            c.Browser.Refresh(); var before = Measure(c.Baseline()); var builds = c.Metrics().ContentBuilds; var copies = c.Copies();
            var flip = false;
            var after = Measure(() => { c.Browser.Search(flip ? "a" : ""); flip = !flip; });
            Assert.Equal(builds, c.Metrics().ContentBuilds); Assert.Equal(copies, c.Copies());
            output.WriteLine($"{game}: commands={c.Ids().Count}; 20 warm queries: baseline {before.Milliseconds:F2}ms/{before.Bytes}B; inherited {after.Milliseconds:F2}ms/{after.Bytes}B; builds={builds}; warm copies=0.");
        }
        /// <summary>Closing removes every stale command/record and reopening uses a new content generation.</summary>
        [Theory, InlineData("DCS"), InlineData("Elite"), InlineData("Squadrons"), InlineData("War Thunder")]
        public void Native_close_and_reopen_clear_stale_projection(string game)
        {
            using var issuer = new OwnerLicenseTestIssuer(); using var c = BrowserConformanceCase.Open(game, issuer.Policy);
            c.Browser.Refresh(); var ids = c.Ids(); var builds = c.Metrics().ContentBuilds; c.Browser.SelectCommand(ids[0]);
            if (c.Context is DcsBrowserContext dcs)
            {
                dcs.Close(); Assert.Empty(c.Ids()); Assert.Empty(c.RecordIds()); Assert.Null(c.Context.State.Selection.CommandId); dcs.Reopen();
            }
            else
            {
                var context = Assert.IsType<ProfileWorkspaceContextProbe>(c.Context); var session = context.Session;
                context.Session = null; Assert.Empty(c.Ids()); Assert.Empty(c.RecordIds()); Assert.Null(c.Context.State.Selection.CommandId); context.Session = session;
            }
            Assert.Equal(ids, c.Ids()); Assert.Equal(builds + 2, c.Metrics().ContentBuilds);
        }
        /// <summary>Exercises shared filtering at 2,000 real DCS commands and 10,000 native assignment records.</summary>
        [Fact]
        public void Large_native_projection_keeps_warm_queries_independent_of_document_reads()
        {
            var snapshot = MixedSnapshotFixture.Create(); var source = snapshot.Assignments[0];
            var commands = Enumerable.Range(0, 2000).Select(i => new Heimdall.Application.DcsCommandPaletteEntry("stress-" + i, "Command " + i, "Stress", false)).ToArray();
            var rows = Enumerable.Range(0, 10000).Select(i => source with
            {
                Slot = source.Slot with { InputKey = "JOY_BTN" + (i + 1) },
                Command = new(commands[i % commands.Length].Id, commands[i % commands.Length].Name, false, Heimdall.Application.AssignmentLayer.Module)
            }).ToArray();
            using var context = new DcsBrowserContext(snapshot with { ModuleCommands = commands, Assignments = rows });
            using var browser = new Heimdall.Dcs.DcsAssignmentBrowser(context); browser.Refresh(); var reads = context.Reads;
            var flip = false; var measured = Measure(() => { browser.Search(flip ? "Command 1" : ""); flip = !flip; });
            Assert.Equal(1, browser.Metrics.ContentBuilds); Assert.Equal(reads, context.Reads);
            output.WriteLine($"Large DCS: 2,000 commands / 10,000 records; 20 queries={measured.Milliseconds:F2}ms allocation={measured.Bytes}B; document reads during warm queries=0.");
        }
        #endregion END Public
        #region BEGIN private helpers
        private static (double Milliseconds, long Bytes) Measure(Action run)
        {
            run(); var allocated = GC.GetAllocatedBytesForCurrentThread(); var watch = Stopwatch.StartNew();
            for (var i = 0; i < 20; i++) run();
            return (watch.Elapsed.TotalMilliseconds, GC.GetAllocatedBytesForCurrentThread() - allocated);
        }
        #endregion END private helpers
    }
}

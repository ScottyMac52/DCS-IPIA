using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Dcs;
using Heimdall.EliteDangerous;
using Heimdall.Squadrons;
using Heimdall.WarThunder;
using Heimdall.TestSupport;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Checks inherited native predicates against the established native status/filter rules on real captures.</summary>
    public sealed class AssignmentBrowserNativePolicyTests
    {
        #region BEGIN Public
        /// <summary>Native command selection determines the binding slot before any WPF editor is rendered.</summary>
        [Fact]
        public void Elite_selection_establishes_axis_and_button_slots_without_editor_writeback()
        {
            using var issuer = new OwnerLicenseTestIssuer(); using var fixture = BrowserConformanceCase.Open("Elite", issuer.Policy);
            var browser = Assert.IsType<EliteAssignmentBrowser>(fixture.Browser); browser.Refresh();
            var axis = browser.Commands.First(command => command.Type == Heimdall.Domain.MultiSim.CommandType.Axis);
            var button = browser.Commands.First(command => command.Type == Heimdall.Domain.MultiSim.CommandType.Button);
            browser.SelectCommand(axis.Id); Assert.Equal("Binding", fixture.Context.State.Selection.SlotId);
            browser.SelectCommand(button.Id); Assert.Equal("Primary", fixture.Context.State.Selection.SlotId);
            fixture.Context.Select(fixture.Context.State.Selection with { SlotId = "Secondary" });
            browser.SelectCommand(button.Id); Assert.Equal("Secondary", fixture.Context.State.Selection.SlotId);
        }
        /// <summary>All status variants retain native all-device, selected-device and unsupported-record meanings.</summary>
        [Theory, InlineData("DCS"), InlineData("Elite"), InlineData("Squadrons"), InlineData("War Thunder")]
        public void Native_filter_semantics_survive_shared_orchestration(string game)
        {
            using var issuer = new OwnerLicenseTestIssuer(); using var c = BrowserConformanceCase.Open(game, issuer.Policy);
            c.Browser.Refresh(); c.Browser.Search(" a ");
            if (c.Browser is DcsAssignmentBrowser dcs && c.Context is DcsBrowserContext dc)
            {
                var snapshot = dc.Working!;
                foreach (var layer in Enum.GetValues<AssignmentLayer>())
                foreach (var status in Enum.GetValues<DcsCommandAssignmentStatus>())
                {
                    var devices = DcsWorkspaceStructureEditor.Describe(snapshot).Devices.Where(x => x.Layer == layer).Select(x => x.ProfileId).Distinct().ToArray();
                    foreach (var controller in devices.Cast<Guid?>().Prepend(null))
                    {
                        dcs.SetFilter(new(layer, controller, Status: status));
                        var palette = layer == AssignmentLayer.Module ? snapshot.ModuleCommands : DcsCommandPalette.FromUiLayer(snapshot.UiCommands);
                        Assert.Equal(DcsCommandBrowser.Filter(palette, snapshot.Assignments, layer, controller, devices, search: "a", status: status).Select(x => x.Id), c.Ids());
                    }
                }
                return;
            }
            var context = Assert.IsType<ProfileWorkspaceContextProbe>(c.Context); var profile = context.Working!.Profile;
            if (c.Browser is EliteAssignmentBrowser ed)
            {
                foreach (var status in Enum.GetValues<GameCommandAssignmentStatus>())
                foreach (var controller in profile.Devices.Select(x => x.LogicalId).Prepend(null))
                {
                    ed.SetFilter(new(ControllerId: controller, Status: status));
                    Assert.Equal(GameCommandBrowser.Filter(EliteCommandBrowser.Entries(ed.AllCommands, ed.AllRecords), new(Search: "a", AssignmentStatus: status), controller).Select(x => x.Command.Id), c.Ids());
                }
            }
            if (c.Browser is SquadronsAssignmentBrowser sq)
            {
                var backend = new SquadronsProfileBackend(); var catalog = backend.GetCatalog(profile).Commands; var rows = backend.ReadBindings(profile);
                sq.Search("");
                var captured = sq.AllRecords.First(row => row.Native.Input is not null);
                sq.SelectRecord(captured.RecordId);
                Assert.Equal(captured.RecordId, context.State.Selection.AlternativeId);
                Assert.Equal(captured.Native.Input!.Key, context.State.Selection.InputId);
                sq.Search("a");
                foreach (var command in sq.AllCommands.Where(command => rows.Any(row => row.Concept == command.Metadata["squadrons.concept"])).Concat(sq.AllCommands.Where(command => !rows.Any(row => row.Concept == command.Metadata["squadrons.concept"])).Take(1)))
                {
                    var concept = command.Metadata["squadrons.concept"]; var expected = SquadronsAssignmentPlanner.For(backend, profile, concept); var actual = sq.Plan(concept);
                    Assert.Equal(expected.CapturedRecords, actual.CapturedRecords); Assert.Equal(expected.EditableRecords, actual.EditableRecords);
                    Assert.Equal(expected.EditableRecordIds, actual.EditableRecordIds);
                }
                foreach (var status in new[] { "All", "Assigned to selected controller", "Not assigned to selected controller", "Assigned anywhere", "Unassigned everywhere" })
                foreach (var controller in new string?[] { null, "all", "squadrons.joystick.1", "system.2" })
                foreach (var input in new[] { "All types", "Axes", "Buttons and hats", "Unknown / unbound" })
                {
                    sq.SetFilter(new(Source: controller, InputType: input, Status: status));
                    Assert.Equal(SquadronsCommandFilter.Apply(catalog, rows, "All", input, status, "a", controller).Select(x => x.Id), c.Ids());
                }
            }
            if (c.Browser is WarThunderAssignmentBrowser wt)
            {
                foreach (var status in Enum.GetValues<GameCommandAssignmentStatus>())
                foreach (var controller in profile.Devices.Select(x => x.LogicalId).Prepend(null))
                {
                    wt.SetFilter(new(controller, Status: status));
                    Assert.Equal(WarThunderWorkspaceProjection.Filter(wt.AllCommands, wt.AllRecords, controller, "a", null, null, status).Select(x => x.Id), c.Ids());
                }
            }
        }
        #endregion END Public
    }
}


using Heimdall.Application;
using Heimdall.Domain.MultiSim;
using Heimdall.Squadrons;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Checks the same axis presentation contract across throttle, stick, rudder and imported commands.</summary>
    public sealed class SquadronsAxisConsistencyTests
    {
        #region BEGIN Public
        /// <summary>Complete axes have one catalog name everywhere, regardless of native record order or negate flags.</summary>
        [Theory]
        [InlineData("Pitch")]
        [InlineData("Roll")]
        [InlineData("Yaw")]
        [InlineData("Throttle")]
        [InlineData("ImportedCustomAxis")]
        public void Complete_axis_uses_one_name_in_rows_callouts_and_tooltips(string name)
        {
            var backend = new SquadronsProfileBackend();
            var source = Source().Replace("ConceptThrottle", "Concept" + name, StringComparison.Ordinal);
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
            var rows = SquadronsWorkspaceProjection.Rows(profile).Where(x => x.DeviceId == "squadrons.joystick.5").ToArray();
            var expected = backend.GetCatalog(profile).Commands.Single(x => x.Metadata["squadrons.concept"].EndsWith(".Concept" + name, StringComparison.Ordinal)).DisplayName;
            Assert.Equal(2, rows.Length);
            Assert.All(rows, row => Assert.Equal(expected, row.CommandName));
            var diagram = Drawing();
            foreach (var ordered in new[] { rows, rows.Reverse().ToArray() })
            {
                var page = SquadronsDrawingProjection.Diagram("Test", diagram, ordered, "squadrons.joystick.5", "Flight");
                Assert.Equal(expected, Assert.Single(page.Labels["axis"]).Text);
                Assert.Equal(expected, SquadronsDrawingProjection.AssignmentSummary(diagram.Controls[0], ordered, "squadrons.joystick.5", "Flight"));
            }
            Assert.Equal(source, backend.Export(profile).Files[SquadronsProfileBackend.FileName]);
        }

        /// <summary>One remaining native direction stays explicit and is described identically by the callout and tooltip.</summary>
        [Theory]
        [InlineData(4, "Throttle (+ only)")]
        [InlineData(5, "Throttle (- only)")]
        public void Incomplete_axis_is_not_presented_as_a_complete_pair(int removedIndex, string expected)
        {
            var backend = new SquadronsProfileBackend();
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = Source() });
            var partial = backend.ClearCapturedRecord(profile, "IncomStarshipInputConcepts.ConceptThrottle." + removedIndex);
            var rows = SquadronsWorkspaceProjection.Rows(partial);
            var diagram = Drawing();
            var page = SquadronsDrawingProjection.Diagram("Test", diagram, rows, "squadrons.joystick.5", "Flight");
            Assert.Equal(expected, Assert.Single(page.Labels["axis"]).Text);
            Assert.Equal(expected, SquadronsDrawingProjection.AssignmentSummary(diagram.Controls[0], rows, "squadrons.joystick.5", "Flight"));
        }

        /// <summary>Distinct digital throttle actions retain Increase/Decrease semantics instead of being merged into an analog axis.</summary>
        [Fact]
        public void Digital_throttle_actions_keep_their_distinct_names()
        {
            var backend = new SquadronsProfileBackend();
            var source = Source().Replace("ConceptThrottle.4.button 41", "ConceptThrottle.4.button 22", StringComparison.Ordinal)
                .Replace("ConceptThrottle.5.button 40", "ConceptThrottle.5.button 23", StringComparison.Ordinal);
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
            var rows = SquadronsWorkspaceProjection.Rows(profile).Where(x => x.DeviceId == "squadrons.joystick.5").ToArray();
            Assert.Contains(rows, row => row.CommandName == "Throttle Increase" && row.Native.Input!.Key == "JOY_BTN1");
            Assert.Contains(rows, row => row.CommandName == "Throttle Decrease" && row.Native.Input!.Key == "JOY_BTN2");
            Assert.Equal(source, backend.Export(profile).Files[SquadronsProfileBackend.FileName]);
        }

        /// <summary>Normal pickers offer one whole axis per verified physical key, matching the drawing rather than native direction records.</summary>
        [Fact]
        public void Physical_picker_deduplicates_all_axes_and_preserves_buttons_and_hats()
        {
            var verified = SquadronsInputCodec.Choices.Select(x => x.PhysicalKey).Distinct(StringComparer.Ordinal).ToArray();
            var choices = SquadronsInputCodec.PhysicalChoices(verified);
            Assert.Equal(verified.Length, choices.Count);
            Assert.Equal(8, choices.Count(x => x.Type == CommandType.Axis));
            Assert.All(choices.Where(x => x.Type == CommandType.Axis), input =>
            {
                Assert.Equal(1, input.Direction);
                Assert.EndsWith(" axis", input.PhysicalDisplayName, StringComparison.Ordinal);
                Assert.DoesNotContain("positive", input.PhysicalDisplayName, StringComparison.Ordinal);
            });
            Assert.Equal(128, choices.Count(x => x.Type == CommandType.Button));
            Assert.Equal(16, choices.Count(x => x.Type == CommandType.Hat));
            Assert.Empty(SquadronsInputCodec.PhysicalChoices(["UNKNOWN"]));
            Assert.Throws<ArgumentNullException>(() => SquadronsInputCodec.PhysicalChoices(null!));
        }
        #endregion END Public
        #region BEGIN private helpers
        private static string Source() => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Synthetic_profile_synced"));
        private static UiLayerDiagram Drawing() => new("Test", null, "fixture", 100, 50, [], [])
        {
            Controls = [new("axis", "Axis", "JOY_Z", 0, 0, 100, 50, true)]
        };
        #endregion END private helpers
    }
}

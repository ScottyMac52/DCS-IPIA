using Heimdall.Squadrons;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Exercises the captured SOL-R pitch pair and semantic export checks with synthetic controller names.</summary>
    public sealed class SquadronsAxisReversalTests
    {
        private const string Pitch = "IncomStarshipInputConcepts.ConceptPitch.";
        #region BEGIN Public
        /// <summary>Reversal changes only the two directional axis codes and is exactly reversible.</summary>
        [Theory]
        [InlineData("\n")]
        [InlineData("\r\n")]
        public void Pitch_reversal_preserves_every_other_native_byte(string newline)
        {
            var backend = new SquadronsProfileBackend();
            var source = Source(newline);
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
            var reversed = backend.ReverseAxisPair(profile, Pitch + "7", Pitch + "6", "squadrons.joystick.1", "JOY_Y");
            var expected = source.Replace(Pitch + "6.axis 9", Pitch + "6.axis 11", StringComparison.Ordinal)
                .Replace(Pitch + "7.axis 11", Pitch + "7.axis 9", StringComparison.Ordinal);
            Assert.Equal(expected, backend.Export(reversed).Files[SquadronsProfileBackend.FileName]);
            Assert.Equal(expected, backend.ExportReview(reversed).Files[SquadronsProfileBackend.FileName]);
            var restored = backend.ReverseAxisPair(reversed, Pitch + "6", Pitch + "7", "squadrons.joystick.1", "JOY_Y");
            Assert.Equal(source, backend.Export(restored).Files[SquadronsProfileBackend.FileName]);
            Assert.Equal(source, backend.Export(profile).Files[SquadronsProfileBackend.FileName]);
        }
        /// <summary>Reversal never moves an axis to another slot or manufactures missing direction records.</summary>
        [Theory]
        [InlineData("7", "7", "squadrons.joystick.1", "JOY_Y")]
        [InlineData("7", "6", "squadrons.joystick.2", "JOY_Y")]
        [InlineData("7", "6", "squadrons.joystick.1", "JOY_X")]
        [InlineData("7", "8", "squadrons.joystick.1", "JOY_Y")]
        public void Invalid_axis_reversal_retains_the_original(string plus, string minus, string slot, string key)
        {
            var backend = new SquadronsProfileBackend();
            var source = Source("\n");
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
            Assert.Throws<InvalidOperationException>(() => backend.ReverseAxisPair(profile, Pitch + plus, Pitch + minus, slot, key));
            Assert.Equal(source, backend.Export(profile).Files[SquadronsProfileBackend.FileName]);
        }
        /// <summary>A resolved slot is still blocked when its explicit physical link identifies another controller.</summary>
        [Fact]
        public void Throttle_link_to_a_panel_slot_blocks_review_export_without_repairing_names()
        {
            var backend = new SquadronsProfileBackend();
            var source = Source("\n");
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
            var plan = backend.ExportReview(profile, new Dictionary<string, string> { ["squadrons.joystick.2"] = "Viper TQS" });
            Assert.False(plan.Succeeded);
            Assert.Empty(plan.Files);
            var issue = Assert.Single(plan.Diagnostics);
            Assert.Equal("squadrons.export.identity-mismatch", issue.Code);
            Assert.Contains("Panel 1", issue.Message, StringComparison.Ordinal);
            Assert.Contains("Viper TQS", issue.Message, StringComparison.Ordinal);
            Assert.Equal(source, backend.Export(profile).Files[SquadronsProfileBackend.FileName]);
        }
        /// <summary>Exact names and explicit occurrence suffixes retain numeric model names without guessing identities.</summary>
        [Theory]
        [InlineData("F16 MFD 1 1", "F16 MFD 1", true)]
        [InlineData("F16 MFD 1", "F16 MFD 1", true)]
        [InlineData("F16 MFD 1 1", "F16 MFD 2", false)]
        [InlineData("Panel 10", "Panel 1", false)]
        public void Export_identity_check_respects_model_numbers(string native, string reported, bool accepted)
        {
            var backend = new SquadronsProfileBackend();
            var source = Source("\n").Replace("Panel 1\n", native + "\n", StringComparison.Ordinal);
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
            var plan = backend.ExportReview(profile, new Dictionary<string, string> { ["squadrons.joystick.2"] = reported });
            Assert.Equal(accepted, plan.Succeeded);
            if (accepted) Assert.Equal(source, plan.Files[SquadronsProfileBackend.FileName]);
        }
        /// <summary>Duplicated declarations are inspectable but cannot silently pass review export without physical links.</summary>
        [Theory]
        [InlineData("Stick 1")]
        [InlineData("stick 1")]
        public void Duplicate_native_identity_blocks_review_and_preserves_captured_source(string repeatedName)
        {
            var backend = new SquadronsProfileBackend();
            var source = Source("\n").Replace("Panel 1\n", repeatedName + "\n", StringComparison.Ordinal);
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
            var plan = backend.ExportReview(profile);
            Assert.False(plan.Succeeded);
            Assert.Empty(plan.Files);
            var issue = Assert.Single(plan.Diagnostics);
            Assert.Equal("squadrons.export.duplicate-identity", issue.Code);
            Assert.Contains("squadrons.joystick.1", issue.Message, StringComparison.Ordinal);
            Assert.Contains("squadrons.joystick.2", issue.Message, StringComparison.Ordinal);
            Assert.Equal(source, backend.Export(profile).Files[SquadronsProfileBackend.FileName]);
        }
        /// <summary>Distinct occurrence names and multiple empty declarations remain valid review data.</summary>
        [Fact]
        public void Distinct_occurrences_and_empty_slots_do_not_trigger_duplicate_identity()
        {
            var backend = new SquadronsProfileBackend();
            var source = Source("\n").Replace("Panel 1\n", "Stick 2\n", StringComparison.Ordinal)
                .Replace("GstInput.JoystickDevice10 \n", "GstInput.JoystickDevice9 \nGstInput.JoystickDevice10 \n", StringComparison.Ordinal);
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
            var plan = backend.ExportReview(profile);
            Assert.True(plan.Succeeded);
            Assert.Equal(source, plan.Files[SquadronsProfileBackend.FileName]);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static string Source(string newline)
        {
            var lines = new List<string> { "GstInput.JoystickDevice1 Stick 1", "GstInput.JoystickDevice2 Panel 1",
                "GstInput.JoystickDevice10 ", "Future.Setting  preserve exactly", "GstKeyBinding.InputDataVersion 4" };
            foreach (var (index, axis, negate, identifier) in new[] { (6, 9, 1, 1573114849), (7, 11, 0, -743371054) })
            {
                var fields = new Dictionary<string, int> { ["altbutton"] = 0, ["axis"] = axis, ["button"] = 174,
                    ["deviceid"] = 0, ["identifier"] = identifier, ["modifier"] = 0, ["negate"] = negate, ["type"] = 3 };
                lines.AddRange(fields.Select(field => "GstKeyBinding." + Pitch + index + "." + field.Key + " " + field.Value));
            }
            var unrelated = new Dictionary<string, int> { ["altbutton"] = 0, ["axis"] = 26, ["button"] = 24,
                ["deviceid"] = 1, ["identifier"] = -123319776, ["modifier"] = 0, ["negate"] = 0, ["type"] = 3, ["future"] = 99 };
            lines.AddRange(unrelated.Select(field => "GstKeyBinding.IncomStarshipInputConcepts.ConceptTargetSelect.1." + field.Key + " " + field.Value));
            return string.Join(newline, lines) + newline;
        }
        #endregion END private helpers
    }
}

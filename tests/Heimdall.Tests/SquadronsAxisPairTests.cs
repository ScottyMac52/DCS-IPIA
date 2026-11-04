using Heimdall.Domain.MultiSim;
using Heimdall.Squadrons;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Locks the shared native definition of a reversible physical axis independently of WPF.</summary>
    public sealed class SquadronsAxisPairTests
    {
        #region BEGIN Public
        /// <summary>A pair requires opposite editable directions on the same controller, concept and physical axis.</summary>
        [Theory]
        [InlineData("empty")]
        [InlineData("single")]
        [InlineData("extra")]
        [InlineData("two-positive")]
        [InlineData("two-negative")]
        [InlineData("concept")]
        [InlineData("controller")]
        [InlineData("axis")]
        [InlineData("modifier")]
        [InlineData("button")]
        public void Rejects_incomplete_ambiguous_or_read_only_pairs(string failure)
        {
            var plus = Row(1); var minus = Row(-1);
            IEnumerable<SquadronsWorkspaceRow> rows = failure switch
            {
                "empty" => [],
                "single" => [plus],
                "extra" => [plus, minus, plus],
                "two-positive" => [plus, plus],
                "two-negative" => [minus, minus],
                "concept" => [plus, minus with { Native = minus.Native with { Concept = "Other" } }],
                "controller" => [plus, minus with { DeviceId = "squadrons.joystick.2" }],
                "axis" => [plus, Change(minus, "axis", 10)],
                "modifier" => [plus, Change(minus, "modifier", 1)],
                _ => [plus, Change(Change(minus, "axis", 26), "button", 22)]
            };
            Assert.Null(SquadronsAxisPair.For(rows));
        }

        /// <summary>Input order does not change positive and negative native record ownership.</summary>
        [Fact]
        public void Complete_pair_is_resolved_by_direction()
        {
            var plus = Row(1); var minus = Row(-1);
            var pair = SquadronsAxisPair.For([minus, plus]);
            Assert.NotNull(pair);
            Assert.Equal(plus, pair.Positive); Assert.Equal(minus, pair.Negative);
        }

        /// <summary>Unavailable commands produce no saved-pair capability.</summary>
        [Fact]
        public void Unknown_command_has_no_saved_axis_pair()
        {
            var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Synthetic_profile_synced"));
            var profile = new SquadronsProfileBackend().Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
            Assert.Null(SquadronsAxisPair.Find(profile, "missing", "squadrons.joystick.3", "JOY_Y"));
        }
        #endregion END Public
        #region BEGIN private helpers
        private static SquadronsWorkspaceRow Row(int direction)
        {
            var (axis, button) = SquadronsInputCodec.Encode(new("JOY_Y", CommandType.Axis, direction));
            var fields = new Dictionary<string, int>
            {
                ["type"] = 3, ["deviceid"] = 0, ["axis"] = axis, ["button"] = button,
                ["identifier"] = 123, ["negate"] = 0, ["modifier"] = 0, ["altbutton"] = 0
            };
            return new(new("Throttle", direction > 0 ? 0 : 1, fields), "Throttle", "Flight", "squadrons.joystick.1", "Stick");
        }
        private static SquadronsWorkspaceRow Change(SquadronsWorkspaceRow row, string field, int value)
        {
            var fields = row.Native.Fields.ToDictionary(item => item.Key, item => item.Value);
            fields[field] = value;
            return row with { Native = row.Native with { Fields = fields } };
        }
        #endregion END private helpers
    }
}

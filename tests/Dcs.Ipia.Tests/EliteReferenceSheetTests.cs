using System.Text.Json;
using Dcs.Ipia.EliteDangerous;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Verifies device/section scope and native behaviour labels without changing assignments.</summary>
    public sealed class EliteReferenceSheetTests
    {
        #region BEGIN Public
        /// <summary>Selected-controller tables include modifiers, holds, toggles and axis tuning, preserving native evidence.</summary>
        [Fact]
        public void Sheet_projection_is_complete_scoped_and_immutable()
        {
            var backend = new EliteDangerousProfileBackend();
            var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "EliteDangerous", "Synthetic.4.2.binds"));
            var profile = backend.Import("controls", new Dictionary<string, string> { ["Synthetic.4.2.binds"] = source });
            var before = JsonSerializer.Serialize(profile);
            var catalog = backend.GetCatalog(profile);
            var stick = profile.Devices.Single(x => x.Identity!.SimSpecificIds["ed.device"] == "ExampleStick");
            var toggle = profile.Metadata.Keys.Single(); profile.Metadata[toggle] = "1";
            var sheet = EliteReferenceSheetBuilder.Build(profile, catalog, stick.LogicalId, "Stick", "Ship", true);
            Assert.Equal(3, sheet.Rows.Count);
            Assert.Contains(sheet.Rows, x => x.Details.Contains("Hold", StringComparison.Ordinal) && x.Details.Contains("Toggle", StringComparison.Ordinal));
            Assert.Contains(sheet.Rows, x => x.Input.Contains("Key_LeftShift", StringComparison.Ordinal) && x.Input.Contains("Keyboard", StringComparison.Ordinal));
            Assert.Contains(sheet.Rows, x => x.Details.Contains("Inverted", StringComparison.Ordinal) && x.Details.Contains("Deadzone", StringComparison.Ordinal));
            Assert.Empty(EliteReferenceSheetBuilder.Build(profile, catalog, stick.LogicalId, "Stick", "On Foot", true).Rows);
            Assert.Equal(5, EliteReferenceSheetBuilder.Build(profile, catalog, stick.LogicalId, "Stick", "All", false).Rows.Count);
            profile.Metadata[toggle] = "0";
            Assert.Equal(before, JsonSerializer.Serialize(profile));
            Assert.Equal(source, backend.Export(profile).Files.Values.Single());
            Assert.Throws<InvalidOperationException>(() => EliteReferenceSheetBuilder.Build(profile, catalog, "missing", "Stick", "All", true));
            Assert.Throws<ArgumentException>(() => EliteReferenceSheetBuilder.Build(profile, catalog, stick.LogicalId, "Stick", "Unknown", true));
        }
        #endregion END Public
    }
}
